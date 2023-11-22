using System;
using Sandbox.Definitions;
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
        public int ReloadDurationTicks { get; private set; }

        /// <summary>
        /// When the weapon finishes reloading.
        /// <para>Compare with Main.Tick to determine if it's still reloading as it does not get reset to 0.</para>
        /// </summary>
        public int ReloadUntilTick { get; internal set; }

        /// <summary>
        /// How many rounds currently until gun has to reload.
        /// <para>NOTE: Does not indicate how much ammo is loaded in the gun!</para>
        /// </summary>
        public int ShotsUntilReload { get; internal set; }

        /// <summary>
        /// How many rounds max the gun can shoot until reload.
        /// </summary>
        public int InternalMagazineCapacity
        {
            get
            {
                if(Gun.GunBase.IsAmmoProjectile)
                    return ProjectileShotsInBurst;

                if(Gun.GunBase.IsAmmoMissile)
                    return MissileShotsInBurst;

                throw new Exception("Gun uses neither projectile nor missile?!");
            }
        }

        public int ProjectileShotsInBurst { get; private set; }
        public int MissileShotsInBurst { get; private set; }

        /// <summary>
        /// Used to detect when the gun shoots; in DateTime ticks.
        /// </summary>
        public long LastShotTime { get; internal set; }

        public TrackedWeapon()
        {
        }

        public bool Init(IMyUserControllableGun gunBlock)
        {
            Block = gunBlock;
            Gun = gunBlock as IMyGunObject<MyGunBase>;
            MyWeaponBlockDefinition blockDef = gunBlock?.SlimBlock?.BlockDefinition as MyWeaponBlockDefinition;
            if(Gun == null || blockDef == null)
                return false;

            MyWeaponDefinition wpDef;
            if(!MyDefinitionManager.Static.TryGetWeaponDefinition(blockDef.WeaponDefinitionId, out wpDef))
                return false;

            WeaponDef = wpDef;

            if(WeaponDef.ReloadTime == 0 || !WeaponDef.HasAmmoMagazines())
                return false;

            if(WeaponDef.HasProjectileAmmoDefined)
            {
                ProjectileShotsInBurst = WeaponDef.WeaponAmmoDatas[(int)MyAmmoType.HighSpeed].ShotsInBurst;
                ShotsUntilReload = ProjectileShotsInBurst;
            }

            if(WeaponDef.HasMissileAmmoDefined)
            {
                MissileShotsInBurst = WeaponDef.WeaponAmmoDatas[(int)MyAmmoType.Missile].ShotsInBurst;
                ShotsUntilReload = MissileShotsInBurst;
            }

            if(ProjectileShotsInBurst == 0 && MissileShotsInBurst == 0)
                return false;

            ReloadDurationTicks = (int)(Constants.TicksPerSecond * (WeaponDef.ReloadTime / 1000f));
            LastShotTime = Gun.GunBase.LastShootTime.Ticks; // needed because it starts non-0 as it is serialized to save.
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
            ProjectileShotsInBurst = 0;
            MissileShotsInBurst = 0;
            LastShotTime = 0;
        }
    }
}