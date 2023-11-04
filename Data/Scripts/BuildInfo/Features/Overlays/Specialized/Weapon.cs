using System;
using System.Collections.Generic;
using CoreSystems.Api;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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

            bool hasMuzzles = (data.Muzzles != null && data.Muzzles.Count > 0);

            MyLargeTurretBaseDefinition turretDef = def as MyLargeTurretBaseDefinition;
            BData_Turret dataTurret = data as BData_Turret;
            bool isTurret = (turretDef != null && dataTurret != null);

            IMyGunObject<MyGunBase> weaponBlock = block?.FatBlock as IMyGunObject<MyGunBase>;
            bool isRealBlock = weaponBlock != null;

            MatrixD barrelMatrix = drawMatrix;
            MatrixD matrixBase2 = drawMatrix;

            if(isRealBlock)
            {
                MyEntity barrelPart;

                if(isTurret)
                {
                    MyEntity subpartBase1;
                    MyEntity subpartBase2;

                    if(!dataTurret.GetTurretParts(block.FatBlock, out subpartBase1, out subpartBase2, out barrelPart))
                        return;

                    matrixBase2 = subpartBase2.PositionComp.WorldMatrixRef;

                    //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color.Lime, subpartBase1.WorldMatrix.Translation, subpartBase1.WorldMatrix.Forward, 3, 0.1f, blendType: BlendTypeEnum.AdditiveTop);
                    //MyTransparentGeometry.AddLineBillboard(MaterialLaser, Color.Red, subpartBase2.WorldMatrix.Translation, subpartBase2.WorldMatrix.Forward, 3, 0.1f, blendType: BlendTypeEnum.AdditiveTop);
                }
                else // not turret
                {
                    if(!data.GetBarrelPart(block.FatBlock, out barrelPart))
                        return;
                }

                barrelMatrix = barrelPart.WorldMatrix;

                //MyTransparentGeometry.AddLineBillboard(MaterialLaser, new Color(255, 0, 255), barrelMatrix.Translation, barrelMatrix.Forward, 25, 0.2f, blendType: BlendTypeEnum.AdditiveTop);
            }
            //else // preview block
            //{
            //    if(isTurret)
            //    {
            //        Vector3D posBase1 = Vector3D.Transform(dataTurret.YawLocalPos, drawMatrix);
            //        Vector3D centerBase1 = Vector3D.Transform(dataTurret.YawModelCenter, drawMatrix);
            //        Vector3D posBase2 = Vector3D.Transform(dataTurret.PitchLocalPos, drawMatrix);
            //
            //        MyTransparentGeometry.AddPointBillboard(MaterialDot, Color.Lime, posBase1, 0.1f, 0, blendType: BlendTypeEnum.AdditiveTop);
            //        MyTransparentGeometry.AddPointBillboard(MaterialDot, Color.Cyan, centerBase1, 0.1f, 0, blendType: BlendTypeEnum.AdditiveTop);
            //        MyTransparentGeometry.AddPointBillboard(MaterialDot, Color.Red, posBase2, 0.1f, 0, blendType: BlendTypeEnum.AdditiveTop);
            //    }
            //}

            // TODO: bring it closer and animate shots on it?
            // TODO: include current gravity estimated trajectory? or just mention that it gets affected by it...
            #region Accuracy cone
            if(hasMuzzles)
            {
                MyAmmoDefinition ammoDef = weaponBlock?.GunBase?.CurrentAmmoDefinition;
                if(ammoDef == null)
                {
                    MyAmmoMagazineDefinition mag = Utils.TryGetMagazineDefinition(weaponDef.AmmoMagazinesId[0], weaponDef.Context);
                    if(mag != null)
                        ammoDef = Utils.TryGetAmmoDefinition(mag.AmmoDefinitionId, mag.Context);
                }

                if(ammoDef != null)
                {
                    MuzzleData md = data.Muzzles[0];
                    MatrixD accMatrix = (isRealBlock ? md.Matrix_RelativeBarrel : md.Matrix_RelativePreview) * barrelMatrix;
                    accMatrix = MatrixD.Normalize(accMatrix);

                    float ammoRange = ammoDef.MaxTrajectory * weaponDef.RangeMultiplier;
                    float projectileMinTravel = ammoRange * Hardcoded.Projectile_RangeMultiplier_Min;
                    float projectileMaxTravel = ammoRange * Hardcoded.Projectile_RangeMultiplier_Max;

                    if(weaponDef.DeviateShotAngle > 0)
                    {
                        float tanShotAngle = (float)Math.Tan(weaponDef.DeviateShotAngle);
                        float radiusAtMaxRange = tanShotAngle * projectileMaxTravel;
                        Utils.DrawTransparentCone(ref accMatrix, radiusAtMaxRange, projectileMaxTravel, ref ColorAccuracy, MySimpleObjectRasterizer.Solid, (360 / AccuracyConeLinePerDeg), lineThickness: AccLineThick, material: MaterialSquare, blendType: BlendType);
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

                        if(weaponDef.UseRandomizedRange)
                            drawInstance.LabelRender.DynamicLabel.Clear().Append("Accuracy cone\n").DistanceRangeFormat(projectileMinTravel, projectileMaxTravel);
                        else
                            drawInstance.LabelRender.DynamicLabel.Clear().Append("Accuracy cone\n").DistanceFormat(projectileMaxTravel);

                        drawInstance.LabelRender.DrawLineLabel(LabelType.DynamicLabel, labelLineStart, labelDir, ColorAccuracy);
                    }
                }
            }
            #endregion

            #region Barrels
            if(hasMuzzles)
            {
                foreach(MuzzleData md in data.Muzzles)
                {
                    MatrixD wm = (isRealBlock ? md.Matrix_RelativeBarrel : md.Matrix_RelativePreview) * barrelMatrix;

                    MyTransparentGeometry.AddPointBillboard(MaterialMuzzleflash, ColorFlash, wm.Translation, 0.15f, 0, blendType: BlendTypeEnum.AdditiveBottom);

                    float size = (md.IsMissile ? 0.06f : 0.025f);
                    float len = (md.IsMissile ? 2f : 5f);
                    MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorBarrel, wm.Translation, (Vector3)wm.Forward, len, size, BlendType);
                }
            }
            #endregion Barrels

            #region Turret pitch/yaw limits
            if(isTurret)
            {
                float radius = (def.Size * drawInstance.CellSizeHalf).AbsMin() + 1f;

                int minPitch = turretDef.MinElevationDegrees; // this one is actually not capped in game for whatever reason
                int maxPitch = Math.Min(turretDef.MaxElevationDegrees, 90); // can't pitch up more than 90deg

                int minYaw = turretDef.MinAzimuthDegrees;
                int maxYaw = turretDef.MaxAzimuthDegrees;

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

                drawInstance.DrawTurretLimits(ref drawMatrix, ref pitchMatrix, dataTurret.TurretInfo, radius, minPitch, maxPitch, minYaw, maxYaw, canDrawLabel);
            }
            #endregion Turret pitch/yaw limits

            #region Camera viewpoint
            if(isTurret)
            {
                MatrixD view;
                Matrix? local = (isRealBlock ? dataTurret.Camera.RelativeSubpart : dataTurret.Camera.RelativePreview);
                if(local != null)
                {
                    view = local.Value * matrixBase2;
                }
                else
                {
                    // MyLargeTurretBase.GetViewMatrix()
                    view = matrixBase2;
                    view.Translation += view.Forward * turretDef.ForwardCameraOffset;
                    view.Translation += view.Up * turretDef.UpCameraOffset;
                }

                // TODO: use turretDef.MaxFov to show view frustum pyramid instead of a line
                MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorCamera, view.Translation, (Vector3)view.Forward, 3, 0.025f, BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelDir = view.Right;
                    Vector3D labelLineStart = view.Translation;

                    drawInstance.LabelRender.DrawLineLabel(LabelType.Camera, labelLineStart, labelDir, ColorCamera, "Camera", scale: 0.75f, alwaysOnTop: true);
                }
            }
            #endregion
        }
    }
}
