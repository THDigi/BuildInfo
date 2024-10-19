using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features.GUI;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Engine.Utils;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.Input;
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
        const bool DebugPrint = false;

        ServerInfoMenu _serverInfo;
        public ServerInfoMenu ServerInfo => _serverInfo ?? (_serverInfo = new ServerInfoMenu());

        List<Menu> Menus = new List<Menu>();

        Dictionary<string, Request> CursorRequests = new Dictionary<string, Request>();
        bool InEscapeLoop = false;
        HudAPIv2.BillBoardHUDMessage Cursor;

        MatrixD SpectatorForcedOrientation;

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

            RecheckUpdates("SetUpdateMenu()");
        }

        /// <summary>
        /// Adds (<paramref name="inMenu"/>=true) or removes (<paramref name="inMenu"/>=false) the given id from the input blockers list.
        /// </summary>
        public void AddCursorRequest(string id, Action escapeCallback = null, bool blockMoveAndRoll = false, bool blockViewXY = false, bool blockClicks = false)
        {
            if(DebugPrint)
                DebugLog.PrintHUD(this, $"AddCursorRequest - {id}{(InEscapeLoop ? "; InEscapeLoop" : "")}", log: true);

            if(InEscapeLoop)
            {
                Log.Error($"CursorRequest id={id} is being added in escape callback, which will be ignored because it gets cleared after the event!");
                return;
            }

            if(CursorRequests.ContainsKey(id))
            {
                Log.Error($"CursorRequest id={id} was already added!");
                return;
            }

            if(blockViewXY || blockMoveAndRoll)
            {
                SpectatorForcedOrientation = MySpectator.Static.Orientation;
            }

            CursorRequests.Add(id, new Request()
            {
                EscapeAction = escapeCallback,
                BlockMovementAndRoll = blockMoveAndRoll,
                BlockViewXY = blockViewXY,
                BlockClicks = blockClicks,
            });

            if(Cursor != null)
                Cursor.Visible = true;

            RecheckUpdates("AddCursorRequest()");
        }

        public void RemoveCursorRequest(string id)
        {
            if(DebugPrint)
                DebugLog.PrintHUD(this, $"RemoveCursorRequest - {id}{(InEscapeLoop ? "; InEscapeLoop" : "")}", log: true);

            if(InEscapeLoop)
                return; // silently ignore

            CursorRequests.Remove(id);

            if(Cursor != null && CursorRequests.Count <= 0)
                Cursor.Visible = false;

            RecheckUpdates("RemoveCursorRequest()");
        }

        void RecheckUpdates(string source)
        {
            bool requestedCursor = (CursorRequests.Count > 0);
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, requestedCursor);
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, requestedCursor || (Menus.Count > 0));

            Main.GameConfig.TempHideHUD(nameof(MenuHandler), Menus.Count > 0);

            if(DebugPrint)
                DebugLog.PrintHUD(this, $"RecheckUpdates from {source} --- {(Menus.Count > 0 ? "HIDE HUD!!!" : "show HUD")}", log: true);
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
                    RecheckUpdates("esc key");
                }

                return;
            }

            var ctrl = MyAPIGateway.Session.ControlledObject as Sandbox.Game.Entities.IMyControllableEntity;
            if(ctrl != null)
            {
                bool blockMove = false;
                bool blockView = false;
                bool blockClick = false;

                foreach(Request request in CursorRequests.Values)
                {
                    blockMove |= request.BlockMovementAndRoll;
                    blockView |= request.BlockViewXY;
                    blockClick |= request.BlockClicks;
                }

                if(blockClick)
                {
                    ctrl.SwitchToWeapon(null);
                    ctrl.EndShoot(MyShootActionEnum.PrimaryAction);
                    ctrl.EndShoot(MyShootActionEnum.SecondaryAction);
                    ctrl.EndShoot(MyShootActionEnum.TertiaryAction);

                    if(MyCubeBuilder.Static.IsActivated)
                    {
                        MyCubeBuilder.Static.Deactivate();
                    }
                }

                // TODO fix passenger seat rotates freely...

                bool forceFlip = false; // MyAPIGateway.Input.IsAnyCtrlKeyPressed();
                MySpectatorCameraMovementEnum specMode = MySpectator.Static.SpectatorCameraMovement;
                bool isSpectator = MyAPIGateway.Session.CameraController is MySpectatorCameraController;

                Vector3 motion = ctrl.LastMotionIndicator;
                Vector2 rotation = new Vector2(ctrl.LastRotationIndicator.X, ctrl.LastRotationIndicator.Y);
                float roll = ctrl.LastRotationIndicator.Z;

                // TODO: turret/camera zoom

                if(isSpectator && specMode != MySpectatorCameraMovementEnum.None)
                {
                    if(!MyAPIGateway.Gui.IsCursorVisible)
                    {
                        motion = MyAPIGateway.Input.GetPositionDelta();
                        rotation = MyAPIGateway.Input.GetRotation();
                        roll = MyAPIGateway.Input.GetRoll();

                        MySpectator.Static.MoveAndRotate(blockMove ? -motion : motion,
                                                         blockView ? -rotation : rotation,
                                                         blockMove ? -roll : roll);

                        if(blockMove || blockView)
                        {
                            SpectatorForcedOrientation.Translation = MySpectator.Static.Position;
                            MySpectator.Static.SetViewMatrix(MatrixD.Invert(SpectatorForcedOrientation));
                        }
                    }
                }
                else if(forceFlip || ctrl is IMyLargeTurretBase || ctrl is IMySearchlight)
                {
                    // TODO: fix: movement is always blocked

                    bool rotateShip = MyAPIGateway.Input.IsAnyAltKeyPressed();

                    if(MyAPIGateway.Gui.IsCursorVisible || rotateShip)
                    {
                        ctrl.MoveAndRotate(blockMove ? Vector3.Zero : motion,
                                           blockView ? Vector2.Zero : rotation,
                                           blockMove ? 0 : roll);
                    }
                    else
                    {
                        ctrl.MoveAndRotate(blockMove ? -motion : motion,
                                           blockView ? -rotation : rotation,
                                           blockMove ? 0 : roll);
                    }
                }
                else
                {
                    // TODO: fix: movement is always blocked for blocks (character works fine) 

                    ctrl.MoveAndRotate(blockMove ? Vector3.Zero : motion,
                                       blockView ? Vector2.Zero : rotation,
                                       blockMove ? 0 : roll);
                }
            }

            if(Cursor == null)
            {
                Cursor = new HudAPIv2.BillBoardHUDMessage(Constants.MatUI_TextAPICursor, Vector2D.Zero, Color.White);
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
            public Action EscapeAction;
            public bool BlockMovementAndRoll;
            public bool BlockViewXY;
            public bool BlockClicks;
        }
    }
}
