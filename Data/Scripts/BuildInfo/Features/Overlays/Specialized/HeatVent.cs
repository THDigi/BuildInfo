using Digi.BuildInfo.Features.LiveData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class HeatVent : SpecializedOverlayBase
    {
        public HeatVent(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_HeatVentBlock));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            BData_HeatVent data = Main.LiveDataHandler.Get<BData_HeatVent>(def, drawInstance.BDataCache);
            if(data?.LightLogicData?.Lights != null && data.LightLogicData.Lights.Count > 0)
            {
                var heatVent = block?.FatBlock as IMyHeatVent;
                float range = heatVent?.GetProperty("Radius").AsFloat().GetValue(heatVent) ?? data.LightLogicData.LightRadius.Default;
                float offset = heatVent?.GetProperty("Offset").AsFloat().GetValue(heatVent) ?? data.LightLogicData.LightOffset.Default;

                SpecializedOverlays.LightDraw.DrawLights(data.LightLogicData, ref drawMatrix, drawInstance, def, range, offset, block);
            }
        }
    }
}
