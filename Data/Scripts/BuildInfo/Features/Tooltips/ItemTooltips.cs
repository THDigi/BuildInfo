using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Utils;

namespace Digi.BuildInfo.Features.Tooltips
{
    // TODO hold <key> to show more tooltip as it apparently updates in realtime
    // ... or don't and have that ingame wiki instead.

    // TODO: add range for handheld weapons

    public class ItemTooltips : ModComponent
    {
        public const string ReqLargeConveyorSymbol = "Ф";
        const string ReqLargeConveyorSymbolSet = "\n            " + ReqLargeConveyorSymbol;
        const string ReqLargeConveyorSymbolAdd = ReqLargeConveyorSymbolSet;

        const int ListLimit = 6;

        readonly List<OriginalData> OriginalItemData = new List<OriginalData>(16);

        enum Sizes { Small, Large, Both, HandWeapon }

        HashSet<MyBlueprintDefinitionBase> TmpBpsMakingThis = new HashSet<MyBlueprintDefinitionBase>();
        HashSet<MyBlueprintDefinitionBase> TmpBpsRequiringThis = new HashSet<MyBlueprintDefinitionBase>();
        Dictionary<string, Sizes> TmpNameAndSize = new Dictionary<string, Sizes>();
        HashSet<string> TmpStringSet = new HashSet<string>();
        StringBuilder SB = new StringBuilder(1024);

        void DisposeTempObjects()
        {
            //TmpBpsMakingThis = null;
            //TmpBpsRequiringThis = null;
            //TmpNameAndSize = null;

            TmpStringSet = null;
        }

        public ItemTooltips(BuildInfoMod main) : base(main)
        {
            AddDescriptions();
            Main.TooltipHandler.Setup += Setup;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            // restore original symbol and tooltips, which doesn't seem necessary for items but better safe.
            foreach(OriginalData data in OriginalItemData)
            {
                data.Def.IconSymbol = data.Symbol;
                data.Def.ExtraInventoryTooltipLine?.Clear()?.Append(data.Tooltip);
            }

            OriginalItemData.Clear();

            Main.TooltipHandler.Setup -= Setup;
        }

        void AddDescriptions()
        {
            string oxygenBottleDesc = "Recharges your life support oxygen automatically when low, if held in inventory." +
               "\nFill the bottle by placing it in the inventory of H2/O2 generators or Oxygen Tanks.";

            string hydrogenBottleDesc = "Recharges your jetpack hydrogen fuel automatically when low, if held in inventory." +
               "\nFill the bottle by placing it in the inventory of H2/O2 generators or Hydrogen Tanks.";

            foreach(MyPhysicalItemDefinition physDef in Main.Caches.ItemDefs)
            {
                MyOxygenContainerDefinition bottleDef = physDef as MyOxygenContainerDefinition;
                if(bottleDef == null)
                    continue;

                if(Main.TooltipHandler.IgnoreModItems.Contains(physDef.Id))
                    continue;

                if(!string.IsNullOrEmpty(bottleDef.DescriptionText))
                    continue;

                if(bottleDef.StoredGasId == MyResourceDistributorComponent.OxygenId)
                {
                    bottleDef.DescriptionEnum = null;
                    bottleDef.DescriptionString = oxygenBottleDesc;
                }
                else if(bottleDef.StoredGasId == MyResourceDistributorComponent.HydrogenId)
                {
                    bottleDef.DescriptionEnum = null;
                    bottleDef.DescriptionString = hydrogenBottleDesc;
                }
            }
        }

        void Setup(bool generate)
        {
            foreach(MyPhysicalItemDefinition physDef in Main.Caches.ItemDefs)
            {
                try
                {
                    if(generate)
                    {
                        string tooltip = null;
                        if(physDef.ExtraInventoryTooltipLine != null && physDef.ExtraInventoryTooltipLine.Length > 0)
                            tooltip = physDef.ExtraInventoryTooltipLine.ToString();

                        OriginalItemData.Add(new OriginalData(physDef, tooltip, physDef.IconSymbol));
                    }

                    HandleSymbol(physDef);
                    HandleTooltip(physDef, generate);
                }
                catch(Exception e)
                {
                    string msg = $"Error setting up tooltips for item: {physDef.Id.ToString()}";
                    Log.Error($"{msg}\n{e}", msg);
                }
            }

            if(generate)
            {
                DisposeTempObjects();
            }
        }

