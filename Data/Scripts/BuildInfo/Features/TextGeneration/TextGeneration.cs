using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Xml.Serialization;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Digi.Input;
using Draygo.API;
using ObjectBuilders.SafeZone;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Definitions.SafeZone;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using WeaponCore.Api;
using Whiplash.WeaponFramework;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

// FIXME: internal info not vanishing on older cached blocks text when resetting config
// FIXME: box size gets cached from overlay lock on or something

namespace Digi.BuildInfo.Features
{
    public enum GridSplitType
    {
        Recalculate,
        NoSplit,
        Split,
    }

    public class TextGeneration : ModComponent
    {
        #region Constants
        private const BlendTypeEnum FG_BLEND_TYPE = BlendTypeEnum.PostPP;

        private readonly MyStringId BG_MATERIAL = MyStringId.GetOrCompute("Square");
        private const BlendTypeEnum BG_BLEND_TYPE = BlendTypeEnum.PostPP;
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
        public readonly Color COLOR_BAD = Color.Red;
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

        public readonly Color COLOR_STAT_TYPE = new Color(55, 255, 155);
        public readonly Color COLOR_STAT_PROJECTILECOUNT = new Color(0, 255, 0);
        public readonly Color COLOR_STAT_SHIPDMG = new Color(0, 255, 200);
        public readonly Color COLOR_STAT_CHARACTERDMG = new Color(255, 155, 0);
        public readonly Color COLOR_STAT_HEADSHOTDMG = new Color(255, 0, 0);
        public readonly Color COLOR_STAT_SPEED = new Color(0, 200, 255);
        public readonly Color COLOR_STAT_TRAVEL = new Color(55, 80, 255);
        #endregion Constants

        public MyDefinitionId LastDefId; // last selected definition ID, can be set to MENU_DEFID too!
        public bool textShown = false;
        private bool aimInfoNeedsUpdate = false;
        private GridSplitType willSplitGrid;
        private readonly HashSet<IMySlimBlock> ProjectedUnder = new HashSet<IMySlimBlock>();
        public Vector3D lastGizmoPosition;
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
        private HudAPIv2.HUDMessage textObject = null;
        private HudAPIv2.BillBoardHUDMessage bgObject = null;
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

        public TextGeneration(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
        }

        public override void RegisterComponent()
        {
            InitLookups();

            Main.TextAPI.Detected += TextAPI_APIDetected;
            Main.GameConfig.HudStateChanged += GameConfig_HudStateChanged;
            Main.GameConfig.OptionsMenuClosed += GameConfig_OptionsMenuClosed;
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;

            ReCheckSide();
        }

        public override void UnregisterComponent()
        {
            Main.TextAPI.Detected -= TextAPI_APIDetected;
            Main.GameConfig.HudStateChanged -= GameConfig_HudStateChanged;
            Main.GameConfig.OptionsMenuClosed -= GameConfig_OptionsMenuClosed;
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
        }

        private void TextAPI_APIDetected()
        {
            // FIXME: doesn't re-show the menu if in it while this happens...
            Main.TextGeneration.HideText(); // force a re-check to make the HUD -> textAPI transition
        }

        private void GameConfig_HudStateChanged(HudState prevState, HudState state)
        {
            ReCheckSide();
        }

        private void GameConfig_OptionsMenuClosed()
        {
            ReCheckSide();

            if(Math.Abs(prevAspectRatio - Main.GameConfig.AspectRatio) > 0.0001)
            {
                prevAspectRatio = Main.GameConfig.AspectRatio;
                CachedBuildInfoTextAPI.Clear();
            }
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            willSplitGrid = GridSplitType.Recalculate;
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
                bgObject.Draw();
                textObject.Draw();
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
            if(MyAPIGateway.CubeBuilder.FreezeGizmo && Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.WorldMatrix.Translation, lastGizmoPosition) > FREEZE_MAX_DISTANCE_SQ)
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

                    if(Main.Config.TextShow.Value)
                    {
                        if(hasAimedBlock)
                        {
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

            return true;
        }

        #region Text handling
        public void PostProcessText(MyDefinitionId id, bool useCache)
        {
            if(Main.TextAPI.IsEnabled)
            {
                textAPIlines.TrimEndWhitespace();

                Vector2D textSize = UpdateTextAPIvisuals(textAPIlines);

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

                    if(CachedBuildInfoNotification.ContainsKey(id))
                        CachedBuildInfoNotification[id] = cache;
                    else
                        CachedBuildInfoNotification.Add(id, cache);
                }
            }
        }

        private Vector2D UpdateTextAPIvisuals(StringBuilder textSB, Vector2D textSize = default(Vector2D))
        {
            if(bgObject == null)
            {
                bgObject = new HudAPIv2.BillBoardHUDMessage(BG_MATERIAL, Vector2D.Zero, Color.White, HideHud: !Main.Config.TextAlwaysVisible.Value, Shadowing: true, Blend: BG_BLEND_TYPE); // scale on bg must always remain 1
                bgObject.Visible = false;
                textObject = new HudAPIv2.HUDMessage(new StringBuilder(TEXTAPI_TEXT_LENGTH), Vector2D.Zero, Scale: Main.Config.TextAPIScale.Value, HideHud: !Main.Config.TextAlwaysVisible.Value, Blend: FG_BLEND_TYPE);
                textObject.Visible = false;
            }

            //bgObject.Visible = true;
            //textObject.Visible = true;

            #region Update text and count lines
            StringBuilder msg = textObject.Message;
            msg.Clear().EnsureCapacity(msg.Length + textSB.Length);
            lines = 0;

            for(int i = 0; i < textSB.Length; i++)
            {
                char c = textSB[i];

                msg.Append(c);

                if(c == '\n')
                    lines++;
            }

            textObject.Flush();
            #endregion Update text and count lines

            Vector2D textPos = Vector2D.Zero;
            Vector2D textOffset = Vector2D.Zero;

            // calculate text size if it wasn't inputted
            if(Math.Abs(textSize.X) <= 0.0001 && Math.Abs(textSize.Y) <= 0.0001)
                textSize = textObject.GetTextLength();

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

            textObject.Origin = textPos;
            textObject.Offset = textOffset;

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

                bgObject.BillBoardColor = color;
                bgObject.Origin = textPos;
                bgObject.Width = (float)Math.Abs(textSize.X) + edge;
                bgObject.Height = (float)Math.Abs(textSize.Y) + edge;
                bgObject.Offset = textOffset + (textSize / 2);
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
                if(MyAPIGateway.Gui.IsCursorVisible || (!Main.Config.TextShow.Value && !Main.QuickMenu.Shown))
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

                if(MyAPIGateway.Gui.IsCursorVisible || (!Main.Config.TextShow.Value && !Main.QuickMenu.Shown))
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
                textAPIlines.NewCleanLine();
            }
            else
            {
                int px = GetStringSizeNotif(notificationLines[line].str);

                largestLineWidth = Math.Max(largestLineWidth, px);

                notificationLines[line].lineWidthPx = px;
            }
        }

        private StringBuilder GetLine()
        {
            return (Main.TextAPI.IsEnabled ? textAPIlines : notificationLines[line].str);
        }

