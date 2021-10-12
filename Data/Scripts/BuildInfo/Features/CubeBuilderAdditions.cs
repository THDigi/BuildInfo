using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.Input.Devices;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
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
            Main.EquipmentMonitor.ToolChanged += ToolChanged;
            Main.EquipmentMonitor.BuilderAimedBlockChanged += BuilderAimedBlockChanged;
        }

        public override void UnregisterComponent()
        {
            if(MyCubeBuilder.Static != null)
                MyCubeBuilder.Static.ShowRemoveGizmo = true;

            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.ToolChanged -= ToolChanged;
            Main.EquipmentMonitor.BuilderAimedBlockChanged -= BuilderAimedBlockChanged;
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
            // TODO: expand selection (preferably white) when holding ctrl or shift to show what will be painted?
            // TODO: include area-paint info in message?

            if(showMessage && !Main.IsPaused)
            {
                string name = null;
                IMyTerminalBlock tb = aimedBlock?.FatBlock as IMyTerminalBlock;

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

                float lineWidth = (aimedBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.02f : 0.016f);
                double inflate = (aimedBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.1 : 0.03);

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
