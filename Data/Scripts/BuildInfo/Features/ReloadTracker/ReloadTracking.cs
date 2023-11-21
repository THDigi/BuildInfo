using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ReloadTracker
{
    public class ReloadTracking : ModComponent
    {
        public readonly Dictionary<long, TrackedWeapon> WeaponLookup = new Dictionary<long, TrackedWeapon>();

        readonly List<TrackedWeapon> WeaponForUpdate = new List<TrackedWeapon>();
        readonly MyConcurrentPool<TrackedWeapon> WeaponPool = new MyConcurrentPool<TrackedWeapon>();

        public ReloadTracking(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;

            BlockMonitor.CallbackDelegate action = new BlockMonitor.CallbackDelegate(WeaponBlockAdded);
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
            WeaponForUpdate.Clear();
            WeaponLookup.Clear();
            WeaponPool.Clean();
        }

        void WeaponBlockAdded(IMySlimBlock block)
        {
            if(block.CubeGrid?.Physics == null)
                return; // no tracking for ghost grids

            IMyUserControllableGun gunBlock = block.FatBlock as IMyUserControllableGun;
            if(gunBlock == null)
                return; // ignore weirdness

            if(Main.CoreSystemsAPIHandler.Weapons.ContainsKey(block.BlockDefinition.Id))
                return; // no tracking of weaponcore blocks

            if(WeaponLookup.ContainsKey(gunBlock.EntityId))
                return; // ignore grid merge/split if gun is already tracked

            TrackedWeapon weapon = WeaponPool.Get();
            if(!weapon.Init(gunBlock))
            {
                weapon.Clear();
                WeaponPool.Return(weapon);
                return;
            }

            WeaponForUpdate.Add(weapon);
            WeaponLookup.Add(gunBlock.EntityId, weapon);
        }

        public override void UpdateAfterSim(int tick)
        {
            for(int i = (WeaponForUpdate.Count - 1); i >= 0; --i)
            {
                TrackedWeapon tw = WeaponForUpdate[i];

                if(tw.Block.MarkedForClose)
                {
                    WeaponForUpdate.RemoveAtFast(i);
                    WeaponLookup.Remove(tw.Block.EntityId);

                    tw.Clear();
                    WeaponPool.Return(tw);
                    continue;
                }

                MyGunBase gunbase = tw.Gun.GunBase;
                long lastShotTime = gunbase.LastShootTime.Ticks;
                if(tw.LastShotTime < lastShotTime)
                {
                    tw.LastShotTime = lastShotTime;

                    if(--tw.ShotsUntilReload == 0)
                    {
                        // NOTE: a bug in game code allows you to go over max shots by switching to other mag type (projectile<>missile).
                        if(gunbase.IsAmmoProjectile)
                            tw.ShotsUntilReload = tw.ProjectileShotsInBurst;
                        else if(gunbase.IsAmmoMissile)
                            tw.ShotsUntilReload = tw.MissileShotsInBurst;

                        tw.ReloadUntilTick = tick + tw.ReloadDurationTicks;
                    }
                }
            }
        }
    }
}