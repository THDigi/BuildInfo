﻿using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class BlueprintTooltips : ModComponent
    {
        List<MyBlueprintDefinitionBase.ProductionInfo> StackedBlueprints = new List<MyBlueprintDefinitionBase.ProductionInfo>();

        void DisposeTempObjects()
        {
            // still used by update now
            //StackedBlueprints = null;
        }

        StringBuilder SB = new StringBuilder(512);

        bool InProductionTab = false;
        MyBlueprintClassDefinition BuildPlannerBPClass;

        public BlueprintTooltips(BuildInfoMod main) : base(main)
        {
            Main.TooltipHandler.Setup += Setup;

            BuildPlannerBPClass = MyDefinitionManager.Static.GetBlueprintClass(Hardcoded.BuildPlanner_BPClassSubtype);
            if(BuildPlannerBPClass != null)
            {
                Main.GUIMonitor.FirstScreenOpen += FirstScreenOpen;
                Main.GUIMonitor.LastScreenClose += LastScreenClose;

                Main.GameConfig.FirstSpawn += FirstSpawn;
            }
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Main.TooltipHandler.Setup -= Setup;

            Main.GUIMonitor.FirstScreenOpen -= FirstScreenOpen;
            Main.GUIMonitor.LastScreenClose -= LastScreenClose;

            Main.GameConfig.FirstSpawn -= FirstSpawn;

            if(!Main.ComponentsRegistered)
                return;
        }

        void FirstScreenOpen()
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        void LastScreenClose()
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
            InProductionTab = false;
        }

        public override void UpdateAfterSim(int tick)
        {
            if(MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.Production)
            {
                if(!InProductionTab)
                {
                    InProductionTab = true;
                    ProcessBuildPlannerGeneratedBlueprints();
                }
            }
            else
            {
                InProductionTab = false;
            }
        }

        void Setup(bool generate)
        {
            foreach(MyBlueprintDefinitionBase bpBaseDef in MyDefinitionManager.Static.GetBlueprintDefinitions())
            {
                try
                {
                    HandleTooltip(bpBaseDef, generate);
                }
                catch(Exception e)
                {
                    string msg = $"Error setting up tooltips for blueprint: {bpBaseDef?.Id.ToString()}";
                    Log.Error($"{msg}\n{e}", msg);
                }
            }

            if(generate)
            {
                DisposeTempObjects();
            }
        }

        void HandleTooltip(MyBlueprintDefinitionBase bpBaseDef, bool generate)
        {
            bool usedByAssemblers = false;
            HashSet<MyProductionBlockDefinition> usedByBlocks;
            if(Main.TooltipHandler.TmpBpUsedIn.TryGetValue(bpBaseDef.Id, out usedByBlocks))
            {
                foreach(MyProductionBlockDefinition prodDef in usedByBlocks)
                {
                    if(prodDef is MyAssemblerDefinition) // includes survival kits
                    {
                        usedByAssemblers = true;
                        break;
                    }
                }
            }

            // refinery-only blueprints aren't visible anywhere in the game UI
            if(!usedByAssemblers)
                return;

            string tooltip = null;
            if(generate)
            {
                // generate tooltips and cache them alone
                SB.Clear();
                GenerateTooltip(SB, bpBaseDef, usedByBlocks);
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
            SB.Append(bpBaseDef.DisplayNameText).TrimEndWhitespace(); // get existing text, then replace/append to it as needed

            if(tooltip != null)
            {
                // tooltip likely contains the cached tooltip, get rid of it.
                if(SB.Length >= tooltip.Length)
                {
                    SB.Replace(tooltip, "");
                    SB.TrimEndWhitespace();
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
                SB.TrimEndWhitespace();
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

            // for the progress % to be on its own line, when hovering queued item in assembler
            // NOTE: sensitive to newlines being propagated every time config is updated, re-test when making changes by toggling internal info or something.
            //if(!(bpBaseDef is MyCompositeBlueprintDefinition) && SB.IndexOf('\n') != -1) // only if it's not a composite and the name has tooltips
            //{
            //    SB.TrimEndWhitespace().Append("\n");
            //}

            bpBaseDef.DisplayNameEnum = null; // prevent this from being used instead of DisplayNameString
            bpBaseDef.DisplayNameString = SB.ToString();
        }

        void GenerateTooltip(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef, HashSet<MyProductionBlockDefinition> usedByBlocks)
        {
            if(Main.TooltipHandler.IgnoreModItems.Contains(bpBaseDef.Id))
                return;

            s.Append('\n');

            BuildTime(s, bpBaseDef, usedByBlocks);

            Requirements(s, bpBaseDef, usedByBlocks);

            ResultDescription(s, bpBaseDef);

            TooltipHandler.AppendModInfo(s, bpBaseDef);

            s.TrimEndWhitespace();
        }

        void Requirements(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef, HashSet<MyProductionBlockDefinition> usedByBlocks)
        {
            MyFixedPoint materialMultiplierMin = MyFixedPoint.MaxValue;
            MyFixedPoint materialMultiplierMax = MyFixedPoint.MinValue;
            bool assigned = false;

            if(usedByBlocks != null)
            {
                foreach(MyProductionBlockDefinition prodDef in usedByBlocks)
                {
                    if(prodDef is MyAssemblerDefinition)
                    {
                        // HACK: from MyTerminalProductionController.AddComponentPrerequisites()
                        MyFixedPoint mul = (MyFixedPoint)(1f / Hardcoded.Assembler_GetEfficiencyMultiplierForBlueprint(prodDef, bpBaseDef));

                        materialMultiplierMin = MyFixedPoint.Min(mul, materialMultiplierMin);
                        materialMultiplierMax = MyFixedPoint.Max(mul, materialMultiplierMax);
                        assigned = true;
                    }
                }
            }

            if(!assigned)
            {
                return; // don't add anything if it's not used by at least one assembler

                //materialMultiplierMin = (MyFixedPoint)MyAPIGateway.Session.AssemblerEfficiencyMultiplier;
                //materialMultiplierMax = materialMultiplierMin;
            }

            s.Append("Requires:\n");

            foreach(MyBlueprintDefinitionBase.Item item in bpBaseDef.Prerequisites)
            {
                string name;

                MyPhysicalItemDefinition itemDef;
                if(MyDefinitionManager.Static.TryGetPhysicalItemDefinition(item.Id, out itemDef))
                {
                    name = itemDef.DisplayNameText;
                }
                else
                {
                    name = $"(ERROR, not found: '{item.Id}')";

                    if(Main.Config.ModderHelpAlerts.Value && bpBaseDef.Context.IsLocal())
                    {
                        Main.ModderHelpMain.ModProblem(bpBaseDef, $"The pre-requisite '{item.Id}' does not exist!");
                    }
                }

                s.Append("  ");

                float min = (float)(item.Amount * materialMultiplierMin);
                float max = (float)(item.Amount * materialMultiplierMax);

                if(Math.Abs(max - min) > 0.01)
                {
                    s.Number(min).Append("x ~ ").Number(max).Append("x ");
                }
                else
                {
                    s.Number(max).Append("x ");
                }

                s.Append(name);

                if(!Main.TooltipHandler.TmpHasBP.Contains(item.Id))
                    s.Append(" (Not Craftable)");

                s.Append('\n');
            }
        }

        void BuildTime(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef, HashSet<MyProductionBlockDefinition> usedByBlocks)
        {
            bool inRefinery = false;
            bool inAssembler = false;

            float refineMin = float.MaxValue;
            float refineMax = float.MinValue;

            float assembleMin = float.MaxValue;
            float assembleMax = float.MinValue;

            foreach(MyProductionBlockDefinition prodDef in usedByBlocks)
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

            if(!inRefinery && !inAssembler)
            {
                s.Append("ERROR: Blueprint in neither refinery or assembler...\n" +
                         "  Something is wrong, please report to BuildInfo author!\n");
                return;
            }

            float min, max;

            if(inRefinery && inAssembler)
            {
                min = Math.Min(refineMin, assembleMin);
                max = Math.Max(refineMax, assembleMax);
            }
            else if(inRefinery)
            {
                min = refineMin;
                max = refineMax;
            }
            else //if(inAssembler)
            {
                min = assembleMin;
                max = assembleMax;
            }

            s.Append("Build time: ");
            if(Math.Abs(min - max) > 0.01f)
                s.TimeFormat(min).Append(" ~ ").TimeFormat(max).Append('\n');
            else
                s.TimeFormat(min).Append('\n');
        }

        void ResultDescription(StringBuilder s, MyBlueprintDefinitionBase bpBaseDef)
        {
            if(bpBaseDef.Results == null || bpBaseDef.Results.Length <= 0)
            {
                s.Append("ERROR: Has no results!\n");

                if(Main.Config.ModderHelpAlerts.Value && bpBaseDef.Context.IsLocal())
                {
                    Main.ModderHelpMain.ModProblem(bpBaseDef, $"Has no results!");
                }
                return;
            }

            if(Main.TooltipHandler.IgnoreModItems.Contains(bpBaseDef.Results[0].Id))
                return;

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
                s.Append("Results in multiple items:\n");

                foreach(MyBlueprintDefinitionBase.Item result in bpBaseDef.Results)
                {
                    s.Append("• ").RoundedNumber((float)result.Amount, 2).Append("x ");

                    if(!MyDefinitionManager.Static.TryGetDefinition(result.Id, out def))
                    {
                        def = null;

                        if(Main.Config.ModderHelpAlerts.Value && bpBaseDef.Context.IsLocal())
                        {
                            Main.ModderHelpMain.ModProblem(bpBaseDef, $"The result '{result.Id}' does not exist!");
                        }
                    }

                    s.DefinitionName(def, result.Id).Append('\n');

                    // no tooltips as it would be too long probably.
                }
            }
            else
            {
                if(MyDefinitionManager.Static.TryGetDefinition(bpBaseDef.Results[0].Id, out def))
                {
                    int lengthBefore = s.Length;

                    string defDisplayName = def.DisplayNameText;

                    // get only the first line in the display name
                    string bpDisplayName = bpBaseDef.DisplayNameText;
                    int newLineIdx = bpDisplayName.IndexOf('\n');
                    if(newLineIdx != -1)
                        bpDisplayName = bpDisplayName.Substring(0, newLineIdx);

                    if(defDisplayName != null && !bpDisplayName.Equals(defDisplayName, StringComparison.OrdinalIgnoreCase))
                    {
                        s.Append("Result item: ").Append(defDisplayName).Append("\n");
                    }
                    else
                    {
                        s.Append("Result item info:\n");
                    }

                    int lengthCheck = s.Length;

                    const int MaxWidth = 70;
                    string desc = def.DescriptionText;
                    if(!string.IsNullOrWhiteSpace(desc))
                    {
                        s.Append('"').AppendWordWrapped(desc, MaxWidth).TrimEndWhitespace().Append('"').Append('\n');
                    }

                    MyPhysicalItemDefinition physDef = def as MyPhysicalItemDefinition;
                    if(physDef != null)
                    {
                        /// NOTE: this is before <see cref="ItemTooltips"/> appends stuff to it.
                        string tooltip = physDef.ExtraInventoryTooltipLine?.ToString().Trim(); // game adds some leading newlines
                        string bpTooltip = bpBaseDef.DisplayNameText;

                        s.TrimEndWhitespace().Append('\n');

                        // don't add this if another mod already did
                        if(!string.IsNullOrWhiteSpace(tooltip) && !bpTooltip.Contains(tooltip))
                        {
                            s.Append('"').AppendWordWrapped(tooltip, MaxWidth).TrimEndWhitespace().Append('"').Append('\n');
                        }

                        // only specific BI's item tooltips added to the blueprint name
                        ItemTooltips it = Main.ItemTooltips;
                        it.TooltipConsumable(s, physDef, true);
                        it.TooltipBottle(s, physDef, true);
                        it.TooltipTool(s, physDef, true);
                        it.TooltipWeapon(s, physDef, true);
                        it.TooltipAmmo(s, physDef, true);
                        it.TooltipUsedIn(s, physDef, true);
                        it.TooltipBoughtOrSold(s, physDef, true);
                        it.TooltipCrafting(s, physDef, true);
                    }

                    // erase the result info if nothing was appended
                    if(s.Length == lengthCheck)
                        s.Length = lengthBefore;
                }
                else
                {
                    s.Append("Result item: ").DefinitionName(null, bpBaseDef.Results[0].Id).Append('\n');

                    if(Main.Config.ModderHelpAlerts.Value && def.Context.IsLocal())
                    {
                        Main.ModderHelpMain.ModProblem(def, $"Results in an item that does not exist which means it will actually build the first item in definitions.");
                    }
                }
            }
        }

        void ProcessBuildPlannerGeneratedBlueprints()
        {
            foreach(MyBlueprintDefinitionBase bpBaseDef in BuildPlannerBPClass)
            {
                // player was alerted of this crashy case on first spawn
                if(bpBaseDef == null)
                    continue;

                // from MyCharacter.UpdateBuildPlanner() - all the blueprints for buildplanner are generated by the vanilla game.
                // mods would have to manually add to this and who knows why they'd do that, ignoring those.
                if(bpBaseDef.Context == null || !bpBaseDef.Context.IsBaseGame)
                    continue;

                MyCompositeBlueprintDefinition compositeBp = bpBaseDef as MyCompositeBlueprintDefinition;
                if(compositeBp == null)
                    continue;

                // ignore ones already modified
                if(bpBaseDef.DisplayNameEnum == null && bpBaseDef.DisplayNameString != null && bpBaseDef.DisplayNameString.IndexOf('\n') != -1)
                    continue;

                StackedBlueprints.Clear();
                compositeBp.GetBlueprints(StackedBlueprints);

                SB.Clear();

                MyDefinitionId blockId;
                MyCubeBlockDefinition blockDef;
                if(MyDefinitionId.TryParse(bpBaseDef.Id.SubtypeName.Substring(Hardcoded.BuildPlanner_BPSubtypePrefix.Length), out blockId)
                && MyDefinitionManager.Static.TryGetCubeBlockDefinition(blockId, out blockDef))
                {
                    int plannerItems = 0;

                    foreach(MyBlueprintDefinitionBase.ProductionInfo bpInfo in StackedBlueprints)
                    {
                        foreach(MyBlueprintDefinitionBase.Item resultInfo in bpInfo.Blueprint.Results)
                        {
                            plannerItems += (int)(bpInfo.Amount * resultInfo.Amount);
                        }
                    }

                    int blockItems = 0;
                    foreach(var comp in blockDef.Components)
                    {
                        blockItems += comp.Count;
                    }

                    if(plannerItems < blockItems)
                    {
                        SB.Append("(Partial) ");
                    }
                }

                SB.Append(bpBaseDef.DisplayNameText);
                SB.Append('\n');

                BuildTime(SB, bpBaseDef, Main.TooltipHandler.BlueprintPlannerBlocks);

                SB.Append("Adds to queue:\n");
                foreach(MyBlueprintDefinitionBase.ProductionInfo bpInfo in StackedBlueprints)
                {
                    SB.Append("• ").RoundedNumber((float)bpInfo.Amount, 2).Append("x ").DefinitionName(bpInfo.Blueprint).Append('\n');
                }

                SB.TrimEndWhitespace();

                StackedBlueprints.Clear();

                bpBaseDef.DisplayNameEnum = null; // prevent this from being used instead of DisplayNameString
                bpBaseDef.DisplayNameString = SB.ToString();
            }
        }

        void FirstSpawn()
        {
            foreach(MyBlueprintDefinitionBase bpBaseDef in BuildPlannerBPClass)
            {
                if(bpBaseDef == null)
                {
                    Utils.ShowColoredChatMessage(BuildInfoMod.ModName,
                        "BuildPlanner has a null blueprint! Please clear your BuildPlanner in G-menu to avoid crashing in Production terminal.",
                        senderColor: Color.Red);

                    Log.Info("Warned player about BuildPlanner having null blueprint.");

                    break;
                }
            }
        }
    }
}
