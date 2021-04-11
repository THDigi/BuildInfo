using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features
{
    public class BlockInfoAdditions : ModComponent
    {
        public readonly MyStringId LINE_MATERIAL = MyStringId.GetOrCompute("BuildInfo_Square");
        private const BlendTypeEnum BLEND_TYPE = BlendTypeEnum.PostPP;
        private const float BLOCKINFO_COMPONENT_HEIGHT = 0.037f; // component height in the vanilla block info
        private const float BLOCKINFO_COMPONENT_WIDTH = 0.011f;
        private const float BLOCKINFO_COMPONENT_UNDERLINE_OFFSET = 0.012f;
        private const float BLOCKINFO_COMPONENT_HIGHLIGHT_HEIGHT = 0.0014f;
        private const float BLOCKINFO_Y_OFFSET = 0.12f;
        private const float BLOCKINFO_Y_OFFSET_2 = 0.0102f;
        private const float BLOCKINFO_LINE_HEIGHT = 0.0001f;
        private readonly Vector4 BLOCKINFO_LINE_FUNCTIONAL = Color.Red.ToVector4();
        private readonly Vector4 BLOCKINFO_LINE_OWNERSHIP = Color.Blue.ToVector4();
        private readonly Vector4 BLOCKINFO_LINE_COMPLOSS = (Color.Yellow * 0.75f).ToVector4();

        private readonly MyStringId SELECT_GIZMO_RED = MyStringId.GetOrCompute("GizmoDrawLineRed");
        private const int SELECT_CANBUILD_SKIP_TICKS = 15;

        private int ComputerComponentIndex = -1;

        private int ComponentReplaceInfoCount = 0;
        private readonly List<ComponentInfo> ComponentReplaceInfo = new List<ComponentInfo>(10);

        private BuildCheckResult ProjectedCanBuildCached;

        private class ComponentInfo
        {
            public int Index;
            public MyPhysicalItemDefinition Replaced;
            public HudAPIv2.SpaceMessage Text;

            public void Set(int index, MyPhysicalItemDefinition replaced)
            {
                Index = index;
                Replaced = replaced;
            }

            public void Clear()
            {
                //if(Text != null)
                //    Text.Visible = false;

                Replaced = null;
            }
        }

        public BlockInfoAdditions(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_DRAW;
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
        }

        public override void UnregisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            for(int i = 0; i < ComponentReplaceInfoCount; ++i)
            {
                ComponentReplaceInfo[i].Clear();
            }

            ComponentReplaceInfoCount = 0;
            ComputerComponentIndex = -1;

            if(def != null)
            {
                for(int i = 0; i < def.Components.Length; ++i)
                {
                    var comp = def.Components[i];

                    if(ComputerComponentIndex == -1 && comp.Definition.Id == Main.Constants.COMPUTER_COMPONENT_ID)
                    {
                        ComputerComponentIndex = i;
                    }

                    if(comp.DeconstructItem != null && comp.DeconstructItem != comp.Definition)
                    {
                        AddCompLoss(i, comp.DeconstructItem);
                    }
                }
            }
        }

        private void AddCompLoss(int index, MyPhysicalItemDefinition replaced)
        {
            ComponentInfo info = null;

            if(ComponentReplaceInfoCount >= ComponentReplaceInfo.Count)
            {
                info = new ComponentInfo();
                ComponentReplaceInfo.Add(info);
            }
            else
            {
                info = ComponentReplaceInfo[ComponentReplaceInfoCount];
            }

            info.Set(index, replaced);
            ComponentReplaceInfoCount++;
        }

        private void DrawProjectedSelection()
        {
            IMySlimBlock aimedBlock = Main.EquipmentMonitor.AimedBlock;
            IMyProjector projector = Main.EquipmentMonitor.AimedProjectedBy;

            if(aimedBlock == null || projector == null)
                return;

            BuildCheckResult canBuild = ProjectedCanBuildCached;

            if(Main.Tick % SELECT_CANBUILD_SKIP_TICKS == 0)
                canBuild = ProjectedCanBuildCached = projector.CanBuild(aimedBlock, checkHavokIntersections: true);

            if(canBuild == BuildCheckResult.OK) // buildable blocks already have a selection box
                return;

            var grid = (MyCubeGrid)aimedBlock.CubeGrid;
            MyCubeBuilder.DrawSemiTransparentBox(aimedBlock.Min, aimedBlock.Max, grid, Color.White, onlyWireframe: true, lineMaterial: SELECT_GIZMO_RED);
        }

        public override void UpdateDraw()
        {
            if(Main.GameConfig.HudState == HudState.OFF || Main.EquipmentMonitor.BlockDef == null || MyAPIGateway.Gui.IsCursorVisible)
                return;

            DrawProjectedSelection();

            var hudComps = MyHud.BlockInfo?.Components;
            if(hudComps == null || hudComps.Count == 0) // don't show block info additions if the block info isn't visible
                return;

#if false
            #region Block info addition background
            // draw the added top part's background only for aimed block (which requires textAPI)
            if(selectedBlock != null && !showMenu && textObject != null && useTextAPI)
            {
                var hud = posHUD;

                // make the position top-right
                hud.Y -= (BLOCKINFO_ITEM_HEIGHT * selectedDef.Components.Length) + BLOCKINFO_Y_OFFSET;

                var worldPos = HudToWorld(hud);
                var size = GetGameHudBlockInfoSize(lines * Settings.textAPIScale);
                worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                double cornerSize = Math.Min(0.0015 * ScaleFOV, size.Y); // prevent corner from being larger than the height of the box
                float cornerW = (float)cornerSize;
                float cornerH = (float)cornerSize;

                {
                    var finalW = size.X - cornerW;
                    var finalH = cornerH;
                    var finalWorldPos = worldPos + camMatrix.Left * cornerW + camMatrix.Up * (size.Y - cornerH);
                    MyTransparentGeometry.AddBillboardOriented(MATERIAL_VANILLA_SQUARE, BLOCKINFO_BG_COLOR, finalWorldPos, camMatrix.Left, camMatrix.Up, finalW, finalH, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                }

                // HACK NOTE: this custom topright corner material will draw above textAPI if textAPI is loaded after this mod
                {
                    var finalW = cornerW;
                    var finalH = cornerH;
                    var finalWorldPos = worldPos + camMatrix.Right * (size.X - cornerW) + camMatrix.Up * (size.Y - cornerH);
                    MyTransparentGeometry.AddBillboardOriented(MATERIAL_TOPRIGHTCORNER, BLOCKINFO_BG_COLOR, finalWorldPos, camMatrix.Left, camMatrix.Up, finalW, finalH, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                }

                {
                    var finalW = size.X;
                    var finalH = size.Y - cornerH;
                    var finalWorldPos = worldPos + camMatrix.Down * cornerH;
                    MyTransparentGeometry.AddBillboardOriented(MATERIAL_VANILLA_SQUARE, BLOCKINFO_BG_COLOR, finalWorldPos, camMatrix.Left, camMatrix.Up, finalW, finalH, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                }
            }
            #endregion Block info addition background
#endif

            #region Lines on top of block info
            if(Main.Config.BlockInfoAdditions.Value)
            {
                var blockDef = Main.EquipmentMonitor.BlockDef;
                var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

                Vector2 posCompList = Main.DrawUtils.GetHudComponentListStart();
                int totalComps = blockDef.Components.Length;

                int scrollIndexOffset = Main.BlockInfoScrollComponents.IndexOffset;

                // for debugging
                //if(MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.Control))
                //{
                //    for(int i = totalComps - 1; i >= 0; --i)
                //    {
                //        var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * DrawUtils.ScaleFOV, BLOCKINFO_COMPONENT_HIGHLIGHT_HEIGHT * DrawUtils.ScaleFOV);
                //
                //        var hud = posCompList;
                //        hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - i - scrollIndexOffset - 1);
                //
                //        var worldPos = DrawUtils.HUDtoWorld(hud);
                //
                //        worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;
                //
                //        MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, Color.HotPink * (0.25f + ((i / (float)totalComps) / 2)), worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                //    }
                //}

                // red functionality line
                int critIndex = blockDef.CriticalGroup + scrollIndexOffset;
                if(critIndex >= 0 && critIndex < totalComps)
                {
                    var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * Main.DrawUtils.ScaleFOV, BLOCKINFO_LINE_HEIGHT * Main.DrawUtils.ScaleFOV);

                    var hud = posCompList;
                    hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - critIndex - 2) + BLOCKINFO_COMPONENT_UNDERLINE_OFFSET;

                    var worldPos = Main.DrawUtils.HUDtoWorld(hud);

                    worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                    MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, BLOCKINFO_LINE_FUNCTIONAL, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                }

                // blue hacking line
                int compIndex = ComputerComponentIndex + scrollIndexOffset;
                if(compIndex >= 0 && compIndex < totalComps)
                {
                    var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * Main.DrawUtils.ScaleFOV, BLOCKINFO_LINE_HEIGHT * Main.DrawUtils.ScaleFOV);

                    var hud = posCompList;
                    hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - compIndex - 2) + BLOCKINFO_COMPONENT_UNDERLINE_OFFSET;

                    var worldPos = Main.DrawUtils.HUDtoWorld(hud);

                    worldPos += camMatrix.Left * size.X + camMatrix.Up * (size.Y * 3); // extra offset to allow for red line to be visible

                    MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, BLOCKINFO_LINE_OWNERSHIP, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                }

                int maxVisible = Main.BlockInfoScrollComponents.MaxVisible;
                int scrollIdx = Main.BlockInfoScrollComponents.Index;
                int maxIdx = scrollIdx + maxVisible;

                // different return item on grind
                for(int i = (ComponentReplaceInfoCount - 1); i >= 0; --i)
                {
                    var info = ComponentReplaceInfo[i];
                    int index = info.Index + scrollIndexOffset;

                    if(info.Index >= maxIdx // hide for components scrolled above
                    || info.Index < scrollIdx // hide for components scrolled below
                    || info.Index == (maxVisible - 1) && Main.BlockInfoScrollComponents.ShowUpHint // hide for top hint component
                    || info.Index == (totalComps - maxVisible) && Main.BlockInfoScrollComponents.ShowDownHint) // hide for bottom hint component
                    {
                        //if(info.Text != null)
                        //    info.Text.Visible = false;

                        continue;
                    }

                    var hud = posCompList;
                    hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - index - 1);

                    var worldPos = Main.DrawUtils.HUDtoWorld(hud);

                    if(Main.TextAPI.IsEnabled)
                    {
                        const double LeftOffset = 0.0183;
                        const string LabelPrefix = "<color=255,255,0>Grinds to: ";
                        const int NameMaxChars = 21;
                        double textScale = 0.0012 * Main.DrawUtils.ScaleFOV;

                        worldPos += camMatrix.Left * (LeftOffset * Main.DrawUtils.ScaleFOV);

                        if(info.Text == null)
                        {
                            info.Text = new HudAPIv2.SpaceMessage(new StringBuilder(LabelPrefix.Length + NameMaxChars), worldPos, camMatrix.Up, camMatrix.Left, textScale, null, 2, HudAPIv2.TextOrientation.ltr, BLEND_TYPE);
                            info.Text.Visible = false; // not required when using manual .Draw()
                        }
                        else
                        {
                            info.Text.WorldPosition = worldPos;
                            info.Text.Left = camMatrix.Left;
                            info.Text.Up = camMatrix.Up;
                            info.Text.Scale = textScale;
                        }

                        var sb = info.Text.Message.Clear();
                        sb.Append(LabelPrefix);
                        sb.AppendMaxLength(info.Replaced.DisplayNameText, NameMaxChars);

                        //info.Text.Visible = true;
                        info.Text.Draw();
                    }
                    else
                    {
                        var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * Main.DrawUtils.ScaleFOV, BLOCKINFO_COMPONENT_HIGHLIGHT_HEIGHT * Main.DrawUtils.ScaleFOV);

                        worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                        MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, BLOCKINFO_LINE_COMPLOSS, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                    }
                }
            }
            #endregion Lines on top of block info
        }
    }
}
