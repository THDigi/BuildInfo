using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Draygo.API;
using Sandbox.Definitions;
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
        readonly Vector4 LineFunctionalColor = Color.Red.ToVector4();
        readonly Vector4 LineOwnershipColor = Color.Blue.ToVector4();
        readonly Vector4 HighlightCompLossColor = (Color.Yellow * 0.75f).ToVector4();

        // from MyGuiControlBlockGroupInfo.CreateBlockInfoControl()
        readonly Vector4 ScrollbarColor = new Vector4(118f / 255f, 166f / 255f, 64f / 85f, 1f);
        readonly Vector4 ScrollbarBgColor = new Vector4(142f / (339f * MathHelper.Pi), 46f / 255f, 52f / 255f, 1f);

        readonly MyStringId MaterialSquare = MyStringId.GetOrCompute("BuildInfo_UI_Square");
        const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        const float HudComponentHeight = 0.037f; // on HUD space
        const float HudComponentWidth = 0.011f;
        const float HudComponentUnderlineOffset = 0.012f;

        const float WorldBlockInfoLineHeight = 0.0001f; // on world space
        const float WorldComponentHighlightHeight = 0.0014f;
        const float WorldComponentHeight = 0.00185f;
        const float WorldBuildInfoMargin = 0.0008f;

        const float WorldScrollbarWidth = 0.0005f;
        const float WorldScrollbarInnerMargin = (WorldScrollbarWidth / 4f);

        int ComputerComponentIndex = -1;
        int ComponentReplaceInfoCount = 0;
        readonly List<ComponentInfo> ComponentReplaceInfo = new List<ComponentInfo>(10);

        class ComponentInfo
        {
            public int Index;
            public MyPhysicalItemDefinition Replaced;
            public HudAPIv2.SpaceMessage Text;

            public void Set(int index, MyPhysicalItemDefinition replaced)
            {
                Index = index;
                Replaced = replaced;
            }

            public void Reset()
            {
                //if(Text != null)
                //    Text.Visible = false;

                Replaced = null;
            }
        }

        public BlockInfoAdditions(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            Main.Config.BlockInfoAdditions.ValueAssigned += BlockInfoAdditions_ValueAssigned;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            Main.Config.BlockInfoAdditions.ValueAssigned -= BlockInfoAdditions_ValueAssigned;
        }

        void BlockInfoAdditions_ValueAssigned(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (newValue && Main.EquipmentMonitor.BlockDef != null));
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (def != null && Main.Config.BlockInfoAdditions.Value));

            for(int i = 0; i < ComponentReplaceInfoCount; ++i)
            {
                ComponentReplaceInfo[i].Reset();
            }

            ComponentReplaceInfoCount = 0;
            ComputerComponentIndex = -1;

            if(def != null)
            {
                for(int i = 0; i < def.Components.Length; ++i)
                {
                    MyCubeBlockDefinition.Component comp = def.Components[i];

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

        public override void UpdateDraw()
        {
            if(Main.GameConfig.HudState == HudState.OFF || Main.EquipmentMonitor.BlockDef == null || MyAPIGateway.Gui.IsCursorVisible)
                return;

            List<MyHudBlockInfo.ComponentInfo> hudComps = MyHud.BlockInfo?.Components;
            if(hudComps == null || hudComps.Count == 0) // don't show block info additions if the block info isn't visible
                return;

            //if(Main.Config.BlockInfoAdditions.Value)
            {
                MyCubeBlockDefinition blockDef = Main.EquipmentMonitor.BlockDef;
                MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

                #region Compute BlockInfo HUD position
                // it's actually bottom right of the top component line... O:)
                Vector2 compListTopRight = new Vector2(0.9894f, 0.678f);

                if(MyAPIGateway.Session.ControlledObject is IMyShipController)
                    compListTopRight.Y = 0.498f;

                if(Main.GameConfig.HudState == HudState.BASIC)
                    compListTopRight.Y = 0.558f;

                // FIXME: vanilla UI is all over the place with this, needs tweaking
                if(Main.GameConfig.AspectRatio > 5) // triple monitor
                    compListTopRight.X += 0.75f;
                #endregion

                int totalComps = blockDef.Components.Length;

                int maxVisibleComps = Main.BlockInfoScrollComponents.MaxVisible;
                int scrollIdx = Main.BlockInfoScrollComponents.Index;
                int scrollIdxOffset = Main.BlockInfoScrollComponents.IndexOffset;
                int maxScrollIdx = scrollIdx + maxVisibleComps;

                float scaleFOV = Main.DrawUtils.ScaleFOV;

                // for debugging
                //if(MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.Control))
                //{
                //    for(int i = totalComps - 1; i >= 0; --i)
                //    {
                //        Vector2 sizeWorld = new Vector2(HudComponentWidth * scaleFOV, WorldComponentHighlightHeight * scaleFOV);

                //        Vector2 posHud = compListTopRight;
                //        posHud.Y += HudComponentHeight * (totalComps - i - scrollIdxOffset - 1);
                //        Vector3D posWorld = Main.DrawUtils.HUDtoWorld(posHud);

                //        DrawDotAt(posWorld, new Color(255, 0, 255));

                //        posWorld = Main.DrawUtils.HUDtoWorld(posHud);
                //        posWorld += camMatrix.Left * sizeWorld.X + camMatrix.Up * sizeWorld.Y; // move to center of sprite?

                //        MyTransparentGeometry.AddBillboardOriented(MaterialSquare, Color.HotPink * (0.25f + ((i / (float)totalComps) / 2)), posWorld, camMatrix.Left, camMatrix.Up, sizeWorld.X, sizeWorld.Y, Vector2.Zero, BlendType);
                //    }
                //}

                // red functionality line
                int critIndex = blockDef.CriticalGroup + scrollIdxOffset;
                if(critIndex >= 0 && critIndex < totalComps)
                {
                    Vector2 sizeWorld = new Vector2(HudComponentWidth * scaleFOV, WorldBlockInfoLineHeight * scaleFOV);

                    Vector2 posHud = compListTopRight;
                    posHud.Y += HudComponentHeight * (totalComps - critIndex - 2) + HudComponentUnderlineOffset;

                    Vector3D posWorld = Main.DrawUtils.HUDtoWorld(posHud);

                    posWorld += camMatrix.Left * sizeWorld.X + camMatrix.Up * sizeWorld.Y;

                    MyTransparentGeometry.AddBillboardOriented(MaterialSquare, LineFunctionalColor, posWorld, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, sizeWorld.X, sizeWorld.Y, Vector2.Zero, BlendType);
                }

                // blue hacking line
                int compIndex = ComputerComponentIndex + scrollIdxOffset;
                if(compIndex >= 0 && compIndex < totalComps)
                {
                    Vector2 sizeWorld = new Vector2(HudComponentWidth * scaleFOV, WorldBlockInfoLineHeight * scaleFOV);

                    Vector2 posHud = compListTopRight;
                    posHud.Y += HudComponentHeight * (totalComps - compIndex - 2) + HudComponentUnderlineOffset;

                    Vector3D posWOrld = Main.DrawUtils.HUDtoWorld(posHud);

                    posWOrld += camMatrix.Left * sizeWorld.X + camMatrix.Up * (sizeWorld.Y * 3); // extra offset to allow for red line to be visible

                    MyTransparentGeometry.AddBillboardOriented(MaterialSquare, LineOwnershipColor, posWOrld, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, sizeWorld.X, sizeWorld.Y, Vector2.Zero, BlendType);
                }

                // scrollbar for scrollable components feature
                if(Main.Config.ScrollableComponentsList.Value && totalComps > maxVisibleComps)
                {
                    float scrollbarHeightRatio = 1f;
                    float scrollPos = 0f;

                    if(totalComps > maxVisibleComps)
                    {
                        scrollbarHeightRatio = ((float)maxVisibleComps / (float)totalComps);
                        scrollPos = (float)scrollIdxOffset / (float)(totalComps - maxVisibleComps);
                    }

                    Vector2 posHud = compListTopRight;
                    posHud.Y -= HudComponentHeight; // make it actually top-right

                    #region background coordinates
                    int visibleComps = Math.Min(maxVisibleComps, totalComps);
                    Vector2 bgSizeWorld = new Vector2(WorldScrollbarWidth * scaleFOV, visibleComps * WorldComponentHeight * scaleFOV);

                    Vector3D bgPosWorld = Main.DrawUtils.HUDtoWorld(posHud);
                    bgPosWorld += camMatrix.Right * bgSizeWorld.X * 2 + camMatrix.Down * bgSizeWorld.Y;
                    #endregion

                    #region bar coordinates
                    float emptySpaceHeight = (bgSizeWorld.Y * (1 - scrollbarHeightRatio));
                    Vector2 barSizeWorld = new Vector2(bgSizeWorld.X, bgSizeWorld.Y * scrollbarHeightRatio);

                    Vector3D barPosWorld = Main.DrawUtils.HUDtoWorld(posHud);
                    barPosWorld += camMatrix.Right * barSizeWorld.X * 2 + camMatrix.Down * (barSizeWorld.Y + (emptySpaceHeight * 2 * scrollPos));
                    #endregion

                    // make scrollbar smaller than the background
                    barSizeWorld.X -= WorldScrollbarInnerMargin;
                    barSizeWorld.Y -= WorldScrollbarInnerMargin;

                    // shift entire scrollbar down until it lines up with the bottom, as it looks too high because of the bottom-aligned texts on components
                    bgPosWorld += camMatrix.Down * WorldBuildInfoMargin;
                    barPosWorld += camMatrix.Down * WorldBuildInfoMargin;

                    MyTransparentGeometry.AddBillboardOriented(MaterialSquare, ScrollbarBgColor, bgPosWorld, camMatrix.Left, camMatrix.Up, bgSizeWorld.X, bgSizeWorld.Y, Vector2.Zero, BlendType);
                    MyTransparentGeometry.AddBillboardOriented(MaterialSquare, ScrollbarColor, barPosWorld, camMatrix.Left, camMatrix.Up, barSizeWorld.X, barSizeWorld.Y, Vector2.Zero, BlendType);
                }

                // different return item on grind
                for(int i = (ComponentReplaceInfoCount - 1); i >= 0; --i)
                {
                    ComponentInfo info = ComponentReplaceInfo[i];
                    int index = info.Index + scrollIdxOffset;

                    if(info.Index >= maxScrollIdx // hide for components scrolled above
                    || info.Index < scrollIdx // hide for components scrolled below
                    || info.Index == (maxVisibleComps - 1) && Main.BlockInfoScrollComponents.ShowUpHint // hide for top hint component
                    || info.Index == (totalComps - maxVisibleComps) && Main.BlockInfoScrollComponents.ShowDownHint) // hide for bottom hint component
                    {
                        //if(info.Text != null)
                        //    info.Text.Visible = false;

                        continue;
                    }

                    Vector2 hud = compListTopRight;
                    hud.Y += HudComponentHeight * (totalComps - index - 1);

                    Vector3D posWorld = Main.DrawUtils.HUDtoWorld(hud);

                    if(Main.TextAPI.IsEnabled)
                    {
                        const double LeftOffset = 0.0183;
                        const string LabelPrefix = "<color=255,255,0>Grinds to: ";
                        const int NameMaxChars = 24;
                        double textScale = 0.0012 * scaleFOV;

                        posWorld += camMatrix.Left * (LeftOffset * scaleFOV);

                        if(info.Text == null)
                        {
                            info.Text = new HudAPIv2.SpaceMessage(new StringBuilder(LabelPrefix.Length + NameMaxChars), posWorld, camMatrix.Up, camMatrix.Left, textScale, null, 2, HudAPIv2.TextOrientation.ltr, BlendType);
                            info.Text.Visible = false; // not required when using manual .Draw()
                        }
                        else
                        {
                            info.Text.WorldPosition = posWorld;
                            info.Text.Left = camMatrix.Left;
                            info.Text.Up = camMatrix.Up;
                            info.Text.Scale = textScale;
                        }

                        StringBuilder sb = info.Text.Message.Clear();
                        sb.Append(LabelPrefix);
                        sb.AppendMaxLength(info.Replaced.DisplayNameText, NameMaxChars);

                        //info.Text.Visible = true;
                        info.Text.Draw();
                    }
                    else
                    {
                        Vector2 sizeWorld = new Vector2(HudComponentWidth * scaleFOV, WorldComponentHighlightHeight * scaleFOV);

                        posWorld += camMatrix.Left * sizeWorld.X + camMatrix.Up * sizeWorld.Y;

                        MyTransparentGeometry.AddBillboardOriented(MaterialSquare, HighlightCompLossColor, posWorld, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, sizeWorld.X, sizeWorld.Y, Vector2.Zero, BlendType);
                    }
                }
            }
        }

        void DrawDotAt(Vector3D posWorld, Color color)
        {
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            float dotSize = 0.0001f;
            MyTransparentGeometry.AddBillboardOriented(MaterialSquare, color * 0.75f, posWorld, camMatrix.Left, camMatrix.Up, dotSize, dotSize, Vector2.Zero, BlendType);
        }
    }
}
