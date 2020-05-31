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
        private IMyHudNotification Notification;

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
            if(!paused && !inMenu && Config.LockOverlayBind.Value.IsJustPressed())
            {
                LockOverlayToAimedBlock();
            }
        }

        public void LockOverlayToAimedBlock()
        {
            if(LockedOnBlock == null && EquipmentMonitor.AimedBlock == null)
            {
                NotifyLockOverlay("Aim at a block with welder/grinder first.", 2000, MyFontEnum.Red);
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
                SetLockOnBlock(null, "Too far from block, disabled overlay lock.");
                return false;
            }

            aimedBlock = LockedOnBlock;
            def = LockedOnBlockDef;
            cellSize = LockedOnBlock.CubeGrid.GridSize;

            if(!MyParticlesManager.Paused && Notification != null)
            {
                Notification.Show();
            }

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

            // hook new
            if(LockedOnBlock != null)
            {
                if(LockedOnBlock.FatBlock != null)
                    LockedOnBlock.FatBlock.OnMarkForClose += LockedOnBlock_MarkedForClose;

                LockedOnBlockDef = (MyCubeBlockDefinition)LockedOnBlock.BlockDefinition;

                Overlays.SetOverlayCallFor(LockedOnBlockDef.Id);

                NotifyLockOverlay($"Locked overlay to {LockedOnBlockDef.DisplayNameText}. [{Config.LockOverlayBind.Value.GetBinds()}] to unlock/change to aimed.", 100);
            }
            else
            {
                NotifyLockOverlay(message ?? "Turned off overlay lock.");
            }
        }

        void NotifyLockOverlay(string message, int aliveTimeMs = 2000, string font = MyFontEnum.White)
        {
            if(Notification == null)
                Notification = MyAPIGateway.Utilities.CreateNotification("");

            Notification.Hide(); // required since SE v1.194
            Notification.AliveTime = aliveTimeMs;
            Notification.Font = font;
            Notification.Text = message;
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

                SetLockOnBlock(null, "Block removed, disabled overlay lock.");
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
