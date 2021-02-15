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

        public string CustomLabel;

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

        long CurrentShipControllerId;
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

        public ToolbarMonitor(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_INPUT;

            for(int i = 0; i < Slots.Length; i++)
            {
                Slots[i] = new ToolbarItem(i);
            }
        }

        protected override void RegisterComponent()
        {
            EquipmentMonitor.ShipControllerOBChanged += ToolbarOBChanged;
        }

        protected override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            EquipmentMonitor.ShipControllerOBChanged -= ToolbarOBChanged;
        }

        void ToolbarOBChanged(MyObjectBuilder_ShipController ob)
        {
            // refresh render 1 tick later so that Writer can finish updating the items too.
            Main.ToolbarLabelRender.ForceRefreshAtTick = Main.Tick + 1;

            // can't disable this just for the visual toolbar because status relies on this aswell.
            //if(Main.Config.ToolbarLabels.Value == 0)
            //    return;

            var shipController = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipController != null)
                Main.ToolbarCustomLabels.ParseCustomData(shipController);

            var labelData = (shipController == null ? null : Main.ToolbarCustomLabels.BlockData.GetValueOrDefault(shipController.EntityId, null));

            // reset all slots first
            for(int index = Slots.Length - 1; index >= 0; index--)
            {
                var slot = Slots[index];
                slot.Block = null;
                slot.Name = null;
                slot.ActionWrapper = null;
                slot.ActionId = null;
                slot.ActionName = null;
                slot.GroupName = null;
                slot.PBArgument = null;
                slot.SlotOB = default(MyObjectBuilder_Toolbar.Slot);
                slot.CustomLabel = labelData?.CustomLabels.GetValueOrDefault(index, null);
            }

            HighestIndexUsed = 0;
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
                var item = items[i];
                if(item.Index >= TotalSlots)
                    break; // HACK: gamepad pages can go forever...

                var slotData = Slots[item.Index];

                slotData.SlotOB = item;

                if(DebugLogging)
                    Log.Info($"    {item.Index.ToString(),-4} data={item.Data.GetType().Name,-48} id={item.Data.TypeId.ToString()}/{item.Data.SubtypeName}");

                HighestIndexUsed = Math.Max(HighestIndexUsed, item.Index);

                var terminalItem = item.Data as MyObjectBuilder_ToolbarItemTerminal;
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
                    var groupItem = terminalItem as MyObjectBuilder_ToolbarItemTerminalGroup;
                    if(groupItem != null)
                    {
                        // NOTE: this is always the toolbar host, not the first block in the group!
                        block = MyEntities.GetEntityById(groupItem.BlockEntityId) as IMyTerminalBlock;

                        slotData.GroupName = groupItem.GroupName;
                    }
                    else
                    {
                        var blockItem = terminalItem as MyObjectBuilder_ToolbarItemTerminalBlock;
                        if(blockItem != null)
                        {
                            block = MyEntities.GetEntityById(blockItem.BlockEntityId) as IMyTerminalBlock;
                        }
                    }

                    bool isValid = (block != null);

                    if(DebugLogging)
                        Log.Info($"    ^-- got terminalItem, isValid={isValid.ToString()}; block={block}");

                    // these must only be added for valid toolbar items!
                    if(isValid)
                    {
                        SequencedItems.Add(slotData); // required for status

                        slotData.ActionId = terminalItem._Action;
                        // other fields are set in the wrapper.NewWriter() as they're readily available there
                    }
                }
                else
                {
                    var itemDef = item.Data as MyObjectBuilder_ToolbarItemDefinition;
                    if(itemDef != null)
                    {
                        var def = MyDefinitionManager.Static.GetDefinition(itemDef.DefinitionId);
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

        // Handling toolbar page and action trigger detection
        protected override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            WrapperSlotIndex = 0;

            if(MyAPIGateway.Gui.ChatEntryVisible)
                return;

            var shipController = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipController == null)
                return;

            var screen = MyAPIGateway.Gui.ActiveGamePlayScreen;
            bool inToolbarConfig = screen == "MyGuiScreenCubeBuilder";
            if(!(screen == null || inToolbarConfig)) // toolbar config menu only for cockpit, not for other blocks like timers' action toolbars
                return;

            if(CurrentShipControllerId != shipController.EntityId)
            {
                ToolbarPage = PagePerCockpit.GetValueOrDefault(shipController.EntityId, 0);
                GamepadToolbarPage = GamepadPagePerCockpit.GetValueOrDefault(shipController.EntityId, 0);
                CurrentShipControllerId = shipController.EntityId;
            }

            var controlSlots = Main.Constants.CONTROL_SLOTS;

            for(int i = 1; i < controlSlots.Length; ++i) // intentionally skipping SLOT0
            {
                if(MyAPIGateway.Input.IsNewGameControlPressed(controlSlots[i]))
                {
                    if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
                    {
                        SetToolbarPage(shipController, i - 1);
                    }
                    else
                    {
                        TriggeredIndex = (ToolbarPage * SlotsPerPage) + (i - 1);
                        TriggeredAtTick = Main.Tick + 1; // refresh soon after, not exactly same tick
                        ToolbarSlotTriggered?.Invoke(TriggeredIndex);
                    }
                }
            }

            // HACK next/prev toolbar hotkeys don't work in the menu unless you click on the icons list...
            // spectator condition is in game code because toolbar up/down is used for going between players.
            // also MUST be after the slot checks to match the vanilla code's behavior.
            //if(!inToolbarConfig && MySpectator.Static.SpectatorCameraMovement != MySpectatorCameraMovementEnum.ConstantDelta)
            if(MySpectator.Static.SpectatorCameraMovement != MySpectatorCameraMovementEnum.ConstantDelta)
            {
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOOLBAR_UP))
                {
                    AdjustToolbarPage(shipController, 1);
                }
                // no 'else' because that's how the game handles it, meaning pressing both controls in same tick would do both actions.
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOOLBAR_DOWN))
                {
                    AdjustToolbarPage(shipController, -1);
                }
            }

            if(EnableGamepadSupport && MyAPIGateway.Input.IsJoystickLastUsed && MyAPIGateway.Input.IsAnyNewMouseOrJoystickPressed())
            {
                // selecting slot
                var dpad = Constants.DPAD_NAMES;

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
                    AdjustGamepadToolbarPage(shipController, buttonA ? -1 : 1);
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
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                MyVisualScriptLogicProvider.SetToolbarPageLocal(page);
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
