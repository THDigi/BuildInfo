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
        readonly MyStringId MaterialGizmoRedLine = MyStringId.GetOrCompute("GizmoDrawLineRed");

        bool UpdateHud;
        MyCubeBlockDefinition UpdateHudDef;

        readonly Dictionary<MyDefinitionId, MyComponentStack> CompStackPerDefId = new Dictionary<MyDefinitionId, MyComponentStack>(MyDefinitionId.Comparer);

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

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            if(!Main.Config.SelectAllProjectedBlocks.Value)
            {
                /// NOTE: this feature is toggled in <see cref="OverrideToolSelectionDraw"/> and <see cref="EquipmentMonitor"/> too!
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                return;
            }

            bool hasProjectedBlock = (block != null && Main.EquipmentMonitor.AimedProjectedBy != null);
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, hasProjectedBlock && !Main.Config.OverrideToolSelectionDraw.Value); // model tool selection component handles projection selection too

            // HACK: schedule HUD update for next tick as this will get overwritten when aiming away from valid projected/real blocks to unbuildable projected ones
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            UpdateHud = hasProjectedBlock;
            UpdateHudDef = def;
        }

        void ConfigValueSet(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            EquipmentMonitor_BlockChanged(Main.EquipmentMonitor.BlockDef, Main.EquipmentMonitor.AimedBlock);
        }

        public override void UpdateAfterSim(int tick)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

            try
            {
                var def = UpdateHudDef;
                var hud = MyHud.BlockInfo;
                if(UpdateHud)
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
                }
                else if(def == null)
                {
                    var tool = MyAPIGateway.Session?.Player?.Character?.EquippedTool as IMyHandheldGunObject<MyToolBase>;
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
                    }
                }
            }
            finally
            {
                UpdateHudDef = null;
            }
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

            var grid = (MyCubeGrid)aimedBlock.CubeGrid;
            MyCubeBuilder.DrawSemiTransparentBox(aimedBlock.Min, aimedBlock.Max, grid, Color.White, onlyWireframe: true, lineMaterial: MaterialGizmoRedLine);
        }
    }
}
