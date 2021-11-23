using System;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.Input.Devices;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    // TODO: find in-paste grid and show some info on the right? or maybe in BI's text info...
    public class CubeBuilderAdditions : ModComponent
    {
        IMyHudNotification Notify;
        BlockSelectInfo BlockSelectInfo = new BlockSelectInfo();

        public CubeBuilderAdditions(BuildInfoMod main) : base(main)
        {
            UpdateOrder = -495; // for Draw() mainly, to always render first (and therefore, under)
        }

        public override void RegisterComponent()
        {
            MyCubeBuilder.Static.OnBlockSizeChanged += CubeBuilder_SizeChanged;
            Main.EquipmentMonitor.ToolChanged += ToolChanged;
            Main.EquipmentMonitor.BuilderAimedBlockChanged += BuilderAimedBlockChanged;
        }

        public override void UnregisterComponent()
        {
            if(MyCubeBuilder.Static != null)
                MyCubeBuilder.Static.ShowRemoveGizmo = true;

            if(MyCubeBuilder.Static != null)
                MyCubeBuilder.Static.OnBlockSizeChanged -= CubeBuilder_SizeChanged;

            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.ToolChanged -= ToolChanged;
            Main.EquipmentMonitor.BuilderAimedBlockChanged -= BuilderAimedBlockChanged;
        }

        bool IgnoreNextBuilderSizeChangeEvent = false;
        void CubeBuilder_SizeChanged()
        {
            try
            {
                if(IgnoreNextBuilderSizeChangeEvent)
                {
                    IgnoreNextBuilderSizeChangeEvent = false;
                    return;
                }

                // prevent cube size from changing when pressing any modifier, simple solution to prevent it from interferring hotkeys
                MyCubeBlockDefinition blockDef = MyCubeBuilder.Static?.CurrentBlockDefinition;
                if(blockDef != null && (MyAPIGateway.Input.IsAnyShiftKeyPressed() || MyAPIGateway.Input.IsAnyCtrlKeyPressed() || MyAPIGateway.Input.IsAnyAltKeyPressed()))
                {
                    MyCubeBlockDefinitionGroup blockPairDef = MyDefinitionManager.Static.GetDefinitionGroup(blockDef.BlockPairName);
                    if((blockDef.CubeSize == MyCubeSize.Large && blockPairDef.Small != null) || (blockDef.CubeSize == MyCubeSize.Small && blockPairDef.Large != null))
                    {
                        IgnoreNextBuilderSizeChangeEvent = true;

                        if(blockDef.CubeSize == MyCubeSize.Large)
                            MyCubeBuilder.Static.Activate(blockPairDef.Small.Id);
                        else
                            MyCubeBuilder.Static.Activate(blockPairDef.Large.Id);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void BuilderAimedBlockChanged(IMySlimBlock obj)
        {
            BlockSelectInfo.ClearCaches();
        }

        void ToolChanged(MyDefinitionId toolDefId)
        {
            BlockSelectInfo.ClearCaches();

            bool drawBox = Main.Config.OverrideToolSelectionDraw.Value;
            bool showInfo = Main.EquipmentMonitor.IsCubeBuilder && (drawBox || Main.Config.CubeBuilderSelectionInfoMode.ValueEnum != CubeBuilderSelectionInfo.Off);

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, showInfo);

            MyCubeBuilder.Static.ShowRemoveGizmo = !(showInfo && drawBox);
        }

        public override void UpdateDraw()
        {
            // ignore drag-to-build/drag-to-remove
            if(MyAPIGateway.Input.IsGameControlPressed(MyControlsSpace.PRIMARY_TOOL_ACTION) || MyAPIGateway.Input.IsGameControlPressed(MyControlsSpace.SECONDARY_TOOL_ACTION))
                return;

            IMySlimBlock aimedBlock = Main.EquipmentMonitor.BuilderAimedBlock;
            if(aimedBlock == null)
                return;

            bool showMessage = false;
            if(!Main.IsPaused && Main.GameConfig.HudState != HudState.OFF)
            {
                CubeBuilderSelectionInfo mode = Main.Config.CubeBuilderSelectionInfoMode.ValueEnum;

                if(mode == CubeBuilderSelectionInfo.AlwaysOn)
                    showMessage = true;

                if(mode == CubeBuilderSelectionInfo.HudHints && Main.GameConfig.HudState == HudState.HINTS)
                    showMessage = true;

                if(mode == CubeBuilderSelectionInfo.HudHints || mode == CubeBuilderSelectionInfo.ShowOnPress)
                {
                    ControlContext context = (MyAPIGateway.Session.ControlledObject is IMyCharacter ? ControlContext.CHARACTER : ControlContext.VEHICLE);
                    if(Main.Config.ShowCubeBuilderSelectionInfoBind.Value.IsPressed(context))
                        showMessage = true;
                }
            }

            // TODO: compute mirrored selections too?

            if(showMessage && !Main.IsPaused)
            {
                string name = null;
                IMyTerminalBlock tb = aimedBlock.FatBlock as IMyTerminalBlock;

                if(tb != null)
                    name = tb.CustomName;

                if(string.IsNullOrWhiteSpace(name))
                    name = aimedBlock.BlockDefinition.DisplayNameText;

                if(MyAPIGateway.Input.IsJoystickLastUsed)
                {
                    if(MyCubeBuilder.Static.ToolType == MyCubeBuilderToolType.ColorTool)
                        ShowText($"Selected for paint: [{name}]");
                    else if(Utils.CreativeToolsEnabled)
                        ShowText($"Selected for remove: [{name}]");
                }
                else
                {
                    if(Utils.CreativeToolsEnabled)
                        ShowText($"Selected for paint/remove: [{name}]");
                    else
                        ShowText($"Selected for paint: [{name}]");
                }
            }

            if(Main.Config.OverrideToolSelectionDraw.Value)
            {
                MyCubeBuilder.Static.ShowRemoveGizmo = false; // required because pressing same key twice on block without other size would show gizmo again

                bool isLarge = (aimedBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large);
                float lineWidth = (isLarge ? 0.02f : 0.016f);
                double inflate = (isLarge ? 0.1 : 0.03);

                Main.OverrideToolSelectionDraw.GetBlockModelBB(aimedBlock, BlockSelectInfo, inflate);

                if(BlockSelectInfo.ModelBB.HasValue)
                    Main.OverrideToolSelectionDraw.DrawSelection(BlockSelectInfo.ModelMatrix, BlockSelectInfo.ModelBB.Value, new Color(255, 200, 55), lineWidth);

                // always draw boundary when using cubebuilder
                Main.OverrideToolSelectionDraw.DrawSelection(BlockSelectInfo.BlockMatrix, BlockSelectInfo.Boundaries, new Color(100, 155, 255), lineWidth);
            }
        }

        void ShowText(string message)
        {
            if(Notify == null)
                Notify = MyAPIGateway.Utilities.CreateNotification("", 16 * 5, FontsHandler.WhiteSh);

            Notify.Hide();
            Notify.Text = message;
            Notify.Show();
        }
    }
}
