using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Definitions;
using VRage.Utils;

// DEBUG TODO: get rid of keen's notifications and use textAPI

namespace Digi.BuildInfo.Features
{
    public class ItemTooltips : ModComponent
    {
        const string ModLabel = "\nMod: ";
        const string IdLabel = "\nId: ";
        const string ReqLargeConveyorLabel = "\n\n* Item can not pass through small conveyors.";

        public ItemTooltips(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.NONE;
        }

        protected override void RegisterComponent()
        {
            SetItemsTooltip();
            UpdateIdTooltip(Config.InternalInfo.Value);

            Config.InternalInfo.ValueAssigned += InternalInfoChanged;
        }

        protected override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Config.InternalInfo.ValueAssigned += InternalInfoChanged;

            // apparently the items get reset by themselves so I don't have to reset them... yet.
        }

        void SetItemsTooltip()
        {
            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var physDef = def as MyPhysicalItemDefinition;
                if(physDef == null)
                    continue;

                var sb = physDef.ExtraInventoryTooltipLine;

                if(!physDef.Context.IsBaseGame)
                {
                    sb.Append(ModLabel).AppendMaxLength(physDef.Context.ModName, TextGeneration.MOD_NAME_MAX_LENGTH);

                    var workshopId = physDef.Context.GetWorkshopID();
                    if(workshopId > 0)
                        sb.Append(" (id: ").Append(workshopId).Append(")");
                }

                if(Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
                {
                    sb.Append(ReqLargeConveyorLabel);

                    if(physDef.IconSymbol.HasValue)
                        physDef.IconSymbol = MyStringId.GetOrCompute($"{physDef.IconSymbol.Value.String}\n*");
                    else
                        physDef.IconSymbol = MyStringId.GetOrCompute("*");
                }
            }
        }

        void UpdateIdTooltip(bool add)
        {
            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var physDef = def as MyPhysicalItemDefinition;
                if(physDef == null)
                    continue;

                var sb = physDef.ExtraInventoryTooltipLine;

                if(add)
                {
                    int foundLabelIndex = sb.IndexOf(IdLabel);
                    if(foundLabelIndex != -1)
                        continue;

                    if(!Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
                        sb.Append('\n');

                    sb.Append(IdLabel).Append(physDef.Id.TypeId.ToString()).Append("/").Append(physDef.Id.SubtypeName);
                }
                else
                {
                    int foundIndex = sb.IndexOf(IdLabel);
                    if(foundIndex == -1)
                        continue;

                    if(!Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
                        foundIndex -= 1; // include the extra \n

                    int nextLine = sb.IndexOf('\n', foundIndex + IdLabel.Length);
                    if(nextLine == -1)
                    {
                        //string textAfter = sb.ToString(nextLine, sb.Length - nextLine);
                        //sb.Length = foundIndex;
                        //sb.Append(textAfter);
                        sb.Remove(foundIndex, sb.Length - foundIndex);
                    }
                    else
                    {
                        // nothing past, just reduce size.
                        sb.Length = foundIndex;
                    }
                }
            }
        }

        void InternalInfoChanged(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            if(oldValue == newValue)
                return;

            UpdateIdTooltip(newValue);
        }
    }
}
