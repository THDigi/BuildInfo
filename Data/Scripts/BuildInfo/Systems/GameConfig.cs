using System;
using Digi.ComponentLib;
using Sandbox.Game;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Systems
{
    public enum HudState
    {
        OFF = 0,
        HINTS = 1,
        BASIC = 2
    }

    public class GameConfig : ClientComponent
    {
        public delegate void EventHandlerHudStateChanged(HudState prevState, HudState state);
        public event EventHandlerHudStateChanged HudStateChanged;

        public event Action OptionsMenuClosed;

        public HudState HudState;
        public float HudBackgroundOpacity;
        public double AspectRatio;
        public bool RotationHints;

        public GameConfig(Client mod) : base(mod)
        {
            Flags = UpdateFlags.UPDATE_AFTER_SIM;
        }

        public override void RegisterComponent()
        {
            MyAPIGateway.Gui.GuiControlRemoved += GuiControlRemoved;

            UpdateConfigValues();
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Gui.GuiControlRemoved -= GuiControlRemoved;
        }

        public override void UpdateAfterSim(int tick)
        {
            // required in simulation update because it gets the previous value if used in HandleInput()
            if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
            {
                UpdateHudState();
            }
        }

        private void GuiControlRemoved(object obj)
        {
            try
            {
                if(obj.ToString().EndsWith("ScreenOptionsSpace")) // closing options menu just assumes you changed something so it'll re-check config settings
                {
                    UpdateConfigValues();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void UpdateConfigValues()
        {
            UpdateHudState();

            HudBackgroundOpacity = MyAPIGateway.Session.Config?.HUDBkOpacity ?? 0.6f;

            RotationHints = MyAPIGateway.Session.Config?.RotationHints ?? true;

            var viewportSize = MyAPIGateway.Session.Camera.ViewportSize;
            AspectRatio = (double)viewportSize.X / (double)viewportSize.Y;

            OptionsMenuClosed?.Invoke();
        }

        private void UpdateHudState()
        {
            var prevState = HudState;

            HudState = (HudState)(MyAPIGateway.Session.Config?.HudState ?? (int)HudState.HINTS);

            HudStateChanged?.Invoke(prevState, HudState);
        }
    }
}
