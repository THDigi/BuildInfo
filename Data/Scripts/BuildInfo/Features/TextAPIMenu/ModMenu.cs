using System.Text;
using Digi.BuildInfo.Features.Config;
using Digi.ConfigLib;
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
        private readonly ItemGroup groupTextInfo = new ItemGroup();
        private readonly ItemGroup groupCustomStyling = new ItemGroup();
        private readonly ItemGroup groupBinds = new ItemGroup();
        private readonly ItemGroup groupLabelsToggle = new ItemGroup();
        private readonly ItemGroup groupLabels = new ItemGroup();
        private readonly ItemGroup groupAimInfoToggle = new ItemGroup();
        private readonly ItemGroup groupAimInfo = new ItemGroup();
        private readonly ItemGroup groupPlaceInfoToggle = new ItemGroup();
        private readonly ItemGroup groupPlaceInfo = new ItemGroup();

        private readonly StringBuilder tmp = new StringBuilder();

        private const uint SLIDERS_FORCEDRAWTICKS = 60 * 10;
        private const uint TOGGLE_FORCEDRAWTICKS = 60 * 2;
        private const string TEXT_START = "<color=gray>Lorem ipsum dolor sit amet, consectetur adipiscing elit." +
                                        "\nPellentesque ac quam in est feugiat mollis." +
                                        "\nAenean commodo, dolor ac molestie commodo, quam nulla" +
                                        "\n  suscipit est, sit amet consequat neque purus sed dui.";
        private const string TEXT_END = "\n<color=gray>Fusce aliquam eros sit amet varius convallis." +
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
            Category_HUD = AddCategory("HUD Additions", Category_Mod);
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

            SimpleToggle(Category_TextCustomize, "Internal Info", Config.InternalInfo);

            SimpleToggle(Category_Overlays, "Show when HUD is off", Config.OverlaysAlwaysVisible);
            ItemAdd_OverlayLabelToggles(Category_Overlays);

            SimpleToggle(Category_HUD, "Block Info Stages", Config.BlockInfoStages);
            SimpleToggle(Category_HUD, "Ship Tool Inventory Bar", Config.ShipToolInventoryBar);
            SimpleToggle(Category_HUD, "Turret Ammo Count", Config.TurretAmmo);

            new ItemColor(Category_LeakInfo, "Particle Color World", Config.LeakParticleColorWorld, () => ApplySettings(redraw: false), () => ApplySettings(save: false, redraw: false));
            new ItemColor(Category_LeakInfo, "Particle Color Overlay", Config.LeakParticleColorOverlay, () => ApplySettings(redraw: false), () => ApplySettings(save: false, redraw: false));

            SimpleBind(Category_Binds, "Menu Bind", Features.Config.Config.MENU_BIND_INPUT_NAME, Config.MenuBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Cycle Overlays Bind", Features.Config.Config.CYCLE_OVERLAYS_INPUT_NAME, Config.CycleOverlaysBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Freeze Placement Bind", Features.Config.Config.FREEZE_PLACEMENT_INPUT_NAME, Config.FreezePlacementBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Toggle Transparency Bind", Features.Config.Config.TOGGLE_TRANSPARENCY_INPUT_NAME, Config.ToggleTransparencyBind, groupBinds, groupBinds);
            SimpleBind(Category_Binds, "Block Picker Bind", Features.Config.Config.BLOCK_PICKER_INPUT_NAME, Config.BlockPickerBind, groupBinds, groupBinds);
            SimpleToggle(Category_Binds, "Placement Distance in Survival", Config.AdjustBuildDistanceSurvival);
            SimpleToggle(Category_Binds, "Placement Distance in Ship Creative", Config.AdjustBuildDistanceShipCreative);

            new ItemButton(Category_Mod, "Mod's workshop page", ChatCommands.ShowBuildInfoWorkshop);

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
                    ApplySettings(redraw: v, drawTicks: (v ? TOGGLE_FORCEDRAWTICKS : 0u));
                    groupTextInfo.SetInteractable(v);
                    groupCustomStyling.SetInteractable(v ? Config.TextAPICustomStyling : false);
                });
        }

        private void ItemAdd_TextScale(MenuCategoryBase category)
        {
            var item = new ItemSlider(category, "Text Scale", min: Config.TextAPIScale.Min, max: Config.TextAPIScale.Max, rounding: 2,
                getter: () => Config.TextAPIScale.Value,
                setter: (val) =>
                {
                    Config.TextAPIScale.Value = val;
                    ApplySettings(redraw: false);
                },
                sliding: (val) =>
                {
                    Config.TextAPIScale.Value = val;
                    ApplySettings(save: false, drawTicks: SLIDERS_FORCEDRAWTICKS);
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
            var item = new ItemSlider(category, "Background Opacity", min: -0.1f, max: Config.TextAPIBackgroundOpacity.Max, rounding: 2,
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
                    ApplySettings(drawTicks: TOGGLE_FORCEDRAWTICKS);
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
                    ApplySettings(redraw: false);
                },
                selected: (pos) =>
                {
                    Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(save: false, moveHint: true, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                moving: (pos) =>
                {
                    Config.TextAPIScreenPosition.Value = pos;
                    ApplySettings(save: false, moveHint: true, drawTicks: SLIDERS_FORCEDRAWTICKS);
                },
                cancelled: (origPos) =>
                {
                    Config.TextAPIScreenPosition.Value = origPos;
                    ApplySettings(save: false, redraw: false);
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
                    ApplySettings(drawTicks: TOGGLE_FORCEDRAWTICKS);
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
                    ApplySettings(drawTicks: TOGGLE_FORCEDRAWTICKS);
                },
                onText: "Bottom",
                offText: "Top");

            item.ColorOn = item.ColorOff = new Color(255, 255, 0);

            groupCustomStyling.Add(item);
        }

        private void ItemAdd_PlaceInfoToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<PlaceInfoFlags>(category, "Toggle All", Config.PlaceInfo,
                onValueSet: (flag, set) => ApplySettings(redraw: false)
            );
        }

        private void ItemAdd_AimInfoToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<AimInfoFlags>(category, "Toggle All", Config.AimInfo,
                onValueSet: (flag, set) => ApplySettings(redraw: false)
            );
        }

        private void ItemAdd_OverlayLabelToggles(MenuCategoryBase category)
        {
            var item = new ItemFlags<OverlayLabelsFlags>(category, "Toggle All Labels", Config.OverlayLabels,
                onValueSet: (flag, set) => ApplySettings(redraw: false)
            );
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
                    ApplySettings(redraw: false);
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
                    ApplySettings(redraw: false);
                    updateGroupOnSet?.UpdateTitles();
                });

            addToGroup?.Add(item);
        }

        private void ApplySettings(bool save = true, bool redraw = true, bool moveHint = false, uint drawTicks = 0)
        {
            if(save)
                Config.Save();

            tmp.Clear();
            tmp.Append(TEXT_START);

            if(moveHint)
                tmp.Append($"\n<color=0,255,0>Click and drag anywhere to move!\n<color=255,255,0>Current position: {Config.TextAPIScreenPosition.Value.X:0.000}, {Config.TextAPIScreenPosition.Value.Y:0.000}");

            tmp.Append(TEXT_END);

            TextGeneration.Refresh(redraw: redraw, write: tmp, forceDrawTicks: drawTicks);
        }
        #endregion Helper methods
    }
}
