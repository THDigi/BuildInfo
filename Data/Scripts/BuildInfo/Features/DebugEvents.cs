using System.Text;
using Digi.BuildInfo.Systems;
using Draygo.API;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class DebugEvents : ModComponent
    {
        public DebugEvents(BuildInfoMod main) : base(main)
        {
            //Flags |= UpdateFlags.UPDATE_INPUT;
            //Flags |= UpdateFlags.UPDATE_DRAW;
        }

        protected override void RegisterComponent()
        {
            //EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            //EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        protected override void UnregisterComponent()
        {
            //EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            //EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
        }

        //private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        //{
        //    if(Config.Debug)
        //        MyAPIGateway.Utilities.ShowNotification($"Equipment.ToolChanged :: {toolDefId}", 1000, MyFontEnum.Green);
        //}

        //private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        //{
        //    if(Config.Debug)
        //        MyAPIGateway.Utilities.ShowNotification($"Equipment.BlockChanged :: {def?.Id.ToString() ?? "Unequipped"}, {(def == null ? "" : (block != null ? "Aimed" : "Held"))}", 1000);
        //}

        private HudAPIv2.HUDMessage debugEquipmentMsg;

        private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(TextAPI.WasDetected)
            {
                if(Config.Debug.Value)
                {
                    if(debugEquipmentMsg == null)
                        debugEquipmentMsg = new HudAPIv2.HUDMessage(new StringBuilder(), new Vector2D(-0.2f, 0.98f), Scale: 0.75, HideHud: false);

                    debugEquipmentMsg.Visible = true;
                    debugEquipmentMsg.Message.Clear().Append($"BuildInfo Debug - Equipment.Update()\n" +
                        $"{(character != null ? "Character" : (shipController != null ? "Ship" : "<color=red>Other<color=white>"))}\n" +
                        $"tool=<color=yellow>{(EquipmentMonitor.ToolDefId == default(MyDefinitionId) ? "NONE" : EquipmentMonitor.ToolDefId.ToString())}\n" +
                        $"<color=white>block=<color=yellow>{EquipmentMonitor.BlockDef?.Id.ToString() ?? "NONE"}");
                }
                else if(debugEquipmentMsg != null && debugEquipmentMsg.Visible)
                {
                    debugEquipmentMsg.Visible = false;
                }
            }
        }

        //private HudAPIv2.HUDMessage debugHudMsg;

        //protected override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        //{
        //    MyAPIGateway.Utilities.ShowMessage("DEBUG", $"HUD={MyAPIGateway.Session.Config.HudState}; MinimalHUD={MyAPIGateway.Session.Config.MinimalHud}");

        //    if(!TextAPI.WasDetected)
        //        return;

        //    if(debugHudMsg == null)
        //        debugHudMsg = new HudAPIv2.HUDMessage(new StringBuilder(), new Vector2D(-0.2f, 0.9f), Scale: 0.75, HideHud: false);

        //    debugHudMsg.Message.Clear().Append($"" +
        //        $"HUD State = {MyAPIGateway.Session.Config.HudState}\n" +
        //        $"MinimalHUD = {MyAPIGateway.Session.Config.MinimalHud}");

        //    if(anyKeyOrMouse && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.L))
        //    {
        //        MyVisualScriptLogicProvider.ShowHud(false);
        //        debugHudMsg.Message.Append("\n<color=red>HIDDEN!!!!!");
        //    }
        //}

        //HudAPIv2.SpaceMessage msg;
        //HudAPIv2.SpaceMessage shadow;

        //protected override void UpdateDraw()
        //{
        //    if(TextAPI.WasDetected)
        //    {
        //        var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
        //        var up = camMatrix.Up;
        //        var left = camMatrix.Left;
        //        var pos = camMatrix.Translation + camMatrix.Forward * 0.2;

        //        double textSize = 0.24;
        //        double shadowOffset = 0.007;

        //        if(msg == null)
        //        {
        //            var offset = new Vector2D(0, -0.05);
        //            msg = new HudAPIv2.SpaceMessage(new StringBuilder("Text"), pos, up, left, textSize, offset, Blend: BlendTypeEnum.SDR);

        //            offset += new Vector2D(shadowOffset, -shadowOffset);
        //            shadow = new HudAPIv2.SpaceMessage(new StringBuilder("<color=black>Text"), pos, up, left, textSize, offset, Blend: BlendTypeEnum.Standard);
        //        }

        //        msg.Up = up;
        //        msg.Left = left;
        //        msg.WorldPosition = pos;
        //        msg.Flush();

        //        //pos += up * -shadowOffset + left * -shadowOffset;

        //        shadow.Up = up;
        //        shadow.Left = left;
        //        shadow.WorldPosition = pos;
        //        shadow.Flush();
        //    }
        //}
    }
}
