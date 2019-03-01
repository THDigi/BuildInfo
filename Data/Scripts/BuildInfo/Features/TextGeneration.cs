﻿using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utils;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Digi.Input;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum; // HACK allows the use of BlendTypeEnum which is whitelisted but bypasses accessing MyBillboard which is not whitelisted

namespace Digi.BuildInfo.Features
{
    public enum GridSplitType
    {
        Recalculate,
        NoSplit,
        Split,
    }

    public class TextGeneration : ClientComponent
    {
        #region Constants
        private const BlendTypeEnum FG_BLEND_TYPE = BlendTypeEnum.SDR;

        private readonly MyStringId BG_MATERIAL = MyStringId.GetOrCompute("Square");
        private const BlendTypeEnum BG_BLEND_TYPE = BlendTypeEnum.Standard;
        private readonly Color BG_COLOR = new Vector4(0.20784314f, 0.266666681f, 0.298039228f, 1f);
        private const float BG_DGE = 0.02f; // added padding edge around the text boundary for the background image

        private const float MENU_BG_OPACITY = 0.7f;

        private const int SCROLL_FROM_LINE = 2; // ignore lines to this line when scrolling, to keep important stuff like mass in view at all times; used in HUD notification view mode.
        private const int SPACE_SIZE = 8; // space character's width; used in HUD notification view mode.
        private const int MAX_LINES = 8; // max amount of HUD notification lines to print; used in HUD notification view mode.
        public const int MOD_NAME_MAX_LENGTH = 30;
        public const int PLAYER_NAME_MAX_LENGTH = 18;

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
        public readonly Color COLOR_BAD = Color.Red;
        public readonly Color COLOR_WARNING = Color.Yellow;
        public readonly Color COLOR_UNIMPORTANT = Color.Gray;
        public readonly Color COLOR_PART = new Color(55, 255, 155);
        public readonly Color COLOR_MOD = Color.DeepSkyBlue;
        public readonly Color COLOR_MOD_TITLE = Color.GreenYellow;
        public readonly Color COLOR_OWNER = new Color(55, 255, 255);
        public readonly Color COLOR_INFO = new Color(69, 177, 227);

        public readonly Color COLOR_STAT_PROJECTILECOUNT = new Color(0, 255, 0);
        public readonly Color COLOR_STAT_SHIPDMG = new Color(0, 255, 200);
        public readonly Color COLOR_STAT_CHARACTERDMG = new Color(255, 155, 0);
        public readonly Color COLOR_STAT_HEADSHOTDMG = new Color(255, 0, 0);
        public readonly Color COLOR_STAT_SPEED = new Color(0, 200, 255);
        public readonly Color COLOR_STAT_TRAVEL = new Color(55, 80, 255);
        #endregion

        public MyDefinitionId LastDefId; // last selected definition ID, can be set to MENU_DEFID too!
        public bool textShown = false;
        private bool aimInfoNeedsUpdate = false;
        private GridSplitType willSplitGrid;
        public Vector3D lastGizmoPosition;
        public Cache cache = null; // currently selected cache to avoid another dictionary lookup in Draw()

        // used by the textAPI view mode
        public readonly Dictionary<MyDefinitionId, Cache> CachedBuildInfoTextAPI = new Dictionary<MyDefinitionId, Cache>(MyDefinitionId.Comparer);
        private bool useLeftSide = true;
        private double prevAspectRatio = 1;
        private int lines;
        private StringBuilder textAPIlines = null;
        private HudAPIv2.HUDMessage textObject = null;
        private HudAPIv2.BillBoardHUDMessage bgObject = null;
        private float TextAPIScale => Config.TextAPIScale * 1.2f; // DEBUG TODO remove multiplication when textAPI preview is merged to live version
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

        // caches
        private readonly List<MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition>> ammoProjectiles
                   = new List<MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition>>();
        private readonly List<MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition>> ammoMissiles
                   = new List<MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition>>();

        public TextGeneration(Client mod) : base(mod)
        {
            Flags = UpdateFlags.UPDATE_AFTER_SIM;
        }

        public override void RegisterComponent()
        {
            InitTextGeneration();

            TextAPI.Detected += TextAPI_APIDetected;
            GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            GameConfig.OptionsMenuClosed += GameConfig_OptionsMenuClosed;
            EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;

            ReCheckSide();
        }

        public override void UnregisterComponent()
        {
            TextAPI.Detected -= TextAPI_APIDetected;
            GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            GameConfig.OptionsMenuClosed -= GameConfig_OptionsMenuClosed;
            EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        private void TextAPI_APIDetected()
        {
            TextGeneration.HideText(); // force a re-check to make the HUD -> textAPI transition
        }

        private void GameConfig_HudStateChanged(HudState prevState, HudState state)
        {
            ReCheckSide();
        }

        private void GameConfig_OptionsMenuClosed()
        {
            ReCheckSide();

            if(Math.Abs(prevAspectRatio - GameConfig.AspectRatio) > 0.0001)
            {
                prevAspectRatio = GameConfig.AspectRatio;
                CachedBuildInfoTextAPI.Clear();
            }
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            willSplitGrid = GridSplitType.Recalculate;
        }

        private void ReCheckSide()
        {
            bool shouldUseLeftSide = (state == HudState.HINTS && GameConfig.RotationHints);

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

            Update(tick);

            if(tick % CACHE_PURGE_TICKS == 0)
            {
                PurgeCache();
            }
        }

        private void Update(int tick)
        {
            var prevToolDefId = EquipmentMonitor.ToolDefId;

            if(EquipmentMonitor.AimedBlock != null && tick % 10 == 0) // make the aimed info refresh every 10 ticks
                aimInfoNeedsUpdate = true;

            if(EquipmentMonitor.ToolDefId != prevToolDefId)
                LastDefId = default(MyDefinitionId);

            var def = EquipmentMonitor.BlockDef;

            if(def != null || QuickMenu.Shown)
            {
                if(QuickMenu.Shown)
                {
                    if(QuickMenu.NeedsUpdate)
                    {
                        LastDefId = DEFID_MENU;
                        QuickMenu.NeedsUpdate = false;
                        textShown = false;

                        GenerateMenuText();
                        PostProcessText(DEFID_MENU, false);
                    }
                }
                else
                {
                    bool changedBlock = (def.Id != LastDefId);

                    if(changedBlock || (aimInfoNeedsUpdate && EquipmentMonitor.AimedBlock != null))
                    {
                        LastDefId = def.Id;

                        if(Config.TextShow)
                        {
                            if(EquipmentMonitor.IsCubeBuilder)
                            {
                                if(Config.PlaceInfo.Value != 0)
                                {
                                    if(TextAPIEnabled ? CachedBuildInfoTextAPI.TryGetValue(def.Id, out cache) : CachedBuildInfoNotification.TryGetValue(def.Id, out cache))
                                    {
                                        textShown = false; // make the textAPI update
                                    }
                                    else
                                    {
                                        GenerateBlockText(def);
                                        PostProcessText(def.Id, true);
                                    }
                                }
                            }
                            else
                            {
                                if(Config.AimInfo.Value != 0)
                                {
                                    aimInfoNeedsUpdate = false;
                                    GenerateAimBlockText(def);
                                    PostProcessText(def.Id, false);
                                }
                            }
                        }
                    }
                }

                UpdateVisualText();

                // turn off frozen block preview if camera is too far away from it
                if(MyAPIGateway.CubeBuilder.FreezeGizmo && Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.WorldMatrix.Translation, lastGizmoPosition) > FREEZE_MAX_DISTANCE_SQ)
                {
                    QuickMenu.SetFreezePlacement(false);
                }
            }
            else if(textShown)
            {
                QuickMenu.Shown = false;

                if(MyAPIGateway.CubeBuilder.FreezeGizmo)
                {
                    QuickMenu.SetFreezePlacement(false);
                }

                HideText();
            }
        }

        #region Text handling
        public void PostProcessText(MyDefinitionId id, bool useCache)
        {
            if(TextAPIEnabled)
            {
                var textSize = UpdateTextAPIvisuals(textAPIlines);

                if(useCache)
                {
                    cache = new CacheTextAPI(textAPIlines, textSize);

                    if(CachedBuildInfoTextAPI.ContainsKey(id))
                        CachedBuildInfoTextAPI[id] = cache;
                    else
                        CachedBuildInfoTextAPI.Add(id, cache);
                }
            }
            else
            {
                long now = DateTime.UtcNow.Ticks;
                lastScroll = now + TimeSpan.TicksPerSecond;
                atLine = SCROLL_FROM_LINE;

                for(int i = line; i >= 0; --i)
                {
                    var l = notificationLines[i];

                    var textWidthPx = largestLineWidth - l.lineWidthPx;

                    int fillChars = (int)Math.Floor((float)textWidthPx / (float)SPACE_SIZE);

                    if(fillChars > 0)
                    {
                        l.str.Append(' ', fillChars);
                    }
                }

                if(useCache)
                {
                    cache = new CacheNotifications(notificationLines);

                    if(CachedBuildInfoNotification.ContainsKey(id))
                        CachedBuildInfoNotification[id] = cache;
                    else
                        CachedBuildInfoNotification.Add(id, cache);
                }
            }
        }

        private Vector2D UpdateTextAPIvisuals(StringBuilder textSB, Vector2D textSize = default(Vector2D))
        {
            if(textObject == null)
            {
                textObject = new HudAPIv2.HUDMessage(new StringBuilder(TEXTAPI_TEXT_LENGTH), Vector2D.Zero, Scale: TextAPIScale, HideHud: !Config.TextAlwaysVisible, Blend: FG_BLEND_TYPE);
            }

            if(bgObject == null)
            {
                bgObject = new HudAPIv2.BillBoardHUDMessage(BG_MATERIAL, Vector2D.Zero, Color.White, HideHud: !Config.TextAlwaysVisible, Shadowing: true, Blend: BG_BLEND_TYPE); // scale on bg must always remain 1
            }

            bgObject.Visible = true;
            textObject.Visible = true;

            #region Update text and count lines
            var msg = textObject.Message;
            msg.Clear().EnsureCapacity(msg.Length + textSB.Length);
            lines = 0;

            for(int i = 0; i < textSB.Length; i++)
            {
                var c = textSB[i];

                msg.Append(c);

                if(c == '\n')
                    lines++;
            }
            #endregion

            var textPos = Vector2D.Zero;
            var textOffset = Vector2D.Zero;

            // calculate text size if it wasn't inputted
            if(Math.Abs(textSize.X) <= 0.0001 && Math.Abs(textSize.Y) <= 0.0001)
                textSize = textObject.GetTextLength();

            if(QuickMenu.Shown) // in the menu
            {
                textOffset = new Vector2D(-textSize.X, textSize.Y / -2);
            }
#if false
            else if(selectedBlock != null) // welder/grinder info attached to the game's block info
            {
                var cam = MyAPIGateway.Session.Camera;
                var camMatrix = cam.WorldMatrix;

                var hud = GetGameHudBlockInfoPos();
                hud.Y -= (BLOCKINFO_ITEM_HEIGHT * selectedDef.Components.Length) + BLOCKINFO_Y_OFFSET; // make the position top-right

                var worldPos = HudToWorld(hud);
                var size = GetGameHudBlockInfoSize((float)Math.Abs(textSize.Y) / 0.03f);
                var offset = new Vector2D(BLOCKINFO_TEXT_PADDING, BLOCKINFO_TEXT_PADDING) * ScaleFOV;

                worldPos += camMatrix.Left * (size.X + (size.X - offset.X)) + camMatrix.Up * (size.Y + (size.Y - offset.Y));

                // using textAPI's math to convert from world to its local coords
                double localScale = 0.1 * ScaleFOV;
                var local = Vector3D.Transform(worldPos, cam.ViewMatrix);
                local.X = (local.X / (localScale * aspectRatio)) * 2;
                local.Y = (local.Y / localScale) * 2;

                textPos.X = local.X;
                textPos.Y = local.Y;

                // not using textAPI's background for this as drawing my own manually is easier for the 3-part billboard that I need
                bgObject.Visible = false;
            }
#endif
            else if(Config.TextAPICustomStyling) // custom alignment and position
            {
                textPos = Config.TextAPIScreenPosition.Value;

                if(Config.TextAPIAlign.IsSet(TextAlignFlags.Right))
                    textOffset.X = -textSize.X;

                if(Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom))
                    textOffset.Y = -textSize.Y;
            }
            else if(!useLeftSide) // right side autocomputed
            {
                textPos = (GameConfig.AspectRatio > 5 ? TEXT_HUDPOS_RIGHT_WIDE : TEXT_HUDPOS_RIGHT);
                textOffset = new Vector2D(-textSize.X, -textSize.Y); // bottom-right pivot
            }
            else // left side autocomputed
            {
                textPos = (GameConfig.AspectRatio > 5 ? TEXT_HUDPOS_WIDE : TEXT_HUDPOS);
                textOffset = new Vector2D(0, 0); // top-left pivot
            }

