using System;
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

        readonly bool Debug = false;

        public GUIMonitor(BuildInfoMod main) : base(main)
        {
            if(Debug)
                UpdateMethods = ComponentLib.UpdateFlags.UPDATE_AFTER_SIM;
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

                if(name.EndsWith("ScreenCubeBuilder")) // toolbar config menu, from MyGuiScreenCubeBuilder.GetFriendlyName()
                {
                    InAnyToolbarGUI = true;
                    InToolbarConfig = (MyAPIGateway.Gui.ActiveGamePlayScreen == "MyGuiScreenCubeBuilder");
                }

                if(Debug)
                    DebugLog.PrintHUD(this, $"GUI Added: {name}");
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

                if(name.EndsWith("ScreenCubeBuilder"))
                {
                    InAnyToolbarGUI = false;
                    InToolbarConfig = false;
                }

                if(Debug)
                    DebugLog.PrintHUD(this, $"GUI Removed: {name}");
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
                DebugLog.PrintHUD(this, $"ActiveScreen changed: {activeScreen}");
            }
        }
    }
}
