using Digi.BuildInfo.Utilities;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public class ItemSubMenu : ItemBase<MenuSubCategory>
    {
        public string Title;

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
            string titleColor = (Item.Interactable ? "" : Utils.ColorTag(ConfigMenuHandler.LabelColorDisabled));
            string valueColor = Utils.ColorTag(Item.Interactable ? ConfigMenuHandler.HeaderColor : ConfigMenuHandler.LabelColorDisabled);
            Item.Text = $"{titleColor}{Title} {valueColor}>>>";
        }
    }
}
