using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum; // HACK allows the use of BlendTypeEnum which is whitelisted but bypasses accessing MyBillboard which is not whitelisted

namespace Digi.BuildInfo.Features
{
    public class BlockInfoAdditions : ClientComponent
    {
        public readonly MyStringId LINE_MATERIAL = MyStringId.GetOrCompute("BuildInfo_Square");
        private const BlendTypeEnum BLEND_TYPE = BlendTypeEnum.SDR;
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

        private int computerCompIndex = -1;
        private List<CompLoss> componentLossIndexes = new List<CompLoss>();
        private class CompLoss
        {
            public readonly int Index;
            public readonly MyPhysicalItemDefinition Replaced;
            public HudAPIv2.SpaceMessage Msg;

            public CompLoss(int index, MyPhysicalItemDefinition item)
            {
                Index = index;
                Replaced = item;
            }

            public void Close()
            {
                Msg?.DeleteMessage();
            }
        }

        public BlockInfoAdditions(Client mod) : base(mod)
        {
            Flags = UpdateFlags.UPDATE_DRAW | UpdateFlags.UPDATE_AFTER_SIM;
        }

        public override void RegisterComponent()
        {
            EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
        }

        public override void UnregisterComponent()
        {
            EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, VRage.Game.ModAPI.IMySlimBlock block)
        {
            computerCompIndex = -1;

            foreach(var data in componentLossIndexes)
            {
                data.Close();
            }

            componentLossIndexes.Clear();

            if(def != null)
            {
                for(int i = 0; i < def.Components.Length; ++i)
                {
                    var comp = def.Components[i];

                    if(computerCompIndex == -1 && comp.Definition.Id.TypeId == typeof(MyObjectBuilder_Component) && comp.Definition.Id.SubtypeId == Constants.COMPUTER_COMPONENT_NAME) // HACK this is what the game checks internally, hardcoded to computer component.
                    {
                        computerCompIndex = i;
                    }

                    if(comp.DeconstructItem != comp.Definition)
                    {
                        componentLossIndexes.Add(new CompLoss(i, comp.DeconstructItem));
                    }
                }
            }
        }

        public override void UpdateDraw()
        {
            if(GameConfig.HudState == HudState.OFF || EquipmentMonitor.BlockDef == null || MyAPIGateway.Gui.IsCursorVisible)
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
            #endregion
#endif

            #region Lines on top of block info
            if(Config.BlockInfoStages)
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
                if(computerCompIndex != -1)
                {
                    var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * DrawUtils.ScaleFOV, BLOCKINFO_LINE_HEIGHT * DrawUtils.ScaleFOV);

                    var hud = posCompList;
                    hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - computerCompIndex - 2) + BLOCKINFO_COMPONENT_UNDERLINE_OFFSET;

                    var worldPos = DrawUtils.HUDtoWorld(hud);

                    worldPos += camMatrix.Left * size.X + camMatrix.Up * (size.Y * 3); // extra offset to allow for red line to be visible

                    MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, BLOCKINFO_LINE_OWNERSHIP, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                }

                // different return item on grind
                for(int i = componentLossIndexes.Count - 1; i >= 0; --i)
                {
                    var data = componentLossIndexes[i];

                    var hud = posCompList;
                    hud.Y += BLOCKINFO_COMPONENT_HEIGHT * (totalComps - data.Index - 1);

                    var worldPos = DrawUtils.HUDtoWorld(hud);

                    if(TextAPIEnabled)
                    {
                        const double LEFT_OFFSET = 0.0183;
                        const double TEXT_SCALE = 0.00055;
                        string textColor = "<color=255,255,0>";
                        int maxCharacters = textColor.Length + 33;

                        worldPos += camMatrix.Left * (LEFT_OFFSET * DrawUtils.ScaleFOV);

                        if(data.Msg == null)
                        {
                            var text = new StringBuilder().Append(textColor).Append("Grinds to: ").Append(data.Replaced.DisplayNameText);

                            if(text.Length > maxCharacters)
                            {
                                text.Length = maxCharacters;
                                text.Append('…');
                            }

                            data.Msg = new HudAPIv2.SpaceMessage(text, worldPos, camMatrix.Up, camMatrix.Left, TEXT_SCALE, null, 2, HudAPIv2.TextOrientation.ltr, BLEND_TYPE);
                        }
                        else
                        {
                            data.Msg.WorldPosition = worldPos;
                            data.Msg.Left = camMatrix.Left;
                            data.Msg.Up = camMatrix.Up;
                            data.Msg.TimeToLive = 2;
                        }

                        data.Msg.Draw();
                    }
                    else
                    {
                        var size = new Vector2(BLOCKINFO_COMPONENT_WIDTH * DrawUtils.ScaleFOV, BLOCKINFO_COMPONENT_HIGHLIGHT_HEIGHT * DrawUtils.ScaleFOV);

                        worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                        MyTransparentGeometry.AddBillboardOriented(LINE_MATERIAL, BLOCKINFO_LINE_COMPLOSS, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLEND_TYPE);
                    }
                }
            }
            #endregion
        }
    }
}
