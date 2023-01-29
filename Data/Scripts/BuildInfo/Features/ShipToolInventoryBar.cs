using System;
using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features
{
    // TODO: compatible with gamepad HUD too?
    // TODO: show ship tool inventory bar for CTC?

    public class ShipToolInventoryBar : ModComponent
    {
        public bool Shown { get; private set; } = false;

        bool ShouldUpdate;
        bool UsingTool;
        MyStringId BarIcon;
        float FilledRatio;
        int CanRefreshAfterTick;

        List<IMyCubeGrid> TempGrids = new List<IMyCubeGrid>();

        static readonly MyObjectBuilderType TypeGrinder = typeof(MyObjectBuilder_ShipGrinder);
        static readonly MyObjectBuilderType TypeDrill = typeof(MyObjectBuilder_Drill);

        const int FillComputeEveryTicks = 30; // computes fill every these ticks
        readonly MyStringId GrinderIconMaterial = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryGrinderIcon");
        readonly MyStringId DrillIconMaterial = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryDrillIcon");
        readonly MyStringId BarMaterial = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryBar");
        readonly Vector2D DefaultSize = new Vector2D(32, 2.8) * 0.001;
        readonly Vector4 BgColor = (new Color(80, 100, 120) * (120f / 255f)).ToVector4();
        readonly Vector4 IconColor = Color.White.ToVector4(); // new Color(186, 238, 249).ToVector4();
        readonly Vector4 BarColor = new Color(136, 218, 240).ToVector4();
        const float WarnAboveFillRatio = 0.7f;
        readonly Vector4 BarWarnColor1 = new Color(200, 80, 0).ToVector4();
        readonly Vector4 BarWarnColor2 = new Color(200, 0, 0).ToVector4();
        const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        public ShipToolInventoryBar(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.ControlledChanged += EquipmentMonitor_ControlledChanged;
            Main.Config.ShipToolInvBarShow.ValueAssigned += ConfigBoolChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.ControlledChanged -= EquipmentMonitor_ControlledChanged;
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
            UpdateShow(force: true);
        }

        void EquipmentMonitor_ControlledChanged(IMyControllableEntity controlled)
        {
            UpdateShow(force: true);
        }

        void UpdateShow(bool force = false)
        {
            if(!force && Main.Tick < CanRefreshAfterTick)
                return;

            CanRefreshAfterTick = Main.Tick + FillComputeEveryTicks;

            TempGrids.Clear();

            IMyShipController ctrl = MyAPIGateway.Session.ControlledObject as IMyShipController;
            ShouldUpdate = ctrl != null && ctrl.CanControlShip && !MyAPIGateway.Input.IsJoystickLastUsed && Main.GameConfig.HudState != HudState.OFF && Main.Config.ShipToolInvBarShow.Value;
            UsingTool = Main.EquipmentMonitor.ToolDefId.TypeId == TypeGrinder || Main.EquipmentMonitor.ToolDefId.TypeId == TypeDrill;

            if(UsingTool)
                BarIcon = Main.EquipmentMonitor.IsAnyGrinder ? GrinderIconMaterial : DrillIconMaterial;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, ShouldUpdate);

            if(ShouldUpdate && !UsingTool)
            {
                MyAPIGateway.GridGroups.GetGroup(ctrl.CubeGrid, GridLinkTypeEnum.Logical, TempGrids);

                float highestFilledGrinder = 0f;
                float highestFilledDrill = 0f;
                bool grindersOn = false;
                bool drillsOn = false;

                foreach(MyCubeGrid grid in TempGrids)
                {
                    int grinders = grid.BlocksCounters.GetValueOrDefault(TypeGrinder, 0);
                    int drills = grid.BlocksCounters.GetValueOrDefault(TypeDrill, 0);

                    if(grinders == 0 && drills == 0)
                        continue;

                    foreach(MyCubeBlock block in grid.GetFatBlocks())
                    {
                        MyObjectBuilderType typeId = block.BlockDefinition.Id.TypeId;
                        if(typeId != TypeGrinder && typeId != TypeDrill)
                            continue;

                        IMyInventory inv = block.GetInventory(0);
                        if(inv == null || inv.MaxVolume <= 0)
                            continue;

                        float filled = (float)inv.CurrentVolume / (float)inv.MaxVolume;
                        IMyFunctionalBlock functional = (IMyFunctionalBlock)block;

                        if(typeId == TypeGrinder)
                        {
                            grindersOn |= functional.Enabled;
                            highestFilledGrinder = Math.Max(highestFilledGrinder, filled);
                        }
                        else
                        {
                            drillsOn |= functional.Enabled;
                            highestFilledDrill = Math.Max(highestFilledDrill, filled);
                        }
                    }
                }

                if(grindersOn && drillsOn)
                {
                    // I don't even
                }
                else if(grindersOn)
                {
                    UsingTool = true;
                    FilledRatio = highestFilledGrinder;
                    BarIcon = GrinderIconMaterial;
                }
                else if(drillsOn)
                {
                    UsingTool = true;
                    FilledRatio = highestFilledDrill;
                    BarIcon = DrillIconMaterial;
                }

                TempGrids.Clear();
            }
        }

        public override void UpdateDraw()
        {
            Shown = false;

            if(!ShouldUpdate || MyAPIGateway.Gui.IsCursorVisible)
                return;

            if(Main.Tick % FillComputeEveryTicks == 0)
                UpdateShow();

            if(!UsingTool)
                return;

            Shown = true;

            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D worldPos = Main.DrawUtils.TextAPIHUDtoWorld(Main.Config.ShipToolInvBarPosition.Value);

            Vector2D size = DefaultSize * Main.Config.ShipToolInvBarScale.Value;
            float w = (float)(size.X * Main.DrawUtils.ScaleFOV);
            float h = (float)(size.Y * Main.DrawUtils.ScaleFOV);

            MyTransparentGeometry.AddBillboardOriented(BarMaterial, BgColor, worldPos, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, w, h, Vector2.Zero, blendType: BlendType);
            MyTransparentGeometry.AddBillboardOriented(BarIcon, IconColor, worldPos, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, w, h, Vector2.Zero, blendType: BlendType);

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
