using System;
using Digi.BuildInfo.Utilities;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.ConfigMenu
{
    public class ItemBoxMove : ItemBase<MenuScreenInput>
    {
        public Func<Vector2D> Getter;
        public Action<Vector2D> Setter;
        public Action<Vector2D> Selected;
        public Action<Vector2D> Moving;
        public Action<Vector2D> Cancelled;
        public string Title;
        public readonly Vector2D Min;
        public readonly Vector2D Max;
        public readonly Vector2D DefaultValue;
        public readonly int Rounding;
        private readonly string format;

        public ItemBoxMove(MenuCategoryBase category, string title, Vector2D min, Vector2D max, Vector2D defaultValue, int rounding,
            Func<Vector2D> getter,
            Action<Vector2D> setter = null,
            Action<Vector2D> selected = null,
            Action<Vector2D> moving = null,
            Action<Vector2D> cancelled = null) : base(category)
        {
            Title = title;
            Min = min;
            Max = max;
            DefaultValue = defaultValue;
            Rounding = rounding;
            format = "N" + rounding.ToString();
            Getter = getter;
            Setter = setter;
            Selected = selected;
            Moving = moving;
            Cancelled = cancelled;

            // HACK using `" "` instead of string.Empty due to an issue with textAPI.
            Item = new MenuScreenInput(string.Empty, category, Getter(), Vector2D.Zero, " ", OnSubmit, OnMove, OnCancel, OnSelect);
            UpdateTitle();
        }

        protected override void UpdateValue()
        {
            Item.Origin = Getter();
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

        void OnSelect()
        {
            try
            {
                UpdateValue();
                Selected?.Invoke(Item.Origin);
                Update();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void OnMove(Vector2D pos)
        {
            try
            {
                pos = new Vector2D(Math.Round(pos.X, Rounding), Math.Round(pos.Y, Rounding));
                pos = Vector2D.Clamp(pos, Min, Max);
                Moving?.Invoke(pos);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void OnSubmit(Vector2D pos)
        {
            try
            {
                pos = new Vector2D(Math.Round(pos.X, Rounding), Math.Round(pos.Y, Rounding));
                pos = Vector2D.Clamp(pos, Min, Max);
                Setter?.Invoke(pos);
                Update();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void OnCancel()
        {
            try
            {
                Cancelled?.Invoke(Item.Origin);
                Update();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
