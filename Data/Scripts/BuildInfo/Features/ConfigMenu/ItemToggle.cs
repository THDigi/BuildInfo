﻿using System;
using Digi.BuildInfo.Utilities;
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
            bool isOn = Getter();
            string title = (Item.Interactable ? Title : Utils.ColorTag(ConfigMenuHandler.LabelColorDisabled, Title));
            string valueColor = (Item.Interactable ? Utils.ColorTag(isOn == DefaultValue ? ConfigMenuHandler.ValueColorDefault : ConfigMenuHandler.ValueColorChanged) : "");
            Item.Text = $"{title}: {valueColor}{(isOn ? OnText : OffText)}";
        }

        void OnClick()
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
