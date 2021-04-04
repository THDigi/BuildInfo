using System;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.HUD
{
    // allow HUD to be shown when turret is controlled.
    public class TurretControlStat : IMyHudStat
    {
        public MyStringHash Id { get; private set; }
        public float CurrentValue { get; private set; }
        public float MinValue { get; } = 0f;
        public float MaxValue { get; } = 1f;
        public string GetValueString() => CurrentValue >= 0.5f ? "1" : "0";

        public TurretControlStat()
        {
            if(!BuildInfo_GameSession.IsKilled)
                Id = MyStringHash.GetOrCompute("controlled_is_turret");
        }

        public void Update()
        {
            if(BuildInfo_GameSession.IsKilled)
                return;

            try
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
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}