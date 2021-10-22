using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class WindTurbine : SpecializedOverlayBase
    {
        static readonly Color MinColor = Color.Red;
        static readonly Color MaxColor = Color.YellowGreen;
        static readonly Vector4 MinLineColorVec = MinColor.ToVector4();
        static readonly Vector4 MaxLineColorVec = MaxColor.ToVector4();
        static readonly Vector4 MinColorVec = (MinColor * 0.45f).ToVector4();
        static readonly Vector4 MaxColorVec = (MaxColor * 0.45f).ToVector4();
        static readonly Vector4 TriangleColor = MinColorVec.ToLinearRGB(); // HACK required to match the colors of other billboards

        public WindTurbine(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_WindTurbine));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            MyWindTurbineDefinition turbineDef = (MyWindTurbineDefinition)def;
            bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

            const float lineThick = 0.05f;
            const float groundLineThick = 0.1f;
            const float groundBottomLineThick = 0.05f;
            MyQuadD quad;

            #region Side clearence circle
            float maxRadius = turbineDef.RaycasterSize;
            float minRadius = maxRadius * turbineDef.MinRaycasterClearance;

            float minRadiusRatio = turbineDef.MinRaycasterClearance;
            float maxRadiusRatio = 1f - minRadiusRatio;

            Vector3 up = (Vector3)drawMatrix.Up;
            Vector3D center = drawMatrix.Translation;

            Vector3D current = Vector3D.Zero;
            Vector3D previous = Vector3D.Zero;
            Vector3D previousInner = Vector3D.Zero;

            const int wireDivideRatio = 360 / 5;
            const float stepDeg = 360f / wireDivideRatio;

            for(int i = 0; i <= wireDivideRatio; i++)
            {
                double angleRad = MathHelperD.ToRadians(stepDeg * i);

                current.X = maxRadius * Math.Cos(angleRad);
                current.Y = 0;
                current.Z = maxRadius * Math.Sin(angleRad);
                current = Vector3D.Transform(current, drawMatrix);

                Vector3D dirToOut = (current - center);
                Vector3D inner = center + dirToOut * minRadiusRatio;

                if(i > 0)
                {
                    // inner circle slice
                    MyTransparentGeometry.AddTriangleBillboard(center, inner, previousInner, up, up, up, Vector2.Zero, Vector2.Zero, Vector2.Zero, MaterialSquare, 0, center, TriangleColor, BlendType);

                    // outer circle gradient slices
                    quad = new MyQuadD()
                    {
                        Point0 = previousInner,
                        Point1 = previous,
                        Point2 = current,
                        Point3 = inner,
                    };
                    MyTransparentGeometry.AddQuad(MaterialGradient, ref quad, MinColorVec, ref center, blendType: BlendType);

                    quad = new MyQuadD()
                    {
                        Point0 = previous,
                        Point1 = previousInner,
                        Point2 = inner,
                        Point3 = current,
                    };
                    MyTransparentGeometry.AddQuad(MaterialGradient, ref quad, MaxColorVec, ref center, blendType: BlendType);

                    // inner+outer circle rims
                    MyTransparentGeometry.AddLineBillboard(MaterialLaser, MinLineColorVec, previousInner, (Vector3)(inner - previousInner), 1f, lineThick, BlendType);
                    MyTransparentGeometry.AddLineBillboard(MaterialLaser, MaxLineColorVec, previous, (Vector3)(current - previous), 1f, lineThick, BlendType);
                }

                previous = current;
                previousInner = inner;
            }
            #endregion Side clearence circle

            if(drawLabel)
            {
                Vector3D labelDir = drawMatrix.Up;
                Vector3D labelLineStart = center + drawMatrix.Left * minRadius;
                drawInstance.LabelRender.DrawLineLabel(LabelType.SideClearence, labelLineStart, labelDir, new Color(255, 155, 0), "Side Clearence", lineHeight: 0.5f);

                //labelDir = drawMatrix.Up;
                //labelLineStart = center + drawMatrix.Left * maxRadius;
                //DrawLineLabel(TextAPIMsgIds.OptimalClearence, labelLineStart, labelDir, maxColor, message: "Optimal Clearence");
            }

            #region Ground clearence line
            Vector3D lineStart = drawMatrix.Translation;
            float artificialMultiplier;
            Vector3 gravityAccel = MyAPIGateway.Physics.CalculateNaturalGravityAt(lineStart, out artificialMultiplier);
            bool gravityNearby = (gravityAccel.LengthSquared() > 0);
            Vector3D end;

            if(gravityNearby)
                end = lineStart + Vector3.Normalize(gravityAccel) * turbineDef.OptimalGroundClearance;
            else
                end = lineStart + drawMatrix.Down * turbineDef.OptimalGroundClearance;

            Vector3D minClearence = Vector3D.Lerp(lineStart, end, turbineDef.MinRaycasterClearance);

            MyTransparentGeometry.AddLineBillboard(MaterialSquare, MinColor, lineStart, (Vector3)(minClearence - lineStart), 1f, groundLineThick, BlendType);

            Vector3 lineDir = (Vector3)(end - minClearence);
            MyTransparentGeometry.AddLineBillboard(MaterialGradient, MinColor, minClearence, lineDir, 1f, groundLineThick, BlendType);
            MyTransparentGeometry.AddLineBillboard(MaterialGradient, MaxColor, end, -lineDir, 1f, groundLineThick, BlendType);

            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3 right = (Vector3)Vector3D.Normalize(Vector3D.Cross(lineDir, camMatrix.Forward)); // this determines line width, it's normalized so 1m, doubled because of below math
            MyTransparentGeometry.AddLineBillboard(MaterialSquare, MaxColor, end - right, right, 2f, groundBottomLineThick, BlendType);

            if(drawLabel)
            {
                Vector3D labelDir = drawMatrix.Left;
                Vector3D labelLineStart = Vector3D.Lerp(lineStart, end, 0.5f);
                drawInstance.LabelRender.DrawLineLabel(LabelType.TerrainClearence, labelLineStart, labelDir, new Color(255, 155, 0), "Terrain Clearence");
            }
            #endregion Ground clearence line
        }
    }
}
