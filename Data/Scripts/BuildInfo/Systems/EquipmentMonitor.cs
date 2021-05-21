using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ModAPI;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Systems
{
    /// <summary>
    /// Monitors the local player's equipped tools, both on character and in ships.
    /// <para>This only cares about currently controlled entities.</para>
    /// <para>Note: designed specifically for BuildInfo, not plug&play.</para>
    /// </summary>
    public class EquipmentMonitor : ModComponent
    {
        public event EventHandlerToolChanged ToolChanged;
        public delegate void EventHandlerToolChanged(MyDefinitionId toolDefId);

        public event EventHandlerBlockChanged BlockChanged;
        public delegate void EventHandlerBlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock);

        /// <summary>
        /// Update after simulation and after this system computed its things.
        /// All args can be null at the same time.
        /// </summary>
        public event EventHandlerUpdateControlled UpdateControlled;
        public delegate void EventHandlerUpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick);

        public event EventHandlerControlledChanged ControlledChanged;
        public delegate void EventHandlerControlledChanged(IMyControllableEntity controlled);

        /// <summary>
        /// Aimed-at block with ship welder/grinder, null if nothing is aimed at.
        /// </summary>
        public IMySlimBlock AimedBlock { get; private set; }

        /// <summary>
        /// If aimed block is projected, this is the source, otherwise null.
        /// </summary>
        public IMyProjector AimedProjectedBy { get; private set; }

        /// <summary>
        /// If aiming also intersects a projected grid, returns projector hosting it.
        /// </summary>
        public IMyProjector NearbyProjector { get; private set; }

        /// <summary>
        /// If aimed block is projected this will indicate its can-build status.
        /// </summary>
        public BuildCheckResult AimedProjectedCanBuild { get; private set; }

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
        /// Wether the selected tool is a welder (ship or handheld)
        /// </summary>
        public bool IsAnyWelder { get; private set; }

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

        /// <summary>
        /// WARNING: This is only for optimization in reducing OB calls, has quirks!
        /// It doesn't get reliably nulled, nullcheck and test entityId matching before using.
        /// Advantage is that it does get updated when UI gets closed, controller changes or toolbar navigates.
        /// </summary>
        public MyObjectBuilder_ShipController ShipControllerOB { get; private set; }
        public event Action<MyObjectBuilder_ShipController> ShipControllerOBChanged;

        private IMyEntity prevHeldTool;
        private IMyControllableEntity prevControlled;
        private MyCasterComponent handToolCasterComp;
        private bool closedSomeUI = false;
        private int recheckOBAtTick = 0;
        private readonly List<MyEntity> Entities = new List<MyEntity>();

        public EquipmentMonitor(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM;
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

            if(controllerChanged)
                ControlledChanged?.Invoke(controlled);

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
                return; // no tool equipped, nothing to update; block gets nullified on tool change

            var equippedDef = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

            if(IsCubeBuilder && equippedDef != null)
            {
                SetBlock(equippedDef, null);
                return;
            }

            var aimBlock = handToolCasterComp?.HitBlock as IMySlimBlock;

            if(!Main.Config.SelectAllProjectedBlocks.Value)
            {
                SetBlock(null, aimBlock);
                return;
            }

            // this execution order (until the end of the method) was extracted from the game code
            if(CanWeld(aimBlock))
            {
                SetBlock(null, aimBlock);
                return;
            }

            if(Main.Tick % 4 != 0) // 15fps
                return;

            IMySlimBlock targetProjectedBlock;
            BuildCheckResult targetProjectedStatus;
            IMySlimBlock closestProjectedBlock;
            BuildCheckResult closestProjectedStatus;
            IMyProjector projector;
            FindProjectedBlock(character, out targetProjectedBlock, out targetProjectedStatus, out closestProjectedBlock, out closestProjectedStatus, out projector);

            if(targetProjectedBlock != null)
            {
                SetBlock(null, targetProjectedBlock, targetProjectedStatus, nearbyProjector: projector);
                return;
            }

            if(aimBlock != null)
            {
                SetBlock(null, aimBlock, nearbyProjector: projector);
                return;
            }

            if(closestProjectedBlock != null)
            {
                SetBlock(null, closestProjectedBlock, closestProjectedStatus, nearbyProjector: projector);
                return;
            }

            SetBlock(null, null, nearbyProjector: projector);
        }

        bool CanWeld(IMySlimBlock block)
        {
            if(block == null)
                return false;

            if(!Utils.CheckSafezoneAction(block, Utils.SZAWelding))
                return false;

            if(!block.IsFullIntegrity || block.HasDeformation)
                return true;

            return false;
        }

        // HACK hardcoded from MyWelder.FindProjectedBlock()
        readonly List<Vector3I> cells = new List<Vector3I>();
        readonly List<IMySlimBlock> blocks = new List<IMySlimBlock>(); // must be list to preserve insert order
        bool FindProjectedBlock(IMyCharacter character, out IMySlimBlock targetBlock, out BuildCheckResult targetStatus, out IMySlimBlock closestBlock, out BuildCheckResult closestStatus, out IMyProjector projector)
        {
            targetBlock = null;
            targetStatus = BuildCheckResult.NotFound;
            closestBlock = null;
            closestStatus = BuildCheckResult.NotFound;
            projector = null;

            if(handToolCasterComp == null || HandTool?.PhysicalItemDefinition == null)
                return false;

            var weaponPosition = character?.Components?.Get<MyCharacterWeaponPositionComponent>();
            if(weaponPosition == null)
                return false;

            var handItemDef = MyDefinitionManager.Static.TryGetHandItemForPhysicalItem(HandTool.PhysicalItemDefinition.Id) as MyEngineerToolBaseDefinition;

            float reachDistance = Hardcoded.EngineerToolBase_DefaultReachDistance * (handItemDef == null ? 1f : handItemDef.DistanceMultiplier);

            // hardcoded from MyEngineerToolBase.UpdateSensorPosition()
            MatrixD worldMatrix = MatrixD.CreateTranslation(weaponPosition.LogicalPositionWorld);
            worldMatrix.Right = character.WorldMatrix.Right;
            worldMatrix.Forward = weaponPosition.LogicalOrientationWorld;
            worldMatrix.Up = Vector3.Cross(worldMatrix.Right, worldMatrix.Forward);

            Vector3D forward = worldMatrix.Forward;
            Vector3D start = worldMatrix.Translation; // + forward * originOffset;
            Vector3D end = start + forward * reachDistance;

            LineD line = new LineD(start, end);
            MyCubeGrid grid;
            Vector3I discardVec;
            double discardDouble;
            if(!MyCubeGrid.GetLineIntersection(ref line, out grid, out discardVec, out discardDouble, (g) => g.Projector != null))
                return false;

            projector = grid?.Projector as IMyProjector;
            if(projector == null)
                return false;

            // inlined RayCastBlocksAllOrdered(grid, start, end, blocks);
            blocks.Clear();
            grid.RayCastCells(start, end, cells, clearOutHitPositions: true);

            for(int i = 0; i < cells.Count; ++i)
            {
                IMySlimBlock slim = grid.GetCubeBlock(cells[i]) as IMySlimBlock;
                if(slim == null || blocks.Contains(slim))
                    continue;

                BuildCheckResult canBuild = projector.CanBuild(slim, checkHavokIntersections: true);

                if(closestBlock == null && !projector.ShowOnlyBuildable)
                {
                    closestBlock = slim;
                    closestStatus = canBuild;
                }

                if(canBuild == BuildCheckResult.OK)
                {
                    targetBlock = slim;

                    // HACK: doesn't check for welding, also must be here because it must target the same thing as vanilla.
                    if(!Utils.CheckSafezoneAction(slim, Utils.SZAWelding))
                        canBuild = BuildCheckResult.IntersectedWithSomethingElse;

                    targetStatus = canBuild;
                    break;
                }

                blocks.Add(slim);
            }

            blocks.Clear();
            return true;
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

            int tick = Main.Tick;
            bool check = recheckOBAtTick == tick || controllerChanged || closedSomeUI; // check tools if controller was just changed or UI closed (because you can change tools in G menu)
            closedSomeUI = false;

            if(!check && Features.ToolbarInfo.ToolbarMonitor.EnableGamepadSupport && Main.Tick % 60 == 0)
                check = true;

            bool isInput = false;

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
                        {
                            check = true;
                            isInput = true;
                        }
                    }

                    if(!check)
                    {
                        var controlSlots = Main.Constants.CONTROL_SLOTS;

                        // intentionally skipping SLOT0
                        for(int i = 1; i < controlSlots.Length; ++i)
                        {
                            if(MyAPIGateway.Input.IsNewGameControlPressed(controlSlots[i]))
                            {
                                check = true;
                                isInput = true;
                                break;
                            }
                        }
                    }
                }

                // no fast shortcut for checking if any joystick button is pressed
                if(MyAPIGateway.Input.IsJoystickLastUsed && MyAPIGateway.Input.IsAnyNewMouseOrJoystickPressed())
                {
                    if(!check)
                    {
                        // selecting slot
                        if(MyAPIGateway.Input.IsNewJoystickButtonReleased(MyJoystickButtonsEnum.JDRight)
                        || MyAPIGateway.Input.IsNewJoystickButtonReleased(MyJoystickButtonsEnum.JDLeft)
                        || MyAPIGateway.Input.IsNewJoystickButtonReleased(MyJoystickButtonsEnum.JDDown)
                        || MyAPIGateway.Input.IsNewJoystickButtonReleased(MyJoystickButtonsEnum.JDUp))
                        {
                            check = true;
                            isInput = true;
                        }
                    }

                    if(!check)
                    {
                        // next/prev toolbar
                        if((MyAPIGateway.Input.IsJoystickButtonNewPressed(MyJoystickButtonsEnum.J01) || MyAPIGateway.Input.IsJoystickButtonNewPressed(MyJoystickButtonsEnum.J02))
                        && MyAPIGateway.Input.IsJoystickButtonPressed(MyJoystickButtonsEnum.J09))
                        {
                            check = true;
                            isInput = true;
                        }
                    }
                }
            }
            #endregion Check if any toolbar slot or next/prev was pressed

            if(!check)
                return;

            // TODO: find a better way to get selected tool type
            ShipControllerOB = shipController.GetObjectBuilderCubeBlock(false) as MyObjectBuilder_ShipController;
            ShipControllerOBChanged?.Invoke(ShipControllerOB);

            // HACK: recheck a few ticks after an input press
            if(!isInput && !MyAPIGateway.Multiplayer.IsServer)
            {
                recheckOBAtTick = tick + 10;
            }

            // FIXME: components can't detect selected tool if world starts with a selected tool (it is set, just components aren't aware)

            var selectedToolId = ShipControllerOB.SelectedGunId;
            if(selectedToolId.HasValue)
            {
                SetTool(selectedToolId.Value);
                SetBlock(null); // tool changed, reset selected block
                return; // found a valid tool, stop here.
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
            IsAnyWelder = (!IsCubeBuilder && (defId.TypeId == typeof(MyObjectBuilder_Welder) || defId.TypeId == typeof(MyObjectBuilder_ShipWelder)));
            IsAnyGrinder = (!IsCubeBuilder && (defId.TypeId == typeof(MyObjectBuilder_AngleGrinder) || defId.TypeId == typeof(MyObjectBuilder_ShipGrinder)));
            IsBuildTool = (IsAnyWelder || IsAnyGrinder);
            IsAnyTool = (IsCubeBuilder || IsBuildTool || defId.TypeId == typeof(MyObjectBuilder_Drill) || defId.TypeId == typeof(MyObjectBuilder_HandDrill));

            if(IsCubeBuilder)
                handToolCasterComp = null;

            ToolChanged?.Invoke(ToolDefId);
        }

        /// <summary>
        /// Updates the block if different and invokes the <see cref="BlockChanged"/> event.
        /// </summary>
        private void SetBlock(MyCubeBlockDefinition def, IMySlimBlock block = null, BuildCheckResult projectedCanBuild = BuildCheckResult.NotFound, IMyProjector nearbyProjector = null)
        {
            if(def == null)
                def = block?.BlockDefinition as MyCubeBlockDefinition;

            // these change independent of block so must always be updated
            AimedProjectedCanBuild = projectedCanBuild;

            if(BlockDef == def && AimedBlock == block)
                return;

            AimedBlock = block;
            AimedProjectedBy = null;
            NearbyProjector = nearbyProjector;

            if(block != null)
            {
                var internalGrid = (MyCubeGrid)block.CubeGrid;
                AimedProjectedBy = internalGrid?.Projector;
                if(AimedProjectedBy != null)
                    NearbyProjector = AimedProjectedBy;
            }

            BlockDef = def;
            BlockGridSize = (def == null ? 0 : (block != null ? block.CubeGrid.GridSize : MyDefinitionManager.Static.GetCubeSize(def.CubeSize)));

            // if no projector was detected by the other means, try again
            if(NearbyProjector == null && block != null)
            {
                BoundingBoxD bb;
                block.GetWorldBoundingBox(out bb);

                Entities.Clear();
                MyGamePruningStructure.GetTopMostEntitiesInBox(ref bb, Entities, MyEntityQueryType.Both);

                for(int i = 0; i < Entities.Count; i++)
                {
                    var grid = Entities[i] as MyCubeGrid;
                    if(grid == null || grid.Projector == null || grid.Projector.CubeGrid != block.CubeGrid)
                        continue;

                    NearbyProjector = grid.Projector;
                    break;
                }

                Entities.Clear();
            }

            BlockChanged?.Invoke(def, block);
        }
    }
}
