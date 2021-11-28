using Digi.BuildInfo.Features.LiveData;
using Sandbox.Definitions;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class Piston : MultiSubpartBase
    {
        bool Valid;
        PreviewEntityWrapper TopPart;
        MyPistonBaseDefinition PistonDef;
        BData_Piston Data;

        protected override void Initialized()
        {
            base.Initialized();

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

            TopPart = new PreviewEntityWrapper(topDef.Model);
            Valid = (TopPart != null);
        }

        protected override void Disposed()
        {
            base.Disposed();

            TopPart?.Close();
            TopPart = null;

            PistonDef = null;
            Data = null;
        }

        public override void Update(ref MatrixD drawMatrix)
        {
            base.Update(ref drawMatrix);

            if(!Valid)
                return;

            MatrixD topMatrix = Data.TopLocalMatrix * drawMatrix;

            TopPart.Update(ref topMatrix);
        }
    }
}
