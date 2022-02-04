using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Localization;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage;
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

            Processor.AddStatus(type, CycleTarget, "TargetingGroup_CycleSubsystems");
            Processor.AddGroupStatus(type, GroupCycleTarget, "TargetingGroup_CycleSubsystems");
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
                float seconds = (weaponInfo.ReloadUntilTick - BuildInfoMod.Instance.Tick) / (float)Constants.TicksPerSecond;
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
                if(BuildInfoMod.Instance.CoreSystemsAPIHandler.Weapons.ContainsKey(gunBlock.BlockDefinition))
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

                        float seconds = (weaponInfo.ReloadUntilTick - BuildInfoMod.Instance.Tick) / (float)Constants.TicksPerSecond;
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

        bool CycleTarget(StringBuilder sb, ToolbarItem item)
        {
            // HACK: backwards compatible
#if !(VERSION_190 || VERSION_191 || VERSION_192 || VERSION_193 || VERSION_194 || VERSION_195 || VERSION_196 || VERSION_197 || VERSION_198 || VERSION_199)

            int groupIdx = (int)item.Block.GetValue<long>("TargetingGroup_Selector") - 1;

            if(groupIdx == -1)
            {
                sb.Append(MyTexts.GetString(MySpaceTexts.BlockPropertyItem_TargetOptions_Default));
            }
            else
            {
                List<MyTargetingGroupDefinition> targetGroups = MyDefinitionManager.Static.GetTargetingGroupDefinitions();

                if(groupIdx < 0 || groupIdx >= targetGroups.Count)
                {
                    sb.Append("#").Append(groupIdx);
                }
                else
                {
                    MyTargetingGroupDefinition group = targetGroups[groupIdx];
                    sb.Append(group.DisplayNameText);
                }
            }

            return true;
#else
            return false;
#endif
        }

        readonly HashSet<int> Targetting = new HashSet<int>();

        bool GroupCycleTarget(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            // HACK: backwards compatible
#if !(VERSION_190 || VERSION_191 || VERSION_192 || VERSION_193 || VERSION_194 || VERSION_195 || VERSION_196 || VERSION_197 || VERSION_198 || VERSION_199)

            if(!groupData.GetGroupBlocks<IMyLargeTurretBase>())
                return false;

            Targetting.Clear();

            foreach(IMyLargeTurretBase gunBlock in groupData.Blocks)
            {
                //if(BuildInfoMod.Instance.CoreSystemsAPIHandler.Weapons.ContainsKey(gunBlock.BlockDefinition))
                //    continue;

                ITerminalProperty prop = gunBlock.GetProperty("TargetingGroup_Selector");
                if(prop != null)
                {
                    int groupIdx = (int)prop.As<long>().GetValue(gunBlock) - 1;
                    Targetting.Add(groupIdx);

                    //Targetting[groupIdx] = Targetting.GetValueOrDefault(groupIdx, 0) + 1;
                }
            }

            if(Targetting.Count == 0)
            {
                sb.Append("Unknown");
            }
            else if(Targetting.Count > 1)
            {
                sb.Append("Mixed");
            }
            else
            {
                sb.Append("All\n");

                //KeyValuePair<int, int> firstPair = Targetting.FirstPair();
                //int groupIdx = firstPair.Key;
                int groupIdx = Targetting.FirstElement();

                if(groupIdx == -1)
                {
                    sb.Append(MyTexts.GetString(MySpaceTexts.BlockPropertyItem_TargetOptions_Default));
                }
                else
                {
                    List<MyTargetingGroupDefinition> targetGroups = MyDefinitionManager.Static.GetTargetingGroupDefinitions();

                    if(groupIdx < 0 || groupIdx >= targetGroups.Count)
                    {
                        sb.Append("#").Append(groupIdx);
                    }
                    else
                    {
                        MyTargetingGroupDefinition group = targetGroups[groupIdx];
                        sb.Append(group.DisplayNameText);
                    }
                }
            }

            return true;
#else
            return false;
#endif
        }
    }
}
