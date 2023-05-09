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

        /// <summary>
        /// Only gets updated if there's a blocker (and therefore this class draws the cursor).
        /// In textAPI coordinate space (-1 to 1).
        /// </summary>
        public Vector2D MousePosition;

        Dictionary<string, Action> Blockers = new Dictionary<string, Action>();
        List<Menu> Menus = new List<Menu>();
        HudAPIv2.BillBoardHUDMessage Cursor;
        HudState? RevertHud;

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

                const float CursorSize = 64; // in pixels because of the above option flag
                Cursor.Width = CursorSize;
                Cursor.Height = CursorSize;
                Cursor.Offset = new Vector2D(CursorSize / -2); // textures has center as cursor clicing point, not sure why this offset is necessary but it is
            }

            // mouse position only updates in HandleInput() so there's no point in moving this later

            Vector2 mousePx = MyAPIGateway.Input.GetMousePosition();

            Cursor.Origin = mousePx; // in pixels because of the above option flag

            Vector2 screenSize = MyAPIGateway.Session.Camera.ViewportSize;
            Vector2 mousePos = mousePx / screenSize; // pixels to scalar
            MousePosition = new Vector2D(mousePos.X * 2 - 1, 1 - 2 * mousePos.Y); // turn from 0~1 to -1~1
        }
    }
}
