using System.Text;
using Digi.BuildInfo.Systems;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class DebugEvents : ClientComponent
    {
        private HudAPIv2.HUDMessage debugEquipmentMsg;

        public DebugEvents(Client mod) : base(mod)
        {
        }

        public override void RegisterComponent()
        {
            //EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            //EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        public override void UnregisterComponent()
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

        private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(TextAPI.WasDetected)
            {
                if(Config.Debug)
                {
                    if(debugEquipmentMsg == null)
                        debugEquipmentMsg = new HudAPIv2.HUDMessage(new StringBuilder(), new Vector2D(-0.2f, 0.98f), Scale: 0.75);

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
    }
}
