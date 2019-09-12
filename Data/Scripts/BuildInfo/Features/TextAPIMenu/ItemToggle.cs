using System;
using Digi.BuildInfo.Utilities;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemToggle : IItem
    {
        public readonly MenuItem Item = null;
        public Func<bool> Getter;
        public Action<bool> Setter;
        public string Title;
        public string OnText;
        public string OffText;
        public Color ColorOn = new Color(50, 255, 75);
        public Color ColorOff = new Color(255, 155, 0);

        public ItemToggle(MenuCategoryBase category, string title, Func<bool> getter, Action<bool> setter, string onText = "On", string offText = "Off")
        {
            Title = title;
            OnText = onText;
            OffText = offText;
            Getter = getter;
            Setter = setter;
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
            var isOn = Getter();
            var titleColor = (Item.Interactable ? "" : "<color=gray>");
            var value = (isOn ? Utils.ColorTag(ColorOn, OnText) : Utils.ColorTag(ColorOff, OffText));
            Item.Text = $"{titleColor}{Title}: {value}";
        }

        private void OnClick()
        {
            try
            {
                Setter.Invoke(!Getter());
                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
