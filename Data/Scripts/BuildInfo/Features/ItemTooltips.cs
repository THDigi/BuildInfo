using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Utils;

namespace Digi.BuildInfo.Features
{
    public class ItemTooltips : ModComponent
    {
        const string ReqLargeConveyorSymbol = "*";
        const string ReqLargeConveyorSymbolAdd = "\n*";
        const int ListLimit = 6;

        public readonly HashSet<MyDefinitionId> IgnoreModItems = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        readonly Dictionary<string, string> StatDisplayNames = new Dictionary<string, string>()
        {
            ["BatteryCharge"] = "Battery",
        };

        readonly List<OriginalData> OriginalItemData = new List<OriginalData>(16);
        readonly Dictionary<MyDefinitionId, string> Tooltips = new Dictionary<MyDefinitionId, string>(MyDefinitionId.Comparer);
        readonly StringBuilder TempSB = new StringBuilder(1024);

        public ItemTooltips(BuildInfoMod main) : base(main)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(tick >= 30)
            {
                DelayedRegister();
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
            }
        }

        void DelayedRegister()
        {
            if(OriginalItemData.Count > 0)
                throw new Exception("OriginalItemData already has data before init?!");

            SetupItems(init: true);

            Config.InternalInfo.ValueAssigned += ConfigValueChanged;
            Config.ItemTooltipAdditions.ValueAssigned += ConfigValueChanged;
        }

        protected override void UnregisterComponent()
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

