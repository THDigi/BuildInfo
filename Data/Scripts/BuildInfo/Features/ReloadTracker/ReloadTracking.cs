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

        public readonly Dictionary<long, Weapon> WeaponLookup = new Dictionary<long, Weapon>();

        private readonly List<Weapon> weaponForUpdate = new List<Weapon>();
        private readonly MyConcurrentPool<Weapon> weaponPool = new MyConcurrentPool<Weapon>();

        public ReloadTracking(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            var action = new BlockMonitor.CallbackDelegate(WeaponBlockAdded);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_LargeGatlingTurret), action);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_LargeMissileTurret), action);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_SmallMissileLauncher), action);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_SmallMissileLauncherReload), action);

            // HACK: these block types don't support reloading
            // Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_InteriorTurret), action);
            // Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_SmallGatlingGun), action);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            weaponForUpdate.Clear();
            WeaponLookup.Clear();
            weaponPool.Clean();
        }

        private void WeaponBlockAdded(IMySlimBlock block)
        {
            if(block.CubeGrid?.Physics == null)
                return; // no tracking for ghost grids

            var gunBlock = block.FatBlock as IMyUserControllableGun;
            if(gunBlock == null)
                return; // ignore weirdness

            if(Main.WeaponCoreAPIHandler.Weapons.ContainsKey(block.BlockDefinition.Id))
                return; // no tracking of weaponcore blocks

            if(WeaponLookup.ContainsKey(gunBlock.EntityId))
                return; // ignore grid merge/split if gun is already tracked

            Weapon weapon = weaponPool.Get();
            if(!weapon.Init(gunBlock))
            {
                weapon.Clear();
                weaponPool.Return(weapon);
                return;
            }

            weaponForUpdate.Add(weapon);
            WeaponLookup.Add(gunBlock.EntityId, weapon);
        }

        public override void UpdateAfterSim(int tick)
        {
            for(int i = (weaponForUpdate.Count - 1); i >= 0; --i)
            {
                Weapon weapon = weaponForUpdate[i];
                if(!weapon.Update(tick))
                {
                    weaponForUpdate.RemoveAtFast(i);
                    WeaponLookup.Remove(weapon.Block.EntityId);

                    weapon.Clear();
                    weaponPool.Return(weapon);
                    continue;
                }
            }
        }
    }
}