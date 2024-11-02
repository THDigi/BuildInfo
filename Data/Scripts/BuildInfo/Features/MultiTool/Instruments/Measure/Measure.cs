using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.Input;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.Utils;
using VRageMath;
using static Digi.BuildInfo.Systems.TextAPI;
using static VRageRender.MyBillboard;

namespace Digi.BuildInfo.Features.MultiTool.Instruments.Measure
{
    enum Trigger { None, Click, Hold }

    enum RulerSize { LargeGrid, SmallGrid, Metric } // change SetStepSizeMode() too when modifying this

    public class Measure : InstrumentBase
    {
        internal const float MaxRange = 10;
        internal const float RangeNoHit = 2.5f;
        internal const float AimableRadius = 0.1f;
        internal const double TextScale = 0.8;
        internal const float LineThick = 0.1f;
        internal const float LineStepThick = 0.01f;
        internal const float AlphaSeeThrough = 0.75f;

        internal const double MaxDrawDistanceSq = 1000 * 1000;
        internal const double MaxLabelDistanceSq = 300 * 300;

        internal const int RaycastLayer = 15;

        Color ColorInProgress = Color.White;

        Color CursorColorHit = Color.Lime;
        Color CursorColorFloating = Color.Gray;

        IMyHudNotification[] Notifications = new IMyHudNotification[2];

        //internal readonly List<MeasurementLine> Lines = new List<MeasurementLine>();
        //internal readonly List<MeasurementAngle> Angles = new List<MeasurementAngle>();
        internal readonly List<MeasurementBase> Measurements = new List<MeasurementBase>();

        internal RulerSize RulerSizeMode;
        internal float RulerSizeMeters;

        internal double AngleSnapRad;
        internal int AngleSnapDividerIdx;
        readonly int[] AngleSnapDividers =
        {
            12, // 15 deg
            32, // 5.625 deg
            180, // 1 deg
            //36, // 5 deg
        };

        int PrimaryHeld;
        int SymmetryHeld;

        IMeasureVertex InProgressMeasure;

        TextPackage Text;

        Aimables Aimables;
        Modifiers Modifiers;

        string BindsInfo;

        public Measure() : base("Measure (Beta)", Constants.MatUI_Icon_Measure)
        {
            Main.GUIMonitor.OptionsMenuClosed += RefreshBindsInfo;
            Main.TextAPI.Detected += CreateUI;

            Aimables = new Aimables(this);
            Modifiers = new Modifiers(this);

            SetRulerSize(RulerSize.LargeGrid, notify: false);
            SetAngleSnap(0, notify: false);

            RefreshBindsInfo();
        }

        public override void Dispose()
        {
            Main.GUIMonitor.OptionsMenuClosed -= RefreshBindsInfo;
            Main.TextAPI.Detected -= CreateUI;
        }

        public override void Selected()
        {
        }

        public override void Deselected()
        {
            Aim = default(AimedAt);
            HasTarget = false;
            PrimaryHeld = 0;

            HideAll();

            if(Text != null)
                Text.Visible = false;
        }

        void RefreshBindsInfo()
        {
            var sb = new StringBuilder(512);

            MultiTool.ControlPrimary.GetBind(sb);
            sb.AppendLine(" measure");

            MultiTool.ControlSecondary.GetBind(sb);
            sb.AppendLine(" snap to grid/line");

            sb.AppendLine("SHIFT snap direction");

            sb.AppendLine("CTRL snap angle");

            MultiTool.ControlAlignDefault.GetBind(sb);
            sb.AppendLine(" snap length");

            MultiTool.ControlSymmetry.GetBind(sb);
            sb.AppendLine(" cancel progress/delete aimed");

            sb.Append("Hold ");
            MultiTool.ControlSymmetry.GetBind(sb);
            sb.AppendLine(" to delete all");

            MultiTool.ControlReload.GetBind(sb);
            sb.Append(" cycle ruler (").Append(MyEnum<RulerSize>.GetName(RulerSizeMode));
            sb.Append(" / ").DistanceFormat(RulerSizeMeters);
            sb.Append(")").AppendLine();

            MultiTool.ControlSymmetrySwitch.GetBind(sb);
            sb.Append(" cycle angle snapping (").Append(MathHelper.ToDegrees(AngleSnapRad).ToString("0.#####")).Append("°)").AppendLine();

            BindsInfo = sb.ToString();

            PrevAccel = -1; // force refresh description

            UpdateDescription();
        }

