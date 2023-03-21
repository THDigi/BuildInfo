using System;
using System.Collections.Generic;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features.Overlays
{
    public class Overlays : ModComponent
    {
        public ModeEnum OverlayMode { get; private set; }
        public string OverlayModeName { get; private set; }

        public enum ModeEnum { Off, AirtightAndSpecialized, MountPoints, Ports }
        public static readonly string[] OverlayNames = new[] { "OFF", "Airtightness + Specialized", "Mount Points", "Ports" };

        readonly OverlayDrawInstance[] DrawInstances;
        public OverlayDrawInstance DrawInstanceBuilderHeld;
        OverlayDrawInstance DrawInstanceToolAimed;
        //OverlayDrawInstance DrawInstanceBuilderAimed;
        OverlayDrawInstance DrawInstanceLockedOn;

        IMyHudNotification OverlayNotification;

        public Overlays(BuildInfoMod main) : base(main)
        {
            UpdateOrder = -500; // for Draw() mainly, to always render first (and therefore, under)

            DrawInstances = new OverlayDrawInstance[]
            {
                DrawInstanceBuilderHeld = new OverlayDrawInstance(this, "CubePlacer"),
                DrawInstanceToolAimed = new OverlayDrawInstance(this, "Aimed by Tool"),
                //DrawInstanceBuilderAimed = new OverlayDrawInstance(this, "Aimed by Placer"),
                DrawInstanceLockedOn = new OverlayDrawInstance(this, "Lock-On"),
            };

            DrawInstanceBuilderHeld.DrawBuildStageMounts = new List<MyTuple<MatrixD, int>>(0);

            int[] modeValues = (int[])Enum.GetValues(typeof(ModeEnum));
            if(OverlayNames.Length != modeValues.Length)
                throw new Exception("Not all overlay modes have names or vice-versa!");
        }

        public override void RegisterComponent()
        {
            Main.LockOverlay.LockedOnBlockChanged += LockedOnBlockChanged;

            Main.EquipmentMonitor.BlockChanged += AimedOrEquippedBlockChanged;
            //Main.EquipmentMonitor.BuilderAimedBlockChanged += EquipmentMonitor_BuilderAimedBlockChanged;

            Main.EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;

            SetMode(ModeEnum.Off, showNotification: false);
        }

        public override void UnregisterComponent()
        {
            Main.LockOverlay.LockedOnBlockChanged -= LockedOnBlockChanged;

            Main.EquipmentMonitor.BlockChanged -= AimedOrEquippedBlockChanged;
            //Main.EquipmentMonitor.BuilderAimedBlockChanged -= EquipmentMonitor_BuilderAimedBlockChanged;

            Main.EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
        }

        public void CycleMode(bool showNotification = true)
        {
            int mode = (int)OverlayMode;
            if(++mode >= OverlayNames.Length)
                mode = 0;
            SetMode((ModeEnum)mode, showNotification);
        }

        public void SetMode(ModeEnum setMode, bool showNotification = true)
        {
            int mode = (int)setMode;
            if(mode < 0 || mode >= OverlayNames.Length)
                throw new Exception($"Unknown mode: {setMode.ToString()}");

            OverlayMode = setMode;
            OverlayModeName = OverlayNames[mode];
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, CheckNeedsDraw());

            if(showNotification)
            {
                if(OverlayNotification == null)
                    OverlayNotification = MyAPIGateway.Utilities.CreateNotification("", 2000, FontsHandler.WhiteSh);

                OverlayNotification.Hide(); // required since SE v1.194
                OverlayNotification.Text = $"Overlays: [{OverlayModeName}]";
                OverlayNotification.Show();
            }
        }

        void LockedOnBlockChanged(IMySlimBlock slimBlock)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, CheckNeedsDraw());
        }

        void AimedOrEquippedBlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, CheckNeedsDraw());
        }

        //void EquipmentMonitor_BuilderAimedBlockChanged(IMySlimBlock slimBlock)
        //{
        //    SetUpdateMethods(UpdateFlags.UPDATE_DRAW, CheckNeedsDraw());
        //}

        bool CheckNeedsDraw()
        {
            if(OverlayMode == ModeEnum.Off && (!Main.Config.OverlayLockRememberMode.Value || Main.LockOverlay.LockedOnBlock == null))
                return false;

            return Main.EquipmentMonitor.BlockDef != null
            //  || Main.EquipmentMonitor.BuilderAimedBlock != null
                || Main.LockOverlay.LockedOnBlock != null;
        }

        void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            // TODO: move ship cockpit tool aiming line to always be shown? + config option
            if(shipController != null && Main.EquipmentMonitor.IsBuildTool && OverlayMode != ModeEnum.Off)
            {
                const BlendTypeEnum BlendType = BlendTypeEnum.SDR;
                const float ReachDistance = Hardcoded.ShipTool_ReachDistance;
                Vector4 color = new Vector4(2f, 0, 0, 0.1f); // above 1 color creates bloom

                MyShipControllerDefinition shipCtrlDef = (MyShipControllerDefinition)shipController.SlimBlock.BlockDefinition;

                // only show laser when it's offset from view
                if(!Vector3.IsZero(shipCtrlDef.RaycastOffset, 0.01f))
                {
                    MatrixD m = shipController.WorldMatrix;
                    m.Translation = Vector3D.Transform(shipCtrlDef.RaycastOffset, m);

                    MyTransparentGeometry.AddLineBillboard(OverlayDrawInstance.MaterialLaser, color, m.Translation, (Vector3)m.Forward, ReachDistance, 0.005f, blendType: BlendType);
                    MyTransparentGeometry.AddPointBillboard(OverlayDrawInstance.MaterialDot, color, m.Translation + m.Forward * ReachDistance, 0.015f, 0f, blendType: BlendType);
                }
            }
        }

        public override void UpdateDraw()
        {
            if(OverlayMode == ModeEnum.Off && (!Main.Config.OverlayLockRememberMode.Value || Main.LockOverlay.LockedOnBlock == null))
                return;

            if(Main.GameConfig.HudState == HudState.OFF && !Main.Config.OverlaysAlwaysVisible.Value)
                return;

            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

            IMySlimBlock lockedOnBlock = Main.LockOverlay.LockedOnBlock;
            if(lockedOnBlock != null)
            {
                ModeEnum? overrideMode = (Main.Config.OverlayLockRememberMode.Value ? Main.LockOverlay.LockedOnMode : (ModeEnum?)null);
                DrawInstanceLockedOn.Draw(Main.LockOverlay.LockedOnBlockDef, lockedOnBlock, overrideMode);
            }

            IMySlimBlock toolAimedBlock = Main.EquipmentMonitor.AimedBlock;
            if(toolAimedBlock != null && lockedOnBlock != toolAimedBlock)
            {
                DrawInstanceToolAimed.Draw(Main.EquipmentMonitor.BlockDef, toolAimedBlock);
            }

            if(Main.EquipmentMonitor.IsCubeBuilder)
            {
                //IMySlimBlock builderAimedBlock = Main.EquipmentMonitor.BuilderAimedBlock;
                //if(builderAimedBlock != null && lockedOnBlock != builderAimedBlock)
                //{
                //    DrawInstanceBuilderAimed.Draw((MyCubeBlockDefinition)builderAimedBlock.BlockDefinition, builderAimedBlock);
                //}

                MyCubeBlockDefinition heldBlockDef = Main.EquipmentMonitor.BlockDef;
                if(heldBlockDef != null && !(MyAPIGateway.Session.IsCameraUserControlledSpectator && !Utils.CreativeToolsEnabled))
                {
                    DrawInstanceBuilderHeld.Draw(heldBlockDef, null);
                }
            }
        }
    }
}
