using System;
using System.Collections.Generic;
using System.IO;
using Digi.BuildInfo.Features.ModderHelp;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    [Flags]
    public enum ConveyorFlags : byte
    {
        None = 0,
        Small = (1 << 0),
        In = (1 << 1),
        Out = (1 << 2),
        Interactive = (1 << 3),
    }

    /// <summary>
    /// Relative to a grid
    /// </summary>
    public struct PortPos
    {
        public Vector3I Position;
        public Base6Directions.Direction Direction;
    }

    public struct ConveyorInfo
    {
        public readonly ConveyorFlags Flags;
        public readonly Matrix LocalMatrix;

        /// <summary>
        /// Relative to block's def.Center!
        /// </summary>
        public readonly Vector3I CellPosition;
        public readonly Base6Directions.Direction Direction;

        public ConveyorInfo(ConveyorFlags flags, Matrix localMatrix, Vector3I cellPos, Base6Directions.Direction dir)
        {
            Utils.MatrixMinSize(ref localMatrix, BData_Base.MatrixAllMinScale, BData_Base.MatrixIndividualMinScale);

            Flags = flags;
            LocalMatrix = localMatrix;
            CellPosition = cellPos;
            Direction = dir;
        }

        public PortPos TransformToGrid(IMySlimBlock block)
        {
            // from MyConveyorConnector.PositionToGridCoords() + optimized with integer transform
            MyBlockOrientation orientation = block.Orientation;
            MatrixI matrix = new MatrixI(block.Position, orientation.Forward, orientation.Up);
            return new PortPos()
            {
                Position = Vector3I.Transform(CellPosition, ref matrix),
                Direction = orientation.TransformDirection(Direction),
            };
        }
    }

    public struct UpgradePortInfo
    {
        public readonly Matrix LocalMatrix;

        /// <summary>
        /// Relative to block's def.Center!
        /// </summary>
        public readonly Vector3I CellPosition;
        public readonly Base6Directions.Direction Direction;

        public UpgradePortInfo(Matrix localMatrix, Vector3I cellPos, Base6Directions.Direction dir)
        {
            Utils.MatrixMinSize(ref localMatrix, BData_Base.MatrixAllMinScale, BData_Base.MatrixIndividualMinScale);

            LocalMatrix = localMatrix;
            CellPosition = cellPos;
            Direction = dir;
        }

        public PortPos TransformToGrid(IMySlimBlock block)
        {
            // from MyConveyorConnector.PositionToGridCoords() + optimized with integer transform
            MyBlockOrientation orientation = block.Orientation;
            MatrixI matrix = new MatrixI(block.Position, orientation.Forward, orientation.Up);
            return new PortPos()
            {
                Position = Vector3I.Transform(CellPosition, ref matrix),
                Direction = orientation.TransformDirection(Direction),
            };
        }
    }

    public struct InteractionInfo
    {
        public readonly Matrix LocalMatrix;
        public readonly string Name;
        public readonly Color Color;

        public InteractionInfo(Matrix localMatrix, string name, Color color)
        {
            Utils.MatrixMinSize(ref localMatrix, BData_Base.MatrixAllMinScale, BData_Base.MatrixIndividualMinScale);

            LocalMatrix = localMatrix;
            Name = name;
            Color = color;
        }
    }

    public struct SubpartInfo
    {
        public readonly string Name;
        public readonly Matrix LocalMatrix;
        public readonly string Model;
        public readonly List<SubpartInfo> Subparts;

        public SubpartInfo(string dummyName, Matrix localMatrix, string model, List<SubpartInfo> subparts)
        {
            Name = dummyName;
            LocalMatrix = localMatrix;
            Model = model;
            Subparts = subparts;
        }
    }

    // Not serialized in any way, can safely change numbers around.
    [Flags]
    public enum BlockHas : byte
    {
        Nothing = 0,

        /// <summary>
        /// Block type supports connecting to conveyor network.
        /// </summary>
        ConveyorSupport = (1 << 0),

        /// <summary>
        /// Block has a terminal (is IMyTerminalBlock)
        /// </summary>
        Terminal = (1 << 1),

        /// <summary>
        /// Block has at least one inventory.
        /// </summary>
        Inventory = (1 << 2),

        /// <summary>
        /// Block model has interactive access to terminal/inventory
        /// </summary>
        PhysicalTerminalAccess = (1 << 3),

        /// <summary>
        /// Block has detector_ownership in the model which enables ownership support (but rather buggy, especially if it's not present in construction model).
        /// </summary>
        OwnershipDetector = (1 << 4),

        ///// <summary>
        ///// Block has custom useobjects or custom gamelogic.
        ///// </summary>
        //CustomLogic = (1 << 5),

        /// <summary>
        /// All conveyor ports on this block are large ports, if flag is missing then it can have mixed or all small.
        /// </summary>
        LargeConveyorPorts = (1 << 6),

        /// <summary>
        /// Block can be turned on/off (is IMyFunctionalBlock)
        /// </summary>
        OnOff = (1 << 7),
    }

    public class BData_Base
    {
        public const float MatrixAllMinScale = 0.2f;
        public const float MatrixIndividualMinScale = 0.05f;

        public BlockHas Has = BlockHas.Nothing;
        public List<ConveyorInfo> ConveyorPorts;
        public List<InteractionInfo> Interactive;
        public List<UpgradePortInfo> UpgradePorts;
        public List<string> Upgrades;
        public List<SubpartInfo> Subparts;
        public float DisassembleRatio = 1f;
        public Vector3 ConveyorVisCenter;

        public BData_Base()
        {
        }

        public bool CheckAndAdd(IMyCubeBlock block) // only gets called if the block IsBuilt
        {
            if(!block.SlimBlock.ComponentStack.IsBuilt)
                Log.Error($"CheckAndAdd() called for a not-IsBuilt block! {block.BlockDefinition} / entId={block.EntityId}");

            if(!Utils.AssertMainThread(false))
                Log.Error($"CheckAndAdd() not on main thread! for block: {block.BlockDefinition} / entId={block.EntityId}");

            MyCubeBlockDefinition def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            if(new BoundingBoxI(block.Min, block.Max).Contains(block.Position) == ContainmentType.Disjoint)
                Log.Error($"Block {def.Id} ({def.Context.GetNameAndId()}) has grid Position outside of boundingbox, this will cause various issues with vanilla and mod scripts!\nContact author to fix (likely the <Center> tag is wrong).");

            DisassembleRatio = block.DisassembleRatio;

            ComputeSubparts(block);
            ComputeUpgrades(block);
            ComputeHas(block);
            ComputeDummies(block, def, dummies);
            //ComputeGamelogic(block, def)

            dummies.Clear();

            ConveyorVisCenter = Vector3.Zero;
            if(ConveyorPorts != null && ConveyorPorts.Count > 0)
            {
                ConveyorVisCenter = Vector3D.Transform(block.CubeGrid.GridIntegerToWorld(block.Position), block.WorldMatrixInvScaled);
            }

            bool isValid = IsValid(block, def);

            // always valid because of the base data and this triggers only if block IsBuilt so it's not a problem with construction stages
            BuildInfoMod.Instance.LiveDataHandler.BlockData.Add(def.Id, this);
            return true;
        }

        protected virtual bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def) => false;

        void ComputeUpgrades(IMyCubeBlock block)
        {
            MyCubeBlock internalBlock = (MyCubeBlock)block;
            if(internalBlock.UpgradeValues.Count > 0)
            {
                Upgrades = new List<string>(internalBlock.UpgradeValues.Count);

                foreach(string upgrade in internalBlock.UpgradeValues.Keys)
                {
                    Upgrades.Add(upgrade);
                }
            }
        }

        void ComputeSubparts(IMyCubeBlock block)
        {
            MyEntity ent = (MyEntity)block;

            if(ent.Subparts != null && ent.Subparts.Count > 0)
            {
                Subparts = new List<SubpartInfo>(ent.Subparts.Count);
                RecursiveSubpartScan(ent, Subparts);
            }
        }

        static void RecursiveSubpartScan(MyEntity entity, List<SubpartInfo> addTo)
        {
            foreach(KeyValuePair<string, MyEntitySubpart> kv in entity.Subparts)
            {
                string dummyName = "subpart_" + kv.Key;
                MyEntitySubpart subpart = kv.Value;
                IMyModel model = (IMyModel)subpart.Model;

                Matrix localMatrix = subpart.PositionComp.LocalMatrixRef;

                SubpartInfo info;
                if(subpart.Subparts != null && subpart.Subparts.Count > 0)
                {
                    info = new SubpartInfo(dummyName, localMatrix, model.AssetName, new List<SubpartInfo>(subpart.Subparts.Count));
                    RecursiveSubpartScan(subpart, info.Subparts);
                }
                else
                {
                    info = new SubpartInfo(dummyName, localMatrix, model.AssetName, null);
                }

                addTo.Add(info);
            }
        }

        void ComputeHas(IMyCubeBlock block)
        {
            MyCubeBlock internalBlock = (MyCubeBlock)block;

            if(BuildInfoMod.Instance.LiveDataHandler.ConveyorSupportTypes.GetValueOrDefault(block.BlockDefinition.TypeId, false))
                Has |= BlockHas.ConveyorSupport;

            if(block is IMyTerminalBlock)
                Has |= BlockHas.Terminal;

            if(block is IMyFunctionalBlock)
                Has |= BlockHas.OnOff;

            if(block.HasInventory)
                Has |= BlockHas.Inventory;

            // HACK: from MyCubeBlock.InitOwnership()
            if(internalBlock.UseObjectsComponent != null && internalBlock.UseObjectsComponent.GetDetectors("ownership").Count > 0)
                Has |= BlockHas.OwnershipDetector;
        }

        void ComputeDummies(IMyCubeBlock block, MyCubeBlockDefinition def, Dictionary<string, IMyModelDummy> dummies)
        {
            if(dummies.Count == 0)
                return;

            if(block?.Model == null)
            {
                //Log.Error($"Block '{def.Id.ToString()}' has a FatBlock but no Model, this will crash when opening info tab on its grid; I recommend you add a <Model> tag to it, even if it's a single tiny triangle model.", Log.PRINT_MESSAGE);
                return;
            }

            const StringComparison CompareType = StringComparison.OrdinalIgnoreCase;

            Interactive = new List<InteractionInfo>(dummies.Values.Count);

            // using grid's actual size in case this is a supergrid'd block, to properly translate into cells
            float cellSize = block.CubeGrid.GridSize; // MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
            float cellSizeHalf = cellSize / 2f;
            Vector3 sizeMetric = new Vector3(def.Size) * cellSizeHalf;

            bool blockTypeHasConveyorSupport = (Has & BlockHas.ConveyorSupport) != 0;
            BuildInfoMod Main = BuildInfoMod.Instance;

            bool hasConveyorsForUnsupported = false;

            foreach(IMyModelDummy dummy in dummies.Values)
            {
                string name = dummy.Name;
                if(!name.StartsWith("detector_", CompareType))
                    continue;

                Matrix matrix = dummy.Matrix;
                matrix.Translation += def.ModelOffset;

                int index = 9; // "detector_".Length
                StringSegment detectorType = GetNextSection(name, ref index); // detector_<here>_small_in
                TextPtr detectorPtr = new TextPtr(detectorType.Text, detectorType.Start);
                StringSegment part1 = GetNextSection(name, ref index); // detector_conveyorline_<here>_in
                StringSegment part2 = GetNextSection(name, ref index); // detector_conveyorline_small_<here>

                if(detectorPtr.StartsWithCaseInsensitive("conveyor"))
                {
                    if(!blockTypeHasConveyorSupport)
                    {
                        hasConveyorsForUnsupported = true;
                        continue;
                    }

                    ConveyorFlags flags = ConveyorFlags.None;

                    // from MyConveyorLine.GetBlockLinePositions()
                    if(part1.EqualsIgnoreCase("small"))
                        flags |= ConveyorFlags.Small;

                    // same logic order: 'out' overrides 'in'
                    if(part1.EqualsIgnoreCase("out") || part2.EqualsIgnoreCase("out"))
                        flags |= ConveyorFlags.Out;
                    else if(part1.EqualsIgnoreCase("in") || part2.EqualsIgnoreCase("in"))
                        flags |= ConveyorFlags.In;

                    if(!detectorPtr.StartsWithCaseInsensitive("conveyorline"))
                    {
                        flags |= ConveyorFlags.Interactive;
                        Has |= BlockHas.PhysicalTerminalAccess;
                    }

                    Vector3 portLocalPos = matrix.Translation + sizeMetric; // + blockDef.ModelOffset  (already done to port matrix)
                    Vector3I clamped = Vector3I.Clamp(Vector3I.Floor(portLocalPos / cellSize), Vector3I.Zero, def.Size - Vector3I.One);
                    Vector3I portCellPos = clamped - def.Center;

                    Vector3 cellV3 = (new Vector3(clamped) + Vector3.Half) * cellSize;
                    Vector3 dirVec = Vector3.DominantAxisProjection((portLocalPos - cellV3) / cellSize);
                    dirVec.Normalize();
                    Base6Directions.Direction portDir = Base6Directions.GetDirection(dirVec);

                    ConveyorInfo port = new ConveyorInfo(flags, matrix, portCellPos, portDir);

                    // some blocks like classic large turrets have their interactive ports as real conveyor ports but they are not reachable because they start from middle cell.
                    {
                        PortPos portPos = port.TransformToGrid(block.SlimBlock);
                        Vector3I connectingToCell = portPos.Position + Base6Directions.GetIntVector(portPos.Direction);
                        IMySlimBlock slimCheck = block.CubeGrid.GetCubeBlock(connectingToCell);
                        if(slimCheck == block.SlimBlock) // connects to itself, mark unreachable
                        {
                            Interactive.Add(new InteractionInfo(matrix, "Inventory/Terminal access", OverlayDrawInstance.InteractiveTerminalColor));
                            Has |= BlockHas.PhysicalTerminalAccess;
                            continue;
                        }
                    }

                    if(ConveyorPorts == null)
                        ConveyorPorts = new List<ConveyorInfo>();

                    ConveyorPorts.Add(port);

                    if((flags & ConveyorFlags.Small) == 0)
                        Has |= BlockHas.LargeConveyorPorts;

                    // less memory to just compute it in overlay, not much added overhead
                    //if((flags & ConveyorFlags.Interactive) != 0)
                    //{
                    //    if((flags & ConveyorFlags.Unreachable) != 0)
                    //    {
                    //        Interactive.Add(new InteractionInfo(matrix, "Inventory/Terminal access", colorTerminalOnly));
                    //    }
                    //    else
                    //    {
                    //        if((flags & ConveyorFlags.Small) != 0)
                    //            Interactive.Add(new InteractionInfo(matrix, "        Interactive\nSmall conveyor port", interactivePortColor));
                    //        else
                    //            Interactive.Add(new InteractionInfo(matrix, "        Interactive\nLarge conveyor port", interactivePortColor));
                    //    }
                    //}
                }
                else if(detectorType.EqualsIgnoreCase("upgrade"))
                {
                    if(UpgradePorts == null)
                        UpgradePorts = new List<UpgradePortInfo>();

                    Vector3 portLocalPos = matrix.Translation + sizeMetric; // + blockDef.ModelOffset  (already done to port matrix)
                    Vector3I clamped = Vector3I.Clamp(Vector3I.Floor(portLocalPos / cellSize), Vector3I.Zero, def.Size - Vector3I.One);
                    Vector3I portCellPos = clamped - def.Center;

                    Vector3 cellV3 = (new Vector3(clamped) + Vector3.Half) * cellSize;
                    Vector3 dirVec = Vector3.DominantAxisProjection((portLocalPos - cellV3) / cellSize);
                    dirVec.Normalize();
                    Base6Directions.Direction portDir = Base6Directions.GetDirection(dirVec);

                    var port = new UpgradePortInfo(matrix, portCellPos, portDir);


                    UpgradePorts.Add(port);
                }
                // from classes that use MyUseObjectAttribute
                else if(detectorType.EqualsIgnoreCase("terminal"))
                {
                    if(Hardcoded.DetectorIsOpenCloseDoor("terminal", block))
                        Interactive.Add(new InteractionInfo(matrix, "Open/Close\n+Terminal access", OverlayDrawInstance.InteractiveActionOrTerminalColor));
                    else
                        Interactive.Add(new InteractionInfo(matrix, "Terminal/inventory access", OverlayDrawInstance.InteractiveTerminalColor));

                    Has |= BlockHas.PhysicalTerminalAccess;
                }
                else if(detectorType.EqualsIgnoreCase("inventory")
                     || detectorType.EqualsIgnoreCase("vendingMachine") // excluding vendingMachineBuy/vendingMachineNext/vendingMachinePrevious; clicking this one just opens terminal
                     || detectorType.EqualsIgnoreCase("jukebox")) // excluding jukeboxNext/jukeboxPrevious/jukeboxPause; clicking this one just opens terminal
                {
                    Interactive.Add(new InteractionInfo(matrix, "Inventory/Terminal access", OverlayDrawInstance.InteractiveTerminalColor));
                }
                else if(detectorType.EqualsIgnoreCase("textpanel"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Edit LCD\n+Terminal access", OverlayDrawInstance.InteractiveActionOrTerminalColor));
                }
                else if(detectorType.EqualsIgnoreCase("advanceddoor")
                     || detectorType.EqualsIgnoreCase("door"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Open/Close\n+Terminal access", OverlayDrawInstance.InteractiveActionOrTerminalColor));
                    Has |= BlockHas.PhysicalTerminalAccess;
                }
                else if(detectorType.EqualsIgnoreCase("block")) // medical room/survival kit heal
                {
                    Interactive.Add(new InteractionInfo(matrix, "Recharge\n+Terminal access", OverlayDrawInstance.InteractiveActionOrTerminalColor));
                    Has |= BlockHas.PhysicalTerminalAccess;
                }
                else if(detectorType.EqualsIgnoreCase("contract"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Open Contracts\n+Terminal access", OverlayDrawInstance.InteractiveActionOrTerminalColor));
                    Has |= BlockHas.PhysicalTerminalAccess;
                }
                else if(detectorType.EqualsIgnoreCase("store"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Open Store\n+Terminal access", OverlayDrawInstance.InteractiveActionOrTerminalColor));
                    Has |= BlockHas.PhysicalTerminalAccess;
                }
                else if(detectorType.EqualsIgnoreCase("ATM"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Open Transactions\n+Terminal access", OverlayDrawInstance.InteractiveActionOrTerminalColor));
                    Has |= BlockHas.PhysicalTerminalAccess;
                }
                // from here only interactive things that can't open terminal
                else if(detectorType.EqualsIgnoreCase("cockpit")
                     || detectorType.EqualsIgnoreCase("cryopod"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Entrance", OverlayDrawInstance.InteractiveColor));
                }
                else if(detectorType.EqualsIgnoreCase("connector"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Connector (large)", OverlayDrawInstance.InteractiveColor));
                }
                else if(detectorType.EqualsIgnoreCase("small") && part1.EqualsIgnoreCase("connector"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Connector (small)", OverlayDrawInstance.InteractiveColor));
                }
                else if(detectorType.EqualsIgnoreCase("wardrobe")
                     || detectorType.EqualsIgnoreCase("ladder")
                     || detectorType.EqualsIgnoreCase("respawn") // medical room/survival kit respawn point
                     || detectorType.EqualsIgnoreCase("jukeboxNext")
                     || detectorType.EqualsIgnoreCase("jukeboxPrevious")
                     || detectorType.EqualsIgnoreCase("jukeboxPause")
                     || detectorType.EqualsIgnoreCase("vendingMachineBuy")
                     || detectorType.EqualsIgnoreCase("vendingMachineNext")
                     || detectorType.EqualsIgnoreCase("vendingMachinePrevious")
                     || detectorType.EqualsIgnoreCase("shiptool")
                     || detectorType.EqualsIgnoreCase("merge")
                     || detectorType.EqualsIgnoreCase("collector")
                     || detectorType.EqualsIgnoreCase("ejector")
                     || detectorType.EqualsIgnoreCase("ladder")
                     || detectorPtr.StartsWithCaseInsensitive("panel_button")
                     || detectorPtr.StartsWithCaseInsensitive("textpanel") // does not match the useobject but it's used in emotioncontroller and does nothing, just ignoring it here
                     || detectorPtr.StartsWithCaseInsensitive("maintenance"))
                {
                    // nothing, just ignoring known dummies so I can find new ones
                }
                else if(BuildInfoMod.IsDevMod)
                {
                    Log.Info($"[DEV] Model for {def.Id.ToString()} has unknown dummy '{name}'.");
                }
            }

            if(hasConveyorsForUnsupported && Main.Config.ModderHelpAlerts.Value && (ModderHelpMain.CheckEverything || def.Context.IsLocal()))
            {
                // skip models in game folder unless we're checking everything
                if(ModderHelpMain.CheckEverything || !Path.IsPathRooted(def.Model))
                {
                    Main.ModderHelpMain.ModHint(def, $"Its model has 'conveyor_' dummies but the block type does not support connecting to conveyor network." +
                                                      "\nDepending on the intent:" +
                                                      "\n- If you want conveyor connection you must pick a compatible block type, see: https://spaceengineers.wiki.gg/wiki/Modding/Reference/SBC/BlockTypeSupport " +
                                                      "\n- If you only want interaction to open inventory, use a 'detector_inventory' dummy instead.");
                }
            }

            dummies.Clear();

            TrimList(ref ConveyorPorts);
            TrimList(ref UpgradePorts);
            TrimList(ref Interactive);
        }

        static void TrimList<T>(ref List<T> list)
        {
            if(list == null)
                return;

            if(list.Count == 0)
            {
                list = null;
                return;
            }

            if(list.Count > list.Capacity)
                list.Capacity = list.Count;
        }

#if false
        static readonly List<IMyUseObject> TempUseObjects = new List<IMyUseObject>();

        void ComputeGamelogic(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            // block has 2+ gamelogic components
            if(block.GameLogic is MyCompositeGameLogicComponent)
            {
                Has |= BlockHas.CustomLogic;
                return;
            }

            if(block.GameLogic != null)
            {
                // HACK: not very reliable but it'll do
                string classFullName = block.GameLogic.GetType().FullName;
                bool vanillaUseObject = classFullName.StartsWith("SpaceEngineers.") || classFullName.StartsWith("Sandbox.") || classFullName.StartsWith("VRage.");

                if(!vanillaUseObject)
                {
                    Has |= BlockHas.CustomLogic;
                    return;
                }
            }

            MyCubeBlock internalBlock = (MyCubeBlock)block;
            MyUseObjectsComponentBase comp = internalBlock.UseObjectsComponent;
            if(comp != null)
            {
                TempUseObjects.Clear();
                comp.GetInteractiveObjects(TempUseObjects);

                if(TempUseObjects.Count > 0)
                {
                    foreach(IMyUseObject useObject in TempUseObjects)
                    {
                        // HACK: not very reliable but it'll do
                        string classFullName = useObject.GetType().FullName;
                        bool vanillaUseObject = classFullName.StartsWith("SpaceEngineers.") || classFullName.StartsWith("Sandbox.") || classFullName.StartsWith("VRage.");

                        if(!vanillaUseObject)
                        {
                            Has |= BlockHas.CustomLogic;
                            break;
                        }
                    }

                    TempUseObjects.Clear();
                }
            }
        }
#endif

        static StringSegment GetNextSection(string text, ref int startIndex)
        {
            if(startIndex >= text.Length)
                return default(StringSegment);

            int sepIndex = text.IndexOf('_', startIndex);
            if(sepIndex > -1)
            {
                StringSegment segment = new StringSegment(text, startIndex, sepIndex - startIndex);
                startIndex = sepIndex + 1;
                return segment;
            }
            else
            {
                StringSegment segment = new StringSegment(text, startIndex, text.Length - startIndex);
                startIndex = text.Length;
                return segment;
            }
        }
    }
}