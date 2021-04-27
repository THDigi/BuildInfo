using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public abstract class ItemBase<T> : IItem
                    where T : MenuItemBase
    {
        public T Item { get; protected set; }
        public readonly MenuCategoryBase Category;

        public ItemBase(MenuCategoryBase category)
        {
            Category = category;
        }

        public virtual bool Interactable
        {
            get { return Item.Interactable; }
            set
            {
                Item.Interactable = value;
                UpdateTitle();
            }
        }

        public void Update()
        {
            UpdateValue();
            UpdateTitle();
        }

        protected abstract void UpdateTitle();

        protected abstract void UpdateValue();
    }
}
