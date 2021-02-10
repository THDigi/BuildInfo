using System;
using Digi.BuildInfo.Systems;
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

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
        }

        protected override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(paused || inMenu)
                return;

            if(Config.LockOverlayBind.Value.IsJustPressed())
            {
                LockOverlayToAimedBlock();
            }
        }

        protected override void UpdateDraw()
        {
            if(LockedOnBlock == null)
                return;

            // alternate update for when overlays doesn't update this
            if(Overlays.DrawOverlay > 0)
                return;

            var def = EquipmentMonitor.BlockDef;
            var aimedBlock = EquipmentMonitor.AimedBlock;
            var cellSize = EquipmentMonitor.BlockGridSize;
            LockOverlay.UpdateLockedOnBlock(ref aimedBlock, ref def, ref cellSize);
        }

        public void LockOverlayToAimedBlock()
        {
            if(LockedOnBlock == null && EquipmentMonitor.AimedBlock == null)
            {
                SetNotification("Aim at a block with welder/grinder first.", 2000, FontsHandler.RedSh);
                return;
            }

            SetLockOnBlock(EquipmentMonitor.AimedBlock);
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

            if(Overlays.DrawOverlay == 0)
                SetNotification($"Overlays off. [{Config.CycleOverlaysBind.Value.GetBinds()}] to cycle, or [{Config.LockOverlayBind.Value.GetBinds()}] to unlock.", 100);
            else
                SetNotification($"{LockedOnBlockDef.DisplayNameText}. [{Config.LockOverlayBind.Value.GetBinds()}] to change/unlock.", 100);

            return true;
        }

        void SetLockOnBlock(IMySlimBlock block, string message = null)
        {
            Overlays.HideLabels();
            Overlays.SetOverlayCallFor(null);
            LockedOnBlockDef = null;

            // unhook previous
            if(LockedOnBlock != null)
            {
                if(LockedOnBlock.FatBlock != null)
                    LockedOnBlock.FatBlock.OnMarkForClose -= LockedOnBlock_MarkedForClose;
            }

            LockedOnBlock = block;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (LockedOnBlock != null));

            // hook new
            if(LockedOnBlock != null)
            {
                if(LockedOnBlock.FatBlock != null)
                    LockedOnBlock.FatBlock.OnMarkForClose += LockedOnBlock_MarkedForClose;

                LockedOnBlockDef = (MyCubeBlockDefinition)LockedOnBlock.BlockDefinition;

                Overlays.SetOverlayCallFor(LockedOnBlockDef.Id);
            }
            else
            {
                SetNotification(message ?? "Turned off");
            }
        }

        void SetNotification(string message, int aliveTimeMs = 2000, string font = FontsHandler.WhiteSh)
        {
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
