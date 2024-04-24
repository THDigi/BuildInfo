using System;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.Input;
using Digi.Input.Devices;
using Draygo.API;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features.Overlays
{
    public enum LabelType
    {
        /// <summary>
        /// Use <see cref="LabelRendering.DynamicLabel"/> to edit this label in realtime.
        /// </summary>
        DynamicLabel = 0,

        // the rest have static messages that are assigned on creation
        AxisZ,
        AxisX,
        AxisY,
        ModelOffset,
        SensorRadius,
        MineRadius,
        CarveRadius,
        WeldingRadius,
        GrindingRadius,
        PitchLimit,
        YawLimit,
        AirtightWhenClosed,
        ThrustDamage,
        MagnetizedArea,
        CollectionArea,
        TerrainClearance,
        SideClearance,
        OptimalClearance,
        Laser,
        RaycastLimits,
        SteeringAxis,
        ShipCenterOfMass,
        Camera,
    }

    public class LabelRendering
    {
        class LabelData
        {
            public HudAPIv2.SpaceMessage Text;
            public HudAPIv2.SpaceMessage Shadow;
            public float UnderlineLength = -1;
        }

        public readonly StringBuilder DynamicLabel = new StringBuilder(128);
        public bool AnyLabelShown { get; private set; }
        public bool ForceDrawLabel = false;

        readonly LabelData[] LabelByType;
        readonly BuildInfoMod Main;

        public const double TextScale = 0.24;
        public static readonly Vector2D TextOffset = new Vector2D(0.1, 0.1);
        public static readonly Vector2D ShadowOffset = new Vector2D(0.01, -0.01);

        public static readonly Vector4 ShadowColor = Color.Black * 0.9f;

        public const string TextFont = FontsHandler.BI_SEOutlined;
        public const bool UseShadowMessage = false;

        public const BlendTypeEnum TextBlendType = BlendTypeEnum.PostPP;
        public const BlendTypeEnum ShadowBlendType = BlendTypeEnum.PostPP;

        public static readonly MyStringId LineMaterial = MyStringId.GetOrCompute("BuildInfo_Laser");
        public const float LineThickScale = 4f;
        public const float LabelScale = 1.2f; // TODO config?

        public LabelRendering(BuildInfoMod mod)
        {
            if(mod == null)
                throw new Exception($"invalid instantiation of {nameof(LabelRendering)}");

            Main = mod;

            LabelByType = new LabelData[Enum.GetValues(typeof(LabelType)).Length];
        }

        public bool CanDrawLabel(OverlayLabelsFlags labelsSetting = OverlayLabelsFlags.Other)
        {
            return Main.TextAPI.IsEnabled && (ForceDrawLabel || Main.Config.OverlayLabels.IsSet(labelsSetting) || (Main.Config.OverlaysShowLabelsWithBind.Value && InputLib.GetGameControlPressed(ControlContext.CHARACTER, MyControlsSpace.LOOKAROUND)));
        }

        public void DrawLine(Vector3D start, Vector3D direction, Color color,
            float scale = 1f, float lineHeight = 0.5f, float lineThick = 0.005f,
            bool autoAlign = true, bool alwaysOnTop = false)
        {
            scale *= LabelScale;

            MatrixD cm = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D textWorldPos = start + direction * lineHeight;

            if(alwaysOnTop)
            {
                start = cm.Translation + ((start - cm.Translation) * OverlayDrawInstance.DepthRatio);
                scale = scale * OverlayDrawInstance.DepthRatioF;
            }

            lineHeight *= scale;

            // TODO: config setting for overlay text relative scale?
            float distanceToCam = (float)Vector3D.Distance(cm.Translation, textWorldPos);
            distanceToCam = Math.Max(distanceToCam, 1f);
            scale = scale * 0.1f * distanceToCam;

            lineThick *= LineThickScale * scale;

            textWorldPos = start + direction * lineHeight;

            Vector3D shadowOffset = cm.Right * (ShadowOffset.X * scale) + cm.Up * (ShadowOffset.Y * scale);

            MyTransparentGeometry.AddLineBillboard(LineMaterial, ShadowColor, start + shadowOffset, (Vector3)direction, lineHeight, lineThick, ShadowBlendType);
            MyTransparentGeometry.AddLineBillboard(LineMaterial, color, start, (Vector3)direction, lineHeight, lineThick, TextBlendType);
        }

        public void DrawLineLabel(LabelType id, Vector3D start, Vector3D direction, Color color,
            string cacheMessage = null, float scale = 1f, float lineHeight = 0.5f, float lineThick = 0.005f,
            OverlayLabelsFlags settingFlag = OverlayLabelsFlags.Other, HudAPIv2.TextOrientation align = HudAPIv2.TextOrientation.ltr,
            bool autoAlign = true, bool alwaysOnTop = false)
        {
            if(!Main.TextAPI.IsEnabled)
                return;

            scale *= LabelScale;

            MatrixD cm = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D textWorldPos = start + direction * lineHeight;

            if(alwaysOnTop)
            {
                start = cm.Translation + ((start - cm.Translation) * OverlayDrawInstance.DepthRatio);
                scale = scale * OverlayDrawInstance.DepthRatioF;
            }

            lineHeight *= scale;

            // TODO: config setting for overlay text relative scale?
            float distanceToCam = (float)Vector3D.Distance(cm.Translation, textWorldPos);
            distanceToCam = Math.Max(distanceToCam, 1f);
            scale = scale * 0.1f * distanceToCam;

            lineThick *= LineThickScale * scale;

            textWorldPos = start + direction * lineHeight;

            Vector3D shadowOffset = cm.Right * (ShadowOffset.X * scale) + cm.Up * (ShadowOffset.Y * scale);

            MyTransparentGeometry.AddLineBillboard(LineMaterial, ShadowColor, start + shadowOffset, (Vector3)direction, lineHeight, lineThick, ShadowBlendType);
            MyTransparentGeometry.AddLineBillboard(LineMaterial, color, start, (Vector3)direction, lineHeight, lineThick, TextBlendType);

            if(!Main.Config.OverlayLabels.IsSet(settingFlag) && !(Main.Config.OverlaysShowLabelsWithBind.Value && InputLib.GetGameControlPressed(ControlContext.CHARACTER, MyControlsSpace.LOOKAROUND)))
                return;

            // has issue on always-on-top labels
            //if(!alwaysOnTop)
            //{
            //    var sphere = new BoundingSphereD(textWorldPos, 0.01f);
            //    if(!MyAPIGateway.Session.Camera.IsInFrustum(ref sphere))
            //        return;
            //}

            Vector3D textDir = cm.Right;
            if(autoAlign && textDir.Dot(direction) <= -0.8f)
            {
                textDir = cm.Left;
                if(align == HudAPIv2.TextOrientation.ltr)
                    align = HudAPIv2.TextOrientation.rtl;
            }

            Vector3D textPos = textWorldPos + textDir * (TextOffset.X * scale) + cm.Up * (TextOffset.Y * scale);
            Vector3D shadowPos = textPos + textDir * (ShadowOffset.X * scale) + cm.Up * (ShadowOffset.Y * scale) + cm.Forward * 0.00000001;

            int i = (int)id;
            LabelData labelData = LabelByType[i];
            if(labelData == null)
                LabelByType[i] = labelData = new LabelData();

            if(labelData.Text == null)
            {
                StringBuilder shadowSB = new StringBuilder(DynamicLabel.Capacity);
                StringBuilder msgSB = new StringBuilder(DynamicLabel.Capacity);

                if(UseShadowMessage)
                {
                    labelData.Shadow = new HudAPIv2.SpaceMessage(shadowSB, Vector3D.Zero, Vector3D.Up, Vector3D.Left, TextScale, Blend: ShadowBlendType);
                    labelData.Shadow.Visible = false;
                }

                labelData.Text = new HudAPIv2.SpaceMessage(msgSB, Vector3D.Zero, Vector3D.Up, Vector3D.Left, TextScale, Blend: TextBlendType, Font: TextFont);
                labelData.Text.Visible = false;

                if(cacheMessage != null)
                {
                    shadowSB.Color(ShadowColor).Append(cacheMessage);
                    msgSB.Color(color).Append(cacheMessage);

                    Vector2D textSize = labelData.Text.GetTextLength();
                    labelData.UnderlineLength = (float)(TextOffset.X + (textSize.X * TextScale));
                }
            }

            HudAPIv2.SpaceMessage shadow = labelData.Shadow;
            HudAPIv2.SpaceMessage text = labelData.Text;

            if(cacheMessage == null)
            {
                if(UseShadowMessage)
                    shadow.Message.Clear().Color(ShadowColor).AppendStringBuilder(DynamicLabel);

                text.Message.Clear().Color(color).AppendStringBuilder(DynamicLabel);

                Vector2D textSize = text.GetTextLength();
                labelData.UnderlineLength = (float)(TextOffset.X + (textSize.X * TextScale));
            }

            if(UseShadowMessage)
            {
                shadow.TxtOrientation = align;
                shadow.Scale = scale * TextScale;
                shadow.WorldPosition = shadowPos;
                shadow.Left = cm.Left;
                shadow.Up = cm.Up;
                shadow.Draw(); // this removes the need of having the text visible, also draws text more accurately to my position
            }

            text.TxtOrientation = align;
            text.Scale = scale * TextScale;
            text.WorldPosition = textPos;
            text.Left = cm.Left;
            text.Up = cm.Up;
            text.Draw();

            float underlineLength = labelData.UnderlineLength * scale;
            MyTransparentGeometry.AddLineBillboard(LineMaterial, ShadowColor, textWorldPos + shadowOffset, (Vector3)textDir, underlineLength, lineThick, ShadowBlendType);
            MyTransparentGeometry.AddLineBillboard(LineMaterial, color, textWorldPos, (Vector3)textDir, underlineLength, lineThick, TextBlendType);

            AnyLabelShown = true;
        }
    }
}