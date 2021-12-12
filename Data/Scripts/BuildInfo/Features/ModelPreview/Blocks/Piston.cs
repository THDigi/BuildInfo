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

        protected override bool Initialized()
        {
            bool baseReturn = base.Initialized();
            Valid = false;

            Data = Main.LiveDataHandler.Get<BData_Piston>(BlockDef);
            PistonDef = BlockDef as MyPistonBaseDefinition;
            if(Data == null || PistonDef == null)
                return baseReturn;

            MyCubeBlockDefinitionGroup blockPair = MyDefinitionManager.Static.TryGetDefinitionGroup(PistonDef.TopPart);
            if(blockPair == null)
                return baseReturn;

            MyCubeBlockDefinition topDef = blockPair[BlockDef.CubeSize];
            if(topDef == null)
                return baseReturn;

            TopPart = new PreviewEntityWrapper(topDef.Model, null, topDef);
            Valid = (TopPart != null);
            return baseReturn || Valid;
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
