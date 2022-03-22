using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.ModelPreview.Blocks;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview
{
    public class SubpartPreview : ModComponent
    {
        readonly Dictionary<MyObjectBuilderType, Func<PreviewInstanceBase>> PreviewFactory = new Dictionary<MyObjectBuilderType, Func<PreviewInstanceBase>>(MyObjectBuilderType.Comparer);

        PreviewInstanceBase CurrentPreview;

        // used by all other blocks that don't have a specific preview class registered.
        readonly MultiSubpartBase FallbackPreview = new MultiSubpartBase();

        public SubpartPreview(BuildInfoMod main) : base(main)
        {
            RegisterType<Motor>(typeof(MyObjectBuilder_MotorStator));
            RegisterType<Motor>(typeof(MyObjectBuilder_MotorAdvancedStator));

            RegisterType<Suspension>(typeof(MyObjectBuilder_MotorSuspension));

            RegisterType<Piston>(typeof(MyObjectBuilder_ExtendedPistonBase));
            RegisterType<Piston>(typeof(MyObjectBuilder_PistonBase));

            //RegisterType<InteriorTurret>(typeof(MyObjectBuilder_InteriorTurret));

            RegisterType<AdvancedDoor>(typeof(MyObjectBuilder_AdvancedDoor));
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

        void EquippedBlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            Refresh(def);
        }

        void ConfigValueAssigned(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            if(newValue)
                Refresh(Main.EquipmentMonitor?.BlockDef);
            else
                Refresh(null);
        }

        void LiveDataGenerated(MyDefinitionId defId, BData_Base data)
        {
            MyCubeBlockDefinition heldDef = Main.EquipmentMonitor.BlockDef;
            if(heldDef == null || !Main.EquipmentMonitor.IsCubeBuilder)
                return;

            if(heldDef.Id.TypeId == defId.TypeId || (heldDef is MyMotorSuspensionDefinition && data is BData_Wheel)) // HACK: catch wheel spawn... needs a better system.
            {
                Refresh(heldDef);
            }
        }

        void Refresh(MyCubeBlockDefinition def)
        {
            CurrentPreview?.Dispose();
            CurrentPreview = null;

            if(def != null && Main.Config.CubeBuilderDrawSubparts.Value && Main.EquipmentMonitor.IsCubeBuilder)
            {
                Func<PreviewInstanceBase> factory = PreviewFactory.GetValueOrDefault(def.Id.TypeId);
                if(factory != null)
                    CurrentPreview = factory.Invoke();
                else // if no specific type declared, use the fallback one
                    CurrentPreview = FallbackPreview;

                if(!CurrentPreview.Setup(def))
                {
                    CurrentPreview.Dispose();
                    CurrentPreview = null;
                }
            }

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, CurrentPreview != null);
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
