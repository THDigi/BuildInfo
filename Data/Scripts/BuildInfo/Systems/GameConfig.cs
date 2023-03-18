using System;
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

        public event Action OptionsMenuClosed;

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

        void GuiControlRemoved(object obj)
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

        void UpdateConfigValues()
        {
            UpdateHudState();

            IMyConfig cfg = MyAPIGateway.Session.Config;

            HudBackgroundOpacity = cfg?.HUDBkOpacity ?? 0.6f;
            UIBackgroundOpacity = cfg?.UIBkOpacity ?? 0.8f;
            RotationHints = cfg?.RotationHints ?? true;

            Vector2 viewportSize = MyAPIGateway.Session.Camera.ViewportSize;
            AspectRatio = (double)viewportSize.X / (double)viewportSize.Y;

            Vector2 mouseAreaSize = MyAPIGateway.Input.GetMouseAreaSize();
            Log.Info($"UpdateConfigValues()" +
                $"\nCamera.ViewportSize = {viewportSize.X} x {viewportSize.Y}" +
                $"\nConfig.Screen = {cfg.ScreenWidth} x {cfg.ScreenHeight}" +
                $"\nInput.GetMouseAreaSize() = {mouseAreaSize.X} x {mouseAreaSize.Y}");

            OptionsMenuClosed?.Invoke();
        }

        void UpdateHudState()
        {
            HudState prevState = HudState;

            HudState = (HudState)(MyAPIGateway.Session.Config?.HudState ?? (int)HudState.HINTS);

            HudStateChanged?.Invoke(prevState, HudState);
        }
    }
}
