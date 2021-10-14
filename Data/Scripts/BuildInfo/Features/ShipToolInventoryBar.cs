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
    // TODO: compatible with gamepad HUD too?

    public class ShipToolInventoryBar : ModComponent
    {
        public bool Shown { get; private set; } = false;

        public readonly List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>(4);

        private bool ShouldShow = false;

        private float FilledRatio = 0f;

        private const int FillComputeEveryTicks = 30; // computes fill every these ticks
        private readonly MyStringId GrinderIconMaterial = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryGrinderIcon");
        private readonly MyStringId DrillIconMaterial = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryDrillIcon");
        private readonly MyStringId BarMaterial = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryBar");
        private readonly Vector2D DefaultSize = new Vector2D(32, 2.8) * 0.001;
        private readonly Vector4 BgColor = (new Color(80, 100, 120) * (120f / 255f)).ToVector4();
        private readonly Vector4 IconColor = Color.White.ToVector4(); // new Color(186, 238, 249).ToVector4();
        private readonly Vector4 BarColor = new Color(136, 218, 240).ToVector4();
        private const float WarnAboveFillRatio = 0.7f;
        private readonly Vector4 BarWarnColor1 = new Color(200, 80, 0).ToVector4();
        private readonly Vector4 BarWarnColor2 = new Color(200, 0, 0).ToVector4();
        private const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        public ShipToolInventoryBar(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
            Main.Config.ShipToolInvBarShow.ValueAssigned += ConfigBoolChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
            Main.Config.ShipToolInvBarShow.ValueAssigned -= ConfigBoolChanged;
        }

        void ConfigBoolChanged(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            UpdateShow();
        }

        void GameConfig_HudStateChanged(HudState prevState, HudState newState)
        {
            UpdateShow();
        }

        void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            UpdateShow();
            UpdateFillRatio(MyAPIGateway.Session.ControlledObject as IMyShipController);
        }

        void UpdateShow()
        {
            ShouldShow = (Main.GameConfig.HudState != HudState.OFF
                      && Main.Config.ShipToolInvBarShow.Value
                      //&& !MyAPIGateway.Input.IsJoystickLastUsed // not even working properly with gamepad, apart from not looking properly
                      && (Main.EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder) || Main.EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_Drill)));

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, ShouldShow);
        }

        void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(shipController == null || !ShouldShow || MyAPIGateway.Gui.IsCursorVisible)
                return;

            if(tick % FillComputeEveryTicks == 0)
            {
                UpdateFillRatio(shipController);
            }
        }

        void UpdateFillRatio(IMyShipController shipController)
        {
            if(shipController == null)
                return;

            FilledRatio = 0;

            if(Main.EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_Drill))
            {
                FilledRatio = GetFilledRatio<IMyShipDrill>(shipController.CubeGrid, Blocks);
            }
            else if(Main.EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder))
            {
                FilledRatio = GetFilledRatio<IMyShipGrinder>(shipController.CubeGrid, Blocks);
            }
        }

        static float GetFilledRatio<T>(IMyCubeGrid grid, List<IMyTerminalBlock> blocks) where T : class, IMyTerminalBlock
        {
            float volume = 0;
            float maxVolume = 0;

            blocks.Clear();

            // NOTE: ship tool toolbar item's click turns on tools beyond rotors/pistons and connectors aswell, so no filtering here.
            IMyGridTerminalSystem gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            gts?.GetBlocksOfType<T>(blocks);

            foreach(T block in blocks)
            {
                IMyInventory inv = block.GetInventory(0);
                if(inv == null)
                    continue;

                volume += (float)inv.CurrentVolume;
                maxVolume += (float)inv.MaxVolume;
            }

            blocks.Clear();

            return (maxVolume > 0 ? MathHelper.Clamp(volume / maxVolume, 0, 1) : 0);
        }

        public override void UpdateDraw()
        {
            Shown = false;

            if(!ShouldShow || MyAPIGateway.Gui.IsCursorVisible)
                return;

            Shown = true;

            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D worldPos = Main.DrawUtils.TextAPIHUDtoWorld(Main.Config.ShipToolInvBarPosition.Value);

            Vector2D size = DefaultSize * Main.Config.ShipToolInvBarScale.Value;
            float w = (float)(size.X * Main.DrawUtils.ScaleFOV);
            float h = (float)(size.Y * Main.DrawUtils.ScaleFOV);

            MyTransparentGeometry.AddBillboardOriented(BarMaterial, BgColor, worldPos, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, w, h, Vector2.Zero, blendType: BlendType);

            MyStringId iconMaterial;
            if(Main.EquipmentMonitor.IsAnyGrinder)
                iconMaterial = GrinderIconMaterial;
            else
                iconMaterial = DrillIconMaterial;

            MyTransparentGeometry.AddBillboardOriented(iconMaterial, IconColor, worldPos, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, w, h, Vector2.Zero, blendType: BlendType);

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
                float barFill = min + ((max - min) * FilledRatio);
                Vector2 uv = new Vector2(-(1 - barFill), 0);

                worldPos += camMatrix.Left * ((1 - barFill) * size.X * 2 * Main.DrawUtils.ScaleFOV);

                MyTransparentGeometry.AddBillboardOriented(BarMaterial, color, worldPos, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, w, h, uv, blendType: BlendType);
            }
        }
    }
}
