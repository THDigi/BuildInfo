using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features
{
    // TODO: configurable position and scale too?

    public class ShipToolInventoryBar : ModComponent
    {
        public bool Shown { get; private set; } = false;

        private bool ShouldShow = false;

        private float FilledRatio = 0f;
        private int FillComputeTick = SkipTicks;
        private const int SkipTicks = 10; // how many ticks to skip to run one fill ratio update
        private readonly List<IMyShipDrill> Drills = new List<IMyShipDrill>();
        private readonly List<IMyShipGrinder> Grinders = new List<IMyShipGrinder>();

        private readonly MyStringId IconMaterial = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryIcon");
        private readonly MyStringId BarMaterial = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryBar");
        private readonly Vector2 HudPosition = new Vector2(0.5f, 0.835f); // textures are designed to be centered on the HUD
        private readonly Vector2 Size = new Vector2(32f, 2.8f) * 0.001f;
        private readonly Vector4 BgColor = (new Color(80, 100, 120) * (120f / 255f)).ToVector4();
        private readonly Vector4 IconColor = new Color(186, 238, 249).ToVector4();
        private readonly Vector4 BarColor = new Color(136, 218, 240).ToVector4();
        private const float WarnAboveFillRatio = 0.7f;
        private readonly Vector4 BarWarnColor1 = new Color(200, 80, 0).ToVector4();
        private readonly Vector4 BarWarnColor2 = new Color(200, 0, 0).ToVector4();
        private const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        public ShipToolInventoryBar(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_DRAW;
        }

        protected override void RegisterComponent()
        {
            GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        protected override void UnregisterComponent()
        {
            GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
        }

        private void GameConfig_HudStateChanged(HudState prevState, HudState newState)
        {
            UpdateShow();
        }

        private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            UpdateShow();
        }

        private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(shipController != null && ShouldShow)
            {
                if(++FillComputeTick > SkipTicks)
                {
                    FillComputeTick = 0;

                    if(TextAPIEnabled && Main.Config.ShipToolInventoryBar.Value && !MyAPIGateway.Gui.IsCursorVisible)
                        ComputeFillRatio();
                }
            }
        }

        private void UpdateShow()
        {
            ShouldShow = (GameConfig.HudState != HudState.OFF && (EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder) || EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_Drill)));

            //show = (EquipmentMonitor.IsAnyTool
            //    && !EquipmentMonitor.IsCubeBuilder
            //    && EquipmentMonitor.HandEntity == null
            //    && GameConfig.HudState != HudState.OFF);
        }

        private void ComputeFillRatio()
        {
            FilledRatio = 0;

            var shipController = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipController == null)
                return;

            if(EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_Drill))
            {
                FilledRatio = GetFilledRatio(shipController.CubeGrid, Drills);
            }
            else if(EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder))
            {
                FilledRatio = GetFilledRatio(shipController.CubeGrid, Grinders);
            }
        }

        private static float GetFilledRatio<T>(IMyCubeGrid grid, List<T> blocks) where T : class, IMyTerminalBlock
        {
            float volume = 0;
            float maxVolume = 0;

            // ship tool toolbar actuates tools beyond rotors/pistons and connectors!
            var GTS = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            GTS.GetBlocksOfType(blocks);

            foreach(T block in blocks)
            {
                var inv = block.GetInventory(0);

                if(inv == null)
                    continue;

                volume += (float)inv.CurrentVolume;
                maxVolume += (float)inv.MaxVolume;
            }

            blocks.Clear();

            return (maxVolume > 0 ? MathHelper.Clamp(volume / maxVolume, 0, 1) : 0);
        }

        protected override void UpdateDraw()
        {
            Shown = false;

            if(!ShouldShow || !TextAPIEnabled || !Main.Config.ShipToolInventoryBar.Value || MyAPIGateway.Gui.IsCursorVisible)
                return;

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var worldPos = DrawUtils.HUDtoWorld(HudPosition);
            float w = Size.X * DrawUtils.ScaleFOV;
            float h = Size.Y * DrawUtils.ScaleFOV;

            MyTransparentGeometry.AddBillboardOriented(BarMaterial, BgColor, worldPos, camMatrix.Left, camMatrix.Up, w, h, Vector2.Zero, blendType: BlendType);
            MyTransparentGeometry.AddBillboardOriented(IconMaterial, IconColor, worldPos, camMatrix.Left, camMatrix.Up, w, h, Vector2.Zero, blendType: BlendType);

            if(FilledRatio > 0)
            {
                Vector4 color = BarColor;
                if(FilledRatio > WarnAboveFillRatio)
                {
                    float lerpAmount = ((FilledRatio - WarnAboveFillRatio) / (1 - WarnAboveFillRatio));

                    // dividing lerp into 2 sections so that it can lerp nicer to red.

                    lerpAmount *= 2;
                    if(lerpAmount <= 1f)
                        color = Vector4.Lerp(color, BarWarnColor1, lerpAmount);
                    else
                        color = Vector4.Lerp(BarWarnColor1, BarWarnColor2, lerpAmount - 1);
                }

                // UV cutoff, quite manually tweaked...
                const float min = 0.06f;
                const float max = 0.98f;
                var barFill = min + ((max - min) * FilledRatio);
                var uv = new Vector2(-(1 - barFill), 0);

                worldPos += camMatrix.Left * ((1 - barFill) * Size.X * 2 * DrawUtils.ScaleFOV);

                MyTransparentGeometry.AddBillboardOriented(BarMaterial, color, worldPos, camMatrix.Left, camMatrix.Up, w, h, uv, blendType: BlendType);
            }

            Shown = true;
        }
    }
}
