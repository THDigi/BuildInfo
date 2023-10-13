using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features
{
    public class PickBlock : ModComponent
    {
        public MyCubeBlockDefinition PickedBlockDef { get; private set; }

        IMyHudNotification Notify;

        public PickBlock(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += ToolBlockAimChanged;
            Main.EquipmentMonitor.BuilderAimedBlockChanged += BuilderAimedBlockChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.BlockChanged -= ToolBlockAimChanged;
            Main.EquipmentMonitor.BuilderAimedBlockChanged -= BuilderAimedBlockChanged;
        }

        public void AskToPick(MyCubeBlockDefinition def)
        {
            if(def != null && !CheckDef(def))
                def = null;

            PickedBlockDef = def;
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, (def != null || Main.EquipmentMonitor.AimedBlock != null || Main.EquipmentMonitor.BuilderAimedBlock != null));

            if(def != null)
            {
                ShowText($"Press [Slot number] to place [{PickedBlockDef.DisplayNameText}]. Slot0/Unequip to cancel.", 16 * 20, FontsHandler.WhiteSh);
            }
        }

        bool CheckDef(MyCubeBlockDefinition def)
        {
            if(def == null)
                return true;

            if(!def.Public)
            {
                ShowText($"{def.DisplayNameText} is not available to build.", 3000, FontsHandler.RedSh);
                return false;
            }

            if(def.DLCs != null)
            {
                foreach(string name in def.DLCs)
                {
                    if(!MyAPIGateway.DLC.HasDLC(name, MyAPIGateway.Multiplayer.MyId))
                    {
                        string displayName = name;
                        IMyDLC dlc;
                        if(MyAPIGateway.DLC.TryGetDLC(name, out dlc))
                            displayName = MyTexts.GetString(dlc.DisplayName);

                        ShowText($"{def.DisplayNameText} requires the '{displayName}' DLC to build.", 3000, FontsHandler.RedSh);
                        return false;
                    }
                }
            }

            return true;
        }

        void ToolBlockAimChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, (slimBlock != null || PickedBlockDef != null));
        }

        void BuilderAimedBlockChanged(IMySlimBlock slimBlock)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, (slimBlock != null || PickedBlockDef != null));
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(PickedBlockDef == null)
            {
                IMySlimBlock aimed = Main.EquipmentMonitor.AimedBlock ?? Main.EquipmentMonitor.BuilderAimedBlock;

                if(aimed != null && Main.Config.BlockPickerBind.Value.IsJustPressed())
                {
                    AskToPick((MyCubeBlockDefinition)aimed.BlockDefinition);
                }
            }
            else
            {
                // refresh showing the slot message
                if(!paused && Notify != null && Main.Tick % 10 == 0)
                {
                    Notify.Hide();
                    Notify.Show();
                }

                // waiting for a slot input...
                if(MyAPIGateway.Input.IsAnyCtrlKeyPressed()) // ignore ctrl to allow toolbar page changing
                    return;

                if(MyAPIGateway.Session?.Player == null)
                {
                    AskToPick(null);
                    Utils.ShowColoredChatMessage(BuildInfoMod.ModName, Constants.WarnPlayerIsNull, FontsHandler.RedSh);
                    return;
                }

                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.SLOT0))
                {
                    AskToPick(null);
                    ShowText("Block picking cancelled.", 2000, FontsHandler.WhiteSh);
                    return;
                }

                int slot = 0;
                MyStringId[] controlSlots = Main.Constants.ToolbarSlotControlIds;

                // intentionally skipping last (slot0)
                for(int i = 0; i < controlSlots.Length - 1; ++i)
                {
                    MyStringId controlId = controlSlots[i];

                    if(MyAPIGateway.Input.IsNewGameControlPressed(controlId))
                    {
                        slot = i + 1;
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
                    MyVisualScriptLogicProvider.SetToolbarSlotToItemLocal(slot - 1, PickedBlockDef.Id, MyAPIGateway.Session.Player.IdentityId);

                    ShowText($"[{PickedBlockDef.DisplayNameText}] placed in slot [{slot.ToString()}].", 3000, FontsHandler.GreenSh);

                    AskToPick(null);
                }
            }
        }

        void ShowText(string text, int notifyMs, string font)
        {
            if(Notify == null)
                Notify = MyAPIGateway.Utilities.CreateNotification(string.Empty);

            Notify.Hide();
            Notify.Text = text;
            Notify.AliveTime = notifyMs;
            Notify.Font = font;
            Notify.Show();
        }
    }
}
