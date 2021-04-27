using System;
using Digi.ConfigLib;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public class ItemFlags<T> : ItemBase<MenuItemBase> where T : struct
    {
        public readonly string ToggleTitle;
        public readonly FlagsSetting<T> Setting;
        public Action<int, bool> OnValueSet;

        bool AllInteractible = true;

        private readonly int allValue;

        private readonly ItemGroup topToggle = new ItemGroup();
        private readonly ItemGroup individualToggles = new ItemGroup();

        public ItemFlags(MenuCategoryBase category, string toggleTitle, FlagsSetting<T> setting, Action<int, bool> onValueSet = null) : base(category)
        {
            ToggleTitle = toggleTitle;
            Setting = setting;
            OnValueSet = onValueSet;

            allValue = (int)Enum.Parse(typeof(T), "All");

            CreateToggleAll(category);
            CreateFlagToggles(category);
        }

        public override bool Interactable
        {
            get { return AllInteractible; }
            set
            {
                AllInteractible = value;
                topToggle.SetInteractable(value);
                individualToggles.SetInteractable(value);
                // no update title here as the childs will update titles themselves
            }
        }

        protected override void UpdateValue()
        {
            topToggle.Update();
            individualToggles.Update();
        }

        protected override void UpdateTitle()
        {
            topToggle.Update();
            individualToggles.Update();
        }

        private void CreateToggleAll(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, ToggleTitle,
                getter: () => Setting.Value == allValue,
                setter: (v) =>
                {
                    Setting.Value = (v ? allValue : 0);
                    OnValueSet?.Invoke(allValue, v);
                    individualToggles.Update();
                },
                defaultValue: (Setting.DefaultValue & allValue) != 0);

            Item = item.Item;

            topToggle.Add(item);
        }

        private void CreateFlagToggles(MenuCategoryBase category)
        {
            var names = Enum.GetNames(typeof(T));
            var values = (int[])Enum.GetValues(typeof(T));

            for(int i = 0; i < names.Length; ++i)
            {
                var name = names[i];

                if(name == "All" || name == "None")
                    continue;

                int value = values[i]; // captured by lambda, needs to be in this scope to not change

                var item = new ItemToggle(category, $"    {name}",
                    getter: () => Setting.IsSet(value),
                    setter: (v) =>
                    {
                        Setting.Set(value, v);
                        OnValueSet?.Invoke(value, v);
                        topToggle.Update();
                    },
                    defaultValue: (Setting.DefaultValue & value) != 0);

                individualToggles.Add(item);
            }
        }
    }
}
