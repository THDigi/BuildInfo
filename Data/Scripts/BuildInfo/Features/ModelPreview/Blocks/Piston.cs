using Digi.BuildInfo.Features.LiveData;
using Sandbox.Definitions;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class Piston : PreviewInstanceBase
    {
        bool Valid;
        PreviewEntityWrapper PreviewEntity;
        MyPistonBaseDefinition PistonDef;
        BData_Piston Data;

        protected override void Initialized()
        {
            Valid = false;

            Data = Main.LiveDataHandler.Get<BData_Piston>(BlockDef);
            PistonDef = BlockDef as MyPistonBaseDefinition;
            if(Data == null || PistonDef == null)
                return;

            MyCubeBlockDefinitionGroup blockPair = MyDefinitionManager.Static.TryGetDefinitionGroup(PistonDef.TopPart);
            if(blockPair == null)
                return;

            MyCubeBlockDefinition topDef = blockPair[BlockDef.CubeSize];
            if(topDef == null)
                return;

            PreviewEntity = new PreviewEntityWrapper(topDef.Model);
            Valid = (PreviewEntity != null);
        }

        protected override void Disposed()
        {
            PreviewEntity?.Close();
            PreviewEntity = null;

            PistonDef = null;
            Data = null;
        }

        public override void Update(ref MatrixD drawMatrix)
        {
            if(!Valid)
                return;

            // TODO draw other subparts too?

            MatrixD topMatrix = Data.TopLocalMatrix * drawMatrix;

            PreviewEntity.Update(ref topMatrix);
        }
    }
}
