using Digi.BuildInfo.Features.LiveData;
using Sandbox.Definitions;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class Motor : MultiSubpartBase
    {
        bool Valid;
        PreviewEntityWrapper TopPart;
        MyMotorStatorDefinition MotorDef;
        BData_Motor Data;

        protected override void Initialized()
        {
            base.Initialized();

            Valid = false;

            Data = Main.LiveDataHandler.Get<BData_Motor>(BlockDef);
            MotorDef = BlockDef as MyMotorStatorDefinition;
            if(Data == null || MotorDef == null)
                return;

            MyCubeBlockDefinitionGroup blockPair = MyDefinitionManager.Static.TryGetDefinitionGroup(MotorDef.TopPart);
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

            MotorDef = null;
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

            float displacement = 0 + MotorDef.RotorDisplacementInModel; // this is what IMyMotorStator.Displacement would return for 0 displacement

            MatrixD topMatrix = Data.GetRotorMatrix(localMatrix, blockWorldMatrix, gridWorldMatrix, displacement);

            TopPart.Update(ref topMatrix);
        }
    }
}
