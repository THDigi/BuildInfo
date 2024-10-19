using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.Overlays.Specialized;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features.Overlays
{
    public class OverlayDrawInstance
    {
        public readonly string DebugName;
        public readonly LabelRendering LabelRender;

        public List<MyTuple<MatrixD, int>> DrawBuildStageMounts;

        internal float CellSize;
        internal float CellSizeHalf;
        internal bool BlockFunctionalForPressure;

        internal readonly LiveDataHandler.Cache BDataCache = new LiveDataHandler.Cache();

        MyCubeBlockDefinition LastDef;
        SpecializedOverlayBase SpecializedOverlay;

        readonly BuildInfoMod Main;
        readonly Overlays Overlays;
        readonly Config.Config Config;

        internal static BoundingBoxD UnitBB = new BoundingBoxD(-Vector3D.Half, Vector3D.Half);
        internal static readonly Base6Directions.Direction[] CycledDirections = new Base6Directions.Direction[] // NOTE: order is important, corresponds to +X, -X, +Y, -Y, +Z, -Z
        {
            Base6Directions.Direction.Right,
            Base6Directions.Direction.Left,
            Base6Directions.Direction.Up,
            Base6Directions.Direction.Down,
            Base6Directions.Direction.Backward,
            Base6Directions.Direction.Forward,
        };

        public const double DepthRatio = 0.01; // for see-through walls
        public const float DepthRatioF = 0.01f;

        // affects all overlays including specialized ones
        public static readonly MyStringId MaterialSquare = Constants.Mat_Square;
        public static readonly MyStringId MaterialLaser = Constants.Mat_Laser;
        public static readonly MyStringId MaterialDot = Constants.Mat_Dot;
        public static readonly MyStringId MaterialGradient = Constants.Mat_Gradient;
        public static readonly MyStringId MaterialGradientSRGB = Constants.Mat_GradientSRGB;

        public const BlendTypeEnum MountpointBlendType = BlendTypeEnum.SDR;
        public const BlendTypeEnum MountpointAimedBlendType = BlendTypeEnum.SDR;
        public const double MountpointThickness = 0.05; // per meter
        public const float MountpointAlpha = 0.8f;
        public static Color MountpointColorNormal = new Color(255, 255, 0) * MountpointAlpha;
        public static Color MountpointColorMasked = new Color(255, 100, 55) * MountpointAlpha;
        public static Color MountpointColorCoupling = new Color(255, 0, 55) * MountpointAlpha;
        public static Color MountpointColorGrayed = new Color(70, 15, 15) * MountpointAlpha;
        public static Color MountpointColorAutoRotate = new Color(0, 55, 255) * MountpointAlpha;
        public static Color MountpointAimedColor = Color.White;

        public const float AirtightAlpha = 0.8f;
        public static Color AirtightColor = new Color(0, 155, 255) * AirtightAlpha;
        public static Color AirtightUnavailableColor = Color.Gray * AirtightAlpha;

        public static float SideBrightnessChange = 0.1f; // some sides are darkened or brightened by this amount to give it perspective

        public static readonly Color ConveyorPortColor = new Color(255, 255, 0);
        public static readonly Color InteractiveConveyorPortColor = new Color(155, 255, 0);
        public static readonly Color InteractiveTerminalColor = new Color(55, 255, 220);
        public static readonly Color InteractiveActionOrTerminalColor = new Color(50, 255, 150);
        public static readonly Color InteractiveColor = new Color(25, 100, 155);
        public static readonly Color UpgradePortColor = new Color(200, 55, 255);
        public static readonly Color UnknownPortColor = new Color(0, 0, 200);

        public const BlendTypeEnum PortBlendType = BlendTypeEnum.SDR;
        public const BlendTypeEnum PortAimedBlendType = BlendTypeEnum.SDR;

        public OverlayDrawInstance(Overlays overlays, string debugName)
        {
            DebugName = debugName;
            LabelRender = new LabelRendering(overlays.Main);

            Overlays = overlays;
            Main = overlays.Main;
            Config = Main.Config;
        }

        public void Draw(MyCubeBlockDefinition def, IMySlimBlock block = null, Overlays.ModeEnum? modeOverride = null)
        {
            try
            {
                if(def == null)
                    throw new Exception("blockDef must not be null!");

                #region block changed
                if(LastDef != def)
                {
                    LastDef = def;

                    CellSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    CellSizeHalf = CellSize / 2;

                    SpecializedOverlay = Main.SpecializedOverlays.Get(def.Id.TypeId);
                }
                #endregion

                Overlays.ModeEnum mode = modeOverride ?? Overlays.OverlayMode;
                MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

                MatrixD drawMatrix;
                if(block != null)
                {
                    drawMatrix = Utils.GetBlockCenteredWorldMatrix(block);
                }
                else // assuming cubebuilder
                {
                    if(!Utils.GetEquippedCenteredMatrix(out drawMatrix))
                        return;
                }

                bool isCamController = (block?.FatBlock != null && MyAPIGateway.Session.CameraController == block.FatBlock);

                #region draw ModelOffset indicator
                if(Config.InternalInfo.Value && def.ModelOffset.LengthSquared() > 0)
                {
                    Color color = new Color(255, 0, 255);

                    MatrixD closeMatrix = drawMatrix;
                    float depthMul = ConvertToAlwaysOnTop(ref closeMatrix);

                    float OffsetLineThickness = 0.005f * depthMul;
                    float OffsetPointThickness = 0.05f * depthMul;

                    Vector3D start = closeMatrix.Translation;
                    Vector3 dir = Vector3.TransformNormal(def.ModelOffset, closeMatrix);

                    Vector3D offset = (camMatrix.Right * LabelRendering.ShadowOffset.X + camMatrix.Up * LabelRendering.ShadowOffset.Y) * depthMul;

                    MyTransparentGeometry.AddLineBillboard(MaterialSquare, LabelRendering.ShadowColor, start + offset, dir, 1f, OffsetLineThickness, LabelRendering.ShadowBlendType);
                    MyTransparentGeometry.AddLineBillboard(MaterialSquare, color, start, dir, 1f, OffsetLineThickness, blendType: LabelRendering.TextBlendType);

                    MyTransparentGeometry.AddPointBillboard(MaterialDot, LabelRendering.ShadowColor, start + dir + offset, OffsetPointThickness, 0, blendType: LabelRendering.ShadowBlendType);
                    MyTransparentGeometry.AddPointBillboard(MaterialDot, color, start + dir, OffsetPointThickness, 0, blendType: LabelRendering.TextBlendType);

                    if(LabelRender.CanDrawLabel())
                    {
                        dir = Vector3.TransformNormal(def.ModelOffset, drawMatrix);
                        LabelRender.DrawLineLabel(LabelType.ModelOffset, drawMatrix.Translation + dir, dir, color, "Center", 0);
                    }
                }
                #endregion

                #region draw orientation indicators
                if(!isCamController)
                {
                    float textScale = 1f;

                    MatrixD matrix = MatrixD.CreateScale(def.Size * (CellSize / textScale / 2f) + new Vector3D(0.5f));
                    matrix.Translation = Vector3D.Zero; // (def.Center - (def.Size * 0.5f));
                    matrix = matrix * drawMatrix;

                    bool alwaysOnTop = true; // (mode == Overlays.ModeEnum.Ports);

                    if(Main.TextAPI.IsEnabled && mode != Overlays.ModeEnum.AirtightAndSpecialized)
                    {
                        LabelRender.DrawLineLabel(LabelType.AxisX, matrix.Translation, matrix.Right, Color.Red, cacheMessage: "Right",
                            lineHeight: 1f, scale: textScale, settingFlag: OverlayLabelsFlags.Axis, autoAlign: true, alwaysOnTop: alwaysOnTop);

                        LabelRender.DrawLineLabel(LabelType.AxisY, matrix.Translation, matrix.Up, Color.Lime, cacheMessage: "Up",
                            lineHeight: 1f, scale: textScale, settingFlag: OverlayLabelsFlags.Axis, autoAlign: true, alwaysOnTop: alwaysOnTop);

                        LabelRender.DrawLineLabel(LabelType.AxisZ, matrix.Translation, matrix.Forward, Color.Blue, cacheMessage: "Forward",
                            lineHeight: 1f, scale: textScale, settingFlag: OverlayLabelsFlags.Axis, autoAlign: true, alwaysOnTop: alwaysOnTop);
                    }
                    else
                    {
                        LabelRender.DrawLine(matrix.Translation, matrix.Right, Color.Red,
                            lineHeight: 1f, scale: textScale, autoAlign: true, alwaysOnTop: alwaysOnTop);

                        LabelRender.DrawLine(matrix.Translation, matrix.Up, Color.Lime,
                            lineHeight: 1f, scale: textScale, autoAlign: true, alwaysOnTop: alwaysOnTop);

                        LabelRender.DrawLine(matrix.Translation, matrix.Forward, Color.Blue,
                            lineHeight: 1f, scale: textScale, autoAlign: true, alwaysOnTop: alwaysOnTop);

                        // re-assigning mount points temporarily to prevent the original mountpoint wireframe from being drawn while keeping the axis text
                        //MyCubeBlockDefinition.MountPoint[] mp = def.MountPoints;
                        //def.MountPoints = BLANK_MOUNTPOINTS;
                        //MyCubeBuilder.DrawMountPoints(CellSize, def, ref drawMatrix);
                        //def.MountPoints = mp;
                        //
                        //private readonly MyCubeBlockDefinition.MountPoint[] BLANK_MOUNTPOINTS = new MyCubeBlockDefinition.MountPoint[0];
                    }
                }
                #endregion

                #region draw airtightness & mountpoints
                if(mode == Overlays.ModeEnum.AirtightAndSpecialized || mode == Overlays.ModeEnum.MountPoints)
                {
                    Vector3I center = def.Center;
                    MatrixD mainMatrix = MatrixD.CreateTranslation((center - (def.Size * 0.5f)) * CellSize) * drawMatrix;
                    MyCubeBlockDefinition.MountPoint[] mountPoints = def.GetBuildProgressModelMountPoints(1f);
                    bool drawLabel = LabelRender.CanDrawLabel();

                    if(mode == Overlays.ModeEnum.AirtightAndSpecialized)
                    {
                        Color color = AirtightColor;

                        BlockFunctionalForPressure = true;

                        if(Main.EquipmentMonitor.AimedProjectedBy == null && block != null && def.BuildProgressModels != null && def.BuildProgressModels.Length > 0)
                        {
                            // HACK: condition matching the condition in MyGridGasSystem.IsAirtightFromDefinition()
                            MyCubeBlockDefinition.BuildProgressModel progressModel = def.BuildProgressModels[def.BuildProgressModels.Length - 1];
                            if(block.BuildLevelRatio < progressModel.BuildRatioUpperBound)
                                BlockFunctionalForPressure = false;
                        }

                        if(!BlockFunctionalForPressure)
                        {
                            //if(drawLabel)
                            //{
                            //    DynamicLabelSB.Clear().Append("Unfinished blocks are never airtight");
                            //
                            //    Vector3D labelPos = drawMatrix.Translation;
                            //    Vector3D labelDir = drawMatrix.Up;
                            //
                            //    DrawLineLabel(TextAPIMsgIds.DynamicLabel, labelPos, labelDir, Color.OrangeRed, lineHeight: 0f, lineThick: 0f, align: HudAPIv2.TextOrientation.center, autoAlign: false, alwaysOnTop: true);
                            //}

                            color = AirtightUnavailableColor;
                        }

                        if(def.IsAirTight.HasValue)
                        {
                            if(def.IsAirTight.Value)
                            {
                                Vector3 halfExtents = def.Size * CellSizeHalf;
                                BoundingBoxD localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MountpointThickness * CellSize * 0.5);
                                //MySimpleObjectDraw.DrawTransparentBox(ref drawMatrix, ref localBB, ref color, MySimpleObjectRasterizer.Solid, 1, lineWidth: 0.01f, lineMaterial: MaterialSquare, faceMaterial: MaterialSquare, blendType: MountpointBlendType);

                                MyStringId faceMaterial = MaterialSquare;
                                BlendTypeEnum blendType = MountpointBlendType;

                                Color colorZ = color;
                                Color colorX = Color.Darken(color, SideBrightnessChange);
                                Color colorY = Color.Lighten(color, SideBrightnessChange);

                                MatrixD objMatrix = drawMatrix.GetOrientation();
                                Vector3D value = drawMatrix.Translation + Vector3D.Transform(localBB.Center, objMatrix);
                                float width = (float)(localBB.Max.X - localBB.Min.X) / 2f;
                                float height = (float)(localBB.Max.Y - localBB.Min.Y) / 2f;
                                float depth = (float)(localBB.Max.Z - localBB.Min.Z) / 2f;

                                MyQuadD quad;

                                Vector3D offset = objMatrix.Forward * depth;
                                Vector3D position = value + offset;
                                MyUtils.GenerateQuad(out quad, ref position, width, height, ref drawMatrix);
                                MyTransparentGeometry.AddQuad(faceMaterial, ref quad, colorZ, ref position, blendType: blendType);

                                position = value - offset;
                                MyUtils.GenerateQuad(out quad, ref position, width, height, ref drawMatrix);
                                MyTransparentGeometry.AddQuad(faceMaterial, ref quad, colorZ, ref position, blendType: blendType);

                                MatrixD matrix = MatrixD.CreateRotationY(MathHelper.ToRadians(90f)) * drawMatrix;
                                offset = drawMatrix.Left * width;
                                position = value + offset;
                                MyUtils.GenerateQuad(out quad, ref position, depth, height, ref matrix);
                                MyTransparentGeometry.AddQuad(faceMaterial, ref quad, colorY, ref position, blendType: blendType);

                                position = value - offset;
                                MyUtils.GenerateQuad(out quad, ref position, depth, height, ref matrix);
                                MyTransparentGeometry.AddQuad(faceMaterial, ref quad, colorY, ref position, blendType: blendType);

                                matrix = Matrix.CreateRotationX(MathHelper.ToRadians(90f)) * drawMatrix;
                                offset = drawMatrix.Up * height;
                                position = value + offset;
                                MyUtils.GenerateQuad(out quad, ref position, width, depth, ref matrix);
                                MyTransparentGeometry.AddQuad(faceMaterial, ref quad, colorX, ref position, blendType: blendType);

                                position = value - offset;
                                MyUtils.GenerateQuad(out quad, ref position, width, depth, ref matrix);
                                MyTransparentGeometry.AddQuad(faceMaterial, ref quad, colorX, ref position, blendType: blendType);
                            }
                        }
                        else if(mountPoints != null)
                        {
                            Vector3D half = Vector3D.One * -CellSizeHalf;
                            Vector3D corner = (Vector3D)def.Size * -CellSizeHalf;
                            MatrixD transformMatrix = MatrixD.CreateTranslation(corner - half) * drawMatrix;

                            foreach(KeyValuePair<Vector3I, Dictionary<Vector3I, MyCubeBlockDefinition.MyCubePressurizationMark>> kv in def.IsCubePressurized) // precomputed: [position][normal] = airtight type
                            {
                                foreach(KeyValuePair<Vector3I, MyCubeBlockDefinition.MyCubePressurizationMark> kv2 in kv.Value)
                                {
                                    if(kv2.Value != MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedAlways) // pos+normal not always airtight
                                        continue;

                                    Vector3D pos = Vector3D.Transform((Vector3D)(kv.Key * CellSize), transformMatrix);
                                    Vector3 dirForward = Vector3.TransformNormal(kv2.Key, drawMatrix);
                                    int dirIndex = (int)Base6Directions.GetDirection(kv2.Key);
                                    Base6Directions.Direction dirUpEnum = CycledDirections[((dirIndex + 2) % 6)];
                                    Vector3 dirUp = (Vector3)drawMatrix.GetDirectionVector(dirUpEnum);

                                    MatrixD m = MatrixD.Identity;
                                    m.Translation = pos + dirForward * CellSizeHalf;
                                    m.Forward = dirForward;
                                    m.Backward = -dirForward;
                                    m.Left = Vector3D.Cross(dirForward, dirUp);
                                    m.Right = -m.Left;
                                    m.Up = dirUp;
                                    m.Down = -dirUp;
                                    Vector3D scale = new Vector3D(CellSize, CellSize, MountpointThickness * CellSize);
                                    MatrixD.Rescale(ref m, ref scale);

                                    Color finalColor = color;
                                    Base6Directions.Axis axis = Base6Directions.GetAxis(Base6Directions.GetDirection(kv2.Key));
                                    if(axis == Base6Directions.Axis.LeftRight)
                                        finalColor = Color.Lighten(color, SideBrightnessChange);
                                    else if(axis == Base6Directions.Axis.UpDown)
                                        finalColor = Color.Darken(color, SideBrightnessChange);

                                    MySimpleObjectDraw.DrawTransparentBox(ref m, ref UnitBB, ref finalColor, ref color, MySimpleObjectRasterizer.Solid, 1, lineWidth: 0.01f, lineMaterial: MaterialSquare, faceMaterial: MaterialSquare, onlyFrontFaces: true, blendType: MountpointBlendType);

                                    // TODO: use see-through for airtightness and mountpoints?
                                    #region See-through-wall version
                                    //var closeMatrix = m;
                                    //float depthScale = ConvertToAlwaysOnTop(ref closeMatrix);
                                    ////lineWdith *= depthScale;

                                    //Color colorSeeThrough = finalColor * SEETHROUGH_COLOR_MUL;

                                    //MySimpleObjectDraw.DrawTransparentBox(ref closeMatrix, ref unitBB, ref colorSeeThrough, ref colorSeeThrough, MySimpleObjectRasterizer.Solid, 1, lineWidth: 0.01f, lineMaterial: OVERLAY_SQUARE_MATERIAL, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                                    #endregion
                                }
                            }
                        }
                    }
                    else if(mode == Overlays.ModeEnum.MountPoints && mountPoints != null)
                    {
                        ClosestMount = null;
                        ClosestMountDist = double.MaxValue;
                        ClosestMountOBB = default(MyOrientedBoundingBoxD);
                        ClosestMountMatrix = default(MatrixD);
                        ClosestMountGrayed = false;
                        ClosestMountAutoRotate = false;

                        if(DrawBuildStageMounts != null && DrawBuildStageMounts.Count > 0)
                        {
                            DrawMountpoints(def, mountPoints, drawMatrix);

                            foreach(MyTuple<MatrixD, int> tuple in DrawBuildStageMounts)
                            {
                                MyCubeBlockDefinition.BuildProgressModel bpm = def.BuildProgressModels[tuple.Item2];
                                DrawMountpoints(def, def.GetBuildProgressModelMountPoints(bpm.BuildRatioUpperBound), tuple.Item1);
                            }
                        }
                        else
                        {
                            if(block != null || MyAPIGateway.Session.SurvivalMode) // creative tools still prevents this from being placed
                            {
                                // mountpoints at 100% built are grayed out
                                DrawMountpoints(def, mountPoints, drawMatrix, grayedOut: true);

                                // mountpoints at 0% built or at current level
                                mountPoints = def.GetBuildProgressModelMountPoints(block != null ? block.BuildLevelRatio : MyComponentStack.MOUNT_THRESHOLD);
                                DrawMountpoints(def, mountPoints, drawMatrix);
                            }
                            else
                            {
                                // mountpoints at 100% built
                                DrawMountpoints(def, mountPoints, drawMatrix);
                            }
                        }

                        if(ClosestMount.HasValue)
                        {
                            const float textScale = 1.5f;

                            Vector3D labelPos = ClosestMountOBB.Center;
                            Vector3D labelDir = Vector3D.Normalize(camMatrix.Up + camMatrix.Right * 0.5);
                            MyCubeBlockDefinition.MountPoint mountPoint = ClosestMount.Value;

                            // selection wire box over the mountpoint
                            MatrixD.Rescale(ref ClosestMountMatrix, 1.01);
                            float depthScale = ConvertToAlwaysOnTop(ref ClosestMountMatrix);
                            float lineWdith = 0.01f * depthScale;

                            Color colorFace = Color.White * 0.25f;
                            MySimpleObjectDraw.DrawTransparentBox(ref ClosestMountMatrix, ref UnitBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MaterialSquare, blendType: MountpointAimedBlendType);

                            MySimpleObjectDraw.DrawTransparentBox(ref ClosestMountMatrix, ref UnitBB, ref MountpointAimedColor, MySimpleObjectRasterizer.Wireframe, 1, lineWidth: lineWdith, lineMaterial: MaterialLaser, blendType: MountpointAimedBlendType);

                            bool hasCouplingTag = !string.IsNullOrEmpty(mountPoint.CouplingTag);

                            StringBuilder dynamicLabel = LabelRender.DynamicLabel;
                            dynamicLabel.Clear();
                            if(mountPoint.PropertiesMask != 0 || mountPoint.ExclusionMask != 0 || hasCouplingTag)
                                dynamicLabel.Append("Limited mount point");
                            else
                                dynamicLabel.Append("Standard mount point");

                            dynamicLabel.Color(MountpointColorMasked);

                            if(mountPoint.PropertiesMask != 0)
                            {
                                dynamicLabel.Append("\nProperties: ");

                                for(int i = 0; i < Hardcoded.MountPointMaskNames.Length; i++)
                                {
                                    if((mountPoint.PropertiesMask & Hardcoded.MountPointMaskValues[i]) != 0)
                                    {
                                        dynamicLabel.Append(Hardcoded.MountPointMaskNames[i]).Append(", ");
                                    }
                                }

                                dynamicLabel.Length -= 2; // remove last comma and space
                            }

                            if(mountPoint.ExclusionMask != 0)
                            {
                                dynamicLabel.Append("\nExcludes: ");

                                for(int i = 0; i < Hardcoded.MountPointMaskNames.Length; i++)
                                {
                                    if((mountPoint.ExclusionMask & Hardcoded.MountPointMaskValues[i]) != 0)
                                    {
                                        dynamicLabel.Append(Hardcoded.MountPointMaskNames[i]).Append(", ");
                                    }
                                }

                                dynamicLabel.Length -= 2; // remove last comma and space
                            }

                            dynamicLabel.ResetFormatting();

                            // from MyCubeGrid.CheckCouplingTags()
                            if(hasCouplingTag)
                            {
                                dynamicLabel.Color(MountpointColorCoupling).Append("\nCoupling tag: ").Append(mountPoint.CouplingTag);

                                if(!mountPoint.AllowCouplingWithItself)
                                    dynamicLabel.Append("\nCannot mount to itself");

                                dynamicLabel.ResetFormatting();
                            }

                            if(ClosestMountAutoRotate)
                                dynamicLabel.Color(MountpointColorAutoRotate).Append("\nUsed by auto-rotate").ResetFormatting();

                            if(ClosestMountGrayed)
                                dynamicLabel.Color(MountpointColorGrayed).Append("\nOnly when fully built").ResetFormatting();

                            LabelRender.DrawLineLabel(LabelType.DynamicLabel, labelPos, labelDir, Color.White, scale: textScale, autoAlign: false, alwaysOnTop: true);
                        }
                    }
                }
                #endregion

                #region draw ports mode
                if(mode == Overlays.ModeEnum.Ports)
                {
                    BData_Base data = Main.LiveDataHandler.Get<BData_Base>(def, BDataCache);
                    if(data != null)
                    {
                        MatrixD blockWorldMatrix = drawMatrix;
                        blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);

                        if(data.ConveyorPorts != null)
                        {
                            foreach(ConveyorInfo info in data.ConveyorPorts)
                            {
                                MatrixD matrix = info.LocalMatrix * blockWorldMatrix;
                                bool isSmall = (info.Flags & ConveyorFlags.Small) != 0;

                                if((info.Flags & ConveyorFlags.Interactive) != 0)
                                {
                                    if(isSmall)
                                        DrawPort("        Interactive\nSmall conveyor port", matrix, InteractiveConveyorPortColor);
                                    else
                                        DrawPort("        Interactive\nLarge conveyor port", matrix, InteractiveConveyorPortColor, largePort: true);
                                }
                                else
                                {
                                    if(isSmall)
                                        DrawPort("       Small\nConveyor port", matrix, ConveyorPortColor);
                                    else
                                        DrawPort("       Large\nConveyor port", matrix, ConveyorPortColor, largePort: true);
                                }
                            }
                        }

                        if(data.Interactive != null)
                        {
                            foreach(InteractionInfo info in data.Interactive)
                            {
                                MatrixD matrix = info.LocalMatrix * blockWorldMatrix;
                                DrawPort(info.Name, matrix, info.Color);
                            }
                        }

                        if(data.UpgradePorts != null)
                        {
                            bool hasUpgrades = (data.Upgrades != null && data.Upgrades.Count > 0);

                            foreach(UpgradePortInfo portInfo in data.UpgradePorts)
                            {
                                MatrixD matrix = portInfo.LocalMatrix * blockWorldMatrix;

                                if(hasUpgrades)
                                    DrawPort("Upgrade port", matrix, UpgradePortColor);
                                else
                                    DrawPort(null, matrix, UnknownPortColor); // special treatment message
                            }
                        }

                        //if(!MyAPIGateway.Input.IsAnyShiftKeyPressed())
                        //{
                        //    if(data.Dummies != null)
                        //    {
                        //        foreach(var kv in data.Dummies)
                        //        {
                        //            var matrix = kv.Item2 * blockWorldMatrix;
                        //            DrawPort(kv.Item1, matrix, Color.Red);
                        //        }
                        //    }
                        //}

                        // NOTE: not using classic labels (one per color type) because some ports are small, others large...
                        if(AimedPorts.Count > 0)
                        {
                            const float textScale = 1.5f;

                            PortInfo? closestPort = null;
                            double closestDistance = double.MaxValue;

                            for(int i = 0; i < AimedPorts.Count; i++)
                            {
                                PortInfo portInfo = AimedPorts[i];
                                if(closestDistance > portInfo.Distance)
                                {
                                    closestDistance = portInfo.Distance;
                                    closestPort = portInfo;
                                }
                            }

                            if(closestPort.HasValue)
                            {
                                // overlayed selection box
                                Color colorFace = Color.White * 0.25f;
                                //Color colorLine = Color.White;
                                //float lineWidth = 0.01f * (float)DepthRatio;

                                MatrixD closeRenderMatrix = closestPort.Value.CloseMatrix;

                                MySimpleObjectDraw.DrawTransparentBox(ref closeRenderMatrix, ref UnitBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MaterialSquare, blendType: PortBlendType);

                                //MySimpleObjectDraw.DrawTransparentBox(ref closeRenderMatrix, ref unitBB, ref colorLine, MySimpleObjectRasterizer.Wireframe, 1, lineWidth, lineMaterial: MaterialLaser, blendType: PortBlendType);

                                // label text
                                MatrixD cm = MyAPIGateway.Session.Camera.WorldMatrix;
                                Vector3D labelPos = closestPort.Value.Matrix.Translation;
                                Vector3D labelDir = Vector3D.Normalize(cm.Up + cm.Right * 0.5);

                                if(closestPort.Value.Message == null)
                                    LabelRender.DynamicLabel.Clear().Append("Unknown port");
                                else
                                    LabelRender.DynamicLabel.Clear().Append(closestPort.Value.Message);

                                LabelRender.DrawLineLabel(LabelType.DynamicLabel, labelPos, labelDir, Color.White, scale: textScale, autoAlign: false, alwaysOnTop: true);
                            }

                            AimedPorts.Clear();
                        }
                    }
                }
                #endregion

                if(mode == Overlays.ModeEnum.AirtightAndSpecialized)
                {
                    SpecializedOverlay?.Draw(ref drawMatrix, this, def, block);
                }

                //NewFeatureTestingDraw(def, block, drawMatrix);
            }
            catch(Exception e)
            {
                Log.Error($"Overlay draw error for {DebugName}; block={block?.BlockDefinition?.Id.ToString()}; def={def?.Id.ToString()}; error={e.Message}\n{e.StackTrace}");
            }
        }

        #region Draw ports
        readonly List<PortInfo> AimedPorts = new List<PortInfo>();

        void DrawPort(string message, MatrixD portMatrix, Color color, bool largePort = false)
        {
            const float LineIntensity = 10f;

            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            float lineWidth = 0.01f;

            MatrixD closeRenderMatrix = portMatrix;

            // see through walls
            float scale = ConvertToAlwaysOnTop(ref closeRenderMatrix);
            lineWidth *= scale;

            Color colorFace = color * 0.3f;
            Color colorLine = color;

            MySimpleObjectDraw.DrawTransparentBox(ref closeRenderMatrix, ref UnitBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1,
                faceMaterial: MaterialSquare, blendType: PortAimedBlendType);

            MySimpleObjectDraw.DrawTransparentBox(ref closeRenderMatrix, ref UnitBB, ref colorLine, MySimpleObjectRasterizer.Wireframe, 1, lineWidth,
                lineMaterial: MaterialLaser, blendType: PortAimedBlendType, intensity: LineIntensity);

            // TODO: some kind of large conveyor indicator?
            //if(largePort)
            //{
            //    var middleMatrix = closeRenderMatrix;
            //    var originalScale = middleMatrix.Scale;
            //    var scaleVec = Vector3D.One;
            //
            //    if(originalScale.Z < originalScale.X && originalScale.Z < originalScale.Y)
            //        scaleVec.Y = 0.05; // Z is thin, pick either Y or X
            //    else if(originalScale.X < originalScale.Y && originalScale.X < originalScale.Z)
            //        scaleVec.Z = 0.05; // X is thin, pick either Y or Z
            //    else if(originalScale.Y < originalScale.X && originalScale.Y < originalScale.Z)
            //        scaleVec.X = 0.05; // Y is thin, pick either X or Z
            //
            //    MatrixD.Rescale(ref middleMatrix, ref scaleVec);
            //    MySimpleObjectDraw.DrawTransparentBox(ref middleMatrix, ref unitBB, ref colorLine, MySimpleObjectRasterizer.Wireframe, 1, lineWidth, lineMaterial: OVERLAY_LASER_MATERIAL, blendType: OVERLAY_BLEND_TYPE);
            //}

            if(Main.TextAPI.IsEnabled)
            {
                MyOrientedBoundingBoxD obb = new MyOrientedBoundingBoxD(portMatrix);
                RayD aimLine = new RayD(camMatrix.Translation, camMatrix.Forward);
                double? distance = obb.Intersects(ref aimLine);
                if(distance.HasValue)
                {
                    AimedPorts.Add(new PortInfo((float)distance.Value, portMatrix, closeRenderMatrix, color, message));
                }
            }
        }

        struct PortInfo
        {
            public readonly float Distance;
            public readonly MatrixD Matrix;
            public readonly MatrixD CloseMatrix;
            public readonly Color Color;
            public readonly string Message;

            public PortInfo(float distance, MatrixD matrix, MatrixD closeMatrix, Color color, string message)
            {
                Distance = distance;
                Matrix = matrix;
                CloseMatrix = closeMatrix;
                Color = color;
                Message = message;
            }
        }
        #endregion

        #region draw helpers
        public static float ConvertToAlwaysOnTop(ref MatrixD matrix)
        {
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D posOverlay = camMatrix.Translation + ((matrix.Translation - camMatrix.Translation) * DepthRatio);

            MatrixD.Rescale(ref matrix, DepthRatio);
            matrix.Translation = posOverlay;
            return DepthRatioF;
        }

        public static float ConvertToAlwaysOnTop(ref Vector3D vector)
        {
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            vector = camMatrix.Translation + ((vector - camMatrix.Translation) * DepthRatio);
            return DepthRatioF;
        }

        public void DrawTurretLimits(ref MatrixD drawMatrix, ref MatrixD pitchMatrix, TurretInfo turretInfo,
            float radius, int minPitch, int maxPitch, int minYaw, int maxYaw, bool canDrawLabel)
        {
            Color ColorPitch = (Color.Red * SpecializedOverlayBase.SolidOverlayAlpha).ToVector4();
            Vector4 ColorPitchLine = Color.Red.ToVector4();
            Color ColorYaw = (Color.Lime * SpecializedOverlayBase.SolidOverlayAlpha).ToVector4();
            Vector4 ColorYawLine = Color.Lime.ToVector4();
            const int LimitsLineEveryDegrees = SpecializedOverlayBase.RoundedQualityHigh;
            const float LimitsLineThick = 0.03f;

            // pitch limit indicator
            {
                // only yaw rotation
                MatrixD m = MatrixD.CreateWorld(drawMatrix.Translation, Vector3D.Cross(pitchMatrix.Left, drawMatrix.Up), drawMatrix.Up);
                Vector3D rotationPivot = Vector3D.Transform(turretInfo.PitchLocalPos, m);

                // only yaw rotation but for cylinder
                MatrixD yawOnlyMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Down, pitchMatrix.Left);

                Vector3D firstOuterRimVec, lastOuterRimVec;
                DrawAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                    ref yawOnlyMatrix, radius, minPitch, maxPitch, LimitsLineEveryDegrees,
                    ColorPitch, ColorPitchLine, MaterialSquare, MaterialLaser, LimitsLineThick, SpecializedOverlayBase.BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelDir = Vector3D.Normalize(lastOuterRimVec - pitchMatrix.Translation);
                    LabelRender.DrawLineLabel(LabelType.PitchLimit, lastOuterRimVec, labelDir, ColorPitchLine, "Pitch limit", scale: 0.75f, alwaysOnTop: true);
                }
            }

            // yaw limit indicator
            {
                Vector3D rotationPivot = Vector3D.Transform(turretInfo.YawModelCenter, drawMatrix);

                MatrixD yawMatrix = MatrixD.CreateWorld(rotationPivot, drawMatrix.Right, drawMatrix.Down);

                Vector3D firstOuterRimVec, lastOuterRimVec;
                DrawAxisLimit(out firstOuterRimVec, out lastOuterRimVec,
                    ref yawMatrix, radius, minYaw, maxYaw, LimitsLineEveryDegrees,
                    ColorYaw, ColorYawLine, MaterialSquare, MaterialLaser, LimitsLineThick, SpecializedOverlayBase.BlendType);

                if(canDrawLabel)
                {
                    Vector3D labelDir = Vector3D.Normalize(firstOuterRimVec - yawMatrix.Translation);
                    LabelRender.DrawLineLabel(LabelType.YawLimit, firstOuterRimVec, labelDir, ColorYawLine, "Yaw limit", scale: 0.75f, alwaysOnTop: true);
                }
            }
        }

        public void DrawAxisLimit(out Vector3D firstOuterRimVec, out Vector3D lastOuterRimVec,
            ref MatrixD worldMatrix, float radius, int startAngle, int endAngle, int lineEveryDegrees,
            Vector4 faceColor, Vector4 lineColor, MyStringId? faceMaterial = null, MyStringId? lineMaterial = null, float lineThickness = 0.01f,
            BlendTypeEnum blendType = BlendTypeEnum.Standard)
        {
            int wireDivRatio = (360 / lineEveryDegrees); // quality

            Vector3D center = worldMatrix.Translation;
            Vector3D normal = worldMatrix.Forward;
            firstOuterRimVec = center + normal * radius; // fallback
            lastOuterRimVec = firstOuterRimVec;

            Vector4 triangleColor = (faceMaterial.HasValue ? faceColor.ToLinearRGB() : faceColor); // HACK: keeping color consistent with other billboards, MyTransparentGeoemtry.CreateBillboard()

            // from MyLargeTurretBase

            //static float NormalizeAngle(int angle)
            //{
            //    int n = angle % 360;
            //    if(n == 0 && angle != 0)
            //        return 360f;
            //    return n;
            //}
            // inlined of above
            int startN = startAngle % 360;
            startAngle = ((startN == 0 && startAngle != 0) ? 360 : startN);

            int endN = endAngle % 360;
            endAngle = ((endN == 0 && endAngle != 0) ? 360 : endN);

            double startRad = MathHelperD.ToRadians(startAngle);
            double endRad = MathHelperD.ToRadians(endAngle);
            if(startRad > endRad)
                startRad -= MathHelperD.TwoPi;

            Vector3D current = Vector3D.Zero;
            Vector3D previous = Vector3D.Zero;
            double angleRad = startRad;

            double stepRad = MathHelperD.TwoPi / wireDivRatio;
            bool first = true;

            Vector2 uv0 = new Vector2(0, 0.5f);
            Vector2 uv1 = new Vector2(1, 0);
            Vector2 uv2 = new Vector2(1, 1);

            while(true)
            {
                bool exit = false;
                if(angleRad > endRad)
                {
                    angleRad = endRad;
                    exit = true;
                }

                double x = radius * Math.Cos(angleRad);
                double z = radius * Math.Sin(angleRad);
                current.X = worldMatrix.M41 + x * worldMatrix.M11 + z * worldMatrix.M31; // inlined Transform() without scale
                current.Y = worldMatrix.M42 + x * worldMatrix.M12 + z * worldMatrix.M32;
                current.Z = worldMatrix.M43 + x * worldMatrix.M13 + z * worldMatrix.M33;

                if((first || exit) && lineMaterial.HasValue)
                {
                    MyTransparentGeometry.AddLineBillboard(lineMaterial.Value, lineColor, center, (Vector3)(current - center), 1f, lineThickness, blendType);
                }

                if(!first && faceMaterial.HasValue)
                {
                    Vector3 normalF = (Vector3)normal;
                    MyTransparentGeometry.AddTriangleBillboard(center, current, previous, normalF, normalF, normalF, uv0, uv1, uv2, faceMaterial.Value, uint.MaxValue, center, triangleColor, blendType);
                }

                if(exit)
                {
                    lastOuterRimVec = current;
                    break;
                }

                if(first)
                {
                    firstOuterRimVec = current;

                    angleRad = -MathHelperD.TwoPi;
                    while(angleRad < startRad)
                        angleRad += stepRad;
                }
                else
                {
                    angleRad += stepRad;
                }

                first = false;
                previous = current;
            }
        }
        #endregion

        MyCubeBlockDefinition.MountPoint? ClosestMount = null;
        double ClosestMountDist = double.MaxValue;
        MyOrientedBoundingBoxD ClosestMountOBB = default(MyOrientedBoundingBoxD);
        MatrixD ClosestMountMatrix = default(MatrixD);
        bool ClosestMountGrayed = false;
        bool ClosestMountAutoRotate = false;

        void DrawMountpoints(MyCubeBlockDefinition def, MyCubeBlockDefinition.MountPoint[] mountPoints, MatrixD drawMatrix, bool grayedOut = false)
        {
            Vector3I center = def.Center;
            MatrixD mainMatrix = MatrixD.CreateTranslation((center - (def.Size * 0.5f)) * CellSize) * drawMatrix;
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            bool drawLabel = LabelRender.CanDrawLabel();

            bool hasDefault = false;
            for(int i = 0; i < mountPoints.Length; i++)
            {
                MyCubeBlockDefinition.MountPoint mountPoint = mountPoints[i];
                if(mountPoint.Enabled && mountPoint.Default)
                {
                    hasDefault = true;
                    break;
                }
            }

            for(int i = 0; i < mountPoints.Length; i++)
            {
                MyCubeBlockDefinition.MountPoint mountPoint = mountPoints[i];

                if(!mountPoint.Enabled)
                    continue;

                Vector3 startLocal = mountPoint.Start - center;
                Vector3 endLocal = mountPoint.End - center;

                BoundingBoxD bb = new BoundingBoxD(Vector3.Min(startLocal, endLocal) * CellSize, Vector3.Max(startLocal, endLocal) * CellSize);
                MyOrientedBoundingBoxD obb = new MyOrientedBoundingBoxD(bb, mainMatrix);

                Base6Directions.Axis normalAxis = Base6Directions.GetAxis(Base6Directions.GetDirection(ref mountPoint.Normal));

                double thickness = MountpointThickness * CellSize;

                MatrixD boxMatrix = MatrixD.CreateFromQuaternion(obb.Orientation);
                boxMatrix.Right *= Math.Max(obb.HalfExtent.X * 2, (normalAxis == Base6Directions.Axis.LeftRight ? thickness : 0));
                boxMatrix.Up *= Math.Max(obb.HalfExtent.Y * 2, (normalAxis == Base6Directions.Axis.UpDown ? thickness : 0));
                boxMatrix.Forward *= Math.Max(obb.HalfExtent.Z * 2, (normalAxis == Base6Directions.Axis.ForwardBackward ? thickness : 0));
                boxMatrix.Translation = obb.Center;

                Color colorFace;

                if(grayedOut)
                    colorFace = MountpointColorGrayed;
                else if(!string.IsNullOrEmpty(mountPoint.CouplingTag))
                    colorFace = MountpointColorCoupling;
                else if(mountPoint.ExclusionMask != 0 || mountPoint.PropertiesMask != 0)
                    colorFace = MountpointColorMasked;
                else
                    colorFace = MountpointColorNormal;

                Base6Directions.Axis axis = Base6Directions.GetAxis(Base6Directions.GetDirection(mountPoint.Normal));
                if(axis == Base6Directions.Axis.LeftRight)
                    colorFace = Color.Lighten(colorFace, SideBrightnessChange);
                else if(axis == Base6Directions.Axis.UpDown)
                    colorFace = Color.Darken(colorFace, SideBrightnessChange);

                MySimpleObjectDraw.DrawTransparentBox(ref boxMatrix, ref UnitBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MaterialSquare, onlyFrontFaces: true, blendType: MountpointBlendType);

                bool isAutoRotate = mountPoint.Default;
                if(!hasDefault)
                    isAutoRotate = MyCubeBlockDefinition.NormalToBlockSide(mountPoint.Normal) == BlockSideEnum.Bottom;

                if(isAutoRotate)
                {
                    float lineWdith = (float)obb.HalfExtent.AbsMax() * 0.01f; // 0.01 per meter
                    Color colorAutoRotate = (grayedOut ? MountpointColorGrayed : MountpointColorAutoRotate);
                    MySimpleObjectDraw.DrawTransparentBox(ref boxMatrix, ref UnitBB, ref colorAutoRotate, MySimpleObjectRasterizer.Wireframe, 1, lineWidth: lineWdith, lineMaterial: MaterialSquare, onlyFrontFaces: true, blendType: MountpointBlendType);
                }

                #region See-through-wall faint addition
                //var closeMatrix = m;
                //float depthScale = ConvertToAlwaysOnTop(ref closeMatrix);
                //lineWdith *= depthScale;

                //colorFace *= SEETHROUGH_COLOR_MUL;
                //colorDefault *= SEETHROUGH_COLOR_MUL;

                //MySimpleObjectDraw.DrawTransparentBox(ref closeMatrix, ref unitBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                //if(mountPoint.Default)
                //    MySimpleObjectDraw.DrawTransparentBox(ref closeMatrix, ref unitBB, ref colorDefault, MySimpleObjectRasterizer.Wireframe, 8, lineWidth: lineWdith, lineMaterial: OVERLAY_SQUARE_MATERIAL, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                #endregion

                if(drawLabel)
                {
                    RayD aimLine = new RayD(camMatrix.Translation, camMatrix.Forward);
                    double? distance = obb.Intersects(ref aimLine);

                    // make grayed out mountpoints have lower priority for selection if they're overlapping current mountpoints
                    if(grayedOut && distance.HasValue)
                        distance += 10;

                    if(distance.HasValue && distance.Value < ClosestMountDist)
                    {
                        ClosestMountDist = distance.Value;
                        ClosestMount = mountPoint;
                        ClosestMountOBB = obb;
                        ClosestMountMatrix = boxMatrix;
                        ClosestMountGrayed = grayedOut;
                        ClosestMountAutoRotate = isAutoRotate;
                    }
                }
            }
        }

        void NewFeatureTestingDraw(MyCubeBlockDefinition def, IMySlimBlock block, MatrixD drawMatrix)
        {
            //if(block != null)
            //{
            //    var neighbours = block.Neighbours; // DEBUG TODO: change to non-alloc if implemented properly
            //    MyAPIGateway.Utilities.ShowNotification($"neighbours = {neighbours.Count}", 16);
            //    foreach(var n in neighbours)
            //    {
            //        Matrix m;
            //        Vector3D center;
            //        n.Orientation.GetMatrix(out m);
            //        n.ComputeWorldCenter(out center);

            //        var wm = m * n.CubeGrid.WorldMatrix;
            //        wm.Translation = center;

            //        var nd = (MyCubeBlockDefinition)n.BlockDefinition;
            //        var halfExtents = nd.Size * CellSizeHalf;
            //        var localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MOUNTPOINT_THICKNESS * 0.5);

            //        var color = Color.Lime;

            //        MySimpleObjectDraw.DrawTransparentBox(ref wm, ref localBB, ref color, MySimpleObjectRasterizer.Wireframe, 4, 0.001f, lineMaterial: MyStringId.GetOrCompute("Square"), blendType: BlendTypeEnum.PostPP);
            //    }
            //}

            // TODO: implement solar as overlay ? a box that covers the rays+padding and points towards sun, fades out to not expose max distance...
            //if(block != null)
            //{
            //    IMySolarPanel solarPanel = block.FatBlock as IMySolarPanel;
            //    if(solarPanel != null && solarPanel.IsFunctional)
            //    {
            //        Vector3 sunDir = MyVisualScriptLogicProvider.GetSunDirection();
            //        MySolarPanelDefinition solarDef = (MySolarPanelDefinition)def;

            //        float angleToSun = Vector3.Dot(Vector3.Transform(solarDef.PanelOrientation, solarPanel.WorldMatrix.GetOrientation()), sunDir);
            //        bool isTwoSided = solarDef.IsTwoSided;

            //        if(solarDef.Pivots != null)
            //        {
            //            float scaleFactor = solarPanel.CubeGrid.GridSize;
            //            Vector3[] positions = solarDef.Pivots;
            //            Vector3[] pivots = new Vector3[positions.Length];
            //            for(int i = 0; i < positions.Length; i++)
            //            {
            //                pivots[i] = positions[i] * scaleFactor;
            //            }

            //            int pivotsCount = isTwoSided ? pivots.Length / 2 : pivots.Length;

            //            for(int i = 0; i < pivotsCount; i++)
            //            {
            //                MatrixD worldMatrix = solarPanel.WorldMatrix;
            //                Vector3 vector = Vector3.Transform(solarDef.PanelOrientation, worldMatrix.GetOrientation());

            //                //float m_angleToSun = Vector3.Dot(vector, MySector.DirectionToSunNormalized);
            //                //if((m_angleToSun < 0f && !m_isTwoSided) || !m_solarBlock.IsFunctional)
            //                //{
            //                //    MySandboxGame.Static.Invoke(OnSunAngleComputedFunc, "SolarGamelogic:OnSunAngleComputed");
            //                //    return;
            //                //}

            //                //if(MySectorWeatherComponent.IsOnDarkSide(worldMatrix.Translation))
            //                //{
            //                //    m_isPivotInSun.ForEach(delegate (bool x)
            //                //    {
            //                //        x = false;
            //                //    });
            //                //    m_pivotsInSun = 0;
            //                //    MySandboxGame.Static.Invoke(OnSunAngleComputedFunc, "SolarGamelogic:OnSunAngleComputed");
            //                //    return;
            //                //}

            //                int idx = i * (isTwoSided ? 2 : 1);
            //                Vector3D point = worldMatrix.Translation;
            //                point += worldMatrix.Right * pivots[idx].X;
            //                point += worldMatrix.Up * pivots[idx].Y;
            //                point += worldMatrix.Forward * pivots[idx].Z;

            //                MyTransparentGeometry.AddPointBillboard(MaterialDot, Color.Red, point, 0.1f, 0, blendType: BlendTypeEnum.AdditiveTop);
            //                DebugDraw.Draw3DText(new StringBuilder().Append(idx), point, 0.05, constantSize: false, alwaysOnTop: true);

            //                if(isTwoSided)
            //                {
            //                    Vector3D point2 = worldMatrix.Translation;
            //                    point2 += worldMatrix.Right * pivots[idx + 1].X;
            //                    point2 += worldMatrix.Up * pivots[idx + 1].Y;
            //                    point2 += worldMatrix.Forward * pivots[idx + 1].Z;

            //                    MyTransparentGeometry.AddPointBillboard(MaterialDot, Color.Blue, point2, 0.1f, 0, blendType: BlendTypeEnum.AdditiveTop);
            //                    DebugDraw.Draw3DText(new StringBuilder().Append(idx + 1), point2, 0.05, constantSize: false, alwaysOnTop: true);

            //                    Vector3D test = worldMatrix.Translation + sunDir * 100f;

            //                    if((point2 - test).LengthSquared() < (point - test).LengthSquared())
            //                    {
            //                        point = point2;
            //                    }
            //                }

            //                Vector3D from = point + sunDir * 100f;
            //                Vector3D to = point;

            //                MyTransparentGeometry.AddLineBillboard(MaterialSquare, Color.Orange, from, (to - from), 1f, 0.03f, BlendTypeEnum.AdditiveTop);
            //            }
            //        }
            //        else
            //        {
            //        #if false
            //            for(int idx = 0; idx < 8; idx++)
            //            {
            //                if((angleToSun < 0f && !isTwoSided))
            //                    continue;

            //                //var pos = solar.WorldMatrix.Translation;
            //                //MyPlanet closestPlanet = MyGamePruningStructure.GetClosestPlanet(pos);
            //                //if(closestPlanet == null)
            //                //    continue;
            //                //
            //                //public static bool IsThereNight(MyPlanet planet, ref Vector3D position)
            //                //{
            //                //    Vector3D value = position - planet.PositionComp.GetPosition();
            //                //    if((float)value.Length() > planet.MaximumRadius * 1.1f)
            //                //    {
            //                //        return false;
            //                //    }
            //                //    Vector3 vector = Vector3.Normalize(value);
            //                //    return Vector3.Dot(MySector.DirectionToSunNormalized, vector) < -0.1f;
            //                //}
            //                //if(IsThereNight(closestPlanet, ref pos))
            //                //    continue;

            //                MatrixD orientation = solarPanel.WorldMatrix.GetOrientation();
            //                Vector3D panelOrientationWorld = Vector3.Transform(solarDef.PanelOrientation, orientation);

            //                float dotFw = (float)solarPanel.WorldMatrix.Forward.Dot(panelOrientationWorld);

            //                Vector3D translation = solarPanel.WorldMatrix.Translation;
            //                translation += ((idx % 4) - 1.5f) * CellSize * dotFw * (solarDef.Size.X / 4f) * solarPanel.WorldMatrix.Left;
            //                translation += ((idx / 4) - 0.5f) * CellSize * dotFw * (solarDef.Size.Y / 2f) * solarPanel.WorldMatrix.Up;

            //                translation += CellSize * dotFw * (solarDef.Size.Z / 2f) * panelOrientationWorld * solarDef.PanelOffset;

            //                Vector3D from = point + sunDir * 100f;
            //                Vector3D to = point + sunDir * solarPanel.CubeGrid.GridSize / 4f;

            //                PerPoint.Invoke(idx, from, to);
            //            }
            //            #endif
            //        }
            //    }
            //}

            // TODO: real time neighbour airtight display?
            //{
            //    var heldDef = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

            //    if(heldDef != null && MyCubeBuilder.Static.IsActivated)
            //    {
            //        var grid = MyCubeBuilder.Static.FindClosestGrid();

            //        Vector3D worldAdd;
            //        MyCubeBuilder.Static.GetAddPosition(out worldAdd);

            //        var bb = MyCubeBuilder.Static.GetBuildBoundingBox();
            //        var matrix = Matrix.CreateFromQuaternion(bb.Orientation);

            //        var startPos = grid.WorldToGridInteger(worldAdd);

            //        for(int i = 0; i < Base6Directions.IntDirections.Length; ++i)
            //        {
            //            var endPos = startPos + Base6Directions.IntDirections[i];
            //            bool airtight = heldDef.IsAirTight || Pressurization.TestPressurize(startPos, endPos - startPos, matrix, heldDef);

            //            //if(!airtight)
            //            //{
            //            //    IMySlimBlock b2 = grid.GetCubeBlock(startPos);
            //            //
            //            //    if(b2 != null)
            //            //    {
            //            //        var def2 = (MyCubeBlockDefinition)b2.BlockDefinition;
            //            //        airtight = def2.IsAirTight || Pressurization.IsPressurized(b2, endPos, startPos - endPos);
            //            //    }
            //            //}

            //            MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), (airtight ? Color.Green : Color.Red), worldAdd, Vector3D.TransformNormal(Base6Directions.IntDirections[i], matrix), 1f, 0.1f);

            //            //MyAPIGateway.Utilities.ShowNotification($"{i}. airtight={airtight}", 16); // DEBUG print
            //        }
            //    }
            //}
        }
    }
}
