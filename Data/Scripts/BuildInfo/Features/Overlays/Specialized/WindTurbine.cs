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
        static readonly Vector4 MinColorVec = (MinColor * SolidOverlayAlpha).ToVector4();
        static readonly Vector4 MaxColorVec = (MaxColor * SolidOverlayAlpha).ToVector4();
        static readonly Vector4 TriangleColor = MinColorVec.ToLinearRGB(); // HACK: keeping color consistent with other billboards, MyTransparentGeoemtry.CreateBillboard()

        const int LineEveryDeg = RoundedQualityHigh;

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

            Vector3D blockPos = Vector3D.Transform(def.ModelOffset, drawMatrix); // game code uses WorldMatrix.Translation which is affected by ModelOffset

            #region Side clearance circle
            float minRadius = turbineDef.RaycasterSize * turbineDef.MinRaycasterClearance;
            float maxRadius = turbineDef.RaycasterSize;

            float minRadiusRatio = turbineDef.MinRaycasterClearance;
            float maxRadiusRatio = 1f - minRadiusRatio;

            Vector3 up = (Vector3)drawMatrix.Up;
            Vector3D center = drawMatrix.Translation;

            Vector3D current = Vector3D.Zero;
            Vector3D previous = Vector3D.Zero;
            Vector3D previousInner = Vector3D.Zero;

            const int wireDivideRatio = 360 / LineEveryDeg;
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
                    MyTransparentGeometry.AddQuad(MaterialGradientSRGB, ref quad, MinColorVec, ref center, blendType: BlendType);

                    quad = new MyQuadD()
                    {
                        Point0 = previous,
                        Point1 = previousInner,
                        Point2 = inner,
                        Point3 = current,
                    };
                    MyTransparentGeometry.AddQuad(MaterialGradientSRGB, ref quad, MaxColorVec, ref center, blendType: BlendType);

                    // inner+outer circle rims
                    MyTransparentGeometry.AddLineBillboard(MaterialLaser, MinLineColorVec, previousInner, (Vector3)(inner - previousInner), 1f, lineThick, BlendType);
                    MyTransparentGeometry.AddLineBillboard(MaterialLaser, MaxLineColorVec, previous, (Vector3)(current - previous), 1f, lineThick, BlendType);
                }

                previous = current;
                previousInner = inner;
            }
            #endregion Side clearance circle

            if(drawLabel)
            {
                Vector3D labelDir = drawMatrix.Up;
                Vector3D labelLineStart = center + drawMatrix.Left * minRadius;
                drawInstance.LabelRender.DrawLineLabel(LabelType.SideClearance, labelLineStart, labelDir, new Color(255, 155, 0), "Side Clearance", lineHeight: 0.5f);

                //labelDir = drawMatrix.Up;
                //labelLineStart = center + drawMatrix.Left * maxRadius;
                //DrawLineLabel(TextAPIMsgIds.OptimalClearance, labelLineStart, labelDir, maxColor, message: "Optimal Clearance");
            }

            #region Ground clearance line
            float groundMinDist = turbineDef.OptimalGroundClearance * turbineDef.MinRaycasterClearance;
            float groundMaxDist = turbineDef.OptimalGroundClearance;

            float artificialMultiplier;
            Vector3 gravityAccel = MyAPIGateway.Physics.CalculateNaturalGravityAt(blockPos, out artificialMultiplier);
            Vector3 groundDir;
            if(gravityAccel.LengthSquared() > 0)
                groundDir = Vector3.Normalize(gravityAccel);
            else
                groundDir = (Vector3)drawMatrix.Down;

            Vector3D minPos = blockPos + groundDir * groundMinDist;
            Vector3D maxPos = blockPos + groundDir * groundMaxDist;

            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3 lineDir = (Vector3)(maxPos - minPos);
            Vector3 right = (Vector3)Vector3D.Normalize(Vector3D.Cross(lineDir, camMatrix.Forward));

            // red line
            MyTransparentGeometry.AddLineBillboard(MaterialSquare, MinColor, blockPos, (Vector3)(minPos - blockPos), 1f, groundLineThick, BlendType);

            // marker at min clearance
            MyTransparentGeometry.AddLineBillboard(MaterialSquare, MinColor, minPos - right, right, 2f, groundBottomLineThick, BlendType);

            // gradient red-to-green line (texture gradients to transparent, hence the 2 lines)
            MyTransparentGeometry.AddLineBillboard(MaterialGradient, MinColor, minPos, lineDir, 1f, groundLineThick, BlendType);
            MyTransparentGeometry.AddLineBillboard(MaterialGradient, MaxColor, maxPos, -lineDir, 1f, groundLineThick, BlendType);

            // marker at max clearance
            MyTransparentGeometry.AddLineBillboard(MaterialSquare, MaxColor, maxPos - right, right, 2f, groundBottomLineThick, BlendType);

            if(drawLabel)
            {
                Vector3D labelDir = drawMatrix.Left;
                Vector3D labelLineStart;
                if(groundMinDist >= (drawInstance.CellSize * 2))
                    labelLineStart = minPos;
                else
                    labelLineStart = Vector3D.Lerp(minPos, maxPos, 0.5f);

                drawInstance.LabelRender.DrawLineLabel(LabelType.TerrainClearance, labelLineStart, labelDir, new Color(255, 155, 0), "Terrain Clearance");
            }
            #endregion Ground clearance line
        }
    }
}
