using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using VRage.Game;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class BlueprintTooltips : ModComponent
    {
        const int ListLimit = 6;

        Dictionary<string, Sizes> TmpNameAndSize = new Dictionary<string, Sizes>();

        void DisposeTempObjects()
        {
            TmpNameAndSize = null;
        }

        enum Sizes { Small, Large, Both, HandWeapon }

        StringBuilder SB = new StringBuilder(512);

        public BlueprintTooltips(BuildInfoMod main) : base(main)
        {
            Main.TooltipHandler.Setup += Setup;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.TooltipHandler.Setup -= Setup;
        }

        void Setup(bool generate)
        {
            foreach(var bpBaseDef in MyDefinitionManager.Static.GetBlueprintDefinitions())
            {
                HandleTooltip(bpBaseDef, generate);
            }

            if(generate)
            {
                DisposeTempObjects();
            }
        }

        void HandleTooltip(MyBlueprintDefinitionBase bpBaseDef, bool generate)
        {
            string tooltip = null;
            if(generate)
            {
                // generate tooltips and cache them alone
                SB.Clear();
                GenerateTooltip(SB, bpBaseDef);
                if(SB.Length > 5)
                {
                    tooltip = SB.ToString();
                    Main.TooltipHandler.Tooltips[bpBaseDef.Id] = tooltip;
                }
            }
            else
            {
                // retrieve cached tooltip string
                tooltip = Main.TooltipHandler.Tooltips.GetValueOrDefault(bpBaseDef.Id, null);
            }

            SB.Clear();
            SB.Append(bpBaseDef.DisplayNameText); // get existing text, then replace/append to it as needed

            if(tooltip != null)
            {
                // tooltip likely contains the cached tooltip, get rid of it.
                if(SB.Length >= tooltip.Length)
                {
                    SB.Replace(tooltip, "");
                }

                if(Main.Config.ItemTooltipAdditions.Value)
                {
                    SB.Append(tooltip);
                }
            }

            #region internal info
            const string BpIdLabel = "\nBlueprint Id: ";
            // TODO crafted items too?
            //const string CraftIdLabel = "\nCrafted Id: ";

            if(SB.Length > 0)
            {
                SB.RemoveLineStartsWith(BpIdLabel);
                //TempSB.RemoveLineStartsWith(CraftIdLabel);
            }

            if(Main.Config.InternalInfo.Value)
            {
                int obPrefixLen = "MyObjectBuilder_".Length;
                string typeIdString = bpBaseDef.Id.TypeId.ToString();
                SB.Append(BpIdLabel).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(bpBaseDef.Id.SubtypeName);

                //var resultDef = bpDef.Results...;

                //string typeIdString = bpDef.Id.TypeId.ToString();
                //TempSB.Append(BpIdLabel).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(bpDef.Id.SubtypeName);
            }
            #endregion internal info

            bpBaseDef.DisplayNameEnum = null; // prevent this from being used instead of DisplayNameString
            bpBaseDef.DisplayNameString = SB.ToString();
        }

        void GenerateTooltip(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef)
        {
            s.Append('\n');

            if(!Main.TooltipHandler.IgnoreModItems.Contains(bpBaseDef.Id))
            {
                if(bpBaseDef.Results != null && bpBaseDef.Results.Length > 0)
                {
                    TooltipDescription(s, bpBaseDef);
                    TooltipPhysItemResult(s, bpBaseDef);
                }
            }

            if(!bpBaseDef.Context.IsBaseGame)
            {
                s.Append("\nMod: ").AppendMaxLength(bpBaseDef.Context.ModName, TextGeneration.MOD_NAME_MAX_LENGTH);

                var workshopId = bpBaseDef.Context.GetWorkshopID();
                if(workshopId > 0)
                    s.Append(" (id: ").Append(workshopId).Append(")");
            }

            s.TrimEndWhitespace();
        }

        void TooltipDescription(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef)
        {
            const int MaxWidth = 70;

            var compositeBp = bpBaseDef as MyCompositeBlueprintDefinition;
            if(compositeBp != null)
            {
                MyDefinitionId id;
                if(MyDefinitionId.TryParse("MyObjectBuilder_" + bpBaseDef.Id.SubtypeName, out id))
                {
                    var def = MyDefinitionManager.Static.GetDefinition(id);
                    if(def != null)
                    {
                        string desc = def.DescriptionText;
                        if(!string.IsNullOrWhiteSpace(desc))
                        {
                            s.TrimEndWhitespace().Append("\n\n").AppendWordWrapped(desc, MaxWidth).TrimEndWhitespace().Append("\n");
                        }
                    }
                }
                return;
            }

            var resultDef = MyDefinitionManager.Static.GetDefinition(bpBaseDef.Results[0].Id);
            if(resultDef != null)
            {
                bool appendedDescription = false;
                string desc = resultDef.DescriptionText;
                if(!string.IsNullOrWhiteSpace(desc))
                {
                    s.TrimEndWhitespace().Append("\n\n").AppendWordWrapped(desc, MaxWidth).TrimEndWhitespace().Append("\n");
                    appendedDescription = true;
                }

                var physDef = resultDef as MyPhysicalItemDefinition;
                if(physDef != null)
                {
                    /// NOTE: this is before <see cref="ItemTooltips"/> appends stuff to it.
                    var tooltip = physDef.ExtraInventoryTooltipLine?.ToString();
                    if(!string.IsNullOrWhiteSpace(tooltip))
                    {
                        s.TrimEndWhitespace().Append(appendedDescription ? "\n" : "\n\n").AppendWordWrapped(tooltip, MaxWidth).TrimEndWhitespace().Append("\n");
                    }
                }

                return;
            }
        }

        void TooltipPhysItemResult(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef)
        {
            var physDef = MyDefinitionManager.Static.GetDefinition(bpBaseDef.Results[0].Id) as MyPhysicalItemDefinition;
            if(physDef == null)
                return;

            var it = Main.ItemTooltips;
            it.TooltipBottle(s, physDef, true);
            it.TooltipTool(s, physDef, true);
            it.TooltipWeapon(s, physDef, true);
            it.TooltipAmmo(s, physDef, true);
            it.TooltipConsumable(s, physDef, true);
        }
    }
}
