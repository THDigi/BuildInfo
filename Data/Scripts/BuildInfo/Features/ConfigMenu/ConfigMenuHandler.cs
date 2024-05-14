﻿using System;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using static Draygo.API.HudAPIv2;
using static Draygo.API.HudAPIv2.MenuRootCategory;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    /// <summary>
    /// The mod menu invoked by TextAPI
    /// </summary>
    public class ConfigMenuHandler : ModComponent
    {
        private MenuRootCategory Category_Mod;
        private MenuCategoryBase Category_Textbox;
        private MenuCategoryBase Category_PlaceInfo;
        private MenuCategoryBase Category_AimInfo;
        private MenuCategoryBase Category_Overlays;
        private MenuCategoryBase Category_HUD;
        private MenuCategoryBase Category_Toolbar;
        private MenuCategoryBase Category_GUI;
        private MenuCategoryBase Category_LeakInfo;
        private MenuCategoryBase Category_Binds;
        private MenuCategoryBase Category_Misc;
        private MenuCategoryBase Category_ConfirmReset;

        // groups of items to update on when other settings are changed
        private readonly ItemGroup groupTextInfo = new ItemGroup();
        private readonly ItemGroup groupCustomStyling = new ItemGroup();
        private readonly ItemGroup groupToolbarLabels = new ItemGroup();
        private readonly ItemGroup groupToolbarStatusFont = new ItemGroup();
        private readonly ItemGroup groupEventToolbar = new ItemGroup();
        private readonly ItemGroup groupShipToolInvBar = new ItemGroup();
        private readonly ItemGroup groupWeaponModeIcon = new ItemGroup();
        private readonly ItemGroup groupOverlayLabelsShowWithLookaround = new ItemGroup();
        private readonly ItemGroup groupTerminalDetailInfo = new ItemGroup();
        private readonly ItemGroup groupBinds = new ItemGroup(); // for updating titles on bindings that use other bindings

        private readonly ItemGroup groupAll = new ItemGroup(); // for mass-updating titles

        public static readonly Color HeaderColor = new Color(155, 220, 255);
        public static readonly Color LabelColor = new Color(230, 240, 255);
        public static readonly Color LabelColorDisabled = Color.Gray;
        public static readonly Color ValueColorDefault = new Color(200, 255, 200);
        public static readonly Color ValueColorChanged = new Color(255, 230, 180);
        public static readonly Color DefaultValueTooltipColor = Color.Gray;

        public void RefreshAll()
        {
            bool textModeOn = Main.Config.TextShow.Value != 0;

            groupTextInfo.SetInteractable(textModeOn);
            groupCustomStyling.SetInteractable(textModeOn && Main.Config.TextAPICustomStyling.Value);
            groupToolbarLabels.SetInteractable(Main.Config.ToolbarLabels.Value != (int)ToolbarLabelsMode.Off);
            //groupEventToolbar.SetInteractable(true);
            groupShipToolInvBar.SetInteractable(Main.Config.ShipToolInvBarShow.Value);
            groupWeaponModeIcon.SetInteractable(Main.Config.WeaponModeIndicatorScale.Value > 0f);
            groupOverlayLabelsShowWithLookaround.SetInteractable(Main.Config.OverlayLabels.Value != int.MaxValue);
            groupTerminalDetailInfo.SetInteractable(Main.Config.TerminalDetailInfoAdditions.Value);

            groupBinds.Update();

            groupAll.Update();
        }

        private readonly StringBuilder tmp = new StringBuilder();

        private IMyHudNotification SharedNotify;

        private const int SLIDERS_FORCEDRAWTICKS = 60 * 10;
        private const int TOGGLE_FORCEDRAWTICKS = 60 * 2;
        private const string TEXT_START = "<color=gray>Lorem ipsum dolor sit amet, consectetur adipiscing elit." +
                                        "\nPellentesque ac quam in est feugiat mollis." +
                                        "\nAenean commodo, dolor ac molestie commodo, quam nulla" +
                                        "\n  suscipit est, sit amet consequat neque purus sed dui.";
        private const string TEXT_END = "\n<color=gray>Fusce aliquam eros sit amet varius convallis." +
                                        "\nClass aptent taciti sociosqu ad litora torquent" +
                                        "\n  per conubia nostra, per inceptos himenaeos.";

        public ConfigMenuHandler(BuildInfoMod main) : base(main)
        {
            Main.TextAPI.Detected += TextAPI_Detected;
            Main.RichHud.Initialized += RichHud_Initialized;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Main.TextAPI.Detected -= TextAPI_Detected;
            Main.RichHud.Initialized -= RichHud_Initialized;
            Main.Config.Handler.SettingsLoaded -= Handler_SettingsLoaded;
        }

        public override void UpdateAfterSim(int tick)
        {
            if(!MyAPIGateway.Gui.ChatEntryVisible)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                Main.ChatCommandHandler.CommandHelp.ExecuteNoArgs();
            }
            else
            {
                ShowNotify("[Close chat to see help window]", 16);
            }
        }

        void RichHud_Initialized()
        {
            Log.Info("RichHUD detected, creating menu.");

            RichHudTerminal.Root.Enabled = true;

            RichHudTerminal.Root.Add(new TextPage()
            {
                Name = "Settings?",
                SubHeaderText = string.Empty,
                HeaderText = $"{BuildInfoMod.ModName} Settings",
                Text = new RichText($"This is the RichHUD menu, while {BuildInfoMod.ModName} only supports TextAPI for now.\n\nTo access TextAPI's mod configurator, open chat and press F2 then look top-left for a \"Mod Settings\" button."),
            });
        }

        void TextAPI_Detected()
        {
            Log.Info($"TextAPI detected, creating menu.");

            Category_Mod = new MenuRootCategory(BuildInfoMod.ModName, MenuFlag.PlayerMenu, BuildInfoMod.ModName + " Settings");

            new ItemButton(Category_Mod, $"Legend: {Utils.ColorTag(ValueColorDefault)}(Default) {Utils.ColorTag(ValueColorChanged)}(Changed)", () => { });

            new ItemButton(Category_Mod, "Help Window", () =>
            {
                // HACK: schedule to be shown after chat is closed, due to a soft lock bug with ShowMissionScreen() when chat is opened.
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            });

            new ItemButton(Category_Mod, "World settings & mods", () =>
            {
                Main.ChatCommandHandler.CommandServerInfo.ExecuteNoArgs();
            });

            Category_Textbox = AddCategory("Text Box", Category_Mod, header: "Text box when holding or aiming at a block.");
            Category_Overlays = AddCategory("Block Overlays", Category_Mod, header: "Block Overlays - See binds for how to show overlays.");
            Category_HUD = AddCategory("HUD", Category_Mod, header: "HUD additions and modifications");
            Category_Toolbar = AddCategory("Toolbar", Category_Mod, header: "ToolbarInfo box and other toolbar modifications");
            Category_GUI = AddCategory("Terminal/Inventory/GUI", Category_Mod, header: "GUI additions and modifications");
            Category_LeakInfo = AddCategory("Air Leak Scanner", Category_Mod, header: "Air Leak Scanner - Access from any AirVent block");
            Category_Binds = AddCategory("Binds", Category_Mod, header: "Key/button bindings");
            Category_Misc = AddCategory("Misc", Category_Mod, header: "Various other settings");

            #region TextBox
            SimpleEnumCycle(Category_Textbox, null, Main.Config.TextShow, execOnCycle: (v) =>
            {
                Main.Config.TextShow.SetValue(v);
                bool off = (v == 0);
                bool forceShow = (!off && Main.EquipmentMonitor.BlockDef == null);
                UpdateTextBox(redraw: !off, drawTicks: (forceShow ? TOGGLE_FORCEDRAWTICKS : 0));
                groupTextInfo.SetInteractable(!off);
                groupCustomStyling.SetInteractable(!off ? Main.Config.TextAPICustomStyling.Value : false);
            });
            SimpleToggle(Category_Textbox, null, Main.Config.TextAlwaysVisible, groupTextInfo);
            ItemAdd_TextInfoScale(Category_Textbox, groupTextInfo);
            ItemAdd_BackgroundOpacity(Category_Textbox);
            AddSpacer(Category_Textbox);
            ItemAdd_CustomStyling(Category_Textbox);
            ItemAdd_ScreenPosition(Category_Textbox);
            SimplePositionReset(Category_Textbox, Main.Config.TextAPIScreenPosition, groupCustomStyling, () => UpdateTextBox(save: false, redraw: true));
            ItemAdd_HorizontalAlign(Category_Textbox);
            ItemAdd_VerticalAlign(Category_Textbox);
            AddSpacer(Category_Textbox);
            Category_PlaceInfo = AddCategory("Place Info", Category_Textbox, header: "Holding a block will show these stats:", group: groupTextInfo);
            ItemAdd_PlaceInfoToggles(Category_PlaceInfo);
            Category_AimInfo = AddCategory("Aim Info", Category_Textbox, header: "Aiming at a block will show these stats:", group: groupTextInfo);
            ItemAdd_AimInfoToggles(Category_AimInfo);
            #endregion

            #region Overlays
            SimpleToggle(Category_Overlays, null, Main.Config.OverlaysAlwaysVisible);
            ItemAdd_OverlayLabelToggles(Category_Overlays);
            SimpleToggle(Category_Overlays, null, Main.Config.OverlaysShowLabelsWithBind, groupOverlayLabelsShowWithLookaround);
            SimpleToggle(Category_Overlays, "Overlay Lock - remember mode", Main.Config.OverlayLockRememberMode);
            #endregion

            #region HUD
            SimpleToggle(Category_HUD, null, Main.Config.BlockInfoAdditions);
            SimpleToggle(Category_HUD, null, Main.Config.ScrollableComponentsList);
            SimpleToggle(Category_HUD, null, Main.Config.SelectAllProjectedBlocks);
            SimpleToggle(Category_HUD, null, Main.Config.OverrideToolSelectionDraw);
            SimpleToggle(Category_HUD, null, Main.Config.CubeBuilderDrawSubparts);
            SimpleEnumCycle(Category_HUD, null, Main.Config.CubeBuilderSelectionInfoMode);
            SimpleToggle(Category_HUD, null, Main.Config.UnderCrosshairMessages);
            AddSpacer(Category_HUD);
            SimpleToggle(Category_HUD, null, Main.Config.RelativeDampenerInfo);
            SimpleToggle(Category_HUD, null, Main.Config.SpectatorControlInfo);
            SimpleToggle(Category_HUD, null, Main.Config.TurretHUD);
            SimpleToggle(Category_HUD, null, Main.Config.HudFontOverride);
            SimpleToggle(Category_HUD, null, Main.Config.BackpackBarOverride);
            SimpleToggle(Category_HUD, null, Main.Config.HealthOverride);
            SimpleEnumCycle(Category_HUD, null, Main.Config.MassOverride);
            SimpleToggle(Category_HUD, null, Main.Config.CockpitBuildHideRightHud);
            AddSpacer(Category_HUD);
            SimpleToggle(Category_HUD, null, Main.Config.ShipToolInvBarShow, setGroupInteractable: groupShipToolInvBar);
            ItemAdd_ShipInvBarPosition(Category_HUD, groupShipToolInvBar);
            SimplePositionReset(Category_HUD, Main.Config.ShipToolInvBarPosition, groupShipToolInvBar);
            SimpleDualSlider(Category_HUD, null, Main.Config.ShipToolInvBarScale, groupShipToolInvBar);
            AddSpacer(Category_HUD);
            SimplePercentWithOff(Category_HUD, null, Main.Config.WeaponModeIndicatorScale, onSet: (v) => groupWeaponModeIcon.SetInteractable(v > 0f));
            SimplePositionRedirect(Category_HUD, null, "Weapon mode indicator (bottom-right while in a cockpit) can be simply dragged while in this config menu, and scroll to scale.", Main.Config.WeaponModeIndicatorPosition, groupWeaponModeIcon);
            SimplePositionReset(Category_HUD, Main.Config.WeaponModeIndicatorPosition, groupWeaponModeIcon);
            #endregion

            #region Toolbar
            ItemSlider cockpitEnterTimeSlider = null;
            SimpleEnumCycle(Category_Toolbar, null, Main.Config.ToolbarLabels, setGroupInteractable: groupToolbarLabels, execOnCycle: (v) =>
            {
                ToolbarLabelsMode ve = (ToolbarLabelsMode)v;
                cockpitEnterTimeSlider.Interactable = (ve == ToolbarLabelsMode.ShowOnPress || ve == ToolbarLabelsMode.HudHints);
            });
            cockpitEnterTimeSlider = SimpleSlider(Category_Toolbar, null, Main.Config.ToolbarLabelsEnterCockpitTime, groupToolbarLabels, dialogTitle: "Shown for this many seconds if not always visible.");
            SimpleEnumCycle(Category_Toolbar, null, Main.Config.ToolbarItemNameMode, groupToolbarLabels);
            SimpleToggle(Category_Toolbar, null, Main.Config.ToolbarLabelsHeader, groupToolbarLabels);
            SimpleEnumCycle(Category_Toolbar, null, Main.Config.ToolbarStyleMode, groupToolbarLabels);

            AddSpacer(Category_Toolbar);

            groupToolbarLabels.Add(new ItemButton(Category_Toolbar, "ToolbarInfo preset - Default (centered)",
                () =>
                {
                    Main.Config.ToolbarStyleMode.ResetToDefault();
                    Main.Config.ToolbarLabelsPosition.ResetToDefault();
                    Main.Config.ToolbarLabelsOffsetForInvBar.ResetToDefault();
                    Main.Config.ToolbarLabelsScale.ResetToDefault();
                    Main.Config.ToolbarLabelsHeader.ResetToDefault();
                    Main.Config.Save();
                    ShowNotify($"Set ToolbarInfo box to Default (centered) preset.", 3000);
                    RefreshAll();
                }));

            groupToolbarLabels.Add(new ItemButton(Category_Toolbar, "ToolbarInfo preset - Left Side HUD",
                () =>
                {
                    Main.Config.ToolbarStyleMode.SetValue((int)ToolbarStyle.SingleList);
                    Main.Config.ToolbarLabelsPosition.SetValue(new Vector2D(-0.715, -0.7069));
                    Main.Config.ToolbarLabelsOffsetForInvBar.SetValue(new Vector2D(0, 0));
                    Main.Config.ToolbarLabelsScale.SetValue(0.9f);
                    Main.Config.ToolbarLabelsHeader.SetValue(false);
                    Main.Config.Save();
                    ShowNotify($"Set ToolbarInfo box to Left Side HUD preset.", 3000);
                    RefreshAll();
                }));

            AddSpacer(Category_Toolbar);

            ItemAdd_ToolbarLabelsPos(Category_Toolbar, Main.Config.ToolbarLabelsPosition, groupToolbarLabels);
            SimplePositionReset(Category_Toolbar, Main.Config.ToolbarLabelsPosition, groupToolbarLabels);
            SimpleSlider(Category_Toolbar, null, Main.Config.ToolbarLabelsScale, groupToolbarLabels);
            SimpleDualSlider(Category_Toolbar, null, Main.Config.ToolbarLabelsOffsetForInvBar, groupToolbarLabels, dialogTitle: "Applies if Ship Tool Inventory Bar is visible.");
            SimplePositionRedirect(Category_Toolbar, null, "ToolbarInfo box can be moved in menu by holding LMB on it and dragging.", Main.Config.ToolbarLabelsMenuPosition, groupToolbarLabels);
            SimplePositionReset(Category_Toolbar, Main.Config.ToolbarLabelsMenuPosition, groupToolbarLabels);
            SimpleSlider(Category_Toolbar, null, Main.Config.ToolbarLabelsMenuScale, groupToolbarLabels);

            AddSpacer(Category_Toolbar);

            SimpleToggle(Category_Toolbar, null, Main.Config.EventToolbarInfo, setGroupInteractable: groupEventToolbar);
            SimpleSlider(Category_Toolbar, null, Main.Config.EventToolbarInfoScale, groupEventToolbar);
            SimplePositionRedirect(Category_Toolbar, null, "Event toolbar info box can be moved in menu by holding LMB on it and dragging.", Main.Config.EventToolbarInfoPosition, groupEventToolbar);
            SimplePositionReset(Category_Toolbar, Main.Config.EventToolbarInfoPosition, groupEventToolbar);

            AddSpacer(Category_Toolbar);

            SimpleToggle(Category_Toolbar, null, Main.Config.ToolbarStatusFontOverride, setGroupInteractable: groupToolbarStatusFont);

            SimpleToggle(Category_Toolbar, null, Main.Config.ToolbarActionStatus, groupToolbarStatusFont, callOnSet: (v) =>
            {
                if(!Main.ToolbarStatusProcessor.Enabled)
                {
                    Utils.ShowColoredChatMessage(BuildInfoMod.ModName, $"NOTE: Toolbar action status is forced off, reason: {Main.ToolbarStatusProcessor.DisableReason}", FontsHandler.YellowSh);
                }
            });

            SimpleSlider(Category_Toolbar, null, Main.Config.ToolbarStatusTextScaleOverride);

            SimpleEnumCycle(Category_Toolbar, null, Main.Config.ToolbarActionIcons, execOnCycle: (v) => MyAPIGateway.Utilities.ShowNotification($"NOTE: Toolbar action icons can't be refreshed in real time, you'll need to rejoin world.", 3000, FontsHandler.YellowSh));
            #endregion

            #region Terminal
            SimpleToggle(Category_GUI, null, Main.Config.TerminalDetailInfoAdditions, setGroupInteractable: groupTerminalDetailInfo);
            SimpleToggle(Category_GUI, null, Main.Config.TerminalDetailInfoHeader, groupTerminalDetailInfo);
            SimplePositionRedirect(Category_GUI, null, "Refresh and Copy buttons can always be moved in terminal by holding RMB on either of them.", Main.Config.TerminalButtonsPosition);
            SimplePositionReset(Category_GUI, Main.Config.TerminalButtonsPosition);
            SimpleSlider(Category_GUI, null, Main.Config.TerminalButtonsScale);

            SimplePositionRedirect(Category_GUI, null, "Multi-select info is always movable with RMB on the vertical line.", Main.Config.TerminalMultiDetailedInfoPosition);
            SimplePositionReset(Category_GUI, Main.Config.TerminalMultiDetailedInfoPosition);

            AddSpacer(Category_GUI);
            SimpleToggle(Category_GUI, null, Main.Config.ItemTooltipAdditions);
            SimpleToggle(Category_GUI, null, Main.Config.ItemSymbolAdditions);
            SimpleToggle(Category_GUI, null, Main.Config.BlockIconOverlays);
            #endregion

            #region Leak Info
            SimpleColor(Category_LeakInfo, null, Main.Config.LeakParticleColorWorld);
            SimpleColor(Category_LeakInfo, null, Main.Config.LeakParticleColorOverlay);
            #endregion

            #region Binds
            SimpleBind(Category_Binds, "Menu Bind", Config.Config.MENU_BIND_INPUT_NAME, Main.Config.MenuBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Text Show Bind", Config.Config.TEXT_SHOW_INPUT_NAME, Main.Config.TextShowBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Cycle Overlays Bind", Config.Config.CYCLE_OVERLAYS_INPUT_NAME, Main.Config.CycleOverlaysBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Freeze Placement Bind", Config.Config.FREEZE_PLACEMENT_INPUT_NAME, Main.Config.FreezePlacementBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Toggle Transparency Bind", Config.Config.TOGGLE_TRANSPARENCY_INPUT_NAME, Main.Config.ToggleTransparencyBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Block Picker Bind", Config.Config.BLOCK_PICKER_INPUT_NAME, Main.Config.BlockPickerBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Lock Overlay Bind", Config.Config.LOCK_OVERLAY_INPUT_NAME, Main.Config.LockOverlayBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Show Toolbar Info Bind", Config.Config.SHOW_TOOLBAR_INFO_INPUT_NAME, Main.Config.ShowToolbarInfoBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Show CubeBuilder Selection Info Bind", Config.Config.SHOW_CB_SELECTION_INFO_INPUT_NAME, Main.Config.ShowCubeBuilderSelectionInfoBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Construction Model Preview Bind", Config.Config.CONSTRUCTION_MODEL_PREVIEW_INPUT_NAME, Main.Config.ConstructionModelPreviewBind, groupBinds, groupBinds);
            #endregion

            #region Misc
            SimpleToggle(Category_Misc, null, Main.Config.AdjustBuildDistanceSurvival);
            SimpleToggle(Category_Misc, null, Main.Config.AdjustBuildDistanceShipCreative);
            SimpleToggle(Category_Misc, Main.Config.SpectatorAllowRotationModifier.Name, Main.Config.SpectatorAllowRotationModifier);
            SimpleToggle(Category_Misc, null, Main.Config.InternalInfo);
            SimpleToggle(Category_Misc, null, Main.Config.ScientificNotation);
            SimpleToggle(Category_Misc, "ModderHelp: Show hints/problems with other mods", Main.Config.ModderHelpAlerts);
            AddSpacer(Category_Misc);
            Category_ConfirmReset = AddCategory("Reset to defaults", Category_Misc, header: "Reset, are you sure?");
            new ItemButton(Category_ConfirmReset, "<color=255,200,0>I am sure!", () =>
            {
                Main.Config.Handler.ResetToDefaults();
                Main.Config.Handler.SaveToFile();
                Main.Config.Reload();
                ShowNotify("Config reset to defaults and saved.", 3000, FontsHandler.WhiteSh);
                RefreshAll();
                UpdateTextBox(redraw: false);
            });
            #endregion

            ItemButton button = new ItemButton(Category_Mod, "Mod's workshop page", Main.ChatCommandHandler.CommandWorkshop.ExecuteNoArgs);
            button.Interactable = (Log.WorkshopId > 0);

            // set initial interactable states
            RefreshAll();

            Main.Config.Handler.SettingsLoaded += Handler_SettingsLoaded;
        }

        void Handler_SettingsLoaded()
        {
            RefreshAll();
        }

        private void ItemAdd_BackgroundOpacity(MenuCategoryBase category)
        {
            ItemSlider item = new ItemSlider(category, "Background Opacity", min: BackgroundOpacitySetting.ValueHUD, max: Main.Config.TextAPIBackgroundOpacity.Max, defaultValue: Main.Config.TextAPIBackgroundOpacity.DefaultValue, rounding: 2,
                getter: () => Main.Config.TextAPIBackgroundOpacity.Value,
                setter: (val) =>
                {
                    if(val < 0)
                        val = BackgroundOpacitySetting.ValueHUD;

                    Main.Config.TextAPIBackgroundOpacity.SetValue(val);
                    UpdateTextBox(redraw: false);
                },
                sliding: (val) =>
                {
                    Main.Config.TextAPIBackgroundOpacity.SetValue(val);
                    UpdateTextBox(save: false, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                cancelled: (orig) =>
                {
                    Main.Config.TextAPIBackgroundOpacity.SetValue(orig);
                    UpdateTextBox(save: false);
                },
                format: (v) => (v < 0 ? "HUD" : (v * 100).ToString() + "%"));

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_CustomStyling(MenuCategoryBase category)
        {
            ItemToggle item = new ItemToggle(category, "Custom Styling",
                getter: () => Main.Config.TextAPICustomStyling.Value,
                setter: (v) =>
                {
                    Main.Config.TextAPICustomStyling.SetValue(v);
                    groupCustomStyling.SetInteractable(v);
                    UpdateTextBox(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                defaultValue: Main.Config.TextAPICustomStyling.DefaultValue);

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_ScreenPosition(MenuCategoryBase category)
        {
            ItemBoxMove item = new ItemBoxMove(category, "Screen Position", min: Main.Config.TextAPIScreenPosition.Min, max: Main.Config.TextAPIScreenPosition.Max, defaultValue: Main.Config.TextAPIScreenPosition.DefaultValue, rounding: 4,
                getter: () => Main.Config.TextAPIScreenPosition.Value,
                setter: (pos) =>
                {
                    Main.Config.TextAPIScreenPosition.SetValue(pos);
                    UpdateTextBox(redraw: true);
                },
                selected: (pos) =>
                {
                    Main.Config.TextAPIScreenPosition.SetValue(pos);
                    UpdateTextBox(save: false, redraw: true, moveHint: true, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                moving: (pos) =>
                {
                    Main.Config.TextAPIScreenPosition.SetValue(pos);
                    UpdateTextBox(save: false, redraw: true, moveHint: true, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                cancelled: (origPos) =>
                {
                    Main.Config.TextAPIScreenPosition.SetValue(origPos);
                    UpdateTextBox(save: false, redraw: false);
                });

            groupCustomStyling.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_TextInfoScale(MenuCategoryBase category, ItemGroup group = null)
        {
            FloatSetting setting = Main.Config.TextAPIScale;
            ItemSlider item = new ItemSlider(category, GetLabelFromSetting(null, setting), min: setting.Min, max: setting.Max, defaultValue: setting.DefaultValue, rounding: 2,
                getter: () => setting.Value,
                setter: (val) =>
                {
                    setting.SetValue(val);
                    UpdateTextBox(redraw: false);
                },
                sliding: (val) =>
                {
                    setting.SetValue(val);
                    UpdateTextBox(save: false, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                cancelled: (orig) =>
                {
                    setting.SetValue(orig);
                    UpdateTextBox(save: false);
                });

            group?.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_HorizontalAlign(MenuCategoryBase category)
        {
            ItemToggle item = new ItemToggle(category, "Horizontal Anchor",
                getter: () => Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Right),
                setter: (v) =>
                {
                    bool set = !Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Right);
                    Main.Config.TextAPIAlign.Set(TextAlignFlags.Right, set);
                    Main.Config.TextAPIAlign.Set(TextAlignFlags.Left, !set);
                    UpdateTextBox(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                defaultValue: (Main.Config.TextAPIAlign.DefaultValue & (int)TextAlignFlags.Right) != 0,
                onText: "Right",
                offText: "Left");

            groupCustomStyling.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_VerticalAlign(MenuCategoryBase category)
        {
            ItemToggle item = new ItemToggle(category, "Vertical Anchor",
                getter: () => Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom),
                setter: (v) =>
                {
                    bool set = !Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom);
                    Main.Config.TextAPIAlign.Set(TextAlignFlags.Bottom, set);
                    Main.Config.TextAPIAlign.Set(TextAlignFlags.Top, !set);
                    UpdateTextBox(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                defaultValue: (Main.Config.TextAPIAlign.DefaultValue & (int)TextAlignFlags.Bottom) != 0,
                onText: "Bottom",
                offText: "Top");

            groupCustomStyling.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_PlaceInfoToggles(MenuCategoryBase category)
        {
            ItemFlags<PlaceInfoFlags> item = new ItemFlags<PlaceInfoFlags>(category, "Toggle All", Main.Config.PlaceInfo,
                onValueSet: (flag, set) => UpdateTextBox(redraw: false)
            );

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_AimInfoToggles(MenuCategoryBase category)
        {
            ItemFlags<AimInfoFlags> item = new ItemFlags<AimInfoFlags>(category, "Toggle All", Main.Config.AimInfo,
                onValueSet: (flag, set) => UpdateTextBox(redraw: false)
            );

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_OverlayLabelToggles(MenuCategoryBase category)
        {
            ItemFlags<OverlayLabelsFlags> item = new ItemFlags<OverlayLabelsFlags>(category, "Toggle All Labels", Main.Config.OverlayLabels,
                onValueSet: (flag, set) =>
                {
                    groupOverlayLabelsShowWithLookaround.SetInteractable(Main.Config.OverlayLabels.Value != int.MaxValue);
                    UpdateTextBox(redraw: false);
                }
            );

            groupAll.Add(item);
        }

        private void ItemAdd_ShipInvBarPosition(MenuCategoryBase category, ItemGroup group = null)
        {
            Vector2DSetting setting = Main.Config.ShipToolInvBarPosition;

            ItemBoxMove item = new ItemBoxMove(category, GetLabelFromSetting(null, setting), min: setting.Min, max: setting.Max, defaultValue: setting.DefaultValue, rounding: 4,
                getter: () => setting.Value,
                selected: (pos) =>
                {
                    if(Main.ShipToolInventoryBar.Shown)
                    {
                        setting.SetValue(pos);
                    }
                    else
                    {
                        ShowNotify("First get in a cockpit and select a ship grinder or drill.", 5000, FontsHandler.RedSh);
                    }
                },
                moving: (pos) =>
                {
                    if(Main.ShipToolInventoryBar.Shown)
                    {
                        setting.SetValue(pos);
                    }
                    else
                    {
                        ShowNotify("First get in a cockpit and select a ship grinder or drill.", 16, FontsHandler.RedSh);
                    }
                },
                cancelled: (origPos) =>
                {
                    if(Main.ShipToolInventoryBar.Shown)
                    {
                        setting.SetValue(origPos);
                        ShowNotify("Cancelled changes", 3000);
                    }
                    else
                    {
                        ShowNotify(null);
                    }
                },
                setter: (pos) =>
                {
                    if(Main.ShipToolInventoryBar.Shown)
                    {
                        setting.SetValue(pos);
                        Main.Config.Save();
                        ShowNotify("Saved to config", 3000);
                    }
                    else
                    {
                        ShowNotify(null);
                    }
                });

            group?.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_ToolbarLabelsPos(MenuCategoryBase category, Vector2DSetting setting, ItemGroup group = null)
        {
            ItemBoxMove item = new ItemBoxMove(category, GetLabelFromSetting(null, setting), min: setting.Min, max: setting.Max, defaultValue: setting.DefaultValue, rounding: 4,
                getter: () => setting.Value,
                selected: (pos) =>
                {
                    if(Main.ToolbarLabelRender.MustBeVisible)
                    {
                        setting.SetValue(pos);
                    }
                    else
                    {
                        ShowNotify("First get in a cockpit and have something in the toolbar.", 5000, FontsHandler.RedSh);
                    }
                },
                moving: (pos) =>
                {
                    if(Main.ToolbarLabelRender.MustBeVisible)
                    {
                        setting.SetValue(pos);
                    }
                    else
                    {
                        ShowNotify("First get in a cockpit and have something in the toolbar.", 5000, FontsHandler.RedSh);
                    }
                },
                cancelled: (origPos) =>
                {
                    if(Main.ToolbarLabelRender.MustBeVisible)
                    {
                        setting.SetValue(origPos);
                        ShowNotify("Cancelled changes", 3000);
                    }
                    else
                    {
                        ShowNotify(null);
                    }
                },
                setter: (pos) =>
                {
                    if(Main.ToolbarLabelRender.MustBeVisible)
                    {
                        setting.SetValue(pos);
                        Main.Config.Save();
                        ShowNotify("Saved to config", 3000);
                    }
                    else
                    {
                        ShowNotify(null);
                    }
                });

            group?.Add(item);
            groupAll.Add(item);
        }

        #region Helper methods
        private void ShowNotify(string message, int timeoutMs = 1000, string font = FontsHandler.WhiteSh)
        {
            if(SharedNotify == null)
                SharedNotify = MyAPIGateway.Utilities.CreateNotification(string.Empty, 1000, FontsHandler.WhiteSh);

            SharedNotify.Hide();

            if(message != null)
            {
                SharedNotify.AliveTime = timeoutMs;
                SharedNotify.Font = font;
                SharedNotify.Text = message;
                SharedNotify.Show();
            }
        }

        private MenuCategoryBase AddCategory(string name, MenuCategoryBase parent, string header = null, ItemGroup group = null)
        {
            ItemSubMenu item = new ItemSubMenu(parent, name, header);
            group?.Add(item);
            return item.Item;
        }

        private void AddSpacer(MenuCategoryBase category, string label = "————————————————————")
        {
            new MenuItem($"<color=155,155,155>{label}", category);
        }

        private void SimplePositionReset(MenuCategoryBase category, Vector2DSetting setting, ItemGroup group = null, Action call = null)
        {
            ItemButton item = new ItemButton(category, GetLabelFromSetting(null, setting) + " Reset", () =>
            {
                setting.ResetToDefault();
                Main.Config.Save();
                ShowNotify($"Reset [{setting.Name}] to default.", 3000);
                RefreshAll();
                call?.Invoke();
            });

            group?.Add(item);
            groupAll.Add(item);
        }

        private void SimpleColor(MenuCategoryBase category, string label, ColorSetting setting, bool useAlpha = false, ItemGroup group = null)
        {
            ItemColor item = new ItemColor(category, GetLabelFromSetting(label, setting), setting,
                apply: () => UpdateTextBox(redraw: false),
                preview: () => UpdateTextBox(save: false, redraw: false),
                useAlpha: useAlpha);

            group?.Add(item);
            groupAll.Add(item);
        }

        private void SimpleToggle(MenuCategoryBase category, string label, BoolSetting setting, ItemGroup group = null, ItemGroup setGroupInteractable = null, Action<bool> callOnSet = null)
        {
            ItemToggle item = new ItemToggle(category, GetLabelFromSetting(label, setting),
                getter: () => setting.Value,
                setter: (v) =>
                {
                    setting.SetValue(v);
                    UpdateTextBox(redraw: false);
                    setGroupInteractable?.SetInteractable(setting.Value);
                    callOnSet?.Invoke(v);
                },
                defaultValue: setting.DefaultValue);

            group?.Add(item);
            groupAll.Add(item);
        }

        /// <summary>
        /// <paramref name="label"/> can be null but <paramref name="denialText"/> must be defined!
        /// </summary>
        private void SimplePositionRedirect(MenuCategoryBase category, string label, string denialText, Vector2DSetting setting, ItemGroup group = null)
        {
            ItemPositionRedirect item = new ItemPositionRedirect(category, GetLabelFromSetting(label, setting), denialText, setting.DefaultValue, rounding: 4, getter: () => setting.Value);

            group?.Add(item);
            groupAll.Add(item);
        }

        private void SimpleDualSlider(MenuCategoryBase category, string label, Vector2DSetting setting, ItemGroup group = null, string dialogTitle = null)
        {
            ItemSlider itemForX = new ItemSlider(category, GetLabelFromSetting(label, setting) + " X", min: (float)setting.Min.X, max: (float)setting.Max.X, defaultValue: (float)setting.DefaultValue.X, rounding: 2,
                getter: () => (float)setting.Value.X,
                setter: (val) =>
                {
                    setting.SetValue(new Vector2D(val, setting.Value.Y));
                    Main.Config.Save();
                },
                sliding: (val) =>
                {
                    setting.SetValue(new Vector2D(val, setting.Value.Y));
                },
                cancelled: (orig) =>
                {
                    setting.SetValue(new Vector2D(orig, setting.Value.Y));
                },
                dialogTitle: dialogTitle);

            ItemSlider itemForY = new ItemSlider(category, GetLabelFromSetting(label, setting) + " Y", min: (float)setting.Min.Y, max: (float)setting.Max.Y, defaultValue: (float)setting.DefaultValue.Y, rounding: 2,
                getter: () => (float)setting.Value.Y,
                setter: (val) =>
                {
                    setting.SetValue(new Vector2D(setting.Value.X, val));
                    Main.Config.Save();
                },
                sliding: (val) =>
                {
                    setting.SetValue(new Vector2D(setting.Value.X, val));
                },
                cancelled: (orig) =>
                {
                    setting.SetValue(new Vector2D(setting.Value.X, orig));
                },
                dialogTitle: dialogTitle);

            group?.Add(itemForX);
            group?.Add(itemForY);
            groupAll.Add(itemForX);
            groupAll.Add(itemForY);
        }

        private ItemSlider SimpleSlider(MenuCategoryBase category, string label, FloatSetting setting, ItemGroup group = null, string dialogTitle = null)
        {
            ItemSlider item = new ItemSlider(category, GetLabelFromSetting(label, setting), min: setting.Min, max: setting.Max, defaultValue: setting.DefaultValue, rounding: 2,
                getter: () => setting.Value,
                sliding: (val) =>
                {
                    setting.SetValue(val);
                },
                cancelled: (orig) =>
                {
                    setting.SetValue(orig);
                    ShowNotify("Cancelled changes", 3000);
                },
                setter: (val) =>
                {
                    setting.SetValue(val);
                    Main.Config.Save();
                    ShowNotify("Saved to config", 3000);
                },
                dialogTitle: dialogTitle);

            group?.Add(item);
            groupAll.Add(item);
            return item;
        }

        private ItemSlider SimplePercentWithOff(MenuCategoryBase category, string label, FloatSetting setting, float smallerThanForOff = 0f, float offValue = -0.1f, string offText = "OFF", string dialogTitle = null, ItemGroup group = null, Action<float> onSet = null)
        {
            ItemSlider item = new ItemSlider(category, GetLabelFromSetting(label, setting), min: offValue, max: setting.Max, defaultValue: setting.DefaultValue, rounding: 2,
                getter: () => setting.Value,
                sliding: (val) =>
                {
                    setting.SetValue(val);
                },
                cancelled: (orig) =>
                {
                    setting.SetValue(orig);
                    ShowNotify("Cancelled changes", 3000);
                },
                setter: (val) =>
                {
                    if(val < smallerThanForOff)
                        val = offValue;

                    setting.SetValue(val);
                    Main.Config.Save();
                    ShowNotify("Saved to config", 3000);
                    onSet?.Invoke(val);
                },
                format: (v) => (v < smallerThanForOff ? offText : (v * 100).ToString() + "%"),
                dialogTitle: dialogTitle);

            group?.Add(item);
            groupAll.Add(item);
            return item;
        }

        private void SimpleBind(MenuCategoryBase category, string label, string inputName, InputCombinationSetting setting, ItemGroup group = null, ItemGroup updateGroupOnSet = null)
        {
            ItemInput item = new ItemInput(category, GetLabelFromSetting(label, setting), inputName,
                getter: () => setting.Value,
                setter: (combination) =>
                {
                    setting.SetValue(combination);
                    UpdateTextBox(redraw: false);
                    updateGroupOnSet?.Update();
                },
                defaultValue: setting.DefaultValue);

            group?.Add(item);
            groupAll.Add(item);
        }

        //private void SimpleScreenPosition(MenuCategoryBase category, string label, Vector2DSetting setting, ItemGroup group = null)
        //{
        //    var item = new ItemBoxMove(category, GetLabelFromSetting(label, setting), min: setting.Min, max: setting.Max, defaultValue: setting.DefaultValue, rounding: 4,
        //        getter: () => setting.Value,
        //        selected: (pos) =>
        //        {
        //            setting.Value = pos;
        //        },
        //        moving: (pos) =>
        //        {
        //            setting.Value = pos;
        //        },
        //        cancelled: (origPos) =>
        //        {
        //            setting.Value = origPos;
        //            ShowNotify("Cancelled changes", 3000);
        //        },
        //        setter: (pos) =>
        //        {
        //            setting.Value = pos;
        //            Main.Config.Save();
        //            ShowNotify("Saved to config", 3000);
        //        });

        //    group?.Add(item);
        //    groupAll.Add(item);
        //}

        private void SimpleEnumCycle<TEnum>(MenuCategoryBase category, string label, EnumSetting<TEnum> setting, ItemGroup group = null, ItemGroup setGroupInteractable = null, int offValue = 0, Action<int> execOnCycle = null) where TEnum : struct
        {
            ItemEnumCycle item = new ItemEnumCycle(category, GetLabelFromSetting(label, setting),
                getter: () => setting.Value,
                setter: (v) =>
                {
                    setting.SetValue(v);
                    setGroupInteractable?.SetInteractable(v != offValue);
                    execOnCycle?.Invoke(v);
                    Main.Config.Save();
                },
                enumType: typeof(TEnum),
                defaultValue: setting.DefaultValue);

            group?.Add(item);
            groupAll.Add(item);
        }

        static string GetLabelFromSetting<T>(string label, SettingBase<T> setting)
        {
            if(label == null)
            {
                label = setting.Name;
                int separator = label.IndexOf(':');
                if(separator != -1)
                {
                    separator += 2; // go after the : and include the space too
                    if(separator < label.Length)
                        label = label.Substring(separator, label.Length - separator);
                }
            }
            return label;
        }

        private void UpdateTextBox(bool save = true, bool redraw = true, bool moveHint = false, int drawTicks = 0)
        {
            if(save)
                Main.Config.Save();

            tmp.Clear();
            tmp.Append(TEXT_START);

            if(moveHint)
            {
                tmp.NewLine();
                tmp.NewLine().Append("<color=0,255,0>Click and drag anywhere to move!");
                tmp.NewLine().Append("<color=255,255,0>Current position: ").Append(Main.Config.TextAPIScreenPosition.Value.X.ToString("0.000")).Append(", ").Append(Main.Config.TextAPIScreenPosition.Value.Y.ToString("0.000"));
                tmp.NewLine().Append("<color=100,100,55>Default: ").Append(Main.Config.TextAPIScreenPosition.DefaultValue.X.ToString("0.000")).Append(", ").Append(Main.Config.TextAPIScreenPosition.DefaultValue.Y.ToString("0.000"));
                tmp.NewLine();
            }

            tmp.Append(TEXT_END);

            Main.TextGeneration.Refresh(redraw: redraw, write: tmp, forceDrawTicks: drawTicks);
        }
        #endregion Helper methods
    }
}
