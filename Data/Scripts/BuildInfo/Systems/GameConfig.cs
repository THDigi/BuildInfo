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
        }

        public override void UpdateAfterSim(int tick)
        {
            // required in simulation update because it gets the previous value if used in HandleInput()
            if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
            {
                UpdateHudState();
            }
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

            HudStateChanged?.Invoke(prevState, HudState);
        }
    }
}