            textObject.Origin = textPos;
            textObject.Offset = textOffset;

#if false
            if(showMenu || selectedBlock == null)
#endif
            {
                float edge = BG_DGE * TextAPIScale;

                bgObject.BillBoardColor = BG_COLOR * (QuickMenu.Shown ? MENU_BG_OPACITY : (Config.TextAPIBackgroundOpacity.Value < 0 ? GameConfig.HudBackgroundOpacity : Config.TextAPIBackgroundOpacity.Value));
                bgObject.Origin = textPos;
                bgObject.Width = (float)Math.Abs(textSize.X) + edge;
                bgObject.Height = (float)Math.Abs(textSize.Y) + edge;
                bgObject.Offset = textOffset + (textSize / 2);
            }

            textShown = true;
            return textSize;
        }

        public void UpdateVisualText()
        {
            var aimedBlock = EquipmentMonitor.AimedBlock;

            if(TextAPIEnabled)
            {
                if(MyAPIGateway.Gui.IsCursorVisible || (!Config.TextShow && !QuickMenu.Shown))
                {
                    HideText();
                    return;
                }

                // force reset, usually needed to fix notification to textAPI transition when heartbeat returns true
                if(textObject == null || (cache == null && !(QuickMenu.Shown || aimedBlock != null)))
                {
                    LastDefId = default(MyDefinitionId);
                    return;
                }

                // show last generated block info message only for cubebuilder
                if(!textShown && textObject != null)
                {
                    if(QuickMenu.Shown || aimedBlock != null)
                    {
                        UpdateTextAPIvisuals(textAPIlines);
                    }
                    else if(cache != null)
                    {
                        var cacheTextAPI = (CacheTextAPI)cache;
                        cacheTextAPI.ResetExpiry();
                        UpdateTextAPIvisuals(cacheTextAPI.Text, cacheTextAPI.TextSize);
                    }
                }
            }
            else
            {
                if(MyAPIGateway.Gui.IsCursorVisible || (!Config.TextShow && !QuickMenu.Shown))
                {
                    return;
                }

                List<IMyHudNotification> hudLines = null;

                if(QuickMenu.Shown || aimedBlock != null)
                {
                    hudLines = hudNotifLines;

                    for(int i = 0; i < notificationLines.Count; ++i)
                    {
                        var line = notificationLines[i];

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
                        cache.ResetExpiry();
                    }

                    hudLines = ((CacheNotifications)cache).Lines;
                }

                int lines = 0;

                foreach(var hud in hudLines)
                {
                    if(hud.Text.Length > 0)
                        lines++;

                    hud.Hide();
                }

                if(QuickMenu.Shown)
                {
                    // HACK this must match the data from the menu
                    const int itemsStartAt = 1;
                    const int itemsEndAt = QuickMenu.MENU_TOTAL_ITEMS;

                    var selected = itemsStartAt + QuickMenu.SelectedItem;

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
                            var hud = hudLines[l];
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
                            var hud = hudLines[l];

                            if(l < SCROLL_FROM_LINE)
                            {
                                hud.ResetAliveTime();
                                hud.Show();
                            }
                        }

                        int d = SCROLL_FROM_LINE;
                        l = atLine;

                        while(d < MAX_LINES)
                        {
                            var hud = hudLines[l];

                            if(hud.Text.Length == 0)
                                break;

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
                            var hud = hudLines[l];
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
                textShown = false;
                LastDefId = default(MyDefinitionId);

                // text API hide
                if(textObject != null)
                    textObject.Visible = false;

                if(bgObject != null)
                    bgObject.Visible = false;

                // HUD notifications don't need hiding, they expire in one frame.
            }
        }

        private void ResetLines()
        {
            if(TextAPIEnabled)
            {
                textAPIlines.Clear();
            }
            else
            {
                foreach(var l in notificationLines)
                {
                    l.str.Clear();
                }
            }

            line = -1;
            largestLineWidth = 0;
            addLineCalled = false;
        }

        private StringBuilder AddLine(string font = MyFontEnum.White)
        {
            EndAddedLines();
            addLineCalled = true;

            ++line;

            if(TextAPIEnabled)
            {
                return textAPIlines;
            }
            else
            {
                if(line >= notificationLines.Count)
                    notificationLines.Add(new HudLine());

                var nl = notificationLines[line];
                nl.font = font;

                return nl.str.Append("• ");
            }
        }

        public void EndAddedLines()
        {
            if(!addLineCalled)
                return;

            addLineCalled = false;

            if(TextAPIEnabled)
            {
                textAPIlines.ResetColor().Append('\n');
            }
            else
            {
                var px = GetStringSizeNotif(notificationLines[line].str);

                largestLineWidth = Math.Max(largestLineWidth, px);

                notificationLines[line].lineWidthPx = px;
            }
        }

        private StringBuilder GetLine()
        {
            return (TextAPIEnabled ? textAPIlines : notificationLines[line].str);
        }

        private void AddOverlaysHint(MyCubeBlockDefinition def)
        {
            if(Overlays.drawLookup.ContainsKey(def.Id.TypeId))
            {
                AddLine(MyFontEnum.DarkBlue).Color(COLOR_UNIMPORTANT).Append("(Overlay available. ");
                Config.CycleOverlaysBind.Value.GetBinds(GetLine());
                GetLine().Append(" to cycle)");
            }
        }

        public static int GetStringSizeNotif(StringBuilder builder)
        {
            int endLength = builder.Length;
            int len;
            int size = 0;

            for(int i = 0; i < endLength; ++i)
            {
                if(BuildInfoMod.Client.Constants.charSize.TryGetValue(builder[i], out len))
                    size += len;
                else
                    size += 15;
            }

            return size;
        }
        #endregion Text handling

        #region Menu generation
        private StringBuilder AddMenuItemLine(int item, bool enabled = true)
        {
            AddLine(font: (QuickMenu.SelectedItem == item ? MyFontEnum.Green : (enabled ? MyFontEnum.White : MyFontEnum.Red)));

            if(QuickMenu.SelectedItem == item)
                GetLine().Color(COLOR_GOOD).Append("  > ");
            else
                GetLine().Color(enabled ? COLOR_NORMAL : COLOR_UNIMPORTANT).Append(' ', 6);

            return GetLine();
        }

        public void GenerateMenuText()
        {
            ResetLines();

            AddLine(MyFontEnum.Blue).Color(COLOR_BLOCKTITLE).Append("Build info mod");

            int i = 0;

            // HACK this must match the data from the HandleInput() which controls the actual actions of these

            AddMenuItemLine(i++).Append("Close menu");

            GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
            if(Config.MenuBind.Value.IsAssigned())
            {
                Config.MenuBind.Value.GetBinds(GetLine());
            }
            else
            {
                GetLine().Append(ChatCommands.CMD_BUILDINFO);
            }
            GetLine().Append(")");

            if(TextAPIEnabled)
            {
                AddLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Actions:");
            }

            AddMenuItemLine(i++).Append("Add aimed block to toolbar");
            GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
            if(Config.BlockPickerBind.Value.IsAssigned())
            {
                Config.BlockPickerBind.Value.GetBinds(GetLine());
            }
            else
            {
                GetLine().Append(ChatCommands.CMD_GETBLOCK);
            }
            GetLine().Append(")");

            AddMenuItemLine(i++).Append("Open block's mod workshop").Color(COLOR_UNIMPORTANT).Append("   (").Append(ChatCommands.CMD_MODLINK).Append(')');

            AddMenuItemLine(i++).Append("Help topics").Color(COLOR_UNIMPORTANT).Append("   (").Append(ChatCommands.CMD_HELP).Append(')');

            AddMenuItemLine(i++).Append("Open this mod's workshop").Color(COLOR_UNIMPORTANT).Append("   (").Append(ChatCommands.CMD_WORKSHOP).Append(')');

            if(TextAPIEnabled)
            {
                AddLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Settings:");
            }

            AddMenuItemLine(i++).Append("Text info: ").Append(Config.TextShow ? "ON" : "OFF");

            AddMenuItemLine(i++).Append("Draw overlays: ").Append(Overlays.NAMES[Overlays.drawOverlay]);
            if(Config.CycleOverlaysBind.Value.IsAssigned())
            {
                GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
                Config.CycleOverlaysBind.Value.GetBinds(GetLine());
                GetLine().Append(")").ResetColor();
            }

            AddMenuItemLine(i++).Append("Placement transparency: ").Append(MyCubeBuilder.Static.UseTransparency ? "ON" : "OFF");
            if(Config.ToggleTransparencyBind.Value.IsAssigned())
            {
                GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
                Config.ToggleTransparencyBind.Value.GetBinds(GetLine());
                GetLine().Append(")").ResetColor();
            }

            AddMenuItemLine(i++).Append("Freeze in position: ").Append(MyAPIGateway.CubeBuilder.FreezeGizmo ? "ON" : "OFF");
            if(Config.FreezePlacementBind.Value.IsAssigned())
            {
                GetLine().Color(COLOR_UNIMPORTANT).Append("   (");
                Config.FreezePlacementBind.Value.GetBinds(GetLine());
                GetLine().Append(")").ResetColor();
            }

            AddMenuItemLine(i++, TextAPI.WasDetected).Append("Use TextAPI: ");
            if(TextAPI.WasDetected)
                GetLine().Append(TextAPI.Use ? "ON" : "OFF");
            else
                GetLine().Append("OFF (Mod not detected)");

            AddMenuItemLine(i++).Append("Reload settings file").Color(COLOR_UNIMPORTANT).Append("   (").Append(ChatCommands.CMD_RELOAD).Append(')');

            if(TextAPIEnabled)
                AddLine();

            AddLine(MyFontEnum.Blue).Color(COLOR_INFO).Append("Navigation: Up/down = ").Append(MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE.GetAssignedInputName()).Append("/").Append(MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE.GetAssignedInputName()).Append(", change = ").Append(MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE.GetAssignedInputName()).ResetColor().Append(' ', 10);

            EndAddedLines();
        }
        #endregion

        #region Aimed block info generation
        public void GenerateAimBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            var aimedBlock = EquipmentMonitor.AimedBlock;
            var integrityRatio = aimedBlock.Integrity / aimedBlock.MaxIntegrity;
            var grid = aimedBlock.CubeGrid;

            var terminalBlock = aimedBlock.FatBlock as IMyTerminalBlock;
            bool hasComputer = (terminalBlock != null && def.ContainsComputer());

            #region Block name
            if(Config.AimInfo.IsSet(AimInfoFlags.TerminalName) && terminalBlock != null)
            {
                const int LENGTH_LIMIT = 35;

                AddLine().Append('"').Color(COLOR_BLOCKTITLE);

                var name = terminalBlock.CustomName;
                var newLine = name.IndexOf('\n');

                if(newLine >= 0)
                    name = name.Substring(0, newLine); // strip everything past new line (incl new line char)

                GetLine().AppendMaxLength(name, LENGTH_LIMIT).ResetColor().Append('"');
            }
            #endregion

            #region Mass, grid mass
            if(Config.AimInfo.IsSet(AimInfoFlags.Mass))
            {
                var mass = def.Mass;
                var massColor = Color.GreenYellow;

                if(aimedBlock.FatBlock != null)
                {
                    var inv = aimedBlock.FatBlock.GetInventory();

                    if(inv != null)
                    {
                        var invMass = (float)inv.CurrentMass;

                        if(invMass > 0)
                        {
                            mass += invMass;
                            massColor = COLOR_WARNING;
                        }
                    }
                }

                AddLine().Color(massColor).MassFormat(mass);

                if(grid.Physics != null)
                {
                    GetLine().ResetColor().Separator().Append(" Grid mass: ").MassFormat(aimedBlock.CubeGrid.Physics.Mass);
                }
            }
            #endregion

            #region Integrity
            if(Config.AimInfo.IsSet(AimInfoFlags.Integrity))
            {
                AddLine().ResetColor().Append("Integrity: ").Color(integrityRatio < def.CriticalIntegrityRatio ? COLOR_BAD : (integrityRatio < 1 ? COLOR_WARNING : COLOR_GOOD))
                    .IntegrityFormat(aimedBlock.Integrity).ResetColor()
                    .Append(" / ").IntegrityFormat(aimedBlock.MaxIntegrity);

                if(def.BlockTopology == MyBlockTopology.Cube && aimedBlock.HasDeformation)
                    GetLine().Color(COLOR_WARNING).Append(" (deformed)");
            }
            #endregion

            #region Optional: intake damage multiplier
            if(Config.AimInfo.IsSet(AimInfoFlags.DamageMultiplier))
            {
                // MySlimBlock.BlockGeneralDamageModifier is inaccessible
                int dmgResPercent = (int)((1 - (aimedBlock.DamageRatio * def.GeneralDamageMultiplier)) * 100);
                int gridDamageRes = (int)((1 - ((MyCubeGrid)grid).GridGeneralDamageModifier) * 100);

                if(dmgResPercent != 0 || gridDamageRes != 0)
                {
                    AddLine().Color(dmgResPercent == 0 ? COLOR_NORMAL : (dmgResPercent > 0 ? COLOR_GOOD : COLOR_WARNING)).Append("Resistance: ").Append(dmgResPercent > 0 ? "+" : "").Append(dmgResPercent).Append("%").ResetColor();

                    if(gridDamageRes != 0)
                        GetLine().Color(dmgResPercent == 0 ? COLOR_NORMAL : (dmgResPercent > 0 ? COLOR_GOOD : COLOR_WARNING)).Append(" (Grid: ").Append(gridDamageRes > 0 ? "+" : "").Append(gridDamageRes).Append("%)").ResetColor();
                }

                // TODO impact resistance? wheels in particular...
            }
            #endregion

            #region Optional: ownership
            if(Config.AimInfo.IsSet(AimInfoFlags.Ownership) && hasComputer)
            {
                var relation = (aimedBlock.OwnerId > 0 ? MyAPIGateway.Session.Player.GetRelationTo(aimedBlock.OwnerId) : MyRelationsBetweenPlayerAndBlock.NoOwnership);
                var shareMode = Utilities.GetBlockShareMode(aimedBlock.FatBlock);

                AddLine();

                if(relation == MyRelationsBetweenPlayerAndBlock.Enemies || relation == MyRelationsBetweenPlayerAndBlock.Neutral)
                    GetLine().Color(COLOR_BAD);
                else if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                    GetLine().Color(COLOR_OWNER);
                else if(relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                    GetLine().Color(COLOR_GOOD);
                else if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                    GetLine().Color(COLOR_WARNING);

                if(aimedBlock.OwnerId == 0)
                {
                    GetLine().Append("Not owned");
                }
                else
                {
                    GetLine().Append("Owner: ");

                    // NOTE: MyVisualScriptLogicProvider.GetPlayersName() returns local player on id 0 and id 0 is also use for "nobody" in ownership.
                    var factionTag = aimedBlock.FatBlock.GetOwnerFactionTag();

                    if(!string.IsNullOrEmpty(factionTag))
                        GetLine().Append(factionTag).Append('.');

                    GetLine().AppendMaxLength(MyVisualScriptLogicProvider.GetPlayersName(aimedBlock.FatBlock.OwnerId), PLAYER_NAME_MAX_LENGTH);
                }

                GetLine().ResetColor().Separator();

                if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                {
                    GetLine().Color(COLOR_GOOD).Append("Access: All");
                }
                else if(shareMode == MyOwnershipShareModeEnum.All)
                {
                    if(relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies)
                        GetLine().Color(COLOR_GOOD);
                    else
                        GetLine().Color(COLOR_WARNING);

                    GetLine().Append("Access: All");
                }
                else if(shareMode == MyOwnershipShareModeEnum.Faction)
                {
                    if(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                        GetLine().Color(COLOR_GOOD);
                    else
                        GetLine().Color(COLOR_BAD);

                    GetLine().Append("Access: Faction");
                }
                else if(shareMode == MyOwnershipShareModeEnum.None)
                {
                    if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                        GetLine().Color(COLOR_WARNING);
                    else
                        GetLine().Color(COLOR_BAD);

                    GetLine().Append("Access: Owner");
                }
            }
            #endregion

            #region Time to complete/grind
            if(Config.AimInfo.IsSet(AimInfoFlags.ToolUseTime))
            {
                float toolMul = 1;

                if(EquipmentMonitor.HandTool != null)
                {
                    var toolDef = MyDefinitionManager.Static.TryGetHandItemForPhysicalItem(EquipmentMonitor.HandTool.PhysicalItemDefinition.Id) as MyEngineerToolBaseDefinition;
                    toolMul = (toolDef == null ? 1 : toolDef.SpeedMultiplier);
                }
                else // assuming ship tool
                {
                    toolMul = Hardcoded.ShipWelder_WeldPerSecond;
                }

                var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
                var grindMul = MyAPIGateway.Session.GrinderSpeedMultiplier;
                var grindRatio = def.DisassembleRatio;

                if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition)
                    grindRatio *= Hardcoded.Door_Closed_DisassembleRatioMultiplier;

                var buildTime = ((def.MaxIntegrity / def.IntegrityPointsPerSec) / weldMul) / toolMul;
                var grindTime = ((buildTime / (1f / grindRatio)) / grindMul);

                if(!EquipmentMonitor.IsAnyGrinder)
                {
                    var time = buildTime * (1 - integrityRatio);

                    if(time > 0)
                    {
                        AddLine().Append("Complete: ").TimeFormat(buildTime * (1 - integrityRatio));

                        if(def.CriticalIntegrityRatio < 1 && integrityRatio < def.CriticalIntegrityRatio)
                        {
                            var funcTime = buildTime * def.CriticalIntegrityRatio * (1 - (integrityRatio / def.CriticalIntegrityRatio));

                            GetLine().Separator().Append("Functional: ").TimeFormat(funcTime);
                        }
                    }
                }
                else
                {
                    bool hackable = hasComputer && aimedBlock.OwnerId != MyAPIGateway.Session.Player.IdentityId && (integrityRatio >= def.OwnershipIntegrityRatio);
                    float hackTime = 0f;

                    if(hackable)
                    {
                        var noOwnershipTime = (grindTime * def.OwnershipIntegrityRatio);
                        hackTime = (grindTime * ((1 - def.OwnershipIntegrityRatio) - (1 - integrityRatio))) / MyAPIGateway.Session.HackSpeedMultiplier;
                        grindTime = noOwnershipTime + hackTime;
                    }
                    else
                    {
                        grindTime *= integrityRatio;
                    }

                    if(grindTime > 0)
                    {
                        AddLine().Append("Dismantled: ").TimeFormat(grindTime);

                        if(hackable)
                        {
                            GetLine().Separator().Append("Hacked: ").TimeFormat(hackTime);
                        }
                    }
                }
            }
            #endregion

            #region Optional: item changes on grind
            if(Config.AimInfo.IsSet(AimInfoFlags.GrindChangeWarning) && EquipmentMonitor.IsAnyGrinder)
            {
                foreach(var comp in def.Components)
                {
                    if(comp.DeconstructItem != comp.Definition)
                    {
                        AddLine(MyFontEnum.Red).Color(COLOR_WARNING).Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText);
                    }
                }
            }
            #endregion

            #region Optional: grid moving
            if(Config.AimInfo.IsSet(AimInfoFlags.GridMoving) && grid.Physics != null)
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
            #endregion

            #region Optional: ship grinder apply force
            if(Config.AimInfo.IsSet(AimInfoFlags.ShipGrinderImpulse) && EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder))
            {
                var controller = MyAPIGateway.Session.ControlledObject as IMyShipController;

                if(controller != null)
                {
                    var impulse = Hardcoded.ShipGrinderImpulseForce(controller.CubeGrid, aimedBlock);

                    if(impulse > 0.00001f)
                    {
                        var speed = impulse / aimedBlock.CubeGrid.Physics.Mass;

                        if(speed >= 0.5f)
                            AddLine(MyFontEnum.Red).Color(COLOR_BAD);
                        else
                            AddLine(MyFontEnum.Red).Color(COLOR_WARNING);

                        GetLine().Append("Grind impulse: ").SpeedFormat(speed, 5).Append(" (").ForceFormat(impulse).Append(")");
                    }
                }
            }
            #endregion

