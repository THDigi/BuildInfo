using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class ProjectedBlockInfo : ModComponent
    {
        static bool Debug = false;

        readonly MyStringId MaterialGizmoRedLine = MyStringId.GetOrCompute("GizmoDrawLineRed");

        readonly Dictionary<MyDefinitionId, MyComponentStack> CompStackPerDefId = new Dictionary<MyDefinitionId, MyComponentStack>(MyDefinitionId.Comparer);

        int waitTool;
        int waitBlock;
        const int WaitTicks = 5;

        public ProjectedBlockInfo(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            Main.Config.SelectAllProjectedBlocks.ValueAssigned += ConfigValueSet;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            Main.Config.SelectAllProjectedBlocks.ValueAssigned -= ConfigValueSet;
        }

        void ConfigValueSet(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, newValue);
        }

        public override void UpdateAfterSim(int tick)
        {
            EquipmentMonitor eq = Main.EquipmentMonitor;
            MyCubeBlockDefinition def = eq.BlockDef;
            MyHudBlockInfo hud = MyHud.BlockInfo;

            if(def != null)
            {
                waitTool = 0;

                if(hud.DefinitionId == def.Id)
                {
                    waitBlock = 0;
                }
                else if(++waitBlock > WaitTicks)
                {
                    waitBlock = 0;

                    if(eq.AimedProjectedBy != null && (eq.IsAnyGrinder || eq.AimedProjectedCanBuild != BuildCheckResult.OK))
                    {
                        // HACK TODO: continue hardcoded hunt
                        // HACK: hardcoded from MyWelder.DrawHud()
                        hud.MissingComponentIndex = 0;
                        hud.DefinitionId = def.Id;
                        hud.BlockName = def.DisplayNameText;
                        hud.PCUCost = def.PCU;
                        hud.BlockIcons = def.Icons;
                        hud.BlockIntegrity = 0.01f;
                        hud.CriticalIntegrity = def.CriticalIntegrityRatio;
                        hud.CriticalComponentIndex = def.CriticalGroup;
                        hud.OwnershipIntegrity = def.OwnershipIntegrityRatio;
                        hud.BlockBuiltBy = 0;
                        hud.GridSize = def.CubeSize;

                        hud.Components.Clear();

                        // HACK: simpler than to mess with all this component stuff
                        MyComponentStack compStack;
                        if(!CompStackPerDefId.TryGetValue(def.Id, out compStack))
                        {
                            compStack = new MyComponentStack(def, 0, 0);
                            CompStackPerDefId[def.Id] = compStack;
                        }

                        for(int i = 0; i < compStack.GroupCount; i++)
                        {
                            MyComponentStack.GroupInfo groupInfo = compStack.GetGroupInfo(i);
                            hud.Components.Add(new MyHudBlockInfo.ComponentInfo()
                            {
                                DefinitionId = groupInfo.Component.Id,
                                ComponentName = groupInfo.Component.DisplayNameText,
                                Icons = groupInfo.Component.Icons,
                                TotalCount = groupInfo.TotalCount,
                                MountedCount = 0,
                                StockpileCount = 0,
                                AvailableAmount = 0,
                            });
                        }

                        hud.SetContextHelp(def);

                        if(Debug)
                            MyAPIGateway.Utilities.ShowNotification("HUD for block", 1000, "Red");
                    }
                }
                return;
            }
            else // def is null
            {
                waitBlock = 0;

                IMyHandheldGunObject<MyToolBase> tool = eq.HandTool as IMyHandheldGunObject<MyToolBase>;
                MyPhysicalItemDefinition toolDef = tool?.PhysicalItemDefinition;
                if(toolDef != null)
                {
                    if(hud.DefinitionId == toolDef.Id)
                    {
                        waitTool = 0;
                    }
                    else if(++waitTool > WaitTicks)
                    {
                        waitTool = 0;

                        // HACK: hardcoded from MyEngineerToolBase.DrawHud() because calling it doesn't work
                        hud.MissingComponentIndex = -1;
                        hud.DefinitionId = toolDef.Id;
                        hud.BlockName = toolDef.DisplayNameText;
                        hud.PCUCost = 0;
                        hud.BlockIcons = toolDef.Icons;
                        hud.BlockIntegrity = 1f;
                        hud.CriticalIntegrity = 0f;
                        hud.CriticalComponentIndex = 0;
                        hud.OwnershipIntegrity = 0f;
                        hud.BlockBuiltBy = 0L;
                        hud.GridSize = MyCubeSize.Small;
                        hud.Components.Clear();
                        hud.SetContextHelp(toolDef);

                        if(Debug)
                            MyAPIGateway.Utilities.ShowNotification("HUD set for TOOL", 1000, "Red");
                    }
                }
                return;
            }
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            if(!Main.Config.SelectAllProjectedBlocks.Value)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                return;
            }

            EquipmentMonitor eq = Main.EquipmentMonitor;
            bool aimingAtProjected = (eq.AimedBlock != null && eq.AimedProjectedBy != null);
            bool aimedProjectedBuildable = (eq.IsAnyWelder && eq.AimedProjectedCanBuild == BuildCheckResult.OK);

            /// draw basic selection only if <see cref="OverrideToolSelectionDraw"/> is off.
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, aimingAtProjected && !aimedProjectedBuildable && !Main.Config.OverrideToolSelectionDraw.Value);
        }

        public override void UpdateDraw()
        {
            if(Main.GameConfig.HudState == HudState.OFF || Main.EquipmentMonitor.BlockDef == null)
                return;

            IMySlimBlock aimedBlock = Main.EquipmentMonitor.AimedBlock;
            IMyProjector projector = Main.EquipmentMonitor.AimedProjectedBy;

            if(aimedBlock == null || projector == null)
                return;

            if(Main.EquipmentMonitor.IsAnyWelder && Main.EquipmentMonitor.AimedProjectedCanBuild == BuildCheckResult.OK)
                return; // buildable blocks already have a selection box

            MyCubeGrid grid = (MyCubeGrid)aimedBlock.CubeGrid;
            MyCubeBuilder.DrawSemiTransparentBox(aimedBlock.Min, aimedBlock.Max, grid, Color.White, onlyWireframe: true, lineMaterial: MaterialGizmoRedLine);
        }

