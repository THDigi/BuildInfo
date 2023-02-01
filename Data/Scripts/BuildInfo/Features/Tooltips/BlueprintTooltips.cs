using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using VRage.Game;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class BlueprintTooltips : ModComponent
    {
        List<MyBlueprintDefinitionBase.ProductionInfo> StackedBlueprints = new List<MyBlueprintDefinitionBase.ProductionInfo>();

        void DisposeTempObjects()
        {
            StackedBlueprints = null;
        }

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
            foreach(MyBlueprintDefinitionBase bpBaseDef in MyDefinitionManager.Static.GetBlueprintDefinitions())
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
            const string BpIdLabel = "\n\nBlueprint Id: ";
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
                SB.TrimEndWhitespace().Append(BpIdLabel).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(bpBaseDef.Id.SubtypeName);

                //var resultDef = bpDef.Results...;

                //string typeIdString = bpDef.Id.TypeId.ToString();
                //TempSB.Append(BpIdLabel).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(bpDef.Id.SubtypeName);
            }
            #endregion internal info

            SB.TrimEndWhitespace().Append("\n"); // for the progress % to be on its own line, when hovering queued item in assembler

            bpBaseDef.DisplayNameEnum = null; // prevent this from being used instead of DisplayNameString
            bpBaseDef.DisplayNameString = SB.ToString();
        }

        void GenerateTooltip(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef)
        {
            s.Append('\n');

            BuildTime(s, bpBaseDef);

            if(!Main.TooltipHandler.IgnoreModItems.Contains(bpBaseDef.Id))
            {
                if(bpBaseDef.Results != null && bpBaseDef.Results.Length > 0)
                {
                    TooltipDescription(s, bpBaseDef);
                }
            }

            TooltipHandler.AppendModInfo(s, bpBaseDef);

            s.TrimEndWhitespace();
        }

        void BuildTime(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef)
        {
            List<MyProductionBlockDefinition> prodDefs;
            if(Main.TooltipHandler.TmpBpUsedIn.TryGetValue(bpBaseDef.Id, out prodDefs))
            {
                bool inRefinery = false;
                bool inAssembler = false;

                float refineMin = float.MaxValue;
                float refineMax = float.MinValue;

                float assembleMin = float.MaxValue;
                float assembleMax = float.MinValue;

                foreach(MyProductionBlockDefinition prodDef in prodDefs)
                {
                    var assemblerDef = prodDef as MyAssemblerDefinition;
                    if(assemblerDef != null)
                    {
                        inAssembler = true;
                        float time = Hardcoded.Assembler_BpProductionTime(bpBaseDef, assemblerDef);
                        assembleMin = Math.Min(assembleMin, time);
                        assembleMax = Math.Max(assembleMax, time);
                        continue;
                    }

                    var refineryDef = prodDef as MyRefineryDefinition;
                    if(refineryDef != null)
                    {
                        inRefinery = true;
                        float time = Hardcoded.Refinery_BpProductionTime(bpBaseDef, refineryDef);
                        refineMin = Math.Min(refineMin, time);
                        refineMax = Math.Max(refineMax, time);
                        continue;
                    }
                }

                if(inRefinery != inAssembler)
                {
                    float min, max;

                    if(inRefinery)
                    {
                        min = refineMin;
                        max = refineMax;
                    }
                    else //if(inAssembler)
                    {
                        min = assembleMin;
                        max = assembleMax;
                    }

                    s.Append("Stock build time: ");
                    if(Math.Abs(min - max) > 0.1f)
                        s.TimeFormat(min).Append(" ~ ").TimeFormat(max).Append('\n');
                    else
                        s.TimeFormat(min).Append('\n');
                }
            }
        }

        void TooltipDescription(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef)
        {
            const int MaxWidth = 70;
            MyDefinitionBase def;

            // likely a block blueprint which adds all its components to queue.
            MyCompositeBlueprintDefinition compositeBp = bpBaseDef as MyCompositeBlueprintDefinition;
            if(compositeBp != null)
            {
                StackedBlueprints.Clear();
                compositeBp.GetBlueprints(StackedBlueprints);

                // the subtypeId of the blueprint is the block's Id in "typeId/subtypeId" format, so we can use that directly to get the block.
                MyDefinitionId id;
                if(MyDefinitionId.TryParse(bpBaseDef.Id.SubtypeName, out id) && MyDefinitionManager.Static.TryGetDefinition(id, out def))
                {
                    //string desc = def.DescriptionText;
                    //if(!string.IsNullOrWhiteSpace(desc))
                    //{
                    //    s.TrimEndWhitespace().Append("\n\n").AppendWordWrapped(desc, MaxWidth).TrimEndWhitespace().Append("\n\n");
                    //}

                    s.Append("Adds block's components to queue:\n");
                }
                else
                {
                    s.Append("Adds to queue:\n");
                }

                foreach(MyBlueprintDefinitionBase.ProductionInfo bpInfo in StackedBlueprints)
                {
                    s.Append("• ").RoundedNumber((float)bpInfo.Amount, 2).Append("x ").DefinitionName(bpInfo.Blueprint).Append('\n');
                }

                StackedBlueprints.Clear();
                return;
            }

            if(bpBaseDef.Results.Length > 1)
            {
                s.TrimEndWhitespace().Append("\nMultiple results:\n");

                foreach(MyBlueprintDefinitionBase.Item result in bpBaseDef.Results)
                {
                    s.Append("• ").RoundedNumber((float)result.Amount, 2).Append("x ");

                    if(!MyDefinitionManager.Static.TryGetDefinition(result.Id, out def))
                        def = null;

                    s.DefinitionName(def).Append('\n');

                    // no tooltips as it would be too long probably.
                }
            }
            else
            {
                if(MyDefinitionManager.Static.TryGetDefinition(bpBaseDef.Results[0].Id, out def))
                {
                    bool appendedDescription = false;
                    string desc = def.DescriptionText;
                    if(!string.IsNullOrWhiteSpace(desc))
                    {
                        s.TrimEndWhitespace().Append('\n').AppendWordWrapped(desc, MaxWidth).TrimEndWhitespace().Append('\n');
                        appendedDescription = true;
                    }

                    MyPhysicalItemDefinition physDef = def as MyPhysicalItemDefinition;
                    if(physDef != null)
                    {
                        /// NOTE: this is before <see cref="ItemTooltips"/> appends stuff to it.
                        string tooltip = physDef.ExtraInventoryTooltipLine?.ToString();
                        string bpTooltip = bpBaseDef.DisplayNameText;

                        // don't add this if another mod already did
                        if(!string.IsNullOrWhiteSpace(tooltip) && !bpTooltip.Contains(tooltip))
                        {
                            s.TrimEndWhitespace().Append(appendedDescription ? "\n" : "\n\n").AppendWordWrapped(tooltip, MaxWidth).TrimEndWhitespace().Append('\n');
                        }

                        // only specific BI's item tooltips added to the blueprint name
                        ItemTooltips it = Main.ItemTooltips;
                        it.TooltipBottle(s, physDef, true);
                        it.TooltipTool(s, physDef, true);
                        it.TooltipWeapon(s, physDef, true);
                        it.TooltipAmmo(s, physDef, true);
                        it.TooltipConsumable(s, physDef, true);
                    }
                }
            }
        }
    }
}
