using System;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.ModAPI;
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
        private readonly ItemGroup groupToolbarLabels = new ItemGroup();
        private readonly ItemGroup groupShipToolInvBar = new ItemGroup();
        private readonly ItemGroup groupOverlayLabelsAlt = new ItemGroup();
        private readonly ItemGroup groupBinds = new ItemGroup(); // for updating titles on bindings that use other bindings

        private readonly ItemGroup groupAll = new ItemGroup(); // for mass-updating titles

        public void RefreshAll()
        {
            groupTextInfo.SetInteractable(Main.Config.TextShow.Value);
            groupCustomStyling.SetInteractable(Main.Config.TextShow.Value && Main.Config.TextAPICustomStyling.Value);
            groupToolbarLabels.SetInteractable(Main.Config.ToolbarLabels.Value != (int)ToolbarLabelsMode.Off);
            groupShipToolInvBar.SetInteractable(Main.Config.ShipToolInvBarShow.Value);
            groupOverlayLabelsAlt.SetInteractable(Main.Config.OverlayLabels.Value != int.MaxValue);

            groupBinds.Update();

            groupAll.Update();
        }

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

        public override void RegisterComponent()
        {
            Main.TextAPI.Detected += TextAPI_Detected;
        }

        public override void UnregisterComponent()
        {
            Main.TextAPI.Detected -= TextAPI_Detected;
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

            Category_Textbox = AddCategory("Text Box", Category_Mod);
            Category_Overlays = AddCategory("Block Overlays", Category_Mod);
            Category_HUD = AddCategory("HUD & GUI", Category_Mod);
            Category_Toolbar = AddCategory("Toolbar", Category_Mod);
            Category_Terminal = AddCategory("Terminal", Category_Mod);
            Category_LeakInfo = AddCategory("Air Leak Info", Category_Mod);
            Category_Binds = AddCategory("Binds", Category_Mod);
            Category_Misc = AddCategory("Misc", Category_Mod);

            ItemAdd_TextShow(Category_Textbox);
            SimpleToggle(Category_Textbox, "Show when HUD is off", Main.Config.TextAlwaysVisible, groupTextInfo);
            SimpleSlider(Category_Textbox, "Text Scale", Main.Config.TextAPIScale, groupTextInfo);
            ItemAdd_BackgroundOpacity(Category_Textbox);
            ItemAdd_CustomStyling(Category_Textbox);
            ItemAdd_ScreenPosition(Category_Textbox);
            ItemAdd_HorizontalAlign(Category_Textbox);
            ItemAdd_VerticalAlign(Category_Textbox);

            Category_PlaceInfo = AddCategory("Place Info", Category_Textbox, group: groupTextInfo);
            ItemAdd_PlaceInfoToggles(Category_PlaceInfo);

            Category_AimInfo = AddCategory("Aim Info", Category_Textbox, group: groupTextInfo);
            ItemAdd_AimInfoToggles(Category_AimInfo);

            SimpleToggle(Category_Overlays, "Show when HUD is off", Main.Config.OverlaysAlwaysVisible);
            ItemAdd_OverlayLabelToggles(Category_Overlays);
            SimpleToggle(Category_Overlays, "Labels shown with ALT", Main.Config.OverlaysLabelsAlt, groupOverlayLabelsAlt);

            SimpleToggle(Category_HUD, "Block Info Additions", Main.Config.BlockInfoAdditions);
            SimpleToggle(Category_HUD, "Ship Tool Inventory Bar", Main.Config.ShipToolInvBarShow, setGroupInteractable: groupShipToolInvBar);
            SimpleScreenPosition(Category_HUD, "Ship Tool Inventory Bar Position", Main.Config.ShipToolInvBarPosition, groupShipToolInvBar);
            SimpleDualSlider(Category_HUD, "Ship Tool Inventory Bar Scale", Main.Config.ShipToolInvBarScale, groupShipToolInvBar);
            SimpleToggle(Category_HUD, "Backpack Bar Override", Main.Config.BackpackBarOverride);
            SimpleToggle(Category_HUD, "Turret HUD", Main.Config.TurretHUD);
            SimpleToggle(Category_HUD, "HUD Stat Overrides", Main.Config.HudStatOverrides);
            SimpleToggle(Category_HUD, "Relative Dampener Info", Main.Config.RelativeDampenerInfo);
            SimpleToggle(Category_HUD, "Item Tooltip Additions", Main.Config.ItemTooltipAdditions);

            SimpleEnumCycle(Category_Toolbar, "Labels Mode", typeof(ToolbarLabelsMode), Main.Config.ToolbarLabels, setGroupInteractable: groupToolbarLabels);
            SimpleEnumCycle(Category_Toolbar, "Toolbar Item Names Mode", typeof(ToolbarNameMode), Main.Config.ToolbarItemNameMode, groupToolbarLabels);
            SimpleToggle(Category_Toolbar, "Labels Show Title", Main.Config.ToolbarLabelsShowTitle, groupToolbarLabels);
            SimpleEnumCycle(Category_Toolbar, "Label Box Style", typeof(ToolbarStyle), Main.Config.ToolbarStyleMode, groupToolbarLabels);
            SimpleScreenPosition(Category_Toolbar, "Labels Box HUD Position", Main.Config.ToolbarLabelsPosition, groupToolbarLabels);
            SimpleScreenPosition(Category_Toolbar, "Labels Box In-Menu Position", Main.Config.ToolbarLabelsInMenuPosition, groupToolbarLabels);
            SimpleSlider(Category_Toolbar, "Labels Box Scale", Main.Config.ToolbarLabelsScale, groupToolbarLabels);
            SimpleDualSlider(Category_Toolbar, "Labels Box ShipToolInvBar Offset", Main.Config.ToolbarLabelsOffsetForInvBar, groupToolbarLabels);
            SimpleToggle(Category_Toolbar, "Improve Action Status", Main.Config.ToolbarActionStatus);

            SimpleToggle(Category_Terminal, "Detail Info Additions", Main.Config.TerminalDetailInfoAdditions);

            SimpleColor(Category_LeakInfo, "Particle Color World", Main.Config.LeakParticleColorWorld);
            SimpleColor(Category_LeakInfo, "Particle Color Overlay", Main.Config.LeakParticleColorOverlay);

            SimpleBind(Category_Binds, "Menu Bind", Features.Config.Config.MENU_BIND_INPUT_NAME, Main.Config.MenuBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Cycle Overlays Bind", Features.Config.Config.CYCLE_OVERLAYS_INPUT_NAME, Main.Config.CycleOverlaysBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Freeze Placement Bind", Features.Config.Config.FREEZE_PLACEMENT_INPUT_NAME, Main.Config.FreezePlacementBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Toggle Transparency Bind", Features.Config.Config.TOGGLE_TRANSPARENCY_INPUT_NAME, Main.Config.ToggleTransparencyBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Block Picker Bind", Features.Config.Config.BLOCK_PICKER_INPUT_NAME, Main.Config.BlockPickerBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Lock Overlay Bind", Features.Config.Config.LOCK_OVERLAY_INPUT_NAME, Main.Config.LockOverlayBind, groupBinds, groupBinds);

            SimpleToggle(Category_Misc, "Adjust Build Distance in Survival", Main.Config.AdjustBuildDistanceSurvival);
            SimpleToggle(Category_Misc, "Adjust Build Distance in Ship Creative", Main.Config.AdjustBuildDistanceShipCreative);
            SimpleToggle(Category_Misc, "Internal Info", Main.Config.InternalInfo);
            AddSpacer(Category_Misc);
            Category_ConfirmReset = AddCategory("Reset to defaults", Category_Misc, header: "Are you sure?");
            new ItemButton(Category_ConfirmReset, "I am sure!", () =>
            {
                Main.Config.Handler.ResetToDefaults();
                Main.Config.Handler.SaveToFile();
                Main.Config.Reload();
                MyAPIGateway.Utilities.ShowNotification("Config reset to defaults and saved.", 3000, FontsHandler.RedSh);
                RefreshAll();
            });

            var button = new ItemButton(Category_Mod, "Mod's workshop page", Main.ChatCommandHandler.CommandWorkshop.ExecuteNoArgs);
            button.Interactable = (Log.WorkshopId > 0);

            // set initial interactable states
            RefreshAll();

            Main.Config.Handler.SettingsLoaded += Handler_SettingsLoaded;
        }

        void Handler_SettingsLoaded()
        {
            RefreshAll();
        }

        private void ItemAdd_TextShow(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Show",
                getter: () => Main.Config.TextShow.Value,
                setter: (v) =>
                {
                    Main.Config.TextShow.Value = v;
                    ApplySettings(redraw: v, drawTicks: (v ? TOGGLE_FORCEDRAWTICKS : 0));
                    groupTextInfo.SetInteractable(v);
                    groupCustomStyling.SetInteractable(v ? Main.Config.TextAPICustomStyling.Value : false);
                },
                defaultValue: Main.Config.TextShow.DefaultValue);

            groupAll.Add(item);
        }

        private void ItemAdd_BackgroundOpacity(MenuCategoryBase category)
        {
            var item = new ItemSlider(category, "Background Opacity", min: -0.1f, max: Main.Config.TextAPIBackgroundOpacity.Max, defaultValue: Main.Config.TextAPIBackgroundOpacity.DefaultValue, rounding: 2,
                getter: () => Main.Config.TextAPIBackgroundOpacity.Value,
                setter: (val) =>
                {
                    if(val < 0)
                        val = -0.1f;

                    Main.Config.TextAPIBackgroundOpacity.Value = val;
                    ApplySettings(redraw: false);
                },
                sliding: (val) =>
                {
                    Main.Config.TextAPIBackgroundOpacity.Value = val;
                    ApplySettings(save: false, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                cancelled: (orig) =>
                {
                    Main.Config.TextAPIBackgroundOpacity.Value = orig;
                    ApplySettings(save: false);
                },
                format: (v) => (v < 0 ? "HUD" : (v * 100).ToString() + "%"));

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_CustomStyling(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Custom Styling",
                getter: () => Main.Config.TextAPICustomStyling.Value,
                setter: (v) =>
                {
                    Main.Config.TextAPICustomStyling.Value = v;
                    groupCustomStyling.SetInteractable(v);
                    ApplySettings(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                defaultValue: Main.Config.TextAPICustomStyling.DefaultValue);

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_ScreenPosition(MenuCategoryBase category)
        {
            var item = new ItemBoxMove(category, "Screen Position", min: Main.Config.TextAPIScreenPosition.Min, max: Main.Config.TextAPIScreenPosition.Max, defaultValue: Main.Config.TextAPIScreenPosition.DefaultValue, rounding: 3,
                getter: () => Main.Config.TextAPIScreenPosition.Value,
                setter: (pos) =>
                {
                    Main.Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(redraw: false);
                },
                selected: (pos) =>
                {
                    Main.Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(save: false, redraw: true, moveHint: true, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                moving: (pos) =>
                {
                    Main.Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(save: false, redraw: true, moveHint: true, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                cancelled: (origPos) =>
                {
                    Main.Config.TextAPIScreenPosition.Value = origPos;
                    ApplySettings(save: false, redraw: false);
                });

            groupCustomStyling.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_HorizontalAlign(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Horizontal Anchor",
                getter: () => Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Right),
                setter: (v) =>
                {
                    var set = !Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Right);
                    Main.Config.TextAPIAlign.Set(TextAlignFlags.Right, set);
                    Main.Config.TextAPIAlign.Set(TextAlignFlags.Left, !set);
                    ApplySettings(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                defaultValue: (Main.Config.TextAPIAlign.DefaultValue & (int)TextAlignFlags.Right) != 0,
                onText: "Right",
                offText: "Left");

            item.ColorOn = item.ColorOff = new Color(255, 255, 0);

            groupCustomStyling.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_VerticalAlign(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Vertical Anchor",
                getter: () => Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom),
                setter: (v) =>
                {
                    var set = !Main.Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom);
                    Main.Config.TextAPIAlign.Set(TextAlignFlags.Bottom, set);
                    Main.Config.TextAPIAlign.Set(TextAlignFlags.Top, !set);
                    ApplySettings(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                defaultValue: (Main.Config.TextAPIAlign.DefaultValue & (int)TextAlignFlags.Bottom) != 0,
                onText: "Bottom",
                offText: "Top");

            item.ColorOn = item.ColorOff = new Color(255, 255, 0);

            groupCustomStyling.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_PlaceInfoToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<PlaceInfoFlags>(category, "Toggle All", Main.Config.PlaceInfo,
                onValueSet: (flag, set) => ApplySettings(redraw: false)
            );

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_AimInfoToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<AimInfoFlags>(category, "Toggle All", Main.Config.AimInfo,
                onValueSet: (flag, set) => ApplySettings(redraw: false)
            );

            groupTextInfo.Add(item);
            groupAll.Add(item);
        }

        private void ItemAdd_OverlayLabelToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<OverlayLabelsFlags>(category, "Toggle All Labels", Main.Config.OverlayLabels,
                onValueSet: (flag, set) =>
                {
                    groupOverlayLabelsAlt.SetInteractable(Main.Config.OverlayLabels.Value != int.MaxValue);
                    ApplySettings(redraw: false);
                }
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
                    Main.Config.Save();
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
                    Main.Config.Save();
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
            var item = new ItemBoxMove(category, label, min: setting.Min, max: setting.Max, defaultValue: setting.DefaultValue, rounding: 6,
                getter: () => setting.Value,
                setter: (pos) =>
                {
                    setting.Value = pos;
                    Main.Config.Save();
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
                    Main.Config.Save();
                },
                enumType: enumType,
                defaultValue: setting.DefaultValue);

            group?.Add(item);
            groupAll.Add(item);
        }

        private void ApplySettings(bool save = true, bool redraw = true, bool moveHint = false, int drawTicks = 0)
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
