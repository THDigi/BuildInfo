using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.Input;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static VRageRender.MyBillboard;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Light : SpecializedOverlayBase
    {
        public Light(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_InteriorLight));
            Add(typeof(MyObjectBuilder_ReflectorLight));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_Light data = Main.LiveDataHandler.Get<BData_Light>(def, drawInstance.BDataCache);
            if(data?.LightLogicData?.Lights != null && data.LightLogicData.Lights.Count > 0)
            {
                var light = block?.FatBlock as IMyLightingBlock;

                // HACK: light.Radius does not seem to work for spotlights and .ReflectorRadius is tagged obsolete...
                float range = light?.GetProperty("Radius").AsFloat().GetValue(light) ?? data.LightLogicData.LightRadius.Default;
                float offset = light?.GetProperty("Offset").AsFloat().GetValue(light) ?? data.LightLogicData.LightOffset.Default;

                SpecializedOverlays.LightDraw.DrawLights(data.LightLogicData, ref drawMatrix, drawInstance, def, range, offset, block);
            }
        }
    }
}