        void CreateUI()
        {
            Text = new TextPackage(64, false, Constants.MatUI_Square);
            Text.Scale = TextScale;

            foreach(var m in Measurements)
            {
                m.CreateUI();
            }
        }

        void HideAll()
        {
            foreach(var m in Measurements)
            {
                m.Hide();
            }

            if(Text != null)
                Text.Visible = false;

            ResetAim();
        }

        void ClearMeasurements()
        {
            try
            {
                foreach(var m in Measurements)
                {
                    m.Dispose();
                }
            }
            finally
            {
                Measurements.Clear();
            }
        }

        void CancelProgress()
        {
            if(InProgressMeasure != null)
            {
                InProgressMeasure.Dispose();
                InProgressMeasure = null;

                if(Text != null)
                    Text.Visible = false;

                ResetAim();
            }
        }

        void SetRulerSize(RulerSize mode, bool notify = true)
        {
            RulerSize max = MyEnum<RulerSize>.Range.Max;
            if(mode < 0)
                mode = max;
            else if(mode > max)
                mode = 0;

            RulerSizeMode = mode;

            switch(mode)
            {
                case RulerSize.LargeGrid: RulerSizeMeters = MyDefinitionManager.Static.GetCubeSize(MyCubeSize.Large); break;
                case RulerSize.SmallGrid: RulerSizeMeters = MyDefinitionManager.Static.GetCubeSize(MyCubeSize.Small); break;
                case RulerSize.Metric: RulerSizeMeters = 1f; break;
                default: throw new Exception($"New unknown ruler enum: {mode}");
            }

            if(notify)
            {
                Notify(0, $"Ruler changed to [{RulerSizeMeters:0.#####}m] ({MyEnum<RulerSize>.GetName(RulerSizeMode)})");

                RefreshBindsInfo();
            }
        }

        void SetAngleSnap(int index, bool notify = true)
        {
            if(index < 0)
                index = AngleSnapDividers.Length - 1;
            else if(index >= AngleSnapDividers.Length)
                index = 0;

            AngleSnapDividerIdx = index;
            int divider = AngleSnapDividers[index];

            AngleSnapRad = Math.PI / divider;

            if(notify)
            {
                Notify(0, $"Angle snap changed to [{MathHelper.ToDegrees(AngleSnapRad):0.#####}°] (180/{divider})");

                RefreshBindsInfo();
            }
        }

        public override void Update(bool inputReadable)
        {
            if(Main.Tick % 3 == 0)
            {
                if(MultiTool.IsUIVisible && Main.GameConfig.HudState != HudState.OFF)
                {
                    UpdateDescription();
                }
            }

            if(inputReadable)
            {
                HandleControls();
            }
        }

        float PrevAccel;

        void UpdateDescription()
        {
            float accel = MyAPIGateway.Session?.ControlledObject?.Entity?.Physics?.LinearAcceleration.Length() ?? 0;
            if(accel == PrevAccel)
                return;

            PrevAccel = accel;

            var sb = Description.Builder.Clear();

            sb.Clear();
            sb.Append(BindsInfo);
            sb.AppendLine();
            sb.Append("Acceleration: ").AccelerationFormat(accel).AppendLine();

            Description.UpdateFromBuilder();
        }

        public override void Draw()
        {
            InputReadable = MultiTool.IsUIVisible && InputLib.IsInputReadable();

            if(!MultiTool.IsUIVisible)
            {
                HideAll();
                return;
            }

            foreach(var m in Measurements)
            {
                m.Draw();
            }

            foreach(var m in Measurements)
            {
                m.DrawHUD();
            }

            if(InputReadable)
            {
                HandleAim();
            }
        }

        void DeleteLine(MeasurementLine line)
        {
            try
            {
                for(int i = Measurements.Count - 1; i >= 0; i--)
                {
                    MeasurementAngle m = Measurements[i] as MeasurementAngle;
                    if(m == null)
                        continue;

                    if(m.Common == line.A || m.Common == line.B
                    || m.PointA == line.A || m.PointA == line.B
                    || m.PointB == line.A || m.PointB == line.B)
                    {
                        m.Dispose();
                        Measurements.RemoveAtFast(i);
                    }
                }

                line.Dispose();
            }
            finally
            {
                Measurements.Remove(line);
            }
        }

