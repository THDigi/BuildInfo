using System;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays
{
    public class LockOverlay : ModComponent
    {
        public IMySlimBlock LockedOnBlock { get; private set; }
        public MyCubeBlockDefinition LockedOnBlockDef { get; private set; }
        public event Action<IMySlimBlock> LockedOnBlockChanged;

        private IMyHudNotification Notification;

        const string NotifyPrefix = "Overlay Lock-On: ";

        public LockOverlay(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.ToolChanged += ToolChanged;
        }

        public override void UnregisterComponent()
        {
            Main.EquipmentMonitor.ToolChanged -= ToolChanged;
        }

        void ToolChanged(MyDefinitionId toolDefId)
        {
            EquipmentMonitor eq = Main.EquipmentMonitor;
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, (LockedOnBlock != null || eq.IsCubeBuilder || eq.IsAnyWelder || eq.IsAnyGrinder));
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(paused || inMenu)
                return;

            if(LockedOnBlock != null)
            {
                if(Main.Config.LockOverlayBind.Value.IsJustPressed())
                    SetLockOnBlock(null);
            }
            else
            {
                EquipmentMonitor eq = Main.EquipmentMonitor;
                if((eq.IsCubeBuilder || eq.IsAnyWelder || eq.IsAnyGrinder) && Main.Config.LockOverlayBind.Value.IsJustPressed())
                {
                    IMySlimBlock aimed = eq.AimedBlock ?? eq.BuilderAimedBlock;

                    if(aimed == null)
                        SetNotification("Aim at a block with welder/grinder/builder first.", 2000, FontsHandler.RedSh);
                    else
                        SetLockOnBlock(aimed);
                }
            }
        }

        public override void UpdateDraw()
        {
            if(Main.IsPaused)
                return;

            if(LockedOnBlock == null)
                return;

            if(LockedOnBlock.IsFullyDismounted || LockedOnBlock.IsDestroyed || (LockedOnBlock.FatBlock != null && LockedOnBlock.FatBlock.MarkedForClose))
            {
                SetLockOnBlock(null, "Block removed, disabled overlay lock.");
                return;
            }

            Vector3D blockPos;
            double maxRangeSq = 3;

            if(LockedOnBlock.FatBlock != null)
            {
                BoundingSphereD blockVolume = LockedOnBlock.FatBlock.WorldVolume;
                maxRangeSq = blockVolume.Radius;
                blockPos = blockVolume.Center;
            }
            else
            {
                LockedOnBlock.ComputeWorldCenter(out blockPos);
            }

            maxRangeSq = (maxRangeSq + 20) * 2;
            maxRangeSq *= maxRangeSq;

            if(Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.WorldMatrix.Translation, blockPos) > maxRangeSq)
            {
                SetLockOnBlock(null, "Turned off, too far.");
                return;
            }

            if(Main.Overlays.OverlayMode == Overlays.ModeEnum.Off)
                SetNotification($"Overlays off. [{Main.Config.CycleOverlaysBind.Value.GetBinds()}] to cycle, or [{Main.Config.LockOverlayBind.Value.GetBinds()}] to unlock.", 100);
            else
                SetNotification($"{LockedOnBlockDef.DisplayNameText}. [{Main.Config.LockOverlayBind.Value.GetBinds()}] to change/unlock.", 100);
        }

        void SetLockOnBlock(IMySlimBlock block, string message = null)
        {
            LockedOnBlockDef = null;

            // unhook previous
            if(LockedOnBlock != null)
            {
                LockedOnBlock.CubeGrid.OnBlockRemoved -= LockedOnGrid_OnBlockRemoved;
            }

            LockedOnBlock = block;

            try
            {
                LockedOnBlockChanged?.Invoke(block);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (LockedOnBlock != null));

            // hook new
            if(LockedOnBlock != null)
            {
                LockedOnBlockDef = (MyCubeBlockDefinition)LockedOnBlock.BlockDefinition;
                LockedOnBlock.CubeGrid.OnBlockRemoved += LockedOnGrid_OnBlockRemoved;
            }
            else
            {
                SetNotification(message ?? "Turned off");
            }
        }

        void LockedOnGrid_OnBlockRemoved(IMySlimBlock block)
        {
            try
            {
                if(block == null)
                    return;

                if(LockedOnBlock == block)
                {
                    SetLockOnBlock(null, "Turned off, block was removed.");
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void SetNotification(string message, int aliveTimeMs = 2000, string font = FontsHandler.WhiteSh)
        {
            if(Main.IsPaused)
                return; // avoid notification glitching out if showing them continuously when game is paused

            if(Notification == null)
                Notification = MyAPIGateway.Utilities.CreateNotification("");

            Notification.Hide(); // required since SE v1.194
            Notification.AliveTime = aliveTimeMs;
            Notification.Font = font;
            Notification.Text = NotifyPrefix + message;
            Notification.Show();
        }
    }
}
