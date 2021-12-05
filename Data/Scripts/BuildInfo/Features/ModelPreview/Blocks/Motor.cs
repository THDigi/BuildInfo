using Digi.BuildInfo.Features.LiveData;
using Digi.Input;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class Motor : MultiSubpartBase
    {
        bool Valid;
        PreviewEntityWrapper TopPart;
        MyMotorStatorDefinition MotorDef;
        BData_Motor Data;
        float Displacement;

        protected override bool Initialized()
        {
            bool baseReturn = base.Initialized();
            Valid = false;
            Displacement = 0;

            Data = Main.LiveDataHandler.Get<BData_Motor>(BlockDef);
            MotorDef = BlockDef as MyMotorStatorDefinition;
            if(Data == null || MotorDef == null)
                return baseReturn;

            MyCubeBlockDefinitionGroup blockPair = MyDefinitionManager.Static.TryGetDefinitionGroup(MotorDef.TopPart);
            if(blockPair == null)
                return baseReturn;

            MyCubeBlockDefinition topDef = blockPair[BlockDef.CubeSize];
            if(topDef == null)
                return baseReturn;

            TopPart = new PreviewEntityWrapper(topDef.Model);
            Valid = (TopPart != null);
            return baseReturn || Valid;
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

            if(MotorDef.RotorType == MyRotorType.Rotor && InputLib.IsInputReadable())
            {
                bool isSmall = (MotorDef.CubeSize == MyCubeSize.Small);
                float minDisplacement = (isSmall ? MotorDef.RotorDisplacementMinSmall : MotorDef.RotorDisplacementMin);
                float maxDisplacement = (isSmall ? MotorDef.RotorDisplacementMaxSmall : MotorDef.RotorDisplacementMax);

                if(MyAPIGateway.Input.IsAnyShiftKeyPressed())
                    Displacement += (maxDisplacement - minDisplacement) / 60f; // so it takes a second to go from one end to the other
                else if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
                    Displacement -= (maxDisplacement - minDisplacement) / 60f;

                Displacement = MathHelper.Clamp(Displacement, minDisplacement, maxDisplacement);
            }

            float displacement = Displacement - MotorDef.RotorDisplacementInModel;

            MatrixD topMatrix = Data.GetRotorMatrix(localMatrix, blockWorldMatrix, gridWorldMatrix, displacement);

            TopPart.Update(ref topMatrix);
        }
    }
}