        void HandleControls()
        {
            const int HoldTicksToDelete = 40;

            if(MultiTool.ControlSymmetry.IsPressed())
            {
                SymmetryHeld++;

                if(SymmetryHeld == HoldTicksToDelete)
                {
                    ClearMeasurements();
                    CancelProgress();
                    Notify(0, "All measurements removed");
                }
            }
            else
            {
                if(SymmetryHeld > 0)
                {
                    if(SymmetryHeld < HoldTicksToDelete)
                    {
                        CancelProgress();
                    }

                    SymmetryHeld = 0;
                }
            }

            if(MultiTool.ControlReload.IsJustPressed())
            {
                SetRulerSize(RulerSizeMode + 1);
            }

            if(MultiTool.ControlSymmetrySwitch.IsJustPressed())
            {
                SetAngleSnap(AngleSnapDividerIdx + 1);
            }
        }

        internal bool InputReadable;
        internal bool UpdatedAim;
        internal AimedAt Aim;
        internal bool HasTarget;
        internal Color CursorColor;
        internal bool IgnoreHeld;
        internal int LetItGo;

        StringBuilder ExtraText = new StringBuilder(256);

        void ResetAim()
        {
            InputReadable = false;
            Aim = default(AimedAt);
            HasTarget = false;
            UpdatedAim = false;
            IgnoreHeld = true;
            LetItGo = 0;
        }

        void HandleAim()
        {
            bool isPrimaryActuallyHeld = MultiTool.ControlPrimary.IsPressed();

            if(!isPrimaryActuallyHeld)
            {
                IgnoreHeld = false;
                LetItGo = 0;
            }

            if(IgnoreHeld && ++LetItGo == 60)
            {
                MyAPIGateway.Utilities.ShowNotification("You can let go now", 1000);
            }

            bool isPrimaryHeld = !IgnoreHeld && isPrimaryActuallyHeld;

            CursorColor = CursorColorHit;

            if(!isPrimaryHeld)
            {
                UpdatedAim = true;
                HasTarget = Aimables.GetPos(out Aim);
                if(!HasTarget)
                {
                    Aim = default(AimedAt);

                    MatrixD camWM = MyAPIGateway.Session.Camera.WorldMatrix;
                    Aim.WorldPosition = camWM.Translation + camWM.Forward * RangeNoHit;

                    CursorColor = CursorColorFloating;
                }

                if(InProgressMeasure == null && (Aim.AnchorVertex?.HostLine != null || Aim.AnchorLine != null) && MultiTool.ControlSymmetry.IsJustPressed())
                {
                    DeleteLine(Aim.AnchorLine ?? Aim.AnchorVertex.HostLine);
                    return;
                }
            }

            if(!UpdatedAim)
            {
                return;
            }

            bool isInProgress = InProgressMeasure != null;

            Modifiers.Reset();

            if(isInProgress && !isPrimaryHeld)
            {
                Modifiers.Apply(InProgressMeasure);
            }

            #region Draw ghost line+angle 
            if(isInProgress)
            {
                ExtraText.Clear();

                if(Modifiers.SnapDir != null)
                    ExtraText.Color(Color.Lime).Append("Snap direction: ").Append(Modifiers.SnapDir).NewCleanLine();

                if(Modifiers.SnapAngle != null)
                    ExtraText.Color(Color.Cyan).Append("Snap angle: ").Append(Modifiers.SnapAngle).NewCleanLine();

                DrawMeasurementLine(InProgressMeasure.GetWorldPosition(), Aim.WorldPosition, ColorInProgress, true, Text, ExtraText);

                var progressVertexAnchor = InProgressMeasure as VertexAnchoredVertex;
                var progressLineAnchor = InProgressMeasure as VertexAnchoredLine;
                var otherLine = progressVertexAnchor?.AnchoredVertex?.HostLine ?? progressLineAnchor?.AnchoredLine;
                if(otherLine != null)
                {
                    Vector3D point;
                    if(progressVertexAnchor != null)
                        point = otherLine.GetOther(progressVertexAnchor.AnchoredVertex).GetWorldPosition();
                    else
                        point = progressLineAnchor.AnchoredLine.A.GetWorldPosition();

                    DrawMeasurementAngle(InProgressMeasure.GetWorldPosition(), Aim.WorldPosition, point, ColorInProgress, true, Text);
                }

                if(Text != null && Text.TextStringBuilder.Length > 0)
                {
                    Text.UpdateBackgroundSize(padding: 0.01f);
                    Text.Draw();
                }
            }
            #endregion

            DrawCursor(Aim.WorldPosition, CursorColor);

            #region Triggering
            const int TicksForHold = 30;
            const double MinDistanceSq = 0.01 * 0.01;

            Trigger trigger = Trigger.None;

            if(isPrimaryHeld)
            {
                PrimaryHeld++;
                if(PrimaryHeld == TicksForHold)
                    trigger = Trigger.Hold;
            }
            else
            {
                if(PrimaryHeld > 0)
                {
                    if(PrimaryHeld < TicksForHold)
                        trigger = Trigger.Click;
                    PrimaryHeld = 0;
                }
            }

            bool lengthValid = !isInProgress || Vector3D.DistanceSquared(Aim.WorldPosition, InProgressMeasure.GetWorldPosition()) >= MinDistanceSq;
            bool needsConfirmation = !HasTarget && PrimaryHeld < TicksForHold;

            if(!lengthValid)
                trigger = Trigger.None;

            if(needsConfirmation && trigger == Trigger.Click)
                trigger = Trigger.None;

            if(isPrimaryHeld)
            {
                if(!lengthValid)
                {
                    Notify(0, $"Too small a measurement", 1000, FontsHandler.RedSh);
                }

                if(needsConfirmation)
                {
                    Notify(0, $"No surface selected, hold to place vertex anyway", 1000, FontsHandler.YellowSh);
                }
            }

            if(trigger != Trigger.None)
            {
                PrimaryAction(trigger);
            }
            #endregion
        }

