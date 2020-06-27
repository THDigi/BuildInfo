using System.Collections.Generic;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemGroup
    {
        public readonly List<IItem> Items = new List<IItem>();

        public ItemGroup()
        {
        }

        public void Add(IItem item)
        {
            Items.Add(item);
        }

        public void SetInteractable(bool set)
        {
            foreach(var e in Items)
            {
                e.Interactable = set;
            }
        }

        public void Update()
        {
            foreach(var e in Items)
            {
                e.Update();
            }
        }
    }
}
