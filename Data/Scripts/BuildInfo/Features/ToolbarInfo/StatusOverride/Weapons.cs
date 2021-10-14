using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class Weapons : StatusOverrideBase
    {
        public Weapons(ToolbarStatusProcessor processor) : base(processor)
        {
            RegisterFor(typeof(MyObjectBuilder_SmallGatlingGun));
            RegisterFor(typeof(MyObjectBuilder_SmallMissileLauncher));
            RegisterFor(typeof(MyObjectBuilder_SmallMissileLauncherReload));
            RegisterFor(typeof(MyObjectBuilder_InteriorTurret));
            RegisterFor(typeof(MyObjectBuilder_LargeGatlingTurret));
            RegisterFor(typeof(MyObjectBuilder_LargeMissileTurret));
        }

        void RegisterFor(MyObjectBuilderType type)
        {
            Processor.AddStatus(type, Shoot, "ShootOnce", "Shoot", "Shoot_On", "Shoot_Off");

            Processor.AddGroupStatus(type, GroupShoot, "ShootOnce", "Shoot", "Shoot_On", "Shoot_Off");
        }

        bool Shoot(StringBuilder sb, ToolbarItem item)
        {
            if(BuildInfoMod.Instance.WeaponCoreAPIHandler.Weapons.ContainsKey(item.Block.BlockDefinition))
                return false;

            if(!Processor.AppendSingleStats(sb, item.Block))
            {
                if(item.ActionId != "ShootOnce")
                {
                    bool shoot = item.Block.GetValue<bool>("Shoot");
                    sb.Append(shoot ? "Fire" : "No fire").Append('\n');
                }
            }

            IMyGunObject<MyGunBase> gun = (IMyGunObject<MyGunBase>)item.Block;
            int ammo = gun.GunBase.GetTotalAmmunitionAmount();
            if(ammo <= gun.GunBase.CurrentAmmo)
                ammo = gun.GunBase.CurrentAmmo + gun.GunBase.GetInventoryAmmoMagazinesCount() * gun.GunBase.CurrentAmmoMagazineDefinition.Capacity;

            if(ammo == 0)
            {
                sb.Append("NoAmmo"); // just about fits
                return true;
            }

            ReloadTracker.TrackedWeapon weaponInfo = BuildInfoMod.Instance.ReloadTracking.WeaponLookup.GetValueOrDefault(item.Block.EntityId, null);
            if(weaponInfo != null && weaponInfo.ReloadUntilTick > 0)
            {
                float seconds = (weaponInfo.ReloadUntilTick - BuildInfoMod.Instance.Tick) / (float)Constants.TICKS_PER_SECOND;
                sb.Append("Rld:").TimeFormat(seconds);
            }
            else
            {
                sb.NumberCapped(ammo, 5);
            }

            return true;
        }

        bool GroupShoot(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyUserControllableGun>())
                return false;

            bool isShootOnce = (groupToolbarItem.ActionId == "ShootOnce");

            int broken = 0;
            int off = 0;
            int total = 0;
            int reloading = 0;
            int noAmmo = 0;
            int firing = 0;

            float minReloadTimeLeft = float.MaxValue;

            int leastAmmo = int.MaxValue;
            int mostAmmo = 0;

            Dictionary<long, ReloadTracker.TrackedWeapon> weaponLookup = BuildInfoMod.Instance.ReloadTracking.WeaponLookup;

            foreach(IMyUserControllableGun gunBlock in groupData.Blocks)
            {
                if(BuildInfoMod.Instance.WeaponCoreAPIHandler.Weapons.ContainsKey(gunBlock.BlockDefinition))
                    continue;

                if(!gunBlock.IsFunctional)
                {
                    broken++;
                    continue; // these should not contribute stats
                }

                if(!gunBlock.Enabled)
                {
                    off++;
                    continue; // these should not contribute stats
                }

                if(!isShootOnce && gunBlock.GetValue<bool>("Shoot"))
                    firing++;

                IMyGunObject<MyGunBase> gun = (IMyGunObject<MyGunBase>)gunBlock;
                int ammo = gun.GunBase.GetTotalAmmunitionAmount();
                if(ammo <= gun.GunBase.CurrentAmmo) // can be 0 when was not shot or be only loaded ammo, neither are great
                    ammo = gun.GunBase.CurrentAmmo + gun.GunBase.GetInventoryAmmoMagazinesCount() * gun.GunBase.CurrentAmmoMagazineDefinition.Capacity;

                if(ammo == 0)
                {
                    noAmmo++;
                    //leastAmmo = 0;
                }
                else
                {
                    ReloadTracker.TrackedWeapon weaponInfo = weaponLookup.GetValueOrDefault(gunBlock.EntityId, null);
                    if(weaponInfo != null && weaponInfo.ReloadUntilTick > 0)
                    {
                        reloading++;

                        float seconds = (weaponInfo.ReloadUntilTick - BuildInfoMod.Instance.Tick) / (float)Constants.TICKS_PER_SECOND;
                        minReloadTimeLeft = Math.Min(minReloadTimeLeft, seconds);
                    }
                    else
                    {
                        leastAmmo = Math.Min(leastAmmo, ammo);
                        mostAmmo = Math.Max(mostAmmo, ammo);
                    }
                }

                total++;
            }

            if(total == 0)
                return false; // no supported weapons

            if(!Processor.AppendGroupStats(sb, broken, off) && !isShootOnce)
            {
                if(firing == total)
                    sb.Append("All fire");
                else
                    sb.NumberCapped(firing, 2).Append(" fire");

                sb.Append('\n');
            }

            if(noAmmo == total)
            {
                // TODO: what if the weapon has multiple magazines and it's only out of this type but has other types in inv?
                sb.Append("NoAmmo"); // just about fits
                return true;
            }

            if(reloading == total)
            {
                sb.Append("Rld:").TimeFormat(minReloadTimeLeft);
                return true;
            }

            if(reloading > 0)
                sb.Append("Rld:").TimeFormat(minReloadTimeLeft).Append('\n');

            if(mostAmmo > leastAmmo)
                sb.Append(Math.Min(leastAmmo, 99999)).Append('+');
            else
                sb.NumberCapped(mostAmmo, 5);

            return true;
        }
    }
}
