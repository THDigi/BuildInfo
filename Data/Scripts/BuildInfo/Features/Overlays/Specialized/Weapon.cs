using System;
using System.Collections.Generic;
using CoreSystems.Api;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Weapon : SpecializedOverlayBase
    {
        static Color ColorAccuracy = new Color(255, 155, 0);
        static Color ColorAccuracyLine = ColorAccuracy * 0.3f;

        static Color ColorRicochetText = new Color(255, 255, 255);
        static Color ColorRaycastRicochetLine = new Color(200, 0, 0);
        static Color ColorRaycastHitLine = new Color(0, 200, 0);
        static Color ColorRaycastNoHitLine = new Color(0, 55, 255);

        const float AccLineThick = 0.025f;
        const int AccuracyConeLinePerDeg = RoundedQualityMed;

        Vector4 ColorBarrel = Vector4.One; // white
        Vector4 ColorFlash = new Vector4(10, 10, 10, 1); // just like hand rifle

        Color ColorCamera = new Color(55, 155, 255);

        readonly MyStringId MaterialMuzzleflash = MyStringId.GetOrCompute("MuzzleFlashMachineGunFront");
        readonly MyStringId MaterialPenetration = MyStringId.GetOrCompute("Explosion");

        float RicochetBounceMin = 1f; // global data
        float RicochetBounceMax = 0f;

        public Weapon(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_SmallGatlingGun));
            Add(typeof(MyObjectBuilder_SmallMissileLauncher));
            Add(typeof(MyObjectBuilder_SmallMissileLauncherReload));
            Add(typeof(MyObjectBuilder_LargeGatlingTurret));
            Add(typeof(MyObjectBuilder_LargeMissileTurret));
            Add(typeof(MyObjectBuilder_InteriorTurret));

            // for ricochet directional chance, getting a few samples of the random to get a realistic range
            for(int i = 0; i < 100; i++)
            {
                // from MyMissile.HitEntity()
                float scaleFactor = (MyUtils.GetRandomFloat(0f, 1f) + MyUtils.GetRandomFloat(0f, 1f) + MyUtils.GetRandomFloat(0f, 1f)) / 3f;

                RicochetBounceMin = Math.Min(RicochetBounceMin, scaleFactor);
                RicochetBounceMax = Math.Max(RicochetBounceMax, scaleFactor);
            }
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

            bool isCamController = (isRealBlock ? MyAPIGateway.Session.CameraController == block.FatBlock : false);

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            MatrixD barrelMatrix = blockWorldMatrix;
            MatrixD matrixBase2 = blockWorldMatrix;

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
            #region Accuracy cone + ricochet simulation
            if(hasMuzzles)
            {
                AmmoInfo ammoInfo = new AmmoInfo()
                #region Ammo info retrieval
                {
                    RangeMin = float.MaxValue,
                    RangeMax = float.MaxValue,
                };

                if(isRealBlock)
                {
                    if(weaponBlock?.GunBase != null)
                    {
                        GetAmmoData(weaponDef, weaponBlock.GunBase.CurrentAmmoMagazineId, out ammoInfo);
                    }
                }
                else
                {
                    // find smallest range magazine
                    foreach(MyDefinitionId magId in weaponDef.AmmoMagazinesId)
                    {
                        AmmoInfo tmpAmmoInfo;
                        if(GetAmmoData(weaponDef, magId, out tmpAmmoInfo))
                        {
                            if(ammoInfo.RangeMin > tmpAmmoInfo.RangeMin)
                                ammoInfo = tmpAmmoInfo;
                        }
                    }
                }
                #endregion

                if(ammoInfo.AmmoDef != null)
                {
                    #region Draw accuracy cone
                    MuzzleData md = data.Muzzles[0];
                    MatrixD accMatrix = (isRealBlock ? md.Matrix_RelativeBarrel : md.Matrix_RelativePreview) * barrelMatrix;

                    float diameterAtMinRange = 0;

                    if(weaponDef.DeviateShotAngle > 0)
                    {
                        float tanShotAngle = (float)Math.Tan(weaponDef.DeviateShotAngle);
                        float radiusAtMinRange = tanShotAngle * ammoInfo.RangeMin;
                        diameterAtMinRange = radiusAtMinRange * 2;

                        Utils.DrawTransparentCone(ref accMatrix, radiusAtMinRange, ammoInfo.RangeMin, ref ColorAccuracyLine, MySimpleObjectRasterizer.Solid, (360 / AccuracyConeLinePerDeg), lineThickness: AccLineThick, material: MaterialSquare, blendType: BlendType);
                    }
                    else
                    {
                        MyTransparentGeometry.AddLineBillboard(MaterialSquare, ColorAccuracyLine, accMatrix.Translation, (Vector3)accMatrix.Forward, ammoInfo.RangeMin, AccLineThick, blendType: BlendType);
                    }

                    //const float PointRadius = 0.025f;
                    //MyTransparentGeometry.AddPointBillboard(OVERLAY_DOT_MATERIAL, accColor, accMatrix.Translation, PointRadius, 0, blendType: OVERLAY_BLEND_TYPE); // this is drawn always on top on purpose
                    #endregion

                    #region Ricochet visualization
                    MyMissileAmmoDefinition missileDef = ammoInfo.AmmoDef as MyMissileAmmoDefinition;
                    if(missileDef != null && missileDef.MissileMinRicochetAngle >= 0)
                    {
                        const float RicochetDistance = 30f; // or ammo's minimum distance if smaller
                        const float RicochetLineLength = 10f;

                        Vector3D from = accMatrix.Translation;
                        Vector3D direction = accMatrix.Forward;

                        float raycastDistance = Math.Min(RicochetDistance, ammoInfo.RangeMin);

                        Vector3D textPos = accMatrix.Translation + accMatrix.Forward * 5;
                        string text;

                        IHitInfo hit;
                        if(MyAPIGateway.Physics.CastRay(from, from + direction * raycastDistance, out hit, CollisionLayers.DefaultCollisionLayer)
                            && hit?.HitEntity is IMyCubeGrid)
                        {
                            Vector3 hitNormal = hit.Normal;
                            Vector3 velocity = direction;

                            // from MyMissile.Init() & MyMissile.HitEntity()

                            float ricochetMinAngleRad = MathHelper.ToRadians(missileDef.MissileMinRicochetAngle);

                            float impactAngle = MyMath.AngleBetween(hitNormal, -velocity);

                            float probabilityAtAngle = Hardcoded.Missile_GetRicochetProbability(missileDef, impactAngle);
                            text = $"{(90 - MathHelper.ToDegrees(impactAngle)):0}° = {probabilityAtAngle * 100:0}% chance of ricochet";

                            textPos = hit.Position;

                            // draw a triangle along where the hits will likely scatter
                            if(impactAngle >= ricochetMinAngleRad)
                            {
                                Vector3 projected = Vector3.ProjectOnPlane(ref velocity, ref hitNormal);
                                Vector3 diff = velocity - projected;

                                Vector3 minVelDir = Vector3.Normalize(projected - diff * RicochetBounceMin);
                                Vector3 maxVelDir = Vector3.Normalize(projected - diff * RicochetBounceMax);

                                Vector3 p0 = hit.Position;
                                Vector3 p1 = p0 + minVelDir * RicochetLineLength;
                                Vector3 p2 = p0 + maxVelDir * RicochetLineLength;

                                Vector3 n = Vector3.Forward; // does not offer anything

                                // texture is solid left and gradients to invisible right, so the triangle UV is as follows:
                                Vector2 uv0 = new Vector2(0.0f, 0.5f);
                                Vector2 uv1 = new Vector2(1.0f, 0.0f);
                                Vector2 uv2 = new Vector2(1.0f, 1.0f);

                                MyTransparentGeometry.AddTriangleBillboard(p0, p1, p2, n, n, n, uv0, uv1, uv2, MaterialGradient, uint.MaxValue, p1, ColorRaycastRicochetLine, BlendType);

                                MyTransparentGeometry.AddLineBillboard(MaterialSquare, ColorRaycastRicochetLine, from, (Vector3)direction, raycastDistance, AccLineThick, blendType: BlendType);
                            }
                            else
                            {
                                MyTransparentGeometry.AddLineBillboard(MaterialSquare, ColorRaycastHitLine, from, (Vector3)direction, raycastDistance, AccLineThick, blendType: BlendType);

                                MyTransparentGeometry.AddPointBillboard(MaterialPenetration, Vector4.One, hit.Position, 0.5f, 0, blendType: BlendType);
                            }
                        }
                        else
                        {
                            text = "Ricochet visualization\nAim this line at a grid";

                            MyTransparentGeometry.AddLineBillboard(MaterialSquare, ColorRaycastNoHitLine, from, (Vector3)direction, raycastDistance, AccLineThick, blendType: BlendType);
                        }

                        if(canDrawLabel)
                        {
                            Vector3D labelDir = accMatrix.Left;

                            drawInstance.LabelRender.DynamicLabel.Clear().Append(text);
                            drawInstance.LabelRender.DrawLineLabel(LabelType.DynamicLabel, textPos, labelDir, ColorRicochetText, alwaysOnTop: true);
                        }
                    }
                    #endregion

                    if(canDrawLabel)
                    {
                        Vector3D labelDir = accMatrix.Up;
                        Vector3D labelLineStart = accMatrix.Translation + accMatrix.Forward * ammoInfo.RangeMin;

                        drawInstance.LabelRender.DynamicLabel.Clear().DistanceFormat(diameterAtMinRange).Append(" group at ").DistanceFormat(ammoInfo.RangeMin);

                        drawInstance.LabelRender.DrawLineLabel(LabelType.DynamicLabel, labelLineStart, labelDir, ColorAccuracy, scale: 0.8f);
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

                    float thick = (md.IsMissile ? 0.06f : 0.025f);
                    float length = (md.IsMissile ? 1f : 2f);
                    MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorBarrel, wm.Translation, (Vector3)wm.Forward, length, thick, BlendType);
                }
            }
            #endregion Barrels

            #region Turret pitch/yaw limits
            if(isTurret && !isCamController)
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
                    pitchMatrix.Translation = blockWorldMatrix.Translation;
                }
                else
                {
                    pitchMatrix = blockWorldMatrix;
                }

                drawInstance.DrawTurretLimits(ref blockWorldMatrix, ref pitchMatrix, dataTurret.TurretInfo, radius, minPitch, maxPitch, minYaw, maxYaw, canDrawLabel);
            }
            #endregion Turret pitch/yaw limits

            #region Camera viewpoint
            if(isTurret && !isCamController)
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

                // TODO: use turretDef.MaxFov to show view frustum pyramid instead of a line - already done for raycast in camera overlay
                MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorCamera, view.Translation, (Vector3)view.Forward, 3, 0.025f, BlendType);
                MyTransparentGeometry.AddPointBillboard(MaterialDot, ColorCamera, view.Translation, 0.04f, 0, blendType: BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelDir = view.Right;
                    Vector3D labelLineStart = view.Translation;

                    drawInstance.LabelRender.DrawLineLabel(LabelType.Camera, labelLineStart, labelDir, ColorCamera, "Camera", scale: 0.75f, alwaysOnTop: true);
                }
            }
            #endregion
        }

        struct AmmoInfo
        {
            public MyAmmoMagazineDefinition MagDef;
            public MyAmmoDefinition AmmoDef;
            public float RangeMin;
            public float RangeMax;
        }

        bool GetAmmoData(MyWeaponDefinition weaponDef, MyDefinitionId magId, out AmmoInfo ammoInfo)
        {
            ammoInfo.MagDef = Utils.TryGetMagazineDefinition(magId, weaponDef.Context);
            ammoInfo.AmmoDef = (ammoInfo.MagDef != null ? Utils.TryGetAmmoDefinition(ammoInfo.MagDef.AmmoDefinitionId, ammoInfo.MagDef.Context) : null);
            ammoInfo.RangeMin = float.NaN;
            ammoInfo.RangeMax = float.NaN;

            if(ammoInfo.AmmoDef != null)
            {
                ammoInfo.RangeMin = ammoInfo.AmmoDef.MaxTrajectory;
                ammoInfo.RangeMax = ammoInfo.RangeMin;

                // HACK: only projectiles care about weapon.RangeMultiplier and weapon.UseRandomizedRange
                MyProjectileAmmoDefinition projectileDef = ammoInfo.AmmoDef as MyProjectileAmmoDefinition;
                if(projectileDef != null)
                {
                    ammoInfo.RangeMin *= weaponDef.RangeMultiplier;
                    ammoInfo.RangeMax *= weaponDef.RangeMultiplier;

                    if(weaponDef.UseRandomizedRange)
                    {
                        ammoInfo.RangeMin *= Hardcoded.Projectile_RandomRangeMin;
                        ammoInfo.RangeMax *= Hardcoded.Projectile_RandomRangeMax;
                    }
                }

                return true;
            }
            else
            {
                ammoInfo.MagDef = null;
                return false;
            }
        }
    }
}
