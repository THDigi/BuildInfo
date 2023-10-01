using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Systems
{
    public enum HudState
    {
        OFF = 0,
        HINTS = 1,
        BASIC = 2
    }

    // TODO: add UI scale if/when API gets it

    public class GameConfig : ModComponent
    {
        public delegate void EventHandlerHudStateChanged(HudState prevState, HudState state);
        public event EventHandlerHudStateChanged HudStateChanged;

        public HudState HudState;
        public float HudBackgroundOpacity;
        public float UIBackgroundOpacity;
        public double AspectRatio;
        public bool RotationHints;
        public bool UsingGamepad;
        public event Action UsingGamepadChanged;

        HudState? PreviousHudState;
        HashSet<string> HideHudRequests = new HashSet<string>();

        public GameConfig(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
        }

        public override void RegisterComponent()
        {
            Main.GUIMonitor.OptionsMenuClosed += UpdateConfigValues;

            UpdateConfigValues();
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.GUIMonitor.OptionsMenuClosed += UpdateConfigValues;

            if(PreviousHudState != null)
            {
                SetHudState(PreviousHudState.Value, callEvents: false);
                PreviousHudState = null;
                HideHudRequests.Clear();
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            // required in simulation update because it gets the previous value if used in HandleInput()
            if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
            {
                UpdateHudState();
            }

            // since we're updating anyway, might as well provide an event for this
            bool usingGamepadNew = MyAPIGateway.Input.IsJoystickLastUsed;
            if(UsingGamepad != usingGamepadNew)
            {
                UsingGamepad = usingGamepadNew;
                UsingGamepadChanged?.Invoke();
            }
        }

        /// <summary>
        /// Adds/removes an id to keep HUD hidden, once all IDs are removed the HUD is automatically unhidden.
        /// When world unloads it also automatically unhides it.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="hide"></param>
        public void TempHideHUD(string id, bool hide)
        {
            if(hide)
            {
                // TODO: what about if HUD is already hidden? and what if player manually un-hides after this?
                if(HideHudRequests.Count == 0)
                {
                    PreviousHudState = this.HudState;
                    SetHudState(HudState.OFF);
                }

                HideHudRequests.Add(id);
            }
            else
            {
                HideHudRequests.Remove(id);

                if(HideHudRequests.Count == 0 && PreviousHudState != null)
                {
                    SetHudState(PreviousHudState.Value);
                    PreviousHudState = null;
                }
            }
        }

        public void SetHudState(HudState state, bool callEvents = true)
        {
            MyVisualScriptLogicProvider.SetHudState((int)state, playerId: 0); // gets called locally, because of playerId 0

            if(callEvents)
                UpdateHudState();
        }

        void UpdateConfigValues()
        {
            UpdateHudState();

            IMyConfig cfg = MyAPIGateway.Session.Config;

            HudBackgroundOpacity = cfg?.HUDBkOpacity ?? 0.6f;
            UIBackgroundOpacity = cfg?.UIBkOpacity ?? 0.8f;
            RotationHints = cfg?.RotationHints ?? true;

            Vector2 viewportSize = MyAPIGateway.Session.Camera.ViewportSize;
            AspectRatio = (double)viewportSize.X / (double)viewportSize.Y;
        }

        void UpdateHudState()
        {
            HudState prevState = HudState;
            HudState = (HudState)(MyAPIGateway.Session.Config?.HudState ?? (int)HudState.HINTS);

            if(prevState != HudState)
                HudStateChanged?.Invoke(prevState, HudState);
        }
    }
}
