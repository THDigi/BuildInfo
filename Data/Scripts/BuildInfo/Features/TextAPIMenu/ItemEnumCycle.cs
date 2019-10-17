using System;
using Digi.BuildInfo.Utilities;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemEnumCycle : IItem
    {
        public readonly MenuItem Item = null;
        public Func<int> Getter;
        public Action<int> Setter;
        public string Title;

        public Color ColorName = new Color(50, 255, 75);
        public Color ColorValue = new Color(255, 255, 255);

        private readonly string[] names;
        private readonly int[] values;

        public ItemEnumCycle(MenuCategoryBase category, string title, Func<int> getter, Action<int> setter, Type enumType)
        {
            Title = title;
            Getter = getter;
            Setter = setter;

            names = Enum.GetNames(enumType);
            values = (int[])Enum.GetValues(enumType);

            Item = new MenuItem(string.Empty, category, OnClick);
            UpdateTitle();
        }

        public bool Interactable
        {
            get { return Item.Interactable; }
            set { Item.Interactable = value; }
        }

        public void UpdateTitle()
        {
            int val = Getter();
            var titleColor = (Item.Interactable ? "" : "<color=gray>");
            Item.Text = $"{titleColor}{Title}: {Utils.ColorTag(ColorName)}{names[val]} {Utils.ColorTag(ColorValue)}({val.ToString()})";
        }

        private void OnClick()
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
