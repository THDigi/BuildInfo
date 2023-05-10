using System;
using Digi.BuildInfo.Utilities;
using Digi.ConfigLib;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public class ItemColor : ItemBase<MenuColorPickerInput>
    {
        public readonly ColorSetting Setting;
        public string Title;
        public Action Apply;
        public Action Preview;

        public ItemColor(MenuCategoryBase category, string title, ColorSetting setting, Action apply = null, Action preview = null, bool useAlpha = false) : base(category)
        {
            Title = title;
            Setting = setting;
            Apply = apply;
            Preview = preview;

            string inputDialogTitle = $"{Title} | Default: {Setting.DefaultValue.R},{Setting.DefaultValue.G},{Setting.DefaultValue.B}";

            Item = new MenuColorPickerInput(title, category, setting.Value, inputDialogTitle, OnSubmit, OnSlide, OnCancel, useAlpha);

            UpdateTitle();
        }

        protected override void UpdateValue()
        {
            Item.InitialColor = Setting.Value;
        }

        protected override void UpdateTitle()
        {
            string valueStr = $"{Utils.ColorTag(Setting.Value == Setting.DefaultValue ? ConfigMenuHandler.ValueColorDefault : ConfigMenuHandler.ValueColorChanged)}{Setting.Value.R},{Setting.Value.G},{Setting.Value.B}";
            string defaultValueStr = $"{Utils.ColorTag(ConfigMenuHandler.DefaultValueTooltipColor)}[default:{Setting.DefaultValue.R},{Setting.DefaultValue.G},{Setting.DefaultValue.B}]";
            Item.Text = $"{Title}: {Utils.ColorTag(Setting.Value, "□")} {valueStr} {defaultValueStr}";
        }

        void OnSubmit(Color color)
        {
            try
            {
                Setting.Value = color;
                Item.InitialColor = color;
                Apply?.Invoke();
                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void OnSlide(Color color)
        {
            try
            {
                Setting.Value = color;
                Preview?.Invoke();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void OnCancel()
        {
            try
            {
                Setting.Value = Item.InitialColor;
                Preview?.Invoke();
                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
