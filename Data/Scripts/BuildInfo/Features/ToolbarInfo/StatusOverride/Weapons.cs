using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
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
            var weaponInfo = Main.ReloadTracking.GetWeaponInfo(item.Block);
            if(weaponInfo == null)
                return false; // likely weaponcore or other unsupported weapon

            //var gun = (IMyGunObject<MyGunBase>)item.Block;
            //int ammo = gun.GunBase.GetTotalAmmunitionAmount();

            int ammo = weaponInfo.Ammo;

            //if(ammo == 0)
            //{
            //    ammo = gun.GunBase.CurrentAmmo;
            //}

            //var gunUser = item.Block as IMyGunBaseUser;
            //if(ammo == 0 && gunUser != null && gunUser.AmmoInventory != null && gun.GunBase.HasAmmoMagazines)
            //{
            //    ammo = gun.GunBase.CurrentAmmo + (int)gunUser.AmmoInventory.GetItemAmount(gun.GunBase.CurrentAmmoMagazineId) * gun.GunBase.CurrentAmmoMagazineDefinition.Capacity;
            //}

            Processor.AppendSingleStats(sb, item.Block);

            if(weaponInfo.Reloading)
            {
                sb.Append("Reload");
            }
            else
            {
                sb.Append(ammo);
            }

            //else if(item.ActionId != "ShootOnce")
            //{
            //    bool shoot = item.Block.GetValueBool("Shoot");
            //    sb.Append(shoot ? "Shoot" : "Ready");
            //}

            //sb.Append("\n");
            //const int maxDigits = 9;
            //int digits = (int)Math.Floor(Math.Log10(ammo) + 1);
            //sb.Append(' ', Math.Max(maxDigits - digits, 0) * 2);

            return true;
        }

        bool GroupShoot(StringBuilder sb, ToolbarItem item, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyUserControllableGun>())
                return false;

            int broken = 0;
            int off = 0;
            int total = 0;
            int reloading = 0;
            int leastAmmo = int.MaxValue;
            int mostAmmo = 0;

            foreach(IMyUserControllableGun gun in groupData.Blocks)
            {
                var weaponInfo = Main.ReloadTracking.GetWeaponInfo(gun);
                if(weaponInfo == null)
                    continue; // likely weaponcore or other unsupported weapon

                if(!gun.IsFunctional)
                    broken++;

                if(!gun.Enabled)
                    off++;

                if(weaponInfo.Reloading)
                    reloading++;

                leastAmmo = Math.Min(leastAmmo, weaponInfo.Ammo);
                mostAmmo = Math.Max(mostAmmo, weaponInfo.Ammo);
                total++;

                //int ammo = gun.GunBase.GetTotalAmmunitionAmount();
                //leastAmmo = Math.Min(leastAmmo, ammo);
                //mostAmmo = Math.Max(mostAmmo, ammo);
            }

            //sb.Append("H: ").Append(mostAmmo);
            //sb.Append("\nL: ").Append(leastAmmo);

            if(total == 0)
                return false; // no supported weapons

            Processor.AppendGroupStats(sb, broken, off);

            if(reloading == total)
            {
                sb.Append("All reload");
            }
            else
            {
                if(reloading > 0)
                    sb.Append("R: ").Append(reloading).Append('\n');

                if(mostAmmo == leastAmmo)
                {
                    sb.Append("A: ").Append(mostAmmo);
                }
                else
                {
                    sb.Append("L: ").Append(leastAmmo);
                    sb.Append("\nH: ").Append(mostAmmo);
                }
            }

            return true;
        }
    }
}
