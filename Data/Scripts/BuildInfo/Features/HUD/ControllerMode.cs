using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class ControllerMode : HudStatBase
    {
        public ControllerMode() : base("controller_mode")
        {
        }

        protected override string ValueAsString() => CurrentValue >= 0.5f ? "1" : "0";

        protected override void UpdateBeforeSim(ref float current, ref float min, ref float max)
        {
            switch(Main.Config.ForceControllerHUD.ValueEnum)
            {
                case Config.ForceControllerMode.KeyboardMouseOnly:
                    current = 0;
                    break;

                case Config.ForceControllerMode.ControllerOnly:
                    current = 1;
                    break;

                default:
                    // vanilla game's logic for this stat
                    current = (MyAPIGateway.Input.IsJoystickLastUsed ? 1 : 0);
                    break;
            }
        }
    }
}