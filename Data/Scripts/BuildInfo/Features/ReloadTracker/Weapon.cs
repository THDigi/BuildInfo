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

        public int ReloadUntilTick { get; private set; }
        public bool Reloading { get; private set; }

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
            gun = gunBlock as IMyGunObject<MyGunBase>;
            var blockDef = gunBlock.SlimBlock.BlockDefinition as MyWeaponBlockDefinition;
            if(gun == null || blockDef == null)
                return false;

            if(!MyDefinitionManager.Static.TryGetWeaponDefinition(blockDef.WeaponDefinitionId, out weaponDef))
                return false;

            if(weaponDef.ReloadTime == 0)
                return false;

            if(weaponDef.HasProjectileAmmoDefined)
            {
                projectileShotsInBurst = weaponDef.WeaponAmmoDatas[(int)MyAmmoType.HighSpeed].ShotsInBurst;
                projectilesUtilReload = projectileShotsInBurst;
            }

            if(weaponDef.HasMissileAmmoDefined)
            {
                missileShotsInBurst = weaponDef.WeaponAmmoDatas[(int)MyAmmoType.Missile].ShotsInBurst;
                missilesUntilReload = missileShotsInBurst;
            }

            if(projectileShotsInBurst == 0 && missileShotsInBurst == 0)
                return false;

            //int ammo = gun.GunBase.GetTotalAmmunitionAmount();
            //if(ammo == 0)
            //{
            //    var gunUser = (IMyGunBaseUser)gun;
            //    ammo = (int)gunUser.AmmoInventory.GetItemAmount(gun.GunBase.CurrentAmmoMagazineId) * gun.GunBase.CurrentAmmoMagazineDefinition.Capacity;
            //}

            //Log.Info($"{GetType().Name} :: {gunBlock.CustomName}: totalAmmo={gun.GunBase.GetTotalAmmunitionAmount()}; inventoryAmmo={ammo}; CurrentAmmo={gun.GunBase.CurrentAmmo}");

            //if(gun.GunBase.CurrentAmmoDefinition.AmmoType == MyAmmoType.HighSpeed)
            //{
            //    projectilesUtilReload = Math.Min(ammo, projectileShotsInBurst);
            //}
            //else if(gun.GunBase.CurrentAmmoDefinition.AmmoType == MyAmmoType.Missile)
            //{
            //    missilesUntilReload = Math.Min(ammo, missileShotsInBurst);
            //}

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
            Reloading = false;
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
            {
                ReloadUntilTick = 0;
                Reloading = false;
            }

            if(gun.GunBase.LastShootTime.Ticks > lastShotTime)
            {
                bool reloading = false;

                lastShotTime = gun.GunBase.LastShootTime.Ticks;

                if(gun.GunBase.IsAmmoProjectile)
                {
                    if(--projectilesUtilReload == 0)
                    {
                        projectilesUtilReload = Math.Min(gun.GunBase.GetTotalAmmunitionAmount(), projectileShotsInBurst);
                        reloading = true;
                    }
                }
                else if(gun.GunBase.IsAmmoMissile)
                {
                    if(--missilesUntilReload == 0)
                    {
                        missilesUntilReload = Math.Min(gun.GunBase.GetTotalAmmunitionAmount(), missileShotsInBurst);
                        reloading = true;
                    }
                }

                if(reloading)
                {
                    ReloadUntilTick = tick + (int)((float)Constants.TICKS_PER_SECOND * (weaponDef.ReloadTime / 1000f));
                    Reloading = true;
                }
            }

            return true;
        }
    }
}