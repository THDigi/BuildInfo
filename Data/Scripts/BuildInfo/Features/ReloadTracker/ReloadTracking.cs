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
        private readonly MyConcurrentPool<Weapon> weaponPool = new MyConcurrentPool<Weapon>();

        public ReloadTracking(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            var action = new BlockMonitor.CallbackDelegate(WeaponBlockAdded);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_LargeGatlingTurret), action);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_LargeMissileTurret), action);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_InteriorTurret), action);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_SmallGatlingGun), action);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_SmallMissileLauncher), action);
            Main.BlockMonitor.MonitorType(typeof(MyObjectBuilder_SmallMissileLauncherReload), action);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
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

            if(Main.WeaponCoreAPIHandler.IsBlockWeapon(block.BlockDefinition.Id))
                return; // no tracking of weaponcore blocks

            var gunBlock = block.FatBlock as IMyUserControllableGun;
            if(gunBlock == null)
                return;

            if(weaponLookup.ContainsKey(gunBlock.EntityId))
                return; // ignore grid merge/split if gun is already tracked

            var weapon = weaponPool.Get();
            if(!weapon.Init(gunBlock))
            {
                weapon.Clear();
                weaponPool.Return(weapon);
                return;
            }

            weapons.Add(weapon);
            weaponLookup.Add(gunBlock.EntityId, weapon);
        }

        public override void UpdateAfterSim(int tick)
        {
            for(int i = (weapons.Count - 1); i >= 0; --i)
            {
                var weapon = weapons[i];
                if(!weapon.Update(tick))
                {
                    weapons.RemoveAtFast(i);
                    weaponLookup.Remove(weapon.Block.EntityId);

                    weapon.Clear();
                    weaponPool.Return(weapon);
                    continue;
                }
            }
        }
    }
}