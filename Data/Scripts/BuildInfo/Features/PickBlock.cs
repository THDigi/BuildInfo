using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utils;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.BuildInfo.Features
{
    public class PickBlock : ModComponent
    {
        private MyCubeBlockDefinition _blockDef;
        public MyCubeBlockDefinition BlockDef
        {
            get { return _blockDef; }
            set
            {
                _blockDef = value;
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, (value != null));
            }
        }

        public PickBlock(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_INPUT;
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
        }

        protected override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(BlockDef == null && EquipmentMonitor.AimedBlock != null && Config.BlockPickerBind.Value.IsJustPressed())
            {
                // FIXME pick block temporarily disabled in MP
                if(MyAPIGateway.Multiplayer.MultiplayerActive)
                {
                    Utilities.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, "Pick block feature temporarily disabled for MP due to severe issues, see workshop page for details.", MyFontEnum.Red);
                    return;
                }

                BlockDef = EquipmentMonitor.BlockDef;
            }

            // waiting for a slot input...
            if(BlockDef != null && !MyAPIGateway.Input.IsAnyCtrlKeyPressed()) // ignore ctrl to allow toolbar page changing
            {
                // FIXME pick block temporarily disabled in MP
                if(MyAPIGateway.Multiplayer.MultiplayerActive)
                {
                    BlockDef = null;
                    Utilities.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, "Pick block feature temporarily disabled for MP due to severe issues, see workshop page for details.", MyFontEnum.Red);
                    return;
                }

                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.SLOT0))
                {
                    BlockDef = null;
                    MyAPIGateway.Utilities.ShowNotification("Block picking cancelled.", 2000);
                    return;
                }

                int slot = 0;
                var controlSlots = Main.Constants.CONTROL_SLOTS;

                // intentionally skipping 0
                for(int i = 1; i < controlSlots.Length; ++i)
                {
                    var controlId = controlSlots[i];

                    if(MyAPIGateway.Input.IsNewGameControlPressed(controlId))
                    {
                        slot = i;
                        break;
                    }
                }

                //if(slot == 0)
                //{
                //    for(MyKeys k = MyKeys.D1; k <= MyKeys.D9; k++)
                //    {
                //        if(MyAPIGateway.Input.IsKeyPress(k))
                //        {
                //            slot = (k - MyKeys.D0);
                //            break;
                //        }
                //    }
                //}

                //if(slot == 0)
                //{
                //    for(MyKeys k = MyKeys.NumPad1; k <= MyKeys.NumPad9; k++)
                //    {
                //        if(MyAPIGateway.Input.IsKeyPress(k))
                //        {
                //            slot = (k - MyKeys.NumPad0);
                //            break;
                //        }
                //    }
                //}

                if(slot != 0)
                {
                    MyVisualScriptLogicProvider.SetToolbarSlotToItem(slot - 1, BlockDef.Id, MyAPIGateway.Session.Player.IdentityId);

                    MyAPIGateway.Utilities.ShowNotification($"{BlockDef.DisplayNameText} placed in slot {slot.ToString()}.", 2000, MyFontEnum.Green);

                    BlockDef = null;
                }
            }
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(BlockDef != null && tick % 5 == 0)
            {
                MyAPIGateway.Utilities.ShowNotification($"Press SLOT number for '{BlockDef.DisplayNameText}'; or Slot0/Unequip to cancel.", 16 * 5, MyFontEnum.Blue);
            }
        }

        public void ParseCommand(string msg)
        {
            // FIXME pick block temporarily disabled in MP
            if(MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                Utilities.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, "Pick block feature temporarily disabled for MP due to severe issues, see workshop page for details.", MyFontEnum.Red);
                return;
            }

            if(EquipmentMonitor.AimedBlock != null)
            {
                if(msg.Length > ChatCommands.CMD_GETBLOCK.Length)
                {
                    var arg = msg.Substring(ChatCommands.CMD_GETBLOCK.Length);

                    if(!string.IsNullOrWhiteSpace(arg))
                    {
                        int slot;

                        if(int.TryParse(arg, out slot) && slot >= 1 && slot <= 9)
                        {
                            MyVisualScriptLogicProvider.SetToolbarSlotToItem(slot, EquipmentMonitor.BlockDef.Id, MyAPIGateway.Session.Player.IdentityId);
                            Utilities.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, $"{EquipmentMonitor.BlockDef.DisplayNameText} placed in slot {slot.ToString()}.", MyFontEnum.Green);
                        }
                        else
                        {
                            Utilities.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, $"'{arg}' is not a number from 1 to 9.", MyFontEnum.Red);
                        }

                        return;
                    }
                }

                // if no argument is defined, ask for a number
                BlockDef = EquipmentMonitor.BlockDef;
            }
            else
            {
                Utilities.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, "Aim at a block with a welder or grinder first.", MyFontEnum.Red);
            }
        }
    }
}
