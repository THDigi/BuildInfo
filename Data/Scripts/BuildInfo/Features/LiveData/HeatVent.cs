using Sandbox.Definitions;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_HeatVent : BData_Base
    {
        public LightLogicData LightLogicData;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var heatVentDef = def as MyHeatVentBlockDefinition;
            if(heatVentDef != null)
            {
                // data matching MyLightingLogic.ctor(..., MyHeatVentBlockDefinition)
                LightLogicData = new LightLogicData(block, heatVentDef.LightDummyName, heatVentDef.LightRadiusBounds,
                    heatVentDef.LightOffsetBounds.Max, heatVentDef.LightOffsetBounds);
                // ignoring heatVentDef.ReflectorConeDegrees because it's not actually used, MyLightingLogic.IsReflector remains false.
            }
            else
                Log.Error($"Unexpected for '{def.Id}' to not have a heat vent definition! Might cause issues in general, check definition xsi:type.");

            return base.IsValid(block, def) || true;
        }
    }
}