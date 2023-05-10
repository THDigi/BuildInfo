using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Spotlight : BData_Base
    {
        public bool HasRotatingParts = false;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            IMyReflectorLight spotlight = block as IMyReflectorLight;
            IMyTerminalControl prop = spotlight?.GetProperty("RotationSpeed") as IMyTerminalControl;
            if(prop?.Visible != null)
            {
                // not very accurate as it just checks if block has any subparts, but maybe keen will improve it...
                // the light logic is not accessible and it's too much to copy for just this check.
                HasRotatingParts = prop.Visible.Invoke(spotlight);
            }

            base.IsValid(block, def);
            return true;
        }
    }
}
