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
using SpaceEngineers.Game.ModAPI;
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

            // TODO: maybe in their own class?
            RegisterFor(typeof(MyObjectBuilder_TurretControlBlock));
            RegisterFor(typeof(MyObjectBuilder_Searchlight));
        }

        void RegisterFor(MyObjectBuilderType type)
        {
            Processor.AddStatus(type, Shoot, "ShootOnce", "Shoot", "Shoot_On", "Shoot_Off");

            Processor.AddGroupStatus(type, GroupShoot, "ShootOnce", "Shoot", "Shoot_On", "Shoot_Off");


            ToolbarStatusProcessor.StatusDel CycleTargetFunc = CycleTarget;
            ToolbarStatusProcessor.GroupStatusDel GroupCycleTargetFunc = GroupCycleTarget;

            Processor.AddStatus(type, CycleTargetFunc, "TargetingGroup_CycleSubsystems");
            Processor.AddGroupStatus(type, GroupCycleTargetFunc, "TargetingGroup_CycleSubsystems");

            foreach(MyTargetingGroupDefinition def in Processor.Main.Caches.OrderedTargetGroups)
            {
                string actionId = $"TargetingGroup_{def.Id.SubtypeName}";
                Processor.AddStatus(type, CycleTargetFunc, actionId);
                Processor.AddGroupStatus(type, GroupCycleTargetFunc, actionId);
            }
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
            string targetGroup = GetTargetGroupSubtypeId(item.Block);
            if(targetGroup == null)
                return false;

            sb.Append(GetTargetGroupName(targetGroup));

            return true;
        }

        readonly HashSet<string> TempTargetting = new HashSet<string>();
        bool GroupCycleTarget(StringBuilder sb, ToolbarItem groupToolbarItem, GroupData groupData)
        {
            if(!groupData.GetGroupBlocks<IMyTerminalBlock>())
                return false;

            TempTargetting.Clear();

            foreach(IMyTerminalBlock tb in groupData.Blocks)
            {
                string targetGroup = GetTargetGroupSubtypeId(tb);
                if(targetGroup == null)
                    continue;

                TempTargetting.Add(targetGroup);
            }

            if(TempTargetting.Count == 0)
            {
                sb.Append("Unknown");
            }
            else if(TempTargetting.Count > 1)
            {
                sb.Append("Mixed");
            }
            else
            {
                sb.Append("All\n");

                string targetGroup = TempTargetting.FirstElement();

                sb.Append(GetTargetGroupName(targetGroup));
            }

            return true;
        }

        /// <summary>
        /// Returns subtypeId for current targeting group, "" if it's default, and null if it's not a supported block or has invalid group.
        /// </summary>
        string GetTargetGroupSubtypeId(IMyTerminalBlock block)
        {
            //{
            //    IMyLargeTurretBase turret = block as IMyLargeTurretBase;
            //    if(turret != null)
            //    {
            //        return turret.GetTargetingGroup() ?? "";
            //    }
            //}
            //{
            //    IMyTurretControlBlock turretControl = block as IMyTurretControlBlock;
            //    if(turretControl != null)
            //    {
            //        return turretControl.GetTargetingGroup() ?? "";
            //    }
            //}

            ITerminalProperty<long> prop = block.GetProperty("TargetingGroup_Selector")?.As<long>();
            if(prop != null)
            {
                int groupIdx = (int)prop.GetValue(block); // 0 is default, 1+ is group index+1
                groupIdx -= 1; // turn default to -1

                if(groupIdx == -1)
                    return "";

                List<MyTargetingGroupDefinition> targetGroups = Processor.Main.Caches.OrderedTargetGroups;
                if(groupIdx < 0 || groupIdx >= targetGroups.Count) // unknown state
                    return null;

                return targetGroups[groupIdx].Id.SubtypeName;
            }

            return null;
        }

        string GetTargetGroupName(string subtypeId)
        {
            if(subtypeId == null)
                return "Unknown";

            if(string.IsNullOrEmpty(subtypeId))
                return MyTexts.GetString(MySpaceTexts.BlockPropertyItem_TargetOptions_Default);

            MyTargetingGroupDefinition def;
            if(Processor.Main.Caches.TargetGroups.TryGetValue(subtypeId, out def))
            {
                return def.DisplayNameText;
            }

            return subtypeId;
        }
    }
}
