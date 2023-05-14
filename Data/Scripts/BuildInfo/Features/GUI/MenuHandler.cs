using System;
using System.Collections.Generic;
using System.Linq;
using Digi.BuildInfo.Features.GUI;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public abstract class Menu
    {
        public virtual void Update()
        {
        }
    }

    public class MenuHandler : ModComponent
    {
        ServerInfoMenu _serverInfo;
        public ServerInfoMenu ServerInfo => _serverInfo ?? (_serverInfo = new ServerInfoMenu());

        Dictionary<string, Action> Blockers = new Dictionary<string, Action>();
        List<Menu> Menus = new List<Menu>();
        HudAPIv2.BillBoardHUDMessage Cursor;
        HudState? RevertHud;

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

        // bad idea xD
        /// <summary>
        /// Scalar (-1 to 1) mouse position for fake GUI/textAPI cursor.
        /// </summary>
        //public static Vector2D GetMousePositionRender()
        //{
        //    Vector2 mousePx = MyAPIGateway.Input.GetMousePosition();
        //
        //    Vector2 renderSize = MyAPIGateway.Session.Camera.ViewportSize;
        //    Vector2 mousePos3D = Vector2.Min(mousePx, renderSize) / renderSize; // pixels to scalar
        //
        //    return new Vector2D(mousePos3D.X * 2 - 1, 1 - 2 * mousePos3D.Y); // turn from 0~1 to -1~1
        //}

        public MenuHandler(BuildInfoMod main) : base(main)
        {
            ServerInfoMenu.Test();
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            RevertHUDBack();
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
        public void SetInMenu(string id, bool inMenu, Action escapeCallback = null)
        {
            if(inMenu)
                Blockers.Add(id, escapeCallback);
            else
                Blockers.Remove(id);

            RecheckUpdates();
        }

        void RecheckUpdates()
        {
            bool inMenu = (Blockers.Count > 0);
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, inMenu);

            if(!inMenu && Cursor != null)
            {
                // HACK: doing this so cursor is always created over any fake UI
                Cursor?.DeleteMessage();
                Cursor = null;
            }

            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, (Menus.Count > 0));

            if(Menus.Count > 0)
            {
                if(RevertHud == null)
                {
                    RevertHud = Main.GameConfig.HudState;
                    MyVisualScriptLogicProvider.SetHudState((int)HudState.OFF, playerId: 0); // playerId=0 shorcircuits to calling it locally
                }
            }
            else
            {
                RevertHUDBack();
            }
        }

        void RevertHUDBack()
        {
            if(RevertHud != null)
            {
                MyVisualScriptLogicProvider.SetHudState((int)RevertHud, playerId: 0);
                RevertHud = null;
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            for(int i = Menus.Count - 1; i >= 0; i--)
            {
                Menus[i].Update();
            }
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(Blockers.Count <= 0)
                return;

            if(MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Escape))
            {
                try
                {
                    List<Action> copy = Blockers.Values.ToList(); // HACK: copying it because it might get modified 
                    foreach(Action action in copy)
                    {
                        action?.Invoke();
                    }
                }
                finally
                {
                    Blockers.Clear();
                    RecheckUpdates();
                }

                return;
            }

            var ctrl = MyAPIGateway.Session.ControlledObject;
            if(ctrl != null)
            {
                ctrl.MoveAndRotate(Vector3.Zero, Vector2.Zero, 0);
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
            }

            // mouse position only updates in HandleInput() so there's no point in moving this later

            Vector2 mousePx = MyAPIGateway.Input.GetMousePosition() / MyAPIGateway.Input.GetMouseAreaSize();
            mousePx *= MyAPIGateway.Session.Camera.ViewportSize;
            Cursor.Origin = mousePx; // in pixels because of the above option flag
        }
    }
}
