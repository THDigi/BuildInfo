using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public abstract class BData_Turret : BData_Weapon
    {
        public TurretInfo TurretInfo;
        public TurretAttachmentInfo Camera;

        // HACK: also hardcoded in ModelPreview\Blocks\TurretBase.cs
        public abstract bool GetTurretParts(IMyCubeBlock block, out MyEntity subpartBase1, out MyEntity subpartBase2, out MyEntity barrelPart);

        public override bool GetBarrelPart(IMyCubeBlock block, out MyEntity barrelPart)
        {
            MyEntity subpartBase1;
            MyEntity subpartBase2;
            return GetTurretParts(block, out subpartBase1, out subpartBase2, out barrelPart);
        }

        protected override bool CheckWeapon(MyCubeBlock block, MyWeaponBlockDefinition def)
        {
            if(!block.IsBuilt)
                return false;

            MyEntity subpartBase1;
            MyEntity subpartBase2;
            MyEntity barrelPart;
            if(!GetTurretParts(block, out subpartBase1, out subpartBase2, out barrelPart))
                return false;

            // HACK: set subpart rotations just like the game would, but keeping it at resting position as it affects the overlays
            UnrotateSubparts(block.PositionComp.LocalMatrixRef, subpartBase1, subpartBase2);

            MyLargeTurretBaseDefinition turretDef = (MyLargeTurretBaseDefinition)def;

            BarrelInit(block, barrelPart, turretDef);

            TurretInfo = new TurretInfo();
            TurretInfo.AssignData(block, subpartBase1, subpartBase2);

            Camera = new TurretAttachmentInfo();
            Camera.AssignData(subpartBase2, block, turretDef.CameraDummyName);

            return true;
        }

        // from MyLargeTurretBase.RotateModels() without the rotation angles
        public static void UnrotateSubparts(Matrix blockLocal, MyEntity subpartBase1, MyEntity subpartBase2)
        {
            Matrix rotYaw = Matrix.Identity; // Matrix.CreateRotationY(Rotation);
            rotYaw.Translation = subpartBase1.PositionComp.LocalMatrixRef.Translation;

            Matrix renderYaw = rotYaw * blockLocal;

            // no need to update render but do need worldmatrix to be accurate
            subpartBase1.PositionComp.SetLocalMatrix(ref rotYaw, updateWorld: true);
            //subpartBase1.PositionComp.SetLocalMatrix(ref rotYaw, subpartBase1.Physics, false, ref renderYaw, forceUpdateRender: true);


            Matrix rotPitch = Matrix.Identity; //Matrix.CreateRotationX(Elevation);
            rotPitch.Translation = subpartBase2.PositionComp.LocalMatrixRef.Translation;

            Matrix renderPitch = rotPitch * renderYaw;

            subpartBase2.PositionComp.SetLocalMatrix(ref rotPitch, updateWorld: true);
            //subpartBase2.PositionComp.SetLocalMatrix(ref rotPitch, subpartBase2.Physics, true, ref renderPitch, forceUpdateRender: true);
        }

        protected virtual void BarrelInit(MyCubeBlock block, MyEntity entity, MyWeaponBlockDefinition def)
        {
            if(entity.Model != null)
            {
                // MyLargeBarrelBase.CameraDummy is not used
                //Dictionary<string, IMyModelDummy> dummies = entity.Model.GetDummies();
                //IMyModelDummy cameraDummy = dummies.GetValueOrDefault("camera", null);

                GunBase_LoadDummies(block, entity.Model.GetDummies(), def.DummyNames);
            }
        }
    }
}