using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ConfigLib;
using Sandbox.Definitions;
using VRage.Utils;

// DEBUG TODO: get rid of keen's notifications and use textAPI

namespace Digi.BuildInfo.Features
{
    public class ItemTooltips : ModComponent
    {
        // NOTE: must start with newline character
        const string ModLabel = "\nMod: ";
        const string IdLabel = "\nId: ";
        const string ReqLargeConveyorLabel = "\n* Item can not pass through small conveyors.";

        const string ReqLargeConveyorSymbol = "*";
        const string ReqLargeConveyorSymbolAdd = "\n*";

        List<OriginalData> OriginalItemData = new List<OriginalData>();

        public ItemTooltips(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            RefreshTooltips(storeOriginal: true);

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

        void RefreshTooltips(bool storeOriginal = false)
        {
            bool internalInfo = Config.InternalInfo.Value;
            bool tooltipAdditions = Config.ItemTooltipAdditions.Value;

            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var physDef = def as MyPhysicalItemDefinition;
                if(physDef == null)
                    continue;

                if(storeOriginal)
                {
                    if(physDef.IconSymbol.HasValue || (physDef.ExtraInventoryTooltipLine != null && physDef.ExtraInventoryTooltipLine.Length > 0))
                    {
                        OriginalItemData.Add(new OriginalData(physDef, physDef.ExtraInventoryTooltipLine?.ToString(), physDef.IconSymbol));
                    }
                }

                #region Symbol handling
                if(Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
                {
                    if(tooltipAdditions)
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
                #endregion Symbol handling

                #region Tooltip handling
                var sb = physDef.ExtraInventoryTooltipLine;

                RemoveLineStartsWith(sb, ReqLargeConveyorLabel);
                RemoveLineStartsWith(sb, IdLabel);
                RemoveLineStartsWith(sb, ModLabel);

                // add everything in the right order

                if(tooltipAdditions)
                {
                    if(!physDef.Context.IsBaseGame)
                    {
                        sb.Append(ModLabel).AppendMaxLength(physDef.Context.ModName, TextGeneration.MOD_NAME_MAX_LENGTH);

                        var workshopId = physDef.Context.GetWorkshopID();
                        if(workshopId > 0)
                            sb.Append(" (id: ").Append(workshopId).Append(")");
                    }
                }

                if(internalInfo)
                {
                    sb.Append(IdLabel).Append(physDef.Id.TypeId.ToString()).Append("/").Append(physDef.Id.SubtypeName);
                }

                if(tooltipAdditions)
                {
                    if(Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
                    {
                        sb.Append(ReqLargeConveyorLabel);
                    }
                }
                #endregion Tooltip handling
            }
        }

        void RemoveLineStartsWith(StringBuilder sb, string prefix)
        {
            int prefixIndex = sb.IndexOf(prefix);
            if(prefixIndex == -1)
                return;

            int endIndex = -1;
            if(prefixIndex + prefix.Length < sb.Length)
            {
                endIndex = sb.IndexOf('\n', prefixIndex + prefix.Length);
                // newlines are at the start of the line for prefixes so don't add the trailing newline too
            }

            if(endIndex == -1)
                endIndex = sb.Length;

            sb.Remove(prefixIndex, endIndex - prefixIndex);
        }

        void ConfigValueChanged(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            if(oldValue == newValue)
                return;

            RefreshTooltips();
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