        private void AddOverlaysHint(MyCubeBlockDefinition def)
        {
            // TODO: remove last condition when adding overlay to WC
            if(Main.Overlays.DrawLookup.ContainsKey(def.Id.TypeId) && !Main.WeaponCoreAPIHandler.Weapons.ContainsKey(def.Id))
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

            AddLine(FontsHandler.SkyBlueSh).Color(COLOR_BLOCKTITLE).Append(BuildInfoMod.MOD_NAME).Append(" mod");

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

            AddMenuItemLine(i++).Append("Text info: ").Append(Main.Config.TextShow.Value ? "ON" : "OFF");

            AddMenuItemLine(i++).Append("Draw overlays: ").Append(Main.Overlays.OverlayNames[Main.Overlays.DrawOverlay]);
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
            bool projected = (projectedBy != null);
            float integrityRatio = (projected ? 0 : aimedBlock.Integrity / aimedBlock.MaxIntegrity);
            IMyCubeGrid grid = (projected ? projectedBy.CubeGrid : aimedBlock.CubeGrid);

            IMyTerminalBlock terminalBlock = aimedBlock.FatBlock as IMyTerminalBlock;
            bool hasComputer = (terminalBlock != null && def.ContainsComputer());

            #region Block name
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.TerminalName))
            {
                if(terminalBlock != null)
                {
                    AddLine().Append('"').Color(COLOR_BLOCKTITLE).AppendMaxLength(terminalBlock.CustomName, BLOCK_NAME_MAX_LENGTH).ResetFormatting().Append('"');
                }
                else if(projected) // show block def name because game might not.
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
            }
            #endregion Internal info

            #region Mass, grid mass
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.Mass))
            {
                float mass = (def.HasPhysics ? def.Mass : 0); // HACK: game doesn't use mass from blocks with HasPhysics=false
                Color massColor = Color.GreenYellow;

                if(projected)
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
            if(projected && Main.Config.AimInfo.IsSet(AimInfoFlags.Projected))
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
            IMyCubeGrid nearbyProjectedGrid = Main.EquipmentMonitor.NearbyProjector?.ProjectedGrid;
            if(!projected && nearbyProjectedGrid != null && Main.Config.AimInfo.IsSet(AimInfoFlags.Projected))
            {
                ProjectedUnder.Clear();
                string firstProjectedName = null;

                Vector3I min = aimedBlock.Min;
                Vector3I max = aimedBlock.Max;
                Vector3I_RangeIterator iterator = new Vector3I_RangeIterator(ref min, ref max);
                while(iterator.IsValid())
                {
                    IMySlimBlock projectedUnder = nearbyProjectedGrid.GetCubeBlock(iterator.Current);
                    if(projectedUnder != null && projectedUnder.BlockDefinition.Id != aimedBlock.BlockDefinition.Id)
                    {
                        if(firstProjectedName == null)
                            firstProjectedName = projectedUnder.BlockDefinition.DisplayNameText;

                        ProjectedUnder.Add(projectedUnder);
                    }

                    iterator.MoveNext();
                }

                int projectedUnderCount = ProjectedUnder.Count;
                if(projectedUnderCount == 1)
                {
                    AddLine().Color(COLOR_BAD).Label("Projected under").Append(firstProjectedName);
                }
                else if(projectedUnderCount > 1)
                {
                    AddLine().Color(COLOR_BAD).Label("Projected under").Append(projectedUnderCount).Append(" blocks");
                }

                ProjectedUnder.Clear();
            }
            #endregion Different block projected under this one

            #region Integrity
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.Integrity))
            {
                if(projected)
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
            if(!projected && Main.Config.AimInfo.IsSet(AimInfoFlags.DamageMultiplier))
            {
                // MySlimBlock.BlockGeneralDamageModifier is inaccessible
                float dmgMul = aimedBlock.DamageRatio * def.GeneralDamageMultiplier;
                float gridDmgMul = ((MyCubeGrid)grid).GridGeneralDamageModifier;

                if(dmgMul != 1 || gridDmgMul != 1)
                {
                    AddLine();
                    ResistanceFormat(dmgMul);

                    if(gridDmgMul != 1)
                    {
                        GetLine().Separator();
                        ResistanceFormat(gridDmgMul, label: "Grid");
                    }
                }

                // TODO: impact resistance? wheels in particular...
            }
            #endregion Optional: intake damage multiplier

            #region Optional: ownership
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.Ownership))
            {
                if(!projected && hasComputer)
                {
                    MyRelationsBetweenPlayerAndBlock relation = (aimedBlock.OwnerId > 0 ? localPlayer.GetRelationTo(aimedBlock.OwnerId) : MyRelationsBetweenPlayerAndBlock.NoOwnership);
                    MyOwnershipShareModeEnum shareMode = Utils.GetBlockShareMode(aimedBlock.FatBlock);

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

                        // NOTE: MyVisualScriptLogicProvider.GetPlayersName() returns local player on id 0 and id 0 is also used for "nobody" in ownership.
                        string factionTag = aimedBlock.FatBlock.GetOwnerFactionTag();

                        if(!string.IsNullOrEmpty(factionTag))
                            GetLine().Append(factionTag).Append('.');

                        GetLine().AppendMaxLength(MyVisualScriptLogicProvider.GetPlayersName(aimedBlock.FatBlock.OwnerId), PLAYER_NAME_MAX_LENGTH);
                    }

                    GetLine().ResetFormatting().Separator();

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
                else if(projected)
                {
                    MyRelationsBetweenPlayerAndBlock relation = (projectedBy.OwnerId > 0 ? localPlayer.GetRelationTo(projectedBy.OwnerId) : MyRelationsBetweenPlayerAndBlock.NoOwnership);

                    AddLine();

                    if(relation == MyRelationsBetweenPlayerAndBlock.Enemies || relation == MyRelationsBetweenPlayerAndBlock.Neutral)
                        GetLine().Color(COLOR_BAD);
                    else if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                        GetLine().Color(COLOR_OWNER);
                    else if(relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                        GetLine().Color(COLOR_GOOD);
                    else if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                        GetLine().Color(COLOR_WARNING);

                    if(projectedBy.OwnerId == 0)
                    {
                        GetLine().Append("Projector not owned");
                    }
                    else
                    {
                        GetLine().Append("Projector owner: ");

                        // NOTE: MyVisualScriptLogicProvider.GetPlayersName() returns local player on id 0 and id 0 is also use for "nobody" in ownership.
                        string factionTag = projectedBy.GetOwnerFactionTag();

                        if(!string.IsNullOrEmpty(factionTag))
                            GetLine().Append(factionTag).Append('.');

                        GetLine().AppendMaxLength(MyVisualScriptLogicProvider.GetPlayersName(projectedBy.OwnerId), PLAYER_NAME_MAX_LENGTH);
                    }
                }
            }
            #endregion Optional: ownership

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
                        AddLine().Append("Completed: ").TimeFormat(currentTime).Color(COLOR_UNIMPORTANT).MultiplierFormat(MyAPIGateway.Session.WelderSpeedMultiplier).ResetFormatting();

                        if(def.CriticalIntegrityRatio < 1 && integrityRatio < def.CriticalIntegrityRatio)
                        {
                            float funcTime = buildTime * def.CriticalIntegrityRatio * (1 - (integrityRatio / def.CriticalIntegrityRatio));

                            GetLine().Separator().Append("Functional: ").TimeFormat(funcTime);
                        }
                    }
                }
                else
                {
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
                        AddLine().Append("Dismantled: ").TimeFormat(grindTime).Color(COLOR_UNIMPORTANT).MultiplierFormat(MyAPIGateway.Session.GrinderSpeedMultiplier).ResetFormatting();

                        if(hackable)
                        {
                            GetLine().Separator().Append("Hacked: ").TimeFormat(hackTime).Color(COLOR_UNIMPORTANT).MultiplierFormat(MyAPIGateway.Session.HackSpeedMultiplier).ResetFormatting();
                        }
                    }
                }
            }
            #endregion Time to complete/grind

            #region Optional: item changes on grind
            if(!projected && Main.Config.AimInfo.IsSet(AimInfoFlags.GrindChangeWarning) && Main.EquipmentMonitor.IsAnyGrinder && !Main.TextAPI.IsEnabled)
            {
                foreach(MyCubeBlockDefinition.Component comp in def.Components)
                {
                    if(comp.DeconstructItem != null && comp.DeconstructItem != comp.Definition)
                    {
                        AddLine(FontsHandler.RedSh).Color(COLOR_WARNING).Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText);
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
            if(!projected && Main.Config.AimInfo.IsSet(AimInfoFlags.ShipGrinderImpulse) && Main.EquipmentMonitor.ToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder))
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
            if(!projected && Main.Config.AimInfo.IsSet(AimInfoFlags.GrindGridSplit) && Main.EquipmentMonitor.IsAnyGrinder)
            {
                if(willSplitGrid == GridSplitType.Recalculate)
                    willSplitGrid = grid.WillRemoveBlockSplitGrid(aimedBlock) ? GridSplitType.Split : GridSplitType.NoSplit;

                if(willSplitGrid == GridSplitType.Split)
                    AddLine(FontsHandler.RedSh).Color(COLOR_WARNING).Append("Grid will split if removed!");

                // TODO: find if split grid will vanish due to no physics/no standalone
            }
            #endregion Optional: grinder makes grid split

            #region Optional: added by mod
            MyModContext context = def.Context;
            if(Main.Config.AimInfo.IsSet(AimInfoFlags.AddedByMod) && !context.IsBaseGame)
            {
                if(Main.TextAPI.IsEnabled)
                {
                    AddLine().Color(COLOR_MOD).Append("Mod: ").Color(COLOR_MOD_TITLE).AppendMaxLength(context.ModName, MOD_NAME_MAX_LENGTH);

                    MyObjectBuilder_Checkpoint.ModItem modItem = context.GetModItem();
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
            }
            #endregion Block name line only for textAPI

            #region Internal info
            if(Main.Config.InternalInfo.Value)
            {
                int obPrefixLen = "MyObjectBuilder_".Length;
                string typeIdString = def.Id.TypeId.ToString();
                AddLine().Color(COLOR_INTERNAL).Label("Id").Color(COLOR_NORMAL).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(def.Id.SubtypeName);
                AddLine().Color(COLOR_INTERNAL).Label("BlockPairName").Color(COLOR_NORMAL).Append(def.BlockPairName);

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
                    int conveyors = (data.ConveyorPorts?.Count ?? 0);
                    int interactiveConveyors = (data.InteractableConveyorPorts?.Count ?? 0);

                    AddLine();

                    bool hasConveyorPorts = (data.Has & BlockHas.ConveyorSupport) != 0 && (conveyors > 0 || interactiveConveyors > 0);
                    if(hasConveyorPorts)
                    {
                        GetLine().Color(COLOR_CONVEYORPORTS).Label("Conveyor ports").Append(conveyors + interactiveConveyors).ResetFormatting().Separator(); // separator because next thing shows up regardless
                    }

                    //bool hasInventory = (data.Has & BlockHas.Inventory) != 0;

                    if((data.Has & BlockHas.Terminal) != 0)
                        GetLine().Color(COLOR_GOOD).Append("Terminal").ResetFormatting();
                    else
                        GetLine().Append("No terminal");

                    if((data.Has & BlockHas.TerminalAndInventoryAccess) != 0)
                        GetLine().Separator().Color(COLOR_GOOD).Append("Terminal/inventory access");
                    else
                        GetLine().Separator().Color(COLOR_WARNING).Append("No terminal/inventory access");

                    // HACK: weird conveyor support mention
                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings))
                    {
                        if(hasConveyorPorts && def.CubeSize == MyCubeSize.Small && def.Id.TypeId == typeof(MyObjectBuilder_SmallMissileLauncher))
                            AddLine(FontsHandler.YellowSh).Color(COLOR_WARNING).Append("UseConveyors is default off!");
                    }
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
                        AddLine().Label("Unknown ports").Append(upgradePorts).MoreInfoInHelp(3);
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

            EndAddedLines();
        }
        #endregion Equipped block info generation

        #region Shared generation methods
        private void AppendBasics(MyCubeBlockDefinition def, bool part = false)
        {
            int airTightFaces = 0;
            int totalFaces = 0;
            AirTightMode airTight = Utils.GetAirTightFaces(def, ref airTightFaces, ref totalFaces);
            bool deformable = (def.BlockTopology == MyBlockTopology.Cube && def.UsesDeformation);
            int assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
            bool buildModels = (def.BuildProgressModels != null && def.BuildProgressModels.Length > 0);
            float weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
            float grindRatio = def.DisassembleRatio;

            if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition)
                grindRatio *= Hardcoded.Door_Closed_DisassembleRatioMultiplier;

            string padding = (part ? (Main.TextAPI.IsEnabled ? "        | " : "       | ") : "");

            if(part)
                AddLine(FontsHandler.SkyBlueSh).Color(COLOR_PART).Label("Part").Append(def.DisplayNameText);

            #region Mass/size/build time/deconstruct time/no models
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Line1))
            {
                AddLine();

                if(part)
                    GetLine().Color(COLOR_PART).Append(padding);

                // HACK: game doesn't use mass from blocks with HasPhysics=false
                GetLine().Color(new Color(200, 255, 55)).MassFormat(def.HasPhysics ? def.Mass : 0).ResetFormatting().Separator()
                    .Size3DFormat(def.Size).Separator()
                    .TimeFormat(assembleTime / weldMul).Color(COLOR_UNIMPORTANT).MultiplierFormat(weldMul).ResetFormatting();

                if(Math.Abs(grindRatio - 1) >= 0.0001f)
                    GetLine().Separator().Color(grindRatio > 1 ? COLOR_BAD : (grindRatio < 1 ? COLOR_GOOD : COLOR_NORMAL)).Append("Deconstructs: ").ProportionToPercent(1f / grindRatio).ResetFormatting();

                if(!buildModels)
                    GetLine().Separator().Color(COLOR_WARNING).Append("(No construction models)").ResetFormatting();
            }
            #endregion Mass/size/build time/deconstruct time/no models

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Line2))
            {
                AddLine();

                if(part)
                    GetLine().Color(COLOR_PART).Append(padding).ResetFormatting();

                GetLine().Label("Integrity").Append(def.MaxIntegrity.ToString("#,###,###,###,###"));

                if(deformable)
                    GetLine().Separator().Label("Deform Ratio").RoundedNumber(def.DeformationRatio, 2);

                float dmgMul = def.GeneralDamageMultiplier;
                if(dmgMul != 1)
                {
                    GetLine().Separator();
                    ResistanceFormat(dmgMul);
                }
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

                AddLine().Append("Components: ").Color(totalVolumeM3 > charInvVolM3 ? COLOR_WARNING : COLOR_NORMAL).VolumeFormat(totalVolumeM3 * 1000).ResetFormatting();
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                if(!def.IsStandAlone || !def.HasPhysics)
                    AddLine();

                if(!def.HasPhysics)
                {
                    GetLine().Append("No collisions");
                }

                if(!def.IsStandAlone || !def.HasPhysics)
                {
                    if(!def.HasPhysics)
                        GetLine().Separator();

                    GetLine().Color(COLOR_WARNING).Append("No standalone").MoreInfoInHelp(2);
                }
            }

            #region Airtightness
            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Airtight))
            {
                AddLine(font: (airTight == AirTightMode.SEALED ? FontsHandler.GreenSh : (airTight == AirTightMode.NOT_SEALED ? FontsHandler.YellowSh : FontsHandler.SkyBlueSh)));

                if(part)
                    GetLine().Color(COLOR_PART).Append(padding);

                bool isDoor = (def is MyDoorDefinition || def is MyAdvancedDoorDefinition || def is MyAirtightDoorGenericDefinition);
                bool sealsOnClose = false;

                if(isDoor)
                {
                    if(airTight == AirTightMode.SEALED)
                    {
                        sealsOnClose = true;
                    }
                    else
                    {
                        for(int i = 0; i < 6; ++i)
                        {
                            Vector3I normal = (Vector3I)Main.Overlays.DIRECTIONS[i];

                            if(Pressurization.IsDoorAirtight(def, ref normal, fullyClosed: true))
                            {
                                sealsOnClose = true;
                                break;
                            }
                        }
                    }
                }

                if(isDoor && sealsOnClose)
                {
                    if(airTight == AirTightMode.SEALED)
                        GetLine().Color(COLOR_GOOD).Label("Air-tight").Append("Sealed, even if open");
                    else if(airTight == AirTightMode.NOT_SEALED)
                        GetLine().Color(COLOR_WARNING).Label("Air-tight").Append("Not sealed, unless closed (see overlay)");
                    else
                        GetLine().Color(COLOR_WARNING).Label("Air-tight").Append(airTightFaces).Append(" of ").Append(totalFaces).Append(" faces are sealed, unless closed (see overlay)");
                }
                else
                {
                    if(airTight == AirTightMode.SEALED)
                        GetLine().Color(COLOR_GOOD).Label("Air-tight").Append("Sealed");
                    else if(airTight == AirTightMode.NOT_SEALED)
                        GetLine().Color(isDoor ? COLOR_BAD : COLOR_WARNING).Label("Air-tight").Append("Not sealed").Append(isDoor ? ", even if closed" : "");
                    else
                        GetLine().Color(COLOR_WARNING).Label("Air-tight").Append(airTightFaces).Append(" of ").Append(totalFaces).Append(" faces are sealed");
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

            action = Format_SoundBlock;
            Add(typeof(MyObjectBuilder_SoundBlock), action);
            Add(typeof(MyObjectBuilder_Jukebox), action);

            Add(typeof(MyObjectBuilder_SensorBlock), Format_Sensor);

            Add(typeof(MyObjectBuilder_CameraBlock), Format_Camera);

            Add(typeof(MyObjectBuilder_ButtonPanel), Format_Button);

            Add(typeof(MyObjectBuilder_LCDPanelsBlock), Format_LCDPanels);

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

            Add(Constants.TargetDummyType, Format_TargetDummy);

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
                AddLine().Color(COLOR_NORMAL).LabelHardcoded("Power required", COLOR_NORMAL);

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
                    if(data.CanConnect)
                        AddLine().Append("Connectable: Yes");
                    else
                        AddLine().Color(COLOR_WARNING).Append("Connectable: No").ResetFormatting();

                    GetLine().Separator().LabelHardcoded("Can throw contents").Append("Yes");
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
            List<WcApiDef.WeaponDefinition> wcDefs;
            if(Main.WeaponCoreAPIHandler.IsRunning && Main.WeaponCoreAPIHandler.Weapons.TryGetValue(def.Id, out wcDefs))
            {
                Format_WeaponCore(def, wcDefs);
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

                AppendBasics(partDef, part: true);
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

                    AddLine().LabelHardcoded("Peak weld speed").ProportionToPercent(peakWeld).Color(COLOR_UNIMPORTANT).MultiplierFormat(mul).ResetFormatting().Append(" for one block")
                             .Separator().ProportionToPercent(leastWeld).Color(COLOR_UNIMPORTANT).MultiplierFormat(mul).ResetFormatting().Append(" for ").Append(weakestAt).Append("+ blocks");

                    AddLine().Label("Welding radius").DistanceFormat(shipTool.SensorRadius);
                }
                else
                {
                    float mul = MyAPIGateway.Session.GrinderSpeedMultiplier;

                    float peakGrind = Hardcoded.ShipGrinder_GrindPerSec(1);

                    int weakestAt = Hardcoded.ShipGrinder_DivideByTargets;
                    float leastGrind = Hardcoded.ShipGrinder_GrindPerSec(weakestAt);

                    AddLine().LabelHardcoded("Peak grind speed").ProportionToPercent(peakGrind).Color(COLOR_UNIMPORTANT).MultiplierFormat(mul).ResetFormatting().Append(" for one block")
                             .Separator().ProportionToPercent(leastGrind).Color(COLOR_UNIMPORTANT).MultiplierFormat(mul).ResetFormatting().Append(" for ").Append(weakestAt).Append("+ blocks");

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
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Append("Abilities: ");

                int preLen = GetLine().Length;

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
            }

            MyCockpitDefinition cockpit = def as MyCockpitDefinition;
            if(cockpit != null)
            {
                if(cockpit.HasInventory)
                    InventoryStats(def, hardcodedVolume: Hardcoded.Cockpit_InventoryVolume);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                {
                    AddLine((cockpit.IsPressurized ? FontsHandler.GreenSh : FontsHandler.RedSh))
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
                            AddLine(FontsHandler.GreenSh).Color(COLOR_GOOD).Append("Custom HUD: ").Append(cockpit.HUD).ResetFormatting().Separator().Color(COLOR_MOD).Append("Mod: ").ModFormat(defHUD.Context);
                        }
                        else
                        {
                            AddLine(FontsHandler.RedSh).Color(COLOR_BAD).Append("Custom HUD: ").Append(cockpit.HUD).Append("  (Error: not found)");
                        }
                    }
                }
            }

            AddScreenInfo(def, shipController.ScreenAreas);
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
                float consumptionMultiplier = 1f + (thrust.ConsumptionFactorPerG / Hardcoded.GAME_EARTH_GRAVITY);
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
                    // TODO: test if this NeedsAtmosphereForInfluence actually does anything with earth, mars, moon and space.

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

                        if(thrust.NeedsAtmosphereForInfluence)
                            AddLine().Append("No atmosphere causes 'thrust at min air'.");
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

                    AddLine().Label("Damage to ships").Number(data.DamagePerTickToBlocks * Constants.TICKS_PER_SECOND).Append("/s")
                        .Separator().Label("to other").Number(data.DamagePerTickToOther * Constants.TICKS_PER_SECOND).Append("/s");
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
            bool isSpotlight = (def is MyReflectorBlockDefinition);

            if(isSpotlight)
                radius = light.LightReflectorRadius;

            PowerRequired(light.RequiredPowerInput, light.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                AddLine().Append("Radius: ").DistanceFormat(radius.Min).Append(" to ").DistanceFormat(radius.Max).Separator().Append("Default: ").DistanceFormat(radius.Default);
                AddLine().Append("Intensity: ").RoundedNumber(light.LightIntensity.Min, 2).Append(" to ").RoundedNumber(light.LightIntensity.Max, 2).Separator().Append("Default: ").RoundedNumber(light.LightIntensity.Default, 2);
                AddLine().Append("Falloff: ").RoundedNumber(light.LightFalloff.Min, 2).Append(" to ").RoundedNumber(light.LightFalloff.Max, 2).Separator().Append("Default: ").RoundedNumber(light.LightFalloff.Default, 2);
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

            AddScreenInfo(def, projector.ScreenAreas);
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
                    AddLine().Label("Required item to deploy").Append(parachute.MaterialDeployCost).Append("x ").IdTypeSubtypeFormat(parachute.MaterialDefinitionId);
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
                    AddLine(FontsHandler.RedSh).LabelHardcoded("Refuel", COLOR_WARNING).Append("No").ResetFormatting();
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

            AddScreenInfo(def, medicalRoom.ScreenAreas);
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

                    AddLine().Append("Assembly speed: ").ProportionToPercent(assembler.AssemblySpeed * mulSpeed).Color(COLOR_UNIMPORTANT).MultiplierFormat(mulSpeed).ResetFormatting().Separator().Append("Efficiency: ").ProportionToPercent(mulEff).MultiplierFormat(mulEff);
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

                AddScreenInfo(def, survivalKit.ScreenAreas);
            }

            MyRefineryDefinition refinery = def as MyRefineryDefinition;
            if(refinery != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    float mul = MyAPIGateway.Session.RefinerySpeedMultiplier;

                    AddLine().Append("Refine speed: ").ProportionToPercent(refinery.RefineSpeed * mul).Color(COLOR_UNIMPORTANT).MultiplierFormat(mul).ResetFormatting().Separator().Append("Efficiency: ").ProportionToPercent(refinery.MaterialEfficiency);
                }
            }

            MyGasTankDefinition gasTank = def as MyGasTankDefinition;
            if(gasTank != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    AddLine().Append("Stores: ").Append(gasTank.StoredGasId.SubtypeName).Separator().Append("Capacity: ").VolumeFormat(gasTank.Capacity);
                }
            }

            MyOxygenGeneratorDefinition oxygenGenerator = def as MyOxygenGeneratorDefinition;
            if(oxygenGenerator != null)
            {
                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
                {
                    AddLine().Append("Ice consumption: ").MassFormat(oxygenGenerator.IceConsumptionPerSecond).Append("/s");

                    if(oxygenGenerator.ProducedGases.Count > 0)
                    {
                        AddLine().Append("Produces: ");

                        foreach(MyOxygenGeneratorDefinition.MyGasGeneratorResourceInfo gas in oxygenGenerator.ProducedGases)
                        {
                            GetLine().Append(gas.Id.SubtypeName).Append(" (").VolumeFormat(oxygenGenerator.IceConsumptionPerSecond * gas.IceToGasRatio).Append("/s), ");
                        }

                        GetLine().Length -= 2;
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

            if(production.BlueprintClasses != null && Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Production))
            {
                AddLine();

                int SpacePadding = 11;

                if(refinery != null)
                {
                    GetLine().Label("Refines");
                }
                else if(gasTank != null)
                {
                    GetLine().Label("Refills");
                }
                else if(assembler != null)
                {
                    GetLine().Label("Builds");
                }
                else if(oxygenGenerator != null)
                {
                    GetLine().Label("Generates");
                    SpacePadding = 17;
                }
                else
                {
                    GetLine().Label("Blueprints");
                    SpacePadding = 17;
                }

                if(production.BlueprintClasses.Count == 0)
                {
                    GetLine().Color(COLOR_WARNING).Append("N/A");
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
                                AddLine().Color(COLOR_LIST).Append(' ', SpacePadding).Append("| ").ResetFormatting();
                            else
                                GetLine().Separator();
                        }

                        // not using DisplayNameText because some are really badly named, like BasicIngots -> Ingots; also can contain newlines.
                        //string name = bp.DisplayNameText;
                        //int newLineIndex = name.IndexOf('\n');
                        //
                        //if(newLineIndex != -1) // name contains a new line, ignore everything after that
                        //{
                        //    for(int ci = 0; ci < newLineIndex; ++ci)
                        //    {
                        //        GetLine().Append(name[ci]);
                        //    }
                        //
                        //    GetLine().TrimEndWhitespace();
                        //}
                        //else
                        //{
                        //    GetLine().Append(name);
                        //}

                        string name = bp.Id.SubtypeName;
                        GetLine().Append(name);
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
                    AddLine().Label("Needs fuel").IdTypeSubtypeFormat(h2Engine.Fuel.FuelId);
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

                            GetLine().IdTypeSubtypeFormat(fuel.FuelId).Append(" (").RoundedNumber(fuel.ConsumptionPerSecond_Items, 5).Append("/s)");
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
                    float dischargeTime = (battery.MaxStoredPower * 60 * 60) / battery.MaxPowerOutput;
                    AddLine().Label("Discharge time").TimeFormat(dischargeTime);

#if VERSION_190 || VERSION_191 || VERSION_192 || VERSION_193 || VERSION_194 || VERSION_195 || VERSION_196 || VERSION_197 || VERSION_198 // HACK: backwards compatible
                    const float OldHardcodedRechargeMultiplier = 0.8f;
                    float chargeTime = (battery.MaxStoredPower * 60 * 60) / (battery.RequiredPowerInput * OldHardcodedRechargeMultiplier);
                    AddLine().Label("Recharge time").TimeFormat(chargeTime).Separator().LabelHardcoded("Loss").ProportionToPercent(1f - OldHardcodedRechargeMultiplier);
#else
                    float chargeTime = (battery.MaxStoredPower * 60 * 60) / (battery.RequiredPowerInput * battery.RechargeMultiplier);
                    AddLine().Label("Recharge  time").TimeFormat(chargeTime).Separator();

                    if(battery.RechargeMultiplier <= 1f)
                        GetLine().Color(battery.RechargeMultiplier < 1 ? COLOR_BAD : COLOR_GOOD).Label("Loss").ProportionToPercent(1f - battery.RechargeMultiplier);
                    else
                        GetLine().Color(COLOR_GOOD).Label("Multiplier").MultiplierToPercent(battery.RechargeMultiplier);
#endif
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
        }

        private void Format_LaserAntenna(MyCubeBlockDefinition def)
        {
            MyLaserAntennaDefinition laserAntenna = (MyLaserAntennaDefinition)def;

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                AddLine().Label("Power - Active").PowerFormat(Hardcoded.LaserAntenna_PowerUsage(laserAntenna, 1000)).Append(" per km").MoreInfoInHelp(1);
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

            AddScreenInfo(def, pb.ScreenAreas);

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
                HackyScreenArea surface;
                string script = null;
                bool supportsRotation = false;

                // HACK: LCD block has rotations under these conditions, otherwise it just uses the old data from the definition
                if(lcd.ScreenAreas != null && lcd.ScreenAreas.Count == 4)
                {
                    supportsRotation = true;
                    List<HackyScreenArea> surfaces = GetOrParseScreenAreaData(lcd.Id, lcd.ScreenAreas, onlyFirst: true);
                    surface = surfaces[0];

                    info = Hardcoded.TextSurface_GetInfo(surface.ScreenWidth, surface.ScreenHeight, surface.TextureResolution);
                    script = surface.Script;
                }
                else
                {
                    info = Hardcoded.TextSurface_GetInfo(lcd.ScreenWidth, lcd.ScreenHeight, lcd.TextureResolution);
                }

                AddLine().Label("Resolution").Color(COLOR_STAT_CHARACTERDMG).Number(info.SurfaceSize.X).Append("x").Number(info.SurfaceSize.Y).ResetFormatting()
                    .Separator().Label("Rotatable").BoolFormat(supportsRotation)
                    .Separator().Label("Render").DistanceFormat(lcd.MaxScreenRenderDistance)
                    .Separator().LabelHardcoded("Sync").DistanceFormat(Hardcoded.TextSurfaceMaxSyncDistance);

                // not that useful info
                //if(!string.IsNullOrEmpty(script))
                //    AddLine().Label("Default script").Color(COLOR_STAT_TRAVEL).Append(script).ResetFormatting();

                AddLine().Label("Font size limits").RoundedNumber(lcd.MinFontSize, 4).Append(" to ").RoundedNumber(lcd.MaxFontSize, 4);
            }
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

            MyJukeboxDefinition jukebox = def as MyJukeboxDefinition;
            if(jukebox != null)
            {
                AddScreenInfo(def, jukebox.ScreenAreas);
            }
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

                // TODO: visualize angle limits?
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

            AddScreenInfo(def, button.ScreenAreas);
        }

        private void Format_LCDPanels(MyCubeBlockDefinition def)
        {
            MyLCDPanelsBlockDefinition panel = (MyLCDPanelsBlockDefinition)def;

            PowerRequired(panel.RequiredPowerInput, panel.ResourceSinkGroup);

            AddScreenInfo(def, panel.ScreenAreas);
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

            PowerRequired(jumpDrive.RequiredPowerInput, jumpDrive.ResourceSinkGroup);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                AddLine().Label("Power for jump").PowerStorageFormat(jumpDrive.PowerNeededForJump);
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
            {
                float chargeTime = (jumpDrive.PowerNeededForJump * 60 * 60) / (jumpDrive.RequiredPowerInput * Hardcoded.JumpDriveRechargeMultiplier);
                AddLine().Label("Charge time").TimeFormat(chargeTime).Separator().LabelHardcoded("Loss").ProportionToPercent(1f - Hardcoded.JumpDriveRechargeMultiplier);
                AddLine().LabelHardcoded("Jump process").TimeFormat(Hardcoded.JumpDriveJumpDelay); // HACK: jumpDrive.JumpDelay is not used in game code
                AddLine().Label("Max distance").DistanceFormat((float)jumpDrive.MaxJumpDistance);
                AddLine().Label("Max mass").MassFormat((float)jumpDrive.MaxJumpMass);
            }
        }
        #endregion Magic blocks

        private void Format_Weapon(MyCubeBlockDefinition def)
        {
            List<WcApiDef.WeaponDefinition> wcDefs;
            if(Main.WeaponCoreAPIHandler.IsRunning && Main.WeaponCoreAPIHandler.Weapons.TryGetValue(def.Id, out wcDefs))
            {
                Format_WeaponCore(def, wcDefs);
                return;
            }

            MyWeaponBlockDefinition weaponDef = (MyWeaponBlockDefinition)def;
            MyWeaponDefinition wpDef;
            if(!MyDefinitionManager.Static.TryGetWeaponDefinition(weaponDef.WeaponDefinitionId, out wpDef))
            {
                AddLine(FontsHandler.RedSh).Color(Color.Red).Append("Block error: can't find weapon definition: ");
                if(weaponDef.WeaponDefinitionId.TypeId != typeof(MyObjectBuilder_WeaponDefinition))
                    GetLine().Append(weaponDef.WeaponDefinitionId.ToString());
                else
                    GetLine().Append(weaponDef.WeaponDefinitionId.SubtypeName);
                return;
            }

            MyLargeTurretBaseDefinition turret = def as MyLargeTurretBaseDefinition;

            WeaponConfig gunWWF = Main.WhipWeaponFrameworkAPI.Weapons.GetValueOrDefault(def.Id, null);
            TurretWeaponConfig turretWWF = gunWWF as TurretWeaponConfig;

            bool extraInfo = Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo);

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.PowerStats))
            {
                if(gunWWF != null)
                {
                    float powerUsage = turretWWF?.IdlePowerDrawMax ?? gunWWF.IdlePowerDrawBase;
                    AddLine().Label("Power - Idle").PowerFormat(powerUsage).Separator().Label("Recharge").PowerFormat(gunWWF.ReloadPowerDraw).Append(" (adaptable)");

                    // HACK: hardcoded like in https://gitlab.com/whiplash141/Revived-Railgun-Mod/-/blob/develop/Data/Scripts/WeaponFramework/WhipsWeaponFramework/WeaponBlockBase.cs#L566
                    if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
                        GetLine().Separator().ResourcePriority("Thrust", true);
                }
                else
                {
                    float requiredPowerInput = (turret != null ? Hardcoded.Turret_PowerReq : Hardcoded.ShipGun_PowerReq);
                    PowerRequired(requiredPowerInput, weaponDef.ResourceSinkGroup, powerHardcoded: true);
                }
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.InventoryStats))
            {
                MyInventoryComponentDefinition invComp = Utils.GetInventoryFromComponent(def);
                AddLine().Label("Inventory").InventoryFormat(weaponDef.InventoryMaxVolume, wpDef.AmmoMagazinesId, invComp);
            }

            if(turret != null)
            {
                AddLine().Color(turret.AiEnabled ? COLOR_GOOD : COLOR_WARNING).Label("Auto-target").BoolFormat(turret.AiEnabled).ResetFormatting().Append(turret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)").Separator().Color(COLOR_WARNING).Append("Max range: ").DistanceFormat(turret.MaxRangeMeters);

                if(extraInfo)
                {
                    AddLine().Append("Rotation - ");

                    int minPitch = turret.MinElevationDegrees; // this one is actually not capped in game for whatever reason
                    int maxPitch = Math.Min(turret.MaxElevationDegrees, 90); // turret can't rotate past 90deg up

                    int minYaw = turret.MinAzimuthDegrees;
                    int maxYaw = turret.MaxAzimuthDegrees;

                    if(minPitch == -90 && maxPitch >= 90)
                        GetLine().Color(COLOR_GOOD).Append("Pitch: ").AngleFormatDeg(minPitch).Append(" to ").AngleFormatDeg(maxPitch);
                    else
                        GetLine().Color(COLOR_WARNING).Append("Pitch: ").AngleFormatDeg(minPitch).Append(" to ").AngleFormatDeg(maxPitch);

                    GetLine().ResetFormatting().Append(" @ ").RotationSpeed(turret.ElevationSpeed * Hardcoded.Turret_RotationSpeedMul).Separator();

                    if(minYaw <= -180 && maxYaw >= 180)
                        GetLine().Color(COLOR_GOOD).Append("Yaw: ").AngleFormatDeg(360);
                    else
                        GetLine().Color(COLOR_WARNING).Append("Yaw: ").AngleFormatDeg(minYaw).Append(" to ").AngleFormatDeg(maxYaw);

                    GetLine().ResetFormatting().Append(" @ ").RotationSpeed(turret.RotationSpeed * Hardcoded.Turret_RotationSpeedMul);
                }
            }

            if(gunWWF == null && extraInfo)
            {
                // accuracy cone diameter = tan(angle) * baseRadius * 2
                float accuracyAt100m = (float)Math.Tan(wpDef.DeviateShotAngle) * 100 * 2;
                float reloadTime = wpDef.ReloadTime / 1000;

                AddLine().Label("Accuracy").DistanceFormat(accuracyAt100m).Append(" group at 100m").Separator().Append("Reload: ").TimeFormat(reloadTime);
            }

            bool showAmmoDetails = Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.AmmoDetails);

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

            if(showAmmoDetails && gunWWF == null)
            {
                ammoProjectiles.Clear();
                ammoMissiles.Clear();

                for(int i = 0; i < wpDef.AmmoMagazinesId.Length; i++)
                {
                    MyAmmoMagazineDefinition mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wpDef.AmmoMagazinesId[i]);
                    MyAmmoDefinition ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                    int ammoType = (int)ammo.AmmoType;

                    if(wpDef.WeaponAmmoDatas[ammoType] != null)
                    {
                        switch(ammoType)
                        {
                            case 0: ammoProjectiles.Add(MyTuple.Create(mag, (MyProjectileAmmoDefinition)ammo)); break;
                            case 1: ammoMissiles.Add(MyTuple.Create(mag, (MyMissileAmmoDefinition)ammo)); break;
                        }
                    }
                }

                bool isValidWeapon = false;

                if(ammoProjectiles.Count > 0 || ammoMissiles.Count > 0)
                {
                    for(int i = 0; i < ammoProjectiles.Count; ++i)
                    {
                        MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition> data = ammoProjectiles[i];
                        MyProjectileAmmoDefinition ammo = data.Item2;

                        if(ammo.ProjectileMassDamage != 0 || ammo.ProjectileHealthDamage != 0 || (ammo.HeadShot && ammo.ProjectileHeadShotDamage != 0))
                        {
                            isValidWeapon = true;
                            break;
                        }
                    }

                    for(int i = 0; i < ammoMissiles.Count; ++i)
                    {
                        MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition> data = ammoMissiles[i];
                        MyMissileAmmoDefinition ammo = data.Item2;

                        if(ammo.MissileExplosionDamage != 0)
                        {
                            isValidWeapon = true;
                            break;
                        }
                    }
                }

                if(!isValidWeapon)
                {
                    if(ammoProjectiles.Count == 0 && ammoMissiles.Count == 0)
                        AddLine().Color(COLOR_WARNING).Append("Has no ammo! Might have custom behavior.");
                    else
                        AddLine().Color(COLOR_WARNING).Append("Ammo deals no vanilla damage! Might have custom behavior.");

                    ammoProjectiles.Clear();
                    ammoMissiles.Clear();
                }

                const int MaxMagNameLength = 20;
                bool blockTypeCanReload = Hardcoded.NoReloadTypes.Contains(def.Id.TypeId);

                if(ammoProjectiles.Count > 0)
                {
                    // HACK: wepDef.DamageMultiplier is only used for hand weapons in 1.193 - check if it's used for ship weapons in future game versions

                    MyWeaponDefinition.MyWeaponAmmoData projectilesData = wpDef.WeaponAmmoDatas[0];

                    bool hasReload = (blockTypeCanReload && projectilesData.ShotsInBurst > 0);
                    double rps = Math.Round(projectilesData.RateOfFire / 60f, 2);

                    AddLine().Label("Projectiles - Fire rate").Append(rps).Append(rps == 1 ? " round/s" : " rounds/s")
                        .Separator().Color(hasReload ? COLOR_WARNING : COLOR_GOOD).Append("Magazine: ");

                    if(hasReload)
                        GetLine().Append(projectilesData.ShotsInBurst);
                    else
                        GetLine().Append("No reloading");

                    AddLine().Append("Projectiles - ").Color(COLOR_STAT_TYPE).Append("Type").ResetFormatting().Append(" (")
                        .Color(COLOR_STAT_SHIPDMG).Append("ship").ResetFormatting().Append(", ")
                        .Color(COLOR_STAT_CHARACTERDMG).Append("character").ResetFormatting().Append(", ")
                        .Color(COLOR_STAT_HEADSHOTDMG).Append("headshot").ResetFormatting().Append(", ")
                        .Color(COLOR_STAT_SPEED).Append("speed").ResetFormatting().Append(", ")
                        .Color(COLOR_STAT_TRAVEL).Append("travel").ResetFormatting().Append(")");

                    for(int i = 0; i < ammoProjectiles.Count; ++i)
                    {
                        MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition> data = ammoProjectiles[i];
                        MyAmmoMagazineDefinition mag = data.Item1;
                        MyProjectileAmmoDefinition ammo = data.Item2;

                        AddLine().Append("  | ").Color(COLOR_STAT_TYPE).AppendMaxLength(mag.DisplayNameText, MaxMagNameLength).ResetFormatting().Append(" (");

                        if(ammo.ProjectileCount > 1)
                            GetLine().Color(COLOR_STAT_PROJECTILECOUNT).Append(ammo.ProjectileCount).Append("x ");

                        GetLine().Color(COLOR_STAT_SHIPDMG).Append(ammo.ProjectileMassDamage).ResetFormatting().Append(", ")
                            .Color(COLOR_STAT_CHARACTERDMG).Append(ammo.ProjectileHealthDamage).ResetFormatting().Append(", ")
                            .Color(COLOR_STAT_HEADSHOTDMG).Append(ammo.HeadShot ? ammo.ProjectileHeadShotDamage : ammo.ProjectileHealthDamage).ResetFormatting().Append(", ");

                        // from MyProjectile.Start()
                        if(ammo.SpeedVar > 0)
                            GetLine().Color(COLOR_STAT_SPEED).Number(ammo.DesiredSpeed * (1f - ammo.SpeedVar)).Append("~").Number(ammo.DesiredSpeed * (1f + ammo.SpeedVar)).Append(" m/s");
                        else
                            GetLine().Color(COLOR_STAT_SPEED).SpeedFormat(ammo.DesiredSpeed);

                        float range = wpDef.RangeMultiplier * ammo.MaxTrajectory;

                        GetLine().ResetFormatting().Append(", ").Color(COLOR_STAT_TRAVEL);

                        if(wpDef.UseRandomizedRange)
                            GetLine().DistanceRangeFormat(range * Hardcoded.Projectile_RangeMultiplier_Min, range * Hardcoded.Projectile_RangeMultiplier_Max);
                        else
                            GetLine().DistanceFormat(range);

                        GetLine().ResetFormatting().Append(")");
                    }
                }

                if(ammoMissiles.Count > 0)
                {
                    MyWeaponDefinition.MyWeaponAmmoData missileData = wpDef.WeaponAmmoDatas[1];

                    bool hasReload = (blockTypeCanReload && missileData.ShotsInBurst > 0);
                    double rps = Math.Round(missileData.RateOfFire / 60f, 2);

                    AddLine().Label("Missiles - Fire rate").Append(rps).Append(rps == 1 ? " round/s" : " rounds/s")
                        .Separator().Color(hasReload ? COLOR_WARNING : COLOR_GOOD).Append("Magazine: ");

                    if(hasReload)
                        GetLine().Append(missileData.ShotsInBurst);
                    else
                        GetLine().Append("No reloading");

                    AddLine().Append("Missiles - ").Color(COLOR_STAT_TYPE).Append("Type").ResetFormatting().Append(" (")
                        .Color(COLOR_STAT_SHIPDMG).Append("damage").ResetFormatting().Append(", ")
                        .Color(COLOR_STAT_CHARACTERDMG).Append("radius").ResetFormatting().Append(", ")
                        .Color(COLOR_STAT_SPEED).Append("speed").ResetFormatting().Append(", ")
                        .Color(COLOR_STAT_TRAVEL).Append("travel").ResetFormatting().Append(")");

                    for(int i = 0; i < ammoMissiles.Count; ++i)
                    {
                        MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition> data = ammoMissiles[i];
                        MyAmmoMagazineDefinition mag = data.Item1;
                        MyMissileAmmoDefinition ammo = data.Item2;

                        AddLine().Append("  | ").Color(COLOR_STAT_TYPE).AppendMaxLength(mag.DisplayNameText, MaxMagNameLength).ResetFormatting().Append(" (")
                            .Color(COLOR_STAT_SHIPDMG).Append(ammo.MissileExplosionDamage).ResetFormatting().Append(", ")
                            .Color(COLOR_STAT_CHARACTERDMG).DistanceFormat(ammo.MissileExplosionRadius).ResetFormatting().Append(", ");

                        // HACK: ammo.SpeedVar is not used for missiles
                        // HACK: wepDef.RangeMultiplier and wepDef.UseRandomizedRange are not used for missiles

                        GetLine().Color(COLOR_STAT_SPEED);

                        if(!ammo.MissileSkipAcceleration)
                            GetLine().SpeedFormat(ammo.MissileInitialSpeed).Append(" + ").AccelerationFormat(ammo.MissileAcceleration);
                        else
                            GetLine().SpeedFormat(ammo.DesiredSpeed);

                        GetLine().ResetFormatting().Append(", ").Color(COLOR_STAT_TRAVEL).DistanceFormat(ammo.MaxTrajectory)
                            .ResetFormatting().Append(")");
                    }
                }

                ammoProjectiles.Clear();
                ammoMissiles.Clear();
            }

            if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.Warnings) && !MyAPIGateway.Session.SessionSettings.WeaponsEnabled)
            {
                AddLine().Color(COLOR_BAD).Append("Weapons are disabled in this world");
            }
        }

        private void Format_WeaponCore(MyCubeBlockDefinition blockDef, List<WcApiDef.WeaponDefinition> wcDefs)
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

            AddLine().Color(COLOR_UNIMPORTANT).Append("(WeaponCore block, no stats to show)");


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
                AddLine().Label("Radius").DistanceFormat(warhead.ExplosionRadius);
                AddLine().Label("Damage").Append(warhead.WarheadExplosionDamage.ToString("#,###,###,###,##0.##"));
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
                    AddLine().Color(COLOR_WARNING).Label("Regeneration requires item").Append(dummyDef.ConstructionItemAmount).Append("x ").IdTypeSubtypeFormat(dummyDef.ConstructionItem);
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

            AddScreenInfo(def, safeZone.ScreenAreas);
        }

        private void Format_ContractBlock(MyCubeBlockDefinition def)
        {
            MyContractBlockDefinition contracts = (MyContractBlockDefinition)def;

            AddScreenInfo(def, contracts.ScreenAreas);
        }

        private void Format_StoreBlock(MyCubeBlockDefinition def)
        {
            MyStoreBlockDefinition store = (MyStoreBlockDefinition)def;
            MyVendingMachineDefinition vending = def as MyVendingMachineDefinition;

            InventoryStats(def);

            AddScreenInfo(def, store.ScreenAreas);

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

                                GetLine().IdTypeSubtypeFormat(entry.Item.Value).Separator().Label("Price").CurrencyFormat(entry.PricePerUnit);

                                if(!exists)
                                    GetLine().Append(" (Item not found!)").ResetFormatting();
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
                    GetLine().Append(MyTexts.GetString(dlc.DisplayName));
                }
                else
                {
                    GetLine().Append("(Unknown: ").Color(COLOR_BAD).Append(dlcId).ResetFormatting().Append(")");
                }
            }
        }

        private void ResistanceFormat(float damageMultiplier, string label = "Resistance")
        {
            int dmgResPercent = Utils.DamageMultiplierToResistance(damageMultiplier);

            GetLine()
                .Color(dmgResPercent == 0 ? COLOR_NORMAL : (dmgResPercent > 0 ? COLOR_GOOD : COLOR_WARNING)).Label(label).Append(dmgResPercent > 0 ? "+" : "").Append(dmgResPercent).Append("%")
                .Color(COLOR_UNIMPORTANT).Append(" (x").RoundedNumber(damageMultiplier, 2).Append(")").ResetFormatting();
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
                    AddLine().Color(color).LabelHardcoded("Power required", color);
                else
                    AddLine().Color(color).Label("Power required");

                if(mw <= 0)
                    GetLine().Append("No");
                else
                    GetLine().PowerFormat(mw);

                if(Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ResourcePriorities))
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
                    MyPhysicalItemDefinition itemDef = MyDefinitionManager.Static.GetPhysicalItemDefinition(id);
                    if(itemDef == null)
                        continue;

                    AddLine().Append("       - ").Append(itemDef.DisplayNameText).Append(" (").IdTypeSubtypeFormat(id).Append(")");

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
                    AddLine().Append("       - All of type: ");

                    string friendlyName = TypeFriendlyNames.GetValueOrDefault(type, null);
                    if(friendlyName != null)
                    {
                        GetLine().Append(friendlyName).Append(" (").IdTypeFormat(type).Append(")");
                    }
                    else
                    {
                        GetLine().IdTypeFormat(type);
                    }
                }
            }
        }

        readonly Dictionary<MyObjectBuilderType, string> TypeFriendlyNames = new Dictionary<MyObjectBuilderType, string>()
        {
            [typeof(MyObjectBuilder_PhysicalGunObject)] = "Hand-Tool/Gun",
            [typeof(MyObjectBuilder_AmmoMagazine)] = "Ammo Magazine",
            [typeof(MyObjectBuilder_GasContainerObject)] = "Gas Bottle",
            [typeof(MyObjectBuilder_OxygenContainerObject)] = "Oxygen Bottle",
        };

        [ProtoContract]
        public class HackyScreenArea
        {
            [ProtoMember(1)]
            [XmlAttribute]
            public string Name;

            [ProtoMember(4)]
            [XmlAttribute]
            public string DisplayName;

            [ProtoMember(7)]
            [XmlAttribute]
            [DefaultValue(512)]
            public int TextureResolution = 512;

            [ProtoMember(10)]
            [XmlAttribute]
            [DefaultValue(1)]
            public int ScreenWidth = 1;

            [ProtoMember(13)]
            [XmlAttribute]
            [DefaultValue(1)]
            public int ScreenHeight = 1;

            [ProtoMember(16)]
            [XmlAttribute]
            [DefaultValue(null)]
            public string Script;
        }

        readonly Dictionary<MyDefinitionId, List<HackyScreenArea>> ScreenAreaData = new Dictionary<MyDefinitionId, List<HackyScreenArea>>(MyDefinitionId.Comparer);

        /// <summary>
        /// HACK: getting data from prohibited serializable class
        /// </summary>
        private List<HackyScreenArea> GetOrParseScreenAreaData<T>(MyDefinitionId defId, List<T> screenAreas, bool onlyFirst = false)
        {
            List<HackyScreenArea> cachedSurfaces;
            if(!ScreenAreaData.TryGetValue(defId, out cachedSurfaces))
            {
                ScreenAreaData[defId] = cachedSurfaces = new List<HackyScreenArea>();

                for(int i = 0; i < screenAreas.Count; i++)
                {
                    byte[] binary = MyAPIGateway.Utilities.SerializeToBinary(screenAreas[i]);
                    HackyScreenArea surface = MyAPIGateway.Utilities.SerializeFromBinary<HackyScreenArea>(binary);
                    cachedSurfaces.Add(surface);

                    if(onlyFirst)
                        break;
                }
            }

            return cachedSurfaces;
        }

        private void AddScreenInfo<T>(MyCubeBlockDefinition def, List<T> screens)
        {
            if(screens == null || screens.Count == 0 || !Main.Config.PlaceInfo.IsSet(PlaceInfoFlags.ExtraInfo))
                return;

            List<HackyScreenArea> surfaces = GetOrParseScreenAreaData(def.Id, screens);

            const int SpacePrefix = 9;
            AddLine().Label(surfaces.Count > 1 ? "LCDs" : "LCD");

            // TODO: toggle between list and just count?
            for(int i = 0; i < surfaces.Count; i++)
            {
                HackyScreenArea surface = surfaces[i];
                string displayName = MyTexts.GetString(MyStringId.GetOrCompute(surface.DisplayName));
                Hardcoded.TextSurfaceInfo info = Hardcoded.TextSurface_GetInfo(surface.ScreenWidth, surface.ScreenHeight, surface.TextureResolution);

                if(i > 0)
                    AddLine().Append(' ', SpacePrefix).Append("| ");

                GetLine().Append(displayName);

                // very edge case use for a lot of width added, who needs it can get it from API or SBC
                //if(Main.Config.InternalInfo.Value)
                //    GetLine().Color(COLOR_UNIMPORTANT).Append(" (").Append(surface.Name).Append(")");

                GetLine().ResetFormatting().Separator().Color(COLOR_STAT_CHARACTERDMG).Number(info.SurfaceSize.X).Append("x").Number(info.SurfaceSize.Y).ResetFormatting();

                // not that useful info
                //if(!string.IsNullOrEmpty(surface.Script))
                //    GetLine().Separator().Color(COLOR_STAT_TRAVEL).Label("Default script").Append(surface.Script).ResetFormatting();
            }

            AddLine().LabelHardcoded("LCD - Render").DistanceFormat(Hardcoded.TextSurfaceMaxRenderDistance).Separator().LabelHardcoded("Sync").DistanceFormat(Hardcoded.TextSurfaceMaxSyncDistance);
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

                if(Main.Config.TextAlwaysVisible.Value)
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

            public CacheTextAPI(StringBuilder textSB, Vector2D textSize)
            {
                ResetExpiry();
                Text = new StringBuilder(textSB.Length);
                Text.AppendStringBuilder(textSB);
                TextSize = textSize;
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
