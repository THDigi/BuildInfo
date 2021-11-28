using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.ModelPreview.Blocks;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview
{
    public class ModelPreview : ModComponent
    {
        readonly Dictionary<MyObjectBuilderType, Func<PreviewInstanceBase>> PreviewFactory = new Dictionary<MyObjectBuilderType, Func<PreviewInstanceBase>>(MyObjectBuilderType.Comparer);

        PreviewInstanceBase CurrentPreview;

        // used by all other blocks that don't have a specific preview class registered.
        readonly MultiSubpartBase FallbackPreview = new MultiSubpartBase();

        public ModelPreview(BuildInfoMod main) : base(main)
        {
            RegisterType<Motor>(typeof(MyObjectBuilder_MotorStator));
            RegisterType<Motor>(typeof(MyObjectBuilder_MotorAdvancedStator));

            RegisterType<Suspension>(typeof(MyObjectBuilder_MotorSuspension));

            RegisterType<Piston>(typeof(MyObjectBuilder_ExtendedPistonBase));
            RegisterType<Piston>(typeof(MyObjectBuilder_PistonBase));

            RegisterType<InteriorTurret>(typeof(MyObjectBuilder_InteriorTurret));
        }

        public override void RegisterComponent()
        {
            Main.Config.CubeBuilderDrawSubparts.ValueAssigned += ConfigValueAssigned;
            Main.EquipmentMonitor.BlockChanged += EquippedBlockChanged;
            Main.LiveDataHandler.DataGenerated += LiveDataGenerated;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.CubeBuilderDrawSubparts.ValueAssigned -= ConfigValueAssigned;
            Main.EquipmentMonitor.BlockChanged -= EquippedBlockChanged;
            Main.LiveDataHandler.DataGenerated -= LiveDataGenerated;
        }

        void RegisterType<T>(MyObjectBuilderType blockType) where T : PreviewInstanceBase, new() => PreviewFactory.Add(blockType, Instance<T>);
        static T Instance<T>() where T : PreviewInstanceBase, new() => new T();

        void ConfigValueAssigned(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            if(newValue)
                EquippedBlockChanged(Main.EquipmentMonitor?.BlockDef, Main.EquipmentMonitor?.AimedBlock);
            else
                EquippedBlockChanged(null, null);
        }

        void EquippedBlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            CurrentPreview?.Dispose();
            CurrentPreview = null;

            if(def != null && Main.Config.CubeBuilderDrawSubparts.Value && Main.EquipmentMonitor.IsCubeBuilder)
            {
                Func<PreviewInstanceBase> factory = PreviewFactory.GetValueOrDefault(def.Id.TypeId);
                if(factory != null)
                {
                    CurrentPreview = factory.Invoke();
                    CurrentPreview.Setup(def);
                }
                else // if no specific type declared, use the fallback one
                {
                    CurrentPreview = FallbackPreview;
                    CurrentPreview.Setup(def);
                }
            }

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, CurrentPreview != null);
        }

        void LiveDataGenerated(Type type, BData_Base data)
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

            MatrixD drawMatrix;
            if(!Utils.GetEquippedBlockMatrix(out drawMatrix))
                return;

            CurrentPreview.Update(ref drawMatrix);
        }
    }
}
