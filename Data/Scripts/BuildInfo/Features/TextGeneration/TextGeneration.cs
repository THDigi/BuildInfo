using System;
using System.Collections.Generic;
using System.Text;
using CoreSystems.Api;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Digi.Input;
using Digi.Input.Devices;
using ObjectBuilders.SafeZone;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.Definitions.SafeZone;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Library.Utils;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using Whiplash.WeaponFramework;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

// FIXME: internal info not vanishing on older cached blocks text when resetting config
// FIXME: box size gets cached from overlay lock on or something
// TODO: needs redesign to be more flexible and allow hotkey to show block definition info while aiming at a real one
// TODO change color scheme to indicate default vs changed with blue vs yellow, then other colors for "HEY LOOK AT ME" ...?

namespace Digi.BuildInfo.Features
{
    public class TextGeneration : ModComponent
    {
        #region Constants
        private const BlendTypeEnum FG_BLEND_TYPE = BlendTypeEnum.PostPP;

        private readonly MyStringId BG_MATERIAL = MyStringId.GetOrCompute("BuildInfo_UI_Square");
        private readonly Color BG_COLOR = new Color(41, 54, 62);
        private const float BG_EDGE = 0.02f; // added padding edge around the text boundary for the background image

        private const float CharInvVolM3Offset = 50 / 1000f; // subtracting 50L from char inv max volume to account for common tools

        private const float MENU_BG_OPACITY = 0.7f;

        private const int SCROLL_FROM_LINE = 2; // ignore lines to this line when scrolling, to keep important stuff like mass in view at all times; used in HUD notification view mode.
        private const int SPACE_SIZE = 8; // space character's width; used in HUD notification view mode.
        private const int MAX_LINES = 8; // max amount of HUD notification lines to print; used in HUD notification view mode.
        public const int MOD_NAME_MAX_LENGTH = 40;
        public const int PLAYER_NAME_MAX_LENGTH = 24;
        public const int BLOCK_NAME_MAX_LENGTH = 35;

        private const double FREEZE_MAX_DISTANCE_SQ = 50 * 50; // max distance allowed to go from the frozen block preview before it gets turned off.

        public const int CACHE_PURGE_TICKS = 60 * 30; // how frequent the caches are being checked for purging, in ticks
        public const int CACHE_EXPIRE_SECONDS = 60 * 5; // how long a cached string remains stored until it's purged, in seconds

        private readonly Vector2D TEXT_HUDPOS = new Vector2D(-0.9675, 0.49); // textAPI default left side position
        private readonly Vector2D TEXT_HUDPOS_WIDE = new Vector2D(-0.9675 / 3f, 0.49); // textAPI default left side position when using a really wide resolution
        private readonly Vector2D TEXT_HUDPOS_RIGHT = new Vector2D(0.9692, 0.26); // textAPI default right side position
        private readonly Vector2D TEXT_HUDPOS_RIGHT_WIDE = new Vector2D(0.9692 / 3f, 0.26); // textAPI default right side position when using a really wide resolution

        private readonly MyDefinitionId DEFID_MENU = new MyDefinitionId(typeof(MyObjectBuilder_GuiScreen)); // just a random non-block type to use as the menu's ID

        public readonly Color COLOR_BLOCKTITLE = new Color(50, 155, 255);
        public readonly Color COLOR_BLOCKVARIANTS = new Color(255, 233, 55);
        public readonly Color COLOR_NORMAL = Color.White;
        public readonly Color COLOR_GOOD = Color.Lime;
        public readonly Color COLOR_BAD = new Color(255, 10, 10);
        public readonly Color COLOR_WARNING = Color.Yellow;
        public readonly Color COLOR_UNIMPORTANT = Color.Gray;
        public readonly Color COLOR_PART = new Color(55, 255, 155);
        public readonly Color COLOR_MOD = Color.DeepSkyBlue;
        public readonly Color COLOR_MOD_TITLE = Color.GreenYellow;
        public readonly Color COLOR_OWNER = new Color(55, 255, 255);
        public readonly Color COLOR_INFO = new Color(69, 177, 227);
        public readonly Color COLOR_INTERNAL = new Color(125, 125, 255);
        public readonly Color COLOR_DLC = Color.DeepSkyBlue;
        public readonly Color COLOR_LIST = Color.White;
        public readonly Color COLOR_CONVEYORPORTS = new Color(255, 200, 0);
        public readonly Color COLOR_HIGHLIGHT = new Color(255, 200, 0);
        #endregion Constants

        public MyDefinitionId LastDefId; // last selected definition ID, can be set to MENU_DEFID too!
        public bool textShown = false;
        private bool aimInfoNeedsUpdate = false;
        private readonly HashSet<IMySlimBlock> ProjectedUnder = new HashSet<IMySlimBlock>();
        public Vector3D? LastGizmoPosition;
        public Cache cache = null; // currently selected cache to avoid another dictionary lookup in Draw()

        private int gridMassComputeCooldown;
        private float gridMassCache;
        private long prevSelectedGrid;

        // used by the textAPI view mode
        public readonly Dictionary<MyDefinitionId, Cache> CachedBuildInfoTextAPI = new Dictionary<MyDefinitionId, Cache>(MyDefinitionId.Comparer);
        private bool useLeftSide = true;
        private double prevAspectRatio = 1;
        private int lines;
        private int forceDrawTicks = 0;
        private StringBuilder textAPIlines = new StringBuilder(TEXTAPI_TEXT_LENGTH);
        private TextAPI.TextPackage textObject;
        private const int TEXTAPI_TEXT_LENGTH = 2048;

        // used by the HUD notification view mode
        public readonly List<IMyHudNotification> hudNotifLines = new List<IMyHudNotification>();
        public readonly Dictionary<MyDefinitionId, Cache> CachedBuildInfoNotification = new Dictionary<MyDefinitionId, Cache>(MyDefinitionId.Comparer);
        private int atLine = SCROLL_FROM_LINE;
        private long lastScroll = 0;
        private int largestLineWidth = 0;
        private List<HudLine> notificationLines = new List<HudLine>();

        // used in generating the block info text or menu for either view mode
        private int line = -1;
        private bool addLineCalled = false;

        // used to quickly find the format method for block types
        private delegate void TextGenerationCall(MyCubeBlockDefinition def);
        private readonly Dictionary<MyObjectBuilderType, TextGenerationCall> formatLookup
                   = new Dictionary<MyObjectBuilderType, TextGenerationCall>(MyObjectBuilderType.Comparer);

        public TextGeneration(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
        }

        public override void RegisterComponent()
        {
            InitLookups();

            Main.TextAPI.Detected += TextAPI_APIDetected;
            Main.GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            Main.GUIMonitor.OptionsMenuClosed += GUIMonitor_OptionsMenuClosed;
            Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;

            ReCheckSide();
        }

        public override void UnregisterComponent()
        {
            Main.TextAPI.Detected -= TextAPI_APIDetected;
            Main.GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            Main.GUIMonitor.OptionsMenuClosed -= GUIMonitor_OptionsMenuClosed;
            Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
        }

        private void TextAPI_APIDetected()
        {
            // FIXME: doesn't re-show the menu if in it while this happens...
            Main.TextGeneration.HideText(); // force a re-check to make the HUD -> textAPI transition
        }

        private void GameConfig_HudStateChanged(HudState prevState, HudState state)
        {
            if(Main.Config.TextShow.ValueEnum == TextShowMode.HudHints)
            {
                LastDefId = default(MyDefinitionId);
            }

            ReCheckSide();
        }

        private void GUIMonitor_OptionsMenuClosed()
        {
            ReCheckSide();

            if(Math.Abs(prevAspectRatio - Main.GameConfig.AspectRatio) > 0.0001)
            {
                prevAspectRatio = Main.GameConfig.AspectRatio;
                CachedBuildInfoTextAPI.Clear();
            }
        }

        private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            LastDefId = default(MyDefinitionId);
        }

        private void ReCheckSide()
        {
            bool shouldUseLeftSide = (Main.GameConfig.HudState == HudState.HINTS);

            if(useLeftSide != shouldUseLeftSide)
            {
                useLeftSide = shouldUseLeftSide;
                HideText();
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            if(textShown && textObject != null && MyAPIGateway.Gui.IsCursorVisible)
                HideText();

            if(!textShown && Main.Config.TextShow.ValueEnum == TextShowMode.ShowOnPress && Main.Config.TextShowBind.Value.IsPressed(InputLib.GetCurrentInputContext()))
                LastDefId = default(MyDefinitionId);

            Update(tick);

            if(tick % CACHE_PURGE_TICKS == 0)
            {
                PurgeCache();
            }

            if(forceDrawTicks > 0)
            {
                forceDrawTicks--;
            }
        }

        public override void UpdateDraw()
        {
            if(textShown && textObject != null)
            {
                if(textObject.Background != null)
                    textObject.Background.Draw();

                if(textObject.Shadow != null)
                    textObject.Shadow.Draw();

                textObject.Text.Draw();
            }
            else
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
            }
        }

        private void Update(int tick)
        {
            MyDefinitionId prevToolDefId = Main.EquipmentMonitor.ToolDefId;

            if(Main.EquipmentMonitor.AimedBlock != null && tick % 10 == 0) // make the aimed info refresh every 10 ticks
                aimInfoNeedsUpdate = true;

            if(Main.EquipmentMonitor.ToolDefId != prevToolDefId)
                LastDefId = default(MyDefinitionId);

            // turn off frozen block preview if camera is too far away from it
            if(LastGizmoPosition.HasValue && MyAPIGateway.CubeBuilder.FreezeGizmo && Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.WorldMatrix.Translation, LastGizmoPosition.Value) > FREEZE_MAX_DISTANCE_SQ)
            {
                Main.QuickMenu.SetFreezePlacement(false);
            }

            MyCubeBlockDefinition def = Main.EquipmentMonitor.BlockDef;

            if(def != null || Main.QuickMenu.Shown)
            {
                if(UpdateWithDef(def))
                    return;
            }

            if(textShown)
            {
                Main.QuickMenu.CloseMenu();

                if(MyAPIGateway.CubeBuilder.FreezeGizmo)
                {
                    Main.QuickMenu.SetFreezePlacement(false);
                }

                HideText();
            }
        }

        private bool UpdateWithDef(MyCubeBlockDefinition def)
        {
            LocalTooltips.Clear();

            bool processTooltips = false;

            // TODO: separate quick menu!
            if(Main.QuickMenu.Shown)
            {
                if(Main.QuickMenu.NeedsUpdate)
                {
                    LastDefId = DEFID_MENU;
                    Main.QuickMenu.NeedsUpdate = false;
                    textShown = false;

                    GenerateMenuText();
                    PostProcessText(DEFID_MENU, false);
                }
            }
            else
            {
                bool changedBlock = (def.Id != LastDefId);
                bool hasBlockDef = (Main.EquipmentMonitor.BlockDef != null);
                bool hasAimedBlock = (Main.EquipmentMonitor.AimedBlock != null);

                if(hasBlockDef && Main.Config.PlaceInfo.Value == 0)
                    return false;

                if(hasAimedBlock && Main.Config.AimInfo.Value == 0)
                    return false;

                if(changedBlock || (aimInfoNeedsUpdate && hasAimedBlock))
                {
                    LastDefId = def.Id;

                    if(Main.Config.TextShow.ShouldShowText)
                    {
                        Main.ScreenTooltips.ClearTooltips(nameof(TextGeneration));
                        processTooltips = true;

                        if(hasAimedBlock)
                        {
                            cache = null;
                            try
                            {
                                aimInfoNeedsUpdate = false;
                                GenerateAimBlockText(def);
                                PostProcessText(def.Id, false);
                            }
                            catch(Exception e)
                            {
                                Log.Error($"Error on aimed defId={def?.Id.ToString()} - {e.Message}\n{e.StackTrace}");
                            }
                        }
                        else if(hasBlockDef)
                        {
                            if(Main.TextAPI.IsEnabled ? CachedBuildInfoTextAPI.TryGetValue(def.Id, out cache) : CachedBuildInfoNotification.TryGetValue(def.Id, out cache))
                            {
                                textShown = false; // make the textAPI update
                            }
                            else
                            {
                                try
                                {
                                    GenerateBlockText(def);
                                    PostProcessText(def.Id, true);
                                }
                                catch(Exception e)
                                {
                                    Log.Error($"Error on equipped defId={def?.Id.ToString()} - {e.Message}\n{e.StackTrace}");
                                }
                            }
                        }
                        else
                        {
                            cache = null;
                            try
                            {
                                aimInfoNeedsUpdate = false;
                                GenerateAimBlockText(def);
                                PostProcessText(def.Id, false);
                            }
                            catch(Exception e)
                            {
                                Log.Error($"Error on aimed defId={def?.Id.ToString()} - {e.Message}\n{e.StackTrace}");
                            }
                        }
                    }
                }
            }

            UpdateVisualText();

            if(processTooltips)
                FinalizeTooltips();

            return true;
        }

        #region Tooltips
        public struct LocalTooltip
        {
            public int Line;
            public StringBuilder Text;
            public Action Action;
        }

        List<LocalTooltip> LocalTooltips = new List<LocalTooltip>();
        bool LineHadTooltip = false;

        /// <summary>
        /// If line not defined, automatically uses current line and automatically calls <see cref="Utilities.StringBuilderExtensions.MarkTooltip(StringBuilder)"/> when it ends.
        /// </summary>
        StringBuilder CreateTooltip(Action action = null, int line = -1)
        {
            if(!Main.TextAPI.IsEnabled)
                return null;

            if(line < 0)
            {
                LineHadTooltip = true;
                line = this.line;
            }

            StringBuilder sb = new StringBuilder(256);

            LocalTooltips.Add(new LocalTooltip()
            {
                Text = sb,
                Line = line,
                Action = action,
            });

            return sb;
        }

        void SimpleTooltip(string text)
        {
            CreateTooltip()?.Append(text);
        }

        void FinalizeTooltips()
        {
            if(!Main.TextAPI.IsEnabled)
                return;

            List<LocalTooltip> localTooltips;
            CacheTextAPI cacheTextAPI = cache as CacheTextAPI;
            if(cacheTextAPI != null)
                localTooltips = cacheTextAPI.Tooltips; // this can also be null and that is a valid state
            else
                localTooltips = LocalTooltips;

            if(localTooltips != null && localTooltips.Count > 0)
            {
                Vector2D textSize = cacheTextAPI?.TextSize ?? textObject?.Text?.GetTextLength() ?? Vector2D.Zero;

                float lineHeight = (float)(Math.Abs(textSize.Y) / lines);

                Vector2 textMin = (Vector2)(textObject.Text.Origin + textObject.Text.Offset);
                Vector2 addMax = new Vector2((float)textSize.X, -lineHeight);

                //DebugLog.PrintHUD(this, $"FinalizeTooltips() :: lineH={lineHeight:0.#####}; lines={lines}; textMin={textMin}; addMax={addMax}", log: true); // DEBUG log

                foreach(LocalTooltip lt in localTooltips)
                {
                    Vector2 min = textMin + new Vector2(0, -lineHeight * lt.Line);
                    Vector2 max = min + addMax;
                    BoundingBox2 bb = new BoundingBox2(Vector2.Min(min, max), Vector2.Max(min, max));

                    //DebugLog.PrintHUD(this, $"FinalizeTooltips() :: tooltip added: bb={bb.Min} to {bb.Max}; line={lt.Line}; text={lt.Text.ToString()}", log: true); // DEBUG log

                    Main.ScreenTooltips.AddTooltip(nameof(TextGeneration), bb, lt.Text.TrimEndWhitespace().ToString(), lt.Action);
                }
            }
        }
        #endregion

        #region Text handling
        public void PostProcessText(MyDefinitionId id, bool useCache)
        {
            if(Main.TextAPI.IsEnabled)
            {
                textAPIlines.TrimEndWhitespace();

                Vector2D textSize = UpdateTextAPIvisuals(textAPIlines);

                if(useCache)
                {
                    cache = new CacheTextAPI(textAPIlines, textSize, LocalTooltips);

                    CachedBuildInfoTextAPI[id] = cache;
                }
            }
            else
            {
                long now = DateTime.UtcNow.Ticks;
                lastScroll = now + TimeSpan.TicksPerSecond;
                atLine = SCROLL_FROM_LINE;

                for(int i = line; i >= 0; --i)
                {
                    HudLine l = notificationLines[i];

                    int textWidthPx = largestLineWidth - l.lineWidthPx;

                    int fillChars = (int)Math.Floor((float)textWidthPx / (float)SPACE_SIZE);

                    if(fillChars > 0)
                    {
                        l.str.Append(' ', fillChars);
                    }
                }

                if(useCache)
                {
                    cache = new CacheNotifications(notificationLines);

                    CachedBuildInfoNotification[id] = cache;
                }
            }
        }

        private Vector2D UpdateTextAPIvisuals(StringBuilder textSB, Vector2D textSize = default(Vector2D))
        {
            if(textObject == null)
            {
                textObject = new TextAPI.TextPackage(TEXTAPI_TEXT_LENGTH, backgroundTexture: BG_MATERIAL);
                textObject.HideWithHUD = !Main.Config.TextAlwaysVisible.Value;
                textObject.Scale = Main.Config.TextAPIScale.Value;
            }

            //bgObject.Visible = true;
            //textObject.Visible = true;

            #region Update text and count lines
            StringBuilder msg = textObject.Text.Message;
            msg.Clear().EnsureCapacity(msg.Length + textSB.Length);
            lines = 1;

            for(int i = 0; i < textSB.Length; i++)
            {
                char c = textSB[i];

                msg.Append(c);

                if(c == '\n')
                    lines++;
            }

            textObject.Text.Flush();
            #endregion Update text and count lines

            Vector2D textPos = Vector2D.Zero;
            Vector2D textOffset = Vector2D.Zero;

            // calculate text size if it wasn't inputted
            if(Math.Abs(textSize.X) <= 0.0001 && Math.Abs(textSize.Y) <= 0.0001)
                textSize = textObject.Text.GetTextLength();

            if(Main.QuickMenu.Shown) // in the menu
            {
                textOffset = new Vector2D(-textSize.X, textSize.Y / -2);
            }
#if false // disabled blockinfo-attached GUI
            else if(selectedBlock != null) // welder/grinder info attached to the game's block info
            {
                IMyCamera cam = MyAPIGateway.Session.Camera;
                MatrixD camMatrix = cam.WorldMatrix;

                var hud = GetGameHudBlockInfoPos();
                hud.Y -= (BLOCKINFO_ITEM_HEIGHT * selectedDef.Components.Length) + BLOCKINFO_Y_OFFSET; // make the position top-right

                Vector3D worldPos = HudToWorld(hud);
                Vector2D size = GetGameHudBlockInfoSize((float)Math.Abs(textSize.Y) / 0.03f);
                Vector2D offset = new Vector2D(BLOCKINFO_TEXT_PADDING, BLOCKINFO_TEXT_PADDING) * ScaleFOV;

                worldPos += camMatrix.Left * (size.X + (size.X - offset.X)) + camMatrix.Up * (size.Y + (size.Y - offset.Y));

                // using textAPI's math to convert from world to its local coords
                double localScale = 0.1 * ScaleFOV;
                Vector3D local = Vector3D.Transform(worldPos, cam.ViewMatrix);
                local.X = (local.X / (localScale * aspectRatio)) * 2;
                local.Y = (local.Y / localScale) * 2;

                textPos.X = local.X;
                textPos.Y = local.Y;

                // not using textAPI's background for this as drawing my own manually is easier for the 3-part billboard that I need
                bgObject.Visible = false;
            }
#endif
            else if(Main.Config.TextAPICustomStyling.Value) // custom alignment and position
            {
                textPos = Main.Config.TextAPIScreenPosition.Value;

                if(Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Right))
                    textOffset.X = -textSize.X;

                if(Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom))
                    textOffset.Y = -textSize.Y;
            }
            else if(!useLeftSide) // right side autocomputed
            {
                textPos = (Main.GameConfig.AspectRatio > 5 ? TEXT_HUDPOS_RIGHT_WIDE : TEXT_HUDPOS_RIGHT);
                textOffset = new Vector2D(-textSize.X, -textSize.Y); // bottom-right pivot
            }
            else // left side autocomputed
            {
                textPos = (Main.GameConfig.AspectRatio > 5 ? TEXT_HUDPOS_WIDE : TEXT_HUDPOS);
                textOffset = new Vector2D(0, 0); // top-left pivot
            }

            textObject.Text.Origin = textPos;
            textObject.Text.Offset = textOffset;

#if false // disabled blockinfo-attached GUI
            if(showMenu || selectedBlock == null)
