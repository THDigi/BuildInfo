using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.ReloadTracker;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game.Components;
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
            RegisterFor(typeof(MyObjectBuilder_TurretControlBlock));
        }

        void RegisterFor(MyObjectBuilderType type)
        {
            Processor.AddStatus(type, Shoot, "ShootOnce", "Shoot", "Shoot_On", "Shoot_Off");
            Processor.AddGroupStatus(type, GroupShoot, "ShootOnce", "Shoot", "Shoot_On", "Shoot_Off");
        }

        bool Shoot(StringBuilder sb, ToolbarItem item)
        {
            if(BuildInfoMod.Instance.CoreSystemsAPIHandler.Weapons.ContainsKey(item.Block.BlockDefinition))
                return false;

            if(!Processor.AppendSingleStats(sb, item.Block))
            {
                if(item.ActionId != "ShootOnce")
                {
                    bool shoot = item.Block.GetValue<bool>("Shoot");
                    if(shoot)
                        sb.Append("Fire");
                    else
                        sb.Append("No fire");
                    sb.Append('\n');
                }
            }

            IMyGunObject<MyGunBase> gun = (IMyGunObject<MyGunBase>)item.Block;
            int ammo = gun.GunBase.GetTotalAmmunitionAmount();
            if(ammo <= gun.GunBase.CurrentAmmo)
                ammo = gun.GunBase.CurrentAmmo + gun.GunBase.GetInventoryAmmoMagazinesCount() * gun.GunBase.CurrentAmmoMagazineDefinition.Capacity;

            if(ammo == 0)
            {
                // TODO: in MP this can show up when weapon has other ammo
                // probably because the gun clientside isn't very aware of the selected magazine type

                sb.Append(IconAlert).Append("NoAmmo");
                return true;
            }

            bool showAmmo = true;

            int tick = Processor.Main.Tick;
            TrackedWeapon weaponInfo = Processor.Main.ReloadTracking.WeaponLookup.GetValueOrDefault(item.Block.EntityId, null);
            if(weaponInfo != null && weaponInfo.ReloadUntilTick > tick)
            {
                float seconds = (weaponInfo.ReloadUntilTick - tick) / (float)Constants.TicksPerSecond;
                sb.Append("R:").TimeFormat(seconds);
                showAmmo = false;
            }
            else
            {
                IMyStoredPowerRatio chargeComp = null;

                foreach(MyComponentBase comp in item.Block.Components)
                {
                    chargeComp = comp as IMyStoredPowerRatio;
                    if(chargeComp != null)
                        break;
                }

                if(chargeComp != null && chargeComp.StoredPowerRatio < 1f)
                {
                    sb.Append("C:").ProportionToPercent(chargeComp.StoredPowerRatio);
                    showAmmo = false;
                }
            }

            if(showAmmo)
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
            int charging = 0;
            int noAmmo = 0;
            int firing = 0;

            float minReloadTimeLeft = float.MaxValue;
            float highestCharge = 0f;

            int leastAmmo = int.MaxValue;
            int mostAmmo = 0;

            int tick = Processor.Main.Tick;
            var weaponLookup = Processor.Main.ReloadTracking.WeaponLookup;
            var WCLookup = Processor.Main.CoreSystemsAPIHandler.Weapons;

            foreach(IMyUserControllableGun gunBlock in groupData.Blocks)
            {
                if(WCLookup.ContainsKey(gunBlock.BlockDefinition))
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
                    TrackedWeapon weaponInfo = weaponLookup.GetValueOrDefault(gunBlock.EntityId, null);
                    if(weaponInfo != null && weaponInfo.ReloadUntilTick > tick)
                    {
                        reloading++;

                        float seconds = (weaponInfo.ReloadUntilTick - tick) / (float)Constants.TicksPerSecond;
                        minReloadTimeLeft = Math.Min(minReloadTimeLeft, seconds);
                    }
                    else
                    {
                        IMyStoredPowerRatio chargeComp = null;

                        foreach(MyComponentBase comp in gunBlock.Components)
                        {
                            chargeComp = comp as IMyStoredPowerRatio;
                            if(chargeComp != null)
                                break;
                        }

                        if(chargeComp != null && chargeComp.StoredPowerRatio < 1f)
                        {
                            charging++;
                            highestCharge = Math.Max(highestCharge, chargeComp.StoredPowerRatio);
                        }
                        else
                        {
                            leastAmmo = Math.Min(leastAmmo, ammo);
                            mostAmmo = Math.Max(mostAmmo, ammo);
                        }
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
                    sb.NumberCappedSpaced(firing, MaxChars - 4).Append("fire");

                sb.Append('\n');
            }

            if(noAmmo == total)
            {
                sb.Append(IconAlert).Append("NoAmmo");
                return true;
            }

            if(reloading == total)
            {
                sb.Append("R:").TimeFormat(minReloadTimeLeft);
                return true;
            }

            if(charging == total)
            {
                sb.Append("C:").ProportionToPercent(highestCharge);
                return true;
            }

            if(reloading > 0)
                sb.Append("R:").TimeFormat(minReloadTimeLeft).Append('\n');
            else if(charging > 0)
                sb.Append("C:").ProportionToPercent(highestCharge).Append('\n');

            if(mostAmmo > leastAmmo)
                sb.Append(Math.Min(leastAmmo, 99999)).Append('+');
            else
                sb.NumberCapped(mostAmmo, 5);

            return true;
        }
    }
}
