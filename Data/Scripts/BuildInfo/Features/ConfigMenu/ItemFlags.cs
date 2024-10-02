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

        private readonly int CombinedValues;

        private readonly ItemGroup topToggle = new ItemGroup();
        private readonly ItemGroup individualToggles = new ItemGroup();

        public ItemFlags(MenuCategoryBase category, string toggleTitle, FlagsSetting<T> setting, Action<int, bool> onValueSet = null) : base(category)
        {
            ToggleTitle = toggleTitle;
            Setting = setting;
            OnValueSet = onValueSet;

            string[] names = Enum.GetNames(typeof(T));
            int[] values = (int[])Enum.GetValues(typeof(T));

            CombinedValues = 0;
            for(int i = 0; i < values.Length; ++i)
            {
                int value = values[i];
                if(value == 0 || value == int.MaxValue)
                    continue;
                CombinedValues |= value;
            }

            CreateToggleAll(category);
            CreateFlagToggles(category, names, values);
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

        void CreateToggleAll(MenuCategoryBase category)
        {
            ItemToggle item = new ItemToggle(category, ToggleTitle,
                getter: () => Setting.Value == CombinedValues || Setting.Value == int.MaxValue,
                setter: (v) =>
                {
                    Setting.SetValue(v ? CombinedValues : 0);
                    OnValueSet?.Invoke(CombinedValues, v);
                    individualToggles.Update();
                },
                defaultValue: Setting.DefaultValue == int.MaxValue || (Setting.DefaultValue & CombinedValues) != 0);

            Item = item.Item;

            topToggle.Add(item);
        }

        void CreateFlagToggles(MenuCategoryBase category, string[] names, int[] values)
        {
            for(int i = 0; i < values.Length; ++i)
            {
                string name = names[i];
                int value = values[i]; // captured by lambda, needs to be in this scope to not change

                if(value == 0 || value == int.MaxValue)
                    continue;

                ItemToggle item = new ItemToggle(category, $"    {name}",
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
