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
        protected const float OverlayAlpha = 0.8f;
        protected static readonly MyStringId MaterialDot = MyStringId.GetOrCompute("WhiteDot");
        protected static readonly MyStringId MaterialLaser = MyStringId.GetOrCompute("BuildInfo_Laser");
        protected static readonly MyStringId MaterialSquare = MyStringId.GetOrCompute("BuildInfo_Square");
        protected static readonly MyStringId MaterialGradient = MyStringId.GetOrCompute("BuildInfo_TransparentGradient");
        protected const BlendTypeEnum BlendType = BlendTypeEnum.SDR;

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
