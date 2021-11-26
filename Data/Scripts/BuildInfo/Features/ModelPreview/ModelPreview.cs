using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.ModelPreview.Blocks;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview
{
    public class ModelPreview : ModComponent
    {
        readonly Dictionary<MyObjectBuilderType, Func<PreviewInstanceBase>> PreviewFactory = new Dictionary<MyObjectBuilderType, Func<PreviewInstanceBase>>(MyObjectBuilderType.Comparer);

        PreviewInstanceBase CurrentPreview;

        public ModelPreview(BuildInfoMod main) : base(main)
        {
            RegisterType<Motor>(typeof(MyObjectBuilder_MotorStator));
            RegisterType<Motor>(typeof(MyObjectBuilder_MotorAdvancedStator));

            RegisterType<Suspension>(typeof(MyObjectBuilder_MotorSuspension));

            RegisterType<Piston>(typeof(MyObjectBuilder_ExtendedPistonBase));
            RegisterType<Piston>(typeof(MyObjectBuilder_PistonBase));
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += EquippedBlockChanged;
            Main.LiveDataHandler.DataGenerated += LiveDataHandler_DataGenerated;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.BlockChanged -= EquippedBlockChanged;
            Main.LiveDataHandler.DataGenerated -= LiveDataHandler_DataGenerated;
        }

        void RegisterType<T>(MyObjectBuilderType blockType) where T : PreviewInstanceBase, new() => PreviewFactory.Add(blockType, Instance<T>);
        static T Instance<T>() where T : PreviewInstanceBase, new() => new T();

        void EquippedBlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            CurrentPreview?.Dispose();
            CurrentPreview = null;

            if(Main.EquipmentMonitor.IsCubeBuilder && def != null)
            {
                Func<PreviewInstanceBase> factory = PreviewFactory.GetValueOrDefault(def.Id.TypeId);
                if(factory != null)
                {
                    CurrentPreview = factory.Invoke();
                    CurrentPreview.Setup(def);
                }
            }

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, CurrentPreview != null);
        }

        void LiveDataHandler_DataGenerated(Type type, BData_Base data)
        {
            MyCubeBlockDefinition previewDef = CurrentPreview?.BlockDef;
            if(previewDef != null)
            {
                if((previewDef.Id.TypeId == type)
                || (previewDef is MyMotorSuspensionDefinition && data is BData_Wheel)) // to catch wheel spawn... needs a better system.
                {
                    CurrentPreview.Dispose();
                    CurrentPreview.Setup(previewDef);
                }
            }
        }

        public override void UpdateDraw()
        {
            if(CurrentPreview == null)
                return;

            MyCubeBlockDefinition heldBlockDef = Main.EquipmentMonitor.BlockDef;
            if(heldBlockDef == null)
                return;

            // TODO: find a solution to this erroring sometimes (which is not going to be an issue in asyncdraw event)
            // TODO: unify getter for this and overlays
            MyOrientedBoundingBoxD box = MyCubeBuilder.Static.GetBuildBoundingBox();

            MatrixD drawMatrix = MatrixD.CreateFromQuaternion(box.Orientation);

            if(MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.HitInfo.HasValue)
            {
                IMyEntity hitEnt = MyCubeBuilder.Static.HitInfo.Value.GetHitEnt();
                if(hitEnt is IMyVoxelBase)
                {
                    drawMatrix.Translation = MyCubeBuilder.Static.HitInfo.Value.GetHitPos(); // required for position to be accurate when aiming at a planet
                }
                else if(hitEnt is IMyCubeGrid)
                {
                    drawMatrix.Translation = box.Center;
                }
                else
                {
                    drawMatrix.Translation = MyCubeBuilder.Static.FreePlacementTarget; // required for the position to be accurate when the block is not aimed at anything
                }
            }
            else
            {
                //drawMatrix.Translation = box.Center;

                // fix for jittery overlays when aiming at a grid.
                Vector3D addPosition;
                MyCubeBuilder.Static.GetAddPosition(out addPosition);
                drawMatrix.Translation = addPosition;
            }

            CurrentPreview.Update(ref drawMatrix);
        }
    }
}