        void HandleSymbol(MyPhysicalItemDefinition physDef)
        {
            if(!Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
                return;

            if(Main.Config.ItemSymbolAdditions.Value)
            {
                if(physDef.IconSymbol.HasValue)
                {
                    string symbolString = physDef.IconSymbol.Value.String;

                    // only add if it's not there already
                    if(symbolString.IndexOf(ReqLargeConveyorSymbolSet) == -1)
                        physDef.IconSymbol = MyStringId.GetOrCompute(symbolString + ReqLargeConveyorSymbolAdd);
                }
                else
                {
                    physDef.IconSymbol = MyStringId.GetOrCompute(ReqLargeConveyorSymbolSet);
                }
            }
            else
            {
                if(physDef.IconSymbol.HasValue)
                {
                    string symbolString = physDef.IconSymbol.Value.String;
                    if(symbolString == ReqLargeConveyorSymbolSet)
                    {
                        // remove symbol if item didn't have one.
                        physDef.IconSymbol = null;
                    }
                    else
                    {
                        // only one with newline can be here
                        int atIndex = symbolString.IndexOf(ReqLargeConveyorSymbolAdd);
                        if(atIndex != -1)
                            physDef.IconSymbol = MyStringId.GetOrCompute(symbolString.Substring(0, atIndex));
                    }
                }
            }
        }

        void HandleTooltip(MyPhysicalItemDefinition physDef, bool generate)
        {
            string tooltip = null;
            if(generate)
            {
                // generate tooltips and cache them alone
                SB.Clear();
                GenerateTooltip(SB, physDef);
                if(SB.Length > 0)
                {
                    tooltip = SB.ToString();
                    Main.TooltipHandler.Tooltips[physDef.Id] = tooltip;
                }
            }
            else
            {
                // retrieve cached tooltip string
                tooltip = Main.TooltipHandler.Tooltips.GetValueOrDefault(physDef.Id, null);
            }

            StringBuilder itemTooltipSB = physDef.ExtraInventoryTooltipLine;

            if(tooltip != null)
            {
                // item tooltip likely contains the cached tooltip, get rid of it.
                if(itemTooltipSB.Length >= tooltip.Length)
                {
                    itemTooltipSB.Replace(tooltip, "");
                }

                if(Main.Config.ItemTooltipAdditions.Value)
                {
                    itemTooltipSB.Append(tooltip);
                }
            }

            #region internal info type+subtype
            const string IdLabel = "\nId: ";

            if(itemTooltipSB.Length > 0)
            {
                itemTooltipSB.RemoveLineStartsWith(IdLabel);
            }

            if(Main.Config.InternalInfo.Value)
            {
                int obPrefixLen = "MyObjectBuilder_".Length;
                string typeIdString = physDef.Id.TypeId.ToString();
                itemTooltipSB.Append(IdLabel).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(physDef.Id.SubtypeName);
            }
            #endregion
        }

        void GenerateTooltip(StringBuilder s, MyPhysicalItemDefinition physDef)
        {
            if(!Main.TooltipHandler.IgnoreModItems.Contains(physDef.Id))
            {
                const int MaxWidth = 70;
                string desc = physDef.DescriptionText;
                if(!string.IsNullOrWhiteSpace(desc))
                {
                    s.TrimEndWhitespace().Append("\n\n").AppendWordWrapped(desc, MaxWidth).TrimEndWhitespace().Append("\n");
                }

                TooltipConsumable(s, physDef);
                TooltipBottle(s, physDef);
                TooltipTool(s, physDef);
                TooltipWeapon(s, physDef);
                TooltipAmmo(s, physDef);
                TooltipUsedIn(s, physDef);
                TooltipBoughtOrSold(s, physDef);
                TooltipCrafting(s, physDef);
            }

            TooltipHandler.AppendModInfo(s, physDef);

            if(Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
            {
                s.Append('\n').Append(ReqLargeConveyorSymbol).Append(" Item is too large for small conveyors.");
            }
        }

        public void TooltipConsumable(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            MyConsumableItemDefinition consumable = physDef as MyConsumableItemDefinition;
            if(consumable != null && consumable.Stats.Count > 0)
            {
                s.Append("\nConsumable: ");

                Dictionary<string, string> statNames = Main.TooltipHandler.TmpStatDisplayNames;

                if(consumable.Stats.Count == 1)
                {
                    MyConsumableItemDefinition.StatValue stat = consumable.Stats[0];
                    s.Append(stat.Value > 0 ? "+" : "").ProportionToPercent(stat.Value * stat.Time, 2).Append(" ")
                     .Append(statNames.GetValueOrDefault(stat.Name, stat.Name)).Append(" over ").TimeFormat(stat.Time);
                }
                else
                {
                    foreach(MyConsumableItemDefinition.StatValue stat in consumable.Stats)
                    {
                        s.Append("\n  ").Append(stat.Value > 0 ? "+" : "").ProportionToPercent(stat.Value * stat.Time, 2).Append(" ")
                         .Append(statNames.GetValueOrDefault(stat.Name, stat.Name)).Append(" over ").TimeFormat(stat.Time);
                    }
                }
            }
        }

        public void TooltipBottle(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            MyOxygenContainerDefinition bottleDef = physDef as MyOxygenContainerDefinition;
            if(bottleDef == null)
                return;

            s.Append("\nCapacity: ").VolumeFormat(bottleDef.Capacity).Append(" of ").ItemName(bottleDef.StoredGasId);

            // not really necessary
            /*
            List<MyProductionBlockDefinition> prodDefs;
            if(Main.TooltipHandler.TmpItemRefillIn.TryGetValue(physDef.Id, out prodDefs))
            {
                TmpNameAndSize.Clear();

                foreach(MyProductionBlockDefinition prodDef in prodDefs)
                {
                    AddToList(TmpNameAndSize, prodDef.DisplayNameText, prodDef.CubeSize);
                }

                PrintList(TmpNameAndSize, s, "Refillable in");
            }
            */
        }

        public void TooltipTool(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            if(!MyDefinitionManager.Static.HandItemExistsFor(physDef.Id)) // HACK: because TryGetHandItemForPhysicalItem() logs on failure, spamming the log. 
                return;

            MyEngineerToolBaseDefinition toolDef = MyDefinitionManager.Static.TryGetHandItemForPhysicalItem(physDef.Id) as MyEngineerToolBaseDefinition;
            if(toolDef == null)
                return;

            MyHandDrillDefinition drillDef = toolDef as MyHandDrillDefinition;
            if(drillDef != null)
            {
                s.Append("\nDrill radius: ").DistanceFormat(Hardcoded.HandDrill_DefaultRadius * toolDef.DistanceMultiplier).Append(" (x").RoundedNumber(Hardcoded.Drill_MineVoelNoOreRadiusMul, 2).Append(" with secondary)");
                s.Append("\nDrill speed: ").MultiplierToPercent(toolDef.SpeedMultiplier);
                s.Append("\nDrill harvest: ").MultiplierToPercent(drillDef.HarvestRatioMultiplier);
                return;
            }

            // HACK: MyWelderDefinition & MyAngleGrinderDefinition are internal
            string defType = toolDef.GetType().Name;

            if(defType == "MyAngleGrinderDefinition")
            {
                s.Append("\nReach: ").DistanceFormat(Hardcoded.EngineerToolBase_DefaultReachDistance * toolDef.DistanceMultiplier);
                s.Append("\nGrind speed: ").MultiplierToPercent(toolDef.SpeedMultiplier);
                return;
            }

            if(defType == "MyWelderDefinition")
            {
                s.Append("\nReach: ").DistanceFormat(Hardcoded.EngineerToolBase_DefaultReachDistance * toolDef.DistanceMultiplier);
                s.Append("\nWeld speed: ").MultiplierToPercent(toolDef.SpeedMultiplier);
                return;
            }

            s.Append("\nTool distance: ").MultiplierToPercent(toolDef.DistanceMultiplier);
            s.Append("\nTool speed: ").MultiplierToPercent(toolDef.SpeedMultiplier);
        }

        public void TooltipWeapon(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            MyWeaponItemDefinition weaponItemDef = physDef as MyWeaponItemDefinition;
            if(weaponItemDef == null)
                return;

            List<CoreSystems.Api.CoreSystemsDef.WeaponDefinition> csDefs;
            if(Main.CoreSystemsAPIHandler.Weapons.TryGetValue(physDef.Id, out csDefs))
            {
                // TODO: WC stats?
                return;
            }

            MyWeaponDefinition weaponDef;
            if(!MyDefinitionManager.Static.TryGetWeaponDefinition(weaponItemDef.WeaponDefinitionId, out weaponDef))
                return;

            // TODO: some weapon stats? they depend on the ammo tho...

            if(weaponDef.AmmoMagazinesId != null && weaponDef.AmmoMagazinesId.Length > 0)
            {
                if(weaponDef.AmmoMagazinesId.Length == 1)
                    s.Append("\nUses magazine:");
                else
                    s.Append("\nUses magazines:");

                foreach(MyDefinitionId magId in weaponDef.AmmoMagazinesId)
                {
                    s.Append("\n  ");

                    MyAmmoMagazineDefinition magDef = Utils.TryGetMagazineDefinition(magId, weaponDef.Context);
                    if(magDef == null)
                        s.Append("(NotFound=").Append(magId.ToString()).Append(")");
                    else
                        s.Append(magDef.DisplayNameText);

                    if(!Main.TooltipHandler.TmpHasBP.Contains(magId))
                        s.Append(" (Not Craftable)");
                }
            }
        }

        public void TooltipAmmo(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            MyAmmoMagazineDefinition magDef = physDef as MyAmmoMagazineDefinition;
            if(magDef == null)
                return;

            if(magDef.Capacity > 1)
                s.Append("\nMagazine Capacity: ").Append(magDef.Capacity);

            MyAmmoDefinition ammoDef = Utils.TryGetAmmoDefinition(magDef.AmmoDefinitionId, magDef.Context);
            if(ammoDef != null)
            {
                float damagePerMag;
                if(Hardcoded.GetAmmoInventoryExplosion(magDef, ammoDef, 1, out damagePerMag))
                {
                    if(damagePerMag > 0)
                    {
                        s.Append("\nOn container destroyed: explodes by ").RoundedNumber(damagePerMag, 5).Append("dmg/mag");
                    }
                    else if(damagePerMag < 0)
                    {
                        s.Append("\nOn container destroyed: reduces other ammo explosion by ").RoundedNumber(damagePerMag, 5).Append("dmg/mag");
                    }
                }
            }
        }

        public void TooltipUsedIn(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            TmpNameAndSize.Clear();

            // used as ammo
            MyAmmoMagazineDefinition magDef = physDef as MyAmmoMagazineDefinition;
            if(magDef != null)
            {
                List<MyTuple<MyDefinitionBase, MyWeaponDefinition>> weapons;
                if(Main.TooltipHandler.TmpMagUsedIn.TryGetValue(magDef.Id, out weapons))
                {
                    foreach(MyTuple<MyDefinitionBase, MyWeaponDefinition> tuple in weapons)
                    {
                        {
                            MyWeaponBlockDefinition wpBlockDef = tuple.Item1 as MyWeaponBlockDefinition;
                            if(wpBlockDef != null)
                            {
                                AddToList(TmpNameAndSize, wpBlockDef);
                                continue;
                            }
                        }
                        {
                            MyWeaponItemDefinition wpPhysItem = tuple.Item1 as MyWeaponItemDefinition;
                            if(wpPhysItem != null)
                            {
                                AddToList(TmpNameAndSize, wpPhysItem, Sizes.HandWeapon);
                                continue;
                            }
                        }
                    }
                }
            }

            // used as fuel
            HashSet<MyCubeBlockDefinition> blockDefs;
            if(Main.TooltipHandler.TmpBlockFuel.TryGetValue(physDef.Id, out blockDefs))
            {
                foreach(MyCubeBlockDefinition blockDef in blockDefs)
                {
                    AddToList(TmpNameAndSize, blockDef);
                }
            }

            if(TmpNameAndSize.Count > 0)
                PrintList(TmpNameAndSize, s, "Used in");
        }

        public void TooltipBoughtOrSold(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            TmpNameAndSize.Clear();

            HashSet<MyVendingMachineDefinition> storeList;
            if(Main.TooltipHandler.TmpVendingBuy.TryGetValue(physDef.Id, out storeList))
            {
                foreach(MyVendingMachineDefinition storeDef in storeList)
                {
                    AddToList(TmpNameAndSize, storeDef);
                }
            }

            if(TmpNameAndSize.Count > 0)
            {
                PrintList(TmpNameAndSize, s, "Buy from");
            }

            TmpNameAndSize.Clear();
            storeList = null;
            if(Main.TooltipHandler.TmpVendingSell.TryGetValue(physDef.Id, out storeList))
            {
                foreach(MyVendingMachineDefinition storeDef in storeList)
                {
                    AddToList(TmpNameAndSize, storeDef);
                }
            }

            if(TmpNameAndSize.Count > 0)
            {
                PrintList(TmpNameAndSize, s, "Sell to");
            }

            // TODO find if it's sold by NPC stations?
        }

        public void TooltipCrafting(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false, bool forTextBoxTooltip = false)
        {
            TmpBpsMakingThis.Clear();
            TmpBpsRequiringThis.Clear();

            bool survival = MyAPIGateway.Session.SurvivalMode;

            foreach(MyBlueprintDefinitionBase bpDef in MyDefinitionManager.Static.GetBlueprintDefinitions())
            {
                if(!bpDef.Public || (survival && !bpDef.AvailableInSurvival))
                    continue;

                // if composite's subtype contains a type/subtype separator then assume it's for a block and skip it
                if(bpDef is MyCompositeBlueprintDefinition && bpDef.Id.SubtypeName.Contains("/"))
                    continue;

                bool isResult = false;

                foreach(MyBlueprintDefinitionBase.Item result in bpDef.Results)
                {
                    if(result.Id == physDef.Id)
                    {
                        TmpBpsMakingThis.Add(bpDef);
                        isResult = true;
                        break; // results loop
                    }
                }

                foreach(MyBlueprintDefinitionBase.Item req in bpDef.Prerequisites)
                {
                    if(req.Id == physDef.Id)
                    {
                        if(isResult)
                            TmpBpsMakingThis.Remove(bpDef); // ignore bps that require the same item as they produce
                        else
                            TmpBpsRequiringThis.Add(bpDef);

                        break; // preqreq loop
                    }
                }
            }

            if(forTextBoxTooltip)
            {
                Crafting_Sources(s, physDef);
            }
            else if(forBlueprint)
            {
                Crafting_Ingredient(s, physDef, detailed: true);
                Crafting_BlockComponent(s, physDef, detailed: true);
            }
            else
            {
                Crafting_Sources(s, physDef);
                Crafting_Ingredient(s, physDef, detailed: false);
                Crafting_BlockComponent(s, physDef, detailed: false);
            }

            if(!forBlueprint && !forTextBoxTooltip)
            {
                HashSet<MyCubeBlockDefinition> blockDefs;
                if(Main.TooltipHandler.TmpComponentFromGrindingBlocks.TryGetValue(physDef.Id, out blockDefs))
                {
                    s.Append("\nGained by grinding ").Append(blockDefs.Count).Append(" specific ").Append(blockDefs.Count == 1 ? "block." : "blocks.");
                }
            }

            TmpBpsMakingThis.Clear();
            TmpBpsRequiringThis.Clear();
        }

        void Crafting_Sources(StringBuilder s, MyPhysicalItemDefinition physDef)
        {
            bool showNotCraftable = true;

            if(TmpBpsMakingThis.Count > 0)
            {
                TmpNameAndSize.Clear();

                bool areResults = true;
                foreach(MyBlueprintDefinitionBase bp in TmpBpsMakingThis)
                {
                    HashSet<MyProductionBlockDefinition> prodList;
                    if(BuildInfoMod.Instance.TooltipHandler.TmpBpUsedIn.TryGetValue(bp.Id, out prodList))
                    {
                        foreach(MyProductionBlockDefinition prodDef in prodList)
                        {
                            // HACK: bp results of gas generators or gas tanks are not used, skip
                            if(areResults && (prodDef is MyGasTankDefinition || prodDef is MyOxygenGeneratorDefinition))
                                continue;

                            AddToList(TmpNameAndSize, prodDef);
                        }
                    }
                }

                if(TmpNameAndSize.Count > 0)
                {
                    showNotCraftable = false;
                    PrintList(TmpNameAndSize, s, "Crafted by");
                }
            }

            if(showNotCraftable)
            {
                s.Append("\nNot Craftable.");
            }
        }

        void Crafting_Ingredient(StringBuilder s, MyPhysicalItemDefinition physDef, bool detailed)
        {
            int usedForAssembly = 0;

            TmpStringSet.Clear();

            foreach(MyBlueprintDefinitionBase bp in TmpBpsRequiringThis)
            {
                HashSet<MyProductionBlockDefinition> prodDefs;
                if(Main.TooltipHandler.TmpBpUsedIn.TryGetValue(bp.Id, out prodDefs))
                {
                    bool used = false;

                    // is used in any non-refill role?
                    foreach(var prodDef in prodDefs)
                    {
                        if(prodDef is MyGasTankDefinition || prodDef is MyOxygenGeneratorDefinition)
                            continue;

                        used = true;
                        break;
                    }

                    if(!used)
                        continue; // blueprints loop
                }

                usedForAssembly++;

                if(detailed)
                {
                    string nameNoTooltip = bp.DisplayNameText;
                    int newlineIdx = nameNoTooltip.IndexOf('\n');
                    if(newlineIdx != -1)
                        nameNoTooltip = nameNoTooltip.Substring(0, newlineIdx);

                    TmpStringSet.Add(nameNoTooltip);
                }
            }

            if(usedForAssembly > 0)
            {
                if(detailed && TmpStringSet.Count <= ListLimit)
                {
                    s.Append("\nUsed for crafting ").Append(usedForAssembly == 1 ? "item" : "items").Append(":");

                    int limit = 0;

                    foreach(string name in TmpStringSet)
                    {
                        if(++limit > ListLimit)
                        {
                            limit--;
                            s.Append("\n  ...and ").Append(TmpStringSet.Count - limit).Append(" more");
                            break;
                        }

                        s.Append("\n  ").Append(name);
                    }
                }
                else
                {
                    s.Append("\nUsed for crafting ").Append(usedForAssembly).Append(" item").Append(usedForAssembly == 1 ? "." : "s.");
                }
            }
        }

        void Crafting_BlockComponent(StringBuilder s, MyPhysicalItemDefinition physDef, bool detailed)
        {
            HashSet<MyCubeBlockDefinition> blockDefs;
            if(Main.TooltipHandler.TmpComponentInBlocks.TryGetValue(physDef.Id, out blockDefs))
            {
                TmpNameAndSize.Clear();

                if(detailed)
                {
                    foreach(var blockDef in blockDefs)
                    {
                        AddToList(TmpNameAndSize, blockDef);
                    }
                }

                const int listLimit = 10;
                if(detailed && TmpNameAndSize.Count <= listLimit * 2)
                {
                    PrintList(TmpNameAndSize, s, (blockDefs.Count == 1 ? "Used for building a block" : "Used for building blocks"), customListLimit: listLimit);
                }
                else
                {
                    s.Append("\nUsed for building ").Append(blockDefs.Count).Append(" ").Append(blockDefs.Count == 1 ? "block." : "blocks.");
                }
            }
        }

        static void AddToList(Dictionary<string, Sizes> dict, MyDefinitionBase def, Sizes? currentSize = null)
        {
            if(def == null) throw new ArgumentNullException("def");

            string key = def.DisplayNameText;
            if(key == null)
                key = def.Id.ToString();

            if(currentSize == null)
            {
                var blockDef = def as MyCubeBlockDefinition;
                if(blockDef != null)
                    currentSize = (blockDef.CubeSize == MyCubeSize.Small ? Sizes.Small : Sizes.Large);
                else
                    Log.Error($"ItemTooltips.AddToList() :: No size provided for {def.Id} and can't guess it because it's not a block def.");
            }

            Sizes existingSize;
            if(dict.TryGetValue(key, out existingSize))
            {
                if(existingSize != Sizes.Both && existingSize != currentSize.Value)
                    dict[key] = Sizes.Both;
            }
            else
            {
                dict[key] = currentSize.Value;
            }
        }

        static void PrintList(Dictionary<string, Sizes> dict, StringBuilder s, string label = "Used in", int customListLimit = ListLimit)
        {
            s.Append("\n").Append(label).Append(":");

            int limit = 0;
            foreach(KeyValuePair<string, Sizes> kv in dict)
            {
                if(++limit > customListLimit)
                {
                    limit--;
                    s.Append("\n  ...and ").Append(dict.Count - limit).Append(" more");
                    break;
                }

                s.Append("\n  ").Append(kv.Key);

                switch(kv.Value)
                {
                    case Sizes.Small: s.Append(" (Small)"); break;
                    case Sizes.Large: s.Append(" (Large)"); break;
                    case Sizes.Both: s.Append(" (Small + Large)"); break;
                    case Sizes.HandWeapon: s.Append(" (Hand-held)"); break;
                }
            }
        }

        struct OriginalData
        {
            public readonly MyPhysicalItemDefinition Def;
            public readonly string Tooltip;
            public readonly MyStringId? Symbol;

            public OriginalData(MyPhysicalItemDefinition def, string tooltip, MyStringId? symbol)
            {
                Def = def;
                Tooltip = tooltip;
                Symbol = symbol;
            }
        }
    }
}
