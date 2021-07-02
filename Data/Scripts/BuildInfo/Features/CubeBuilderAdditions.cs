using System;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
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
        BoundingBoxD? LocalBBCache;

        readonly MyCubeBlock BlockForHax = new MyCubeBlock(); // needed just for the MySlimBlock reference for the generic hax below

        public CubeBuilderAdditions(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
        }

        public override void UnregisterComponent()
        {
            Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;

            if(MyCubeBuilder.Static != null)
                MyCubeBuilder.Static.ShowRemoveGizmo = true;
        }

        private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            LastRemoveBlock = null;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, Main.EquipmentMonitor.IsCubeBuilder);

            MyCubeBuilder.Static.ShowRemoveGizmo = !Main.Config.OverrideToolSelectionDraw.Value;
        }

        public override void UpdateDraw()
        {
            if(Main.IsPaused || !MyAPIGateway.CubeBuilder.IsActivated)
                return;

            AimedGrid = MyCubeBuilder.Static.FindClosestGrid();
            if(AimedGrid == null)
                return;

            bool creativeTools = Utils.CreativeToolsEnabled;
            if(MyAPIGateway.Input.IsJoystickLastUsed && MyCubeBuilder.Static.ToolType == MyCubeBuilderToolType.BuildTool && !creativeTools)
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
                LocalBBCache = null;

            LastRemoveBlock = removeBlock;

            if(removeBlock == null)
                return;

            if(!Main.IsPaused)
            {
                string name = null;
                IMyTerminalBlock tb = LastRemoveBlock?.FatBlock as IMyTerminalBlock;

                if(tb != null)
                    name = tb.CustomName;

                if(string.IsNullOrWhiteSpace(name))
                    name = LastRemoveBlock.BlockDefinition.DisplayNameText;

                if(MyAPIGateway.Input.IsJoystickLastUsed)
                {
                    if(MyCubeBuilder.Static.ToolType == MyCubeBuilderToolType.ColorTool)
                        MyAPIGateway.Utilities.ShowNotification($"Selected for paint: [{name}]", 16, FontsHandler.WhiteSh);
                    else if(creativeTools)
                        MyAPIGateway.Utilities.ShowNotification($"Selected for remove: [{name}]", 16, FontsHandler.WhiteSh);
                }
                else
                {
                    if(creativeTools)
                        MyAPIGateway.Utilities.ShowNotification($"Selected for paint/remove: [{name}]", 16, FontsHandler.WhiteSh);
                    else
                        MyAPIGateway.Utilities.ShowNotification($"Selected for paint: [{name}]", 16, FontsHandler.WhiteSh);
                }
            }

            if(!Main.Config.OverrideToolSelectionDraw.Value)
                return;

            MatrixD worldMatrix;
            BoundingBoxD localBB;
            Main.OverrideToolSelectionDraw.GetBlockLocalBB(LastRemoveBlock, ref LocalBBCache, out localBB, out worldMatrix);

            localBB.Inflate((LastRemoveBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.1 : 0.03));
            float lineWidth = (LastRemoveBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.02f : 0.016f);

            Main.OverrideToolSelectionDraw.DrawSelection(ref worldMatrix, ref localBB, Color.OrangeRed, lineWidth);
        }

        IMySlimBlock Hackery<T>(T refType, Func<T, IMySlimBlock> callback) where T : class
        {
            return callback.Invoke(null);
        }
    }
}
