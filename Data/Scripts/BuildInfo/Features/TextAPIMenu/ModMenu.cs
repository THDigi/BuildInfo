using System;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;
using static Draygo.API.HudAPIv2;
using static Draygo.API.HudAPIv2.MenuRootCategory;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    /// <summary>
    /// The mod menu invoked by TextAPI
    /// </summary>
    public class ModMenu : ModComponent
    {
        private MenuRootCategory Category_Mod;
        private MenuCategoryBase Category_Textbox;
        private MenuCategoryBase Category_PlaceInfo;
        private MenuCategoryBase Category_AimInfo;
        private MenuCategoryBase Category_Overlays;
        private MenuCategoryBase Category_HUD;
        private MenuCategoryBase Category_Toolbar;
        private MenuCategoryBase Category_Terminal;
        private MenuCategoryBase Category_LeakInfo;
        private MenuCategoryBase Category_Binds;
        private MenuCategoryBase Category_Misc;
        private MenuCategoryBase Category_ConfirmReset;

        // groups of items to update on when other settings are changed
        private readonly ItemGroup groupTextInfo = new ItemGroup();
        private readonly ItemGroup groupCustomStyling = new ItemGroup();
        private readonly ItemGroup groupBinds = new ItemGroup();
        private readonly ItemGroup groupLabelsToggle = new ItemGroup();
        private readonly ItemGroup groupLabels = new ItemGroup();
        private readonly ItemGroup groupAimInfoToggle = new ItemGroup();
        private readonly ItemGroup groupAimInfo = new ItemGroup();
        private readonly ItemGroup groupPlaceInfoToggle = new ItemGroup();
        private readonly ItemGroup groupPlaceInfo = new ItemGroup();
        private readonly ItemGroup groupToolbarLabels = new ItemGroup();
        private readonly ItemGroup groupShipToolInvBar = new ItemGroup();
        private readonly ItemGroup groupOverlayLabelsAlt = new ItemGroup();

        private readonly ItemGroup groupAll = new ItemGroup(); // for mass-updating titles

        private readonly StringBuilder tmp = new StringBuilder();

        private const int SLIDERS_FORCEDRAWTICKS = 60 * 10;
        private const int TOGGLE_FORCEDRAWTICKS = 60 * 2;
        private const string TEXT_START = "<color=gray>Lorem ipsum dolor sit amet, consectetur adipiscing elit." +
                                        "\nPellentesque ac quam in est feugiat mollis." +
                                        "\nAenean commodo, dolor ac molestie commodo, quam nulla" +
                                        "\n  suscipit est, sit amet consequat neque purus sed dui.";
        private const string TEXT_END = "\n<color=gray>Fusce aliquam eros sit amet varius convallis." +
                                        "\nClass aptent taciti sociosqu ad litora torquent" +
                                        "\n  per conubia nostra, per inceptos himenaeos.";

        public ModMenu(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            TextAPI.Detected += TextAPI_Detected;
        }

        protected override void UnregisterComponent()
        {
            TextAPI.Detected -= TextAPI_Detected;
            Config.Handler.SettingsLoaded -= Handler_SettingsLoaded;
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(!MyAPIGateway.Gui.ChatEntryVisible)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                Main.ChatCommandHandler.CommandHelp.ExecuteNoArgs();
            }
            else
            {
                MyAPIGateway.Utilities.ShowNotification("[Close chat to see help window]", 16);
            }
        }

        private void TextAPI_Detected()
        {
            Category_Mod = new MenuRootCategory(BuildInfoMod.MOD_NAME, MenuFlag.PlayerMenu, BuildInfoMod.MOD_NAME + " Settings");

            new ItemButton(Category_Mod, "Help Window", () =>
            {
                // HACK: schedule to be shown after chat is closed, due to a soft lock bug with ShowMissionScreen() when chat is opened.
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            });

            Category_Textbox = AddCategory("Text box", Category_Mod);
            Category_Overlays = AddCategory("Overlays", Category_Mod);
            Category_HUD = AddCategory("HUD", Category_Mod);
            Category_Toolbar = AddCategory("Toolbar", Category_Mod);
            Category_Terminal = AddCategory("Terminal", Category_Mod);
            Category_LeakInfo = AddCategory("Air Leak Info", Category_Mod);
            Category_Binds = AddCategory("Binds", Category_Mod);
            Category_Misc = AddCategory("Misc", Category_Mod);

            ItemAdd_TextShow(Category_Textbox);
            SimpleToggle(Category_Textbox, "Show when HUD is off", Config.TextAlwaysVisible, groupTextInfo);
            SimpleSlider(Category_Textbox, "Text Scale", Config.TextAPIScale, groupTextInfo);
            ItemAdd_BackgroundOpacity(Category_Textbox);
            ItemAdd_CustomStyling(Category_Textbox);
            ItemAdd_ScreenPosition(Category_Textbox);
            ItemAdd_HorizontalAlign(Category_Textbox);
            ItemAdd_VerticalAlign(Category_Textbox);

            Category_PlaceInfo = AddCategory("Place Info", Category_Textbox, group: groupTextInfo);
            ItemAdd_PlaceInfoToggles(Category_PlaceInfo);

            Category_AimInfo = AddCategory("Aim Info", Category_Textbox, group: groupTextInfo);
            ItemAdd_AimInfoToggles(Category_AimInfo);

            SimpleToggle(Category_Overlays, "Show when HUD is off", Config.OverlaysAlwaysVisible);
            ItemAdd_OverlayLabelToggles(Category_Overlays);
            SimpleToggle(Category_Overlays, "Labels shown with ALT", Config.OverlaysLabelsAlt, groupOverlayLabelsAlt);

            SimpleToggle(Category_HUD, "Block Info Additions", Config.BlockInfoAdditions);
            SimpleToggle(Category_HUD, "Ship Tool Inventory Bar", Config.ShipToolInvBarShow, setGroupInteractable: groupShipToolInvBar);
            SimpleScreenPosition(Category_HUD, "Ship Tool Inventory Bar Position", Config.ShipToolInvBarPosition, groupShipToolInvBar);
            SimpleDualSlider(Category_HUD, "Ship Tool Inventory Bar Scale", Config.ShipToolInvBarScale, groupShipToolInvBar);
            SimpleToggle(Category_HUD, "Backpack Bar Override", Config.BackpackBarOverride);
            SimpleToggle(Category_HUD, "Turret HUD", Config.TurretHUD);
            SimpleToggle(Category_HUD, "HUD Stat Overrides", Config.HudStatOverrides);
            SimpleToggle(Category_HUD, "Relative Dampener Info", Config.RelativeDampenerInfo);
            SimpleToggle(Category_HUD, "Item Tooltip Additions", Config.ItemTooltipAdditions);

            SimpleEnumCycle(Category_Toolbar, "Labels Mode", typeof(ToolbarLabelsMode), Config.ToolbarLabels, setGroupInteractable: groupToolbarLabels);
            SimpleEnumCycle(Category_Toolbar, "Toolbar Item Names Mode", typeof(ToolbarNameMode), Config.ToolbarItemNameMode, groupToolbarLabels);
            SimpleToggle(Category_Toolbar, "Labels Show Title", Config.ToolbarLabelsShowTitle, groupToolbarLabels);
            SimpleEnumCycle(Category_Toolbar, "Label Box Style", typeof(ToolbarStyle), Config.ToolbarStyleMode, groupToolbarLabels);
            SimpleScreenPosition(Category_Toolbar, "Labels Box HUD Position", Config.ToolbarLabelsPosition, groupToolbarLabels);
            SimpleScreenPosition(Category_Toolbar, "Labels Box In-Menu Position", Config.ToolbarLabelsInMenuPosition, groupToolbarLabels);
            SimpleSlider(Category_Toolbar, "Labels Box Scale", Config.ToolbarLabelsScale, groupToolbarLabels);
            SimpleDualSlider(Category_Toolbar, "Labels Box ShipToolInvBar Offset", Config.ToolbarLabelsOffsetForInvBar, groupToolbarLabels);
            SimpleToggle(Category_Toolbar, "Override Action Status", Config.ToolbarActionStatus);

            SimpleToggle(Category_Terminal, "Detail Info Additions", Config.TerminalDetailInfoAdditions);

            SimpleColor(Category_LeakInfo, "Particle Color World", Config.LeakParticleColorWorld);
            SimpleColor(Category_LeakInfo, "Particle Color Overlay", Config.LeakParticleColorOverlay);

            SimpleBind(Category_Binds, "Menu Bind", Features.Config.Config.MENU_BIND_INPUT_NAME, Config.MenuBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Cycle Overlays Bind", Features.Config.Config.CYCLE_OVERLAYS_INPUT_NAME, Config.CycleOverlaysBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Freeze Placement Bind", Features.Config.Config.FREEZE_PLACEMENT_INPUT_NAME, Config.FreezePlacementBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Toggle Transparency Bind", Features.Config.Config.TOGGLE_TRANSPARENCY_INPUT_NAME, Config.ToggleTransparencyBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Block Picker Bind", Features.Config.Config.BLOCK_PICKER_INPUT_NAME, Config.BlockPickerBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Lock Overlay Bind", Features.Config.Config.LOCK_OVERLAY_INPUT_NAME, Config.LockOverlayBind, groupBinds, groupBinds);

            SimpleToggle(Category_Misc, "Placement Distance in Survival", Config.AdjustBuildDistanceSurvival);
            SimpleToggle(Category_Misc, "Placement Distance in Ship Creative", Config.AdjustBuildDistanceShipCreative);
            SimpleToggle(Category_Misc, "Internal Info", Config.InternalInfo);
            AddSpacer(Category_Misc);
            Category_ConfirmReset = AddCategory("Reset to defaults", Category_Misc, header: "Are you sure?");
            new ItemButton(Category_ConfirmReset, "I am sure!", () =>
            {
                Config.Handler.ResetToDefaults();
                Config.Handler.SaveToFile();
                Config.Reload();
                MyAPIGateway.Utilities.ShowNotification("Config reset to defaults and saved.", 3000, FontsHandler.RedSh);
            });
            // FIXME: resetting to defaults (probably happens with delete config+reload too) doesn't update interactible properly, might need redesign...


            var button = new ItemButton(Category_Mod, "Mod's workshop page", Main.ChatCommandHandler.CommandWorkshop.ExecuteNoArgs);
            button.Interactable = (Log.WorkshopId > 0);

            // gray out items that need to start like that
            groupTextInfo.SetInteractable(Config.TextShow.Value);
            groupCustomStyling.SetInteractable(Config.TextShow.Value && Config.TextAPICustomStyling.Value);
            groupToolbarLabels.SetInteractable(Config.ToolbarLabels.Value != (int)ToolbarLabelsMode.Off);

            Config.Handler.SettingsLoaded += Handler_SettingsLoaded;
        }

        void Handler_SettingsLoaded()
        {
            groupAll.Update();
        }

        private void ItemAdd_TextShow(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Show",
                getter: () => Config.TextShow.Value,
                setter: (v) =>
                {
                    Config.TextShow.Value = v;
                    ApplySettings(redraw: v, drawTicks: (v ? TOGGLE_FORCEDRAWTICKS : 0));
                    groupTextInfo.SetInteractable(v);
                    groupCustomStyling.SetInteractable(v ? Config.TextAPICustomStyling.Value : false);
                },
                defaultValue: Config.TextShow.DefaultValue);

            groupAll.Add(item);
        }

        private void ItemAdd_BackgroundOpacity(MenuCategoryBase category)
        {
            var item = new ItemSlider(category, "Background Opacity", min: -0.1f, max: Config.TextAPIBackgroundOpacity.Max, defaultValue: Config.TextAPIBackgroundOpacity.DefaultValue, rounding: 2,
                getter: () => Config.TextAPIBackgroundOpacity.Value,
                setter: (val) =>
                {
                    if(val < 0)
                        val = -0.1f;

                    Config.TextAPIBackgroundOpacity.Value = val;
                    ApplySettings(redraw: false);
                },
                sliding: (val) =>
                {
                    Config.TextAPIBackgroundOpacity.Value = val;
                    ApplySettings(save: false, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                cancelled: (orig) =>
                {
                    Config.TextAPIBackgroundOpacity.Value = orig;
                    ApplySettings(save: false);
                },
                format: (v) => (v < 0 ? "HUD" : (v * 100).ToString() + "%"));

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_CustomStyling(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Custom Styling",
                getter: () => Config.TextAPICustomStyling.Value,
                setter: (v) =>
                {
                    Config.TextAPICustomStyling.Value = v;
                    groupCustomStyling.SetInteractable(v);
                    ApplySettings(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                defaultValue: Config.TextAPICustomStyling.DefaultValue);

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_ScreenPosition(MenuCategoryBase category)
        {
            var item = new ItemBoxMove(category, "Screen Position", min: Config.TextAPIScreenPosition.Min, max: Config.TextAPIScreenPosition.Max, defaultValue: Config.TextAPIScreenPosition.DefaultValue, rounding: 3,
                getter: () => Config.TextAPIScreenPosition.Value,
                setter: (pos) =>
                {
                    Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(redraw: false);
                },
                selected: (pos) =>
                {
                    Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(save: false, redraw: true, moveHint: true, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                moving: (pos) =>
                {
                    Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(save: false, redraw: true, moveHint: true, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                cancelled: (origPos) =>
                {
                    Config.TextAPIScreenPosition.Value = origPos;
                    ApplySettings(save: false, redraw: false);
                });

            groupCustomStyling.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_HorizontalAlign(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Horizontal Anchor",
                getter: () => Config.TextAPIAlign.IsSet(TextAlignFlags.Right),
                setter: (v) =>
                {
                    var set = !Config.TextAPIAlign.IsSet(TextAlignFlags.Right);
                    Config.TextAPIAlign.Set(TextAlignFlags.Right, set);
                    Config.TextAPIAlign.Set(TextAlignFlags.Left, !set);
                    ApplySettings(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                defaultValue: (Config.TextAPIAlign.DefaultValue & (int)TextAlignFlags.Right) != 0,
                onText: "Right",
                offText: "Left");

            item.ColorOn = item.ColorOff = new Color(255, 255, 0);

            groupCustomStyling.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_VerticalAlign(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Vertical Anchor",
                getter: () => Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom),
                setter: (v) =>
                {
                    var set = !Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom);
                    Config.TextAPIAlign.Set(TextAlignFlags.Bottom, set);
                    Config.TextAPIAlign.Set(TextAlignFlags.Top, !set);
                    ApplySettings(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                defaultValue: (Config.TextAPIAlign.DefaultValue & (int)TextAlignFlags.Bottom) != 0,
                onText: "Bottom",
                offText: "Top");

            item.ColorOn = item.ColorOff = new Color(255, 255, 0);

            groupCustomStyling.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_PlaceInfoToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<PlaceInfoFlags>(category, "Toggle All", Config.PlaceInfo,
                onValueSet: (flag, set) => ApplySettings(redraw: false)
            );

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_AimInfoToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<AimInfoFlags>(category, "Toggle All", Config.AimInfo,
                onValueSet: (flag, set) => ApplySettings(redraw: false)
            );

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_OverlayLabelToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<OverlayLabelsFlags>(category, "Toggle All Labels", Config.OverlayLabels,
                onValueSet: (flag, set) => ApplySettings(redraw: false)
            );

            groupAll.Add(item);
        }

        #region Helper methods
        private MenuCategoryBase AddCategory(string name, MenuCategoryBase parent, string header = null, ItemGroup group = null)
        {
            var item = new ItemSubMenu(parent, name, header);
            group?.Add(item);
            return item.Item;
        }

        private void AddSpacer(MenuCategoryBase category, string label = null)
        {
            new MenuItem($"<color=0,55,0>{(label == null ? new string('=', 10) : $"=== {label} ===")}", category);
        }

        private void SimpleColor(MenuCategoryBase category, string title, ColorSetting setting, bool useAlpha = false, ItemGroup group = null)
        {
            var item = new ItemColor(category, title, setting,
                apply: () => ApplySettings(redraw: false),
                preview: () => ApplySettings(save: false, redraw: false),
                useAlpha: useAlpha);

            group?.Add(item);
            groupAll.Add(item);
        }

        private void SimpleToggle(MenuCategoryBase category, string title, BoolSetting setting, ItemGroup group = null, ItemGroup setGroupInteractable = null)
        {
            var item = new ItemToggle(category, title,
                getter: () => setting.Value,
                setter: (v) =>
                {
                    setting.Value = v;
                    ApplySettings(redraw: false);
                    setGroupInteractable?.SetInteractable(setting.Value);
                },
                defaultValue: setting.DefaultValue);

            group?.Add(item);
            groupAll.Add(item);
        }

        private void SimpleDualSlider(MenuCategoryBase category, string title, Vector2DSetting setting, ItemGroup group = null)
        {
            var itemForX = new ItemSlider(category, title + " X", min: (float)setting.Min.X, max: (float)setting.Max.X, defaultValue: (float)setting.DefaultValue.X, rounding: 2,
                getter: () => (float)setting.Value.X,
                setter: (val) =>
                {
                    setting.Value = new Vector2D(val, setting.Value.Y);
                    Config.Save();
                },
                sliding: (val) =>
                {
                    setting.Value = new Vector2D(val, setting.Value.Y);
                },
                cancelled: (orig) =>
                {
                    setting.Value = new Vector2D(orig, setting.Value.Y);
                });

            var itemForY = new ItemSlider(category, title + " Y", min: (float)setting.Min.Y, max: (float)setting.Max.Y, defaultValue: (float)setting.DefaultValue.Y, rounding: 2,
                getter: () => (float)setting.Value.Y,
                setter: (val) =>
                {
                    setting.Value = new Vector2D(setting.Value.X, val);
                    Config.Save();
                },
                sliding: (val) =>
                {
                    setting.Value = new Vector2D(setting.Value.X, val);
                },
                cancelled: (orig) =>
                {
                    setting.Value = new Vector2D(setting.Value.X, orig);
                });

            group?.Add(itemForX);
            group?.Add(itemForY);
            groupAll.Add(itemForX);
            groupAll.Add(itemForY);
        }

        private void SimpleSlider(MenuCategoryBase category, string title, FloatSetting setting, ItemGroup group = null)
        {
            var item = new ItemSlider(category, title, min: setting.Min, max: setting.Max, defaultValue: setting.DefaultValue, rounding: 2,
                getter: () => setting.Value,
                setter: (val) =>
                {
                    setting.Value = val;
                    ApplySettings(redraw: false);
                },
                sliding: (val) =>
                {
                    setting.Value = val;
                    ApplySettings(save: false, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                cancelled: (orig) =>
                {
                    setting.Value = orig;
                    ApplySettings(save: false);
                });

            group?.Add(item);
            groupAll.Add(item);
        }

        private void SimpleBind(MenuCategoryBase category, string name, string inputName, InputCombinationSetting setting, ItemGroup group = null, ItemGroup updateGroupOnSet = null)
        {
            var item = new ItemInput(category, name, inputName,
                getter: () => setting.Value,
                setter: (combination) =>
                {
                    setting.Value = combination;
                    ApplySettings(redraw: false);
                    updateGroupOnSet?.Update();
                },
                defaultValue: setting.DefaultValue);

            group?.Add(item);
            groupAll.Add(item);
        }

        private void SimpleScreenPosition(MenuCategoryBase category, string label, Vector2DSetting setting, ItemGroup group = null)
        {
            var item = new ItemBoxMove(category, label, min: setting.Min, max: setting.Max, defaultValue: setting.DefaultValue, rounding: 2,
                getter: () => setting.Value,
                setter: (pos) =>
                {
                    setting.Value = pos;
                    Config.Save();
                },
                selected: (pos) =>
                {
                    setting.Value = pos;
                },
                moving: (pos) =>
                {
                    setting.Value = pos;
                },
                cancelled: (origPos) =>
                {
                    setting.Value = origPos;
                });

            group?.Add(item);
            groupAll.Add(item);
        }

        private void SimpleEnumCycle(MenuCategoryBase category, string label, Type enumType, IntegerSetting setting, ItemGroup group = null, ItemGroup setGroupInteractable = null, int offValue = 0)
        {
            var item = new ItemEnumCycle(category, label,
                getter: () => setting.Value,
                setter: (v) =>
                {
                    setting.Value = v;
                    setGroupInteractable?.SetInteractable(v != offValue);
                    Config.Save();
                },
                enumType: enumType,
                defaultValue: setting.DefaultValue);

            group?.Add(item);
            groupAll.Add(item);
        }

        private void ApplySettings(bool save = true, bool redraw = true, bool moveHint = false, int drawTicks = 0)
        {
            if(save)
                Config.Save();

            tmp.Clear();
            tmp.Append(TEXT_START);

            if(moveHint)
            {
                tmp.NewLine();
                tmp.NewLine().Append("<color=0,255,0>Click and drag anywhere to move!");
                tmp.NewLine().Append("<color=255,255,0>Current position: ").Append(Config.TextAPIScreenPosition.Value.X.ToString("0.000")).Append(", ").Append(Config.TextAPIScreenPosition.Value.Y.ToString("0.000"));
                tmp.NewLine().Append("<color=100,100,55>Default: ").Append(Config.TextAPIScreenPosition.DefaultValue.X.ToString("0.000")).Append(", ").Append(Config.TextAPIScreenPosition.DefaultValue.Y.ToString("0.000"));
                tmp.NewLine();
            }

            tmp.Append(TEXT_END);

            TextGeneration.Refresh(redraw: redraw, write: tmp, forceDrawTicks: drawTicks);
        }
        #endregion Helper methods
    }
}
