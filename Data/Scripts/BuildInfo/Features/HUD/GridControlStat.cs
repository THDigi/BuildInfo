using System;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features.HUD
{
    // hide right side HUD in certain cases (like cockpit build mode).
    public class GridControlStat : IMyHudStat
    {
        public MyStringHash Id { get; private set; }
        public float CurrentValue { get; private set; }
        public float MinValue { get; } = 0f;
        public float MaxValue { get; } = 1f;
        public string GetValueString() => CurrentValue >= 0.5f ? "1" : "0";

        public GridControlStat()
        {
            if(!BuildInfo_GameSession.GetOrComputeIsKilled(this.GetType().Name))
                Id = MyStringHash.GetOrCompute("controlled_is_grid");
        }

        public void Update()
        {
            if(BuildInfo_GameSession.IsKilled)
                return;

            try
            {
                // if cubebuilder is used, hide the right side ship info.
                if(MyAPIGateway.CubeBuilder.IsActivated)
                {
                    BoolSetting setting = BuildInfoMod.Instance?.Config?.HudStatOverrides;
                    if(setting != null && setting.Value)
                    {
                        CurrentValue = 0f;
                        return;
                    }
                }

                // vanilla game's logic for this stat
                IMyControllableEntity controlled = MyAPIGateway.Session?.ControlledObject;
                if(controlled == null)
                {
                    CurrentValue = 0f;
                }
                else if(controlled is IMyLargeTurretBase)
                {
                    CurrentValue = (MyAPIGateway.Session?.Player?.Character?.Parent is IMyShipController ? 1 : 0);
                }
                else
                {
                    CurrentValue = (controlled is IMyShipController ? 1 : 0);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}