using System;
using Digi.BuildInfo.Utilities;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemSlider : ItemBase<MenuSliderInput>
    {
        public Func<float> Getter;
        public Action<float> Setter;
        public Action<float> Sliding;
        public Action<float> Cancelled;
        public Func<float, string> Format;
        public readonly string Title;
        public readonly float Min;
        public readonly float Max;
        public readonly float DefaultValue;
        public readonly int Rounding;
        public Color ValueColor = new Color(0, 255, 100);

        public ItemSlider(MenuCategoryBase category, string title, float min, float max, float defaultValue, int rounding,
            Func<float> getter,
            Action<float> setter = null,
            Action<float> sliding = null,
            Action<float> cancelled = null,
            Func<float, string> format = null) : base(category)
        {
            Title = title;
            Min = min;
            Max = max;
            Rounding = rounding;
            Getter = getter;
            Setter = setter;
            Sliding = sliding;
            Cancelled = cancelled;
            Format = format;
            DefaultValue = defaultValue;

            if(Format == null)
            {
                var formatString = "N" + rounding.ToString();
                Format = (val) => val.ToString(formatString);
            }

            float initialPercent = ValueToPercent(Min, Max, Getter());
            Item = new MenuSliderInput(string.Empty, category, initialPercent, title, OnSubmit, OnSlide, OnCancel);

            UpdateTitle();
        }

        protected override void UpdateValue()
        {
            Item.InitialPercent = ValueToPercent(Min, Max, Getter());
        }

        protected override void UpdateTitle()
        {
            var value = Getter();
            var titleColor = (Item.Interactable ? "" : "<color=gray>");
            var valueColor = (Item.Interactable ? Utils.ColorTag(ValueColor) : "");
            Item.Text = $"{titleColor}{Title}: {valueColor}{Format.Invoke(value)} <color=gray>[default:{Format.Invoke(DefaultValue)}]";
        }

        private void OnSubmit(float percent)
        {
            try
            {
                var value = PercentToRange(Min, Max, percent, Rounding);
                Setter?.Invoke(value);

                Item.InitialPercent = ValueToPercent(Min, Max, Getter());

                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private string OnSlide(float percent)
        {
            try
            {
                var value = PercentToRange(Min, Max, percent, Rounding);
                Sliding?.Invoke(value);
                return $"Default: {Format.Invoke(DefaultValue)} | Current: {Format.Invoke(value)}";
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            return "ERROR!";
        }

        private void OnCancel()
        {
            try
            {
                var value = PercentToRange(Min, Max, Item.InitialPercent, Rounding);
                Cancelled?.Invoke(value);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private static float PercentToRange(float min, float max, float percent, int round)
        {
            float value = min + ((max - min) * percent);

            if(round == 0)
            {
                value = (int)Math.Floor(value);
            }
            else if(round > 0)
            {
                var mul = Math.Pow(10, round);
                value = (float)Math.Round(((value * mul) / mul), round);
            }

            return value;
        }

        private static float ValueToPercent(float min, float max, float value)
        {
            return (value - min) / (max - min);
        }
    }
}
