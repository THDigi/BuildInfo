using System;
using Digi.BuildInfo.Utilities;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public class ItemEnumCycle : ItemBase<MenuItem>
    {
        public Func<int> Getter;
        public Action<int> Setter;
        public string Title;

        private readonly string[] names;
        private readonly int[] values;
        private readonly int defaultValue;

        public ItemEnumCycle(MenuCategoryBase category, string title, Func<int> getter, Action<int> setter, Type enumType, int defaultValue) : base(category)
        {
            Title = title;
            Getter = getter;
            Setter = setter;
            this.defaultValue = defaultValue;

            names = Enum.GetNames(enumType);
            values = (int[])Enum.GetValues(enumType);

            Item = new MenuItem(string.Empty, category, OnClick);
            UpdateTitle();
        }

        protected override void UpdateValue()
        {
            // nothing to update
        }

        protected override void UpdateTitle()
        {
            int idx = Getter();
            string titleColor = (Item.Interactable ? "" : Utils.ColorTag(ConfigMenuHandler.LabelColorDisabled));
            string valueColor = (Item.Interactable ? Utils.ColorTag(idx == defaultValue ? ConfigMenuHandler.ValueColorDefault : ConfigMenuHandler.ValueColorChanged) : "");

            Item.Text = $"{titleColor}{Title}: {valueColor}{names[idx]} <color=white>({idx + 1} of {(names.Length)}) {Utils.ColorTag(ConfigMenuHandler.DefaultValueTooltipColor)}[default:{names[defaultValue]}]";
        }

        void OnClick()
        {
            try
            {
                int val = Getter();

                val += 1;

                if(val > values[values.Length - 1])
                    val = 0;

                Setter.Invoke(val);
                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
