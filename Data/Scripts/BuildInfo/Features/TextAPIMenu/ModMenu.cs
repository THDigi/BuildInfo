using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.ConfigLib;
using Digi.Input;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Input;
using VRageMath;
using static Draygo.API.HudAPIv2;
using static Draygo.API.HudAPIv2.MenuRootCategory;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    /// <summary>
    /// The mod menu invoked by TextAPI
    /// </summary>
    public class ModMenu : ClientComponent
    {
        private MenuRootCategory Category_Mod;
        private MenuCategoryBase Category_TextCustomize;
        private MenuCategoryBase Category_PlaceInfo;
        private MenuCategoryBase Category_AimInfo;
        private MenuCategoryBase Category_Overlays;
        private MenuCategoryBase Category_HUD;
        private MenuCategoryBase Category_LeakInfo;
        private MenuCategoryBase Category_Binds;

        // groups of items to update on when other settings are changed
        private ItemGroup groupTextInfo = new ItemGroup();
        private ItemGroup groupCustomStyling = new ItemGroup();
        private ItemGroup groupBinds = new ItemGroup();
        private ItemGroup groupLabelsToggle = new ItemGroup();
        private ItemGroup groupLabels = new ItemGroup();
        private ItemGroup groupAimInfoToggle = new ItemGroup();
        private ItemGroup groupAimInfo = new ItemGroup();
        private ItemGroup groupPlaceInfoToggle = new ItemGroup();
        private ItemGroup groupPlaceInfo = new ItemGroup();

        private readonly StringBuilder tmp = new StringBuilder();

        private const string TEXT_FORMAT = "<color=gray>Lorem ipsum dolor sit amet, consectetur adipiscing elit." +
            "\nPellentesque ac quam in est feugiat mollis." +
            "\nAenean commodo, dolor ac molestie commodo, quam nulla" +
            "\n  suscipit est, sit amet consequat neque purus sed dui." +
            "\n<color=255,0,255>Grab the pink box and move me!" +
            "\n<color=yellow>Current position: {0:0.000}. {1:0.000}" +
            "\n<color=gray>Fusce aliquam eros sit amet varius convallis." +
            "\nClass aptent taciti sociosqu ad litora torquent" +
            "\n  per conubia nostra, per inceptos himenaeos.";

        public ModMenu(Client mod) : base(mod)
        {
        }

        public override void RegisterComponent()
        {
            TextAPI.Detected += TextAPI_Detected;
        }

        public override void UnregisterComponent()
        {
            TextAPI.Detected -= TextAPI_Detected;
        }

        private void TextAPI_Detected()
        {
            Category_Mod = new MenuRootCategory("Build Info", MenuFlag.PlayerMenu, "Build Info Settings");

            new ItemButton(Category_Mod, "Help Window", ChatCommands.ShowHelp);

            Category_TextCustomize = AddCategory("TextBox Customization", Category_Mod);
            Category_Overlays = AddCategory("Overlays", Category_Mod);
            Category_HUD = AddCategory("HUD", Category_Mod);
            Category_LeakInfo = AddCategory("Leak Info", Category_Mod);
            Category_Binds = AddCategory("Binds and related", Category_Mod);

            ItemAdd_TextShow(Category_TextCustomize);
            SimpleToggle(Category_TextCustomize, "Show when HUD is off", Config.TextAlwaysVisible, groupTextInfo);
            ItemAdd_TextScale(Category_TextCustomize);
            ItemAdd_BackgroundOpacity(Category_TextCustomize);
            ItemAdd_CustomStyling(Category_TextCustomize);
            ItemAdd_ScreenPosition(Category_TextCustomize);
            ItemAdd_HorizontalAlign(Category_TextCustomize);
            ItemAdd_VerticalAlign(Category_TextCustomize);

            Category_PlaceInfo = AddCategory("Place Info", Category_TextCustomize);
            ItemAdd_PlaceInfoToggles(Category_PlaceInfo);

            Category_AimInfo = AddCategory("Aim Info", Category_TextCustomize);
            ItemAdd_AimInfoToggles(Category_AimInfo);

            SimpleToggle(Category_Overlays, "Show when HUD is off", Config.OverlaysAlwaysVisible);
            ItemAdd_OverlayLabelToggles(Category_Overlays);

            SimpleToggle(Category_HUD, "Block Info Stages", Config.BlockInfoStages);
            SimpleToggle(Category_HUD, "Ship Tool Inventory Bar", Config.ShipToolInventoryBar);
            SimpleToggle(Category_HUD, "Turret Ammo Count", Config.TurretAmmo);

            new ItemColor(Category_LeakInfo, "Particle Color World", Config.LeakParticleColorWorld, () => ApplySettings(), () => ApplySettings(save: false));
            new ItemColor(Category_LeakInfo, "Particle Color Overlay", Config.LeakParticleColorOverlay, () => ApplySettings(), () => ApplySettings(save: false));

            SimpleBind(Category_Binds, "Menu Bind", Features.Config.Config.MENU_BIND_INPUT_NAME, Config.MenuBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Cycle Overlays Bind", Features.Config.Config.CYCLE_OVERLAYS_INPUT_NAME, Config.CycleOverlaysBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Freeze Placement Bind", Features.Config.Config.FREEZE_PLACEMENT_INPUT_NAME, Config.FreezePlacementBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Toggle Transparency Bind", Features.Config.Config.TOGGLE_TRANSPARENCY_INPUT_NAME, Config.ToggleTransparencyBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Block Picker Bind", Features.Config.Config.BLOCK_PICKER_INPUT_NAME, Config.BlockPickerBind, groupBinds, groupBinds);
            SimpleToggle(Category_Binds, "Placement Distance in Survival", Config.AdjustBuildDistanceSurvival);
            SimpleToggle(Category_Binds, "Placement Distance in Ship Creative", Config.AdjustBuildDistanceShipCreative);

            new ItemButton(Category_Mod, "Mod's workshop page", ChatCommands.ShowModWorkshop);

            // gray out items that need to start like that
            groupTextInfo.SetInteractable(Config.TextShow);
            groupCustomStyling.SetInteractable(Config.TextShow && Config.TextAPICustomStyling.Value);
        }

        private void ItemAdd_TextShow(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Show",
                getter: () => Config.TextShow.Value,
                setter: (v) =>
                {
                    Config.TextShow.Value = v;
                    ApplySettings();
                    groupTextInfo.SetInteractable(v);
                    groupCustomStyling.SetInteractable(v ? Config.TextAPICustomStyling : false);
                });
        }

        private void ItemAdd_TextScale(MenuCategoryBase category)
        {
            var item = new ItemSlider(Category_TextCustomize, "Text Scale", min: Config.TextAPIScale.Min, max: Config.TextAPIScale.Max, rounding: 2,
                getter: () => Config.TextAPIScale.Value,
                setter: (val) =>
                {
                    Config.TextAPIScale.Value = val;
                    ApplySettings();
                },
                sliding: (val) =>
                {
                    Config.TextAPIScale.Value = val;
                    ApplySettings(save: false);
                },
                cancelled: (orig) =>
                {
                    Config.TextAPIScale.Value = orig;
                    ApplySettings(save: false);
                });

            groupTextInfo.Add(item);
        }

        private void ItemAdd_BackgroundOpacity(MenuCategoryBase category)
        {
            var item = new ItemSlider(Category_TextCustomize, "Background Opacity", min: -0.1f, max: Config.TextAPIBackgroundOpacity.Max, rounding: 2,
                getter: () => Config.TextAPIBackgroundOpacity.Value,
                setter: (val) =>
                {
                    if(val < 0)
                        val = -0.1f;

                    Config.TextAPIBackgroundOpacity.Value = val;
                    ApplySettings();
                },
                sliding: (val) =>
                {
                    Config.TextAPIBackgroundOpacity.Value = val;
                    ApplySettings(save: false);
                },
                cancelled: (orig) =>
                {
                    Config.TextAPIBackgroundOpacity.Value = orig;
                    ApplySettings(save: false);
                },
                format: (v) => (v < 0 ? "HUD" : $"{(v * 100)}%"));

            groupTextInfo.Add(item);
        }

        private void ItemAdd_CustomStyling(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Custom Styling",
                getter: () => Config.TextAPICustomStyling,
                setter: (v) =>
                {
                    Config.TextAPICustomStyling.Value = v;
                    groupCustomStyling.SetInteractable(v);
                    ApplySettings();
                });
            groupTextInfo.Add(item);
        }

        private void ItemAdd_ScreenPosition(MenuCategoryBase category)
        {
            var item = new ItemBoxMove(category, "Screen Position", min: Config.TextAPIScreenPosition.Min, max: Config.TextAPIScreenPosition.Max, rounding: 3,
                getter: () => Config.TextAPIScreenPosition.Value,
                setter: (pos) =>
                {
                    Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings();
                },
                selected: (pos) =>
                {
                    Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(save: false);
                },
                moving: (pos) =>
                {
                    Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(save: false);
                },
                cancelled: (origPos) =>
                {
                    Config.TextAPIScreenPosition.Value = origPos;
                    ApplySettings(save: false);
                });
            groupCustomStyling.Add(item);
        }

        private void ItemAdd_HorizontalAlign(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Horizontal Alignment",
                getter: () => Config.TextAPIAlign.IsSet(TextAlignFlags.Right),
                setter: (v) =>
                {
                    var set = !Config.TextAPIAlign.IsSet(TextAlignFlags.Right);
                    Config.TextAPIAlign.Set(TextAlignFlags.Right, set);
                    Config.TextAPIAlign.Set(TextAlignFlags.Left, !set);
                    ApplySettings();
                },
                onText: "Right",
                offText: "Left");

            item.ColorOn = item.ColorOff = new Color(255, 255, 0);

            groupCustomStyling.Add(item);
        }

        private void ItemAdd_VerticalAlign(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, "Vertical Alignment",
                getter: () => Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom),
                setter: (v) =>
                {
                    var set = !Config.TextAPIAlign.IsSet(TextAlignFlags.Bottom);
                    Config.TextAPIAlign.Set(TextAlignFlags.Bottom, set);
                    Config.TextAPIAlign.Set(TextAlignFlags.Top, !set);
                    ApplySettings();
                },
                onText: "Bottom",
                offText: "Top");

            item.ColorOn = item.ColorOff = new Color(255, 255, 0);

            groupCustomStyling.Add(item);
        }

        private void ItemAdd_PlaceInfoToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<PlaceInfoFlags>(category, "Toggle All", Config.PlaceInfo);

            item.OnValueSet = (flag, set) => ApplySettings();
        }

        private void ItemAdd_AimInfoToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<AimInfoFlags>(category, "Toggle All", Config.AimInfo);

            item.OnValueSet = (flag, set) => ApplySettings();
        }

        private void ItemAdd_OverlayLabelToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<OverlayLabelsFlags>(category, "Toggle All Labels", Config.OverlayLabels);

            item.OnValueSet = (flag, set) => ApplySettings();
        }

        #region Helper methods
        private MenuCategoryBase AddCategory(string name, MenuCategoryBase parent)
        {
            return new MenuSubCategory($"{name} <color=0,155,255> >>>", parent, name);
        }

        private void AddSpacer(MenuCategoryBase category, string label = null)
        {
            new MenuItem($"<color=0,55,0>{(label == null ? new string('=', 10) : $"=== {label} ===")}", category);
        }

        private void SimpleToggle(MenuCategoryBase category, string title, BoolSetting setting, ItemGroup group = null)
        {
            var item = new ItemToggle(category, title,
                getter: () => setting.Value,
                setter: (v) =>
                {
                    setting.Value = v;
                    ApplySettings();
                });
            group?.Add(item);
        }

        private void SimpleBind(MenuCategoryBase category, string name, string inputName, InputCombinationSetting setting, ItemGroup addToGroup = null, ItemGroup updateGroupOnSet = null)
        {
            var item = new ItemInput(category, name, inputName,
                getter: () => setting.Value,
                setter: (combination) =>
                {
                    setting.Value = combination;
                    ApplySettings();
                    updateGroupOnSet?.UpdateTitles();
                });

            addToGroup?.Add(item);
        }

        private void ApplySettings(bool save = true)
        {
            if(save)
                Config.Save();

            tmp.Clear().AppendFormat(TEXT_FORMAT, Config.TextAPIScreenPosition.Value.X.ToString(), Config.TextAPIScreenPosition.Value.Y.ToString());

            TextGeneration.Refresh(redraw: true, write: tmp);
        }
        #endregion Helper methods
    }
}