        void PrimaryAction(Trigger trigger)
        {
            IMeasureVertex created;
            string type;

            if(trigger == Trigger.Hold)
            {
                created = new VertexWorld(Aim.WorldPosition, Aim.WorldNormal);
                if(!HasTarget)
                    type = "free-floating";
                else
                    type = "forced without anchor";
            }
            else
            {
                if(Aim.AnchorLine != null)
                {
                    // why even have linked line if it's gonna require static anyway xD
                    // TODO: what about when the linked line stretches...
                    //if(aim.AnchorLine.A.IsStatic && aim.AnchorLine.B.IsStatic)

                    var line = Aim.AnchorLine;
                    Vector3D a = line.A.GetWorldPosition();
                    Vector3D b = line.B.GetWorldPosition();

                    if(MyUtils.GetPointLineDistance(ref a, ref b, ref Aim.WorldPosition) > 0.001)
                    {
                        created = new VertexWorld(Aim.WorldPosition, Aim.WorldNormal);
                        type = "free-floating";
                    }
                    else
                    {
                        Vector3D dirN = Vector3D.Normalize(a - b);
                        float pointAtLength = (float)Vector3D.Dot(a - Aim.WorldPosition, dirN);

                        created = new VertexAnchoredLine(Aim.AnchorLine, pointAtLength);
                        type = "line anchor";
                    }
                }
                else if(Aim.AnchorVertex != null)
                {
                    created = new VertexAnchoredVertex(Aim.AnchorVertex);
                    type = "vertex anchor";
                }
                else if(Aim.AnchorEntity != null)
                {
                    created = new VertexAnchoredEntity(Aim.AnchorEntity, Aim.WorldPosition);
                    type = "entity anchor";
                }
                else
                {
                    created = new VertexWorld(Aim.WorldPosition, Aim.WorldNormal);
                    type = "static";
                }
            }

            if(InProgressMeasure == null)
            {
                InProgressMeasure = created;
                ResetAim();

                Notify(0, $"Started with {type}", 2000, FontsHandler.YellowSh);
            }
            else
            {
                try
                {
                    // TODO: check for line and angle duplicates?

                    // TODO: support for spheres and circles

                    MeasurementLine line = new MeasurementLine(this, InProgressMeasure, created);
                    Measurements.Add(line);

                    AddAngleFrom(InProgressMeasure);
                    AddAngleFrom(created);

                    Notify(0, $"Finalized with {type}", 2000, FontsHandler.GreenSh);
                }
                finally
                {
                    // don't Dispose() because it's given to a line now

                    InProgressMeasure = null;
                    ResetAim();

                    if(Text != null)
                        Text.Visible = false;
                }
            }
        }

