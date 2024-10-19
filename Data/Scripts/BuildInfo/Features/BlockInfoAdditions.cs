﻿using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features
{
    public class BlockInfoAdditions : ModComponent
    {
        readonly Vector4 LineFunctionalColor = new Color(115, 69, 80).ToVector4(); // from MyGuiScreenHudSpace.RecreateControls()
        readonly Vector4 LineOwnershipColor = new Color(56, 67, 147).ToVector4();

        // from MyGuiControlBlockGroupInfo.CreateBlockInfoControl()
        readonly Vector4 ScrollbarColor = new Vector4(118f / 255f, 166f / 255f, 64f / 85f, 1f);
        readonly Vector4 ScrollbarBgColor = new Vector4(142f / (339f * MathHelper.Pi), 46f / 255f, 52f / 255f, 1f);

        const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        const float HudComponentHeight = 0.037f; // on HUD space
        const float HudComponentWidth = 0.011f;
        const float HudComponentUnderlineOffset = 0.012f;

        const float WorldBlockInfoLineHeight = 0.0001f; // on world space
        const float WorldComponentHighlightHeight = 0.0014f;
        const float WorldComponentHeight = 0.00185f;
        const float WorldBuildInfoMargin = 0.0008f;

        const float WorldScrollbarWidth = 0.0005f;
        const float WorldScrollbarInnerMargin = (WorldScrollbarWidth / 4f);

        MyCubeBlockDefinition CurrentHudBlockDef;
        int? OwnershipComponentIndex = null;
        int ComponentReplaceInfoCount = 0;
        readonly List<ComponentInfo> ComponentReplaceInfo = new List<ComponentInfo>(2);
        readonly Dictionary<MyDefinitionId, int> CompsInInv = new Dictionary<MyDefinitionId, int>(MyDefinitionId.Comparer);

        List<MyInventory> MonitorInventories = new List<MyInventory>(2);

        class ComponentInfo
        {
            public int Index;
            public bool ReturnsDifferent;
            public MyPhysicalItemDefinition ReturnsItem;
            public HudAPIv2.SpaceMessage Text;
        }

        public BlockInfoAdditions(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.GameBlockInfoHandler.RegisterHudChangedEvent(HudInfoChanged, 100);
            Main.Config.BlockInfoAdditions.ValueAssigned += ConfigValueSet;
            Main.BlockInfoScrollComponents.ScrollUpdate += BlockInfoScrollUpdate;
            Main.EquipmentMonitor.BlockChanged += BlockChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.Config.BlockInfoAdditions.ValueAssigned -= ConfigValueSet;
            Main.BlockInfoScrollComponents.ScrollUpdate -= BlockInfoScrollUpdate;
            Main.EquipmentMonitor.BlockChanged -= BlockChanged;
        }

        void ConfigValueSet(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            Main.GameBlockInfoHandler.ForceResetBlockInfo();
        }

        void BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            if(def == null)
            {
                StopUpdates();
            }
        }

        void StopUpdates()
        {
            CurrentHudBlockDef = null;

            if(MonitorInventories.Count > 0)
            {
                foreach(MyInventory inv in MonitorInventories)
                {
                    inv.InventoryContentChanged -= InventoryContentChanged;
                }
                MonitorInventories.Clear();
            }

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
        }

        void HudInfoChanged(MyHudBlockInfo hud)
        {
            CurrentHudBlockDef = null;

            MyCubeBlockDefinition blockDef;
            if(!Main.Config.BlockInfoAdditions.Value || !MyDefinitionManager.Static.TryGetCubeBlockDefinition(hud.DefinitionId, out blockDef))
            {
                StopUpdates();
                return;
            }

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
            CurrentHudBlockDef = blockDef;
            BData_Base data = Main.LiveDataHandler.Get<BData_Base>(blockDef);

            #region compute ownership component index and grind loss marks
            ComponentReplaceInfoCount = 0;
            OwnershipComponentIndex = null;

            if(data != null && (data.Has & BlockHas.OwnershipDetector) != 0)
            {
                // HACK: fixing ownership line to be representitive
                if(blockDef.OwnershipIntegrityRatio > 0)
                    OwnershipComponentIndex = blockDef.CriticalGroup; // has computer, means it can be hacked, but at critical integrity not at computer position
                else
                    OwnershipComponentIndex = -1; // no computer, no hacking, see early exit in MyCubeBlock.OnIntegrityChanged()
            }

            foreach(ComponentInfo info in ComponentReplaceInfo)
            {
                info.Index = -1;
                info.ReturnsItem = null;
                info.ReturnsDifferent = false;
            }

            for(int i = 0; i < blockDef.Components.Length; ++i)
            {
                MyCubeBlockDefinition.Component comp = blockDef.Components[i];

                if(!OwnershipComponentIndex.HasValue && comp.Definition.Id == Hardcoded.ComputerComponentId)
                {
                    OwnershipComponentIndex = i;
                }

                bool returnsDifferent = (comp.DeconstructItem == null || comp.DeconstructItem != comp.Definition);
                if(Main.ModDetector.DetectedAwwScrap || returnsDifferent)
                {
                    ComponentInfo info = null;
                    if(ComponentReplaceInfoCount >= ComponentReplaceInfo.Count)
                    {
                        info = new ComponentInfo();
                        ComponentReplaceInfo.Add(info);
                    }
                    else
                    {
                        info = ComponentReplaceInfo[ComponentReplaceInfoCount];
                    }

                    info.Index = i;
                    info.ReturnsItem = comp.DeconstructItem;
                    info.ReturnsDifferent = returnsDifferent;

                    ComponentReplaceInfoCount++;
                }
            }
            #endregion

            #region set vanilla critical & ownership lines
            hud.CriticalComponentIndex = blockDef.CriticalGroup;
            hud.CriticalIntegrity = blockDef.CriticalIntegrityRatio;
            hud.OwnershipIntegrity = blockDef.OwnershipIntegrityRatio;

            // HACK: fixing ownership line to be representitive
            if(data != null && (data.Has & BlockHas.OwnershipDetector) != 0)
            {
                if(blockDef.OwnershipIntegrityRatio > 0)
                    hud.OwnershipIntegrity = blockDef.CriticalIntegrityRatio; // has computer, means it can be hacked, but at critical integrity not at computer position
                else
                    hud.OwnershipIntegrity = 0; // no computer, no hacking, see early exit in MyCubeBlock.OnIntegrityChanged()
            }
            #endregion

            if(MyCubeBuilder.Static.IsActivated)
            {
                // TODO: find a nicer way of showing this:
                #region inform player if the block has the other size available
                //MyCubeBlockDefinitionGroup pairDef = MyDefinitionManager.Static.TryGetDefinitionGroup(blockDef.BlockPairName);
                //if(pairDef != null && pairDef.Large != null && pairDef.Small != null)
                //{
                //    const int NameSpacePadding = 24; // make name wider so that font gets smaller

                //    IMyControl control = MyAPIGateway.Input.GetGameControl(MyControlsSpace.CUBE_BUILDER_CUBESIZE_MODE);
                //    if(control != null)
                //    {
                //        string bind = control.GetControlButtonName(MyGuiInputDeviceEnum.Mouse);

                //        if(string.IsNullOrEmpty(bind))
                //            bind = control.GetControlButtonName(MyGuiInputDeviceEnum.Keyboard);

                //        if(string.IsNullOrEmpty(bind))
                //            bind = control.GetControlButtonName(MyGuiInputDeviceEnum.KeyboardSecond);

                //        if(string.IsNullOrEmpty(bind))
                //            MyHud.BlockInfo.BlockName = $"{blockDef.DisplayNameText,-NameSpacePadding}\n(can swap size)";
                //        else
                //            MyHud.BlockInfo.BlockName = $"{blockDef.DisplayNameText,-NameSpacePadding}\n({bind} to swap size)";
                //    }
                //    else
                //    {
                //        MyHud.BlockInfo.BlockName = $"{blockDef.DisplayNameText,-NameSpacePadding}\n(can swap size)";
                //    }
                //}
                #endregion

                try
                {
                    UpdateHudInventoryComps();
                }
                catch(Exception e)
                {
                    Log.Error(e);
                }
            }

            Main.GameBlockInfoHandler.RedrawBlockInfo();
        }

        void InventoryContentChanged(MyInventoryBase inventory, MyPhysicalInventoryItem item, MyFixedPoint amount)
        {
            try
            {
                UpdateHudInventoryComps();
                Main.GameBlockInfoHandler.RedrawBlockInfo();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void BlockInfoScrollUpdate(ListReader<MyHudBlockInfo.ComponentInfo> originalComponents)
        {
            if(MonitorInventories.Count <= 0 || Utils.CreativeToolsEnabled)
                return;

            MyHudBlockInfo hud = MyHud.BlockInfo;
            hud.MissingComponentIndex = -1;

            // scrolled at the bottom
            if(Main.BlockInfoScrollComponents.Index == 0)
            {
                MyHudBlockInfo.ComponentInfo firstComp = originalComponents[0];

                int amount = 0;
                foreach(MyInventory inv in MonitorInventories)
                {
                    amount += (int)inv.GetItemAmount(firstComp.DefinitionId);
                    if(amount > 0)
                        break;
                }

                if(amount <= 0)
                    hud.MissingComponentIndex = 0;
            }
        }

        void UpdateHudInventoryComps()
        {
            int debugStage = 0;
            try
            {
                MyHudBlockInfo hud = MyHud.BlockInfo;
                MyCubeBlockDefinition blockDef;
                if(!Main.Config.BlockInfoAdditions.Value || !MyDefinitionManager.Static.TryGetCubeBlockDefinition(hud.DefinitionId, out blockDef))
                {
                    StopUpdates();
                    return;
                }

                if(!MyCubeBuilder.Static.IsActivated)
                    return;

                bool isCreative = Utils.CreativeToolsEnabled;

                hud.BlockIntegrity = (isCreative ? blockDef.MaxIntegrityRatio : 0.0000001f);

                #region Inventory find and monitor
                foreach(MyInventory inv in MonitorInventories)
                {
                    inv.InventoryContentChanged -= InventoryContentChanged;
                }

                MonitorInventories.Clear();

                debugStage = 1;

                MyCockpit cockpit = MyAPIGateway.Session.ControlledObject as MyCockpit;
                if(cockpit != null)
                {
                    IMyCharacter pilot = cockpit.Pilot;
                    if(pilot.HasInventory)
                        MonitorInventories.Add((MyInventory)pilot.GetInventory());

                    if(cockpit.HasInventory)
                    {
                        for(int i = 0; i < cockpit.InventoryCount; i++)
                        {
                            MonitorInventories.Add(cockpit.GetInventory(i));
                        }

                        // TODO: cockpit build mode uses conveyor-connected blocks too... use?
                        //ListReader<MyCubeBlock> fatBlocks = cockpit.CubeGrid.GetFatBlocks();
                        //if(fatBlocks.Count <= 500)
                        //{
                        //    VRage.Game.ModAPI.Ingame.MyItemType standardBuildingItem = VRage.Game.ModAPI.Ingame.MyItemType.MakeComponent("SteelPlate"); // TODO: maybe find any large-conveyor requiring item instead?
                        //    IMyInventory cockpitInventory = cockpit.GetInventory(0);
                        //
                        //    foreach(MyCubeBlock block in fatBlocks)
                        //    {
                        //        if(block == cockpit)
                        //            continue;
                        //
                        //        if(!block.HasInventory)
                        //            continue;
                        //
                        //        IMyInventory firstInv = block.GetInventory(0);
                        //        if(!firstInv.CanTransferItemTo(cockpitInventory, standardBuildingItem))
                        //            continue;
                        //
                        //        for(int i = 0; i < block.InventoryCount; i++)
                        //        {
                        //            MyInventory inv = block.GetInventory(i);
                        //            MonitorInventories.Add(inv);
                        //        }
                        //    }
                        //}
                    }
                }
                else
                {
                    IMyCharacter chr = MyAPIGateway.Session.ControlledObject as IMyCharacter;
                    if(chr != null && chr.HasInventory)
                    {
                        MonitorInventories.Add((MyInventory)chr.GetInventory());
                    }
                }

                debugStage = 2;

                foreach(MyInventory inv in MonitorInventories)
                {
                    inv.InventoryContentChanged += InventoryContentChanged;
                }
                #endregion

                if(isCreative)
                {
                    debugStage = 3;

                    for(int i = 0; i < hud.Components.Count; i++)
                    {
                        MyHudBlockInfo.ComponentInfo comp = hud.Components[i];
                        SetCompStockpile(ref comp, comp.TotalCount, grayedOut: false);
                        hud.Components[i] = comp;
                    }
                }
                else if(MonitorInventories.Count > 0)
                {
                    debugStage = 4;

                    CompsInInv.Clear();

                    for(int i = 0; i < hud.Components.Count; i++)
                    {
                        MyHudBlockInfo.ComponentInfo comp = hud.Components[i];

                        if(!CompsInInv.ContainsKey(comp.DefinitionId))
                        {
                            int amount = 0;
                            foreach(MyInventory inv in MonitorInventories)
                            {
                                amount += (int)inv.GetItemAmount(comp.DefinitionId);
                            }
                            CompsInInv[comp.DefinitionId] = amount;
                        }
                    }

                    debugStage = 5;

                    const bool OnlyFirstCompRedHighlight = true;

                    // mark first component red if you don't have it to indicate you can't place it
                    if(OnlyFirstCompRedHighlight)
                    {
                        if(hud.Components.Count > 0 && CompsInInv.GetValueOrDefault(hud.Components[0].DefinitionId, 0) <= 0)
                        {
                            hud.MissingComponentIndex = 0;
                        }
                    }
                    else
                    {
                        hud.MissingComponentIndex = -1;
                    }

                    debugStage = 6;

                    for(int i = 0; i < hud.Components.Count; i++)
                    {
                        MyHudBlockInfo.ComponentInfo comp = hud.Components[i];

                        int inInv = Math.Max(0, CompsInInv[comp.DefinitionId]);
                        if(inInv > 0)
                            CompsInInv[comp.DefinitionId] = inInv - comp.TotalCount; // remove for next comp stack of same type

                        bool hasThisComp = (inInv >= comp.TotalCount);

                        if(!OnlyFirstCompRedHighlight)
                        {
                            // mark first unavailable component as red unless it's past the red line, then don't care
                            if(hud.MissingComponentIndex < 0 && blockDef.CriticalGroup >= i && inInv < comp.TotalCount)
                            {
                                hud.MissingComponentIndex = i;
                            }
                        }

                        int available = Math.Min(inInv, comp.TotalCount); // show up to what block needs
                        SetCompStockpile(ref comp, available, grayedOut: !hasThisComp);

                        hud.Components[i] = comp;
                    }
                }
                else // survival and no inventories found
                {
                    debugStage = 7;

                    for(int i = 0; i < hud.Components.Count; i++)
                    {
                        MyHudBlockInfo.ComponentInfo comp = hud.Components[i];
                        SetCompStockpile(ref comp, 0, grayedOut: true);
                        hud.Components[i] = comp;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error($"{GetType().Name} at part #{debugStage}");
                Log.Error(e, null);
            }
        }

        static void SetCompStockpile(ref MyHudBlockInfo.ComponentInfo comp, int stockpile, bool grayedOut)
        {
            // HACK: hardcoded based on how math is handled in MyGuiControlBlockInfo.Draw(float transitionAlpha, float backgroundTransitionAlpha)
            // goal is to show custom number for stockpile while also controlling if font is white or gray

            // this affects if text is white or grayed out: if(c.MountedCount == c.TotalCount)
            comp.MountedCount = (grayedOut ? 0 : comp.TotalCount);

            // this gets printed: InstalledCount => MountedCount + StockpileCount;
            comp.StockpileCount = stockpile - comp.MountedCount;
        }

        public override void UpdateDraw()
        {
            MyHudBlockInfo hud = MyHud.BlockInfo;
            if(CurrentHudBlockDef == null || Main.GameConfig.HudState == HudState.OFF || MyAPIGateway.Gui.IsCursorVisible || hud == null)
                return;

            List<MyHudBlockInfo.ComponentInfo> hudComps = hud.Components;
            if(hudComps == null || hudComps.Count == 0) // don't show block info additions if the block info isn't visible
                return;

            //if(Main.Config.BlockInfoAdditions.Value)
            {
                MyCubeBlockDefinition blockDef = CurrentHudBlockDef;
                MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;

                #region Compute BlockInfo HUD position
                Vector2 compListBottomRight = new Vector2(0.9894f, 0.678f); // more or less from MyGuiScreenHudSpace.PrepareDraw()

                if(Main.GameConfig.HudState == HudState.BASIC)
                    compListBottomRight.Y = 0.558f;

                // FIXME: vanilla UI is all over the place with this, needs tweaking
                if(Main.GameConfig.AspectRatio > 5) // triple monitor
                    compListBottomRight.X += 0.75f;
                #endregion

                int totalComps = blockDef.Components.Length;

                int maxVisibleComps = Main.BlockInfoScrollComponents.MaxVisible;
                int maxVisibleIdx = (maxVisibleComps - 1);
                int scrollIdx = Main.BlockInfoScrollComponents.Index;
                int scrollIdxOffset = Main.BlockInfoScrollComponents.IndexOffset;
                int maxScrollIdx = scrollIdx + maxVisibleComps;

                float scaleFOV = Main.DrawUtils.ScaleFOV;

                const bool debugMode = false;

                #region for debugging
                if(debugMode)
                {
                    if(!MyParticlesManager.Paused)
                        MyAPIGateway.Utilities.ShowNotification($"maxVisibleComps={maxVisibleComps}; total={totalComps}; scrollIdxOffset={scrollIdxOffset}; scrollIdx={scrollIdx}; maxScrollIdx={maxScrollIdx}", 16, FontsHandler.BI_SEOutlined);

                    if(MyAPIGateway.Input.IsKeyPress(VRage.Input.MyKeys.F))
                    {
                        for(int i = totalComps - 1; i >= 0; --i)
                        {
                            Vector2 sizeWorld = new Vector2(HudComponentWidth * scaleFOV, WorldComponentHighlightHeight * scaleFOV);

                            int num = (totalComps - i - scrollIdxOffset - 1);

                            Vector2 posHud = compListBottomRight;
                            posHud.Y += HudComponentHeight * num;
                            Vector3D posWorld = Main.DrawUtils.HUDtoWorld(posHud);

                            DrawDotAt(posWorld, new Color(255, 0, 255));

                            posWorld = Main.DrawUtils.HUDtoWorld(posHud);
                            posWorld += camMatrix.Left * sizeWorld.X + camMatrix.Up * sizeWorld.Y; // move to center of sprite?

                            Color color = (num < 0 ? Color.Red : (num > maxVisibleIdx ? Color.Blue : Color.HotPink));

                            MyTransparentGeometry.AddBillboardOriented(Constants.MatUI_Square, color * (0.25f + ((i / (float)totalComps) / 2)), posWorld, camMatrix.Left, camMatrix.Up, sizeWorld.X, sizeWorld.Y, Vector2.Zero, BlendType);
                        }
                    }
                }
                #endregion

                Vector2 lineSizeWorld = new Vector2(HudComponentWidth * scaleFOV, WorldBlockInfoLineHeight * scaleFOV);

                int criticalCompIndex = blockDef.CriticalGroup;

                if(debugMode)
                    if(!MyAPIGateway.Input.IsKeyPress(MyKeys.Q))
                        criticalCompIndex = (int)Dev.GetValueScroll("set criticalCompIndex", 0, MyKeys.Control);

                #region red functionality line
                if(criticalCompIndex >= (scrollIdx - 1) && criticalCompIndex <= maxScrollIdx)// within scroll limits
                {
                    int redLineVisualIdx = criticalCompIndex + scrollIdxOffset;
                    if(redLineVisualIdx >= 0 && redLineVisualIdx < totalComps) // within total limits
                    {
                        Vector2 posHud = compListBottomRight;
                        posHud.Y += HudComponentHeight * (totalComps - redLineVisualIdx - 2) + HudComponentUnderlineOffset;

                        Vector3D posWorld = Main.DrawUtils.HUDtoWorld(posHud);

                        posWorld += camMatrix.Left * lineSizeWorld.X + camMatrix.Up * lineSizeWorld.Y;

                        MyTransparentGeometry.AddBillboardOriented(Constants.MatUI_Square, LineFunctionalColor, posWorld, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, lineSizeWorld.X, lineSizeWorld.Y, Vector2.Zero, BlendType);
                    }

                    if(debugMode)
                        if(!MyParticlesManager.Paused)
                            MyAPIGateway.Utilities.ShowNotification($"redLineVisualIdx={redLineVisualIdx}", 16, FontsHandler.BI_SEOutlined);
                }
                #endregion

                #region blue hacking line
                if(OwnershipComponentIndex.HasValue && OwnershipComponentIndex.Value >= (scrollIdx - 1) && OwnershipComponentIndex.Value <= maxScrollIdx) // within scroll limits
                {
                    int blueLineVisualIdx = OwnershipComponentIndex.Value + scrollIdxOffset;
                    if(blueLineVisualIdx >= 0 && blueLineVisualIdx < totalComps) // within total limits
                    {
                        Vector2 posHud = compListBottomRight;
                        posHud.Y += HudComponentHeight * (totalComps - blueLineVisualIdx - 2) + HudComponentUnderlineOffset;

                        Vector3D posWOrld = Main.DrawUtils.HUDtoWorld(posHud);

                        posWOrld += camMatrix.Left * lineSizeWorld.X + camMatrix.Up * (lineSizeWorld.Y * 3); // extra offset to allow for red line to be visible

                        MyTransparentGeometry.AddBillboardOriented(Constants.MatUI_Square, LineOwnershipColor, posWOrld, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, lineSizeWorld.X, lineSizeWorld.Y, Vector2.Zero, BlendType);
                    }

                    if(debugMode)
                        if(!MyParticlesManager.Paused)
                            MyAPIGateway.Utilities.ShowNotification($"blueLineVisualIdx={blueLineVisualIdx}", 16, FontsHandler.BI_SEOutlined);
                }
                #endregion

                #region scrollbar for scrollable components feature
                if(Main.Config.ScrollableComponentsList.Value && totalComps > maxVisibleComps)
                {
                    float scrollbarHeightRatio = 1f;
                    float scrollPos = 0f;

                    if(totalComps > maxVisibleComps)
                    {
                        scrollbarHeightRatio = ((float)maxVisibleComps / (float)totalComps);
                        scrollPos = (float)scrollIdxOffset / (float)(totalComps - maxVisibleComps);
                    }

                    Vector2 posHud = compListBottomRight;
                    posHud.Y -= HudComponentHeight; // make it actually top-right

                    #region background coordinates
                    int visibleComps = Math.Min(maxVisibleComps, totalComps);
                    Vector2 bgSizeWorld = new Vector2(WorldScrollbarWidth * scaleFOV, visibleComps * WorldComponentHeight * scaleFOV);

                    Vector3D bgPosWorld = Main.DrawUtils.HUDtoWorld(posHud);
                    bgPosWorld += camMatrix.Right * bgSizeWorld.X * 2 + camMatrix.Down * bgSizeWorld.Y;
                    #endregion

                    #region bar coordinates
                    float emptySpaceHeight = (bgSizeWorld.Y * (1 - scrollbarHeightRatio));
                    Vector2 barSizeWorld = new Vector2(bgSizeWorld.X, bgSizeWorld.Y * scrollbarHeightRatio);

                    Vector3D barPosWorld = Main.DrawUtils.HUDtoWorld(posHud);
                    barPosWorld += camMatrix.Right * barSizeWorld.X * 2 + camMatrix.Down * (barSizeWorld.Y + (emptySpaceHeight * 2 * scrollPos));
                    #endregion

                    // make scrollbar smaller than the background
                    barSizeWorld.X -= WorldScrollbarInnerMargin;
                    barSizeWorld.Y -= WorldScrollbarInnerMargin;

                    // shift entire scrollbar down until it lines up with the bottom, as it looks too high because of the bottom-aligned texts on components
                    bgPosWorld += camMatrix.Down * WorldBuildInfoMargin;
                    barPosWorld += camMatrix.Down * WorldBuildInfoMargin;

                    MyTransparentGeometry.AddBillboardOriented(Constants.MatUI_Square, ScrollbarBgColor, bgPosWorld, camMatrix.Left, camMatrix.Up, bgSizeWorld.X, bgSizeWorld.Y, Vector2.Zero, BlendType);
                    MyTransparentGeometry.AddBillboardOriented(Constants.MatUI_Square, ScrollbarColor, barPosWorld, camMatrix.Left, camMatrix.Up, barSizeWorld.X, barSizeWorld.Y, Vector2.Zero, BlendType);
                }
                #endregion

                #region different return item on grind
                for(int i = (ComponentReplaceInfoCount - 1); i >= 0; --i)
                {
                    ComponentInfo info = ComponentReplaceInfo[i];
                    int index = info.Index + scrollIdxOffset;

                    if(info.Index >= maxScrollIdx // hide for components scrolled above
                    || info.Index < scrollIdx // hide for components scrolled below
                    || info.Index == maxVisibleIdx && Main.BlockInfoScrollComponents.ShowUpHint // hide for top hint component
                    || info.Index == (totalComps - maxVisibleComps) && Main.BlockInfoScrollComponents.ShowDownHint) // hide for bottom hint component
                    {
                        //if(info.Text != null)
                        //    info.Text.Visible = false;

                        continue;
                    }

                    Vector2 posHud = compListBottomRight;
                    posHud.Y += HudComponentHeight * (totalComps - index - 1);

                    Vector3D posWorld = Main.DrawUtils.HUDtoWorld(posHud);

                    Color color;
                    if(info.ReturnsItem == null) // grinds to nothing, vanilla doesn't really support this but doesn't hurt to have the case
                        color = Color.Red;
                    else if(info.ReturnsDifferent)
                        color = (Main.ModDetector.DetectedAwwScrap ? Color.Gray : Color.Yellow);
                    else // returns same as grinded, only shows up if aww scrap is detected
                        color = Color.Lime;

                    if(Main.TextAPI.IsEnabled)
                    {
                        const double LeftOffset = 0.0183;
                        const string TextFont = FontsHandler.BI_SEOutlined;
                        const int NameMaxChars = 24;
                        double textScale = 0.0012 * scaleFOV;

                        posWorld += camMatrix.Left * (LeftOffset * scaleFOV);

                        if(info.Text == null)
                        {
                            info.Text = new HudAPIv2.SpaceMessage(new StringBuilder(32 + NameMaxChars), posWorld, camMatrix.Up, camMatrix.Left, textScale, null, 2, HudAPIv2.TextOrientation.ltr, BlendType, TextFont);
                            info.Text.Visible = false; // not required when using manual .Draw()
                        }
                        else
                        {
                            info.Text.WorldPosition = posWorld;
                            info.Text.Left = camMatrix.Left;
                            info.Text.Up = camMatrix.Up;
                            info.Text.Scale = textScale;
                        }

                        StringBuilder sb = info.Text.Message.Clear();

                        if(info.ReturnsItem == null)
                        {
                            sb.Color(color).Append("Grinds to nothing!");
                        }
                        else if(info.ReturnsDifferent)
                        {
                            sb.Color(color).Append("Grinds to: ").AppendMaxLength(info.ReturnsItem.DisplayNameText, NameMaxChars);
                        }
                        else // returns same, only shows up if aww scrap mod is detected
                        {
                            sb.Color(color).Append("Grinds to same component");
                        }

                        //info.Text.Visible = true;
                        info.Text.Draw();
                    }
                    else
                    {
                        Vector2 sizeWorld = new Vector2(HudComponentWidth * scaleFOV, WorldComponentHighlightHeight * scaleFOV);

                        posWorld += camMatrix.Left * sizeWorld.X + camMatrix.Up * sizeWorld.Y;

                        MyTransparentGeometry.AddBillboardOriented(Constants.MatUI_Square, color * 0.75f, posWorld, (Vector3)camMatrix.Left, (Vector3)camMatrix.Up, sizeWorld.X, sizeWorld.Y, Vector2.Zero, BlendType);
                    }
                }
                #endregion
            }
        }

        void DrawDotAt(Vector3D posWorld, Color color)
        {
            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            float dotSize = 0.0001f;
            MyTransparentGeometry.AddBillboardOriented(Constants.MatUI_Square, color * 0.75f, posWorld, camMatrix.Left, camMatrix.Up, dotSize, dotSize, Vector2.Zero, BlendType);
        }
    }
}
