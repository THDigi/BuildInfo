using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.VanillaData;
using Digi.Input;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class Motor : MultiSubpartBase
    {
        static readonly float? TopPartTransparency = Hardcoded.CubeBuilderTransparency * 2f;

        bool Valid;
        PreviewEntityWrapper TopPart;
        BData_Motor Data;
        float Displacement;

        protected override bool Initialized()
        {
            bool baseReturn = base.Initialized();
            Valid = false;
            Displacement = 0;

            Data = Main.LiveDataHandler.Get<BData_Motor>(BlockDef);
            if(Data == null || Data.StatorDef == null || Data.TopDef == null)
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

            Matrix localMatrix = Matrix.Identity;

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(BlockDef.ModelOffset, blockWorldMatrix);

            MatrixD gridWorldMatrix = blockWorldMatrix;

            if(Data.StatorDef.RotorType == MyRotorType.Rotor && InputLib.IsInputReadable())
            {
                bool isSmall = (Data.StatorDef.CubeSize == MyCubeSize.Small);
                float minDisplacement = (isSmall ? Data.StatorDef.RotorDisplacementMinSmall : Data.StatorDef.RotorDisplacementMin);
                float maxDisplacement = (isSmall ? Data.StatorDef.RotorDisplacementMaxSmall : Data.StatorDef.RotorDisplacementMax);

                if(MyAPIGateway.Input.IsAnyShiftKeyPressed())
                    Displacement += (maxDisplacement - minDisplacement) / 60f; // so it takes a second to go from one end to the other
                else if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
                    Displacement -= (maxDisplacement - minDisplacement) / 60f;

                Displacement = MathHelper.Clamp(Displacement, minDisplacement, maxDisplacement);
            }

            MatrixD topMatrix = Data.GetRotorMatrix(localMatrix, blockWorldMatrix, gridWorldMatrix, Displacement);

            TopPart.Update(ref topMatrix, TopPartTransparency);
        }
    }
}
