using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Light : BData_Base
    {
        public LightLogicData LightLogicData;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var lightDef = def as MyLightingBlockDefinition;
            if(lightDef != null)
            {
                // data matching MyLightingLogic.ctor(..., MyLightingBlockDefinition)
                if(def.Id.TypeId == typeof(MyObjectBuilder_ReflectorLight))
                {
                    LightLogicData = new LightLogicData(block, lightDef.LightDummyName, lightDef.LightReflectorRadius,
                        lightDef.LightReflectorRadius.Max, lightDef.LightOffset, lightDef.ReflectorConeDegrees);
                }
                else
                {
                    LightLogicData = new LightLogicData(block, lightDef.LightDummyName, lightDef.LightRadius,
                        lightDef.LightReflectorRadius.Max, lightDef.LightOffset);
                }
            }
            else
                Log.Error($"Unexpected for '{def.Id}' to not have a lighting block definition! Might cause issues in general, check definition xsi:type.");

            return base.IsValid(block, def) || true;
        }
    }
}