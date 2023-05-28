using System;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public class ItemPositionRedirect : ItemBase<MenuItem>
    {
        public string Title;
        public string DenialText;
        public Func<Vector2D> Getter;
        public readonly Vector2D DefaultValue;
        public readonly int Rounding;
        private readonly string format;

        public ItemPositionRedirect(MenuCategoryBase category, string title, string denialText, Vector2D defaultValue, int rounding, Func<Vector2D> getter) : base(category)
        {
            Title = title;
            DenialText = denialText;

            DefaultValue = defaultValue;
            Rounding = rounding;
            format = "N" + rounding.ToString();

            Getter = getter;

            Item = new MenuItem(string.Empty, category, Action);
            UpdateTitle();
        }

        void Action()
        {
            MyAPIGateway.Utilities.ShowNotification(DenialText, 7000, FontsHandler.YellowSh);
        }

        protected override void UpdateValue()
        {
            UpdateTitle();
        }

        protected override void UpdateTitle()
        {
            Vector2D value = Getter();

            string title = (Item.Interactable ? Title : Utils.ColorTag(ConfigMenuHandler.LabelColorDisabled) + Title);
            string valueColor = "";

            if(Item.Interactable)
            {
                if(Vector2D.DistanceSquared(value, DefaultValue) < 0.00001)
                    valueColor = Utils.ColorTag(ConfigMenuHandler.ValueColorDefault);
                else
                    valueColor = Utils.ColorTag(ConfigMenuHandler.ValueColorChanged);
            }

            Item.Text = $"{title}: {valueColor}{value.X.ToString(format)},{value.Y.ToString(format)} {Utils.ColorTag(ConfigMenuHandler.DefaultValueTooltipColor)}[default:{DefaultValue.X.ToString(format)},{DefaultValue.Y.ToString(format)}]";
        }
    }
}
