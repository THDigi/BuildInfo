using System;
using Digi.BuildInfo.Utilities;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public class ItemButton : ItemBase<MenuItem>
    {
        public Action Action;
        public string Title;
        public Color TitleColor = new Color(255, 255, 255);

        public ItemButton(MenuCategoryBase category, string title, Action action) : base(category)
        {
            Title = title;
            Action = action;
            Item = new MenuItem(string.Empty, category, OnClick);
            UpdateTitle();
        }

        protected override void UpdateValue()
        {
            // nothing to update
        }

        protected override void UpdateTitle()
        {
            Item.Text = (Item.Interactable ? Utils.ColorTag(TitleColor, Title) : "<color=gray>" + Title);
        }

        private void OnClick()
        {
            try
            {
                Action.Invoke();
                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
