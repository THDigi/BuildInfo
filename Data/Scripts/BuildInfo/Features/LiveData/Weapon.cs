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
    }

    public class BData_Weapon : BData_Base
    {
        public Matrix FirstMuzzleLocalMatrix;

        public TurretData Turret;
        public List<Matrix> ProjectileMuzzles = new List<Matrix>();
        public List<Matrix> MissileMuzzles = new List<Matrix>();

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
                    valid = GetTurretData(block, out Turret, "GatlingTurretBase1", "GatlingTurretBase2", "GatlingBarrel");
                }
                else if(block is IMyLargeMissileTurret)
                {
                    valid = GetTurretData(block, out Turret, "MissileTurretBase1", "MissileTurretBarrels");
                }
                else if(block is IMyLargeInteriorTurret)
                {
                    valid = GetTurretData(block, out Turret, "InteriorTurretBase1", "InteriorTurretBase2");
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

            var dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            muzzleEntity.Model.GetDummies(dummies);

            // from MyGunBase.LoadDummies()
            foreach(var dummy in dummies.Values)
            {
                if(dummy.Name.ContainsIgnoreCase("muzzle_projectile"))
                    weapon.ProjectileMuzzles.Add(dummy.Matrix);
                else if(dummy.Name.ContainsIgnoreCase("muzzle_missile"))
                    weapon.MissileMuzzles.Add(dummy.Matrix);
            }

            // it doesn't seem to actually be used
            //if(weapon.Turret != null)
            //{
            //    // from MyLargeBarrelBase.Init(MyEntity, MyLargeTurretBase)
            //    var cameraDummy = dummies.GetValueOrDefault("camera", null);
            //    if(cameraDummy != null)
            //        weapon.Turret.CameraLocalMatrix = cameraDummy.Matrix;
            //}

            bool hasDummies = (weapon.ProjectileMuzzles.Count > 0 || weapon.MissileMuzzles.Count > 0);
            if(!hasDummies)
            {
                if(block is IMyLargeGatlingTurret)
                {
                    // from MyLargeGatlingBarrel.Init()
                    weapon.ProjectileMuzzles.Add(Matrix.CreateTranslation(Vector3.Forward * 2));
                }
                else if(block is IMyLargeMissileTurret)
                {
                    // from MyLargeMissileBarrel.Init()
                    weapon.ProjectileMuzzles.Add(Matrix.CreateTranslation(Vector3.Forward * 3));
                }
                else if(block is IMyLargeInteriorTurret)
                {
                    // from MyLargeInteriorBarrel.Init()
                    weapon.ProjectileMuzzles.Add(Matrix.CreateTranslation(Vector3.Forward * -0.8f));
                }
                else if(block is IMySmallGatlingGun)
                {
                    // from MySmallGatlingGun.GetBarrelAndMuzzle()
                    var muzzleDummy = dummies.GetValueOrDefault("Muzzle", null);
                    if(muzzleDummy != null)
                        weapon.ProjectileMuzzles.Add(muzzleDummy.Matrix);
                    else
                        weapon.ProjectileMuzzles.Add(Matrix.CreateTranslation(new Vector3(0f, 0f, -1f)));
                }
                else if(block is IMySmallMissileLauncher)
                {
                    // from MySmallMissileLauncher.LoadDummies()
                    foreach(var dummy in dummies.Values)
                    {
                        if(dummy.Name.ContainsIgnoreCase("barrel"))
                            weapon.MissileMuzzles.Add(dummy.Matrix);
                    }
                }
            }

            dummies.Clear();
            return true;
        }

        public static bool GetTurretData(IMyCubeBlock block, out TurretData turret, string yawName, string pitchName, string barrelName = null)
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