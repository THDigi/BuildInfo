using System;
using System.Text;
using Digi.BuildInfo.Features;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Systems
{
    public class TextAPI : ModComponent
    {
        public const string DefaultFont = FontsHandler.SEOutlined;
        public const bool DefaultUseShadow = false;
        public const BlendTypeEnum DefaultHUDBlendType = BlendTypeEnum.PostPP;
        public const BlendTypeEnum DefaultWorldBlendType = BlendTypeEnum.Standard;

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

        HudAPIv2.HUDMessage measuringMsg;
        public Vector2D GetStringSize(StringBuilder text)
        {
            if(!WasDetected)
                throw new Exception("GetStringSize() was calledbefore TextAPI was available!");

            if(measuringMsg == null)
            {
                measuringMsg = new HudAPIv2.HUDMessage();
                measuringMsg.Visible = false;
            }

            measuringMsg.Message = text;
            return measuringMsg.GetTextLength();
        }

        /// <summary>
        /// NOTE: Starts off invisible.
        /// </summary>
        public static HudAPIv2.HUDMessage CreateHUDText(StringBuilder sb, Vector2D hudPos, double scale = 1, bool hideWithHud = true)
        {
            HudAPIv2.HUDMessage msg = new HudAPIv2.HUDMessage(sb, hudPos, Scale: scale, HideHud: hideWithHud, Shadowing: false, Font: DefaultFont, Blend: DefaultHUDBlendType);
            msg.Visible = false;
            return msg;
        }

        /// <summary>
        /// NOTE: Starts off invisible.
        /// </summary>
        public static HudAPIv2.BillBoardHUDMessage CreateHUDTexture(MyStringId material, Color color, Vector2D hudPos, bool hideWithHud = true)
        {
            HudAPIv2.BillBoardHUDMessage msg = new HudAPIv2.BillBoardHUDMessage(material, hudPos, color, HideHud: hideWithHud, Shadowing: false, Blend: DefaultHUDBlendType);
            msg.Visible = false;
            return msg;
        }

        public class TextPackage
        {
            public readonly StringBuilder TextStringBuilder;
            public readonly StringBuilder ShadowStringBuilder;
            public readonly HudAPIv2.HUDMessage Text;
            public readonly HudAPIv2.HUDMessage Shadow;
            public readonly HudAPIv2.BillBoardHUDMessage Background;

            /// <summary>
            /// NOTE: Starts off invisible.
            /// NOTE: The given StringBuilder is used on both text and shadow. Use the other constructor for it to make one per text.
            /// </summary>
            public TextPackage(StringBuilder sb, bool useShadow = DefaultUseShadow, MyStringId? backgroundTexture = null)
            {
                if(backgroundTexture.HasValue)
                {
                    Background = CreateHUDTexture(backgroundTexture.Value, Color.White, Vector2D.Zero);
                }

                if(useShadow)
                {
                    ShadowStringBuilder = sb;
                    Shadow = CreateHUDText(ShadowStringBuilder, Vector2D.Zero);
                    Shadow.InitialColor = Color.Black;
                    Shadow.Offset = new Vector2D(0.002, -0.002);
                }

                TextStringBuilder = sb;
                Text = CreateHUDText(TextStringBuilder, Vector2D.Zero);
            }

            /// <summary>
            /// NOTE: Starts off invisible.
            /// </summary>
            public TextPackage(int initialSBSize, bool useShadow = DefaultUseShadow, MyStringId? backgroundTexture = null)
            {
                if(backgroundTexture.HasValue)
                {
                    Background = CreateHUDTexture(backgroundTexture.Value, Color.White, Vector2D.Zero);
                }

                if(useShadow)
                {
                    ShadowStringBuilder = new StringBuilder(initialSBSize);
                    Shadow = CreateHUDText(ShadowStringBuilder, Vector2D.Zero);
                    Shadow.InitialColor = Color.Black;
                    Shadow.Offset = new Vector2D(0.002, -0.002);
                }

                TextStringBuilder = new StringBuilder(initialSBSize);
                Text = CreateHUDText(TextStringBuilder, Vector2D.Zero);
            }

            public bool Visible
            {
                get { return Text.Visible; }
                set
                {
                    Text.Visible = value;

                    if(Shadow != null)
                        Shadow.Visible = value;

                    if(Background != null)
                        Background.Visible = value;
                }
            }

            public double Scale
            {
                get { return Text.Scale; }
                set
                {
                    Text.Scale = value;

                    if(Shadow != null)
                        Shadow.Scale = value;
                }
            }

            public Vector2D Position
            {
                get { return Text.Origin; }
                set
                {
                    Text.Origin = value;

                    if(Shadow != null)
                        Shadow.Origin = value;

                    if(Background != null)
                        Background.Origin = value;
                }
            }

            public bool HideWithHUD
            {
                get { return (Text.Options & HudAPIv2.Options.HideHud) != 0; }
                set
                {
                    if(value)
                    {
                        Text.Options |= HudAPIv2.Options.HideHud;

                        if(Shadow != null)
                            Shadow.Options |= HudAPIv2.Options.HideHud;

                        if(Background != null)
                            Background.Options |= HudAPIv2.Options.HideHud;
                    }
                    else
                    {
                        Text.Options &= ~HudAPIv2.Options.HideHud;

                        if(Shadow != null)
                            Shadow.Options &= ~HudAPIv2.Options.HideHud;

                        if(Background != null)
                            Background.Options &= ~HudAPIv2.Options.HideHud;
                    }
                }
            }

            public string Font
            {
                get { return Text.Font; }
                set
                {
                    Text.Font = value;

                    if(Shadow != null)
                        Shadow.Font = value;
                }
            }
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
