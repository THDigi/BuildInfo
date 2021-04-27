using System;
using Digi.BuildInfo.Utilities;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public class ItemToggle : ItemBase<MenuItem>
    {
        public Func<bool> Getter;
        public Action<bool> Setter;
        public string Title;
        public string OnText;
        public string OffText;
        public bool DefaultValue;
        public Color ColorOn = new Color(50, 255, 75);
        public Color ColorOff = new Color(255, 155, 0);

        public ItemToggle(MenuCategoryBase category, string title, Func<bool> getter, Action<bool> setter, bool defaultValue = true, string onText = "On", string offText = "Off") : base(category)
        {
            Title = title;
            OnText = onText;
            OffText = offText;
            Getter = getter;
            Setter = setter;
            DefaultValue = defaultValue;
            Item = new MenuItem(string.Empty, category, OnClick);
            UpdateTitle();
        }

        protected override void UpdateValue()
        {
            // nothing to update
        }

        protected override void UpdateTitle()
        {
            var isOn = Getter();
            var titleColor = (Item.Interactable ? "" : "<color=gray>");
            var value = (isOn ? Utils.ColorTag(Item.Interactable ? ColorOn : Color.Gray, OnText) : Utils.ColorTag(Item.Interactable ? ColorOff : Color.Gray, OffText));
            Item.Text = $"{titleColor}{Title}: {value}{(DefaultValue == isOn ? " <color=gray>[default]" : "")}";
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
