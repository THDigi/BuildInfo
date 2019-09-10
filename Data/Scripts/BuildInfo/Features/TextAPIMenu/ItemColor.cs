using System;
using Digi.BuildInfo.Utilities;
using Digi.ConfigLib;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemColor : IItem
    {
        public readonly MenuItem Item = null;
        public readonly ColorSetting Setting;
        public readonly ItemSlider[] Sliders = new ItemSlider[3];
        public string Title;
        public Action Apply;
        public Action Preview;

        private readonly string[] CHANNEL_NAMES = new string[]
        {
            " > Red",
            " > Green",
            " > Blue",
        };

        public ItemColor(MenuCategoryBase category, string title, ColorSetting setting, Action apply, Action preview)
        {
            Title = title;
            Setting = setting;
            Apply = apply;
            Preview = preview;

            Item = new MenuItem(string.Empty, category, OnClick: null, Interactable: false);

            for(int i = 0; i < Sliders.Length; ++i)
            {
                int channel = i;

                var slider = Sliders[channel] = new ItemSlider(category, CHANNEL_NAMES[channel], min: 0, max: 255, rounding: 0,
                    getter: () => GetChannel(setting.Value, channel),
                    setter: (val) =>
                    {
                        setting.Value = SetChannel(setting.Value, channel, (byte)val);
                        Apply?.Invoke();
                        UpdateTitle();
                    },
                    sliding: (val) =>
                    {
                        setting.Value = SetChannel(setting.Value, channel, (byte)val);
                        Preview?.Invoke();
                        UpdateTitle();
                    },
                    cancelled: (orig) =>
                    {
                        setting.Value = SetChannel(setting.Value, channel, (byte)orig);
                        Preview?.Invoke();
                        UpdateTitle();
                    });

                switch(channel)
                {
                    case 0: slider.ValueColor = new Color(255, 0, 0); break;
                    case 1: slider.ValueColor = new Color(0, 255, 0); break;
                    case 2: slider.ValueColor = new Color(0, 0, 255); break;
                }

                slider.UpdateTitle();
            }

            UpdateTitle();
        }

        private byte GetChannel(Color color, int channel)
        {
            switch(channel)
            {
                case 0: return color.R;
                case 1: return color.G;
                case 2: return color.B;
                case 3: return color.A;
            }
            return 0;
        }

        private Color SetChannel(Color color, int channel, byte value)
        {
            switch(channel)
            {
                case 0: color.R = value; break;
                case 1: color.G = value; break;
                case 2: color.B = value; break;
                case 3: color.A = value; break;
            }
            return color;
        }

        public bool Interactable
        {
            get { return Item.Interactable; }
            set { Item.Interactable = value; }
        }

        public void UpdateTitle()
        {
            var valueColor = Utils.ColorTag(Setting.Value);
            Item.Text = $"{Title}: {valueColor}|||||";
        }
    }
}
