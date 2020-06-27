using System;
using Digi.BuildInfo.Utilities;
using Digi.ConfigLib;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
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

            var inputDialogTitle = $"{Title} | Default: {Setting.DefaultValue.R.ToString()},{Setting.DefaultValue.G.ToString()},{Setting.DefaultValue.B.ToString()}";

            Item = new MenuColorPickerInput(title, category, setting.Value, inputDialogTitle, OnSubmit, OnSlide, OnCancel, useAlpha);

            UpdateTitle();
        }

        protected override void UpdateValue()
        {
            Item.InitialColor = Setting.Value;
        }

        protected override void UpdateTitle()
        {
            var valueColor = Utils.ColorTag(Setting.Value);
            Item.Text = $"{Title}: {valueColor}{Setting.Value.R.ToString()},{Setting.Value.G.ToString()},{Setting.Value.B.ToString()} <color=gray>[default:{Setting.DefaultValue.R.ToString()},{Setting.DefaultValue.G.ToString()},{Setting.DefaultValue.B.ToString()}]";
        }

        private void OnSubmit(Color color)
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

        private void OnSlide(Color color)
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

        private void OnCancel()
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
