using System;
using System.Collections.Generic;
using System.Text;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Input;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    public class ToolbarItem
    {
        public readonly int Index;
        public readonly StringBuilder StatusSB = new StringBuilder(64);

        public ActionWrapper ActionWrapper;
        public string ActionId;
        public string ActionName;

        public string GroupName;
        public IMyBlockGroup Group;

        public string CustomLabel;

        public long BlockEntId;
        public IMyTerminalBlock Block;
        public string Name;

        public string PBArgument;

        public MyObjectBuilder_Toolbar.Slot SlotOB;

        public ToolbarItem(int index)
        {
            Index = index;
        }
    }

    /// <summary>
    /// Various data gathering from ship toolbar
    /// </summary>
    public class ToolbarMonitor : ModComponent
    {
        public const int Pages = 9;
        public const int SlotsPerPage = 9;
        public const int SlotsPerPageGamepad = 4; // TODO: gamepad support?
        public const int TotalSlots = SlotsPerPage * Pages;

        public const bool EnableGamepadSupport = false; // HACK: it's very jank

        public const bool DebugLogging = false;

        /// <summary>
        /// Current visible toolbar page, 0 to 8.
        /// </summary>
        public int ToolbarPage { get; private set; }

        /// <summary>
        /// Current visible toolbar page in gamepad HUD, 0 to inf
        /// </summary>
        public int GamepadToolbarPage { get; private set; }

        readonly Dictionary<long, int> PagePerCockpit = new Dictionary<long, int>();
        readonly Dictionary<long, int> GamepadPagePerCockpit = new Dictionary<long, int>();

        public event Action ToolbarPageChanged;
        public event Action<int> ToolbarSlotTriggered;

        /// <summary>
        /// Triggered toolbar slot index (which includes page), 0 to 80
        /// </summary>
        public int TriggeredIndex { get; private set; }
        public int TriggeredAtTick { get; private set; }

        /// <summary>
        /// Current toolbar data per slot.
        /// </summary>
        public readonly ToolbarItem[] Slots = new ToolbarItem[TotalSlots];
        public int HighestIndexUsed;

        public int? SelectedIndex;

        /// <summary>
        /// Valid toolbar slots in sequence, for use by <see cref="ActionWrapper"/>'s Writer.
        /// </summary>
        public List<ToolbarItem> SequencedItems = new List<ToolbarItem>(9);
        public int WrapperSlotIndex;
        public IMyShipController ControlledBlock { get; private set; }

        private List<IMyTerminalBlock> TmpBlocks = new List<IMyTerminalBlock>();

        public ToolbarMonitor(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_INPUT;

            for(int i = 0; i < Slots.Length; i++)
            {
                Slots[i] = new ToolbarItem(i);
            }
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.ControlledChanged += ControlledChanged;
            Main.EquipmentMonitor.ShipControllerOBChanged += ToolbarOBChanged;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.ControlledChanged -= ControlledChanged;
            Main.EquipmentMonitor.ShipControllerOBChanged -= ToolbarOBChanged;
        }

        void ToolbarOBChanged(MyObjectBuilder_ShipController ob)
        {
            // refresh render 1 tick later so that Writer can finish updating the items too.
            Main.ToolbarLabelRender.ForceRefreshAtTick = Main.Tick + 1;
            Main.ToolbarLabelRender.IgnoreTick = Main.Tick;

            // can't disable this just for the visual toolbar because status relies on this aswell.
            //if(Main.Config.ToolbarLabels.Value == 0)
            //    return;

            IMyShipController shipController = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipController != null)
                Main.ToolbarCustomLabels.ParseCustomData(shipController);

            CustomToolbarData labelData = (shipController == null ? null : Main.ToolbarCustomLabels.BlockData.GetValueOrDefault(shipController.EntityId, null));

            // reset all slots first
            for(int index = Slots.Length - 1; index >= 0; index--)
            {
                ToolbarItem slot = Slots[index];
                slot.BlockEntId = 0;
                slot.Block = null;
                slot.Name = null;
                slot.ActionWrapper = null;
                slot.ActionId = null;
                slot.ActionName = null;
                slot.GroupName = null;
                slot.Group = null;
                slot.PBArgument = null;
                slot.SlotOB = default(MyObjectBuilder_Toolbar.Slot);
                slot.CustomLabel = labelData?.CustomLabels.GetValueOrDefault(index, null);
            }

            HighestIndexUsed = -1;
            SequencedItems.Clear();

            if(shipController == null)
            {
                Log.Error($"EquipmentMonitor_ShipControllerOBChanged :: no ship controller?!");
                return;
            }

            if(ob == null || ob.EntityId != shipController.EntityId)
            {
                // likely not gonna happen but leaving it here just to catch those rare cases
                if(ob == null)
                    Log.Error($"EquipmentMonitor.ShipControllerOB is null! Please report exact cockpit circumstances to author!");
                else
                    Log.Error($"EquipmentMonitor.ShipControllerOB is for a different entity! Please report exact cockpit circumstances to author!");

                //ob = shipController.GetObjectBuilderCubeBlock(false) as MyObjectBuilder_ShipController;
                return;
            }

            bool gamepadHUD = EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed;

            List<MyObjectBuilder_Toolbar.Slot> items;
            if(gamepadHUD)
                items = ob?.Toolbar?.SlotsGamepad;
            else
                items = ob?.Toolbar?.Slots;

            if(items == null)
                return;

            SelectedIndex = ob.Toolbar.SelectedSlot;

            if(DebugLogging)
                Log.Info($"---------------------------------------- TOOLBAR FOR {shipController.CustomName} gamepadHUD={gamepadHUD.ToString()} --------------------------");

            for(int i = 0; i < items.Count; i++)
            {
                MyObjectBuilder_Toolbar.Slot item = items[i];
                if(item.Index >= TotalSlots)
                    break; // HACK: gamepad pages can go forever...

                ToolbarItem slotData = Slots[item.Index];

                slotData.SlotOB = item;

                if(DebugLogging)
                    Log.Info($"    {item.Index.ToString(),-4} data={item.Data.GetType().Name,-48} id={item.Data.TypeId.ToString()}/{item.Data.SubtypeName}");

                HighestIndexUsed = Math.Max(HighestIndexUsed, item.Index);

                MyObjectBuilder_ToolbarItemTerminal terminalItem = item.Data as MyObjectBuilder_ToolbarItemTerminal;
                if(terminalItem != null)
                {
                    // HACK: major assumptions here, but there's no other use case and some stuff is prohibited so just w/e
                    if(terminalItem?.Parameters != null && terminalItem.Parameters.Count > 0 && terminalItem._Action == "Run")
                    {
                        string arg = terminalItem.Parameters[0]?.Value;
                        if(!string.IsNullOrEmpty(arg))
                            slotData.PBArgument = arg;

                        if(DebugLogging)
                            Log.Info($"    ^-- got PB arg = '{arg}'");
                    }

                    IMyTerminalBlock block = null;

                    // checking if slot is valid otherwise it'll break the sequence for Writer.
                    MyObjectBuilder_ToolbarItemTerminalGroup groupItem = terminalItem as MyObjectBuilder_ToolbarItemTerminalGroup;
                    if(groupItem != null && groupItem.GroupName != null)
                    {
                        // groupItem.BlockEntityId is only the toolbar host, and we need first block in the group so we're looking for it:
                        IMyTerminalBlock toolbarBlock = MyEntities.GetEntityById(groupItem.BlockEntityId) as IMyTerminalBlock;
                        if(toolbarBlock != null)
                        {
                            TmpBlocks.Clear();
                            IMyGridTerminalSystem gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(toolbarBlock.CubeGrid);
                            IMyBlockGroup group = gts?.GetBlockGroupWithName(groupItem.GroupName);
                            group?.GetBlocks(TmpBlocks);

                            slotData.Group = group;

                            if(TmpBlocks.Count > 0)
                            {
                                long playerId = MyAPIGateway.Session?.Player?.IdentityId ?? 0;

                                // logic from MyToolbarItemTerminalGroup.FirstFunctional()
                                foreach(IMyTerminalBlock b in TmpBlocks)
                                {
                                    if(b.IsFunctional && (b.HasPlayerAccess(playerId) || b.HasPlayerAccess(toolbarBlock.OwnerId)))
                                    {
                                        block = b;
                                        break;
                                    }
                                }

                                if(block == null)
                                    block = TmpBlocks[0];
                            }
                        }

                        slotData.GroupName = groupItem.GroupName;
                    }
                    else
                    {
                        MyObjectBuilder_ToolbarItemTerminalBlock blockItem = terminalItem as MyObjectBuilder_ToolbarItemTerminalBlock;
                        if(blockItem != null)
                        {
                            block = MyEntities.GetEntityById(blockItem.BlockEntityId) as IMyTerminalBlock;
                        }
                    }

                    bool isValid = (block != null);

                    if(DebugLogging)
                        Log.Info($"    ^-- got terminalItem, isValid={isValid.ToString()}; block={block?.CustomName}; action={terminalItem._Action}");

                    // these must only be added for valid toolbar items!
                    if(isValid)
                    {
                        SequencedItems.Add(slotData); // required for status

                        slotData.ActionId = terminalItem._Action;
                        slotData.BlockEntId = block.EntityId;

                        // other fields are set in the wrapper.NewWriter() as they're readily available there
                    }
                }
                else
                {
                    MyObjectBuilder_ToolbarItemDefinition itemDef = item.Data as MyObjectBuilder_ToolbarItemDefinition;
                    if(itemDef != null)
                    {
                        MyDefinitionBase def = MyDefinitionManager.Static.GetDefinition(itemDef.DefinitionId);
                        if(def != null)
                        {
                            slotData.Name = def.DisplayNameText;
                        }
                    }
                }
            }

            if(DebugLogging)
                Log.Info($"---------------------------------------- TOOLBAR END ------------------------------------------");
        }

        void ControlledChanged(VRage.Game.ModAPI.Interfaces.IMyControllableEntity controlled)
        {
            ControlledBlock = controlled as IMyShipController;

            if(ControlledBlock != null)
            {
                ToolbarPage = PagePerCockpit.GetValueOrDefault(ControlledBlock.EntityId, 0);
                GamepadToolbarPage = GamepadPagePerCockpit.GetValueOrDefault(ControlledBlock.EntityId, 0);
            }
            else
            {
                ToolbarPage = 0;
                GamepadToolbarPage = 0;
            }

            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, (ControlledBlock != null));
        }

        // Handling toolbar page and action trigger detection
        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(!paused && DebugLogging)
                Log.Info($"---------------------------------------- INDEX RESET ------------------------------------------");

            WrapperSlotIndex = 0;

            if(MyAPIGateway.Gui.ChatEntryVisible || ControlledBlock == null)
                return;

            // if in menu, only read in cockpit toolbar config
            if(MyAPIGateway.Gui.IsCursorVisible && !Main.GUIMonitor.InToolbarConfig)
                return;

            MyStringId[] controlSlots = Main.Constants.CONTROL_SLOTS;

            for(int i = 1; i < controlSlots.Length; ++i) // intentionally skipping SLOT0
            {
                if(MyAPIGateway.Input.IsNewGameControlPressed(controlSlots[i]))
                {
                    if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
                    {
                        SetToolbarPage(ControlledBlock, i - 1);
                    }
                    else
                    {
                        TriggeredIndex = (ToolbarPage * SlotsPerPage) + (i - 1);
                        TriggeredAtTick = Main.Tick + 1; // refresh soon after, not exactly same tick
                        ToolbarSlotTriggered?.Invoke(TriggeredIndex);
                    }
                }
            }

            // HACK next/prev toolbar hotkeys don't work in the menu unless you click on the icons list... but I'm forcing toolbar to cycle regardless.
            // spectator condition is in game code because toolbar up/down is used for going between players.
            // also MUST be after the slot checks to match the vanilla code's behavior.
            //if(!inToolbarConfig && MySpectator.Static.SpectatorCameraMovement != MySpectatorCameraMovementEnum.ConstantDelta)
            if(!Main.GUIMonitor.InAnyDialogBox && MySpectator.Static.SpectatorCameraMovement != MySpectatorCameraMovementEnum.ConstantDelta)
            {
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOOLBAR_UP))
                {
                    AdjustToolbarPage(ControlledBlock, 1);
                }
                // no 'else' because that's how the game handles it, meaning pressing both controls in same tick would do both actions.
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOOLBAR_DOWN))
                {
                    AdjustToolbarPage(ControlledBlock, -1);
                }
            }

            if(EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed && MyAPIGateway.Input.IsAnyNewMouseOrJoystickPressed())
            {
                // selecting slot
                MyJoystickButtonsEnum[] dpad = Main.Constants.DPAD_NAMES;

                for(int i = 0; i < dpad.Length; ++i)
                {
                    if(MyAPIGateway.Input.IsNewJoystickButtonReleased(dpad[i]))
                    {
                        TriggeredIndex = (GamepadToolbarPage * SlotsPerPageGamepad) + i;
                        TriggeredAtTick = Main.Tick + 1; // refresh soon after, not exactly same tick
                        ToolbarSlotTriggered?.Invoke(TriggeredIndex);
                    }
                }

                // next/prev toolbar
                bool buttonA = MyAPIGateway.Input.IsJoystickButtonNewPressed(MyJoystickButtonsEnum.J01);
                bool buttonB = MyAPIGateway.Input.IsJoystickButtonNewPressed(MyJoystickButtonsEnum.J02);
                if((buttonA || buttonB) && MyAPIGateway.Input.IsJoystickButtonPressed(MyJoystickButtonsEnum.J09))
                {
                    AdjustGamepadToolbarPage(ControlledBlock, buttonA ? -1 : 1);
                }
            }
        }

        void AdjustToolbarPage(IMyShipController shipController, int change)
        {
            int page;
            if(!PagePerCockpit.TryGetValue(shipController.EntityId, out page))
            {
                shipController.OnMarkForClose -= ShipControllerMarkedForClose;
                shipController.OnMarkForClose += ShipControllerMarkedForClose;
            }

            page += (change > 0 ? 1 : -1);

            // loop-around
            if(page > 8)
                page = 0;
            else if(page < 0)
                page = 8;

            PagePerCockpit[shipController.EntityId] = page; // add/update dictionary entry
            ToolbarPage = page;
            ToolbarPageChanged?.Invoke();

            // HACK: ensure the toolbar page is what the code expects, avoids toolbar page desync
            // HACK: needs to be delayed otherwise it jumps more than one page
            int copyPage = page;
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                MyVisualScriptLogicProvider.SetToolbarPageLocal(copyPage);
            });
        }

        void SetToolbarPage(IMyShipController shipController, int page)
        {
            if(!PagePerCockpit.ContainsKey(shipController.EntityId))
            {
                shipController.OnMarkForClose -= ShipControllerMarkedForClose;
                shipController.OnMarkForClose += ShipControllerMarkedForClose;
            }

            page = MathHelper.Clamp(page, 0, 8);

            if(ToolbarPage != page)
            {
                PagePerCockpit[shipController.EntityId] = page; // add/update dictionary entry
                ToolbarPage = page;
                ToolbarPageChanged?.Invoke();
            }
        }

        void AdjustGamepadToolbarPage(IMyShipController shipController, int change)
        {
            int page;
            if(!GamepadPagePerCockpit.TryGetValue(shipController.EntityId, out page))
            {
                shipController.OnMarkForClose -= ShipControllerMarkedForClose;
                shipController.OnMarkForClose += ShipControllerMarkedForClose;
            }

            page += (change > 0 ? 1 : -1);
            page = Math.Max(page, 0); // no upper cap from what I've seen

            GamepadPagePerCockpit[shipController.EntityId] = page; // add/update dictionary entry
            GamepadToolbarPage = page;
            ToolbarPageChanged?.Invoke();
        }

        void ShipControllerMarkedForClose(IMyEntity ent)
        {
            PagePerCockpit.Remove(ent.EntityId);
            GamepadPagePerCockpit.Remove(ent.EntityId);
        }
    }
}
