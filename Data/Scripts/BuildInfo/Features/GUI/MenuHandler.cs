using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.GUI;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public abstract class Menu
    {
        public virtual void UpdateDraw()
        {
        }
    }

    public class MenuHandler : ModComponent
    {
        ServerInfoMenu _serverInfo;
        public ServerInfoMenu ServerInfo => _serverInfo ?? (_serverInfo = new ServerInfoMenu());

        List<Menu> Menus = new List<Menu>();

        Dictionary<string, Request> CursorRequests = new Dictionary<string, Request>();
        bool InEscapeLoop = false;
        HudAPIv2.BillBoardHUDMessage Cursor;

        public MenuHandler(BuildInfoMod main) : base(main)
        {
            ServerInfoMenu.Test();

            UpdateOrder = 5000;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        public void SetUpdateMenu(Menu menu, bool on)
        {
            if(on)
            {
                if(!Menus.Contains(menu))
                    Menus.Add(menu);
            }
            else
            {
                Menus.Remove(menu);
            }

            RecheckUpdates();
        }

        /// <summary>
        /// Adds (<paramref name="inMenu"/>=true) or removes (<paramref name="inMenu"/>=false) the given id from the input blockers list.
        /// </summary>
        public void AddCursorRequest(string id, Action escapeCallback = null, bool blockViewXY = false, bool blockMoveAndRoll = false, bool unequip = false)
        {
            if(InEscapeLoop)
            {
                Log.Error($"CursorRequest id={id} is being added in escape callback, which will be ignored because it gets cleared after the event!");
                return;
            }

            if(unequip)
            {
                // TODO: find a way to unequip character tools/weapons as well as deselect weapons in cockpits/RC/etc
                // not sure what to do while controlling turrets...

                if(MyCubeBuilder.Static.IsActivated)
                    MyCubeBuilder.Static.Deactivate();
            }

            if(CursorRequests.ContainsKey(id))
            {
                Log.Error($"CursorRequest id={id} was already added!");
                return;
            }

            CursorRequests.Add(id, new Request(escapeCallback, blockViewXY, blockMoveAndRoll));

            if(Cursor != null)
                Cursor.Visible = true;

            RecheckUpdates();
        }

        public void RemoveCursorRequest(string id)
        {
            if(InEscapeLoop)
                return; // silently ignore

            CursorRequests.Remove(id);

            if(Cursor != null && CursorRequests.Count <= 0)
                Cursor.Visible = false;

            RecheckUpdates();
        }

        void RecheckUpdates()
        {
            bool requestedCursor = (CursorRequests.Count > 0);
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, requestedCursor);
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, requestedCursor || (Menus.Count > 0));

            Main.GameConfig.TempHideHUD(nameof(MenuHandler), Menus.Count > 0);
        }

        public override void UpdateDraw()
        {
            if(Menus.Count > 0)
            {
                for(int i = Menus.Count - 1; i >= 0; i--)
                {
                    Menus[i].UpdateDraw();
                }
            }
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(CursorRequests.Count <= 0)
                return;

            if(MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Escape))
            {
                try
                {
                    InEscapeLoop = true; // prevent removals from working which would cause collection modified exception
                    foreach(Request request in CursorRequests.Values)
                    {
                        request.EscapeAction?.Invoke();
                    }
                }
                finally
                {
                    CursorRequests.Clear();
                    if(Cursor != null)
                        Cursor.Visible = false;

                    InEscapeLoop = false;
                    RecheckUpdates();
                }

                return;
            }

            var ctrl = MyAPIGateway.Session.ControlledObject;
            if(ctrl != null)
            {
                bool blockView = false;
                bool blockMove = false;

                foreach(Request request in CursorRequests.Values)
                {
                    blockView |= request.BlockViewXY;
                    blockMove |= request.BlockMovementAndRoll;
                }

                // FIXME: still can move/rotate in spec cam, see if can be fixed

                ctrl.MoveAndRotate(blockMove ? Vector3.Zero : ctrl.LastMotionIndicator,
                                   blockView ? Vector2.Zero : new Vector2(ctrl.LastRotationIndicator.X, ctrl.LastRotationIndicator.Y),
                                   blockMove ? 0 : ctrl.LastRotationIndicator.Z);
            }

            if(Cursor == null)
            {
                // HACK: MouseCursor material is from textAPI
                Cursor = new HudAPIv2.BillBoardHUDMessage(MyStringId.GetOrCompute("MouseCursor"), Vector2D.Zero, Color.White);
                Cursor.Options = HudAPIv2.Options.Pixel | HudAPIv2.Options.HideHud;
                Cursor.SkipLinearRGB = false;

                const float CursorSize = 64;

                Vector2 mouseSizePx = MyAPIGateway.Session.Camera.ViewportSize / MyAPIGateway.Input.GetMouseAreaSize();
                Cursor.Width = mouseSizePx.X * CursorSize;
                Cursor.Height = mouseSizePx.Y * CursorSize;
                Cursor.Offset = new Vector2D(Cursor.Width / -2, Cursor.Height / -2); // textures has center as cursor clicing point, not sure why this offset is necessary but it is

                Cursor.Visible = true;
            }

            // mouse position only updates in HandleInput() so there's no point in moving this later

            Vector2 mousePx = MyAPIGateway.Input.GetMousePosition() / MyAPIGateway.Input.GetMouseAreaSize();
            mousePx *= MyAPIGateway.Session.Camera.ViewportSize;
            Cursor.Origin = mousePx; // in pixels because of the above option flag
        }



        /// <summary>
        /// Scalar (-1 to 1) mouse position for real GUI cursor.
        /// </summary>
        public static Vector2D GetMousePositionGUI()
        {
            Vector2 mousePx = MyAPIGateway.Input.GetMousePosition();

            Vector2 guiSize = MyAPIGateway.Input.GetMouseAreaSize();
            Vector2 mousePosGUI = Vector2.Min(mousePx, guiSize) / guiSize; // pixels to scalar

            return new Vector2D(mousePosGUI.X * 2 - 1, 1 - 2 * mousePosGUI.Y); // turn from 0~1 to -1~1
        }

        struct Request
        {
            public readonly Action EscapeAction;
            public readonly bool BlockViewXY;
            public readonly bool BlockMovementAndRoll;

            public Request(Action escapeAction = null, bool blockViewXY = false, bool blockMovementAndRoll = false)
            {
                EscapeAction = escapeAction;
                BlockViewXY = blockViewXY;
                BlockMovementAndRoll = blockMovementAndRoll;
            }
        }
    }
}
