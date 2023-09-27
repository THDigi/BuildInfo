using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features.HUD
{
    // allow HUD to be shown when turret is controlled.
    public class TurretControlStat : HudStatBase
    {
        public TurretControlStat() : base("controlled_is_turret")
        {
        }

        protected override void UpdateBeforeSim(ref float current, ref float min, ref float max)
        {
            if(Main.Config.TurretHUD.Value)
            {
                current = 0f;
                return;
            }

            // vanilla game's logic for this stat
            IMyControllableEntity controlled = MyAPIGateway.Session?.ControlledObject;
            if(controlled == null)
                current = 0f;
            else
                current = (controlled is IMyUserControllableGun ? 1 : 0);
        }

        protected override string ValueAsString() => CurrentValue >= 0.5f ? "1" : "0";
    }

    // show gamepad HUD when turret or turretcontroller is controlled...
    // would look cleaner, but disabled because it might cause weirdness and CTC allows your prev controlled's toolbar to be interactive so that's weird.
    //public class TurretGamepadHUD : HudStatBase
    //{
    //    public TurretGamepadHUD() : base("controller_mode")
    //    {
    //    }
    //
    //    protected override string ValueAsString() => CurrentValue >= 0.5f ? "1" : "0";
    //
    //    protected override void UpdateBeforeSim(ref float current, ref float min, ref float max)
    //    {
    //        if(Main.Config.TurretHUD.Value)
    //        {
    //            IMyControllableEntity controlled = MyAPIGateway.Session?.ControlledObject;
    //
    //            current = (MyAPIGateway.Input.IsJoystickLastUsed || controlled is IMyLargeTurretBase || controlled is IMyTurretControlBlock) ? 1 : 0;
    //        }
    //        else
    //        {
    //            // vanilla game's logic for this stat
    //            current = (MyAPIGateway.Input.IsJoystickLastUsed ? 1 : 0);
    //        }
    //    }
    //}
}