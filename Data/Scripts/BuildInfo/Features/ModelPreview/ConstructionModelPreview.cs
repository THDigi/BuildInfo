using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.ModelPreview.Blocks;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Digi.Input.Devices;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview
{
    // TODO: some way to scroll through models around the main block... or even find a way to hide the currently held block
    // TODO: apply color and skin when skin can be read from IMyPlayer

    public class ConstructionModelPreview : ModComponent
    {
        public event Action ConstructionModelRefresh;

        public readonly List<ConstructionModelStack> Stacks = new List<ConstructionModelStack>();

        bool Activated = false;
        bool WasRotatingInPlace = false;
        Vector3D MaintainDir = Vector3D.Down;
        Vector3D LocalStackDir = Vector3D.Down;
        float SmallestAxisSizeMeters = 1f;
        float Spacing = 0f;

        IMyHudNotification Notification;

        public ConstructionModelPreview(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            Main.LiveDataHandler.DataGenerated += LiveDataGenerated;
            Main.Config.CubeBuilderDrawSubparts.ValueAssigned += ConfigValueAssigned;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.CubeBuilderDrawSubparts.ValueAssigned -= ConfigValueAssigned;
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            Main.LiveDataHandler.DataGenerated -= LiveDataGenerated;
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, Main.EquipmentMonitor.IsCubeBuilder);
            Refresh();
        }

        void LiveDataGenerated(MyDefinitionId defId, BData_Base data)
        {
            MyCubeBlockDefinition heldDef = Main.EquipmentMonitor.BlockDef;
            if(heldDef == null || !Main.EquipmentMonitor.IsCubeBuilder)
                return;

            if(heldDef.Id == defId || (heldDef is MyMotorSuspensionDefinition && data is BData_Wheel)) // HACK: catch wheel spawn... needs a better system.
            {
                Refresh();
            }
        }

        void ConfigValueAssigned(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            Refresh();
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(Main.Config.ConstructionModelPreviewBind.Value.IsJustPressed())
            {
                Toggle(!Activated);
            }
        }

        void Toggle(bool on)
        {
            if(on == Activated)
                return;

            Activated = on;
            Refresh();

            if(Activated)
            {
                if(Notification == null)
                    Notification = MyAPIGateway.Utilities.CreateNotification(string.Empty, 16, FontsHandler.WhiteSh);

                string toggleBind = Main.Config.ConstructionModelPreviewBind.Value.GetBinds(ControlContext.BUILD, specialChars: true);

                Notification.Hide();
                Notification.Text = $"Construction Model Preview: [{toggleBind}] to turn off, [Shift] to rotate in place";
            }
        }

        void Refresh()
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);

            foreach(ConstructionModelStack stack in Stacks)
            {
                stack.RemoveModels();
            }

            Stacks.Clear();

            if(!Activated || !Main.EquipmentMonitor.IsCubeBuilder)
                return;

            MyCubeBlockDefinition def = Main.EquipmentMonitor.BlockDef;
            if(def == null)
                return;

            MyCubeBlockDefinition.BuildProgressModel[] buildModels = def.BuildProgressModels;
            if(buildModels != null && buildModels.Length > 0)
            {
                ConstructionModelStack.CreateAndAdd(Stacks, def);

                ConstructionModelRefresh?.Invoke();

                // clone construction models for mismatched amounts
                if(Stacks.Count > 1)
                {
                    ConstructionModelStack longestStack = Stacks[0];
                    for(int i = 1; i < Stacks.Count; i++)
                    {
                        ConstructionModelStack stack = Stacks[i];
                        if(stack.Models.Count > longestStack.Models.Count)
                            longestStack = stack;
                    }

                    int fillUpTo = longestStack.Models.Count;

                    foreach(ConstructionModelStack stack in Stacks)
                    {
                        if(longestStack == stack)
                            continue;

                        PreviewEntityWrapper lastModel = stack.Models[stack.Models.Count - 1];
                        while(stack.Models.Count < fillUpTo)
                        {
                            stack.Models.Add(new PreviewEntityWrapper(lastModel.ModelFullPath, lastModel.LocalMatrix));
                        }
                    }
                }

                float gridSize = Main.EquipmentMonitor.BlockGridSize;
                Vector3I size = def.Size;
                Spacing = (def.Size.AbsMax() * gridSize) * 0.1f;

                // TODO: for suspensions this gets weird, need a box that wraps around the entire properly-aligned contraption...
                //foreach(ConstructionModelStack stack in Stacks)
                //{
                //    size = Vector3I.Max(size, stack.Def.Size);
                //    Spacing = Math.Max(Spacing, (stack.Def.Size.AbsMax() * gridSize) * 0.1f);
                //}

                // HACK: ...
                if(def is MyMotorSuspensionDefinition && Stacks.Count > 1)
                {
                    SmallestAxisSizeMeters = (Stacks[1]?.Def?.Size.X ?? 1) * gridSize;
                    LocalStackDir = Vector3D.Right;
                }
                else
                {
                    // stack on the axis with the smallest size so they don't scatter so much
                    // whichever is first also determines the one picked for equal sizes
                    if(size.X <= size.Y && size.X <= size.Z)
                    {
                        SmallestAxisSizeMeters = size.X * gridSize;
                        LocalStackDir = Vector3D.Right;
                    }
                    else if(size.Y <= size.X && size.Y <= size.Z)
                    {
                        SmallestAxisSizeMeters = size.Y * gridSize;
                        LocalStackDir = Vector3D.Down;
                    }
                    else
                    {
                        SmallestAxisSizeMeters = size.Z * gridSize;
                        LocalStackDir = Vector3D.Backward;
                    }
                }

                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
            }
        }

        public override void UpdateDraw()
        {
            MatrixD drawMatrix;
            if(!Utils.GetEquippedBlockMatrix(out drawMatrix))
                return;

            if(!Main.IsPaused)
            {
                //Notification.ResetAliveTime();
                Notification.Show();
            }

            bool rotateInPlace = MyAPIGateway.Input.IsAnyShiftKeyPressed();
            if(rotateInPlace)
            {
                if(!WasRotatingInPlace)
                {
                    WasRotatingInPlace = true;
                }
            }
            else
            {
                if(WasRotatingInPlace)
                {
                    WasRotatingInPlace = false;
                    LocalStackDir = Vector3D.TransformNormal(MaintainDir, MatrixD.Transpose(drawMatrix));
                }

                MaintainDir = Vector3D.TransformNormal(LocalStackDir, drawMatrix);
            }

            float add = SmallestAxisSizeMeters + Spacing;

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(Main.EquipmentMonitor.BlockDef.ModelOffset, blockWorldMatrix);

            bool hide = !MyCubeBuilder.Static.DynamicMode; // hide when aiming at a grid

            for(int i = 0; i < Stacks.Count; i++)
            {
                ConstructionModelStack stack = Stacks[i];
                float offset = add;

                foreach(PreviewEntityWrapper model in stack.Models)
                {
                    MatrixD m;
                    if(model.LocalMatrix.HasValue)
                        m = model.LocalMatrix.Value * blockWorldMatrix;
                    else
                        m = blockWorldMatrix;

                    m.Translation += MaintainDir * offset;

                    model.Update(ref m, (hide ? 1f : stack.Transparency));
                    offset += add;
                }
            }
        }
    }
}
