using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ReloadTracker
{
    public class ReloadTracking : ModComponent
    {
        const int SKIP_TICKS = 6; // ticks between text updates, min value 1.

        private List<Weapon> turretTrackers = new List<Weapon>();
        private MyConcurrentPool<Weapon> trackerPool = new MyConcurrentPool<Weapon>(activator: () => new Weapon(), clear: (i) => i.Clear());

        public ReloadTracking(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            BlockMonitor.CallbackDelegate action = TurretAdded;
            BlockMonitor.MonitorType(typeof(MyObjectBuilder_LargeGatlingTurret), action);
            BlockMonitor.MonitorType(typeof(MyObjectBuilder_LargeMissileTurret), action);
            BlockMonitor.MonitorType(typeof(MyObjectBuilder_InteriorTurret), action);
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
            turretTrackers.Clear();
            trackerPool.Clean();
        }

        public Weapon GetTrackerForTurret(IMyLargeTurretBase turret)
        {
            for(int i = (turretTrackers.Count - 1); i >= 0; --i)
            {
                var turretTracker = turretTrackers[i];

                if(turretTracker.Turret == turret)
                    return turretTracker;
            }

            return null;
        }

        private void TurretAdded(IMySlimBlock block)
        {
            if(block.CubeGrid?.Physics == null)
                return; // no tracking for ghost grids

            var turret = block.FatBlock as IMyLargeTurretBase;

            if(turret != null)
            {
                var tracker = trackerPool.Get();

                if(!tracker.Init(turret))
                {
                    trackerPool.Return(tracker);
                    return;
                }

                turretTrackers.Add(tracker);
            }
        }

        protected override void UpdateAfterSim(int tick)
        {
            for(int i = (turretTrackers.Count - 1); i >= 0; --i)
            {
                var tracker = turretTrackers[i];

                if(!tracker.Update())
                {
                    turretTrackers.RemoveAtFast(i);
                    trackerPool.Return(tracker);
                    continue;
                }
            }
        }
    }
}