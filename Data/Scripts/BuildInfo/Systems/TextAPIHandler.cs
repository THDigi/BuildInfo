using System;
using System.Text;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;

namespace Digi.BuildInfo.Systems
{
    public class TextAPI : ModComponent
    {
        /// <summary>
        /// Triggered when TextAPI is detected.
        /// </summary>
        public event Action Detected;

        /// <summary>
        /// If TextAPI is detected and user didn't opt out.
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// If TextAPI was detected being installed and running.
        /// </summary>
        public bool WasDetected { get; private set; }

        public bool InModMenu { get; private set; }

        /// <summary>
        /// False if user chose to not allow TextAPI.
        /// </summary>
        public bool Use
        {
            get { return _use; }
            set
            {
                _use = value;
                IsEnabled = WasDetected && value;
                UseChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// Triggered when <see cref="Use"/> changes.
        /// </summary>
        public event Action<bool> UseChanged;

        private HudAPIv2 api;
        private bool _use = true;

        public TextAPI(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_INPUT; // mod menu is openable before detected event is sent
        }

        public override void RegisterComponent()
        {
            api = new HudAPIv2(TextAPIDetected);
        }

        public override void UnregisterComponent()
        {
            api?.Close();
            api = null;
        }

        private void TextAPIDetected()
        {
            try
            {
                if(WasDetected)
                {
                    Log.Error("TextAPI sent the register event twice now! Please report to TextAPI author.", Log.PRINT_MESSAGE);
                    return;
                }

                WasDetected = true;
                IsEnabled = Use;
                Detected?.Invoke();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(!InModMenu && MyAPIGateway.Gui.ChatEntryVisible && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.F2))
            {
                InModMenu = true;
            }

            if(InModMenu && !MyAPIGateway.Gui.ChatEntryVisible)
            {
                InModMenu = false;
            }
        }

        // TODO move to an extensions class ?
        HudAPIv2.HUDMessage measuringMsg;
        public Vector2D GetStringSize(StringBuilder text)
        {
            if(!WasDetected)
                throw new Exception("Requested GetStringSize() before textAPI was available!");

            if(measuringMsg == null)
            {
                measuringMsg = new HudAPIv2.HUDMessage();
                measuringMsg.Visible = false;
            }

            measuringMsg.Message = text;
            return measuringMsg.GetTextLength();
        }

        public static void CopyWithoutColor(StringBuilder text, StringBuilder shadow)
        {
            shadow.Clear();
            shadow.EnsureCapacity(text.Length);

            // append to shadow without color tags
            for(int i = 0; i < text.Length; ++i)
            {
                char c = text[i];

                // skip <color=...>
                if(c == '<' && i + 6 <= text.Length)
                {
                    if(text[i + 1] == 'c'
                    && text[i + 2] == 'o'
                    && text[i + 3] == 'l'
                    && text[i + 4] == 'o'
                    && text[i + 5] == 'r'
                    && text[i + 6] == '=')
                    {
                        // seek ahead for end char
                        int endChar = -1;
                        for(int s = i + 6; s < text.Length; s++)
                        {
                            if(text[s] == '>')
                            {
                                endChar = s;
                                break;
                            }
                        }

                        if(endChar != -1)
                        {
                            i = endChar;
                            continue;
                        }
                    }
                }

                shadow.Append(c);
            }
        }
    }
}
