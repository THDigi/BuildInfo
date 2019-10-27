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
        private const float BLOCKINFO_COMPONENT_UNDERLINE_OFFSET = 0.0062f;
        private const float BLOCKINFO_COMPONENT_HIGHLIGHT_HEIGHT = 0.0014f;
        private const float BLOCKINFO_Y_OFFSET = 0.12f;
        private const float BLOCKINFO_Y_OFFSET_2 = 0.0102f;
        private const float BLOCKINFO_LINE_HEIGHT = 0.0001f;
        private readonly Vector4 BLOCKINFO_LINE_FUNCTIONAL = Color.Red.ToVector4();
        private readonly Vector4 BLOCKINFO_LINE_OWNERSHIP = Color.Blue.ToVector4();
        private readonly Vector4 BLOCKINFO_LINE_COMPLOSS = (Color.Yellow * 0.75f).ToVector4();

        private readonly MyStringId SELECT_GIZMO_RED = MyStringId.GetOrCompute("GizmoDrawLineRed");
        private const int SELECT_CANBUILD_SKIP_TICKS = 15;

        private int computerComponentIndex = -1;

        private int componentReplaceInfoCount = 0;
        private readonly List<ComponentReplaceInfo> componentReplaceInfo = new List<ComponentReplaceInfo>(10);

        private BuildCheckResult projectedCanBuildCached;

        private class ComponentReplaceInfo
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
                if(Text != null)
                    Text.Visible = false;

                Replaced = null;
            }
        }

        public BlockInfoAdditions(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_DRAW;
        }

        protected override void RegisterComponent()
        {
            EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
        }

        protected override void UnregisterComponent()
        {
            EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            for(int i = 0; i < componentReplaceInfoCount; ++i)
            {
                componentReplaceInfo[i].Clear();
            }

            componentReplaceInfoCount = 0;
            computerComponentIndex = -1;

            if(def != null)
            {
                for(int i = 0; i < def.Components.Length; ++i)
                {
                    var comp = def.Components[i];

                    if(computerComponentIndex == -1 && comp.Definition.Id == Constants.COMPUTER_COMPONENT_ID)
                    {
                        computerComponentIndex = i;
                    }

                    if(comp.DeconstructItem != comp.Definition)
                    {
                        AddCompLoss(i, comp.DeconstructItem);
                    }
                }
            }
        }

        private void AddCompLoss(int index, MyPhysicalItemDefinition replaced)
        {
            ComponentReplaceInfo info = null;

            if(componentReplaceInfoCount >= componentReplaceInfo.Count)
            {
                info = new ComponentReplaceInfo();
                componentReplaceInfo.Add(info);
            }
            else
            {
                info = componentReplaceInfo[componentReplaceInfoCount];
            }

            info.Set(index, replaced);
            componentReplaceInfoCount++;
        }

        private void DrawProjectedSelection()
        {
            var aimedBlock = EquipmentMonitor.AimedBlock;
            var projector = EquipmentMonitor.AimedProjectedBy;

            if(aimedBlock == null || projector == null)
                return;

            var canBuild = projectedCanBuildCached;

            if(Main.Tick % SELECT_CANBUILD_SKIP_TICKS == 0)
                canBuild = projectedCanBuildCached = projector.CanBuild(aimedBlock, checkHavokIntersections: true);

            if(canBuild == BuildCheckResult.OK) // buildable blocks already have a selection box
                return;

            var grid = (MyCubeGrid)aimedBlock.CubeGrid;
            MyCubeBuilder.DrawSemiTransparentBox(aimedBlock.Min, aimedBlock.Max, grid, Color.White, onlyWireframe: true, lineMaterial: SELECT_GIZMO_RED);
        }

        protected override void UpdateDraw()
        {
            if(GameConfig.HudState == HudState.OFF || EquipmentMonitor.BlockDef == null || MyAPIGateway.Gui.IsCursorVisible)
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
            if(Config.BlockInfoStages.Value)
            {
                var blockDef = EquipmentMonitor.BlockDef;
                var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

                var posCompList = DrawUtils.GetHudComponentListStart();
                var totalComps = blockDef.Components.Length;

                // for debugging
                //if(MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.Shift))
                //{
                //    for(int i = totalComps - 1; i >= 0; --i)
                //    {
                //        var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * DrawUtils.ScaleFOV, BLOCKINFO_COMPONENT_HIGHLIGHT_HEIGHT * DrawUtils.ScaleFOV);
                //
                //        var hud = posCompList;
                //        hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - i - 1);
                //
                //        var worldPos = DrawUtils.HUDtoWorld(hud);
                //
                //        worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;
                //
                //        MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, Color.HotPink * (0.25f + ((i / (float)totalComps) / 2)), worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                //    }
                //}

                // red functionality line
                if(blockDef.CriticalGroup >= 0 && blockDef.CriticalGroup < totalComps)
                {
                    var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * DrawUtils.ScaleFOV, BLOCKINFO_LINE_HEIGHT * DrawUtils.ScaleFOV);

                    var hud = posCompList;
                    hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - blockDef.CriticalGroup - 2) + BLOCKINFO_COMPONENT_UNDERLINE_OFFSET;

                    var worldPos = DrawUtils.HUDtoWorld(hud);

                    worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                    MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, BLOCKINFO_LINE_FUNCTIONAL, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                }

                // blue hacking line
                if(computerComponentIndex != -1)
                {
                    var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * DrawUtils.ScaleFOV, BLOCKINFO_LINE_HEIGHT * DrawUtils.ScaleFOV);

                    var hud = posCompList;
                    hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - computerComponentIndex - 2) + BLOCKINFO_COMPONENT_UNDERLINE_OFFSET;

                    var worldPos = DrawUtils.HUDtoWorld(hud);

                    worldPos += camMatrix.Left * size.X + camMatrix.Up * (size.Y * 3); // extra offset to allow for red line to be visible

                    MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, BLOCKINFO_LINE_OWNERSHIP, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                }

                // different return item on grind
                for(int i = componentReplaceInfoCount - 1; i >= 0; --i)
                {
                    var info = componentReplaceInfo[i];

                    var hud = posCompList;
                    hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - info.Index - 1);

                    var worldPos = DrawUtils.HUDtoWorld(hud);

                    if(TextAPIEnabled)
                    {
                        const double LEFT_OFFSET = 0.0183;
                        const double TEXT_SCALE = 0.0011;
                        const string LABEL = "<color=255,255,0>Grinds to: ";
                        const int NAME_MAX_CHARACTERS = 21;

                        worldPos += camMatrix.Left * (LEFT_OFFSET * DrawUtils.ScaleFOV);

                        if(info.Text == null)
                        {
                            info.Text = new HudAPIv2.SpaceMessage(new StringBuilder(LABEL.Length + NAME_MAX_CHARACTERS), worldPos, camMatrix.Up, camMatrix.Left, TEXT_SCALE, null, 2, HudAPIv2.TextOrientation.ltr, BLEND_TYPE);
                        }
                        else
                        {
                            info.Text.WorldPosition = worldPos;
                            info.Text.Left = camMatrix.Left;
                            info.Text.Up = camMatrix.Up;
                        }

                        var sb = info.Text.Message.Clear();
                        sb.Append(LABEL);
                        sb.AppendMaxLength(info.Replaced.DisplayNameText, NAME_MAX_CHARACTERS);

                        info.Text.Draw();
                    }
                    else
                    {
                        var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * DrawUtils.ScaleFOV, BLOCKINFO_COMPONENT_HIGHLIGHT_HEIGHT * DrawUtils.ScaleFOV);

                        worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                        MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, BLOCKINFO_LINE_COMPLOSS, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                    }
                }
            }
            #endregion Lines on top of block info
        }
    }
}
