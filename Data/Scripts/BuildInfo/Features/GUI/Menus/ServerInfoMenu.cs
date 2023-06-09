using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Library.Utils;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;
using VRageRender;
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
        MyObjectBuilder_SessionSettings DefaultSettings = new MyObjectBuilder_SessionSettings();
        MyEnvironmentDefinition DefaultEnvDef = null;

        HudAPIv2.BillBoardHUDMessage WindowBG;
        Button CloseButton;
        //HudAPIv2.BillBoardHUDMessage ButtonDebug;
        TextAPI.TextPackage TooltipRender;
        Vector2D PrevMousePos;
        Column.Tooltip? HoveredTooltip = null;

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
        const bool DebugDrawBoxes = false;
        const float CloseButtonScale = 1.2f;

        const string TooltipSettingModdable = "<color=gray>This is only changeable using mods.<reset>";
        //const string TooltipSettingGameUI = "<color=gray>This is a world setting. This in particular can be changed in the world options screen.<reset>";
        //const string TooltipSettingDSUI = "<color=gray>This is a world setting. This in particular can be changed in dedicated server UI or in sandbox_config.sbc file.<reset>";
        //const string TooltipSettingSaveFile = "<color=gray>This is a world setting. This in particular can only be changed in the sandbox_config.sbc file.<reset>";

        public ServerInfoMenu(bool testMode = false)
        {
            Main = BuildInfoMod.Instance;

            if(!testMode || BuildInfoMod.IsDevMod)
                ReadDefaults();
        }

        void ReadDefaults()
        {
            var starSystemConfig = ReadGameXML<MyObjectBuilder_WorldConfiguration>(@"CustomWorlds\Star System\sandbox_config.sbc");
            if(starSystemConfig != null)
            {
                DefaultSettings = starSystemConfig.Settings;
            }

            var envDef = ReadGameXML<MyObjectBuilder_Definitions>(@"Data\Environment.sbc");
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
                    Log.Error("Game's Environment.sbc does not contain the expected EnvironmentDefinition!");
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
            MyStringId material = MyStringId.GetOrCompute("BuildInfo_UI_Square");

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

            TooltipRender = new TextAPI.TextPackage(256, false, material);
            TooltipRender.Background.BillBoardColor = new Color(70, 83, 90);
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

                Main.MenuHandler.AddCursorRequest(nameof(ServerInfoMenu),
                    escapeCallback: () => CloseMenu(escPressed: true),
                    blockViewXY: true,
                    blockMoveAndRoll: false);

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
            Main.MenuHandler.RemoveCursorRequest(nameof(ServerInfoMenu));
            Main.MenuHandler.SetUpdateMenu(this, false);

            if(WindowBG == null)
                return;

            WindowBG.Visible = false;
            TooltipRender.Visible = false;
            CloseButton.SetVisible(false);

            foreach(ScrollableSection section in ScrollableSections)
            {
                section.SetVisible(false, false);
            }

            foreach(Column column in Columns)
            {
                column.Reset();
            }
        }

        public override void Update()
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
        }

        void UpdateTooltip(Vector2D mousePos)
        {
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
                    break;
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
                TooltipRender.TextStringBuilder.Clear().Append(HoveredTooltip.Value.Text);

                const float Padding = 0.01f; // scalar
                const double Offset = 32; // in px
                Vector2D px = HudAPIv2.APIinfo.ScreenPositionOnePX;
                Vector2D pos = mousePos + new Vector2D(px.X * Offset, px.Y * -Offset);
                Vector2D textLen = TooltipRender.Text.GetTextLength();

                pos.X = MathHelper.Clamp(pos.X, -1 + Padding, 1 - textLen.X - Padding);
                pos.Y = MathHelper.Clamp(pos.Y, -1 + Padding + Math.Abs(textLen.Y), 1 - Padding);

                TooltipRender.UpdateBackgroundSize(Padding, textLen);

                TooltipRender.Position = pos;
                TooltipRender.Visible = true;
            }
            else
            {
                TooltipRender.Visible = false;
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
                        clickAction = () =>
                        {
                            // TODO: mod.io? unknown link format to reach a mod by its id...
                            string link = "https://steamcommunity.com/sharedfiles/filedetails/?id=" + mod.PublishedFileId;
                            MyVisualScriptLogicProvider.OpenSteamOverlayLocal(link);
                            Utils.ShowColoredChatMessage("serverinfo", $"Opened overlay with {link}", FontsHandler.GreenSh);
                        };
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

            ExperimentalReason reasons = MyAPIGateway.Session.SessionSettings.GetExperimentalReason(remote: false);

            if(MyAPIGateway.Session.Mods.Count > 0)
                reasons |= ExperimentalReason.Mods;

            foreach(ExperimentalReason reason in MyEnum<ExperimentalReason>.Values)
            {
                if(reason == ExperimentalReason.ReasonMax
                || reason == ExperimentalReason.ExperimentalTurnedOnInConfiguration)
                    continue;

                bool? on = (reasons & reason) != 0;
                if(reason == ExperimentalReason.Plugins
                || reason == ExperimentalReason.InsufficientHardware)
                    on = null;

                string name = MyEnum<ExperimentalReason>.GetName(reason);

                // HACK: hardcodedly filled in details from GetExperimentalReason()
                switch(reason)
                {
                    case ExperimentalReason.Mods: name = "Mods present"; break;
                    case ExperimentalReason.ExperimentalMode: name = "Experimental mode forced on"; break;
                    case ExperimentalReason.AdaptiveSimulationQuality: name = "AdaptiveSimulation OFF"; break;
                    case ExperimentalReason.BlockLimitsEnabled: name = "BlockLimits OFF"; break;
                    case ExperimentalReason.ProceduralDensity: name = "ProceduralDensity >0.35"; break;
                    case ExperimentalReason.MaxFloatingObjects: name = "MaxFloatingObjects >100"; break;
                    case ExperimentalReason.PhysicsIterations: name = "PhysicsIterations not 8"; break;
                    case ExperimentalReason.TotalPCU: name = $"TotalPCU >{MyObjectBuilder_SessionSettings.MaxSafePCU}"; break;
                    case ExperimentalReason.MaxPlayers: name = $"MaxPlayers >{MyObjectBuilder_SessionSettings.MaxSafePlayers}"; break;
                    case ExperimentalReason.SunRotationIntervalMinutes: name = "SunRotationIntervalMinutes <=29"; break;
                    case ExperimentalReason.TotalBotLimit: name = "TotalBotLimit >32"; break;
                    case ExperimentalReason.StationVoxelSupport: name = "Maintain static on split ON (StationVoxelSupport)"; break;
                        //case ExperimentalReason.SyncDistance: name = "SyncDistance not 3000"; break;
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
                                                   "\n- TradeFactionsCount" +
                                                   "\n- StationsDistanceInnerRadius/End/Start";

            if(TestRun)
            {
                KnownFields.Add("TotalPCU"); // calculated PCU shown instead, and this value is shown in its tooltip

                //PrintTrashFlags(sb, nameof(settings.TrashFlagsValue), (MyTrashRemovalFlags)settings.TrashFlagsValue, (MyTrashRemovalFlags)DefSettings.TrashFlagsValue, false,
                //    "Trash Removal Flags", "Defines flags for trash removal system.", () => settings.TrashRemovalEnabled);
                KnownFields.Add("TrashFlagsValue"); // individual relevant flags are shown instead

                //PrintSetting(sb, nameof(settings.TradeFactionsCount), settings.TradeFactionsCount, defaults.TradeFactionsCount, false,
                //    "NPC Factions Count", "The number of NPC factions generated on the start of the world.");
                //PrintSetting(sb, nameof(settings.StationsDistanceInnerRadius), settings.StationsDistanceInnerRadius, defaults.StationsDistanceInnerRadius, false,
                //    "Stations Inner Radius", "The inner radius [m] (center is in 0,0,0), where stations can spawn. Does not affect planet-bound stations (surface Outposts and Orbital stations).");
                //PrintSetting(sb, nameof(settings.StationsDistanceOuterRadiusEnd), settings.StationsDistanceOuterRadiusEnd, defaults.StationsDistanceOuterRadiusEnd, false,
                //    "Stations Outer Radius End", "The outer radius [m] (center is in 0,0,0), where stations can spawn. Does not affect planet-bound stations (surface Outposts and Orbital stations).");
                //PrintSetting(sb, nameof(settings.StationsDistanceOuterRadiusStart), settings.StationsDistanceOuterRadiusStart, defaults.StationsDistanceOuterRadiusStart, false,
                //    "Stations Outer Radius Start", "The outer radius [m] (center is in 0,0,0), where stations can spawn. Does not affect planet-bound stations (surface Outposts and Orbital stations).");
                KnownFields.Add("TradeFactionsCount");
                KnownFields.Add("StationsDistanceInnerRadius");
                KnownFields.Add("StationsDistanceOuterRadiusEnd");
                KnownFields.Add("StationsDistanceOuterRadiusStart");

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

                KnownFields.Add("OptimalSpawnDistance"); // it's not ok for players to know this
                KnownFields.Add("SyncDistance"); // server owners would not like this exposed

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
                sb.Color(ValueColorDefault).Append("(StarSystem default)\n");
                sb.Color(ValueColorChanged).Append("(Changed)\n");
                sb.Color(ValueColorDisabled).Append("(Relies on something else)\n");
            }


            Header(sb, "General");

            PrintSetting(sb, nameof(settings.GameMode), settings.GameMode, DefaultSettings.GameMode, true,
                "Game Mode");
            PrintSetting(sb, nameof(settings.OnlineMode), settings.OnlineMode.ToString(), null, false,
                "Online Mode", "Offline means multiplayer is disabled (and local mods are allowed), while other values determine if and what players can join." +
                               "\nThe mode can be changed in F3 menu if not offline nor dedicated server.");
            PrintSetting(sb, nameof(settings.MaxPlayers), settings.MaxPlayers.ToString(), null, true,
                "Max Players", "The maximum number of players that can play at the same time in this server.", () => settings.OnlineMode != MyOnlineModeEnum.OFFLINE && settings.OnlineMode != MyOnlineModeEnum.PRIVATE);
            PrintFormattedNumber(sb, nameof(settings.AutoSaveInMinutes), settings.AutoSaveInMinutes, DefaultSettings.AutoSaveInMinutes, true,
                "Autosave interval", " min", "Defines autosave interval in minutes.");
            PrintSetting(sb, nameof(settings.ExperimentalMode), settings.ExperimentalMode, DefaultSettings.ExperimentalMode, false,
                "Experimental (hover for reason)", GetExperimentalTooltip());
            PrintSetting(sb, nameof(settings.FamilySharing), settings.FamilySharing, DefaultSettings.FamilySharing, false,
                "Allow family sharing", "Allow players that have the game from family sharing (they don't own it themselves) to join this server.");
            PrintSetting(sb, nameof(settings.Enable3rdPersonView), settings.Enable3rdPersonView, DefaultSettings.Enable3rdPersonView, true,
                "Allow 3rd Person Camera", "Enables 3rd person camera.");
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


            //Header(sb, "Chat");

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


            Header(sb, "Characters");

            PrintSetting(sb, nameof(settings.EnableJetpack), settings.EnableJetpack, DefaultSettings.EnableJetpack, true,
                "Jetpack", "Allows players to use their jetpack.");
            PrintSetting(sb, nameof(settings.SpawnWithTools), settings.SpawnWithTools, DefaultSettings.SpawnWithTools, true,
                "Spawn with Tools", "Enables spawning with tools in the inventory.");
            PrintFormattedNumber(sb, nameof(settings.CharacterSpeedMultiplier), settings.CharacterSpeedMultiplier, DefaultSettings.CharacterSpeedMultiplier, false,
                "On-foot speed Multiplier", "x", "Affects NPCs too.");
            PrintFormattedNumber(sb, nameof(settings.EnvironmentDamageMultiplier), settings.EnvironmentDamageMultiplier, DefaultSettings.EnvironmentDamageMultiplier, false,
                "Environment Damage Multiplier", "x", "This multiplier only applies for damage caused to a character by environment (affects NPCs too).");
            PrintFormattedNumber(sb, nameof(settings.BackpackDespawnTimer), settings.BackpackDespawnTimer, DefaultSettings.BackpackDespawnTimer, false,
                "Backpack Despawn Time", " min", "Sets the timer (minutes) for the backpack to be removed from the world. Default is 5 minutes.");


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
                "Respawn ships", "Enables respawn ships.");
            PrintFormattedNumber(sb, nameof(settings.SpawnShipTimeMultiplier), settings.SpawnShipTimeMultiplier, DefaultSettings.SpawnShipTimeMultiplier, true,
                "Respawn Ship Time Multiplier", "x", "The multiplier for respawn ship timer.");
            PrintSetting(sb, nameof(settings.RespawnShipDelete), settings.RespawnShipDelete, DefaultSettings.RespawnShipDelete, true,
                "Remove Respawn Ships on Logoff", "When enabled, respawn ship is removed after player logout.");


            Header(sb, "Ships & blocks");

            MyEnvironmentDefinition envDef = MyDefinitionManager.Static.EnvironmentDefinition;

            const string ShipSpeedTooltipAdd = "\nCharacter max speed is usually largest of the ship speeds + 10m/s (largest of character's on-foot speeds)\n" + TooltipSettingModdable;

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
                "Friendly missile damage", "Enable explosion damage from missiles being applied to its own grid.");
            PrintSetting(sb, nameof(settings.DestructibleBlocks), settings.DestructibleBlocks, DefaultSettings.DestructibleBlocks, true,
                "Destructible blocks", "Allows blocks to be destroyed.");
            PrintSetting(sb, nameof(settings.AdjustableMaxVehicleSpeed), settings.AdjustableMaxVehicleSpeed, DefaultSettings.AdjustableMaxVehicleSpeed, false,
                "Adjustable suspension speed limit", "Wether the speed limit slider is visible in terminal of suspension blocks.");
            PrintSetting(sb, nameof(settings.StationVoxelSupport), settings.StationVoxelSupport, DefaultSettings.StationVoxelSupport, true,
                "Maintain static on split", "By enabling this option grids will no longer turn dynamic when disconnected from static grids." + TooltipOriginalName("WorldSettings_StationVoxelSupport"));
            PrintSetting(sb, nameof(settings.EnableConvertToStation), settings.EnableConvertToStation, DefaultSettings.EnableConvertToStation, true,
                "Allow convert to station", "Allows players to use Convert to Station from info tab in terminal, making the grid static.");
            PrintSetting(sb, nameof(settings.ThrusterDamage), settings.ThrusterDamage, DefaultSettings.ThrusterDamage, true,
                "Thruster Damage", "Thruster blocks damage blocks and living things in their flame path.");
            PrintSetting(sb, nameof(settings.EnableSubgridDamage), settings.EnableSubgridDamage, DefaultSettings.EnableSubgridDamage, false,
                "Sub-Grid Physical Damage", "Allows physically connected grids to damage eachother from physical forces.");
            PrintSetting(sb, nameof(settings.EnableToolShake), settings.EnableToolShake, DefaultSettings.EnableToolShake, true,
                "Tool Shake", "Ship drills move the ship while mining and ship grinders move both the tool's ship and the grinded ship.");
            PrintSetting(sb, nameof(settings.EnableIngameScripts), settings.EnableIngameScripts, DefaultSettings.EnableIngameScripts, true,
                "Programmable Block Scripts", "Allows players to use scripts in programmable block (decoration otherwise).");
            PrintSetting(sb, nameof(settings.EnableScripterRole), settings.EnableScripterRole, DefaultSettings.EnableScripterRole, true,
                "Scripter Role", "Adds a Scripter role, only Scripters and higher ranks will be able to paste and modify scripts in programmable block.", () => settings.EnableIngameScripts);
            PrintSetting(sb, nameof(settings.EnableSupergridding), settings.EnableSupergridding, DefaultSettings.EnableSupergridding, false,
                "Supergridding", "Allows supergridding exploit to be used (placing block on wrong size grid, e.g. jumpdrive on smallgrid).");


            Header(sb, "PvP");

            PrintSetting(sb, nameof(settings.EnableMatchComponent), settings.EnableMatchComponent, DefaultSettings.EnableMatchComponent, false,
                "Match Enabled");
            PrintFormattedNumber(sb, nameof(settings.MatchRestartWhenEmptyTime), settings.MatchRestartWhenEmptyTime, DefaultSettings.MatchRestartWhenEmptyTime, false,
                "Match Restart When Empty", " min", "Server will restart after specified time (minutes), when it's empty after match started. Works only in PvP scenarios.\n0 means disabled.", () => settings.EnableMatchComponent);
            PrintFormattedNumber(sb, nameof(settings.MatchDuration), settings.MatchDuration, DefaultSettings.MatchDuration, false,
                "Match Duration", " min", "Duration of Match phase of the match.", () => settings.EnableMatchComponent);
            PrintFormattedNumber(sb, nameof(settings.PreMatchDuration), settings.PreMatchDuration, DefaultSettings.PreMatchDuration, false,
                "Pre-Match Duration", " min", "Duration of PreMatch phase of the match.", () => settings.EnableMatchComponent);
            PrintFormattedNumber(sb, nameof(settings.PostMatchDuration), settings.PostMatchDuration, DefaultSettings.PostMatchDuration, false,
                "Post-Match Duration", " min", "Duration of PostMatch phase of the match.", () => settings.EnableMatchComponent);
            PrintSetting(sb, nameof(settings.EnableTeamScoreCounters), settings.EnableTeamScoreCounters, DefaultSettings.EnableTeamScoreCounters, false,
                "Team Score Counters", "Show team scores at the top of the screen.", () => settings.EnableMatchComponent);
            PrintSetting(sb, nameof(settings.EnableFriendlyFire), settings.EnableFriendlyFire, DefaultSettings.EnableFriendlyFire, false,
                "Friendly Fire", "If disabled, character damage from friendlies is reduced.");
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


            sb = NextColumn(); // ------------------------------------------------------------------------------------------------------------------------------


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


            Header(sb, "Environment");

            PrintSetting(sb, nameof(settings.EnableSunRotation), settings.EnableSunRotation, DefaultSettings.EnableSunRotation, true,
                "Sun Rotation", "Sun rotates in skybox based on sun rotation interval setting.");
            PrintFormattedNumber(sb, nameof(settings.SunRotationIntervalMinutes), settings.SunRotationIntervalMinutes, DefaultSettings.SunRotationIntervalMinutes, true,
                "Sun Rotation Interval", " min", "Defines interval of one rotation of the sun.", () => settings.EnableSunRotation);
            PrintSetting(sb, nameof(settings.RealisticSound), settings.RealisticSound, DefaultSettings.RealisticSound, true,
                "Realistic Sound", "Enables sounds to be muffled or not heard at all depending on the medium sound travels through (air, contact, void)");
            PrintSetting(sb, nameof(settings.EnableOxygen), settings.EnableOxygen, DefaultSettings.EnableOxygen, true,
                "Oxygen", "Enables oxygen in the world.");
            PrintSetting(sb, nameof(settings.EnableOxygenPressurization), settings.EnableOxygenPressurization, DefaultSettings.EnableOxygenPressurization, true,
                "Airtightness", "Enables grids interiors to be processed for airtightness (requires oxygen to be enabled)", () => settings.EnableOxygen);
            PrintFormattedNumber(sb, nameof(settings.WorldSizeKm), settings.WorldSizeKm, DefaultSettings.WorldSizeKm, true,
                "Space boundary", " km", "Defines how far you can go outwards from center of the map." + TooltipOriginalName("ServerDetails_WorldSizeKm"));
            PrintSetting(sb, nameof(settings.WeatherSystem), settings.WeatherSystem, DefaultSettings.WeatherSystem, false,
                "Automatic weather system", "Enable automatic weather generation on planets.");
            PrintSetting(sb, nameof(settings.EnvironmentHostility), settings.EnvironmentHostility, DefaultSettings.EnvironmentHostility, true,
                "Meteorite showers", $"Enables meteorites, available difficulties: {string.Join(", ", Enum.GetNames(typeof(MyEnvironmentHostilityEnum)))}" + TooltipOriginalName("WorldSettings_EnvironmentHostility"));
            PrintSetting(sb, nameof(settings.EnableVoxelDestruction), settings.EnableVoxelDestruction, DefaultSettings.EnableVoxelDestruction, true,
                "Voxel Destruction", "Enables voxel destructions.");
            PrintSetting(sb, nameof(settings.VoxelGeneratorVersion), settings.VoxelGeneratorVersion.ToString(), null, false,
                "Voxel Generator version", "Voxel generator determines what shapes voxels have for a given seed number." +
                                           "\nIt exists to maintain how voxels look in existing worlds when new voxel generators are added by developers." +
                                           "\nShould not be changed unless you understand how voxels are generated." +
                                          $"\nLatest version: {new MyObjectBuilder_SessionSettings().VoxelGeneratorVersion}"); // HACK: this is more reliably going to be set to latest than StarSystem's version.
            PrintSetting(sb, nameof(settings.ProceduralDensity), settings.ProceduralDensity, DefaultSettings.ProceduralDensity, true,
                "Procedural Density", "Defines density of the procedurally generated content.");
            PrintSetting(sb, nameof(settings.ProceduralSeed), settings.ProceduralSeed, DefaultSettings.ProceduralSeed, false,
                "Procedural Seed", "Defines unique starting seed for the procedurally generated content.");
            PrintSetting(sb, nameof(settings.DepositsCountCoefficient), settings.DepositsCountCoefficient, DefaultSettings.DepositsCountCoefficient, false,
                "Deposits Count Coefficient", "Resource deposits count coefficient for generated world content (voxel generator version > 2).", () => settings.VoxelGeneratorVersion > 2);
            PrintSetting(sb, nameof(settings.DepositSizeDenominator), settings.DepositSizeDenominator, DefaultSettings.DepositSizeDenominator, false,
                "Deposit Size Denominator", "Resource deposit size denominator for generated world content (voxel generator version > 2).", () => settings.VoxelGeneratorVersion > 2);


            Header(sb, "NPCs");

            PrintSetting(sb, nameof(settings.CargoShipsEnabled), settings.CargoShipsEnabled, DefaultSettings.CargoShipsEnabled, true,
                "Cargo Ships", "Enables spawning of cargo ships.");
            PrintSetting(sb, nameof(settings.EnableEncounters), settings.EnableEncounters, DefaultSettings.EnableEncounters, true,
                "Encounters", "Enables random encounters in the world.");
            PrintSetting(sb, nameof(settings.EnableSpiders), settings.EnableSpiders, DefaultSettings.EnableSpiders, true,
                "Spiders", "Enables spawning of spiders in the world.");
            PrintSetting(sb, nameof(settings.EnableWolfs), settings.EnableWolfs, DefaultSettings.EnableWolfs, true,
                "Wolves", "Enables spawning of wolves in the world.");
            PrintSetting(sb, nameof(settings.TotalBotLimit), settings.TotalBotLimit, DefaultSettings.TotalBotLimit, false,
                "Max Bots", "Maximum number of organic bots in the world");
            PrintSetting(sb, nameof(settings.EnableDrones), settings.EnableDrones, DefaultSettings.EnableDrones, true,
                "Drones", "Enables spawning of drones in the world.");
            PrintSetting(sb, nameof(settings.MaxDrones), settings.MaxDrones, DefaultSettings.MaxDrones, false,
                "Max Drones", "");
            PrintSetting(sb, nameof(settings.EnableEconomy), settings.EnableEconomy, DefaultSettings.EnableEconomy, false,
                "Economy", "Enables economy features:" +
                           "\n- Generated hidden NPC factions." +
                           "\n- Generated space and planet-side stations (only on planets that were in world when this feature was turned on)." +
                           "\n- Enables currency system for players, ATMs, shop blocks, etc." +
                           "\nNote: this adds 4MB+ of save file bulk from the generated data, which does not get removed when economy is turned off.");
            PrintFormattedNumber(sb, nameof(settings.EconomyTickInSeconds), settings.EconomyTickInSeconds, DefaultSettings.EconomyTickInSeconds, false,
                "Economy update time", " sec", "Seconds between two economy updates (station contracts, etc)", () => settings.EnableEconomy);
            PrintSetting(sb, nameof(settings.EnableBountyContracts), settings.EnableBountyContracts, DefaultSettings.EnableBountyContracts, false,
                "Bounty Contracts", "If enabled bounty contracts will be available on stations.", () => settings.EnableEconomy);
            PrintSetting(sb, nameof(settings.EnableContainerDrops), settings.EnableContainerDrops, DefaultSettings.EnableContainerDrops, false,
                "Drop Containers", "Enables drop containers (unknown signals).");
            PrintFormattedNumber(sb, nameof(settings.MinDropContainerRespawnTime), settings.MinDropContainerRespawnTime, DefaultSettings.MinDropContainerRespawnTime, false,
                "Drop Container min spawn", " min", "Defines minimum respawn time for drop containers.");
            PrintFormattedNumber(sb, nameof(settings.MaxDropContainerRespawnTime), settings.MaxDropContainerRespawnTime, DefaultSettings.MaxDropContainerRespawnTime, false,
                "Drop Container max spawn", " min", "Defines maximum respawn time for drop containers.");


            Header(sb, "Combat");

            PrintSetting(sb, nameof(settings.WeaponsEnabled), settings.WeaponsEnabled, DefaultSettings.WeaponsEnabled, true,
                "Allow Weapons", "Determine if ship and handheld weapons can be used.");
            PrintSetting(sb, nameof(settings.InfiniteAmmo), settings.InfiniteAmmo, DefaultSettings.InfiniteAmmo, false,
                "Infinite ammo in Survival");
            PrintSetting(sb, nameof(settings.EnableRecoil), settings.EnableRecoil, DefaultSettings.EnableRecoil, false,
                "Hand weapons recoil");
            PrintSetting(sb, nameof(settings.AutoHealing), settings.AutoHealing, DefaultSettings.AutoHealing, true,
                "Auto-heal players", "Auto-healing heals players only in oxygen environments and during periods of not taking damage.");


            Header(sb, "Misc.");

            PrintSetting(sb, nameof(settings.EnableSpectator), settings.EnableSpectator, DefaultSettings.EnableSpectator, true,
                "Everyone spectator camera", "Allows <i>all players</i> to use spectator camera (F6-F9).\nWith this off, spectator is still allowed in creative mode or admin creative tools.");
            PrintSetting(sb, nameof(settings.EnableVoxelHand), settings.EnableVoxelHand, DefaultSettings.EnableVoxelHand, false,
                "Voxel Hands", "Only usable in creative mode or admin creative tools.\nAllows use of voxel hand tools to manipulate voxels (in toolbar config menu).");
            PrintSetting(sb, nameof(settings.EnableCopyPaste), settings.EnableCopyPaste, DefaultSettings.EnableCopyPaste, true,
                "Copy & Paste", "Usable only in creative mode or admin creative tools.\nEnables copy and paste feature.");
            PrintSuppressedWarnings(sb, nameof(settings.SuppressedWarnings), settings.SuppressedWarnings, DefaultSettings.SuppressedWarnings, false,
                "Suppressed Warnings", "Makes players ignore certain warnings from top-right red box popup, but not from the fully opened Shift+F1 menu.");
            PrintSetting(sb, string.Empty, "(hover)", null, false,
                "Undisclosed settings", "There are some settings that were intentionally not disclosed in this menu:" + UndisclosedSettingsList);


            sb = NextColumn(); // ------------------------------------------------------------------------------------------------------------------------------


            Header(sb, "Limits");

            PrintSetting(sb, nameof(settings.BlockLimitsEnabled), GetLimitsModeName(settings.BlockLimitsEnabled), GetLimitsModeName(DefaultSettings.BlockLimitsEnabled), true,
                "Limits mode", "Defines the mode that block&PCU limits use." +
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

            PrintSetting(sb, "", pcu.ToString(), null, true,
                labelInitialPCU, "Not the actual setting but a calculation depending on Limits mode:" +
                                $"\n  {GetLimitsModeName(MyBlockLimitsEnabledEnum.GLOBALLY)} = TotalPCU" +
                                $"\n  {GetLimitsModeName(MyBlockLimitsEnabledEnum.PER_PLAYER)} = TotalPCU / MaxPlayers" +
                                $"\n  {GetLimitsModeName(MyBlockLimitsEnabledEnum.PER_FACTION)} = TotalPCU / MaxFactions (MaxFactions cannot be lower than 1 in this mode)" +
                                $"\nTotalPCU is set to: {settings.TotalPCU} (default: {DefaultSettings.TotalPCU})", () => settings.BlockLimitsEnabled != MyBlockLimitsEnabledEnum.NONE);


            PrintSetting(sb, nameof(settings.MaxGridSize), settings.MaxGridSize, DefaultSettings.MaxGridSize, true,
                "Max blocks per grid", "The maximum number of blocks in one grid. 0 means no limit.", () => settings.BlockLimitsEnabled != MyBlockLimitsEnabledEnum.NONE);
            PrintSetting(sb, nameof(settings.MaxBlocksPerPlayer), settings.MaxBlocksPerPlayer, DefaultSettings.MaxBlocksPerPlayer, true,
                "Max blocks per player", "The maximum number of blocks per player. 0 means no limit.", () => settings.BlockLimitsEnabled != MyBlockLimitsEnabledEnum.NONE);

            PrintBlockLimits(sb, nameof(settings.BlockTypeLimits), settings.BlockTypeLimits, DefaultSettings.BlockTypeLimits, true,
                "Block limits", "Additional limits for specific blocks, respects Limit mode.", () => settings.BlockLimitsEnabled != MyBlockLimitsEnabledEnum.NONE);

            // HACK: like in MySession.MaxFactionsCount
            int maxFactions = (settings.BlockLimitsEnabled != MyBlockLimitsEnabledEnum.PER_FACTION ? settings.MaxFactionsCount : Math.Max(1, settings.MaxFactionsCount));
            PrintSetting(sb, nameof(settings.MaxFactionsCount), maxFactions, DefaultSettings.MaxFactionsCount, true,
                "Max player factions", "The max number of player factions. 0 means no limit." +
                                       "\nIf Limit mode is Per-Faction, this setting cannot be lower than 1.");
            PrintSetting(sb, nameof(settings.EnablePcuTrading), settings.EnablePcuTrading, DefaultSettings.EnablePcuTrading, false,
                "Allow PCU trading", "Enable trading of PCUs between players or factions depending on PCU settings.");
            PrintSetting(sb, nameof(settings.UseConsolePCU), settings.UseConsolePCU, DefaultSettings.UseConsolePCU, false,
                "Console PCU", "To conserve memory, some of the blocks have different PCU values for consoles.");
            // TODO list definitions that have non-null PCUConsole ?
            PrintSetting(sb, nameof(settings.PiratePCU), settings.PiratePCU, DefaultSettings.PiratePCU, false,
                "PCU for NPCs", "Number of Performance Cost Units allocated for NPCs.");


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
            PrintFormattedNumber(sb, nameof(settings.PlayerInactivityThreshold), settings.PlayerInactivityThreshold, DefaultSettings.PlayerInactivityThreshold, false,
                "Player Inactivity Threshold", " hours", "Defines player inactivity (time from logout) threshold for trash removal system." +
                                                         "\nWARNING: This will remove all grids of the player." +
                                                         "\n0 means off.");
            PrintFormattedNumber(sb, nameof(settings.RemoveOldIdentitiesH), settings.RemoveOldIdentitiesH, DefaultSettings.RemoveOldIdentitiesH, false,
                "Remove Old Identities", " hours", "Defines time in hours after which inactive identities that do not own any grids will be removed.\n0 means off.");


            Header(sb, "Grids Cleanup");

            PrintSetting(sb, nameof(settings.TrashRemovalEnabled), settings.TrashRemovalEnabled, DefaultSettings.TrashRemovalEnabled, false,
                "Grid Trash Removal");
            PrintTrashFlag(sb, MyTrashRemovalFlags.Fixed,
                "Static", "Remove static grids", () => settings.TrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.Stationary,
                "Non-moving", "Remove dynamic grids that are not currently moving", () => settings.TrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.Linear,
                "Drifting", "Remove dynamic grids that are linearly moving", () => settings.TrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.Accelerating,
                "Accelerating", "Remove dynamic grids that are accelerating.", () => settings.TrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.Powered,
                "Powered", "Remove grids that have electricity", () => settings.TrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.Controlled,
                "Controlled", "Remove grids even if they are controlled.", () => settings.TrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.WithProduction,
                "With production", "Remove grids that have production blocks", () => settings.TrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.WithMedBay,
                "With respawn points", "Remove grids that have medical rooms or survival kits.", () => settings.TrashRemovalEnabled);
            PrintFormattedNumber(sb, nameof(settings.BlockCountThreshold), settings.BlockCountThreshold, DefaultSettings.BlockCountThreshold, false,
                "With less than", " blocks", "Defines block count threshold for trash removal system.", () => settings.TrashRemovalEnabled);
            PrintFormattedNumber(sb, nameof(settings.PlayerDistanceThreshold), settings.PlayerDistanceThreshold, DefaultSettings.PlayerDistanceThreshold, false,
                "Distance from player", " m", "Defines player distance threshold for trash removal system.", () => settings.TrashRemovalEnabled);
            PrintSetting(sb, nameof(settings.OptimalGridCount), settings.OptimalGridCount, DefaultSettings.OptimalGridCount, false,
                "Optimal Grid Count", "By setting this, server will keep number of grids around this value." +
                          "\nWARNING: It ignores Powered and Fixed flags, Block Count and lowers Distance from player." +
                          "\n0 means off.", () => settings.TrashRemovalEnabled);


            Header(sb, "Voxel Cleanup");

            PrintSetting(sb, nameof(settings.VoxelTrashRemovalEnabled), settings.VoxelTrashRemovalEnabled, DefaultSettings.VoxelTrashRemovalEnabled, false,
                "Voxel reverting", "Enables system for voxel reverting.");
            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertAsteroids,
                "Revert asteroids", "", () => settings.VoxelTrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertBoulders,
                "Revert planet-side boulders", "", () => settings.VoxelTrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertMaterials,
                "Revert materials", "", () => settings.VoxelTrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertCloseToNPCGrids,
                "Close to NPC grids", "Revert voxel chunks close to NPC grids.", () => settings.VoxelTrashRemovalEnabled);
            PrintTrashFlag(sb, MyTrashRemovalFlags.RevertWithFloatingsPresent,
                "Close to floating objects", "Revert voxel chunks close to floating objects.", () => settings.VoxelTrashRemovalEnabled);
            PrintFormattedNumber(sb, nameof(settings.VoxelGridDistanceThreshold), settings.VoxelGridDistanceThreshold, DefaultSettings.VoxelGridDistanceThreshold, false,
                "Distance from grid", " m", "Only voxel chunks that are further from any grid will be reverted.", () => settings.VoxelTrashRemovalEnabled);
            PrintFormattedNumber(sb, nameof(settings.VoxelPlayerDistanceThreshold), settings.VoxelPlayerDistanceThreshold, DefaultSettings.VoxelPlayerDistanceThreshold, false,
                "Distance from player", " m", "Only voxel chunks that are further from player will be reverted.", () => settings.VoxelTrashRemovalEnabled);
            PrintFormattedNumber(sb, nameof(settings.VoxelAgeThreshold), settings.VoxelAgeThreshold, DefaultSettings.VoxelAgeThreshold, false,
                "Voxel Age", " min", "Voxel chunks older than this will be reverted.", () => settings.VoxelTrashRemovalEnabled);


            Header(sb, "Performance");

            PrintEnvGraphicsChanges(sb, envDef, DefaultEnvDef,
                "LOD&Shadow distances", "This can only be changed by mods.\nInfluences how model detail and shadow graphics option behave.\nThis is merely a notice, compare with mods list to ensure it's intended.");
            PrintFormattedNumber(sb, nameof(settings.ViewDistance), settings.ViewDistance, DefaultSettings.ViewDistance, true,
                "View Distance", " m", "");
            PrintSetting(sb, nameof(settings.MaxFloatingObjects), settings.MaxFloatingObjects, DefaultSettings.MaxFloatingObjects, true,
                "Max Floating Objects", "The maximum number of concurrent floating objects (loose ore, items, etc).\nOlder floating objects are removed when newer ones need to spawn.");
            PrintSetting(sb, nameof(settings.AdaptiveSimulationQuality), settings.AdaptiveSimulationQuality, DefaultSettings.AdaptiveSimulationQuality, false,
                "Adaptive Simulation Quality", "Enables adaptive simulation quality system. This system is useful if you have a lot of voxel deformations in the world and low simulation speed.");
            PrintSetting(sb, nameof(settings.EnableSelectivePhysicsUpdates), settings.EnableSelectivePhysicsUpdates, DefaultSettings.EnableSelectivePhysicsUpdates, false,
                "Selective Physics Updates", "When enabled game will update physics only in the specific clusters, which are necessary.\nOnly works on dedicated servers.");
            PrintSetting(sb, nameof(settings.SimplifiedSimulation), settings.SimplifiedSimulation, DefaultSettings.SimplifiedSimulation, false,
                "Simplified Simulation", "It is not recommended on for survival!" +
                                         "\nResources are not properly consumed, inventories are not updated and ammunition is not consumed.");
            PrintSetting(sb, nameof(settings.PhysicsIterations), settings.PhysicsIterations, DefaultSettings.PhysicsIterations, false,
                "Physics Iterations");
            PrintSetting(sb, nameof(settings.EnableOrca), settings.EnableOrca, DefaultSettings.EnableOrca, false,
                "Advanced ORCA algorithm", "Enable advanced Optimal Reciprocal Collision Avoidance algorithm.");
            PrintSetting(sb, nameof(settings.PredefinedAsteroids), settings.PredefinedAsteroids, DefaultSettings.PredefinedAsteroids, false,
                "Load predefined asteroids", "Determines if admin spawn menu has predefined asteroids list.\nNot using predefined asteroids in world helps memory usage (this setting does not remove not prevent them from being spawned, by encounters, mods, etc.)");
            PrintSetting(sb, nameof(settings.MaxPlanets), settings.MaxPlanets, DefaultSettings.MaxPlanets, false,
                "Max Planet Types", "Limit maximum number of types of planets in the world.");
            PrintSetting(sb, nameof(settings.MaxProductionQueueLength), settings.MaxProductionQueueLength, DefaultSettings.MaxProductionQueueLength, false,
                "Max Production Queue Length ", "Maximum assembler production queue size." +
                                                "\nIt becomes a problem when assemblers with no resources have lots of queued stacks, each requesting items through conveyor system.");

        }

        void CheckSettings()
        {
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
                    Log.Info($"new setting: {member.Name}");
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
                Log.Error($"Found new server setting(s)! See log.", Log.PRINT_MESSAGE);
        }

        void PrintSetting<T>(StringBuilder sb, string fieldName, T value, T defaultValue, bool shownInVanillaUI, string displayName, string description = null, Func<bool> condition = null)
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
                && type != typeof(MyEnvironmentHostilityEnum))
                    Log.Error($"Setting {fieldName} is type: {type}");

                return;
            }

            if(value != null && defaultValue != null)
                description += (!string.IsNullOrEmpty(description) ? "\n" : "") + $"Default value: {defaultValue.ToString()}";

            if(!string.IsNullOrEmpty(fieldName) && Main.Config.InternalInfo.Value)
            {
                if(!fieldName.StartsWith(nameof(MyTrashRemovalFlags)))
                    description += (!string.IsNullOrEmpty(description) ? "\n" : "") + $"Internal setting name: {fieldName}";
            }

            if(!string.IsNullOrEmpty(description))
                CurrentColumn.AddTooltip(sb, description);

            bool enabled = (condition?.Invoke() ?? true);

            sb.Append(LabelPrefix).Color(enabled ? LabelColor : LabelColorDisabled).Append(displayName).Append(": ");

            if(value != null)
            {
                Color valueColor = ValueColorDisabled;
                if(enabled)
                {
                    if(typeof(T) == typeof(string) && defaultValue == null)
                        valueColor = Color.White;
                    else if(value.Equals(defaultValue))
                        valueColor = ValueColorDefault;
                    else
                        valueColor = ValueColorChanged;
                }

                sb.Color(valueColor).Append(value.ToString()).Append('\n');
            }
        }

        const string TrueValue = "on";
        const string FalseValue = "off";
        const string NullValue = "null";

        void PrintSetting(StringBuilder sb, string fieldName, bool value, bool defaultValue, bool shownInVanillaUI, string displayName, string description = null, Func<bool> condition = null)
        {
            string v = value ? TrueValue : FalseValue;
            string dv = defaultValue ? TrueValue : FalseValue;
            PrintSetting(sb, fieldName, v, dv, shownInVanillaUI, displayName, description, condition);
        }

        void PrintSetting(StringBuilder sb, string fieldName, bool? value, bool? defaultValue, bool shownInVanillaUI, string displayName, string description = null, Func<bool> condition = null)
        {
            string v = value == null ? NullValue : value.Value ? TrueValue : FalseValue;
            string dv = defaultValue == null ? NullValue : defaultValue.Value ? TrueValue : FalseValue;
            PrintSetting(sb, fieldName, v, dv, shownInVanillaUI, displayName, description, condition);
        }

        //void PrintDSSetting<T>(StringBuilder sb, T value, T defaultValue, string displayName, string description = null, Func<bool> extraCondition = null)
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
        //    PrintSetting(sb, string.Empty, valStr, defStr, false, displayName, description, () => MyAPIGateway.Utilities.IsDedicated && (extraCondition?.Invoke() ?? true));
        //}

        void PrintFormattedNumber(StringBuilder sb, string fieldName, float value, float defaultValue, bool shownInVanillaUI,
        string displayName, string suffix, string description, Func<bool> condition = null)
        {
            string val = value.ToString("0.#####") + suffix;
            string defVal = defaultValue.ToString("0.#####") + suffix;

            PrintSetting(sb, fieldName, val, defVal, shownInVanillaUI, displayName, description, condition);
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
                MyStringId material = MyStringId.GetOrCompute("BuildInfo_UI_Square");
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
                        }
                        else
                        {
                            ScrollbarRender.BillBoardColor = BarColor;
                            MouseDragFrom = null;
                        }
                    }
                }

                FirstUpdate = false;

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

        void PrintBlockLimits(StringBuilder sb, string fieldName, SerializableDictionary<string, short> value, SerializableDictionary<string, short> defaultValue,
        bool shownInVanillaUI, string displayName, string description = null, Func<bool> condition = null)
        {
            if(TestRun)
            {
                KnownFields.Add(fieldName);
                return;
            }

            PrintSetting<string>(sb, fieldName, null, null, shownInVanillaUI, displayName, description, condition);

            bool valuePresent = (value?.Dictionary != null && value.Dictionary.Count > 0);
            bool defaultPresent = (defaultValue?.Dictionary != null && defaultValue.Dictionary.Count > 0);

            bool enabled = (condition?.Invoke() ?? true);

            Color valueColor = ValueColorDefault;
            if(!enabled)
                valueColor = ValueColorDisabled;
            else if(defaultPresent != valuePresent)
                valueColor = ValueColorChanged;

            ScrollableBlockLimits.Reset();
            bool scroll = valuePresent && value.Dictionary.Count > ScrollableBlockLimits.DisplayLines;

            sb.Append('\n');

            int sbIndex = sb.Length;

            if(!valuePresent)
            {
                sb.Append(LabelPrefix).Append("  (Empty)\n");
            }
            else
            {
                foreach(KeyValuePair<string, short> kv in value.Dictionary)
                {
                    string blockPairName = kv.Key;
                    short limit = kv.Value;

                    MyCubeBlockDefinitionGroup pairDef = MyDefinitionManager.Static.TryGetDefinitionGroup(blockPairName);
                    string tooltip;

                    int startIndex = sb.Length;

                    sb.Append(LabelPrefix).Append("  ");

                    if(pairDef != null)
                    {
                        sb.Color(valueColor).Append(limit).Append("x ");

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

                        tooltip = $"BlockPairName: {blockPairName}";
                    }
                    else
                    {
                        sb.Color(Color.Gray).Append(limit).Append("x ").Append(blockPairName);
                        tooltip = "This BlockPairName is not used by any block.";
                    }

                    if(scroll)
                    {
                        int len = sb.Length - startIndex;
                        ScrollableBlockLimits.Add(sb.ToString(startIndex, len), tooltip);
                        sb.Length -= len; // erase!
                    }
                    else
                    {
                        CurrentColumn.AddTooltip(sb, tooltip);

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
            string displayName, string description = null, Func<bool> condition = null)
        {
            bool val = MyAPIGateway.Session.SessionSettings.TrashFlags.HasFlags(flag);
            bool defVal = DefaultSettings.TrashFlags.HasFlags(flag);
            string flagName = nameof(MyTrashRemovalFlags) + "." + MyEnum<MyTrashRemovalFlags>.GetName(flag);

            if(Main.Config.InternalInfo.Value)
            {
                description += (!string.IsNullOrEmpty(description) ? "\n" : "") + $"Internal setting name: TrashFlagsValue\nFlag integer: {(int)flag} (add all flag integers to make the final value for TrashFlagsValue)";
            }

            PrintSetting(sb, flagName, val, defVal, false, displayName, description, condition);
        }

        void PrintEnvGraphicsChanges(StringBuilder sb, MyEnvironmentDefinition envDef, MyEnvironmentDefinition defaultEnvDef,
            string displayName, string description = null, Func<bool> condition = null)
        {
            if(sb == null)
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

            PrintSetting<string>(sb, string.Empty, null, null, true,
               displayName, description, condition);

            Color valueColor = ValueColorDefault;
            string value = "Default";

            if(hasChanges)
            {
                valueColor = ValueColorChanged;
                value = "Changes (hover)";
            }

            bool enabled = (condition?.Invoke() ?? true);
            if(!enabled)
                valueColor = ValueColorDisabled;

            sb.Color(valueColor).Append(value).Append('\n');
        }

        //void PrintTrashFlags(StringBuilder sb, string fieldName, MyTrashRemovalFlags value, MyTrashRemovalFlags defaultValue,
        //    bool shownInVanillaUI, string displayName, string description = null, Func<bool> condition = null)
        //{
        //    if(TestRun)
        //    {
        //        KnownFields.Add(fieldName);
        //        return;
        //    }

        //    bool enabled = (condition?.Invoke() ?? true);
        //    StringBuilder tooltipSB = new StringBuilder(512);

        //    sb.Append(LabelPrefix).Color(enabled ? LabelColor : LabelColorDisabled).Append(displayName).Append(": ");

        //    int labelLine = GetLine(sb);
        //    int valueLines = 0;

        //    tooltipSB.Append(description);
        //    tooltipSB.Append("\nAll flags:");

        //    MyTrashRemovalFlags[] flags = MyEnum<MyTrashRemovalFlags>.Values;
        //    for(int i = 0; i < flags.Length; i++)
        //    {
        //        MyTrashRemovalFlags flag = flags[i];
        //        if(flag == MyTrashRemovalFlags.None || flag == MyTrashRemovalFlags.Default)
        //            continue;

        //        string name = MyEnum<MyTrashRemovalFlags>.GetName(flag);
        //        string langKey = "ScreenDebugAdminMenu_" + name;
        //        string translated = MyTexts.GetString(langKey);
        //        if(translated != langKey)
        //            name = translated;

        //        bool on = value.HasFlags(flag);
        //        bool onDef = defaultValue.HasFlags(flag);
        //        if(on)
        //        {
        //            if(!enabled)
        //                sb.Color(ValueColorDisabled);
        //            else if(on == onDef)
        //                sb.Color(ValueColorDefault);
        //            else
        //                sb.Color(ValueColorChanged);

        //            sb.Append('\n').Append(LabelPrefix).Append("  ").Append(name);
        //            valueLines++;
        //        }

        //        if(on == onDef)
        //            tooltipSB.Color(ValueColorDefault);
        //        else
        //            tooltipSB.Color(ValueColorChanged);

        //        tooltipSB.Append("\n  ").Append(on ? "[x] " : "[  ] ").Append(name);
        //    }

        //    sb.Append('\n');

        //    if(tooltipSB.Length > 0)
        //    {
        //        string tooltipText = tooltipSB.ToString();

        //        for(int i = 0; i <= valueLines; i++)
        //        {
        //            AddTooltip(labelLine + i, tooltipText);
        //        }
        //    }
        //}

        //void PrintTrashFlags(StringBuilder sb, string fieldName, MyTrashRemovalFlags value, MyTrashRemovalFlags defaultValue,
        //    bool shownInVanillaUI, string displayName, string description = null, Func<bool> condition = null)
        //{
        //    if(TestRun)
        //    {
        //        KnownFields.Add(fieldName);
        //        return;
        //    }

        //    PrintSetting<string>(sb, fieldName, null, null, shownInVanillaUI, displayName, description, condition);

        //    bool enabled = (condition?.Invoke() ?? true);

        //    MyTrashRemovalFlags[] flags = MyEnum<MyTrashRemovalFlags>.Values;

        //    for(int i = 0; i < flags.Length; i++)
        //    {
        //        MyTrashRemovalFlags flag = flags[i];
        //        if(flag == MyTrashRemovalFlags.None || flag == MyTrashRemovalFlags.Default)
        //            continue;

        //        string tooltip = null;
        //        switch(flag)
        //        {
        //            case MyTrashRemovalFlags.Fixed: tooltip = "Static grids / stations"; break;

        //            case MyTrashRemovalFlags.RevertMaterials:
        //            case MyTrashRemovalFlags.RevertAsteroids:
        //            case MyTrashRemovalFlags.RevertBoulders:
        //            case MyTrashRemovalFlags.RevertCloseToNPCGrids:
        //            case MyTrashRemovalFlags.RevertWithFloatingsPresent:
        //                tooltip = "Used for voxel revert."; break;
        //        }

        //        string name = MyEnum<MyTrashRemovalFlags>.GetName(flag);

        //        bool on = value.HasFlags(flag);

        //        if(!enabled)
        //            sb.Color(ValueColorDisabled);
        //        else if(on == defaultValue.HasFlags(flag))
        //            sb.Color(ValueColorDefault);
        //        else
        //            sb.Color(ValueColorChanged);

        //        sb.Append("\n      ").Append(on ? "[x] " : "[  ] ").Append(name);

        //        if(tooltip != null)
        //            AddTooltip(sb, tooltip);
        //    }

        //    sb.Append('\n');
        //}

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
                Render = new TextAPI.TextPackage(512, false, debug ? MyStringId.GetOrCompute("BuildInfo_UI_Square") : (MyStringId?)null);
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