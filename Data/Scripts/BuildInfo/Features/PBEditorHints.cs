using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.GUI;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    /// <summary>
    /// Like known bugs, where to get help, etc.
    /// </summary>
    public class PBEditorHints : ModComponent
    {
        const bool DebugPivots = false;
        const string PBEditorSuffix = "GuiScreenEditor";

        TooltipHandler Tooltip;
        Button[] Buttons;
        Button PBAPIGuide;
        Button UnclickableButtons;
        Button LinuxCompile;
        bool ModifiedTerminalControls = false;
        string RestoreURL;
        string RestoreRunCodeDefault;

        public PBEditorHints(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.TextAPI.Detected += TextAPI_Detected;
            Main.GUIMonitor.ActiveScreenChanged += ActiveScreenChanged;

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;

            RestoreURL = MySteamConstants.URL_BROWSE_WORKSHOP_INGAMESCRIPTS_HELP;
            MySteamConstants.URL_BROWSE_WORKSHOP_INGAMESCRIPTS_HELP = "https://spaceengineers.wiki.gg/wiki/Scripting";

            const string Suffix = "(not reliable!)";
            var sb = MyTexts.Get(MySpaceTexts.TerminalControlPanel_RunCodeDefault);
            RestoreRunCodeDefault = sb.ToString();
            if(RestoreRunCodeDefault.Contains(Suffix))
                RestoreRunCodeDefault = null; // somehow the suffix stuck around, let's not touch this one anymore
            else
                sb.Append(' ').Append(Suffix);
        }

        public override void UnregisterComponent()
        {
            MySteamConstants.URL_BROWSE_WORKSHOP_INGAMESCRIPTS_HELP = RestoreURL;

            if(RestoreRunCodeDefault != null)
            {
                var sb = MyTexts.Get(MySpaceTexts.TerminalControlPanel_RunCodeDefault);
                sb.Clear().Append(RestoreRunCodeDefault);
            }

            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;

            if(!Main.ComponentsRegistered)
                return;

            Main.TextAPI.Detected -= TextAPI_Detected;
            Main.GUIMonitor.ActiveScreenChanged -= ActiveScreenChanged;
        }

        void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            try
            {
                if(ModifiedTerminalControls)
                    return;

                var pb = block as IMyProgrammableBlock;
                if(pb == null)
                    return;

                ModifiedTerminalControls = true;
                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;

                foreach(var c in controls)
                {
                    var textBox = c as IMyTerminalControlTextbox;
                    if(textBox != null && c.Id == "ConsoleCommand")
                    {
                        textBox.Title = MyStringId.GetOrCompute(MyTexts.GetString(MySpaceTexts.TerminalControlPanel_RunArgument) + " (for Run button only)");
                        textBox.Tooltip = MyStringId.GetOrCompute(MyTexts.GetString(MySpaceTexts.TerminalControlPanel_RunArgument_ToolTip) +
                            "\nConsult the script's documentation for what commands can be entered here, if any." +
                            "\n\nThis text box is ignored if PB runs from any other source (self-run, toolbars, etc)." +
                            "\nThe \"Run with default argument\" toolbar action only works if host player writes in this textbox, therefore unreliable.");
                        break;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void ActiveScreenChanged(string prevScreenName, string screenName)
        {
            bool isPBEditor = screenName?.EndsWith(PBEditorSuffix) ?? false;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, isPBEditor);

            if(isPBEditor)
                RefreshPositions();
        }

        void TextAPI_Detected()
        {
            Tooltip = new TooltipHandler();

            Buttons = new Button[]
            {
                PBAPIGuide = new Button("PB scripting guide on the SE wiki", "Click to go to https://spaceengineers.wiki.gg/wiki/Scripting", Tooltip, (b) =>
                {
                    if(MyAPIGateway.Input.IsNewLeftMousePressed())
                        Utils.OpenLink("https://spaceengineers.wiki.gg/wiki/Scripting", external: true);
                }, pivot: Align.BottomLeft, directDraw: true, debugPivot: DebugPivots),

                UnclickableButtons = new Button("Can't click above buttons? Hold right-click first", "Click to open the bugreport for this issue", Tooltip, (b) =>
                {
                    if(MyAPIGateway.Input.IsNewLeftMousePressed())
                        Utils.OpenLink("https://support.keenswh.com/spaceengineers/pc/topic/42011-programmable-block-left-click-bug-on-any-button-on-the-edit-menu", external: true);
                }, pivot: Align.BottomLeft, directDraw: true, debugPivot: DebugPivots),

                LinuxCompile = new Button("On Linux the \"Check Code\" might never show any errors", null, Tooltip, (b) =>
                {
                }, pivot: Align.BottomLeft, directDraw: true, debugPivot: DebugPivots),
            };

            //RefreshPositions();
        }

        void RefreshPositions()
        {
            if(Buttons == null)
                return;

            Vector2D pos = new Vector2D(-0.6, -0.98); // TODO: configurable? then also needs to be draggable
            float scale = Main.Config.TerminalButtonsScale.Value;

            foreach(Button button in Buttons)
            {
                button.Scale = scale;
                button.Refresh(pos);
                pos += new Vector2D(button.Label.Background.Width + ((Button.EdgePadding * scale) / 4), 0);
            }

            Tooltip.Refresh(scale);
        }

        public override void UpdateDraw()
        {
            if(Buttons == null)
                return;

            // using GUI size because this is a real UI where we're tracking mouse
            Vector2 guiSize = MyAPIGateway.Input.GetMouseAreaSize();
            Vector2 mousePos = MyAPIGateway.Input.GetMousePosition() / guiSize;
            var mouseOnScreen = new Vector2D(mousePos.X * 2 - 1, 1 - 2 * mousePos.Y); // turn from 0~1 to -1~1

            PBAPIGuide.Update(mouseOnScreen);
            UnclickableButtons.Update(mouseOnScreen);

            if(BuildInfoMod.IsLinux)
                LinuxCompile.Update(mouseOnScreen);

            Tooltip.Draw(mouseOnScreen, drawNow: true);
        }
    }
}
