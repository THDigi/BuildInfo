using System;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

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
                BoolSetting setting = BuildInfoMod.Instance?.Config?.TurretHUD;
                if(setting != null && setting.Value)
                {
                    CurrentValue = 0f;
                }
                else
                {
                    // vanilla game's logic for this stat
                    IMyControllableEntity controlled = MyAPIGateway.Session?.ControlledObject;
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

    // show gamepad HUD when turret or turretcontroller is controlled
    //public class TurretGamepadHUD : IMyHudStat
    //{
    //    public MyStringHash Id { get; private set; }
    //    public float CurrentValue { get; private set; }
    //    public float MinValue { get; } = 0f;
    //    public float MaxValue { get; } = 1f;
    //    public string GetValueString() => CurrentValue >= 0.5f ? "1" : "0";
    //
    //    public TurretGamepadHUD()
    //    {
    //        if(!BuildInfo_GameSession.IsKilled)
    //            Id = MyStringHash.GetOrCompute("controller_mode");
    //    }
    //
    //    public void Update()
    //    {
    //        if(BuildInfo_GameSession.IsKilled)
    //            return;
    //
    //        try
    //        {
    //            BoolSetting setting = BuildInfoMod.Instance?.Config?.TurretHUD;
    //            if(setting != null && setting.Value)
    //            {
    //                IMyControllableEntity controlled = MyAPIGateway.Session?.ControlledObject;
    //
    //                CurrentValue = (MyAPIGateway.Input.IsJoystickLastUsed || controlled is IMyLargeTurretBase || controlled is IMyTurretControlBlock) ? 1f : 0f;
    //            }
    //            else
    //            {
    //                // vanilla game's logic for this stat
    //                CurrentValue = (MyAPIGateway.Input.IsJoystickLastUsed ? 1 : 0);
    //            }
    //        }
    //        catch(Exception e)
    //        {
    //            Log.Error(e);
    //        }
    //    }
    //}
}