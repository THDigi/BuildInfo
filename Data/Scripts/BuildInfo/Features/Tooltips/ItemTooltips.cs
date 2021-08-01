using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using VRage;
using VRage.Game;
using VRage.Utils;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class ItemTooltips : ModComponent
    {
        public const string ReqLargeConveyorSymbol = "Ф";
        const string ReqLargeConveyorSymbolSet = "            " + ReqLargeConveyorSymbol;
        const string ReqLargeConveyorSymbolAdd = "\n" + ReqLargeConveyorSymbolSet;

        const int ListLimit = 6;

        readonly List<OriginalData> OriginalItemData = new List<OriginalData>(16);

        enum Sizes { Small, Large, Both, HandWeapon }

        HashSet<MyBlueprintDefinitionBase> TmpBpsThatResult = new HashSet<MyBlueprintDefinitionBase>();
        HashSet<MyBlueprintDefinitionBase> TmpBpsThatReq = new HashSet<MyBlueprintDefinitionBase>();
        Dictionary<string, Sizes> TmpNameAndSize = new Dictionary<string, Sizes>();

        void DisposeTempObjects()
        {
            TmpBpsThatResult = null;
            TmpBpsThatReq = null;
            TmpNameAndSize = null;
        }

        StringBuilder SB = new StringBuilder(1024);

        public ItemTooltips(BuildInfoMod main) : base(main)
        {
            SetDescription(new MyDefinitionId(typeof(MyObjectBuilder_OxygenContainerObject), "OxygenBottle"), "Recharges your life support oxygen automatically when low, if held in inventory.\nFill the bottle by placing it in the inventory of H2/O2 generators or Oxygen Tanks.");
            SetDescription(new MyDefinitionId(typeof(MyObjectBuilder_GasContainerObject), "HydrogenBottle"), "Recharges your jetpack hydrogen fuel automatically when low, if held in inventory.\nFill the bottle by placing it in the inventory of H2/O2 generators or Hydrogen Tanks.");

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
            foreach(var data in OriginalItemData)
            {
                data.Def.IconSymbol = data.Symbol;
                data.Def.ExtraInventoryTooltipLine?.Clear()?.Append(data.Tooltip);
            }

            OriginalItemData.Clear();

            Main.TooltipHandler.Setup -= Setup;
        }

        void Setup(bool generate)
        {
            foreach(var physDef in Main.Caches.ItemDefs)
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
                    var symbolString = physDef.IconSymbol.Value.String;

                    // only add if it's not there already
                    if(symbolString.IndexOf(ReqLargeConveyorSymbolAdd) == -1)
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
                    var symbolString = physDef.IconSymbol.Value.String;
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
                TooltipConsumable(s, physDef);
                TooltipFuel(s, physDef);
                TooltipBottle(s, physDef);
                TooltipTool(s, physDef);
                TooltipWeapon(s, physDef);
                TooltipAmmo(s, physDef);
                TooltipCrafting(s, physDef);
            }

            TooltipHandler.AppendModInfo(s, physDef);

            if(Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
            {
                s.Append('\n').Append(ReqLargeConveyorSymbol).Append(" Item can not pass through small conveyors.");
            }
        }

        public void TooltipConsumable(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            var consumable = physDef as MyConsumableItemDefinition;
            if(consumable != null && consumable.Stats.Count > 0)
            {
                s.Append("\nConsumption: ");

                var statNames = Main.TooltipHandler.TmpStatDisplayNames;

                if(consumable.Stats.Count == 1)
                {
                    var stat = consumable.Stats[0];
                    s.Append(stat.Value > 0 ? "+" : "").ProportionToPercent(stat.Value * stat.Time, 2).Append(" ").Append(statNames.GetValueOrDefault(stat.Name, stat.Name)).Append(" over ").TimeFormat(stat.Time);
                }
                else
                {
                    foreach(var stat in consumable.Stats)
                    {
                        s.Append("\n  ").Append(stat.Value > 0 ? "+" : "").ProportionToPercent(stat.Value * stat.Time, 2).Append(" ").Append(statNames.GetValueOrDefault(stat.Name, stat.Name)).Append(" over ").TimeFormat(stat.Time);
                    }
                }
            }
        }

        void TooltipFuel(StringBuilder s, MyPhysicalItemDefinition physDef)
        {
            var blocks = Main.TooltipHandler.TmpBlockFuel.GetValueOrDefault(physDef.Id, null);
            if(blocks == null || blocks.Count == 0)
                return;

            TmpNameAndSize.Clear();

            foreach(var blockDef in blocks)
            {
                string key = blockDef.DisplayNameText;
                Sizes currentSize = (blockDef.CubeSize == MyCubeSize.Small ? Sizes.Small : Sizes.Large);
                Sizes existingSize;
                if(TmpNameAndSize.TryGetValue(key, out existingSize))
                {
                    if(existingSize != Sizes.Both && existingSize != currentSize)
                        TmpNameAndSize[key] = Sizes.Both;
                }
                else
                {
                    TmpNameAndSize[key] = currentSize;
                }
            }

            s.Append("\nConsumed by: ");

            int limit = 0;
            foreach(var kv in TmpNameAndSize)
            {
                if(++limit > ListLimit)
                {
                    limit--;
                    s.Append("\n  ...and ").Append(TmpNameAndSize.Count - limit).Append(" more");
                    break;
                }

                s.Append("\n  ").Append(kv.Key);

                switch(kv.Value)
                {
                    case Sizes.Small: s.Append(" (Small)"); break;
                    case Sizes.Large: s.Append(" (Large)"); break;
                    case Sizes.Both: s.Append(" (Small + Large)"); break;
                }
            }
        }

        public void TooltipBottle(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            var bottleDef = physDef as MyOxygenContainerDefinition;
            if(bottleDef == null)
                return;

            s.Append("\nCapacity: ").VolumeFormat(bottleDef.Capacity).Append(" of ").IdTypeSubtypeFormat(bottleDef.StoredGasId);
        }

        public void TooltipTool(StringBuilder s, MyPhysicalItemDefinition physDef, bool forBlueprint = false)
        {
            if(!MyDefinitionManager.Static.HandItemExistsFor(physDef.Id)) // HACK: because TryGetHandItemForPhysicalItem() logs on failure, spamming the log. 
                return;

            var toolDef = MyDefinitionManager.Static.TryGetHandItemForPhysicalItem(physDef.Id) as MyEngineerToolBaseDefinition;
            if(toolDef == null)
                return;

            var drillDef = toolDef as MyHandDrillDefinition;
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
            var weaponItemDef = physDef as MyWeaponItemDefinition;
            if(weaponItemDef == null)
                return;

            MyWeaponDefinition weaponDef;
            if(!MyDefinitionManager.Static.TryGetWeaponDefinition(weaponItemDef.WeaponDefinitionId, out weaponDef))
                return;

            // TODO: check if it's weaponcore weapon and avoid some potentially misleading stats.
            // TODO: some weapon stats? they depend on the ammo tho...

            if(weaponDef.AmmoMagazinesId != null && weaponDef.AmmoMagazinesId.Length > 0)
            {
                if(weaponDef.AmmoMagazinesId.Length == 1)
                    s.Append("\nUses magazine:");
                else
                    s.Append("\nUses magazines:");

                foreach(var magId in weaponDef.AmmoMagazinesId)
                {
                    s.Append("\n  ");

                    var magDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(magId);
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
            var magDef = physDef as MyAmmoMagazineDefinition;
            if(magDef == null)
                return;

            if(magDef.Capacity > 1)
                s.Append("\nMagazine Capacity: ").Append(magDef.Capacity);

            TmpNameAndSize.Clear();

            List<MyTuple<MyDefinitionBase, MyWeaponDefinition>> weapons;
            if(Main.TooltipHandler.TmpMagUsedIn.TryGetValue(magDef.Id, out weapons))
            {
                foreach(var tuple in weapons)
                {
                    {
                        var wpBlockDef = tuple.Item1 as MyWeaponBlockDefinition;
                        if(wpBlockDef != null)
                        {
                            string key = wpBlockDef.DisplayNameText;
                            Sizes currentSize = (wpBlockDef.CubeSize == MyCubeSize.Small ? Sizes.Small : Sizes.Large);
                            Sizes existingSize;
                            if(TmpNameAndSize.TryGetValue(key, out existingSize))
                            {
                                if(existingSize != Sizes.Both && existingSize != currentSize)
                                    TmpNameAndSize[key] = Sizes.Both;
                            }
                            else
                            {
                                TmpNameAndSize[key] = currentSize;
                            }
                            continue;
                        }
                    }
                    {
                        var wpPhysItem = tuple.Item1 as MyWeaponItemDefinition;
                        if(wpPhysItem != null)
                        {
                            TmpNameAndSize[wpPhysItem.DisplayNameText] = Sizes.HandWeapon;
                            continue;
                        }
                    }
                }
            }

            if(TmpNameAndSize.Count == 0)
                return;

            s.Append("\nUsed by:");

            int limit = 0;
            foreach(var kv in TmpNameAndSize)
            {
                if(++limit > ListLimit)
                {
                    limit--;
                    s.Append("\n  ...and ").Append(TmpNameAndSize.Count - limit).Append(" more");
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

        void TooltipCrafting(StringBuilder s, MyPhysicalItemDefinition physDef)
        {
            TmpBpsThatResult.Clear();
            TmpBpsThatReq.Clear();

            int usedForBlocks = 0;
            int usedForAssembly = 0;

            foreach(var bp in MyDefinitionManager.Static.GetBlueprintDefinitions())
            {
                bool isResult = false;

                foreach(var result in bp.Results)
                {
                    if(result.Id == physDef.Id)
                    {
                        TmpBpsThatResult.Add(bp);
                        isResult = true;
                        break;
                    }
                }

                foreach(var req in bp.Prerequisites)
                {
                    if(req.Id == physDef.Id)
                    {
                        // bps that require same item as they result should be ignored.
                        if(isResult)
                        {
                            TmpBpsThatResult.Remove(bp);
                            break;
                        }

                        TmpBpsThatReq.Add(bp);
                        break;
                    }
                }
            }

            if(TmpBpsThatResult.Count > 0)
            {
                TmpNameAndSize.Clear();
                ComputeBps(TmpBpsThatResult, TmpNameAndSize, ref usedForBlocks, areResults: true);
                if(TmpNameAndSize.Count > 0)
                    AppendCraftList(s, "\nCrafted by:", TmpNameAndSize);
            }
            else
            {
                // TODO find if it's created by vending machines
                // TODO find if it's sold by NPC stations
                s.Append("\nNot Craftable.");
            }

            if(TmpBpsThatReq.Count > 0)
            {
                TmpNameAndSize.Clear();
                ComputeBps(TmpBpsThatReq, TmpNameAndSize, ref usedForAssembly);
                if(TmpNameAndSize.Count > 0)
                    AppendCraftList(s, "\nUsed in:", TmpNameAndSize);
            }

            // TODO: show blocks/items used for crafting if they're less than a few

            if(usedForAssembly > 0)
            {
                s.Append("\nUsed for crafting ").Append(usedForAssembly).Append(" items.");
            }

            if(usedForBlocks > 0)
            {
                s.Append("\nUsed for building ").Append(usedForBlocks).Append(" blocks.");
            }
        }

        static void ComputeBps(HashSet<MyBlueprintDefinitionBase> bps, Dictionary<string, Sizes> dict, ref int usedFor, bool areResults = false)
        {
            foreach(var bp in bps)
            {
                List<MyProductionBlockDefinition> prodList;
                if(BuildInfoMod.Instance.TooltipHandler.TmpBpUsedIn.TryGetValue(bp.Id, out prodList))
                {
                    foreach(var prodDef in prodList)
                    {
                        // HACK: bp results of gas generators or gas tanks are not used, skip
                        if(areResults && (prodDef is MyGasTankDefinition || prodDef is MyOxygenGeneratorDefinition))
                            continue;

                        string name = prodDef.DisplayNameText;

                        Sizes currentSize = (prodDef.CubeSize == MyCubeSize.Small ? Sizes.Small : Sizes.Large);
                        Sizes existingSize;
                        if(dict.TryGetValue(name, out existingSize))
                        {
                            if(existingSize != Sizes.Both && existingSize != currentSize)
                                dict[name] = Sizes.Both;
                        }
                        else
                        {
                            dict[name] = currentSize;
                        }
                    }
                }

                // determine if this is a composite bp generated for a block, which contains the block's "Type/Subtype" as the bp's subtype.
                var composite = bp as MyCompositeBlueprintDefinition;
                if(composite != null && composite.Id.SubtypeName.IndexOf('/') != -1)
                {
                    MyDefinitionId blockId;
                    MyCubeBlockDefinition blockDef;
                    if(MyDefinitionId.TryParse("MyObjectBuilder_" + composite.Id.SubtypeName, out blockId)
                    && MyDefinitionManager.Static.TryGetCubeBlockDefinition(blockId, out blockDef))
                    {
                        usedFor++;
                    }
                }
            }
        }

        static void AppendCraftList(StringBuilder s, string label, Dictionary<string, Sizes> list)
        {
            s.Append(label);
            int limit = 0;

            foreach(var kv in list)
            {
                if(++limit > ListLimit)
                {
                    limit--;
                    s.Append("\n  ...and ").Append(list.Count - limit).Append(" more");
                    break;
                }

                s.Append("\n  ").Append(kv.Key);

                switch(kv.Value)
                {
                    case Sizes.Both: s.Append(" (Small + Large)"); break;
                    case Sizes.Small: s.Append(" (Small)"); break;
                    case Sizes.Large: s.Append(" (Large)"); break;
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

        void SetDescription(MyDefinitionId id, string description)
        {
            MyDefinitionBase def;
            if(MyDefinitionManager.Static.TryGetDefinition(id, out def) && string.IsNullOrWhiteSpace(def.DescriptionText))
            {
                def.DescriptionString = description;
            }
        }
    }
}
