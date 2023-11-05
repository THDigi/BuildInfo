using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.VanillaData;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class Suspension : MultiSubpartBase
    {
        static readonly float? TopPartTransparency = Hardcoded.CubeBuilderTransparency * 2f;

        bool Valid;
        PreviewEntityWrapper WheelPart;
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
            if(Data == null || Data.SuspensionDef == null || Data.TopDef == null)
                return baseReturn;

            WheelData = Main.LiveDataHandler.Get<BData_Wheel>(Data.TopDef);
            if(WheelData == null)
                return baseReturn;

            WheelPart = new PreviewEntityWrapper(Data.TopDef.Model, null, "electric_motor");
            Valid = (WheelPart != null);
            RaycastOffset = 0;
            return baseReturn || Valid;
        }

        protected override void Disposed()
        {
            base.Disposed();

            WheelPart?.Close();
            WheelPart = null;

            Data = null;
        }

        public override void Update(ref MatrixD blockWorldMatrix)
        {
            base.Update(ref blockWorldMatrix);

            if(!Valid)
                return;

            Matrix localMatrix = Matrix.Identity;

            MatrixD gridWorldMatrix = blockWorldMatrix;

            float height = MathHelper.Clamp(DefaultHeight, Data.SuspensionDef.MinHeight, Data.SuspensionDef.MaxHeight);

            MatrixD topMatrix = Data.GetWheelMatrix(localMatrix, blockWorldMatrix, gridWorldMatrix, height);

            // MyMotorSuspension.CanPlaceRotor() does it negative too
            topMatrix.Translation -= Vector3D.TransformNormal(WheelData.WheelDummy, topMatrix);

            #region detect when wheel hits something and slide it
            //if(Main.Tick % 2 == 0)
            {
                float totalTravel = (Data.SuspensionDef.MaxHeight - Data.SuspensionDef.MinHeight);
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

            WheelPart.Update(ref topMatrix, TopPartTransparency);

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
