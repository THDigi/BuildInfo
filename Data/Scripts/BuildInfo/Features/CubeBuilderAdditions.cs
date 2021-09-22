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
    public class CubeBuilderAdditions : ModComponent
    {
        IMySlimBlock LastRemoveBlock;
        MyCubeGrid AimedGrid;
        IMyHudNotification Notify;
        CubeBuilderSelectionInfo Mode;
        BlockSelectInfo BlockSelectInfo = new BlockSelectInfo();

        readonly MyCubeBlock BlockForHax = new MyCubeBlock(); // needed just for the MySlimBlock reference for the generic hax below

        public CubeBuilderAdditions(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;

            Main.Config.CubeBuilderSelectionInfoMode.ValueAssigned += CubeBuilderSelectionInfoMode_ValueAssigned;
        }

        public override void UnregisterComponent()
        {
            if(MyCubeBuilder.Static != null)
                MyCubeBuilder.Static.ShowRemoveGizmo = true;

            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;

            Main.Config.CubeBuilderSelectionInfoMode.ValueAssigned -= CubeBuilderSelectionInfoMode_ValueAssigned;
        }

        void CubeBuilderSelectionInfoMode_ValueAssigned(int oldValue, int newValue, ConfigLib.SettingBase<int> setting)
        {
            Mode = (CubeBuilderSelectionInfo)newValue;
        }

        void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            LastRemoveBlock = null;
            BlockSelectInfo.ClearCaches();

            bool drawBox = Main.Config.OverrideToolSelectionDraw.Value;
            bool showInfo = Main.EquipmentMonitor.IsCubeBuilder && (drawBox || Mode != CubeBuilderSelectionInfo.Off);

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, showInfo);

            MyCubeBuilder.Static.ShowRemoveGizmo = !(showInfo && drawBox);
        }

        public override void UpdateDraw()
        {
            if(Main.IsPaused || !MyAPIGateway.CubeBuilder.IsActivated)
                return;

            // drag-to-build/remove shouldn't show selection anymore.
            if(MyAPIGateway.Input.IsGameControlPressed(MyControlsSpace.PRIMARY_TOOL_ACTION) || MyAPIGateway.Input.IsGameControlPressed(MyControlsSpace.SECONDARY_TOOL_ACTION))
                return;

            AimedGrid = MyCubeBuilder.Static.FindClosestGrid();
            if(AimedGrid == null)
                return;

            bool creativeTools = Utils.CreativeToolsEnabled;
            if(MyAPIGateway.Input.IsJoystickLastUsed && MyCubeBuilder.Static.ToolType == MyCubeBuilderToolType.BuildTool && !creativeTools)
                return;

            MyCubeBlockDefinition def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
            if(def == null || def.CubeSize != AimedGrid.GridSizeEnum)
                return;

            // HACK: required to be able to give MySlimBlock to GetAddAndRemovePositions() because it needs to 'out' it.
            IMySlimBlock removeBlock = Hackery(BlockForHax.SlimBlock, (slim) =>
            {
                Vector3I addPos;
                Vector3? addSmallToLargePos;
                Vector3I addDir;
                Vector3I removePos;
                ushort? compoundBlockId;
                bool canBuild = MyCubeBuilder.Static.GetAddAndRemovePositions(AimedGrid.GridSize, false,
                                                                              out addPos, out addSmallToLargePos, out addDir,
                                                                              out removePos, out slim, out compoundBlockId, null);
                return slim;
            });

            if(removeBlock != LastRemoveBlock)
                BlockSelectInfo.ClearCaches();

            LastRemoveBlock = removeBlock;

            if(removeBlock == null)
                return;

            bool showMessage = false;
            if(!Main.IsPaused && Main.GameConfig.HudState != HudState.OFF)
            {
                if(Mode == CubeBuilderSelectionInfo.AlwaysOn)
                    showMessage = true;

                if(Mode == CubeBuilderSelectionInfo.HudHints && Main.GameConfig.HudState == HudState.HINTS)
                    showMessage = true;

                if(Mode == CubeBuilderSelectionInfo.HudHints || Mode == CubeBuilderSelectionInfo.ShowOnPress)
                {
                    ControlContext context = (MyAPIGateway.Session.ControlledObject is IMyCharacter ? ControlContext.CHARACTER : ControlContext.VEHICLE);
                    if(Main.Config.ShowCubeBuilderSelectionInfoBind.Value.IsPressed(context))
                        showMessage = true;
                }
            }

            // TODO: compute mirrored selections too?
            // TODO: expand selection (preferably white) when holding ctrl or shift to show what will be painted?
            // TODO: include area-paint info in message?

            if(showMessage)
            {
                string name = null;
                IMyTerminalBlock tb = removeBlock?.FatBlock as IMyTerminalBlock;

                if(tb != null)
                    name = tb.CustomName;

                if(string.IsNullOrWhiteSpace(name))
                    name = removeBlock.BlockDefinition.DisplayNameText;

                if(MyAPIGateway.Input.IsJoystickLastUsed)
                {
                    if(MyCubeBuilder.Static.ToolType == MyCubeBuilderToolType.ColorTool)
                        ShowHUDNotification($"Selected for paint: [{name}]");
                    else if(creativeTools)
                        ShowHUDNotification($"Selected for remove: [{name}]");
                }
                else
                {
                    if(creativeTools)
                        ShowHUDNotification($"Selected for paint/remove: [{name}]");
                    else
                        ShowHUDNotification($"Selected for paint: [{name}]");
                }
            }

            if(Main.Config.OverrideToolSelectionDraw.Value)
            {
                MyCubeBuilder.Static.ShowRemoveGizmo = false; // required because pressing same key twice on block without other size would show gizmo again

                float lineWidth = (removeBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.02f : 0.016f);
                double inflate = (removeBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.1 : 0.03);

                Main.OverrideToolSelectionDraw.GetBlockModelBB(removeBlock, BlockSelectInfo, inflate);

                if(BlockSelectInfo.ModelBB.HasValue)
                    Main.OverrideToolSelectionDraw.DrawSelection(BlockSelectInfo.ModelMatrix, BlockSelectInfo.ModelBB.Value, new Color(255, 200, 55), lineWidth);

                // always draw boundary when using cubebuilder
                Main.OverrideToolSelectionDraw.DrawSelection(BlockSelectInfo.BlockMatrix, BlockSelectInfo.Boundaries, new Color(100, 155, 255), lineWidth);
            }
        }

        void ShowHUDNotification(string message)
        {
            if(Notify == null)
                Notify = MyAPIGateway.Utilities.CreateNotification("", 16 * 5, FontsHandler.WhiteSh);

            Notify.Hide();
            Notify.Text = message;
            Notify.Show();
        }

        IMySlimBlock Hackery<T>(T refType, Func<T, IMySlimBlock> callback) where T : class
        {
            return callback.Invoke(null);
        }
    }
}
