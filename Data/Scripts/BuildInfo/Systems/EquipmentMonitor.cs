using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ModAPI;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Systems
{
    /// <summary>
    /// Monitors the local player's equipped tools, both on character and in ships.
    /// <para>This only cares about currently controlled entities.</para>
    /// <para>Note: designed specifically for BuildInfo, not plug&play.</para>
    /// </summary>
    public class EquipmentMonitor : ComponentBase<Client>
    {
        public event EventHandlerToolChanged ToolChanged;
        public delegate void EventHandlerToolChanged(MyDefinitionId toolDefId);

        public event EventHandlerBlockChanged BlockChanged;
        public delegate void EventHandlerBlockChanged(MyCubeBlockDefinition def, IMySlimBlock block);

        /// <summary>
        /// Update after simulation and after this system computed its things.
        /// All args can be null at the same time.
        /// </summary>
        public event EventHandlerUpdateControlled UpdateControlled;
        public delegate void EventHandlerUpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick);

        /// <summary>
        /// Aimed-at block with ship welder/grinder, null if nothing is aimed at.
        /// </summary>
        public IMySlimBlock AimedBlock { get; private set; }

        /// <summary>
        /// Aimed or equipped block definition, null if nothing is equipped/aimed.
        /// </summary>
        public MyCubeBlockDefinition BlockDef { get; private set; }

        /// <summary>
        /// Grid cell size in meters for the equipped/aimed block.
        /// </summary>
        public float BlockGridSize { get; private set; }

        /// <summary>
        /// Currently equipped character hand-held entity.
        /// </summary>
        public IMyEntity HandEntity { get; private set; }

        /// <summary>
        /// Currently equipped cubeplacer/welder/grinder hand tool, null otherwise.
        /// </summary>
        public IMyEngineerToolBase HandTool { get; private set; }

        /// <summary>
        /// Currently equipped hand drill (because it doesn't implement IMyEngineerToolBase)
        /// </summary>
        public IMyHandDrill HandDrill { get; private set; }

        /// <summary>
        /// Wether the selected tool is a grinder (ship or handheld)
        /// </summary>
        public bool IsAnyGrinder { get; private set; }

        /// <summary>
        /// If cubebuilder is selected (ship or handheld).
        /// </summary>
        public bool IsCubeBuilder { get; private set; }

        /// <summary>
        /// If cubebuilder is selected for ship in particular.
        /// </summary>
        public bool IsCockpitBuildMode { get; private set; }

        /// <summary>
        /// True if hand/ship welder/grinder is selected (no cubebuilder).
        /// </summary>
        public bool IsBuildTool { get; private set; }

        /// <summary>
        /// True if hand/ship welder/grinder/drill/cubebuilder is selected
        /// </summary>
        public bool IsAnyTool { get; private set; }

        /// <summary>
        /// Hand/ship tool definition id, default otherwise.
        /// </summary>
        public MyDefinitionId ToolDefId { get; private set; }

        private IMyEntity prevHeldTool;
        private IMyControllableEntity prevControlled;
        private MyCasterComponent handToolCasterComp;
        private bool closedSomeUI = false;

        public EquipmentMonitor(Client mod) : base(mod)
        {
            Flags = UpdateFlags.UPDATE_AFTER_SIM;
        }

        public override void RegisterComponent()
        {
            MyAPIGateway.Gui.GuiControlRemoved += GUIControlRemoved;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Gui.GuiControlRemoved -= GUIControlRemoved;
        }

        private void GUIControlRemoved(object obj)
        {
            closedSomeUI = true;
        }

        public override void UpdateAfterSim(int tick)
        {
            var controlled = MyAPIGateway.Session.ControlledObject;
            var shipController = controlled as IMyShipController;
            var character = (shipController == null ? controlled as IMyCharacter : null);

            bool controllerChanged = CheckControlledEntity(controlled, shipController, character);

            if(controllerChanged)
                closedSomeUI = false;

            if(shipController != null)
            {
                UpdateInShip(shipController, controllerChanged);
            }
            else if(character != null)
            {
                UpdateInCharacter(character, controllerChanged);
            }

            UpdateControlled?.Invoke(character, shipController, controlled, tick);
        }

        private void UpdateInShip(IMyShipController shipController, bool controllerChanged)
        {
            CheckShipTool(shipController, controllerChanged);

            if(IsBuildTool && !IsCockpitBuildMode)
            {
                var shipCasterComp = shipController.Components.Get<MyCasterComponent>(); // caster comp is added to ship controller by ship tools when character takes control
                SetBlock(null, shipCasterComp?.HitBlock as IMySlimBlock);
            }
        }

        private void UpdateInCharacter(IMyCharacter character, bool controllerChanged)
        {
            CheckHandTool(character, character.EquippedTool, controllerChanged);

            if(!IsCubeBuilder && !IsBuildTool)
                return; // no tool equipped, nothing to update

            var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
            var casterBlock = handToolCasterComp?.HitBlock as IMySlimBlock;

            SetBlock(def, casterBlock);
        }

        private bool CheckControlledEntity(IMyControllableEntity controlled, IMyShipController shipController, IMyCharacter character)
        {
            if(controlled == prevControlled)
                return false; // no changes, ignore

            prevControlled = controlled;

            if(shipController == null)
                CheckShipTool(shipController, true); // unequip ship tool

            if(character == null)
                CheckHandTool(character, null, true); // unequip hand tool

            return true;
        }

        /// <summary>
        /// Checks for the monitored ship tool.
        /// </summary>
        private void CheckShipTool(IMyShipController shipController, bool controllerChanged)
        {
            if(shipController == null)
            {
                SetTool(null);
                SetBlock(null);
                return;
            }

            var internalController = (MyShipController)shipController;

            if(internalController.BuildingMode)
            {
                SetTool(null, null, internalController);
                SetBlock(MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition);
                return;
            }

            bool check = controllerChanged || closedSomeUI; // check tools if controller was just changed or UI closed (because you can change tools in G menu)
            closedSomeUI = false;

            #region Check if any toolbar slot or next/prev was pressed
            if(!check && !MyAPIGateway.Gui.IsCursorVisible && !MyAPIGateway.Gui.ChatEntryVisible)
            {
                // slightly faster than checking individual keys first
                if(MyAPIGateway.Input.IsAnyKeyPress() || MyAPIGateway.Input.IsAnyNewMousePressed())
                {
                    // special case, unequipped tool
                    if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.SLOT0))
                    {
                        SetTool(null);
                        SetBlock(null);
                        return;
                    }

                    if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOOLBAR_NEXT_ITEM)
                    || MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOOLBAR_PREV_ITEM))
                    {
                        if(!MyAPIGateway.Input.IsAnyCtrlKeyPressed()) // ignore toolbar layer changes
                            check = true;
                    }

                    if(!check)
                    {
                        var controlSlots = Mod.Constants.CONTROL_SLOTS;

                        // intentionally skipping SLOT0
                        for(int i = 1; i < controlSlots.Length; ++i)
                        {
                            if(MyAPIGateway.Input.IsNewGameControlPressed(controlSlots[i]))
                            {
                                check = true;
                                break;
                            }
                        }
                    }
                }

                // no FAST shortcut for checking if any joystick button is pressed
                if(!check)
                {
                    // HACK hardcoded gamepad controls for next/prev toolbar items from MySpaceBindingCreator
                    if(MyAPIGateway.Input.IsJoystickButtonNewPressed(MyJoystickButtonsEnum.JDRight)
                    || MyAPIGateway.Input.IsJoystickButtonNewPressed(MyJoystickButtonsEnum.JDLeft))
                    {
                        check = true;
                    }
                }
            }
            #endregion

            if(!check)
                return;

            // HACK find a better way to get selected tool type
            var shipControllerObj = shipController.GetObjectBuilderCubeBlock(false) as MyObjectBuilder_ShipController;
            var toolbar = shipControllerObj?.Toolbar;
            var slotIndex = (toolbar != null && toolbar.SelectedSlot.HasValue ? toolbar.SelectedSlot.Value : -1);

            if(slotIndex >= 0)
            {
                foreach(var slot in toolbar.Slots)
                {
                    if(slot.Index == slotIndex)
                    {
                        var weapon = slot.Data as MyObjectBuilder_ToolbarItemWeapon; // includes tools too

                        if(weapon != null)
                        {
                            SetTool(weapon.DefinitionId);
                            SetBlock(null); // tool changed, reset selected block
                            return; // found a valid tool, stop here.
                        }

                        break; // slot found, exit loop
                    }
                }
            }

            SetTool(null); // no valid tool was found
            SetBlock(null); // tool changed, reset selected block
        }

        /// <summary>
        /// Checks if character's hand tool changed.
        /// </summary>
        private void CheckHandTool(IMyCharacter character, IMyEntity toolEnt, bool controllerChanged)
        {
            if(toolEnt == prevHeldTool)
                return;

            prevHeldTool = toolEnt;

            if(toolEnt != null)
            {
                var casterComp = toolEnt.Components.Get<MyCasterComponent>();

                SetTool(toolEnt, casterComp);
                SetBlock(null); // tool changed, reset selected block
                return;
            }

            SetTool(null); // no valid tool was found
            SetBlock(null); // tool changed, reset selected block
        }

        /// <summary>
        /// Updates the tool if different and invokes the event.
        /// </summary>
        private void SetTool(IMyEntity handEntity, MyCasterComponent casterComp = null, MyShipController shipController = null)
        {
            var defId = default(MyDefinitionId);
            var cockpitBuildMode = (shipController != null && shipController.BuildingMode);

            if(cockpitBuildMode)
            {
                defId = shipController.BlockDefinition.Id;
            }
            else
            {
                var tool = (handEntity as IMyHandheldGunObject<MyDeviceBase>);

                if(tool != null)
                    defId = tool.DefinitionId;
            }

            SetTool(defId, handEntity, casterComp, cockpitBuildMode);
        }

        /// <summary>
        /// Updates the tool if different and invokes the <see cref="ToolChanged"/> event.
        /// </summary>
        private void SetTool(MyDefinitionId defId, IMyEntity handEntity = null, MyCasterComponent casterComp = null, bool cockpitBuildMode = false)
        {
            if(ToolDefId == defId && HandEntity == handEntity)
                return;

            ToolDefId = defId;
            HandEntity = handEntity;
            HandTool = handEntity as IMyEngineerToolBase;
            HandDrill = handEntity as IMyHandDrill;
            handToolCasterComp = casterComp;

            IsCockpitBuildMode = cockpitBuildMode;
            IsCubeBuilder = (cockpitBuildMode || defId.TypeId == typeof(MyObjectBuilder_CubePlacer));
            IsAnyGrinder = (!IsCubeBuilder && (defId.TypeId == typeof(MyObjectBuilder_AngleGrinder) || defId.TypeId == typeof(MyObjectBuilder_ShipGrinder)));
            IsBuildTool = (!IsCubeBuilder && (defId.TypeId == typeof(MyObjectBuilder_AngleGrinder)
                                           || defId.TypeId == typeof(MyObjectBuilder_ShipGrinder)
                                           || defId.TypeId == typeof(MyObjectBuilder_ShipWelder)
                                           || defId.TypeId == typeof(MyObjectBuilder_Welder)));
            IsAnyTool = (IsCubeBuilder || IsBuildTool || defId.TypeId == typeof(MyObjectBuilder_Drill) || defId.TypeId == typeof(MyObjectBuilder_HandDrill));

            ToolChanged?.Invoke(ToolDefId);
        }

        /// <summary>
        /// Updates the block if different and invokes the <see cref="BlockChanged"/> event.
        /// </summary>
        private void SetBlock(MyCubeBlockDefinition def, IMySlimBlock block = null)
        {
            if(def == null)
                def = block?.BlockDefinition as MyCubeBlockDefinition;

            if(BlockDef == def && AimedBlock == block)
                return;

            AimedBlock = block;
            BlockDef = def;
            BlockGridSize = (def == null ? 0 : MyDefinitionManager.Static.GetCubeSize(def.CubeSize));

            BlockChanged?.Invoke(def, block);
        }
    }
}
