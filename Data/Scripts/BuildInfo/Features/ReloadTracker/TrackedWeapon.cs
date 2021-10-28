using System;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.ReloadTracker
{
    /// <summary>
    /// Re-usable turret ammo tracking for determining when reload is about to happen.
    /// </summary>
    public class TrackedWeapon
    {
        public IMyUserControllableGun Block { get; private set; }
        public IMyGunObject<MyGunBase> Gun { get; private set; }
        public MyWeaponDefinition WeaponDef { get; private set; }
        public int ReloadUntilTick { get; private set; }
        public int ReloadDurationTicks { get; private set; }

        /// <summary>
        /// How many rounds currently until gun has to reload.
        /// <para>NOTE: Does not indicate how much ammo is loaded in the gun!</para>
        /// </summary>
        public int ShotsUntilReload { get; private set; }

        /// <summary>
        /// How many rounds max the gun can shoot until reload.
        /// </summary>
        public int InternalMagazineCapacity
        {
            get
            {
                if(Gun.GunBase.IsAmmoProjectile)
                    return projectileShotsInBurst;

                if(Gun.GunBase.IsAmmoMissile)
                    return missileShotsInBurst;

                throw new Exception("Gun uses neither projectile nor missile?!");
            }
        }

        private int projectileShotsInBurst;
        private int missileShotsInBurst;

        private long lastShotTime;

        public TrackedWeapon()
        {
        }

        public bool Init(IMyUserControllableGun gunBlock)
        {
            Block = gunBlock;
            Gun = gunBlock as IMyGunObject<MyGunBase>;
            MyWeaponBlockDefinition blockDef = gunBlock.SlimBlock.BlockDefinition as MyWeaponBlockDefinition;
            if(Gun == null || blockDef == null)
                return false;

            MyWeaponDefinition wpDef;
            if(!MyDefinitionManager.Static.TryGetWeaponDefinition(blockDef.WeaponDefinitionId, out wpDef))
                return false;

            WeaponDef = wpDef;

            if(WeaponDef.ReloadTime == 0)
                return false;

            if(WeaponDef.HasProjectileAmmoDefined)
            {
                projectileShotsInBurst = WeaponDef.WeaponAmmoDatas[(int)MyAmmoType.HighSpeed].ShotsInBurst;
                ShotsUntilReload = projectileShotsInBurst;
            }

            if(WeaponDef.HasMissileAmmoDefined)
            {
                missileShotsInBurst = WeaponDef.WeaponAmmoDatas[(int)MyAmmoType.Missile].ShotsInBurst;
                ShotsUntilReload = missileShotsInBurst;
            }

            if(projectileShotsInBurst == 0 && missileShotsInBurst == 0)
                return false;

            ReloadDurationTicks = (int)(Constants.TicksPerSecond * (WeaponDef.ReloadTime / 1000f));
            lastShotTime = Gun.GunBase.LastShootTime.Ticks; // needed because it starts non-0 as it is serialized to save.
            return true;
        }

        /// <summary>
        /// Called by pool when returned to it, needs to clear all references.
        /// </summary>
        public void Clear()
        {
            Block = null;
            Gun = null;
            WeaponDef = null;
            ReloadUntilTick = 0;
            ReloadDurationTicks = 0;
            ShotsUntilReload = 0;
            projectileShotsInBurst = 0;
            missileShotsInBurst = 0;
            lastShotTime = 0;
        }

        public bool Update(int tick)
        {
            if(Block.MarkedForClose)
                return false;

            if(ReloadUntilTick != 0 && ReloadUntilTick < tick)
            {
                ReloadUntilTick = 0;
            }

            if(Gun.GunBase.LastShootTime.Ticks > lastShotTime)
            {
                lastShotTime = Gun.GunBase.LastShootTime.Ticks;

                if(--ShotsUntilReload == 0)
                {
                    // NOTE: a bug in here and game code allows you to go over max shots by switching to other mag type (projectile<>missile).
                    if(Gun.GunBase.IsAmmoProjectile)
                        ShotsUntilReload = projectileShotsInBurst;
                    else if(Gun.GunBase.IsAmmoMissile)
                        ShotsUntilReload = missileShotsInBurst;

                    ReloadUntilTick = tick + ReloadDurationTicks;
                }
            }

            return true;
        }
    }
}