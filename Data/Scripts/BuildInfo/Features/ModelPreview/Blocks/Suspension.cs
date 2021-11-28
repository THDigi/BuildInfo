using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class Suspension : MultiSubpartBase
    {
        bool Valid;
        PreviewEntityWrapper WheelPart;
        MyMotorSuspensionDefinition SuspensionDef;
        BData_Suspension Data;
        BData_Wheel WheelData;
        //float Angle;
        float RaycastOffset = 0;

        static readonly float DefaultHeight = -9999; // new MyObjectBuilder_MotorSuspension().Height;

        protected override bool Initialized()
        {
            bool baseReturn = base.Initialized();
            Valid = false;

            Data = Main.LiveDataHandler.Get<BData_Suspension>(BlockDef);
            SuspensionDef = BlockDef as MyMotorSuspensionDefinition;
            if(Data == null || SuspensionDef == null)
                return baseReturn;

            MyCubeBlockDefinitionGroup blockPair = MyDefinitionManager.Static.TryGetDefinitionGroup(SuspensionDef.TopPart);
            if(blockPair == null)
                return baseReturn;

            MyCubeBlockDefinition topDef = blockPair[BlockDef.CubeSize];
            if(topDef == null)
                return baseReturn;

            WheelData = Main.LiveDataHandler.Get<BData_Wheel>(topDef);
            if(WheelData == null)
                return baseReturn;

            WheelPart = new PreviewEntityWrapper(topDef.Model);
            Valid = (WheelPart != null);
            RaycastOffset = 0;
            return baseReturn || Valid;
        }

        protected override void Disposed()
        {
            base.Disposed();

            WheelPart?.Close();
            WheelPart = null;

            SuspensionDef = null;
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

            float height = MathHelper.Clamp(DefaultHeight, SuspensionDef.MinHeight, SuspensionDef.MaxHeight);

            MatrixD topMatrix = Data.GetWheelMatrix(localMatrix, blockWorldMatrix, gridWorldMatrix, height);

            // MyMotorSuspension.CanPlaceRotor() does it negative too
            topMatrix.Translation -= Vector3D.TransformNormal(WheelData.WheelDummy, topMatrix);

            #region detect when wheel hits something and slide it
            //if(Main.Tick % 2 == 0)
            {
                float totalTravel = (SuspensionDef.MaxHeight - SuspensionDef.MinHeight);
                if(totalTravel > 0)
                {
                    Vector3D wheelCenter = Vector3D.Transform(WheelData.ModelCenter, topMatrix);
                    Vector3D raycastFrom = wheelCenter + topMatrix.Forward * totalTravel;
                    Vector3D raycastTo = wheelCenter + topMatrix.Backward * (WheelData.WheelRadius / 2f);

                    IHitInfo hit;
                    if(MyAPIGateway.Physics.CastRay(raycastFrom, raycastTo, out hit, CollisionLayers.CollisionLayerWithoutCharacter))
                    {
                        float length = totalTravel + (WheelData.WheelRadius / 2f);
                        RaycastOffset = MathHelper.Clamp((1f - hit.Fraction) * length, 0, totalTravel);
                    }
                    else
                    {
                        RaycastOffset = 0;
                    }
                }
            }

            topMatrix.Translation += topMatrix.Forward * RaycastOffset;
            #endregion

            // the spin isn't useful, it's gonna be the same direction for both left and right variants...
            //Vector3D pos = topMatrix.Translation;
            //topMatrix.Translation = Vector3D.Zero;
            //topMatrix *= MatrixD.CreateFromAxisAngle(topMatrix.Down, Angle);
            //topMatrix.Translation = pos; 
            //Angle += MathHelper.Pi / 90; // 2deg/tick

            // custom transparency so you can see through the wheel.
            WheelPart.Update(ref topMatrix, customTransparency: Hardcoded.CubeBuilderTransparency * 2f);
        }
    }
}
