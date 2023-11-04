using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public abstract class SpecializedOverlayBase
    {
        public const float LaserOverlayAlpha = 0.8f;
        public const float SolidOverlayAlpha = 0.5f;
        public static readonly MyStringId MaterialDot = MyStringId.GetOrCompute("WhiteDot");
        public static readonly MyStringId MaterialLaser = MyStringId.GetOrCompute("BuildInfo_Laser");
        public static readonly MyStringId MaterialSquare = MyStringId.GetOrCompute("BuildInfo_Square");
        public static readonly MyStringId MaterialGradient = MyStringId.GetOrCompute("BuildInfo_TransparentGradient");
        public static readonly MyStringId MaterialGradientSRGB = MyStringId.GetOrCompute("BuildInfo_TransparentGradientSRGB");
        public const BlendTypeEnum BlendType = BlendTypeEnum.SDR;

        /// <summary>
        /// lowest usable for spheres and such
        /// </summary>
        public const int RoundedQualityLow = 18;

        public const int RoundedQualityMed = 15;

        /// <summary>
        /// mainly for circles, not spheres or anything complex
        /// </summary>
        public const int RoundedQualityHigh = 6;

        protected readonly BuildInfoMod Main;
        protected readonly Overlays Overlays;
        protected readonly SpecializedOverlays SpecializedOverlays;

        public SpecializedOverlayBase(SpecializedOverlays processor)
        {
            SpecializedOverlays = processor;
            Main = processor.Main;
            Overlays = Main.Overlays;
        }

        protected void Add(MyObjectBuilderType type) => SpecializedOverlays.Add(type, this);

        public abstract void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block);
    }
}
