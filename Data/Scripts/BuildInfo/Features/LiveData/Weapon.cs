using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class TurretData
    {
        public Vector3 YawLocalPos;
        public Vector3 PitchLocalPos;
        public Matrix CameraMatrix;
    }

    public struct MuzzleData
    {
        public readonly Matrix MatrixForSubpart;
        public readonly Matrix MatrixForBlock;
        public readonly bool Missile;

        public MuzzleData(Matrix matrixForSubpart, Matrix matrixForBlock, bool missile = false)
        {
            MatrixForSubpart = matrixForSubpart;
            MatrixForBlock = matrixForBlock;
            Missile = missile;
        }
    }

    public class BData_Weapon : BData_Base
    {
        public Matrix FirstMuzzleLocalMatrix;
        public List<MuzzleData> Muzzles;
        public TurretData Turret;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            if(BuildInfoMod.Instance.WeaponCoreAPIHandler.Weapons.ContainsKey(def.Id))
                return false; // ignore weaponcore blocks

            var gun = (IMyGunObject<MyGunBase>)block;
            FirstMuzzleLocalMatrix = gun.GunBase.GetMuzzleLocalMatrix();

            bool valid = true;

            if(block is IMyLargeTurretBase)
            {
                if(block is IMyLargeGatlingTurret)
                {
                    valid = GetTurretData(block, out Turret, "GatlingTurretBase1", "GatlingTurretBase2", "GatlingBarrel", 0.5f, 0.75f);
                }
                else if(block is IMyLargeMissileTurret)
                {
                    valid = GetTurretData(block, out Turret, "MissileTurretBase1", "MissileTurretBarrels", null, 0.5f, 1f);
                }
                else if(block is IMyLargeInteriorTurret)
                {
                    valid = GetTurretData(block, out Turret, "InteriorTurretBase1", "InteriorTurretBase2", null, 0.2f, 0.45f);
                }
                else
                {
                    Log.Info($"WARNING: Unknown turret type: {block.BlockDefinition.ToString()}. This can cause overlay to be inaccurate.");
                    valid = false;
                }
            }

            if(!valid)
                return false;

            var muzzle = GetAimSubpart(block);
            if(muzzle != null && !GetMuzzleData(block, muzzle, this))
                return false;

            return true;
        }

        static bool GetMuzzleData(IMyCubeBlock block, IMyEntity muzzleEntity, BData_Weapon weapon)
        {
            if(muzzleEntity == null)
                muzzleEntity = block;

            var muzzleDummies = BuildInfoMod.Instance.Caches.Dummies;
            muzzleDummies.Clear();
            muzzleEntity.Model.GetDummies(muzzleDummies);

            // from MyGunBase.LoadDummies()
            foreach(var dummy in muzzleDummies.Values)
            {
                var isMissile = dummy.Name.ContainsIgnoreCase("muzzle_missile");
                if(isMissile || dummy.Name.ContainsIgnoreCase("muzzle_projectile"))
                {
                    var matrixForSubpart = dummy.Matrix;
                    var matrixForBlock = (matrixForSubpart * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;

                    // HACK: I dunno, it just worksTM
                    if(block is IMyLargeInteriorTurret)
                        matrixForBlock = matrixForSubpart;

                    if(weapon.Muzzles == null)
                        weapon.Muzzles = new List<MuzzleData>();

                    weapon.Muzzles.Add(new MuzzleData(matrixForSubpart, matrixForBlock, isMissile));
                }
            }

            weapon.Muzzles?.TrimExcess();

            bool hasDummies = (weapon.Muzzles.Count > 0);
            if(!hasDummies)
            {
                if(block is IMyLargeGatlingTurret)
                {
                    // from MyLargeGatlingBarrel.Init()
                    var matrix = Matrix.CreateTranslation(Vector3.Forward * 2);
                    var matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                    weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock));
                }
                else if(block is IMyLargeMissileTurret)
                {
                    // from MyLargeMissileBarrel.Init()
                    var matrix = Matrix.CreateTranslation(Vector3.Forward * 3);
                    var matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                    weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock, missile: true));
                }
                else if(block is IMyLargeInteriorTurret)
                {
                    // from MyLargeInteriorBarrel.Init()
                    var matrix = Matrix.CreateTranslation(Vector3.Forward * -0.8f);
                    var matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                    weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock));
                }
                else if(block is IMySmallGatlingGun)
                {
                    // from MySmallGatlingGun.GetBarrelAndMuzzle()
                    var muzzleDummy = muzzleDummies.GetValueOrDefault("Muzzle", null);
                    Matrix matrix;
                    if(muzzleDummy != null)
                        matrix = muzzleDummy.Matrix;
                    else
                        matrix = Matrix.CreateTranslation(new Vector3(0f, 0f, -1f));

                    var matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                    weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock));
                }
                else if(block is IMySmallMissileLauncher)
                {
                    // from MySmallMissileLauncher.LoadDummies()
                    foreach(var dummy in muzzleDummies.Values)
                    {
                        if(dummy.Name.ContainsIgnoreCase("barrel"))
                        {
                            var matrix = dummy.Matrix;
                            var matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                            weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock, missile: true));
                        }
                    }
                }
            }

            muzzleDummies.Clear();
            return true;
        }

        public static bool GetTurretData(IMyCubeBlock block, out TurretData turret, string yawName, string pitchName, string barrelName = null, float camForwardOffset = 0f, float camUpOffset = 0f)
        {
            turret = new TurretData();

            MyEntitySubpart subpartYaw;
            if(block.TryGetSubpart(yawName, out subpartYaw))
            {
                turret.YawLocalPos = Vector3D.Transform(subpartYaw.WorldMatrix.Translation, block.WorldMatrixInvScaled);

                // avoid y-fighting if it's a multiple of grid size
                int y = (int)(turret.YawLocalPos.Y * 100);
                int gs = (int)(block.CubeGrid.GridSize * 100);
                if(y % gs == 0)
                    turret.YawLocalPos += new Vector3D(0, 0.05f, 0);
            }
            else
            {
                Log.Error($"Couldn't find {yawName} in block {block.BlockDefinition.ToString()}");
                return false;
            }

            MyEntitySubpart subpartPitch;
            if(subpartYaw.TryGetSubpart(pitchName, out subpartPitch))
            {
                turret.PitchLocalPos = Vector3D.Transform(subpartPitch.WorldMatrix.Translation, block.WorldMatrixInvScaled);

                // from MyLargeTurretBase.GetCameraDummy()
                var pitchDummies = BuildInfoMod.Instance.Caches.Dummies;
                pitchDummies.Clear();
                ((IMyEntity)subpartPitch).Model.GetDummies(pitchDummies);
                IMyModelDummy cameraDummy = pitchDummies.GetValueOrDefault("camera", null);

                // from MyLargeTurretBase.GetViewMatrix() (without the invert)
                if(cameraDummy != null)
                {
                    turret.CameraMatrix = Matrix.Normalize(cameraDummy.Matrix);
                }
                else
                {
                    turret.CameraMatrix = Matrix.CreateTranslation(Vector3.Forward * camForwardOffset + Vector3.Up * camUpOffset);
                }
            }
            else
            {
                Log.Error($"Couldn't find {pitchName} in yaw subpart for block {block.BlockDefinition.ToString()}");
                return false;
            }

            MyEntitySubpart aimingSubpart = subpartPitch;
            if(barrelName != null && subpartPitch != null)
            {
                if(!subpartPitch.TryGetSubpart(barrelName, out aimingSubpart) || aimingSubpart == null)
                    aimingSubpart = subpartPitch;
            }

            return true;
        }

        public static IMyEntity GetAimSubpart(IMyCubeBlock block)
        {
            if(block is IMyLargeTurretBase)
            {
                if(block is IMyLargeGatlingTurret)
                    return GetSubpart(block, "GatlingTurretBase1", "GatlingTurretBase2", "GatlingBarrel");
                else if(block is IMyLargeMissileTurret)
                    return GetSubpart(block, "MissileTurretBase1", "MissileTurretBarrels");
                else if(block is IMyLargeInteriorTurret)
                    return GetSubpart(block, "InteriorTurretBase1", "InteriorTurretBase2");
                else
                    Log.Info($"WARNING: New kind of turret: {block.BlockDefinition.ToString()}");
            }
            else
            {
                if(block is IMySmallGatlingGun)
                    return GetSubpart(block, barrelName: "Barrel");
                else
                    return block;
            }

            return null;
        }

        private static IMyEntity GetSubpart(IMyCubeBlock block, string yawName = null, string pitchName = null, string barrelName = null)
        {
            bool isTurret = yawName != null && pitchName != null;

            MyEntitySubpart subpartPitch = null;
            MyEntitySubpart subpartYaw = null;

            if(isTurret)
            {
                if(!block.TryGetSubpart(yawName, out subpartYaw))
                    return null;

                if(!subpartYaw.TryGetSubpart(pitchName, out subpartPitch))
                    return null;
            }

            IMyEntity barrelParent = (isTurret ? (IMyEntity)subpartPitch : block);

            if(barrelName == null)
                return barrelParent;

            MyEntitySubpart subpartBarrel;
            if(!barrelParent.TryGetSubpart(barrelName, out subpartBarrel))
                return null;

            return subpartBarrel;
        }
    }
}