        void AddAngleFrom(IMeasureVertex vertex)
        {
            var otherVertex = vertex.HostLine.GetOther(vertex);

            var vertexAnchor = vertex as VertexAnchoredVertex;
            if(vertexAnchor != null)
            {
                var anchoredTo = vertexAnchor.AnchoredVertex;
                var otherLine = anchoredTo?.HostLine;
                if(otherLine != null)
                {
                    Measurements.Add(new MeasurementAngle(this, vertex, otherVertex, otherLine.GetOther(anchoredTo)));
                }
                return;
            }

            var lineAnchor = vertex as VertexAnchoredLine;
            if(lineAnchor != null)
            {
                var otherLine = lineAnchor.AnchoredLine;
                if(otherLine != null)
                {
                    Measurements.Add(new MeasurementAngle(this, vertex, otherVertex, otherLine.A));
                }
                return;
            }
        }

        #region Drawing
        internal void DrawCursor(Vector3D worldPosition, Color color)
        {
            MatrixD camWM = MyAPIGateway.Session.Camera.WorldMatrix;

            const float Size = 0.1f;

            MyTransparentGeometry.AddBillboardOriented(Constants.MatUI_SquareHollow, color, worldPosition, camWM.Left, camWM.Up, Size, -1, blendType: BlendTypeEnum.PostPP);

            Vector3D closePos = worldPosition;
            float ratio = OverlayDrawInstance.ConvertToAlwaysOnTop(ref closePos);
            MyTransparentGeometry.AddBillboardOriented(Constants.MatUI_SquareHollow, color * AlphaSeeThrough, closePos, camWM.Left, camWM.Up, Size * ratio, -1, blendType: BlendTypeEnum.PostPP);
        }

        internal void DrawVertex(Vector3D worldPosition, Color color, float radius = AimableRadius * 0.5f)
        {
            MyTransparentGeometry.AddPointBillboard(Constants.Mat_Dot, color * 1.3f, worldPosition, radius, 0, blendType: BlendTypeEnum.PostPP);

            Vector3D closePos = worldPosition;
            float ratio = OverlayDrawInstance.ConvertToAlwaysOnTop(ref closePos);
            MyTransparentGeometry.AddPointBillboard(Constants.Mat_Dot, color * 1.3f * AlphaSeeThrough, closePos, radius * ratio, 0, blendType: BlendTypeEnum.PostPP);

            //MyTransparentGeometry.AddPointBillboard(Constants.Mat_Dot, color, worldPosition, uint.MaxValue, ref MatrixD.Identity,
            //    AimableRadius * 0.5f, 0, intensity: 5f, blendType: BlendTypeEnum.AdditiveTop);
        }

