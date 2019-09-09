using System;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.BuildInfo.Features.TurretInfo
{
    /// <summary>
    /// Re-usable turret ammo tracking for determining when reload is about to happen.
    /// </summary>
    public class TurretAmmoTracker
    {
        public IMyLargeTurretBase Turret;
        private IMyGunObject<MyGunBase> gun;
        private MyWeaponDefinition weaponDef;

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

        public TurretAmmoTracker()
        {
        }

        public bool Init(IMyLargeTurretBase turret)
        {
            Turret = turret;
            gun = (IMyGunObject<MyGunBase>)turret;
            var blockDef = (MyLargeTurretBaseDefinition)turret.SlimBlock.BlockDefinition;
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
            Turret = null;
            gun = null;
            weaponDef = null;
        }

        public bool Update()
        {
            if(Turret.MarkedForClose)
                return false;

            if(gun.GunBase.LastShootTime.Ticks > lastShotTime)
            {
                lastShotTime = gun.GunBase.LastShootTime.Ticks;

                if(gun.GunBase.IsAmmoProjectile)
                {
                    if(--projectilesUtilReload == 0)
                    {
                        projectilesUtilReload = projectileShotsInBurst;
                    }
                }
                else if(gun.GunBase.IsAmmoMissile)
                {
                    if(--missilesUntilReload == 0)
                    {
                        missilesUntilReload = missileShotsInBurst;
                    }
                }
            }

            return true;
        }
    }
}