            Config.InternalInfo.ValueAssigned -= ConfigValueChanged;
            Config.ItemTooltipAdditions.ValueAssigned -= ConfigValueChanged;
        }

        void ConfigValueChanged(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            if(oldValue != newValue)
                SetupItems();
        }

        void SetupItems(bool init = false)
        {
            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var physDef = def as MyPhysicalItemDefinition;
                if(physDef == null)
                    continue;

                if(init)
                {
                    string tooltip = null;
                    if(physDef.ExtraInventoryTooltipLine != null && physDef.ExtraInventoryTooltipLine.Length > 0)
                        tooltip = physDef.ExtraInventoryTooltipLine.ToString();

                    OriginalItemData.Add(new OriginalData(physDef, tooltip, physDef.IconSymbol));
                }

                HandleSymbol(physDef);
                HandleTooltip(physDef, init);
            }
        }

        void HandleSymbol(MyPhysicalItemDefinition physDef)
        {
            if(!Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
                return;

            if(Config.ItemTooltipAdditions.Value)
            {
                if(physDef.IconSymbol.HasValue)
                {
                    var symbolString = physDef.IconSymbol.Value.String;

                    // only add if it's not there already
                    if(symbolString.IndexOf(ReqLargeConveyorSymbol) == -1)
                        physDef.IconSymbol = MyStringId.GetOrCompute(symbolString + ReqLargeConveyorSymbolAdd);
                }
                else
                {
                    physDef.IconSymbol = MyStringId.GetOrCompute(ReqLargeConveyorSymbol);
                }
            }
            else
            {
                if(physDef.IconSymbol.HasValue)
                {
                    var symbolString = physDef.IconSymbol.Value.String;
                    if(symbolString == ReqLargeConveyorSymbol)
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

        void HandleTooltip(MyPhysicalItemDefinition physDef, bool init)
        {
            string tooltip = null;
            if(init)
            {
                // initialization, generate tooltips and cache them
                TempSB.Clear();
                GenerateTooltip(TempSB, physDef);
                if(TempSB.Length > 0)
                {
                    tooltip = TempSB.ToString();
                    Tooltips[physDef.Id] = tooltip;
                }
            }
            else
            {
                tooltip = Tooltips.GetValueOrDefault(physDef.Id, null);
            }

            var itemTooltipSB = physDef.ExtraInventoryTooltipLine;

            if(tooltip != null)
            {
                if(itemTooltipSB.Length >= tooltip.Length)
                {
                    TempSB.Clear();
                    int sbLen = itemTooltipSB.Length;
                    int scanIndex = 0;
                    for(int i = 0; i < sbLen; i++)
                    {
                        char c = itemTooltipSB[i];

                        if(scanIndex < tooltip.Length && c == tooltip[scanIndex])
                        {
                            scanIndex++;
                            continue;
                        }

                        scanIndex = 0;
                        TempSB.Append(c);
                    }

                    itemTooltipSB.Clear().AppendStringBuilder(TempSB);
                }

                if(Config.ItemTooltipAdditions.Value)
                {
                    itemTooltipSB.Append(tooltip);
                }
            }

            #region internal info type+subtype
            const string IdLabel = "\nId: ";

            if(itemTooltipSB.Length > 0)
            {
                RemoveLineStartsWith(itemTooltipSB, IdLabel);
            }

            if(Config.InternalInfo.Value)
            {
                int obPrefixLen = "MyObjectBuilder_".Length;
                string typeIdString = physDef.Id.TypeId.ToString();
                itemTooltipSB.Append(IdLabel).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(physDef.Id.SubtypeName);
            }
            #endregion
        }

        void GenerateTooltip(StringBuilder s, MyPhysicalItemDefinition physDef)
        {
            if(!IgnoreModItems.Contains(physDef.Id))
            {
                TooltipConsumable(s, physDef);
                TooltipCrafting(s, physDef);
            }

            if(!physDef.Context.IsBaseGame)
            {
                s.Append("\nMod: ").AppendMaxLength(physDef.Context.ModName, TextGeneration.MOD_NAME_MAX_LENGTH);

                var workshopId = physDef.Context.GetWorkshopID();
                if(workshopId > 0)
                    s.Append(" (id: ").Append(workshopId).Append(")");
            }

            if(Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
            {
                s.Append("\n* Item can not pass through small conveyors.");
            }
        }

        void TooltipConsumable(StringBuilder s, MyPhysicalItemDefinition physDef)
        {
            var consumable = physDef as MyConsumableItemDefinition;
            if(consumable != null && consumable.Stats.Count > 0)
            {
                s.Append("\nConsumption: ");

                if(consumable.Stats.Count == 1)
                {
                    var stat = consumable.Stats[0];
                    s.Append(stat.Value > 0 ? "+" : "").ProportionToPercent(stat.Value * stat.Time, 2).Append(" ").Append(StatDisplayNames.GetValueOrDefault(stat.Name, stat.Name)).Append(" over ").TimeFormat(stat.Time);
                }
                else
                {
                    foreach(var stat in consumable.Stats)
                    {
                        s.Append("\n  ").Append(stat.Value > 0 ? "+" : "").ProportionToPercent(stat.Value * stat.Time, 2).Append(" ").Append(StatDisplayNames.GetValueOrDefault(stat.Name, stat.Name)).Append(" over ").TimeFormat(stat.Time);
                    }
                }
            }
        }

        enum Sizes { Small, Large, Both }

        void TooltipCrafting(StringBuilder s, MyPhysicalItemDefinition physDef)
        {
            var bpsThatResult = new HashSet<MyBlueprintDefinitionBase>();
            var bpsThatReq = new HashSet<MyBlueprintDefinitionBase>();
            var createdBy = new Dictionary<string, Sizes>();
            var usedBy = new Dictionary<string, Sizes>();

            int usedForBlocks = 0;
            int usedForAssembly = 0;

            foreach(var bp in MyDefinitionManager.Static.GetBlueprintDefinitions())
            {
                bool isResult = false;

                foreach(var result in bp.Results)
                {
                    if(result.Id == physDef.Id)
                    {
                        bpsThatResult.Add(bp);
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
                            bpsThatResult.Remove(bp);
                            break;
                        }

                        bpsThatReq.Add(bp);
                        break;
                    }
                }
            }

            if(bpsThatResult.Count > 0)
            {
                ComputeBps(bpsThatResult, createdBy, ref usedForBlocks);
            }

            if(bpsThatReq.Count > 0)
            {
                ComputeBps(bpsThatReq, usedBy, ref usedForAssembly);
            }

            if(createdBy.Count > 0)
            {
                AppendCraftList(s, "\nCreated by:", createdBy);
            }

            if(usedBy.Count > 0)
            {
                AppendCraftList(s, "\nUsed in:", usedBy);
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

        static void ComputeBps(HashSet<MyBlueprintDefinitionBase> bps, Dictionary<string, Sizes> dict, ref int usedFor)
        {
            foreach(var bp in bps)
            {
                foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    var prodDef = def as MyProductionBlockDefinition;
                    if(prodDef == null)
                        continue;

                    foreach(var bpClass in prodDef.BlueprintClasses)
                    {
                        if(bpClass.ContainsBlueprint(bp))
                        {
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
                            break;
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

        static bool RemoveLineStartsWith(StringBuilder sb, string prefix)
        {
            int prefixIndex = sb.IndexOf(prefix);
            if(prefixIndex == -1)
                return false;

            int endIndex = -1;
            if(prefixIndex + prefix.Length < sb.Length)
            {
                endIndex = sb.IndexOf('\n', prefixIndex + prefix.Length);
                // newlines are at the start of the line for prefixes so don't add the trailing newline too
            }

            if(endIndex == -1)
                endIndex = sb.Length;

            sb.Remove(prefixIndex, endIndex - prefixIndex);
            return true;
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
