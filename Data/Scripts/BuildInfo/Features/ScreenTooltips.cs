using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.GUI;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    // TODO: highlight mass to explain it's real physical mass and point to info tab for old value

    public class ScreenTooltips : ModComponent
    {
        static readonly bool DebugDraw = false;

        bool WasChatVisible = false;
        ITooltipHandler TooltipHandler;

        HudAPIv2.BillBoardHUDMessage SelectedBox;

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

            Tooltips.GetValueOrNew(groupId).Add(new Tooltip(area, tooltip, action));
        }

        public void AddTooltips(string groupId, List<Tooltip> tooltips)
        {
            if(Tooltips.Count == 0)
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);

            List<Tooltip> list = Tooltips.GetValueOrNew(groupId);

            foreach(Tooltip tooltip in tooltips)
            {
                list.Add(tooltip);
            }
        }

        public void ClearTooltips(string groupId)
        {
            Tooltips.Remove(groupId);

            if(Tooltips.Count == 0)
            {
                Hide();
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
            }
        }

        void Hide()
        {
            if(TooltipHandler != null)
            {
                TooltipHandler.HoverEnd();
                //SelectedBox.Visible = false;
            }
        }

        public override void UpdateDraw()
        {
            Hide();

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
                {
                    TooltipHandler = new TooltipHandler();
                    SelectedBox = TextAPI.CreateHUDTexture(MyStringId.GetOrCompute("BuildInfo_UI_Square"), Color.Lime * 0.2f, Vector2D.Zero);
                }

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
                    // TODO: hide hud while tooltips hovered --- or better yet, hide HUD if mouse is over entire text box to aid readability regardless of tooltip presence
                    // problem is text info hides too and flickers, needs some special case...
                    //Main.GameConfig.TempHideHUD(nameof(ScreenTooltips), true);

                    SelectedBox.Origin = found.Value.Area.Center;
                    SelectedBox.Width = found.Value.Area.Width;
                    SelectedBox.Height = found.Value.Area.Height;
                    //SelectedBox.Visible = true;
                    SelectedBox.Draw();

                    TooltipHandler.Hover(found.Value.Text);
                    TooltipHandler.Draw(mousePos, drawNow: true);

                    if(found.Value.Action != null && MyAPIGateway.Input.IsNewLeftMousePressed())
                    {
                        found.Value.Action.Invoke();
                    }
                }
                else
                {
                    //Main.GameConfig.TempHideHUD(nameof(ScreenTooltips), false);
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