using Digi.BuildInfo.Features.LiveData;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class Piston : MultiSubpartBase
    {
        bool Valid;
        PreviewEntityWrapper TopPart;
        BData_Piston Data;

        protected override bool Initialized()
        {
            bool baseReturn = base.Initialized();
            Valid = false;

            Data = Main.LiveDataHandler.Get<BData_Piston>(BlockDef);
            if(Data == null || Data.PistonDef == null || Data.TopDef == null)
                return baseReturn;

            TopPart = new PreviewEntityWrapper(Data.TopDef.Model, null, Data.TopDef);
            Valid = (TopPart != null);
            return baseReturn || Valid;
        }

        protected override void Disposed()
        {
            base.Disposed();

            TopPart?.Close();
            TopPart = null;

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
