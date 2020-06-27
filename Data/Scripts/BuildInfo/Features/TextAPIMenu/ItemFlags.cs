using System;
using Digi.ConfigLib;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemFlags<T> : IItem where T : struct
    {
        public readonly ItemToggle Item = null;
        public readonly string ToggleTitle;
        public readonly FlagsSetting<T> Setting;
        public Action<int, bool> OnValueSet;

        private readonly int allValue;

        private readonly ItemGroup toggle = new ItemGroup();
        private readonly ItemGroup other = new ItemGroup();

        public ItemFlags(MenuCategoryBase category, string toggleTitle, FlagsSetting<T> setting, Action<int, bool> onValueSet = null)
        {
            ToggleTitle = toggleTitle;
            Setting = setting;
            OnValueSet = onValueSet;

            allValue = (int)Enum.Parse(typeof(T), "All");

            CreateToggleAll(category);
            CreateFlagToggles(category);
        }

        public bool Interactable { get; set; } // HACK unused, might wanna add functionality

        public void UpdateValue()
        {
            toggle.UpdateValues();
            other.UpdateValues();
        }

        public void UpdateTitle()
        {
            toggle.UpdateTitles();
            other.UpdateTitles();
        }

        private void CreateToggleAll(MenuCategoryBase category)
        {
            var item = new ItemToggle(category, ToggleTitle,
                getter: () => Setting.Value == allValue,
                setter: (v) =>
                {
                    Setting.Value = (v ? allValue : 0);
                    OnValueSet?.Invoke(allValue, v);
                    other.UpdateTitles();
                },
                defaultValue: (Setting.DefaultValue & allValue) != 0);

            toggle.Add(item);
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
                        toggle.UpdateTitles();
                    },
                    defaultValue: (Setting.DefaultValue & value) != 0);

                other.Add(item);
            }
        }
    }
}
