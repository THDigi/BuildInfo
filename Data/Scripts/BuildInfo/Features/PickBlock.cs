using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.BuildInfo.Features
{
    public class PickBlock : ModComponent
    {
        /// <summary>
        /// Setting this will cause the mod to ask for a slot input as to where to place the block, if not null.
        /// </summary>
        public MyCubeBlockDefinition PickedBlockDef
        {
            get { return _blockDef; }
            set
            {
                _blockDef = value;
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, (value != null));
            }
        }
        private MyCubeBlockDefinition _blockDef;

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
            if(PickedBlockDef == null && EquipmentMonitor.AimedBlock != null && Config.BlockPickerBind.Value.IsJustPressed())
            {
                if(!Constants.BLOCKPICKER_IN_MP && MyAPIGateway.Multiplayer.MultiplayerActive)
                {
                    Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, Constants.BLOCKPICKER_DISABLED_CHAT, MyFontEnum.Red);
                    return;
                }

                PickedBlockDef = EquipmentMonitor.BlockDef;
            }

            // waiting for a slot input...
            if(PickedBlockDef != null && !MyAPIGateway.Input.IsAnyCtrlKeyPressed()) // ignore ctrl to allow toolbar page changing
            {
                if(!Constants.BLOCKPICKER_IN_MP && MyAPIGateway.Multiplayer.MultiplayerActive)
                {
                    PickedBlockDef = null;
                    Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, Constants.BLOCKPICKER_DISABLED_CHAT, MyFontEnum.Red);
                    return;
                }

                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.SLOT0))
                {
                    PickedBlockDef = null;
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

                // alternate hardcoded digit key monitor
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

                // alternate numpad key monitor
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
                    MyVisualScriptLogicProvider.SetToolbarSlotToItem(slot - 1, PickedBlockDef.Id, MyAPIGateway.Session.Player.IdentityId);

                    MyAPIGateway.Utilities.ShowNotification($"{PickedBlockDef.DisplayNameText} placed in slot {slot.ToString()}.", 2000, MyFontEnum.Green);

                    PickedBlockDef = null;
                }
            }
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(PickedBlockDef != null && tick % 5 == 0)
            {
                MyAPIGateway.Utilities.ShowNotification($"Press [Slot number] for [{PickedBlockDef.DisplayNameText}] or Slot0/Unequip to cancel.", 16 * 5, MyFontEnum.Blue);
            }
        }

        public void ParseCommand(string msg)
        {
            if(!Constants.BLOCKPICKER_IN_MP && MyAPIGateway.Multiplayer.MultiplayerActive)
            {
                Utils.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, Constants.BLOCKPICKER_DISABLED_CHAT, MyFontEnum.Red);
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
                            Utils.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, $"{EquipmentMonitor.BlockDef.DisplayNameText} placed in slot {slot.ToString()}.", MyFontEnum.Green);
                        }
                        else
                        {
                            Utils.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, $"'{arg}' is not a number from 1 to 9.", MyFontEnum.Red);
                        }

                        return;
                    }
                }

                // if no argument is defined, ask for a number
                PickedBlockDef = EquipmentMonitor.BlockDef;
            }
            else
            {
                Utils.ShowColoredChatMessage(ChatCommands.CMD_GETBLOCK, "Aim at a block with a welder or grinder first.", MyFontEnum.Red);
            }
        }
    }
}
