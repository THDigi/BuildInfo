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

            TopPart = new PreviewEntityWrapper(Data.TopDef.Model, null, "electric_motor");
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

        public override void Update(ref MatrixD blockWorldMatrix)
        {
            base.Update(ref blockWorldMatrix);

            if(!Valid)
                return;

            Matrix localMatrix = Matrix.Identity;

            MatrixD gridWorldMatrix = blockWorldMatrix;

            if(Data.StatorDef.RotorType == MyRotorType.Rotor && MyAPIGateway.Input.IsAnyShiftKeyPressed() && InputLib.IsInputReadable())
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    bool isSmall = (Data.StatorDef.CubeSize == MyCubeSize.Small);
                    float minDisplacement = (isSmall ? Data.StatorDef.RotorDisplacementMinSmall : Data.StatorDef.RotorDisplacementMin);
                    float maxDisplacement = (isSmall ? Data.StatorDef.RotorDisplacementMaxSmall : Data.StatorDef.RotorDisplacementMax);

                    float perScroll = (maxDisplacement - minDisplacement) / 10f;

                    if(scroll < 0)
                        Displacement -= perScroll;
                    else if(scroll > 0)
                        Displacement += perScroll;

                    Displacement = MathHelper.Clamp(Displacement, minDisplacement, maxDisplacement);
                }
            }

            MatrixD topMatrix = Data.GetRotorMatrix(localMatrix, blockWorldMatrix, gridWorldMatrix, Displacement);

            TopPart.Update(ref topMatrix, TopPartTransparency);

            ConstructionStack?.SetLocalMatrix(topMatrix * MatrixD.Invert(blockWorldMatrix));
        }

        public override void SpawnConstructionModel(ConstructionModelPreview comp)
        {
            if(Valid)
            {
                ConstructionStack = ConstructionModelStack.CreateAndAdd(comp.Stacks, Data.TopDef, null, TopPartTransparency);
            }
        }
    }
}
