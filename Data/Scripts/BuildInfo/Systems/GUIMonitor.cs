using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Systems
{
    public class GUIMonitor : ModComponent
    {
        public bool InAnyToolbarGUI { get; private set; }

        /// <summary>
        /// True if in hotkey-opened toolbar config (G-menu).
        /// Does not match button/timer/sensor/airvent/etc toolbar config menus.
        /// </summary>
        public bool InToolbarConfig { get; private set; }

        /// <summary>
        /// In any of the small dialog boxes like PB's Run, etc.
        /// </summary>
        public bool InAnyDialogBox { get; private set; }

        /// <summary>
        /// If terminal has been opened but not necessarily the focus screen.
        /// </summary>
        public bool InTerminal { get; private set; }

        /// <summary>
        /// Rectangle for safe GUI, it independent from screen aspect ration so GUI elements look same on any resolution (especially their width)
        /// </summary>
        public Rectangle SafeGUIRectangle { get; private set; }

        /// <summary>
        /// Height scale of actual screen if compared to reference height
        /// </summary>
        public float SafeScreenScale { get; private set; }

        /// <summary>
        /// List of currently seen screens
        /// </summary>
        public ListReader<string> Screens { get; }
        readonly List<string> _screens = new List<string>();

        public event Action<string> ScreenAdded;
        public event Action<string> ScreenRemoved;

        public event Action FirstScreenOpen;
        public event Action LastScreenClose;

        public event Action OptionsMenuClosed;
        public event Action ResolutionChanged;

        readonly bool LogEvents = false;

        public GUIMonitor(BuildInfoMod main) : base(main)
        {
            Screens = new ListReader<string>(_screens);

            //if(BuildInfoMod.IsDevMod)
            //{
            //    LogEvents = true;
            //    UpdateMethods = ComponentLib.UpdateFlags.UPDATE_AFTER_SIM;
            //}
        }

        public override void RegisterComponent()
        {
            RefreshScreenInfo();

            MyAPIGateway.Gui.GuiControlCreated += GUIAdded;
            MyAPIGateway.Gui.GuiControlRemoved += GUIRemoved;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Gui.GuiControlCreated -= GUIAdded;
            MyAPIGateway.Gui.GuiControlRemoved -= GUIRemoved;
        }

        void GUIAdded(object obj)
        {
            try
            {
                string name = obj.GetType().Name;

                if(name.Contains("GuiScreenHudSpace"))
                    return; // ignore HUD as it's not actually a screen

                if(Screens.Count == 0)
                {
                    FirstScreenOpen?.Invoke();
                }

                _screens.Add(name);

                if(name.EndsWith("ScreenCubeBuilder")) // toolbar config menu, from MyGuiScreenCubeBuilder.GetFriendlyName()
                {
                    InAnyToolbarGUI = true;
                    InToolbarConfig = (MyAPIGateway.Gui.ActiveGamePlayScreen == "MyGuiScreenCubeBuilder");
                }
                else if(name.StartsWith("MyGuiScreenDialog")) // MyGuiScreenDialogText, MyGuiScreenDialogAmount, etc
                {
                    InAnyDialogBox = true;
                }
                else if(name.EndsWith("GuiScreenTerminal")) // terminal window, from MyGuiScreenTerminal.GetFriendlyName()
                {
                    InTerminal = true;
                }

                if(LogEvents)
                    DebugLog.PrintHUD(this, $"GUI Added: {name}; screens={string.Join("/", Screens)}", log: true);

                ScreenAdded?.Invoke(name);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void GUIRemoved(object obj)
        {
            try
            {
                string name = obj.GetType().Name;

                if(name.Contains("GuiScreenHudSpace"))
                    return; // ignore HUD as it's not actually a screen

                if(LogEvents && name != "MyGuiScreenLoading" && (Screens.Count == 0 || Screens[Screens.Count - 1] != name))
                {
                    Log.Error($"{GetType().Name}: Other screen ({name}) got removed before the last one! list={string.Join("/", Screens)}");
                }

                _screens.Remove(name);

                if(Screens.Count == 0)
                {
                    LastScreenClose?.Invoke();
                }

                if(name.EndsWith("ScreenCubeBuilder"))
                {
                    InAnyToolbarGUI = false;
                    InToolbarConfig = false;
                }
                else if(name.StartsWith("MyGuiScreenDialog"))
                {
                    InAnyDialogBox = false;
                }
                else if(name.EndsWith("GuiScreenTerminal"))
                {
                    InTerminal = false;
                }

                if(name.EndsWith("ScreenOptionsSpace"))
                {
                    RefreshScreenInfo();
                    OptionsMenuClosed?.Invoke();
                }

                if(LogEvents)
                    DebugLog.PrintHUD(this, $"GUI Removed: {name}; screens={string.Join("/", Screens)}", log: true);

                ScreenRemoved?.Invoke(name);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        string LastScreen;
        public override void UpdateAfterSim(int tick)
        {
            string activeScreen = MyAPIGateway.Gui.ActiveGamePlayScreen;
            if(activeScreen != LastScreen)
            {
                LastScreen = activeScreen;
                DebugLog.PrintHUD(this, $"ActiveScreen changed: {activeScreen ?? "(null)"}", log: true);
            }
        }

        Vector2I LastScreenResolution;
        void RefreshScreenInfo()
        {
            UpdateScreenSize();

            Vector2I screenSize = Vector2I.Round(MyAPIGateway.Session.Camera.ViewportSize);
            if(LastScreenResolution != screenSize)
            {
                Log.Info($"Resolution change detected ({LastScreenResolution} -> {screenSize}).");

                LastScreenResolution = screenSize;
                ResolutionChanged?.Invoke();

                IMyConfig cfg = MyAPIGateway.Session.Config;
                Vector2 resolution = MyAPIGateway.Session.Camera.ViewportSize;
                Vector2 guiSize = MyAPIGateway.Input.GetMouseAreaSize();

                StringBuilder sb = new StringBuilder(512);
                sb.Append("Resolution changed:");
                sb.AppendLine().Append("Camera.ViewportSize = ").Append(resolution.X).Append(" x ").Append(resolution.Y);
                sb.AppendLine().Append("Config.Screen =");
                if(cfg != null)
                    sb.Append(cfg.ScreenWidth?.ToString() ?? "W NULL").Append(" x ").Append(cfg.ScreenHeight?.ToString() ?? "H NULL");
                else
                    sb.Append("Config is NULL");
                sb.AppendLine().Append("Input.GetMouseAreaSize() = ").Append(guiSize.X).Append(" x ").Append(guiSize.Y);
                sb.AppendLine().Append("SafeGUIRectangle = ").Append(SafeGUIRectangle).Append("; SafeScreenScale=").Append(SafeScreenScale);
                Log.Info(sb.ToString());
            }
        }

        // from MyGuiManager.UpdateScreenSize() and crossref'd with https://github.com/KeenSoftwareHouse/SpaceEngineers/blob/master/Sources/Sandbox.Graphics/MyGuiManager.cs#L725
        void UpdateScreenSize()
        {
            // MyGuiConstants.SAFE_ASPECT_RATIO
            const float SAFE_ASPECT_RATIO = 4.0f / 3.0f;
            const float REFERENCE_SCREEN_HEIGHT = 1080f;

            Vector2I screenSize = Vector2I.Round(MyAPIGateway.Session.Camera.ViewportSize);

            int safeGuiSizeY = screenSize.Y;
            int safeGuiSizeX = Math.Min(screenSize.X, (int)(safeGuiSizeY * SAFE_ASPECT_RATIO)); //  This will mantain same aspect ratio for GUI elements
            SafeGUIRectangle = new Rectangle(screenSize.X / 2 - safeGuiSizeX / 2, 0, safeGuiSizeX, safeGuiSizeY);

            SafeScreenScale = safeGuiSizeY / REFERENCE_SCREEN_HEIGHT;
        }

        public Vector2 GetScreenSizeFromNormalizedSize(Vector2 normalizedSize)
        {
            return new Vector2((SafeGUIRectangle.Width + 1) * normalizedSize.X, SafeGUIRectangle.Height * normalizedSize.Y);
        }
    }
}
