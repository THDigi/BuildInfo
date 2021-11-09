using System;
using Digi.BuildInfo.Utilities;
using Digi.Input;
using Digi.Input.Devices;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;
using static Digi.Input.InputLib;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public class ItemInput : ItemBase<MenuKeybindInput>
    {
        public Func<Combination> Getter;
        public Action<Combination> Setter;
        public string Title;
        public string InputName;
        public Color ValueColor = new Color(0, 255, 100);
        public Combination DefaultValue;

        public ItemInput(MenuCategoryBase category, string title, string inputName, Func<Combination> getter, Action<Combination> setter, Combination defaultValue) : base(category)
        {
            Title = title;
            InputName = inputName;
            Getter = getter;
            Setter = setter;
            DefaultValue = defaultValue;

            Item = new MenuKeybindInput(string.Empty, category, "Press a key to bind.\nCan be combined with alt/ctrl/shift.\nUnbind by confirming without a key.", OnSubmit);

            UpdateTitle();
        }

        protected override void UpdateValue()
        {
            // nothing to update
        }

        protected override void UpdateTitle()
        {
            Combination value = Getter();
            string titleColored = (Item.Interactable ? Title : "<color=gray>" + Title);
            string valueColored = (Item.Interactable ? Utils.ColorTag(ValueColor, value.ToString()) : value.ToString());
            Item.Text = $"{titleColored}: {valueColored}{(Combination.CombinationEqual(DefaultValue, value) ? " <color=gray>[default]" : "")}";
        }

        private void OnSubmit(MyKeys key, bool shift, bool ctrl, bool alt)
        {
            try
            {
                Combination combination = GetCombination(InputName, key, alt, ctrl, shift);

                if(combination != null)
                {
                    Setter?.Invoke(combination);
                    UpdateTitle();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private static Combination GetCombination(string inputName, MyKeys key, bool alt, bool ctrl, bool shift)
        {
            if(key == MyKeys.None) // unbind
            {
                return Combination.Create(inputName, null);
            }

            InputKey input = InputLib.GetInput(key);

            if(input == null)
            {
                MyAPIGateway.Utilities.ShowNotification($"Unknown key: {key.ToString()}", 5000, FontsHandler.RedSh);
                return null;
            }

            string error;
            string combinationString = (alt ? "alt " : "") + (ctrl ? "ctrl " : "") + (shift ? "shift " : "") + input.Id;

            Combination combination = Combination.Create(inputName, combinationString, out error);

            if(error != null)
            {
                MyAPIGateway.Utilities.ShowNotification($"Error binding: {error}", 5000, FontsHandler.RedSh);
                return null;
            }

            MyAPIGateway.Utilities.ShowNotification($"Bound succesfully to: {combination}", 3000, FontsHandler.GreenSh);
            return combination;
        }
    }
}
