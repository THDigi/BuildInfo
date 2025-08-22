using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    /// <summary>
    /// Various things while in inventory GUI, currently:
    /// * Flash under helmet icon if interacting with an inventory that doesn't show up in the ship tab (cubeblocks, bags, etc).
    /// 
    /// TODO: test on more resolutions
    /// TODO: find a way to get UI scaling setting
    /// </summary>
    public class InventoryHints : ModComponent
    {
        HudAPIv2.BillBoardHUDMessage HelmetIconSignal;

        float FadeIn;

        public InventoryHints(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.TextAPI.Detected += TextAPI_Detected;
            Main.GUIMonitor.FirstScreenOpen += GUIMonitor_FirstScreenOpen;
            Main.GUIMonitor.LastScreenClose += GUIMonitor_LastScreenClose;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.TextAPI.Detected -= TextAPI_Detected;
            Main.GUIMonitor.FirstScreenOpen -= GUIMonitor_FirstScreenOpen;
            Main.GUIMonitor.LastScreenClose -= GUIMonitor_LastScreenClose;
        }

        void TextAPI_Detected()
        {
            Vector2D pos = new Vector2D(0.058333, 0.537);
            HelmetIconSignal = new HudAPIv2.BillBoardHUDMessage(Constants.MatUI_Square, pos, Color.Lime, HideHud: false);
            HelmetIconSignal.Visible = false;
            HelmetIconSignal.Width = (float)HudAPIv2.APIinfo.ScreenPositionOnePX.X * 64;
            HelmetIconSignal.Height = (float)HudAPIv2.APIinfo.ScreenPositionOnePX.Y * 64;
        }

        void GUIMonitor_FirstScreenOpen()
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
        }

        void GUIMonitor_LastScreenClose()
        {
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
            FadeIn = 0f;
        }

        public override void UpdateDraw()
        {
            if(HelmetIconSignal != null && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.Inventory)
            {
                if(FadeIn < 1f)
                {
                    HelmetIconSignal.BillBoardColor = Color.Lime * FadeIn;

                    const float TransitionSeconds = 0.2f; // MyGuiScreenBase.GetTransitionOpeningTime() is set to 200ms
                    FadeIn += 1f / (60 * TransitionSeconds);

                    if(FadeIn >= 1f)
                        HelmetIconSignal.BillBoardColor = Color.Lime;
                }

                if(MyAPIGateway.Session.GameplayFrameCounter % 40 < 20)
                {
                    var interacted = MyAPIGateway.Gui.InteractedEntity;
                    if(interacted != null && interacted.InventoryCount > 0 && interacted.GetInventory(0).ItemCount > 0)
                    {
                        if(interacted.GetType() == typeof(MyCubeBlock)
                        || interacted is IMyFloatingObject
                        || interacted is IMyInventoryBag)
                        {
                            HelmetIconSignal.Draw();
                        }
                    }
                }
            }
        }
    }
}
