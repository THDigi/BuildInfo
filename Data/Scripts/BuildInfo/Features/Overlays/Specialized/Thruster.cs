using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static VRageRender.MyBillboard;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Thruster : SpecializedOverlayBase
    {
        static Color ColorGuaranteed = Color.Red;
        static Color ColorGuaranteedLines = ColorGuaranteed * LaserOverlayAlpha;
        static Vector4 ColorGuaranteedLinesLinear = ColorGuaranteedLines.ToVector4().ToLinearRGB();

        static Color ColorChance = Color.Yellow;
        static Color ColorChanceLines = ColorChance * LaserOverlayAlpha;
        static Vector4 ColorChanceLinesLinear = ColorChanceLines.ToVector4().ToLinearRGB();

        static Color ColorOther = Color.SkyBlue;
        static Color ColorOtherLines = ColorOther * LaserOverlayAlpha;

        const int LineEveryDeg = RoundedQualityLow;
        const float LineThickness = 0.02f;

        public Thruster(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_Thrust));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_Thrust data = Main.LiveDataHandler.Get<BData_Thrust>(def, drawInstance.BDataCache);
            if(data == null)
                return;

            MatrixD blockWorldMatrix = drawMatrix;
            blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

            MatrixD capsuleMatrix = MatrixD.CreateWorld(Vector3D.Zero, blockWorldMatrix.Up, blockWorldMatrix.Forward);

            bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

            foreach(BData_Thrust.FlameInfo flame in data.Flames)
            {
                Vector3D start = Vector3D.Transform(flame.LocalFrom, blockWorldMatrix);
                Vector3D dir = Vector3D.TransformNormal(flame.LocalDirection, blockWorldMatrix);

                float paddedRadius = flame.CapsuleRadius + Hardcoded.Thrust_DamageCapsuleRadiusAdd;

                // HACK: MyThrust.DamageGrid() does not include the radius length-wise, therefore it ends up being a cylinder damage shape, but only for blocks!
                // HACK: random range from MyThrust.UpdateThrusterLenght()
                float fullRange = flame.CapsuleLength;
                float guaranteedRange = fullRange * Hardcoded.Thrust_DamageRangeRandomMin;

                capsuleMatrix.Translation = start;

                #region draw complicated capsule
                {
                    // capsule code split into pieces to draw caps as different colors and also have a gradient part-way to indicate the lower chance of hit

                    int wireDivideRatio = (360 / LineEveryDeg);
                    float radius = paddedRadius;
                    float height = flame.CapsuleLength;
                    MyStringId material = MaterialSquare;
                    BlendTypeEnum blendType = BlendType;
                    int customViewProjection = -1;

                    const bool drawWireframe = true;
                    const bool drawSolid = false;

                    Vector3D checkPos = capsuleMatrix.Translation;

                    List<Vector3D> vertices = BuildInfoMod.Instance.Caches.Vertices;
                    vertices.Clear();
                    Utils.GetSphereVertices(ref capsuleMatrix, radius, wireDivideRatio, vertices);

                    int halfVerts = vertices.Count / 2;
                    Vector3D heightVec = capsuleMatrix.Up * height;

                    #region Sphere caps
                    for(int i = 0; i < vertices.Count; i += 4)
                    {
                        MyQuadD quad;

                        if(i < halfVerts)
                        {
                            quad.Point0 = vertices[i + 1] + heightVec;
                            quad.Point1 = vertices[i + 3] + heightVec;
                            quad.Point2 = vertices[i + 2] + heightVec;
                            quad.Point3 = vertices[i] + heightVec;
                        }
                        else
                        {
                            quad.Point0 = vertices[i + 1];
                            quad.Point1 = vertices[i + 3];
                            quad.Point2 = vertices[i + 2];
                            quad.Point3 = vertices[i];
                        }

                        if(drawWireframe)
                        {
                            // lines circling around Y axis
                            MyTransparentGeometry.AddLineBillboard(material, ColorOtherLines, quad.Point0, (Vector3)(quad.Point1 - quad.Point0), 1f, LineThickness, blendType, customViewProjection);

                            // lines from pole to half
                            MyTransparentGeometry.AddLineBillboard(material, ColorOtherLines, quad.Point1, (Vector3)(quad.Point2 - quad.Point1), 1f, LineThickness, blendType, customViewProjection);
                        }

                        //if(drawSolid)
                        //{
                        //    MyTransparentGeometry.AddQuad(material, ref quad, ColorOtherLines, ref checkPos, customViewProjection, blendType);
                        //}
                    }
                    #endregion

                    #region Cylinder - guaranteed hit
                    Color colorGuaranteed = ColorGuaranteedLines;

                    Vector3D topDirScaled = capsuleMatrix.Up * height;
                    Vector3D centerTop = capsuleMatrix.Translation + topDirScaled;
                    Vector3D centerBottom = capsuleMatrix.Translation;

                    Vector3 normal = (Vector3)capsuleMatrix.Up;
                    Vector2 uv0 = new Vector2(0, 0.5f);
                    Vector2 uv1 = new Vector2(1, 0);
                    Vector2 uv2 = new Vector2(1, 1);

                    double wireDivAngle = MathHelperD.Pi * 2f / (double)wireDivideRatio;

                    double cos = radius; // radius * Math.Cos(0)
                    double sin = 0; // radius * Math.Sin(0)

                    for(int k = 0; k < wireDivideRatio; k++)
                    {
                        double angle = k * wireDivAngle;
                        // cos & sin would be assigned here, but optimized to maintain last iteration's values instead
                        double oldCos = cos;
                        double oldSin = sin;

                        angle = (k + 1) * wireDivAngle;
                        cos = (radius * Math.Cos(angle));
                        sin = (radius * Math.Sin(angle));

                        //quad.Point0 = new Vector3D(oldCos, 0, oldSin);
                        //quad.Point1 = new Vector3D(oldCos, fullRange, oldSin);
                        //quad.Point2 = new Vector3D(cos, fullRange, sin);
                        //quad.Point3 = new Vector3D(cos, 0, sin);
                        //
                        //quad.Point0 = Vector3D.Transform(quad.Point0, worldMatrix);
                        //quad.Point1 = Vector3D.Transform(quad.Point1, worldMatrix);
                        //quad.Point2 = Vector3D.Transform(quad.Point2, worldMatrix);
                        //quad.Point3 = Vector3D.Transform(quad.Point3, worldMatrix);

                        #region lines guaranteed
                        if(drawWireframe)
                        {
                            Vector3D p1 = Vector3D.Transform(new Vector3D(cos, 0, sin), capsuleMatrix);

                            // only the lines along the tube, no end cap circles because those are provided by the spheres
                            MyTransparentGeometry.AddLineBillboard(material, colorGuaranteed, p1, capsuleMatrix.Up, guaranteedRange, LineThickness, blendType, customViewProjection);
                        }

                        //if(drawSolid)
                        //{
                        //    MyTransparentGeometry.AddQuad(material, ref quad, colorGuaranteed, ref checkPos, customViewProjection, blendType);
                        //}
                        #endregion

                        #region lines chance
                        if(drawWireframe)
                        {
                            Vector3D p1 = Vector3D.Transform(new Vector3D(cos, guaranteedRange, sin), capsuleMatrix);
                            Vector3D p2 = Vector3D.Transform(new Vector3D(cos, fullRange, sin), capsuleMatrix);

                            MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorGuaranteedLines, p1, (p2 - p1), 1f, LineThickness, blendType, customViewProjection);
                            MyTransparentGeometry.AddLineBillboard(MaterialGradient, ColorChanceLines, p2, (p1 - p2), 1f, LineThickness, blendType, customViewProjection);
                        }

                        //if(drawSolid)
                        //{
                        //    // proper UV for gradient texture, but still doesn't look good with PostPP because it needs sorting :/
                        //    quad.Point0 = new Vector3D(oldCos, guaranteedRange, oldSin);
                        //    quad.Point1 = new Vector3D(oldCos, fullRange, oldSin);
                        //    quad.Point2 = new Vector3D(cos, fullRange, sin);
                        //    quad.Point3 = new Vector3D(cos, guaranteedRange, sin);
                        //
                        //    quad.Point0 = Vector3D.Transform(quad.Point0, worldMatrix);
                        //    quad.Point1 = Vector3D.Transform(quad.Point1, worldMatrix);
                        //    quad.Point2 = Vector3D.Transform(quad.Point2, worldMatrix);
                        //    quad.Point3 = Vector3D.Transform(quad.Point3, worldMatrix);
                        //
                        //    MyTransparentGeometry.AddQuad(MaterialGradient, ref quad, ColorChanceLines, ref checkPos, customViewProjection, BlendType);
                        //}
                        #endregion

                        #region cylinder cap
                        if(drawWireframe)
                        {
                            Vector3D rimBottom = Vector3D.Transform(new Vector3D(cos, 0, sin), capsuleMatrix);
                            Vector3D rimTop = rimBottom + capsuleMatrix.Up * height;

                            // bottom
                            MyTransparentGeometry.AddLineBillboard(material, ColorGuaranteedLines, rimBottom, (Vector3)(centerBottom - rimBottom), 1f, LineThickness, blendType, customViewProjection);

                            // top
                            MyTransparentGeometry.AddLineBillboard(material, ColorChanceLines, rimTop, (Vector3)(centerTop - rimTop), 1f, LineThickness, blendType, customViewProjection);
                        }

                        //if(drawSolid)
                        //{
                        //    // bottom cap
                        //    MyTransparentGeometry.AddTriangleBillboard(centerBottom, quad.Point0, quad.Point1, normal, normal, normal, uv0, uv1, uv2, material, uint.MaxValue, checkPos, ColorGuaranteedLinesLinear, blendType);
                        //
                        //    // top cap
                        //    MyTransparentGeometry.AddTriangleBillboard(centerTop, quad.Point2, quad.Point3, normal, normal, normal, uv0, uv1, uv2, material, uint.MaxValue, checkPos, ColorChanceLinesLinear, blendType);
                        //}
                        #endregion
                    }
                    #endregion
                }
                #endregion

                if(drawLabel)
                {
                    drawLabel = false; // label only on the first flame
                    Vector3D labelDir = blockWorldMatrix.Up;
                    Vector3D labelLineStart = capsuleMatrix.Translation + capsuleMatrix.Up * guaranteedRange + labelDir * paddedRadius;
                    drawInstance.LabelRender.DrawLineLabel(LabelType.ThrustDamage, labelLineStart, labelDir, ColorGuaranteed, "Damage blocks\nGradient indicates chance of hit");

                    labelDir = blockWorldMatrix.Forward;
                    labelLineStart = Vector3D.Transform(flame.LocalFrom + flame.LocalDirection * flame.CapsuleLength, blockWorldMatrix) + labelDir * paddedRadius;
                    drawInstance.LabelRender.DrawLineLabel(LabelType.ThrustOther, labelLineStart, labelDir, ColorOther, "Damage others\n(includes the red cylinder)");
                }
            }
        }
    }
}