        internal void DrawMeasurementLine(Vector3D thisPoint, Vector3D otherPoint, Color color, bool isGhost, TextPackage text = null, StringBuilder extraText = null)
        {
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D lookPos = camMatrix.Translation + camMatrix.Forward * 3;

            Vector3D nearPoint = MyUtils.GetClosestPointOnLine(ref thisPoint, ref otherPoint, ref lookPos);

            if(Vector3D.DistanceSquared(nearPoint, lookPos) > MaxDrawDistanceSq)
                return;

            Vector3D lineDirN = (otherPoint - thisPoint);
            double length = lineDirN.Normalize();
            float lengthF = (float)length;

            Vector3D lineCenter = thisPoint + lineDirN * (length * 0.5);

            Color colorSeeThrough = color * AlphaSeeThrough;

            {
                MyTransparentGeometry.AddLineBillboard(Constants.Mat_Laser, color, thisPoint, lineDirN, lengthF,
                    LineThick, blendType: BlendTypeEnum.PostPP);

                Vector3D thisClose = thisPoint;
                float ratio = OverlayDrawInstance.ConvertToAlwaysOnTop(ref thisClose);

                MyTransparentGeometry.AddLineBillboard(Constants.Mat_Laser, colorSeeThrough, thisClose, lineDirN, lengthF * ratio,
                    LineThick * ratio, blendType: BlendTypeEnum.PostPP);

                //MyTransparentGeometry.AddLineBillboard(Constants.Mat_Laser, color, thisPoint, dirNorm, length, LineThick, intensity: 5f, blendType: BlendTypeEnum.AdditiveTop);
            }

            Vector3D dirToCam = Vector3D.Normalize(camMatrix.Translation - lineCenter);
            Vector3D notchDir = Vector3D.Cross(lineDirN, dirToCam);
            notchDir.Normalize();

            float cellSize = RulerSizeMeters;

            const double Padding = 1e-9; // add this to ensure the steps get shown for close enough values

            //float notchLenHalf = cellSize * 0.5f;
            float notchLenHalf = 0.15f;
            float notchLenHalfClose = notchLenHalf * OverlayDrawInstance.DepthRatioF;

            int notches = (int)Math.Floor((length + Padding) / (double)cellSize);
            Vector3D stepPos = thisPoint;
            for(int i = 0; i <= notches; i++)
            {
                MyTransparentGeometry.AddLineBillboard(Constants.Mat_Square, color, stepPos - notchDir * notchLenHalf, notchDir, notchLenHalf * 2,
                    LineStepThick, blendType: BlendTypeEnum.PostPP);

                Vector3D stepClose = stepPos;
                float ratio = OverlayDrawInstance.ConvertToAlwaysOnTop(ref stepClose);

                MyTransparentGeometry.AddLineBillboard(Constants.Mat_Square, colorSeeThrough, stepClose - notchDir * notchLenHalfClose, notchDir, notchLenHalfClose * 2,
                    LineStepThick * ratio, blendType: BlendTypeEnum.PostPP);

                //MyTransparentGeometry.AddLineBillboard(Constants.Mat_LaserGradient, color, stepPos, stepDir, cellSize, LineStepThick, BlendTypeEnum.AdditiveTop); 

                stepPos += lineDirN * cellSize;
            }

            if(text == null)
                return;

            Vector3D textWorldPos;

            if(isGhost)
            {
                textWorldPos = thisPoint + lineDirN * length;
            }
            else
            {
                textWorldPos = nearPoint;

                if(length > 2)
                {
                    double newLen = Vector3D.Distance(textWorldPos, thisPoint);

                    const double CapEnds = 1;
                    newLen = MathHelper.Clamp(newLen, CapEnds, length - CapEnds);

                    textWorldPos = thisPoint + lineDirN * newLen;
                }

                double distanceToForceCenter = length * 0.5;
                double distToCenter = Vector3D.Distance(lineCenter, camMatrix.Translation);
                if(distToCenter > distanceToForceCenter)
                {
                    double start = distToCenter - distanceToForceCenter;
                    double end = length - distanceToForceCenter;
                    double ratio = MathHelper.Clamp(start / end, 0, 1);

                    textWorldPos = Vector3D.Lerp(textWorldPos, lineCenter, ratio);
                }
            }

            BoundingSphereD textVolume = new BoundingSphereD(textWorldPos, 0.5);

            StringBuilder sb = text.TextStringBuilder.Clear();

            if(Vector3D.DistanceSquared(textVolume.Center, MyAPIGateway.Session.Camera.Position) > MaxLabelDistanceSq
            || !MyAPIGateway.Session.Camera.IsInFrustum(ref textVolume))
            {
                //text.Visible = false;
            }
            else
            {
                sb.Color(color).RoundedNumber(lengthF, 4).Append(" m\n");
                if(RulerSizeMode == RulerSize.LargeGrid)
                    sb.RoundedNumber(lengthF / cellSize, 1).Append(" LargeGrid blocks\n");
                else if(RulerSizeMode == RulerSize.SmallGrid)
                    sb.RoundedNumber(lengthF / cellSize, 1).Append(" SmallGrid blocks\n");

                if(extraText != null && extraText.Length > 0)
                    sb.AppendStringBuilder(extraText).Append('\n');

                sb.TrimEndWhitespace();

                Vector3D screenPos = MyAPIGateway.Session.Camera.WorldToScreen(ref textWorldPos);
                text.Position = new Vector2D(screenPos.X + 0.03, screenPos.Y);
                text.Background.BillBoardColor = Constants.Color_UIBackground * Main.GameConfig.HudBackgroundOpacity;

                if(!isGhost) // updating it manually after angle appends its stuff too
                    text.UpdateBackgroundSize(padding: 0.01f);

                //text.Visible = true;
            }
        }

