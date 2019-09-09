using System;
using VRageMath;
using static Draygo.API.HudAPIv2;

namespace Digi.BuildInfo.Features.TextAPIMenu
{
    public class ItemBoxMove : IItem
    {
        public readonly MenuScreenInput Item = null;
        public Func<Vector2D> Getter;
        public Action<Vector2D> Setter;
        public Action<Vector2D> Selected;
        public Action<Vector2D> Moving;
        public Action<Vector2D> Cancelled;
        public string Title;
        public readonly Vector2D Min;
        public readonly Vector2D Max;
        public readonly int Rounding;
        private readonly string format;

        public ItemBoxMove(MenuCategoryBase category, string title, Vector2D min, Vector2D max, int rounding,
            Func<Vector2D> getter,
            Action<Vector2D> setter = null,
            Action<Vector2D> selected = null,
            Action<Vector2D> moving = null,
            Action<Vector2D> cancelled = null)
        {
            Title = title;
            Min = min;
            Max = max;
            Rounding = rounding;
            format = $"N{rounding}";
            Getter = getter;
            Setter = setter;
            Selected = selected;
            Moving = moving;
            Cancelled = cancelled;

            Item = new MenuScreenInput(string.Empty, category, Getter(), Vector2D.Zero, string.Empty, OnSubmit, OnMove, OnCancel, OnSelect);
            UpdateTitle();
        }

        public bool Interactable
        {
            get { return Item.Interactable; }
            set { Item.Interactable = value; }
        }

        public void UpdateTitle()
        {
            var titleColor = (Item.Interactable ? "" : "<color=gray>");
            var valueColor = (Item.Interactable ? "<color=yellow>" : "");
            var value = Getter();
            Item.Text = $"{titleColor}{Title}: {valueColor}{value.X.ToString(format)},{value.Y.ToString(format)}";
        }

        private void OnSubmit(Vector2D pos)
        {
            try
            {
                pos = new Vector2D(Math.Round(pos.X, Rounding), Math.Round(pos.Y, Rounding));
                pos = Vector2D.Clamp(pos, Min, Max);
                Setter?.Invoke(pos);
                Item.Origin = pos;
                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void OnSelect()
        {
            try
            {
                Selected?.Invoke(Item.Origin);
                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void OnMove(Vector2D pos)
        {
            try
            {
                pos = new Vector2D(Math.Round(pos.X, Rounding), Math.Round(pos.Y, Rounding));
                pos = Vector2D.Clamp(pos, Min, Max);
                Moving?.Invoke(pos);
                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void OnCancel()
        {
            try
            {
                Cancelled?.Invoke(Item.Origin);
                UpdateTitle();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
