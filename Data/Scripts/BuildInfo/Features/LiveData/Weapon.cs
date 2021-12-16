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
        public Matrix CameraForSubpart;
        public Matrix CameraForBlock;
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
        public List<MuzzleData> Muzzles;
        public TurretData Turret;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            if(BuildInfoMod.Instance.CoreSystemsAPIHandler.Weapons.ContainsKey(def.Id))
                return false; // ignore weaponcore blocks

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

            IMyEntity muzzle = GetAimSubpart(block);
            if(muzzle != null && !GetMuzzleData(block, muzzle, this))
                return false;

            return true;
        }

        static bool GetMuzzleData(IMyCubeBlock block, IMyEntity muzzleEntity, BData_Weapon weapon)
        {
            if(muzzleEntity == null)
                muzzleEntity = block;

            Dictionary<string, IMyModelDummy> muzzleDummies = BuildInfoMod.Instance.Caches.Dummies;
            muzzleDummies.Clear();
            muzzleEntity.Model.GetDummies(muzzleDummies);

            weapon.Muzzles = new List<MuzzleData>();

            IMyGunObject<MyGunBase> gun = (IMyGunObject<MyGunBase>)block;
            bool hasProjectileAmmo = gun.GunBase.HasProjectileAmmoDefined;
            bool hasMissileAmmo = gun.GunBase.HasMissileAmmoDefined;

            bool hasProjectileBarrel = false;
            bool hasMissileBarrel = false;

            // from MyGunBase.LoadDummies()
            foreach(IMyModelDummy dummy in muzzleDummies.Values)
            {
                bool isMissile = hasMissileAmmo && dummy.Name.ContainsIgnoreCase("muzzle_missile");
                if(isMissile || (hasProjectileAmmo && dummy.Name.ContainsIgnoreCase("muzzle_projectile")))
                {
                    Matrix matrixForSubpart = Matrix.Normalize(dummy.Matrix);
                    MatrixD matrixForBlock = (matrixForSubpart * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled; // to world then to block local

                    // HACK: I dunno, it just worksTM
                    if(block is IMyLargeInteriorTurret)
                        matrixForBlock = matrixForSubpart;

                    weapon.Muzzles.Add(new MuzzleData(matrixForSubpart, matrixForBlock, isMissile));

                    if(isMissile)
                        hasMissileBarrel = true;
                    else
                        hasProjectileBarrel = true;
                }
            }

            if(weapon.Muzzles.Count == 0)
            {
                if(block is IMyLargeGatlingTurret)
                {
                    if(hasProjectileAmmo) // only add if the weapon can use it
                    {
                        // from MyLargeGatlingBarrel.Init()
                        Matrix matrix = Matrix.CreateTranslation(Vector3.Forward * 2);
                        MatrixD matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                        weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock));
                        hasProjectileBarrel = true;
                    }
                }
                else if(block is IMyLargeMissileTurret)
                {
                    if(hasMissileAmmo)
                    {
                        // from MyLargeMissileBarrel.Init()
                        Matrix matrix = Matrix.CreateTranslation(Vector3.Forward * 3);
                        MatrixD matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                        weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock, missile: true));
                        hasMissileBarrel = true;
                    }
                }
                else if(block is IMyLargeInteriorTurret)
                {
                    if(hasProjectileAmmo)
                    {
                        // from MyLargeInteriorBarrel.Init()
                        Matrix matrix = Matrix.CreateTranslation(Vector3.Forward * -0.8f);
                        MatrixD matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                        weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock));
                        hasProjectileBarrel = true;
                    }
                }
                else if(block is IMySmallGatlingGun)
                {
                    if(hasProjectileAmmo)
                    {
                        // from MySmallGatlingGun.GetBarrelAndMuzzle()
                        IMyModelDummy muzzleDummy = muzzleDummies.GetValueOrDefault("Muzzle", null);
                        Matrix matrix;
                        if(muzzleDummy != null)
                            matrix = Matrix.Normalize(muzzleDummy.Matrix);
                        else
                            matrix = Matrix.CreateTranslation(new Vector3(0f, 0f, -1f));

                        MatrixD matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                        weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock));
                        hasProjectileBarrel = true;
                    }
                }
                else if(block is IMySmallMissileLauncher)
                {
                    if(hasMissileAmmo)
                    {
                        // from MySmallMissileLauncher.LoadDummies()
                        foreach(IMyModelDummy dummy in muzzleDummies.Values)
                        {
                            if(dummy.Name.ContainsIgnoreCase("barrel"))
                            {
                                Matrix matrix = Matrix.Normalize(dummy.Matrix);
                                MatrixD matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;
                                weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock, missile: true));
                                hasMissileBarrel = true;
                            }
                        }
                    }
                }
            }

            if(!hasProjectileBarrel || !hasMissileBarrel)
            {
                Matrix matrix = Matrix.Identity;
                MatrixD matrixForBlock = (matrix * muzzleEntity.WorldMatrix) * block.WorldMatrixInvScaled;

                if(hasMissileAmmo && !hasMissileBarrel)
                {
                    weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock, missile: true));
                }

                if(hasProjectileAmmo && !hasProjectileBarrel)
                {
                    weapon.Muzzles.Add(new MuzzleData(matrix, matrixForBlock));
                }
            }

            muzzleDummies.Clear();

            if(weapon.Muzzles.Count == 0)
            {
                weapon.Muzzles = null;
                return false;
            }
            else
            {
                weapon.Muzzles.TrimExcess();
                return true;
            }
        }

        public static bool GetTurretData(IMyCubeBlock block, out TurretData turret, string yawName, string pitchName, string barrelName = null, float camForwardOffset = 0f, float camUpOffset = 0f)
        {
            turret = new TurretData();

            MyEntitySubpart subpartYaw;
            if(block.TryGetSubpart(yawName, out subpartYaw))
            {
                turret.YawLocalPos = (Vector3)Vector3D.Transform(subpartYaw.WorldMatrix.Translation, block.WorldMatrixInvScaled);

                // avoid y-fighting if it's a multiple of grid size
                int y = (int)(turret.YawLocalPos.Y * 100);
                int gs = (int)(block.CubeGrid.GridSize * 100);
                if(y % gs == 0)
                    turret.YawLocalPos += new Vector3(0, 0.05f, 0);
            }
            else
            {
                Log.Error($"Couldn't find {yawName} in block {block.BlockDefinition.ToString()}");
                return false;
            }

            MyEntitySubpart subpartPitch;
            if(subpartYaw.TryGetSubpart(pitchName, out subpartPitch))
            {
                // HACK: interior turret's default subpart orientation is weird
                if(block is IMyLargeInteriorTurret)
                {
                    Matrix yawLM = subpartYaw.PositionComp.LocalMatrixRef;
                    Matrix pitchLM = subpartPitch.PositionComp.LocalMatrixRef;
                    Matrix lm = pitchLM * (MatrixD.CreateRotationX(-MathHelper.PiOver2) * yawLM);
                    turret.PitchLocalPos = lm.Translation;
                }
                else
                {
                    turret.PitchLocalPos = (Vector3)Vector3D.Transform(subpartPitch.WorldMatrix.Translation, block.WorldMatrixInvScaled);
                }

                // FIXME: camera dummy is ignored on engineer turret block?!

                // from MyLargeTurretBase.GetCameraDummy()
                Dictionary<string, IMyModelDummy> pitchDummies = BuildInfoMod.Instance.Caches.Dummies;
                pitchDummies.Clear();
                ((IMyEntity)subpartPitch).Model.GetDummies(pitchDummies);
                IMyModelDummy cameraDummy = pitchDummies.GetValueOrDefault("camera", null);

                // from MyLargeTurretBase.GetViewMatrix() (without the invert)
                if(cameraDummy != null)
                    turret.CameraForSubpart = Matrix.Normalize(cameraDummy.Matrix);
                else
                    turret.CameraForSubpart = Matrix.CreateTranslation(Vector3.Forward * camForwardOffset + Vector3.Up * camUpOffset);

                turret.CameraForBlock = (turret.CameraForSubpart * subpartPitch.WorldMatrix) * block.WorldMatrixInvScaled;
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
            if(block is IMyLargeGatlingTurret)
                return GetSubpart(block, "GatlingTurretBase1", "GatlingTurretBase2", "GatlingBarrel");
            else if(block is IMyLargeMissileTurret)
                return GetSubpart(block, "MissileTurretBase1", "MissileTurretBarrels");
            else if(block is IMyLargeInteriorTurret)
                return GetSubpart(block, "InteriorTurretBase1", "InteriorTurretBase2");
            else if(block is IMySmallGatlingGun)
                return GetSubpart(block, barrelName: "Barrel");
            else if(block is IMySmallMissileLauncher)
                return block;

            Log.Info($"WARNING: New unrecognized type of weapon block: {block.BlockDefinition.ToString()}");
            return block;
        }

        static IMyEntity GetSubpart(IMyCubeBlock block, string yawName = null, string pitchName = null, string barrelName = null)
        {
            MyEntitySubpart subpartYaw = null;
            MyEntitySubpart subpartPitch = null;

            bool isTurret = (yawName != null && pitchName != null);
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