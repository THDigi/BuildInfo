using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.GUI;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class ScreenTooltips : ModComponent
    {
        static readonly bool DebugDraw = false;

        bool WasChatVisible = false;
        ITooltipHandler TooltipHandler;

        readonly Dictionary<string, List<Tooltip>> Tooltips = new Dictionary<string, List<Tooltip>>();

        public ScreenTooltips(BuildInfoMod main) : base(main)
        {
            UpdateOrder = 10000; // needs to be on top
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            //TooltipHandler?.Dispose();
        }

        /// <summary>
        /// Adds a tooltip to a group ID, the group should usually be the nameof() the class.
        /// </summary>
        public void AddTooltip(string groupId, BoundingBox2 area, string tooltip, Action action = null)
        {
            if(Tooltips.Count == 0)
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);

            Tooltips.GetOrAdd(groupId).Add(new Tooltip(area, tooltip, action));
        }

        public void AddTooltips(string groupId, List<Tooltip> tooltips)
        {
            if(Tooltips.Count == 0)
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);

            List<Tooltip> list = Tooltips.GetOrAdd(groupId);

            foreach(Tooltip tooltip in tooltips)
            {
                list.Add(tooltip);
            }
        }

        public void ClearTooltips(string groupId)
        {
            Tooltips.Remove(groupId);

            if(Tooltips.Count == 0)
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
        }

        public override void UpdateDraw()
        {
            TooltipHandler?.HoverEnd();

            if(!Main.TextAPI.IsEnabled)
                return;

            bool chatVisible = MyAPIGateway.Gui.ChatEntryVisible;
            if(WasChatVisible != chatVisible)
            {
                WasChatVisible = chatVisible;
                if(chatVisible)
                {
                    Main.MenuHandler.AddCursorRequest(GetType().Name);
                }
                else
                {
                    Main.MenuHandler.RemoveCursorRequest(GetType().Name);
                }
            }

            if(chatVisible)
            {
                if(TooltipHandler == null)
                    TooltipHandler = new TooltipHandler();

                Vector2 mousePos = (Vector2)MenuHandler.GetMousePositionGUI();
                Tooltip? found = null;

                foreach(List<Tooltip> tooltips in Tooltips.Values)
                {
                    foreach(Tooltip tooltip in tooltips)
                    {
                        bool hovered = tooltip.Area.Contains(mousePos) != ContainmentType.Disjoint;

                        if(DebugDraw)
                        {
                            DebugDrawTooltip(tooltip.Area, (hovered ? Color.SkyBlue : Color.Lime) * 0.25f);
                        }

                        if(hovered && found == null)
                        {
                            found = tooltip;

                            if(!DebugDraw)
                                break;
                        }
                    }

                    if(found != null && !DebugDraw)
                        break;
                }

                if(found != null)
                {
                    TooltipHandler.Hover(found.Value.Text);
                    TooltipHandler.Draw(mousePos);

                    if(found.Value.Action != null && MyAPIGateway.Input.IsNewLeftMousePressed())
                    {
                        found.Value.Action.Invoke();
                    }
                }
            }
            else if(DebugDraw)
            {
                foreach(List<Tooltip> tooltips in Tooltips.Values)
                {
                    foreach(Tooltip tooltip in tooltips)
                    {
                        DebugDrawTooltip(tooltip.Area, Color.SkyBlue * 0.2f);
                    }
                }
            }
        }

        void DebugDrawTooltip(BoundingBox2 area, Color color)
        {
            MyStringId material = MyStringId.GetOrCompute("Square");
            new HudAPIv2.BillBoardHUDMessage(material, area.Center, color, TimeToLive: 2,
                Width: area.Size.X,
                Height: area.Size.Y,
                HideHud: false);
        }

        public struct Tooltip
        {
            public readonly BoundingBox2 Area;
            public readonly string Text;
            public readonly Action Action;

            public Tooltip(BoundingBox2 area, string text, Action action)
            {
                Area = area;
                Text = text;
                Action = action;
            }
        }
    }
}