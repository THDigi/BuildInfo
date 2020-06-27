using Digi.BuildInfo.Utilities;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemSubMenu : ItemBase<MenuSubCategory>
    {
        public string Title;
        public Color Color = new Color(0, 155, 255);

        public ItemSubMenu(MenuCategoryBase category, string title, string header = null) : base(category)
        {
            Title = title;
            Item = new MenuSubCategory(string.Empty, category, header ?? title);
            UpdateTitle();
        }

        protected override void UpdateValue()
        {
            // nothing to update
        }

        protected override void UpdateTitle()
        {
            var titleColor = (Item.Interactable ? "" : "<color=gray>");
            var valueColor = Utils.ColorTag(Item.Interactable ? Color : Color.Gray);
            Item.Text = $"{titleColor}{Title} {valueColor}>>>";
        }
    }
}
