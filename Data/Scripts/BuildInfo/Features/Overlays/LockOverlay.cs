using System;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays
{
    public class LockOverlay : ModComponent
    {
        public IMySlimBlock LockedOnBlock { get; private set; }
        public MyCubeBlockDefinition LockedOnBlockDef { get; private set; }
        private IMyHudNotification Notification;

        const string NotifyPrefix = "Overlay Lock-On: ";

        public LockOverlay(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_INPUT;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(paused || inMenu)
                return;

            if(Main.Config.LockOverlayBind.Value.IsJustPressed())
            {
                LockOverlayToAimedBlock();
            }
        }

        public override void UpdateDraw()
        {
            if(LockedOnBlock == null)
                return;

            // alternate update for when overlays doesn't update this
            if(Main.Overlays.DrawOverlay > 0)
                return;

            var def = Main.EquipmentMonitor.BlockDef;
            var aimedBlock = Main.EquipmentMonitor.AimedBlock;
            var cellSize = Main.EquipmentMonitor.BlockGridSize;
            Main.LockOverlay.UpdateLockedOnBlock(ref aimedBlock, ref def, ref cellSize);
        }

        public void LockOverlayToAimedBlock()
        {
            if(LockedOnBlock == null && Main.EquipmentMonitor.AimedBlock == null)
            {
                SetNotification("Aim at a block with welder/grinder first.", 2000, FontsHandler.RedSh);
                return;
            }

            SetLockOnBlock(Main.EquipmentMonitor.AimedBlock);
        }

        public bool UpdateLockedOnBlock(ref IMySlimBlock aimedBlock, ref MyCubeBlockDefinition def, ref float cellSize)
        {
            if(LockedOnBlock.IsFullyDismounted || LockedOnBlock.IsDestroyed || (LockedOnBlock != null && LockedOnBlock.FatBlock == null))
            {
                SetLockOnBlock(null, "Block removed, disabled overlay lock.");
                return false;
            }

            Vector3D blockPos;
            double maxRangeSq = 3;

            if(LockedOnBlock.FatBlock != null)
            {
                var blockVolume = LockedOnBlock.FatBlock.WorldVolume;
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
                return false;
            }

            aimedBlock = LockedOnBlock;
            def = LockedOnBlockDef;
            cellSize = LockedOnBlock.CubeGrid.GridSize;

            if(Main.Overlays.DrawOverlay == 0)
                SetNotification($"Overlays off. [{Main.Config.CycleOverlaysBind.Value.GetBinds()}] to cycle, or [{Main.Config.LockOverlayBind.Value.GetBinds()}] to unlock.", 100);
            else
                SetNotification($"{LockedOnBlockDef.DisplayNameText}. [{Main.Config.LockOverlayBind.Value.GetBinds()}] to change/unlock.", 100);

            return true;
        }

        void SetLockOnBlock(IMySlimBlock block, string message = null)
        {
            Main.Overlays.HideLabels();
            Main.Overlays.SetOverlayCallFor(null);
            LockedOnBlockDef = null;

            // unhook previous
            if(LockedOnBlock != null)
            {
                if(LockedOnBlock.FatBlock != null)
                    LockedOnBlock.FatBlock.OnMarkForClose -= LockedOnBlock_MarkedForClose;

                Main.Overlays.SetOverlayCallFor(null);
            }

            LockedOnBlock = block;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (LockedOnBlock != null));

            // hook new
            if(LockedOnBlock != null)
            {
                if(LockedOnBlock.FatBlock != null)
                    LockedOnBlock.FatBlock.OnMarkForClose += LockedOnBlock_MarkedForClose;

                LockedOnBlockDef = (MyCubeBlockDefinition)LockedOnBlock.BlockDefinition;

                Main.Overlays.SetOverlayCallFor(LockedOnBlockDef.Id);
            }
            else
            {
                SetNotification(message ?? "Turned off");
            }
        }

        void SetNotification(string message, int aliveTimeMs = 2000, string font = FontsHandler.WhiteSh)
        {
            if(aliveTimeMs < 1000 && Main.IsPaused)
                return; // HACK: avoid notification glitching out if showing them continuously when game is paused

            if(Notification == null)
                Notification = MyAPIGateway.Utilities.CreateNotification("");

            Notification.Hide(); // required since SE v1.194
            Notification.AliveTime = aliveTimeMs;
            Notification.Font = font;
            Notification.Text = NotifyPrefix + message;
            Notification.Show();
        }

        private void LockedOnBlock_MarkedForClose(IMyEntity ent)
        {
            try
            {
                var block = ent as IMyCubeBlock;
                if(block != null)
                    block.OnMarkForClose -= LockedOnBlock_MarkedForClose;

                if(LockedOnBlock == null || block != LockedOnBlock.FatBlock)
                    return;

                SetLockOnBlock(null, "Turned off, block was removed.");
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