            #region Optional: grinder makes grid split
            if(Config.AimInfo.IsSet(AimInfoFlags.GrindGridSplit) && EquipmentMonitor.IsAnyGrinder)
            {
                if(willSplitGrid == GridSplitType.Recalculate)
                    willSplitGrid = grid.WillRemoveBlockSplitGrid(aimedBlock) ? GridSplitType.Split : GridSplitType.NoSplit;

                if(willSplitGrid == GridSplitType.Split)
                    AddLine(MyFontEnum.Red).Color(COLOR_WARNING).Append("Grid will split if removed!");

                // TODO find if split blocks will vanish due to no physics/no standalone
            }
            #endregion

            #region Optional: added by mod
            var context = def.Context;
            if(Config.AimInfo.IsSet(AimInfoFlags.AddedByMod) && !context.IsBaseGame)
            {
                if(TextAPIEnabled)
                {
                    AddLine().Color(COLOR_MOD).Append("Mod:").Color(COLOR_MOD_TITLE).AppendMaxLength(context.ModName, MOD_NAME_MAX_LENGTH);

                    var id = context.GetWorkshopID();

                    if(id > 0)
                        AddLine().Color(COLOR_MOD).Append("       | ").ResetColor().Append("Workshop ID: ").Append(id);
                }
                else
                {
                    AddLine(MyFontEnum.Blue).Append("Mod: ").ModFormat(context);
                }
            }
            #endregion

            #region Overlay hints
            if(Config.AimInfo.IsSet(AimInfoFlags.OverlayHint))
            {
                AddOverlaysHint(def);
            }
            #endregion