#if false
        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            Main.Config.SelectAllProjectedBlocks.ValueAssigned += ConfigValueSet;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            Main.Config.SelectAllProjectedBlocks.ValueAssigned -= ConfigValueSet;
        }

        void ConfigValueSet(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            EquipmentMonitor_BlockChanged(Main.EquipmentMonitor.BlockDef, Main.EquipmentMonitor.AimedBlock);
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            Log.Info($"{Main.Tick}: EquipmentMonitor_BlockChanged aim/held={def}"); // DEBUG

            if(!Main.Config.SelectAllProjectedBlocks.Value)
            {
                /// NOTE: this feature is toggled in <see cref="OverrideToolSelectionDraw"/> and <see cref="EquipmentMonitor"/> too!
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                WasHUDModified = false;
                return;
            }

            var eq = Main.EquipmentMonitor;
            bool aimingAtProjected = (eq.AimedBlock != null && eq.AimedProjectedBy != null);
            bool aimedProjectedBuildable = (eq.IsAnyWelder && eq.AimedProjectedCanBuild == BuildCheckResult.OK);

            /// draw basic selection only if <see cref="OverrideToolSelectionDraw"/> is off.
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, aimingAtProjected && !aimedProjectedBuildable && !Main.Config.OverrideToolSelectionDraw.Value);

            if(def == null || (!aimingAtProjected || aimedProjectedBuildable))
            {
                WasHUDModified = false;
                //MyAPIGateway.Utilities.ShowNotification($"WasHUDModified reset", 1000, "Green"); // DEBUG
                Log.Info($"{Main.Tick}: WasHUDModified reset"); // DEBUG
            }

            // schedule HUD update for next tick as it doesn't update reliably here.
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void UpdateAfterSim(int tick)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

            var eq = Main.EquipmentMonitor;
            var def = eq.BlockDef;
            bool aimingAtBlock = (def != null && eq.AimedBlock != null);
            bool aimingAtProjected = (eq.AimedProjectedBy != null);
            bool aimedProjectedBuildable = (eq.IsAnyWelder && eq.AimedProjectedCanBuild == BuildCheckResult.OK);

            Log.Info($"{Main.Tick}: UpdateAfterSim aim/held={def}"); // DEBUG

            var hud = MyHud.BlockInfo;

            if(aimingAtBlock && aimingAtProjected && !aimedProjectedBuildable)
            {
                // HACK: hardcoded from MyWelder.DrawHud()
                hud.MissingComponentIndex = 0;
                hud.DefinitionId = def.Id;
                hud.BlockName = def.DisplayNameText;
                hud.PCUCost = def.PCU;
                hud.BlockIcons = def.Icons;
                hud.BlockIntegrity = 0.01f;
                hud.CriticalIntegrity = def.CriticalIntegrityRatio;
                hud.CriticalComponentIndex = def.CriticalGroup;
                hud.OwnershipIntegrity = def.OwnershipIntegrityRatio;
                hud.BlockBuiltBy = 0;
                hud.GridSize = def.CubeSize;

                hud.Components.Clear();

                // HACK: simpler than to mess with all this component stuff
                MyComponentStack compStack;
                if(!CompStackPerDefId.TryGetValue(def.Id, out compStack))
                {
                    compStack = new MyComponentStack(def, 0, 0);
                    CompStackPerDefId[def.Id] = compStack;
                }

                for(int i = 0; i < compStack.GroupCount; i++)
                {
                    MyComponentStack.GroupInfo groupInfo = compStack.GetGroupInfo(i);
                    hud.Components.Add(new MyHudBlockInfo.ComponentInfo()
                    {
                        DefinitionId = groupInfo.Component.Id,
                        ComponentName = groupInfo.Component.DisplayNameText,
                        Icons = groupInfo.Component.Icons,
                        TotalCount = groupInfo.TotalCount,
                        MountedCount = 0,
                        StockpileCount = 0,
                        AvailableAmount = 0,
                    });
                }

                hud.SetContextHelp(def);

                WasHUDModified = true;

                MyAPIGateway.Utilities.ShowNotification("HUD for block", 1000, "Red"); // DEBUG
                Log.Info($"{Main.Tick}: HUD set for block={def.Id}"); // DEBUG

                return;
            }

            // aiming away from unbuildable projected blocks doesn't clear the HUD, must do it manually
            if(WasHUDModified && def == null)
            {
                WasHUDModified = false;

                var tool = eq.HandTool as IMyHandheldGunObject<MyToolBase>;
                if(tool != null)
                {
                    // HACK: hardcoded from MyEngineerToolBase.DrawHud() because calling it doesn't work
                    var toolDef = tool.PhysicalItemDefinition;
                    hud.MissingComponentIndex = -1;
                    hud.DefinitionId = toolDef.Id;
                    hud.BlockName = toolDef.DisplayNameText;
                    hud.PCUCost = 0;
                    hud.BlockIcons = toolDef.Icons;
                    hud.BlockIntegrity = 1f;
                    hud.CriticalIntegrity = 0f;
                    hud.CriticalComponentIndex = 0;
                    hud.OwnershipIntegrity = 0f;
                    hud.BlockBuiltBy = 0L;
                    hud.GridSize = MyCubeSize.Small;
                    hud.Components.Clear();
                    hud.SetContextHelp(toolDef);

                    MyAPIGateway.Utilities.ShowNotification("HUD set for TOOL", 1000, "Red"); // DEBUG
                    Log.Info($"{Main.Tick}: HUD set for tool={toolDef.Id}"); // DEBUG
                }
                return;
            }
        }
#endif
    }
}
