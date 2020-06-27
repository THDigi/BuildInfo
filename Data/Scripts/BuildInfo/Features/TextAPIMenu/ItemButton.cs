using System;
using Digi.BuildInfo.Utilities;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemButton : IItem
    {
        public readonly MenuItem Item = null;
        public Action Action;
        public string Title;
        public Color TitleColor = new Color(255, 255, 255);

        public ItemButton(MenuCategoryBase category, string title, Action action)
        {
            Title = title;
            Action = action;
            Item = new MenuItem(string.Empty, category, OnClick);
            UpdateTitle();
        }

        public bool Interactable
        {
            get { return Item.Interactable; }
            set { Item.Interactable = value; }
        }

        public void UpdateValue()
        {
            // nothing to set
        }

        public void UpdateTitle()
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