            EndAddedLines();
        }
        #endregion

        #region Equipped block info generation
        public void GenerateBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            #region Block name line only for textAPI
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.BlockName) && TextAPIEnabled)
            {
                AddLine().Color(COLOR_BLOCKTITLE).Append(def.DisplayNameText);

                var stages = def.BlockStages;

                if(stages != null && stages.Length > 0)
                {
                    GetLine().Append("  ").Color(COLOR_BLOCKVARIANTS).Append("(Variant 1 of ").Append(stages.Length + 1).Append(")");
                }
                else
                {
                    stages = MyCubeBuilder.Static.ToolbarBlockDefinition.BlockStages;

                    if(stages != null && stages.Length > 0)
                    {
                        int num = 0;

                        for(int i = 0; i < stages.Length; ++i)
                        {
                            if(def.Id == stages[i])
                            {
                                num = i + 2; // +2 instead of +1 because the 1st block is not in the list, it's the list holder
                                break;
                            }
                        }

                        GetLine().Append("  ").Color(COLOR_BLOCKVARIANTS).Append("(Variant ").Append(num).Append(" of ").Append(stages.Length + 1).Append(")");
                    }
                }
            }
            #endregion

            AppendBasics(def, part: false);

            #region Optional - different item gain on grinding
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.GrindChangeWarning))
            {
                foreach(var comp in def.Components)
                {
                    if(comp.DeconstructItem != comp.Definition)
                    {
                        AddLine(MyFontEnum.Red).Color(COLOR_WARNING).Append("When grinding: ").Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText);
                    }
                }
            }
            #endregion

            // TODO use? not sure if useful...
            //if(def.VoxelPlacement.HasValue)
            //{
            //    // Comment from definition: 
            //    // <!--Possible settings Both,InVoxel,OutsideVoxel,Volumetric. If volumetric set than MaxAllowed and MinAllowed will be used.-->
            //
            //    var vp = def.VoxelPlacement.Value;
            //
            //    AddLine().SetTextAPIColor(COLOR_WARNING).Append($"Terrain placement - Dynamic: ").Append(vp.DynamicMode.PlacementMode);
            //
            //    if(vp.DynamicMode.PlacementMode == VoxelPlacementMode.Volumetric)
            //        GetLine().Append(" (").Append(vp.DynamicMode.MinAllowed).Append(" to ").Append(vp.DynamicMode.MaxAllowed).Append(")");
            //
            //    GetLine().Separator().Append($"Static: ").Append(vp.StaticMode.PlacementMode);
            //
            //    if(vp.StaticMode.PlacementMode == VoxelPlacementMode.Volumetric)
            //        GetLine().Append(" (").Append(vp.StaticMode.MinAllowed).Append(" to ").Append(vp.StaticMode.MaxAllowed).Append(")");
            //
            //    GetLine().ResetTextAPIColor();
            //}

            #region Optional - creative-only stuff
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Mirroring) && (MyAPIGateway.Session.CreativeMode || MyAPIGateway.Session.EnableCopyPaste)) // HACK Session.EnableCopyPaste used as spacemaster check
            {
                if(def.MirroringBlock != null)
                {
                    MyCubeBlockDefinition mirrorDef;
                    if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(new MyDefinitionId(def.Id.TypeId, def.MirroringBlock), out mirrorDef))
                        AddLine(MyFontEnum.Blue).Color(COLOR_GOOD).Append("Mirrors with: ").Append(mirrorDef.DisplayNameText);
                    else
                        AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Mirrors with: ").Append(def.MirroringBlock).Append(" (Error: not found)");
                }
            }
            #endregion

            #region Per-block info
            if(def.Id.TypeId != typeof(MyObjectBuilder_CubeBlock)) // anything non-decorative
            {
                TextGenerationCall action;

                if(formatLookup.TryGetValue(def.Id.TypeId, out action))
                {
                    action.Invoke(def);
                }
                // DEBUG HACK temporary until MyObjectBuilder_WindTurbine is whitelisted
                else if(def.Id.TypeId.ToString() == "MyObjectBuilder_WindTurbine")
                {
                    Format_PowerProducer(def);
                }
            }
            #endregion

            #region Added by mod
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.AddedByMod) && !def.Context.IsBaseGame)
            {
                AddLine(MyFontEnum.Blue).Color(COLOR_MOD).Append("Mod: ").ModFormat(def.Context);
            }
            #endregion

            #region Overlay hints
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.OverlayHint))
            {
                AddOverlaysHint(def);
            }
            #endregion

            EndAddedLines();
        }
        #endregion

        #region Shared generation methods
        private void AppendBasics(MyCubeBlockDefinition def, bool part = false)
        {
            int airTightFaces = 0;
            int totalFaces = 0;
            var airTight = Utilities.GetAirTightFaces(def, ref airTightFaces, ref totalFaces);
            var deformable = (def.BlockTopology == MyBlockTopology.Cube && def.UsesDeformation);
            var assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
            var buildModels = (def.BuildProgressModels != null && def.BuildProgressModels.Length > 0);
            var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
            var grindRatio = def.DisassembleRatio;

            if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition)
                grindRatio *= Hardcoded.Door_Closed_DisassembleRatioMultiplier;

            string padding = (part ? (TextAPIEnabled ? "        | " : "       | ") : "");

            if(part)
                AddLine(MyFontEnum.Blue).Color(COLOR_PART).Append("Part: ").Append(def.DisplayNameText);

            #region Mass/size/build time/deconstruct time/no models
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Line1))
            {
                AddLine();

                if(part)
                    GetLine().Color(COLOR_PART).Append(padding);

                GetLine().Color(new Color(200, 255, 55)).MassFormat(def.Mass).ResetColor().Separator()
                    .VectorFormat(def.Size).Separator()
                    .TimeFormat(assembleTime / weldMul).Color(COLOR_UNIMPORTANT).MultiplierFormat(weldMul).ResetColor();

                if(Math.Abs(grindRatio - 1) >= 0.0001f)
                    GetLine().Separator().Color(grindRatio > 1 ? COLOR_BAD : (grindRatio < 1 ? COLOR_GOOD : COLOR_NORMAL)).Append("Deconstructs: ").ProportionToPercent(1f / grindRatio).ResetColor();

                if(!buildModels)
                    GetLine().Separator().Color(COLOR_WARNING).Append("(No construction models)").ResetColor();
            }
            #endregion

            #region Integrity, deformable, damage intake
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Line2))
            {
                AddLine();

                if(part)
                    GetLine().Color(COLOR_PART).Append(padding).ResetColor();

                GetLine().Label("Integrity").AppendFormat("{0:#,###,###,###,###}", def.MaxIntegrity);

                if(deformable)
                    GetLine().Separator().Label("Deformable").RoundedNumber(def.DeformationRatio, 2);

                var dmgResPercent = (int)((1 - def.GeneralDamageMultiplier) * 100);

                if(dmgResPercent != 0)
                    GetLine().Separator().Color(dmgResPercent == 0 ? COLOR_NORMAL : (dmgResPercent > 0 ? COLOR_GOOD : COLOR_WARNING)).Label("Resistance").Append(dmgResPercent > 0 ? "+" : "").Append(dmgResPercent).Append("%").ResetColor();

                //if(!def.IsStandAlone)
                //    GetLine().Separator().Color(COLOR_WARNING).Label("Not standalone");
                // TODO add HasPhysics and IsStandAlone?
            }
            #endregion

            #region Airtightness
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Airtight))
            {
                AddLine(font: (airTight == AirTightMode.SEALED ? MyFontEnum.Green : (airTight == AirTightMode.NOT_SEALED ? MyFontEnum.Red : MyFontEnum.Blue)));

                if(part)
                    GetLine().Color(COLOR_PART).Append(padding);

                GetLine().Color(airTight == AirTightMode.SEALED ? COLOR_GOOD : (airTight == AirTightMode.NOT_SEALED ? COLOR_BAD : COLOR_WARNING)).Append("Air-tight: ");

                if(airTight == AirTightMode.SEALED)
                    GetLine().Append("Sealed");
                else if(airTight == AirTightMode.NOT_SEALED)
                    GetLine().Append("Not sealed");
                else
                    GetLine().Append(airTightFaces).Append(" of ").Append(totalFaces).Append(" faces are sealed");
            }
            #endregion
        }
        #endregion

        #region Per block info
        public void InitTextGeneration()
        {
            textAPIlines = new StringBuilder(TEXTAPI_TEXT_LENGTH);

            formatLookup.Add(typeof(MyObjectBuilder_TerminalBlock), Format_TerminalBlock);

            formatLookup.Add(typeof(MyObjectBuilder_Conveyor), Format_Conveyors);
            formatLookup.Add(typeof(MyObjectBuilder_ConveyorConnector), Format_Conveyors);

            formatLookup.Add(typeof(MyObjectBuilder_ShipConnector), Format_Connector);

            formatLookup.Add(typeof(MyObjectBuilder_Collector), Format_CargoAndCollector);
            formatLookup.Add(typeof(MyObjectBuilder_CargoContainer), Format_CargoAndCollector);

            formatLookup.Add(typeof(MyObjectBuilder_ConveyorSorter), Format_ConveyorSorter);

            formatLookup.Add(typeof(MyObjectBuilder_Drill), Format_Drill);

            formatLookup.Add(typeof(MyObjectBuilder_ShipWelder), Format_WelderAndGrinder);
            formatLookup.Add(typeof(MyObjectBuilder_ShipGrinder), Format_WelderAndGrinder);

            formatLookup.Add(typeof(MyObjectBuilder_PistonBase), Format_Piston);
            formatLookup.Add(typeof(MyObjectBuilder_ExtendedPistonBase), Format_Piston);

            formatLookup.Add(typeof(MyObjectBuilder_MotorStator), Format_Rotor);
            formatLookup.Add(typeof(MyObjectBuilder_MotorAdvancedStator), Format_Rotor);
            formatLookup.Add(typeof(MyObjectBuilder_MotorSuspension), Format_Rotor);

            formatLookup.Add(typeof(MyObjectBuilder_MergeBlock), Format_MergeBlock);

            formatLookup.Add(typeof(MyObjectBuilder_LandingGear), Format_LandingGear);

            formatLookup.Add(typeof(MyObjectBuilder_ShipController), Format_ShipController);
            formatLookup.Add(typeof(MyObjectBuilder_Cockpit), Format_ShipController);
            formatLookup.Add(typeof(MyObjectBuilder_CryoChamber), Format_ShipController);
            formatLookup.Add(typeof(MyObjectBuilder_RemoteControl), Format_ShipController);

            formatLookup.Add(typeof(MyObjectBuilder_Thrust), Format_Thrust);

            formatLookup.Add(typeof(MyObjectBuilder_Gyro), Format_Gyro);

            formatLookup.Add(typeof(MyObjectBuilder_LightingBlock), Format_Light);
            formatLookup.Add(typeof(MyObjectBuilder_InteriorLight), Format_Light);
            formatLookup.Add(typeof(MyObjectBuilder_ReflectorLight), Format_Light);

            formatLookup.Add(typeof(MyObjectBuilder_OreDetector), Format_OreDetector);

            formatLookup.Add(typeof(MyObjectBuilder_ProjectorBase), Format_Projector);
            formatLookup.Add(typeof(MyObjectBuilder_Projector), Format_Projector);

            formatLookup.Add(typeof(MyObjectBuilder_Door), Format_Door);

            formatLookup.Add(typeof(MyObjectBuilder_AirtightDoorGeneric), Format_AirtightDoor);
            formatLookup.Add(typeof(MyObjectBuilder_AirtightHangarDoor), Format_AirtightDoor);
            formatLookup.Add(typeof(MyObjectBuilder_AirtightSlideDoor), Format_AirtightDoor);

            formatLookup.Add(typeof(MyObjectBuilder_AdvancedDoor), Format_AdvancedDoor);

            formatLookup.Add(typeof(MyObjectBuilder_Parachute), Format_Parachute);

            formatLookup.Add(typeof(MyObjectBuilder_MedicalRoom), Format_MedicalRoom);

            formatLookup.Add(typeof(MyObjectBuilder_ProductionBlock), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_Refinery), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_Assembler), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_SurvivalKit), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_GasTank), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_OxygenTank), Format_Production);
            formatLookup.Add(typeof(MyObjectBuilder_OxygenGenerator), Format_Production);

            formatLookup.Add(typeof(MyObjectBuilder_OxygenFarm), Format_OxygenFarm);

            formatLookup.Add(typeof(MyObjectBuilder_AirVent), Format_AirVent);
            formatLookup.Add(typeof(MyObjectBuilder_UpgradeModule), Format_UpgradeModule);

            formatLookup.Add(typeof(MyObjectBuilder_Reactor), Format_PowerProducer);
            formatLookup.Add(typeof(MyObjectBuilder_HydrogenEngine), Format_PowerProducer);
            formatLookup.Add(typeof(MyObjectBuilder_BatteryBlock), Format_PowerProducer);
            formatLookup.Add(typeof(MyObjectBuilder_SolarPanel), Format_PowerProducer);
            //formatLookup.Add(typeof(MyObjectBuilder_WindTurbine), Format_PowerProducer);
            // DEBUG ^^^ when whitelisting is fixed

            formatLookup.Add(typeof(MyObjectBuilder_RadioAntenna), Format_RadioAntenna);

            formatLookup.Add(typeof(MyObjectBuilder_LaserAntenna), Format_LaserAntenna);

            formatLookup.Add(typeof(MyObjectBuilder_Beacon), Format_Beacon);

            formatLookup.Add(typeof(MyObjectBuilder_TimerBlock), Format_Timer);

            formatLookup.Add(typeof(MyObjectBuilder_MyProgrammableBlock), Format_ProgrammableBlock);

            formatLookup.Add(typeof(MyObjectBuilder_TextPanel), Format_LCD);

            formatLookup.Add(typeof(MyObjectBuilder_SoundBlock), Format_SoundBlock);

            formatLookup.Add(typeof(MyObjectBuilder_SensorBlock), Format_Sensor);

            formatLookup.Add(typeof(MyObjectBuilder_CameraBlock), Format_Camera);

            formatLookup.Add(typeof(MyObjectBuilder_ButtonPanel), Format_Button);

            formatLookup.Add(typeof(MyObjectBuilder_GravityGeneratorBase), Format_GravityGenerator);
            formatLookup.Add(typeof(MyObjectBuilder_GravityGenerator), Format_GravityGenerator);
            formatLookup.Add(typeof(MyObjectBuilder_GravityGeneratorSphere), Format_GravityGenerator);

            formatLookup.Add(typeof(MyObjectBuilder_VirtualMass), Format_ArtificialMass);

            formatLookup.Add(typeof(MyObjectBuilder_SpaceBall), Format_SpaceBall);

            formatLookup.Add(typeof(MyObjectBuilder_JumpDrive), Format_JumpDrive);

            formatLookup.Add(typeof(MyObjectBuilder_ConveyorTurretBase), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_UserControllableGun), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_LargeGatlingTurret), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_LargeMissileTurret), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_InteriorTurret), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_SmallGatlingGun), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_SmallMissileLauncher), Format_Weapon);
            formatLookup.Add(typeof(MyObjectBuilder_SmallMissileLauncherReload), Format_Weapon);

            formatLookup.Add(typeof(MyObjectBuilder_Warhead), Format_Warhead);
        }

        private void Format_TerminalBlock(MyCubeBlockDefinition def)
        {
            // HACK hardcoded; control panel doesn't use power
            PowerRequired(0, null, powerHardcoded: true);
        }

        #region Conveyors
        private void Format_Conveyors(MyCubeBlockDefinition def)
        {
            PowerRequired(Hardcoded.Conveyors_PowerReq, Hardcoded.Conveyors_PowerGroup, powerHardcoded: true, groupHardcoded: true);
        }

        private void Format_Connector(MyCubeBlockDefinition def)
        {
            PowerRequired(Hardcoded.ShipConnector_PowerReq(def), Hardcoded.ShipConnector_PowerGroup, powerHardcoded: true, groupHardcoded: true);

            InventoryStats(def, 0, Hardcoded.ShipConnector_InventoryVolume(def));

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                var data = BData_Base.TryGetDataCached<BData_Connector>(def);

                if(data != null)
                {
                    if(data.Connector)
                        AddLine().Append("Connectable: Yes");
                    else
                        AddLine().Color(COLOR_WARNING).Append("Connectable: No").ResetColor();

                    GetLine().Separator().LabelHardcoded("Can throw contents").Append("Yes");
                }
            }
        }

        private void Format_CargoAndCollector(MyCubeBlockDefinition def)
        {
            var cargo = (MyCargoContainerDefinition)def;

            var poweredCargo = def as MyPoweredCargoContainerDefinition; // collector
            if(poweredCargo != null)
            {
                PowerRequired(poweredCargo.RequiredPowerInput, poweredCargo.ResourceSinkGroup);
            }

            InventoryStats(def, cargo.InventorySize.Volume, Hardcoded.CargoContainer_InventoryVolume(def));
        }

        private void Format_ConveyorSorter(MyCubeBlockDefinition def)
        {
            var sorter = (MyConveyorSorterDefinition)def; // does not extend MyPoweredCargoContainerDefinition

            PowerRequired(sorter.PowerInput, sorter.ResourceSinkGroup);

            InventoryStats(def, sorter.InventorySize.Volume, 0);
        }
        #endregion

        private void Format_Piston(MyCubeBlockDefinition def)
        {
            var piston = (MyPistonBaseDefinition)def;

            PowerRequired(piston.RequiredPowerInput, piston.ResourceSinkGroup);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Extended length").DistanceFormat(piston.Maximum).Separator().Label("Max velocity").DistanceFormat(piston.MaxVelocity);
            }

            Suffix_Mechanical(def, piston.TopPart);
        }

        private void Format_Rotor(MyCubeBlockDefinition def)
        {
            var motor = (MyMotorStatorDefinition)def;

            var suspension = def as MyMotorSuspensionDefinition;

            if(suspension != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
                {
                    AddLine().Label("Power - Idle").PowerFormat(suspension.RequiredIdlePowerInput).Separator().Label("Running").PowerFormat(suspension.RequiredPowerInput);

                    if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(suspension.ResourceSinkGroup);
                }

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine().Label("Max torque").TorqueFormat(suspension.PropulsionForce).Separator().Append("Axle Friction: ").TorqueFormat(suspension.AxleFriction);
                    AddLine().Label("Steering - Max angle").AngleFormat(suspension.MaxSteer).Separator().Append("Speed base: ").RotationSpeed(suspension.SteeringSpeed * 60);
                    AddLine().Label("Ride height").DistanceFormat(suspension.MinHeight).Append(" to ").DistanceFormat(suspension.MaxHeight);
                }
            }
            else
            {
                PowerRequired(motor.RequiredPowerInput, motor.ResourceSinkGroup);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine().Label("Max torque").TorqueFormat(motor.MaxForceMagnitude);

                    if(motor.RotorDisplacementMin < motor.RotorDisplacementMax)
                    {
                        AddLine().Label("Displacement Large Top").DistanceFormat(motor.RotorDisplacementMin).Append(" to ").DistanceFormat(motor.RotorDisplacementMax);
                    }

                    if(motor.RotorDisplacementMinSmall < motor.RotorDisplacementMaxSmall)
                    {
                        AddLine().Label("Displacement Small Top").DistanceFormat(motor.RotorDisplacementMinSmall).Append(" to ").DistanceFormat(motor.RotorDisplacementMaxSmall);
                    }
                }
            }

            Suffix_Mechanical(def, motor.TopPart);
        }

        private void Suffix_Mechanical(MyCubeBlockDefinition def, string topPart)
        {
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PartStats))
            {
                var group = MyDefinitionManager.Static.TryGetDefinitionGroup(topPart);

                if(group == null)
                    return;

                var partDef = (def.CubeSize == MyCubeSize.Large ? group.Large : group.Small);

                AppendBasics(partDef, part: true);
            }
        }

        private void Format_MergeBlock(MyCubeBlockDefinition def)
        {
            var merge = (MyMergeBlockDefinition)def;

            // HACK hardcoded; MergeBlock doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Pull strength").AppendFormat("{0:###,###,##0.#######}", merge.Strength);
            }
        }

        private void Format_LandingGear(MyCubeBlockDefinition def)
        {
            var lg = (MyLandingGearDefinition)def;

            // HACK: hardcoded; LG doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max differential velocity for locking").SpeedFormat(lg.MaxLockSeparatingVelocity);
            }
        }

        #region Ship tools
        private void Format_Drill(MyCubeBlockDefinition def)
        {
            var shipDrill = (MyShipDrillDefinition)def;

            PowerRequired(Hardcoded.ShipDrill_Power, shipDrill.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                float volume;
                if(Utilities.GetInventoryFromComponent(def, out volume))
                    AddLine().Label("Inventory").InventoryFormat(volume, Hardcoded.ShipDrill_InventoryConstraint);
                else
                    AddLine().LabelHardcoded("Inventory").InventoryFormat(Hardcoded.ShipDrill_InventoryVolume(def), Hardcoded.ShipDrill_InventoryConstraint);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Mining radius").DistanceFormat(shipDrill.SensorRadius);
                AddLine().Label("Cutout radius").DistanceFormat(shipDrill.CutOutRadius);
            }
        }

        private void Format_WelderAndGrinder(MyCubeBlockDefinition def)
        {
            var shipTool = (MyShipToolDefinition)def;
            var isWelder = def is MyShipWelderDefinition;

            PowerRequired(Hardcoded.ShipTool_PowerReq, Hardcoded.ShipTool_PowerGroup, powerHardcoded: true, groupHardcoded: true);

            InventoryStats(def, 0, Hardcoded.ShipTool_InventoryVolume(def));

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(isWelder)
                {
                    float weld = Hardcoded.ShipWelder_WeldPerSecond;
                    var mul = MyAPIGateway.Session.WelderSpeedMultiplier;
                    AddLine().LabelHardcoded("Weld speed").ProportionToPercent(weld).Append(" split accross targets").Color(COLOR_UNIMPORTANT).MultiplierFormat(mul);

                    AddLine().Label("Welding radius").DistanceFormat(shipTool.SensorRadius);
                }
                else
                {
                    float grind = Hardcoded.ShipGrinder_GrindPerSecond;
                    var mul = MyAPIGateway.Session.GrinderSpeedMultiplier;
                    AddLine().LabelHardcoded("Grind speed").ProportionToPercent(grind * mul).Append(" split accross targets").Color(COLOR_UNIMPORTANT).MultiplierFormat(mul);

                    AddLine().Label("Grinding radius").DistanceFormat(shipTool.SensorRadius);
                }
            }
        }
        #endregion

        private void Format_ShipController(MyCubeBlockDefinition def)
        {
            var shipController = (MyShipControllerDefinition)def;

            var rc = def as MyRemoteControlDefinition;
            if(rc != null)
            {
                PowerRequired(rc.RequiredPowerInput, rc.ResourceSinkGroup);
            }

            var cryo = def as MyCryoChamberDefinition;
            if(cryo != null)
            {
                PowerRequired(cryo.IdlePowerConsumption, cryo.ResourceSinkGroup);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Append("Abilities: ");

                var preLen = GetLine().Length;

                if(shipController.EnableShipControl)
                    GetLine().Append("Ship control, ");

                if(shipController.EnableBuilderCockpit)
                    GetLine().Append("Place blocks, ");

                if(!shipController.EnableFirstPerson)
                    GetLine().Append("3rd person view only, ");

                if(preLen == GetLine().Length)
                {
                    GetLine().Append("None.");
                }
                else
                {
                    GetLine().Length -= 2; // remove last comma
                    GetLine().Append(".");
                }

                //AddLine((shipController.EnableShipControl ? MyFontEnum.Green : MyFontEnum.Red)).Append("Ship controls: ").Append(shipController.EnableShipControl ? "Yes" : "No");
                //AddLine((shipController.EnableFirstPerson ? MyFontEnum.Green : MyFontEnum.Red)).Append("First person view: ").Append(shipController.EnableFirstPerson ? "Yes" : "No");
                //AddLine((shipController.EnableBuilderCockpit ? MyFontEnum.Green : MyFontEnum.Red)).Append("Can build: ").Append(shipController.EnableBuilderCockpit ? "Yes" : "No");
            }

            var cockpit = def as MyCockpitDefinition;
            if(cockpit != null)
            {
                InventoryStats(def, 0, Hardcoded.Cockpit_InventoryVolume);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine((cockpit.IsPressurized ? MyFontEnum.Green : MyFontEnum.Red))
                       .Color(cockpit.IsPressurized ? COLOR_GOOD : COLOR_WARNING)
                       .Label("Pressurized");

                    if(cockpit.IsPressurized)
                        GetLine().Append("Yes, Oxygen capacity: ").VolumeFormat(cockpit.OxygenCapacity);
                    else
                        GetLine().Append("No");

                    if(cockpit.HUD != null)
                    {
                        MyDefinitionBase defHUD;
                        if(MyDefinitionManager.Static.TryGetDefinition(new MyDefinitionId(typeof(MyObjectBuilder_HudDefinition), cockpit.HUD), out defHUD))
                        {
                            // HACK MyHudDefinition is not whitelisted; also GetObjectBuilder() is useless because it doesn't get filled in
                            //var hudDefObj = (MyObjectBuilder_HudDefinition)defBase.GetObjectBuilder();
                            AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("Custom HUD: ").Append(cockpit.HUD).ResetColor().Separator().Color(COLOR_MOD).Append("Mod: ").ModFormat(defHUD.Context);
                        }
                        else
                        {
                            AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Custom HUD: ").Append(cockpit.HUD).Append("  (Error: not found)");
                        }
                    }
                }
            }
        }

        private void Format_Thrust(MyCubeBlockDefinition def)
        {
            var thrust = (MyThrustDefinition)def;

            if(!thrust.FuelConverter.FuelId.IsNull())
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
                {
                    AddLine().Append("Requires power to be controlled");

                    if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(thrust.ResourceSinkGroup);
                }

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
                {
                    AddLine().Append("Requires fuel: ").Append(thrust.FuelConverter.FuelId.SubtypeId).Separator().Append("Efficiency: ").Number(thrust.FuelConverter.Efficiency * 100).Append("%");
                }
            }
            else
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
                {
                    AddLine().Append("Power: ").PowerFormat(thrust.MaxPowerConsumption).Separator().Append("Idle: ").PowerFormat(thrust.MinPowerConsumption);

                    if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(thrust.ResourceSinkGroup);
                }
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Append("Force: ").ForceFormat(thrust.ForceMagnitude).Separator().Append("Dampener factor: ").RoundedNumber(thrust.SlowdownFactor, 2);

                if(thrust.EffectivenessAtMinInfluence < 1.0f || thrust.EffectivenessAtMaxInfluence < 1.0f)
                {
                    // HACK thrust.NeedsAtmosphereForInfluence seems to be a pointless var, planetary influence is always considered atmosphere.

                    AddLine(thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).Color(thrust.EffectivenessAtMaxInfluence < 1f ? COLOR_BAD : COLOR_GOOD)
                        .ProportionToPercent(thrust.EffectivenessAtMaxInfluence).Append(" max thrust ");
                    if(thrust.MaxPlanetaryInfluence < 1f)
                        GetLine().Append("in ").ProportionToPercent(thrust.MaxPlanetaryInfluence).Append(" atmosphere");
                    else
                        GetLine().Append("in atmosphere");

                    AddLine(thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).Color(thrust.EffectivenessAtMinInfluence < 1f ? COLOR_BAD : COLOR_GOOD)
                                    .ProportionToPercent(thrust.EffectivenessAtMinInfluence).Append(" max thrust ");
                    if(thrust.MinPlanetaryInfluence > 0f)
                        GetLine().Append("below ").ProportionToPercent(thrust.MinPlanetaryInfluence).Append(" atmosphere");
                    else
                        GetLine().Append("in space");
                }
                else
                {
                    AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("No thrust limits in space or planets");
                }
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(thrust.ConsumptionFactorPerG > 0)
                    AddLine(MyFontEnum.Red).Append("Extra consumption: +").ProportionToPercent(thrust.ConsumptionFactorPerG).Append(" per natural g acceleration");

                var data = BData_Base.TryGetDataCached<BData_Thrust>(def);

                if(data != null)
                {
                    AddLine().Append("Flames: ").Append(data.Flames.Count).Separator().Append("Max distance: ").DistanceFormat(data.HighestLength, 2);
                    AddLine().Append("Ship damage: ").Number(data.TotalBlockDamage).Append("/s").Separator().Append("Other damage:").Number(data.TotalOtherDamage).Append("/s");
                }

                if(!MyAPIGateway.Session.SessionSettings.ThrusterDamage)
                    AddLine().Color(Color.Green).Append("Thruster damage is disabled in this world");
            }
        }

        private void Format_Gyro(MyCubeBlockDefinition def)
        {
            var gyro = (MyGyroDefinition)def;

            PowerRequired(gyro.RequiredPowerInput, gyro.ResourceSinkGroup);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Force").ForceFormat(gyro.ForceMagnitude);
            }
        }

        private void Format_Light(MyCubeBlockDefinition def)
        {
            var light = (MyLightingBlockDefinition)def;

            var radius = light.LightRadius;
            var isSpotlight = (def is MyReflectorBlockDefinition);

            if(isSpotlight)
                radius = light.LightReflectorRadius;

            PowerRequired(light.RequiredPowerInput, light.ResourceSinkGroup);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Append("Radius: ").DistanceFormat(radius.Min).Append(" to ").DistanceFormat(radius.Max).Separator().Append("Default: ").DistanceFormat(radius.Default);
                AddLine().Append("Intensity: ").RoundedNumber(light.LightIntensity.Min, 2).Append(" to ").RoundedNumber(light.LightIntensity.Max, 2).Separator().Append("Default: ").RoundedNumber(light.LightIntensity.Default, 2);
                AddLine().Append("Falloff: ").RoundedNumber(light.LightFalloff.Min, 2).Append(" to ").RoundedNumber(light.LightFalloff.Max, 2).Separator().Append("Default: ").RoundedNumber(light.LightFalloff.Default, 2);
            }
        }

        private void Format_OreDetector(MyCubeBlockDefinition def)
        {
            var oreDetector = (MyOreDetectorDefinition)def;

            PowerRequired(Hardcoded.OreDetector_PowerReq, oreDetector.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max range").DistanceFormat(oreDetector.MaximumRange);
            }
        }

        private void Format_Projector(MyCubeBlockDefinition def)
        {
            var projector = (MyProjectorDefinition)def;

            PowerRequired(projector.RequiredPowerInput, projector.ResourceSinkGroup);
        }

        #region Doors
        private void Format_Door(MyCubeBlockDefinition def)
        {
            var door = (MyDoorDefinition)def;

            PowerRequired(Hardcoded.Door_PowerReq, door.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float moveTime = Hardcoded.Door_MoveSpeed(door.OpeningSpeed);
                AddLine().Label("Move time").TimeFormat(moveTime).Separator().Label("Distance").DistanceFormat(door.MaxOpen);
            }
        }

        private void Format_AirtightDoor(MyCubeBlockDefinition def)
        {
            var airTightDoor = (MyAirtightDoorGenericDefinition)def; // does not extend MyDoorDefinition

            // MyAirtightHangarDoorDefinition and MyAirtightSlideDoorDefinition are empty

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power").PowerFormat(airTightDoor.PowerConsumptionMoving).Separator().Label("Idle").PowerFormat(airTightDoor.PowerConsumptionIdle);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(airTightDoor.ResourceSinkGroup);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float moveTime = Hardcoded.Door_MoveSpeed(airTightDoor.OpeningSpeed);
                AddLine().Label("Move time").TimeFormat(moveTime);
            }
        }

        private void Format_AdvancedDoor(MyCubeBlockDefinition def)
        {
            var advDoor = (MyAdvancedDoorDefinition)def; // does not extend MyDoorDefinition

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Moving").PowerFormat(advDoor.PowerConsumptionMoving).Separator().Label("Idle").PowerFormat(advDoor.PowerConsumptionIdle);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(advDoor.ResourceSinkGroup);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float openTime, closeTime;
                Hardcoded.AdvDoor_MoveSpeed(advDoor, out openTime, out closeTime);

                AddLine().Label("Move time - Opening").TimeFormat(openTime).Separator().Label("Closing").TimeFormat(closeTime);
            }
        }
        #endregion

        private void Format_Parachute(MyCubeBlockDefinition def)
        {
            var parachute = (MyParachuteDefinition)def;

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Deploy").PowerFormat(parachute.PowerConsumptionMoving).Separator().Label("Idle").PowerFormat(parachute.PowerConsumptionIdle);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(parachute.ResourceSinkGroup);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo) || Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
            {
                const float TARGET_DESCEND_VELOCITY = 10;
                float maxMass, disreefAtmosphere;
                Hardcoded.Parachute_GetLoadEstimate(parachute, TARGET_DESCEND_VELOCITY, out maxMass, out disreefAtmosphere);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
                {
                    AddLine().Label("Required item to deploy").Append(parachute.MaterialDeployCost).Append("x ").IdTypeSubtypeFormat(parachute.MaterialDefinitionId);
                }

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine().Label("Required atmosphere - Minimum").Number(parachute.MinimumAtmosphereLevel).Separator().Label("Fully open").Number(disreefAtmosphere);
                    AddLine().Label("Drag coefficient").AppendFormat("{0:0.0####}", parachute.DragCoefficient);
                    AddLine().Label("Load estimate").Color(COLOR_INFO).MassFormat(maxMass).ResetColor().Append(" falling at ").SpeedFormat(TARGET_DESCEND_VELOCITY).Append(" in ").AccelerationFormat(Hardcoded.GAME_EARTH_GRAVITY).Append(" and 1.0 air density.");
                }
            }
        }

        private void Format_MedicalRoom(MyCubeBlockDefinition def)
        {
            var medicalRoom = (MyMedicalRoomDefinition)def;

            PowerRequired(Hardcoded.MedicalRoom_PowerReq, medicalRoom.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(medicalRoom.RespawnAllowed)
                {
                    AddLine().Label("Respawn");

                    if(medicalRoom.ForceSuitChangeOnRespawn)
                    {
                        GetLine().Append("Yes").Separator().Label("Forced suit");

                        if(string.IsNullOrEmpty(medicalRoom.RespawnSuitName))
                        {
                            GetLine().Color(COLOR_BAD).Append("(Error: empty)").ResetColor();
                        }
                        else
                        {
                            MyCharacterDefinition charDef;
                            if(MyDefinitionManager.Static.Characters.TryGetValue(medicalRoom.RespawnSuitName, out charDef))
                                GetLine().Append(charDef.Name).ResetColor();
                            else
                                GetLine().Append(medicalRoom.RespawnSuitName).Color(COLOR_BAD).Append(" (Error: not found)").ResetColor();
                        }
                    }
                    else
                        GetLine().Append("Yes");
                }
                else
                {
                    AddLine(MyFontEnum.Red).Color(COLOR_WARNING).Label("Respawn").Append("No");
                }
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                if(medicalRoom.HealingAllowed)
                    AddLine().Label("Healing").RoundedNumber(Math.Abs(MyEffectConstants.MedRoomHeal * 60), 2).Append("hp/s");
                else
                    AddLine(MyFontEnum.Red).Color(COLOR_WARNING).Label("Healing").Append("No").ResetColor();

                if(medicalRoom.RefuelAllowed)
                    AddLine().LabelHardcoded("Refuel").Append("Yes (x5)");
                else
                    AddLine(MyFontEnum.Red).LabelHardcoded("Refuel", COLOR_WARNING).Append("No").ResetColor();
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(medicalRoom.SuitChangeAllowed)
                {
                    AddLine().Label("Suit Change");

                    if(medicalRoom.CustomWardrobesEnabled && medicalRoom.CustomWardrobeNames != null && medicalRoom.CustomWardrobeNames.Count > 0)
                    {
                        foreach(var charName in medicalRoom.CustomWardrobeNames)
                        {
                            MyCharacterDefinition charDef;
                            if(!MyDefinitionManager.Static.Characters.TryGetValue(charName, out charDef))
                                AddLine(MyFontEnum.Red).Append("    ").Append(charName).Color(COLOR_BAD).Append(" (not found in definitions)");
                            else
                                AddLine().Append("    ").Append(charDef.DisplayNameText);
                        }
                    }
                    else
                        GetLine().Append("(all)");
                }
                else
                    AddLine(MyFontEnum.Red).Color(COLOR_WARNING).Label("Suit Change").Append("No").ResetColor();
            }
        }

        #region Production
        private void Format_Production(MyCubeBlockDefinition def)
        {
            var production = (MyProductionBlockDefinition)def;

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Append("Power: ").PowerFormat(production.OperationalPowerConsumption).Separator().Append("Idle: ").PowerFormat(production.StandbyPowerConsumption);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(production.ResourceSinkGroup);
            }

            var assembler = def as MyAssemblerDefinition;
            if(assembler != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    var mulSpeed = MyAPIGateway.Session.AssemblerSpeedMultiplier;
                    var mulEff = MyAPIGateway.Session.AssemblerEfficiencyMultiplier;

                    AddLine().Append("Assembly speed: ").ProportionToPercent(assembler.AssemblySpeed * mulSpeed).Color(COLOR_UNIMPORTANT).MultiplierFormat(mulSpeed).ResetColor().Separator().Append("Efficiency: ").ProportionToPercent(mulEff).MultiplierFormat(mulEff);
                }
            }

            var survivalKit = def as MySurvivalKitDefinition; // this extends MyAssemblerDefinition
            if(survivalKit != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    AddLine().Label("Healing").RoundedNumber(Math.Abs(MyEffectConstants.GenericHeal * 60), 2).Append("hp/s");
                    AddLine().LabelHardcoded("Refuel").Append("Yes (x1)");
                }
            }

            var refinery = def as MyRefineryDefinition;
            if(refinery != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    var mul = MyAPIGateway.Session.RefinerySpeedMultiplier;

                    AddLine().Append("Refine speed: ").ProportionToPercent(refinery.RefineSpeed * mul).Color(COLOR_UNIMPORTANT).MultiplierFormat(mul).ResetColor().Separator().Append("Efficiency: ").ProportionToPercent(refinery.MaterialEfficiency);
                }
            }

            var gasTank = def as MyGasTankDefinition;
            if(gasTank != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    AddLine().Append("Stores: ").Append(gasTank.StoredGasId.SubtypeName).Separator().Append("Capacity: ").VolumeFormat(gasTank.Capacity);
                }
            }

            var oxygenGenerator = def as MyOxygenGeneratorDefinition;
            if(oxygenGenerator != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    AddLine().Append("Ice consumption: ").MassFormat(oxygenGenerator.IceConsumptionPerSecond).Append("/s");

                    if(oxygenGenerator.ProducedGases.Count > 0)
                    {
                        AddLine().Append("Produces: ");

                        foreach(var gas in oxygenGenerator.ProducedGases)
                        {
                            GetLine().Append(gas.Id.SubtypeName).Append(" (").VolumeFormat(oxygenGenerator.IceConsumptionPerSecond * gas.IceToGasRatio).Append("/s), ");
                        }

                        GetLine().Length -= 2;
                    }
                    else
                    {
                        AddLine(MyFontEnum.Red).Append("Produces: <N/A>");
                    }
                }
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                var volume = (production.InventoryMaxVolume > 0 ? production.InventoryMaxVolume : production.InventorySize.Volume);

                if(refinery != null || assembler != null)
                {
                    AddLine().Append("In+out inventories: ").InventoryFormat(volume * 2, production.InputInventoryConstraint, production.OutputInventoryConstraint);
                }
                else
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume, production.InputInventoryConstraint);
                }
            }

            if(production.BlueprintClasses != null && Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                if(production.BlueprintClasses.Count == 0)
                {
                    AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Has no blueprint classes.");
                }
                else
                {
                    AddLine();

                    if(refinery != null)
                        GetLine().Append("Refines: ");
                    else if(gasTank != null)
                        GetLine().Append("Refills: ");
                    else if(assembler != null)
                        GetLine().Append("Builds: ");
                    else if(oxygenGenerator != null)
                        GetLine().Append("Generates: ");
                    else
                        GetLine().Append("Blueprints: ");

                    foreach(var bp in production.BlueprintClasses)
                    {
                        var name = bp.Id.SubtypeName; // bp.DisplayNameText; // some are really badly named, like BasicIngots -> Ingots, ugh.
                        var newLineIndex = name.IndexOf('\n');

                        if(newLineIndex != -1) // name contains a new line, ignore everything after that
                        {
                            for(int i = 0; i < newLineIndex; ++i)
                            {
                                GetLine().Append(name[i]);
                            }

                            GetLine().TrimEndWhitespace();
                        }
                        else
                        {
                            GetLine().Append(name);
                        }

                        GetLine().Append(", ");
                    }

                    GetLine().Length -= 2;
                }
            }
        }

        private void Format_OxygenFarm(MyCubeBlockDefinition def)
        {
            var oxygenFarm = (MyOxygenFarmDefinition)def; // does not extend MyProductionBlockDefinition

            PowerRequired(oxygenFarm.OperationalPowerConsumption, oxygenFarm.ResourceSinkGroup);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Produces").RoundedNumber(oxygenFarm.MaxGasOutput, 2).Append(" ").Append(oxygenFarm.ProducedGas.SubtypeName).Append(" l/s");

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(oxygenFarm.ResourceSourceGroup);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine(oxygenFarm.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red).Append(oxygenFarm.IsTwoSided ? "Two-sided" : "One-sided");
            }
        }

        private void Format_AirVent(MyCubeBlockDefinition def)
        {
            var vent = (MyAirVentDefinition)def; // does not extend MyProductionBlockDefinition

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Idle").PowerFormat(vent.StandbyPowerConsumption).Separator().Label("Operational").PowerFormat(vent.OperationalPowerConsumption);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(vent.ResourceSinkGroup);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Output - Rate").VolumeFormat(vent.VentilationCapacityPerSecond).Append("/s");

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(vent.ResourceSourceGroup);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings))
            {
                if(!MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization)
                    AddLine().Color(Color.Red).Append("Airtightness is disabled in this world");
                else if(!MyAPIGateway.Session.SessionSettings.EnableOxygen)
                    AddLine().Color(Color.Red).Append("Oxygen is disabled in this world");
            }
        }

        private void Format_UpgradeModule(MyCubeBlockDefinition def)
        {
            var upgradeModule = (MyUpgradeModuleDefinition)def;

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(upgradeModule.Upgrades == null || upgradeModule.Upgrades.Length == 0)
                {
                    AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Upgrades: N/A");
                }
                else
                {
                    AddLine().Append("Upgrades per slot:");

                    foreach(var upgrade in upgradeModule.Upgrades)
                    {
                        AddLine().Append("    - ").AppendUpgrade(upgrade);
                    }
                }
            }
        }

        private void Format_PowerProducer(MyCubeBlockDefinition def)
        {
            var powerProducer = (MyPowerProducerDefinition)def;

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Append("Power output: ").PowerFormat(powerProducer.MaxPowerOutput);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(powerProducer.ResourceSourceGroup);
            }

            var h2Engine = def as MyHydrogenEngineDefinition;
            if(h2Engine != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
                {
                    AddLine().Label("Needs fuel").IdTypeSubtypeFormat(h2Engine.Fuel.FuelId);
                    AddLine().Label("Consumption").VolumeFormat(h2Engine.MaxPowerOutput / h2Engine.FuelProductionToCapacityMultiplier).Append("/s");

                    if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(h2Engine.ResourceSinkGroup);

                    AddLine().Label("Fuel capacity").VolumeFormat(h2Engine.FuelCapacity);
                }

                return;
            }

            var reactor = def as MyReactorDefinition;
            if(reactor != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ItemInputs))
                {
                    if(reactor.FuelInfos != null && reactor.FuelInfos.Length > 0)
                    {
                        bool hasOneFuel = (reactor.FuelInfos.Length == 1);

                        if(hasOneFuel)
                            AddLine().Append("Needs fuel: ");
                        else
                            AddLine().Color(COLOR_WARNING).Append("Needs combined fuels:").ResetColor();

                        foreach(var fuel in reactor.FuelInfos)
                        {
                            if(!hasOneFuel)
                                AddLine().Append("       - ");

                            GetLine().IdTypeSubtypeFormat(fuel.FuelId).Append(" (").RoundedNumber(fuel.ConsumptionPerSecond_Items, 5).Append("/s)");
                        }
                    }
                }

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
                {
                    var volume = (reactor.InventoryMaxVolume > 0 ? reactor.InventoryMaxVolume : reactor.InventorySize.Volume);
                    var invLimit = reactor.InventoryConstraint;

                    if(invLimit != null)
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume, reactor.InventoryConstraint);

                        if(Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryExtras))
                        {
                            AddLine(MyFontEnum.Blue).Color(COLOR_WARNING).Append("Inventory items ").Append(invLimit.IsWhitelist ? "allowed" : "NOT allowed").Append(":");

                            foreach(var id in invLimit.ConstrainedIds)
                            {
                                AddLine().Append("       - ").IdTypeSubtypeFormat(id);
                            }

                            foreach(var type in invLimit.ConstrainedTypes)
                            {
                                AddLine().Append("       - All of type: ").IdTypeFormat(type);
                            }
                        }
                    }
                    else
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume);
                    }
                }

                return;
            }

            var battery = def as MyBatteryBlockDefinition;
            if(battery != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
                {
                    AddLine(battery.AdaptibleInput ? MyFontEnum.White : MyFontEnum.Red).Append("Power input: ").PowerFormat(battery.RequiredPowerInput).Append(battery.AdaptibleInput ? " (adaptable)" : " (minimum required)");

                    if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority(battery.ResourceSinkGroup);
                }

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
                {
                    AddLine().Append("Power capacity: ").PowerStorageFormat(battery.MaxStoredPower).Separator().Append("Pre-charged: ").PowerStorageFormat(battery.MaxStoredPower * battery.InitialStoredPowerRatio).Append(" (").ProportionToPercent(battery.InitialStoredPowerRatio).Append(')');
                }

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine().Append("Discharge time: ").TimeFormat((battery.MaxStoredPower / battery.MaxPowerOutput) * 3600f).Separator().Append("Recharge time: ").TimeFormat((battery.MaxStoredPower / battery.RequiredPowerInput) * 3600f);
                }

                return;
            }

            var solarPanel = def as MySolarPanelDefinition;
            if(solarPanel != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine(solarPanel.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red).Append(solarPanel.IsTwoSided ? "Two-sided" : "One-sided");
                }

                return;
            }

            var windTurbine = def as MyWindTurbineDefinition;
            if(windTurbine != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine().Label("Clearence - Ground").DistanceFormat(windTurbine.OptimalGroundClearance).Separator().Label("Sides").DistanceFormat(windTurbine.RaycasterSize);
                    AddLine().Label("Optimal wind speed").RoundedNumber(windTurbine.OptimalWindSpeed, 2);
                    // TODO wind speed unit?
                }

                return;
            }
        }
        #endregion

        #region Communication
        private void Format_RadioAntenna(MyCubeBlockDefinition def)
        {
            var radioAntenna = (MyRadioAntennaDefinition)def;

            PowerRequired(Hardcoded.RadioAntenna_PowerReq(radioAntenna.MaxBroadcastRadius), radioAntenna.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max radius").DistanceFormat(radioAntenna.MaxBroadcastRadius);
            }
        }

        private void Format_LaserAntenna(MyCubeBlockDefinition def)
        {
            var laserAntenna = (MyLaserAntennaDefinition)def;

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Active[1]").PowerFormat(Hardcoded.LaserAntenna_PowerUsage(laserAntenna, 1000)).Append(" per km ").Color(COLOR_UNIMPORTANT).Append("(/buildinfo help)");
                AddLine().Label("Power - Turning").PowerFormat(laserAntenna.PowerInputTurning).Separator().Label("Idle").PowerFormat(laserAntenna.PowerInputIdle);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(laserAntenna.ResourceSinkGroup);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine(laserAntenna.RequireLineOfSight ? MyFontEnum.White : MyFontEnum.Green)
                    .Color(laserAntenna.MaxRange < 0 ? COLOR_GOOD : COLOR_NORMAL).Append("Range: ");

                if(laserAntenna.MaxRange < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat(laserAntenna.MaxRange);

                GetLine().ResetColor().Separator().Color(laserAntenna.RequireLineOfSight ? COLOR_WARNING : COLOR_GOOD).Append("Line-of-sight: ").Append(laserAntenna.RequireLineOfSight ? "Required" : "Not required");

                AddLine().Label("Rotation Pitch").AngleFormatDeg(laserAntenna.MinElevationDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxElevationDegrees).Separator().Label("Yaw").AngleFormatDeg(laserAntenna.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxAzimuthDegrees);
                AddLine().Label("Rotation Speed").RotationSpeed(laserAntenna.RotationRate * Hardcoded.LaserAntenna_RotationSpeedMul);

                // TODO visualize angle limits?
            }
        }

        private void Format_Beacon(MyCubeBlockDefinition def)
        {
            var beacon = (MyBeaconDefinition)def;

            PowerRequired(Hardcoded.Beacon_PowerReq(beacon.MaxBroadcastRadius), beacon.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max radius").DistanceFormat(beacon.MaxBroadcastRadius);
            }
        }
        #endregion

        private void Format_Timer(MyCubeBlockDefinition def)
        {
            var timer = (MyTimerBlockDefinition)def;

            PowerRequired(Hardcoded.Timer_PowerReq, timer.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Timer range").TimeFormat(timer.MinDelay / 1000f).Append(" to ").TimeFormat(timer.MaxDelay / 1000f);
            }
        }

        private void Format_ProgrammableBlock(MyCubeBlockDefinition def)
        {
            var pb = (MyProgrammableBlockDefinition)def;

            PowerRequired(Hardcoded.ProgrammableBlock_PowerReq, pb.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings))
            {
                if(!MyAPIGateway.Session.SessionSettings.EnableIngameScripts)
                {
                    AddLine().Color(Color.Red).Append("In-game Scripts are disabled in this world");
                }
                else if(MyAPIGateway.Session.SessionSettings.EnableScripterRole && MyAPIGateway.Session.Player.PromoteLevel < MyPromoteLevel.Scripter)
                {
                    AddLine().Color(Color.Red).Append("Scripter role required to use In-game Scripts");
                }
            }
        }

        private void Format_LCD(MyCubeBlockDefinition def)
        {
            var lcd = (MyTextPanelDefinition)def;

            PowerRequired(lcd.RequiredPowerInput, lcd.ResourceSinkGroup);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Screen resolution").Append(lcd.TextureResolution * lcd.TextureAspectRadio).Append("x").Append(lcd.TextureResolution);
                AddLine().Label("Font size limits").RoundedNumber(lcd.MinFontSize, 4).Append(" to ").RoundedNumber(lcd.MaxFontSize, 4);
            }
        }

        private void Format_SoundBlock(MyCubeBlockDefinition def)
        {
            var sound = (MySoundBlockDefinition)def;

            PowerRequired(Hardcoded.SoundBlock_PowerReq, sound.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Range").DistanceRangeFormat(sound.MinRange, sound.MaxRange);
                AddLine().Label("Max loop time").TimeFormat(sound.MaxLoopPeriod);

                // EmitterNumber and LoopUpdateThreshold seem unused
            }
        }

        private void Format_Sensor(MyCubeBlockDefinition def)
        {
            var sensor = (MySensorBlockDefinition)def;

            var maxField = Hardcoded.Sensor_MaxField(sensor.MaxRange);

            PowerRequired(Hardcoded.Sensor_PowerReq(maxField), sensor.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Max area").VectorFormat(maxField);
            }
        }

        private void Format_Camera(MyCubeBlockDefinition def)
        {
            var camera = (MyCameraBlockDefinition)def;

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Normal use").PowerFormat(camera.RequiredPowerInput).Separator().Label("Raycast charging").PowerFormat(camera.RequiredChargingInput);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().Separator().ResourcePriority(camera.ResourceSinkGroup);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Field of view").AngleFormat(camera.MinFov).Append(" to ").AngleFormat(camera.MaxFov);
                AddLine().Label("Raycast - Cone limit").AngleFormatDeg(camera.RaycastConeLimit).Separator().Label("Distance limit");

                if(camera.RaycastDistanceLimit < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat((float)camera.RaycastDistanceLimit);

                GetLine().Separator().Label("Time multiplier").RoundedNumber(camera.RaycastTimeMultiplier, 2);

                // TODO visualize angle limits?
            }
        }

        private void Format_Button(MyCubeBlockDefinition def)
        {
            var button = (MyButtonPanelDefinition)def;

            PowerRequired(Hardcoded.ButtonPanel_PowerReq, button.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Button count").Append(button.ButtonCount);
            }
        }

        #region Magic blocks
        private void Format_GravityGenerator(MyCubeBlockDefinition def)
        {
            var gravGen = (MyGravityGeneratorBaseDefinition)def;

            var flatGravGen = def as MyGravityGeneratorDefinition;
            if(flatGravGen != null)
            {
                PowerRequired(flatGravGen.RequiredPowerInput, flatGravGen.ResourceSinkGroup);

                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    AddLine().Label("Field size").VectorFormat(flatGravGen.MinFieldSize).Append(" to ").VectorFormat(flatGravGen.MaxFieldSize);
                }
            }
            else
            {
                var sphereGravGen = def as MyGravityGeneratorSphereDefinition;
                if(sphereGravGen != null)
                {
                    PowerRequired(Hardcoded.SphericalGravGen_PowerReq(sphereGravGen, sphereGravGen.MaxRadius, sphereGravGen.MaxGravityAcceleration), sphereGravGen.ResourceSinkGroup, powerHardcoded: true);

                    if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                    {
                        AddLine().Label("Radius").DistanceFormat(sphereGravGen.MinRadius).Append(" to ").DistanceFormat(sphereGravGen.MaxRadius);
                    }
                }
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Acceleration").ForceFormat(gravGen.MinGravityAcceleration).Append(" to ").ForceFormat(gravGen.MaxGravityAcceleration);
            }
        }

        private void Format_ArtificialMass(MyCubeBlockDefinition def)
        {
            var artificialMass = (MyVirtualMassDefinition)def;

            PowerRequired(artificialMass.RequiredPowerInput, artificialMass.ResourceSinkGroup);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Artificial mass").MassFormat(artificialMass.VirtualMass);
            }
        }

        private void Format_SpaceBall(MyCubeBlockDefinition def)
        {
            var spaceBall = (MySpaceBallDefinition)def; // this doesn't extend MyVirtualMassDefinition

            // HACK: hardcoded; SpaceBall doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine().Label("Max artificial mass").MassFormat(spaceBall.MaxVirtualMass);
            }
        }

        private void Format_JumpDrive(MyCubeBlockDefinition def)
        {
            var jumpDrive = (MyJumpDriveDefinition)def;

            PowerRequired(jumpDrive.RequiredPowerInput, jumpDrive.ResourceSinkGroup);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                AddLine().Label("Power storage for jump").PowerStorageFormat(jumpDrive.PowerNeededForJump);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Charge time").TimeFormat((jumpDrive.PowerNeededForJump / jumpDrive.RequiredPowerInput) * 3600f);
                AddLine().Label("Jump delay").TimeFormat(jumpDrive.JumpDelay);
                AddLine().Label("Max distance").DistanceFormat((float)jumpDrive.MaxJumpDistance);
                AddLine().Label("Max mass").MassFormat((float)jumpDrive.MaxJumpMass);
            }
        }
        #endregion

        private void Format_Weapon(MyCubeBlockDefinition def)
        {
            var weapon = (MyWeaponBlockDefinition)def;
            var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);

            if(wepDef == null)
            {
                AddLine(MyFontEnum.Red).Color(Color.Red).Append("Block error: can't find weapon definition: ").Append(weapon.WeaponDefinitionId.ToString());
                return;
            }

            var turret = def as MyLargeTurretBaseDefinition;
            float requiredPowerInput = (turret != null ? Hardcoded.Turret_PowerReq : Hardcoded.ShipGun_PowerReq);

            PowerRequired(requiredPowerInput, weapon.ResourceSinkGroup, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                AddLine().Label("Inventory").InventoryFormat(weapon.InventoryMaxVolume, wepDef.AmmoMagazinesId);
            }

            if(turret != null)
            {
                if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine().Color(turret.AiEnabled ? COLOR_GOOD : COLOR_BAD).Label("Auto-target").BoolFormat(turret.AiEnabled).ResetColor().Append(turret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)").Separator().Color(COLOR_WARNING).Append("Max range: ").DistanceFormat(turret.MaxRangeMeters);
                    AddLine().Append("Rotation - ");

                    if(turret.MinElevationDegrees <= -180 && turret.MaxElevationDegrees >= 180)
                        GetLine().Color(COLOR_GOOD).Append("Pitch: ").AngleFormatDeg(360);
                    else
                        GetLine().Color(COLOR_WARNING).Append("Pitch: ").AngleFormatDeg(turret.MinElevationDegrees).Append(" to ").AngleFormatDeg(turret.MaxElevationDegrees);

                    GetLine().ResetColor().Append(" @ ").RotationSpeed(turret.ElevationSpeed * Hardcoded.Turret_RotationSpeedMul).Separator();

                    if(turret.MinAzimuthDegrees <= -180 && turret.MaxAzimuthDegrees >= 180)
                        GetLine().Color(COLOR_GOOD).Append("Yaw: ").AngleFormatDeg(360);
                    else
                        GetLine().Color(COLOR_WARNING).Append("Yaw: ").AngleFormatDeg(turret.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(turret.MaxAzimuthDegrees);

                    GetLine().ResetColor().Append(" @ ").RotationSpeed(turret.RotationSpeed * Hardcoded.Turret_RotationSpeedMul);

                    // TODO visualize angle limits?
                }
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Label("Accuracy").DistanceFormat((float)Math.Tan(wepDef.DeviateShotAngle) * 200).Append(" group at 100m").Separator().Append("Reload: ").TimeFormat(wepDef.ReloadTime / 1000);
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.AmmoDetails))
            {
                ammoProjectiles.Clear();
                ammoMissiles.Clear();

                for(int i = 0; i < wepDef.AmmoMagazinesId.Length; i++)
                {
                    var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[i]);
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                    int ammoType = (int)ammo.AmmoType;

                    if(wepDef.WeaponAmmoDatas[ammoType] != null)
                    {
                        switch(ammoType)
                        {
                            case 0: ammoProjectiles.Add(MyTuple.Create(mag, (MyProjectileAmmoDefinition)ammo)); break;
                            case 1: ammoMissiles.Add(MyTuple.Create(mag, (MyMissileAmmoDefinition)ammo)); break;
                        }
                    }
                }

                if(ammoProjectiles.Count > 0)
                {
                    // TODO check if wepDef.DamageMultiplier is used for ship weapons (in 1.189 it's only used for handheld weapons)

                    var projectilesData = wepDef.WeaponAmmoDatas[0];

                    AddLine().Label("Projectiles - Fire rate").Append(Math.Round(projectilesData.RateOfFire / 60f, 3)).Append(" rounds/s")
                        .Separator().Color(projectilesData.ShotsInBurst == 0 ? COLOR_GOOD : COLOR_WARNING).Append("Magazine: ");
                    if(projectilesData.ShotsInBurst == 0)
                        GetLine().Append("No reloading");
                    else
                        GetLine().Append(projectilesData.ShotsInBurst);

                    AddLine().Append("Projectiles - ").Color(COLOR_PART).Append("Type").ResetColor().Append(" (")
                        .Color(COLOR_STAT_SHIPDMG).Append("ship").ResetColor().Append(", ")
                        .Color(COLOR_STAT_CHARACTERDMG).Append("character").ResetColor().Append(", ")
                        .Color(COLOR_STAT_HEADSHOTDMG).Append("headshot").ResetColor().Append(", ")
                        .Color(COLOR_STAT_SPEED).Append("speed").ResetColor().Append(", ")
                        .Color(COLOR_STAT_TRAVEL).Append("travel").ResetColor().Append(")");

                    for(int i = 0; i < ammoProjectiles.Count; ++i)
                    {
                        var data = ammoProjectiles[i];
                        var mag = data.Item1;
                        var ammo = data.Item2;

                        AddLine().Append("      - ").Color(COLOR_PART).Append(mag.Id.SubtypeName).ResetColor().Append(" (");

                        if(ammo.ProjectileCount > 1)
                            GetLine().Color(COLOR_STAT_PROJECTILECOUNT).Append(ammo.ProjectileCount).Append("x ");

                        GetLine().Color(COLOR_STAT_SHIPDMG).Append(ammo.ProjectileMassDamage).ResetColor().Append(", ")
                            .Color(COLOR_STAT_CHARACTERDMG).Append(ammo.ProjectileHealthDamage).ResetColor().Append(", ")
                            .Color(COLOR_STAT_HEADSHOTDMG).Append(ammo.HeadShot ? ammo.ProjectileHeadShotDamage : ammo.ProjectileHealthDamage).ResetColor().Append(", ");

                        if(ammo.SpeedVar > 0)
                            GetLine().Color(COLOR_STAT_SPEED).Number(ammo.DesiredSpeed * (1f - ammo.SpeedVar)).Append("~").Number(ammo.DesiredSpeed * (1f + ammo.SpeedVar)).Append(" m/s");
                        else
                            GetLine().Color(COLOR_STAT_SPEED).SpeedFormat(ammo.DesiredSpeed);

                        GetLine().ResetColor().Append(", ")
                            .Color(COLOR_STAT_TRAVEL).DistanceRangeFormat(ammo.MaxTrajectory * Hardcoded.Projectile_RangeMultiplier_Min, ammo.MaxTrajectory * Hardcoded.Projectile_RangeMultiplier_Max).ResetColor().Append(")");
                    }
                }

                if(ammoMissiles.Count > 0)
                {
                    var missileData = wepDef.WeaponAmmoDatas[1];

                    AddLine().Label("Missiles - Fire rate").Append(Math.Round(missileData.RateOfFire / 60f, 3)).Append(" rounds/s")
                        .Separator().Color(missileData.ShotsInBurst == 0 ? COLOR_GOOD : COLOR_WARNING).Append("Magazine: ");
                    if(missileData.ShotsInBurst == 0)
                        GetLine().Append("No reloading");
                    else
                        GetLine().Append(missileData.ShotsInBurst);

                    AddLine().Append("Missiles - ").Color(COLOR_PART).Append("Type").ResetColor().Append(" (")
                        .Color(COLOR_STAT_SHIPDMG).Append("damage").ResetColor().Append(", ")
                        .Color(COLOR_STAT_CHARACTERDMG).Append("radius").ResetColor().Append(", ")
                        .Color(COLOR_STAT_SPEED).Append("speed").ResetColor().Append(", ")
                        .Color(COLOR_STAT_TRAVEL).Append("travel").ResetColor().Append(")");

                    for(int i = 0; i < ammoMissiles.Count; ++i)
                    {
                        var data = ammoMissiles[i];
                        var mag = data.Item1;
                        var ammo = data.Item2;

                        AddLine().Append("      - ").Color(COLOR_PART).Append(mag.Id.SubtypeName).ResetColor().Append(" (")
                            .Color(COLOR_STAT_SHIPDMG).Append(ammo.MissileExplosionDamage).ResetColor().Append(", ")
                            .Color(COLOR_STAT_CHARACTERDMG).DistanceFormat(ammo.MissileExplosionRadius).ResetColor().Append(", ");

                        // SpeedVar is not used for missiles

                        GetLine().Color(COLOR_STAT_SPEED);

                        if(!ammo.MissileSkipAcceleration)
                            GetLine().SpeedFormat(ammo.MissileInitialSpeed).Append(" + ").AccelerationFormat(ammo.MissileAcceleration);
                        else
                            GetLine().SpeedFormat(ammo.DesiredSpeed * Hardcoded.Missile_DesiredSpeedMultiplier);

                        GetLine().ResetColor().Append(", ").Color(COLOR_STAT_TRAVEL).DistanceFormat(ammo.MaxTrajectory)
                            .ResetColor().Append(")");
                    }
                }

                ammoProjectiles.Clear();
                ammoMissiles.Clear();
            }

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings) && !MyAPIGateway.Session.SessionSettings.WeaponsEnabled)
            {
                AddLine().Color(Color.Red).Append("Weapons are disabled in this world");
            }
        }

        private void Format_Warhead(MyCubeBlockDefinition def)
        {
            var warhead = (MyWarheadDefinition)def; // does not extend MyWeaponBlockDefinition

            // HACK: hardcoded; Warhead doesn't require power
            PowerRequired(0, null, powerHardcoded: true);

            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.AmmoDetails))
            {
                AddLine().Label("Radius").DistanceFormat(warhead.ExplosionRadius);
                AddLine().Label("Damage").AppendFormat("{0:#,###,###,###,##0.##}", warhead.WarheadExplosionDamage);
            }
        }
        #endregion

        private void PowerRequired(float mw, string groupName, bool powerHardcoded = false, bool groupHardcoded = false)
        {
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                var groupNameHash = (groupName != null ? MyStringHash.GetOrCompute(groupName) : MyStringHash.NullOrEmpty);
                PowerRequired(mw, groupNameHash, powerHardcoded);
            }
        }

        private void PowerRequired(float mw, MyStringHash groupName, bool powerHardcoded = false)
        {
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                var color = (mw <= 0 ? COLOR_GOOD : COLOR_NORMAL);

                if(powerHardcoded)
                    AddLine().Color(color).Label("Power required");
                else
                    AddLine().Color(color).LabelHardcoded("Power required", color);

                if(mw <= 0)
                    GetLine().Append("No");
                else
                    GetLine().PowerFormat(mw);

                if(groupName != MyStringHash.NullOrEmpty && Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                    GetLine().ResetColor().Separator().ResourcePriority(groupName);
            }
        }

        private void InventoryStats(MyCubeBlockDefinition def, float alternateVolume, float hardcodedVolume)
        {
            if(Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                float volume;
                if(!Utilities.GetInventoryFromComponent(def, out volume))
                    volume = alternateVolume;

                if(volume > 0)
                    AddLine().Label("Inventory").InventoryFormat(volume);
                else if(hardcodedVolume > 0)
                    AddLine().LabelHardcoded("Inventory").InventoryFormat(hardcodedVolume);
                // else unknown inventory /shrug
            }
        }

        private readonly List<MyDefinitionId> removeCacheIds = new List<MyDefinitionId>();
        private void PurgeCache()
        {
            var haveNotifCache = CachedBuildInfoNotification.Count > 0;
            var haveTextAPICache = CachedBuildInfoTextAPI.Count > 0;

            if(haveNotifCache || haveTextAPICache)
            {
                removeCacheIds.Clear();
                var time = DateTime.UtcNow.Ticks;

                if(haveNotifCache)
                {
                    foreach(var kv in CachedBuildInfoNotification)
                        if(kv.Value.expires < time)
                            removeCacheIds.Add(kv.Key);

                    if(CachedBuildInfoNotification.Count == removeCacheIds.Count)
                        CachedBuildInfoNotification.Clear();
                    else
                        foreach(var key in removeCacheIds)
                            CachedBuildInfoNotification.Remove(key);

                    removeCacheIds.Clear();
                }

                if(haveTextAPICache)
                {
                    foreach(var kv in CachedBuildInfoTextAPI)
                        if(kv.Value.expires < time)
                            removeCacheIds.Add(kv.Key);

                    if(CachedBuildInfoTextAPI.Count == removeCacheIds.Count)
                        CachedBuildInfoTextAPI.Clear();
                    else
                        foreach(var key in removeCacheIds)
                            CachedBuildInfoTextAPI.Remove(key);

                    removeCacheIds.Clear();
                }
            }
        }

        public void OnConfigReloaded()
        {
            Refresh();
        }

        public void Refresh(bool redraw = false)
        {
            HideText();
            CachedBuildInfoTextAPI.Clear();

            if(textObject != null)
            {
                textObject.Scale = TextAPIScale;

                if(Config.TextAlwaysVisible)
                {
                    textObject.Options &= ~HudAPIv2.Options.HideHud;
                    bgObject.Options &= ~HudAPIv2.Options.HideHud;
                }
                else
                {
                    textObject.Options |= HudAPIv2.Options.HideHud;
                    bgObject.Options |= HudAPIv2.Options.HideHud;
                }
            }

            if(redraw)
                UpdateTextAPIvisuals(textAPIlines);
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
            public readonly StringBuilder Text = new StringBuilder();
            public readonly Vector2D TextSize;

            public CacheTextAPI(StringBuilder textSB, Vector2D textSize)
            {
                ResetExpiry();
                Text.AppendSB(textSB);
                TextSize = textSize;
            }
        }

        public class CacheNotifications : Cache
        {
            public readonly List<IMyHudNotification> Lines = new List<IMyHudNotification>();

            public CacheNotifications(List<HudLine> hudLines)
            {
                ResetExpiry();

                for(int i = 0; i < hudLines.Count; ++i)
                {
                    var line = hudLines[i];

                    if(line.str.Length > 0)
                    {
                        Lines.Add(MyAPIGateway.Utilities.CreateNotification(line.str.ToString(), 16, line.font));
                    }
                }
            }
        }

        public class HudLine
        {
            public StringBuilder str = new StringBuilder();
            public string font;
            public int lineWidthPx;
        }
        #endregion
    }
}