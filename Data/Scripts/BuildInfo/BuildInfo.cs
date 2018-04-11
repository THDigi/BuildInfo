using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Blocks;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.ModAPI;
using VRageMath;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum; // HACK allows the use of BlendTypeEnum which is whitelisted but bypasses accessing MyBillboard which is not whitelisted

namespace Digi.BuildInfo
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public partial class BuildInfo : MySessionComponentBase
    {
        #region Init and unload
        public override void LoadData()
        {
            Instance = this;
            Log.SetUp(MOD_NAME, MOD_WORKSHOP_ID, MOD_SHORTNAME);
        }

        public bool Init() // called in first call of UpdateAfterSimulation()
        {
            Log.Init();
            init = true;
            isThisDS = (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated);

            if(isThisDS) // not doing anything DS side so get rid of this component entirely
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(DisposeComponent);
                return false;
            }

            int count = Enum.GetValues(typeof(TextAPIMsgIds)).Length;
            textAPILabels = new HudAPIv2.SpaceMessage[count];
            textAPIShadows = new HudAPIv2.SpaceMessage[count];

            ComputeCharacterSizes();
            ComputeResourceGroups();
            UpdateConfigValues();

            settings = new Settings();
            leakInfoComp = new LeakInfoComponent();
            textAPI = new HudAPIv2();

            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
            MyAPIGateway.Gui.GuiControlRemoved += GuiControlRemoved;
            return true;
        }

        private void DisposeComponent()
        {
            Log.Close();
            Instance = null;
            SetUpdateOrder(MyUpdateOrder.NoUpdate); // this throws exceptions if called in an update method, which is why the InvokeOnGameThread() is needed.
            MyAPIGateway.Session.UnregisterComponent(this);
        }

        protected override void UnloadData()
        {
            if(Instance == null)
                return;

            Instance = null;

            try
            {
                if(init)
                {
                    init = false;

                    MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
                    MyAPIGateway.Gui.GuiControlRemoved -= GuiControlRemoved;

                    if(settings != null)
                    {
                        settings.Close();
                        settings = null;
                    }

                    if(leakInfoComp != null)
                    {
                        leakInfoComp.Close();
                        leakInfoComp = null;
                    }

                    if(textAPI != null)
                    {
                        textAPI.Close();
                        textAPI = null;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            Log.Close();
        }
        #endregion

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(isThisDS) // failsafe in case the component is still updating or not removed...
                    return;

                if(!init)
                {
                    if(MyAPIGateway.Session == null)
                        return;

                    if(!Init())
                        return;
                }

                if(!textAPIresponded && textAPI.Heartbeat)
                {
                    textAPIresponded = true;
                    HideText(); // force a re-check to make the HUD -> textAPI transition
                }

                // HUD toggle monitor; required here because it gets the previous value if used in HandleInput()
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
                    hudVisible = !MyAPIGateway.Session.Config.MinimalHud;

                unchecked // global ticker
                {
                    ++tick;
                }

                if(selectedBlock != null && tick % 10 == 0)
                {
                    aimInfoNeedsUpdate = true;
                }

                if(pickBlockDef != null && tick % 5 == 0)
                {
                    MyAPIGateway.Utilities.ShowNotification($"Press a number key to place '{pickBlockDef.DisplayNameText}' in...", 16 * 5, MyFontEnum.Blue);
                }

                if(leakInfoComp != null)
                    leakInfoComp.Update();

                #region Cubebuilder, welder and grinder monitor
                var prevSelectedToolDefId = selectedToolDefId;

                selectedDef = null;
                selectedBlock = null;
                selectedHandTool = null;
                selectedToolDefId = default(MyDefinitionId);
                isToolSelected = false;
                canShowMenu = false;

                UpdateHandTools();
                UpdateShipTools();

                if(selectedDef == null)
                {
                    var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

                    if(def != null && MyCubeBuilder.Static.IsActivated)
                    {
                        canShowMenu = true;
                        var hit = MyCubeBuilder.Static.HitInfo as IHitInfo;
                        var grid = hit?.HitEntity as IMyCubeGrid;

                        if(grid != null && grid.GridSizeEnum != def.CubeSize) // if aimed grid is supported by definition size
                        {
                            if(unsupportedGridSizeNotification == null)
                                unsupportedGridSizeNotification = MyAPIGateway.Utilities.CreateNotification("", 100, MyFontEnum.Red);

                            unsupportedGridSizeNotification.Text = $"{def.DisplayNameText} can't be placed on {grid.GridSizeEnum} grid size.";
                            unsupportedGridSizeNotification.Show();
                        }
                        else
                        {
                            selectedDef = def;
                        }
                    }
                }

                if(selectedToolDefId != prevSelectedToolDefId)
                    lastDefId = default(MyDefinitionId);

                if(selectedDef != null || showMenu)
                {
                    if(showMenu)
                    {
                        if(menuNeedsUpdate)
                        {
                            lastDefId = DEFID_MENU;
                            menuNeedsUpdate = false;
                            textShown = false;

                            GenerateMenuText();
                            PostProcessText(DEFID_MENU, false);
                        }
                    }
                    else
                    {
                        if(settings.showTextInfo && ((aimInfoNeedsUpdate && selectedBlock != null) || selectedDef.Id != lastDefId))
                        {
                            lastDefId = selectedDef.Id;
                            blockDataCache = null;

                            if(selectedBlock != null) // text for welder/grinder
                            {
                                aimInfoNeedsUpdate = false;
                                GenerateAimBlockText(selectedDef);
                                PostProcessText(selectedDef.Id, false);
                            }
                            else // text for holding the block
                            {
                                if(TextAPIEnabled ? cachedBuildInfoTextAPI.TryGetValue(selectedDef.Id, out cache) : cachedBuildInfoNotification.TryGetValue(selectedDef.Id, out cache))
                                {
                                    textShown = false; // make the textAPI update
                                }
                                else
                                {
                                    GenerateBlockText(selectedDef);
                                    PostProcessText(selectedDef.Id, true);
                                }
                            }
                        }
                    }

                    UpdateVisualText();

                    // turn off frozen block preview if camera is too far away from it
                    if(MyAPIGateway.CubeBuilder.FreezeGizmo && Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.WorldMatrix.Translation, lastGizmoPosition) > FREEZE_MAX_DISTANCE_SQ)
                        SetFreezeGizmo(false);
                }
                else // no block equipped
                {
                    blockDataCache = null;
                    showMenu = false;

                    if(MyAPIGateway.CubeBuilder.FreezeGizmo)
                        SetFreezeGizmo(false);

                    HideText();
                }
                #endregion

                #region Purge cache
                if(tick % 60 == 0)
                {
                    var haveNotifCache = cachedBuildInfoNotification.Count > 0;
                    var haveTextAPICache = cachedBuildInfoTextAPI.Count > 0;

                    if(haveNotifCache || haveTextAPICache)
                    {
                        removeCacheIds.Clear();
                        var time = DateTime.UtcNow.Ticks;

                        if(haveNotifCache)
                        {
                            foreach(var kv in cachedBuildInfoNotification)
                                if(kv.Value.expires < time)
                                    removeCacheIds.Add(kv.Key);

                            if(cachedBuildInfoNotification.Count == removeCacheIds.Count)
                                cachedBuildInfoNotification.Clear();
                            else
                                foreach(var key in removeCacheIds)
                                    cachedBuildInfoNotification.Remove(key);

                            removeCacheIds.Clear();
                        }

                        if(haveTextAPICache)
                        {
                            foreach(var kv in cachedBuildInfoTextAPI)
                                if(kv.Value.expires < time)
                                    removeCacheIds.Add(kv.Key);

                            if(cachedBuildInfoTextAPI.Count == removeCacheIds.Count)
                                cachedBuildInfoTextAPI.Clear();
                            else
                                foreach(var key in removeCacheIds)
                                    cachedBuildInfoTextAPI.Remove(key);

                            removeCacheIds.Clear();
                        }
                    }
                }
                #endregion
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Called before all updates and is called even when game is paused.
        /// </summary>
        public override void HandleInput()
        {
            try
            {
                if(!init || MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible || MyParticlesManager.Paused)
                    return; // only monitor input when not in menu or chat, and not paused

                #region Block picker
                // TODO: block picker could use a hotkey...
                //if(pickBlockDef == null && selectedBlock != null && MyAPIGateway.Input.IsAnyCtrlKeyPressed() && MyAPIGateway.Input.IsGameControlPressed(MyControlsSpace.BUILD_SCREEN))
                //{
                //    pickBlockDef = selectedDef;
                //}

                if(pickBlockDef != null && !MyAPIGateway.Input.IsAnyCtrlKeyPressed()) // ignore ctrl to allow toolbar page changing
                {
                    int slot = 0;

                    for(MyKeys k = MyKeys.D1; k <= MyKeys.D9; k++)
                    {
                        if(MyAPIGateway.Input.IsKeyPress(k))
                        {
                            slot = (k - MyKeys.D0);
                            break;
                        }
                    }

                    if(slot == 0)
                    {
                        for(MyKeys k = MyKeys.NumPad1; k <= MyKeys.NumPad9; k++)
                        {
                            if(MyAPIGateway.Input.IsKeyPress(k))
                            {
                                slot = (k - MyKeys.NumPad0);
                                break;
                            }
                        }
                    }

                    if(slot != 0)
                    {
                        MyVisualScriptLogicProvider.SetToolbarSlotToItem(slot - 1, pickBlockDef.Id, -1);

                        MyAPIGateway.Utilities.ShowNotification($"{pickBlockDef.DisplayNameText} placed in slot {slot}.", 2000, MyFontEnum.Green);

                        pickBlockDef = null;
                    }
                }
                #endregion

                #region Hotkeys and menu
                if(canShowMenu)
                {
                    var input = MyAPIGateway.Input;

                    if(showMenu)
                    {
                        if(MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Escape))
                        {
                            showMenu = false;
                            menuNeedsUpdate = true;
                            return;
                        }

                        bool canUseTextAPI = (textAPI != null && textAPI.Heartbeat);

                        if(input.IsNewGameControlPressed(MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE))
                        {
                            menuNeedsUpdate = true;

                            if(++menuSelectedItem >= MENU_TOTAL_ITEMS)
                                menuSelectedItem = 0;
                        }

                        if(input.IsNewGameControlPressed(MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE))
                        {
                            menuNeedsUpdate = true;

                            if(--menuSelectedItem < 0)
                                menuSelectedItem = (MENU_TOTAL_ITEMS - 1);
                        }

                        if(input.IsNewGameControlPressed(MyControlsSpace.CUBE_ROTATE_VERTICAL_NEGATIVE) || input.IsNewGameControlPressed(MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE))
                        {
                            menuNeedsUpdate = true;

                            switch(menuSelectedItem)
                            {
                                case 0:
                                    showMenu = false;
                                    break;
                                case 1:
                                    if(selectedBlock != null)
                                    {
                                        showMenu = false;
                                        pickBlockDef = selectedDef;
                                    }
                                    else
                                    {
                                        MyAPIGateway.Utilities.ShowNotification("This only works with a hand or ship tool.", 3000, MyFontEnum.Red);
                                    }
                                    break;
                                case 2:
                                    showMenu = false;
                                    ShowHelp();
                                    break;
                                case 3:
                                    settings.showTextInfo = !settings.showTextInfo;
                                    settings.Save();

                                    if(buildInfoNotification == null)
                                        buildInfoNotification = MyAPIGateway.Utilities.CreateNotification("");
                                    buildInfoNotification.Text = (settings.showTextInfo ? "Text info ON (saved to config)" : "Text info OFF (saved to config)");
                                    buildInfoNotification.Show();
                                    break;
                                case 4:
                                    CycleOverlay();
                                    break;
                                case 5:
                                    MyCubeBuilder.Static.UseTransparency = !MyCubeBuilder.Static.UseTransparency;
                                    break;
                                case 6:
                                    SetFreezeGizmo(!MyAPIGateway.CubeBuilder.FreezeGizmo);
                                    break;
                                case 7:
                                    if(canUseTextAPI)
                                    {
                                        useTextAPI = !useTextAPI;
                                        cache = null;
                                        HideText();
                                    }
                                    else
                                    {
                                        showMenu = false;
                                        MyAPIGateway.Utilities.ShowNotification("TextAPI mod not detected! (workshop id: 758597413)", 3000, MyFontEnum.Red);
                                    }
                                    break;
                                case 8:
                                    showMenu = false;
                                    ReloadConfig(MOD_NAME);
                                    break;
                            }
                        }
                    }

                    if(input.IsNewGameControlPressed(MyControlsSpace.VOXEL_HAND_SETTINGS))
                    {
                        if(input.IsAnyShiftKeyPressed())
                        {
                            MyCubeBuilder.Static.UseTransparency = !MyCubeBuilder.Static.UseTransparency;
                            menuNeedsUpdate = true;

                            if(transparencyNotification == null)
                                transparencyNotification = MyAPIGateway.Utilities.CreateNotification("");

                            transparencyNotification.Text = (MyCubeBuilder.Static.UseTransparency ? "Placement transparency ON" : "Placement transparency OFF");
                            transparencyNotification.Show();
                        }
                        else if(input.IsAnyCtrlKeyPressed())
                        {
                            CycleOverlay();
                            menuNeedsUpdate = true;

                            if(overlayNotification == null)
                                overlayNotification = MyAPIGateway.Utilities.CreateNotification("");

                            overlayNotification.Text = "Overlays: " + DRAW_OVERLAY_NAME[drawOverlay];
                            overlayNotification.AliveTime = 2000;
                            overlayNotification.Show();
                        }
                        else if(input.IsAnyAltKeyPressed())
                        {
                            SetFreezeGizmo(!MyAPIGateway.CubeBuilder.FreezeGizmo);
                        }
                        else
                        {
                            showMenu = !showMenu;
                            menuNeedsUpdate = true;
                        }
                    }
                }
                else if(showMenu)
                {
                    showMenu = false;
                }
                #endregion
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Called after all updates have finished and is called even when game is paused.
        /// </summary>
        public override void Draw()
        {
            if(!init || isThisDS)
                return;

            try
            {
                DrawOverlays();

                leakInfoComp?.Draw();

                if(!hudVisible && !settings.alwaysVisible)
                    return;

                if(MyAPIGateway.Gui.IsCursorVisible && textShown && textObject != null)
                {
                    HideText();
                }

                #region Block info UI additions
                if(settings.showTextInfo && selectedDef != null && !MyAPIGateway.Gui.IsCursorVisible)
                {
                    // TODO: optimize?

                    var cam = MyAPIGateway.Session.Camera;
                    var camMatrix = cam.WorldMatrix;
                    var scaleFOV = (float)Math.Tan(cam.FovWithZoom / 2);
                    UpdateCameraViewProjInvMatrix();
                    var posHUD = GetGameHUDBlockInfoPos();

                    // draw the added top part's background only for aimed block (which requires textAPI)
                    if(selectedBlock != null && !showMenu && textObject != null && useTextAPI)
                    {
                        var hud = posHUD;

                        // make the position top-right
                        hud.Y -= (BLOCKINFO_ITEM_HEIGHT * selectedDef.Components.Length) + BLOCKINFO_Y_OFFSET;

                        var worldPos = GameHUDToWorld(hud);
                        var size = BLOCKINFO_SIZE;
                        size.Y *= lines * settings.textAPIScale;
                        size.Y += BLOCKINFO_TEXT_PADDING;
                        size *= scaleFOV;

                        if(Math.Abs(aspectRatio - (1280.0 / 1024.0)) <= 0.0001) // HACK 5:4 aspect ratio manual fix
                        {
                            size.X *= ASPECT_RATIO_54_FIX;
                        }

                        worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                        double cornerSize = Math.Min(0.0015 * scaleFOV, size.Y);
                        float cornerW = (float)cornerSize;
                        float cornerH = (float)cornerSize;

                        {
                            var finalW = size.X - cornerW;
                            var finalH = cornerH;
                            var finalWorldPos = worldPos + camMatrix.Left * cornerW + camMatrix.Up * (size.Y - cornerH);
                            MyTransparentGeometry.AddBillboardOriented(MATERIAL_VANILLA_SQUARE, BLOCKINFO_BG_COLOR, finalWorldPos, camMatrix.Left, camMatrix.Up, finalW, finalH, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                        }

                        // HACK NOTE: this custom topright corner material will draw above textAPI if textAPI is loaded after this mod
                        {
                            var finalW = cornerW;
                            var finalH = cornerH;
                            var finalWorldPos = worldPos + camMatrix.Right * (size.X - cornerW) + camMatrix.Up * (size.Y - cornerH);
                            MyTransparentGeometry.AddBillboardOriented(MATERIAL_TOPRIGHTCORNER, BLOCKINFO_BG_COLOR, finalWorldPos, camMatrix.Left, camMatrix.Up, finalW, finalH, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                        }

                        {
                            var finalW = size.X;
                            var finalH = size.Y - cornerH;
                            var finalWorldPos = worldPos + camMatrix.Down * cornerH;
                            MyTransparentGeometry.AddBillboardOriented(MATERIAL_VANILLA_SQUARE, BLOCKINFO_BG_COLOR, finalWorldPos, camMatrix.Left, camMatrix.Up, finalW, finalH, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                        }
                    }

                    bool isGrinder = IsGrinder;
                    bool foundComputer = false;

                    for(int i = selectedDef.Components.Length - 1; i >= 0; --i)
                    {
                        var comp = selectedDef.Components[i];

                        // red functionality line
                        if(selectedDef.CriticalGroup == i)
                        {
                            var size = new Vector2(BLOCKINFO_COMPONENT_LIST_WIDTH * scaleFOV, BLOCKINFO_RED_LINE_HEIGHT * scaleFOV);
                            var hud = posHUD;
                            hud.Y -= BLOCKINFO_ITEM_HEIGHT * i + BLOCKINFO_ITEM_HEIGHT_UNDERLINE + BLOCKINFO_Y_OFFSET_2;

                            var worldPos = GameHUDToWorld(hud);

                            worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                            MyTransparentGeometry.AddBillboardOriented(MATERIAL_SQUARE, Color.Red, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                        }

                        // blue hacking line
                        if(!foundComputer && comp.Definition.Id.TypeId == typeof(MyObjectBuilder_Component) && comp.Definition.Id.SubtypeId == COMPUTER) // HACK this is what the game checks internally, hardcoded to computer component.
                        {
                            foundComputer = true;

                            var size = new Vector2(BLOCKINFO_COMPONENT_LIST_WIDTH * scaleFOV, BLOCKINFO_BLUE_LINE_HEIGHT * scaleFOV);
                            var hud = posHUD;
                            hud.Y -= BLOCKINFO_ITEM_HEIGHT * i + BLOCKINFO_ITEM_HEIGHT_UNDERLINE + BLOCKINFO_Y_OFFSET_2;

                            var worldPos = GameHUDToWorld(hud);

                            worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                            MyTransparentGeometry.AddBillboardOriented(MATERIAL_SQUARE, Color.Blue, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                        }

                        // yellow highlight if returned component is not the same as grinded component
                        if(isGrinder && comp.DeconstructItem != comp.Definition)
                        {
                            var size = new Vector2(BLOCKINFO_COMPONENT_LIST_WIDTH * scaleFOV, BLOCKINFO_COMPONENT_LIST_SELECT_HEIGHT * scaleFOV);

                            var hud = posHUD;
                            hud.Y -= BLOCKINFO_ITEM_HEIGHT * i + BLOCKINFO_Y_OFFSET_2;

                            var worldPos = GameHUDToWorld(hud);

                            worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                            MyTransparentGeometry.AddBillboardOriented(MATERIAL_SQUARE, Color.Yellow, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                        }
                    }
                }
                #endregion





                // testing real time pressurization display
#if false
                {
                    var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

                    if(def != null && MyCubeBuilder.Static.IsActivated)
                    {
                        var grid = MyCubeBuilder.Static.FindClosestGrid();

                        Vector3D worldAdd;
                        MyCubeBuilder.Static.GetAddPosition(out worldAdd);

                        var bb = MyCubeBuilder.Static.GetBuildBoundingBox();
                        var matrix = Matrix.CreateFromQuaternion(bb.Orientation);

                        var startPos = grid.WorldToGridInteger(worldAdd);

                        for(int i = 0; i < Base6Directions.IntDirections.Length; ++i)
                        {
                            var endPos = startPos + Base6Directions.IntDirections[i];
                            bool airtight = def.IsAirTight || Pressurization.TestPressurize(startPos, endPos - startPos, matrix, def);

                            //if(!airtight)
                            //{
                            //    IMySlimBlock b2 = grid.GetCubeBlock(startPos);
                            //
                            //    if(b2 != null)
                            //    {
                            //        var def2 = (MyCubeBlockDefinition)b2.BlockDefinition;
                            //        airtight = def2.IsAirTight || Pressurization.IsPressurized(b2, endPos, startPos - endPos);
                            //    }
                            //}

                            MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), (airtight ? Color.Green : Color.Red), worldAdd, Vector3D.TransformNormal(Base6Directions.IntDirections[i], matrix), 1f, 0.1f);

                            //MyAPIGateway.Utilities.ShowNotification($"{i}. airtight={airtight}", 16); // DEBUG print
                        }
                    }
                }
#endif
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void MessageEntered(string msg, ref bool send)
        {
            try
            {
                if(msg.StartsWith(CMD_BUILDINFO, CMD_COMPARE_TYPE) || msg.StartsWith(CMD_BUILDINFO_OLD, CMD_COMPARE_TYPE))
                {
                    send = false;

                    if(selectedDef == null || msg.StartsWith(CMD_HELP, CMD_COMPARE_TYPE))
                    {
                        ShowHelp();
                    }
                    else // no arg and block equipped/selected
                    {
                        showMenu = true;
                    }

                    return;
                }

                if(msg.StartsWith(CMD_RELOAD, CMD_COMPARE_TYPE))
                {
                    send = false;
                    ReloadConfig(CMD_RELOAD);
                    return;
                }

                if(msg.StartsWith(CMD_CLEARCACHE, CMD_COMPARE_TYPE))
                {
                    send = false;
                    cachedBuildInfoNotification.Clear();
                    cachedBuildInfoTextAPI.Clear();
                    ShowChatMessage(CMD_CLEARCACHE, "Emptied block info cache.", MyFontEnum.Green);
                    return;
                }

                if(msg.StartsWith(CMD_LASERPOWER, CMD_COMPARE_TYPE))
                {
                    send = false;

                    if(selectedDef is MyLaserAntennaDefinition)
                    {
                        var arg = msg.Substring(CMD_LASERPOWER.Length);
                        float km;

                        if(float.TryParse(arg, out km))
                        {
                            var meters = (km * 1000);
                            var megaWatts = GameData.LaserAntennaPowerUsage((MyLaserAntennaDefinition)selectedDef, meters);
                            var s = new StringBuilder().Append(selectedDef.DisplayNameString).Append(" will use ").PowerFormat(megaWatts).Append(" at ").DistanceFormat(meters).Append(".");
                            ShowChatMessage(CMD_LASERPOWER, s.ToString(), MyFontEnum.Green);
                        }
                        else
                        {
                            ShowChatMessage(CMD_LASERPOWER, $"Need a distance in kilometers, e.g. {CMD_LASERPOWER} 500", MyFontEnum.Red);
                        }
                    }
                    else
                    {
                        ShowChatMessage(CMD_LASERPOWER, "Need a reference Laser Antenna, equip one first.", MyFontEnum.Red);
                    }

                    return;
                }

                if(msg.StartsWith(CMD_GETBLOCK, CMD_COMPARE_TYPE))
                {
                    send = false;

                    if(selectedBlock != null)
                    {
                        if(msg.Length > CMD_GETBLOCK.Length)
                        {
                            var arg = msg.Substring(CMD_GETBLOCK.Length);

                            if(!string.IsNullOrWhiteSpace(arg))
                            {
                                int slot;

                                if(int.TryParse(arg, out slot) && slot >= 1 && slot <= 9)
                                {
                                    MyVisualScriptLogicProvider.SetToolbarSlotToItem(slot, selectedDef.Id, -1);
                                    ShowChatMessage(CMD_GETBLOCK, $"{pickBlockDef.DisplayNameText} placed in slot {slot}.", MyFontEnum.Green);
                                }
                                else
                                {
                                    ShowChatMessage(CMD_GETBLOCK, $"'{arg}' is not a number from 1 to 9.", MyFontEnum.Red);
                                }

                                return;
                            }
                        }

                        // if no argument is defined, ask for a number
                        pickBlockDef = selectedDef;
                    }
                    else
                    {
                        ShowChatMessage(CMD_GETBLOCK, $"Aim at a block with a welder or grinder first.", MyFontEnum.Red);
                    }

                    return;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        #region Various config/state helpers
        private void ReloadConfig(string caller)
        {
            if(settings.Load())
                ShowChatMessage(caller, "Reloaded and re-saved config.", MyFontEnum.Green);
            else
                ShowChatMessage(caller, "Config created with the current settings.", MyFontEnum.Green);

            settings.Save();

            HideText();
            cachedBuildInfoTextAPI.Clear();

            if(textObject != null)
            {
                textObject.Scale = TextAPIScale;

                if(settings.alwaysVisible)
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
        }

        private void ShowHelp()
        {
            var help = string.Format(HELP_FORMAT, voxelHandSettingsInput);

            MyAPIGateway.Utilities.ShowMissionScreen("BuildInfo Mod", "", "Various help topics", help, null, "Close");
        }

        private void GuiControlRemoved(object obj)
        {
            try
            {
                if(obj.ToString().EndsWith("ScreenOptionsSpace")) // closing options menu just assumes you changed something so it'll re-check config settings
                {
                    UpdateConfigValues();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void UpdateConfigValues()
        {
            var cfg = MyAPIGateway.Session.Config;
            hudVisible = !cfg.MinimalHud;
            hudBackgroundOpacity = cfg.HUDBkOpacity;
            voxelHandSettingsInput = MyControlsSpace.VOXEL_HAND_SETTINGS.GetAssignedInputName();

            var viewportSize = MyAPIGateway.Session.Camera.ViewportSize;
            aspectRatio = (double)viewportSize.X / (double)viewportSize.Y;

            bool newRotationHints = cfg.RotationHints;

            if(rotationHints != newRotationHints)
            {
                rotationHints = newRotationHints;
                HideText();
            }
        }

        private void CycleOverlay()
        {
            if(++drawOverlay >= DRAW_OVERLAY_NAME.Length)
                drawOverlay = 0;
        }

        private void SetFreezeGizmo(bool freeze)
        {
            if(freezeGizmoNotification == null)
                freezeGizmoNotification = MyAPIGateway.Utilities.CreateNotification("");

            if(selectedBlock != null) // not cubebuilder
            {
                freezeGizmoNotification.Text = "Equip a block and aim at a grid.";
                freezeGizmoNotification.Font = MyFontEnum.Red;
            }
            else if(freeze && MyCubeBuilder.Static.DynamicMode)
            {
                freezeGizmoNotification.Text = "Aim at a grid.";
                freezeGizmoNotification.Font = MyFontEnum.Red;
            }
            else
            {
                // HACK using this method instead of MyAPIGateway.CubeBuilder.FreezeGizmo's setter because that one ignores the value and sets it to true.
                MyCubeBuilder.Static.FreezeGizmo = freeze;

                freezeGizmoNotification.Text = (freeze ? "Freeze placement position ON" : "Freeze placement position OFF");
                freezeGizmoNotification.Font = MyFontEnum.White;

                if(freeze)
                    MyCubeBuilder.Static.GetAddPosition(out lastGizmoPosition);
            }

            freezeGizmoNotification.Show();
        }
        #endregion

        #region Overlay draw
        private void DrawOverlays()
        {
            if(drawOverlay > 0 && (hudVisible || settings.alwaysVisible) && selectedDef != null)
            {
                // TODO: use?
                //overlayNotification.Text = $"Showing {DRAW_OVERLAY_NAME[drawOverlay]} overlays (Ctrl+{voxelHandSettingsInput} to cycle)";
                //overlayNotification.AliveTime = 32;
                //overlayNotification.Show();

                var def = selectedDef;

                // needed to hide text messages that are no longer used while other stuff still draws
                for(int i = 0; i < textAPILabels.Length; ++i)
                {
                    var msgObj = textAPILabels[i];

                    if(msgObj != null)
                    {
                        msgObj.Visible = false;
                        textAPIShadows[i].Visible = false;
                    }
                }

                overlaysDrawn = true;

                MatrixD drawMatrix = MatrixD.Identity;

                if(selectedBlock == null) // using cubebuilder
                {
                    var box = MyCubeBuilder.Static.GetBuildBoundingBox();
                    drawMatrix = MatrixD.CreateFromQuaternion(box.Orientation);

                    if(MyCubeBuilder.Static.DynamicMode)
                    {
                        var hit = (MyCubeBuilder.Static.HitInfo as IHitInfo);

                        if(hit != null && hit.HitEntity is IMyVoxelBase)
                            drawMatrix.Translation = hit.Position; // HACK: required for position to be accurate when aiming at a planet
                        else
                            drawMatrix.Translation = MyCubeBuilder.Static.FreePlacementTarget; // HACK: required for the position to be 100% accurate when the block is not aimed at anything
                    }
                    else
                    {
                        drawMatrix.Translation = box.Center;
                    }
                }
                else // using welder/grinder
                {
                    Matrix m;
                    Vector3D center;
                    selectedBlock.Orientation.GetMatrix(out m);
                    selectedBlock.ComputeWorldCenter(out center);

                    drawMatrix = m * selectedBlock.CubeGrid.WorldMatrix;
                    drawMatrix.Translation = center;
                }

                var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);

                #region Draw mount points
                if(TextAPIEnabled)
                {
                    DrawMountPointAxixText(def, gridSize, ref drawMatrix);
                }
                else
                {
                    // HACK re-assigning mount points temporarily to prevent the original mountpoint wireframe from being drawn while keeping the axis information
                    var mp = def.MountPoints;
                    def.MountPoints = BLANK_MOUNTPOINTS;
                    MyCubeBuilder.DrawMountPoints(gridSize, def, ref drawMatrix);
                    def.MountPoints = mp;
                }

                bool blockFunctionalForPressure = true;

                if(selectedBlock != null && selectedDef.BuildProgressModels != null)
                {
                    // HACK condition matching the condition in MyGridGasSystem.IsPressurized(MySlimBlock block, Vector3I pos, Vector3 normal)
                    blockFunctionalForPressure = (selectedBlock.BuildLevelRatio >= selectedDef.BuildProgressModels[selectedDef.BuildProgressModels.Length - 1].BuildRatioUpperBound);
                }

                // draw custom mount point styling
                {
                    var minSize = (def.CubeSize == MyCubeSize.Large ? 0.05 : 0.02); // a minimum size to have some thickness
                    var center = def.Center;
                    var mainMatrix = MatrixD.CreateTranslation((center - (def.Size * 0.5f)) * gridSize) * drawMatrix;
                    var mountPoints = def.GetBuildProgressModelMountPoints(1f);
                    bool drawLabel = settings.allLabels && TextAPIEnabled;

                    if(drawOverlay == 1)
                    {
                        if(def.IsAirTight) // HACK IsAirTight makes it airtight even if the block is not fully built.
                        {
                            var halfExtents = def.Size * (gridSize * 0.5);
                            var localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MOUNTPOINT_THICKNESS * 0.5);
                            MySimpleObjectDraw.DrawTransparentBox(ref drawMatrix, ref localBB, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE);
                        }
                        else if(blockFunctionalForPressure && mountPoints != null)
                        {
                            var half = Vector3D.One * -(0.5f * gridSize);
                            var corner = (Vector3D)def.Size * -(0.5f * gridSize);
                            var transformMatrix = MatrixD.CreateTranslation(corner - half) * drawMatrix;

                            foreach(var kv in def.IsCubePressurized) // precomputed: [position][normal] = is airtight
                            {
                                foreach(var kv2 in kv.Value)
                                {
                                    if(!kv2.Value) // pos+normal not airtight
                                        continue;

                                    var pos = Vector3D.Transform((Vector3D)(kv.Key * gridSize), transformMatrix);
                                    var dirForward = Vector3.TransformNormal(kv2.Key, drawMatrix);
                                    var dirIndex = (int)Base6Directions.GetDirection(kv2.Key);
                                    var dirUp = Vector3.TransformNormal(DIRECTIONS[((dirIndex + 2) % 6)], drawMatrix);

                                    var m = MatrixD.Identity;
                                    m.Translation = pos + dirForward * (gridSize * 0.5f);
                                    m.Forward = dirForward;
                                    m.Backward = -dirForward;
                                    m.Left = Vector3D.Cross(dirForward, dirUp);
                                    m.Right = -m.Left;
                                    m.Up = dirUp;
                                    m.Down = -dirUp;
                                    var scale = new Vector3D(gridSize, gridSize, MOUNTPOINT_THICKNESS);
                                    MatrixD.Rescale(ref m, ref scale);

                                    MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_COLOR, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                                }
                            }
                        }
                    }
                    else if(drawOverlay == 2 && mountPoints != null)
                    {
                        for(int i = 0; i < mountPoints.Length; i++)
                        {
                            var mountPoint = mountPoints[i];

                            if(!mountPoint.Enabled)
                                continue; // ignore all disabled mount points as airtight ones are rendered separate

                            var colorFace = (mountPoint.Default ? MOUNTPOINT_DEFAULT_COLOR : MOUNTPOINT_COLOR);

                            DrawMountPoint(mountPoint, gridSize, ref center, ref mainMatrix, ref colorFace, minSize);
                        }
                    }
                }
                #endregion

                #region Door airtightness toggle blinking
                if(drawOverlay == 1 && (def is MyDoorDefinition || def is MyAdvancedDoorDefinition || def is MyAirtightDoorGenericDefinition))
                {
                    if(selectedBlock != null)
                    {
                        doorAirtightBlink = true;
                    }
                    else if(!MyParticlesManager.Paused && ++doorAirtightBlinkTick >= 60)
                    {
                        doorAirtightBlinkTick = 0;
                        doorAirtightBlink = !doorAirtightBlink;
                    }

                    var cubeSize = def.Size * (gridSize * 0.5f);
                    bool drawLabel = settings.allLabels && TextAPIEnabled;

                    if(drawLabel || doorAirtightBlink)
                    {
                        bool fullyClosed = true;

                        if(selectedBlock != null)
                        {
                            var door = selectedBlock.FatBlock as IMyDoor;

                            if(door != null)
                                fullyClosed = door.Status == Sandbox.ModAPI.Ingame.DoorStatus.Closed;
                        }

                        for(int i = 0; i < 6; ++i)
                        {
                            var normal = DIRECTIONS[i];

                            if(Pressurization.IsDoorFacePressurized(def, (Vector3I)normal, fullyClosed))
                            {
                                var dirForward = Vector3D.TransformNormal(normal, drawMatrix);
                                var dirLeft = Vector3D.TransformNormal(DIRECTIONS[((i + 4) % 6)], drawMatrix);
                                var dirUp = Vector3D.TransformNormal(DIRECTIONS[((i + 2) % 6)], drawMatrix);

                                var pos = drawMatrix.Translation + dirForward * cubeSize.GetDim((i % 6) / 2);
                                float width = cubeSize.GetDim(((i + 4) % 6) / 2);
                                float height = cubeSize.GetDim(((i + 2) % 6) / 2);

                                if(doorAirtightBlink)
                                {
                                    var m = MatrixD.CreateWorld(pos, dirForward, dirUp);
                                    m.Right *= width * 2;
                                    m.Up *= height * 2;
                                    m.Forward *= MOUNTPOINT_THICKNESS;
                                    MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_TOGGLE_COLOR, ref AIRTIGHT_TOGGLE_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                                }

                                if(drawLabel) // only label the first one
                                {
                                    drawLabel = false;

                                    var labelPos = pos + dirLeft * width + dirUp * height;
                                    DrawLineLabelAlternate(TextAPIMsgIds.DOOR_AIRTIGHT, labelPos, labelPos + dirLeft * 0.5, "Airtight when closed", AIRTIGHT_TOGGLE_COLOR, underlineLength: 1.7f);

                                    if(!doorAirtightBlink) // no need to iterate further if no faces need to be rendered
                                        break;
                                }
                            }
                        }
                    }
                }
                #endregion

                #region Draw drill mining/cutout radius
                var drill = def as MyShipDrillDefinition;
                if(drill != null)
                {
                    const float lineHeight = 0.3f;
                    const int wireDivRatio = 20;
                    var colorMine = Color.Lime;
                    var colorMineFace = colorMine * 0.3f;
                    var colorCut = Color.Red;
                    var colorCutFace = colorCut * 0.3f;
                    bool drawLabels = settings.allLabels && TextAPIEnabled;

                    var mineMatrix = drawMatrix;
                    mineMatrix.Translation += mineMatrix.Forward * drill.SensorOffset;
                    MySimpleObjectDraw.DrawTransparentSphere(ref mineMatrix, drill.SensorRadius, ref colorMineFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: MATERIAL_SQUARE);

                    bool showCutOut = (Math.Abs(drill.SensorRadius - drill.CutOutRadius) > 0.0001f || Math.Abs(drill.SensorOffset - drill.CutOutOffset) > 0.0001f);

                    if(drawLabels)
                    {
                        var labelDir = mineMatrix.Down;
                        var sphereEdge = mineMatrix.Translation + (labelDir * drill.SensorRadius);
                        DrawLineLabel(TextAPIMsgIds.DRILL_MINE, sphereEdge, labelDir, (showCutOut ? "Mining radius" : "Mining/cutout radius"), colorMine, constantTextUpdate: true, lineHeight: lineHeight, underlineLength: (showCutOut ? 0.75f : 1f));
                    }

                    if(showCutOut)
                    {
                        var cutMatrix = drawMatrix;
                        cutMatrix.Translation += cutMatrix.Forward * drill.CutOutOffset;
                        MySimpleObjectDraw.DrawTransparentSphere(ref cutMatrix, drill.CutOutRadius, ref colorCutFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: MATERIAL_SQUARE);

                        if(drawLabels)
                        {
                            var labelDir = cutMatrix.Left;
                            var sphereEdge = cutMatrix.Translation + (labelDir * drill.CutOutRadius);
                            DrawLineLabel(TextAPIMsgIds.DRILL_CUTOUT, sphereEdge, labelDir, "Cutout radius", colorCut, lineHeight: lineHeight);
                        }
                    }
                }
                #endregion

                #region Welder and grinder sensors
                bool isShipWelder = def is MyShipWelderDefinition;
                if(isShipWelder || def is MyShipGrinderDefinition)
                {
                    var data = BData_Base.TryGetDataCached<BData_ShipTool>(def);

                    if(data != null)
                    {
                        const float lineHeight = 0.3f;
                        const int wireDivRatio = 20;
                        var color = Color.Lime;
                        var colorFace = color * 0.3f;
                        var radius = data.SphereDummy.Radius;

                        var sphereMatrix = drawMatrix;
                        sphereMatrix.Translation = Vector3D.Transform(data.SphereDummy.Center, drawMatrix);

                        MySimpleObjectDraw.DrawTransparentSphere(ref sphereMatrix, radius, ref colorFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: MATERIAL_SQUARE);

                        if(settings.allLabels && TextAPIEnabled)
                        {
                            var labelDir = sphereMatrix.Down;
                            var sphereEdge = sphereMatrix.Translation + (labelDir * radius);
                            DrawLineLabel(TextAPIMsgIds.SHIP_TOOL, sphereEdge, labelDir, (isShipWelder ? "Welding radius" : "Grinding radius"), color, constantTextUpdate: true, lineHeight: lineHeight, underlineLength: 0.75f);
                        }
                    }
                }
                #endregion

                #region Weapon accuracy indicator
                var weapon = def as MyWeaponBlockDefinition;
                if(weapon != null)
                {
                    var data = BData_Base.TryGetDataCached<BData_Weapons>(def);

                    if(data != null)
                    {
                        const int wireDivideRatio = 12;
                        const float lineHeight = 0.5f;
                        var color = Color.Red;
                        var colorFace = color * 0.5f;
                        var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);
                        MyAmmoDefinition ammo = null;

                        if(selectedBlock != null)
                        {
                            var weaponBlock = selectedBlock.FatBlock as IMyGunObject<MyGunBase>;

                            if(weaponBlock != null)
                                ammo = weaponBlock.GunBase.CurrentAmmoDefinition;
                        }

                        if(ammo == null)
                        {
                            var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[0]);
                            ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                        }

                        var height = ammo.MaxTrajectory;
                        var tanShotAngle = (float)Math.Tan(wepDef.DeviateShotAngle);
                        var accuracyAtMaxRange = tanShotAngle * (height * 2);
                        var coneMatrix = data.muzzleLocalMatrix * drawMatrix;

                        MyTransparentGeometry.AddPointBillboard(MATERIAL_VANILLA_DOT, color, coneMatrix.Translation, 0.025f, 0, blendType: BlendTypeEnum.SDR); // this is drawn always on top on purpose
                        MySimpleObjectDraw.DrawTransparentCone(ref coneMatrix, accuracyAtMaxRange, height, ref colorFace, wireDivideRatio, faceMaterial: MATERIAL_SQUARE);

                        //const int circleWireDivideRatio = 20;
                        //var accuracyAt100m = tanShotAngle * (100 * 2);
                        //var color100m = Color.Green.ToVector4();
                        //var circleMatrix = MatrixD.CreateWorld(coneMatrix.Translation + coneMatrix.Forward * 3 + coneMatrix.Left * 3, coneMatrix.Down, coneMatrix.Forward);
                        //MySimpleObjectDraw.DrawTransparentCylinder(ref circleMatrix, accuracyAt100m, accuracyAt100m, 0.1f, ref color100m, true, circleWireDivideRatio, 0.05f, MATERIAL_SQUARE);

                        if(settings.allLabels && TextAPIEnabled)
                        {
                            var labelDir = coneMatrix.Up;
                            var labelLineStart = coneMatrix.Translation + coneMatrix.Forward * 3;
                            DrawLineLabel(TextAPIMsgIds.ACCURACY_MAX, labelLineStart, labelDir, $"Accuracy cone - {height} m", color, constantTextUpdate: true, lineHeight: lineHeight, underlineLength: 1.75f);

                            //var lineStart = circleMatrix.Translation + coneMatrix.Down * accuracyAt100m;
                            //var labelStart = lineStart + coneMatrix.Down * 0.3f;
                            //DrawLineLabelAlternate(TextAPIMsgIds.ACCURACY_100M, lineStart, labelStart, "At 100m (zoomed)", color100m, underlineLength: 1.5f);
                        }
                    }
                }
                #endregion

                #region Thruster damage visualization
                var thrust = def as MyThrustDefinition;
                if(thrust != null)
                {
                    var data = BData_Base.TryGetDataCached<BData_Thrust>(def);

                    if(data != null)
                    {
                        const int wireDivideRatio = 12;
                        const float lineHeight = 0.3f;
                        var color = Color.Red;
                        var colorFace = color * 0.5f;
                        var capsuleMatrix = MatrixD.CreateWorld(Vector3D.Zero, drawMatrix.Up, drawMatrix.Backward); // capsule is rotated weirdly (pointing up), needs adjusting
                        bool drawLabel = settings.allLabels && TextAPIEnabled;

                        foreach(var flame in data.damageFlames)
                        {
                            var start = Vector3D.Transform(flame.LocalFrom, drawMatrix);

                            capsuleMatrix.Translation = start + (drawMatrix.Forward * (flame.Height * 0.5));
                            MySimpleObjectDraw.DrawTransparentCapsule(ref capsuleMatrix, flame.Radius, flame.Height, ref colorFace, wireDivideRatio, MATERIAL_SQUARE);

                            if(drawLabel)
                            {
                                drawLabel = false; // label only on the first flame
                                var labelDir = drawMatrix.Down;
                                var labelLineStart = Vector3D.Transform(flame.LocalTo, drawMatrix) + labelDir * flame.Radius;
                                DrawLineLabel(TextAPIMsgIds.THRUST_DAMAGE, labelLineStart, labelDir, "Thrust damage", color, lineHeight: lineHeight, underlineLength: 1.1f);
                            }
                        }
                    }
                }
                #endregion

                #region Landing gear magnet points
                if(def is MyLandingGearDefinition)
                {
                    var data = BData_Base.TryGetDataCached<BData_LandingGear>(def);

                    if(data != null)
                    {
                        var color = new Color(20, 255, 155);
                        var colorFace = color * 0.5f;
                        bool drawLabel = settings.allLabels && TextAPIEnabled;

                        foreach(var obb in data.Magents)
                        {
                            var localBB = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
                            var m = MatrixD.CreateFromQuaternion(obb.Orientation);
                            m.Translation = obb.Center;
                            m *= drawMatrix;

                            MySimpleObjectDraw.DrawTransparentBox(ref m, ref localBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE);

                            if(drawLabel)
                            {
                                drawLabel = false; // only label the first one
                                var labelDir = drawMatrix.Down;
                                var labelLineStart = m.Translation + (m.Down * localBB.HalfExtents.Y) + (m.Backward * localBB.HalfExtents.Z) + (m.Left * localBB.HalfExtents.X);
                                DrawLineLabel(TextAPIMsgIds.MAGNET, labelLineStart, labelDir, "Magnet", color, lineHeight: 0.5f, underlineLength: 0.7f);
                            }
                        }
                    }
                }
                #endregion
            }
            else
            {
                // no block equipped

                if(overlaysDrawn)
                {
                    overlaysDrawn = false;

                    for(int i = 0; i < textAPILabels.Length; ++i)
                    {
                        var msgObj = textAPILabels[i];

                        if(msgObj != null)
                        {
                            msgObj.Visible = false;
                            textAPIShadows[i].Visible = false;
                        }
                    }
                }
            }
        }

        private void DrawLineLabelAlternate(TextAPIMsgIds id, Vector3D start, Vector3D end, string text, Color color, bool constantTextUpdate = false, float lineThick = 0.005f, float underlineLength = 0.75f)
        {
            var cm = MyAPIGateway.Session.Camera.WorldMatrix;
            var direction = (end - start);

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, start, direction, 1f, lineThick, LABELS_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, LABEL_SHADOW_COLOR, start + cm.Right * LABEL_SHADOW_OFFSET + cm.Down * LABEL_SHADOW_OFFSET + cm.Forward * LABEL_SHADOW_OFFSET_Z, direction, 1f, lineThick, LABELS_BLEND_TYPE);

            if(!settings.allLabels || (!settings.axisLabels && (int)id < 3))
                return;

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, end, cm.Right, underlineLength, lineThick, LABELS_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, LABEL_SHADOW_COLOR, end + cm.Right * LABEL_SHADOW_OFFSET + cm.Down * LABEL_SHADOW_OFFSET + cm.Forward * LABEL_SHADOW_OFFSET_Z, cm.Right, underlineLength, lineThick, LABELS_BLEND_TYPE);

            DrawSimpleLabel(id, end, text, color, constantTextUpdate);
        }

        private void DrawLineLabel(TextAPIMsgIds id, Vector3D start, Vector3D direction, string text, Color color, bool constantTextUpdate = false, float lineHeight = 0.3f, float lineThick = 0.005f, float underlineLength = 0.75f)
        {
            var cm = MyAPIGateway.Session.Camera.WorldMatrix;
            var end = start + direction * lineHeight;

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, start, direction, lineHeight, lineThick, LABELS_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, LABEL_SHADOW_COLOR, start + cm.Right * LABEL_SHADOW_OFFSET + cm.Down * LABEL_SHADOW_OFFSET + cm.Forward * LABEL_SHADOW_OFFSET_Z, direction, lineHeight, lineThick, LABELS_BLEND_TYPE);

            if(!settings.allLabels || (!settings.axisLabels && (int)id < 3))
                return;

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, end, cm.Right, underlineLength, lineThick, LABELS_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, LABEL_SHADOW_COLOR, end + cm.Right * LABEL_SHADOW_OFFSET + cm.Down * LABEL_SHADOW_OFFSET + cm.Forward * LABEL_SHADOW_OFFSET_Z, cm.Right, underlineLength, lineThick, LABELS_BLEND_TYPE);

            DrawSimpleLabel(id, end, text, color, constantTextUpdate);
        }

        private void DrawSimpleLabel(TextAPIMsgIds id, Vector3D worldPos, string text, Color textColor, bool updateText = false)
        {
            var i = (int)id;
            var camera = MyAPIGateway.Session.Camera;
            var msgObj = textAPILabels[i];
            HudAPIv2.SpaceMessage shadowObj = textAPIShadows[i];

            if(msgObj == null)
            {
                textAPILabels[i] = msgObj = new HudAPIv2.SpaceMessage(new StringBuilder(), worldPos, Vector3D.Up, Vector3D.Left, 0.1, Blend: LABELS_BLEND_TYPE);
                msgObj.Offset = new Vector2D(0.1, 0.1);

                textAPIShadows[i] = shadowObj = new HudAPIv2.SpaceMessage(new StringBuilder(), worldPos, Vector3D.Up, Vector3D.Left, 0.1, Blend: LABELS_BLEND_TYPE);
                shadowObj.Offset = msgObj.Offset + new Vector2D(LABEL_SHADOW_OFFSET, -LABEL_SHADOW_OFFSET);

                updateText = true;
            }

            if(updateText)
            {
                msgObj.Message.Clear().Append(textColor.ToTextAPIColor()).Append(text);
                shadowObj.Message.Clear().Append(LABEL_SHADOW_COLOR.ToTextAPIColor()).Append(text);
            }

            msgObj.Visible = true;
            msgObj.WorldPosition = worldPos;
            msgObj.Left = camera.WorldMatrix.Left;
            msgObj.Up = camera.WorldMatrix.Up;

            shadowObj.Visible = true;
            shadowObj.WorldPosition = worldPos;
            shadowObj.Left = camera.WorldMatrix.Left;
            shadowObj.Up = camera.WorldMatrix.Up;
        }

        private void DrawMountPoint(MyCubeBlockDefinition.MountPoint mountPoint, float cubeSize, ref Vector3I center, ref MatrixD mainMatrix, ref Color colorFace, double minSize)
        {
            var startLocal = mountPoint.Start - center;
            var endLocal = mountPoint.End - center;

            var bb = new BoundingBoxD(Vector3.Min(startLocal, endLocal) * cubeSize, Vector3.Max(startLocal, endLocal) * cubeSize);
            var obb = new MyOrientedBoundingBoxD(bb, mainMatrix);

            var normalAxis = Base6Directions.GetAxis(Base6Directions.GetDirection(ref mountPoint.Normal));

            var m = MatrixD.CreateFromQuaternion(obb.Orientation);
            m.Right *= Math.Max(obb.HalfExtent.X * 2, (normalAxis == Base6Directions.Axis.LeftRight ? MOUNTPOINT_THICKNESS : 0));
            m.Up *= Math.Max(obb.HalfExtent.Y * 2, (normalAxis == Base6Directions.Axis.UpDown ? MOUNTPOINT_THICKNESS : 0));
            m.Forward *= Math.Max(obb.HalfExtent.Z * 2, (normalAxis == Base6Directions.Axis.ForwardBackward ? MOUNTPOINT_THICKNESS : 0));
            m.Translation = obb.Center;

            MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref colorFace, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);

            //var colorWire = colorFace * 4;
            //MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref colorWire, MySimpleObjectRasterizer.Wireframe, 1, lineWidth: 0.005f, lineMaterial: MATERIAL_SQUARE, onlyFrontFaces: true);
        }

        private void DrawMountPointAxixText(MyCubeBlockDefinition def, float gridSize, ref MatrixD drawMatrix)
        {
            var matrix = MatrixD.CreateScale(def.Size * gridSize);
            matrix.Translation = (def.Center - (def.Size * 0.5f));
            matrix = matrix * drawMatrix;

            DrawAxis(TextAPIMsgIds.AXIS_Z, ref Vector3.Forward, Color.Blue, ref drawMatrix, ref matrix);
            DrawAxis(TextAPIMsgIds.AXIS_X, ref Vector3.Right, Color.Red, ref drawMatrix, ref matrix);
            DrawAxis(TextAPIMsgIds.AXIS_Y, ref Vector3.Up, Color.Lime, ref drawMatrix, ref matrix);
        }

        private void DrawAxis(TextAPIMsgIds id, ref Vector3 direction, Color color, ref MatrixD drawMatrix, ref MatrixD matrix)
        {
            var dir = Vector3D.TransformNormal(direction * 0.5f, matrix);
            var text = AXIS_LABELS[(int)id];
            DrawLineLabel(id, drawMatrix.Translation, dir, text, color, lineHeight: 1.5f, underlineLength: text.Length * 0.1f);
        }
        #endregion

        #region Menu generation
        private StringBuilder AddMenuItemLine(int item, bool enabled = true)
        {
            AddLine(font: (menuSelectedItem == item ? MyFontEnum.Green : (enabled ? MyFontEnum.White : MyFontEnum.Red)));

            if(menuSelectedItem == item)
                GetLine().Color(COLOR_GOOD).Append("  > ");
            else
                GetLine().Color(enabled ? COLOR_NORMAL : COLOR_UNIMPORTANT).Append(' ', 6);

            return GetLine();
        }

        private void GenerateMenuText()
        {
            ResetLines();

            bool canUseTextAPI = (textAPI != null && textAPI.Heartbeat);

            AddLine(MyFontEnum.Blue).Color(COLOR_BLOCKTITLE).Append("Build info mod").ResetTextAPIColor().EndLine();

            int i = 0;

            // HACK this must match the data from the HandleInput() which controls the actual actions of these

            AddMenuItemLine(i++).Append("Close menu");
            if(voxelHandSettingsInput != null)
                GetLine().Append("   (").Append(voxelHandSettingsInput).Append(")");
            GetLine().ResetTextAPIColor().EndLine();

            if(TextAPIEnabled)
            {
                AddLine().EndLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Actions:").ResetTextAPIColor().EndLine();
            }

            AddMenuItemLine(i++).Append("Add aimed block to toolbar").Color(COLOR_UNIMPORTANT).Append("   (/pick)").ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Help topics").Color(COLOR_UNIMPORTANT).Append("   (/buildinfo help)").ResetTextAPIColor().EndLine();

            if(TextAPIEnabled)
            {
                AddLine().EndLine();
                AddLine().Color(COLOR_BLOCKTITLE).Append("Settings:").ResetTextAPIColor().EndLine();
            }

            AddMenuItemLine(i++).Append("Text info: ").Append(settings.showTextInfo ? "ON" : "OFF").ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Draw overlays: ").Append(DRAW_OVERLAY_NAME[drawOverlay]);
            if(voxelHandSettingsInput != null)
                GetLine().Append("   (Ctrl+" + voxelHandSettingsInput + ")");
            GetLine().ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Transparent model: ").Append(MyCubeBuilder.Static.UseTransparency ? "ON" : "OFF");
            if(voxelHandSettingsInput != null)
                GetLine().Append("   (Shift+" + voxelHandSettingsInput + ")");
            GetLine().ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Freeze in position: ").Append(MyAPIGateway.CubeBuilder.FreezeGizmo ? "ON" : "OFF");
            if(voxelHandSettingsInput != null)
                GetLine().Append("   (Alt+" + voxelHandSettingsInput + ")");
            GetLine().ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++, canUseTextAPI).Append("Use TextAPI: ");
            if(canUseTextAPI)
                GetLine().Append(useTextAPI ? "ON" : "OFF");
            else
                GetLine().Append("OFF (Mod not detected)");
            GetLine().ResetTextAPIColor().EndLine();

            AddMenuItemLine(i++).Append("Reload settings file").Color(COLOR_UNIMPORTANT).Append("   (/buildinfo reload)").ResetTextAPIColor().EndLine();

            if(TextAPIEnabled)
                AddLine().EndLine();

            AddLine(MyFontEnum.Blue).Color(COLOR_WARNING).Append("Up/down = ").Append(MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE.GetAssignedInputName()).Append("/").Append(MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE.GetAssignedInputName()).Append(", change = ").Append(MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE.GetAssignedInputName()).ResetTextAPIColor().Append(' ', 10).EndLine();

            if(voxelHandSettingsInput == null)
                AddLine(MyFontEnum.ErrorMessageBoxCaption).Color(COLOR_BAD).Append("The 'Open voxel hand settings' control is not assigned!").ResetTextAPIColor().EndLine();

            EndAddedLines();
        }
        #endregion

        #region CubeBuilder block info generation
        private void GenerateBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            #region Block name line only for textAPI
            if(TextAPIEnabled)
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

                GetLine().ResetTextAPIColor().EndLine();
            }
            #endregion

            AppendBasics(def, part: false);

            #region Optional - different item gain on grinding
            foreach(var comp in def.Components)
            {
                if(comp.DeconstructItem != comp.Definition)
                {
                    AddLine(MyFontEnum.ErrorMessageBoxCaption).Color(COLOR_WARNING).Append("When grinding: ").Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText).ResetTextAPIColor().EndLine();
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
            //    GetLine().ResetTextAPIColor().EndLine();
            //}

            #region Optional - creative-only stuff
            if(MyAPIGateway.Session.CreativeMode || MyAPIGateway.Session.EnableCopyPaste) // HACK Session.EnableCopyPaste used as spacemaster check
            {
                if(def.MirroringBlock != null)
                {
                    MyCubeBlockDefinition mirrorDef;
                    if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(new MyDefinitionId(def.Id.TypeId, def.MirroringBlock), out mirrorDef))
                        AddLine(MyFontEnum.Blue).Color(COLOR_GOOD).Append("Mirrors with: ").Append(mirrorDef.DisplayNameText).EndLine();
                    else
                        AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Mirrors with: ").Append(def.MirroringBlock).Append(" (Error: not found)").EndLine();
                }
            }
            #endregion

            #region Details on last lines
            if(def.Id.TypeId != typeof(MyObjectBuilder_CubeBlock)) // anything non-decorative
                GenerateAdvancedBlockText(def);

            if(!def.Context.IsBaseGame)
                AddLine(MyFontEnum.Blue).Color(COLOR_MOD).Append("Mod: ").ModFormat(def.Context).ResetTextAPIColor().EndLine();

            EndAddedLines();
            #endregion
        }

        private void AppendBasics(MyCubeBlockDefinition def, bool part = false)
        {
            int airTightFaces = 0;
            int totalFaces = 0;
            var airTight = IsAirTight(def, ref airTightFaces, ref totalFaces);
            var deformable = (def.BlockTopology == MyBlockTopology.Cube && def.UsesDeformation);
            var assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
            var buildModels = (def.BuildProgressModels != null && def.BuildProgressModels.Length > 0);
            var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
            var grindRatio = def.DisassembleRatio;

            if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition)
            {
                grindRatio *= GameData.Hardcoded.Door_Closed_DisassembleRatioMultiplier;
            }

            string padding = (part ? (TextAPIEnabled ? "        | " : "       | ") : "");

            if(part)
                AddLine(MyFontEnum.Blue).Color(COLOR_PART).Append("Part: ").Append(def.DisplayNameText).ResetTextAPIColor().EndLine();

            #region Line 1
            AddLine();

            if(part)
                GetLine().Color(COLOR_PART).Append(padding);

            GetLine().Color(new Color(200, 255, 55)).MassFormat(def.Mass).ResetTextAPIColor().Separator()
                .VectorFormat(def.Size).Separator()
                .TimeFormat(assembleTime / weldMul).Color(COLOR_UNIMPORTANT).MultiplierFormat(weldMul).ResetTextAPIColor();

            if(Math.Abs(grindRatio - 1) >= 0.0001f)
                GetLine().Separator().Color(grindRatio > 1 ? COLOR_BAD : (grindRatio < 1 ? COLOR_GOOD : COLOR_NORMAL)).Append("Deconstructs: ").PercentFormat(1f / grindRatio).ResetTextAPIColor();

            if(!buildModels)
                GetLine().Separator().Color(COLOR_WARNING).Append("(No construction models)").ResetTextAPIColor();

            GetLine().EndLine();
            #endregion

            #region Line 2
            AddLine();

            if(part)
                GetLine().Color(COLOR_PART).Append(padding).ResetTextAPIColor();

            GetLine().Append("Integrity: ").AppendFormat("{0:#,###,###,###,###}", def.MaxIntegrity).Separator();

            GetLine().Color(deformable ? COLOR_WARNING : COLOR_NORMAL).Append("Deformable: ");
            if(deformable)
                GetLine().Append("Yes (").PercentFormat(def.DeformationRatio).Append(")");
            else
                GetLine().Append("No");

            GetLine().ResetTextAPIColor();

            if(Math.Abs(def.GeneralDamageMultiplier - 1) >= 0.0001f)
            {
                GetLine().Separator()
                    .Color(def.GeneralDamageMultiplier > 1 ? COLOR_BAD : (def.GeneralDamageMultiplier < 1 ? COLOR_GOOD : COLOR_NORMAL))
                    .Append("Damage intake: ").PercentFormat(def.GeneralDamageMultiplier)
                    .ResetTextAPIColor();
            }

            GetLine().EndLine();
            #endregion

            #region Line 3
            AddLine(font: (airTight ? MyFontEnum.Green : (airTightFaces == 0 ? MyFontEnum.Red : MyFontEnum.Blue)));

            if(part)
                GetLine().Color(COLOR_PART).Append(padding);

            GetLine().Color(airTight ? COLOR_GOOD : (airTightFaces == 0 ? COLOR_BAD : COLOR_WARNING)).Append("Air-tight faces: ");

            if(airTight)
                GetLine().Append("all");
            else
                GetLine().Append(airTightFaces).Append(" of ").Append(totalFaces);

            GetLine().ResetTextAPIColor().EndLine();
            #endregion
        }

        private void GenerateAdvancedBlockText(MyCubeBlockDefinition def)
        {
            var defTypeId = def.Id.TypeId;

            // HACK: control panel type doesn't have a definition
            if(defTypeId == typeof(MyObjectBuilder_TerminalBlock)) // control panel block
            {
                // HACK hardcoded; control panel doesn't use power
                AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("Power required*: No").EndLine();
                return;
            }

            // HACK: conveyor blocks have no definition
            if(defTypeId == typeof(MyObjectBuilder_Conveyor) || defTypeId == typeof(MyObjectBuilder_ConveyorConnector)) // conveyor hubs and tubes
            {
                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.Conveyors_PowerReq).Separator().ResourcePriority("Conveyors", hardcoded: true).EndLine();
                return;
            }

            var shipDrill = def as MyShipDrillDefinition;
            if(shipDrill != null)
            {
                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.ShipDrill_Power).Separator().ResourcePriority(shipDrill.ResourceSinkGroup).EndLine();

                float volume;
                if(GetInventoryFromComponent(def, out volume))
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume, GameData.Hardcoded.ShipDrill_InventoryConstraint).EndLine();
                }
                else
                {
                    AddLine().Append("Inventory*: ").InventoryFormat(GameData.Hardcoded.ShipDrill_InventoryVolume(def), GameData.Hardcoded.ShipDrill_InventoryConstraint).EndLine();
                }

                AddLine().Append("Mining radius: ").DistanceFormat(shipDrill.SensorRadius).Separator().Append("Front offset: ").DistanceFormat(shipDrill.SensorOffset).EndLine();
                AddLine().Append("Cutout radius: ").DistanceFormat(shipDrill.CutOutRadius).Separator().Append("Front offset: ").DistanceFormat(shipDrill.CutOutOffset).EndLine();

                AddLine(MyFontEnum.DarkBlue).Color(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(voxelHandSettingsInput).Append(" to visualize sensors)").ResetTextAPIColor().EndLine();
                return;
            }

            // HACK: ship connector has no definition
            if(defTypeId == typeof(MyObjectBuilder_ShipConnector))
            {
                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.ShipConnector_PowerReq(def)).Separator().ResourcePriority(GameData.Hardcoded.ShipConnector_PowerGroup, hardcoded: true).EndLine();

                float volume;
                if(GetInventoryFromComponent(def, out volume))
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                }
                else
                {
                    AddLine().Append("Inventory*: ").InventoryFormat(GameData.Hardcoded.ShipConnector_InventoryVolume(def)).EndLine();
                }

                var data = BData_Base.TryGetDataCached<BData_Connector>(def);

                if(data != null)
                {
                    if(data.Connector)
                    {
                        AddLine().Append("Connectable: Yes");
                    }
                    else
                    {
                        AddLine().Color(COLOR_WARNING).Append("Connectable: No").ResetTextAPIColor();
                    }

                    GetLine().Separator().Append("Can throw contents*: Yes").EndLine();
                }

                return;
            }

            var shipWelder = def as MyShipWelderDefinition;
            var shipGrinder = def as MyShipGrinderDefinition;
            if(shipWelder != null || shipGrinder != null)
            {
                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.ShipTool_PowerReq).Separator().ResourcePriority(GameData.Hardcoded.ShipTool_PowerGroup, hardcoded: true).EndLine();

                float volume;
                if(GetInventoryFromComponent(def, out volume))
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                }
                else
                {
                    AddLine().Append("Inventory*: ").InventoryFormat(GameData.Hardcoded.ShipTool_InventoryVolume(def)).EndLine();
                }

                var data = BData_Base.TryGetDataCached<BData_ShipTool>(def);

                if(shipWelder != null)
                {
                    float weld = GameData.Hardcoded.ShipWelder_WeldPerSecond;
                    var mul = MyAPIGateway.Session.WelderSpeedMultiplier;
                    AddLine().Append("Weld speed*: ").PercentFormat(weld).Append(" split accross targets").MultiplierFormat(mul).EndLine();

                    if(data != null)
                        AddLine().Append("Welding radius: ").DistanceFormat(data.SphereDummy.Radius).EndLine();
                }
                else
                {
                    float grind = GameData.Hardcoded.ShipGrinder_GrindPerSecond;
                    var mul = MyAPIGateway.Session.GrinderSpeedMultiplier;
                    AddLine().Append("Grind speed*: ").PercentFormat(grind * mul).Append(" split accross targets").MultiplierFormat(mul).EndLine();

                    if(data != null)
                        AddLine().Append("Grinding radius: ").DistanceFormat(data.SphereDummy.Radius).EndLine();
                }

                AddLine(MyFontEnum.DarkBlue).Color(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(voxelHandSettingsInput).Append(" to visualize sensors)").ResetTextAPIColor().EndLine();
                return;
            }

            var mechanicalBase = def as MyMechanicalConnectionBlockBaseDefinition;
            if(mechanicalBase != null)
            {
                string topPart = null;
                var piston = def as MyPistonBaseDefinition;

                if(piston != null)
                {
                    topPart = piston.TopPart;

                    AddLine().Append("Power required: ").PowerFormat(piston.RequiredPowerInput).Separator().ResourcePriority(piston.ResourceSinkGroup).EndLine();
                    AddLine().Append("Extended length: ").DistanceFormat(piston.Maximum).Separator().Append("Max velocity: ").DistanceFormat(piston.MaxVelocity).EndLine();
                }
                else
                {
                    var motor = def as MyMotorStatorDefinition;

                    if(motor != null)
                    {
                        topPart = motor.TopPart;

                        AddLine().Append("Power required: ").PowerFormat(motor.RequiredPowerInput).Separator().ResourcePriority(motor.ResourceSinkGroup).EndLine();

                        var suspension = def as MyMotorSuspensionDefinition;

                        if(suspension == null)
                        {
                            AddLine().Append("Max torque: ").TorqueFormat(motor.MaxForceMagnitude).EndLine();

                            if(motor.RotorDisplacementMin < motor.RotorDisplacementMax)
                                AddLine().Append("Displacement: ").DistanceFormat(motor.RotorDisplacementMin).Append(" to ").DistanceFormat(motor.RotorDisplacementMax).EndLine();
                        }
                        else
                        {
                            AddLine().Append("Max torque: ").TorqueFormat(suspension.PropulsionForce).Separator().Append("Axle Friction: ").TorqueFormat(suspension.AxleFriction).EndLine();
                            AddLine().Append("Steering - Max angle: ").AngleFormat(suspension.MaxSteer).Separator().Append("Speed base: ").RotationSpeed(suspension.SteeringSpeed * 60).EndLine();
                            AddLine().Append("Ride height: ").DistanceFormat(suspension.MinHeight).Append(" to ").DistanceFormat(suspension.MaxHeight).EndLine();
                        }
                    }
                }

                if(topPart == null)
                    return;

                var group = MyDefinitionManager.Static.TryGetDefinitionGroup(topPart);

                if(group == null)
                    return;

                var partDef = (def.CubeSize == MyCubeSize.Large ? group.Large : group.Small);

                AppendBasics(partDef, part: true);
                return;
            }

            var shipController = def as MyShipControllerDefinition;
            if(shipController != null)
            {
                var rc = def as MyRemoteControlDefinition;
                if(rc != null)
                {
                    AddLine().Append("Power required: ").PowerFormat(rc.RequiredPowerInput).Separator().ResourcePriority(rc.ResourceSinkGroup).EndLine();
                }

                var cryo = def as MyCryoChamberDefinition;
                if(cryo != null)
                {
                    AddLine().Append("Power required: ").PowerFormat(cryo.IdlePowerConsumption).Separator().ResourcePriority(cryo.ResourceSinkGroup).EndLine();
                }

                AddLine((shipController.EnableShipControl ? MyFontEnum.Green : MyFontEnum.Red)).Append("Ship controls: ").Append(shipController.EnableShipControl ? "Yes" : "No").EndLine();
                AddLine((shipController.EnableFirstPerson ? MyFontEnum.Green : MyFontEnum.Red)).Append("First person view: ").Append(shipController.EnableFirstPerson ? "Yes" : "No").EndLine();
                AddLine((shipController.EnableBuilderCockpit ? MyFontEnum.Green : MyFontEnum.Red)).Append("Can build: ").Append(shipController.EnableBuilderCockpit ? "Yes" : "No").EndLine();

                var cockpit = def as MyCockpitDefinition;
                if(cockpit != null)
                {
                    float volume;

                    if(GetInventoryFromComponent(def, out volume))
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                    }
                    else
                    {
                        AddLine().Append("Inventory*: ").InventoryFormat(GameData.Hardcoded.Cockpit_InventoryVolume).EndLine();
                    }

                    AddLine((cockpit.IsPressurized ? MyFontEnum.Green : MyFontEnum.Red)).Append("Pressurized: ");

                    if(cockpit.IsPressurized)
                        GetLine().Append("Yes, Oxygen capacity: ").VolumeFormat(cockpit.OxygenCapacity);
                    else
                        GetLine().Append("No");

                    GetLine().EndLine();

                    if(cockpit.HUD != null)
                    {
                        MyDefinitionBase defHUD;
                        if(MyDefinitionManager.Static.TryGetDefinition(new MyDefinitionId(typeof(MyObjectBuilder_HudDefinition), cockpit.HUD), out defHUD))
                        {
                            // HACK MyHudDefinition is not whitelisted; also GetObjectBuilder() is useless because it doesn't get filled in
                            //var hudDefObj = (MyObjectBuilder_HudDefinition)defBase.GetObjectBuilder();
                            AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("Custom HUD: ").Append(cockpit.HUD).ResetTextAPIColor().Separator().Color(COLOR_MOD).Append("Mod: ").ModFormat(defHUD.Context).EndLine();
                        }
                        else
                        {
                            AddLine(MyFontEnum.Red).Color(COLOR_BAD).Append("Custom HUD: ").Append(cockpit.HUD).Append("  (Error: not found)").EndLine();
                        }
                    }
                }

                return;
            }

            var thrust = def as MyThrustDefinition;
            if(thrust != null)
            {
                if(!thrust.FuelConverter.FuelId.IsNull())
                {
                    AddLine().Append("Requires power to be controlled").Separator().ResourcePriority(thrust.ResourceSinkGroup).EndLine();
                    AddLine().Append("Requires fuel: ").Append(thrust.FuelConverter.FuelId.SubtypeId).Separator().Append("Efficiency: ").NumFormat(thrust.FuelConverter.Efficiency * 100, 2).Append("%").EndLine();
                }
                else
                {
                    AddLine().Append("Power: ").PowerFormat(thrust.MaxPowerConsumption).Separator().Append("Idle: ").PowerFormat(thrust.MinPowerConsumption).Separator().ResourcePriority(thrust.ResourceSinkGroup).EndLine();
                }

                AddLine().Append("Force: ").ForceFormat(thrust.ForceMagnitude).Separator().Append("Dampener factor: ").NumFormat(thrust.SlowdownFactor, 3).EndLine();

                if(thrust.EffectivenessAtMinInfluence < 1.0f || thrust.EffectivenessAtMaxInfluence < 1.0f)
                {
                    // thrust.NeedsAtmosphereForInfluence seems to be a pointless var

                    AddLine(thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).Color(thrust.EffectivenessAtMaxInfluence < 1f ? COLOR_BAD : COLOR_GOOD)
                        .PercentFormat(thrust.EffectivenessAtMaxInfluence).Append(" max thrust ").ResetTextAPIColor();
                    if(thrust.MaxPlanetaryInfluence < 1f)
                        GetLine().Append("in ").PercentFormat(thrust.MaxPlanetaryInfluence).Append(" atmosphere");
                    else
                        GetLine().Append("in atmosphere");
                    GetLine().EndLine();

                    AddLine(thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White).Color(thrust.EffectivenessAtMinInfluence < 1f ? COLOR_BAD : COLOR_GOOD)
                        .PercentFormat(thrust.EffectivenessAtMinInfluence).Append(" max thrust ").ResetTextAPIColor();
                    if(thrust.MinPlanetaryInfluence > 0f)
                        GetLine().Append("below ").PercentFormat(thrust.MinPlanetaryInfluence).Append(" atmosphere");
                    else
                        GetLine().Append("in space");
                    GetLine().EndLine();
                }
                else
                {
                    AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("No thrust limits in space or planets").EndLine();
                }

                if(thrust.ConsumptionFactorPerG > 0)
                    AddLine(MyFontEnum.Red).Append("Extra consumption: +").PercentFormat(thrust.ConsumptionFactorPerG).Append(" per natural g acceleration").EndLine();

                var data = BData_Base.TryGetDataCached<BData_Thrust>(def);

                if(data != null)
                {
                    var flameDistance = data.distance * Math.Max(1, thrust.SlowdownFactor); // if dampeners are stronger than normal thrust then the flame will be longer... not sure if this scaling is correct though

                    // HACK hardcoded; from MyThrust.ThrustDamageDealDamage() and MyThrust.DamageGrid()
                    var damage = thrust.FlameDamage * data.flamesCount * 60; // 60 = ticks in a second
                    var flameShipDamage = damage;
                    var flameDamage = damage * data.radius;

                    AddLine();

                    if(data.flamesCount > 1)
                        GetLine().Append("Flames: ").Append(data.flamesCount).Separator().Append("Max distance: ");
                    else
                        GetLine().Append("Flame max distance: ");

                    GetLine().DistanceFormat(flameDistance).Separator().Append("Damage: ").NumFormat(flameShipDamage, 1).Append("/s to ships").Separator().NumFormat(flameDamage, 1).Append("/s to other things").EndLine();
                }

                AddLine(MyFontEnum.DarkBlue).Color(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(voxelHandSettingsInput).Append(" to visualize flames damage area)").ResetTextAPIColor().EndLine();
                return;
            }

            var lg = def as MyLandingGearDefinition;
            if(lg != null)
            {
                // HACK: hardcoded; LG doesn't require power
                AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("Power required*: No").ResetTextAPIColor().EndLine();
                AddLine().Append("Max differential velocity for locking: ").SpeedFormat(lg.MaxLockSeparatingVelocity).EndLine();

                AddLine(MyFontEnum.DarkBlue).Color(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(voxelHandSettingsInput).Append(" to visualize magnets)").ResetTextAPIColor().EndLine();
                return;
            }

            var light = def as MyLightingBlockDefinition;
            if(light != null)
            {
                var radius = light.LightRadius;
                var spotlight = def as MyReflectorBlockDefinition;
                if(spotlight != null)
                    radius = light.LightReflectorRadius;

                AddLine().Append("Power required: ").PowerFormat(light.RequiredPowerInput).Separator().ResourcePriority(light.ResourceSinkGroup).EndLine();
                AddLine().Append("Radius: ").DistanceFormat(radius.Min).Append(" to ").DistanceFormat(radius.Max).Separator().Append("Default: ").DistanceFormat(radius.Default).EndLine();
                AddLine().Append("Intensity: ").NumFormat(light.LightIntensity.Min, 3).Append(" to ").NumFormat(light.LightIntensity.Max, 3).Separator().Append("Default: ").NumFormat(light.LightIntensity.Default, 3).EndLine();
                AddLine().Append("Falloff: ").NumFormat(light.LightFalloff.Min, 3).Append(" to ").NumFormat(light.LightFalloff.Max, 3).Separator().Append("Default: ").NumFormat(light.LightFalloff.Default, 3).EndLine();

                if(spotlight == null)
                    AddLine(MyFontEnum.Blue).Append("Physical collisions: ").Append(light.HasPhysics ? "On" : "Off").EndLine();

                return;
            }

            var oreDetector = def as MyOreDetectorDefinition;
            if(oreDetector != null)
            {
                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.OreDetector_PowerReq).Separator().ResourcePriority(oreDetector.ResourceSinkGroup).EndLine();
                AddLine().Append("Max range: ").DistanceFormat(oreDetector.MaximumRange).EndLine();
                return;
            }

            var gyro = def as MyGyroDefinition;
            if(gyro != null)
            {
                AddLine().Append("Power required: ").PowerFormat(gyro.RequiredPowerInput).Separator().ResourcePriority(gyro.ResourceSinkGroup).EndLine();
                AddLine().Append("Force: ").ForceFormat(gyro.ForceMagnitude).EndLine();
                return;
            }

            var projector = def as MyProjectorDefinition;
            if(projector != null)
            {
                AddLine().Append("Power required: ").PowerFormat(projector.RequiredPowerInput).Separator().ResourcePriority(projector.ResourceSinkGroup).EndLine();
                return;
            }

            var door = def as MyDoorDefinition;
            if(door != null)
            {
                float moveTime = (1f / ((MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS / 1000f) * door.OpeningSpeed)) / MyEngineConstants.UPDATE_STEPS_PER_SECOND; // computed after MyDoor.UpdateCurrentOpening()

                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.Door_PowerReq).Separator().ResourcePriority(door.ResourceSinkGroup).EndLine();
                AddLine().Append("Move time: ").TimeFormat(moveTime).Separator().Append("Distance: ").DistanceFormat(door.MaxOpen).EndLine();

                AddLine(MyFontEnum.DarkBlue).Color(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(voxelHandSettingsInput).Append(" to visualize closed airtightness)").ResetTextAPIColor().EndLine();
                return;
            }

            var airTightDoor = def as MyAirtightDoorGenericDefinition; // does not extend MyDoorDefinition
            if(airTightDoor != null)
            {
                float moveTime = (1f / ((MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS / 1000f) * airTightDoor.OpeningSpeed)) / MyEngineConstants.UPDATE_STEPS_PER_SECOND; // computed after MyDoor.UpdateCurrentOpening()

                AddLine().Append("Power: ").PowerFormat(airTightDoor.PowerConsumptionMoving).Separator().Append("Idle: ").PowerFormat(airTightDoor.PowerConsumptionIdle).Separator().ResourcePriority(airTightDoor.ResourceSinkGroup).EndLine();
                AddLine().Append("Move time: ").TimeFormat(moveTime).EndLine();

                AddLine(MyFontEnum.DarkBlue).Color(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(voxelHandSettingsInput).Append(" to visualize closed airtightness)").ResetTextAPIColor().EndLine();
                return;
            }

            var advDoor = def as MyAdvancedDoorDefinition; // does not extend MyDoorDefinition
            if(advDoor != null)
            {
                AddLine().Append("Power: ").PowerFormat(advDoor.PowerConsumptionMoving).Separator().Append("Idle: ").PowerFormat(advDoor.PowerConsumptionIdle).Separator().ResourcePriority(advDoor.ResourceSinkGroup).EndLine();

                float openTime = 0;
                float closeTime = 0;

                foreach(var seq in advDoor.OpeningSequence)
                {
                    var moveTime = (seq.MaxOpen / seq.Speed);

                    openTime = Math.Max(openTime, seq.OpenDelay + moveTime);
                    closeTime = Math.Max(closeTime, seq.CloseDelay + moveTime);
                }

                AddLine().Append("Move time - Opening: ").TimeFormat(openTime).Separator().Append("Closing: ").TimeFormat(closeTime).EndLine();

                AddLine(MyFontEnum.DarkBlue).Color(COLOR_UNIMPORTANT).Append("(Ctrl+").Append(voxelHandSettingsInput).Append(" to visualize closed airtightness)").ResetTextAPIColor().EndLine();
                return;
            }

            var parachute = def as MyParachuteDefinition;
            if(parachute != null)
            {
                float gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);

                // HACK: formulas from MyParachute.UpdateParachute()
                float atmosphere = 1.0f;
                float atmosMod = 10.0f * (atmosphere - parachute.ReefAtmosphereLevel);

                if(atmosMod <= 0.5f || double.IsNaN(atmosMod))
                {
                    atmosMod = 0.5f;
                }
                else
                {
                    atmosMod = (float)Math.Log(atmosMod - 0.99f) + 5.0f;

                    if(atmosMod < 0.5f || double.IsNaN(atmosMod))
                        atmosMod = 0.5f;
                }

                // basically the atmosphere level at which atmosMod is above 0.5; finds real atmosphere level at which chute starts to fully open
                // thanks to Equinox for helping with the math here and at maxMass :}
                float disreefAtmosphere = ((float)Math.Exp(-4.5) + 1f) / 10 + parachute.ReefAtmosphereLevel;
                float chuteSize = (atmosMod * parachute.RadiusMultiplier * gridSize) / 2.0f;
                float chuteArea = MathHelper.Pi * chuteSize * chuteSize;
                float realAirDensity = (atmosphere * 1.225f);

                const float TARGET_DESCEND_VEL = 10;
                float maxMass = 2.5f * realAirDensity * (TARGET_DESCEND_VEL * TARGET_DESCEND_VEL) * chuteArea * parachute.DragCoefficient / 9.81f;

                AddLine().Append("Power - Deploy: ").PowerFormat(parachute.PowerConsumptionMoving).Separator().Append("Idle: ").PowerFormat(parachute.PowerConsumptionIdle).Separator().ResourcePriority(parachute.ResourceSinkGroup).EndLine();
                AddLine().Append("Required item to deploy: ").Append(parachute.MaterialDeployCost).Append("x ").IdTypeSubtypeFormat(parachute.MaterialDefinitionId).EndLine();
                AddLine().Append("Required atmosphere - Minimum: ").NumFormat(parachute.MinimumAtmosphereLevel, 2).Separator().Append("Fully open: ").NumFormat(disreefAtmosphere, 2).EndLine();
                AddLine().Append("Drag coefficient: ").Append(parachute.DragCoefficient).EndLine();
                AddLine().Append("Load estimate: ").MassFormat(maxMass).Append(" falling at ").SpeedFormat(TARGET_DESCEND_VEL).Append(" in 9.81m/s² and 1.0 air density.").EndLine();
                return;
            }

            var production = def as MyProductionBlockDefinition;
            if(production != null)
            {
                AddLine().Append("Power: ").PowerFormat(production.OperationalPowerConsumption).Separator().Append("Idle: ").PowerFormat(production.StandbyPowerConsumption).Separator().ResourcePriority(production.ResourceSinkGroup).EndLine();

                var assembler = def as MyAssemblerDefinition;
                if(assembler != null)
                {
                    var mulSpeed = MyAPIGateway.Session.AssemblerSpeedMultiplier;
                    var mulEff = MyAPIGateway.Session.AssemblerEfficiencyMultiplier;

                    AddLine().Append("Assembly speed: ").PercentFormat(assembler.AssemblySpeed * mulSpeed).MultiplierFormat(mulSpeed).Separator().Append("Efficiency: ").PercentFormat(mulEff).MultiplierFormat(mulEff).EndLine();
                }

                var refinery = def as MyRefineryDefinition;
                if(refinery != null)
                {
                    var mul = MyAPIGateway.Session.RefinerySpeedMultiplier;

                    AddLine().Append("Refine speed: ").PercentFormat(refinery.RefineSpeed * mul).MultiplierFormat(mul).Separator().Append("Efficiency: ").PercentFormat(refinery.MaterialEfficiency).EndLine();
                }

                var gasTank = def as MyGasTankDefinition;
                if(gasTank != null)
                {
                    AddLine().Append("Stores: ").Append(gasTank.StoredGasId.SubtypeName).Separator().Append("Capacity: ").VolumeFormat(gasTank.Capacity).EndLine();
                }

                var oxygenGenerator = def as MyOxygenGeneratorDefinition;
                if(oxygenGenerator != null)
                {
                    AddLine().Append("Ice consumption: ").MassFormat(oxygenGenerator.IceConsumptionPerSecond).Append("/s").EndLine();

                    if(oxygenGenerator.ProducedGases.Count > 0)
                    {
                        AddLine().Append("Produces: ");

                        foreach(var gas in oxygenGenerator.ProducedGases)
                        {
                            GetLine().Append(gas.Id.SubtypeName).Append(" (").VolumeFormat(oxygenGenerator.IceConsumptionPerSecond * gas.IceToGasRatio).Append("/s), ");
                        }

                        GetLine().Length -= 2;
                        GetLine().EndLine();
                    }
                    else
                    {
                        AddLine(MyFontEnum.Red).Append("Produces: <N/A>").EndLine();
                    }
                }

                var volume = (production.InventoryMaxVolume > 0 ? production.InventoryMaxVolume : production.InventorySize.Volume);

                if(refinery != null || assembler != null)
                {
                    AddLine().Append("In+out inventories: ").InventoryFormat(volume * 2, production.InputInventoryConstraint, production.OutputInventoryConstraint).EndLine();
                }
                else
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume, production.InputInventoryConstraint).EndLine();
                }

                if(production.BlueprintClasses != null)
                {
                    if(production.BlueprintClasses.Count == 0)
                    {
                        AddLine(MyFontEnum.Red).Append("Has no blueprint classes.").EndLine();
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
                            var name = bp.DisplayNameText;
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
                        GetLine().EndLine();
                    }
                }

                return;
            }

            var upgradeModule = def as MyUpgradeModuleDefinition;
            if(upgradeModule != null)
            {
                if(upgradeModule.Upgrades == null || upgradeModule.Upgrades.Length == 0)
                {
                    AddLine(MyFontEnum.Red).Append("Upgrade: N/A").EndLine();
                }
                else
                {
                    foreach(var upgrade in upgradeModule.Upgrades)
                    {
                        AddLine().Append("Upgrade: ").Append(upgrade.UpgradeType).Append(" ");

                        switch(upgrade.ModifierType)
                        {
                            case MyUpgradeModifierType.Additive: GetLine().Append("+").Append(upgrade.Modifier).Append(" added"); break;
                            case MyUpgradeModifierType.Multiplicative: GetLine().Append("multiplied by ").Append(upgrade.Modifier); break;
                            default: GetLine().Append(upgrade.Modifier).Append(" (").Append(upgrade.ModifierType).Append(")"); break;
                        }

                        GetLine().Append(" per slot").EndLine();
                    }
                }
                return;
            }

            var powerProducer = def as MyPowerProducerDefinition;
            if(powerProducer != null)
            {
                AddLine().Append("Power output: ").PowerFormat(powerProducer.MaxPowerOutput).Separator().ResourcePriority(powerProducer.ResourceSourceGroup).EndLine();

                var reactor = def as MyReactorDefinition;
                if(reactor != null)
                {
                    if(reactor.FuelDefinition != null)
                        AddLine().Append("Requires fuel: ").IdTypeSubtypeFormat(reactor.FuelId).EndLine();

                    var volume = (reactor.InventoryMaxVolume > 0 ? reactor.InventoryMaxVolume : reactor.InventorySize.Volume);
                    var invLimit = reactor.InventoryConstraint;

                    if(invLimit != null)
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume, reactor.InventoryConstraint).EndLine();
                        AddLine(MyFontEnum.Blue).Color(COLOR_WARNING).Append("Inventory items ").Append(invLimit.IsWhitelist ? "allowed" : "NOT allowed").Append(":").ResetTextAPIColor().EndLine();

                        foreach(var id in invLimit.ConstrainedIds)
                        {
                            AddLine().Append("       - ").IdTypeSubtypeFormat(id).EndLine();
                        }

                        foreach(var type in invLimit.ConstrainedTypes)
                        {
                            AddLine().Append("       - All of type: ").IdTypeFormat(type).EndLine();
                        }
                    }
                    else
                    {
                        AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                    }
                }

                var battery = def as MyBatteryBlockDefinition;
                if(battery != null)
                {
                    AddLine(battery.AdaptibleInput ? MyFontEnum.White : MyFontEnum.Red).Append("Power input: ").PowerFormat(battery.RequiredPowerInput).Append(battery.AdaptibleInput ? " (adaptable)" : " (minimum required)").Separator().ResourcePriority(battery.ResourceSinkGroup).EndLine();
                    AddLine().Append("Power capacity: ").PowerStorageFormat(battery.MaxStoredPower).Separator().Append("Pre-charged: ").PowerStorageFormat(battery.MaxStoredPower * battery.InitialStoredPowerRatio).Append(" (").NumFormat(battery.InitialStoredPowerRatio * 100, 2).Append("%)").EndLine();
                    AddLine().Append("Discharge time: ").TimeFormat((battery.MaxStoredPower / battery.MaxPowerOutput) * 3600f).Separator().Append("Recharge time: ").TimeFormat((battery.MaxStoredPower / battery.RequiredPowerInput) * 3600f);
                    return;
                }

                var solarPanel = def as MySolarPanelDefinition;
                if(solarPanel != null)
                {
                    AddLine(solarPanel.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red).Append(solarPanel.IsTwoSided ? "Two-sided" : "One-sided").EndLine();
                }
                return;
            }

            var oxygenFarm = def as MyOxygenFarmDefinition;
            if(oxygenFarm != null)
            {
                AddLine().Append("Power: ").PowerFormat(oxygenFarm.OperationalPowerConsumption).Separator().ResourcePriority(oxygenFarm.ResourceSinkGroup).EndLine();
                AddLine().Append("Produces: ").NumFormat(oxygenFarm.MaxGasOutput, 3).Append(" ").Append(oxygenFarm.ProducedGas.SubtypeName).Append(" l/s").Separator().ResourcePriority(oxygenFarm.ResourceSourceGroup).EndLine();
                AddLine(oxygenFarm.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red).Append(oxygenFarm.IsTwoSided ? "Two-sided" : "One-sided").EndLine();
                return;
            }

            var vent = def as MyAirVentDefinition;
            if(vent != null)
            {
                AddLine().Append("Idle: ").PowerFormat(vent.StandbyPowerConsumption).Separator().Append("Operational: ").PowerFormat(vent.OperationalPowerConsumption).Separator().ResourcePriority(vent.ResourceSinkGroup).EndLine();
                AddLine().Append("Output - Rate: ").VolumeFormat(vent.VentilationCapacityPerSecond).Append("/s").Separator().ResourcePriority(vent.ResourceSourceGroup).EndLine();
                return;
            }

            var medicalRoom = def as MyMedicalRoomDefinition;
            if(medicalRoom != null)
            {
                AddLine().Append("Power*: ").PowerFormat(GameData.Hardcoded.MedicalRoom_PowerReq).Separator().ResourcePriority(medicalRoom.ResourceSinkGroup).EndLine();

                AddLine(medicalRoom.ForceSuitChangeOnRespawn ? MyFontEnum.Blue : (!medicalRoom.RespawnAllowed ? MyFontEnum.Red : MyFontEnum.White)).Append("Respawn: ").BoolFormat(medicalRoom.RespawnAllowed).Separator();
                if(medicalRoom.RespawnAllowed && medicalRoom.ForceSuitChangeOnRespawn)
                {
                    GetLine().Append("Forced suit: ");

                    if(string.IsNullOrEmpty(medicalRoom.RespawnSuitName))
                    {
                        GetLine().Append("(Error: empty)");
                    }
                    else
                    {
                        MyCharacterDefinition charDef;
                        if(MyDefinitionManager.Static.Characters.TryGetValue(medicalRoom.RespawnSuitName, out charDef))
                            GetLine().Append(charDef.Name);
                        else
                            GetLine().Append(medicalRoom.RespawnSuitName).Append(" (Error: not found)");
                    }
                }
                else
                    GetLine().Append("Forced suit: No");
                GetLine().EndLine();

                AddLine(medicalRoom.HealingAllowed ? MyFontEnum.White : MyFontEnum.Red).Append("Healing: ").BoolFormat(medicalRoom.HealingAllowed).EndLine();

                AddLine(medicalRoom.RefuelAllowed ? MyFontEnum.White : MyFontEnum.Red).Append("Recharge: ").BoolFormat(medicalRoom.RefuelAllowed).EndLine();

                AddLine().Append("Suit change: ").BoolFormat(medicalRoom.SuitChangeAllowed).EndLine();

                if(medicalRoom.CustomWardrobesEnabled && medicalRoom.CustomWardrobeNames != null && medicalRoom.CustomWardrobeNames.Count > 0)
                {
                    AddLine(MyFontEnum.Blue).Append("Usable suits:");

                    foreach(var charName in medicalRoom.CustomWardrobeNames)
                    {
                        MyCharacterDefinition charDef;
                        if(!MyDefinitionManager.Static.Characters.TryGetValue(charName, out charDef))
                            AddLine(MyFontEnum.Red).Append("    ").Append(charName).Append(" (not found in definitions)").EndLine();
                        else
                            AddLine().Append("    ").Append(charDef.DisplayNameText).EndLine();
                    }
                }
                else
                    AddLine().Append("Usable suits: (all)").EndLine();
            }

            var radioAntenna = def as MyRadioAntennaDefinition;
            if(radioAntenna != null)
            {
                AddLine().Append("Max required power*: ").PowerFormat(GameData.Hardcoded.RadioAntenna_PowerReq(radioAntenna.MaxBroadcastRadius)).Separator().ResourcePriority(radioAntenna.ResourceSinkGroup).EndLine();
                AddLine().Append("Max radius: ").DistanceFormat(radioAntenna.MaxBroadcastRadius).EndLine();
                return;
            }

            var laserAntenna = def as MyLaserAntennaDefinition;
            if(laserAntenna != null)
            {
                float mWpKm = GameData.LaserAntennaPowerUsage(laserAntenna, 1000);

                AddLine().Append("Power - Active[1]: ").PowerFormat(mWpKm).Append(" per km (/buildinfo help)").EndLine();
                AddLine().Append("Power - Turning: ").PowerFormat(laserAntenna.PowerInputTurning).Separator().Append("Idle: ").PowerFormat(laserAntenna.PowerInputIdle).Separator().ResourcePriority(laserAntenna.ResourceSinkGroup).EndLine();

                AddLine(laserAntenna.RequireLineOfSight ? MyFontEnum.White : MyFontEnum.Green)
                    .Color(laserAntenna.MaxRange < 0 ? COLOR_GOOD : COLOR_NORMAL).Append("Range: ");

                if(laserAntenna.MaxRange < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat(laserAntenna.MaxRange);

                GetLine().ResetTextAPIColor().Separator().Color(laserAntenna.RequireLineOfSight ? COLOR_WARNING : COLOR_GOOD).Append("Line-of-sight: ").Append(laserAntenna.RequireLineOfSight ? "Required" : "Not required").ResetTextAPIColor().EndLine();

                AddLine().Append("Rotation Pitch: ").AngleFormatDeg(laserAntenna.MinElevationDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxElevationDegrees).Separator().Append("Yaw: ").AngleFormatDeg(laserAntenna.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(laserAntenna.MaxAzimuthDegrees).EndLine();
                AddLine().Append("Rotation Speed: ").RotationSpeed(laserAntenna.RotationRate * 60).EndLine();

                // TODO visualize angle limits?
                return;
            }

            var beacon = def as MyBeaconDefinition;
            if(beacon != null)
            {
                AddLine().Append("Max required power*: ").PowerFormat(GameData.Hardcoded.Beacon_PowerReq(beacon.MaxBroadcastRadius)).Separator().ResourcePriority(beacon.ResourceSinkGroup).EndLine();
                AddLine().Append("Max radius: ").DistanceFormat(beacon.MaxBroadcastRadius).EndLine();
                return;
            }

            var timer = def as MyTimerBlockDefinition;
            if(timer != null)
            {
                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.Timer_PowerReq).Separator().ResourcePriority(timer.ResourceSinkGroup).EndLine();
                return;
            }

            var pb = def as MyProgrammableBlockDefinition;
            if(pb != null)
            {
                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.ProgrammableBlock_PowerReq).Separator().ResourcePriority(pb.ResourceSinkGroup).EndLine();
                return;
            }

            var sound = def as MySoundBlockDefinition;
            if(sound != null)
            {
                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.SoundBlock_PowerReq).Separator().ResourcePriority(sound.ResourceSinkGroup).EndLine();
                return;
            }

            var sensor = def as MySensorBlockDefinition;
            if(sensor != null)
            {
                var maxField = GameData.Hardcoded.Sensor_MaxField(sensor.MaxRange);
                AddLine().Append("Max required power*: ").PowerFormat(GameData.Hardcoded.Sensor_PowerReq(maxField)).Separator().ResourcePriority(sensor.ResourceSinkGroup).EndLine();
                AddLine().Append("Max area: ").VectorFormat(maxField).EndLine();

                // TODO visualize max area?
                return;
            }

            var artificialMass = def as MyVirtualMassDefinition;
            if(artificialMass != null)
            {
                AddLine().Append("Power required: ").PowerFormat(artificialMass.RequiredPowerInput).Separator().ResourcePriority(artificialMass.ResourceSinkGroup).EndLine();
                AddLine().Append("Artificial mass: ").MassFormat(artificialMass.VirtualMass).EndLine();
                return;
            }

            var spaceBall = def as MySpaceBallDefinition; // this doesn't extend MyVirtualMassDefinition
            if(spaceBall != null)
            {
                // HACK: hardcoded; SpaceBall doesn't require power
                AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("Power required*: No").ResetTextAPIColor().EndLine();
                AddLine().Append("Max artificial mass: ").MassFormat(spaceBall.MaxVirtualMass).EndLine();
                return;
            }

            var warhead = def as MyWarheadDefinition;
            if(warhead != null)
            {
                // HACK: hardcoded; Warhead doesn't require power
                AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("Power required*: No").ResetTextAPIColor().EndLine();
                AddLine().Append("Radius: ").DistanceFormat(warhead.ExplosionRadius).EndLine();
                AddLine().Append("Damage: ").AppendFormat("{0:#,###,###,###,##0.##}", warhead.WarheadExplosionDamage).EndLine();

                // TODO visualize damage radius?
                return;
            }

            var button = def as MyButtonPanelDefinition;
            if(button != null)
            {
                AddLine().Append("Power required*: ").PowerFormat(GameData.Hardcoded.ButtonPanel_PowerReq).Separator().ResourcePriority(button.ResourceSinkGroup).EndLine();
                AddLine().Append("Button count: ").Append(button.ButtonCount).EndLine();
                return;
            }

            var lcd = def as MyTextPanelDefinition;
            if(lcd != null)
            {
                AddLine().Append("Power required: ").PowerFormat(lcd.RequiredPowerInput).Separator().ResourcePriority(lcd.ResourceSinkGroup).EndLine();
                AddLine().Append("Screen resolution: ").Append(lcd.TextureResolution * lcd.TextureAspectRadio).Append("x").Append(lcd.TextureResolution).EndLine();
                AddLine().Append("Font size limits - Min: ").Append(lcd.MinFontSize).Separator().Append("Max: ").Append(lcd.MaxFontSize).EndLine();
                return;
            }

            var camera = def as MyCameraBlockDefinition;
            if(camera != null)
            {
                AddLine().Append("Power - Normal use: ").PowerFormat(camera.RequiredPowerInput).Separator().Append("Raycast charging: ").PowerFormat(camera.RequiredChargingInput).Separator().ResourcePriority(camera.ResourceSinkGroup).EndLine();
                AddLine().Append("Field of view: ").AngleFormat(camera.MinFov).Append(" to ").AngleFormat(camera.MaxFov).EndLine();
                AddLine().Append("Raycast - Cone limit: ").AngleFormatDeg(camera.RaycastConeLimit).Separator().Append("Distance limit: ");

                if(camera.RaycastDistanceLimit < 0)
                    GetLine().Append("Infinite");
                else
                    GetLine().DistanceFormat((float)camera.RaycastDistanceLimit);

                GetLine().Separator().Append("Time multiplier: ").NumFormat(camera.RaycastTimeMultiplier, 3).EndLine();

                //var index = Math.Max(camera.OverlayTexture.LastIndexOf('/'), camera.OverlayTexture.LastIndexOf('\\')); // last / or \ char
                //AddLine().Append("Overlay texture: " + camera.OverlayTexture.Substring(index + 1));

                // TODO visualize angle limits?
                return;
            }

            var cargo = def as MyCargoContainerDefinition;
            if(cargo != null)
            {
                var poweredCargo = def as MyPoweredCargoContainerDefinition;
                if(poweredCargo != null)
                {
                    AddLine().Append("Power required: ").PowerFormat(poweredCargo.RequiredPowerInput).Separator().ResourcePriority(poweredCargo.ResourceSinkGroup).EndLine();
                }

                float volume = cargo.InventorySize.Volume;

                if(Math.Abs(volume) > 0.0001f || GetInventoryFromComponent(def, out volume))
                {
                    AddLine().Append("Inventory: ").InventoryFormat(volume).EndLine();
                }
                else
                {
                    AddLine().Append("Inventory*: ").InventoryFormat(GameData.Hardcoded.CargoContainer_InventoryVolume(def)).EndLine();
                }

                return;
            }

            var sorter = def as MyConveyorSorterDefinition;
            if(sorter != null)
            {
                AddLine().Append("Power required: ").PowerFormat(sorter.PowerInput).Separator().ResourcePriority(sorter.ResourceSinkGroup).EndLine();
                AddLine().Append("Inventory: ").InventoryFormat(sorter.InventorySize.Volume).EndLine();
                return;
            }

            var gravity = def as MyGravityGeneratorBaseDefinition;
            if(gravity != null)
            {
                var gravityFlat = def as MyGravityGeneratorDefinition;
                if(gravityFlat != null)
                {
                    AddLine().Append("Max power use: ").PowerFormat(gravityFlat.RequiredPowerInput).Separator().ResourcePriority(gravityFlat.ResourceSinkGroup).EndLine();
                    AddLine().Append("Field size: ").VectorFormat(gravityFlat.MinFieldSize).Append(" to ").VectorFormat(gravityFlat.MaxFieldSize).EndLine();
                }

                var gravitySphere = def as MyGravityGeneratorSphereDefinition;
                if(gravitySphere != null)
                {
                    AddLine().Append("Base power usage: ").PowerFormat(gravitySphere.BasePowerInput).Separator().Append("Consumption: ").PowerFormat(gravitySphere.ConsumptionPower).Separator().ResourcePriority(gravitySphere.ResourceSinkGroup).EndLine();
                    AddLine().Append("Radius: ").DistanceFormat(gravitySphere.MinRadius).Append(" to ").DistanceFormat(gravitySphere.MaxRadius).EndLine();
                }

                AddLine().Append("Acceleration: ").ForceFormat(gravity.MinGravityAcceleration).Append(" to ").ForceFormat(gravity.MaxGravityAcceleration).EndLine();

                // TODO visualize field?
                return;
            }

            var jumpDrive = def as MyJumpDriveDefinition;
            if(jumpDrive != null)
            {
                AddLine().Append("Power required: ").PowerFormat(jumpDrive.RequiredPowerInput).Separator().Append("For jump: ").PowerFormat(jumpDrive.PowerNeededForJump).Separator().ResourcePriority(jumpDrive.ResourceSinkGroup).EndLine();
                AddLine().Append("Max distance: ").DistanceFormat((float)jumpDrive.MaxJumpDistance).EndLine();
                AddLine().Append("Max mass: ").MassFormat((float)jumpDrive.MaxJumpMass).EndLine();
                AddLine().Append("Jump delay: ").TimeFormat(jumpDrive.JumpDelay).EndLine();
                return;
            }

            var merger = def as MyMergeBlockDefinition;
            if(merger != null)
            {
                // HACK hardcoded; MergeBlock doesn't require power
                AddLine(MyFontEnum.Green).Color(COLOR_GOOD).Append("Power required*: No").ResetTextAPIColor().EndLine();
                AddLine().Append("Pull strength: ").AppendFormat("{0:###,###,##0.#######}", merger.Strength).EndLine();
                return;
            }

            var weapon = def as MyWeaponBlockDefinition;
            if(weapon != null)
            {
                var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);

                float requiredPowerInput = -1;

                if(def is MyLargeTurretBaseDefinition)
                {
                    requiredPowerInput = GameData.Hardcoded.Turret_PowerReq;
                }
                else
                {
                    if(defTypeId == typeof(MyObjectBuilder_SmallGatlingGun)
                    || defTypeId == typeof(MyObjectBuilder_SmallMissileLauncher)
                    || defTypeId == typeof(MyObjectBuilder_SmallMissileLauncherReload))
                        requiredPowerInput = GameData.Hardcoded.ShipGun_PowerReq;
                }

                if(requiredPowerInput > 0)
                    AddLine().Append("Power required*: ").PowerFormat(requiredPowerInput).Separator().ResourcePriority(weapon.ResourceSinkGroup).EndLine();
                else
                    AddLine().Append("Power priority: ").ResourcePriority(weapon.ResourceSinkGroup).EndLine();

                AddLine().Append("Inventory: ").InventoryFormat(weapon.InventoryMaxVolume, wepDef.AmmoMagazinesId).EndLine();

                var largeTurret = def as MyLargeTurretBaseDefinition;
                if(largeTurret != null)
                {
                    AddLine().Color(largeTurret.AiEnabled ? COLOR_GOOD : COLOR_BAD).Append("Auto-target: ").BoolFormat(largeTurret.AiEnabled).ResetTextAPIColor().Append(largeTurret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)").Separator().Color(COLOR_WARNING).Append("Max range: ").DistanceFormat(largeTurret.MaxRangeMeters).ResetTextAPIColor().EndLine();
                    AddLine().Append("Rotation - ");

                    if(largeTurret.MinElevationDegrees <= -180 && largeTurret.MaxElevationDegrees >= 180)
                        GetLine().Color(COLOR_GOOD).Append("Pitch: ").AngleFormatDeg(360);
                    else
                        GetLine().Color(COLOR_WARNING).Append("Pitch: ").AngleFormatDeg(largeTurret.MinElevationDegrees).Append(" to ").AngleFormatDeg(largeTurret.MaxElevationDegrees);

                    GetLine().ResetTextAPIColor().Append(" @ ").RotationSpeed(largeTurret.ElevationSpeed * 60).Separator();

                    if(largeTurret.MinAzimuthDegrees <= -180 && largeTurret.MaxAzimuthDegrees >= 180)
                        GetLine().Color(COLOR_GOOD).Append("Yaw: ").AngleFormatDeg(360);
                    else
                        GetLine().Color(COLOR_WARNING).Append("Yaw: ").AngleFormatDeg(largeTurret.MinAzimuthDegrees).Append(" to ").AngleFormatDeg(largeTurret.MaxAzimuthDegrees);

                    GetLine().ResetTextAPIColor().Append(" @ ").RotationSpeed(largeTurret.RotationSpeed * 60).EndLine();

                    // TODO visualize angle limits?
                }

                AddLine().Append("Accuracy: ").DistanceFormat((float)Math.Tan(wepDef.DeviateShotAngle) * 200).Append(" group at 100m").Separator().Append("Reload: ").TimeFormat(wepDef.ReloadTime / 1000);

                // TODO find a way to refresh this?
                //if(!drawBlockVolumes)
                //{
                //    var inputName = MyControlsSpace.VOXEL_HAND_SETTINGS.GetControlAssignedName();
                //    GetLine().SetTextAPIColor(COLOR_UNIMPORTANT).Append(" (Ctrl+").Append(inputName).Append(" to see accuracy)").ResetTextAPIColor();
                //}

                GetLine().EndLine();

                var ammoProjectiles = new List<MyTuple<MyAmmoMagazineDefinition, MyProjectileAmmoDefinition>>();
                var ammoMissiles = new List<MyTuple<MyAmmoMagazineDefinition, MyMissileAmmoDefinition>>();

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

                var projectilesData = wepDef.WeaponAmmoDatas[0];
                var missileData = wepDef.WeaponAmmoDatas[1];

                if(ammoProjectiles.Count > 0)
                {
                    // HACK hardcoded; from Sandbox.Game.Weapons.MyProjectile.Start()
                    const float MIN_RANGE = 0.8f;
                    const float MAX_RANGE = 1.2f;

                    // TODO check if wepDef.DamageMultiplier is used for weapons (right now in 1.186.5 it's not)

                    AddLine().Append("Projectiles - Fire rate: ").Append(Math.Round(projectilesData.RateOfFire / 60f, 3)).Append(" rounds/s")
                        .Separator().Color(projectilesData.ShotsInBurst == 0 ? COLOR_GOOD : COLOR_WARNING).Append("Magazine: ");
                    if(projectilesData.ShotsInBurst == 0)
                        GetLine().Append("No reloading");
                    else
                        GetLine().Append(projectilesData.ShotsInBurst);
                    GetLine().ResetTextAPIColor().EndLine();

                    AddLine().Append("Projectiles - ").Color(COLOR_PART).Append("Type").ResetTextAPIColor().Append(" (")
                        .Color(COLOR_STAT_SHIPDMG).Append("ship").ResetTextAPIColor().Append(", ")
                        .Color(COLOR_STAT_CHARACTERDMG).Append("character").ResetTextAPIColor().Append(", ")
                        .Color(COLOR_STAT_HEADSHOTDMG).Append("headshot").ResetTextAPIColor().Append(", ")
                        .Color(COLOR_STAT_SPEED).Append("speed").ResetTextAPIColor().Append(", ")
                        .Color(COLOR_STAT_TRAVEL).Append("travel").ResetTextAPIColor().Append(")").EndLine();

                    for(int i = 0; i < ammoProjectiles.Count; ++i)
                    {
                        var data = ammoProjectiles[i];
                        var mag = data.Item1;
                        var ammo = data.Item2;

                        AddLine().Append("      - ").Color(COLOR_PART).Append(mag.Id.SubtypeName).ResetTextAPIColor().Append(" (");

                        if(ammo.ProjectileCount > 1)
                            GetLine().Color(COLOR_STAT_PROJECTILECOUNT).Append(ammo.ProjectileCount).Append("x ");

                        GetLine().Color(COLOR_STAT_SHIPDMG).Append(ammo.ProjectileMassDamage).ResetTextAPIColor().Append(", ")
                            .Color(COLOR_STAT_CHARACTERDMG).Append(ammo.ProjectileHealthDamage).ResetTextAPIColor().Append(", ")
                            .Color(COLOR_STAT_HEADSHOTDMG).Append(ammo.HeadShot ? ammo.ProjectileHeadShotDamage : ammo.ProjectileHealthDamage).ResetTextAPIColor().Append(", ");

                        if(ammo.SpeedVar > 0)
                            GetLine().Color(COLOR_STAT_SPEED).NumFormat(ammo.DesiredSpeed * (1f - ammo.SpeedVar), 2).Append("~").NumFormat(ammo.DesiredSpeed * (1f + ammo.SpeedVar), 2).Append(" m/s");
                        else
                            GetLine().Color(COLOR_STAT_SPEED).SpeedFormat(ammo.DesiredSpeed);

                        GetLine().ResetTextAPIColor().Append(", ")
                            .Color(COLOR_STAT_TRAVEL).DistanceRangeFormat(ammo.MaxTrajectory * MIN_RANGE, ammo.MaxTrajectory * MAX_RANGE).ResetTextAPIColor().Append(")").EndLine();
                    }
                }

                if(ammoMissiles.Count > 0)
                {
                    AddLine().Append("Missiles - Fire rate: ").Append(Math.Round(missileData.RateOfFire / 60f, 3)).Append(" rounds/s")
                        .Separator().Color(missileData.ShotsInBurst == 0 ? COLOR_GOOD : COLOR_WARNING).Append("Magazine: ");
                    if(missileData.ShotsInBurst == 0)
                        GetLine().Append("No reloading");
                    else
                        GetLine().Append(missileData.ShotsInBurst);
                    GetLine().ResetTextAPIColor().EndLine();

                    AddLine().Append("Missiles - ").Color(COLOR_PART).Append("Type").ResetTextAPIColor().Append(" (")
                        .Color(COLOR_STAT_SHIPDMG).Append("damage").ResetTextAPIColor().Append(", ")
                        .Color(COLOR_STAT_CHARACTERDMG).Append("radius").ResetTextAPIColor().Append(", ")
                        .Color(COLOR_STAT_SPEED).Append("speed").ResetTextAPIColor().Append(", ")
                        .Color(COLOR_STAT_TRAVEL).Append("travel").ResetTextAPIColor().Append(")").EndLine();

                    for(int i = 0; i < ammoMissiles.Count; ++i)
                    {
                        var data = ammoMissiles[i];
                        var mag = data.Item1;
                        var ammo = data.Item2;

                        AddLine().Append("      - ").Color(COLOR_PART).Append(mag.Id.SubtypeName).ResetTextAPIColor().Append(" (")
                            .Color(COLOR_STAT_SHIPDMG).Append(ammo.MissileExplosionDamage).ResetTextAPIColor().Append(", ")
                            .Color(COLOR_STAT_CHARACTERDMG).DistanceFormat(ammo.MissileExplosionRadius).ResetTextAPIColor().Append(", ");

                        // SpeedVar is not used for missiles

                        GetLine().Color(COLOR_STAT_SPEED);

                        if(!ammo.MissileSkipAcceleration)
                            GetLine().SpeedFormat(ammo.MissileInitialSpeed).Append(" + ").SpeedFormat(ammo.MissileAcceleration).Append("²");
                        else
                            GetLine().SpeedFormat(ammo.DesiredSpeed * GameData.Hardcoded.Missile_DesiredSpeedMultiplier);

                        GetLine().ResetTextAPIColor().Append(", ").Color(COLOR_STAT_TRAVEL).DistanceFormat(ammo.MaxTrajectory)
                            .ResetTextAPIColor().Append(")").EndLine();
                    }
                }

                return;
            }
        }
        #endregion

        #region Welder/grinder block info generation
        private void GenerateAimBlockText(MyCubeBlockDefinition def)
        {
            ResetLines();

            var integrityRatio = selectedBlock.Integrity / selectedBlock.MaxIntegrity;
            var grid = selectedBlock.CubeGrid;
            var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
            var grindMul = MyAPIGateway.Session.GrinderSpeedMultiplier;
            var grindRatio = def.DisassembleRatio;

            if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition)
            {
                grindRatio *= GameData.Hardcoded.Door_Closed_DisassembleRatioMultiplier;
            }

            var terminalBlock = selectedBlock.FatBlock as IMyTerminalBlock;
            bool hasComputer = (terminalBlock != null && def.ContainsComputer());

            #region
            if(terminalBlock != null)
            {
                const int LENGTH_LIMIT = 35;

                AddLine().Append('"').Color(COLOR_BLOCKTITLE);

                var name = terminalBlock.CustomName;
                var newLine = name.IndexOf('\n');

                if(newLine >= 0)
                    name = name.Substring(0, newLine); // strip everything past new line (incl new line char)

                GetLine().AppendMaxLength(name, LENGTH_LIMIT).ResetTextAPIColor().Append('"').EndLine();
            }
            #endregion

            #region
            var mass = def.Mass;
            var massColor = Color.GreenYellow;

            if(selectedBlock.FatBlock != null)
            {
                var inv = selectedBlock.FatBlock.GetInventory();

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
                GetLine().ResetTextAPIColor().Separator().Append(" Grid mass: ").MassFormat(selectedBlock.CubeGrid.Physics.Mass);
            }

            GetLine().EndLine();
            #endregion

            #region
            AddLine().ResetTextAPIColor().Append("Integrity: ").Color(integrityRatio < def.CriticalIntegrityRatio ? COLOR_BAD : (integrityRatio < 1 ? COLOR_WARNING : COLOR_GOOD))
                .IntegrityFormat(selectedBlock.Integrity).ResetTextAPIColor()
                .Append(" / ").IntegrityFormat(selectedBlock.MaxIntegrity);

            if(selectedBlock.HasDeformation)
            {
                GetLine().Color(COLOR_BAD).Append(" (deformed)");
            }

            GetLine().ResetTextAPIColor().EndLine();
            #endregion

            #region
            if(Math.Abs(def.GeneralDamageMultiplier - 1) >= 0.0001f)
            {
                AddLine().Color(def.GeneralDamageMultiplier > 1 ? COLOR_BAD : (def.GeneralDamageMultiplier < 1 ? COLOR_GOOD : COLOR_NORMAL)).Append("Damage multiplier: ").NumFormat(def.GeneralDamageMultiplier, 2).ResetTextAPIColor().EndLine();
            }
            #endregion

            #region
            float toolMul = 1;

            if(selectedHandTool != null)
            {
                var toolDef = (MyEngineerToolBaseDefinition)MyDefinitionManager.Static.TryGetHandItemForPhysicalItem(selectedHandTool.PhysicalItemDefinition.Id);
                toolMul = toolDef.SpeedMultiplier;
            }
            else // assuming ship tool
            {
                toolMul = GameData.Hardcoded.ShipWelder_WeldPerSecond;
            }

            var buildTime = ((def.MaxIntegrity / def.IntegrityPointsPerSec) / weldMul) / toolMul;
            var grindTime = ((buildTime / (1f / grindRatio)) / grindMul);

            AddLine();

            if(!IsGrinder)
            {
                GetLine().Append("Complete: ").TimeFormat(buildTime * (1 - integrityRatio));

                if(def.CriticalIntegrityRatio < 1 && integrityRatio < def.CriticalIntegrityRatio)
                {
                    var funcTime = buildTime * def.CriticalIntegrityRatio * (1 - (integrityRatio / def.CriticalIntegrityRatio));

                    GetLine().Separator().Append("Functional: ").TimeFormat(funcTime);
                }
            }
            else
            {
                bool hackable = hasComputer && selectedBlock.OwnerId != MyAPIGateway.Session.Player.IdentityId && (integrityRatio >= def.OwnershipIntegrityRatio);
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

                GetLine().Append("Dismantled: ").TimeFormat(grindTime);

                if(hackable)
                {
                    GetLine().Separator().Append("Hacked: ").TimeFormat(hackTime);
                }
            }

            GetLine().EndLine();
            #endregion

            #region
            if(hasComputer)
            {
                AddLine();

                var relation = (selectedBlock.OwnerId > 0 ? MyAPIGateway.Session.Player.GetRelationTo(selectedBlock.OwnerId) : MyRelationsBetweenPlayerAndBlock.NoOwnership);
                var shareMode = GameData.GetBlockShareMode(selectedBlock.FatBlock);

                if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                {
                    GetLine().Color(COLOR_GOOD).Append("Access: all");
                }
                else if(shareMode == MyOwnershipShareModeEnum.All)
                {
                    if(relation == MyRelationsBetweenPlayerAndBlock.Neutral || relation == MyRelationsBetweenPlayerAndBlock.Enemies)
                        GetLine().Color(COLOR_GOOD);
                    else
                        GetLine().Color(COLOR_WARNING);

                    GetLine().Append("Access: all");
                }
                else if(shareMode == MyOwnershipShareModeEnum.Faction)
                {
                    if(relation == MyRelationsBetweenPlayerAndBlock.Owner || relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                        GetLine().Color(COLOR_GOOD);
                    else
                        GetLine().Color(COLOR_BAD);

                    GetLine().Append("Access: faction");
                }
                else if(shareMode == MyOwnershipShareModeEnum.None)
                {
                    if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                        GetLine().Color(COLOR_WARNING);
                    else
                        GetLine().Color(COLOR_BAD);

                    GetLine().Append("Access: owner");
                }

                GetLine().ResetTextAPIColor().Separator();

                if(relation == MyRelationsBetweenPlayerAndBlock.Enemies || relation == MyRelationsBetweenPlayerAndBlock.Neutral)
                    GetLine().Color(COLOR_BAD);
                else if(relation == MyRelationsBetweenPlayerAndBlock.Owner)
                    GetLine().Color(COLOR_OWNER);
                else if(relation == MyRelationsBetweenPlayerAndBlock.FactionShare)
                    GetLine().Color(COLOR_GOOD);
                else if(relation == MyRelationsBetweenPlayerAndBlock.NoOwnership)
                    GetLine().Color(COLOR_WARNING);

                if(selectedBlock.OwnerId == 0)
                {
                    GetLine().Append("Not owned");
                }
                else
                {
                    GetLine().Append("Owner: ");

                    // NOTE MyVisualScriptLogicProvider.GetPlayersName() returns local player on id 0 and id 0 is also use for "nobody" in ownership.
                    var factionTag = selectedBlock.FatBlock.GetOwnerFactionTag();

                    if(!string.IsNullOrEmpty(factionTag))
                        GetLine().Append(factionTag).Append('.');

                    // TODO: cap player name length?

                    GetLine().Append(MyVisualScriptLogicProvider.GetPlayersName(selectedBlock.FatBlock.OwnerId)).ResetTextAPIColor().EndLine();
                }
            }
            #endregion

            #region
            if(IsGrinder)
            {
                foreach(var comp in def.Components)
                {
                    if(comp.DeconstructItem != comp.Definition)
                    {
                        AddLine(MyFontEnum.ErrorMessageBoxCaption).Color(COLOR_WARNING).Append(comp.Definition.DisplayNameText).Append(" turns into ").Append(comp.DeconstructItem.DisplayNameText).ResetTextAPIColor().EndLine();
                    }
                }
            }
            #endregion

            #region
            if(grid.Physics != null)
            {
                bool hasLinearVel = !Vector3.IsZero(grid.Physics.LinearVelocity, 0.00001f);
                bool hasAngularVel = !Vector3.IsZero(grid.Physics.AngularVelocity, 0.00001f);

                if(hasLinearVel || hasAngularVel)
                {
                    AddLine().Color(COLOR_WARNING);

                    if(hasLinearVel)
                    {
                        GetLine().Append("Moving: ").SpeedFormat(grid.Physics.LinearVelocity.Length(), 5);
                    }

                    if(hasAngularVel)
                    {
                        if(hasLinearVel)
                            GetLine().Separator();

                        GetLine().Append("Rotating: ").RotationSpeed((float)grid.Physics.AngularVelocity.Length(), 5);
                    }

                    GetLine().ResetTextAPIColor().EndLine();
                }
            }
            #endregion

            #region
            if(selectedToolDefId.TypeId == typeof(MyObjectBuilder_ShipGrinder))
            {
                var controller = MyAPIGateway.Session.ControlledObject as IMyShipController;

                if(controller != null)
                {
                    var impulse = GameData.ShipGrinderImpulseForce(controller.CubeGrid, selectedBlock);

                    if(impulse > 0.00001f)
                    {
                        var speed = impulse / selectedBlock.CubeGrid.Physics.Mass;

                        if(speed >= 0.5f)
                            AddLine(MyFontEnum.ErrorMessageBoxCaption).Color(COLOR_BAD);
                        else
                            AddLine(MyFontEnum.ErrorMessageBoxCaption).Color(COLOR_WARNING);

                        GetLine().Append("Grind impulse: ").SpeedFormat(speed, 5).Append(" (").ForceFormat(impulse).Append(")").EndLine();

                        //GetLine().Append($"impulse={impulse};\ntest1={impulse / selectedBlock.CubeGrid.Physics.Mass};\ntest2={selectedBlock.CubeGrid.Physics.Mass / impulse}");
                    }
                }
            }
            #endregion

            #region
            var context = def.Context;
            if(!context.IsBaseGame)
            {
                if(TextAPIEnabled)
                {
                    AddLine().Color(COLOR_MOD).Append("Mod:").Color(COLOR_MOD_TITLE).AppendMaxLength(context.ModName, BuildInfo.MOD_NAME_MAX_LENGTH).ResetTextAPIColor().EndLine();

                    var id = context.GetWorkshopID();

                    if(id > 0)
                        AddLine().Color(COLOR_MOD).Append("       | ").ResetTextAPIColor().Append("Workshop ID: ").Append(id).EndLine();
                }
                else
                {
                    AddLine(MyFontEnum.Blue).Append("Mod: ").ModFormat(context).EndLine();
                }
            }
            #endregion

            EndAddedLines();
        }
        #endregion

        #region Text handling
        private void PostProcessText(MyDefinitionId id, bool useCache)
        {
            if(TextAPIEnabled)
            {
                var textSize = UpdateTextAPIvisuals(textAPIlines);

                if(useCache)
                {
                    cache = new CacheTextAPI(textAPIlines, textSize);

                    if(cachedBuildInfoTextAPI.ContainsKey(id))
                        cachedBuildInfoTextAPI[id] = cache;
                    else
                        cachedBuildInfoTextAPI.Add(id, cache);
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

                    if(cachedBuildInfoNotification.ContainsKey(id))
                        cachedBuildInfoNotification[id] = cache;
                    else
                        cachedBuildInfoNotification.Add(id, cache);
                }
            }
        }

        private Vector2D UpdateTextAPIvisuals(StringBuilder textSB, Vector2D textSize = default(Vector2D))
        {
            if(textObject == null)
            {
                textObject = new HudAPIv2.HUDMessage(new StringBuilder(), Vector2D.Zero, Scale: TextAPIScale, HideHud: !settings.alwaysVisible, Blend: BLOCKINFO_BLEND_TYPE);
            }

            if(bgObject == null)
            {
                bgObject = new HudAPIv2.BillBoardHUDMessage(MATERIAL_VANILLA_SQUARE, Vector2D.Zero, Color.White, HideHud: !settings.alwaysVisible, Blend: BLOCKINFO_BLEND_TYPE); // scale on bg must always remain 1
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

            if(showMenu) // in the menu
            {
                textOffset = new Vector2D(-textSize.X, textSize.Y / -2);
            }
            else if(selectedBlock != null) // welder/grinder info attached to the game's block info
            {
                // TODO: re-do in textAPI hud coords?

                var cam = MyAPIGateway.Session.Camera;
                var camMatrix = cam.WorldMatrix;
                var scaleFOV = (float)Math.Tan(cam.FovWithZoom / 2);

                UpdateCameraViewProjInvMatrix(); // required to get up2date camera data since it's before Draw() updates it
                var hud = GetGameHUDBlockInfoPos();
                hud.Y -= (BLOCKINFO_ITEM_HEIGHT * selectedDef.Components.Length) + BLOCKINFO_Y_OFFSET; // make the position top-right

                var worldPos = GameHUDToWorld(hud);
                var size = BLOCKINFO_SIZE;

                size.Y *= (float)Math.Abs(textSize.Y) / 0.03f;
                size.Y += BLOCKINFO_TEXT_PADDING;
                size *= scaleFOV;

                if(Math.Abs(aspectRatio - (1280.0 / 1024.0)) <= 0.0001) // HACK 5:4 aspect ratio manual fix
                {
                    size.X *= ASPECT_RATIO_54_FIX;
                }

                worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                double localScale = 0.1 * scaleFOV;
                var local = Vector3D.Transform(worldPos, cam.ViewMatrix);
                local.X = (local.X / (localScale * aspectRatio)) * 2;
                local.Y = (local.Y / localScale) * 2;

                var pos2D = new Vector2D(local.X, local.Y);

                var offset = new Vector2D(BLOCKINFO_TEXT_PADDING, BLOCKINFO_TEXT_PADDING) * scaleFOV;

                worldPos += camMatrix.Left * (size.X - offset.X) + camMatrix.Up * (size.Y - offset.Y);

                // using textAPI's math to convert from world to its local coords
                local = Vector3D.Transform(worldPos, cam.ViewMatrix);
                local.X = (local.X / (localScale * aspectRatio)) * 2;
                local.Y = (local.Y / localScale) * 2;

                textPos = new Vector2D(local.X, local.Y);

                // not using textAPI's background for this as drawing my own manually is easier for the 3-part billboard that I need
                bgObject.Visible = false;

                // convert world size to textAPI-local size, using textAPI's math inversed
                //float w = size.X;
                //float h = size.Y;
                //w /= (float)(localScale * aspectRatio);
                //h /= (float)localScale;
                //w *= 4;
                //h *= 4;
                //
                //bgObject.Origin = pos2D;
                //bgObject.Width = w;
                //bgObject.Height = h;
                //bgObject.Offset = Vector2D.Zero;
                //bgObject.BillBoardColor = BLOCKINFO_BG_COLOR;
            }
            else if(settings.textAPIUseCustomStyling) // custom alignment and position
            {
                textPos = settings.textAPIScreenPos;

                if(settings.textAPIAlignRight)
                    textOffset.X = -textSize.X;

                if(settings.textAPIAlignBottom)
                    textOffset.Y = -textSize.Y;
            }
            else if(!rotationHints) // right side autocomputed for rotation hints off
            {
                textPos = (aspectRatio > 5 ? TEXT_HUDPOS_RIGHT_WIDE : TEXT_HUDPOS_RIGHT);
                textOffset = new Vector2D(-textSize.X, 0);
            }
            else // left side autocomputed
            {
                textPos = (aspectRatio > 5 ? TEXT_HUDPOS_WIDE : TEXT_HUDPOS);
            }

            textObject.Origin = textPos;
            textObject.Offset = textOffset;

            if(showMenu || selectedBlock == null)
            {
                float edge = BACKGROUND_EDGE * TextAPIScale;

                bgObject.BillBoardColor = BLOCKINFO_BG_COLOR * (showMenu ? 0.95f : (settings.textAPIBackgroundOpacity < 0 ? hudBackgroundOpacity : settings.textAPIBackgroundOpacity));
                bgObject.Origin = textPos;
                bgObject.Width = (float)Math.Abs(textSize.X) + edge;
                bgObject.Height = (float)Math.Abs(textSize.Y) + edge;
                bgObject.Offset = textOffset + (textSize / 2);
            }

            textShown = true;
            return textSize;
        }

        private void UpdateVisualText()
        {
            if(TextAPIEnabled)
            {
                if(MyAPIGateway.Gui.IsCursorVisible || (!settings.showTextInfo && !showMenu))
                {
                    HideText();
                    return;
                }

                // force reset, usually needed to fix notification to textAPI transition when heartbeat returns true
                if(textObject == null || (cache == null && !(showMenu || selectedBlock != null)))
                {
                    lastDefId = default(MyDefinitionId);
                    return;
                }

                // show last generated block info message only for cubebuilder
                if(!textShown && textObject != null)
                {
                    if(showMenu || selectedBlock != null)
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
                if(MyAPIGateway.Gui.IsCursorVisible || (!settings.showTextInfo && !showMenu))
                {
                    return;
                }

                List<IMyHudNotification> hudLines = null;

                if(showMenu || selectedBlock != null)
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
                        lastDefId = default(MyDefinitionId);
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

                if(showMenu)
                {
                    // HACK this must match the data from the menu
                    const int itemsStartAt = 1;
                    const int itemsEndAt = 9;

                    var selected = itemsStartAt + menuSelectedItem;

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

        private void UpdateHandTools()
        {
            var character = MyAPIGateway.Session?.Player?.Character;

            if(character == null || character.EquippedTool == null)
                return;

            var tool = character.EquippedTool as IMyEngineerToolBase;
            var isWelder = tool is IMyWelder;

            if(!isWelder && !(tool is IMyAngleGrinder))
                return;

            var casterComp = tool.Components.Get<MyCasterComponent>();

            if(casterComp == null)
                return;

            canShowMenu = true; // to allow menu use without needing a target
            isToolSelected = true;
            selectedHandTool = tool;
            selectedToolDefId = tool.DefinitionId;

            var block = (IMySlimBlock)casterComp.HitBlock;

            if(block == null)
                return;

            selectedBlock = block;
            selectedDef = (MyCubeBlockDefinition)selectedBlock.BlockDefinition;
            return;
        }

        private void UpdateShipTools()
        {
            var controller = MyAPIGateway.Session.ControlledObject as IMyShipController;
            var casterComp = controller?.Components.Get<MyCasterComponent>(); // caster comp is added to ship controller by ship tools when character takes control

            if(casterComp == null)
            {
                prevCasterComp = null;
                shipControllerObj = null;
                return;
            }

            if(prevCasterComp != casterComp)
            {
                prevCasterComp = casterComp;

                // HACK fix for SE-7575 - Cockpit's welder/grinder aim only updates when ship moves
                var m = controller.WorldMatrix;
                casterComp.OnWorldPosChanged(ref m);

                // TODO find a better way to get selected tool type
                shipControllerObj = (MyObjectBuilder_ShipController)controller.GetObjectBuilderCubeBlock(false);
            }

            if(drawOverlay > 0)
            {
                const BlendTypeEnum BLEND_TYPE = BlendTypeEnum.Standard;
                const float REACH_DISTANCE = GameData.Hardcoded.ShipTool_ReachDistance;
                var color = new Vector4(2f, 0, 0, 0.1f); // above 1 color creates bloom
                var m = controller.WorldMatrix;

                MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, m.Translation, m.Forward, REACH_DISTANCE, 0.005f, blendType: BLEND_TYPE);
                MyTransparentGeometry.AddPointBillboard(MATERIAL_VANILLA_DOT, color, m.Translation + m.Forward * REACH_DISTANCE, 0.015f, 0f, blendType: BLEND_TYPE);
            }

            if(shipControllerObj != null && shipControllerObj.Toolbar != null && shipControllerObj.Toolbar.SelectedSlot.HasValue)
            {
                var item = shipControllerObj.Toolbar.Slots[shipControllerObj.Toolbar.SelectedSlot.Value];
                var weapon = item.Data as MyObjectBuilder_ToolbarItemWeapon;

                if(weapon != null)
                {
                    bool shipWelder = weapon.DefinitionId.TypeId == typeof(MyObjectBuilder_ShipWelder);

                    if(shipWelder || weapon.DefinitionId.TypeId == typeof(MyObjectBuilder_ShipGrinder))
                    {
                        canShowMenu = true; // to allow menu use without needing a target
                        isToolSelected = true;
                        selectedToolDefId = weapon.DefinitionId;
                    }
                }
            }

            if(!isToolSelected)
                return;

            var block = (IMySlimBlock)casterComp.HitBlock;

            if(block == null)
                return;

            selectedBlock = block;
            selectedDef = (MyCubeBlockDefinition)selectedBlock.BlockDefinition;
            return;
        }

        private void HideText()
        {
            if(textShown)
            {
                textShown = false;
                lastDefId = default(MyDefinitionId);

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
                textAPIlines.Append('\n');
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

        public static int GetStringSizeNotif(StringBuilder builder)
        {
            int endLength = builder.Length;
            int len;
            int size = 0;

            for(int i = 0; i < endLength; ++i)
            {
                if(Instance.charSize.TryGetValue(builder[i], out len))
                    size += len;
                else
                    size += 15;
            }

            return size;
        }
        #endregion Text handling
    }
}