        internal void DrawMeasurementAngle(Vector3D common, Vector3D a, Vector3D b, Color color, bool isGhost, TextPackage text = null)
        {
            if(Vector3D.DistanceSquared(common, MyAPIGateway.Session.Camera.Position) > MaxDrawDistanceSq)
                return;

            Vector3D dirA = (common - a);
            Vector3D dirB = (common - b);
            double lenA = dirA.Normalize();
            double lenB = dirB.Normalize();

            double angleRad = Math.Acos(MathHelper.Clamp(dirA.Dot(dirB), -1.0, 1.0));

            if(angleRad > Math.PI || angleRad <= 0)
                return;


            // use the smaller angle -- disabled because it goes away from lines, needs checking for that...
            //double testAngle = Math.Acos(MathHelper.Clamp(dirA.Dot(-dirB), -1.0, 1.0));
            //if((testAngle + (Math.PI / 1000d)) < angleRad) // offset to avoid fighting
            //{
            //    dirB = -dirB;
            //    angleRad = testAngle;
            //}


            double radius = Math.Min(Math.Min(lenA, lenB), 0.5f);
            Vector3D start = common - dirA * radius;
            Vector3D end = common - dirB * radius;


            Vector3D axis = Vector3D.Cross(dirA, dirB);
            axis.Normalize();

            const double Offset = 0.0001; // offset on axis to avoid z-fighting

            MatrixD circleMatrix = MatrixD.Identity;
            {
                circleMatrix.Translation = common + axis * Offset;

                Vector3D back = dirB;
                Vector3D up = axis;
                Vector3D right = Vector3D.Cross(up, back);
                up = Vector3D.Cross(back, right);

                // assign differently because of DrawAxisLimit()
                circleMatrix.Right = -back;
                circleMatrix.Up = up;
                circleMatrix.Backward = right;
            }

            float angleDeg = (float)MathHelper.ToDegrees(angleRad);
            int drawAngle = (int)angleDeg;

            Color solidColor = color * 0.5f;

            Vector3D rimA, rimB;
            OverlayDrawInstance.DrawAxisLimit(out rimA, out rimB, ref circleMatrix, (float)radius, 0, drawAngle, 15,
                solidColor, color, Constants.Mat_Square, null, LineThick / 2f, BlendTypeEnum.PostPP);

            MatrixD closeCircleMatrix = circleMatrix;
            float ratio = OverlayDrawInstance.ConvertToAlwaysOnTop(ref closeCircleMatrix);
            OverlayDrawInstance.DrawAxisLimit(out rimA, out rimB, ref closeCircleMatrix, (float)radius, 0, drawAngle, 15,
                solidColor * AlphaSeeThrough, color * AlphaSeeThrough, Constants.Mat_Square, null, LineThick / 2f, BlendTypeEnum.PostPP);

            if(text == null)
                return;

            // HACK: append when ghost since we're using just one text object for both line and angle.
            if(isGhost)
            {
                text.TextStringBuilder.Append('\n').Append(angleDeg.ToString("0.#####")).Append(FontsHandler.CharDegree);
            }
            else
            {
                text.TextStringBuilder.Clear();

                Vector3D textWorldPos = common - Vector3D.Lerp(dirA, dirB, 0.5) * radius;
                BoundingSphereD textVolume = new BoundingSphereD(textWorldPos, 0.5);

                if(Vector3D.DistanceSquared(textVolume.Center, MyAPIGateway.Session.Camera.Position) > MaxLabelDistanceSq
                || !MyAPIGateway.Session.Camera.IsInFrustum(ref textVolume))
                {
                    //text.Visible = false;
                }
                else
                {
                    text.TextStringBuilder.Append(angleDeg.ToString("0.#####")).Append(FontsHandler.CharDegree);

                    Vector3D screenPos = MyAPIGateway.Session.Camera.WorldToScreen(ref textWorldPos);
                    text.Position = new Vector2D(screenPos.X + 0.03, screenPos.Y);
                    text.Background.BillBoardColor = Constants.Color_UIBackground * Main.GameConfig.HudBackgroundOpacity;
                    text.UpdateBackgroundSize(padding: 0.01f);
                    //text.Visible = true;
                }
            }
        }
        #endregion

        internal void Notify(int channel, string message, int ms = 2000, string font = FontsHandler.WhiteSh)
        {
            if(channel < 0 || channel >= Notifications.Length)
                throw new Exception($"channel is <0 or >={Notifications.Length}");

            var notify = Notifications[channel];

            if(notify == null)
            {
                notify = MyAPIGateway.Utilities.CreateNotification(string.Empty);
                Notifications[channel] = notify;
            }

            notify.Hide(); // required to update visible messages
            notify.AliveTime = ms;
            notify.Font = font;
            notify.Text = message;
            notify.Show();
        }
    }
}
