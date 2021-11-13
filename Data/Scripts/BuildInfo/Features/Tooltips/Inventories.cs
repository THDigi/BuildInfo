using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Localization;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.Tooltips
{
    // TODO: config setting?
    public class Inventories : ModComponent
    {
        const int TooltipMaxPerType = 5; // shown if exactly this or less
        const int TooltipMaxIfMore = 3; // shown if there's more than TooltipMaxPerType (so that the message about more can fit)

        readonly bool DebugLog = false;

        Dictionary<MyObjectBuilderType, string> Icons = new Dictionary<MyObjectBuilderType, string>(MyObjectBuilderType.Comparer)
        {
            [typeof(MyObjectBuilder_Ore)] = @"Textures\GUI\Icons\filter_ore.dds",
            [typeof(MyObjectBuilder_Ingot)] = @"Textures\GUI\Icons\filter_ingot.dds",
            [typeof(MyObjectBuilder_Component)] = @"Textures\GUI\Icons\FilterComponent.dds",
            [typeof(MyObjectBuilder_GasContainerObject)] = Utils.GetModFullPath(@"Textures\FilterBottle.dds"),
            [typeof(MyObjectBuilder_OxygenContainerObject)] = Utils.GetModFullPath(@"Textures\FilterBottle.dds"),
        };

        HashSet<MyDefinitionId> IgnoreItems = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer)
        {
            new MyDefinitionId(typeof(MyObjectBuilder_Ingot), "Scrap"), // it's declared as backwards compatible item
        };

        StringBuilder Tooltip = new StringBuilder(512);
        Dictionary<MyObjectBuilderType, List<string>> ItemsByType = new Dictionary<MyObjectBuilderType, List<string>>(MyObjectBuilderType.Comparer);

        HashSet<MyObjectBuilderType> InputTypes = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer);
        HashSet<MyObjectBuilderType> OutputTypes = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer);

        bool Processed = false;

        public Inventories(BuildInfoMod main) : base(main)
        {
            // computing in Register() is too late for spawned entities, and LoadData() might be too early, so instead compute when first entity is created, hopefully this is early enough.
            MyEntities.OnEntityCreate += EntityCreated;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            MyEntities.OnEntityCreate -= EntityCreated;
        }

        void EntityCreated(MyEntity ent)
        {
            try
            {
                MyEntities.OnEntityCreate -= EntityCreated;

                if(Processed)
                    return;

                Processed = true;

                foreach(MyCubeBlockDefinition blockDef in Main.Caches.BlockDefs)
                {
                    MyProductionBlockDefinition productionDef = blockDef as MyProductionBlockDefinition;
                    if(productionDef != null)
                    {
                        // non-standard definition constraint, let's not mess with it.
                        if(productionDef.InputInventoryConstraint == null || productionDef.OutputInventoryConstraint == null
                        || !productionDef.InputInventoryConstraint.IsWhitelist || !productionDef.OutputInventoryConstraint.IsWhitelist)
                            continue;

                        InputTypes.Clear();
                        OutputTypes.Clear();

                        if(DebugLog)
                            Log.Info($"{blockDef.Id}:");

                        // HACK: getting items types for icon from here instead of the constraint because the constraint contains things it cannot build
                        // for example, it allows ZoneChip in assembler inventory, but it cannot build it nor deconstruct it, it's from the LargeBlocks having SafeZone and does not check.
                        foreach(MyBlueprintClassDefinition bpClassDef in productionDef.BlueprintClasses)
                        {
                            if(DebugLog)
                                Log.Info($"  {bpClassDef.Id} - type={bpClassDef.GetType().Name}");

                            foreach(MyBlueprintDefinitionBase bp in bpClassDef)
                            {
                                // likely block blueprints which we don't care for
                                if(bp is MyCompositeBlueprintDefinition)
                                    continue;

                                foreach(MyBlueprintDefinitionBase.Item item in bp.Prerequisites)
                                {
                                    if(IgnoreItems.Contains(item.Id))
                                        continue;

                                    InputTypes.Add(item.Id.TypeId);
                                }

                                foreach(MyBlueprintDefinitionBase.Item item in bp.Results)
                                {
                                    if(IgnoreItems.Contains(item.Id))
                                        continue;

                                    OutputTypes.Add(item.Id.TypeId);
                                }

                                if(DebugLog)
                                    Log.Info($"    {bp.Id} type={bp.GetType().Name} - req: {string.Join(",", bp.Prerequisites)}; results: {string.Join(",", bp.Results)}");
                            }
                        }

                        if(DebugLog)
                            Log.Info("----------------------------------------------------------------");

                        SetTooltip(blockDef, ref productionDef.InputInventoryConstraint, productionDef.BlueprintClasses, true);
                        SetTooltip(blockDef, ref productionDef.OutputInventoryConstraint, productionDef.BlueprintClasses, false);

                        SetIcon(productionDef, productionDef.InputInventoryConstraint, InputTypes, true);
                        SetIcon(productionDef, productionDef.OutputInventoryConstraint, OutputTypes, false);

                        continue;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                // no longer needed, free up the memory
                InputTypes = null;
                OutputTypes = null;
                Tooltip = null;
                Icons = null;
                IgnoreItems = null;
                ItemsByType = null;
            }
        }

        void SetTooltip(MyDefinitionBase def, ref MyInventoryConstraint constraint, List<MyBlueprintClassDefinition> bpClasses, bool input)
        {
            if(constraint == null)
                return;

            // from MyProductionBlockDefinition.LoadPostProcess() + PrepareConstraint()
            string vanillaTooltip = string.Format(MyTexts.GetString(input ? MySpaceTexts.ToolTipItemFilter_GenericProductionBlockInput : MySpaceTexts.ToolTipItemFilter_GenericProductionBlockOutput), def.DisplayNameText);

            // customized by something else, don't touch.
            if(constraint.Description != vanillaTooltip)
            {
                if(BuildInfoMod.IsDevMod)
                    Log.Info($"[DEV] Tooltips.Inventories: '{def.Id}' has customized {(input ? "input" : "output")} tooltip, ignoring. Tooltip: '{constraint.Description}'");

                return;
            }

            Tooltip.Clear().Append(constraint.IsWhitelist ? "Items allowed:" : "Items NOT allowed:");

            ItemsByType.Clear();

            foreach(MyObjectBuilderType type in constraint.ConstrainedTypes)
            {
                Tooltip.Append("\n- All of type: ").IdTypeFormat(type);
            }

            foreach(MyDefinitionId id in constraint.ConstrainedIds)
            {
                List<string> subtypes;
                if(!ItemsByType.TryGetValue(id.TypeId, out subtypes))
                {
                    subtypes = new List<string>();
                    ItemsByType[id.TypeId] = subtypes;
                }

                subtypes.Add(id.SubtypeName);
            }

            foreach(KeyValuePair<MyObjectBuilderType, List<string>> kv in ItemsByType)
            {
                MyObjectBuilderType typeId = kv.Key;
                List<string> subtypes = kv.Value;

                Tooltip.Append("\n- ");

                // if exceding max, the last item is replaced by "+X other Y types".
                // to avoid it showing "+1 other Y types" which could just as well show the item.
                bool showMore = false;
                int max = subtypes.Count;
                if(max > TooltipMaxPerType)
                {
                    showMore = true;
                    max = TooltipMaxIfMore;
                }

                for(int i = 0; i < max; i++)
                {
                    string subtype = subtypes[i];

                    Tooltip.ItemName(new MyDefinitionId(typeId, subtype)).Append(", ");
                }

                Tooltip.Length -= 2; // remove last comma

                if(showMore)
                    Tooltip.Append(" (+").Append(subtypes.Count - TooltipMaxIfMore).Append(" other ").IdTypeFormat(typeId).Append(" types)");
            }

            // HACK: need to create a new constraint to change the description...
            MyInventoryConstraint newConstraint = new MyInventoryConstraint(Tooltip.ToString(), constraint.Icon, constraint.IsWhitelist);
            newConstraint.m_useDefaultIcon = constraint.m_useDefaultIcon;

            foreach(MyObjectBuilderType type in constraint.ConstrainedTypes)
            {
                newConstraint.AddObjectBuilderType(type);
            }

            foreach(MyDefinitionId item in constraint.ConstrainedIds)
            {
                newConstraint.Add(item);
            }

            constraint = newConstraint;
        }

        void SetIcon(MyDefinitionBase def, MyInventoryConstraint constraint, HashSet<MyObjectBuilderType> types, bool input)
        {
            if(constraint == null)
                return;

            if(types.Count == 0)
            {
                if(BuildInfoMod.IsDevMod)
                    Log.Info($"[DEV] Tooltips.Inventories: '{def.Id}' has no types, ignoring. icon='{constraint.Icon}'");

                return;
            }

            bool misleadingIcon = false;

            if(constraint.Icon != null)
            {
                // likely badly configured vanilla icons
                if(constraint.Icon.EndsWith(@"Textures\GUI\Icons\filter_ore.dds")
                || constraint.Icon.EndsWith(@"Textures\GUI\Icons\filter_ingot.dds"))
                {
                    misleadingIcon = true;
                }
            }

            if(types.Count > 1)
            {
                bool hasBottles = types.Remove(typeof(MyObjectBuilder_GasContainerObject));
                hasBottles |= types.Remove(typeof(MyObjectBuilder_OxygenContainerObject)); // needs both to execute

                bool hasOre = types.Remove(typeof(MyObjectBuilder_Ore));
                bool hasIngot = types.Remove(typeof(MyObjectBuilder_Ingot));

                if(types.Count == 0)
                {
                    if(hasIngot && hasOre && !hasBottles)
                    {
                        constraint.Icon = Utils.GetModFullPath(@"Textures\FilterIngotOre.dds");
                    }
                    else if(hasBottles && hasOre && !hasIngot)
                    {
                        constraint.Icon = Utils.GetModFullPath(@"Textures\FilterBottleOre.dds");
                    }
                    else if(hasBottles && !hasOre && !hasIngot)
                    {
                        constraint.Icon = Icons[typeof(MyObjectBuilder_GasContainerObject)];
                    }
                }
                else
                {
                    //if(BuildInfoMod.IsDevMod)
                    //    Log.Info($"Tooltips.Inventories: '{def.Id}' still has {types.Count} {(input ? "input" : "output")} types: {string.Join(", ", types)}; uses icon='{constraint.Icon}'");

                    // remove misleading icon
                    if(misleadingIcon)
                        constraint.Icon = null;
                }
            }
            else
            {
                MyObjectBuilderType type = types.FirstElement();

                string icon;
                if(Icons.TryGetValue(type, out icon))
                {
                    constraint.Icon = icon;
                }
            }
        }
    }
}
