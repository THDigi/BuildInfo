using System;
using System.Collections.Generic;
using CoreSystems.Api;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Weapon : SpecializedOverlayBase
    {
        Color ColorAccuracy = new Color(255, 155, 0);
        const float AccLineThick = 0.01f;
        const int AccuracyConeLinePerDeg = RoundedQualityMed;

        Vector4 ColorBarrel = Vector4.One; // white
        Vector4 ColorFlash = new Vector4(10, 10, 10, 1); // just like hand rifle

        Color ColorPitch = (Color.Red * SolidOverlayAlpha).ToVector4();
        Vector4 ColorPitchLine = Color.Red.ToVector4();
        Color ColorYaw = (Color.Lime * SolidOverlayAlpha).ToVector4();
        Vector4 ColorYawLine = Color.Lime.ToVector4();
        const int LimitsLineEveryDegrees = RoundedQualityHigh;
        const float LimitsLineThick = 0.03f;

        Color ColorCamera = new Color(55, 155, 255);

        readonly MyStringId MaterialMuzzleflash = MyStringId.GetOrCompute("MuzzleFlashMachineGunFront");

        public Weapon(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_SmallGatlingGun));
            Add(typeof(MyObjectBuilder_SmallMissileLauncher));
            Add(typeof(MyObjectBuilder_SmallMissileLauncherReload));
            Add(typeof(MyObjectBuilder_LargeGatlingTurret));
            Add(typeof(MyObjectBuilder_LargeMissileTurret));
            Add(typeof(MyObjectBuilder_InteriorTurret));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            List<CoreSystemsDef.WeaponDefinition> wcDefs;
            if(Main.CoreSystemsAPIHandler.Weapons.TryGetValue(def.Id, out wcDefs))
            {
                //ConveyorSorter.DrawWeaponCoreWeapon(ref drawMatrix, drawInstance, def, block, wcDefs);
                return;
            }

            BData_Weapon data = Main.LiveDataHandler.Get<BData_Weapon>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            MyWeaponBlockDefinition weaponBlockDef = def as MyWeaponBlockDefinition;
            MyWeaponDefinition weaponDef;
            if(weaponBlockDef == null || !MyDefinitionManager.Static.TryGetWeaponDefinition(weaponBlockDef.WeaponDefinitionId, out weaponDef))
                return;

            bool canDrawLabel = drawInstance.LabelRender.CanDrawLabel();

            IMyGunObject<MyGunBase> weaponBlock = block?.FatBlock as IMyGunObject<MyGunBase>;

            IMyEntity muzzleEntity = null;
            bool hasMuzzles = (data.Muzzles != null && data.Muzzles.Count > 0);

            // TODO: include current gravity estimated trajectory? or just mention that it gets affected by it...
            #region Accuracy cone
            if(hasMuzzles)
            {
                MyAmmoDefinition ammoDef = weaponBlock?.GunBase?.CurrentAmmoDefinition;
                if(ammoDef == null)
                {
                    MyAmmoMagazineDefinition mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(weaponDef.AmmoMagazinesId[0]);
                    ammoDef = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                }

                MatrixD barrelMatrix;
                if(weaponBlock != null)
                {
                    muzzleEntity = BData_Weapon.GetAimSubpart(block.FatBlock);
                    barrelMatrix = muzzleEntity.WorldMatrix;
                }
                else
                {
                    barrelMatrix = drawMatrix;
                }

                // for debugging barrel ent orientation
                //MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, Color.Red, barrelMatrix.Translation, barrelMatrix.Right, 3f, 0.005f, blendType: BlendTypeEnum.AdditiveTop);
                //MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, Color.Green, barrelMatrix.Translation, barrelMatrix.Up, 3f, 0.005f, blendType: BlendTypeEnum.AdditiveTop);
                //MyTransparentGeometry.AddLineBillboard(OVERLAY_SQUARE_MATERIAL, Color.Blue, barrelMatrix.Translation, barrelMatrix.Forward, 3f, 0.005f, blendType: BlendTypeEnum.AdditiveTop);

                MuzzleData md = data.Muzzles[0];
                MatrixD accMatrix = (muzzleEntity != null ? md.MatrixForSubpart : md.MatrixForBlock) * barrelMatrix;

                float ammoRange = ammoDef.MaxTrajectory * weaponDef.RangeMultiplier;
                float projectileMinTravel = ammoRange * Hardcoded.Projectile_RangeMultiplier_Min;
                float projectileMaxTravel = ammoRange * Hardcoded.Projectile_RangeMultiplier_Max;
                bool randomizedRange = weaponDef.UseRandomizedRange;

                // TODO accuracy circle closer instead?

                if(weaponDef.DeviateShotAngle > 0)
                {
                    float tanShotAngle = (float)Math.Tan(weaponDef.DeviateShotAngle);
                    float radiusAtMaxRange = tanShotAngle * projectileMaxTravel;
                    Utils.DrawTransparentCone(ref accMatrix, radiusAtMaxRange, projectileMaxTravel, ref ColorAccuracy, MySimpleObjectRasterizer.Solid, (360 / AccuracyConeLinePerDeg), lineThickness: AccLineThick, material: MaterialSquare, blendType: BlendType);

                    //var colorAtMinRange = Color.Lime.ToVector4();
                    //var radiusAtMinRange = tanShotAngle * projectileMinTravel;
                    //var circleMatrix = MatrixD.CreateWorld(accMatrix.Translation + accMatrix.Forward * projectileMinTravel, accMatrix.Down, accMatrix.Forward);
                    //MySimpleObjectDraw.DrawTransparentCylinder(ref circleMatrix, radiusAtMinRange, radiusAtMinRange, 0.1f, ref colorAtMinRange, true, ConeWireDivideRatio, 0.05f, OVERLAY_SQUARE_MATERIAL);
                }
                else
                {
                    MyTransparentGeometry.AddLineBillboard(MaterialSquare, ColorAccuracy, accMatrix.Translation, (Vector3)accMatrix.Forward, projectileMaxTravel, AccLineThick, blendType: BlendType);
                }

                //const float PointRadius = 0.025f;
                //MyTransparentGeometry.AddPointBillboard(OVERLAY_DOT_MATERIAL, accColor, accMatrix.Translation, PointRadius, 0, blendType: OVERLAY_BLEND_TYPE); // this is drawn always on top on purpose

                if(canDrawLabel)
                {
                    Vector3D labelDir = accMatrix.Up;
                    Vector3D labelLineStart = accMatrix.Translation + accMatrix.Forward * 3;

                    if(randomizedRange)
                        drawInstance.LabelRender.DynamicLabel.Clear().Append("Accuracy cone\n").DistanceRangeFormat(projectileMinTravel, projectileMaxTravel);
                    else
                        drawInstance.LabelRender.DynamicLabel.Clear().Append("Accuracy cone\n").DistanceFormat(projectileMaxTravel);

                    drawInstance.LabelRender.DrawLineLabel(LabelType.DynamicLabel, labelLineStart, labelDir, ColorAccuracy);
                }
            }
            #endregion Accuracy cone

            #region Barrels
            if(hasMuzzles)
            {
                if(muzzleEntity == null && weaponBlock != null)
                    muzzleEntity = BData_Weapon.GetAimSubpart(block.FatBlock);

                bool haveSubpart = (muzzleEntity != null);

                foreach(MuzzleData md in data.Muzzles)
                {
                    MatrixD wm = (haveSubpart ? md.MatrixForSubpart * muzzleEntity.WorldMatrix : md.MatrixForBlock * drawMatrix);

                    MyTransparentGeometry.AddPointBillboard(MaterialMuzzleflash, ColorFlash, wm.Translation, 0.15f, 0, blendType: BlendTypeEnum.AdditiveBottom);

                    float size = (md.Missile ? 0.06f : 0.025f);
                    float len = (md.Missile ? 2f : 5f);
                    MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorBarrel, wm.Translation, (Vector3)wm.Forward, len, size, BlendType);
                }
            }
            #endregion Barrels

            #region Turret pitch/yaw limits
            MyLargeTurretBaseDefinition turretDef = def as MyLargeTurretBaseDefinition;
            bool isTurret = (turretDef != null && data.Turret != null);
            if(isTurret)
            {
                float radius = (def.Size * drawInstance.CellSizeHalf).AbsMin() + 1f;

                int minPitch = turretDef.MinElevationDegrees; // this one is actually not capped in game for whatever reason
                int maxPitch = Math.Min(turretDef.MaxElevationDegrees, 90); // can't pitch up more than 90deg

                int minYaw = turretDef.MinAzimuthDegrees;
                int maxYaw = turretDef.MaxAzimuthDegrees;

                // pitch limit indicator
                {
                    MatrixD pitchMatrix;
                    if(weaponBlock != null)
                    {
                        pitchMatrix = weaponBlock.GunBase.GetMuzzleWorldMatrix();
                        pitchMatrix.Translation = drawMatrix.Translation;
                    }
                    else
                    {
                        pitchMatrix = drawMatrix;
                    }

                    // only yaw rotation
                    MatrixD m = MatrixD.CreateWorld(drawMatrix.Translation, Vector3D.Cross(pitchMatrix.Left, drawMatrix.Up), drawMatrix.Up);
                    Vector3D rotationPivot = Vector3D.Transform(data.Turret.PitchLocalPos, m);

                    // only yaw rotation but for cylinder
                    pitchMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Down, pitchMatrix.Left);

                    Vector3D firstOuterRimVec, lastOuterRimVec;
                    drawInstance.DrawTurretAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                        ref pitchMatrix, radius, minPitch, maxPitch, LimitsLineEveryDegrees,
                        ColorPitch, ColorPitchLine, MaterialSquare, MaterialLaser, LimitsLineThick, BlendType);

                    if(canDrawLabel)
                    {
                        Vector3D labelDir = Vector3D.Normalize(lastOuterRimVec - pitchMatrix.Translation);
                        drawInstance.LabelRender.DrawLineLabel(LabelType.PitchLimit, lastOuterRimVec, labelDir, ColorPitchLine, "Pitch limit");
                    }
                }

                // yaw limit indicator
                {
                    Vector3D rotationPivot = Vector3D.Transform(data.Turret.YawModelCenter, drawMatrix);

                    MatrixD yawMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Right, drawMatrix.Down);

                    Vector3D firstOuterRimVec, lastOuterRimVec;
                    drawInstance.DrawTurretAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                        ref yawMatrix, radius, minYaw, maxYaw, LimitsLineEveryDegrees,
                        ColorYaw, ColorYawLine, MaterialSquare, MaterialLaser, LimitsLineThick, BlendType);

                    if(canDrawLabel)
                    {
                        Vector3D labelDir = Vector3D.Normalize(firstOuterRimVec - yawMatrix.Translation);
                        drawInstance.LabelRender.DrawLineLabel(LabelType.YawLimit, firstOuterRimVec, labelDir, ColorYawLine, "Yaw limit");
                    }
                }

                // camera position indicator
                {
                    MatrixD turretCamMatrix = (muzzleEntity == null ? data.Turret.CameraForBlock * drawMatrix : data.Turret.CameraForSubpart * muzzleEntity.WorldMatrix);
                    MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorCamera, turretCamMatrix.Translation, (Vector3)turretCamMatrix.Forward, 3, 0.01f, BlendType);
                }
            }
            #endregion Turret pitch/yaw limits
        }
    }
}
