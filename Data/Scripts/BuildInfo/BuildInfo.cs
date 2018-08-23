using System;
using System.Collections.Generic;
using System.Text;
using Digi.Input;
using Digi.BuildInfo.Extensions;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
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
            Log.ModName = MOD_NAME;
        }

        public override void BeforeStart()
        {
            IsPlayer = !(MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated);

            if(!IsPlayer) // not needed DS side
            {
                SetUpdateOrder(MyUpdateOrder.NoUpdate); // NOTE: SetUpdateOrder() throws exceptions if called in an update method.
                return;
            }

            Instance = this;
            IsInitialized = true;

            UpdateConfigValues();
            ComputeCharacterSizes();
            ComputeResourceGroups();

            InitOverlays();
            InitTextGeneration();

            int count = Enum.GetValues(typeof(TextAPIMsgIds)).Length;
            textAPILabels = new HudAPIv2.SpaceMessage[count];
            textAPIShadows = new HudAPIv2.SpaceMessage[count];

            InputLib = new InputLib();
            InputLib.AddCustomInput(new InputCustomMenuBind());

            Settings = new Settings();
            LeakInfoComp = new LeakInfoComponent();
            TerminalInfoComp = new TerminalInfoComponent(this);
            BlockMonitorComp = new BlockMonitorComponent(this);
            TextAPI = new HudAPIv2();

            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
            MyAPIGateway.Gui.GuiControlRemoved += GuiControlRemoved;
        }

        protected override void UnloadData()
        {
            if(Instance == null)
                return;

            Instance = null;

            try
            {
                if(IsInitialized)
                {
                    IsInitialized = false;

                    MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
                    MyAPIGateway.Gui.GuiControlRemoved -= GuiControlRemoved;

                    InputLib?.Dispose();
                    InputLib = null;

                    Settings?.Dispose();
                    Settings = null;

                    LeakInfoComp?.Close();
                    LeakInfoComp = null;

                    TerminalInfoComp?.Close();
                    TerminalInfoComp = null;

                    BlockMonitorComp?.Close();
                    BlockMonitorComp = null;

                    TextAPI?.Close();
                    TextAPI = null;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        #endregion

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!IsInitialized)
                    return;

                unchecked // global ticker
                {
                    ++Tick;
                }

                if(!textAPIresponded && TextAPI.Heartbeat)
                {
                    textAPIresponded = true;
                    HideText(); // force a re-check to make the HUD -> textAPI transition
                }

                // HUD toggle monitor; required here because it gets the previous value if used in HandleInput()
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
                    hudVisible = !MyAPIGateway.Session.Config.MinimalHud;

                InputLib?.Update();

                LeakInfoComp?.Update();

                TerminalInfoComp?.Update();

                Update();

                if(pickBlockDef != null && Tick % 5 == 0)
                {
                    MyAPIGateway.Utilities.ShowNotification($"Press SLOT number for '{pickBlockDef.DisplayNameText}'; or Slot0/Unequip to cancel.", 16 * 5, MyFontEnum.Blue);
                }

                if(Tick % CACHE_PURGE_TICKS == 0)
                {
                    PurgeCache();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void Update()
        {
            var prevSelectedToolDefId = selectedToolDefId;
            bool prevToolSelected = isToolSelected;

            UpdateSelectedTool();

            if(selectedToolDefId != prevSelectedToolDefId)
            {
                lastDefId = default(MyDefinitionId);
            }

            if(showMenu && !canShowMenu)
                showMenu = false;

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
                    bool changedBlock = (selectedDef.Id != lastDefId);

                    if(Settings.showTextInfo && (changedBlock || (aimInfoNeedsUpdate && selectedBlock != null)))
                    {
                        if(changedBlock)
                        {
                            selectedGridSize = MyDefinitionManager.Static.GetCubeSize(selectedDef.CubeSize);
                            selectedOverlayCall = drawLookup.GetValueOrDefault(selectedDef.Id.TypeId, null);

                            BlockDataCache = null;
                            BlockDataCacheValid = true;
                        }

                        lastDefId = selectedDef.Id;

                        if(selectedBlock != null) // text for welder/grinder
                        {
                            aimInfoNeedsUpdate = false;
                            GenerateAimBlockText(selectedDef);
                            PostProcessText(selectedDef.Id, false);
                        }
                        else // text for holding the block
                        {
                            if(TextAPIEnabled ? CachedBuildInfoTextAPI.TryGetValue(selectedDef.Id, out cache) : CachedBuildInfoNotification.TryGetValue(selectedDef.Id, out cache))
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
                {
                    SetFreezePlacement(false);
                }
            }
            else if(prevToolSelected) // just unequipped
            {
                BlockDataCache = null;
                BlockDataCacheValid = true;
                selectedOverlayCall = null;
                showMenu = false;

                if(MyAPIGateway.CubeBuilder.FreezeGizmo)
                {
                    SetFreezePlacement(false);
                }

                HideText();
            }
        }

        private void UpdateSelectedTool()
        {
            if(selectedBlock != null && Tick % 10 == 0) // make the aimed info refresh every 10 ticks
            {
                aimInfoNeedsUpdate = true;
            }

            var controlled = MyAPIGateway.Session.ControlledObject;

            if(prevControlled != controlled)
            {
                isToolSelected = false;
                prevControlled = controlled;
            }

            selectedDef = null;
            selectedBlock = null;

            var controllingShip = controlled as IMyShipController;
            if(controllingShip != null)
            {
                UpdateInShip(controllingShip);
                return;
            }

            var controllingCharacter = controlled as IMyCharacter;
            if(controllingCharacter != null)
            {
                UpdateCharacter(controllingCharacter);
                return;
            }

            selectedHandTool = null;
            isToolSelected = false;
            canShowMenu = false;
            selectedToolDefId = default(MyDefinitionId);
        }

        private void UpdateCharacter(IMyCharacter character)
        {
            var tool = character.EquippedTool;

            if(tool != prevHeldTool)
            {
                prevHeldTool = tool;
                CharacterToolEquipChanged(character, tool);
            }

            if(isToolSelected)
            {
                if(heldCasterComp != null) // equipped welder or grinder
                {
                    selectedBlock = heldCasterComp.HitBlock as IMySlimBlock;
                    selectedDef = (selectedBlock == null ? null : (MyCubeBlockDefinition)selectedBlock.BlockDefinition);
                    willSplitGrid = TriState.None;
                }
                else // equipped block
                {
                    var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

                    if(def != null && MyCubeBuilder.Static.IsActivated)
                    {
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
            }
        }

        private void CharacterToolEquipChanged(IMyCharacter character, IMyEntity tool)
        {
            canShowMenu = false;
            isToolSelected = false;
            selectedHandTool = null;
            selectedToolDefId = default(MyDefinitionId);
            heldCasterComp = null;

            if(tool == null)
                return;

            bool cubePlacer = tool is IMyBlockPlacerBase;

            if(cubePlacer || tool is IMyWelder || tool is IMyAngleGrinder)
            {
                if(!cubePlacer)
                {
                    heldCasterComp = tool.Components.Get<MyCasterComponent>();

                    if(heldCasterComp == null) // if this component is missing the tool might be modded to remove that ability
                        return;
                }

                Sandbox.Game.Gui.MyHud.BlockInfo.BlockBuiltBy = 0; // HACK fix for game's inability to clear this when you equip cubebuilder

                canShowMenu = true; // allow menu use without needing a target
                isToolSelected = true;
                selectedHandTool = (IMyEngineerToolBase)tool;
                selectedToolDefId = selectedHandTool.DefinitionId;
                return;
            }
        }

        private void UpdateInShip(IMyShipController shipController)
        {
            prevHeldTool = null;
            selectedHandTool = null;

            var casterComp = shipController.Components.Get<MyCasterComponent>(); // caster comp is added to ship controller by ship tools when character takes control

            if(shipCasterComp != casterComp)
            {
                shipCasterComp = casterComp;
                ShipToolEquipChanged(shipController, casterComp);
            }

            if(isToolSelected)
            {
                if(drawOverlay > 0)
                {
                    const BlendTypeEnum BLEND_TYPE = BlendTypeEnum.Standard;
                    const float REACH_DISTANCE = GameData.Hardcoded.ShipTool_ReachDistance;
                    var color = new Vector4(2f, 0, 0, 0.1f); // above 1 color creates bloom
                    var m = shipController.WorldMatrix;

                    MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, m.Translation, m.Forward, REACH_DISTANCE, 0.005f, blendType: BLEND_TYPE);
                    MyTransparentGeometry.AddPointBillboard(MATERIAL_VANILLA_DOT, color, m.Translation + m.Forward * REACH_DISTANCE, 0.015f, 0f, blendType: BLEND_TYPE);
                }

                selectedBlock = casterComp.HitBlock as IMySlimBlock;
                selectedDef = (selectedBlock == null ? null : (MyCubeBlockDefinition)selectedBlock.BlockDefinition);
                willSplitGrid = TriState.None;
            }
        }

        private void ShipToolEquipChanged(IMyShipController shipController, MyCasterComponent casterComp)
        {
            canShowMenu = false;
            isToolSelected = false;
            selectedToolDefId = default(MyDefinitionId);

            if(casterComp == null)
                return;

            // HACK fix for SE-7575 - Cockpit's welder/grinder aim only updates when ship moves
            var m = shipController.WorldMatrix;
            casterComp.OnWorldPosChanged(ref m);

            // HACK find a better way to get selected tool type
            var shipControllerObj = shipController.GetObjectBuilderCubeBlock(false) as MyObjectBuilder_ShipController;

            if(shipControllerObj != null && shipControllerObj.Toolbar != null && shipControllerObj.Toolbar.SelectedSlot.HasValue)
            {
                var slotIndex = shipControllerObj.Toolbar.SelectedSlot.Value;

                if(slotIndex >= 0)
                {
                    foreach(var slot in shipControllerObj.Toolbar.Slots)
                    {
                        if(slot.Index != slotIndex)
                            continue;

                        var weapon = slot.Data as MyObjectBuilder_ToolbarItemWeapon;

                        if(weapon != null && (weapon.DefinitionId.TypeId == typeof(MyObjectBuilder_ShipWelder) || weapon.DefinitionId.TypeId == typeof(MyObjectBuilder_ShipGrinder)))
                        {
                            canShowMenu = true; // allow menu use without needing a target
                            isToolSelected = true;
                            selectedToolDefId = weapon.DefinitionId;
                        }

                        break;
                    }
                }
            }
        }

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

        /// <summary>
        /// Called before all updates and is called even when game is paused.
        /// </summary>
        public override void HandleInput()
        {
            try
            {
                if(!IsInitialized || !IsPlayer || MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible || MyParticlesManager.Paused)
                    return; // only monitor input when not in menu or chat, and not paused

                #region Block picker
                if(pickBlockDef == null && selectedBlock != null && Settings.BlockPickerBind.IsJustPressed())
                {
                    pickBlockDef = selectedDef;
                }

                if(pickBlockDef != null && !MyAPIGateway.Input.IsAnyCtrlKeyPressed()) // ignore ctrl to allow toolbar page changing
                {
                    if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.SLOT0))
                    {
                        pickBlockDef = null;
                        MyAPIGateway.Utilities.ShowNotification("Block picking cancelled.", 2000);
                        return;
                    }

                    int slot = 0;

                    for(int i = 1; i < CONTROL_SLOTS.Length; ++i)
                    {
                        var controlId = CONTROL_SLOTS[i];

                        if(MyAPIGateway.Input.IsNewGameControlPressed(controlId))
                        {
                            slot = i;
                            break;
                        }
                    }

                    //if(slot == 0)
                    //{
                    //    for(MyKeys k = MyKeys.D1; k <= MyKeys.D9; k++)
                    //    {
                    //        if(MyAPIGateway.Input.IsKeyPress(k))
                    //        {
                    //            slot = (k - MyKeys.D0);
                    //            break;
                    //        }
                    //    }
                    //}

                    //if(slot == 0)
                    //{
                    //    for(MyKeys k = MyKeys.NumPad1; k <= MyKeys.NumPad9; k++)
                    //    {
                    //        if(MyAPIGateway.Input.IsKeyPress(k))
                    //        {
                    //            slot = (k - MyKeys.NumPad0);
                    //            break;
                    //        }
                    //    }
                    //}

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

                        bool canUseTextAPI = (TextAPI != null && TextAPI.Heartbeat);

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
                                        MyAPIGateway.Utilities.ShowNotification("This only works with a hand or ship tool.", 3000, MyFontEnum.Red);
                                    break;
                                case 2:
                                    if(selectedDef == null)
                                    {
                                        MyAPIGateway.Utilities.ShowNotification("Equip/aim at a block that was added by a mod.", 3000, MyFontEnum.Red);
                                    }
                                    else if(selectedDef.Context.IsBaseGame)
                                    {
                                        MyAPIGateway.Utilities.ShowNotification($"{selectedDef.DisplayNameText} was not added by a mod.", 3000, MyFontEnum.Red);
                                    }
                                    else
                                    {
                                        showMenu = false;
                                        ShowModWorkshop();
                                    }
                                    break;
                                case 3:
                                    showMenu = false;
                                    ShowHelp();
                                    break;
                                case 4:
                                    Settings.showTextInfo = !Settings.showTextInfo;
                                    Settings.Save();

                                    if(buildInfoNotification == null)
                                        buildInfoNotification = MyAPIGateway.Utilities.CreateNotification("");
                                    buildInfoNotification.Text = (Settings.showTextInfo ? "Text info ON + saved to config" : "Text info OFF + saved to config");
                                    buildInfoNotification.Show();
                                    break;
                                case 5:
                                    CycleOverlay(showNotification: false);
                                    break;
                                case 6:
                                    SetPlacementTransparency(!MyCubeBuilder.Static.UseTransparency, showNotification: false);
                                    break;
                                case 7:
                                    SetFreezePlacement(!MyAPIGateway.CubeBuilder.FreezeGizmo, showNotification: false);
                                    break;
                                case 8:
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
                                case 9:
                                    showMenu = false;
                                    ReloadConfig(MOD_NAME);
                                    break;
                            }
                        }
                    }

                    var context = InputLib.GetCurrentInputContext();

                    if(Settings.ToggleTransparencyBind.IsJustPressed(context))
                    {
                        menuNeedsUpdate = true;
                        SetPlacementTransparency(!MyCubeBuilder.Static.UseTransparency);
                    }
                    else if(Settings.CycleOverlaysBind.IsJustPressed(context))
                    {
                        menuNeedsUpdate = true;
                        CycleOverlay();
                    }
                    else if(Settings.FreezePlacementBind.IsJustPressed(context))
                    {
                        menuNeedsUpdate = true;
                        SetFreezePlacement(!MyAPIGateway.CubeBuilder.FreezeGizmo);
                    }
                    else if(Settings.MenuBind.IsJustPressed(context))
                    {
                        menuNeedsUpdate = true;
                        showMenu = !showMenu;
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
            if(!IsInitialized || !IsPlayer)
                return;

            try
            {
                ResetDrawCaches();

                DrawOverlays();

                LeakInfoComp?.Draw();

                TerminalInfoComp?.Draw();

                DrawBlockInfo();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void DrawBlockInfo()
        {
            if(!hudVisible && !Settings.alwaysVisible)
                return;

            if(textShown && textObject != null && MyAPIGateway.Gui.IsCursorVisible)
                HideText();

            if(!Settings.showTextInfo || selectedDef == null || MyAPIGateway.Gui.IsCursorVisible)
                return;

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var posHUD = GetGameHudBlockInfoPos();

            #region Block info addition background
            // draw the added top part's background only for aimed block (which requires textAPI)
            if(selectedBlock != null && !showMenu && textObject != null && useTextAPI)
            {
                var hud = posHUD;

                // make the position top-right
                hud.Y -= (BLOCKINFO_ITEM_HEIGHT * selectedDef.Components.Length) + BLOCKINFO_Y_OFFSET;

                var worldPos = HudToWorld(hud);
                var size = GetGameHudBlockInfoSize(lines * Settings.textAPIScale);
                worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                double cornerSize = Math.Min(0.0015 * ScaleFOV, size.Y); // prevent corner from being larger than the height of the box
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
            #endregion

            #region Lines on top of block info
            bool isGrinder = this.IsGrinder;
            bool foundComputer = false;

            for(int i = selectedDef.Components.Length - 1; i >= 0; --i)
            {
                var comp = selectedDef.Components[i];

                // red functionality line
                if(selectedDef.CriticalGroup == i)
                {
                    var size = new Vector2(BLOCKINFO_COMPONENT_LIST_WIDTH * ScaleFOV, BLOCKINFO_LINE_HEIGHT * ScaleFOV);

                    var hud = posHUD;
                    hud.Y -= BLOCKINFO_ITEM_HEIGHT * i + BLOCKINFO_ITEM_HEIGHT_UNDERLINE + BLOCKINFO_Y_OFFSET_2;

                    var worldPos = HudToWorld(hud);

                    worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                    MyTransparentGeometry.AddBillboardOriented(MATERIAL_SQUARE, BLOCKINFO_LINE_FUNCTIONAL, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                }

                // blue hacking line
                if(!foundComputer && comp.Definition.Id.TypeId == typeof(MyObjectBuilder_Component) && comp.Definition.Id.SubtypeId == COMPUTER) // HACK this is what the game checks internally, hardcoded to computer component.
                {
                    foundComputer = true;

                    var size = new Vector2(BLOCKINFO_COMPONENT_LIST_WIDTH * ScaleFOV, BLOCKINFO_LINE_HEIGHT * ScaleFOV);

                    var hud = posHUD;
                    hud.Y -= BLOCKINFO_ITEM_HEIGHT * i + BLOCKINFO_ITEM_HEIGHT_UNDERLINE + BLOCKINFO_Y_OFFSET_2;

                    var worldPos = HudToWorld(hud);

                    worldPos += camMatrix.Left * size.X + camMatrix.Up * (size.Y * 2); // extra offset to allow for red line to be visible

                    MyTransparentGeometry.AddBillboardOriented(MATERIAL_SQUARE, BLOCKINFO_LINE_OWNERSHIP, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                }

                // yellow highlight if returned component is not the same as grinded component
                if(isGrinder && comp.DeconstructItem != comp.Definition)
                {
                    var size = new Vector2(BLOCKINFO_COMPONENT_LIST_WIDTH * ScaleFOV, BLOCKINFO_COMPONENT_LIST_SELECT_HEIGHT * ScaleFOV);

                    var hud = posHUD;
                    hud.Y -= BLOCKINFO_ITEM_HEIGHT * i + BLOCKINFO_Y_OFFSET_2;

                    var worldPos = HudToWorld(hud);

                    worldPos += camMatrix.Left * size.X + camMatrix.Up * size.Y;

                    MyTransparentGeometry.AddBillboardOriented(MATERIAL_SQUARE, BLOCKINFO_LINE_COMPLOSS, worldPos, camMatrix.Left, camMatrix.Up, size.X, size.Y, Vector2.Zero, BLOCKINFO_BLEND_TYPE);
                }
            }
            #endregion
        }

        public void MessageEntered(string msg, ref bool send)
        {
            try
            {
                if(msg.StartsWith(CMD_MODLINK, CMD_COMPARE_TYPE))
                {
                    send = false;
                    ShowModWorkshop();
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
                    CachedBuildInfoNotification.Clear();
                    CachedBuildInfoTextAPI.Clear();
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
                            var megaWatts = GameData.Hardcoded.LaserAntenna_PowerUsage((MyLaserAntennaDefinition)selectedDef, meters);
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
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        #region Various config/state helpers
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

            var viewportSize = MyAPIGateway.Session.Camera.ViewportSize;
            aspectRatio = (double)viewportSize.X / (double)viewportSize.Y;

            bool newRotationHints = cfg.RotationHints;

            if(rotationHints != newRotationHints)
            {
                rotationHints = newRotationHints;
                HideText();
            }
        }

        private void ReloadConfig(string caller)
        {
            if(Settings.Load())
                ShowChatMessage(caller, "Config loaded.", MyFontEnum.Green);
            else
                ShowChatMessage(caller, "Config created and loaded default settings.", MyFontEnum.Green);

            Settings.Save();

            HideText();
            CachedBuildInfoTextAPI.Clear();

            if(textObject != null)
            {
                textObject.Scale = TextAPIScale;

                if(Settings.alwaysVisible)
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
            var help = string.Format(HELP_FORMAT,
                Settings.MenuBind.GetBinds(),
                Settings.CycleOverlaysBind.GetBinds(),
                Settings.ToggleTransparencyBind.GetBinds(),
                Settings.FreezePlacementBind.GetBinds());

            MyAPIGateway.Utilities.ShowMissionScreen("BuildInfo Mod", "", "Various help topics", help, null, "Close");
        }

        private void ShowModWorkshop()
        {
            if(selectedDef != null)
            {
                if(!selectedDef.Context.IsBaseGame)
                {
                    var id = selectedDef.Context.GetWorkshopID();

                    if(id > 0)
                    {
                        var link = $"https://steamcommunity.com/sharedfiles/filedetails/?id={id}";

                        // 0 in this method opens for the local client, hopefully they don't change that to "ALL" like they did on the chat message...
                        MyVisualScriptLogicProvider.OpenSteamOverlay(link, 0);

                        ShowChatMessage(CMD_MODLINK, $"Opened steam overlay with {link}", MyFontEnum.Green);
                    }
                    else
                        ShowChatMessage(CMD_MODLINK, "Can't find mod workshop ID, probably it's a local mod?", MyFontEnum.Red);
                }
                else
                    ShowChatMessage(CMD_MODLINK, $"{selectedDef.DisplayNameText} is not added by a mod.", MyFontEnum.Red);
            }
            else
                ShowChatMessage(CMD_MODLINK, "No block selected/equipped.", MyFontEnum.Red);
        }

        private void CycleOverlay(bool showNotification = true)
        {
            if(++drawOverlay >= DRAW_OVERLAY_NAME.Length)
                drawOverlay = 0;

            if(showNotification)
            {
                if(overlayNotification == null)
                    overlayNotification = MyAPIGateway.Utilities.CreateNotification("", 2000, MyFontEnum.White);

                overlayNotification.Text = "Overlays: " + DRAW_OVERLAY_NAME[drawOverlay];
                overlayNotification.Show();
            }
        }

        private void SetFreezePlacement(bool value, bool showNotification = true)
        {
            if(freezeGizmoNotification == null)
                freezeGizmoNotification = MyAPIGateway.Utilities.CreateNotification("");

            if(selectedBlock != null) // not cubebuilder
            {
                freezeGizmoNotification.Text = "Equip a block and aim at a grid.";
                freezeGizmoNotification.Font = MyFontEnum.Red;
            }
            else if(value && MyCubeBuilder.Static.DynamicMode) // requires a grid target to turn on
            {
                freezeGizmoNotification.Text = "Aim at a grid.";
                freezeGizmoNotification.Font = MyFontEnum.Red;
            }
            else
            {
                // HACK using this method instead of MyAPIGateway.CubeBuilder.FreezeGizmo's setter because that one ignores the value and sets it to true.
                MyCubeBuilder.Static.FreezeGizmo = value;

                freezeGizmoNotification.Text = (value ? "Freeze placement position ON" : "Freeze placement position OFF");
                freezeGizmoNotification.Font = MyFontEnum.White;

                if(value) // store the frozen position to check distance for auto-unfreeze
                    MyCubeBuilder.Static.GetAddPosition(out lastGizmoPosition);
            }

            if(showNotification)
                freezeGizmoNotification.Show();
        }

        private void SetPlacementTransparency(bool value, bool showNotification = true)
        {
            MyCubeBuilder.Static.UseTransparency = value;

            if(showNotification)
            {
                if(transparencyNotification == null)
                    transparencyNotification = MyAPIGateway.Utilities.CreateNotification("");

                transparencyNotification.Text = (MyCubeBuilder.Static.UseTransparency ? "Placement transparency ON" : "Placement transparency OFF");
                transparencyNotification.Font = MyFontEnum.White;
                transparencyNotification.Show();
            }
        }
        #endregion
    }
}