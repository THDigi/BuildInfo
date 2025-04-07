using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Library.Utils;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;
using VRageRender;
using static Digi.BuildInfo.Systems.TextAPI;
using ExperimentalReason = VRage.Game.MyObjectBuilder_SessionSettings.ExperimentalReason;
using TypeExtensions = VRage.TypeExtensions; // HACK: some people have ambiguity on this, probably linux or such

namespace Digi.BuildInfo.Features.GUI
{
    public class ServerInfoMenu : Menu
    {
        public static void Test()
        {
            try
            {
                if(BuildInfoMod.IsDevMod)
                {
                    ServerInfoMenu menu = new ServerInfoMenu(testMode: true);
                    menu.CheckSettings();
                    //Menu.Dispose();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        bool Visible;

        HashSet<string> KnownFields = new HashSet<string>();
        bool TestRun = false;
        MyObjectBuilder_SessionSettings DefaultSettings;
        string DefaultFrom;
        MyEnvironmentDefinition DefaultEnvDef = null;

        HudAPIv2.BillBoardHUDMessage WindowBG;
        Button CloseButton;
        //HudAPIv2.BillBoardHUDMessage ButtonDebug;
        Vector2D PrevMousePos;
        Column.Tooltip? HoveredTooltip = null;
        ITooltipHandler TooltipHandler;
        HudAPIv2.BillBoardHUDMessage TooltipSelectionBox;

        Column[] Columns = new Column[4];
        Column CurrentColumn;
        int ColumnIndex;

        ScrollableSection ScrollableBlockLimits = new ScrollableSection(10);
        ScrollableSection ScrollableModsList = new ScrollableSection(50);
        ScrollableSection ScrollableWarnings = new ScrollableSection(10);
        List<ScrollableSection> ScrollableSections;

        BuildInfoMod Main;

        static float LineHeight;
        static float SpaceWidth;

        const double TextScale = 0.8;
        const string LabelPrefix = "  ";
        static readonly Color HeaderColor = new Color(155, 220, 255);
        static readonly Color LabelColor = new Color(230, 240, 255);
        static readonly Color LabelColorDisabled = Color.Gray;
        static readonly Color ValueColorDefault = new Color(200, 255, 200);
        static readonly Color ValueColorChanged = new Color(255, 230, 180);
        static readonly Color ValueColorDisabled = Color.Gray;
        static readonly Color ValueColorWarning = new Color(255, 60, 25);
        static readonly Color NewSettingColor = new Color(100, 255, 155);
        static readonly Color SearchBgColor = new Color(60, 76, 82);
        const bool DebugDrawBoxes = false;
        const float CloseButtonScale = 1.2f;

        const string NewSettingTag = "NEW:";

        bool ShowInternal => true; // Main.Config.InternalInfo.Value;

        const string TooltipSettingModdable = "\n<color=gray>This is only changeable using mods.<reset>";
        //const string TooltipSettingGameUI = "\n<color=gray>This is a world setting. This in particular can be changed in the world options screen.<reset>";
        //const string TooltipSettingDSUI = "\n<color=gray>This is a world setting. This in particular can be changed in dedicated server UI or in sandbox_config.sbc file.<reset>";
        //const string TooltipSettingSaveFile = "\n<color=gray>This is a world setting. This in particular can only be changed in the sandbox_config.sbc file.<reset>";

        public ServerInfoMenu(bool testMode = false)
        {
            Main = BuildInfoMod.Instance;

            if(!testMode || BuildInfoMod.IsDevMod)
                ReadDefaults();
        }

        void ReadDefaults()
        {
            const string WorldForDefaults = @"CustomWorlds\Star System\sandbox_config.sbc";
            var worldConfig = ReadGameXML<MyObjectBuilder_WorldConfiguration>(WorldForDefaults);
            if(worldConfig != null)
            {
                DefaultSettings = worldConfig.Settings;
                DefaultFrom = "Star System template";
            }
            else
            {
                DefaultSettings = new MyObjectBuilder_SessionSettings();
                DefaultFrom = "(ERROR)";
                // errors will be logged by ReadGameXML() 
            }

            const string EnvSBC = @"Data\Environment.sbc";
            var envDef = ReadGameXML<MyObjectBuilder_Definitions>(EnvSBC);
            if(envDef != null)
            {
                var defOB = envDef.Definitions[0] as MyObjectBuilder_EnvironmentDefinition;
                if(defOB != null)
                {
                    DefaultEnvDef = new MyEnvironmentDefinition();
                    DefaultEnvDef.Init(defOB, MyModContext.BaseGame);
                }
                else
                {
                    Log.Error($"Game's '{EnvSBC}' does not contain the expected EnvironmentDefinition!");
                }
            }

            if(DefaultEnvDef == null)
                DefaultEnvDef = MyDefinitionManager.Static.EnvironmentDefinition;
        }

        /*
        public void Dispose()
        {
            if(WindowBG == null)
                return;

            WindowBG.DeleteMessage();
            WindowBG = null;

            foreach(Column column in Columns)
            {
                column.Render.Text.DeleteMessage();
            }

            Columns = null;

            foreach(ScrollableSection section in ScrollableSections)
            {
                section.Dispose();
            }
            ScrollableSections = null;

            CloseButton.Dispose();
            CloseButton = null;

            TooltipRender.Dispose();
            TooltipRender = null;
        }
        */

        void CreateUIObjects()
        {
            MyStringId material = Constants.MatUI_Square;

            //Color bgColor = new Color(41, 54, 62);
            Color bgColor = new Color(37, 46, 53);

            WindowBG = TextAPI.CreateHUDTexture(material, bgColor, Vector2D.Zero, false);

            for(int i = 0; i < Columns.Length; i++)
            {
                Columns[i] = new Column(DebugDrawBoxes);
            }

            Columns[0].Render.TextStringBuilder.Append("aAqQjJ!W");
            LineHeight = (float)Math.Abs(Columns[0].Render.Text.GetTextLength().Y);
            Columns[0].Render.TextStringBuilder.Append(" ");
            SpaceWidth = (float)Math.Abs(Columns[0].Render.Text.GetTextLength().Y);

            ScrollableSections = new List<ScrollableSection>()
            {
                ScrollableBlockLimits,
                ScrollableModsList,
                ScrollableWarnings,
            };

            foreach(ScrollableSection section in ScrollableSections)
            {
                section.CreateUIObjects();
            }

            CloseButton = new Button("Close",
                tooltip: null, tooltipHandler: null,
                hover: (button) =>
                {
                    if(MyAPIGateway.Input.IsNewLeftMouseReleased())
                        CloseMenu();
                },
                hoverEnd: null,
                pivot: Align.BottomRight);
            //CloseButton.DefaultColor = new Color(155, 155, 155);
            CloseButton.Scale = CloseButtonScale;
            CloseButton.Refresh(Vector2D.Zero);

            SearchBar = new TextPackage(128, false, Constants.MatUI_Square);
            SearchBar.Background.BillBoardColor = SearchBgColor;
            SearchBar.HideWithHUD = false;
            SearchBar.Position = new Vector2D(-0.9, 0.4);
            SearchBar.Font = FontsHandler.TextAPI_OutlinedFont;

            TooltipHandler = new TooltipHandler();

            TooltipSelectionBox = TextAPI.CreateHUDTexture(material, Color.Lime * 0.2f, Vector2D.Zero);
        }

        public void ToggleMenu()
        {
            try
            {
                if(Visible)
                {
                    CloseMenu();
                    return;
                }

                if(!Main.TextAPI.WasDetected)
                {
                    Utils.ShowColoredChatMessage(Log.ModName, "TextAPI not yet initialized, please wait... or bugreport if it persists.", FontsHandler.YellowSh);
                    return;
                }

                TestRun = false;

                if(WindowBG == null)
                    CreateUIObjects();

                GenerateMenuContents();

                Vector2D pxSize = HudAPIv2.APIinfo.ScreenPositionOnePX;

                const double PosX = 0.15;
                const float BorderPaddingPx = 20; // on each side
                const float ColumnSpacingPx = 16; // between columns only
                Vector2D columnSize = new Vector2D(0, 0);
                Vector2D windowSize = new Vector2D(0, 0);

                for(int i = 0; i < Columns.Length; i++)
                {
                    Column column = Columns[i];
                    if(!column.Render.Visible)
                        break;

                    column.TextSize = column.Render.Text.GetTextLength();

                    column.Render.Position = new Vector2D(PosX + columnSize.X, 0);

                    columnSize.X += column.TextSize.X + pxSize.X * ColumnSpacingPx;
                    columnSize.Y = -column.TextSize.Y;

                    windowSize = Vector2D.Max(windowSize, columnSize);
                }

                windowSize.X -= pxSize.X * ColumnSpacingPx; // remove last column space

                for(int i = 0; i < Columns.Length; i++)
                {
                    Column column = Columns[i];
                    if(!column.Render.Visible)
                        break;

                    column.Render.Position -= new Vector2D(windowSize.X / 2, windowSize.Y / -2);

                    if(DebugDrawBoxes)
                        column.Render.UpdateBackgroundSize(0f);
                }

                // with close button centered and enlarging window
                /*
                float closeButtonHeight = (float)Math.Abs(CloseButton.Label.Text.GetTextLength().Y) + BorderPadding;

                WindowBG.Origin = new Vector2D(PosX, -(closeButtonHeight - Padding));
                WindowBG.Width = (float)windowSize.X + Padding;
                WindowBG.Height = (float)windowSize.Y + Padding + closeButtonHeight + Padding;
                WindowBG.Visible = true;

                Vector2D closePos = new Vector2D(PosX, -(WindowBG.Height / 2 + closeButtonHeight - Padding - Padding));
                CloseButton.Refresh(closePos, CloseButtonScale);
                CloseButton.Visible = true;
                CloseButton.Label.Visible = true;
                */

                WindowBG.Origin = new Vector2D(PosX, 0); // right-offset to reduce chat overlap
                WindowBG.Width = (float)(windowSize.X + pxSize.X * BorderPaddingPx * 2);
                WindowBG.Height = (float)(windowSize.Y + pxSize.Y * BorderPaddingPx * 2);
                WindowBG.Visible = true;

                // bottom-left
                //Vector2D closePos = WindowBG.Origin - new Vector2D(WindowBG.Width, -WindowBG.Height) / 2;
                //closePos += new Vector2D(CloseButton.Label.Background.Width, 0);
                //closePos += pxSize * BorderPaddingPx;

                Vector2D closePos = WindowBG.Origin + new Vector2D(WindowBG.Width, -WindowBG.Height) / 2;
                closePos += new Vector2D(-pxSize.X * BorderPaddingPx, pxSize.Y * BorderPaddingPx);

                //if(ButtonDebug == null)
                //{
                //    ButtonDebug = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("Square"), Vector2D.Zero, Color.Red);
                //    ButtonDebug.Width = (float)pxSize.X * 4;
                //    ButtonDebug.Height = (float)Math.Abs(pxSize.Y) * 4;
                //}
                //ButtonDebug.Origin = closePos;

                CloseButton.Scale = CloseButtonScale;
                CloseButton.Refresh(closePos);
                CloseButton.SetVisible(true);

                Main.MenuHandler.AddCursorRequest(GetType().Name,
                    escapeCallback: () => CloseMenu(escPressed: true),
                    blockMoveAndRoll: true,
                    blockViewXY: true,
                    blockClicks: true);

                Main.MenuHandler.SetUpdateMenu(this, true);
                Visible = true;
            }
            catch(Exception e)
            {
                Log.Error(e);

                CloseMenu(false);
            }
        }

        void CloseMenu(bool escPressed = false)
        {
            Visible = false;
            Main.MenuHandler.RemoveCursorRequest(GetType().Name);
            Main.MenuHandler.SetUpdateMenu(this, false);

            if(WindowBG == null)
                return;

            WindowBG.Visible = false;
            CloseButton.SetVisible(false);
            TooltipHandler.SetVisible(false);
            TooltipSelectionBox.Visible = false;

            foreach(ScrollableSection section in ScrollableSections)
            {
                section.SetVisible(false, false);
            }

            foreach(Column column in Columns)
            {
                column.Reset();
            }

            HideHighlighters();
            SearchBarClosed();
        }

        public override void UpdateDraw()
        {
            Vector2D mousePos = MenuHandler.GetMousePositionGUI();

            CloseButton.Update(mousePos);

            bool scrolled = false;

            foreach(ScrollableSection section in ScrollableSections)
            {
                scrolled |= section.Update(mousePos);
            }

            if(scrolled || PrevMousePos != mousePos)
            {
                PrevMousePos = mousePos;
                HoveredTooltip = null;
                UpdateTooltip(mousePos);
            }

            if(!MyAPIGateway.Gui.IsCursorVisible)
            {
                if(HoveredTooltip?.ClickAction != null && MyAPIGateway.Input.IsNewLeftMousePressed())
                {
                    HoveredTooltip.Value.ClickAction.Invoke();
                }
            }

            if(MyAPIGateway.Gui.ChatEntryVisible)
            {
                ListReader<char> input = MyAPIGateway.Input.TextInput;
                if(input.Count > 0 || !SearchBar.Visible)
                {
                    ChatTyped(input);
                }
                else if(scrolled)
                {
                    SearchText();
                }
            }
            else
            {
                SearchBarClosed();
            }
        }

        #region In-window searching
        const int MinCharsToSearch = 2;
        int HighlighterIndex = -1;
        List<HudAPIv2.BillBoardHUDMessage> Highlighters = new List<HudAPIv2.BillBoardHUDMessage>();
        List<char> TextInput = new List<char>(64);
        TextPackage SearchBar;
        HashSet<Vector2I> LinesHighlighted = new HashSet<Vector2I>();

        void ChatTyped(ListReader<char> input)
        {
            foreach(char c in input)
            {
                // TODO: maybe some day we'll get the exact chat text, but right now we only have hax
                if(char.IsControl(c))
                {
                    if(c == '\r')
                        continue;

                    if(c == 1) // ctrl+A
                    {
                        TextInput.Clear();
                        break;
                    }

                    if(c == '\b')
                    {
                        if(TextInput.Count > 0)
                            TextInput.RemoveAt(TextInput.Count - 1);
                    }

                    continue;
                }

                TextInput.Add(c);
            }

            StringBuilder sb = SearchBar.TextStringBuilder.Clear();
            sb.Append("Searching for: '");
            foreach(char c in TextInput)
                sb.Append(c);
            sb.Append("'");

            if(TextInput.Count < MinCharsToSearch)
                sb.Append(" <color=gray>(min ").Append(MinCharsToSearch).Append(")");

            SearchBar.Visible = true;
            SearchBar.UpdateBackgroundSize();

            SearchText();
        }

        void SearchBarClosed()
        {
            TextInput.Clear();

            if(SearchBar != null)
                SearchBar.Visible = false;
        }

        void HideHighlighters()
        {
            for(int i = 0; i <= HighlighterIndex; i++)
            {
                Highlighters[i].Visible = false;
            }

            HighlighterIndex = -1;
        }

        void SearchText()
        {
            HideHighlighters();

            if(WindowBG == null)
                return;

            if(TextInput.Count < MinCharsToSearch)
                return;

            LinesHighlighted.Clear();

            string findTextUpper = string.Join("", TextInput).ToUpperInvariant();
            int findLength = findTextUpper.Length;

            for(int columnIdx = 0; columnIdx < Columns.Length; columnIdx++)
            {
                Column column = Columns[columnIdx];

                // search content
                {
                    StringBuilder sb = column.Render.TextStringBuilder;

                    int line = 0;
                    int maxSearchLength = (sb.Length - findLength) + 1;

                    for(int i = 0; i < maxSearchLength; i++)
                    {
                        char chr = sb[i];

                        if(chr == '\n')
                        {
                            line++;
                            continue;
                        }

                        #region skip over TextAPI formatting
                        if(chr == '<')
                        {
                            int x = i;

                            if(i + 6 <= sb.Length)
                            {
                                if(sb[++x] == 'c'
                                && sb[++x] == 'o'
                                && sb[++x] == 'l'
                                && sb[++x] == 'o'
                                && sb[++x] == 'r'
                                && sb[++x] == '=')
                                {
                                    // seek ahead for end char
                                    int endChar = -1;
                                    for(int s = i + 6; s < sb.Length; s++)
                                    {
                                        if(sb[s] == '>')
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

                            if(SkipOverString(sb, ref i, "<reset>")
                            || SkipOverString(sb, ref i, "<i>")
                            || SkipOverString(sb, ref i, "</i>"))
                                continue;
                        }
                        #endregion

                        if(char.ToUpperInvariant(chr) == findTextUpper[0])
                        {
                            int foundChars = 1;
                            while(foundChars < findLength)
                            {
                                if(char.ToUpperInvariant(sb[i + foundChars]) != findTextUpper[foundChars])
                                    break;

                                foundChars++;
                            }

                            if(foundChars == findLength)
                            {
                                LinesHighlighted.Add(new Vector2I(columnIdx, line));
                                HighlightLine(column, line);

                                // we got a match on this line, now skip to next line to avoid re-highlighting this one
                                int lineEnd = sb.IndexOf('\n', i);
                                if(lineEnd == -1)
                                    break;

                                // -1 required so that the next iteration lands on \n and executes the new line condition
                                i = lineEnd - 1;
                                continue;
                            }
                        }
                    }
                }

                // search tooltips too
                foreach(KeyValuePair<int, Column.Tooltip> kv in column.Tooltips)
                {
                    int line = kv.Key;
                    Vector2I id = new Vector2I(columnIdx, line);
                    if(LinesHighlighted.Contains(id))
                        continue;

                    string tooltipText = kv.Value.Text;

                    if(tooltipText.IndexOf(findTextUpper, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        LinesHighlighted.Add(id);
                        HighlightLine(column, line);
                    }
                }
            }
        }

        static bool SkipOverString(StringBuilder sb, ref int i, string str)
        {
            if(sb.IndexOf(str, i, i + str.Length - 1, true) != -1)
            {
                i += str.Length - 1;
                return true;
            }

            return false;
        }

        void HighlightLine(Column column, int line)
        {
            Vector2D columnMin = column.Render.Text.Origin + column.Render.Text.Offset;
            Vector2D columnMax = columnMin + column.TextSize;
            BoundingBox2D columnBB = new BoundingBox2D(Vector2D.Min(columnMin, columnMax), Vector2D.Max(columnMin, columnMax));

            Vector2D start = new Vector2D(columnBB.Min.X, columnBB.Max.Y - ((line + 1) * LineHeight));
            var area = new BoundingBox2D(start, start + new Vector2D(columnBB.Size.X, LineHeight));

            HudAPIv2.BillBoardHUDMessage hl;

            HighlighterIndex++;
            if(Highlighters.Count <= HighlighterIndex)
            {
                hl = new HudAPIv2.BillBoardHUDMessage(Constants.MatUI_Square, Vector2D.Zero, Color.Yellow * 0.25f);
                Highlighters.Add(hl);
            }
            else
            {
                hl = Highlighters[HighlighterIndex];
            }

            hl.Origin = area.Center;
            hl.Width = (float)area.Width;
            hl.Height = (float)area.Height;
            hl.Visible = true;
        }
        #endregion

        void UpdateTooltip(Vector2D mousePos)
        {
            BoundingBox2D highlightArea = default(BoundingBox2D);

            for(int i = 0; i < Columns.Length; i++)
            {
                Column column = Columns[i];
                Vector2D columnMin = column.Render.Text.Origin + column.Render.Text.Offset;
                Vector2D columnMax = columnMin + column.TextSize;
                BoundingBox2D columnBB = new BoundingBox2D(Vector2D.Min(columnMin, columnMax), Vector2D.Max(columnMin, columnMax));

                if(columnBB.Contains(mousePos) == ContainmentType.Disjoint)
                    continue;

                int line = (int)Math.Ceiling(Math.Abs(mousePos.Y - columnMin.Y) / LineHeight) - 1;

                Column.Tooltip tooltip;
                if(column.Tooltips.TryGetValue(line, out tooltip))
                {
                    HoveredTooltip = tooltip;

                    Vector2D start = new Vector2D(columnBB.Min.X, columnBB.Max.Y - ((line + 1) * LineHeight));
                    highlightArea = new BoundingBox2D(start, start + new Vector2D(columnBB.Size.X, LineHeight));

                    break;
                }
            }

            if(HoveredTooltip == null && SearchBar != null && SearchBar.Visible)
            {
                Vector2D center = SearchBar.Background.Origin + SearchBar.Background.Offset;
                Vector2D halfExtent = new Vector2D(SearchBar.Background.Width, SearchBar.Background.Height) * 0.5;
                BoundingBox2D bb = new BoundingBox2D(center - halfExtent, center + halfExtent);

                if(bb.Contains(mousePos) != ContainmentType.Disjoint)
                {
                    HoveredTooltip = new Column.Tooltip()
                    {
                        Text = "See exactly what is searched for when using chat.\nThis does not match 1:1 with chat because I'd have to reimplement all the textbox input features like arrows, clicking, etc.",
                    };

                    highlightArea = bb;
                }
            }

            //if(HoveredTooltip == null)
            //{
            //    HoveredTooltip = new Tooltip()
            //    {
            //        Text = "Random tooltip\nWith newlines\nAnd really long lines or whatever else we might think of here to write to make it long yes.",
            //    };
            //}

            if(HoveredTooltip != null)
            {
                TooltipSelectionBox.Origin = highlightArea.Center;
                TooltipSelectionBox.Width = (float)highlightArea.Width;
                TooltipSelectionBox.Height = (float)highlightArea.Height;
                TooltipSelectionBox.Visible = true;

                TooltipHandler.Hover(HoveredTooltip.Value.Text);
                TooltipHandler.Draw(mousePos, drawNow: false);
                TooltipHandler.SetVisible(true);
            }
            else
            {
                TooltipSelectionBox.Visible = false;

                TooltipHandler.HoverEnd();
                TooltipHandler.SetVisible(false);
            }
        }

        void GenerateMenuContents()
        {
            ResetFormat();

            AppendSettings();

            StringBuilder sb = NextColumn();

            Header(sb, "Mods");
            CurrentColumn.SetTooltip(0, "Mods at the top are loaded last therefore they override other ones below them." +
                                       "\nNote: in files like sandbox_config.sbc the mods order is the load order, flipped compared to the GUI and here.");

            List<MyObjectBuilder_Checkpoint.ModItem> mods = MyAPIGateway.Session.Mods;

            if(mods.Count == 0)
            {
                sb.Append("<color=gray>(No mods)");

                CurrentColumn.AddTooltip(sb, "No mods in the actual server/world, but clearly this mod is here which means it's brought in by PluginLoader.");

                sb.Append('\n');
            }
            else
            {
                ScrollableModsList.Reset();
                int sbIndex = sb.Length;

#if false // for testing mods list
            {
                int totalMods = 167 - mods.Count;
                var fakeMods = new List<MyObjectBuilder_Checkpoint.ModItem>(totalMods + mods.Count);

                for(int i = 0; i <= totalMods; i++)
                {
                    string modName = "";
                    int len = MyRandom.Instance.Next(5, 100);
                    for(int n = 0; n < len; n++)
                    {
                        if(MyRandom.Instance.Next(0, 100) <= 10)
                            modName += ' ';
                        else
                            modName += (char)MyRandom.Instance.Next('a', 'z');
                    }

                    fakeMods.Add(new MyObjectBuilder_Checkpoint.ModItem()
                    {
                        FriendlyName = modName,
                        IsDependency = MyRandom.Instance.Next(0, 100) <= 10,
                        Name = modName,
                        PublishedFileId = MyRandom.Instance.Next(0, 100) <= 10 ? 0 : (ulong)MyRandom.Instance.NextLong(),
                        PublishedServiceName = "steam",
                    });
                }

                fakeMods.AddList(mods);
                mods = fakeMods;
            }
#endif

                bool scrollMods = mods.Count > ScrollableModsList.DisplayLines;

                mods.Reverse(); // to match the GUI

                for(int i = 0; i < mods.Count; i++)
                {
                    MyObjectBuilder_Checkpoint.ModItem mod = mods[i];

                    string tooltip = mod.FriendlyName + "\n"
                                   + (mod.PublishedFileId != 0 ? $"{mod.PublishedServiceName}:{mod.PublishedFileId}\nClick to open workshop page" : "Local mod")
                                   + (mod.IsDependency ? "\n(Mod added by another mod as dependency)" : "");

                    Action clickAction = null;

                    if(mod.PublishedFileId > 0)
                    {
                        clickAction = () => Utils.OpenModPage(mod.PublishedServiceName, mod.PublishedFileId);
                    }

                    int startIdx = sb.Length;
                    sb.Append(LabelPrefix).Append(i + 1).Append(". ").AppendMaxLength(mod.FriendlyName, 32);

                    if(scrollMods)
                    {
                        int len = sb.Length - startIdx;
                        ScrollableModsList.Add(sb.ToString(startIdx, len), tooltip, clickAction);
                        sb.Length -= len; // erase!
                    }
                    else
                    {
                        CurrentColumn.AddTooltip(sb, tooltip, clickAction);

                        sb.Append('\n');
                    }
                }

                ScrollableModsList.Finish(CurrentColumn, sbIndex);
            }

            FinishColumnFormat();
        }

        string GetExperimentalTooltip()
        {
            //if(MyCampaignManager.Static.IsCampaignRunning)
            //    return "Experimental reasons:\n(None because this is a campaign)";

            StringBuilder sb = new StringBuilder(512);

            sb.Append("Experimental reasons:\n");

            // unsure what remote does but currently it's identical regardless
            ExperimentalReason reasons = MyAPIGateway.Session.SessionSettings.GetExperimentalReason(remote: false);

            if(MyAPIGateway.Session.Mods.Count > 0)
                reasons |= ExperimentalReason.Mods;

            foreach(ExperimentalReason reason in MyEnum<ExperimentalReason>.Values)
            {
                if(reason == ExperimentalReason.ReasonMax // these are just all flags enabled, not useful here
                || reason == ExperimentalReason.ExperimentalTurnedOnInConfiguration) // not assigned in this getter, only in MySession.GetSettingsExperimentalReason()
                    continue;

                bool? on = (reasons & reason) != 0;
                if(reason == ExperimentalReason.Plugins
                || reason == ExperimentalReason.InsufficientHardware)
                    on = null;

                string name;
                switch(reason)
                {
                    // HACK: hardcodedly filled in details from GetExperimentalReason() and GetSettingsExperimentalReason()
                    case ExperimentalReason.Mods: name = "Mods present"; break;
                    case ExperimentalReason.Plugins: name = "DS plugins active"; break;
                    case ExperimentalReason.InsufficientHardware: name = "DS insufficient hardware"; break;
                    case ExperimentalReason.ExperimentalMode: name = "Experimental mode forced on"; break;
                    case ExperimentalReason.AdaptiveSimulationQuality: name = "AdaptiveSimulation OFF"; break;
                    case ExperimentalReason.BlockLimitsEnabled: name = "BlockLimits OFF"; break;
                    case ExperimentalReason.ProceduralDensity: name = "ProceduralDensity >0.35"; break;
                    case ExperimentalReason.MaxFloatingObjects: name = "MaxFloatingObjects >100"; break;
                    case ExperimentalReason.PhysicsIterations: name = "PhysicsIterations not 8"; break;
                    case ExperimentalReason.TotalPCU: name = $"TotalPCU >{MyObjectBuilder_SessionSettings.MaxSafePCU.ToString(NumberFormat)} for lobbies or >{MyObjectBuilder_SessionSettings.MaxSafePCU_Remote.ToString(NumberFormat)} for DS"; break;
                    case ExperimentalReason.MaxPlayers: name = $"MaxPlayers is 0 or >{MyObjectBuilder_SessionSettings.MaxSafePlayers.ToString(NumberFormat)} for lobbies or >{MyObjectBuilder_SessionSettings.MaxSafePlayers_Remote.ToString(NumberFormat)} for DS"; break;
                    case ExperimentalReason.SunRotationIntervalMinutes: name = "SunRotationIntervalMinutes <=29"; break;
                    case ExperimentalReason.TotalBotLimit: name = "TotalBotLimit >32"; break;
                    case ExperimentalReason.EnableIngameScripts: name = "Programmable Block scripts ON"; break;
                    case ExperimentalReason.EnableSubgridDamage: name = "Sub-Grid Physical Damage ON"; break;
                    case ExperimentalReason.EnableSpectator: name = "Everyone Spectator Camera ON"; break;
                    case ExperimentalReason.StationVoxelSupport: name = "Maintain static on split ON (StationVoxelSupport)"; break;
                    //case ExperimentalReason.SyncDistance: name = "SyncDistance not 3000"; break;
                    default: name = MyEnum<ExperimentalReason>.GetName(reason); break;
                }

                sb.Append("  ").Append(on == null ? "[?] " : on.Value ? "[x] " : "[  ] ").Append(name);
                if(on == null)
                    sb.Append(" (cannot be determined)");
                sb.Append("\n");
            }

            return sb.ToString();
        }

        void AppendSettings()
        {
            const string UndisclosedSettingsList = "\n- SyncDistance" +
                                                   "\n- OptimalSpawnDistance" +
                                                   "\n- MaxBackupSaves" +
                                                   "\n- ResetOwnership (would always be false anyway)" +
                                                   "\n- EnableSaving" +
                                                   "\n- MinimumWorldSize (it's completely useless)";

            if(TestRun)
            {
                //PrintTrashFlags(sb, nameof(settings.TrashFlagsValue), (MyTrashRemovalFlags)settings.TrashFlagsValue, (MyTrashRemovalFlags)DefSettings.TrashFlagsValue, false,
                //    "Trash Removal Flags", "Defines flags for trash removal system.", () => settings.TrashRemovalEnabled);
                KnownFields.Add("TrashFlagsValue"); // individual relevant flags are shown instead

                //PrintSetting(sb, nameof(settings.MaxBackupSaves), settings.MaxBackupSaves, defaults.MaxBackupSaves, false,
                //    "Max Backup Saves", "The maximum number of backup saves.");
                KnownFields.Add("MaxBackupSaves");

                //PrintSetting(sb, nameof(settings.ResetOwnership), settings.ResetOwnership, defaults.ResetOwnership, false,
                //    "Reset Ownership", "");
                KnownFields.Add("ResetOwnership");

                //PrintSetting(sb, nameof(settings.EnableSaving), settings.EnableSaving, defaults.EnableSaving, false,
                //    "Allow saving from menu", "Enables saving from the menu.");
                KnownFields.Add("EnableSaving");

                //PrintFormattedNumber(sb, nameof(settings.FloraDensityMultiplier), settings.FloraDensityMultiplier, DefaultSettings.FloraDensityMultiplier, true,
                //    "Flora Density Multiplier", "x", "");
                KnownFields.Add("FloraDensityMultiplier"); // unused

                //PrintSetting(sb, nameof(settings.MinimumWorldSize), settings.MinimumWorldSize, DefaultSettings.MinimumWorldSize, false, "Minimum world size [km]", "World size can't be selected lower than this value");
                KnownFields.Add("MinimumWorldSize"); // does not affect anything in the actual world, it only affects world's creation GUI

                KnownFields.Add("OptimalSpawnDistance"); // it's not ok for players to know this
                KnownFields.Add("SyncDistance"); // server owners would not like this exposed

                // unknown purpose, used in MySpaceRespawnComponent.UpdateBeforeSimulation()
                //PrintSetting(sb, nameof(settings.UpdateRespawnDictionary), settings.UpdateRespawnDictionary, DefaultSettings.UpdateRespawnDictionary, false,
                //    "UpdateRespawnDictionary", "", () => settings.EnableMatchComponent);
                KnownFields.Add("UpdateRespawnDictionary");

                // obsolete/unused
                KnownFields.Add("Scenario");
                KnownFields.Add("ScenarioEditMode");
                KnownFields.Add("CanJoinRunning");

                KnownFields.Add("AutoSave"); // points to AutoSaveInMinutes
                KnownFields.Add("ClientCanSave"); // always false
                KnownFields.Add("TrashFlags"); // points to TrashFlagsValue

                // from medieval engineers
                KnownFields.Add("MaxActiveFracturePieces");
                KnownFields.Add("EnableStructuralSimulation");
            }


            MyObjectBuilder_SessionSettings settings = MyAPIGateway.Session.SessionSettings;
            StringBuilder sb;

            // can't print DS settings because client does not receive them from server
            //IMyConfigDedicated dsConfig = MyAPIGateway.Utilities.ConfigDedicated; // is null for non-DS
            //var dsConfigDefault = new MyConfigDedicatedData<MyObjectBuilder_SessionSettings>();

            sb = NextColumn();


            if(sb != null)
            {
                sb.Color(ValueColorDefault).Append("(").Append(DefaultFrom).Append("'s default)\n");
                sb.Color(ValueColorChanged).Append("(Different)\n");
                sb.Color(ValueColorDisabled).Append("(Requires something else)\n");
                sb.Color(NewSettingColor).Append("*<reset> new settings in SE v205\n");
                //sb.Append("\n<reset>");
                sb.Color(Color.Yellow).Append("Search<reset> by opening chat.\n");
            }

            bool globalEncountersOn = settings.GlobalEncounterCap > 0;

            #region General
            Header(sb, "General");

            PrintSetting(sb, nameof(settings.GameMode), settings.GameMode, DefaultSettings.GameMode, true,
                "Game Mode");
            PrintSetting(sb, nameof(settings.OnlineMode), settings.OnlineMode.ToString(), null, false,
                "Online Mode", "Offline means multiplayer is disabled (and local mods are allowed), while other values determine if and what players can join." +
                               "\nThe mode can be changed in F3 menu if not offline nor dedicated server.");
            PrintSetting(sb, nameof(settings.MaxPlayers), settings.MaxPlayers.ToString(), null, true,
                "Max Players", "The maximum number of players that can play at the same time in this server.",
                GrayIfFalse(settings.OnlineMode != MyOnlineModeEnum.OFFLINE && settings.OnlineMode != MyOnlineModeEnum.PRIVATE));
            PrintFormattedNumber(sb, nameof(settings.AutoSaveInMinutes), settings.AutoSaveInMinutes, DefaultSettings.AutoSaveInMinutes, true,
                "Autosave interval", " min", "Defines autosave interval in minutes. 0 disables.", valueForZero: FalseValue);
            PrintSetting(sb, nameof(settings.ExperimentalMode), settings.ExperimentalMode, DefaultSettings.ExperimentalMode, false,
                "Experimental (hover for reason)", GetExperimentalTooltip());
            PrintSetting(sb, nameof(settings.FamilySharing), settings.FamilySharing, DefaultSettings.FamilySharing, false,
                "Family Sharing Accounts", "Allow players that have the game from family sharing (they don't own it themselves) to join this server.");
            PrintSetting(sb, nameof(settings.Enable3rdPersonView), settings.Enable3rdPersonView, DefaultSettings.Enable3rdPersonView, true,
                "3rd Person Camera", "Enables 3rd person camera.");
            PrintSetting(sb, nameof(settings.EnableGoodBotHints), settings.EnableGoodBotHints, DefaultSettings.EnableGoodBotHints, false,
                "Good.bot Hints", "Enables Good.bot hints in the world. If user has disabled hints, this will not override that.");
            //PrintDSSetting(sb, dsConfig?.ServerDescription, dsConfigDefault.ServerDescription,
            //    "Description");
            //PrintDSSetting(sb, dsConfig?.PauseGameWhenEmpty, dsConfigDefault.PauseGameWhenEmpty,
            //    "Pause Game When Empty", "Game is paused when there are no players online.");
            //PrintDSSetting(sb, dsConfig?.NetworkType, dsConfigDefault.NetworkType,
            //    "Network type", null);
            //PrintDSSetting(sb, dsConfig?.ConsoleCompatibility, dsConfigDefault.ConsoleCompatibility,
            //    "Console Compatibility", null);
            PrintSetting(sb, nameof(settings.BlueprintShare), settings.BlueprintShare, DefaultSettings.BlueprintShare, false,
                "Blueprint Share", "Allows players to send local blueprints to a specific player in this server using the blueprint menu (F10).");
            PrintFormattedNumber(sb, nameof(settings.BlueprintShareTimeout), settings.BlueprintShareTimeout, DefaultSettings.BlueprintShareTimeout, false,
                "Blueprint Share Timeout", " sec", "Time until player can send another blueprint.",
                GrayIfFalse(settings.BlueprintShare), valueForZero: FalseValue);

            //Header(sb, "Chat");

            PrintSetting(sb, nameof(settings.MaxHudChatMessageCount), settings.MaxHudChatMessageCount, DefaultSettings.MaxHudChatMessageCount, false,
                NewSettingTag + "Max messages in HUD chat", "Maximum number of messages displayed in HUD chat");
            PrintSetting(sb, nameof(settings.OffensiveWordsFiltering), settings.OffensiveWordsFiltering, DefaultSettings.OffensiveWordsFiltering, false,
                "Offensive Words Filtering", "Filter offensive words from all input methods.");
            //PrintDSSetting(sb, dsConfig?.ChatAntiSpamEnabled, dsConfigDefault.ChatAntiSpamEnabled,
            //    "Chat Anti-Spam", "Whether chat anti spam is enabled");
            //PrintDSSetting(sb, dsConfig?.SameMessageTimeout, dsConfigDefault.SpamMessagesTimeout,
            //    "Same message timeout", "The timeout for the same message, it cannot be sent again sooner than this (seconds)");
            //PrintDSSetting(sb, dsConfig?.SpamMessagesTime, dsConfigDefault.SpamMessagesTime,
            //    "Spam messages time", "The time threshold for spam. If elapsed time between messages is less they are considered spam (seconds)");
            //PrintDSSetting(sb, dsConfig?.SpamMessagesTimeout, dsConfigDefault.SpamMessagesTimeout,
            //    "Spam messages timeout", "If player is considered a spammer based on SpamMessagesTime they cannot send any messages for the duration of this timeout (seconds)");
            #endregion General


            #region Characters
            Header(sb, "Characters");

            PrintSetting(sb, nameof(settings.EnableJetpack), settings.EnableJetpack, DefaultSettings.EnableJetpack, true,
                "Jetpack", "Allows players to use their jetpack.");
            PrintSetting(sb, nameof(settings.SpawnWithTools), settings.SpawnWithTools, DefaultSettings.SpawnWithTools, true,
                "Spawn with Tools", "Enables spawning with tools in the inventory.");
            PrintFormattedNumber(sb, nameof(settings.CharacterSpeedMultiplier), settings.CharacterSpeedMultiplier, DefaultSettings.CharacterSpeedMultiplier, false,
                "On-foot Speed Multiplier", "x", "Modifier for walking,running,etc and affects NPCs too." +
                                                 "\nJetpack flight not affected.");
            PrintFormattedNumber(sb, nameof(settings.EnvironmentDamageMultiplier), settings.EnvironmentDamageMultiplier, DefaultSettings.EnvironmentDamageMultiplier, false,
                "Environment Damage Multiplier", "x", "This multiplier only applies for damage caused to a character by Environment damage types." +
                                                      "\nAffects NPCs too.");
            PrintFormattedNumber(sb, nameof(settings.BackpackDespawnTimer), settings.BackpackDespawnTimer, DefaultSettings.BackpackDespawnTimer, false,
                "Backpack Despawn Time", " min", "Sets the timer (minutes) for the backpack to be removed from the world."); // TODO: zero might despawn instantly, negative might never despawn? ... needs testing
            PrintFormattedNumber(sb, string.Empty, MyPerGameSettings.CharacterGravityMultiplier, Hardcoded.DefaultCharacterGravityMultiplier, false,
                "Character Gravity Multiplier", "x", "Gravity acceleration is multiplied by this value only for characters." + TooltipSettingModdable);
            #endregion Characters


            #region Respawn
            Header(sb, "Respawn");

            PrintSetting(sb, nameof(settings.PermanentDeath), settings.PermanentDeath, DefaultSettings.PermanentDeath, true,
                "Permanent Death", "If enabled and you cannot respawn at a medical room you will respawn as a new identity, losing ownership of everything you had.");
            PrintSetting(sb, nameof(settings.EnableAutorespawn), settings.EnableAutorespawn, DefaultSettings.EnableAutorespawn, false,
                "Auto-respawn", "Enables automatic respawn at nearest available respawn point.");
            PrintSetting(sb, nameof(settings.StartInRespawnScreen), settings.StartInRespawnScreen, DefaultSettings.StartInRespawnScreen, false,
                "Start in respawn screen");
            PrintSetting(sb, nameof(settings.EnableSpaceSuitRespawn), settings.EnableSpaceSuitRespawn, DefaultSettings.EnableSpaceSuitRespawn, false,
                "Space Suit Spawn", "Enables player to spawn in space suit");
            PrintSetting(sb, nameof(settings.EnableRespawnShips), settings.EnableRespawnShips, DefaultSettings.EnableRespawnShips, true,
                "Respawn Ships", "Enables respawn ships.");
            PrintFormattedNumber(sb, nameof(settings.SpawnShipTimeMultiplier), settings.SpawnShipTimeMultiplier, DefaultSettings.SpawnShipTimeMultiplier, true,
                "Respawn Ship Cooldown Multiplier", "x", "A multiplier to how frequent you can summon a new respawn ship again." +
                                                         "\nEach respawn ship has its own timer, however for planet spawns the shortest cooldown is used.",
                GrayIfFalse(settings.EnableRespawnShips), valueForZero: FalseValue);
            PrintSetting(sb, nameof(settings.RespawnShipDelete), settings.RespawnShipDelete, DefaultSettings.RespawnShipDelete, true,
                "Remove Respawn Ships on Logoff", "When enabled, respawn ship is removed after player logout.",
                GrayIfFalse(settings.EnableRespawnShips));
            #endregion Respawn


            #region Ships & blocks
            Header(sb, "Ships & blocks");

            MyEnvironmentDefinition envDef = MyDefinitionManager.Static.EnvironmentDefinition;

            const string ShipSpeedTooltipAdd = "\nCharacter max speed is usually largest of the ship speeds + 10m/s (largest of character's on-foot speeds)" + TooltipSettingModdable;

            if(envDef.LargeShipMaxSpeed == envDef.SmallShipMaxSpeed)
            {
                PrintFormattedNumber(sb, string.Empty, envDef.LargeShipMaxSpeed, DefaultEnvDef.LargeShipMaxSpeed, false,
                    "Ship max speed", " m/s", "Max speed that large and small grids can move at." + ShipSpeedTooltipAdd);
            }
            else
            {
                PrintFormattedNumber(sb, string.Empty, envDef.LargeShipMaxSpeed, DefaultEnvDef.LargeShipMaxSpeed, false,
                    "Largegrid max speed", " m/s", "Max speed that largegrid ships can move at." + ShipSpeedTooltipAdd);
                PrintFormattedNumber(sb, string.Empty, envDef.SmallShipMaxSpeed, DefaultEnvDef.SmallShipMaxSpeed, false,
                    "Smallgrid max speed", " m/s", "Max speed that smallgrid ships can move at." + ShipSpeedTooltipAdd);
            }

            PrintSetting(sb, nameof(settings.EnableResearch), settings.EnableResearch, DefaultSettings.EnableResearch, false,
                "Progression", "If enabled, blocks must be unlocked by building other blocks.\nSee progression tab in toolbar config menu.");
            PrintSetting(sb, nameof(settings.EnableTurretsFriendlyFire), settings.EnableTurretsFriendlyFire, DefaultSettings.EnableTurretsFriendlyFire, false,
                "Rocket self-damage", "Whether rockets can damage the ship they're fired from (attached grids included).");
            PrintSetting(sb, nameof(settings.DestructibleBlocks), settings.DestructibleBlocks, DefaultSettings.DestructibleBlocks, true,
                "Destructible blocks", "Allows blocks to be destroyed.");
            PrintSetting(sb, nameof(settings.AdjustableMaxVehicleSpeed), settings.AdjustableMaxVehicleSpeed, DefaultSettings.AdjustableMaxVehicleSpeed, false,
                "Adjustable suspension speed limit", "Whether the speed limit slider is visible in terminal of suspension blocks.");
            PrintSetting(sb, nameof(settings.StationVoxelSupport), settings.StationVoxelSupport, DefaultSettings.StationVoxelSupport, true,
                "Maintain static on split", "By enabling this option grids will no longer turn dynamic when disconnected from static grids." + TooltipOriginalName("WorldSettings_StationVoxelSupport"));
            PrintSetting(sb, nameof(settings.EnableConvertToStation), settings.EnableConvertToStation, DefaultSettings.EnableConvertToStation, true,
                "Allow convert to station", "Adds the 'Convert to Station' button in grids' Info tab in the Terminal.\n" +
                                            "Also allows pasted static grids to remain static even if not touching any voxel.");
            PrintSetting(sb, nameof(settings.ThrusterDamage), settings.ThrusterDamage, DefaultSettings.ThrusterDamage, true,
                "Thruster Damage", "Thruster blocks damage blocks and living things in their flame path.");
            PrintSetting(sb, nameof(settings.EnableSubgridDamage), settings.EnableSubgridDamage, DefaultSettings.EnableSubgridDamage, false,
                "Sub-Grid Physical Damage", "Allows physically connected grids to damage eachother from physical forces.");
            PrintSetting(sb, nameof(settings.EnableToolShake), settings.EnableToolShake, DefaultSettings.EnableToolShake, true,
                "Tool Shake", "Ship drills move the ship while mining and ship grinders move both the tool's ship and the grinded ship." +
                              "\nCamera shake is present regardless of this setting.");
            PrintSetting(sb, nameof(settings.EnableIngameScripts), settings.EnableIngameScripts, DefaultSettings.EnableIngameScripts, true,
                "Programmable Block Scripts", "Allows players to use scripts in programmable block (decoration otherwise).");
            PrintSetting(sb, nameof(settings.EnableScripterRole), settings.EnableScripterRole, DefaultSettings.EnableScripterRole, true,
                "Scripter Role", "Adds a Scripter role, only Scripters and higher ranks will be able to paste and modify scripts in programmable blocks.",
                GrayIfFalse(settings.EnableIngameScripts));
            PrintFormattedNumber(sb, nameof(settings.BroadcastControllerMaxOfflineTransmitDistance), settings.BroadcastControllerMaxOfflineTransmitDistance, DefaultSettings.BroadcastControllerMaxOfflineTransmitDistance, false,
                "Broadcast Controller Offline Range", " m", "The maximum range for Broadcast Controller blocks that have offline owners.");
            PrintSetting(sb, nameof(settings.EnableSupergridding), settings.EnableSupergridding, DefaultSettings.EnableSupergridding, false,
                "Supergridding", "Allows supergridding exploit to be used (placing block on wrong size grid, e.g. jumpdrive on smallgrid).");
            PrintSetting(sb, nameof(settings.EnableOrca), settings.EnableOrca, DefaultSettings.EnableOrca, false,
                "Advanced ORCA algorithm", "Enable advanced Optimal Reciprocal Collision Avoidance algorithm.");
            #endregion Ships & blocks


            #region PvP
            Header(sb, "PvP");

            PrintSetting(sb, nameof(settings.EnableMatchComponent), settings.EnableMatchComponent, DefaultSettings.EnableMatchComponent, false,
                "Match Enabled");
            PrintFormattedNumber(sb, nameof(settings.MatchRestartWhenEmptyTime), settings.MatchRestartWhenEmptyTime, DefaultSettings.MatchRestartWhenEmptyTime, false,
                "Match Restart When Empty", " min", "Server will restart after specified time (minutes), when it's empty after match started. Works only in PvP scenarios.\n0 means disabled.",
                GrayIfFalse(settings.EnableMatchComponent));
            PrintFormattedNumber(sb, nameof(settings.MatchDuration), settings.MatchDuration, DefaultSettings.MatchDuration, false,
                "Match Duration", " min", "Duration of Match phase of the match.",
                GrayIfFalse(settings.EnableMatchComponent));
            PrintFormattedNumber(sb, nameof(settings.PreMatchDuration), settings.PreMatchDuration, DefaultSettings.PreMatchDuration, false,
                "Pre-Match Duration", " min", "Duration of PreMatch phase of the match.",
                GrayIfFalse(settings.EnableMatchComponent));
            PrintFormattedNumber(sb, nameof(settings.PostMatchDuration), settings.PostMatchDuration, DefaultSettings.PostMatchDuration, false,
                "Post-Match Duration", " min", "Duration of PostMatch phase of the match.",
                GrayIfFalse(settings.EnableMatchComponent));
            PrintSetting(sb, nameof(settings.EnableTeamScoreCounters), settings.EnableTeamScoreCounters, DefaultSettings.EnableTeamScoreCounters, false,
                "Team Score Counters", "Show team scores at the top of the screen.",
                GrayIfFalse(settings.EnableMatchComponent));
            PrintSetting(sb, nameof(settings.EnableFriendlyFire), settings.EnableFriendlyFire, DefaultSettings.EnableFriendlyFire, false,
                "Friendly Fire", "If disabled, characters do not get damaged by friends with hand weapons or hand tools.");
            PrintSetting(sb, nameof(settings.EnableFactionVoiceChat), settings.EnableFactionVoiceChat, DefaultSettings.EnableFactionVoiceChat, false,
                "Faction Voice Chat", "Faction Voice Chat removes the need of antennas and broadcasting of the character for faction.");
            PrintSetting(sb, nameof(settings.EnableTeamBalancing), settings.EnableTeamBalancing, DefaultSettings.EnableTeamBalancing, false,
                "Team balancing", "New players automatically join the faction with the least members.");
            PrintSetting(sb, nameof(settings.ShowPlayerNamesOnHud), settings.ShowPlayerNamesOnHud, DefaultSettings.ShowPlayerNamesOnHud, true,
                "Show player names", "If false player names are never shown, even if personal broadcast is on.");
            PrintSetting(sb, nameof(settings.EnableFactionPlayerNames), settings.EnableFactionPlayerNames, DefaultSettings.EnableFactionPlayerNames, false,
                "Show teammate names", "Shows player names above their head if they're in the same faction even if personal broadcast is off.");
            PrintSetting(sb, nameof(settings.EnableGamepadAimAssist), settings.EnableGamepadAimAssist, DefaultSettings.EnableGamepadAimAssist, false,
                "Gamepad Aim Assist", "Enable aim assist for gamepad.");
            PrintFormattedNumber(sb, nameof(settings.EnemyTargetIndicatorDistance), settings.EnemyTargetIndicatorDistance, DefaultSettings.EnemyTargetIndicatorDistance, false,
                "Aimed Enemy Indicator Distance", " m", "Max distance to show enemy indicator when aiming at a character.");
            #endregion PvP


            #region Bots
            Header(sb, "Bots");

            PrintSetting(sb, nameof(settings.TotalBotLimit), settings.TotalBotLimit, DefaultSettings.TotalBotLimit, false,
                "Animal NPC Limit", "Maximum number of organic bots in the world");
            PrintSetting(sb, nameof(settings.EnableSpiders), settings.EnableSpiders, DefaultSettings.EnableSpiders, true,
                "Spiders", "Enables spawning of spiders in the world.");
            PrintSetting(sb, nameof(settings.EnableWolfs), settings.EnableWolfs, DefaultSettings.EnableWolfs, true,
                "Wolves", "Enables spawning of wolves in the world.");
            #endregion


            sb = NextColumn(); // ------------------------------------------------------------------------------------------------------------------------------


            #region Multipliers
            Header(sb, "Multipliers");

            PrintFormattedNumber(sb, nameof(settings.BlocksInventorySizeMultiplier), settings.BlocksInventorySizeMultiplier, DefaultSettings.BlocksInventorySizeMultiplier, false,
                "Block Inventory", "x", "Multiplier for block inventory sizes.\nNOTE: Cargo mass gets inversely multiplied meaning a full ship is roughly the same mass regardless of this multiplier.");
            PrintFormattedNumber(sb, nameof(settings.InventorySizeMultiplier), settings.InventorySizeMultiplier, DefaultSettings.InventorySizeMultiplier, true,
                "Character Inventory", "x", "Multiplier for character inventory size.");
            PrintFormattedNumber(sb, nameof(settings.WelderSpeedMultiplier), settings.WelderSpeedMultiplier, DefaultSettings.WelderSpeedMultiplier, true,
                "Welding", "x", "Speed multiplier for welding (both hand and ship).");
            PrintFormattedNumber(sb, nameof(settings.GrinderSpeedMultiplier), settings.GrinderSpeedMultiplier, DefaultSettings.GrinderSpeedMultiplier, true,
                "Grinding", "x", "Speed multiplier for grinding friendly blocks (both hand and ship).");
            PrintFormattedNumber(sb, nameof(settings.HackSpeedMultiplier), settings.HackSpeedMultiplier, DefaultSettings.HackSpeedMultiplier, true,
                "Hacking", "x", "Speed multiplier for grinding enemy blocks (both hand and ship).");
            PrintFormattedNumber(sb, nameof(settings.HarvestRatioMultiplier), settings.HarvestRatioMultiplier, DefaultSettings.HarvestRatioMultiplier, false,
                "Mining", "x", "Harvest multiplier for drilling voxels (both hand and ship).");
            PrintFormattedNumber(sb, nameof(settings.AssemblerEfficiencyMultiplier), settings.AssemblerEfficiencyMultiplier, DefaultSettings.AssemblerEfficiencyMultiplier, true,
                "Assembler Efficiency", "x", "Higher multiplier means less resources necessary for crafting things in assemblers and survival kits.");
            PrintFormattedNumber(sb, nameof(settings.AssemblerSpeedMultiplier), settings.AssemblerSpeedMultiplier, DefaultSettings.AssemblerSpeedMultiplier, true,
                "Assembler Speed", "x", "Speed multiplier for reducing all assembler crafting times, including survival kit.");
            PrintFormattedNumber(sb, nameof(settings.RefinerySpeedMultiplier), settings.RefinerySpeedMultiplier, DefaultSettings.RefinerySpeedMultiplier, true,
                "Refinery Speed", "x", "Speed multiplier for all refineries.");
            #endregion Multipliers


            #region Environment
            Header(sb, "Environment");

            {
                KnownFields.Add(nameof(settings.EnableSunRotation));
                KnownFields.Add(nameof(settings.SunRotationIntervalMinutes));

                string val = settings.EnableSunRotation && settings.SunRotationIntervalMinutes != 0 ? settings.SunRotationIntervalMinutes.ToString(NumberFormat) : FalseValue;
                string defVal = DefaultSettings.EnableSunRotation && DefaultSettings.SunRotationIntervalMinutes != 0 ? DefaultSettings.SunRotationIntervalMinutes.ToString(NumberFormat) : FalseValue;
                PrintSetting(sb, $"{nameof(settings.EnableSunRotation)} and {nameof(settings.SunRotationIntervalMinutes)}", val, defVal, true,
                    "Sun Rotation", "Whether the Sun rotates in the skybox around the world, otherwise it's static.");
                //PrintSetting(sb, nameof(settings.EnableSunRotation), settings.EnableSunRotation, DefaultSettings.EnableSunRotation, true,
                //    "Sun Rotation", "Sun rotates in skybox based on sun rotation interval setting.");
                //PrintFormattedNumber(sb, nameof(settings.SunRotationIntervalMinutes), settings.SunRotationIntervalMinutes, DefaultSettings.SunRotationIntervalMinutes, true,
                //    "Sun Rotation Interval", " min", "Defines interval of one rotation of the sun.",
                //    GrayIfFalse(settings.EnableSunRotation));
            }

            PrintSetting(sb, nameof(settings.RealisticSound), settings.RealisticSound, DefaultSettings.RealisticSound, true,
                "Realistic Sound", "Enables sounds to be muffled or not heard at all depending on the medium sound travels through (air, contact, void)" +
                                   "\nOff is arcade sound mode, sounds can be heard clear regardless.");
            PrintSetting(sb, nameof(settings.EnableOxygen), settings.EnableOxygen, DefaultSettings.EnableOxygen, true,
                "Oxygen", "Enables oxygen in the world.");
            PrintSetting(sb, nameof(settings.EnableOxygenPressurization), settings.EnableOxygenPressurization, DefaultSettings.EnableOxygenPressurization, true,
                "Airtightness", "Enables grids interiors to be processed for airtightness (requires oxygen to be enabled)",
                GrayIfFalse(settings.EnableOxygen));
            PrintFormattedNumber(sb, nameof(settings.WorldSizeKm), settings.WorldSizeKm, DefaultSettings.WorldSizeKm, true,
                "Space Boundary", " km", "Defines how far you can go outwards from center of the map." + TooltipOriginalName("ServerDetails_WorldSizeKm"), valueForZero: FalseValue);
            PrintSetting(sb, nameof(settings.WeatherSystem), settings.WeatherSystem, DefaultSettings.WeatherSystem, false,
                "Automatic Weather System", "Enable automatic weather generation on planets.");
            PrintSetting(sb, nameof(settings.EnvironmentHostility), settings.EnvironmentHostility, DefaultSettings.EnvironmentHostility, true,
                "Meteorite Showers", $"Enables meteorites, available difficulties: {string.Join(", ", Enum.GetNames(typeof(MyEnvironmentHostilityEnum)))}" + TooltipOriginalName("WorldSettings_EnvironmentHostility"));
            PrintSetting(sb, nameof(settings.WeatherLightingDamage), settings.WeatherLightingDamage, DefaultSettings.WeatherLightingDamage, false,
                "Enable lightning damage", "Lightning strikes from weather can damage grids.");
            PrintSetting(sb, nameof(settings.EnableVoxelDestruction), settings.EnableVoxelDestruction, DefaultSettings.EnableVoxelDestruction, true,
                "Voxel Destruction", "Enables voxel destructions.");
            PrintSetting(sb, nameof(settings.ProceduralSeed), settings.ProceduralSeed, DefaultSettings.ProceduralSeed, false,
                "Procedural Content Seed", "Defines unique starting seed for the procedurally generated content (voxels and encounters).");

            const string GeneratorVersioning = "\nVersioning allows devs to change things that would break existing worlds without breaking existing worlds." +
                                               "\nHigher numbers don't necessarily mean newer or better, it could be a less intensive variant for lower-end hardware for example." +
                                               "\nExisting worlds should not modify this number. It is shown here for awareness.";

            bool proceduralAsteroids = settings.ProceduralDensity > 0;
            PrintSetting(sb, nameof(settings.VoxelGeneratorVersion), settings.VoxelGeneratorVersion, DefaultSettings.VoxelGeneratorVersion, false,
                "Voxel Generator Version", "Voxel generator determines what shapes voxels have for a given seed number." +
                                           GeneratorVersioning,
                GrayIfFalse(proceduralAsteroids));
            PrintSetting(sb, nameof(settings.ProceduralDensity), settings.ProceduralDensity, DefaultSettings.ProceduralDensity, true,
                "Procedural Asteroids Density", "Defines density of the procedurally generated asteroids.");
            PrintSetting(sb, nameof(settings.DepositsCountCoefficient), settings.DepositsCountCoefficient, DefaultSettings.DepositsCountCoefficient, false,
                "Deposits Count Coefficient", "Resource deposits count coefficient for generated world content (voxel generator v3 or higher).",
                GrayIfFalse(proceduralAsteroids && settings.VoxelGeneratorVersion >= 3));
            PrintSetting(sb, nameof(settings.DepositSizeDenominator), settings.DepositSizeDenominator, DefaultSettings.DepositSizeDenominator, false,
                "Deposit Size Denominator", "Resource deposit size denominator for generated world content (voxel generator v3 or higher).",
                GrayIfFalse(proceduralAsteroids && settings.VoxelGeneratorVersion >= 3));
            #endregion Environment


            #region Economy
            Header(sb, "Economy");

            bool economyOn = settings.EnableEconomy;

            PrintSetting(sb, nameof(settings.EnableEconomy), settings.EnableEconomy, DefaultSettings.EnableEconomy, false,
                "Economy", "Enables economy features:" +
                           "\n- Generated NPC factions that need to be discovered." +
                           "\n- Generated space and planet-bound trade stations (with safe zones)." +
                           "\n -- Only planets that existed when economy was first turned on" +
                           "\n- Enables currency system for players, ATMs, shop blocks, etc." +
                           "\nNote: this adds 4MB+ of save file bulk from the generated data, which does not get removed when economy is turned off.");
            PrintFormattedNumber(sb, nameof(settings.EconomyTickInSeconds), settings.EconomyTickInSeconds / 60d, DefaultSettings.EconomyTickInSeconds / 60d, false,
                "Economy Update Frequency", " min", "Time between two economy updates (station contracts, etc)" +
                                                    (ShowInternal ? "\nNote: internal setting is in seconds, this is shown in minutes for readability." : ""),
                GrayIfFalse(economyOn));
            PrintSetting(sb, nameof(settings.EnableBountyContracts), settings.EnableBountyContracts, DefaultSettings.EnableBountyContracts, false,
                "Kill Contracts on Players", "If trading outposts generate kill contracts against players that are friends with the pirate faction.",
                GrayIfFalse(economyOn));
            PrintSetting(sb, nameof(settings.TradeFactionsCount), settings.TradeFactionsCount, DefaultSettings.TradeFactionsCount, false,
                "Trade Factions", "The number of NPC factions for trade stations generated on the start of the world." +
                                  "\nYou need to find one of their grids to see the faction in the factions menu.",
                GrayIfFalse(economyOn));
            PrintFormattedNumber(sb, nameof(settings.StationsDistanceInnerRadius), settings.StationsDistanceInnerRadius / 1000d, DefaultSettings.StationsDistanceInnerRadius / 1000d, false,
                "Trade Space Stations", "km", "The inner radius around 0,0,0 where trading space stations (with safezones) can spawn." +
                                               "\nDoes not affect planet-bound stations (surface Outposts and Orbital stations)." +
                                               (ShowInternal ? "\nNote: internal setting uses meters, this is shown in km for readability." : ""),
                GrayIfFalse(economyOn));
            {
                PrintRangeSetting(sb, nameof(settings.StationsDistanceOuterRadiusStart), nameof(settings.StationsDistanceOuterRadiusEnd),
                                      settings.StationsDistanceOuterRadiusStart / 1000, settings.StationsDistanceOuterRadiusEnd / 1000d,
                                      DefaultSettings.StationsDistanceOuterRadiusStart / 1000, DefaultSettings.StationsDistanceOuterRadiusEnd / 1000d, false,
                                      "Trade Deep-Space Stations", "km", "The belt around 0,0,0 where trading deep-space stations (with safezones) can spawn." +
                                                                        "\nDoes not affect planet-bound stations (surface Outposts and Orbital stations)." +
                                                                         (ShowInternal ? "\nNote: internal setting uses meters, this is shown in km for readability." : ""),
                                      GrayIfFalse(economyOn));
                //PrintFormattedNumber(sb, nameof(settings.StationsDistanceOuterRadiusStart), settings.StationsDistanceOuterRadiusStart, DefaultSettings.StationsDistanceOuterRadiusStart, false,
                //    "Stations Outer Radius Start", "m", "The outer radius [m] (center is in 0,0,0), where stations can spawn. Does not affect planet-bound stations (surface Outposts and Orbital stations).");
                //PrintFormattedNumber(sb, nameof(settings.StationsDistanceOuterRadiusEnd), settings.StationsDistanceOuterRadiusEnd, DefaultSettings.StationsDistanceOuterRadiusEnd, false,
                //    "Stations Outer Radius End", "m", "The outer radius [m] (center is in 0,0,0), where stations can spawn. Does not affect planet-bound stations (surface Outposts and Orbital stations).");
            }

            #endregion Economy


            #region Encounters
            Header(sb, "Encounters");

            bool encountersOn = settings.EnableEncounters && settings.EncounterDensity > 0f;

            PrintSetting(sb, nameof(settings.CargoShipsEnabled), settings.CargoShipsEnabled, DefaultSettings.CargoShipsEnabled, true,
                "Cargo Ships", "Cargo ships only spawn in space and .");

            PrintSetting(sb, nameof(settings.EnableEncounters), settings.EnableEncounters, DefaultSettings.EnableEncounters, true,
                "Encounters", "Enables random encounters in the world.");

            PrintSetting(sb, nameof(settings.EncounterDensity), settings.EncounterDensity, DefaultSettings.EncounterDensity, false,
                NewSettingTag + "Encounter Density", "", // TODO description?
                GrayIfFalse(encountersOn));

            PrintSetting(sb, nameof(settings.EncounterGeneratorVersion), settings.EncounterGeneratorVersion, DefaultSettings.EncounterGeneratorVersion, false,
                NewSettingTag + "Encounter Generator version", "Encounter generator determines how the encounters are generated for a given seed number." +
                                           GeneratorVersioning,
                GrayIfFalse(encountersOn));

            {
                KnownFields.Add(nameof(settings.EnableDrones));
                KnownFields.Add(nameof(settings.MaxDrones));

                int maxDrones = settings.EnableDrones ? settings.MaxDrones : 0;
                int defMaxDrones = DefaultSettings.EnableDrones ? DefaultSettings.MaxDrones : 0;

                PrintSetting(sb, $"{nameof(settings.MaxDrones)} and {nameof(settings.EnableDrones)}", maxDrones, defMaxDrones, false,
                    "Drones", "Drones can be spawned by enemy encounters to defend against attackers." +
                                        "\nThis setting limits how many of those can be active at any one time in the world.");
                //PrintSetting(sb, nameof(settings.EnableDrones), settings.EnableDrones, DefaultSettings.EnableDrones, true,
                //    "Pirate Drones", "Enables spawning of drones in the world from pirate antennas.",
                //    GrayIfFalse(settings.MaxDrones > 0));
                //PrintSetting(sb, nameof(settings.MaxDrones), settings.MaxDrones, DefaultSettings.MaxDrones, false,
                //    "Max Pirate Drones", "Maximum number of pirate drones at one time.",
                //    GrayIfFalse(settings.EnableDrones && settings.MaxDrones > 0));
            }

            PrintSetting(sb, nameof(settings.EnableContainerDrops), settings.EnableContainerDrops, DefaultSettings.EnableContainerDrops, false,
                "Drop Containers", "Allows drop containers (unknown signal) to spawn in space and on planets." +
                                   "\nThese are very small pods that contain some resources and a button to claim a random character/tool skin item." +
                                   "\nStrong signals are visible to everyone, the green ones only you can see the marker but other players can find them the old fashioned way too.");

            PrintRangeSetting(sb, nameof(settings.MinDropContainerRespawnTime), nameof(settings.MaxDropContainerRespawnTime),
                                  settings.MinDropContainerRespawnTime, settings.MaxDropContainerRespawnTime,
                                  DefaultSettings.MinDropContainerRespawnTime, DefaultSettings.MaxDropContainerRespawnTime, false,
                                  "Drop Container Spawn Time", "min", "Randomly chosen number within this range for the next drop container spawn.",
                                  GrayIfFalse(settings.EnableContainerDrops));
            //PrintFormattedNumber(sb, nameof(settings.MinDropContainerRespawnTime), settings.MinDropContainerRespawnTime, DefaultSettings.MinDropContainerRespawnTime, false,
            //    "Drop Container min spawn", " min", "Defines minimum respawn time for drop containers.");
            //PrintFormattedNumber(sb, nameof(settings.MaxDropContainerRespawnTime), settings.MaxDropContainerRespawnTime, DefaultSettings.MaxDropContainerRespawnTime, false,
            //    "Drop Container max spawn", " min", "Defines maximum respawn time for drop containers.");

            PrintFormattedNumber(sb, nameof(settings.NPCGridClaimTimeLimit), settings.NPCGridClaimTimeLimit, DefaultSettings.NPCGridClaimTimeLimit, false,
                "Claim time for NPC grids", " min", "NPC grids despawn if not claimed within this amount of time since their spawn.");

            PrintFormattedNumber(sb, nameof(settings.GlobalEncounterCap), settings.GlobalEncounterCap, DefaultSettings.GlobalEncounterCap, false,
                NewSettingTag + "Global Encounter Cap", "", "Maximum of active Global Encounters (unidentified signal) at the same time. Turned off when 0.",
                GrayIfFalse(globalEncountersOn), valueForZero: FalseValue);

            PrintFormattedNumber(sb, nameof(settings.GlobalEncounterTimer), settings.GlobalEncounterTimer, DefaultSettings.GlobalEncounterTimer, false,
                NewSettingTag + "GE Spawn Timer", " min", "Global Encounters will try to spawn every this many minutes. The cap and global-encounter-specific PCU apply.",
                GrayIfFalse(globalEncountersOn));


            {
                // HACK: from MyGlobalEncountersGenerator.RegisterEncounter()
                const int LimitLow = 90;
                const int LimitHigh = 180;
                int max = MyUtils.GetClampInt(settings.GlobalEncounterMaxRemovalTimer, LimitLow + 1, LimitHigh);
                int min = MyUtils.GetClampInt(settings.GlobalEncounterMinRemovalTimer, LimitLow, max);
                int defaultMax = MyUtils.GetClampInt(DefaultSettings.GlobalEncounterMaxRemovalTimer, LimitLow + 1, LimitHigh);
                int defaultMin = MyUtils.GetClampInt(DefaultSettings.GlobalEncounterMinRemovalTimer, LimitLow, defaultMax);

                KnownFields.Add(nameof(settings.GlobalEncounterEnableRemovalTimer));
                KnownFields.Add(nameof(settings.GlobalEncounterMinRemovalTimer));
                KnownFields.Add(nameof(settings.GlobalEncounterMaxRemovalTimer));

                const string label = NewSettingTag + "GE Removal Timer";
                string description = $"This combines 3 settings: {nameof(settings.GlobalEncounterMinRemovalTimer)}, {nameof(settings.GlobalEncounterMinRemovalTimer)} and {nameof(settings.GlobalEncounterMaxRemovalTimer)}" +
                                     "\nThey all control the same thing so it can be narrowed down to either off or a range between 2 values." +
                                     "\nThe time range means it will pick a random number between those values, and that will decide how long until the encounter vanishes." +
                                     $"\nThe range cannot be smaller than {LimitLow} or larger than {LimitHigh} (hardcoded).";

                const string suffix = " min";

                string val = !settings.GlobalEncounterEnableRemovalTimer ? FalseValue : $"{min.ToString(NumberFormat)} ~ {max.ToString(NumberFormat)} {suffix}";
                string defVal = !settings.GlobalEncounterEnableRemovalTimer ? FalseValue : $"{defaultMin.ToString(NumberFormat)} ~ {defaultMax.ToString(NumberFormat)} {suffix}";

                PrintSetting(sb, null, val, defVal, false, label, description, GrayIfFalse(globalEncountersOn));

                //PrintSetting(sb, nameof(settings.GlobalEncounterEnableRemovalTimer), settings.GlobalEncounterEnableRemovalTimer, DefaultSettings.GlobalEncounterEnableRemovalTimer, false,
                //    NewSettingTag + "Global Encounter Removal Timer", "Auto-removal of Global Encounters after their timer runs out.",
                //    GrayIfFalse(globalEncountersOn));
                //PrintFormattedNumber(sb, nameof(settings.GlobalEncounterMinRemovalTimer), settings.GlobalEncounterMinRemovalTimer, DefaultSettings.GlobalEncounterMinRemovalTimer, false,
                //    NewSettingTag + "Removal Timer Minimum", " min", "Minimum removal timer for Global Encounters.",
                //    GrayIfFalse(globalEncountersOn));
                //PrintFormattedNumber(sb, nameof(settings.GlobalEncounterMaxRemovalTimer), settings.GlobalEncounterMaxRemovalTimer, DefaultSettings.GlobalEncounterMaxRemovalTimer, false,
                //    NewSettingTag + "Removal Timer Maximum", " min", "Maximum removal timer for Global Encounters.",
                //    GrayIfFalse(globalEncountersOn));
            }

            PrintFormattedNumber(sb, nameof(settings.GlobalEncounterRemovalTimeClock), settings.GlobalEncounterRemovalTimeClock, DefaultSettings.GlobalEncounterRemovalTimeClock, false,
                NewSettingTag + "GE Display Timer", " min",
                "Show global encounter's remaining time on its GPS marker if it's under this many minutes.",
                GrayIfFalse(globalEncountersOn && settings.GlobalEncounterEnableRemovalTimer));


            bool planetaryEncountersOn = settings.EnablePlanetaryEncounters;

            const string TrackedPENote = "\nNOTE: Only affects <i>tracked<reset> planetary encounters. An encounter is no longer tracked if it gets damaged or claimed.";

            PrintSetting(sb, nameof(settings.EnablePlanetaryEncounters), settings.EnablePlanetaryEncounters, DefaultSettings.EnablePlanetaryEncounters, false,
                NewSettingTag + "Planetary Encounters",
                "Whether planetary installations/stations (no safezone) are allowed to spawn.");

            {
                // HACK: from MyPlanetaryEncountersGenerator.Init()
                float min = settings.PlanetaryEncounterTimerMin;
                float max = Math.Max(min, settings.PlanetaryEncounterTimerMax);
                float defMin = DefaultSettings.PlanetaryEncounterTimerMin;
                float defMax = Math.Max(defMin, DefaultSettings.PlanetaryEncounterTimerMax);

                PrintRangeSetting(sb, nameof(settings.PlanetaryEncounterTimerMin), nameof(settings.PlanetaryEncounterTimerMax),
                                      min, max,
                                      defMin, defMax, false,
                                      NewSettingTag + "PE Spawn Timer Range", " min",
                                      "Random number within this range is chosen for the next planetary encounter spawn.",
                                      GrayIfFalse(planetaryEncountersOn));

                //PrintFormattedNumber(sb, nameof(settings.PlanetaryEncounterTimerMin), settings.PlanetaryEncounterTimerMin, DefaultSettings.PlanetaryEncounterTimerMin, false,
                //    NewSettingTag + "Installations Timer Min", " min", "Minimum [minutes] for Planetary Encounter spawn timer.",
                //    GrayIfFalse(planetaryEncountersOn));
                //PrintFormattedNumber(sb, nameof(settings.PlanetaryEncounterTimerMax), settings.PlanetaryEncounterTimerMax, DefaultSettings.PlanetaryEncounterTimerMax, false,
                //    NewSettingTag + "Installations Timer Max", " min", "Maximum [minutes] for Planetary Encounter spawn timer.",
                //    GrayIfFalse(planetaryEncountersOn));
            }

            PrintFormattedNumber(sb, nameof(settings.PlanetaryEncounterDesiredSpawnRange), settings.PlanetaryEncounterDesiredSpawnRange, DefaultSettings.PlanetaryEncounterDesiredSpawnRange, false,
                NewSettingTag + "PE Spawn Distance To Players", "m",
                "Planetary encounters spawn in randomly chosen position within this range of every online player's controlled entity." +
                "\nThis number is also the height they must be from the surface to be eligible for spawning planetary encounters.",
                GrayIfFalse(planetaryEncountersOn));

            PrintFormattedNumber(sb, nameof(settings.PlanetaryEncounterAreaLockdownRange), settings.PlanetaryEncounterAreaLockdownRange, DefaultSettings.PlanetaryEncounterAreaLockdownRange, false,
                NewSettingTag + "PE Denial Area", "m",
                "Planetary encounters will mark this radius around them where no planetary encounters can spawn." +
                TrackedPENote,
                GrayIfFalse(planetaryEncountersOn));

            PrintFormattedNumber(sb, nameof(settings.PlanetaryEncounterExistingStructuresRange), settings.PlanetaryEncounterExistingStructuresRange, DefaultSettings.PlanetaryEncounterExistingStructuresRange, false,
                NewSettingTag + "Static Grids Denial Area", "m",
                "Static grids (that are not tracked planetary encounters) will mark this radius around them where no planetary encounters can spawn.",
                GrayIfFalse(planetaryEncountersOn));

            PrintFormattedNumber(sb, nameof(settings.PlanetaryEncounterPresenceRange), settings.PlanetaryEncounterPresenceRange, DefaultSettings.PlanetaryEncounterPresenceRange, false,
                NewSettingTag + "PE Spawn Presence", "m",
                "Tracked planetary encounters despawn as soon as no players are within this range." +
                "\nHowever, they're still remembered and will respawn if a player comes back within range before \"PE Despawn Timer\" removes it permanently." +
                TrackedPENote,
                GrayIfFalse(planetaryEncountersOn));

            PrintFormattedNumber(sb, nameof(settings.PlanetaryEncounterDespawnTimeout), settings.PlanetaryEncounterDespawnTimeout, DefaultSettings.PlanetaryEncounterDespawnTimeout, false,
                NewSettingTag + "PE No-presence Removal Timer", " min",
                "This timer only starts once no players are within the above \"PE Spawn Presence\" range, and will reset if any player comes into range before it runs out." +
                "\nWhen this timer runs out, the planetary encounter is deleted and won't respawn in the same place." +
                TrackedPENote,
                GrayIfFalse(planetaryEncountersOn));

            PrintPlanetsHavingEncounters(sb, NewSettingTag + "PE on Planets", planetaryEncountersOn);
            #endregion Encounters


            #region Combat
            Header(sb, "Combat");

            PrintSetting(sb, nameof(settings.WeaponsEnabled), settings.WeaponsEnabled, DefaultSettings.WeaponsEnabled, true,
                "Allow Weapons", "Determine if ship and handheld weapons can be used.");
            PrintSetting(sb, nameof(settings.InfiniteAmmo), settings.InfiniteAmmo, DefaultSettings.InfiniteAmmo, false,
                "Infinite ammo in Survival");
            PrintSetting(sb, nameof(settings.EnableRecoil), settings.EnableRecoil, DefaultSettings.EnableRecoil, false,
                "Hand weapons recoil");
            PrintSetting(sb, nameof(settings.AutoHealing), settings.AutoHealing, DefaultSettings.AutoHealing, true,
                "Auto-heal players", "Auto-healing heals players only in oxygen environments and during periods of not taking damage.");
            PrintSetting(sb, nameof(settings.ScrapEnabled), settings.ScrapEnabled, DefaultSettings.ScrapEnabled, false,
                "Enable Scrap Drops", "Allow scrap to be dropped from destroyed blocks");
            PrintSetting(sb, nameof(settings.TemporaryContainers), settings.TemporaryContainers, DefaultSettings.TemporaryContainers, false,
                "Enable Temporary Containers", "Enable Temporary Containers to spawn after destroying block with inventory.");
            #endregion Combat



            #region Misc
            Header(sb, "Misc.");

            PrintSetting(sb, nameof(settings.EnableSpectator), settings.EnableSpectator, DefaultSettings.EnableSpectator, true,
                "Everyone Spectator Camera", "Allows all players to use spectator camera (F6-F9).\nWith this off, spectator is still allowed in creative mode or admin creative tools.",
                GrayOrWarn(true, MyAPIGateway.Session.SurvivalMode && settings.EnableSpectator));
            PrintSetting(sb, nameof(settings.EnableVoxelHand), settings.EnableVoxelHand, DefaultSettings.EnableVoxelHand, false,
                "Voxel Hands", "Only usable in creative mode or admin creative tools.\nAllows use of voxel hand tools to manipulate voxels (in toolbar config menu).");
            PrintSetting(sb, nameof(settings.EnableCopyPaste), settings.EnableCopyPaste, DefaultSettings.EnableCopyPaste, true,
                "Copy & Paste", "Usable only in creative mode or admin creative tools.\nEnables copy and paste feature.");
            PrintSuppressedWarnings(sb, nameof(settings.SuppressedWarnings), settings.SuppressedWarnings, DefaultSettings.SuppressedWarnings, false,
                "Suppressed Warnings", "Makes players ignore certain warnings from top-right red box popup, but not from the fully opened Shift+F1 menu.");
            PrintSetting(sb, string.Empty, "(hover)", null, false,
                "Undisclosed settings", "There are some settings that were intentionally not disclosed in this menu:" + UndisclosedSettingsList);
            #endregion Misc


            sb = NextColumn(); // ------------------------------------------------------------------------------------------------------------------------------


            #region Limits
            Header(sb, "Limits");

            PrintSetting(sb, nameof(settings.BlockLimitsEnabled), GetLimitsModeName(settings.BlockLimitsEnabled), GetLimitsModeName(DefaultSettings.BlockLimitsEnabled), true,
                "Limits Mode", "Defines the mode that block&PCU limits use." +
                               "\nA few other settings rely on this aswell like Max Grid Blocks and Max Blocks per Player." +
                               "\nPerformance Cost Units (PCU) is a way to limit block counts based on their potential performance impact.");

            string labelInitialPCU = "PCU Limit";
            switch(settings.BlockLimitsEnabled)
            {
                case MyBlockLimitsEnabledEnum.GLOBALLY: labelInitialPCU = "<i>Global</i> PCU"; break;
                case MyBlockLimitsEnabledEnum.PER_PLAYER: labelInitialPCU = "<i>Per-player</i> PCU"; break;
                case MyBlockLimitsEnabledEnum.PER_FACTION: labelInitialPCU = "<i>Per-faction</i> PCU"; break;
            }

            // HACK: similar to MyGuiScreenServerDetailsBase.AddAdditionalSettings()
            int pcu = settings.BlockLimitsEnabled == MyBlockLimitsEnabledEnum.NONE ? 0 : MyObjectBuilder_SessionSettings.GetInitialPCU(MyAPIGateway.Session.SessionSettings);
            bool blocksAreLimited = settings.BlockLimitsEnabled != MyBlockLimitsEnabledEnum.NONE;

            KnownFields.Add(nameof(settings.TotalPCU));
            PrintSetting(sb, string.Empty, pcu.ToString(), null, true,
                labelInitialPCU, "Not the actual setting but a calculation depending on Limits mode:" +
                                $"\n  {GetLimitsModeName(MyBlockLimitsEnabledEnum.GLOBALLY)} = TotalPCU" +
                                $"\n  {GetLimitsModeName(MyBlockLimitsEnabledEnum.PER_PLAYER)} = TotalPCU / MaxPlayers" +
                                $"\n  {GetLimitsModeName(MyBlockLimitsEnabledEnum.PER_FACTION)} = TotalPCU / MaxFactions (MaxFactions cannot be lower than 1 in this mode)" +
                                $"\nTotalPCU is set to: {settings.TotalPCU} (default: {DefaultSettings.TotalPCU})",
                GrayIfFalse(blocksAreLimited));

            //PrintSetting(sb, nameof(settings.LimitBlocksBy), settings.LimitBlocksBy, DefaultSettings.LimitBlocksBy, false,
            //    NewSettingTag + "Limit Blocks by", "Which block attribute is used for defining the block limits." +
            //                                       $"\nOptions can be: {string.Join(", ", MyEnum<MyObjectBuilder_SessionSettings.LimitBlocksByOption>.Values)}",
            //    GrayIfFalse(blocksAreLimited));

            KnownFields.Add(nameof(settings.LimitBlocksBy));

            PrintBlockLimits(sb, nameof(settings.BlockTypeLimits), settings.BlockTypeLimits, DefaultSettings.BlockTypeLimits,
                                 nameof(settings.LimitBlocksBy), settings.LimitBlocksBy, DefaultSettings.LimitBlocksBy, true,
                NewSettingTag + "Block limits", "Additional limits for specific blocks, respects Limit mode." +
                                                $"\nThis world is using block's {settings.LimitBlocksBy} for the limits (as set by {nameof(settings.LimitBlocksBy)} setting)",
                GrayIfFalse(blocksAreLimited));

            PrintSetting(sb, nameof(settings.MaxGridSize), settings.MaxGridSize, DefaultSettings.MaxGridSize, true,
                "Max blocks per grid", "The maximum number of blocks in one grid. 0 means no limit.",
                GrayIfFalse(blocksAreLimited)); // it does indeed check if limits are enabled before allowing this check

            PrintSetting(sb, nameof(settings.MaxBlocksPerPlayer), settings.MaxBlocksPerPlayer, DefaultSettings.MaxBlocksPerPlayer, true,
                "Max blocks per player", "The maximum number of blocks per player. 0 means no limit.",
                GrayIfFalse(blocksAreLimited));

            // HACK: like in MySession.MaxFactionsCount
            int maxFactions = (settings.BlockLimitsEnabled != MyBlockLimitsEnabledEnum.PER_FACTION ? settings.MaxFactionsCount : Math.Max(1, settings.MaxFactionsCount));
            PrintSetting(sb, nameof(settings.MaxFactionsCount), maxFactions, DefaultSettings.MaxFactionsCount, true,
                "Max player factions", "The max number of player factions. 0 means no limit." +
                                       "\nIf Limit mode is Per-Faction, this setting cannot be lower than 1.");

            PrintSetting(sb, nameof(settings.EnablePcuTrading), settings.EnablePcuTrading, DefaultSettings.EnablePcuTrading, false,
                "Allow PCU trading", "Enable trading of PCUs between players or factions depending on PCU settings.");

            // cannot easily identify which definitions have console PCU because it's only in the OB
            PrintSetting(sb, nameof(settings.UseConsolePCU), settings.UseConsolePCU, DefaultSettings.UseConsolePCU, false,
                "Using Console PCU", "To conserve memory, some of the blocks have different PCU values for consoles." +
                                     "\nIn vanilla blocks, all armor blocks have 2 PCU with this enabled instead of 1." +
                                     "\nAny mod can choose to have a console-specific PCU, however it's not practical to identify because only the final PCU is stored.");

            PrintSetting(sb, nameof(settings.PiratePCU), settings.PiratePCU, DefaultSettings.PiratePCU, false,
                "PCU for NPCs", "Number of Performance Cost Units allocated for NPCs ships, except Global Encounters.");

            PrintSetting(sb, nameof(settings.GlobalEncounterPCU), settings.GlobalEncounterPCU, DefaultSettings.GlobalEncounterPCU, false,
                NewSettingTag + "Global Encounters PCU", "Number of Performance Cost Units allocated for Global Encounters.",
                GrayIfFalse(globalEncountersOn));
            #endregion Limits


            #region Cleanup
            Header(sb, "Cleanup");

            PrintSetting(sb, nameof(settings.EnableRemoteBlockRemoval), settings.EnableRemoteBlockRemoval, DefaultSettings.EnableRemoteBlockRemoval, true,
                "Player manual grid removal", "Allows grid author to remotely remove grids from the info tab in terminal.");

            PrintFormattedNumber(sb, nameof(settings.AFKTimeountMin), settings.AFKTimeountMin, DefaultSettings.AFKTimeountMin, false,
                "AFK Timeout", " min", "Defines time in minutes after which inactive players will be kicked. 0 is off.");

            PrintFormattedNumber(sb, nameof(settings.StopGridsPeriodMin), settings.StopGridsPeriodMin, DefaultSettings.StopGridsPeriodMin, false,
                "Stop grids after", " min", "Defines time in minutes after which grids will be stopped if far from player.\n0 means off.");

            PrintFormattedNumber(sb, nameof(settings.PlayerCharacterRemovalThreshold), settings.PlayerCharacterRemovalThreshold, DefaultSettings.PlayerCharacterRemovalThreshold, false,
                "Character Removal Threshold", " min", "Defines character removal threshold for trash removal system." +
                                               "\nIf player disconnects it will remove his character after this time." +
                                               "\n0 means off.");

            PrintFormattedNumber(sb, nameof(settings.RemoveOldIdentitiesH), settings.RemoveOldIdentitiesH, DefaultSettings.RemoveOldIdentitiesH, false,
                "Remove Old Identities", " hours", "Defines time in hours after which inactive identities that do not own any grids will be removed.\n0 means off.");

            PrintSetting(sb, nameof(settings.EnableTrashSettingsPlatformOverride), settings.EnableTrashSettingsPlatformOverride, DefaultSettings.EnableTrashSettingsPlatformOverride, false,
                "Platform Trash Setting Override", "Enable trash settings to be overriden by console specific settings.");

            PrintSetting(sb, nameof(settings.TrashCleanerCargoBagsMaxLiveTime), settings.TrashCleanerCargoBagsMaxLiveTime, DefaultSettings.TrashCleanerCargoBagsMaxLiveTime, false,
                "Max Cargo Bags Lifetime", "The maximum amount of time (in minutes) allowed for cargo bags to be alive before deletion.");

            PrintSetting(sb, nameof(settings.MaxCargoBags), settings.MaxCargoBags, DefaultSettings.MaxCargoBags, false,
                "Max Cargo Bags", "The maximum number of existing cargo bags.");

            PrintSetting(sb, nameof(settings.MaxFloatingObjects), settings.MaxFloatingObjects, DefaultSettings.MaxFloatingObjects, true,
                "Max Floating Objects", "The maximum number of concurrent loose items.\nOlder floating objects are removed when newer ones need to spawn.");
            #endregion Cleanup


            #region Grids Cleanup
            Header(sb, "Grids Cleanup");

            PrintSetting(sb, nameof(settings.TrashRemovalEnabled), settings.TrashRemovalEnabled, DefaultSettings.TrashRemovalEnabled, false,
                "Grid Trash Removal");
            PrintTrashFlag(sb, MyTrashRemovalFlags.Fixed,
                "Static", "Remove static grids",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintTrashFlag(sb, MyTrashRemovalFlags.Stationary,
                "Not moving", "Remove dynamic grids that are not currently moving",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintTrashFlag(sb, MyTrashRemovalFlags.Linear,
                "Drifting", "Remove dynamic grids that are linearly moving",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintTrashFlag(sb, MyTrashRemovalFlags.Accelerating,
                "Accelerating", "Remove dynamic grids that are accelerating.",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintTrashFlag(sb, MyTrashRemovalFlags.Powered,
                "Powered", "Remove grids that have electricity",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintTrashFlag(sb, MyTrashRemovalFlags.Controlled,
                "Controlled", "Remove grids even if they are controlled.",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintTrashFlag(sb, MyTrashRemovalFlags.WithProduction,
                "With production", "Remove grids that have production blocks",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintTrashFlag(sb, MyTrashRemovalFlags.WithMedBay,
                "With respawn points", "Remove grids that have medical rooms or survival kits.",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintFormattedNumber(sb, nameof(settings.BlockCountThreshold), settings.BlockCountThreshold, DefaultSettings.BlockCountThreshold, false,
                "With less than", " blocks", "Defines block count threshold for trash removal system.",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintFormattedNumber(sb, nameof(settings.PlayerDistanceThreshold), settings.PlayerDistanceThreshold, DefaultSettings.PlayerDistanceThreshold, false,
                "Distance from player", " m", "Defines player distance threshold for trash removal system.",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintSetting(sb, nameof(settings.OptimalGridCount), settings.OptimalGridCount, DefaultSettings.OptimalGridCount, false,
                "Optimal Grid Count", "By setting this, server will keep number of grids around this value." +
                          "\nWARNING: It ignores 'Powered' flag, 'Static' flag and 'With less than N blocks' setting. It also lowers 'Distance from player' dynamically." +
                          "\n0 means off.",
                GrayIfFalse(settings.TrashRemovalEnabled));
            PrintFormattedNumber(sb, nameof(settings.PlayerInactivityThreshold), settings.PlayerInactivityThreshold, DefaultSettings.PlayerInactivityThreshold, false,
                "Player Inactivity Threshold", " hours", "Defines player inactivity (time from logout) threshold for trash removal system." +
                                                         "\nWARNING: This will remove all grids that are owned by the player." +
                                                         "\n0 means off.",
                GrayOrWarn(settings.TrashRemovalEnabled, settings.PlayerInactivityThreshold > 0));
            #endregion Grids Cleanup


            #region Voxel Cleanup
            Header(sb, "Voxel Cleanup");

            PrintSetting(sb, nameof(settings.VoxelTrashRemovalEnabled), settings.VoxelTrashRemovalEnabled, DefaultSettings.VoxelTrashRemovalEnabled, false,
                "Voxel reverting", "Enables system for voxel reverting.");

            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertAsteroids,
                "Revert asteroids", "",
                GrayIfFalse(settings.VoxelTrashRemovalEnabled));

            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertBoulders,
                "Revert planet-side boulders", "",
                GrayIfFalse(settings.VoxelTrashRemovalEnabled));

            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertMaterials,
                "Revert materials", "",
                GrayIfFalse(settings.VoxelTrashRemovalEnabled));

            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertCloseToNPCGrids,
                "Close to NPC grids", "Revert voxel chunks close to NPC grids.",
                GrayIfFalse(settings.VoxelTrashRemovalEnabled));

            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertWithFloatingsPresent,
                "Close to floating objects", "Revert voxel chunks close to floating objects.",
                GrayIfFalse(settings.VoxelTrashRemovalEnabled));

            PrintFormattedNumber(sb, nameof(settings.VoxelGridDistanceThreshold), settings.VoxelGridDistanceThreshold, DefaultSettings.VoxelGridDistanceThreshold, false,
                "Distance from grid", " m", "Only voxel chunks that are further from any grid will be reverted.",
                GrayIfFalse(settings.VoxelTrashRemovalEnabled));

            PrintFormattedNumber(sb, nameof(settings.VoxelPlayerDistanceThreshold), settings.VoxelPlayerDistanceThreshold, DefaultSettings.VoxelPlayerDistanceThreshold, false,
                "Distance from player", " m", "Only voxel chunks that are further from player will be reverted.",
                GrayIfFalse(settings.VoxelTrashRemovalEnabled));

            PrintFormattedNumber(sb, nameof(settings.VoxelAgeThreshold), settings.VoxelAgeThreshold, DefaultSettings.VoxelAgeThreshold, false,
                "Voxel Age", " min", "Voxel chunks older than this will be reverted.",
                GrayIfFalse(settings.VoxelTrashRemovalEnabled));
            #endregion Voxel Cleanup


            #region Performance
            Header(sb, "Performance");

            PrintEnvGraphicsChanges(sb, envDef, DefaultEnvDef,
                "LOD&Shadow distances", "This can only be changed by mods.\nInfluences how model detail and shadow graphics option behave.\nThis is merely a notice, compare with mods list to ensure it's intended.");
            PrintFormattedNumber(sb, nameof(settings.ViewDistance), settings.ViewDistance, DefaultSettings.ViewDistance, true,
                "View Distance", " m", "");
            PrintSetting(sb, nameof(settings.AdaptiveSimulationQuality), settings.AdaptiveSimulationQuality, DefaultSettings.AdaptiveSimulationQuality, false,
                "Adaptive Simulation Quality", "If enabled and CPU load (locally) is higher than 90% sustained, then a few things stop happening:" +
                                               "\nBlock deformations, some voxel cutouts, voxel cutouts from explosions, projectiles update less frequent, character limb IK and ragdoll, some grid impact details.");
            PrintSetting(sb, nameof(settings.EnableSelectivePhysicsUpdates), settings.EnableSelectivePhysicsUpdates, DefaultSettings.EnableSelectivePhysicsUpdates, false,
                "Selective Physics Updates", "When enabled game will update physics only in the specific clusters, which are necessary.\nOnly works on dedicated servers.");
            PrintSetting(sb, nameof(settings.SimplifiedSimulation), settings.SimplifiedSimulation, DefaultSettings.SimplifiedSimulation, false,
                "Simplified Simulation", "It is not recommended on for survival!" +
                                         "\nResources are not properly consumed, inventories are not updated and ammunition is not consumed.");
            PrintSetting(sb, nameof(settings.PhysicsIterations), settings.PhysicsIterations, DefaultSettings.PhysicsIterations, false,
                "Physics Iterations", $"Havok physics engine solver iterations, higher would have slightly more accurate physics but require more CPU power. It cannot be lower than {Hardcoded.SolverIterationsMin}.");
            PrintSetting(sb, nameof(settings.PredefinedAsteroids), settings.PredefinedAsteroids, DefaultSettings.PredefinedAsteroids, false,
                "Spawn-menu predefined asteroids", "Determines if admin spawn menu has predefined asteroids list." +
                                                   "\nNot using predefined asteroids in world helps memory usage (this setting does not remove not prevent them from being spawned, by encounters, mods, etc.)");
            PrintSetting(sb, nameof(settings.MaxPlanets), settings.MaxPlanets, DefaultSettings.MaxPlanets, false,
                "Max Planet Types", "Limit maximum number of types of planets in the world.");
            PrintSetting(sb, nameof(settings.MaxProductionQueueLength), settings.MaxProductionQueueLength, DefaultSettings.MaxProductionQueueLength, false,
                "Max Production Queue Length ", "Maximum assembler production queue size." +
                                                "\nIt becomes a problem when assemblers with no resources have lots of queued stacks, each requesting items through conveyor system.");
            PrintFormattedNumber(sb, nameof(settings.PrefetchShapeRayLengthLimit), settings.PrefetchShapeRayLengthLimit, DefaultSettings.PrefetchShapeRayLengthLimit, false,
                "Prefetch Voxels Range Limit", " m", "Defines at what maximum distance weapons could interact with voxels." +
                                                     "\n\nIn technical terms: prevents MyPlanet.PrefetchShapeOnRay() from prefetching voxels if the line is longer than this." +
                                                     "\nThis call is used by bullet projectiles, targeting systems and mods can use it too.");
            #endregion Performance


            #region Stats
            Header(sb, "Stats");

            DateTime date = MyAPIGateway.Session.GameDateTime;
            TimeSpan simTime = (date - Hardcoded.GameStartDate);
            TimeSpan sessionTime = MyAPIGateway.Session.ElapsedPlayTime;

            StringBuilder temp = new StringBuilder(256);

            temp.Clear();
            temp.Append("The world's date, increments only when simulated.");
            temp.Append("\nTotal simulation time: ").TimeFormat(simTime.TotalSeconds);

            PrintPlainText(sb, "Date", date.ToString("d.MMM.yyyy HH:mm"), true, temp.ToString());

            PrintPlainText(sb, "Session", temp.Clear().TimeFormat(sessionTime.TotalSeconds).ToString(),
                description: "Current session elapsed time, resets on restarts.");

            #endregion
        }

        void CheckSettings()
        {
            Log.Info("[DEV] Checking world settings for new settings...");

            TestRun = true;
            AppendSettings();
            TestRun = false;

            // ignored members
            KnownFields.Add("SubtypeId");
            KnownFields.Add("SubtypeName");
            KnownFields.Add("TypeId");

            IEnumerable<MemberInfo> members = TypeExtensions.GetDataMembers(typeof(MyObjectBuilder_SessionSettings), true, true, false, true, false, true, true, false);

            bool foundNewSettings = false;

            foreach(MemberInfo member in members)
            {
                if(!KnownFields.Contains(member.Name))
                {
                    foundNewSettings = true;
                    Log.Info($"New setting: {member.Name}");
                }
            }

            string trashFlagPrefix = nameof(MyTrashRemovalFlags) + ".";

            foreach(MyTrashRemovalFlags flag in MyEnum<MyTrashRemovalFlags>.Values)
            {
                switch(flag)
                {
                    case MyTrashRemovalFlags.Default:
                    case MyTrashRemovalFlags.None:
                    // used by the trash collection to flag grids, not as settings...
                    case MyTrashRemovalFlags.WithBlockCount:
                    case MyTrashRemovalFlags.DistanceFromPlayer:
                    case MyTrashRemovalFlags.Indestructible:
                        continue;
                }

                string flagName = MyEnum<MyTrashRemovalFlags>.GetName(flag);
                if(!KnownFields.Contains(trashFlagPrefix + flagName))
                {
                    foundNewSettings = true;
                    Log.Info($"new TrashFlag: {flagName}");
                }
            }

            if(foundNewSettings)
                Log.Error($"[DEV] Found new server setting(s)! See log.", Log.PRINT_MESSAGE);
            else
                Log.Info("[DEV] Done, found nothing new.");
        }

        #region Print setting methods
        const string TrueValue = "on";
        const string FalseValue = "off";
        const string NullValue = "null";
        const string NumberFormat = "###,###,###,###,###,##0.#####";

        enum Formatting
        {
            Normal = 0,
            GrayedOut,
            Warning,
        }

        Formatting GrayIfFalse(bool enabled) => !enabled ? Formatting.GrayedOut : Formatting.Normal;
        Formatting GrayOrWarn(bool enabled, bool warn) => !enabled ? Formatting.GrayedOut : warn ? Formatting.Warning : Formatting.Normal;

        /// <summary>
        /// Note: if value is null, it will not add a newline.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sb"></param>
        /// <param name="fieldName"></param>
        /// <param name="value"></param>
        /// <param name="defaultValue"></param>
        /// <param name="shownInVanillaUI"></param>
        /// <param name="displayName"></param>
        /// <param name="description"></param>
        /// <param name="formatting"></param>
        void PrintSetting<T>(StringBuilder sb, string fieldName, T value, T defaultValue, bool shownInVanillaUI, string displayName, string description = null, Formatting formatting = Formatting.Normal)
        {
            if(TestRun)
            {
                KnownFields.Add(fieldName);

                Type type = typeof(T);

                if(type != typeof(int)
                && type != typeof(uint)
                && type != typeof(short)
                && type != typeof(float)
                && type != typeof(double)
                && type != typeof(string)
                && type != typeof(bool)
                && type != typeof(bool?)
                && type != typeof(MyGameModeEnum)
                && type != typeof(MyOnlineModeEnum)
                && type != typeof(MyBlockLimitsEnabledEnum)
                && type != typeof(MyEnvironmentHostilityEnum)
                && type != typeof(MyObjectBuilder_SessionSettings.LimitBlocksByOption))
                    Log.Error($"Setting {fieldName} is type: {type}");

                return;
            }

            if(value != null && defaultValue != null)
                description += (!string.IsNullOrEmpty(description) ? "\n" : "") + $"Default: {defaultValue.ToString()}  (in {DefaultFrom})";

            if(!string.IsNullOrEmpty(fieldName) && ShowInternal)
            {
                if(!fieldName.StartsWith(nameof(MyTrashRemovalFlags)))
                    description += (!string.IsNullOrEmpty(description) ? "\n" : "") + $"Internal setting name: {fieldName}";
            }

            if(!string.IsNullOrEmpty(description))
                CurrentColumn.AddTooltip(sb, description);

            #region Append display name
            bool enabled = formatting != Formatting.GrayedOut;

            sb.Append(LabelPrefix);

            bool isNew = displayName.StartsWith(NewSettingTag);

            if(isNew)
            {
                sb.Color(NewSettingColor).Append("*<reset>");
            }

            sb.Color(enabled ? LabelColor : LabelColorDisabled);

            if(isNew)
                sb.Append(displayName, NewSettingTag.Length, displayName.Length - NewSettingTag.Length);
            else
                sb.Append(displayName);

            sb.Append(": ");
            #endregion

            if(value != null)
            {
                Color valueColor = ValueColorDisabled;
                if(enabled)
                {
                    if(formatting == Formatting.Warning)
                        valueColor = ValueColorWarning;
                    else if(typeof(T) == typeof(string) && defaultValue == null)
                        valueColor = Color.White;
                    else if(value.Equals(defaultValue))
                        valueColor = ValueColorDefault;
                    else
                        valueColor = ValueColorChanged;
                }

                sb.Color(valueColor).Append(value.ToString()).Append('\n');
            }
        }

        void PrintSetting(StringBuilder sb, string fieldName, bool value, bool defaultValue, bool shownInVanillaUI,
            string displayName, string description = null, Formatting formatting = Formatting.Normal)
        {
            string v = value ? TrueValue : FalseValue;
            string dv = defaultValue ? TrueValue : FalseValue;
            PrintSetting(sb, fieldName, v, dv, shownInVanillaUI, displayName, description, formatting);
        }

        void PrintSetting(StringBuilder sb, string fieldName, bool? value, bool? defaultValue, bool shownInVanillaUI,
            string displayName, string description = null, Formatting formatting = Formatting.Normal)
        {
            string v = value == null ? NullValue : value.Value ? TrueValue : FalseValue;
            string dv = defaultValue == null ? NullValue : defaultValue.Value ? TrueValue : FalseValue;
            PrintSetting(sb, fieldName, v, dv, shownInVanillaUI, displayName, description, formatting);
        }

        void PrintFormattedNumber(StringBuilder sb, string fieldName, double value, double defaultValue, bool shownInVanillaUI,
            string displayName, string suffix, string description = null, Formatting formatting = Formatting.Normal, string valueForZero = null)
        {
            string val = value.ToString(NumberFormat) + suffix;
            string defVal = defaultValue.ToString(NumberFormat) + suffix;

            if(valueForZero != null)
            {
                if(value == 0)
                    val = valueForZero;

                if(defaultValue == 0)
                    defVal = valueForZero;
            }

            PrintSetting(sb, fieldName, val, defVal, shownInVanillaUI, displayName, description, formatting);
        }

        void PrintBlockLimits(StringBuilder sb, string fieldLimits, SerializableDictionary<string, short> limits, SerializableDictionary<string, short> defaultLimits,
            string fieldLimitBy, MyObjectBuilder_SessionSettings.LimitBlocksByOption limitBy, MyObjectBuilder_SessionSettings.LimitBlocksByOption defaultLimitBy, bool shownInVanillaUI,
            string displayName, string description = null, Formatting formatting = Formatting.Normal)
        {
            if(TestRun)
            {
                KnownFields.Add(fieldLimits);
                KnownFields.Add(fieldLimitBy);
                return;
            }

            PrintSetting<string>(sb, $"{fieldLimits} and {fieldLimitBy}", $"(by {limitBy})", $"(by {defaultLimitBy})", shownInVanillaUI, displayName, description, formatting);

            bool valuePresent = (limits?.Dictionary != null && limits.Dictionary.Count > 0);
            bool defaultPresent = (defaultLimits?.Dictionary != null && defaultLimits.Dictionary.Count > 0);

            Color valueColor = ValueColorDefault;
            if(formatting == Formatting.GrayedOut)
                valueColor = ValueColorDisabled;
            else if(formatting == Formatting.Warning)
                valueColor = ValueColorWarning;
            else if(defaultPresent != valuePresent)
                valueColor = ValueColorChanged;

            ScrollableBlockLimits.Reset();
            bool scroll = valuePresent && limits.Dictionary.Count > ScrollableBlockLimits.DisplayLines;

            int sbIndex = sb.Length;

            if(!valuePresent)
            {
                sb.Append(LabelPrefix).Color(valueColor).Append("  (Empty)\n");
            }
            else
            {
                HashSet<MyDefinitionId> skipBlocks = new HashSet<MyDefinitionId>();

                StringBuilder tooltip = new StringBuilder(512);

                foreach(KeyValuePair<string, short> kv in limits.Dictionary)
                {
                    string key = kv.Key;
                    short limit = kv.Value;

                    int startIndex = sb.Length;
                    tooltip.Clear();

                    sb.Append(LabelPrefix).Append("  ");

                    if(limitBy == MyObjectBuilder_SessionSettings.LimitBlocksByOption.BlockPairName)
                    {
                        MyCubeBlockDefinitionGroup pairDef = MyDefinitionManager.Static.TryGetDefinitionGroup(key);
                        if(pairDef != null)
                        {
                            sb.Color(valueColor).Number(limit).Append("x ");

                            string nameLarge = pairDef.Large?.DisplayNameText;
                            string nameSmall = pairDef.Small?.DisplayNameText;

                            if(nameLarge == nameSmall)
                                sb.Append(nameLarge).Append(" (L+S)");
                            else if(nameLarge != null && nameSmall == null)
                                sb.AppendMaxLength(nameLarge, 24).Append(" (L)");
                            else if(nameLarge == null && nameSmall != null)
                                sb.AppendMaxLength(nameSmall, 24).Append(" (S)");
                            else
                                sb.AppendMaxLength(nameSmall, 16).Append(" & ").AppendMaxLength(nameSmall, 16);

                            tooltip.Append("BlockPairName: ").Append(key);
                        }
                        else
                        {
                            sb.Color(Color.Gray).Number(limit).Append("x ").Append(key);
                            tooltip.Append("This BlockPairName is not used by any block.");
                        }
                    }
                    else if(limitBy == MyObjectBuilder_SessionSettings.LimitBlocksByOption.Tag)
                    {
                        MyCubeBlockTagDefinition tagDef = MyDefinitionManager.Static.GetTagDefinition(key);
                        if(tagDef != null)
                        {
                            sb.Color(valueColor).Number(limit).Append("x ");

                            sb.Append(tagDef.DisplayNameText).Append(" (covers ").Append(tagDef.Blocks.Length).Append(" blocks)");

                            tooltip.Append("Blocks in this BlocksTag:\n");

                            skipBlocks.Clear();

                            foreach(MyDefinitionId blockId in tagDef.Blocks)
                            {
                                if(skipBlocks.Contains(blockId))
                                    continue;

                                MyCubeBlockDefinition blockDef;
                                if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(blockId, out blockDef))
                                {
                                    MyCubeBlockDefinitionGroup pairDef = MyDefinitionManager.Static.TryGetDefinitionGroup(blockDef.BlockPairName);
                                    if(pairDef != null)
                                    {
                                        if(pairDef.Small != null)
                                            skipBlocks.Add(pairDef.Small.Id);
                                        if(pairDef.Large != null)
                                            skipBlocks.Add(pairDef.Large.Id);

                                        string nameLarge = pairDef.Large?.DisplayNameText;
                                        string nameSmall = pairDef.Small?.DisplayNameText;

                                        if(nameLarge == nameSmall)
                                            tooltip.Append(nameLarge).Append(" (L+S)");
                                        else if(nameLarge != null && nameSmall == null)
                                            tooltip.AppendMaxLength(nameLarge, 24).Append(" (L)");
                                        else if(nameLarge == null && nameSmall != null)
                                            tooltip.AppendMaxLength(nameSmall, 24).Append(" (S)");
                                        else
                                            tooltip.AppendMaxLength(nameSmall, 16).Append(" & ").AppendMaxLength(nameSmall, 16);
                                    }
                                    else
                                    {
                                        tooltip.Append("  ").Append(blockDef.DisplayNameText).Append(" (").Append(blockDef.CubeSize == MyCubeSize.Large ? "L" : "S").Append(")");
                                    }
                                }
                                else
                                {
                                    tooltip.Append("  ").Append("(Inexistent:").IdTypeSubtypeFormat(blockId).Append(")");
                                }
                                tooltip.Append('\n');
                            }
                        }
                        else
                        {
                            sb.Color(Color.Gray).Number(limit).Append("x ").Append(key);
                            tooltip.Append("This BlockTag does not exist.");
                        }
                    }
                    else
                    {
                        sb.Color(Color.Red).Append($"Not implemented for a new LimitBlocksBy value: {limitBy}").ResetFormatting();
                    }

                    tooltip.TrimEndWhitespace();

                    if(scroll)
                    {
                        int len = sb.Length - startIndex;
                        ScrollableBlockLimits.Add(sb.ToString(startIndex, len), tooltip.ToString());
                        sb.Length -= len; // erase!
                    }
                    else
                    {
                        CurrentColumn.AddTooltip(sb, tooltip.ToString());

                        sb.Append('\n');
                    }
                }
            }

            if(scroll)
            {
                ScrollableBlockLimits.Finish(CurrentColumn, sbIndex);
            }
        }

        void PrintSuppressedWarnings(StringBuilder sb, string fieldName, List<string> value, List<string> defaultValue, bool shownInVanillaUI, string displayName, string description = null)
        {
            if(TestRun)
            {
                KnownFields.Add(fieldName);
                return;
            }

            PrintSetting<string>(sb, fieldName, null, null, shownInVanillaUI, displayName, description);

            bool valuePresent = (value != null && value.Count > 0);
            bool defaultPresent = (defaultValue != null && defaultValue.Count > 0);

            if(defaultPresent == valuePresent)
                sb.Color(ValueColorDefault);
            else
                sb.Color(ValueColorChanged);

            sb.Append('\n');

            if(!valuePresent)
            {
                sb.Append(LabelPrefix).Append("  (Empty)\n");
            }
            else
            {
                ScrollableWarnings.Reset();
                bool scroll = value.Count > ScrollableWarnings.DisplayLines;
                int sbIndex = sb.Length;

                int line = GetLine(sb);

                for(int i = 0; i < value.Count; i++)
                {
                    string text = value[i];
                    int startIdx = sb.Length;
                    sb.Append(LabelPrefix).Append("  ").Append(MyTexts.GetString(text));

                    string tooltip = $"Value name: {text}";

                    if(scroll)
                    {
                        int len = sb.Length - startIdx;
                        ScrollableWarnings.Add(sb.ToString(startIdx, len), tooltip);
                        sb.Length -= len; // erase!
                    }
                    else
                    {
                        CurrentColumn.SetTooltip(line + i, tooltip);

                        sb.Append('\n');
                    }
                }

                ScrollableWarnings.Finish(CurrentColumn, sbIndex);
            }
        }

        void PrintTrashFlag(StringBuilder sb, MyTrashRemovalFlags flag,
            string displayName, string description = null, Formatting formatting = Formatting.Normal)
        {
            bool val = MyAPIGateway.Session.SessionSettings.TrashFlags.HasFlags(flag);
            bool defVal = DefaultSettings.TrashFlags.HasFlags(flag);
            string flagName = nameof(MyTrashRemovalFlags) + "." + MyEnum<MyTrashRemovalFlags>.GetName(flag);

            if(ShowInternal)
            {
                description += (!string.IsNullOrEmpty(description) ? "\n" : "") + $"Internal setting name: TrashFlagsValue\nFlag integer: {(int)flag} (add all flag integers to make the final value for TrashFlagsValue)";
            }

            PrintSetting(sb, flagName, val, defVal, false, displayName, description, formatting);
        }

        void PrintEnvGraphicsChanges(StringBuilder sb, MyEnvironmentDefinition envDef, MyEnvironmentDefinition defaultEnvDef,
            string displayName, string description = null, Formatting formatting = Formatting.Normal)
        {
            if(TestRun)
                return;

            bool hasChanges = false;

            if(!envDef.LowLoddingSettings.Equals(defaultEnvDef.LowLoddingSettings))
            {
                hasChanges = true;
                description += "\n- Low model detail has changes";
            }

            if(!envDef.MediumLoddingSettings.Equals(defaultEnvDef.MediumLoddingSettings))
            {
                hasChanges = true;
                description += "\n- Medium model detail has changes";
            }

            if(!envDef.HighLoddingSettings.Equals(defaultEnvDef.HighLoddingSettings))
            {
                hasChanges = true;
                description += "\n- High model detail has changes";
            }

            if(!envDef.ExtremeLoddingSettings.Equals(defaultEnvDef.ExtremeLoddingSettings))
            {
                hasChanges = true;
                description += "\n- Extreme model detail has changes";
            }

            bool hasShadowChanges = false;
            if(!envDef.ShadowSettings.ShadowCascadeFrozen.SequenceEqual(defaultEnvDef.ShadowSettings.ShadowCascadeFrozen)
            || !envDef.ShadowSettings.ShadowCascadeSmallSkipThresholds.SequenceEqual(defaultEnvDef.ShadowSettings.ShadowCascadeSmallSkipThresholds)
            || !envDef.ShadowSettings.Cascades.SequenceEqual(defaultEnvDef.ShadowSettings.Cascades))
            {
                hasShadowChanges = true;
            }
            else
            {
                string current = MyAPIGateway.Utilities.SerializeToXML(envDef.ShadowSettings.Data);
                string defaults = MyAPIGateway.Utilities.SerializeToXML(defaultEnvDef.ShadowSettings.Data);
                if(current != defaults)
                {
                    hasShadowChanges = true;
                }
            }

            if(hasShadowChanges)
            {
                hasChanges = true;
                description += "\n- Shadow details have changes\n";
            }

            PrintSetting<string>(sb, string.Empty, null, null, false,
               displayName, description, formatting);

            Color valueColor = ValueColorDefault;
            string value = "Default";

            if(hasChanges)
            {
                valueColor = ValueColorChanged;
                value = "Changes (hover)";
            }

            if(formatting == Formatting.GrayedOut)
                valueColor = ValueColorDisabled;

            sb.Color(valueColor).Append(value).Append('\n');
        }

        void PrintPlanetsHavingEncounters(StringBuilder sb, string displayName, bool planetaryEncountersOn)
        {
            if(TestRun)
                return;

            StringBuilder description = new StringBuilder(512);

            description.Append("Not an actual setting, just computed information.\n");
            description.Append("All spawned planets are checked for compatibility with the\n  current planetary encounters (including mod ones if they use this system).\n");
            description.Append("\n");

            if(!planetaryEncountersOn)
            {
                description.Color(Color.Red).Append("Note: ").ResetFormatting()
                    .Append("Planetary encounters are OFF, therefore no planets in this world will actually have them.").NewCleanLine();
            }
            //else
            //{
            //    description.Color(Color.Lime).Append("Note: ").ResetFormatting()
            //        .Append("This is computed manually from cloned code, it might be inaccurate.").NewCleanLine();
            //}

            string value = "";

            List<MyPlanet> spawnedPlanets = Main.PlanetMonitor.Planets;

            if(spawnedPlanets.Count == 0)
            {
                value = "(No planets present)";
                description.Append("There are no planets spawned in this world.\n");
            }
            else
            {
                HashSet<string> voxelMats = new HashSet<string>();
                HashSet<string> voxelMatTypes = new HashSet<string>();

                if(planetaryEncountersOn)
                    description.Append("Planets that get planetary encounters:\n");
                else
                    description.Append("Planets that would've gotten planetary encounters:\n");

                int num = 0;

                // HACK: checks from MyPlanetaryEncountersGenerator.SpawnAreaChecker
                foreach(MyPlanet planet in spawnedPlanets.OrderBy(p => p.Generator.Id.SubtypeName))
                {
                    bool hasPlanteryEncounters = false;

                    foreach(var spawnDef in MyDefinitionManager.Static.GetSpawnGroupDefinitions())
                    {
                        if(!spawnDef.IsPlanetaryEncounter)
                            continue;

                        var allowedPlanets = spawnDef.PlanetaryInstallationSettings.Planets;
                        if(allowedPlanets != null && allowedPlanets.Count > 0)
                        {
                            if(!allowedPlanets.Any((s) => planet.Name.Contains(s)))
                                continue;
                        }

                        var allowedMatTypes = spawnDef.PlanetaryInstallationSettings.VoxelMaterials;
                        if(allowedMatTypes != null && allowedMatTypes.Count > 0)
                        {
                            voxelMats.Clear();
                            voxelMatTypes.Clear();

                            foreach(MyPlanetMaterialDefinition mat in planet.Generator.SurfaceMaterialTable) // <CustomMaterialTable>
                            {
                                if(mat.Material != null)
                                    voxelMats.Add(mat.Material);

                                if(mat.HasLayers)
                                {
                                    foreach(var layer in mat.Layers)
                                    {
                                        if(layer.Material != null)
                                            voxelMats.Add(layer.Material);
                                    }
                                }
                            }

                            foreach(var matGroup in planet.Generator.MaterialGroups) // <ComplexMaterials>
                            {
                                foreach(var rule in matGroup.MaterialRules)
                                {
                                    if(rule.Material != null)
                                        voxelMats.Add(rule.Material);

                                    if(rule.HasLayers)
                                    {
                                        foreach(var layer in rule.Layers)
                                        {
                                            if(layer.Material != null)
                                                voxelMats.Add(layer.Material);
                                        }
                                    }
                                }
                            }

                            foreach(var mat in voxelMats)
                            {
                                MyVoxelMaterialDefinition matDef;
                                if(MyDefinitionManager.Static.TryGetVoxelMaterialDefinition(mat, out matDef))
                                {
                                    voxelMatTypes.Add(matDef.MaterialTypeName);
                                }
                                else
                                {
                                    Log.Error($"Planet '{planet.Generator.Id.SubtypeName}' has unknown voxel material: '{mat}'");
                                }
                            }

                            bool found = false;

                            foreach(var matType in allowedMatTypes)
                            {
                                if(voxelMatTypes.Contains(matType))
                                {
                                    found = true;
                                    break;
                                }
                            }

                            if(!found)
                                continue;
                        }

                        hasPlanteryEncounters = true;
                        break;
                    }

                    if(hasPlanteryEncounters)
                    {
                        description.Append("• ").Append(planet.Generator.Id.SubtypeName).Append('\n');
                        num++;
                    }
                }

                if(num == 0)
                {
                    description.Append("  (none)\n");
                }

                if(num < spawnedPlanets.Count)
                {
                    description.Append("The other ").Append(spawnedPlanets.Count - num).Append(" don't support planetary encounters.");
                }

                value = $"{num} of {spawnedPlanets.Count} (hover)";
            }

            description.TrimEndWhitespace();

            PrintSetting<string>(sb, string.Empty, value, null, false,
                displayName, description.ToString(),
                GrayIfFalse(planetaryEncountersOn));
        }

        void PrintRangeSetting(StringBuilder sb, string fieldNameMin, string fieldNameMax,
                                                 double min, double max,
                                                 double defaultMin, double defaultMax, bool shownInVanillaUI,
                                                 string displayName, string suffix, string description = null, Formatting formatting = Formatting.Normal)
        {
            string fieldName = null;

            if(fieldNameMin != null && fieldNameMax != null)
            {
                if(TestRun)
                {
                    KnownFields.Add(fieldNameMin);
                    KnownFields.Add(fieldNameMax);
                }

                fieldName = $"{fieldNameMin} and {fieldNameMax}";
            }

            string val;
            string defVal;

            if(min == max)
                val = $"{max.ToString(NumberFormat)} {suffix}";
            else
                val = $"{min.ToString(NumberFormat)} ~ {max.ToString(NumberFormat)} {suffix}";

            if(defaultMin == defaultMax)
                defVal = $"{defaultMax.ToString(NumberFormat)} {suffix} (both min and max)";
            else
                defVal = $"{defaultMin.ToString(NumberFormat)} ~ {defaultMax.ToString(NumberFormat)} {suffix}";

            PrintSetting(sb, fieldName, val, defVal, shownInVanillaUI, displayName, description, formatting);
        }

        void PrintPlainText(StringBuilder sb, string displayName, string value, bool isDefault = true, string description = null, Formatting formatting = Formatting.Normal)
        {
            if(TestRun)
                return;

            PrintSetting<string>(sb, string.Empty, value, null, false,
                displayName, description, formatting);
        }

        //void PrintDSSetting<T>(StringBuilder sb, T value, T defaultValue, string displayName, string description = null, Formatting formatting = Formatting.Normal)
        //{
        //    const string AppendTooltip = "Only relevant in a dedicated server.";
        //
        //    string valStr = value == null ? "N/A" : value.ToString();
        //    string defStr = defaultValue == null ? "null" : defaultValue.ToString();
        //
        //    if(string.IsNullOrEmpty(description))
        //        description = AppendTooltip;
        //    else
        //        description = description + "\n" + AppendTooltip;
        //
        //    if(!MyAPIGateway.Utilities.IsDedicated)
        //        formatting = Formatting.GrayedOut;
        //
        //    PrintSetting(sb, string.Empty, valStr, defStr, false, displayName, description, formatting);
        //}
        #endregion Print setting methods

        class Column
        {
            public struct Tooltip
            {
                public string Text;
                public Action ClickAction;
            }

            public TextAPI.TextPackage Render;
            public Vector2D TextSize;
            public Dictionary<int, Tooltip> Tooltips;

            public Column(bool debug = false)
            {
                Render = new TextAPI.TextPackage(512, false, debug ? Constants.MatUI_Square : (MyStringId?)null);
                Render.HideWithHUD = false;
                Render.Scale = TextScale;

                if(debug)
                    Render.Background.BillBoardColor = Color.Red * 0.25f;

                Tooltips = new Dictionary<int, Tooltip>();
            }

            public void Reset()
            {
                Render.Visible = false;
                Render.TextStringBuilder.Clear();
                Tooltips.Clear();
            }

            /// <summary>
            /// Must be called before the ending newline
            /// </summary>
            public void AddTooltip(StringBuilder sb, string tooltip, Action clickAction = null)
            {
                int line = GetLine(sb);

                if(Tooltips.ContainsKey(line))
                    Log.Error($"Tooltip for line {line} already exists! New tooltip: {tooltip}");

                Tooltips[line] = new Tooltip()
                {
                    Text = tooltip,
                    ClickAction = clickAction,
                };
            }

            public void SetTooltip(int line, string tooltip, Action clickAction = null)
            {
                Tooltips[line] = new Tooltip()
                {
                    Text = tooltip,
                    ClickAction = clickAction,
                };
            }
        }

        class ScrollableSection
        {
            public struct Line
            {
                public string Text;
                public string Tooltip;
                public Action ClickAction;
            }

            public int DisplayLines;

            List<Line> Lines = new List<Line>();
            bool CanAdd;
            Column Column;
            Vector2D ColumnSize;
            int SBStartIndex;
            int SBEndIndex;
            int StartLine;
            int Scroll;
            bool FirstUpdate;
            float ScrollableHeight;
            Vector2D? MouseDragFrom;
            int ScrollAtDrag;

            HudAPIv2.BillBoardHUDMessage ScrollbarBgRender;
            HudAPIv2.BillBoardHUDMessage ScrollbarRender;

            //static readonly Color BgColor = new Color(70, 83, 90);
            static readonly Color BgColor = new Color(60, 76, 82);
            static readonly Color BarColor = new Color(86, 93, 104) * 1.4f;
            static readonly Color BarHighlight = Color.LightGray;
            static readonly Color BarDragged = Color.White;

            public ScrollableSection(int displayLines)
            {
                DisplayLines = displayLines;
            }

            public void CreateUIObjects()
            {
                MyStringId material = Constants.MatUI_Square;
                ScrollbarBgRender = new HudAPIv2.BillBoardHUDMessage(material, Vector2D.Zero, BgColor);
                ScrollbarRender = new HudAPIv2.BillBoardHUDMessage(material, Vector2D.Zero, BarColor);
                SetVisible(false, false);
            }

            public void SetVisible(bool bg, bool bar)
            {
                ScrollbarBgRender.Visible = bg;
                ScrollbarRender.Visible = bar;
            }

            public void Reset()
            {
                Lines.Clear();
                SetVisible(false, false);
                FirstUpdate = true;
                Scroll = 0;
                CanAdd = true;
            }

            public void Add(string line, string tooltip, Action clickAction = null)
            {
                if(!CanAdd)
                    Log.Error("Added scroll content when it shouldn't be, either Reset() or Finish() are missing!");

                if(line.IndexOf('\n') != -1)
                    Log.Error($"Scrollable line contains newline character: {line}");

                Lines.Add(new Line()
                {
                    Text = line + "\n",
                    Tooltip = tooltip,
                    ClickAction = clickAction,
                });
            }

            public void Finish(Column column, int sbIndex)
            {
                CanAdd = false;

                if(Lines.Count == 0)
                    return;

                Column = column;
                SBStartIndex = sbIndex;

                StringBuilder sb = Column.Render.TextStringBuilder;
                StartLine = GetLine(sb);

                string widestLine = null;
                double widestLen = 0;
                foreach(Line line in Lines)
                {
                    Vector2D size = BuildInfoMod.Instance.TextAPI.GetStringSize(line.Text, column.Render.Text.Scale);
                    if(widestLen < size.X)
                    {
                        widestLine = line.Text;
                        widestLen = size.X;
                    }
                }

                sb.Append(widestLine);

                for(int i = 1; i < DisplayLines; i++)
                {
                    sb.Append("\n-"); // needs a non-whitespace character as they get stripped from the end
                }

                SBEndIndex = sb.Length;

                FirstUpdate = true;
            }

            public bool Update(Vector2D mousePos)
            {
                if(Lines.Count == 0)
                    return false;

                if(FirstUpdate)
                {
                    Vector2D pxSize = HudAPIv2.APIinfo.ScreenPositionOnePX;

                    const float ScrollbarWidth = 10; // px
                    const float ScrollbarPadding = 4; // px
                    ScrollbarBgRender.Visible = true;
                    ScrollbarBgRender.Width = (float)pxSize.X * ScrollbarWidth;
                    ScrollbarBgRender.Height = LineHeight * DisplayLines;

                    Vector2D columnPos = Column.Render.Text.Offset + Column.Render.Text.Origin;
                    ColumnSize = Column.Render.Text.GetTextLength();

                    ScrollbarBgRender.Origin = new Vector2D(columnPos.X + ColumnSize.X + ScrollbarBgRender.Width / 2,
                                                            columnPos.Y - (LineHeight * StartLine) - (ScrollbarBgRender.Height / 2));

                    ScrollbarRender.Width = (float)pxSize.X * (ScrollbarWidth - ScrollbarPadding);
                    ScrollbarRender.Height = ScrollbarBgRender.Height / (Lines.Count / (float)DisplayLines);

                    double centerAtTop = ScrollbarBgRender.Height / 2;
                    float halfHeight = ScrollbarRender.Height / 2;
                    ScrollbarRender.Offset = ScrollbarBgRender.Origin + new Vector2D(0, centerAtTop - halfHeight);

                    float heightPadding = (float)pxSize.Y * ScrollbarPadding;
                    ScrollbarRender.Height -= heightPadding;

                    ScrollableHeight = ScrollbarBgRender.Height - ScrollbarRender.Height - heightPadding;

                    SetVisible(true, true);
                }

                bool changed = FirstUpdate;

                if(!FirstUpdate)
                {
                    int deltaScrollWheel = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                    if(deltaScrollWheel != 0)
                    {
                        Vector2D centerPos = ScrollbarBgRender.Origin + ScrollbarBgRender.Offset;
                        centerPos.X = Column.Render.Text.Origin.X + (ColumnSize.X + ScrollbarBgRender.Width) / 2;

                        Vector2D halfSize = new Vector2D(ColumnSize.X + ScrollbarBgRender.Width, LineHeight * DisplayLines) / 2;
                        BoundingBox2D scrollContentBB = new BoundingBox2D(centerPos - halfSize, centerPos + halfSize);

                        if(scrollContentBB.Contains(mousePos) == ContainmentType.Contains)
                        {
                            if(deltaScrollWheel > 0)
                                Scroll -= 3;
                            else if(deltaScrollWheel < 0)
                                Scroll += 3;

                            changed = true;
                        }
                    }

                    if(MouseDragFrom == null)
                    {
                        Vector2D centerPos = ScrollbarRender.Origin + ScrollbarRender.Offset;
                        Vector2D halfSize = new Vector2D(ScrollbarBgRender.Width, ScrollbarRender.Height) / 2;
                        BoundingBox2D scrollbarBB = new BoundingBox2D(centerPos - halfSize, centerPos + halfSize);

                        if(scrollbarBB.Contains(mousePos) == ContainmentType.Contains)
                        {
                            ScrollbarRender.BillBoardColor = BarHighlight;

                            if(MyAPIGateway.Input.IsLeftMousePressed())
                            {
                                ScrollbarRender.BillBoardColor = BarDragged;
                                MouseDragFrom = mousePos;
                                ScrollAtDrag = Scroll;
                            }
                        }
                        else
                        {
                            ScrollbarRender.BillBoardColor = BarColor;
                        }
                    }

                    if(MouseDragFrom != null)
                    {
                        if(MyAPIGateway.Input.IsLeftMousePressed())
                        {
                            double drag = (MouseDragFrom.Value.Y - mousePos.Y);
                            double dragPerLine = (drag / ScrollableHeight);
                            int draggableLines = (Lines.Count - DisplayLines);
                            Scroll = ScrollAtDrag + (int)Math.Round(dragPerLine * draggableLines);
                            changed = true;
                        }
                        else
                        {
                            ScrollbarRender.BillBoardColor = BarColor;
                            MouseDragFrom = null;
                        }
                    }
                }

                FirstUpdate = false;

                if(!changed)
                    return false;

                int maxScrollIndex = Lines.Count - DisplayLines;
                Scroll = MathHelper.Clamp(Scroll, 0, maxScrollIndex);
                int max = Math.Min(Scroll + DisplayLines, Lines.Count);

                StringBuilder sb = Column.Render.TextStringBuilder;

                int removeLen = Math.Min(SBEndIndex - SBStartIndex, sb.Length - SBStartIndex);
                if(removeLen > 0)
                    sb.Remove(SBStartIndex, removeLen);

                int idx = SBStartIndex;
                bool appendInstead = idx >= sb.Length;

                int lineNum = 0;

                for(int i = Scroll; i < max; i++)
                {
                    Line line = Lines[i];

                    if(appendInstead)
                        sb.Append(line.Text);
                    else
                        sb.Insert(idx, line.Text);

                    idx += line.Text.Length;

                    Column.SetTooltip(StartLine + lineNum, line.Tooltip);
                    lineNum++;
                }

                SBEndIndex = idx;

                double scrollRatio = -(Scroll / (double)maxScrollIndex);
                ScrollbarRender.Origin = new Vector2D(0, scrollRatio * ScrollableHeight);
                return true;
            }
        }

        void ResetFormat()
        {
            CurrentColumn = null;
            ColumnIndex = 0;

            foreach(Column column in Columns)
            {
                column.Reset();
            }
        }

        StringBuilder NextColumn()
        {
            if(TestRun)
                return null;

            FinishColumnFormat();

            CurrentColumn = Columns[ColumnIndex];
            CurrentColumn.Render.Visible = true;
            ColumnIndex++;
            return CurrentColumn.Render.TextStringBuilder;
        }

        void FinishColumnFormat()
        {
            if(CurrentColumn != null)
            {
                CurrentColumn.Render.TextStringBuilder.TrimEndWhitespace();
            }
        }

        static void Header(StringBuilder sb, string title)
        {
            if(sb == null)
                return;

            if(sb.Length > 0)
                sb.Append("\n");

            sb.Color(HeaderColor).Append(title).Append("<reset>\n");
        }

        static string TooltipOriginalName(string langKey)
        {
            return $"\nOriginal name in settings GUI: {MyTexts.GetString(langKey)}";
        }

        static string GetLimitsModeName(MyBlockLimitsEnabledEnum mode)
        {
            switch(mode)
            {
                case MyBlockLimitsEnabledEnum.NONE: return "Off";
                case MyBlockLimitsEnabledEnum.GLOBALLY: return "Global";
                case MyBlockLimitsEnabledEnum.PER_PLAYER: return "Per-Player";
                case MyBlockLimitsEnabledEnum.PER_FACTION: return "Per-Faction";
                default: return mode.ToString();
            }
        }

        static int GetLine(StringBuilder sb)
        {
            int line = 0;
            for(int i = 0; i < sb.Length; i++)
            {
                char c = sb[i];
                if(c == '\n')
                    line++;
            }
            return line;
        }

        static T ReadGameXML<T>(string filePath) where T : class
        {
            if(!MyAPIGateway.Utilities.FileExistsInGameContent(filePath))
            {
                Log.Error($"Couldn't find in game content folder: '<SE>\\Content\\{filePath}'");
                return null;
            }

            string xmlText;
            using(TextReader reader = MyAPIGateway.Utilities.ReadFileInGameContent(filePath))
            {
                xmlText = reader.ReadToEnd();
            }

            T deserialized;
            try
            {
                deserialized = MyAPIGateway.Utilities.SerializeFromXML<T>(xmlText);
            }
            catch(Exception e)
            {
                Log.Error($"Failed to deserialize: '<SE>\\Content\\{filePath}'\n{e}");
                return null;
            }

            if(deserialized == null)
            {
                Log.Error($"Failed to deserialize: '<SE>\\Content\\{filePath}'\nSerializeFromXML() returned null.");
            }

            return deserialized;
        }

        //static void ExportToFile(string text)
        //{
        //    StringBuilder fileNameSB = new StringBuilder(256);
        //    fileNameSB.Append("ServerInfo '");
        //    fileNameSB.Append(MyAPIGateway.Session.Name);
        //    fileNameSB.Append("' - ");
        //    fileNameSB.Append(DateTime.Now.ToString("yyyy-MM-dd HHmm"));
        //    fileNameSB.Append(".txt");
        //
        //    foreach(char invalidChar in Path.GetInvalidFileNameChars())
        //    {
        //        fileNameSB.Replace(invalidChar, '_');
        //    }
        //
        //    while(fileNameSB.IndexOf("__") != -1)
        //    {
        //        fileNameSB.Replace("__", "_");
        //    }
        //
        //    TextWriter writer = null;
        //    try
        //    {
        //        string fileName = fileNameSB.ToString();
        //
        //        writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(fileName, typeof(CommandServerInfo));
        //        writer.Write(text);
        //        writer.Flush();
        //
        //        string modStorageName = MyAPIGateway.Utilities.GamePaths.ModScopeName;
        //        Utils.ShowColoredChatMessage(BuildInfoMod.ModName, $"Exported server info to: %appdata%/SpaceEngineers/Storage/{modStorageName}/{fileName}", FontsHandler.GreenSh);
        //    }
        //    catch(Exception e)
        //    {
        //        Utils.ShowColoredChatMessage(BuildInfoMod.ModName, $"Failed to export server info! Exception: {e.Message}; see SE log for details.", FontsHandler.RedSh);
        //        Log.Error(e);
        //    }
        //    finally
        //    {
        //        writer?.Dispose();
        //    }
        //}
    }
}