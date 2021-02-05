using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.HUD
{
    // allow HUD to be shown when turret is controlled.
    public class TurretControlStat : IMyHudStat
    {
        public MyStringHash Id { get; private set; } = MyStringHash.GetOrCompute("controlled_is_turret");
        public float CurrentValue { get; private set; }
        public float MinValue => 0f;
        public float MaxValue => 1f;
        public string GetValueString() => CurrentValue >= 0.5f ? "1" : "0";

        public TurretControlStat()
        {
        }

        public void Update()
        {
            var setting = BuildInfoMod.Instance?.Config?.TurretHUD;
            if(setting != null && setting.Value)
            {
                CurrentValue = 0f;
            }
            else
            {
                // vanilla game's logic for this stat
                var controlled = MyAPIGateway.Session?.ControlledObject;
                if(controlled == null)
                    CurrentValue = 0f;
                else
                    CurrentValue = (controlled is IMyUserControllableGun ? 1 : 0);
            }
        }
    }
}