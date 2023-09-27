using Sandbox.ModAPI;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features.HUD
{
    // hide right side bottom HUD in certain cases (like cockpit build mode).
    public class GridControlStat : HudStatBase
    {
        public GridControlStat() : base("controlled_is_grid")
        {
        }

        protected override void UpdateBeforeSim(ref float current, ref float min, ref float max)
        {
            // if cubebuilder is used, hide the right side ship info.
            if(Main.Config.CockpitBuildHideRightHud.Value && MyAPIGateway.CubeBuilder.IsActivated)
            {
                current = 0f;
                return;
            }

            // the rest is vanilla game's logic for this stat
            IMyControllableEntity controlled = MyAPIGateway.Session?.ControlledObject;
            if(controlled == null)
            {
                current = 0f;
            }
            else if(controlled is IMyLargeTurretBase)
            {
                current = (MyAPIGateway.Session?.Player?.Character?.Parent is IMyShipController ? 1 : 0);
            }
            else
            {
                current = (controlled is IMyShipController ? 1 : 0);
            }
        }

        protected override string ValueAsString() => CurrentValue >= 0.5f ? "1" : "0";
    }
}