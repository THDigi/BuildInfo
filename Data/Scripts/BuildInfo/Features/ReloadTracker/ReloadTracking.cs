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

        private readonly List<Weapon> weapons = new List<Weapon>();
        private readonly Dictionary<long, Weapon> weaponLookup = new Dictionary<long, Weapon>();
        private readonly MyConcurrentPool<Weapon> weaponPool = new MyConcurrentPool<Weapon>(activator: () => new Weapon(), clear: (i) => i.Clear());

        public ReloadTracking(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            var action = new BlockMonitor.CallbackDelegate(WeaponBlockAdded);
            BlockMonitor.MonitorType(typeof(MyObjectBuilder_LargeGatlingTurret), action);
            BlockMonitor.MonitorType(typeof(MyObjectBuilder_LargeMissileTurret), action);
            BlockMonitor.MonitorType(typeof(MyObjectBuilder_InteriorTurret), action);
            BlockMonitor.MonitorType(typeof(MyObjectBuilder_SmallGatlingGun), action);
            BlockMonitor.MonitorType(typeof(MyObjectBuilder_SmallMissileLauncher), action);
            BlockMonitor.MonitorType(typeof(MyObjectBuilder_SmallMissileLauncherReload), action);
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
            weapons.Clear();
            weaponLookup.Clear();
            weaponPool.Clean();
        }

        public Weapon GetWeaponInfo(IMyCubeBlock block)
        {
            return weaponLookup.GetValueOrDefault(block.EntityId, null);
        }

        private void WeaponBlockAdded(IMySlimBlock block)
        {
            if(block.CubeGrid?.Physics == null)
                return; // no tracking for ghost grids

            if(WeaponCoreAPIHandler.IsBlockWeapon(block.BlockDefinition.Id))
                return; // no tracking of weaponcore blocks

            var gunBlock = block.FatBlock as IMyUserControllableGun;
            if(gunBlock != null)
            {
                var weapon = weaponPool.Get();
                if(!weapon.Init(gunBlock))
                {
                    weaponPool.Return(weapon);
                    return;
                }

                weapons.Add(weapon);
                weaponLookup.Add(gunBlock.EntityId, weapon);
            }
        }

        protected override void UpdateAfterSim(int tick)
        {
            for(int i = (weapons.Count - 1); i >= 0; --i)
            {
                var weapon = weapons[i];
                if(!weapon.Update(tick))
                {
                    weapons.RemoveAtFast(i);
                    weaponLookup.Remove(weapon.Block.EntityId);
                    weaponPool.Return(weapon);
                    continue;
                }
            }
        }
    }
}