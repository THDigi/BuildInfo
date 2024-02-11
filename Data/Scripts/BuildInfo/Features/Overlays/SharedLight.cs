using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.Overlays.Specialized;
using Digi.BuildInfo.Utilities;
using Digi.Input;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static VRageRender.MyBillboard;

namespace Digi.BuildInfo.Features.Overlays
{
    public class SharedLightDraw
    {
        static Color Color = Color.Yellow;
        static Color ColorSphere = Color;
        static Color ColorSphereSeeThrough = Color * 0.4f;
        static Color ColorDot = Color;
        static Color ColorDotSeeThrough = Color * 0.25f;

        const float WireFrameThick = 0.02f;
        const float DotRadius = 0.1f;

        class InstanceData
        {
            public MyDefinitionId LastId;
            public float TweakRadius;
            public float TweakOffset;
            public IMyHudNotification Notify;
            public readonly List<LightLogicData.LightInfo> TempLights = new List<LightLogicData.LightInfo>(0);
        }

        Dictionary<OverlayDrawInstance, InstanceData> PerInstanceData = new Dictionary<OverlayDrawInstance, InstanceData>();

        // used by this and searchlight
        public void DrawLights(LightLogicData lightData, ref MatrixD drawMatrix, OverlayDrawInstance drawInstance,
            MyCubeBlockDefinition def, float range, float offset,
            IMySlimBlock block, MatrixD? originOverride = null)
        {
            #region Instance data handling
            InstanceData instanceData;
            if(!PerInstanceData.TryGetValue(drawInstance, out instanceData))
                PerInstanceData[drawInstance] = instanceData = new InstanceData();

            bool defChanged = instanceData.LastId != def.Id;
            instanceData.LastId = def.Id;
            #endregion

            BuildInfoMod Main = BuildInfoMod.Instance;

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            MatrixD toLocal = MatrixD.Invert(blockWorldMatrix);

            bool isCubeBuilder = Main.Overlays.DrawInstanceBuilderHeld == drawInstance;
            if(isCubeBuilder)
            {
                if(defChanged)
                {
                    instanceData.TweakRadius = lightData.LightRadius.Clamp(range);
                    instanceData.TweakOffset = lightData.LightOffset.Clamp(offset);
                }

                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    bool shift = MyAPIGateway.Input.IsAnyShiftKeyPressed();
                    bool alt = MyAPIGateway.Input.IsAnyAltKeyPressed();
                    if((shift || alt) && InputLib.IsInputReadable())
                    {
                        float radiusPerScroll = (float)Math.Round(lightData.LightRadius.Max / 10f, 2);
                        float offsetPerScroll = (float)Math.Round(lightData.LightOffset.Max / 10f, 2);

                        // intentionally allow both if both pressed
                        if(shift) instanceData.TweakRadius += (scroll > 0 ? radiusPerScroll : -radiusPerScroll);
                        if(alt) instanceData.TweakOffset += (scroll > 0 ? offsetPerScroll : -offsetPerScroll);

                        instanceData.TweakRadius = lightData.LightRadius.Clamp(instanceData.TweakRadius);
                        instanceData.TweakOffset = lightData.LightOffset.Clamp(instanceData.TweakOffset);

                        if(!Main.IsPaused)
                        {
                            if(instanceData.Notify == null)
                                instanceData.Notify = MyAPIGateway.Utilities.CreateNotification(string.Empty, 2000);

                            instanceData.Notify.Hide();
                            instanceData.Notify.Text = $"([Shift]) Radius: {instanceData.TweakRadius:0.##}m / ([Alt]) Offset: {instanceData.TweakOffset:0.##}m";
                            instanceData.Notify.Show();
                        }

                        // TODO maybe place the block with these settings?
                    }
                }

                range = instanceData.TweakRadius;
                offset = instanceData.TweakOffset;
            }

            List<LightLogicData.LightInfo> lights = lightData.Lights;

            if(originOverride == null && lightData.HasSubpartLights && block?.FatBlock != null)
            {
                lights = instanceData.TempLights;

                bool refreshLights = false;
                if(!defChanged)
                {
                    foreach(LightLogicData.LightInfo li in lights)
                    {
                        if(li.Subpart != null && li.Subpart.MarkedForClose)
                        {
                            refreshLights = true;
                            break;
                        }
                    }
                }

                if(defChanged || refreshLights)
                {
                    lights.Clear();

                    MatrixD transformToParent = block.FatBlock.WorldMatrixInvScaled;
                    LightLogicData.GetSubpartLightDataRecursive(lights, (MyEntity)block.FatBlock, lightData.DummyName, ref transformToParent);
                }
            }

            // HACK: to better fit with the visual light
            const float EyeballedMul = 0.95f;
            const int WireDivRatio = 360 / SpecializedOverlayBase.RoundedQualityMed;

            // like in MyLightingLogic.UpdateLightPosition()
            foreach(LightLogicData.LightInfo li in lights)
            {
                MatrixD wm;
                if(originOverride != null)
                {
                    wm = originOverride.Value;
                }
                else if(li.Subpart != null)
                {
                    wm = li.DummyMatrix * li.Subpart.WorldMatrix;
                }
                else
                {
                    wm = li.BlockLocalMatrix * blockWorldMatrix;
                }

                float pointRadius = range;

                if(lightData.IsSpotlight)
                {
                    float coneRadius = range * lightData.SpotlightConeTan;

                    Utils.DrawTransparentCone(ref wm, coneRadius, range, ref ColorSphere, MySimpleObjectRasterizer.Wireframe, WireDivRatio,
                        SpecializedOverlayBase.MaterialLaser, WireFrameThick, blendType: SpecializedOverlayBase.BlendType);

                    Utils.DrawTransparentCone(ref wm, coneRadius, range, ref ColorSphereSeeThrough, MySimpleObjectRasterizer.Wireframe, WireDivRatio,
                        SpecializedOverlayBase.MaterialLaser, WireFrameThick, blendType: BlendTypeEnum.AdditiveTop);

                    // HACK: hardcoded from MyReflectorLight/MySearchlight.UpdateRadius() which affects the point light
                    if(def is MyReflectorBlockDefinition || def is MySearchlightDefinition)
                        pointRadius = 10f * (pointRadius / lightData.LightReflectorRadiusMax);
                }

                // spotlights have a small point light too

                // only point light is offset by this
                wm.Translation += wm.Forward * offset;

                pointRadius *= EyeballedMul;

                Utils.DrawTransparentSphere(ref wm, pointRadius, ref ColorSphere, MySimpleObjectRasterizer.Wireframe, WireDivRatio,
                    SpecializedOverlayBase.MaterialLaser, WireFrameThick, blendType: SpecializedOverlayBase.BlendType);

                Utils.DrawTransparentSphere(ref wm, pointRadius, ref ColorSphereSeeThrough, MySimpleObjectRasterizer.Wireframe, WireDivRatio,
                    SpecializedOverlayBase.MaterialLaser, WireFrameThick, blendType: BlendTypeEnum.AdditiveTop);

                MyTransparentGeometry.AddPointBillboard(SpecializedOverlayBase.MaterialDot, ColorDot, wm.Translation, DotRadius, 0, blendType: SpecializedOverlayBase.BlendType);
                MyTransparentGeometry.AddPointBillboard(SpecializedOverlayBase.MaterialDot, ColorDotSeeThrough, wm.Translation, DotRadius, 0, blendType: BlendTypeEnum.AdditiveTop);
            }
        }
    }
}
