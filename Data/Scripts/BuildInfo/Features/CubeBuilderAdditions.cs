using System;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
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
        IMyHudNotification Notify;

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
                LocalBBCache = null;

            LastRemoveBlock = removeBlock;

            if(removeBlock == null)
                return;

            // TODO: compute mirrored selections too?

            if(!Main.IsPaused)
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

            if(!Main.Config.OverrideToolSelectionDraw.Value)
                return;

            MatrixD worldMatrix;
            BoundingBoxD localBB;
            Main.OverrideToolSelectionDraw.GetBlockLocalBB(removeBlock, ref LocalBBCache, out localBB, out worldMatrix);

            localBB.Inflate((removeBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.1 : 0.03));
            float lineWidth = (removeBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large ? 0.02f : 0.016f);

            Main.OverrideToolSelectionDraw.DrawSelection(ref worldMatrix, ref localBB, new Color(255, 200, 55), lineWidth);
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
