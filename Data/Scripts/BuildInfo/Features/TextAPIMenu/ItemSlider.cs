using System;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemSlider : IItem
    {
        public readonly MenuSliderInput Item = null;
        public Func<float> Getter;
        public Action<float> Setter;
        public Action<float> Sliding;
        public Action<float> Cancelled;
        public Func<float, string> Format;
        public string Title;
        public float Min;
        public float Max;
        public int Rounding;
        public Color ValueColor = new Color(0, 255, 100);

        public ItemSlider(MenuCategoryBase category, string title, float min, float max, int rounding,
            Func<float> getter,
            Action<float> setter = null,
            Action<float> sliding = null,
            Action<float> cancelled = null,
            Func<float, string> format = null)
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

            if(Format == null)
                Format = (val) => val.ToString($"N{rounding}");

            Item = new MenuSliderInput(string.Empty, category, Getter(), title, OnSubmit, OnSlide, OnCancel);

            UpdateTitle();
        }

        public bool Interactable
        {
            get { return Item.Interactable; }
            set { Item.Interactable = value; }
        }

        public void UpdateTitle()
        {
            var titleColor = (Item.Interactable ? "" : "<color=gray>");
            var valueColor = (Item.Interactable ? $"<color={ValueColor.R},{ValueColor.G},{ValueColor.B}>" : "");
            Item.Text = $"{titleColor}{Title}: {valueColor}{Format.Invoke(Getter())}";
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
                return $"Value: {Format.Invoke(value)}";
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
                value = (float)Math.Round((Math.Floor(value * mul) / mul), round); // floor-based rounding to avoid skipping numbers due to slider resolution
            }

            return value;
        }

        private static float ValueToPercent(float min, float max, float value)
        {
            return (value - min) / (max - min);
        }
    }
}