#endif
            {
                Color color = BG_COLOR;
                if(Main.QuickMenu.Shown)
                {
                    color *= MENU_BG_OPACITY;
                }
                else if(Main.Config.TextAPIBackgroundOpacity.Value >= 0)
                {
                    color *= Main.Config.TextAPIBackgroundOpacity.Value;
                }
                else
                {
                    Utils.FadeColorHUD(ref color, Main.GameConfig.HudBackgroundOpacity);
                }

                float edge = BG_EDGE * Main.Config.TextAPIScale.Value;

                textObject.Background.BillBoardColor = color;
                textObject.Background.Origin = textPos;
                textObject.Background.Width = (float)Math.Abs(textSize.X) + edge;
                textObject.Background.Height = (float)Math.Abs(textSize.Y) + edge;
                textObject.Background.Offset = textOffset + (textSize / 2);
            }

            textShown = true;
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
            return textSize;
        }

        public void UpdateVisualText()
        {
            IMySlimBlock aimedBlock = Main.EquipmentMonitor.AimedBlock;

            if(Main.TextAPI.IsEnabled)
            {
                if(MyAPIGateway.Gui.IsCursorVisible || (!Main.Config.TextShow.ShouldShowText && !Main.QuickMenu.Shown))
                {
                    HideText();
                    return;
                }

                // force reset, usually needed to fix notification to textAPI transition when heartbeat returns true
                if(textObject == null || (cache == null && !(Main.QuickMenu.Shown || aimedBlock != null)))
                {
                    LastDefId = default(MyDefinitionId);
                    return;
                }

                // show last generated block info message only for cubebuilder
                if(!textShown && textObject != null)
                {
                    if(Main.QuickMenu.Shown || aimedBlock != null)
                    {
                        UpdateTextAPIvisuals(textAPIlines);
                    }
                    else if(cache != null)
                    {
                        CacheTextAPI cacheTextAPI = (CacheTextAPI)cache;
                        cacheTextAPI.ResetExpiry();
                        UpdateTextAPIvisuals(cacheTextAPI.Text, cacheTextAPI.TextSize);
                    }
                }
            }
            else
            {
                if(Main.IsPaused)
                    return; // HACK: avoid notification glitching out if showing them continuously when game is paused

                if(MyAPIGateway.Gui.IsCursorVisible || (!Main.Config.TextShow.ShouldShowText && !Main.QuickMenu.Shown))
                    return;

                List<IMyHudNotification> hudLines = null;

                if(Main.QuickMenu.Shown || aimedBlock != null)
                {
                    hudLines = hudNotifLines;

                    for(int i = 0; i < notificationLines.Count; ++i)
                    {
                        HudLine line = notificationLines[i];

                        if(line.str.Length > 0)
                        {
                            if(hudLines.Count <= i)
                            {
                                hudLines.Add(MyAPIGateway.Utilities.CreateNotification(line.str.ToString(), 16, line.font));
                            }
                            else
                            {
                                hudLines[i].Text = line.str.ToString();
                                hudLines[i].Font = line.font;
                            }
                        }
                        else if(hudLines.Count > i)
                        {
                            hudLines[i].Text = "";
                        }
                    }
                }
                else
                {
                    if(cache == null)
                    {
                        LastDefId = default(MyDefinitionId);
                        return;
                    }

                    if(!textShown)
                    {
                        textShown = true;
                        SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
                        cache.ResetExpiry();
                    }

                    hudLines = ((CacheNotifications)cache).Lines;
                }

                int lines = 0;

                foreach(IMyHudNotification hud in hudLines)
                {
                    if(hud.Text.Length > 0)
                        lines++;

                    hud.Hide();
                }

                if(Main.QuickMenu.Shown)
                {
                    // HACK this must match the data from the menu
                    const int itemsStartAt = 1;
                    const int itemsEndAt = QuickMenu.MENU_TOTAL_ITEMS;

                    int selected = itemsStartAt + Main.QuickMenu.SelectedItem;

                    for(int l = 0; l < lines; ++l)
                    {
                        if(l < itemsStartAt
                        || l > itemsEndAt
                        || (selected == itemsEndAt && l == (selected - 2))
                        || l == (selected - 1)
                        || l == selected
                        || l == (selected + 1)
                        || (selected == itemsStartAt && l == (selected + 2)))
                        {
                            IMyHudNotification hud = hudLines[l];
                            hud.Hide(); // required since SE v1.194
                            hud.ResetAliveTime();
                            hud.Show();
                        }
                    }
                }
                else
                {
                    if(lines > MAX_LINES)
                    {
                        int l;

                        for(l = 0; l < lines; ++l)
                        {
                            IMyHudNotification hud = hudLines[l];

                            if(l < SCROLL_FROM_LINE)
                            {
                                hud.Hide(); // required since SE v1.194
                                hud.ResetAliveTime();
                                hud.Show();
                            }
                        }

                        int d = SCROLL_FROM_LINE;
                        l = atLine;

                        while(d < MAX_LINES)
                        {
                            IMyHudNotification hud = hudLines[l];

                            if(hud.Text.Length == 0)
                                break;

                            hud.Hide(); // required since SE v1.194
                            hud.ResetAliveTime();
                            hud.Show();

                            if(++l >= lines)
                                l = SCROLL_FROM_LINE;

                            d++;
                        }

                        long now = DateTime.UtcNow.Ticks;

                        if(lastScroll < now)
                        {
                            if(++atLine >= lines)
                                atLine = SCROLL_FROM_LINE;

                            lastScroll = now + (long)(TimeSpan.TicksPerSecond * 1.5f);
                        }
                    }
                    else
                    {
                        for(int l = 0; l < lines; l++)
                        {
                            IMyHudNotification hud = hudLines[l];
                            hud.Hide(); // required since SE v1.194
                            hud.ResetAliveTime();
                            hud.Show();
                        }
                    }
                }
            }
        }

        public void HideText()
        {
            if(textShown)
            {
                if(forceDrawTicks <= 0)
                {
                    textShown = false;
                    LastDefId = default(MyDefinitionId);

                    Main.ScreenTooltips.ClearTooltips(nameof(TextGeneration));

                    // text API hide
                    //if(textObject != null)
                    //{
                    //    textObject.Visible = false;
                    //    bgObject.Visible = false;
                    //}
                }

                // HUD notifications don't need hiding, they expire in one frame.

                //Main.Overlays.HideLabels();
            }
        }

        private void ResetLines()
        {
            if(Main.TextAPI.IsEnabled)
            {
                textAPIlines.Clear();
            }
            else
            {
                foreach(HudLine l in notificationLines)
                {
                    l.str.Clear();
                }
            }

            line = -1;
            largestLineWidth = 0;
            addLineCalled = false;
        }

        private StringBuilder AddLine(string font = FontsHandler.WhiteSh)
        {
            EndAddedLines();
            addLineCalled = true;

            ++line;

            if(Main.TextAPI.IsEnabled)
            {
                return textAPIlines;
            }
            else
            {
                if(line >= notificationLines.Count)
                    notificationLines.Add(new HudLine());

                HudLine nl = notificationLines[line];
                nl.font = font;

                return nl.str.Append("• ");
            }
        }

        public void EndAddedLines()
        {
            if(!addLineCalled)
                return;

            addLineCalled = false;

            if(Main.TextAPI.IsEnabled)
            {
                // for testing tooltip lines lining up
                //{
                //    string lastLine = textAPIlines.ToString();
                //    int lastLineIdx = lastLine.LastIndexOf('\n');
                //    if(lastLineIdx != -1)
                //    {
                //        lastLineIdx += 1;
                //        lastLine = lastLine.Substring(lastLineIdx, lastLine.Length - lastLineIdx);
                //    }
                //
                //    CreateTooltip().Append($"line #{line} == {lastLine}");
                //}

                if(LineHadTooltip)
                {
                    textAPIlines.MarkTooltip();
                }

                textAPIlines.NewCleanLine();
            }
            else
            {
                HudLine hudLine = notificationLines[line];
                hudLine.lineWidthPx = GetStringSizeNotif(hudLine.str);

                largestLineWidth = Math.Max(largestLineWidth, hudLine.lineWidthPx);
            }

            LineHadTooltip = false;
        }

        private StringBuilder GetLine()
        {
            return (Main.TextAPI.IsEnabled ? textAPIlines : notificationLines[line].str);
        }

        private void AddOverlaysHint(MyCubeBlockDefinition def)
        {
            // TODO: remove last condition when adding overlay to WC
            if(Main.SpecializedOverlays.Get(def.Id.TypeId) != null && !Main.CoreSystemsAPIHandler.Weapons.ContainsKey(def.Id))
            {
                AddLine(FontsHandler.GraySh).Color(COLOR_UNIMPORTANT).Append("(Specialized overlay available. ");
                Main.Config.CycleOverlaysBind.Value.GetBinds(GetLine());
                GetLine().Append(" to cycle)");
            }
        }

        int GetStringSizeNotif(StringBuilder builder)
        {
            int endLength = builder.Length;
            int size = 0;

            for(int i = 0; i < endLength; ++i)
            {
                size += Main.FontsHandler.CharSize.GetValueOrDefault(builder[i], FontsHandler.DefaultCharSize);
            }

            return size;
        }
        #endregion Text handling

        #region Menu generation
        private StringBuilder AddMenuItemLine(int item, bool enabled = true)
        {
            AddLine(font: (Main.QuickMenu.SelectedItem == item ? FontsHandler.GreenSh : (enabled ? FontsHandler.WhiteSh : FontsHandler.RedSh)));

            if(Main.QuickMenu.SelectedItem == item)
                GetLine().Color(COLOR_GOOD).Append("  > ");
            else
                GetLine().Color(enabled ? COLOR_NORMAL : COLOR_UNIMPORTANT).Append(' ', 6);

            return GetLine();
        }

        public void GenerateMenuText()
        {
            ResetLines();

            AddLine(FontsHandler.SkyBlueSh).Color(COLOR_BLOCKTITLE).Append(BuildInfoMod.ModName).Append(" mod");

            int i = 0;

            // HACK this must match the data from the HandleInput() which controls the actual actions of these

            AddMenuItemLine(i++).Append("Close menu");

            GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
            if(Main.Config.MenuBind.Value.IsAssigned())
            {
                Main.Config.MenuBind.Value.GetBinds(GetLine());
            }
            else
            {
                GetLine().Append(Main.ChatCommandHandler.CommandQuickMenu.MainAlias);
            }
            GetLine().Append(")");

            if(Main.TextAPI.IsEnabled)
            {
                AddLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Actions:");
            }

            AddMenuItemLine(i++).Append("Add aimed block to toolbar");
            GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
            if(Main.Config.BlockPickerBind.Value.IsAssigned())
            {
                Main.Config.BlockPickerBind.Value.GetBinds(GetLine());
            }
            else
            {
                GetLine().Append(Main.ChatCommandHandler.CommandGetBlock.MainAlias);
            }
            GetLine().Append(")");

            AddMenuItemLine(i++).Append("Open block's mod workshop").Color(COLOR_UNIMPORTANT).Append("   (").Append(Main.ChatCommandHandler.CommandModLink.MainAlias).Append(')');

            AddMenuItemLine(i++).Append("Help topics").Color(COLOR_UNIMPORTANT).Append("   (").Append(Main.ChatCommandHandler.CommandHelp.MainAlias).Append(')');

            AddMenuItemLine(i++).Append("Open this mod's workshop").Color(COLOR_UNIMPORTANT).Append("   (").Append(Main.ChatCommandHandler.CommandWorkshop.MainAlias).Append(')');

            if(Main.TextAPI.IsEnabled)
            {
                AddLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Settings:");
            }

            AddMenuItemLine(i++).Append("Text info: ").Append(Main.Config.TextShow.ValueName);

            AddMenuItemLine(i++).Append("Draw overlays: ").Append(Main.Overlays.OverlayModeName);
            if(Main.Config.CycleOverlaysBind.Value.IsAssigned())
            {
                GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
                Main.Config.CycleOverlaysBind.Value.GetBinds(GetLine());
                GetLine().Append(")").ResetFormatting();
            }

            AddMenuItemLine(i++).Append("Placement transparency: ").Append(MyCubeBuilder.Static.UseTransparency ? "ON" : "OFF");
            if(Main.Config.ToggleTransparencyBind.Value.IsAssigned())
            {
                GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
                Main.Config.ToggleTransparencyBind.Value.GetBinds(GetLine());
                GetLine().Append(")").ResetFormatting();
            }

            AddMenuItemLine(i++).Append("Freeze in position: ").Append(MyAPIGateway.CubeBuilder.FreezeGizmo ? "ON" : "OFF");
            if(Main.Config.FreezePlacementBind.Value.IsAssigned())
            {
                GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
                Main.Config.FreezePlacementBind.Value.GetBinds(GetLine());
                GetLine().Append(")").ResetFormatting();
            }

            AddMenuItemLine(i++, Main.TextAPI.WasDetected).Append("Use TextAPI: ");
            if(Main.TextAPI.WasDetected)
                GetLine().Append(Main.TextAPI.Use ? "ON" : "OFF");
            else
                GetLine().Append("OFF (Mod not detected)");

            AddMenuItemLine(i++).Append("Reload settings file").Color(COLOR_UNIMPORTANT).Append("   (").Append(Main.ChatCommandHandler.CommandReloadConfig.MainAlias).Append(')');

            if(Main.TextAPI.IsEnabled)
                AddLine();

            AddLine(FontsHandler.SkyBlueSh).Color(COLOR_INFO).Append("Navigation: Up/down = ").Append(MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE.GetAssignedInputName()).Append("/").Append(MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE.GetAssignedInputName()).Append(", change = ").Append(MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE.GetAssignedInputName()).ResetFormatting().Append(' ', 10);

            EndAddedLines();
        }
        #endregion Menu generation

        #region Aimed block info generation
        public void GenerateAimBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            IMyPlayer localPlayer = MyAPIGateway.Session?.Player;

            if(Main.Config.AimInfo.Value == 0 || localPlayer == null)
                return;

            IMySlimBlock aimedBlock = Main.EquipmentMonitor.AimedBlock;
            if(aimedBlock == null)
            {
                Log.Error($"Aimed block not found in GenerateAimBlockText() :: defId={def?.Id.ToString()}", Log.PRINT_MESSAGE);
                return;
            }

            IMyProjector projectedBy = Main.EquipmentMonitor.AimedProjectedBy;
            bool isProjected = (projectedBy != null);
            float integrityRatio = (isProjected ? 0 : aimedBlock.Integrity / aimedBlock.MaxIntegrity);
            IMyCubeGrid grid = (isProjected ? projectedBy.CubeGrid : aimedBlock.CubeGrid);

            IMyTerminalBlock terminalBlock = aimedBlock.FatBlock as IMyTerminalBlock;
            bool hasComputer = (terminalBlock != null && def.ContainsComputer());

            #region Block name
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.TerminalName))
            {
                if(terminalBlock != null)
                {
                    AddLine().Append('"').Color(COLOR_BLOCKTITLE).AppendMaxLength(terminalBlock.CustomName, BLOCK_NAME_MAX_LENGTH).ResetFormatting().Append('"');
                }
                else if(isProjected) // show block def name because game might not.
                {
                    AddLine().Color(COLOR_BLOCKTITLE).AppendMaxLength(def.DisplayNameText, BLOCK_NAME_MAX_LENGTH).ResetFormatting();
                }
            }
            #endregion Block name

            #region Internal info
            if(Main.Config.InternalInfo.Value)
            {
                int obPrefixLen = "MyObjectBuilder_".Length;
                string typeIdString = def.Id.TypeId.ToString();
                AddLine().Color(COLOR_INTERNAL).Label("Id").Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(def.Id.SubtypeName);
                AddLine().Color(COLOR_INTERNAL).Label("BlockPairName").Append(def.BlockPairName);
                AddLine().Color(COLOR_INTERNAL).Label("ModelIntersection").Append(def.UseModelIntersection);
            }
            #endregion Internal info

            #region Mass, grid mass
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.Mass))
            {
                float mass = (def.HasPhysics ? def.Mass : 0); // HACK: game doesn't use mass from blocks with HasPhysics=false
                Color massColor = Color.GreenYellow;

                if(isProjected)
                {
                    AddLine().Color(massColor).MassFormat(mass);
                }
                else
                {
                    // include inventory mass
                    IMyCubeBlock aimedFatblock = aimedBlock.FatBlock;
                    if(aimedFatblock != null && aimedFatblock.InventoryCount > 0)
                    {
                        for(int i = (aimedFatblock.InventoryCount - 1); i >= 0; i--)
                        {
                            IMyInventory inv = aimedFatblock.GetInventory(i);
                            if(inv == null)
                                continue;

                            float invMass = (float)inv.CurrentMass;
                            if(invMass > 0)
                            {
                                mass += invMass;
                                massColor = new Color(255, 200, 0);
                            }
                        }
                    }

                    AddLine().Color(massColor).MassFormat(mass);

                    if(grid.Physics != null)
                    {
                        if(grid.EntityId != prevSelectedGrid || --gridMassComputeCooldown <= 0)
                        {
                            prevSelectedGrid = grid.EntityId;
                            gridMassComputeCooldown = (60 * 3) / 10; // divide by 10 because this method executes very 10 ticks
                            gridMassCache = BuildInfoMod.Instance.GridMassCompute.GetGridMass(grid);
                        }

                        GetLine().ResetFormatting().Separator().Append("Grid mass: ").MassFormat(gridMassCache);
                    }
                }
            }
            #endregion Mass, grid mass

            #region Projector info and status
            if(isProjected && Main.Config.AimInfo.IsSet(AimInfoFlags.Projected))
            {
                // TODO: custom extracted method to be able to compare blocks and not select the projection of the same block that's already placed

                AddLine().Label("Projected by").Append("\"").Color(COLOR_BLOCKTITLE).AppendMaxLength(projectedBy.CustomName, BLOCK_NAME_MAX_LENGTH).ResetFormatting().Append('"');

                AddLine().Label("Status");

                switch(Main.EquipmentMonitor.AimedProjectedCanBuild)
                {
                    case BuildCheckResult.OK:
                        GetLine().Color(COLOR_GOOD).Append("Ready to build");
                        break;
                    case BuildCheckResult.AlreadyBuilt:
                        GetLine().Color(COLOR_WARNING).Append("Already built!");
                        break;
                    case BuildCheckResult.IntersectedWithGrid:
                        GetLine().Color(COLOR_BAD).Append("Other block in the way");
                        break;
                    case BuildCheckResult.IntersectedWithSomethingElse:
                        if(!Utils.CheckSafezoneAction(aimedBlock, Utils.SZABuildingProjections))
                            GetLine().Color(COLOR_BAD).Append("Can't build projections in this SafeZone");
                        else if(!Utils.CheckSafezoneAction(aimedBlock, Utils.SZAWelding))
                            GetLine().Color(COLOR_BAD).Append("Can't weld in this SafeZone");
                        else
                            GetLine().Color(COLOR_WARNING).Append("Something in the way");
                        break;
                    case BuildCheckResult.NotConnected:
                        GetLine().Color(COLOR_WARNING).Append("Nothing to attach to");
                        break;
                    case BuildCheckResult.NotWeldable:
                        GetLine().Color(COLOR_BAD).Append("Projector doesn't allow building");
                        break;
                    //case BuildCheckResult.NotFound: // not used by CanBuild()
                    default:
                        GetLine().Color(COLOR_BAD).Append("(Unknown)");
                        break;
                }
            }
            #endregion Projector info and status

            #region Different block projected under this one
            MyCubeGrid nearbyProjectedGrid = Main.EquipmentMonitor.NearbyProjector?.ProjectedGrid as MyCubeGrid;
            if(!isProjected && nearbyProjectedGrid != null && Main.Config.AimInfo.IsSet(AimInfoFlags.Projected))
            {
                IMyProjector projector = nearbyProjectedGrid.Projector as IMyProjector;
                MyProjectorDefinition projectorDef = projector?.SlimBlock?.BlockDefinition as MyProjectorDefinition;
                if(projectorDef != null && Hardcoded.Projector_AllowWelding(projectorDef))
                {
                    ProjectedUnder.Clear();

                    // need the real block's positions in the projected grid's space
                    Vector3I min = nearbyProjectedGrid.WorldToGridInteger(aimedBlock.CubeGrid.GridIntegerToWorld(aimedBlock.Min));
                    Vector3I max = nearbyProjectedGrid.WorldToGridInteger(aimedBlock.CubeGrid.GridIntegerToWorld(aimedBlock.Max));
                    Vector3I_RangeIterator iterator = new Vector3I_RangeIterator(ref min, ref max);
                    while(iterator.IsValid())
                    {
                        IMySlimBlock projectedBlock = nearbyProjectedGrid.GetCubeBlock(iterator.Current);
                        if(projectedBlock != null && projectedBlock.BlockDefinition.Id != aimedBlock.BlockDefinition.Id)
                        {
                            ProjectedUnder.Add(projectedBlock);
                        }

                        iterator.MoveNext();
                    }

                    int projectedUnderCount = ProjectedUnder.Count;
                    if(projectedUnderCount == 1)
                    {
                        AddLine().Color(COLOR_BAD).Label("Projected under").AppendMaxLength(ProjectedUnder.FirstElement().BlockDefinition.DisplayNameText, 24);
                    }
                    else if(projectedUnderCount > 1)
                    {
                        AddLine().Color(COLOR_BAD).Label("Projected under").Append(projectedUnderCount).Append(" blocks");
                    }

                    // TODO: add this to under-crosshair messages?

                    ProjectedUnder.Clear();
                }
            }
            #endregion Different block projected under this one

            #region Integrity
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.Integrity))
            {
                if(isProjected)
                {
                    float originalIntegrity = (aimedBlock.Integrity / aimedBlock.MaxIntegrity);

                    AddLine().ResetFormatting().Append("Integrity in blueprint: ").Color(originalIntegrity < 1 ? COLOR_WARNING : COLOR_GOOD)
                        .ProportionToPercent(originalIntegrity).ResetFormatting();
                }
                else
                {
                    AddLine().ResetFormatting().Append("Integrity: ").Color(integrityRatio < def.CriticalIntegrityRatio ? COLOR_BAD : (integrityRatio < 1 ? COLOR_WARNING : COLOR_GOOD))
                        .IntegrityFormat(aimedBlock.Integrity).ResetFormatting()
                        .Append(" / ").IntegrityFormat(aimedBlock.MaxIntegrity);

                    if(def.BlockTopology == MyBlockTopology.Cube && aimedBlock.HasDeformation)
                        GetLine().Color(COLOR_WARNING).Append(" (deformed)");
                }
            }
            #endregion Integrity

            #region Component volume
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.ComponentsVolume))
            {
                float totalVolumeM3 = 0f;

                foreach(MyCubeBlockDefinition.Component comp in def.Components)
                {
                    totalVolumeM3 += comp.Definition.Volume * comp.Count;
                }

                Dictionary<string, int> names = Main.Caches.NamedSums;
                names.Clear();
                aimedBlock.GetMissingComponents(names);

                float missingVolumeM3 = 0f;

                foreach(KeyValuePair<string, int> kv in names)
                {
                    MyComponentDefinition compDef;
                    if(MyDefinitionManager.Static.TryGetComponentDefinition(new MyDefinitionId(typeof(MyObjectBuilder_Component), kv.Key), out compDef))
                    {
                        missingVolumeM3 += compDef.Volume * kv.Value;
                    }
                }

                float installedVolumeM3 = totalVolumeM3 - missingVolumeM3;

                float charInvVolM3 = float.MaxValue;
                IMyInventory inv = MyAPIGateway.Session?.Player?.Character?.GetInventory();
                if(inv != null)
                    charInvVolM3 = (float)inv.MaxVolume - CharInvVolM3Offset;

                AddLine().Append("Components: ").VolumeFormat(installedVolumeM3 * 1000).Append(" / ").Color(totalVolumeM3 > charInvVolM3 ? COLOR_WARNING : COLOR_NORMAL).VolumeFormat(totalVolumeM3 * 1000).ResetFormatting();
            }
            #endregion Component volume

            #region Optional: intake damage multiplier
            if(!isProjected && Main.Config.AimInfo.IsSet(AimInfoFlags.DamageMultiplier))
            {
                // MySlimBlock.BlockGeneralDamageModifier is inaccessible
                float dmgMul = aimedBlock.DamageRatio * def.GeneralDamageMultiplier;
                float gridDmgMul = ((MyCubeGrid)grid).GridGeneralDamageModifier;

                if(dmgMul != 1 || gridDmgMul != 1)
                {
                    StringBuilder sb = AddLine();
                    DamageMultiplierAsResistance(dmgMul);

                    if(gridDmgMul != 1)
                    {
                        sb.Separator();
                        DamageMultiplierAsResistance(gridDmgMul, label: "Grid");
                    }
                }

                // TODO: add here or not?
                //CoreSystemsDef.ArmorDefinition csArmorDef;
                //if(Main.CoreSystemsAPIHandler.Armor.TryGetValue(def.Id.SubtypeName, out csArmorDef))
                //{
                //    Format_CoreSystemsArmor(def, csArmorDef);
                //}

                // TODO: impact resistance? wheels in particular...
            }
            #endregion Optional: intake damage multiplier

            #region Optional: ownership
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.Ownership))
            {
                if(!isProjected && hasComputer)
                {
                    MyRelationsBetweenPlayerAndBlock relation = (aimedBlock.OwnerId > 0 ? localPlayer.GetRelationTo(aimedBlock.OwnerId) : MyRelationsBetweenPlayerAndBlock.NoOwnership);
                    MyOwnershipShareModeEnum shareMode = Utils.GetBlockShareMode(aimedBlock.FatBlock);

                    StringBuilder sb = AddLine();

                    sb.Label("Owner");

                    if(aimedBlock.OwnerId == 0)
                    {
                        sb.Color(COLOR_WARNING).Append("(Nobody)");
                    }
                    else
                    {
                        Color ownershipColor = COLOR_NORMAL;
                        if(relation == MyRelationsBetweenPlayerAndBlock.Enemies || relation == MyRelationsBetweenPlayerAndBlock.Neutral)
                            ownershipColor = COLOR_BAD;
                        else if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                            ownershipColor = COLOR_OWNER;
                        else if(relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                            ownershipColor = COLOR_GOOD;
                        else if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                            ownershipColor = COLOR_WARNING;

                        sb.Color(ownershipColor);

                        string factionTag = aimedBlock.FatBlock.GetOwnerFactionTag();

                        if(!string.IsNullOrEmpty(factionTag))
                            sb.Append(factionTag).Append('.');

                        // NOTE: MyVisualScriptLogicProvider.GetPlayersName() returns local player on id 0 and id 0 is also used for "nobody" in ownership.
                        sb.AppendMaxLength(MyVisualScriptLogicProvider.GetPlayersName(aimedBlock.FatBlock.OwnerId), PLAYER_NAME_MAX_LENGTH);
                    }

                    sb.ResetFormatting().Separator();

                    sb.Label("Access");

                    if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                    {
                        sb.Color(COLOR_GOOD).Append("All");
                    }
                    else if(shareMode == MyOwnershipShareModeEnum.All)
                    {
                        if(relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies)
                            sb.Color(COLOR_GOOD);
                        else
                            sb.Color(COLOR_WARNING);

                        sb.Append("All");
                    }
                    else if(shareMode == MyOwnershipShareModeEnum.Faction)
                    {
                        if(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                            GetLine().Color(COLOR_GOOD);
                        else
                            GetLine().Color(COLOR_BAD);

                        GetLine().Append("Faction");
                    }
                    else if(shareMode == MyOwnershipShareModeEnum.None)
                    {
                        if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                            GetLine().Color(COLOR_WARNING);
                        else
                            GetLine().Color(COLOR_BAD);

                        GetLine().Append("Owner");
                    }
                }
                else if(isProjected)
                {
                    MyRelationsBetweenPlayerAndBlock relation = (projectedBy.OwnerId > 0 ? localPlayer.GetRelationTo(projectedBy.OwnerId) : MyRelationsBetweenPlayerAndBlock.NoOwnership);

                    StringBuilder sb = AddLine();

                    if(projectedBy.OwnerId == 0)
                    {
                        sb.Color(COLOR_WARNING).Append("Projector not owned");
                    }
                    else
                    {
                        Color ownershipColor = COLOR_NORMAL;
                        if(relation == MyRelationsBetweenPlayerAndBlock.Enemies || relation == MyRelationsBetweenPlayerAndBlock.Neutral)
                            ownershipColor = COLOR_BAD;
                        else if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                            ownershipColor = COLOR_OWNER;
                        else if(relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                            ownershipColor = COLOR_GOOD;
                        else if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                            ownershipColor = COLOR_WARNING;

                        sb.Label("Projector owner").Color(ownershipColor);

                        // NOTE: MyVisualScriptLogicProvider.GetPlayersName() returns local player on id 0 and id 0 is also use for "nobody" in ownership.
                        string factionTag = projectedBy.GetOwnerFactionTag();

                        if(!string.IsNullOrEmpty(factionTag))
                            sb.Append(factionTag).Append('.');

                        sb.AppendMaxLength(MyVisualScriptLogicProvider.GetPlayersName(projectedBy.OwnerId), PLAYER_NAME_MAX_LENGTH);
                    }
                }
            }
            #endregion Optional: ownership

            #region connector trade mode alert
            //if(Main.Config.AimInfo.IsSet(AimInfoFlags.BlockSpecific))
            {
                IMyShipConnector connector = aimedBlock.FatBlock as IMyShipConnector;
                if(connector != null)
                {
                    if(connector.GetValue<bool>("Trading")) // HACK: replace with interface property if that ever gets added
                    {
                        if(!connector.HasLocalPlayerAccess())
                            AddLine(FontsHandler.GreenSh).Color(COLOR_GOOD).Append("Connector is in Trade-Mode.");
                        else
                            AddLine(FontsHandler.YellowSh).Color(COLOR_WARNING).Append("Connector is in Trade-Mode.");
                    }
                }
            }
            #endregion 

            #region Time to complete/grind
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.ToolUseTime))
            {
                bool isWelder = Main.EquipmentMonitor.IsAnyWelder;
                float toolPerSec;

                if(Main.EquipmentMonitor.HandTool != null)
                {
                    MyEngineerToolBaseDefinition toolDef = MyDefinitionManager.Static.TryGetHandItemForPhysicalItem(Main.EquipmentMonitor.HandTool.PhysicalItemDefinition.Id) as MyEngineerToolBaseDefinition;
                    float toolMul = toolDef?.SpeedMultiplier ?? 1;

                    if(isWelder)
                        toolPerSec = Hardcoded.HandWelder_GetWeldPerSec(toolMul);
                    else
                        toolPerSec = Hardcoded.HandGrinder_GetGrindPerSec(toolMul);
                }
                else // not hand tool, assuming ship tool
                {
                    const int AssumedTargets = 1; // getting how many targets ship welder/grinder affects is a whole other can of worms

                    if(isWelder)
                        toolPerSec = Hardcoded.ShipWelder_WeldPerSec(AssumedTargets);
                    else
                        toolPerSec = Hardcoded.ShipGrinder_GrindPerSec(AssumedTargets);
                }

                if(isWelder)
                {
                    float buildTime = def.MaxIntegrity / (def.IntegrityPointsPerSec * toolPerSec);
                    float currentTime = buildTime * (1 - integrityRatio);
                    if(currentTime > 0)
                    {
                        AddLine().Append("Completed: ").TimeFormat(currentTime).Color(COLOR_UNIMPORTANT).OptionalMultiplier(MyAPIGateway.Session.WelderSpeedMultiplier).ResetFormatting();

                        if(def.CriticalIntegrityRatio < 1 && integrityRatio < def.CriticalIntegrityRatio)
                        {
                            float funcTime = buildTime * def.CriticalIntegrityRatio * (1 - (integrityRatio / def.CriticalIntegrityRatio));

                            GetLine().Separator().Append("Functional: ").TimeFormat(funcTime);
                        }
                    }
                }
                else
                {
                    // accounts for hardcoded multipliers from MyDoor and MyAdvancedDoor
                    float grindRatio = (aimedBlock?.FatBlock != null ? aimedBlock.FatBlock.DisassembleRatio : def.DisassembleRatio);

                    bool hackable = false;
                    float hackMultiplier = 1f;
                    if(Main.EquipmentMonitor.HandTool != null && aimedBlock?.FatBlock != null) // HACK: HackSpeedMultiplier seems to be only used for hand-grinder
                    {
                        MyRelationsBetweenPlayerAndBlock relation = aimedBlock.FatBlock.GetPlayerRelationToOwner();
                        if(relation == MyRelationsBetweenPlayerAndBlock.Enemies || relation == MyRelationsBetweenPlayerAndBlock.Neutral)
                        {
                            hackMultiplier = MyAPIGateway.Session.HackSpeedMultiplier;
                            hackable = true;
                        }
                    }

                    float buildTime = def.MaxIntegrity / (def.IntegrityPointsPerSec * (toolPerSec / grindRatio));
                    float grindTime = buildTime;

                    float hackTime = 0;
                    if(hackMultiplier != 1)
                    {
                        float noOwnershipTime = (grindTime * def.OwnershipIntegrityRatio);
                        hackTime = (grindTime * ((1 - def.OwnershipIntegrityRatio) - (1 - integrityRatio))) / MyAPIGateway.Session.HackSpeedMultiplier;
                        grindTime = noOwnershipTime + hackTime;
                    }
                    else
                    {
                        grindTime *= integrityRatio;
                    }

                    if(grindTime > 0)
                    {
                        AddLine().Append("Dismantled: ").TimeFormat(grindTime).Color(COLOR_UNIMPORTANT).OptionalMultiplier(MyAPIGateway.Session.GrinderSpeedMultiplier).ResetFormatting();

                        if(hackable)
                        {
                            GetLine().Separator().Append("Hacked: ").TimeFormat(hackTime).Color(COLOR_UNIMPORTANT).OptionalMultiplier(MyAPIGateway.Session.HackSpeedMultiplier).ResetFormatting();
                        }
                    }
                }
            }
            #endregion Time to complete/grind

            #region Optional: item changes on grind
            if(!isProjected && Main.Config.AimInfo.IsSet(AimInfoFlags.GrindChangeWarning) && Main.EquipmentMonitor.IsAnyGrinder && !Main.TextAPI.IsEnabled)
            {
                if(Main.ModDetector.DetectedAwwScrap)
                {
                    AddLine(FontsHandler.YellowSh).Color(COLOR_WARNING).Append("Some/All components turn into specialized scrap on grind.");
                }
                else
                {
                    foreach(MyCubeBlockDefinition.Component comp in def.Components)
                    {
                        if(comp.DeconstructItem != null && comp.DeconstructItem != comp.Definition)
                        {
                            AddLine(FontsHandler.YellowSh).Color(COLOR_WARNING).Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText);
                        }
                    }
                }
            }
            #endregion Optional: item changes on grind

            #region Optional: grid moving
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.GridMoving) && grid.Physics != null)
            {
                bool hasLinearVel = !Vector3.IsZero(grid.Physics.LinearVelocity, 0.01f);
                bool hasAngularVel = !Vector3.IsZero(grid.Physics.AngularVelocity, 0.01f);

                if(hasLinearVel || hasAngularVel)
                {
                    AddLine().Color(COLOR_WARNING);

                    if(hasLinearVel)
                    {
                        GetLine().Append("Moving: ").SpeedFormat(grid.Physics.LinearVelocity.Length(), 2);
                    }

                    if(hasAngularVel)
                    {
                        if(hasLinearVel)
                            GetLine().Separator();

                        GetLine().Append("Rotating: ").RotationSpeed((float)grid.Physics.AngularVelocity.Length(), 2);
                    }
                }
            }
            #endregion Optional: grid moving

            #region Optional: ship grinder apply force
            if(!isProjected && Main.Config.AimInfo.IsSet(AimInfoFlags.ShipGrinderImpulse) && Main.EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder))
            {
                IMyShipController controller = MyAPIGateway.Session.ControlledObject as IMyShipController;
                if(controller != null)
                {
                    float impulse = Hardcoded.ShipGrinderImpulseForce(controller.CubeGrid, aimedBlock);
                    if(impulse > 0.00001f)
                    {
                        float gridMass = Main.GridMassCompute.GetGridMass(aimedBlock.CubeGrid);
                        float speed = impulse / gridMass;

                        if(speed >= 0.5f)
                            AddLine(FontsHandler.RedSh).Color(COLOR_BAD);
                        else
                            AddLine(FontsHandler.RedSh).Color(COLOR_WARNING);

                        GetLine().Append("Grind impulse: ").SpeedFormat(speed, 5).Append(" (").ForceFormat(impulse).Append(")");
                    }
                }
            }
            #endregion Optional: ship grinder apply force

            #region Optional: grinder makes grid split
            if(!Main.Config.UnderCrosshairMessages.Value && !isProjected && Main.Config.AimInfo.IsSet(AimInfoFlags.GrindGridSplit) && Main.EquipmentMonitor.IsAnyGrinder)
            {
                SplitFlags splitInfo = Main.SplitChecking.GetSplitInfo(aimedBlock);

                if(splitInfo.IsSet(SplitFlags.BlockLoss))
                {
                    AddLine(FontsHandler.RedSh).Color(COLOR_BAD).Append("Some block will vanish if this is removed!");
                }
                else if(splitInfo.IsSet(SplitFlags.Split))
                {
                    AddLine(FontsHandler.RedSh).Color(COLOR_BAD).Append("Grid will split if removed!");
                }
                else if(splitInfo.IsSet(SplitFlags.Disconnect))
                {
                    AddLine(FontsHandler.RedSh).Color(COLOR_WARNING).Append("Something will disconnect if this is removed.");
                }
            }
            #endregion Optional: grinder makes grid split

            #region Optional: warn about inventory contents being explosive
            // TODO: ammo detonation for inventory-owning blocks?
            // TODO: include some Main.Config.AimInfo.IsSet(AimInfoFlags.Explode) ?
            //if(!isProjected && has inventory ??)
            //{
            //    MyRelationsBetweenPlayerAndBlock relation = (aimedBlock.OwnerId > 0 ? localPlayer.GetRelationTo(aimedBlock.OwnerId) : MyRelationsBetweenPlayerAndBlock.NoOwnership);
            //
            //    if(relation.IsFriendly())
            //    {
            //        // HACK unused: def.DetonateChance & def.DamageThreshold
            //
            //        MyGameDefinition gameDef = MyDefinitionManager.Static.GetDefinition<MyGameDefinition>(MyGameDefinition.Default); // NOTE: This is not default if world is using some kind of scenario game definition...
            //
            //        
            //        /*
            //        if (MyFakes.ENABLE_AMMO_DETONATION)
            //        {
            //            MyCubeBlockDefinition cubeBlockDefinition = MyDefinitionManager.Static.GetCubeBlockDefinition(builder.GetId());
            //            MyGameDefinition gameDefinition = MySession.Static.GameDefinition;
            //            m_detonationData = new DetonationData
            //            {
            //                DamageThreshold = cubeBlockDefinition.DamageThreshold,
            //                DetonateChance = cubeBlockDefinition.DetonateChance,
            //                ExplosionAmmoVolumeMin = gameDefinition.ExplosionAmmoVolumeMin,
            //                ExplosionAmmoVolumeMax = gameDefinition.ExplosionAmmoVolumeMax,
            //                ExplosionRadiusMin = gameDefinition.ExplosionRadiusMin,
            //                ExplosionRadiusMax = gameDefinition.ExplosionRadiusMax,
            //                ExplosionDamagePerLiter = gameDefinition.ExplosionDamagePerLiter,
            //                ExplosionDamageMax = gameDefinition.ExplosionDamageMax
            //            };
            //            MyInventory inventory = this.GetInventory();
            //            if (inventory != null)
            //            {
            //                inventory.InventoryContentChanged += CacheItem;
            //                CacheInventory(inventory);
            //            }
            //        }*/
            //    }
            //}
            #endregion

            #region Optional: added by mod
            MyModContext context = def.Context;
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.AddedByMod) && !context.IsBaseGame)
            {
                if(Main.TextAPI.IsEnabled)
                {
                    AddLine().Color(COLOR_MOD).Append("Mod: ").Color(COLOR_MOD_TITLE).AppendMaxLength(context.ModName, MOD_NAME_MAX_LENGTH);

                    MyObjectBuilder_Checkpoint.ModItem modItem = context.ModItem;
                    if(modItem.Name != null && modItem.PublishedFileId > 0)
                        AddLine().Color(COLOR_MOD).Append("       | ").ResetFormatting().Append("ID: ").Append(modItem.PublishedServiceName).Append(":").Append(modItem.PublishedFileId);
                }
                else
                {
                    AddLine(FontsHandler.SkyBlueSh).Append("Mod: ").ModFormat(context);
                }
            }
            #endregion Optional: added by mod

            #region Optional: requires DLC
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.RequiresDLC))
            {
                DLCFormat(def);
            }
            #endregion Optional: requires DLC

            #region Overlay hints
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.OverlayHint))
            {
                AddOverlaysHint(def);
            }
            #endregion Overlay hints

            EndAddedLines();
        }
        #endregion Aimed block info generation

        #region Equipped block info generation
        public void GenerateBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            if(Main.Config.PlaceInfo.Value == 0)
                return;

            #region Block name line only for textAPI
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.BlockName) && Main.TextAPI.IsEnabled)
            {
                AddLine().Color(COLOR_BLOCKTITLE).Append(def.DisplayNameText);

                MyBlockVariantGroup variantsGroup = def.BlockVariantsGroup;

                if(variantsGroup != null)
                {
                    // variantsGroup.Blocks.Length contains all blocks of all sizes, it needs to filter out the irrelevant sizes.
                    int blockNumber = 0;
                    int totalBlocks = 0;

                    for(int i = 0; i < variantsGroup.Blocks.Length; ++i)
                    {
                        MyCubeBlockDefinition blockDef = variantsGroup.Blocks[i];

                        if(blockDef.CubeSize == def.CubeSize)
                        {
                            totalBlocks++;

                            if(blockDef == def)
                                blockNumber = totalBlocks;
                        }
                    }

                    if(totalBlocks > 1)
                        GetLine().Append("  ").Color(COLOR_BLOCKVARIANTS).Append("(Variant ").Append(blockNumber).Append(" of ").Append(totalBlocks).Append(")");
                }

                // TODO: implement in some nicer way?
                //MyCubeBlockDefinitionGroup pairDef = MyDefinitionManager.Static.TryGetDefinitionGroup(def.BlockPairName);
                //if(pairDef != null)
                //{
                //    if(pairDef.Large != null && pairDef.Small != null)
                //    {
                //        GetLine().ResetFormatting().Append(" (");

                //        if(def.CubeSize == MyCubeSize.Small)
                //            GetLine().Color(COLOR_GOOD).Append("s").ResetFormatting().Append("|L)");
                //        else
                //            GetLine().Append("s|").Color(COLOR_GOOD).Append("L").ResetFormatting().Append(")");
                //    }
                //}
            }
            #endregion Block name line only for textAPI

            #region Internal info
            if(Main.Config.InternalInfo.Value)
            {
                int obPrefixLen = "MyObjectBuilder_".Length;
                string typeIdString = def.Id.TypeId.ToString();
                AddLine().Color(COLOR_INTERNAL).Label("Id").Color(COLOR_NORMAL).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(def.Id.SubtypeName);
                AddLine().Color(COLOR_INTERNAL).Label("BlockPairName").Color(COLOR_NORMAL).Append(def.BlockPairName);
                AddLine().Color(COLOR_INTERNAL).Label("ModelIntersection").Append(def.UseModelIntersection);

                Vector3 offset = def.ModelOffset;
                if(offset.LengthSquared() > 0)
                    AddLine().Color(COLOR_INTERNAL).Label("ModelOffset").Color(COLOR_WARNING).Append("X:").Number(offset.X).Append(" Y:").Number(offset.Y).Append(" Z:").Number(offset.Z);
            }
            #endregion Internal info

            AppendBasics(def, part: false);

            #region Optional - different item gain on grinding
            if(!Main.TextAPI.IsEnabled && Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.GrindChangeWarning))
            {
                foreach(MyCubeBlockDefinition.Component comp in def.Components)
                {
                    if(comp.DeconstructItem != null && comp.DeconstructItem != comp.Definition)
                    {
                        AddLine(FontsHandler.RedSh).Color(COLOR_WARNING).Append("When grinding: ").Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText);
                    }
                }
            }
            #endregion Optional - different item gain on grinding

            // TODO: use? not sure if useful...
            //if(def.VoxelPlacement.HasValue)
            //{
            //    // Comment from definition:
            //    // <!--Possible settings Both,InVoxel,OutsideVoxel,Volumetric. If volumetric set than MaxAllowed and MinAllowed will be used.-->
            //
            //    var vp = def.VoxelPlacement.Value;
            //
            //    AddLine().Color(COLOR_WARNING).Append($"Terrain placement - Dynamic: ").Append(vp.DynamicMode.PlacementMode.ToString());
            //
            //    if(vp.DynamicMode.PlacementMode == VoxelPlacementMode.Volumetric)
            //        GetLine().Append(" (").ProportionToPercent(vp.DynamicMode.MinAllowed).Append(" to ").ProportionToPercent(vp.DynamicMode.MaxAllowed).Append(")");
            //
            //    GetLine().Separator().Append($"Static: ").Append(vp.StaticMode.PlacementMode.ToString());
            //
            //    if(vp.StaticMode.PlacementMode == VoxelPlacementMode.Volumetric)
            //        GetLine().Append(" (").ProportionToPercent(vp.StaticMode.MinAllowed).Append(" to ").ProportionToPercent(vp.StaticMode.MaxAllowed).Append(")");
            //
            //    GetLine().ResetColor();
            //}

            // TODO: cache needs clearing for this to get added/removed as creative tools are on/off
            #region Optional - creative-only stuff
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Mirroring) && (MyAPIGateway.Session.CreativeMode || MyAPIGateway.Session.EnableCopyPaste)) // HACK Session.EnableCopyPaste used as spacemaster check
            {
                if(def.MirroringBlock != null)
                {
                    MyCubeBlockDefinition mirrorDef;
                    if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(new MyDefinitionId(def.Id.TypeId, def.MirroringBlock), out mirrorDef))
                        AddLine(FontsHandler.GreenSh).Color(COLOR_GOOD).Append("Mirrors with: ").Append(mirrorDef.DisplayNameText);
                    else
                        AddLine(FontsHandler.RedSh).Color(COLOR_BAD).Append("Mirrors with: ").Append(def.MirroringBlock).Append(" (Error: not found)");
                }
            }
            #endregion Optional - creative-only stuff

            BData_Base data = null;

            #region Conveyor/interactibles count
            // TODO: move to its own flag?
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                data = data ?? Main.LiveDataHandler.Get<BData_Base>(def);
                if(data != null)
                {
                    int conveyors = 0;
                    int interactiveConveyors = 0;

                    if(data.ConveyorPorts != null)
                    {
                        foreach(ConveyorInfo port in data.ConveyorPorts)
                        {
                            if((port.Flags & ConveyorFlags.Interactive) != 0)
                                interactiveConveyors++;
                        }

                        conveyors = data.ConveyorPorts.Count - interactiveConveyors;
                    }

                    bool hasCustomLogic = false; // (data.Has & BlockHas.CustomLogic) != 0;
                    bool hasTerminal = (data.Has & BlockHas.Terminal) != 0;
                    bool hasPhysicalTerminal = (data.Has & BlockHas.PhysicalTerminalAccess) != 0;
                    bool hasConveyorPorts = (data.Has & BlockHas.ConveyorSupport) != 0 && (conveyors > 0 || interactiveConveyors > 0);
                    //bool hasInventory = (data.Has & BlockHas.Inventory) != 0;

                    StringBuilder line = AddLine();

                    if(hasTerminal)
                        line.Append("Terminal");
                    else
                        line.Append("No terminal");

                    if(hasPhysicalTerminal)
                        line.Separator().Color(COLOR_GOOD).Append("Physical terminal");
                    else if(!hasCustomLogic)
                        line.Separator().Color(hasTerminal ? COLOR_WARNING : COLOR_NORMAL).Append("No physical terminal");

                    if(hasConveyorPorts)
                    {
                        line.Separator().Color(COLOR_CONVEYORPORTS).Label("Conveyor ports").Append(conveyors + interactiveConveyors).ResetFormatting();
                    }

                    // HACK: weird conveyor support mention
                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings))
                    {
                        if(hasConveyorPorts && def.CubeSize == MyCubeSize.Small && def.Id.TypeId == typeof(MyObjectBuilder_SmallMissileLauncher))
                            AddLine(FontsHandler.YellowSh).Color(COLOR_WARNING).Append("UseConveyors is default off!");
                    }

                    //if(hasCustomLogic)
                    //{
                    //    AddLine().Append("NOTE: Block has custom logic from a mod, its behavior could be different or could not.");
                    //}
                }
            }
            #endregion Conveyor/interactibles count

            #region Optional upgrades
            // TODO: move to its own flag?
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo) && !(def is MyUpgradeModuleDefinition))
            {
                data = data ?? Main.LiveDataHandler.Get<BData_Base>(def);
                if(data != null)
                {
                    int upgrades = (data.Upgrades?.Count ?? 0);
                    int upgradePorts = (data.UpgradePorts?.Count ?? 0);

                    if(upgrades > 0 && upgradePorts > 0)
                    {
                        AddLine().Label("Upgrade ports").Color(COLOR_GOOD).Append(upgradePorts);
                        AddLine().Label(upgrades > 1 ? "Optional upgrades" : "Optional upgrade");
                        const int SpacePadding = 32;
                        const int NumPerRow = 2;

                        for(int i = 0; i < data.Upgrades.Count; i++)
                        {
                            if(i > 0)
                            {
                                if(i % NumPerRow == 0)
                                    AddLine().Color(COLOR_LIST).Append(' ', SpacePadding).Append("| ");
                                else
                                    GetLine().Separator();
                            }

                            GetLine().Color(COLOR_GOOD).Append(data.Upgrades[i]).ResetFormatting();
                        }
                    }
                    else if(upgradePorts > 0)
                    {
                        AddLine().Label("Unknown ports").Append(upgradePorts);

                        SimpleTooltip("These ports are upgrade ports but the block has no upgrades declared in code."
                                    + "\nThey can have custom functionality provided by a mod or no functionality at all."
                                    + "\nThey're shown in ports overlay mode if you need to see them.");
                    }
                }
            }
            #endregion Optional upgrades

            bool hasFormatter = false;

            #region Per-block info
            if(def.Id.TypeId != typeof(MyObjectBuilder_CubeBlock)) // anything non-decorative
            {
                TextGenerationCall action;

                if(formatLookup.TryGetValue(def.Id.TypeId, out action))
                {
                    action.Invoke(def);
                    hasFormatter = true;
                }
            }
            #endregion Per-block info

            if(!hasFormatter)
            {
                InventoryStats(def);
            }

            MyFunctionalBlockDefinition fbDef = def as MyFunctionalBlockDefinition; // NOTE: this doesn't mean that it can be turned on/off, it means it has LCD... because keen.
            if(fbDef != null)
            {
                AddScreenInfo(fbDef);
            }

            #region Added by mod
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.AddedByMod) && !def.Context.IsBaseGame)
            {
                AddLine(FontsHandler.SkyBlueSh).Color(COLOR_MOD).Append("Mod: ").ModFormat(def.Context);
            }
            #endregion Added by mod

            #region requires DLC
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.RequiresDLC))
            {
                DLCFormat(def);
            }
            #endregion Optional: requires DLC

            #region Overlay hints
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.OverlayHint))
            {
                AddOverlaysHint(def);
            }
            #endregion Overlay hints

            //{
            //    MyContainerDefinition containerDef;
            //    if(MyComponentContainerExtension.TryGetContainerDefinition(def.Id.TypeId, def.Id.SubtypeId, out containerDef) && containerDef.DefaultComponents != null)
            //    {
            //        foreach(MyContainerDefinition.DefaultComponent compInfo in containerDef.DefaultComponents)
            //        {
            //            AddLine().Append($"{compInfo.BuilderType} / {compInfo.InstanceType} / {compInfo.SubtypeId}; forceCreate={compInfo.ForceCreate}");
            //
            //            MyStringHash subtype = compInfo.SubtypeId.GetValueOrDefault(def.Id.SubtypeId);
            //
            //            MyComponentDefinitionBase compBase;
            //            if(MyComponentContainerExtension.TryGetComponentDefinition(compInfo.BuilderType, subtype, out compBase))
            //            {
            //                GetLine().Append($" - {compBase}");
            //            }
            //        }
            //    }
            //}

            EndAddedLines();
        }
        #endregion Equipped block info generation

        #region Shared generation methods
        private void AppendBasics(MyCubeBlockDefinition def, bool part = false)
        {
            bool deformable = (def.BlockTopology == MyBlockTopology.Cube && def.UsesDeformation);
            int assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
            bool buildModels = (def.BuildProgressModels != null && def.BuildProgressModels.Length > 0);
            float weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
            float grindRatio = def.DisassembleRatio;

            if(def is MyDoorDefinition)
                grindRatio *= Hardcoded.Door_DisassembleRatioMultiplier;
            else if(def is MyAdvancedDoorDefinition)
                grindRatio *= Hardcoded.AdvDoor_Closed_DisassembleRatioMultiplier;

            string partPrefix = string.Empty;
            if(part)
            {
                AddLine(FontsHandler.SkyBlueSh).Color(COLOR_PART).Label("Part").Append(def.DisplayNameText);
                partPrefix = (Main.TextAPI.IsEnabled ? "<color=55,255,155>        | <reset>" : "       | ");
                Utilities.StringBuilderExtensions.CurrentColor = COLOR_NORMAL;
            }

            #region Mass/size/build time/deconstruct time/no models
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Line1))
            {
                AddLine().Append(partPrefix);

                // HACK: game doesn't use mass from blocks with HasPhysics=false
                GetLine().Color(new Color(200, 255, 55)).MassFormat(def.HasPhysics ? def.Mass : 0).ResetFormatting().Separator()
                    .Size3DFormat(def.Size).Separator()
                    .TimeFormat(assembleTime / weldMul).Color(COLOR_UNIMPORTANT).OptionalMultiplier(weldMul).ResetFormatting();

                if(Math.Abs(grindRatio - 1) >= 0.0001f)
                    GetLine().Separator().Color(grindRatio > 1 ? COLOR_BAD : (grindRatio < 1 ? COLOR_GOOD : COLOR_NORMAL)).Append("Deconstructs: ").ProportionToPercent(1f / grindRatio).ResetFormatting();
            }
            #endregion Mass/size/build time/deconstruct time/no models

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Line2))
            {
                AddLine().Append(partPrefix).Label("Integrity").Append(def.MaxIntegrity.ToString("#,###,###,###,###"));

                if(deformable)
                    GetLine().Separator().Label("Deform Ratio").RoundedNumber(def.DeformationRatio, 2);

                float dmgMul = def.GeneralDamageMultiplier;
                if(dmgMul != 1f)
                {
                    GetLine().Separator();
                    DamageMultiplierAsResistance(dmgMul);
                }

                // .DamageThreshold and .DetonateChance are for cargo+ammo detonation but are NOT used

                // TODO: improve formatting?
                // HACK: DamageMultiplierExplosion is only used if block has a FatBlock and it's applied after the damage event.
                float expDmgMul = def.DamageMultiplierExplosion;
                if(expDmgMul != 1f && !string.IsNullOrEmpty(def.Model)) // having an independent model makes it have a fatblock
                {
                    GetLine().Separator();
                    DamageMultiplierAsResistance(expDmgMul, "Explosive Res");
                }

                if(buildModels)
                {
                    bool customBuildMounts = false;

                    for(int i = 0; i < def.BuildProgressModels.Length; i++)
                    {
                        MyCubeBlockDefinition.BuildProgressModel bpm = def.BuildProgressModels[i];
                        if(bpm.MountPoints != null && bpm.MountPoints.Length > 0)
                        {
                            customBuildMounts = true;
                            break;
                        }
                    }

                    if(customBuildMounts)
                    {
                        StringBuilder sb = AddLine().Color(COLOR_WARNING).Append("Different mount points in build stage!").ResetFormatting().Append(" (");
                        Main.Config.ConstructionModelPreviewBind.Value.GetBinds(sb, ControlContext.BUILD, specialChars: true);
                        sb.Append(" and ");
                        Main.Config.CycleOverlaysBind.Value.GetBinds(sb, ControlContext.BUILD, specialChars: true);
                        sb.Append(" to see)").ResetFormatting();
                    }
                }
                //else
                //{
                //    AddLine().Color(COLOR_WARNING).Append("No build stage models.").ResetFormatting();
                //}
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ComponentsVolume))
            {
                float totalVolumeM3 = 0f;

                foreach(MyCubeBlockDefinition.Component comp in def.Components)
                {
                    totalVolumeM3 += comp.Definition.Volume * comp.Count;
                }

                float charInvVolM3 = float.MaxValue;
                IMyInventory inv = MyAPIGateway.Session?.Player?.Character?.GetInventory();
                if(inv != null)
                    charInvVolM3 = (float)inv.MaxVolume - CharInvVolM3Offset;

                AddLine().Append(partPrefix).Append("Components: ").Color(totalVolumeM3 > charInvVolM3 ? COLOR_WARNING : COLOR_NORMAL).VolumeFormat(totalVolumeM3 * 1000).ResetFormatting();
            }

            #region Target groups and priority
            // TODO: its own flag?
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Line2))
            {
                StringBuilder sb = AddLine().Append(partPrefix).Label("Target - Priority").MultiplierFormat(def.PriorityModifier);

                // HACK: from MyLargeTurretTargetingSystem.TestPotentialTarget()
                // HACK: MyFunctionalBlockDefinition is also used by non-MyFunctionalBlock types...
                if(MyAPIGateway.Reflection.IsAssignableFrom(typeof(MyObjectBuilder_FunctionalBlock), def.Id.TypeId))
                {
                    sb.Separator().MultiplierFormat(def.PriorityModifier * def.NotWorkingPriorityMultiplier).Append(" if not working");
                }

                bool hasGroup = def.TargetingGroups != null && def.TargetingGroups.Count > 0;
                if(!hasGroup)
                {
                    foreach(MyTargetingGroupDefinition group in BuildInfoMod.Instance.Caches.OrderedTargetGroups)
                    {
                        if(group.DefaultBlockTypes.Contains(def.Id.TypeId))
                        {
                            hasGroup = true;
                            break;
                        }
                    }
                }

                sb.Separator().Label("Type");
                if(hasGroup)
                {
                    int atLabelLen = sb.Length;

                    if(def.TargetingGroups != null && def.TargetingGroups.Count > 0)
                    {
                        foreach(MyStringHash group in def.TargetingGroups)
                        {
                            sb.Append(group.String).Append(", ");
                        }
                    }
                    else
                    {
                        foreach(MyTargetingGroupDefinition group in BuildInfoMod.Instance.Caches.OrderedTargetGroups)
                        {
                            if(group.DefaultBlockTypes.Contains(def.Id.TypeId))
                            {
                                sb.Append(group.Id.SubtypeName).Append(", ");
                            }
                        }
                    }

                    if(sb.Length > atLabelLen)
                        sb.Length -= 2; // remove last comma
                }
                else
                {
                    sb.Append("Default");
                }
            }
            #endregion

            // TODO: add PlaceInfoFlags.ModDetails? and use everywhere where's mod-given info
            //if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ModDetails))
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Line2))
            {
                CoreSystemsDef.ArmorDefinition csArmorDef;
                if(Main.CoreSystemsAPIHandler.Armor.TryGetValue(def.Id.SubtypeName, out csArmorDef))
                {
                    Format_CoreSystemsArmor(def, csArmorDef);
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(!def.IsStandAlone || !def.HasPhysics)
                    AddLine().Append(partPrefix);

                if(!def.HasPhysics)
                {
                    GetLine().Append("No collisions");
                }

                if(!def.IsStandAlone || !def.HasPhysics)
                {
                    if(!def.HasPhysics)
                        GetLine().Separator();

                    GetLine().Color(COLOR_WARNING).Append("No standalone");

                    SimpleTooltip("'No standalone' means grid will self-delete if it's entirely made of blocks with this tag."
                                + "\nBlocks with 'No collisions' also have this behavior while also not providing any mass to the grid.");
                }
            }

            #region Airtightness
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Airtight))
            {
                int airTightFaces, toggledAirTightFaces, totalFaces;
                AirTightMode airTight = Pressurization.GetAirTightFaces(def, out airTightFaces, out toggledAirTightFaces, out totalFaces);

                StringBuilder sb = AddLine(font: (airTight == AirTightMode.SEALED ? FontsHandler.GreenSh : (airTight == AirTightMode.NOT_SEALED ? FontsHandler.YellowSh : FontsHandler.SkyBlueSh)));
                sb.Append(partPrefix);

                bool isDoor = (def is MyDoorDefinition || def is MyAdvancedDoorDefinition || def is MyAirtightDoorGenericDefinition);

                if(isDoor)
                {
                    int toggledSides = 0;

                    if(airTight == AirTightMode.SEALED)
                    {
                        toggledSides = 0;
                    }
                    else
                    {
                        for(int i = 0; i < Base6Directions.IntDirections.Length; ++i)
                        {
                            Vector3I normal = Base6Directions.IntDirections[i];
                            if(Pressurization.IsDoorAirtight(def, ref normal, fullyClosed: true))
                                toggledSides++;
                        }
                    }

                    if(airTight == AirTightMode.SEALED)
                        sb.Color(COLOR_WARNING).Label("Air-tight").Append("Fully sealed - Even when open!");
                    else if(airTight == AirTightMode.NOT_SEALED && toggledSides == 0 && toggledAirTightFaces == 0)
                        sb.Color(COLOR_WARNING).Label("Air-tight").Append("Passthrough - Even when closed!");
                    else
                    {
                        sb.Color(COLOR_INFO).Label("Air-tight").Append(airTightFaces).Append(" of ").Append(totalFaces).Append(" faces sealed");
                        if(toggledAirTightFaces > 0 || toggledSides > 0)
                            sb.Append(" - More if closed");
                    }
                }
                else
                {
                    if(airTight == AirTightMode.SEALED)
                        sb.Color(COLOR_GOOD).Label("Air-tight").Append("Fully sealed");
                    else if(airTight == AirTightMode.NOT_SEALED)
                        sb.Color(COLOR_WARNING).Label("Air-tight").Append("Passthrough");
                    else
                        sb.Color(COLOR_INFO).Label("Air-tight").Append(airTightFaces).Append(" of ").Append(totalFaces).Append(" faces sealed");
                }

                StringBuilder tooltip = CreateTooltip();
                if(tooltip != null)
                {
                    tooltip.Append("This is a very simplified summary of block's interaction with pressurization." +
                                  "\nA 'face' is a side of a grid cell. A 1x1x1 block occupies only one grid cell, larger blocks occupy more cells." +
                                  "\nOnly faces towards the exterior of the block can be airtight." +
                                  "\nDoor block types can also have extra sides or faces that they seal when closed." +
                                  "\nTo properly see airtightness of a block, turn on overlays using: <color=0,255,155>");
                    Main.Config.CycleOverlaysBind.Value.GetBinds(tooltip);
                }
            }
            #endregion Airtightness
        }
        #endregion Shared generation methods

        #region Per block info
        public void InitLookups()
        {
            TextGenerationCall action;

            Add(typeof(MyObjectBuilder_TerminalBlock), Format_TerminalBlock);

            action = Format_Conveyors;
            Add(typeof(MyObjectBuilder_Conveyor), action);
            Add(typeof(MyObjectBuilder_ConveyorConnector), action);

            Add(typeof(MyObjectBuilder_ShipConnector), Format_Connector);

            action = Format_CargoAndCollector;
            Add(typeof(MyObjectBuilder_Collector), action);
            Add(typeof(MyObjectBuilder_CargoContainer), action);

            Add(typeof(MyObjectBuilder_ConveyorSorter), Format_ConveyorSorter);

            Add(typeof(MyObjectBuilder_Drill), Format_Drill);

            action = Format_WelderAndGrinder;
            Add(typeof(MyObjectBuilder_ShipWelder), action);
            Add(typeof(MyObjectBuilder_ShipGrinder), action);

            action = Format_Piston;
            Add(typeof(MyObjectBuilder_PistonBase), action);
            Add(typeof(MyObjectBuilder_ExtendedPistonBase), action);

            action = Format_Rotor;
            Add(typeof(MyObjectBuilder_MotorStator), action);
            Add(typeof(MyObjectBuilder_MotorAdvancedStator), action);
            Add(typeof(MyObjectBuilder_MotorSuspension), action);

            Add(typeof(MyObjectBuilder_MergeBlock), Format_MergeBlock);

            Add(typeof(MyObjectBuilder_LandingGear), Format_LandingGear);

            action = Format_ShipController;
            Add(typeof(MyObjectBuilder_ShipController), action);
            Add(typeof(MyObjectBuilder_Cockpit), action);
            Add(typeof(MyObjectBuilder_CryoChamber), action);
            Add(typeof(MyObjectBuilder_RemoteControl), action);

            Add(typeof(MyObjectBuilder_Thrust), Format_Thrust);

            Add(typeof(MyObjectBuilder_Gyro), Format_Gyro);

            action = Format_Light;
            Add(typeof(MyObjectBuilder_LightingBlock), action);
            Add(typeof(MyObjectBuilder_InteriorLight), action);
            Add(typeof(MyObjectBuilder_ReflectorLight), action);

            Add(typeof(MyObjectBuilder_OreDetector), Format_OreDetector);

            action = Format_Projector;
            Add(typeof(MyObjectBuilder_ProjectorBase), action);
            Add(typeof(MyObjectBuilder_Projector), action);

            Add(typeof(MyObjectBuilder_Door), Format_Door);

            Add(typeof(MyObjectBuilder_Ladder2), Format_Ladder);

            action = Format_AirtightDoor;
            Add(typeof(MyObjectBuilder_AirtightDoorGeneric), action);
            Add(typeof(MyObjectBuilder_AirtightHangarDoor), action);
            Add(typeof(MyObjectBuilder_AirtightSlideDoor), action);

            Add(typeof(MyObjectBuilder_AdvancedDoor), Format_AdvancedDoor);

            Add(typeof(MyObjectBuilder_Parachute), Format_Parachute);

            Add(typeof(MyObjectBuilder_MedicalRoom), Format_MedicalRoom);

            action = Format_Production;
            Add(typeof(MyObjectBuilder_ProductionBlock), action);
            Add(typeof(MyObjectBuilder_Refinery), action);
            Add(typeof(MyObjectBuilder_Assembler), action);
            Add(typeof(MyObjectBuilder_SurvivalKit), action);
            Add(typeof(MyObjectBuilder_GasTank), action);
            Add(typeof(MyObjectBuilder_OxygenTank), action);
            Add(typeof(MyObjectBuilder_OxygenGenerator), action);

            Add(typeof(MyObjectBuilder_OxygenFarm), Format_OxygenFarm);

            Add(typeof(MyObjectBuilder_AirVent), Format_AirVent);
            Add(typeof(MyObjectBuilder_UpgradeModule), Format_UpgradeModule);

            action = Format_PowerProducer;
            Add(typeof(MyObjectBuilder_Reactor), action);
            Add(typeof(MyObjectBuilder_HydrogenEngine), action);
            Add(typeof(MyObjectBuilder_BatteryBlock), action);
            Add(typeof(MyObjectBuilder_SolarPanel), action);
            Add(typeof(MyObjectBuilder_WindTurbine), action);

            Add(typeof(MyObjectBuilder_RadioAntenna), Format_RadioAntenna);

            Add(typeof(MyObjectBuilder_LaserAntenna), Format_LaserAntenna);

            Add(typeof(MyObjectBuilder_Beacon), Format_Beacon);

            Add(typeof(MyObjectBuilder_TimerBlock), Format_Timer);

            Add(typeof(MyObjectBuilder_MyProgrammableBlock), Format_ProgrammableBlock);

            Add(typeof(MyObjectBuilder_TextPanel), Format_LCD);
            Add(typeof(MyObjectBuilder_LCDPanelsBlock), Format_LCDPanels);

            action = Format_SoundBlock;
            Add(typeof(MyObjectBuilder_SoundBlock), action);
            Add(typeof(MyObjectBuilder_Jukebox), action);

            Add(typeof(MyObjectBuilder_SensorBlock), Format_Sensor);

            Add(typeof(MyObjectBuilder_CameraBlock), Format_Camera);

            Add(typeof(MyObjectBuilder_ButtonPanel), Format_Button);

            action = Format_GravityGenerator;
            Add(typeof(MyObjectBuilder_GravityGeneratorBase), action);
            Add(typeof(MyObjectBuilder_GravityGenerator), action);
            Add(typeof(MyObjectBuilder_GravityGeneratorSphere), action);

            Add(typeof(MyObjectBuilder_VirtualMass), Format_ArtificialMass);

            Add(typeof(MyObjectBuilder_SpaceBall), Format_SpaceBall);

            Add(typeof(MyObjectBuilder_JumpDrive), Format_JumpDrive);

            action = Format_Weapon;
            Add(typeof(MyObjectBuilder_ConveyorTurretBase), action);
            Add(typeof(MyObjectBuilder_UserControllableGun), action);
            Add(typeof(MyObjectBuilder_LargeGatlingTurret), action);
            Add(typeof(MyObjectBuilder_LargeMissileTurret), action);
            Add(typeof(MyObjectBuilder_InteriorTurret), action);
            Add(typeof(MyObjectBuilder_SmallGatlingGun), action);
            Add(typeof(MyObjectBuilder_SmallMissileLauncher), action);
            Add(typeof(MyObjectBuilder_SmallMissileLauncherReload), action);

            Add(typeof(MyObjectBuilder_Warhead), Format_Warhead);

            Add(Hardcoded.TargetDummyType, Format_TargetDummy);

            Add(typeof(MyObjectBuilder_TurretControlBlock), Format_TurretControl);

            Add(typeof(MyObjectBuilder_Searchlight), Format_Searchlight);

            Add(typeof(MyObjectBuilder_FlightMovementBlock), Format_AIBlocks);
            Add(typeof(MyObjectBuilder_DefensiveCombatBlock), Format_AIBlocks);
            Add(typeof(MyObjectBuilder_OffensiveCombatBlock), Format_AIBlocks);
            Add(typeof(MyObjectBuilder_BasicMissionBlock), Format_AIBlocks);
            Add(typeof(MyObjectBuilder_PathRecorderBlock), Format_AIBlocks);

            Add(typeof(MyObjectBuilder_EventControllerBlock), Format_EventController);
            Add(typeof(MyObjectBuilder_EmotionControllerBlock), Format_EmotionController);

            Add(typeof(MyObjectBuilder_HeatVentBlock), Format_HeatVent);

            Add(typeof(MyObjectBuilder_SafeZoneBlock), Format_SafeZone);
            Add(typeof(MyObjectBuilder_ContractBlock), Format_ContractBlock);
            Add(typeof(MyObjectBuilder_StoreBlock), Format_StoreBlock);
            Add(typeof(MyObjectBuilder_VendingMachine), Format_StoreBlock);
        }

        private void Add(MyObjectBuilderType blockType, TextGenerationCall call)
        {
            formatLookup.Add(blockType, call);
        }

        private void Format_TerminalBlock(MyCubeBlockDefinition def)
        {
            // HACK hardcoded; control panel doesn't use power
            PowerRequired(0, null, powerHardcoded: true);
        }

        #region Conveyors
        private void Format_Conveyors(MyCubeBlockDefinition def)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().LabelHardcoded("Power required");
                GetLine().PowerFormat(Hardcoded.Conveyors_PowerReqPerGrid).Append(" per grid (regardless of conveyor presence)");

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    AddLine().Append("    ").ResourcePriority(Hardcoded.Conveyors_PowerGroup, hardcoded: true);
            }
        }

        private void Format_Connector(MyCubeBlockDefinition def)
        {
            PowerRequired(0, null, powerHardcoded: true);

            InventoryStats(def, hardcodedVolume: Hardcoded.ShipConnector_InventoryVolume(def));

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                BData_Connector data = Main.LiveDataHandler.Get<BData_Connector>(def);
                if(data != null)
                {
                    StringBuilder sb = AddLine().Label("Connectable");
                    if(data.IsConnector)
                        sb.Append("Yes (").Append(data.IsSmallConnector ? "Small" : "Large").Append(" port)");
                    else
                        sb.Append("No");

                    AddLine().LabelHardcoded("Can throw out items").Append("Yes");
                }
            }
        }

        private void Format_CargoAndCollector(MyCubeBlockDefinition def)
        {
            MyCargoContainerDefinition cargo = (MyCargoContainerDefinition)def;

            MyPoweredCargoContainerDefinition poweredCargo = def as MyPoweredCargoContainerDefinition; // collector
            if(poweredCargo != null)
            {
                PowerRequired(poweredCargo.RequiredPowerInput, poweredCargo.ResourceSinkGroup);
            }

            InventoryStats(def, alternateVolume: cargo.InventorySize.Volume);
        }

        private void Format_ConveyorSorter(MyCubeBlockDefinition def)
        {
            // conveyor sorter type is used by WeaponCore too.
            List<CoreSystemsDef.WeaponDefinition> csWeaponDefs;
            if(Main.CoreSystemsAPIHandler.IsRunning && Main.CoreSystemsAPIHandler.Weapons.TryGetValue(def.Id, out csWeaponDefs))
            {
                Format_CoreSystemsWeapon(def, csWeaponDefs);
                return;
            }

            MyConveyorSorterDefinition sorter = def as MyConveyorSorterDefinition; // does not extend MyPoweredCargoContainerDefinition
            if(sorter == null)
                return;

            PowerRequired(sorter.PowerInput, sorter.ResourceSinkGroup);

            InventoryStats(def, alternateVolume: sorter.InventorySize.Volume);
        }
        #endregion Conveyors

        private void Format_Piston(MyCubeBlockDefinition def)
        {
            MyPistonBaseDefinition piston = (MyPistonBaseDefinition)def;

            PowerRequired(piston.RequiredPowerInput, piston.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Extended length").DistanceFormat(piston.Maximum).Separator().Label("Max velocity").DistanceFormat(piston.MaxVelocity);
                AddLine().Label("Max Force, Safe").ForceFormat(piston.UnsafeImpulseThreshold).Separator().Label("Unsafe").ForceFormat(piston.MaxImpulse);
            }

            Suffix_Mechanical(def, piston.TopPart);
        }

        private void Format_Rotor(MyCubeBlockDefinition def)
        {
            MyMotorStatorDefinition motor = (MyMotorStatorDefinition)def;
            MyMotorSuspensionDefinition suspension = def as MyMotorSuspensionDefinition;
            if(suspension != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
                {
                    AddLine().Label("Power - Idle").PowerFormat(suspension.RequiredIdlePowerInput).Separator().Label("Running").PowerFormat(suspension.RequiredPowerInput);

                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(suspension.ResourceSinkGroup);
                }

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine().Label("Max torque").TorqueFormat(suspension.PropulsionForce).Separator().Label("Axle Friction").TorqueFormat(suspension.AxleFriction);
                    AddLine().Label("Steering - Max angle").AngleFormat(suspension.MaxSteer).Separator().Label("Speed base").RotationSpeed(suspension.SteeringSpeed * 60);
                    AddLine().Label("Ride height").DistanceRangeFormat(suspension.MinHeight, suspension.MaxHeight);
                }
            }
            else
            {
                PowerRequired(motor.RequiredPowerInput, motor.ResourceSinkGroup);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    if(motor.MinAngleDeg.HasValue || motor.MaxAngleDeg.HasValue)
                    {
                        AddLine().Label("Rotation Limits").AngleFormatDeg(motor.MinAngleDeg.GetValueOrDefault(-180)).Append(" to ").AngleFormatDeg(motor.MaxAngleDeg.GetValueOrDefault(180));
                    }

                    AddLine().Label("Max Torque, Safe").TorqueFormat(motor.UnsafeTorqueThreshold).Separator().Label("Unsafe").TorqueFormat(motor.MaxForceMagnitude);

                    if(motor.RotorDisplacementMin < motor.RotorDisplacementMax)
                    {
                        AddLine().Label("Displacement Large Top").DistanceRangeFormat(motor.RotorDisplacementMin, motor.RotorDisplacementMax);
                    }

                    if(motor.RotorDisplacementMinSmall < motor.RotorDisplacementMaxSmall)
                    {
                        AddLine().Label("Displacement Small Top").DistanceRangeFormat(motor.RotorDisplacementMinSmall, motor.RotorDisplacementMaxSmall);
                    }
                }
            }

            Suffix_Mechanical(def, motor.TopPart);
        }

        private void Suffix_Mechanical(MyCubeBlockDefinition def, string topPart)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PartStats))
            {
                MyCubeBlockDefinitionGroup group = MyDefinitionManager.Static.TryGetDefinitionGroup(topPart);
                if(group == null)
                    return;

                MyCubeBlockDefinition partDef = (def.CubeSize == MyCubeSize.Large ? group.Large : group.Small);

                if(partDef != null)
                    AppendBasics(partDef, part: true);
                else
                    AddLine().Color(COLOR_BAD).Append("No attachable part declared!");
            }
        }

        private void Format_MergeBlock(MyCubeBlockDefinition def)
        {
            MyMergeBlockDefinition merge = (MyMergeBlockDefinition)def;

            // HACK hardcoded; MergeBlock doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Pull strength").Append(merge.Strength.ToString("###,###,##0.#######"));
            }
        }

        private void Format_LandingGear(MyCubeBlockDefinition def)
        {
            MyLandingGearDefinition lg = (MyLandingGearDefinition)def;

            // HACK: hardcoded; LG doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max differential velocity for locking").SpeedFormat(lg.MaxLockSeparatingVelocity);
            }
        }

        #region Ship tools
        private void Format_Drill(MyCubeBlockDefinition def)
        {
            MyShipDrillDefinition shipDrill = (MyShipDrillDefinition)def;

            PowerRequired(Hardcoded.ShipDrill_Power, shipDrill.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                MyInventoryComponentDefinition invComp = Utils.GetInventoryFromComponent(def);
                float volume = invComp?.Volume ?? Hardcoded.ShipDrill_InventoryVolume(def);
                AddLine().LabelHardcoded("Inventory").InventoryFormat(volume, Hardcoded.ShipDrill_InventoryConstraint, invComp);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float mineRadius = Hardcoded.ShipDrill_VoxelVisualAdd + shipDrill.CutOutRadius;
                float carveRadius = Hardcoded.ShipDrill_VoxelVisualAdd + (shipDrill.CutOutRadius * Hardcoded.Drill_MineVoelNoOreRadiusMul);
                AddLine().LabelHardcoded("Mining radius").DistanceFormat(mineRadius).Separator().LabelHardcoded("when not collecting").DistanceFormat(carveRadius);
                AddLine().Label("Entity detection radius").DistanceFormat(shipDrill.SensorRadius);
            }
        }

        private void Format_WelderAndGrinder(MyCubeBlockDefinition def)
        {
            MyShipToolDefinition shipTool = (MyShipToolDefinition)def;
            bool isWelder = def is MyShipWelderDefinition;

            PowerRequired(Hardcoded.ShipTool_PowerReq, Hardcoded.ShipTool_PowerGroup, powerHardcoded: true, groupHardcoded: true);

            InventoryStats(def, hardcodedVolume: Hardcoded.ShipTool_InventoryVolume(def));

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(isWelder)
                {
                    float mul = MyAPIGateway.Session.WelderSpeedMultiplier;

                    float peakWeld = Hardcoded.ShipWelder_WeldPerSec(1);

                    int weakestAt = Hardcoded.ShipWelder_DivideByTargets;
                    float leastWeld = Hardcoded.ShipWelder_WeldPerSec(weakestAt);

                    AddLine().LabelHardcoded("Peak weld speed").ProportionToPercent(peakWeld).Color(COLOR_UNIMPORTANT).OptionalMultiplier(mul).ResetFormatting().Append(" for one block")
                             .Separator().ProportionToPercent(leastWeld).Color(COLOR_UNIMPORTANT).OptionalMultiplier(mul).ResetFormatting().Append(" for ").Append(weakestAt).Append("+ blocks");

                    AddLine().Label("Welding radius").DistanceFormat(shipTool.SensorRadius);
                }
                else
                {
                    float mul = MyAPIGateway.Session.GrinderSpeedMultiplier;

                    float peakGrind = Hardcoded.ShipGrinder_GrindPerSec(1);

                    int weakestAt = Hardcoded.ShipGrinder_DivideByTargets;
                    float leastGrind = Hardcoded.ShipGrinder_GrindPerSec(weakestAt);

                    AddLine().LabelHardcoded("Peak grind speed").ProportionToPercent(peakGrind).Color(COLOR_UNIMPORTANT).OptionalMultiplier(mul).ResetFormatting().Append(" for one block")
                             .Separator().ProportionToPercent(leastGrind).Color(COLOR_UNIMPORTANT).OptionalMultiplier(mul).ResetFormatting().Append(" for ").Append(weakestAt).Append("+ blocks");

                    AddLine().Label("Grinding radius").DistanceFormat(shipTool.SensorRadius);
                }
            }
        }
        #endregion Ship tools

        private void Format_ShipController(MyCubeBlockDefinition def)
        {
            MyShipControllerDefinition shipController = (MyShipControllerDefinition)def;

            MyRemoteControlDefinition rc = def as MyRemoteControlDefinition;
            if(rc != null)
            {
                PowerRequired(rc.RequiredPowerInput, rc.ResourceSinkGroup);
            }

            MyCryoChamberDefinition cryo = def as MyCryoChamberDefinition;
            if(cryo != null)
            {
                PowerRequired(cryo.IdlePowerConsumption, cryo.ResourceSinkGroup);

                if(cryo.HasInventory)
                    InventoryStats(def, hardcodedVolume: Hardcoded.Cockpit_InventoryVolume);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                StringBuilder sb = AddLine().Append("Abilities: ");

                int preLen = sb.Length;

                if(shipController is MyCryoChamberDefinition)
                    sb.Append("Cryo, ");

                if(shipController.EnableShipControl)
                    sb.Append("Ship control, ");

                if(shipController.EnableBuilderCockpit)
                    sb.Append("Place blocks, ");

                if(!shipController.EnableFirstPerson)
                    sb.Append("3rd person view only, ");

                if(preLen == sb.Length)
                {
                    sb.Append("None.");
                }
                else
                {
                    sb.Length -= 2; // remove last comma
                    sb.Append(".");
                }
            }

            MyCockpitDefinition cockpit = def as MyCockpitDefinition;
            if(cockpit != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    StringBuilder sb = AddLine((cockpit.IsPressurized ? FontsHandler.GreenSh : FontsHandler.RedSh))
                       .Color(cockpit.IsPressurized ? COLOR_GOOD : COLOR_WARNING)
                       .Label("Pressurized seat");

                    if(cockpit.IsPressurized)
                        sb.Append("Yes, Oxygen capacity: ").VolumeFormat(cockpit.OxygenCapacity);
                    else
                        sb.Append("No");

                    if(cockpit.HUD != null)
                    {
                        MyDefinitionBase defHUD;
                        if(MyDefinitionManager.Static.TryGetDefinition(new MyDefinitionId(typeof(MyObjectBuilder_HudDefinition), cockpit.HUD), out defHUD))
                        {
                            AddLine(FontsHandler.GreenSh).Color(COLOR_GOOD).Append("Custom HUD: ").Append(cockpit.HUD).ResetFormatting().Separator().Color(COLOR_MOD).Append("Mod: ").ModFormat(defHUD.Context);
                        }
                        else
                        {
                            AddLine(FontsHandler.RedSh).Color(COLOR_BAD).Append("Custom HUD: ").Append(cockpit.HUD).Append("  (Error: not found)");
                        }
                    }
                }

                if(cockpit.HasInventory)
                    InventoryStats(def, hardcodedVolume: Hardcoded.Cockpit_InventoryVolume);
            }
        }

        private void Format_Thrust(MyCubeBlockDefinition def)
        {
            MyThrustDefinition thrust = (MyThrustDefinition)def;

            if(thrust.FuelConverter != null && !thrust.FuelConverter.FuelId.IsNull() && thrust.FuelConverter.FuelId != MyResourceDistributorComponent.ElectricityId)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
                {
                    MyGasProperties fuelDef;
                    if(MyDefinitionManager.Static.TryGetDefinition(thrust.FuelConverter.FuelId, out fuelDef))
                    {
                        // HACK formula from MyEntityThrustComponent.PowerAmountToFuel()
                        float eff = (fuelDef.EnergyDensity * thrust.FuelConverter.Efficiency);
                        float minFuelUsage = thrust.MinPowerConsumption / eff;
                        float maxFuelUsage = thrust.MaxPowerConsumption / eff;

                        AddLine().Label("Requires").Append(thrust.FuelConverter.FuelId.SubtypeId);

                        if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                            GetLine().Separator().ResourcePriority(thrust.ResourceSinkGroup);

                        AddLine().Label("Consumption, Max").VolumeFormat(maxFuelUsage).Append("/s").Separator().Label("Idle").VolumeFormat(minFuelUsage).Append("/s");
                    }
                    else
                    {
                        AddLine(FontsHandler.RedSh).Color(COLOR_BAD).Label("Requires").Append(thrust.FuelConverter.FuelId.SubtypeId).Append(" (does not exist)");
                    }
                }
            }
            else
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
                {
                    AddLine().Label("Requires").Append("Electricity");

                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(thrust.ResourceSinkGroup);

                    AddLine().Label("Consumption, Max").PowerFormat(thrust.MaxPowerConsumption).Separator().Label("Idle").PowerFormat(thrust.MinPowerConsumption);
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                // HACK: ConsumptionFactorPerG is NOT per g. Game gives gravity multiplier (g) to method, not acceleration. See MyEntityThrustComponent.RecomputeTypeThrustParameters()
                float consumptionMultiplier = 1f + (thrust.ConsumptionFactorPerG / Hardcoded.EarthGravity);
                //float consumptionMultiplier = 1f + thrust.ConsumptionFactorPerG;

                bool show = true;
                if(consumptionMultiplier > 1)
                    AddLine(FontsHandler.RedSh).Color(COLOR_BAD);
                else if(consumptionMultiplier < 1)
                    AddLine(FontsHandler.RedSh).Color(COLOR_GOOD);
                else
                    show = false;

                if(show)
                    GetLine().Label("Consumption multiplier").Append("x").RoundedNumber(consumptionMultiplier, 2).Append(" per g (natural gravity)");
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Force").ForceFormat(thrust.ForceMagnitude);

                if(Math.Abs(thrust.SlowdownFactor - 1) > 0.001f)
                    GetLine().Separator().Color(COLOR_WARNING).Label("Dampeners").Append("x").RoundedNumber(thrust.SlowdownFactor, 2);

                AddLine().Label("Limits");
                const int PrefixSpaces = 11;

                if(thrust.EffectivenessAtMinInfluence < 1.0f || thrust.EffectivenessAtMaxInfluence < 1.0f)
                {
                    // HACK thrust.NeedsAtmosphereForInfluence seems to be a pointless var because planetary influence is air density.
                    // tested NeedsAtmosphereForInfluence=false with atmos thrusts and they don't work.

                    // renamed to what they actually are for simpler code
                    float minAir = thrust.MinPlanetaryInfluence;
                    float maxAir = thrust.MaxPlanetaryInfluence;
                    float thrustAtMinAir = thrust.EffectivenessAtMinInfluence;
                    float thrustAtMaxAir = thrust.EffectivenessAtMaxInfluence;

                    // flip values if they're in wrong order
                    if(thrust.InvDiffMinMaxPlanetaryInfluence < 0)
                    {
                        minAir = thrust.MaxPlanetaryInfluence;
                        maxAir = thrust.MinPlanetaryInfluence;
                        thrustAtMinAir = thrust.EffectivenessAtMaxInfluence;
                        thrustAtMaxAir = thrust.EffectivenessAtMinInfluence;
                    }

                    // if mod has weird values, can't really present them in an understandable manner so just printing the values instead
                    if(!Hardcoded.Thrust_HasSaneLimits(thrust))
                    {
                        GetLine().Append("Min air density: ").ProportionToPercent(minAir);
                        AddLine().Append(' ', PrefixSpaces).Append("| Max air density: ").ProportionToPercent(maxAir);
                        AddLine().Append(' ', PrefixSpaces).Append("| Thrust at min air: ").ProportionToPercent(thrustAtMinAir);
                        AddLine().Append(' ', PrefixSpaces).Append("| Thrust at max air: ").ProportionToPercent(thrustAtMaxAir);
                        //AddLine().Append(' ', PrefixSpaces).Append("| NeedsAtmosphereForInfluence: ").Append(thrust.NeedsAtmosphereForInfluence);
                    }
                    else
                    {
                        GetLine().Color(thrustAtMaxAir < 1f ? COLOR_BAD : COLOR_GOOD)
                            .ProportionToPercent(thrustAtMaxAir).Append(" thrust ")
                            // no "in atmosphere" because it needs to explicitly state that it expects 100% air density, which some planets do not have (like Mars)
                            .Append("in ").ProportionToPercent(maxAir).Append(" air density");

                        AddLine().Append(' ', PrefixSpaces).Append("| ")
                            .Color(thrustAtMinAir < 1f ? COLOR_BAD : COLOR_GOOD)
                            .ProportionToPercent(thrustAtMinAir).Append(" thrust ");
                        if(minAir <= 0)
                            GetLine().Append("in vacuum.");
                        else
                            GetLine().Append("below ").ProportionToPercent(minAir).Append(" air density");
                    }
                }
                else
                {
                    GetLine().Color(COLOR_GOOD).Append("full thrust in atmosphere and vacuum");
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                BData_Thrust data = Main.LiveDataHandler.Get<BData_Thrust>(def);
                if(data != null)
                {
                    AddLine().Label("Flames").Append(data.Flames.Count)
                        .Separator().Label("Longest").DistanceFormat(data.LongestFlame, 2)
                        .Append(" (").DistanceFormat(data.LongestFlamePastEdge).Append(" past cube edge)");

                    AddLine().Label("Damage to ships").Number(data.DamagePerTickToBlocks * Constants.TicksPerSecond).Append("/s")
                        .Separator().Label("to other").Number(data.DamagePerTickToOther * Constants.TicksPerSecond).Append("/s");
                }

                if(!MyAPIGateway.Session.SessionSettings.ThrusterDamage)
                    AddLine().Color(Color.Green).Append("Thruster damage is disabled in this world");
            }
        }

        private void Format_Gyro(MyCubeBlockDefinition def)
        {
            MyGyroDefinition gyro = (MyGyroDefinition)def;

            PowerRequired(gyro.RequiredPowerInput, gyro.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Force").ForceFormat(gyro.ForceMagnitude);
            }
        }

        private void Format_Light(MyCubeBlockDefinition def)
        {
            MyLightingBlockDefinition light = (MyLightingBlockDefinition)def;

            MyBounds radius = light.LightRadius;

            MyReflectorBlockDefinition spotLight = def as MyReflectorBlockDefinition;
            bool isSpotlight = (spotLight != null);

            if(isSpotlight)
                radius = light.LightReflectorRadius;

            PowerRequired(light.RequiredPowerInput, light.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Append("Radius: ").DistanceFormat(radius.Min).Append(" to ").DistanceFormat(radius.Max).Separator().Append("Default: ").DistanceFormat(radius.Default);
                AddLine().Append("Intensity: ").RoundedNumber(light.LightIntensity.Min, 2).Append(" to ").RoundedNumber(light.LightIntensity.Max, 2).Separator().Append("Default: ").RoundedNumber(light.LightIntensity.Default, 2);
                AddLine().Append("Falloff: ").RoundedNumber(light.LightFalloff.Min, 2).Append(" to ").RoundedNumber(light.LightFalloff.Max, 2).Separator().Append("Default: ").RoundedNumber(light.LightFalloff.Default, 2);

                if(isSpotlight)
                {
                    BData_Spotlight data = Main.LiveDataHandler.Get<BData_Spotlight>(def);
                    if(data != null && data.HasRotatingParts)
                    {
                        float min = spotLight.RotationSpeedBounds.Min * Hardcoded.Spotlight_RadiansPerSecondMul;
                        float max = spotLight.RotationSpeedBounds.Max * Hardcoded.Spotlight_RadiansPerSecondMul;
                        float rotationDefault = spotLight.RotationSpeedBounds.Default * Hardcoded.Spotlight_RadiansPerSecondMul;
                        AddLine().Append("Rotation speed: ").RotationSpeed(min, 0).Append(" to ").RotationSpeed(max, 0).Separator().Append("Default: ").RotationSpeed(rotationDefault, 0);
                    }
                }
            }
        }

        private void Format_OreDetector(MyCubeBlockDefinition def)
        {
            MyOreDetectorDefinition oreDetector = (MyOreDetectorDefinition)def;

            PowerRequired(Hardcoded.OreDetector_PowerReq, oreDetector.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max range").DistanceFormat(oreDetector.MaximumRange);
            }
        }

        private void Format_Projector(MyCubeBlockDefinition def)
        {
            MyProjectorDefinition projector = (MyProjectorDefinition)def;

            PowerRequired(projector.RequiredPowerInput, projector.ResourceSinkGroup);

            StringBuilder sb = AddLine().Label("Projector mode");

            if(Hardcoded.Projector_AllowWelding(projector))
            {
                sb.Append("Building (no rescale, always same grid size)");
            }
            else
            {
                sb.Append("Preview").Separator().Label("Allows sizes");
                if(projector.IgnoreSize)
                    sb.Color(COLOR_GOOD).Append("both");
                else
                    sb.Color(COLOR_WARNING).Append(def.CubeSize == MyCubeSize.Large ? "large" : "small").Append(" grid");
            }
        }

        #region Doors
        private void Format_Door(MyCubeBlockDefinition def)
        {
            MyDoorDefinition door = (MyDoorDefinition)def;

            PowerRequired(Hardcoded.Door_PowerReq, door.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float moveTime = Hardcoded.Door_MoveSpeed(door.OpeningSpeed, door.MaxOpen);
                AddLine().Label("Move time").TimeFormat(moveTime).Separator().Label("Distance").DistanceFormat(door.MaxOpen);
            }
        }

        private void Format_AirtightDoor(MyCubeBlockDefinition def)
        {
            MyAirtightDoorGenericDefinition airTightDoor = (MyAirtightDoorGenericDefinition)def; // does not extend MyDoorDefinition

            // MyAirtightHangarDoorDefinition and MyAirtightSlideDoorDefinition are empty

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power").PowerFormat(airTightDoor.PowerConsumptionMoving).Separator().Label("Idle").PowerFormat(airTightDoor.PowerConsumptionIdle);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(airTightDoor.ResourceSinkGroup);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float moveTime = Hardcoded.Door_MoveSpeed(airTightDoor.OpeningSpeed);
                AddLine().Label("Move time").TimeFormat(moveTime);
            }
        }

        private void Format_AdvancedDoor(MyCubeBlockDefinition def)
        {
            MyAdvancedDoorDefinition advDoor = (MyAdvancedDoorDefinition)def; // does not extend MyDoorDefinition

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Moving").PowerFormat(advDoor.PowerConsumptionMoving).Separator().Label("Idle").PowerFormat(advDoor.PowerConsumptionIdle);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(advDoor.ResourceSinkGroup);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float openTime, closeTime;
                Hardcoded.AdvDoor_MoveSpeed(advDoor, out openTime, out closeTime);

                AddLine().Label("Move time - Opening").TimeFormat(openTime).Separator().Label("Closing").TimeFormat(closeTime);
            }
        }
        #endregion Doors

        private void Format_Ladder(MyCubeBlockDefinition def)
        {
            PowerRequired(0, null, powerHardcoded: true);

            BData_Ladder data = Main.LiveDataHandler.Get<BData_Ladder>(def);
            if(data != null)
            {
                float climbSpeed = Hardcoded.LadderClimbSpeed(data.DistanceBetweenPoles);
                AddLine().Label("Climb speed").SpeedFormat(climbSpeed);
            }
        }

        private void Format_Parachute(MyCubeBlockDefinition def)
        {
            MyParachuteDefinition parachute = (MyParachuteDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Deploy").PowerFormat(parachute.PowerConsumptionMoving).Separator().Label("Idle").PowerFormat(parachute.PowerConsumptionIdle);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(parachute.ResourceSinkGroup);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo) || Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
            {
                const float TARGET_DESCEND_VELOCITY = 10;
                float maxMass, disreefAtmosphere;
                Hardcoded.Parachute_GetLoadEstimate(parachute, TARGET_DESCEND_VELOCITY, out maxMass, out disreefAtmosphere);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
                {
                    AddLine().Label("Required item to deploy").Append(parachute.MaterialDeployCost).Append("x ").ItemName(parachute.MaterialDefinitionId);
                }

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine().Label("Required atmosphere - Minimum").Number(parachute.MinimumAtmosphereLevel).Separator().Label("Fully open").Number(disreefAtmosphere);
                    AddLine().Label("Drag coefficient").Append(parachute.DragCoefficient.ToString("0.0####"));
                    AddLine().Label("Load estimate").Color(COLOR_INFO).MassFormat(maxMass).ResetFormatting().Append(" falling at ").SpeedFormat(TARGET_DESCEND_VELOCITY).Append(" in 1g and 1.0 air density.");
                }
            }
        }

        private void Format_MedicalRoom(MyCubeBlockDefinition def)
        {
            MyMedicalRoomDefinition medicalRoom = (MyMedicalRoomDefinition)def;

            PowerRequired(Hardcoded.MedicalRoom_PowerReq, medicalRoom.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(medicalRoom.RespawnAllowed)
                {
                    AddLine().Label("Respawn");

                    if(medicalRoom.ForceSuitChangeOnRespawn)
                    {
                        GetLine().Append("Yes").Separator().Label("Forced suit");

                        if(string.IsNullOrEmpty(medicalRoom.RespawnSuitName))
                        {
                            GetLine().Color(COLOR_BAD).Append("(Error: empty)").ResetFormatting();
                        }
                        else
                        {
                            MyCharacterDefinition charDef;
                            if(MyDefinitionManager.Static.Characters.TryGetValue(medicalRoom.RespawnSuitName, out charDef))
                                GetLine().Append(charDef.Name).ResetFormatting();
                            else
                                GetLine().Append(medicalRoom.RespawnSuitName).Color(COLOR_BAD).Append(" (Error: not found)").ResetFormatting();
                        }
                    }
                    else
                        GetLine().Append("Yes");
                }
                else
                {
                    AddLine(FontsHandler.RedSh).Color(COLOR_WARNING).Label("Respawn").Append("No");
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                if(medicalRoom.HealingAllowed)
                    AddLine().Label("Healing").RoundedNumber(Math.Abs(MyEffectConstants.MedRoomHeal * 60), 2).Append("hp/s");
                else
                    AddLine(FontsHandler.RedSh).Color(COLOR_WARNING).Label("Healing").Append("No").ResetFormatting();

                if(medicalRoom.RefuelAllowed)
                    AddLine().LabelHardcoded("Refuel").Append("Yes (x5)");
                else
                    AddLine(FontsHandler.RedSh).Color(COLOR_WARNING).LabelHardcoded("Refuel").Append("No").ResetFormatting();
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(medicalRoom.SuitChangeAllowed)
                {
                    AddLine().Label("Suit Change");

                    if(medicalRoom.CustomWardrobesEnabled && medicalRoom.CustomWardrobeNames != null && medicalRoom.CustomWardrobeNames.Count > 0)
                    {
                        foreach(string charName in medicalRoom.CustomWardrobeNames)
                        {
                            MyCharacterDefinition charDef;
                            if(!MyDefinitionManager.Static.Characters.TryGetValue(charName, out charDef))
                                AddLine(FontsHandler.RedSh).Append("    ").Append(charName).Color(COLOR_BAD).Append(" (not found in definitions)");
                            else
                                AddLine().Append("    ").Append(charDef.DisplayNameText);
                        }
                    }
                    else
                        GetLine().Append("(all)");
                }
                else
                    AddLine(FontsHandler.RedSh).Color(COLOR_WARNING).Label("Suit Change").Append("No").ResetFormatting();
            }
        }

        #region Production
        private void Format_Production(MyCubeBlockDefinition def)
        {
            MyProductionBlockDefinition production = (MyProductionBlockDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Append("Power: ").PowerFormat(production.OperationalPowerConsumption).Separator().Append("Idle: ").PowerFormat(production.StandbyPowerConsumption);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(production.ResourceSinkGroup);
            }

            MyAssemblerDefinition assembler = def as MyAssemblerDefinition;
            if(assembler != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    float mulSpeed = MyAPIGateway.Session.AssemblerSpeedMultiplier;
                    float mulEff = MyAPIGateway.Session.AssemblerEfficiencyMultiplier;

                    AddLine().Append("Assembly speed: ").ProportionToPercent(assembler.AssemblySpeed * mulSpeed).Color(COLOR_UNIMPORTANT).OptionalMultiplier(mulSpeed).ResetFormatting().Separator().Append("Efficiency: ").ProportionToPercent(mulEff).OptionalMultiplier(mulEff);
                }
            }

            MySurvivalKitDefinition survivalKit = def as MySurvivalKitDefinition; // this extends MyAssemblerDefinition
            if(survivalKit != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    AddLine().Label("Healing").RoundedNumber(Math.Abs(MyEffectConstants.GenericHeal * 60), 2).Append("hp/s");
                    AddLine().LabelHardcoded("Refuel").Append("Yes (x1)");
                }
            }

            MyRefineryDefinition refinery = def as MyRefineryDefinition;
            if(refinery != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    float mul = MyAPIGateway.Session.RefinerySpeedMultiplier;

                    AddLine().Append("Refine speed: ").ProportionToPercent(refinery.RefineSpeed * mul).Color(COLOR_UNIMPORTANT).OptionalMultiplier(mul).ResetFormatting().Separator().Append("Efficiency: ").ProportionToPercent(refinery.MaterialEfficiency);
                }
            }

            MyGasTankDefinition gasTank = def as MyGasTankDefinition;
            if(gasTank != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    AddLine().Append("Stores: ").Append(gasTank.StoredGasId.SubtypeName).Separator().Append("Capacity: ").VolumeFormat(gasTank.Capacity);
                }

                if(gasTank.LeakPercent != 0)
                {
                    float ratioPerSec = (gasTank.LeakPercent / 100f) * 60f; // HACK: LeakPercent gets subtracted every 100 ticks

                    if(ratioPerSec > 0)
                        AddLine().Color(COLOR_WARNING).Label("Damaged Leak").Append("-").VolumeFormat(ratioPerSec * gasTank.Capacity).Append("/s"); //.Append(" (").ExponentNumber(leakRatioPerSec).Append("%/s)");
                    else
                        AddLine().Color(COLOR_GOOD).Label("Damaged Leak... Magic-Gain").Append("+").VolumeFormat(Math.Abs(ratioPerSec) * gasTank.Capacity).Append("/s");
                }
                else
                {
                    AddLine().Label("Damaged Leak").Color(COLOR_GOOD).Append("Leak-proof");
                }

                if(gasTank.GasExplosionDamageMultiplier > 0)
                {
                    // HACK: from MyGasTank.CalculateGasExplosionRadius() and CalculateGasExplosionDamage()
                    AddLine().Color(COLOR_WARNING).Label("Destroyed Explosion - Max Damage").ExponentNumber(gasTank.GasExplosionDamageMultiplier * gasTank.Capacity)
                        .Separator().Label("Max Radius").DistanceFormat(gasTank.GasExplosionMaxRadius);
                }
                else
                {
                    AddLine().Label("Destroyed Explosion").Color(COLOR_GOOD).Append("Inexistent");
                }
            }

            MyOxygenGeneratorDefinition gasGenerator = def as MyOxygenGeneratorDefinition;
            if(gasGenerator != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    {
                        StringBuilder sb = AddLine().Label("Consumes");
                        int startLen = sb.Length;

                        if(gasGenerator.InputInventoryConstraint != null)
                        {
                            foreach(MyDefinitionId id in gasGenerator.InputInventoryConstraint.ConstrainedIds)
                            {
                                if(id.TypeId == typeof(MyObjectBuilder_OxygenContainerObject) || id.TypeId == typeof(MyObjectBuilder_GasContainerObject))
                                    continue;

                                sb.ItemName(id).Append(", ");
                            }

                            foreach(MyObjectBuilderType type in gasGenerator.InputInventoryConstraint.ConstrainedTypes)
                            {
                                if(type == typeof(MyObjectBuilder_OxygenContainerObject) || type == typeof(MyObjectBuilder_GasContainerObject))
                                    continue;

                                sb.IdTypeFormat(type).Append(", ");
                            }

                            if(sb.Length > startLen)
                                sb.Length -= 2; // remove last comma
                        }
                        else
                        {
                            sb.Append("(unknown)");
                        }
                    }

                    AddLine().Label("Consumption per active output").Number(gasGenerator.IceConsumptionPerSecond).Append("/s");

                    if(gasGenerator.ProducedGases != null && gasGenerator.ProducedGases.Count > 0)
                    {
                        StringBuilder sb = AddLine().Label("Produces");

                        foreach(MyOxygenGeneratorDefinition.MyGasGeneratorResourceInfo gas in gasGenerator.ProducedGases)
                        {
                            sb.Append(gas.Id.SubtypeName).Append(" (").VolumeFormat(gasGenerator.IceConsumptionPerSecond * gas.IceToGasRatio).Append("/s), ");
                        }

                        sb.Length -= 2; // remove last comma
                    }
                    else
                    {
                        AddLine(FontsHandler.RedSh).Append("Produces: <N/A>");
                    }
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                float volume = (production.InventoryMaxVolume > 0 ? production.InventoryMaxVolume : production.InventorySize.Volume);

                MyInventoryComponentDefinition invComp = Utils.GetInventoryFromComponent(def);

                if(refinery != null || assembler != null)
                {
                    AddLine().Append("In+out inventories: ").InventoryFormat(volume * 2, production.InputInventoryConstraint, production.OutputInventoryConstraint, invComp);
                }
                else
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume, production.InputInventoryConstraint, invComp);
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production) && production.BlueprintClasses != null)
            {
                if(gasGenerator != null || gasTank != null)
                {
                    StringBuilder sb = AddLine().Label("Refills");
                    int startLen = sb.Length;

                    if(production.InputInventoryConstraint != null)
                    {
                        foreach(MyDefinitionId id in production.InputInventoryConstraint.ConstrainedIds)
                        {
                            if(id.TypeId == typeof(MyObjectBuilder_OxygenContainerObject) || id.TypeId == typeof(MyObjectBuilder_GasContainerObject))
                            {
                                sb.ItemName(id).Append(", ");
                            }
                        }

                        foreach(MyObjectBuilderType type in production.InputInventoryConstraint.ConstrainedTypes)
                        {
                            if(type == typeof(MyObjectBuilder_OxygenContainerObject) || type == typeof(MyObjectBuilder_GasContainerObject))
                            {
                                sb.IdTypeFormat(type).Append(", ");
                            }
                        }

                        if(sb.Length > startLen)
                            sb.Length -= 2; // remove last comma
                    }
                    else
                    {
                        sb.Append("(unknown)");
                    }
                }
                else
                {
                    StringBuilder sb = AddLine();

                    int SpacePadding = 11;

                    if(refinery != null)
                    {
                        sb.Label("Refines");
                        SpacePadding = 13;
                    }
                    else if(assembler != null)
                    {
                        sb.Label("Builds");
                        SpacePadding = 11;
                    }
                    //else if(gasTank != null)
                    //{
                    //    sb.Label("Refills");
                    //    SpacePadding = 11;
                    //}
                    //else if(gasGenerator != null)
                    //{
                    //    sb.Label("Uses/Refills");
                    //    SpacePadding = 22;
                    //}
                    else
                    {
                        sb.Label("Blueprints");
                        SpacePadding = 17;
                    }

                    if(production.BlueprintClasses.Count == 0)
                    {
                        sb.Color(COLOR_WARNING).Append("N/A");
                    }
                    else
                    {
                        const int NumPerRow = 3;

                        for(int i = 0; i < production.BlueprintClasses.Count; i++)
                        {
                            MyBlueprintClassDefinition bp = production.BlueprintClasses[i];

                            if(i > 0)
                            {
                                if(i % NumPerRow == 0)
                                    sb = AddLine().Color(COLOR_LIST).Append(' ', SpacePadding).Append("| ").ResetFormatting();
                                else
                                    sb.Separator();
                            }

                            // not using DisplayNameText because some are really badly named, like BasicIngots -> Ingots; also can contain newlines.
                            //string name = bp.DisplayNameText;
                            //int newLineIndex = name.IndexOf('\n');
                            //
                            //if(newLineIndex != -1) // name contains a new line, ignore everything after that
                            //{
                            //    for(int ci = 0; ci < newLineIndex; ++ci)
                            //    {
                            //        sb.Append(name[ci]);
                            //    }
                            //
                            //    sb.TrimEndWhitespace();
                            //}
                            //else
                            //{
                            //    sb.Append(name);
                            //}

                            string name = bp.Id.SubtypeName;
                            sb.Append(name);
                        }
                    }
                }
            }
        }

        private void Format_OxygenFarm(MyCubeBlockDefinition def)
        {
            MyOxygenFarmDefinition oxygenFarm = (MyOxygenFarmDefinition)def; // does not extend MyProductionBlockDefinition

            PowerRequired(oxygenFarm.OperationalPowerConsumption, oxygenFarm.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Produces").RoundedNumber(oxygenFarm.MaxGasOutput, 2).Append(" ").Append(oxygenFarm.ProducedGas.SubtypeName).Append(" l/s");

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(oxygenFarm.ResourceSourceGroup, isSource: true);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine(oxygenFarm.IsTwoSided ? FontsHandler.WhiteSh : FontsHandler.YellowSh).Append(oxygenFarm.IsTwoSided ? "Two-sided" : "One-sided");
            }
        }

        private void Format_AirVent(MyCubeBlockDefinition def)
        {
            MyAirVentDefinition vent = (MyAirVentDefinition)def; // does not extend MyProductionBlockDefinition

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Idle").PowerFormat(vent.StandbyPowerConsumption).Separator().Label("Operational").PowerFormat(vent.OperationalPowerConsumption);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(vent.ResourceSinkGroup);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Output - Rate").VolumeFormat(vent.VentilationCapacityPerSecond).Append("/s");

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(vent.ResourceSourceGroup, isSource: true);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings))
            {
                if(!MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization)
                    AddLine().Color(Color.Red).Append("Airtightness is disabled in this world");
                else if(!MyAPIGateway.Session.SessionSettings.EnableOxygen)
                    AddLine().Color(Color.Red).Append("Oxygen is disabled in this world");
            }
        }

        private void Format_UpgradeModule(MyCubeBlockDefinition def)
        {
            MyUpgradeModuleDefinition upgradeModule = (MyUpgradeModuleDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                BData_Base data = Main.LiveDataHandler.Get<BData_Base>(def);
                if(data != null)
                {
                    if(upgradeModule.Upgrades == null || upgradeModule.Upgrades.Length == 0)
                    {
                        AddLine(FontsHandler.RedSh).Color(COLOR_WARNING).Append("Upgrades: (Unknown)");
                    }
                    else
                    {
                        int ports = (data.UpgradePorts?.Count ?? 0);

                        if(ports == 0)
                            AddLine(FontsHandler.RedSh).Color(COLOR_BAD).Label("Upgrade ports").Append(0).ResetFormatting();
                        else
                            AddLine().Label("Upgrade ports").Append(ports);

                        bool multiple = upgradeModule.Upgrades.Length > 1;

                        AddLine().Label(multiple ? "Effects per port" : "Effect per port").AppendUpgrade(upgradeModule.Upgrades[0]);

                        for(int i = 1; i < upgradeModule.Upgrades.Length; i++)
                        {
                            AddLine().Color(COLOR_LIST).Append("                          | ").ResetFormatting().AppendUpgrade(upgradeModule.Upgrades[i]);
                        }
                    }
                }
            }
        }

        private void Format_PowerProducer(MyCubeBlockDefinition def)
        {
            MyPowerProducerDefinition powerProducer = (MyPowerProducerDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Append("Power output: ").PowerFormat(powerProducer.MaxPowerOutput);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(powerProducer.ResourceSourceGroup, isSource: true);
            }

            MyHydrogenEngineDefinition h2Engine = def as MyHydrogenEngineDefinition;
            if(h2Engine != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
                {
                    AddLine().Label("Needs fuel").ItemName(h2Engine.Fuel.FuelId);

                    // HACK: hardcoded h2 consumption rate
                    AddLine().Label("Consumption").VolumeFormat(h2Engine.MaxPowerOutput / h2Engine.FuelProductionToCapacityMultiplier).Append("/s");

                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(h2Engine.ResourceSinkGroup);

                    AddLine().Label("Fuel capacity").VolumeFormat(h2Engine.FuelCapacity);
                }

                return;
            }

            MyReactorDefinition reactor = def as MyReactorDefinition;
            if(reactor != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
                {
                    if(reactor.FuelInfos != null && reactor.FuelInfos.Length > 0)
                    {
                        bool hasOneFuel = (reactor.FuelInfos.Length == 1);

                        if(hasOneFuel)
                            AddLine().Append("Needs fuel: ");
                        else
                            AddLine().Color(COLOR_WARNING).Append("Needs combined fuels:").ResetFormatting();

                        foreach(MyReactorDefinition.FuelInfo fuel in reactor.FuelInfos)
                        {
                            if(!hasOneFuel)
                                AddLine().Append("       - ");

                            GetLine().ItemName(fuel.FuelId).Append(" (").RoundedNumber(fuel.ConsumptionPerSecond_Items, 5).Append("/s)");
                        }
                    }
                }

                float volume = (reactor.InventoryMaxVolume > 0 ? reactor.InventoryMaxVolume : reactor.InventorySize.Volume);
                InventoryStats(def, alternateVolume: volume);
                return;
            }

            MyBatteryBlockDefinition battery = def as MyBatteryBlockDefinition;
            if(battery != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
                {
                    AddLine(battery.AdaptibleInput ? FontsHandler.WhiteSh : FontsHandler.YellowSh).Append("Power input: ").PowerFormat(battery.RequiredPowerInput).Append(battery.AdaptibleInput ? " (adaptable)" : " (minimum required)");

                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(battery.ResourceSinkGroup);
                }

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
                {
                    AddLine().Append("Power capacity: ").PowerStorageFormat(battery.MaxStoredPower).Separator().Append("Pre-charged: ").PowerStorageFormat(battery.MaxStoredPower * battery.InitialStoredPowerRatio).Append(" (").ProportionToPercent(battery.InitialStoredPowerRatio).Append(')');
                }

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    float megawatts = (battery.MaxStoredPower * 60 * 60);
                    float chargeTime = megawatts / (battery.RequiredPowerInput * battery.RechargeMultiplier);
                    float dischargeTime = megawatts / battery.MaxPowerOutput;

                    StringBuilder sb = AddLine().Label("Recharge time").TimeFormat(chargeTime).Separator();

                    if(battery.RechargeMultiplier <= 1f)
                        sb.Color(battery.RechargeMultiplier < 1 ? COLOR_BAD : COLOR_GOOD).Label("Loss").ProportionToPercent(1f - battery.RechargeMultiplier);
                    else
                        sb.Color(COLOR_GOOD).Label("Multiplier").MultiplierToPercent(battery.RechargeMultiplier);

                    AddLine().Label("Discharge time").TimeFormat(dischargeTime);
                }

                return;
            }

            MySolarPanelDefinition solarPanel = def as MySolarPanelDefinition;
            if(solarPanel != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine(solarPanel.IsTwoSided ? FontsHandler.WhiteSh : FontsHandler.YellowSh).Append(solarPanel.IsTwoSided ? "Two-sided" : "One-sided");
                }

                return;
            }

            MyWindTurbineDefinition windTurbine = def as MyWindTurbineDefinition;
            if(windTurbine != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    float groundMin = windTurbine.OptimalGroundClearance * windTurbine.MinRaycasterClearance;
                    float groundMax = windTurbine.OptimalGroundClearance;

                    float sideMin = windTurbine.RaycasterSize * windTurbine.MinRaycasterClearance;
                    float sideMax = windTurbine.RaycasterSize;

                    AddLine().Label("Clearence - Terrain").DistanceRangeFormat(groundMin, groundMax).Separator().Label("Sides").DistanceRangeFormat(sideMin, sideMax);

                    AddLine().Label("Optimal wind speed").RoundedNumber(windTurbine.OptimalWindSpeed, 2);
                    // TODO: wind speed unit? noone knows...
                }

                return;
            }
        }
        #endregion Production

        #region Communication
        private void Format_RadioAntenna(MyCubeBlockDefinition def)
        {
            MyRadioAntennaDefinition radioAntenna = (MyRadioAntennaDefinition)def;

            PowerRequired(Hardcoded.RadioAntenna_PowerReq(radioAntenna.MaxBroadcastRadius), radioAntenna.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max radius").DistanceFormat(radioAntenna.MaxBroadcastRadius);
            }

            // TODO: lightning catch area? also for decoy blocks...
        }

        private void Format_LaserAntenna(MyCubeBlockDefinition def)
        {
            MyLaserAntennaDefinition laserAntenna = (MyLaserAntennaDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Active").PowerFormat(Hardcoded.LaserAntenna_PowerUsage(laserAntenna, 1000)).Append(" per km");

                SimpleTooltip("Laser antenna power usage is linear up to 200km, after that it's a quadratic ecuation."
                            + "\nTo calculate it at your needed distance, hold a laser antenna block and type in chat: <color=0,255,155>/bi laserpower <km>");

                AddLine().Label("Power - Turning").PowerFormat(laserAntenna.PowerInputTurning).Separator().Label("Idle").PowerFormat(laserAntenna.PowerInputIdle);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(laserAntenna.ResourceSinkGroup);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine(laserAntenna.RequireLineOfSight ? FontsHandler.YellowSh : FontsHandler.GreenSh)
                    .Color(laserAntenna.MaxRange < 0 ? COLOR_GOOD : COLOR_NORMAL).Append("Range: ");

                if(laserAntenna.MaxRange < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat(laserAntenna.MaxRange);

                GetLine().ResetFormatting().Separator().Color(laserAntenna.RequireLineOfSight ? COLOR_WARNING : COLOR_GOOD).Append("Line-of-sight: ").Append(laserAntenna.RequireLineOfSight ? "Required" : "Not required");

                int minPitch = Math.Max(laserAntenna.MinElevationDegrees, -90);
                int maxPitch = Math.Min(laserAntenna.MaxElevationDegrees, 90);

                int minYaw = laserAntenna.MinAzimuthDegrees;
                int maxYaw = laserAntenna.MaxAzimuthDegrees;

                AddLine().Append("Rotation - ");

                if(minPitch == -90 && maxPitch >= 90)
                    GetLine().Color(COLOR_GOOD).Append("Pitch: ").AngleFormatDeg(minPitch).Append(" to ").AngleFormatDeg(maxPitch);
                else
                    GetLine().Color(COLOR_WARNING).Append("Pitch: ").AngleFormatDeg(minPitch).Append(" to ").AngleFormatDeg(maxPitch);

                GetLine().Separator();

                if(minYaw <= -180 && maxYaw >= 180)
                    GetLine().Color(COLOR_GOOD).Append("Yaw: ").AngleFormatDeg(360);
                else
                    GetLine().Color(COLOR_WARNING).Append("Yaw: ").AngleFormatDeg(minYaw).Append(" to ").AngleFormatDeg(maxYaw);

                AddLine().Label("Rotation Speed").RotationSpeed(laserAntenna.RotationRate * Hardcoded.LaserAntenna_RotationSpeedMul);
            }
        }

        private void Format_Beacon(MyCubeBlockDefinition def)
        {
            MyBeaconDefinition beacon = (MyBeaconDefinition)def;

            PowerRequired(Hardcoded.Beacon_PowerReq(beacon), beacon.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max radius").DistanceFormat(beacon.MaxBroadcastRadius);
            }
        }
        #endregion Communication

        private void Format_Timer(MyCubeBlockDefinition def)
        {
            MyTimerBlockDefinition timer = (MyTimerBlockDefinition)def;

            PowerRequired(Hardcoded.Timer_PowerReq, timer.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Timer range").TimeFormat(timer.MinDelay / 1000f).Append(" to ").TimeFormat(timer.MaxDelay / 1000f);
            }
        }

        private void Format_ProgrammableBlock(MyCubeBlockDefinition def)
        {
            MyProgrammableBlockDefinition pb = (MyProgrammableBlockDefinition)def;

            PowerRequired(Hardcoded.ProgrammableBlock_PowerReq, pb.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings))
            {
                if(!MyAPIGateway.Session.SessionSettings.EnableIngameScripts)
                {
                    AddLine().Color(Color.Red).Append("In-game Scripts are disabled in this world");
                }
                else if(MyAPIGateway.Session.SessionSettings.EnableScripterRole && MyAPIGateway.Session?.Player != null && MyAPIGateway.Session.Player.PromoteLevel < MyPromoteLevel.Scripter)
                {
                    AddLine().Color(Color.Red).Append("Scripter role required to use In-game Scripts");
                }
            }
        }

        private void Format_LCD(MyCubeBlockDefinition def)
        {
            MyTextPanelDefinition lcd = (MyTextPanelDefinition)def;

            PowerRequired(lcd.RequiredPowerInput, lcd.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                Hardcoded.TextSurfaceInfo info;
                string script = null;
                bool supportsRotation = false;

                // HACK: LCD block has rotations under these conditions, otherwise it just uses the old data from the definition
                if(lcd.ScreenAreas != null && lcd.ScreenAreas.Count == 4)
                {
                    supportsRotation = true;
                    ScreenArea surface = lcd.ScreenAreas[0];
                    info = Hardcoded.TextSurface_GetInfo(surface.ScreenWidth, surface.ScreenHeight, surface.TextureResolution);
                    script = surface.Script;
                }
                else
                {
                    info = Hardcoded.TextSurface_GetInfo(lcd.ScreenWidth, lcd.ScreenHeight, lcd.TextureResolution);
                }

                AddLine().Label("LCD - Resolution").Color(COLOR_HIGHLIGHT).Number(info.SurfaceSize.X).Append("x").Number(info.SurfaceSize.Y).ResetFormatting()
                    .Separator().Label("Rotatable").BoolFormat(supportsRotation)
                    .Separator().Label("Font size limits").RoundedNumber(lcd.MinFontSize, 4).Append(" to ").RoundedNumber(lcd.MaxFontSize, 4);

                AddLine().LabelHardcoded("LCD - Render").DistanceFormat(Hardcoded.TextSurfaceMaxRenderDistance).Separator().LabelHardcoded("Sync").DistanceFormat(Hardcoded.TextSurfaceMaxSyncDistance);

                // not that useful info
                //if(!string.IsNullOrEmpty(script))
                //    AddLine().Label("Default script").Color(COLOR_STAT_TRAVEL).Append(script).ResetFormatting();
            }
        }

        void AddScreenInfo(MyFunctionalBlockDefinition fbDef)
        {
            if(fbDef is MyTextPanelDefinition)
                return; // the TextPanel formatter has its own separate stuff

            List<ScreenArea> surfaces = fbDef.ScreenAreas;
            if(surfaces == null || surfaces.Count == 0 || !Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                return;

            const int SpacePrefix = 9;
            AddLine().Label(surfaces.Count > 1 ? "LCDs" : "LCD");

            // TODO: toggle between list and just count?
            for(int i = 0; i < surfaces.Count; i++)
            {
                ScreenArea surface = surfaces[i];

                string displayName = MyTexts.GetString(MyStringId.GetOrCompute(surface.DisplayName));

                Hardcoded.TextSurfaceInfo info = Hardcoded.TextSurface_GetInfo(surface.ScreenWidth, surface.ScreenHeight, surface.TextureResolution);

                if(i > 0)
                    AddLine().Append(' ', SpacePrefix).Append("| ");

                GetLine().Append(displayName);

                // very edge case use for a lot of width added, who needs it can get it from API or SBC
                //if(Main.Config.InternalInfo.Value)
                //    GetLine().Color(COLOR_UNIMPORTANT).Append(" (").Append(surface.Name).Append(")");

                GetLine().ResetFormatting().Separator().Color(COLOR_HIGHLIGHT).Number(info.SurfaceSize.X).Append("x").Number(info.SurfaceSize.Y).ResetFormatting();

                // not that useful info
                //if(!string.IsNullOrEmpty(surface.Script))
                //    GetLine().Separator().Color(COLOR_STAT_TRAVEL).Label("Default script").Append(surface.Script).ResetFormatting();
            }

            AddLine().LabelHardcoded("LCD - Render").DistanceFormat(Hardcoded.TextSurfaceMaxRenderDistance).Separator().LabelHardcoded("Sync").DistanceFormat(Hardcoded.TextSurfaceMaxSyncDistance);
        }

        private void Format_LCDPanels(MyCubeBlockDefinition def)
        {
            MyLCDPanelsBlockDefinition panel = (MyLCDPanelsBlockDefinition)def;

            PowerRequired(panel.RequiredPowerInput, panel.ResourceSinkGroup);

            // LCD stats are in AddScreenInfo()
        }

        private void Format_SoundBlock(MyCubeBlockDefinition def)
        {
            // NOTE: this includes jukebox
            MySoundBlockDefinition sound = (MySoundBlockDefinition)def;

            PowerRequired(Hardcoded.SoundBlock_PowerReq, sound.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Range").DistanceRangeFormat(sound.MinRange, sound.MaxRange);
                AddLine().Label("Max loop time").TimeFormat(sound.MaxLoopPeriod);

                // EmitterNumber and LoopUpdateThreshold seem unused
            }

            //MyJukeboxDefinition jukebox = def as MyJukeboxDefinition;
            //if(jukebox != null)
            //{
            //}
        }

        private void Format_Sensor(MyCubeBlockDefinition def)
        {
            MySensorBlockDefinition sensor = (MySensorBlockDefinition)def;

            Vector3 minField = Hardcoded.Sensor_MinField;
            Vector3 maxField = Hardcoded.Sensor_MaxField(sensor.MaxRange);

            PowerRequired(Hardcoded.Sensor_PowerReq(maxField), sensor.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Field size").Size3DFormat(minField).Append(" m to ").Size3DFormat(maxField).Append(" m");
            }
        }

        private void Format_Camera(MyCubeBlockDefinition def)
        {
            MyCameraBlockDefinition camera = (MyCameraBlockDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Normal use").PowerFormat(camera.RequiredPowerInput).Separator().Label("Raycast charging").PowerFormat(camera.RequiredChargingInput);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(camera.ResourceSinkGroup);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Field of view").AngleFormat(camera.MinFov).Append(" to ").AngleFormat(camera.MaxFov);
                AddLine().Label("Raycast - Cone limit").AngleFormatDeg(camera.RaycastConeLimit).Separator().Label("Distance limit");

                if(camera.RaycastDistanceLimit < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat((float)camera.RaycastDistanceLimit);

                GetLine().Separator().Label("Time multiplier").RoundedNumber(camera.RaycastTimeMultiplier, 2);

                SimpleTooltip("Programmable Block can use Camera blocks to rangefind objects using physics raycast."
                            + "\nFor more info see the PB API (on MDK wiki for example).");
            }
        }

        private void Format_Button(MyCubeBlockDefinition def)
        {
            MyButtonPanelDefinition button = (MyButtonPanelDefinition)def;

            PowerRequired(Hardcoded.ButtonPanel_PowerReq, button.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Button count").Append(button.ButtonCount);
            }
        }

        #region Magic blocks
        private void Format_GravityGenerator(MyCubeBlockDefinition def)
        {
            MyGravityGeneratorBaseDefinition gravGen = (MyGravityGeneratorBaseDefinition)def;

            MyGravityGeneratorDefinition flatGravGen = def as MyGravityGeneratorDefinition;
            if(flatGravGen != null)
            {
                PowerRequired(flatGravGen.RequiredPowerInput, flatGravGen.ResourceSinkGroup);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    AddLine().Label("Field size").Size3DFormat(flatGravGen.MinFieldSize).Append(" m to ").Size3DFormat(flatGravGen.MaxFieldSize).Append(" m");
                }
            }
            else
            {
                MyGravityGeneratorSphereDefinition sphereGravGen = def as MyGravityGeneratorSphereDefinition;
                if(sphereGravGen != null)
                {
                    PowerRequired(Hardcoded.SphericalGravGen_PowerReq(sphereGravGen, sphereGravGen.MaxRadius, sphereGravGen.MaxGravityAcceleration), sphereGravGen.ResourceSinkGroup, powerHardcoded: true);

                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                    {
                        AddLine().Label("Radius").DistanceFormat(sphereGravGen.MinRadius).Append(" to ").DistanceFormat(sphereGravGen.MaxRadius);
                    }
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Acceleration").ForceFormat(gravGen.MinGravityAcceleration).Append(" to ").ForceFormat(gravGen.MaxGravityAcceleration);
            }
        }

        private void Format_ArtificialMass(MyCubeBlockDefinition def)
        {
            MyVirtualMassDefinition artificialMass = (MyVirtualMassDefinition)def;

            PowerRequired(artificialMass.RequiredPowerInput, artificialMass.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Artificial mass").MassFormat(artificialMass.VirtualMass);
            }
        }

        private void Format_SpaceBall(MyCubeBlockDefinition def)
        {
            MySpaceBallDefinition spaceBall = (MySpaceBallDefinition)def; // this doesn't extend MyVirtualMassDefinition

            // HACK: hardcoded; SpaceBall doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Max artificial mass").MassFormat(spaceBall.MaxVirtualMass);
            }
        }

        private void Format_JumpDrive(MyCubeBlockDefinition def)
        {
            MyJumpDriveDefinition jumpDrive = (MyJumpDriveDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                StringBuilder sb = AddLine().Label("Power to charge").PowerFormat(jumpDrive.RequiredPowerInput);
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    sb.ResetFormatting().Separator().ResourcePriority(jumpDrive.ResourceSinkGroup);

                AddLine().Label("Capacity for Jump").PowerStorageFormat(jumpDrive.PowerNeededForJump);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float chargeTime = (jumpDrive.PowerNeededForJump * 60 * 60) / (jumpDrive.RequiredPowerInput * jumpDrive.PowerEfficiency);
                StringBuilder sb = AddLine().Label("Charge - Time").TimeFormat(chargeTime).Separator();

                float rechargeMultiplier = jumpDrive.PowerEfficiency;
                if(rechargeMultiplier <= 1f)
                    sb.Color(rechargeMultiplier < 1 ? COLOR_BAD : COLOR_GOOD).Label("Loss").ProportionToPercent(1f - rechargeMultiplier);
                else
                    sb.Color(COLOR_GOOD).Label("Multiplier").MultiplierToPercent(rechargeMultiplier);

                AddLine().LabelHardcoded("Jump process").TimeFormat(Hardcoded.JumpDriveJumpDelay);
                AddLine().Label("Distance limit").DistanceRangeFormat((float)jumpDrive.MinJumpDistance, (float)jumpDrive.MaxJumpDistance);
                AddLine().Label("Max mass").MassFormat((float)jumpDrive.MaxJumpMass);
            }
        }
        #endregion Magic blocks

        public readonly Color COLOR_STAT_TYPE = new Color(55, 255, 155);
        public readonly Color COLOR_STAT_SHIPDMG = new Color(55, 155, 255);
        public readonly Color COLOR_STAT_PENETRATIONDMG = new Color(100, 155, 255);
        public readonly Color COLOR_STAT_CHARACTERDMG = new Color(255, 220, 100);
        public readonly Color COLOR_STAT_EXPLOSION = new Color(255, 100, 0);
        public readonly Color COLOR_STAT_RICOCHET = new Color(230, 100, 100);

        readonly List<MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition>> AmmoBullets = new List<MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition>>();
        readonly List<MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition>> AmmoMissiles = new List<MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition>>();

        private void Format_Weapon(MyCubeBlockDefinition def)
        {
            #region for debugging icons
            //{
            //    StringBuilder sb = AddLine().Append("  ");
            //    int gridX = 0;
            //    int gridY = 0;
            //    int maxGridX = 8;
            //
            //    for(int i = 0; i < 64; i++)
            //    {
            //        if(gridX >= maxGridX)
            //        {
            //            gridY++;
            //            gridX = 0;
            //            sb = AddLine().Append("  ");
            //        }
            //
            //        char c = (char)(FontsHandler.IconStartingChar + i);
            //
            //        sb.Append(c).Append(" # ");
            //
            //        gridX++;
            //    }
            //
            //    AddLine();
            //    sb = AddLine().Append("  ");
            //    for(int i = 0; i < 8; i++)
            //    {
            //        char c = (char)('\ue100' + i);
            //        sb.Append(c).Append(" # ");
            //
            //        gridX++;
            //    }
            //}
            #endregion

            List<CoreSystemsDef.WeaponDefinition> csWeaponDefs;
            if(Main.CoreSystemsAPIHandler.IsRunning && Main.CoreSystemsAPIHandler.Weapons.TryGetValue(def.Id, out csWeaponDefs))
            {
                Format_CoreSystemsWeapon(def, csWeaponDefs);
                return;
            }

            MyWeaponBlockDefinition weaponDef = (MyWeaponBlockDefinition)def;
            MyWeaponDefinition wpDef;
            if(!MyDefinitionManager.Static.TryGetWeaponDefinition(weaponDef.WeaponDefinitionId, out wpDef))
            {
                StringBuilder sb = AddLine(FontsHandler.RedSh).Color(Color.Red).Append("Block error: can't find weapon definition: ");
                if(weaponDef.WeaponDefinitionId.TypeId != typeof(MyObjectBuilder_WeaponDefinition))
                    sb.Append(weaponDef.WeaponDefinitionId.ToString());
                else
                    sb.Append(weaponDef.WeaponDefinitionId.SubtypeName);
                return;
            }

            MyLargeTurretBaseDefinition turret = def as MyLargeTurretBaseDefinition;

            WeaponConfig gunWWF = Main.WhipWeaponFrameworkAPI.Weapons.GetValueOrDefault(def.Id, null);
            TurretWeaponConfig turretWWF = gunWWF as TurretWeaponConfig;

            // HACK: only static launcher supports capacitor component
            MyEntityCapacitorComponentDefinition capacitorDef = null;
            if(gunWWF == null && (def.Id.TypeId == typeof(MyObjectBuilder_SmallMissileLauncher) || def.Id.TypeId == typeof(MyObjectBuilder_SmallMissileLauncherReload)))
                capacitorDef = Utils.GetEntityComponentFromDef<MyEntityCapacitorComponentDefinition>(def.Id, typeof(MyObjectBuilder_EntityCapacitorComponent));

            bool extraInfo = Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                if(gunWWF != null)
                {
                    float powerUsage = turretWWF?.IdlePowerDrawMax ?? gunWWF.IdlePowerDrawBase;
                    StringBuilder sb = AddLine().Label("Power - Idle").PowerFormat(powerUsage).Separator().Label("Recharge").PowerFormat(gunWWF.ReloadPowerDraw).Append(" (adaptable)");

                    // HACK: hardcoded like in https://gitlab.com/whiplash141/Revived-Railgun-Mod/-/blob/develop/Data/Scripts/WeaponFramework/WhipsWeaponFramework/WeaponBlockBase.cs#L608
                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        sb.Separator().ResourcePriority("Thrust", true);
                }
                else
                {
                    if(capacitorDef != null)
                    {
                        StringBuilder sb = AddLine().Label("Power Charge").PowerFormat(capacitorDef.RechargeDraw);

                        if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                            sb.ResetFormatting().Separator().ResourcePriority(weaponDef.ResourceSinkGroup);

                        float capacitorChargeMultiplier = Hardcoded.CapacitorChargeMultiplier;
                        if(capacitorChargeMultiplier <= 1f)
                            sb.Separator().Color(capacitorChargeMultiplier < 1 ? COLOR_BAD : COLOR_GOOD).Label("Loss").ProportionToPercent(1f - capacitorChargeMultiplier);
                        else
                            sb.Separator().Color(COLOR_GOOD).Label("Multiplier").MultiplierToPercent(capacitorChargeMultiplier);

                        float chargeTime = (capacitorDef.Capacity * 60 * 60) / (capacitorDef.RechargeDraw * capacitorChargeMultiplier);

                        AddLine().Label("Power Capacity").PowerStorageFormat(capacitorDef.Capacity).Separator().Color(COLOR_WARNING).Label("Recharge time").TimeFormat(chargeTime);
                    }
                    else
                    {
                        float requiredPowerInput = (turret != null ? Hardcoded.Turret_PowerReq : Hardcoded.ShipGun_PowerReq);
                        PowerRequired(requiredPowerInput, weaponDef.ResourceSinkGroup, powerHardcoded: true);
                    }
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                MyInventoryComponentDefinition invComp = Utils.GetInventoryFromComponent(def);
                AddLine().Label("Inventory").InventoryFormat(weaponDef.InventoryMaxVolume, wpDef.AmmoMagazinesId, invComp);
            }

            if(turret != null)
            {
                StringBuilder sb = AddLine().Color(turret.AiEnabled ? COLOR_GOOD : COLOR_WARNING).Label("Auto-target").BoolFormat(turret.AiEnabled).ResetFormatting();

                if(turret.AiEnabled)
                {
                    sb.Append(turret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)").Separator().Color(COLOR_WARNING).Append("Max range: ").DistanceFormat(turret.MaxRangeMeters);
                }

                if(extraInfo)
                {
                    if(turret.AiEnabled)
                    {
                        AppendCanTargetOptions(turret.HiddenTargetingOptions, turret.EnabledTargetingOptions);
                    }

                    AddLine().Label("Camera field of view").AngleFormat(turret.MinFov).Append(" to ").AngleFormat(turret.MaxFov);

                    int minPitch = turret.MinElevationDegrees; // this one is actually not capped in game for whatever reason
                    int maxPitch = Math.Min(turret.MaxElevationDegrees, 90); // turret can't rotate past 90deg up
                    float pitchSpeed = turret.ElevationSpeed * Hardcoded.Turret_RotationSpeedMul;

                    int minYaw = turret.MinAzimuthDegrees;
                    int maxYaw = turret.MaxAzimuthDegrees;
                    float yawSpeed = turret.RotationSpeed * Hardcoded.Turret_RotationSpeedMul;

                    AppendTurretAngles(minPitch, maxPitch, pitchSpeed, minYaw, maxYaw, yawSpeed);
                }
            }

            bool showAmmoDetails = Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.AmmoDetails);

            #region Whiplash weapon framework
            if(gunWWF != null)
            {
                AddLine().Append("Whiplash Weapon Framework:");

                float reloadTime = 60f / gunWWF.RateOfFireRPM;
                AddLine().Label("| Fire rate").Number(gunWWF.RateOfFireRPM).Append(" RPM").Separator().Append("Reload: ").TimeFormat(reloadTime).Append(" at max power");

                if(extraInfo)
                {
                    float accuracyAt100m = (float)Math.Tan(MathHelper.ToRadians(gunWWF.DeviationAngleDeg)) * 100 * 2;
                    AddLine().Label("| Accuracy").DistanceFormat(accuracyAt100m).Append(" group at 100m").Separator().Label("Recoil force").ForceFormat(gunWWF.RecoilImpulse);
                }

                if(showAmmoDetails)
                {
                    const int MaxMagNameLength = 20;

                    // only supports one magazine so no point in trying to show all.
                    if(wpDef.AmmoMagazinesId.Length > 0)
                    {
                        MyAmmoMagazineDefinition magDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wpDef.AmmoMagazinesId[0]);
                        AddLine().Label("| Ammo Magazine").Color(COLOR_STAT_TYPE).AppendMaxLength(magDef.DisplayNameText, MaxMagNameLength).ResetFormatting();
                    }

                    AddLine().Label("| Projectile - Velocity").SpeedFormat(gunWWF.MuzzleVelocity).Separator().Label("Max range").DistanceFormat(gunWWF.MaxRange).Separator().Label("Impact force").ForceFormat(gunWWF.HitImpulse);

                    if(extraInfo)
                    {
                        AddLine().Label("| Influenced by gravity").MultiplierToPercent(gunWWF.NaturalGravityMultiplier).Append(" natural").Separator().MultiplierToPercent(gunWWF.ArtificialGravityMultiplier).Append(" artificial");
                    }

                    if(gunWWF.PenetrateOnContact)
                        AddLine().Label("| Penetration damage pool").Append(gunWWF.PenetrationDamage).Separator().Label("Max depth").DistanceFormat(gunWWF.PenetrationRange);

                    if(extraInfo)
                    {
                        if(gunWWF.ExplodePostPenetration && gunWWF.PenetrationExplosionDamage > 0)
                            AddLine().Label("| Penetration stopped explosion - Damage").Append(gunWWF.PenetrationExplosionDamage).Separator().Label("Radius").DistanceFormat(gunWWF.PenetrationExplosionRadius);

                        if(gunWWF.ExplodeOnContact && gunWWF.ContactExplosionDamage > 0)
                            AddLine().Label("| Contact explosion - Damage").Append(gunWWF.ContactExplosionDamage).Separator().Label("Radius").DistanceFormat(gunWWF.ContactExplosionRadius);
                    }

                    if(Main.DefenseShieldsDetector.IsRunning)
                    {
                        // HACK: calculated like in https://gitlab.com/whiplash141/Revived-Railgun-Mod/-/blob/develop/Data/Scripts/WeaponFramework/WhipsWeaponFramework/Projectiles/WeaponProjectile.cs#L234
                        float shieldDamage = (gunWWF.PenetrationDamage + gunWWF.ContactExplosionDamage) * gunWWF.ShieldDamageMultiplier;
                        AddLine().Label("| Damage against DefenseShields").Append(shieldDamage);
                    }

                    if(gunWWF.ShouldProximityDetonate)
                        AddLine().Label("| Proximity detonation").Append(gunWWF.ProximityDetonationRange).Separator().Label("Travel for arming").DistanceFormat(gunWWF.ProximityDetonationArmingRange);
                }
            }
            #endregion

            if(gunWWF == null) // vanilla weapon system
            {
                bool blockTypeCanReload = !Hardcoded.NoReloadTypes.Contains(def.Id.TypeId);
                bool validWeapon = false;
                bool hasAmmo = true;
                bool hasBullets = false;
                bool hasMissiles = false;

                AmmoBullets.Clear();
                AmmoMissiles.Clear();

                for(int i = 0; i < wpDef.AmmoMagazinesId.Length; i++)
                {
                    MyAmmoMagazineDefinition mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wpDef.AmmoMagazinesId[i]);
                    MyAmmoDefinition ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);

                    int ammoTypeIdx = (int)ammo.AmmoType;
                    if(wpDef.WeaponAmmoDatas[ammoTypeIdx] == null)
                        continue;

                    switch(ammo.AmmoType)
                    {
                        case MyAmmoType.HighSpeed:
                        {
                            hasBullets = true;

                            MyProjectileAmmoDefinition bullet = (MyProjectileAmmoDefinition)ammo;
                            if(bullet.ProjectileMassDamage * wpDef.DamageMultiplier != 0
                            || bullet.ProjectileHealthDamage * wpDef.DamageMultiplier != 0
                            || (bullet.HeadShot && bullet.ProjectileHeadShotDamage * wpDef.DamageMultiplier != 0)
                            || bullet.ProjectileExplosionDamage != 0)
                            {
                                validWeapon = true;
                                AmmoBullets.Add(MyTuple.Create(mag, bullet));
                            }

                            break;
                        }
                        case MyAmmoType.Missile:
                        {
                            hasMissiles = true;

                            MyMissileAmmoDefinition missile = (MyMissileAmmoDefinition)ammo;
                            if(missile.MissileExplosionDamage != 0 || missile.MissileHealthPool != 0)
                            {
                                validWeapon = true;
                                AmmoMissiles.Add(MyTuple.Create(mag, missile));
                            }

                            break;
                        }
                        default:
                        {
                            Log.Error($"Warning: Unknown ammo type: {ammo.AmmoType} (#{ammoTypeIdx})");
                            break;
                        }
                    }
                }

                if(!hasBullets && !hasMissiles) // has no valid magazines
                {
                    validWeapon = false;
                    hasAmmo = false;
                }

                float reloadSeconds = wpDef.ReloadTime / 1000;

                if(extraInfo && validWeapon)
                {
                    // DeviateShotAngleAiming only used by hand weapons
                    if(wpDef.DeviateShotAngle == 0)
                    {
                        AddLine().Label("Accuracy").Color(COLOR_GOOD).Append("Pinpoint");
                    }
                    else
                    {
                        // cone base radius = tan(angleFromTip) * height
                        // DeviateShotAngle is in radians (same for Aiming one) and it's angle offset from center line
                        float coneBaseDiameter = 2 * (float)Math.Tan(wpDef.DeviateShotAngle) * 100;

                        AddLine().Label("Accuracy").DistanceFormat(coneBaseDiameter).Append(" group at 100m");
                    }
                }

                bool printBothAmmoData = false;
                bool printFirstAmmoData = false;

                // determine if RoF and reload are identical for both bullets and missiles
                if(hasBullets && hasMissiles)
                {
                    MyWeaponDefinition.MyWeaponAmmoData dataBullets = wpDef.WeaponAmmoDatas[(int)MyAmmoType.HighSpeed];
                    MyWeaponDefinition.MyWeaponAmmoData dataMissiles = wpDef.WeaponAmmoDatas[(int)MyAmmoType.Missile];

                    if(dataBullets.RateOfFire == dataMissiles.RateOfFire && (!blockTypeCanReload || dataBullets.ShotsInBurst == dataMissiles.ShotsInBurst))
                        printFirstAmmoData = true;
                    else
                        printBothAmmoData = true;
                }
                else
                {
                    printFirstAmmoData = true;
                }

                if(showAmmoDetails && validWeapon)
                {
                    const int MaxMagNameLength = 20;
                    const string MagazineSeparator = ": ";
                    const string ColumnSeparator = " <color=gray>|<reset> ";
                    const string DamageSeparator = ColumnSeparator;

                    if(hasBullets)
                    {
                        MyWeaponDefinition.MyWeaponAmmoData ammoData = wpDef.WeaponAmmoDatas[(int)MyAmmoType.HighSpeed];

                        if(printFirstAmmoData || printBothAmmoData)
                        {
                            printFirstAmmoData = false;

                            float rps = ammoData.RateOfFire / 60f;
                            StringBuilder sb = AddLine().Label(printBothAmmoData ? "Bullets - Rate of fire" : "Rate of fire").Number(rps).Append("/s").Separator();

                            sb.Label("Reload");
                            if(blockTypeCanReload && ammoData.ShotsInBurst > 0)
                            {
                                if(ammoData.ShotsInBurst == 1)
                                    sb.TimeFormat(reloadSeconds).Append(" after every").Append(printBothAmmoData ? " bullet" : " shot");
                                else
                                    sb.TimeFormat(reloadSeconds).Append(" after ").Append(ammoData.ShotsInBurst).Append(printBothAmmoData ? " bullets" : " shots");
                            }
                            else
                            {
                                sb.Color(COLOR_GOOD).Append("Instant").ResetFormatting();
                            }
                        }

                        foreach(MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition> tuple in AmmoBullets)
                        {
                            MyAmmoMagazineDefinition mag = tuple.Item1;
                            MyProjectileAmmoDefinition projectile = tuple.Item2;

                            StringBuilder line = AddLine().Append("| ").Color(COLOR_STAT_TYPE).AppendMaxLength(mag.DisplayNameText, MaxMagNameLength).ResetFormatting().Append(MagazineSeparator);

                            if(projectile.ProjectileCount > 1)
                                line.Color(COLOR_GOOD).Append(projectile.ProjectileCount).Append("x ").ResetFormatting();

                            if(projectile.ProjectileMassDamage == 0)
                                line.Color(COLOR_BAD);
                            else
                                line.Color(COLOR_STAT_SHIPDMG);
                            line.Number(projectile.ProjectileMassDamage * wpDef.DamageMultiplier).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);

                            if(projectile.ProjectileHealthDamage == 0)
                                line.Color(COLOR_BAD);
                            else
                                line.Color(COLOR_STAT_CHARACTERDMG);
                            line.Number(projectile.ProjectileHealthDamage * wpDef.DamageMultiplier).Icon(FontsHandler.IconCharacter).Append(DamageSeparator);

                            if(projectile.HeadShot)
                            {
                                if(projectile.ProjectileHeadShotDamage == 0)
                                    line.Color(COLOR_BAD);
                                else
                                    line.Color(COLOR_STAT_CHARACTERDMG);
                                line.Number(projectile.ProjectileHeadShotDamage * wpDef.DamageMultiplier).Icon(FontsHandler.IconCharacterHead).Append(DamageSeparator);
                            }

                            if(projectile.ProjectileExplosionRadius > 0 && projectile.ProjectileExplosionDamage != 0)
                            {
                                // HACK: wpDef.DamageMultiplier is not used for this explosion
                                line.Color(COLOR_STAT_EXPLOSION).DistanceFormat(projectile.ProjectileExplosionRadius).Icon(FontsHandler.IconSphere);
                                line.Color(COLOR_STAT_EXPLOSION).Number(projectile.ProjectileExplosionDamage).Icon(FontsHandler.IconExplode).Append(DamageSeparator);
                            }

                            line.Length -= DamageSeparator.Length;

                            line.Append(ColumnSeparator);

                            // from MyProjectile.Start()
                            if(projectile.SpeedVar > 0)
                                line.Number(projectile.DesiredSpeed * (1f - projectile.SpeedVar)).Append("~").Number(projectile.DesiredSpeed * (1f + projectile.SpeedVar)).Append(" m/s");
                            else
                                line.SpeedFormat(projectile.DesiredSpeed);

                            line.Append(ColumnSeparator);

                            float range = wpDef.RangeMultiplier * projectile.MaxTrajectory;
                            if(wpDef.UseRandomizedRange)
                                line.DistanceRangeFormat(range * Hardcoded.Projectile_RangeMultiplier_Min, range * Hardcoded.Projectile_RangeMultiplier_Max);
                            else
                                line.DistanceFormat(range);

                            // projectiles always have gravity
                            line.Append(" ").Icon(COLOR_WARNING, FontsHandler.IconProjectileGravity).ResetFormatting();

                            // TODO: include ProjectileTrailProbability? only if it has some visible values...
                        }
                    }

                    if(hasMissiles)
                    {
                        MyWeaponDefinition.MyWeaponAmmoData ammoData = wpDef.WeaponAmmoDatas[(int)MyAmmoType.Missile];

                        if(printFirstAmmoData || printBothAmmoData)
                        {
                            printFirstAmmoData = false;

                            float rps = ammoData.RateOfFire / 60f;
                            StringBuilder sb = AddLine().Label(printBothAmmoData ? "Missiles - Rate of fire" : "Rate of fire").Number(rps).Append("/s").Separator();

                            sb.Label("Reload");
                            if(blockTypeCanReload && ammoData.ShotsInBurst > 0)
                            {
                                if(ammoData.ShotsInBurst == 1)
                                    sb.TimeFormat(reloadSeconds).Append(" after every").Append(printBothAmmoData ? " missile" : " shot");
                                else
                                    sb.TimeFormat(reloadSeconds).Append(" after ").Append(ammoData.ShotsInBurst).Append(printBothAmmoData ? " missiles" : " shots");
                            }
                            else
                            {
                                sb.Color(COLOR_GOOD).Append("Instant").ResetFormatting();
                            }
                        }

                        foreach(MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition> tuple in AmmoMissiles)
                        {
                            MyAmmoMagazineDefinition mag = tuple.Item1;
                            MyMissileAmmoDefinition missile = tuple.Item2;

                            StringBuilder line = AddLine().Append("| ").Color(COLOR_STAT_TYPE).AppendMaxLength(mag.DisplayNameText, MaxMagNameLength).ResetFormatting().Append(MagazineSeparator);

                            float penetrationDamage = missile.MissileHealthPool;
                            bool showPenetrationDamage = penetrationDamage > 0;

                            float explosiveDamage = missile.MissileExplosionRadius > 0 ? missile.MissileExplosionDamage : 0;
                            bool showExplosiveDamage = explosiveDamage > 0;

                            // ricochet system is activated
                            // how it normally works: https://steamcommunity.com/sharedfiles/filedetails/?id=2963715247
                            // but it can be changed into bonus damage or can prevent penetration entirely, going through those scenarios below
                            if(missile.MissileRicochetAngle >= 0 && missile.MissileRicochetProbability > 0)
                            {
                                int ricochetChance = (int)Math.Round(missile.MissileRicochetProbability * 100);

                                bool ricochetAllAngles = missile.MissileRicochetAngle == 0f;
                                float ricochetSurfaceAngle = 90 - missile.MissileRicochetAngle; // swapped normal angle to surface angle

                                line.Color(COLOR_STAT_RICOCHET).Append(ricochetChance).Append("% ")
                                    .AngleFormatDeg(ricochetSurfaceAngle).Icon(FontsHandler.IconRicochet)
                                    .Append(" ").Number(missile.MissileRicochetDamage).Icon(FontsHandler.IconBlockDamage)
                                    .Append(DamageSeparator);

                                // TODO: expand to include all the non-ricochet uses of this feature

                                /*
                                bool ricochetCanOverridePenetration = penetrationDamage <= missile.MissileRicochetDamage;

                                if(ricochetCanOverridePenetration)
                                {
                                    showPenetrationDamage = false;

                                    if(ricochetAllAngles && ricochetChance >= 100) // penetration damage completely ignored
                                    {
                                        if(penetrationDamage > 0)
                                            line.Color(COLOR_WARNING);
                                        else
                                            line.Color(COLOR_STAT_SHIPDMG);

                                        line.Number(missile.MissileRicochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);
                                    }
                                    else
                                    {
                                        if(ricochetAllAngles && ricochetChance < 100) // only depends on chance
                                        {
                                            if(penetrationDamage > 0)
                                                line.Append(100 - ricochetChance).Append("%: ").Color(COLOR_STAT_PENETRATIONDMG).Number(penetrationDamage).Icon(FontsHandler.IconBlockPenetration).Append(DamageSeparator);

                                            line.Append(ricochetChance).Append("%: ").Color(COLOR_STAT_SHIPDMG).Number(missile.MissileRicochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);
                                        }
                                        else if(!ricochetAllAngles && ricochetChance >= 100) // only depends on angle
                                        {
                                            if(penetrationDamage > 0)
                                                line.AngleFormatDeg(ricochetSurfaceAngle).Append(": ").Color(COLOR_STAT_PENETRATIONDMG).Number(penetrationDamage).Icon(FontsHandler.IconBlockPenetration).Append(DamageSeparator);

                                            line.AngleFormatDeg(ricochetSurfaceAngle).Append(": ").Color(COLOR_STAT_SHIPDMG).Number(missile.MissileRicochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);
                                        }
                                        else // variable chance and angle...
                                        {
                                            line.Append(ricochetChance).Append("% ").AngleFormatDeg(ricochetSurfaceAngle);

                                            line.Append(": ").Color(COLOR_STAT_SHIPDMG).Number(missile.MissileRicochetDamage).Icon(FontsHandler.IconBlockDamage);

                                            if(penetrationDamage > 0)
                                                line.Color(COLOR_BAD).Append(" or ").Color(COLOR_STAT_PENETRATIONDMG).Number(penetrationDamage).Icon(FontsHandler.IconBlockPenetration);

                                            line.Append(DamageSeparator);
                                        }
                                    }
                                }
                                else
                                {
                                    line.Append(ricochetChance).Append("% ").AngleFormatDeg(ricochetSurfaceAngle).Icon(FontsHandler.IconRicochet).Append(": ").Color(COLOR_STAT_SHIPDMG).Number(missile.MissileRicochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);

                                    // TODO add OR with penetration and explosion afterwards
                                }

                                //line.Color(COLOR_STAT_RICOCHET).Append(ricochetChance).Append("% ").AngleFormatDeg(ricochetSurfaceAngle).Icon(FontsHandler.IconRicochet).Color(COLOR_STAT_RICOCHET).Append(": ").Number(missile.MissileRicochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);
                                */
                            }

                            // HACK: wpDef.DamageMultiplier is not used for missile explosions nor healthpool

                            if(showPenetrationDamage)
                            {
                                line.Color(COLOR_STAT_PENETRATIONDMG).Number(penetrationDamage).Icon(FontsHandler.IconBlockPenetration).Append(DamageSeparator);
                            }

                            if(showExplosiveDamage)
                            {
                                line.Color(COLOR_STAT_EXPLOSION).DistanceFormat(missile.MissileExplosionRadius).Icon(FontsHandler.IconSphere);
                                line.Color(COLOR_STAT_EXPLOSION).Number(missile.MissileExplosionDamage).Icon(FontsHandler.IconExplode).Append(DamageSeparator);
                            }

                            line.Length -= DamageSeparator.Length;

                            line.Append(ColumnSeparator).Color(COLOR_UNIMPORTANT);

                            // HACK: ammo.SpeedVar is not used for missiles
                            // HACK: wepDef.RangeMultiplier and wepDef.UseRandomizedRange are not used for missiles
                            float maxTravel = missile.MaxTrajectory;
                            float maxSpeed = missile.DesiredSpeed;
                            float spawnSpeed = missile.MissileInitialSpeed;
                            float accel = missile.MissileAcceleration;

                            if(!missile.MissileSkipAcceleration && accel != 0)
                            {
                                if(accel > 0)
                                {
                                    float time = (maxSpeed - spawnSpeed) / accel;
                                    line.TimeFormat(time).Icon(FontsHandler.IconMissile).SpeedFormat(maxSpeed);
                                }
                                else // negative acceleration
                                {
                                    line.AccelerationFormat(accel).Icon(FontsHandler.IconMissile).SpeedFormat(maxSpeed);
                                }
                            }
                            else
                            {
                                float speed = Math.Min(spawnSpeed, maxSpeed);
                                line.SpeedFormat(speed);
                            }

                            line.Append(ColumnSeparator).Color(COLOR_UNIMPORTANT);

                            line.DistanceFormat(maxTravel);

                            line.Append(" ");
                            if(missile.MissileGravityEnabled)
                                line.Icon(COLOR_WARNING, FontsHandler.IconProjectileGravity);
                            else
                                line.Icon(COLOR_GOOD, FontsHandler.IconProjectileNoGravity);
                            line.ResetFormatting();

                            //line.MoreInfoInHelp(5);
                        };
                    }
                }

                if(!validWeapon)
                {
                    StringBuilder sb = AddLine().Color(COLOR_WARNING);
                    if(!hasAmmo)
                        sb.Append("Has no ammo magazines.");
                    else
                        sb.Append("Ammo deals no vanilla damage.");

                    sb.Append(" Might have custom behavior.");
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings) && !MyAPIGateway.Session.SessionSettings.WeaponsEnabled)
            {
                AddLine().Color(COLOR_BAD).Append("Weapons are disabled in this world");
            }
        }

        /// <summary>
        /// NOTE: <paramref name="maxTotalWidth"/> includes hidden stuff like &lt;color=red&gt;, &lt;reset&gt; >etc.
        /// </summary>
        void AppendCanTargetOptions(MyTurretTargetingOptions hidden, MyTurretTargetingOptions defaultOn, int maxOptionsPerLine = 5, bool hardcoded = false)
        {
            StringBuilder sb = AddLine();

            if(hardcoded)
                sb.LabelHardcoded("Can target");
            else
                sb.Label("Can target");

            int spacePadding = hardcoded ? 19 : 18;

            int totalOptions = 0;
            int optionsPerLine = 0;

            foreach(MyTurretTargetingOptions option in Hardcoded.TargetOptionsSorted)
            {
                if((hidden & option) != 0)
                    continue; // does not support this option

                if(optionsPerLine >= maxOptionsPerLine)
                {
                    optionsPerLine = 0;
                    sb = AddLine().Color(COLOR_LIST).Append(' ', spacePadding).Append("| ").ResetFormatting();
                }

                bool comesEnabled = ((defaultOn & option) != 0);
                string name = Hardcoded.CustomTargetingOptionName.GetValueOrDefault(option) ?? MyEnum<MyTurretTargetingOptions>.GetName(option);

                sb.Color(comesEnabled ? new Color(230, 255, 230) : new Color(255, 230, 230)).Append(name).ResetFormatting().Append(", ");

                optionsPerLine++;
                totalOptions++;
            }

            if(totalOptions > 0)
            {
                sb.Length -= 2; // remove last comma
                sb.Append(".");
            }
            else
            {
                sb.Color(COLOR_BAD).Append("(Nothing)");
            }
        }

        void AppendTurretAngles(int minPitch, int maxPitch, float pitchSpeed, int minYaw, int maxYaw, float yawSpeed)
        {
            AddLine().Append("Rotation - ");

            if(minPitch == -90 && maxPitch >= 90)
                GetLine().Color(COLOR_GOOD).Append("Pitch: ").AngleFormatDeg(minPitch).Append(" to ").AngleFormatDeg(maxPitch);
            else
                GetLine().Color(COLOR_WARNING).Append("Pitch: ").AngleFormatDeg(minPitch).Append(" to ").AngleFormatDeg(maxPitch);

            GetLine().ResetFormatting().Append(" @ ").RotationSpeed(pitchSpeed).Separator();

            if(minYaw <= -180 && maxYaw >= 180)
                GetLine().Color(COLOR_GOOD).Append("Yaw: ").AngleFormatDeg(360);
            else
                GetLine().Color(COLOR_WARNING).Append("Yaw: ").AngleFormatDeg(minYaw).Append(" to ").AngleFormatDeg(maxYaw);

            GetLine().ResetFormatting().Append(" @ ").RotationSpeed(yawSpeed);
        }

        void Format_CoreSystemsArmor(MyCubeBlockDefinition blockDef, CoreSystemsDef.ArmorDefinition armorDef)
        {
            StringBuilder sb = AddLine().Append(CoreSystemsAPIHandler.APIName).Append(" Armor: ");

            switch(armorDef.Kind)
            {
                case CoreSystemsDef.ArmorDefinition.ArmorType.Heavy: sb.Append("Heavy"); break;
                case CoreSystemsDef.ArmorDefinition.ArmorType.Light: sb.Append("Light"); break;
                case CoreSystemsDef.ArmorDefinition.ArmorType.NonArmor: sb.Append("None"); break;
                default: sb.Append(armorDef.Kind.ToString()); break;
            }

            sb.Separator();
            ResistanceFormat(armorDef.EnergeticResistance, "Energy");
            sb.Separator();
            ResistanceFormat(armorDef.KineticResistance, "Kinetic");
        }

        void Format_CoreSystemsWeapon(MyCubeBlockDefinition blockDef, List<CoreSystemsDef.WeaponDefinition> weaponDefs)
        {
            // NOTE: this includes conveyor sorter too

            // TODO: show inventory volume without cargo mass estimate?
            //var sorter = blockDef as MyConveyorSorterDefinition; // does not extend MyPoweredCargoContainerDefinition
            //if(sorter != null)
            //{
            //    InventoryStats(blockDef, alternateVolume: sorter.InventorySize.Volume);
            //}
            //else
            //{
            //    var weaponDef = blockDef as MyWeaponBlockDefinition;
            //    if(weaponDef != null)
            //    {
            //        if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            //        {
            //            AddLine().Label("Inventory").InventoryFormat(weaponDef.InventoryMaxVolume);
            //        }
            //    }
            //}

            AddLine().Color(COLOR_UNIMPORTANT).Append("This is a ").Append(CoreSystemsAPIHandler.APIName).Append(" block, vanilla stats not relevant.");
            AddLine().Color(COLOR_UNIMPORTANT).Append("Check block's HUD description for any useful stats.");


#if false
            string paddingCategory = $" | ";
            string paddingList = $"<color={COLOR_LIST.R},{COLOR_LIST.G},{COLOR_LIST.B}>    | <reset>";
            
            for(int i = 0; i < weaponDefs.Count; i++)
            {
                CoreSystemsDef.WeaponDefinition wpDef = weaponDefs[i];

                AddLine().Append($"Weapon #{i}:");

                AddLine().Append(paddingCategory).Label("Ammos");

                foreach(var ammo in wpDef.Ammos)
                {
                    AddLine().Append(paddingList).Append($"mag={ammo.AmmoMagazine}; round={ammo.AmmoRound}; hybrid={ammo.HybridRound}; beams={ammo.Beams.Enable}");
                }

                //AddLine().Append(paddingCategory).Label("Muzzles");

                //foreach(var muzzle in wpDef.Assignments.Muzzles)
                //{
                //    AddLine().Append(paddingList).Append($"{muzzle}");
                //}

                //AddLine().Append(paddingCategory).Label("Mountpoints");

                //foreach(var mount in wpDef.Assignments.MountPoints)
                //{
                //    AddLine().Append(paddingList).Append($"id={mount.SubtypeId}; spin={mount.SpinPartId}; yaw={mount.AzimuthPartId}; pitch={mount.ElevationPartId}; durability={mount.DurabilityMod}");
                //}

                //AddLine().Append(paddingCategory).Append($"ejector={wpDef.Assignments.Ejector}; scope={wpDef.Assignments.Scope}");

                //AddLine().Append(paddingCategory).Append($"shootSubmerged={wpDef.HardPoint.CanShootSubmerged}; aimPrediction={wpDef.HardPoint.AimLeadingPrediction}; type={wpDef.HardPoint.HardWare.Type}");

                //AddLine().Append(paddingCategory).Append($"invSize={wpDef.HardPoint.HardWare.InventorySize}; idlepower={wpDef.HardPoint.HardWare.IdlePower};");

                //AddLine().Append(paddingCategory).Label("Upgrades");

                //foreach(var upgrade in wpDef.Upgrades)
                //{
                //    AddLine().Append(paddingList).Append($"{upgrade.Key} = {upgrade.Value.Length} things");
                //}

                //AddLine().Append(paddingCategory).Label("Targetting");

                //AddLine().Append(paddingList).Append($"subsystems={string.Join(",", wpDef.Targeting.SubSystems)}; threats={string.Join(",", wpDef.Targeting.Threats)}");
            }
#endif


            //for(int wcIdx = 0; wcIdx < wcDefs.Count; wcIdx++)
            //{
            //    var wcDef = wcDefs[wcIdx];

            //    var ammos = wcDef.Ammos;
            //    if(ammos != null && ammos.Length > 0)
            //    {
            //        if(ammos.Length == 1)
            //        {
            //            AddLine().Label("Ammo type").Append(ammos[0].AmmoMagazine);
            //        }
            //        else
            //        {
            //            AddLine().Label("Ammo types");

            //            foreach(var ammo in wcDef.Ammos)
            //            {
            //                AddLine().Color(COLOR_PART).Append("         | ").ResetFormatting().Append(ammo.AmmoMagazine);
            //            }
            //        }
            //    }
            //}


            // not actually implemented on WC-side xD
            //float maxPower = Main.WeaponCoreAPIHandler.API.GetMaxPower(blockDef.Id);
            //AddLine().Label("Max Power").PowerFormat(maxPower);

            // bad:
            // - text is cached so it needs to clear cache on hud mode cycling to refresh this
            // - text is long, needs word wrapping
            //if(GameConfig.HudState != HudState.HINTS)
            //{
            //    if(!string.IsNullOrEmpty(blockDef.DescriptionText))
            //    {
            //        if(string.IsNullOrEmpty(blockDef.DescriptionArgs))
            //        {
            //            AddLine().Append(blockDef.DescriptionText);
            //        }
            //        else
            //        {
            //            char[] separator = { ',' };
            //            string[] args = blockDef.DescriptionArgs.Split(separator);
            //            object[] formatParams = new object[args.Length];

            //            for(int i = 0; i < args.Length; i++)
            //            {
            //                formatParams[i] = GetHighlightedControl(args[i]);
            //            }

            //            AddLine().AppendFormat(blockDef.DescriptionText, formatParams);
            //        }
            //    }
            //}
        }

        //static object GetHighlightedControl(string controlId)
        //{
        //    var control = MyAPIGateway.Input.GetGameControl(MyStringId.GetOrCompute(controlId));
        //    if(control == null)
        //        return controlId;

        //    var kb = control.GetControlButtonName(MyGuiInputDeviceEnum.Keyboard) ?? control.GetControlButtonName(MyGuiInputDeviceEnum.KeyboardSecond);
        //    var mb = control.GetControlButtonName(MyGuiInputDeviceEnum.Mouse);

        //    bool hasKb = string.IsNullOrEmpty(kb);
        //    bool hasMb = string.IsNullOrEmpty(mb);

        //    if(hasKb && hasMb)
        //        return $"<color=yellow>{kb} / {mb}<reset>";

        //    if(hasKb)
        //        return $"<color=yellow>{kb}<reset>";

        //    if(hasMb)
        //        return $"<color=yellow>{mb}<reset>";

        //    return controlId;
        //}

        private void Format_Warhead(MyCubeBlockDefinition def)
        {
            MyWarheadDefinition warhead = (MyWarheadDefinition)def; // does not extend MyWeaponBlockDefinition

            // HACK: hardcoded; Warhead doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.AmmoDetails))
            {
                AddLine().Label("Radius").DistanceFormat(warhead.ExplosionRadius).Icon(FontsHandler.IconSphere);
                AddLine().Label("Damage").Append(warhead.WarheadExplosionDamage.ToString("#,###,###,###,##0.##")).Icon(FontsHandler.IconExplode);
            }
        }

        private void Format_TargetDummy(MyCubeBlockDefinition def)
        {
            MyTargetDummyBlockDefinition dummyDef = (MyTargetDummyBlockDefinition)def;

            // HACK: hardcoded; TargetDummy doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            InventoryStats(def, alternateVolume: dummyDef.InventoryMaxVolume, constraintFromDef: dummyDef.InventoryConstraint);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
            {
                if(dummyDef.ConstructionItemAmount == 0)
                    AddLine().Color(COLOR_GOOD).Label("Regeneration requires item").Append("(nothing)");
                else
                    AddLine().Color(COLOR_WARNING).Label("Regeneration requires item").Append(dummyDef.ConstructionItemAmount).Append("x ").ItemName(dummyDef.ConstructionItem);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Regeneration time").TimeFormat(dummyDef.MinRegenerationTimeInS).Append(" to ").TimeFormat(dummyDef.MaxRegenerationTimeInS);

                if(dummyDef.SubpartDefinitions != null && dummyDef.SubpartDefinitions.Count > 0)
                {
                    const int Spaces = 28;
                    AddLine().Label("Shootable parts");

                    bool first = true;

                    foreach(KeyValuePair<string, MyTargetDummyBlockDefinition.MyDummySubpartDescription> kv in dummyDef.SubpartDefinitions)
                    {
                        string name = kv.Key;
                        float hp = kv.Value.Health;
                        bool crit = kv.Value.IsCritical;

                        if(!first)
                            AddLine().Color(COLOR_LIST).Append(' ', Spaces).Append("| ").ResetFormatting();

                        GetLine().Append(name).Append(" - ").RoundedNumber(hp, 2).Append(" hp").Append(crit ? " (critical)" : "");

                        first = false;
                    }
                }
            }
        }

        private void Format_TurretControl(MyCubeBlockDefinition def)
        {
            MyTurretControlBlockDefinition tcbDef = def as MyTurretControlBlockDefinition;
            if(tcbDef == null)
                return;

            // idle seems to be always
            PowerRequired(tcbDef.PowerInputIdle, tcbDef.ResourceSinkGroup);

            bool autoTarget = Hardcoded.CTC_AutoTarget;
            AddLine().LabelHardcoded("Auto-target").BoolFormat(autoTarget);

            if(autoTarget)
            {
                AddLine().Label("Max auto-target range").DistanceFormat(tcbDef.MaxRangeMeters);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                    AppendCanTargetOptions(Hardcoded.CTC_TargetOptionsHidden, Hardcoded.CTC_TargetOptionsDefault, hardcoded: true);
            }

            // PlayerInputDivider is making player rotate it slower when manually controlling, doesn't seem relevant

            bool hasSunTracker = Utils.IsEntityComponentPresent(def.Id, typeof(MyObjectBuilder_SunTrackingComponent));
            AddLine().Color(hasSunTracker ? COLOR_NORMAL : COLOR_WARNING).Label("Can track Sun").BoolFormat(hasSunTracker);
        }

        private void Format_Searchlight(MyCubeBlockDefinition def)
        {
            MySearchlightDefinition searchlight = def as MySearchlightDefinition;
            if(searchlight == null)
                return;

            PowerRequired(searchlight.RequiredPowerInput, searchlight.ResourceSinkGroup);

            StringBuilder sb = AddLine().Color(searchlight.AiEnabled ? COLOR_GOOD : COLOR_WARNING).Label("Auto-target").BoolFormat(searchlight.AiEnabled).ResetFormatting();

            if(searchlight.AiEnabled)
            {
                sb.Append(searchlight.IdleRotation ? " (With idle rotation)" : "(No idle rotation)").Separator().Color(COLOR_WARNING).Append("Max range: ").DistanceFormat(searchlight.MaxRangeMeters);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(searchlight.AiEnabled)
                {
                    AppendCanTargetOptions(Hardcoded.Searchlight_TargetOptionsHidden, Hardcoded.Searchlight_TargetOptionsDefault, hardcoded: true);
                }

                AddLine().Label("Camera field of view").AngleFormat(searchlight.MinFov).Append(" to ").AngleFormat(searchlight.MaxFov);

                AddLine().Append("Radius: ").DistanceFormat(searchlight.LightReflectorRadius.Min).Append(" to ").DistanceFormat(searchlight.LightReflectorRadius.Max).Separator().Append("Default: ").DistanceFormat(searchlight.LightReflectorRadius.Default);
                AddLine().Append("Intensity: ").RoundedNumber(searchlight.LightIntensity.Min, 2).Append(" to ").RoundedNumber(searchlight.LightIntensity.Max, 2).Separator().Append("Default: ").RoundedNumber(searchlight.LightIntensity.Default, 2);
                AddLine().Append("Falloff: ").RoundedNumber(searchlight.LightFalloff.Min, 2).Append(" to ").RoundedNumber(searchlight.LightFalloff.Max, 2).Separator().Append("Default: ").RoundedNumber(searchlight.LightFalloff.Default, 2);

                // TODO: determine limits
                int minPitch = searchlight.MinElevationDegrees;
                int maxPitch = searchlight.MaxElevationDegrees;
                float pitchSpeed = searchlight.ElevationSpeed * Hardcoded.Searchlight_RotationSpeedMul;

                int minYaw = searchlight.MinAzimuthDegrees;
                int maxYaw = searchlight.MaxAzimuthDegrees;
                float yawSpeed = searchlight.RotationSpeed * Hardcoded.Searchlight_RotationSpeedMul;

                AppendTurretAngles(minPitch, maxPitch, pitchSpeed, minYaw, maxYaw, yawSpeed);
            }
        }

        private void Format_AIBlocks(MyCubeBlockDefinition def)
        {
            MyObjectBuilder_AiBlockPowerComponentDefinition powerCompOB = null;
            //MyObjectBuilder_SearchEnemyComponentDefinition searchCompOB = null;
            MyTargetLockingBlockComponentDefinition tlbDef = null;
            MyPathRecorderComponentDefinition pathRecordDef = null;

            MyContainerDefinition containerDef;
            if(MyComponentContainerExtension.TryGetContainerDefinition(def.Id.TypeId, def.Id.SubtypeId, out containerDef) && containerDef.DefaultComponents != null)
            {
                foreach(MyContainerDefinition.DefaultComponent compPointer in containerDef.DefaultComponents)
                {
                    MyComponentDefinitionBase compDefBase;
                    if(!MyComponentContainerExtension.TryGetComponentDefinition(compPointer.BuilderType, compPointer.SubtypeId.GetValueOrDefault(def.Id.SubtypeId), out compDefBase))
                        continue;

                    if(tlbDef == null) // && compPointer.BuilderType == typeof(MyObjectBuilder_TargetLockingBlockComponent))
                    {
                        tlbDef = compDefBase as MyTargetLockingBlockComponentDefinition;
                    }

                    if(pathRecordDef == null)
                    {
                        pathRecordDef = compDefBase as MyPathRecorderComponentDefinition;
                    }

                    if(powerCompOB == null && compPointer.BuilderType == typeof(MyObjectBuilder_AiBlockPowerComponent))
                    {
                        // HACK: MyAiBlockPowerComponentDefinition is not whitelisted...
                        powerCompOB = compDefBase.GetObjectBuilder() as MyObjectBuilder_AiBlockPowerComponentDefinition;
                    }

                    //if(searchCompOB == null && compPointer.BuilderType == typeof(MyObjectBuilder_SearchEnemyComponent))
                    //{
                    //    // HACK: MySearchEnemyComponentDefinition is not whitelisted...
                    //    searchCompOB = compDefBase.GetObjectBuilder() as MyObjectBuilder_SearchEnemyComponentDefinition;
                    //}
                }
            }

            // MyFlightMovementBlockDefinition has power stuff but they're not used

            if(powerCompOB != null)
            {
                PowerRequired(powerCompOB.RequiredPowerInput, powerCompOB.ResourceSinkGroup);
            }

            // can't print as it's inaccurate, SearchRadius is never assigned by GetOB()
            //if(searchCompOB != null)
            //{
            //    AddLine().Label("Target search radius").DistanceFormat(searchCompOB.SearchRadius);
            //}

            if(tlbDef != null)
            {
                AddLine().Label("Target locking - Max distance").DistanceFormat(tlbDef.FocusSearchMaxDistance);

                foreach(MyCubeSize size in MyEnum<MyCubeSize>.Values)
                {
                    float sizeModifier = (size == MyCubeSize.Large ? tlbDef.LockingModifierLargeGrid : tlbDef.LockingModifierSmallGrid);
                    float modifiers = (tlbDef.LockingModifierDistance * sizeModifier);

                    if(modifiers == 0)
                    {
                        float lockTime = Hardcoded.TargetLocking_SecondsToLock(Vector3D.Zero, Vector3D.Forward * tlbDef.FocusSearchMaxDistance, size, tlbDef);
                        AddLine().Append("| ").Append(size == MyCubeSize.Large ? "Largegrid" : "Smallgrid").Append(" lock time: ").TimeFormat(lockTime);
                    }
                    else
                    {
                        float distanceRatioForMin = tlbDef.LockingTimeMin / modifiers;
                        float minTimeDistance = tlbDef.FocusSearchMaxDistance * distanceRatioForMin;

                        float distanceRatioForMax = tlbDef.LockingTimeMax / modifiers;
                        float maxTimeDistance = tlbDef.FocusSearchMaxDistance * distanceRatioForMax;

                        AddLine().Append("| ").Append(size == MyCubeSize.Large ? "Largegrid" : "Smallgrid").Append(" - Min: ")
                            .TimeFormat(tlbDef.LockingTimeMin).Append(" at <").DistanceFormat(minTimeDistance)
                            .Separator().Append("Max: ").TimeFormat(tlbDef.LockingTimeMax).Append(" at >").DistanceFormat(maxTimeDistance);
                    }
                }
            }

            // TODO: print MyBasicMissionFollowPlayerDefinition's stuff when it gets whitelisted

            if(pathRecordDef != null)
            {
                AddLine().Label("Max record time").TimeFormat(pathRecordDef.MaxRecordTime);

                // HACK: update is a UpdateAfterSimulation100() and larger numbers skip runs, hence each being 1.66s
                AddLine().Label("Record interval").TimeFormat(pathRecordDef.MinUpdateBetweenRecords * 100f / 60f).Append(" ~ ").TimeFormat(pathRecordDef.MaxUpdateBetweenRecords * 100f / 60f);

                AddLine().Label("Distance between records").DistanceFormat(pathRecordDef.MinDistanceBetweenRecords).Append(" ~ ").DistanceFormat(pathRecordDef.MaxDistanceBetweenRecords);
            }
        }

        private void Format_EventController(MyCubeBlockDefinition def)
        {
            var eventDef = def as MyEventControllerBlockDefinition;
            if(eventDef == null)
                return;

            PowerRequired(eventDef.RequiredPowerInput, eventDef.ResourceSinkGroup);
        }

        private void Format_EmotionController(MyCubeBlockDefinition def)
        {
            var emoDef = def as MyEmotionControllerBlockDefinition;
            if(emoDef == null)
                return;

            PowerRequired(emoDef.RequiredPowerInput, emoDef.ResourceSinkGroup);
        }

        private void Format_HeatVent(MyCubeBlockDefinition def)
        {
            MyHeatVentBlockDefinition ventDef = def as MyHeatVentBlockDefinition;
            if(ventDef == null)
                return;

            // RequiredPowerInput is not used

            PowerRequired(0, MyStringHash.NullOrEmpty, powerHardcoded: true, groupHardcoded: true);


            // TODO: stats?

            /*
            AddLine().Label("PowerDependency").Number(ventDef.PowerDependency);
            AddLine().Label("RequiredPowerInput").Number(ventDef.RequiredPowerInput);

            //AddLine().Label("ColorMinimalPower").Color(ventDef.ColorMinimalPower).AppendRGBA(ventDef.ColorMinimalPower);
            //AddLine().Label("ColorMaximalPower").Color(ventDef.ColorMaximalPower).AppendRGBA(ventDef.ColorMaximalPower);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                MyBounds radius = ventDef.LightRadiusBounds;
                MyBounds intensity = ventDef.LightIntensityBounds;
                MyBounds falloff = ventDef.LightFalloffBounds;
                MyBounds offset = ventDef.LightOffsetBounds;

                AddLine().Append("Radius: ").DistanceFormat(radius.Min).Append(" to ").DistanceFormat(radius.Max).Separator().Append("Default: ").DistanceFormat(radius.Default);
                AddLine().Append("Intensity: ").RoundedNumber(intensity.Min, 2).Append(" to ").RoundedNumber(intensity.Max, 2).Separator().Append("Default: ").RoundedNumber(intensity.Default, 2);
                AddLine().Append("Falloff: ").RoundedNumber(falloff.Min, 2).Append(" to ").RoundedNumber(falloff.Max, 2).Separator().Append("Default: ").RoundedNumber(falloff.Default, 2);
                AddLine().Append("Offset: ").RoundedNumber(offset.Min, 2).Append(" to ").RoundedNumber(offset.Max, 2).Separator().Append("Default: ").RoundedNumber(offset.Default, 2);
            }
            */
        }

        private void Format_SafeZone(MyCubeBlockDefinition def)
        {
            MySafeZoneBlockDefinition safeZone = (MySafeZoneBlockDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power usage - Min").PowerFormat(safeZone.MinSafeZonePowerDrainkW / 1000f).Separator().Label("Max").PowerFormat(safeZone.MaxSafeZonePowerDrainkW / 1000f);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(safeZone.ResourceSinkGroup);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Radius").DistanceRangeFormat(safeZone.MinSafeZoneRadius, safeZone.MaxSafeZoneRadius).Separator().Label("Default").DistanceFormat(safeZone.DefaultSafeZoneRadius);
                AddLine().Label("Activation time").TimeFormat(safeZone.SafeZoneActivationTimeS);

                // HACK block is hardcoded to only use zone chips, see MySafeZoneComponent.TryConsumeUpkeep().
                MyComponentDefinition zoneChipDef = MyDefinitionManager.Static.GetComponentDefinition(new MyDefinitionId(typeof(MyObjectBuilder_Component), "ZoneChip"));
                string itemName = (zoneChipDef == null ? "ZoneChip" : zoneChipDef.DisplayNameText);

                AddLine().Label("Upkeep").Append(safeZone.SafeZoneUpkeep).Append("x ").Append(itemName).Separator().Label("for").TimeFormat(safeZone.SafeZoneUpkeepTimeM * 60f);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                MyInventoryComponentDefinition invComp = Utils.GetInventoryFromComponent(def); // SafeZone type has no inventory data in its definition, only components can add inventory to it.
                if(invComp != null)
                {
                    AddLine().Label("Inventory").InventoryFormat(invComp.Volume, invComp.InputConstraint, invComp);
                    InventoryConstraints(invComp.Volume, invComp.InputConstraint, invComp);
                }
            }
        }

        private void Format_ContractBlock(MyCubeBlockDefinition def)
        {
            PowerRequired(0, MyStringHash.NullOrEmpty, powerHardcoded: true, groupHardcoded: true);
        }

        private void Format_StoreBlock(MyCubeBlockDefinition def)
        {
            MyStoreBlockDefinition store = (MyStoreBlockDefinition)def;
            MyVendingMachineDefinition vending = def as MyVendingMachineDefinition;

            InventoryStats(def);

            if(vending != null)
            {
                if(vending.DefaultItems != null && vending.DefaultItems.Count > 0)
                {
                    AddLine().Label("Default store items:");

                    foreach(MyObjectBuilder_StoreItem entry in vending.DefaultItems)
                    {
                        AddLine().Append("    ")
                            .Append(entry.StoreItemType == StoreItemTypes.Offer ? "Sell: " : "Buy: ");

                        switch(entry.ItemType)
                        {
                            case ItemTypes.Hydrogen:
                                GetLine().Append("Hydrogen").Separator().Label("Price").CurrencyFormat(entry.PricePerUnit);
                                break;
                            case ItemTypes.Oxygen:
                                GetLine().Append("Oxygen").Separator().Label("Price").CurrencyFormat(entry.PricePerUnit);
                                break;
                            case ItemTypes.PhysicalItem:
                            {
                                MyPhysicalItemDefinition itemDef = null;
                                bool exists = MyDefinitionManager.Static.TryGetDefinition(entry.Item.Value, out itemDef); // same getter the game uses
                                if(!exists)
                                    GetLine().Color(COLOR_BAD);

                                GetLine().ItemName(entry.Item.Value).Separator().Label("Price").CurrencyFormat(entry.PricePerUnit);

                                if(!exists)
                                    GetLine().ResetFormatting();

                                //if(!exists)
                                //    GetLine().Append(" (Item not found!)").ResetFormatting();
                                break;
                            }
                            case ItemTypes.Grid:
                            {
                                MyPrefabDefinition prefabDef = MyDefinitionManager.Static.GetPrefabDefinition(entry.PrefabName); // same getter the game uses
                                if(prefabDef == null)
                                    GetLine().Color(COLOR_BAD);

                                GetLine().Append(entry.PrefabName).Append(" (").Append(entry.PrefabTotalPcu).Append(" PCU)").Separator().Label("Price").CurrencyFormat(entry.PricePerUnit);

                                if(prefabDef == null)
                                    GetLine().Append(" (Prefab not found!)").ResetFormatting();
                                break;
                            }
                        }
                    }
                }
            }
        }
        #endregion Per block info

        private void DLCFormat(MyCubeBlockDefinition def)
        {
            if(def.DLCs == null || def.DLCs.Length == 0)
                return;

            AddLine(FontsHandler.SkyBlueSh).Color(COLOR_DLC).Label("DLC").ResetFormatting();

            bool multiDLC = def.DLCs.Length > 1;

            for(int i = 0; i < def.DLCs.Length; ++i)
            {
                string dlcId = def.DLCs[i];

                if(multiDLC && i > 0)
                {
                    if(Main.TextAPI.IsEnabled)
                        AddLine().Append("               | ");
                    else
                        GetLine().Append(", ");
                }

                MyDLCs.MyDLC dlc;
                if(MyDLCs.TryGetDLC(dlcId, out dlc))
                {
                    if(!MyAPIGateway.DLC.HasDLC(dlcId, MyAPIGateway.Multiplayer.MyId))
                        GetLine().Color(COLOR_BAD);

                    GetLine().Append(MyTexts.GetString(dlc.DisplayName)).ResetFormatting();
                }
                else
                {
                    GetLine().Append("(Unknown: ").Color(COLOR_BAD).Append(dlcId).ResetFormatting().Append(")");
                }
            }
        }

        private void DamageMultiplierAsResistance(float damageMultiplier, string label = "Resistance")
        {
            int dmgResPercent = (int)(((1f / damageMultiplier) - 1) * 100);

            GetLine()
                .Color(dmgResPercent == 0 ? COLOR_NORMAL : (dmgResPercent > 0 ? COLOR_GOOD : COLOR_WARNING)).Label(label).Append(dmgResPercent > 0 ? "+" : "").Append(dmgResPercent).Append("%")
                .Color(COLOR_UNIMPORTANT).Append(" (x").RoundedNumber(damageMultiplier, 2).Append(")").ResetFormatting();
        }

        private void ResistanceFormat(double resistance, string label = "Resistance")
        {
            int resPercent = (int)((resistance - 1) * 100f);
            float multiplier = (float)(1d / resistance);

            GetLine()
                .Color(resPercent == 1 ? COLOR_NORMAL : (resPercent > 0 ? COLOR_GOOD : COLOR_WARNING)).Label(label).Append(resPercent > 0 ? "+" : "").Append(resPercent).Append("%")
                .Color(COLOR_UNIMPORTANT).Append(" (x").RoundedNumber(multiplier, 2).Append(")").ResetFormatting();
        }

        private void PowerRequired(float mw, string groupName, bool powerHardcoded = false, bool groupHardcoded = false)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                MyStringHash groupNameHash = (groupName != null ? MyStringHash.GetOrCompute(groupName) : MyStringHash.NullOrEmpty);
                PowerRequired(mw, groupNameHash, powerHardcoded, groupHardcoded);
            }
        }

        private void PowerRequired(float mw, MyStringHash groupName, bool powerHardcoded = false, bool groupHardcoded = false)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                Color color = (mw <= 0 ? COLOR_GOOD : COLOR_NORMAL);

                if(powerHardcoded)
                    AddLine().Color(color).LabelHardcoded("Power required");
                else
                    AddLine().Color(color).Label("Power required");

                if(mw <= 0)
                    GetLine().Append("No");
                else
                    GetLine().PowerFormat(mw);

                if(mw > 0 && Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                {
                    GetLine().ResetFormatting().Separator().ResourcePriority(groupName, groupHardcoded);
                }
            }
        }

        private void InventoryStats(MyCubeBlockDefinition def, float alternateVolume = 0, float hardcodedVolume = 0, bool showConstraints = true, MyInventoryConstraint constraintFromDef = null)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                float volume = alternateVolume;
                MyInventoryComponentDefinition invComp = Utils.GetInventoryFromComponent(def);

                if(invComp != null)
                    volume = invComp.Volume;

                MyInventoryConstraint invConstraint = invComp?.InputConstraint ?? constraintFromDef;

                if(volume > 0)
                    AddLine().Label("Inventory").InventoryFormat(volume, invConstraint, invComp);
                else if(hardcodedVolume > 0)
                    AddLine().LabelHardcoded("Inventory").InventoryFormat(hardcodedVolume, invConstraint, invComp);

                if((volume > 0 || hardcodedVolume > 0) && showConstraints)
                    InventoryConstraints(volume, invConstraint, invComp);
            }
        }

        private void InventoryConstraints(float maxVolume, MyInventoryConstraint invLimit, MyInventoryComponentDefinition invComp)
        {
            if(!Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryExtras))
                return;

            if(invComp != null)
            {
                int maxItems = invComp.MaxItemCount;
                float maxMass = invComp.Mass;

                if(maxItems >= int.MaxValue)
                    maxItems = 0;

                if(maxMass >= float.MaxValue)
                    maxMass = 0;

                if(maxItems > 0 || maxMass > 0)
                {
                    AddLine().Append("    ");

                    if(maxItems > 0)
                    {
                        GetLine().Color(COLOR_BAD).Label("Max items").Append(maxItems).ResetFormatting();
                    }

                    if(maxMass > 0)
                    {
                        if(maxItems > 0)
                            GetLine().Separator();

                        GetLine().Color(COLOR_BAD).Label("Max mass").MassFormat(maxMass).ResetFormatting();
                    }
                }
            }

            if(invLimit != null)
            {
                AddLine(FontsHandler.YellowSh).Color(COLOR_WARNING).Label(invLimit.IsWhitelist ? "    Items allowed" : "    Items NOT allowed");

                foreach(MyDefinitionId id in invLimit.ConstrainedIds)
                {
                    AddLine().Append("       - ").ItemName(id);

                    //MyPhysicalItemDefinition itemDef = MyDefinitionManager.Static.GetPhysicalItemDefinition(id);
                    //if(itemDef == null)
                    //    continue;

                    //AddLine().Append("       - ").Append(itemDef.DisplayNameText).Append(" (").IdTypeSubtypeFormat(id).Append(")");

                    //AddLine().Append("       - ").IdTypeSubtypeFormat(id).Append(" (Max fit: ");
                    //
                    //MyPhysicalItemDefinition itemDef = MyDefinitionManager.Static.GetPhysicalItemDefinition(id);
                    //if(itemDef == null)
                    //{
                    //    GetLine().Color(COLOR_BAD).Append("ERROR: NOT FOUND!").ResetFormatting();
                    //}
                    //else
                    //{
                    //    float maxFit = (maxVolume / itemDef.Volume);
                    //
                    //    if(itemDef.HasIntegralAmounts)
                    //        GetLine().Append(Math.Floor(maxFit));
                    //    else
                    //        GetLine().Append(maxFit.ToString("0.##"));
                    //}
                    //
                    //GetLine().Append(")");
                }

                foreach(MyObjectBuilderType type in invLimit.ConstrainedTypes)
                {
                    AddLine().Append("       - All of type: ").IdTypeFormat(type);
                }
            }
        }

        private readonly List<MyDefinitionId> removeCacheIds = new List<MyDefinitionId>();
        private void PurgeCache()
        {
            bool haveNotifCache = CachedBuildInfoNotification.Count > 0;
            bool haveTextAPICache = CachedBuildInfoTextAPI.Count > 0;

            if(haveNotifCache || haveTextAPICache)
            {
                removeCacheIds.Clear();
                long time = DateTime.UtcNow.Ticks;

                if(haveNotifCache)
                {
                    foreach(KeyValuePair<MyDefinitionId, Cache> kv in CachedBuildInfoNotification)
                        if(kv.Value.expires < time)
                            removeCacheIds.Add(kv.Key);

                    if(CachedBuildInfoNotification.Count == removeCacheIds.Count)
                        CachedBuildInfoNotification.Clear();
                    else
                        foreach(MyDefinitionId key in removeCacheIds)
                            CachedBuildInfoNotification.Remove(key);

                    removeCacheIds.Clear();
                }

                if(haveTextAPICache)
                {
                    foreach(KeyValuePair<MyDefinitionId, Cache> kv in CachedBuildInfoTextAPI)
                        if(kv.Value.expires < time)
                            removeCacheIds.Add(kv.Key);

                    if(CachedBuildInfoTextAPI.Count == removeCacheIds.Count)
                        CachedBuildInfoTextAPI.Clear();
                    else
                        foreach(MyDefinitionId key in removeCacheIds)
                            CachedBuildInfoTextAPI.Remove(key);

                    removeCacheIds.Clear();
                }
            }
        }

        public void OnConfigReloaded()
        {
            Refresh();
        }

        public void Refresh(bool redraw = false, StringBuilder write = null, int forceDrawTicks = 0)
        {
            HideText();
            CachedBuildInfoTextAPI.Clear();

            if(textObject != null)
            {
                textObject.Scale = Main.Config.TextAPIScale.Value;
                textObject.HideWithHUD = !Main.Config.TextAlwaysVisible.Value;
            }

            if(redraw)
            {
                if(Main.EquipmentMonitor.BlockDef != null || Main.QuickMenu.Shown)
                    UpdateTextAPIvisuals(textAPIlines);
                else
                    UpdateTextAPIvisuals(write ?? textAPIlines);
            }

            this.forceDrawTicks = forceDrawTicks;
        }

        #region Classes for storing generated info
        public class Cache
        {
            public long expires;

            public void ResetExpiry()
            {
                expires = DateTime.UtcNow.Ticks + (TimeSpan.TicksPerSecond * CACHE_EXPIRE_SECONDS);
            }
        }

        public class CacheTextAPI : Cache
        {
            public readonly StringBuilder Text;
            public readonly Vector2D TextSize;
            public readonly List<LocalTooltip> Tooltips;

            public CacheTextAPI(StringBuilder textSB, Vector2D textSize, List<LocalTooltip> tooltips)
            {
                ResetExpiry();
                Text = new StringBuilder(textSB.Length);
                Text.AppendStringBuilder(textSB);
                TextSize = textSize;
                Tooltips = tooltips.Count > 0 ? new List<LocalTooltip>(tooltips) : null;
            }
        }

        public class CacheNotifications : Cache
        {
            public readonly List<IMyHudNotification> Lines;

            public CacheNotifications(List<HudLine> hudLines)
            {
                ResetExpiry();

                int lineNum = 0;

                for(int i = 0; i < hudLines.Count; ++i)
                {
                    HudLine line = hudLines[i];
                    if(line.str.Length > 0)
                        lineNum++;
                }

                Lines = new List<IMyHudNotification>(lineNum);

                for(int i = 0; i < hudLines.Count; ++i)
                {
                    HudLine line = hudLines[i];

                    if(line.str.Length > 0)
                    {
                        Lines.Add(MyAPIGateway.Utilities.CreateNotification(line.str.ToString(), 16, line.font));
                    }
                }
            }
        }

        public class HudLine
        {
            public StringBuilder str = new StringBuilder(128);
            public string font;
            public int lineWidthPx;
        }
        #endregion Classes for storing generated info
    }
}
