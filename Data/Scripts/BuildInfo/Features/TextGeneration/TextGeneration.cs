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
using VRage.Game.ObjectBuilders.Definitions.SessionComponents;
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
        static readonly string LightningAttractionTooltip = "If lightning strikes within the range of a tree, decoy or radio antenna it will be redirected to that instead." +
                                                            $"\nTrees have a radius of {Hardcoded.Tree_LightningRodRadius}m to attract lightning, but being under one does not guarantee that a decoy won't be hit." +
                                                            "\nRadio antennas and decoys require to be functional and turned on to be targeted by lightning." +
                                                            "\nLightning damage can be disabled by world settings or mods overriding weather definitions.";

        #region Constants
        const BlendTypeEnum FG_BLEND_TYPE = BlendTypeEnum.PostPP;

        readonly MyStringId BG_MATERIAL = MyStringId.GetOrCompute("BuildInfo_UI_Square");
        readonly Color BG_COLOR = new Color(41, 54, 62);
        const float BG_EDGE = 0.02f; // added padding edge around the text boundary for the background image

        const float CharInvVolM3Offset = 50 / 1000f; // subtracting 50L from char inv max volume to account for common tools

        const float MENU_BG_OPACITY = 0.7f;

        const int SCROLL_FROM_LINE = 2; // ignore lines to this line when scrolling, to keep important stuff like mass in view at all times; used in HUD notification view mode.
        const int SPACE_SIZE = 8; // space character's width; used in HUD notification view mode.
        const int MAX_LINES = 8; // max amount of HUD notification lines to print; used in HUD notification view mode.
        public const int MOD_NAME_MAX_LENGTH = 40;
        public const int PLAYER_NAME_MAX_LENGTH = 24;
        public const int BLOCK_NAME_MAX_LENGTH = 35;

        const double FREEZE_MAX_DISTANCE_SQ = 50 * 50; // max distance allowed to go from the frozen block preview before it gets turned off.

        public const int CACHE_PURGE_TICKS = 60 * 30; // how frequent the caches are being checked for purging, in ticks
        public const int CACHE_EXPIRE_SECONDS = 60 * 5; // how long a cached string remains stored until it's purged, in seconds

        readonly Vector2D TEXT_HUDPOS = new Vector2D(-0.9675, 0.49); // textAPI default left side position
        readonly Vector2D TEXT_HUDPOS_WIDE = new Vector2D(-0.9675 / 3f, 0.49); // textAPI default left side position when using a really wide resolution
        readonly Vector2D TEXT_HUDPOS_RIGHT = new Vector2D(0.9692, 0.26); // textAPI default right side position
        readonly Vector2D TEXT_HUDPOS_RIGHT_WIDE = new Vector2D(0.9692 / 3f, 0.26); // textAPI default right side position when using a really wide resolution

        readonly MyDefinitionId DEFID_MENU = new MyDefinitionId(typeof(MyObjectBuilder_GuiScreen)); // just a random non-block type to use as the menu's ID

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
        bool aimInfoNeedsUpdate = false;
        readonly HashSet<IMySlimBlock> ProjectedUnder = new HashSet<IMySlimBlock>();
        public Vector3D? LastGizmoPosition;
        public Cache cache = null; // currently selected cache to avoid another dictionary lookup in Draw()

        int gridMassComputeCooldown;
        float gridMassCache;
        long prevSelectedGrid;

        // used by the textAPI view mode
        public readonly Dictionary<MyDefinitionId, Cache> CachedBuildInfoTextAPI = new Dictionary<MyDefinitionId, Cache>(MyDefinitionId.Comparer);
        bool useLeftSide = true;
        double prevAspectRatio = 1;
        int lines;
        int forceDrawTicks = 0;
        StringBuilder textAPIlines = new StringBuilder(TEXTAPI_TEXT_LENGTH);
        TextAPI.TextPackage textObject;
        const int TEXTAPI_TEXT_LENGTH = 2048;

        // used by the HUD notification view mode
        public readonly List<IMyHudNotification> hudNotifLines = new List<IMyHudNotification>();
        public readonly Dictionary<MyDefinitionId, Cache> CachedBuildInfoNotification = new Dictionary<MyDefinitionId, Cache>(MyDefinitionId.Comparer);
        int atLine = SCROLL_FROM_LINE;
        long lastScroll = 0;
        int largestLineWidth = 0;
        List<HudLine> notificationLines = new List<HudLine>();

        // used in generating the block info text or menu for either view mode
        int line = -1;
        bool addLineCalled = false;

        // used to quickly find the format method for block types
        delegate void TextGenerationCall(MyCubeBlockDefinition def);
        readonly Dictionary<MyObjectBuilderType, TextGenerationCall> formatLookup
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

        void TextAPI_APIDetected()
        {
            // FIXME: doesn't re-show the menu if in it while this happens...
            Main.TextGeneration.HideText(); // force a re-check to make the HUD -> textAPI transition
        }

        void GameConfig_HudStateChanged(HudStateChangedInfo info)
        {
            if(info.IsTemporary)
                return;

            if(Main.Config.TextShow.ValueEnum == TextShowMode.HudHints)
            {
                LastDefId = default(MyDefinitionId);
            }

            ReCheckSide();
        }

        void GUIMonitor_OptionsMenuClosed()
        {
            ReCheckSide();

            if(Math.Abs(prevAspectRatio - Main.GameConfig.AspectRatio) > 0.0001)
            {
                prevAspectRatio = Main.GameConfig.AspectRatio;
                CachedBuildInfoTextAPI.Clear();
            }
        }

        void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            LastDefId = default(MyDefinitionId);
        }

        void ReCheckSide()
        {
            bool shouldUseLeftSide = (Main.GameConfig.HudState != HudState.BASIC);

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
                // HACK: let this box be rendered by textAPI to draw under the textAPI menu.
                textObject.Visible = Main.TextAPI.InModMenu;

                if(!Main.TextAPI.InModMenu)
                    textObject.Draw();
            }
            else
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
            }
        }

        void Update(int tick)
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

        bool UpdateWithDef(MyCubeBlockDefinition def)
        {
            LocalTooltips.Clear();

            bool processTooltips = false;

            if(!textShown)
                processTooltips = true;

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
            public int StartLine;
            public int Lines;
            public StringBuilder Text;
            public Action Action;
        }

        List<LocalTooltip> LocalTooltips = new List<LocalTooltip>();
        bool LineHadTooltip = false;

        /// <summary>
        /// Will return NULL if textAPI is not present/disabled.
        /// </summary>
        /// <param name="action">Called when clicking the line.</param>
        /// <param name="line">if -1 then the current line is used and automatically calls <see cref="Utilities.StringBuilderExtensions.MarkTooltip(StringBuilder)"/> when it ends.</param>
        /// <param name="coveringLines">How many lines the tooltip to cover.
        /// Must be called after the first line but before the other lines!
        /// Only the first line will get the info symbol.</param>
        /// <returns></returns>
        StringBuilder CreateTooltip(Action action = null, int line = -1, int coveringLines = 1)
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
                StartLine = line,
                Lines = coveringLines,
                Action = action,
            });

            return sb;
        }

        void SimpleTooltip(string text)
        {
            CreateTooltip()?.Append(text);
        }

        StringBuilder ModClickTooltip(string serviceName, ulong publishedId, int coveringLines = 1)
        {
            Action action = null;

            if(publishedId > 0)
                action = () => Utils.OpenModPage(serviceName, publishedId);

            StringBuilder tooltip = CreateTooltip(action, coveringLines: coveringLines);

            if(tooltip != null)
                tooltip.Append("You can <color=lime>click<reset> to go to the mod's workshop page.");

            return tooltip;
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
                    Vector2 min = textMin + new Vector2(0, -lineHeight * lt.StartLine);
                    Vector2 max = min + new Vector2(addMax.X, addMax.Y * Math.Max(1, lt.Lines));

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

        Vector2D UpdateTextAPIvisuals(StringBuilder textSB, Vector2D textSize = default(Vector2D))
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
                if(textObject != null)
                {
                    textObject.Visible = false;
                }

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

        void ResetLines()
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

        StringBuilder AddLine(string font = FontsHandler.WhiteSh)
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

        StringBuilder GetLine()
        {
            return (Main.TextAPI.IsEnabled ? textAPIlines : notificationLines[line].str);
        }

        void AddOverlaysHint(MyCubeBlockDefinition def)
        {
            // TODO: remove last condition when adding overlay to WC
            if(Main.SpecializedOverlays.Get(def.Id.TypeId) != null && !Main.CoreSystemsAPIHandler.Weapons.ContainsKey(def.Id))
            {
                StringBuilder sb = AddLine(FontsHandler.GraySh).Color(COLOR_UNIMPORTANT).Append("(Specialized overlay available. ");
                Main.Config.CycleOverlaysBind.Value.GetBinds(sb);
                sb.Append(" to cycle)");
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
        StringBuilder AddMenuItemLine(int item, bool enabled = true)
        {
            StringBuilder sb = AddLine(font: (Main.QuickMenu.SelectedItem == item ? FontsHandler.GreenSh : (enabled ? FontsHandler.WhiteSh : FontsHandler.RedSh)));

            if(Main.QuickMenu.SelectedItem == item)
                sb.Color(COLOR_GOOD).Append("  > ");
            else
                sb.Color(enabled ? COLOR_NORMAL : COLOR_UNIMPORTANT).Append(' ', 6);

            return sb;
        }

        public void GenerateMenuText()
        {
            ResetLines();

            AddLine(FontsHandler.SkyBlueSh).Color(COLOR_BLOCKTITLE).Append(BuildInfoMod.ModName).Append(" mod");

            int i = 0;

            // HACK this must match the data from the HandleInput() which controls the actual actions of these

            StringBuilder sb = AddMenuItemLine(i++).Append("Close menu");

            sb.Color(COLOR_UNIMPORTANT).Append("   (");
            if(Main.Config.MenuBind.Value.IsAssigned())
            {
                Main.Config.MenuBind.Value.GetBinds(sb);
            }
            else
            {
                sb.Append(Main.ChatCommandHandler.CommandQuickMenu.PrimaryCommand);
            }
            sb.Append(")");

            if(Main.TextAPI.IsEnabled)
            {
                AddLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Actions:");
            }

            sb = AddMenuItemLine(i++).Append("Add aimed block to toolbar");
            sb.Color(COLOR_UNIMPORTANT).Append("   (");
            if(Main.Config.BlockPickerBind.Value.IsAssigned())
            {
                Main.Config.BlockPickerBind.Value.GetBinds(sb);
            }
            else
            {
                sb.Append(Main.ChatCommandHandler.CommandGetBlock.PrimaryCommand);
            }
            sb.Append(")");

            AddMenuItemLine(i++).Append("Open block's mod workshop").Color(COLOR_UNIMPORTANT).Append("   (").Append(Main.ChatCommandHandler.CommandModLink.PrimaryCommand).Append(')');

            AddMenuItemLine(i++).Append("Help topics").Color(COLOR_UNIMPORTANT).Append("   (").Append(Main.ChatCommandHandler.CommandHelp.PrimaryCommand).Append(')');

            AddMenuItemLine(i++).Append("Open this mod's workshop").Color(COLOR_UNIMPORTANT).Append("   (").Append(Main.ChatCommandHandler.CommandWorkshop.PrimaryCommand).Append(')');

            if(Main.TextAPI.IsEnabled)
            {
                AddLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Settings:");
            }

            AddMenuItemLine(i++).Append("Text info: ").Append(Main.Config.TextShow.ValueName);

            sb = AddMenuItemLine(i++).Append("Draw overlays: ").Append(Main.Overlays.OverlayModeName);
            if(Main.Config.CycleOverlaysBind.Value.IsAssigned())
            {
                sb.Color(COLOR_UNIMPORTANT).Append("   (");
                Main.Config.CycleOverlaysBind.Value.GetBinds(sb);
                sb.Append(")").ResetFormatting();
            }

            sb = AddMenuItemLine(i++).Append("Placement transparency: ").Append(MyCubeBuilder.Static.UseTransparency ? "ON" : "OFF");
            if(Main.Config.ToggleTransparencyBind.Value.IsAssigned())
            {
                sb.Color(COLOR_UNIMPORTANT).Append("   (");
                Main.Config.ToggleTransparencyBind.Value.GetBinds(sb);
                sb.Append(")").ResetFormatting();
            }

            sb = AddMenuItemLine(i++).Append("Freeze in position: ").Append(MyAPIGateway.CubeBuilder.FreezeGizmo ? "ON" : "OFF");
            if(Main.Config.FreezePlacementBind.Value.IsAssigned())
            {
                sb.Color(COLOR_UNIMPORTANT).Append("   (");
                Main.Config.FreezePlacementBind.Value.GetBinds(sb);
                sb.Append(")").ResetFormatting();
            }

            sb = AddMenuItemLine(i++, Main.TextAPI.WasDetected).Append("Use TextAPI: ");
            if(Main.TextAPI.WasDetected)
                sb.Append(Main.TextAPI.Use ? "ON" : "OFF");
            else
                sb.Append("OFF (Mod not detected)");

            AddMenuItemLine(i++).Append("Reload settings file").Color(COLOR_UNIMPORTANT).Append("   (").Append(Main.ChatCommandHandler.CommandReloadConfig.PrimaryCommand).Append(')');

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
                AddLine().Color(COLOR_INTERNAL).Label("Id").ResetFormatting().Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(def.Id.SubtypeName);
                AddLine().Color(COLOR_INTERNAL).Label("BlockPairName").ResetFormatting().Append(def.BlockPairName);
            }
            #endregion Internal info

            #region Mass, grid mass
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.Mass))
            {
                float mass = (def.HasPhysics ? def.Mass : 0); // HACK: game doesn't use mass from blocks with HasPhysics=false
                Color massColor = Color.GreenYellow;

                if(isProjected)
                {
                    AddLine().Color(massColor).ExactMassFormat(mass);
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

                    StringBuilder sb = AddLine().Color(massColor).ExactMassFormat(mass);

                    if(grid.Physics != null)
                    {
                        if(grid.EntityId != prevSelectedGrid || --gridMassComputeCooldown <= 0)
                        {
                            prevSelectedGrid = grid.EntityId;
                            gridMassComputeCooldown = (60 * 3) / 10; // divide by 10 because this method executes very 10 ticks
                            gridMassCache = BuildInfoMod.Instance.GridMassCompute.GetGridMass(grid);
                        }

                        sb.ResetFormatting().Separator().Append("Grid mass: ").ExactMassFormat(gridMassCache);
                    }
                }
            }
            #endregion Mass, grid mass

            #region Projector info and status
            if(isProjected && Main.Config.AimInfo.IsSet(AimInfoFlags.Projected))
            {
                // TODO: custom extracted method to be able to compare blocks and not select the projection of the same block that's already placed

                AddLine().Label("Projected by").Append("\"").Color(COLOR_BLOCKTITLE).AppendMaxLength(projectedBy.CustomName, BLOCK_NAME_MAX_LENGTH).ResetFormatting().Append('"');

                StringBuilder sb = AddLine().Label("Status");

                switch(Main.EquipmentMonitor.AimedProjectedCanBuild)
                {
                    case BuildCheckResult.OK:
                        sb.Color(COLOR_GOOD).Append("Ready to build");
                        break;
                    case BuildCheckResult.AlreadyBuilt:
                        sb.Color(COLOR_WARNING).Append("Already built!");
                        break;
                    case BuildCheckResult.IntersectedWithGrid:
                        sb.Color(COLOR_BAD).Append("Other block in the way");
                        break;
                    case BuildCheckResult.IntersectedWithSomethingElse:
                        if(!Utils.CheckSafezoneAction(aimedBlock, SafeZoneAction.BuildingProjections))
                            sb.Color(COLOR_BAD).Append("Can't build projections in this SafeZone");
                        else if(!Utils.CheckSafezoneAction(aimedBlock, SafeZoneAction.Welding))
                            sb.Color(COLOR_BAD).Append("Can't weld in this SafeZone");
                        else
                            sb.Color(COLOR_WARNING).Append("Something in the way");
                        break;
                    case BuildCheckResult.NotConnected:
                        sb.Color(COLOR_WARNING).Append("Nothing to attach to");
                        break;
                    case BuildCheckResult.NotWeldable:
                        sb.Color(COLOR_BAD).Append("Projector doesn't allow building");
                        break;
                    //case BuildCheckResult.NotFound: // not used by CanBuild()
                    default:
                        sb.Color(COLOR_BAD).Append("(Unknown)");
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
                    StringBuilder sb = AddLine().ResetFormatting().Append("Integrity: ").Color(integrityRatio < def.CriticalIntegrityRatio ? COLOR_BAD : (integrityRatio < 1 ? COLOR_WARNING : COLOR_GOOD))
                        .IntegrityFormat(aimedBlock.Integrity).ResetFormatting()
                        .Append(" / ").IntegrityFormat(aimedBlock.MaxIntegrity);

                    if(def.BlockTopology == MyBlockTopology.Cube && aimedBlock.HasDeformation)
                        sb.Color(COLOR_WARNING).Append(" (deformed)");
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
                            sb.Color(COLOR_GOOD);
                        else
                            sb.Color(COLOR_BAD);

                        sb.Append("Faction");
                    }
                    else if(shareMode == MyOwnershipShareModeEnum.None)
                    {
                        if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                            sb.Color(COLOR_WARNING);
                        else
                            sb.Color(COLOR_BAD);

                        sb.Append("Owner");
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
                        StringBuilder sb = AddLine().Append("Completed: ").TimeFormat(currentTime).Color(COLOR_UNIMPORTANT).OptionalMultiplier(MyAPIGateway.Session.WelderSpeedMultiplier).ResetFormatting();

                        if(def.CriticalIntegrityRatio < 1 && integrityRatio < def.CriticalIntegrityRatio)
                        {
                            float funcTime = buildTime * def.CriticalIntegrityRatio * (1 - (integrityRatio / def.CriticalIntegrityRatio));

                            sb.Separator().Append("Functional: ").TimeFormat(funcTime);
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
                        MyRelationsBetweenPlayerAndBlock relation = aimedBlock.FatBlock.GetUserRelationToOwner(localPlayer.IdentityId);
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
                        StringBuilder sb = AddLine().Append("Dismantled: ").TimeFormat(grindTime).Color(COLOR_UNIMPORTANT).OptionalMultiplier(MyAPIGateway.Session.GrinderSpeedMultiplier).ResetFormatting();

                        if(hackable)
                        {
                            sb.Separator().Append("Hacked: ").TimeFormat(hackTime).Color(COLOR_UNIMPORTANT).OptionalMultiplier(MyAPIGateway.Session.HackSpeedMultiplier).ResetFormatting();
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
                    StringBuilder sb = AddLine().Color(COLOR_WARNING);

                    if(hasLinearVel)
                    {
                        sb.Append("Moving: ").SpeedFormat(grid.Physics.LinearVelocity.Length(), 2);
                    }

                    if(hasAngularVel)
                    {
                        if(hasLinearVel)
                            sb.Separator();

                        sb.Append("Rotating: ").RotationSpeed((float)grid.Physics.AngularVelocity.Length(), 2);
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

                        StringBuilder sb = AddLine(FontsHandler.RedSh).Color(speed >= 0.5f ? COLOR_BAD : COLOR_WARNING);

                        sb.Append("Grind impulse: ").SpeedFormat(speed, 5).Append(" (").ForceFormat(impulse).Append(")");
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

                    bool isPublished = modItem.Name != null && modItem.PublishedFileId > 0;

                    ModClickTooltip(modItem.PublishedServiceName, modItem.PublishedFileId, isPublished ? 2 : 1);

                    if(isPublished)
                    {
                        AddLine().Color(COLOR_MOD).Append("       | ").ResetFormatting().Append("ID: ").Append(modItem.PublishedServiceName).Append(":").Append(modItem.PublishedFileId);
                    }
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

        void AppendVoxelPlacement(VoxelPlacementSettings ps, StringBuilder sb)
        {
            // for static vs dynamic:
            // - cubebuilder aimed at grid (regardless of static/dynamic) it uses Static
            // - cubebuilder aimed at voxel in "local grid mode" uses Static, otherwise if you can move it freely on the surface it's Dynamic
            // - cubebuilder aimed at empty space uses Dynamic
            // - grid paste ghost aimed at existing grid it uses Static
            // - grid paste ghost aimed at nothing it uses Dynamic

            // if the block has null def.VoxelPlacement then it falls back to SessionComponents.sbc based on the equipped thing
            // ... with a few hardcoded overrides (these only override the session comp stuff as they're done before TestVoxelPlacement() which overrides from def.VoxelPlacement):
            // - MyCubeBuilder.RequestGridSpawn() forces it to be OutsideVoxel
            // - MyCubeGrid.ShouldBeStatic() forces it to be Volumetric with 0 min and 0 max
            // - MyCubeGrid.TestBlockPlacementArea() forces to Both if dynamic && largegrid

            // NOTE: InVoxel and OutsideVoxel checks if any BB corner is in voxel, so big blocks can still be in voxels but not detected as such.

            if(ps.PlacementMode == VoxelPlacementMode.None)
            {
                sb.Color(COLOR_BAD).Append("Cannot be placed regardless of voxel proximity!");
            }
            else if(ps.PlacementMode == VoxelPlacementMode.Both)
            {
                sb.Color(COLOR_GOOD).Append("No terrain restrictions");
            }
            else if(ps.PlacementMode == VoxelPlacementMode.InVoxel)
            {
                sb.Color(COLOR_BAD).Append("Touching terrain");
            }
            else if(ps.PlacementMode == VoxelPlacementMode.OutsideVoxel)
            {
                sb.Color(COLOR_WARNING).Append("Outside of terrain");
            }
            else if(ps.PlacementMode == VoxelPlacementMode.Volumetric)
            {
                // HACK: because GetVoxelContentInBoundingBox_Fast() returns very weird results
                const float PercentageFix = 0.75f;

                // only Volumetric uses Min/MaxAllowed
                int minP = (int)MathHelper.Clamp((ps.MinAllowed / PercentageFix) * 100, 0, 100);
                int maxP = (int)MathHelper.Clamp((ps.MaxAllowed / PercentageFix) * 100, 0, 100);

                if(minP <= maxP)
                {
                    if(minP > 0)
                    {
                        sb.Color(COLOR_BAD).Append("Has to be ").Append(minP).Append("% to ").Append(maxP).Append("% inside terrain");
                    }
                    else
                    {
                        sb.Color(COLOR_WARNING).Append(100 - maxP).Append("% outside of terrain");
                    }
                }
                else
                {
                    sb.Color(COLOR_BAD).Append($"Weird volumetric - Min: {ps.MinAllowed * 100:0}%, Max: {ps.MaxAllowed * 100:0}%");
                }
            }
            else
            {
                sb.Color(COLOR_BAD).Append($"(Unknown Mode: {ps.PlacementMode})");
            }
        }

        #region Equipped block info generation
        public void GenerateBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            if(Main.Config.PlaceInfo.Value == 0)
                return;

            #region Block name line only for textAPI
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.BlockName) && Main.TextAPI.IsEnabled)
            {
                StringBuilder sb = AddLine().Color(COLOR_BLOCKTITLE).Append(def.DisplayNameText);

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
                        sb.Append("  ").Color(COLOR_BLOCKVARIANTS).Append("(Variant ").Append(blockNumber).Append(" of ").Append(totalBlocks).Append(")");
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
                AddLine().Color(COLOR_INTERNAL).Label("Id").Color(COLOR_NORMAL).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(def.Id.SubtypeName)
                    .Separator().Color(COLOR_INTERNAL).Label("Pair").Color(COLOR_NORMAL).Append(def.BlockPairName);

                StringBuilder tooltip = CreateTooltip(coveringLines: 2);

                StringBuilder sb = AddLine();

                Vector3 offset = def.ModelOffset;
                sb.Color(COLOR_INTERNAL).Label("ModelOffset").Color(offset.LengthSquared() > 0 ? COLOR_WARNING : COLOR_NORMAL).Append("X:").Number(offset.X).Append(" Y:").Number(offset.Y).Append(" Z:").Number(offset.Z)
                    .ResetFormatting().Separator();

                sb.Color(COLOR_INTERNAL).Label("ModelIntersection").Color(def.UseModelIntersection ? COLOR_WARNING : COLOR_NORMAL).Append(def.UseModelIntersection);

                if(tooltip != null)
                {
                    tooltip.Append("These are only shown because you have '").Append(Main.Config.InternalInfo.Name).Append("' setting turned on for this mod.");
                }
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

            #region Optional - voxel placement restrictions
            //if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.placement???))
            {
                var cbDynamic = MyCubeBuilder.CubeBuilderDefinition.BuildingSettings.GetGridPlacementSettings(def.CubeSize, isStatic: false);
                var cbStatic = MyCubeBuilder.CubeBuilderDefinition.BuildingSettings.GetGridPlacementSettings(def.CubeSize, isStatic: true);

                // game will probably crash if cubebuilder defaults don't have values, but checking them anyway
                if(def.VoxelPlacement.HasValue || (cbDynamic.VoxelPlacement.HasValue && cbStatic.VoxelPlacement.HasValue))
                {
                    VoxelPlacementSettings staticSettings;
                    VoxelPlacementSettings dynamicSettings;

                    bool fromCubeBuilder = true;

                    if(def.VoxelPlacement.HasValue)
                    {
                        fromCubeBuilder = false;

                        VoxelPlacementOverride vp = def.VoxelPlacement.Value;
                        staticSettings = vp.StaticMode;
                        dynamicSettings = vp.DynamicMode;
                    }
                    else
                    {
                        staticSettings = cbStatic.VoxelPlacement.Value;

                        // HACK: from MyCubeGrid.TestBlockPlacementArea()
                        if(def.CubeSize == MyCubeSize.Large)
                            dynamicSettings = new VoxelPlacementSettings { PlacementMode = VoxelPlacementMode.Both };
                        else
                            dynamicSettings = cbDynamic.VoxelPlacement.Value;
                    }

                    bool identical = false;
                    if(dynamicSettings.PlacementMode == staticSettings.PlacementMode)
                    {
                        if(staticSettings.PlacementMode == VoxelPlacementMode.Volumetric)
                        {
                            if(dynamicSettings.MinAllowed == staticSettings.MinAllowed && dynamicSettings.MaxAllowed == staticSettings.MaxAllowed)
                                identical = true;
                        }
                        else
                            identical = true;
                    }

                    const int PlaceLimitAsSpaces = 31; // calculated with /bi measure "Voxel placement "

                    StringBuilder sb = AddLine();
                    if(identical)
                    {
                        sb.Label("Voxel placement");
                        AppendVoxelPlacement(staticSettings, sb);
                    }
                    else
                    {
                        sb.Label("Voxel placement - 3D grid");
                        AppendVoxelPlacement(staticSettings, sb);
                    }

                    if(fromCubeBuilder)
                        sb.Append(" <color=gray>(default)");

                    StringBuilder tooltip = CreateTooltip(coveringLines: identical ? 1 : 2);
                    if(tooltip != null)
                    {
                        tooltip.Append("How the block is allowed to be placed in relation to nearby voxels (terrain).")
                               .Append("\n")
                               .Append("\nThere's 2 pairs of settings, one for 3D-grid-aligned and another for free placment, if both have identical settings then only one is printed here.")
                               .Append("\nGrid is used when the block is locked to a 3D grid, either aiming at a cubegrid or using 'Local grid mode' when aiming at terrain.")
                               .Append("\nFree is when not locked to any 3D grid, freely aimed either mid-air or at terrain.")
                               .Append("\n")
                               .Append("\nThe <color=gray>(default)<reset> tag means that these rules are not on this specific block, but from fallback to cubebuilder's generic rules.");
                    }

                    if(!identical)
                    {
                        sb = AddLine().Append(' ', PlaceLimitAsSpaces).Label("- Free");
                        AppendVoxelPlacement(dynamicSettings, sb);

                        if(fromCubeBuilder)
                            sb.Append(" <color=gray>(default)");
                    }
                }
            }
            #endregion Optional - voxel placement restrictions

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

                        SimpleTooltip("These are for upgrade module blocks to be directly mounted onto (no conveyors)."
                                    + "\nWhich module blocks are supported depends on the available upgrades below and the module's provided upgrades.");

                        StringBuilder sb = AddLine().Label(upgrades > 1 ? "Optional upgrades" : "Optional upgrade");
                        const int SpacePadding = 32;
                        const int NumPerRow = 2;

                        for(int i = 0; i < data.Upgrades.Count; i++)
                        {
                            if(i > 0)
                            {
                                if(i % NumPerRow == 0)
                                    sb = AddLine().Color(COLOR_LIST).Append(' ', SpacePadding).Append("| ");
                                else
                                    sb.Separator();
                            }

                            sb.Color(COLOR_GOOD).Append(data.Upgrades[i]).ResetFormatting();
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

                MyObjectBuilder_Checkpoint.ModItem modItem = def.Context.ModItem;
                ModClickTooltip(modItem.PublishedServiceName, modItem.PublishedFileId);
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
        void AppendBasics(MyCubeBlockDefinition def, bool part = false)
        {
            bool deformable = (def.BlockTopology == MyBlockTopology.Cube && def.UsesDeformation);
            bool buildModels = (def.BuildProgressModels != null && def.BuildProgressModels.Length > 0);

            BData_Base data = Main.LiveDataHandler.Get<BData_Base>(def);

            #region DisassembleRatio
            float disassembleRatio;
            if(data != null)
            {
                disassembleRatio = data.DisassembleRatio;
                // MyADvancedDoor uses x1 for open and x3.3 for closed... this data should have it from closed state.
            }
            else
            {
                disassembleRatio = def.DisassembleRatio;
                if(def is MyDoorDefinition)
                    disassembleRatio *= Hardcoded.Door_DisassembleRatioMultiplier;
                else if(def is MyAdvancedDoorDefinition)
                    disassembleRatio *= Hardcoded.AdvDoor_Closed_DisassembleRatioMultiplier;
            }
            #endregion

            float weldPerSec = Hardcoded.HandWelder_GetWeldPerSec(1f);
            float grindPerSec = Hardcoded.HandGrinder_GetGrindPerSec(1f);
            float weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
            float grindMul = MyAPIGateway.Session.GrinderSpeedMultiplier;

            float weldTime = def.MaxIntegrity / (def.IntegrityPointsPerSec * weldPerSec);
            float grindTime = def.MaxIntegrity / (def.IntegrityPointsPerSec * (grindPerSec / disassembleRatio));

            string partPrefix = string.Empty;
            if(part)
            {
                StringBuilder line = AddLine(FontsHandler.SkyBlueSh).Color(COLOR_PART).Label("Part").Append(def.DisplayNameText);
                partPrefix = (Main.TextAPI.IsEnabled ? "<color=55,255,155>        | <reset>" : "       | ");
                Utilities.StringBuilderExtensions.CurrentColor = COLOR_NORMAL;

                if(Main.Config.InternalInfo.Value)
                {
                    int obPrefixLen = "MyObjectBuilder_".Length;
                    string typeIdString = def.Id.TypeId.ToString();

                    line.ResetFormatting()
                        .Separator().Color(COLOR_INTERNAL).Label("Id").Color(COLOR_NORMAL).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(def.Id.SubtypeName)
                        .Separator().Color(COLOR_INTERNAL).Label("Pair").Color(COLOR_NORMAL).Append(def.BlockPairName);
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Line1))
            {
                StringBuilder line = AddLine().Append(partPrefix);

                // HACK: game doesn't use mass from blocks with HasPhysics=false
                line.Color(new Color(200, 255, 55)).ExactMassFormat(def.HasPhysics ? def.Mass : 0).ResetFormatting().Separator();

                line.Size3DFormat(def.Size).Separator();

                line.Label("Weld").TimeFormat(weldTime).Color(COLOR_UNIMPORTANT).OptionalMultiplier(weldMul).ResetFormatting();

                if(Math.Abs(grindTime - weldTime) >= 0.0001f)
                {
                    line.Separator().Color(grindTime > weldTime ? COLOR_WARNING : COLOR_NORMAL).Label("Grind").TimeFormat(grindTime).Color(COLOR_UNIMPORTANT).OptionalMultiplier(grindMul).ResetFormatting();
                }

                SimpleTooltip("Weld and grind times are for lowest tier tools and non-enemy blocks."
                           + $"\nFor blocks owned by enemies, grind speed is multiplied by {MyAPIGateway.Session.HackSpeedMultiplier:0.##} until blue hack line.");
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Line2))
            {
                StringBuilder line = AddLine().Append(partPrefix).Label("Integrity").Append(def.MaxIntegrity.ToString("#,###,###,###,###"));

                if(deformable)
                    line.Separator().Label("Deform Ratio").RoundedNumber(def.DeformationRatio, 2);

                float dmgMul = def.GeneralDamageMultiplier;
                if(dmgMul != 1f)
                {
                    line.Separator();
                    DamageMultiplierAsResistance(dmgMul);
                }

                // .DamageThreshold and .DetonateChance are for cargo+ammo detonation but are NOT used

                // TODO: improve formatting?
                // HACK: DamageMultiplierExplosion is only used if block has a FatBlock and it's applied after the damage event.
                float expDmgMul = def.DamageMultiplierExplosion;
                if(expDmgMul != 1f && !string.IsNullOrEmpty(def.Model)) // having an independent model makes it have a fatblock
                {
                    line.Separator();
                    DamageMultiplierAsResistance(expDmgMul, "Explosive Res");
                }

                if(!Hardcoded.CanThrustDamageBlock(MyCubeSize.Small, def))
                {
                    line.Separator().Color(COLOR_GOOD).Label("Thrust Res").Append("Small");

                    SimpleTooltip("Large-grid thrusters damage all blocks." +
                        "\nSmall-grid thrusters only damage blocks with DeformationRatio larger than 0.25." +
                        "\nCaution: Thrust flames penetrate and will damage blocks behind resistant blocks.");
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
                        StringBuilder extra = AddLine().Color(COLOR_WARNING).Append("Different mount points in build stage!").ResetFormatting().Append(" (");
                        Main.Config.ConstructionModelPreviewBind.Value.GetBinds(extra, ControlContext.BUILD, specialChars: true);
                        extra.Append(" and ");
                        Main.Config.CycleOverlaysBind.Value.GetBinds(extra, ControlContext.BUILD, specialChars: true);
                        extra.Append(" to see)").ResetFormatting();
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
                StringBuilder sb = AddLine().Append(partPrefix);

                if(def.BlockTopology == MyBlockTopology.Cube && string.IsNullOrEmpty(def.Model)) // deformable armor can't be targeted as targets require to be entities
                {
                    sb.Color(COLOR_GOOD).Append("Not targetable");
                }
                else if(def.PriorityModifier == 0f) // similar check the game does in MyLargeTurretTargetingSystem.TestPotentialTarget()
                {
                    sb.Color(COLOR_GOOD).Append("Not targetable");
                }
                else
                {
                    int groups = 0;

                    if(def.TargetingGroups != null && def.TargetingGroups.Count > 0)
                    {
                        groups = def.TargetingGroups.Count;
                    }
                    else
                    {
                        foreach(MyTargetingGroupDefinition group in BuildInfoMod.Instance.Caches.OrderedTargetGroups)
                        {
                            if(group.DefaultBlockTypes.Contains(def.Id.TypeId))
                            {
                                groups++;
                            }
                        }
                    }

                    if(groups > 0)
                    {
                        sb.Color(COLOR_WARNING).Label(groups > 1 ? "Targetable - Groups" : "Targetable - Group");

                        int atLabelLen = sb.Length;

                        if(def.TargetingGroups != null)
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

                        sb.Separator().Label("Priority").MultiplierFormat(def.PriorityModifier);

                        // HACK: from MyLargeTurretTargetingSystem.TestPotentialTarget()
                        // HACK: MyFunctionalBlockDefinition is also used by non-MyFunctionalBlock types...
                        if(MyAPIGateway.Reflection.IsAssignableFrom(typeof(MyObjectBuilder_FunctionalBlock), def.Id.TypeId))
                        {
                            // and MyLargeTurretTargetingSystem.IsTarget() excludes broken blocks so that only leaves IsWorking as off or unpowered
                            sb.Separator().MultiplierFormat(def.PriorityModifier * def.NotWorkingPriorityMultiplier).Append(" when off");
                        }
                    }
                    else
                    {
                        sb.Color(COLOR_GOOD).Append("Not targetable");
                    }
                }

                SimpleTooltip("Whether this block can be targeted by automated enemy turrets." +
                              "\nThe groups affect if turrets aim for a specific type of target." +
                              "\nIf the block is not targetable it does not mean it is safe, turrets will shoot through it to reach targetable blocks." +
                              "\nTargetable blocks that are damaged below functional state (red line) will become non-targetable.");
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

            if(def.MountPoints != null && Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                bool hasMask = false;
                bool hasCoupling = false;

                foreach(MyCubeBlockDefinition.MountPoint mp in def.MountPoints)
                {
                    if(!mp.Enabled)
                        continue;

                    if(mp.PropertiesMask != 0 || mp.ExclusionMask != 0)
                    {
                        hasMask = true;
                    }

                    if(!string.IsNullOrEmpty(mp.CouplingTag))
                    {
                        hasCoupling = true;
                    }
                }

                if(hasCoupling || hasMask)
                {
                    StringBuilder sb = AddLine().Color(COLOR_WARNING).Append("Mountpoints have ");

                    if(hasCoupling)
                        sb.Append("coupling rules, ");

                    if(hasMask)
                        sb.Append("mask rules, ");

                    sb.Length -= 2;
                    sb.Append(". See overlays.");
                }
            }
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
            Add(typeof(MyObjectBuilder_BroadcastController), Format_BroadcastController);
            Add(typeof(MyObjectBuilder_TransponderBlock), Format_Transponder);

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

            Add(typeof(MyObjectBuilder_Decoy), Format_Decoy);

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

        void Add(MyObjectBuilderType blockType, TextGenerationCall call)
        {
            formatLookup.Add(blockType, call);
        }

        void Format_TerminalBlock(MyCubeBlockDefinition def)
        {
            // HACK hardcoded; control panel doesn't use power
            PowerRequired(0, null, powerHardcoded: true);
        }

        #region Conveyors
        void Format_Conveyors(MyCubeBlockDefinition def)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                StringBuilder sb = AddLine().LabelHardcoded("Power required");

                sb.PowerFormat(Hardcoded.Conveyors_PowerReqPerGrid).Append(" per grid (regardless of conveyor presence)");

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    AddLine().Append("    ").ResourcePriority(Hardcoded.Conveyors_PowerGroup, hardcoded: true);
            }
        }

        void Format_Connector(MyCubeBlockDefinition def)
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
                    {
                        sb.Append("Yes").Separator().Label("Port size").Append(data.IsSmallConnector ? "Small" : "Large");
                        SimpleTooltip("Connectors can only connect to other connectors that have the same size port.");

                        AddLine().LabelHardcoded("Connect limits - Max distance").DistanceFormat(Hardcoded.Connector_ConnectMaxDistance).Separator().LabelHardcoded("Max angle").AngleFormat(Hardcoded.Connector_ConnectAngleMinMax);
                        SimpleTooltip("Connectors will only magnetize under these conditions relative to another." +
                                      "\nDistance is checked from connecting position and angle is offset between connectors' Forward, resulting in a cone.");
                    }
                    else
                    {
                        sb.Append("No");
                    }

                    AddLine().LabelHardcoded("Can throw out items").Append("Yes");
                }
            }
        }

        void Format_CargoAndCollector(MyCubeBlockDefinition def)
        {
            MyCargoContainerDefinition cargo = (MyCargoContainerDefinition)def;

            MyPoweredCargoContainerDefinition poweredCargo = def as MyPoweredCargoContainerDefinition; // collector
            if(poweredCargo != null)
            {
                PowerRequired(poweredCargo.RequiredPowerInput, poweredCargo.ResourceSinkGroup);
            }

            InventoryStats(def, alternateVolume: cargo.InventorySize.Volume);
        }

        void Format_ConveyorSorter(MyCubeBlockDefinition def)
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

        void Format_Piston(MyCubeBlockDefinition def)
        {
            MyPistonBaseDefinition piston = (MyPistonBaseDefinition)def;

            PowerRequired(piston.RequiredPowerInput, piston.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Extended length").DistanceFormat(piston.Maximum).Separator().Label("Max velocity").DistanceFormat(piston.MaxVelocity);

                // HACK: hardcoded max unsafe from MyPistonBase.CreateTerminalControls()
                // there is piston.MaxImpulse but it's not used
                AddLine().Label("Max Force, Safe").ForceFormat(piston.UnsafeImpulseThreshold).Separator().Label("Unsafe").ForceFormat(float.MaxValue);
            }

            Suffix_Mechanical(def, piston.TopPart);
        }

        void Format_Rotor(MyCubeBlockDefinition def)
        {
            MyMotorStatorDefinition motor = (MyMotorStatorDefinition)def;
            MyMotorSuspensionDefinition suspension = def as MyMotorSuspensionDefinition;
            if(suspension != null)
            {
                if(PowerRequired2("Idle", suspension.RequiredIdlePowerInput, "Max", suspension.RequiredPowerInput, suspension.ResourceSinkGroup))
                {
                    SimpleTooltip("The power usage scales linearly between Idle and Max based on Power slider (propulsion force) and pedal (input or override)." +
                                  "\nPower usage can drop if the motor isn't required to put much effort into it, for example if it's freely spinning it would go back to Idle usage.");
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

        void Suffix_Mechanical(MyCubeBlockDefinition def, string topPart)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PartStats))
            {
                MyCubeBlockDefinitionGroup group = MyDefinitionManager.Static.TryGetDefinitionGroup(topPart);
                MyCubeBlockDefinition partDef = (def.CubeSize == MyCubeSize.Large ? group?.Large : group?.Small);

                if(partDef != null)
                    AppendBasics(partDef, part: true);
                else
                    AddLine().Color(COLOR_BAD).Append("No attachable part declared!");
            }
        }

        void Format_MergeBlock(MyCubeBlockDefinition def)
        {
            MyMergeBlockDefinition merge = (MyMergeBlockDefinition)def;

            // HACK hardcoded; MergeBlock doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float maxStrengthPercent = merge.Strength * (def.CubeSize == MyCubeSize.Large ? Hardcoded.Merge_StrengthMulLargeGrid : Hardcoded.Merge_StrengthMulSmallGrid) * 100;
                AddLine().Label("Pull strength").Append(maxStrengthPercent.ToString("###,###,##0.#######")).Append("% of the other ship mass");
                SimpleTooltip("Gets reduced the farther it is from the other mergeblock and the faster they move towards eachother.");
            }
        }

        void Format_LandingGear(MyCubeBlockDefinition def)
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
        void Format_Drill(MyCubeBlockDefinition def)
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

        void Format_WelderAndGrinder(MyCubeBlockDefinition def)
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

        void Format_ShipController(MyCubeBlockDefinition def)
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

        void Format_Thrust(MyCubeBlockDefinition def)
        {
            MyThrustDefinition thrust = (MyThrustDefinition)def;

            float effMulMax = Math.Max(thrust.EffectivenessAtMinInfluence, thrust.EffectivenessAtMaxInfluence);
            float minPowerUsage = thrust.MinPowerConsumption; // not affected by effectiviness
            float maxPowerUsage = thrust.MaxPowerConsumption * effMulMax;

            if(thrust.FuelConverter != null && !thrust.FuelConverter.FuelId.IsNull() && thrust.FuelConverter.FuelId != MyResourceDistributorComponent.ElectricityId)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
                {
                    MyGasProperties fuelDef;
                    if(MyDefinitionManager.Static.TryGetDefinition(thrust.FuelConverter.FuelId, out fuelDef))
                    {
                        // HACK formula from MyEntityThrustComponent.PowerAmountToFuel()
                        float eff = (fuelDef.EnergyDensity * thrust.FuelConverter.Efficiency);
                        float minFuelUsage = minPowerUsage / eff;
                        float maxFuelUsage = maxPowerUsage / eff;

                        AddLine().Label("Requires").Append(thrust.FuelConverter.FuelId.SubtypeId);

                        if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                            GetLine().Separator().ResourcePriority(thrust.ResourceSinkGroup);

                        AddLine().Label("Consumption - Max").VolumeFormat(maxFuelUsage).Append("/s").Separator().Label("Idle").VolumeFormat(minFuelUsage).Append("/s");
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

                    AddLine().Label("Consumption - Max").PowerFormat(maxPowerUsage).Separator().Label("Idle").PowerFormat(minPowerUsage);
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
                float forceN = thrust.ForceMagnitude * effMulMax;

                StringBuilder line = AddLine().Label("Force").ForceFormat(forceN);

                // TODO: thrust-to-weight ratio useful?
                //float twr = float.PositiveInfinity;
                //if(def.HasPhysics)
                //    twr = forceN / (def.Mass * Hardcoded.EarthGravity);
                //
                //line.Separator().Label("Thrust-to-weight ratio").RoundedNumber(twr, 4);

                if(Math.Abs(thrust.SlowdownFactor - 1) > 0.001f)
                {
                    line.Separator().Color(COLOR_WARNING).Label("Dampeners").Append("x").RoundedNumber(thrust.SlowdownFactor, 2);

                    // HACK: from MyThrusterBlockThrustComponent.RegisterLazy()
                    SimpleTooltip("This thruster has different force multiplier when dampeners trigger the thruster, called SlowdownFactor internally." +
                                 "\nNOTE: The highest SlowdownFactor from all thrusters is used for all thrusters!");
                }

                //SimpleTooltip($"Thrust-to-weight is shown in Earth gravity ({Hardcoded.EarthGravity:0.##}m/s/s)");

                AddLine().Label("Limits");
                const int PrefixSpaces = 11;

                if(thrust.EffectivenessAtMinInfluence != 1.0f || thrust.EffectivenessAtMaxInfluence != 1.0f)
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

                    thrustAtMinAir /= effMulMax;
                    thrustAtMaxAir /= effMulMax;

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
                        GetLine().Color(thrustAtMaxAir < 1f ? COLOR_WARNING : COLOR_GOOD)
                            .ProportionToPercent(thrustAtMaxAir).Append(" thrust ")
                            // no "in atmosphere" because it needs to explicitly state that it expects 100% air density, which some planets do not have (like Mars)
                            .Append("in ").ProportionToPercent(maxAir).Append(" air density");

                        AddLine().Append(' ', PrefixSpaces).Append("| ")
                            .Color(thrustAtMinAir < 1f ? COLOR_WARNING : COLOR_GOOD)
                            .ProportionToPercent(thrustAtMinAir).Append(" thrust ");
                        if(minAir <= 0)
                            GetLine().Append("in vacuum");
                        else
                            GetLine().Append("below ").ProportionToPercent(minAir).Append(" air density");
                    }
                }
                else
                {
                    GetLine().Color(COLOR_GOOD).Append("full thrust in atmosphere and vacuum");
                }

                SimpleTooltip("Thrust power linearly scales between the indicated air density values.");
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                BData_Thrust data = Main.LiveDataHandler.Get<BData_Thrust>(def);
                if(data != null)
                {
                    //AddLine().Label("Flames").Append(data.Flames.Count)
                    //    .Separator().Label("Longest").DistanceFormat(data.LongestFlame, 2)
                    //    .Append(" (").DistanceFormat(data.LongestFlamePastEdge).Append(" past cube edge)");

                    AddLine().Label("Damage - To Ships").Number(data.DamagePerTickToBlocks * Constants.TicksPerSecond).Append("/s")
                        .Separator().Label("To Others").Number(data.DamagePerTickToOther * Constants.TicksPerSecond).Append("/s");

                    var tooltip = CreateTooltip(coveringLines: 2);
                    if(tooltip != null)
                    {
                        int rangeMinPercent = (int)(Hardcoded.Thrust_DamageRangeRandomMin * 100);

                        // HACK: from MyThrust.ThrustDamageDealDamage()
                        tooltip.Append("Voxels are not damaged by thrusters. The \"To Others\" are: characters, loose items, missiles and meteorites.");
                        tooltip.Append("\nFor blocks, damage is dealt for each physics shape that intersects with the flame capsule, but only if the block boundingbox is within the cylinder.");
                        tooltip.Append("\n  e.g. Ship Welder has 3 physics shapes which means it can take up to 3 times the damage.");
                        tooltip.Append("\nThruster update rate varies, however it damages for the right amount for the game-time that passed.");
                        tooltip.Append('\n');
                        tooltip.Append("\nThe range is for blocks only. There's an extra ").DistanceFormat(data.LongestFlameCapsuleRadius, 4).Append(" on this block that can damage everything else. See the overlay for visualization.");
                        tooltip.Append("\nThe flame range is random between ").Append(rangeMinPercent).Append("% and 100% (on all thrusters) which means there's a lower chance to hit blocks the farther they are.");
                        tooltip.Append('\n');
                        tooltip.Append("\nIn the overlay, the entire capsule damages everything except blocks, while only the cylinder is used for blocks.");
                    }

                    float cellSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    float flameRangeInBlocks = (data.LongestFlame / cellSize);

                    StringBuilder line = AddLine()
                        .Label("Damage - Range").DistanceFormat(data.LongestFlame, 4).Append(" past edge (").Append(flameRangeInBlocks.ToString("0.##")).Append(" blocks)");

                    if(data.Flames.Count > 1)
                        line.Separator().Append("From ").Append(data.Flames.Count).Append(" different points");
                }

                if(!MyAPIGateway.Session.SessionSettings.ThrusterDamage)
                    AddLine().Color(Color.Green).Append("Thruster damage is disabled in this world");
            }
        }

        void Format_Gyro(MyCubeBlockDefinition def)
        {
            MyGyroDefinition gyro = (MyGyroDefinition)def;

            PowerRequired(gyro.RequiredPowerInput, gyro.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Torque").TorqueFormat(gyro.ForceMagnitude);
            }
        }

        void Format_Light(MyCubeBlockDefinition def)
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
                    BData_Light data = Main.LiveDataHandler.Get<BData_Light>(def);
                    if(data != null && data.LightLogicData.HasSubpartLights)
                    {
                        float min = spotLight.RotationSpeedBounds.Min * Hardcoded.Spotlight_RadiansPerSecondMul;
                        float max = spotLight.RotationSpeedBounds.Max * Hardcoded.Spotlight_RadiansPerSecondMul;
                        float rotationDefault = spotLight.RotationSpeedBounds.Default * Hardcoded.Spotlight_RadiansPerSecondMul;
                        AddLine().Append("Rotation speed: ").RotationSpeed(min, 0).Append(" to ").RotationSpeed(max, 0).Separator().Append("Default: ").RotationSpeed(rotationDefault, 0);
                    }
                }
            }
        }

        void Format_OreDetector(MyCubeBlockDefinition def)
        {
            MyOreDetectorDefinition oreDetector = (MyOreDetectorDefinition)def;

            PowerRequired(Hardcoded.OreDetector_PowerReq, oreDetector.ResourceSinkGroup, powerHardcoded: true);

            StringBuilder sb = AddLine().Label("Max range").DistanceFormat(oreDetector.MaximumRange);

            if(def.ModelOffset.LengthSquared() > 0)
                sb.Separator().Label("Offset").VectorOffsetFormat(def.ModelOffset);
        }

        void Format_Projector(MyCubeBlockDefinition def)
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
        void Format_Door(MyCubeBlockDefinition def)
        {
            MyDoorDefinition door = (MyDoorDefinition)def;

            PowerRequired(Hardcoded.Door_PowerReq, door.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float moveTime = Hardcoded.Door_MoveSpeed(door.OpeningSpeed, door.MaxOpen);
                AddLine().Label("Move time").TimeFormat(moveTime).Separator().Label("Distance").DistanceFormat(door.MaxOpen);
            }
        }

        void Format_AirtightDoor(MyCubeBlockDefinition def)
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

        void Format_AdvancedDoor(MyCubeBlockDefinition def)
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

        void Format_Ladder(MyCubeBlockDefinition def)
        {
            PowerRequired(0, null, powerHardcoded: true);

            BData_Ladder data = Main.LiveDataHandler.Get<BData_Ladder>(def);
            if(data != null)
            {
                float climbSpeed = Hardcoded.LadderClimbSpeed(data.DistanceBetweenPoles);
                AddLine().Label("Climb speed").SpeedFormat(climbSpeed);
            }
        }

        void Format_Parachute(MyCubeBlockDefinition def)
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

        void Format_MedicalRoom(MyCubeBlockDefinition def)
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
        void Format_Production(MyCubeBlockDefinition def)
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

                    AddLine().Append("Assembly speed: ").ProportionToPercent(assembler.AssemblySpeed * mulSpeed).Color(COLOR_UNIMPORTANT).OptionalMultiplier(mulSpeed).ResetFormatting()
                        .Separator().Append("Efficiency: ").ProportionToPercent(mulEff);

                    SimpleTooltip($"Assembler speed is from the block multiplied by the world setting ({assembler.AssemblySpeed:0.##} * {mulSpeed:0.##})."
                                + $"\nAssembler efficiency is entirely the world setting.");
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
                    float mulSpeed = MyAPIGateway.Session.RefinerySpeedMultiplier;
                    float matEff = refinery.MaterialEfficiency;

                    AddLine().Append("Refine speed: ").ProportionToPercent(refinery.RefineSpeed * mulSpeed).Color(COLOR_UNIMPORTANT).OptionalMultiplier(mulSpeed).ResetFormatting()
                        .Separator().Append("Efficiency: ").ProportionToPercent(matEff);

                    SimpleTooltip($"Refinery speed is from the block multiplied by the world setting ({refinery.RefineSpeed:0.##} * {mulSpeed:0.##})."
                                + $"\nRefinery efficiency is entirely per-block, but might also be modified by attached upgrade module blocks.");
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
                    AddLine().Color(COLOR_WARNING).Label("Destroyed Explosion - Max Damage").ScientificNumber(gasTank.GasExplosionDamageMultiplier * gasTank.Capacity)
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

        void Format_OxygenFarm(MyCubeBlockDefinition def)
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

                // HACK: from MySolarGameLogicComponent
                const int pivots = 8;
                const float time = pivots * (100f / 60f); // uses update100()

                AddLine().LabelHardcoded("Zero to Max output").Append("roughly ").TimeFormat(time);
                SimpleTooltip($"Approximate time it takes to evaluate if all {pivots} points on the block have line of sight to the Sun.");
            }
        }

        void Format_AirVent(MyCubeBlockDefinition def)
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

        void Format_UpgradeModule(MyCubeBlockDefinition def)
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

        void Format_PowerProducer(MyCubeBlockDefinition def)
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
                    // battery.AdaptibleInput is not used anywhere, ignoring.

                    StringBuilder sb = AddLine().Append("Power input: ").PowerFormat(battery.RequiredPowerInput).IsPowerAdaptable(battery.ResourceSinkGroup, showNotAdaptable: true);

                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        sb.Separator().ResourcePriority(battery.ResourceSinkGroup);
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

                    // HACK: from MySolarPanel.Init() and MySolarGameLogicComponent
                    int pivots = 8;
                    if(solarPanel.Pivots != null)
                    {
                        pivots = solarPanel.Pivots.Length;
                        if(solarPanel.IsTwoSided)
                            pivots /= 2;
                    }

                    float time = pivots * (100f / 60f); // uses update100()

                    AddLine().LabelHardcoded("Zero to Max output").Append("roughly ").TimeFormat(time);
                    SimpleTooltip($"Approximate time it takes to evaluate if all {pivots} points on the block have line of sight to the Sun.");
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

                    AddLine().Label("Clearance - Terrain").DistanceRangeFormat(groundMin, groundMax).Separator().Label("Sides").DistanceRangeFormat(sideMin, sideMax);

                    AddLine().Label("Optimal wind speed").RoundedNumber(windTurbine.OptimalWindSpeed, 2);
                    // TODO: wind speed unit? noone knows...
                }

                return;
            }
        }
        #endregion Production

        #region Communication
        void Format_RadioAntenna(MyCubeBlockDefinition def)
        {
            var radioAntenna = def as MyRadioAntennaDefinition;
            if(radioAntenna == null)
                return;

            PowerRequired(Hardcoded.RadioAntenna_PowerReq(radioAntenna.MaxBroadcastRadius), radioAntenna.ResourceSinkGroup, powerHardcoded: true);

            StringBuilder sb = AddLine().Label("Max radius").DistanceFormat(radioAntenna.MaxBroadcastRadius);

            if(def.ModelOffset.LengthSquared() > 0)
                sb.Separator().Label("Offset").VectorOffsetFormat(def.ModelOffset);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                // from MyRadioAntenna.GetRodRadius()
                float lightningCatchRadius = def.CubeSize == MyCubeSize.Small ? radioAntenna.LightningRodRadiusSmall : radioAntenna.LightningRodRadiusLarge; // bonkers

                sb = AddLine().Label("Lightning attraction").DistanceFormat(lightningCatchRadius).Separator().Label("Damage");
                if(Main.Caches.LightningMinDamage == Main.Caches.LightningMaxDamage)
                    sb.Append(Main.Caches.LightningMinDamage);
                else
                    sb.Append(Main.Caches.LightningMinDamage).Append(" to ").Append(Main.Caches.LightningMaxDamage);

                SimpleTooltip(LightningAttractionTooltip);
            }
        }

        void Format_LaserAntenna(MyCubeBlockDefinition def)
        {
            MyLaserAntennaDefinition laserAntenna = (MyLaserAntennaDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Active").PowerFormat(Hardcoded.LaserAntenna_PowerUsage(laserAntenna, 1000)).Append(" per km");

                SimpleTooltip("Laser antenna power usage is linear up to 200km, after that it's a quadratic ecuation."
                            + $"\nTo calculate it at your needed distance, hold a laser antenna block and type in chat: <color=0,255,155>{Main.ChatCommandHandler.CommandLaserPower.PrimaryCommand} <km>");

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

        void Format_Beacon(MyCubeBlockDefinition def)
        {
            MyBeaconDefinition beacon = (MyBeaconDefinition)def;

            PowerRequired(Hardcoded.Beacon_PowerReq(beacon), beacon.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max radius").DistanceFormat(beacon.MaxBroadcastRadius);
            }
        }

        void Format_BroadcastController(MyCubeBlockDefinition def)
        {
            MyBroadcastControllerDefinition broadcaster = (MyBroadcastControllerDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                PowerRequired(broadcaster.RequiredPowerInput, broadcaster.ResourceSinkGroup);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Offline owner max distance").DistanceFormat(MyAPIGateway.Session.SessionSettings.BroadcastControllerMaxOfflineTransmitDistance);

                SimpleTooltip("If the owner of this block is not online then this block will be limited to this distance over antennas.\nNOTE: This is a world setting.");

                // MyChatBroadcastEntityComponent.Init() - uses ConfigDedicated.SpamMessagesTime if on a DS, which clients cannot retrieve
                //AddLine().Label("Anti-spam time").TimeFormat(0.5f);

                AddLine().LabelHardcoded("Max queued messages").Number(Hardcoded.BroadcastController_MaxMessageCount);
                SimpleTooltip("It won't show messages more frequent than every 0.5 seconds (or dedicated server's SpamMessagesTime which is inaccessible from clients)." +
                              "\nBecause of that it adds them to a queue instead of just ignoring them, and that queue is limited to this many messages.");

                AddLine().LabelHardcoded("Max message size").Number(Hardcoded.BroadcastController_MaxMessageLength).Append(" characters");
            }
        }

        void Format_Transponder(MyCubeBlockDefinition def)
        {
            MyTransponderBlockDefinition transponder = (MyTransponderBlockDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                PowerRequired(transponder.RequiredPowerInput, transponder.ResourceSinkGroup);
            }
        }
        #endregion Communication

        void Format_Timer(MyCubeBlockDefinition def)
        {
            MyTimerBlockDefinition timer = (MyTimerBlockDefinition)def;

            PowerRequired(Hardcoded.Timer_PowerReq, timer.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Timer range").TimeFormat(timer.MinDelay / 1000f).Append(" to ").TimeFormat(timer.MaxDelay / 1000f);
            }
        }

        void Format_ProgrammableBlock(MyCubeBlockDefinition def)
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

        void Format_LCD(MyCubeBlockDefinition def)
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

                AddLine().Label("LCD - Resolution").Color(COLOR_HIGHLIGHT).Number(info.SurfaceSize.X).ResetFormatting().Append(" x ").Color(COLOR_HIGHLIGHT).Number(info.SurfaceSize.Y).ResetFormatting()
                    .Separator().Label("Rotatable").BoolFormat(supportsRotation)
                    .Separator().Label("Font size limits").RoundedNumber(lcd.MinFontSize, 2).Append(" to ").RoundedNumber(lcd.MaxFontSize, 2);

                // HACK: default TSS does not work on MyTextPanel, omitting this until it's fixed.
                //if(!string.IsNullOrEmpty(script))
                //{
                //    SimpleTooltip($"Extra info for surface:"
                //                + $"\nUses an LCD app by default: {LCDScriptPrettyName(script)}");
                //}

                Hardcoded.LCDRenderDistanceInfo lcdRenderDistanceInfo = Hardcoded.TextSurface_MaxRenderDistance(def);

                AddLine().LabelHardcoded("LCD - Render").DistanceFormat(lcdRenderDistanceInfo.RenderDistanceRaw * lcdRenderDistanceInfo.TextureQualityMultiplier).Separator().LabelHardcoded("Sync").DistanceFormat(Hardcoded.TextSurfaceMaxSyncDistance);

                SimpleTooltip("LCD render distance is affected by game's Texture Quality setting, 2/3 for medium and 1/3 for low."
                            + $"\nThe value you see is already modified by your current texture setting (x{lcdRenderDistanceInfo.TextureQualityMultiplier:0.##})."
                            + $"\nThe raw render distance for this LCD is: {lcdRenderDistanceInfo.RenderDistanceRaw:0.##} m");
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
            StringBuilder line = AddLine().Label(surfaces.Count > 1 ? "LCDs" : "LCD");

            // TODO: toggle between list and just count?
            for(int i = 0; i < surfaces.Count; i++)
            {
                ScreenArea surface = surfaces[i];

                string displayName = MyTexts.GetString(MyStringId.GetOrCompute(surface.DisplayName));

                Hardcoded.TextSurfaceInfo info = Hardcoded.TextSurface_GetInfo(surface.ScreenWidth, surface.ScreenHeight, surface.TextureResolution);

                if(i > 0)
                    line = AddLine().Append(' ', SpacePrefix).Append("| ");

                line.Append(displayName);

                line.ResetFormatting().Separator().Color(COLOR_HIGHLIGHT).Number(info.SurfaceSize.X).ResetFormatting().Append(" x ").Color(COLOR_HIGHLIGHT).Number(info.SurfaceSize.Y).ResetFormatting();

                if(!string.IsNullOrEmpty(surface.Script))
                {
                    SimpleTooltip($"Extra info for surface '{displayName}':"
                                + $"\nUses an LCD app by default: {LCDScriptPrettyName(surface.Script)}");
                }
            }

            Hardcoded.LCDRenderDistanceInfo lcdRenderDistanceInfo = Hardcoded.TextSurface_MaxRenderDistance(fbDef);

            AddLine().LabelHardcoded("LCD - Render").DistanceFormat(lcdRenderDistanceInfo.RenderDistanceRaw * lcdRenderDistanceInfo.TextureQualityMultiplier).Separator().LabelHardcoded("Sync").DistanceFormat(Hardcoded.TextSurfaceMaxSyncDistance);

            SimpleTooltip("LCD render distance is affected by game's Texture Quality setting, 2/3 for medium and 1/3 for low."
                        + $"\nThe value you see is already modified by your current texture setting (x{lcdRenderDistanceInfo.TextureQualityMultiplier:0.##})."
                        + $"\nThe raw render distance for this LCD is: {lcdRenderDistanceInfo.RenderDistanceRaw:0.##} m");
        }

        string LCDScriptPrettyName(string scriptId)
        {
            if(scriptId.StartsWith("TSS_"))
            {
                string langKey = $"DisplayName_{scriptId}";
                string displayName = MyTexts.GetString(langKey);

                if(langKey == displayName) // no localization found
                    return scriptId.Substring("TSS_".Length);
                else
                    return displayName;
            }
            else // likely a mod one, can't guess name
            {
                return scriptId;
            }
        }

        void Format_LCDPanels(MyCubeBlockDefinition def)
        {
            MyLCDPanelsBlockDefinition panel = (MyLCDPanelsBlockDefinition)def;

            PowerRequired(panel.RequiredPowerInput, panel.ResourceSinkGroup);

            // LCD stats are in AddScreenInfo()
        }

        void Format_SoundBlock(MyCubeBlockDefinition def)
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

        void Format_Sensor(MyCubeBlockDefinition def)
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

        void Format_Camera(MyCubeBlockDefinition def)
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
                AddLine().Label("Field of view").AngleFormat(camera.MaxFov).Append(" to ").AngleFormat(camera.MinFov);
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

        void Format_Button(MyCubeBlockDefinition def)
        {
            MyButtonPanelDefinition button = (MyButtonPanelDefinition)def;

            PowerRequired(Hardcoded.ButtonPanel_PowerReq, button.ResourceSinkGroup, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Button count").Append(button.ButtonCount);
            }
        }

        #region Magic blocks
        void Format_GravityGenerator(MyCubeBlockDefinition def)
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

        void Format_ArtificialMass(MyCubeBlockDefinition def)
        {
            MyVirtualMassDefinition artificialMass = (MyVirtualMassDefinition)def;

            PowerRequired(artificialMass.RequiredPowerInput, artificialMass.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Artificial weight").ExactMassFormat(artificialMass.VirtualMass);
                SimpleTooltip("A force that gets applied directly to the grid at the block position by artificial gravity generators.");
            }
        }

        void Format_SpaceBall(MyCubeBlockDefinition def)
        {
            MySpaceBallDefinition spaceBall = (MySpaceBallDefinition)def; // this doesn't extend MyVirtualMassDefinition

            // HACK: hardcoded; SpaceBall doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            var OB = new MyObjectBuilder_SpaceBall();

            // HACK: broadcast is broken and starts off and doesn't get saved
            OB.EnableBroadcast = false;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Artificial weight - Max").ExactMassFormat(spaceBall.MaxVirtualMass).Separator().Label("Default").ExactMassFormat(OB.VirtualMass);
                SimpleTooltip("A force that gets applied directly to the grid at the block position by artificial gravity generators.");

                AddLine().LabelHardcoded("Radio broadcaster").DistanceFormat(Hardcoded.SpaceBall_RadioBroadcastRange).Separator().Label("Default").Append(OB.EnableBroadcast ? "On" : "Off");
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Friction").ProportionToPercent(OB.Friction)
                    .Separator().Label("Restitution").ProportionToPercent(OB.Restitution)
                    .Separator().Color(MyPerGameSettings.BallFriendlyPhysics ? COLOR_GOOD : COLOR_NORMAL).Label("Configurable").BoolFormat(MyPerGameSettings.BallFriendlyPhysics);
                SimpleTooltip("Friction affects how well it grips surfaces." +
                              "\nRestitution is kinetic energy lost in collisions, affects bouncyness." +
                              "\nWhether they're configurable in terminal is a world-wide setting that can only be enabled by mods using scripts.");
            }
        }

        void Format_JumpDrive(MyCubeBlockDefinition def)
        {
            MyJumpDriveDefinition jumpDrive = (MyJumpDriveDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings))
            {
                if(!MyPerGameSettings.EnableJumpDrive)
                {
                    AddLine().Color(COLOR_BAD).Append("All vanilla jumpdrive functions are disabled by a script mod.");
                }
            }

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
                AddLine().Label("Max mass").ExactMassFormat((float)jumpDrive.MaxJumpMass);
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

        void Format_Weapon(MyCubeBlockDefinition def)
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

                        AddLine().Label("Power Capacity").PowerStorageFormat(capacitorDef.Capacity).Separator().Color(COLOR_WARNING).Label("Recharge time").Append("around ").TimeFormat(chargeTime);
                        SimpleTooltip("The recharge time varies greatly (-2s to +9s) because of how it's implemented.");
                    }
                    else
                    {
                        float requiredPowerInput = (turret != null ? Hardcoded.Turret_PowerReq : Hardcoded.ShipGun_PowerReq);
                        PowerRequired(requiredPowerInput, weaponDef.ResourceSinkGroup, powerHardcoded: true);
                    }
                }
            }

            MyInventoryComponentDefinition invComp = Utils.GetInventoryFromComponent(def);
            float invVolume = invComp?.Volume ?? weaponDef.InventoryMaxVolume;
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                AddLine().Label("Inventory").InventoryFormat(invVolume, wpDef.AmmoMagazinesId, invComp);
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

                    AddLine().Label("Camera field of view").AngleFormat(turret.MaxFov).Append(" to ").AngleFormat(turret.MinFov);

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
                    float precisionAt100m = (float)Math.Tan(MathHelper.ToRadians(gunWWF.DeviationAngleDeg)) * 100 * 2;
                    AddLine().Label("| Precision").DistanceFormat(precisionAt100m).Append(" group at 100m").Separator().Label("Recoil force").ForceFormat(gunWWF.RecoilImpulse);
                }

                if(showAmmoDetails)
                {
                    const int MaxMagNameLength = 20;

                    // only supports one magazine so no point in trying to show all.
                    if(wpDef.AmmoMagazinesId.Length > 0)
                    {
                        MyAmmoMagazineDefinition magDef = Utils.TryGetMagazineDefinition(wpDef.AmmoMagazinesId[0], wpDef.Context);
                        if(magDef != null)
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
                bool blockTypeCanReload = Hardcoded.ReloadableBlockTypes.Contains(def.Id.TypeId);
                bool validWeapon = false;
                bool hasAmmo = true;
                bool hasZeroProjectiles = false;
                bool hasBullets = false;
                bool hasMissiles = false;

                AmmoBullets.Clear();
                AmmoMissiles.Clear();

                for(int i = 0; i < wpDef.AmmoMagazinesId.Length; i++)
                {
                    MyAmmoMagazineDefinition mag = Utils.TryGetMagazineDefinition(wpDef.AmmoMagazinesId[i], wpDef.Context);
                    MyAmmoDefinition ammo = (mag != null ? Utils.TryGetAmmoDefinition(mag.AmmoDefinitionId, mag.Context) : null);

                    if(ammo == null)
                        continue;

                    int ammoTypeIdx = (int)ammo.AmmoType;
                    if(wpDef.WeaponAmmoDatas[ammoTypeIdx] == null)
                        continue;

                    if(ammo.AmmoType == MyAmmoType.HighSpeed)
                    {
                        hasBullets = true;

                        MyProjectileAmmoDefinition bullet = (MyProjectileAmmoDefinition)ammo;

                        // if this is 0 then nothing else matters, this ammo type does nothing.
                        if(bullet.ProjectileCount <= 0)
                        {
                            hasZeroProjectiles = true;
                        }
                        else
                        {
                            if(bullet.ProjectileMassDamage * wpDef.DamageMultiplier != 0
                            || bullet.ProjectileHealthDamage * wpDef.DamageMultiplier != 0
                            || (bullet.HeadShot && bullet.ProjectileHeadShotDamage * wpDef.DamageMultiplier != 0)
                            || bullet.ProjectileExplosionDamage != 0)
                            {
                                validWeapon = true;
                                AmmoBullets.Add(MyTuple.Create(mag, bullet));
                            }
                        }
                    }
                    else if(ammo.AmmoType == MyAmmoType.Missile)
                    {
                        hasMissiles = true;

                        MyMissileAmmoDefinition missile = (MyMissileAmmoDefinition)ammo;
                        if(missile.MissileExplosionDamage != 0
                        || missile.MissileRicochetDamage != 0
                        || missile.MissileHealthPool != 0)
                        {
                            validWeapon = true;
                            AmmoMissiles.Add(MyTuple.Create(mag, missile));
                        }
                    }
                    else
                    {
                        Log.Error($"Warning: Unknown ammo type: {MyEnum<MyAmmoType>.GetName(ammo.AmmoType)} (#{ammoTypeIdx.ToString()})");
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
                        AddLine().Label("Precision").Color(COLOR_GOOD).Append("Pinpoint");
                    }
                    else
                    {
                        // cone base radius = tan(angleFromTip) * height
                        // DeviateShotAngle is in radians (same for Aiming one) and it's angle offset from center line
                        float coneBaseDiameter = 2 * (float)Math.Tan(wpDef.DeviateShotAngle) * 100;

                        AddLine().Label("Precision").DistanceFormat(coneBaseDiameter).Append(" group at 100m");
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
                    const int MaxMagNameLength = 16;
                    const string MagazineSeparator = ": ";
                    const string ColumnSeparator = " <color=gray>|<reset> ";
                    const string DamageSeparator = ColumnSeparator;
                    Color COLOR_OR_DAMAGE = new Color(255, 55, 25);

                    if(hasBullets)
                    {
                        MyWeaponDefinition.MyWeaponAmmoData ammoData = wpDef.WeaponAmmoDatas[(int)MyAmmoType.HighSpeed];

                        if(printFirstAmmoData || printBothAmmoData)
                        {
                            printFirstAmmoData = false;

                            float rps = Hardcoded.WeaponRealRPS(ammoData.ShootIntervalInMiliseconds);
                            StringBuilder sb = AddLine();

                            if(printBothAmmoData)
                                sb.Append("Bullets - ");

                            // only show fire rate if it can't reload or shoots more than one round between reloads
                            if(!blockTypeCanReload || ammoData.ShotsInBurst != 1)
                                sb.Label("Rate of fire").Number(rps).Append("/s").Separator();

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

                        if(wpDef.DamageMultiplier != 1f || wpDef.RangeMultiplier != 1f)
                        {
                            StringBuilder sb = AddLine();

                            if(wpDef.DamageMultiplier != 1f)
                                sb.Color(wpDef.DamageMultiplier > 1f ? COLOR_GOOD : COLOR_WARNING).Label("Damage modifier").MultiplierToPercent(wpDef.DamageMultiplier).ResetFormatting().Separator();

                            if(wpDef.RangeMultiplier != 1f)
                                sb.Color(wpDef.RangeMultiplier > 1f ? COLOR_GOOD : COLOR_WARNING).Label("Range modifier").MultiplierToPercent(wpDef.RangeMultiplier).ResetFormatting().Separator();

                            sb.RemoveLastSeparator();

                            SimpleTooltip("Weapon can multiply the damage and/or range of the ammo." +
                                          "\nThe shown stats are already modified by the multipliers." +
                                          "\nNote: Damage multiplier only affects bullet damage for characters and grids, does not affect explosions or any missile damage.");
                        }

                        foreach(MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition> tuple in AmmoBullets)
                        {
                            MyAmmoMagazineDefinition mag = tuple.Item1;
                            MyProjectileAmmoDefinition projectile = tuple.Item2;

                            float gridDamage = projectile.ProjectileMassDamage * wpDef.DamageMultiplier;
                            float charDamage = projectile.ProjectileHealthDamage * wpDef.DamageMultiplier;
                            float charHeadDamage = !projectile.HeadShot ? 0 : projectile.ProjectileHeadShotDamage * wpDef.DamageMultiplier;
                            float explosionDamage = projectile.ProjectileExplosionDamage; // HACK: wpDef.DamageMultiplier is not used for this explosion

                            StringBuilder line = AddLine().Append("| ").Color(COLOR_STAT_TYPE).AppendMaxLength(mag.DisplayNameText, MaxMagNameLength).ResetFormatting().Append(MagazineSeparator);

                            if(projectile.ProjectileCount > 1)
                                line.Color(COLOR_GOOD).Append(projectile.ProjectileCount).Append("x ").ResetFormatting();

                            line.Color(gridDamage == 0 ? COLOR_BAD : COLOR_STAT_SHIPDMG);
                            line.Number(gridDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);

                            line.Color(charDamage == 0 ? COLOR_BAD : COLOR_STAT_CHARACTERDMG);
                            line.Number(charDamage).Icon(FontsHandler.IconCharacter);

                            if(projectile.HeadShot)
                            {
                                // no space before "or" because it looks good with the slim character icon
                                line.Color(COLOR_OR_DAMAGE).Append("or ").Color(charHeadDamage == 0 ? COLOR_BAD : COLOR_STAT_CHARACTERDMG);
                                line.Number(charHeadDamage).Icon(FontsHandler.IconCharacterHead);
                            }

                            line.Append(DamageSeparator);

                            bool hasExplosion = projectile.ProjectileExplosionRadius > 0 && explosionDamage != 0;
                            if(hasExplosion)
                            {
                                line.Color(COLOR_STAT_EXPLOSION).DistanceFormat(projectile.ProjectileExplosionRadius).Icon(FontsHandler.IconSphere)
                                    .Number(explosionDamage).Icon(FontsHandler.IconExplode).Append(DamageSeparator);
                            }

                            line.Length -= DamageSeparator.Length;

                            line.Append(ColumnSeparator).Color(COLOR_UNIMPORTANT);

                            // from MyProjectile.SetInitialVelocities()
                            bool randomizedSpeed = projectile.SpeedVar > 0f;
                            float speedMin = projectile.DesiredSpeed * (randomizedSpeed ? (1f - projectile.SpeedVar) : 1f);
                            float speedMax = projectile.DesiredSpeed * (randomizedSpeed ? (1f + projectile.SpeedVar) : 1f);

                            if(randomizedSpeed)
                                line.Number(speedMin).Append("~").Number(speedMax).Append(" m/s");
                            else
                                line.SpeedFormat(projectile.DesiredSpeed);

                            line.Append(ColumnSeparator).Color(COLOR_UNIMPORTANT);

                            float range = wpDef.RangeMultiplier * projectile.MaxTrajectory;
                            if(wpDef.UseRandomizedRange)
                                line.DistanceRangeFormat(range * Hardcoded.Projectile_RandomRangeMin, range * Hardcoded.Projectile_RandomRangeMax);
                            else
                                line.DistanceFormat(range);

                            // projectiles always have gravity
                            line.Append(" ").Icon(COLOR_WARNING, FontsHandler.IconProjectileGravity).ResetFormatting();

                            StringBuilder tooltip = CreateTooltip();
                            if(tooltip != null)
                            {
                                TooltipMagazineStats(tooltip, mag, projectile, invVolume);

                                if(projectile.ProjectileCount > 1)
                                    tooltip.Color(COLOR_GOOD).Append("Each shot sends ").Append(projectile.ProjectileCount).Append(" independent projectiles.<reset>\n");

                                tooltip.Color(COLOR_STAT_SHIPDMG).Append("Block damage: ").ResetFormatting().Number(gridDamage).Append(FontsHandler.IconBlockDamage).Append('\n');

                                tooltip.Color(COLOR_STAT_CHARACTERDMG).Append("Character damage: ").ResetFormatting().Number(charDamage).Append(FontsHandler.IconCharacter);

                                if(projectile.HeadShot)
                                    tooltip.Append(" or ").Number(charHeadDamage).Append(FontsHandler.IconCharacterHead).Append(" if headshot");
                                else
                                    tooltip.Append(" (no extra damage on headshot)");

                                tooltip.Append('\n');

                                if(hasExplosion)
                                {
                                    tooltip.Color(COLOR_STAT_EXPLOSION).Append("Explosion:<reset> ").Number(explosionDamage).Append(FontsHandler.IconExplode)
                                        .Append("damage with ").DistanceFormat(projectile.ProjectileExplosionRadius).Append(FontsHandler.IconSphere).Append("radius\n");
                                }
                                else
                                {
                                    tooltip.Color(COLOR_STAT_EXPLOSION).Append("Explosion:<reset> (none)\n");
                                }

                                float impactForce = projectile.ProjectileHitImpulse * Hardcoded.Projectile_HitImpulsePreMultiplier * Constants.TicksPerSecond; // impulse to force

                                if(impactForce != 0f)
                                    tooltip.Append("Impact push: ").ForceFormat(impactForce).Append(" (x").Number(Hardcoded.Projectile_HitImpulseCharacterMultiplier).Append(" if it hits a character)\n");
                                else
                                    tooltip.Append("Impact push: (none)\n");

                                tooltip.Append("Speed: ");
                                if(randomizedSpeed)
                                    tooltip.Append("random between ").Number(speedMin).Append(" and ").Number(speedMax).Append(" m/s");
                                else
                                    tooltip.SpeedFormat(projectile.DesiredSpeed);
                                tooltip.Append('\n');


                                tooltip.Append("Max range: ");
                                if(wpDef.UseRandomizedRange)
                                {
                                    tooltip.Append("random between ").DistanceFormat(range * Hardcoded.Projectile_RandomRangeMin).
                                        Append(" and ").DistanceFormat(range * Hardcoded.Projectile_RandomRangeMax);
                                }
                                else
                                    tooltip.DistanceFormat(range);
                                tooltip.Append('\n');

                                tooltip.Append("Bullets are always affected by gravity ").Color(COLOR_WARNING).Append(FontsHandler.IconProjectileGravity).Append("<reset>\n");

                                float recoilForce = projectile.BackkickForce * Constants.TicksPerSecond; // impulse to force

                                if(recoilForce != 0f)
                                    tooltip.Append("Recoil: ").ForceFormat(recoilForce).Append('\n');
                                else
                                    tooltip.Append("Recoil: (none)\n");

                                tooltip.Append("Tracer: ").ProportionToPercent(projectile.ProjectileTrailProbability).Append(" chance to be visible").Append('\n');
                            }
                        }
                    }

                    if(hasMissiles)
                    {
                        MyWeaponDefinition.MyWeaponAmmoData ammoData = wpDef.WeaponAmmoDatas[(int)MyAmmoType.Missile];

                        if(printFirstAmmoData || printBothAmmoData)
                        {
                            printFirstAmmoData = false;

                            float rps = Hardcoded.WeaponRealRPS(ammoData.ShootIntervalInMiliseconds);
                            StringBuilder sb = AddLine();

                            if(printBothAmmoData)
                                sb.Append("Missiles - ");

                            // only show fire rate if it can't reload or shoots more than one round between reloads
                            if(!blockTypeCanReload || ammoData.ShotsInBurst != 1)
                                sb.Label("Rate of fire").Number(rps).Append("/s").Separator();

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

                            StringBuilder tooltip = CreateTooltip();
                            if(tooltip != null)
                            {
                                TooltipMagazineStats(tooltip, mag, missile, invVolume);
                            }

                            // HACK: wpDef.DamageMultiplier is not used for any missile damage

                            MyExplosionFlags explosionFlags = Hardcoded.GetMissileExplosionFlags(missile);

                            float penetrationPool = missile.MissileHealthPool;
                            bool showPenetrationDamage = penetrationPool > 0;

                            float explosiveRadius = missile.MissileExplosionRadius;
                            float explosiveDamage = 0;
                            if(explosiveRadius > 0 && (explosionFlags & MyExplosionFlags.APPLY_FORCE_AND_DAMAGE) != 0)
                                explosiveDamage = missile.MissileExplosionDamage;
                            bool showExplosiveDamage = explosiveDamage > 0;

                            // ricochet system is activated
                            // how it normally works: https://steamcommunity.com/sharedfiles/filedetails/?id=2963715247
                            // but it can be changed into bonus damage or can prevent penetration entirely, going through those scenarios below
                            if(missile.MissileMinRicochetAngle >= 0f)
                            {
                                // HACK: clamps like in MyMissile.Init()
                                float minProbability = MathHelper.Clamp(missile.MissileMinRicochetProbability, 0f, 1f);
                                int ricochetChanceMin = (int)Math.Round(minProbability * 100);
                                int ricochetChanceMax = (int)Math.Round(MathHelper.Clamp(missile.MissileMaxRicochetProbability, minProbability, 1f) * 100);

                                // swapped normal angle to surface angle
                                float ricochetSurfaceAngleMin = 90 - missile.MissileMinRicochetAngle;
                                float ricochetSurfaceAngleMax = 90 - MathHelper.Max(missile.MissileMinRicochetAngle, missile.MissileMaxRicochetAngle);

                                float ricochetDamage = missile.MissileRicochetDamage;

                                bool isNormalRicochet = true;

                                // TODO: show all the different weird configurations of ricochet
#if false
                                bool detonatesOnRicochet = missile.MissileRicochetDamage >= penetrationPool;
                                bool ricochetAllChances = missile.MissileMinRicochetProbability >= 1f;
                                bool ricochetAllAngles = missile.MissileMinRicochetAngle == 0f;
                                bool alwaysRicochets = ricochetAllChances && ricochetAllAngles;

                                if(detonatesOnRicochet) // doesn't actually ricochet
                                {
                                    isNormalRicochet = false;
                                    showPenetrationDamage = false;

                                    if(alwaysRicochets)
                                    {
                                        if(penetrationPool > 0)
                                            line.Color(COLOR_BAD);
                                        else
                                            line.Color(COLOR_STAT_SHIPDMG);

                                        line.Number(ricochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);

                                        if(tooltip != null)
                                        {
                                            tooltip.Color(COLOR_STAT_SHIPDMG).Append("Damage: <reset>")
                                                .Number(ricochetDamage).Append(FontsHandler.IconBlockDamage).Append(" (no penetration)\n");

                                            if(penetrationPool > 0)
                                            {
                                                tooltip.Color(COLOR_BAD).Append("Warning:<reset> This has penetration pool of ").Number(penetrationPool).Append(" but will never happen." +
                                                                                "\n                Because of the ricochet damage being larger, therefore eats all the pool on the hit.\n");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if(penetrationPool <= 0) // no penetration means ricochet damage is just bonus damage
                                        {
                                            if(ricochetAllAngles)
                                            {
                                                line.Color(COLOR_STAT_SHIPDMG).Append(ricochetChance).Append("% +").Number(ricochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);
                                            }
                                            else if(ricochetAllChances)
                                            {
                                                line.Color(COLOR_STAT_SHIPDMG).AngleFormatDeg(ricochetSurfaceAngle).Append(" +").Number(ricochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);
                                            }
                                            else
                                            {
                                                line.Color(COLOR_STAT_SHIPDMG).Append(ricochetChance).Append("% ").AngleFormatDeg(ricochetSurfaceAngle)
                                                    .Append(" +").Number(ricochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);
                                            }
                                        }
                                        else // weird config: penetration gets replaced by ricochet damage
                                        {
                                            line.Color(COLOR_BAD).Append("[complex condition]");

                                            if(tooltip != null)
                                            {
                                                tooltip.Color(COLOR_STAT_SHIPDMG).Append("Damage - complex conditions:<reset>\n");

                                                tooltip.Append($"  {ricochetChance}% chance and <").AngleFormatDeg(ricochetSurfaceAngle).Append(" surface angle replaces penetration with ")
                                                    .Number(ricochetDamage).Append(FontsHandler.IconBlockDamage).Append(" single block damage.");
                                            }
                                        }
                                    }
                                }
                                else // does not detonate/stop on ricochet
                                {
                                    if(alwaysRicochets)
                                    {
                                        isNormalRicochet = false;
                                        showPenetrationDamage = false;

                                        if(penetrationPool > 0)
                                        {
                                            line.Color(COLOR_BAD).Number(ricochetDamage).Icon(FontsHandler.IconBlockDamage).Icon(FontsHandler.IconRicochet).Append(DamageSeparator);

                                            if(tooltip != null)
                                            {
                                                tooltip.Color(COLOR_STAT_SHIPDMG).Append("Damage: <reset>")
                                                    .Number(ricochetDamage).Append(FontsHandler.IconBlockDamage).Append(" and ricochets").Append(FontsHandler.IconRicochet).Append("\n");

                                                tooltip.Color(COLOR_BAD).Append("Warning:<reset> This has penetration pool of ").Number(penetrationPool).Append(" but will never happen." +
                                                                                "\n                Because it always ricochets (>100% and >90deg) therefore it never gets to penetrate.\n");
                                            }
                                        }
                                        else
                                        {
                                            line.Color(COLOR_STAT_SHIPDMG).Number(ricochetDamage).Icon(FontsHandler.IconBlockDamage).Append(DamageSeparator);

                                            if(tooltip != null)
                                            {
                                                tooltip.Color(COLOR_STAT_SHIPDMG).Append("Damage: <reset>")
                                                    .Number(ricochetDamage).Append(FontsHandler.IconBlockDamage).Append(" and ricochets").Append(FontsHandler.IconRicochet).Append("\n");
                                            }
                                        }
                                    }
                                }
#endif

                                if(isNormalRicochet)
                                {
                                    line.Color(COLOR_STAT_RICOCHET).AngleFormatDeg(ricochetSurfaceAngleMin).Icon(FontsHandler.IconRicochet)
                                        .Append("<reset> ").Color(COLOR_STAT_RICOCHET).Number(ricochetDamage).Icon(FontsHandler.IconBlockDamage);

                                    if(showPenetrationDamage)
                                        line.Color(COLOR_OR_DAMAGE).Append(" or<reset> ");
                                    else
                                        line.Append(DamageSeparator);

                                    if(tooltip != null)
                                    {
                                        const int LabelSpaces = 21;

                                        tooltip.Color(COLOR_STAT_RICOCHET).Append("Ricochet<reset>").Append(FontsHandler.IconRicochet)
                                            .Append($": {ricochetChanceMax}% chance from {(ricochetSurfaceAngleMax <= 0 ? "" : "0° to ")}{ricochetSurfaceAngleMax:0.##}° surface angle.\n");
                                        tooltip.Append(' ', LabelSpaces).Append($"Then linearly scales to {ricochetChanceMin}% chance up until {ricochetSurfaceAngleMin:0.##}°");

                                        if(ricochetSurfaceAngleMin >= 90)
                                            tooltip.Append(" (which is the max)\n");
                                        else
                                            tooltip.Append(", no ricochet past this angle.\n");

                                        tooltip.Append(' ', LabelSpaces).Append("Ricochets only happen against blocks.\n");

                                        tooltip.Color(COLOR_STAT_RICOCHET).Append("On ricochet:<reset> ").Number(missile.MissileRicochetDamage).Append(FontsHandler.IconBlockDamage)
                                            .Append(" damage and reduces round's penetration pool by same amount.\n");

                                        tooltip.Append("Can view a ricochet simulation in the <color=0,255,155>block's overlay (");
                                        Main.Config.CycleOverlaysBind.Value.GetBinds(tooltip);
                                        tooltip.Append(")<reset>.\n");
                                    }
                                }
                            }
                            else
                            {
                                if(tooltip != null)
                                {
                                    tooltip.Color(COLOR_STAT_RICOCHET).Append("Ricochet<reset> (none)\n");
                                }
                            }

                            if(showPenetrationDamage)
                            {
                                line.Color(COLOR_STAT_PENETRATIONDMG).Number(penetrationPool).Icon(FontsHandler.IconBlockPenetration).Append(DamageSeparator);

                                if(tooltip != null)
                                {
                                    tooltip.Color(COLOR_STAT_PENETRATIONDMG).Append("Penetration pool:<reset> ")
                                        .Number(penetrationPool).Append(FontsHandler.IconBlockPenetration)
                                        .Append(" hp. Destroys blocks or characters in its path,\n")
                                        .Append(' ', 30).Append("each subtracting their hp from this number.\n");
                                }
                            }
                            else if(penetrationPool <= 0)
                            {
                                if(tooltip != null)
                                {
                                    tooltip.Color(COLOR_STAT_PENETRATIONDMG).Append("Penetration pool:<reset> (none)\n");
                                }
                            }

                            if(showExplosiveDamage)
                            {
                                line.Color(COLOR_STAT_EXPLOSION).DistanceFormat(explosiveRadius).Icon(FontsHandler.IconSphere)
                                    .Number(explosiveDamage).Icon(FontsHandler.IconExplode).Append(DamageSeparator);

                                if(tooltip != null)
                                {
                                    // TODO show as concussion/screenshake only if 0 damage but >0 range?
                                    tooltip.Color(COLOR_STAT_EXPLOSION).Append("Explosion: ").ResetFormatting()
                                        .DistanceFormat(explosiveRadius).Append(FontsHandler.IconSphere).Append("radius with ")
                                        .Number(explosiveDamage).Append(FontsHandler.IconExplode).Append(" volumetric damage\n");

                                    tooltip.Append("Explosion affects voxels: ").BoolFormat((explosionFlags & MyExplosionFlags.AFFECT_VOXELS) != 0);

                                    if(!MyAPIGateway.Session.SessionSettings.EnableVoxelDestruction)
                                        tooltip.Color(COLOR_WARNING).Append(" (EnableVoxelDestruction is off)<reset>");
                                    else if(MyAPIGateway.Session.SessionSettings.AdaptiveSimulationQuality)
                                        tooltip.Append(" (Caution: adaptive sim world setting is active)");

                                    tooltip.Append('\n');

                                    tooltip.Append("Explosion damages blocks: ").BoolFormat((explosionFlags & MyExplosionFlags.APPLY_DEFORMATION) != 0).Append('\n');

                                    // CREATE_PARTICLE_EFFECT decides if it spawns the main explosion particle effect.
                                    // CREATE_PARTICLE_DEBRIS spawns an extra `Explosion_Debris` particle effect.
                                    // CREATE_DEBRIS spawns some model debris only on hitting an entity.
                                    // FORCE_CUSTOM_END_OF_LIFE_EFFECT allows EndOfLifeEffect&EndOfLifeSound to override.
                                    // Seem unused: CREATE_SHRAPNELS, FORCE_DEBRIS, CREATE_DECALS
                                }
                            }
                            else if(penetrationPool <= 0)
                            {
                                if(tooltip != null)
                                {
                                    tooltip.Color(COLOR_STAT_EXPLOSION).Append("Explosion:<reset> (none)\n");
                                }
                            }

                            line.Length -= DamageSeparator.Length;

                            line.Append(ColumnSeparator).Color(COLOR_UNIMPORTANT);

                            // HACK: ammo.SpeedVar is not used for missiles
                            // HACK: wepDef.RangeMultiplier and wepDef.UseRandomizedRange are not used for missiles
                            float maxTravel = missile.MaxTrajectory;
                            float maxSpeed = missile.DesiredSpeed;
                            float spawnSpeed = missile.MissileInitialSpeed;
                            float accel = missile.MissileAcceleration;
                            float linearSpeed = Math.Min(spawnSpeed, maxSpeed);
                            //float totalFlightTime;

                            if(!missile.MissileSkipAcceleration && accel != 0)
                            {
                                if(accel > 0)
                                {
                                    float timeToMaxSpeed = (maxSpeed - spawnSpeed) / accel;
                                    line.TimeFormat(timeToMaxSpeed).Icon(FontsHandler.IconMissile).SpeedFormat(maxSpeed);

                                    // TODO: better maffs
                                    //float speed = spawnSpeed;
                                    //float travelled = 0;
                                    //for(int i = 0; i < 60 * 60 * 5; i++)
                                    //{
                                    //    speed += accel / 60f;
                                    //    travelled += speed;
                                    //    if(speed >= maxSpeed)
                                    //        break;
                                    //}
                                    //
                                    //float timeAfterAccel = 0;
                                    //if(travelled < maxTravel)
                                    //    timeAfterAccel = (maxTravel - travelled) / maxSpeed;
                                    //
                                    //totalFlightTime = timeAfterAccel + timeToMaxSpeed;
                                }
                                else // negative acceleration
                                {
                                    //totalFlightTime = float.PositiveInfinity;

                                    line.AccelerationFormat(accel).Icon(FontsHandler.IconMissile).SpeedFormat(maxSpeed);
                                }
                            }
                            else // linear speed
                            {
                                line.SpeedFormat(linearSpeed);

                                //totalFlightTime = maxTravel / linearSpeed;
                            }

                            line.Append(ColumnSeparator).Color(COLOR_UNIMPORTANT);

                            line.DistanceFormat(maxTravel);

                            line.Append(" ");
                            if(missile.MissileGravityEnabled)
                                line.Icon(COLOR_WARNING, FontsHandler.IconProjectileGravity);
                            else
                                line.Icon(COLOR_GOOD, FontsHandler.IconProjectileNoGravity);
                            line.ResetFormatting();

                            if(tooltip != null)
                            {
                                if(!missile.MissileSkipAcceleration && accel != 0)
                                {
                                    tooltip.Append("Speed: from ").SpeedFormat(spawnSpeed).Append(" accelerates by ").AccelerationFormat(accel).
                                        Append(" until it reaches ").SpeedFormat(maxSpeed).Append('\n');
                                }
                                else
                                {
                                    tooltip.Append("Speed: ").SpeedFormat(linearSpeed).Append('\n');
                                }

                                tooltip.Append("Max range: ").DistanceFormat(maxTravel)
                                    //.Append(", Travel time: ").TimeFormat(totalFlightTime)
                                    .Append('\n');

                                if(missile.MissileGravityEnabled)
                                    tooltip.Append("Affected by gravity ").Color(COLOR_WARNING).Append(FontsHandler.IconProjectileGravity);
                                else
                                    tooltip.Append("Ignores gravity ").Color(COLOR_GOOD).Append(FontsHandler.IconProjectileNoGravity);
                                tooltip.Append("<reset>\n");

                                if(missile.BackkickForce != 0f)
                                    tooltip.Append("Recoil: ").ForceFormat(missile.BackkickForce).Append('\n');
                                else
                                    tooltip.Append("Recoil: (none)\n");

                                tooltip.Append("Missile mass: ").ExactMassFormat(missile.MissileMass).Append('\n');
                            }
                        }
                    }
                }

                if(!validWeapon)
                {
                    StringBuilder sb = AddLine().Color(COLOR_WARNING);
                    if(!hasAmmo)
                        sb.Append("Has no ammo magazines.");
                    else if(hasZeroProjectiles)
                        sb.Append("Ammo shoots nothing (0 projectiles).");
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

        void TooltipMagazineStats(StringBuilder tooltip, MyAmmoMagazineDefinition magDef, MyAmmoDefinition ammoDef, float invVolume)
        {
            tooltip.Color(COLOR_STAT_TYPE).Append(magDef.DisplayNameText).ResetFormatting().Append('\n');

            // TODO: internal ID?
            // TODO: mod?

            tooltip.Append("Volume in inventory: ").VolumeFormat(magDef.Volume);
            if(invVolume > 0)
            {
                // HACK: this can give wrong results for very tight fits, like 0.072f/0.024f = 2.99999976f, hence trying MyFixedPoint that the game uses
                //int fit = (int)Math.Floor(invVolume / magDef.Volume);

                MyFixedPoint invSize = (MyFixedPoint)invVolume;
                MyFixedPoint mul = (MyFixedPoint)(1f / magDef.Volume); // HACK: because MyFixedPoint doesn't support division, using a multiplier instead
                int fit = (int)MyFixedPoint.Floor(invSize * mul);

                tooltip.Append(" (can fit ").Number(fit).Append(")");
            }
            tooltip.Append('\n');

            if(magDef.Capacity > 1)
                tooltip.Append("Magazine holds ").Append(magDef.Capacity).Append(" rounds\n");

            float damagePerMag;
            if(Hardcoded.GetAmmoInventoryExplosion(magDef, ammoDef, 1, out damagePerMag))
            {
                if(damagePerMag > 0)
                {
                    tooltip.Append("On container destroyed: ").Color(COLOR_WARNING).Append("explodes ");
                }
                else //if(damagePerMag < 0)
                {
                    tooltip.Append("On container destroyed: ").Color(COLOR_GOOD).Append("reduces other ammo explosion by ");
                }

                tooltip.RoundedNumber(damagePerMag, 5).Append(FontsHandler.IconExplode).Append("/mag<reset>\n");
            }

            try
            {
                tooltip.TrimEndWhitespace();
                Main.ItemTooltips.TooltipCrafting(tooltip, magDef, forTextBoxTooltip: true);
                tooltip.TrimEndWhitespace().Append('\n');
            }
            catch(Exception e)
            {
                string msg = $"Error generating tooltip for magazine: {magDef?.Id.ToString()}";
                Log.Error($"{msg}\n{e}", msg);
            }

            tooltip.Append('\n'); // empty line to separate magazine stats from ammo stats

            if(ammoDef is MyProjectileAmmoDefinition)
                tooltip.Append("Ammo type: Bullet (can't be targeted)").Append('\n');
            else
                tooltip.Append("Ammo type: Missile (can be targeted by turrets)").Append('\n');
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

            int lines = 1;
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
                    lines++;
                }

                bool comesEnabled = ((defaultOn & option) != 0);
                string name = Hardcoded.CustomTargetingOptionName.GetValueOrDefault(option) ?? MyEnum<MyTurretTargetingOptions>.GetName(option);

                const int OffShade = 215;
                sb.Color(comesEnabled ? new Color(OffShade, 255, OffShade) : new Color(255, OffShade, OffShade)).Append(name).ResetFormatting().Append(", ");

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

            var tooltip = CreateTooltip(coveringLines: lines);
            if(tooltip != null)
            {
                tooltip.Append("The slight shade of green means the option starts enabled, slight shade of red means the opposite.");

                if((hidden & MyTurretTargetingOptions.Missiles) == 0)
                {
                    string name = Hardcoded.CustomTargetingOptionName.GetValueOrDefault(MyTurretTargetingOptions.Missiles) ?? MyEnum<MyTurretTargetingOptions>.GetName(MyTurretTargetingOptions.Missiles);
                    tooltip.Append("\nThe ").Append(name).Append(" option can target any missile-type ammo.");
                }
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
                default: sb.Append(MyEnum<CoreSystemsDef.ArmorDefinition.ArmorType>.GetName(armorDef.Kind)); break;
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

                //AddLine().Append(paddingCategory).Label("Targeting");

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

        void Format_Warhead(MyCubeBlockDefinition def)
        {
            var warhead = def as MyWarheadDefinition; // does not extend MyWeaponBlockDefinition
            if(warhead == null)
                return;

            // HACK: hardcoded; Warhead doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.AmmoDetails))
            {
                AddLine().Label("Damage").Append(warhead.WarheadExplosionDamage.ToString("#,###,###,###,##0.##")).Icon(FontsHandler.IconExplode);

                float cellSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);

                // HACK: hardcoded from MyWarhead.MarkForExplosion()
                float searchRadius = Hardcoded.WarheadSearchRadius(warhead);

                float gridRadius = cellSize * Hardcoded.WarheadScanRadiusGridMul;
                float minRadius = gridRadius + cellSize; // from the warheads loop

                int warheadsInside = 1; // always includes itself in the search
                float radiusCapped = Math.Min(Hardcoded.WarheadMaxRadius, (1f + Hardcoded.WarheadRadiusRatioPerOther * warheadsInside) * warhead.ExplosionRadius);

                float actualExplosionRadiusAlone = Math.Max(radiusCapped, minRadius);

                AddLine().Label("Radius").DistanceFormat(actualExplosionRadiusAlone, 2).Icon(FontsHandler.IconSphere)
                    .Append(" + <i>some bonus</i> per warhead within ").DistanceFormat(searchRadius).Icon(FontsHandler.IconSphere);

                StringBuilder tooltip = CreateTooltip();
                if(tooltip != null)
                {
                    tooltip.Append("Warhead's explosion radius is boosted by <i>some amount</i> for every other warhead within ")
                        .DistanceFormat(searchRadius).HardcodedMarker().Append(" (for ").Append(def.CubeSize == MyCubeSize.Large ? "Large" : "Small").Append("Grid).")
                        .Append("\nNo exact numbers because the game behavior on these is needlesly complex and cannot be explained in one tooltip.")
                        .Append("\nIt is however simulated in the <color=0,255,155>block's overlay (");
                    Main.Config.CycleOverlaysBind.Value.GetBinds(tooltip);
                    tooltip.Append(")<reset> so you can see it in action there.")
                        .Append("\nCareful that the behavior differs between blocks, always check the exact block you plan on using including the nearby ones.")
                        .Append("\n\nExplosions are volumetric therefore some blocks within the radius can remain unaffected.");
                }
            }
        }

        void Format_TargetDummy(MyCubeBlockDefinition def)
        {
            var dummyDef = def as MyTargetDummyBlockDefinition;
            if(dummyDef == null)
                return;

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
                AddLine().Label("Regeneration time").TimeFormat(dummyDef.MinRegenerationTimeInS).Append(" to ").TimeFormat(dummyDef.MaxRegenerationTimeInS).Append(" (terminal configurable)");

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

        void Format_Decoy(MyCubeBlockDefinition def)
        {
            var decoy = def as MyDecoyDefinition;
            if(decoy == null)
                return;

            // HACK: hardcoded; Decoy doesn't require power
            PowerRequired(0, MyStringHash.NullOrEmpty, powerHardcoded: true, groupHardcoded: true);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                // from MyDecoy.GetSafetyRodRadius()
                float lightningCatchRadius = def.CubeSize == MyCubeSize.Small ? decoy.LightningRodRadiusSmall : decoy.LightningRodRadiusLarge; // bonkers

                lightningCatchRadius = Math.Max(Hardcoded.Decoy_MinLightningRodDistance, lightningCatchRadius);

                StringBuilder sb = AddLine().Label("Lightning attraction").DistanceFormat(lightningCatchRadius).Separator().Label("Damage");
                if(Main.Caches.LightningMinDamage == Main.Caches.LightningMaxDamage)
                    sb.Append(Main.Caches.LightningMinDamage);
                else
                    sb.Append(Main.Caches.LightningMinDamage).Append(" to ").Append(Main.Caches.LightningMaxDamage);

                SimpleTooltip(LightningAttractionTooltip + $"\nNOTE: Decoys cannot have smaller radius than {Hardcoded.Decoy_MinLightningRodDistance}m.");
            }
        }

        void Format_TurretControl(MyCubeBlockDefinition def)
        {
            var tcbDef = def as MyTurretControlBlockDefinition;
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

        void Format_Searchlight(MyCubeBlockDefinition def)
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

                AddLine().Label("Camera field of view").AngleFormat(searchlight.MaxFov).Append(" to ").AngleFormat(searchlight.MinFov);

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

        void Format_AIBlocks(MyCubeBlockDefinition def)
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

        void Format_EventController(MyCubeBlockDefinition def)
        {
            var eventDef = def as MyEventControllerBlockDefinition;
            if(eventDef == null)
                return;

            PowerRequired(eventDef.RequiredPowerInput, eventDef.ResourceSinkGroup);
        }

        void Format_EmotionController(MyCubeBlockDefinition def)
        {
            var emoDef = def as MyEmotionControllerBlockDefinition;
            if(emoDef == null)
                return;

            PowerRequired(emoDef.RequiredPowerInput, emoDef.ResourceSinkGroup);
        }

        void Format_HeatVent(MyCubeBlockDefinition def)
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

        void Format_SafeZone(MyCubeBlockDefinition def)
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

        void Format_ContractBlock(MyCubeBlockDefinition def)
        {
            PowerRequired(0, MyStringHash.NullOrEmpty, powerHardcoded: true, groupHardcoded: true);
        }

        void Format_StoreBlock(MyCubeBlockDefinition def)
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

        void DLCFormat(MyCubeBlockDefinition def)
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

                IMyDLC dlc;
                if(MyAPIGateway.DLC.TryGetDLC(dlcId, out dlc))
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

        void DamageMultiplierAsResistance(float damageMultiplier, string label = "Resistance")
        {
            int dmgResPercent = (int)(((1f / damageMultiplier) - 1) * 100);

            GetLine()
                .Color(dmgResPercent == 0 ? COLOR_NORMAL : (dmgResPercent > 0 ? COLOR_GOOD : COLOR_WARNING)).Label(label).Append(dmgResPercent > 0 ? "+" : "").Append(dmgResPercent).Append("%")
                .Color(COLOR_UNIMPORTANT).Append(" (x").RoundedNumber(damageMultiplier, 2).Append(")").ResetFormatting();
        }

        void ResistanceFormat(double resistance, string label = "Resistance")
        {
            int resPercent = (int)((resistance - 1) * 100f);
            float multiplier = (float)(1d / resistance);

            GetLine()
                .Color(resPercent == 1 ? COLOR_NORMAL : (resPercent > 0 ? COLOR_GOOD : COLOR_WARNING)).Label(label).Append(resPercent > 0 ? "+" : "").Append(resPercent).Append("%")
                .Color(COLOR_UNIMPORTANT).Append(" (x").RoundedNumber(multiplier, 2).Append(")").ResetFormatting();
        }

        void PowerRequired(float mw, string groupName, bool powerHardcoded = false, bool groupHardcoded = false)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                MyStringHash groupNameHash = (groupName != null ? MyStringHash.GetOrCompute(groupName) : MyStringHash.NullOrEmpty);
                PowerRequired(mw, groupNameHash, powerHardcoded, groupHardcoded);
            }
        }

        void PowerRequired(float mw, MyStringHash groupName, bool powerHardcoded = false, bool groupHardcoded = false)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                Color color = (mw <= 0 ? COLOR_GOOD : COLOR_NORMAL);
                StringBuilder sb = AddLine().Color(color);

                if(powerHardcoded)
                    sb.LabelHardcoded("Power required");
                else
                    sb.Label("Power required");

                if(mw <= 0)
                    sb.Append("None!");
                else
                    sb.PowerFormat(mw);

                if(mw > 0 && Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                {
                    sb.ResetFormatting().Separator().ResourcePriority(groupName, groupHardcoded);
                }
            }
        }


        bool PowerRequired2(string labelA, float mwA, string labelB, float mwB, MyStringHash group, bool groupHardcoded = false)
        {
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                StringBuilder sb = AddLine().Append("Power - ").Label(labelA).PowerFormat(mwA).Separator().Label(labelB).PowerFormat(mwB);

                if((mwA > 0 || mwB > 0) && Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                {
                    sb.Separator().ResourcePriority(group, groupHardcoded);
                }

                return true;
            }

            return false;
        }

        void InventoryStats(MyCubeBlockDefinition def, float alternateVolume = 0, float hardcodedVolume = 0, bool showConstraints = true, MyInventoryConstraint constraintFromDef = null)
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

        void InventoryConstraints(float maxVolume, MyInventoryConstraint invLimit, MyInventoryComponentDefinition invComp)
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
                    StringBuilder sb = AddLine().Append("    ");

                    if(maxItems > 0)
                    {
                        sb.Color(COLOR_BAD).Label("Max items").Append(maxItems).ResetFormatting();
                    }

                    if(maxMass > 0)
                    {
                        if(maxItems > 0)
                            sb.Separator();

                        sb.Color(COLOR_BAD).Label("Max mass").MassFormat(maxMass).ResetFormatting();
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

        readonly List<MyDefinitionId> removeCacheIds = new List<MyDefinitionId>();
        void PurgeCache()
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
                LastDefId = default(MyDefinitionId);

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
