using System;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.BuildInfo.Features.ReloadTracker
{
    // DEBUG FIXME: can't tell when weapon is out of ammo
    // and feeding just 1 magazine says it'll have more than it actually does.

    /// <summary>
    /// Re-usable turret ammo tracking for determining when reload is about to happen.
    /// </summary>
    public class Weapon
    {
        public IMyUserControllableGun Block;
        private IMyGunObject<MyGunBase> gun;
        private MyWeaponDefinition weaponDef;

        public int ReloadUntilTick;
        public bool Reloading => ReloadUntilTick > 0;

        public int Ammo
        {
            get
            {
                if(gun.GunBase.IsAmmoProjectile)
                    return projectilesUtilReload;

                if(gun.GunBase.IsAmmoMissile)
                    return missilesUntilReload;

                throw new Exception("Gun uses neither projectile nor missile?!");
            }
        }

        public int AmmoMax
        {
            get
            {
                if(gun.GunBase.IsAmmoProjectile)
                    return projectileShotsInBurst;

                if(gun.GunBase.IsAmmoMissile)
                    return missileShotsInBurst;

                throw new Exception("Gun uses neither projectile nor missile?!");
            }
        }

        private int projectilesUtilReload;
        private int projectileShotsInBurst;

        private int missilesUntilReload;
        private int missileShotsInBurst;

        private long lastShotTime;

        public Weapon()
        {
        }

        public bool Init(IMyUserControllableGun gunBlock)
        {
            Block = gunBlock;
            gun = (IMyGunObject<MyGunBase>)gunBlock;
            var blockDef = (MyWeaponBlockDefinition)gunBlock.SlimBlock.BlockDefinition;
            weaponDef = MyDefinitionManager.Static.GetWeaponDefinition(blockDef.WeaponDefinitionId);

            if(weaponDef.ReloadTime == 0)
                return false;

            if(weaponDef.HasProjectileAmmoDefined)
                projectilesUtilReload = projectileShotsInBurst = weaponDef.WeaponAmmoDatas[(int)MyAmmoType.HighSpeed].ShotsInBurst;

            if(weaponDef.HasMissileAmmoDefined)
                missilesUntilReload = missileShotsInBurst = weaponDef.WeaponAmmoDatas[(int)MyAmmoType.Missile].ShotsInBurst;

            if(projectilesUtilReload == 0 && missilesUntilReload == 0)
                return false;

            lastShotTime = gun.GunBase.LastShootTime.Ticks; // needed because it starts non-0 as it is serialized to save.
            return true;
        }

        /// <summary>
        /// Called by pool when returned to it, needs to clear all references.
        /// </summary>
        public void Clear()
        {
            Block = null;
            gun = null;
            weaponDef = null;
            ReloadUntilTick = 0;
            projectilesUtilReload = 0;
            projectileShotsInBurst = 0;
            missilesUntilReload = 0;
            missileShotsInBurst = 0;
        }

        public bool Update(int tick)
        {
            if(Block.MarkedForClose)
                return false;

            if(ReloadUntilTick != 0 && ReloadUntilTick < tick)
                ReloadUntilTick = 0;

            if(gun.GunBase.LastShootTime.Ticks > lastShotTime)
            {
                bool reloading = false;

                lastShotTime = gun.GunBase.LastShootTime.Ticks;

                if(gun.GunBase.IsAmmoProjectile)
                {
                    if(--projectilesUtilReload == 0)
                    {
                        projectilesUtilReload = projectileShotsInBurst;
                        reloading = true;
                    }
                }
                else if(gun.GunBase.IsAmmoMissile)
                {
                    if(--missilesUntilReload == 0)
                    {
                        missilesUntilReload = missileShotsInBurst;
                        reloading = true;
                    }
                }

                if(reloading)
                    ReloadUntilTick = tick + (int)((float)Constants.TICKS_PER_SECOND * (weaponDef.ReloadTime / 1000f));
            }

            return true;
        }
    }
}