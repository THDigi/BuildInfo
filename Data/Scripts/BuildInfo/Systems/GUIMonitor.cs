using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features;
using Sandbox.ModAPI;

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
        /// List of currently seen screens
        /// </summary>
        public readonly List<string> Screens = new List<string>();

        public event Action<string> ScreenAdded;
        public event Action<string> ScreenRemoved;

        readonly bool LogEvents = false;

        public GUIMonitor(BuildInfoMod main) : base(main)
        {
            //if(BuildInfoMod.IsDevMod)
            //{
            //    LogEvents = true;
            //    UpdateMethods = ComponentLib.UpdateFlags.UPDATE_AFTER_SIM;
            //}
        }

        public override void RegisterComponent()
        {
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

                Screens.Add(name);

                if(name.EndsWith("ScreenCubeBuilder")) // toolbar config menu, from MyGuiScreenCubeBuilder.GetFriendlyName()
                {
                    InAnyToolbarGUI = true;
                    InToolbarConfig = (MyAPIGateway.Gui.ActiveGamePlayScreen == "MyGuiScreenCubeBuilder");
                }
                else if(name.StartsWith("MyGuiScreenDialog")) // MyGuiScreenDialogText, MyGuiScreenDialogAmount, etc
                {
                    InAnyDialogBox = true;
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

                if(LogEvents && name != "MyGuiScreenLoading" && (Screens.Count == 0 || Screens[Screens.Count - 1] != name))
                {
                    Log.Error($"{GetType().Name}: Other screen ({name}) got removed before the last one! list={string.Join("/", Screens)}");
                }

                Screens.Remove(name);

                if(name.EndsWith("ScreenCubeBuilder"))
                {
                    InAnyToolbarGUI = false;
                    InToolbarConfig = false;
                }
                else if(name.StartsWith("MyGuiScreenDialog"))
                {
                    InAnyDialogBox = false;
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
    }
}
