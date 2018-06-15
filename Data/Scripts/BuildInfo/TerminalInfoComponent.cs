using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Blocks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRageMath;

using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum; // HACK allows the use of BlendTypeEnum which is whitelisted but bypasses accessing MyBillboard which is not whitelisted
using MyShipConnectorStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

namespace Digi.BuildInfo
{
    public class TerminalInfoComponent
    {
        private BuildInfo mod;
        private IMyTerminalBlock viewedInTerminal;
        private int delayCursorCheck = 0;

        private CustomInfoCall currentFormatCall;
        private delegate void CustomInfoCall(IMyTerminalBlock block, StringBuilder info);
        private readonly Dictionary<MyObjectBuilderType, CustomInfoCall> formatLookup
                   = new Dictionary<MyObjectBuilderType, CustomInfoCall>(MyObjectBuilderType.Comparer);

        private readonly HashSet<MyDefinitionId> ignoreModBlocks = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        private readonly HashSet<long> longSetTemp = new HashSet<long>();

        private MyResourceSinkComponent _sinkCache = null;
        private MyResourceSourceComponent _sourceCache = null;
        private IMyInventory _invCache = null;
        private IMyInventory _inv2Cache = null;

        // on-demand self-caching component getters for the currently viewed block
        private MyResourceSinkComponent Sink => (_sinkCache = _sinkCache ?? viewedInTerminal.Components.Get<MyResourceSinkComponent>());
        private MyResourceSourceComponent Source => (_sourceCache = _sourceCache ?? viewedInTerminal.Components.Get<MyResourceSourceComponent>());
        private IMyInventory Inv => (_invCache = _invCache ?? viewedInTerminal.GetInventory(0));
        private IMyInventory Inv2 => (_inv2Cache = _inv2Cache ?? viewedInTerminal.GetInventory(1));

        private void ClearCaches()
        {
            _sinkCache = null;
            _sourceCache = null;
            _invCache = null;
            _inv2Cache = null;
        }

        public TerminalInfoComponent(BuildInfo mod)
        {
            this.mod = mod;

            MyAPIGateway.Utilities.RegisterMessageHandler(BuildInfo.MOD_API_ID, ModMessageReceived);

            MyAPIGateway.TerminalControls.CustomControlGetter += TerminalCustomControlGetter;

            #region Registering formats
            formatLookup[typeof(MyObjectBuilder_InteriorLight)] = Format_LightingBlock;
            formatLookup[typeof(MyObjectBuilder_ReflectorLight)] = Format_LightingBlock;

            formatLookup[typeof(MyObjectBuilder_Door)] = Format_Doors;
            formatLookup[typeof(MyObjectBuilder_AdvancedDoor)] = Format_Doors;
            formatLookup[typeof(MyObjectBuilder_AirtightDoorGeneric)] = Format_Doors;
            formatLookup[typeof(MyObjectBuilder_AirtightHangarDoor)] = Format_Doors;
            formatLookup[typeof(MyObjectBuilder_AirtightSlideDoor)] = Format_Doors;

            formatLookup[typeof(MyObjectBuilder_CargoContainer)] = Format_CargoContainer;

            formatLookup[typeof(MyObjectBuilder_ConveyorSorter)] = Format_ConveyorSorter;

            formatLookup[typeof(MyObjectBuilder_ShipWelder)] = Format_ShipWelder;

            formatLookup[typeof(MyObjectBuilder_ShipGrinder)] = Format_ShipGrinder;

            formatLookup[typeof(MyObjectBuilder_PistonBase)] = Format_Piston; // this one is actually ancient and unused?
            formatLookup[typeof(MyObjectBuilder_ExtendedPistonBase)] = Format_Piston;

            formatLookup[typeof(MyObjectBuilder_ShipConnector)] = Format_Connector;

            formatLookup[typeof(MyObjectBuilder_MotorAdvancedStator)] = Format_Rotor;
            formatLookup[typeof(MyObjectBuilder_MotorStator)] = Format_Rotor;
            formatLookup[typeof(MyObjectBuilder_MotorSuspension)] = Format_Rotor;

            formatLookup[typeof(MyObjectBuilder_TimerBlock)] = Format_TimerBlock;

            formatLookup[typeof(MyObjectBuilder_SoundBlock)] = Format_SoundBlock;

            formatLookup[typeof(MyObjectBuilder_ButtonPanel)] = Format_ButtonPanel;

            formatLookup[typeof(MyObjectBuilder_TurretBase)] = Format_Weapons;
            formatLookup[typeof(MyObjectBuilder_ConveyorTurretBase)] = Format_Weapons;
            formatLookup[typeof(MyObjectBuilder_SmallGatlingGun)] = Format_Weapons;
            formatLookup[typeof(MyObjectBuilder_SmallMissileLauncher)] = Format_Weapons;
            formatLookup[typeof(MyObjectBuilder_SmallMissileLauncherReload)] = Format_Weapons;
            formatLookup[typeof(MyObjectBuilder_InteriorTurret)] = Format_Weapons;
            formatLookup[typeof(MyObjectBuilder_LargeGatlingTurret)] = Format_Weapons;
            formatLookup[typeof(MyObjectBuilder_LargeMissileTurret)] = Format_Weapons;

            // nothing useful to add, it also has a huge detail info text when a projection is loaded
            //blockTypeFormat[typeof(MyObjectBuilder_Projector)] = Format_Projector;
            //blockTypeFormat[typeof(MyObjectBuilder_ProjectorBase)] = Format_Projector;

            formatLookup[typeof(MyObjectBuilder_OreDetector)] = Format_OreDetector;

            formatLookup[typeof(MyObjectBuilder_Parachute)] = Format_Parachute;

            formatLookup[typeof(MyObjectBuilder_GasTank)] = Format_GasTank;
            formatLookup[typeof(MyObjectBuilder_OxygenTank)] = Format_GasTank;

            formatLookup[typeof(MyObjectBuilder_Cockpit)] = Format_Seats;
            formatLookup[typeof(MyObjectBuilder_CryoChamber)] = Format_Seats;

            formatLookup[typeof(MyObjectBuilder_RemoteControl)] = Format_RemoteControl;

            // not needed, already contains current power usage, sort of
            //blockTypeFormat[typeof(MyObjectBuilder_Gyro)] = Format_Gyro;

            formatLookup[typeof(MyObjectBuilder_Thrust)] = Format_Thruster;

            formatLookup[typeof(MyObjectBuilder_Collector)] = Format_Collector;

            formatLookup[typeof(MyObjectBuilder_Reactor)] = Format_Reactor;

            formatLookup[typeof(MyObjectBuilder_Refinery)] = Format_Production;
            formatLookup[typeof(MyObjectBuilder_Assembler)] = Format_Production;

            formatLookup[typeof(MyObjectBuilder_UpgradeModule)] = Format_UpgradeModule;

            formatLookup[typeof(MyObjectBuilder_MedicalRoom)] = Format_MedicalRoom;

            formatLookup[typeof(MyObjectBuilder_OxygenGenerator)] = Format_GasGenerator;

            formatLookup[typeof(MyObjectBuilder_OxygenFarm)] = Format_OxygenFarm;

            formatLookup[typeof(MyObjectBuilder_AirVent)] = Format_AirVent;

            formatLookup[typeof(MyObjectBuilder_RadioAntenna)] = Format_RadioAntenna;

            formatLookup[typeof(MyObjectBuilder_LaserAntenna)] = Format_LaserAntenna;

            formatLookup[typeof(MyObjectBuilder_Beacon)] = Format_Beacon;
            #endregion
        }

        /// <summary>
        /// This must be called when the world is unloaded.
        /// </summary>
        public void Close()
        {
            mod = null;

            MyAPIGateway.Utilities.UnregisterMessageHandler(BuildInfo.MOD_API_ID, ModMessageReceived);

            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalCustomControlGetter;
        }

        /// <summary>
        /// This must be called in a session component's simulation update method (doesn't matter if it's before, after or during simulation)
        /// </summary>
        public void Update()
        {
            if(powerSourcesCooldown > 0)
                powerSourcesCooldown--;

            if(viewedInTerminal == null)
                return;

            // check if the block is still valid or if the player exited the menu
            // HACK IsCursorVisible reacts slowly to the menu being opened, an ignore period is needed
            if(viewedInTerminal.Closed || ((delayCursorCheck == 0 || --delayCursorCheck == 0) && !MyAPIGateway.Gui.IsCursorVisible))
            {
                ViewedBlockChanged(viewedInTerminal, null);
                return;
            }

            //if(mod.Tick % 6 == 0 && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel) // only actively refresh if viewing the block list
            //{
            //    // HACK: RefreshCustomInfo() doesn't update the detail info panel in realtime; bugreport: SE-7777
            //    viewedInTerminal.RefreshCustomInfo();
            //
            //    // HACK: force refresh terminal UI by changing a property
            //    // ISSUE: causes network traffic
            //    // ISSUE: prevents scrolling in drop down lists
            //    // ISSUE: due to it being sync'd, it causes the above issues to other players as well.
            //    viewedInTerminal.ShowInToolbarConfig = !viewedInTerminal.ShowInToolbarConfig;
            //    viewedInTerminal.ShowInToolbarConfig = !viewedInTerminal.ShowInToolbarConfig;
            //}
        }

        public void Draw()
        {
            //DrawTest();
        }

        // EXPERIMENT: clickable UI element for manual refresh of detail info panel
#if false
        private Draygo.API.HudAPIv2.HUDMessage debugText = null;
        int clickCooldown = 0;

        void DrawTest()
        {
            if(clickCooldown > 0)
                clickCooldown--;

            if(viewedInTerminal == null || MyAPIGateway.Gui.GetCurrentScreen != MyTerminalPageEnum.ControlPanel)
                return;

            var cam = MyAPIGateway.Session.Camera;
            var camMatrix = cam.WorldMatrix;

            var hudPosStart = new Vector2(0.62f, 0.57f);
            var hudPosEnd = new Vector2(0.84f, 0.91f);
            var hudPos = hudPosStart + ((hudPosEnd - hudPosStart) * 0.5f);
            var hudSize = new Vector2(0.02f, 0.02f);

            var worldPos = mod.HudToWorld(hudPos, textAPIcoords: false);
            var worldSize = hudSize * mod.ScaleFOV;

            var mouseArea = MyAPIGateway.Input.GetMouseAreaSize();
            var mousePos = MyAPIGateway.Input.GetMousePosition();
            var mouseHudPos = mousePos / mouseArea;

            // DEBUG draw
            if(mod.TextAPIEnabled)
            {
                if(debugText == null)
                    debugText = new Draygo.API.HudAPIv2.HUDMessage(new StringBuilder(), new Vector2D(0.75, -0.9));

                debugText.Message.Clear().Append($"mouseArea={mouseArea.X:0.00},{mouseArea.Y:0.00}\nmousePos={mousePos.X:0.00},{mousePos.Y:0.00}\nmouseHudPos={mouseHudPos.X:0.00},{mouseHudPos.Y:0.00}");
            }

            var color = Color.Red;

            // DEBUG TODO - it's bad on aspect ratios and resolutions
            if(mouseHudPos.X >= hudPosStart.X && mouseHudPos.X <= hudPosEnd.X
            && mouseHudPos.Y >= hudPosStart.Y && mouseHudPos.Y <= hudPosEnd.Y)
            {
                if(MyAPIGateway.Input.IsLeftMousePressed())
                {
                    color = Color.Lime;

                    if(clickCooldown == 0)
                    {
                        clickCooldown = 10;

                        viewedInTerminal.RefreshCustomInfo();

                        // HACK
                        viewedInTerminal.ShowInToolbarConfig = !viewedInTerminal.ShowInToolbarConfig;
                        viewedInTerminal.ShowInToolbarConfig = !viewedInTerminal.ShowInToolbarConfig;
                    }
                }
                else
                {
                    color = Color.Yellow;
                }
            }

            MyTransparentGeometry.AddBillboardOriented(mod.MATERIAL_VANILLA_SQUARE, color * 0.3f, worldPos, camMatrix.Left, camMatrix.Up, worldSize.X, worldSize.Y, Vector2.Zero, blendType: BlendTypeEnum.SDR);
        }
#endif

        // Gets called when local client clicks on a block in the terminal.
        // Used to know the currently viewed block in the terminal.
        void TerminalCustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if(block == viewedInTerminal) // clicked same block
            {
                viewedInTerminal?.RefreshCustomInfo();
                return;
            }

            ViewedBlockChanged(viewedInTerminal, block);
        }

        void ViewedBlockChanged(IMyTerminalBlock oldBlock, IMyTerminalBlock newBlock)
        {
            if(oldBlock != null)
            {
                oldBlock.AppendingCustomInfo -= CustomInfo;
            }

            viewedInTerminal = null;
            currentFormatCall = null;

            if(newBlock != null)
            {
                if(!formatLookup.TryGetValue(newBlock.BlockDefinition.TypeId, out currentFormatCall))
                    return; // ignore blocks that don't need stats

                viewedInTerminal = newBlock;

                delayCursorCheck = 10;

                fullScan = true;
                powerSourcesCooldown = 0; // remove cooldown to instantly rescan
                powerSources.Clear();

                ClearCaches(); // block changed so caches are no longer relevant

                newBlock.AppendingCustomInfo += CustomInfo;
                newBlock.RefreshCustomInfo(); // invokes AppendingCustomInfo 
            }
        }

        // Called by AppendingCustomInfo's invoker: RefreshCustomInfo()
        void CustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            try
            {
                if(currentFormatCall == null)
                    return;

                // Append other mod's info after my own.
                // This is possible since this event is surely executed last as it's hooked when block is clicked
                //   and because the same StringBuiler is given to all mods.
                var otherModInfo = (info.Length > 0 ? info.ToString() : null);

                info.Clear();
                currentFormatCall.Invoke(block, info);
                info.TrimEndWhitespace();

                if(otherModInfo != null)
                {
                    info.Append('\n');
                    info.Append(otherModInfo);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
                info?.Append($"\n[ {Log.ModName} ERROR; SEND GAME LOG! ]");
            }
        }

        #region Text formatting per block type
        void Format_LightingBlock(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_InputPower(Sink);
        }

        void Format_Doors(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_InputPower(Sink);
        }

        void Format_OreDetector(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_InputPower(Sink);
        }

        void Format_TimerBlock(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      <timer status>

            info.DetailInfo_InputPower(Sink);
        }

        void Format_SoundBlock(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_InputPower(Sink);
        }

        void Format_CargoContainer(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_Inventory(Inv, GameData.Hardcoded.CargoContainer_InventoryVolume((MyCubeBlockDefinition)block.SlimBlock.BlockDefinition));
        }

        void Format_ConveyorSorter(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Current Input: <n> W

            var sorterDef = (MyConveyorSorterDefinition)block.SlimBlock.BlockDefinition;

            info.DetailInfo_MaxPowerUsage(Sink);
            info.DetailInfo_Inventory(Inv, sorterDef.InventorySize.Volume);
        }

        void Format_Connector(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_InputPower(Sink);

            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;
            float volume;

            if(BuildInfo.GetInventoryFromComponent(def, out volume))
            {
                info.DetailInfo_Inventory(Inv, volume);
            }
            else
            {
                info.DetailInfo_Inventory(Inv, GameData.Hardcoded.ShipConnector_InventoryVolume(def));
            }

            var data = BData_Base.TryGetDataCached<BData_Connector>(def);

            if(data != null && data.Connector)
            {
                var connector = (IMyShipConnector)block;

                if(connector.Status == MyShipConnectorStatus.Connectable)
                {
                    info.Append("Status: Ready to connect\n");
                    info.Append("Target: ").Append(connector.OtherConnector.CustomName).Append('\n');
                    info.Append("Ship: ").Append(connector.OtherConnector.CubeGrid.CustomName).Append('\n');
                }
                else if(connector.Status == MyShipConnectorStatus.Connected)
                {
                    info.Append("Status: Connected\n");
                    info.Append("Target: ").Append(connector.OtherConnector.CustomName).Append('\n');
                    info.Append("Ship: ").Append(connector.OtherConnector.CubeGrid.CustomName).Append('\n');
                }
                else
                {
                    info.Append("Status: Not connected\n");
                    info.Append("Target: ").Append("N/A").Append('\n');
                    info.Append("Ship: ").Append("N/A").Append('\n');
                }
            }
        }

        void Format_Weapons(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_InputPower(Sink);
            info.DetailInfo_Inventory(Inv, GameData.Hardcoded.CargoContainer_InventoryVolume((MyCubeBlockDefinition)block.SlimBlock.BlockDefinition));

            var gun = (IMyGunObject<MyGunBase>)block;
            int mags = gun.GetAmmunitionAmount();
            int totalAmmo = gun.GunBase.GetTotalAmmunitionAmount();

            info.Append("Ammo: ").Append(totalAmmo).Append(" (").Append(mags).Append(" mags)").Append('\n');
            info.Append("Magazine: ").Append(gun.GunBase.CurrentAmmoMagazineDefinition.DisplayNameText).Append('\n');
        }

        void Format_Production(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W
            //      Current Input: <n> W
            //      Productivity: <n>%
            //      Effectiveness: <n>%
            //      Power efficiency: <n>%
            //      Used upgrade module slots: <n> / <n>

            info.Append('\n');

            var productionDef = (MyProductionBlockDefinition)block.SlimBlock.BlockDefinition;
            var volume = (productionDef.InventoryMaxVolume > 0 ? productionDef.InventoryMaxVolume : productionDef.InventorySize.Volume);
            info.DetailInfo_Inventory(Inv, volume, "Inventory In");
            info.DetailInfo_Inventory(Inv2, volume, "Inventory Out");

            info.Append('\n');

            var production = (IMyProductionBlock)block;
            var assembler = block as IMyAssembler;

            if(assembler != null)
            {
                info.Append("Mode: ").Append(assembler.Mode).Append('\n');
                info.Append("Loop queue: ").Append(assembler.Repeating ? "On" : "Off").Append('\n');
            }

            if(production.IsQueueEmpty)
            {
                info.Append("Queue: (empty)\n");
            }
            else
            {
                info.Append("Queue: ").Append(production.IsProducing ? "Working..." : "STOPPED").Append('\n');

                var queue = production.GetQueue();

                for(int i = 0; i < queue.Count; ++i)
                {
                    var item = queue[i];

                    info.Append("• ").Number((float)item.Amount).Append("x ").Append(item.Blueprint.DisplayNameText).Append('\n');
                }
            }
        }

        void Format_UpgradeModule(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            var upgradeModule = (IMyUpgradeModule)block;

            if(upgradeModule.UpgradeCount == 0) // probably a platform for something else and not an actual upgrade module, therefore skip
                return;

            var upgradeModuleDef = (MyUpgradeModuleDefinition)block.SlimBlock.BlockDefinition;

            info.Append("Connections:");

            if(upgradeModule.Connections > 0)
            {
                info.Append('\n');

                // HACK since upgrade module doesn't expose what blocks it's connected to, I'll look for nearby blocks that have this upgrade module listed in their upgrades.
                longSetTemp.Clear();
                var nearbyBlocks = upgradeModule.SlimBlock.Neighbours;

                foreach(var nearSlim in nearbyBlocks)
                {
                    if(nearSlim?.FatBlock == null)
                        continue;

                    if(longSetTemp.Contains(nearSlim.FatBlock.EntityId)) // already processed this item
                        continue;

                    longSetTemp.Add(nearSlim.FatBlock.EntityId);

                    var nearCube = (MyCubeBlock)nearSlim.FatBlock;

                    if(nearCube.CurrentAttachedUpgradeModules == null)
                        continue;

                    foreach(var module in nearCube.CurrentAttachedUpgradeModules.Values)
                    {
                        if(module.Block == upgradeModule)
                        {
                            var name = ((nearCube as IMyTerminalBlock)?.CustomName ?? nearCube.DisplayNameText);
                            info.Append("• ").Append(module.SlotCount).Append("x ").Append(name);

                            if(!module.Compatible)
                                info.Append(" (incompatible)");

                            info.Append('\n');
                            break;
                        }
                    }
                }

                longSetTemp.Clear();
            }
            else
            {
                info.Append(" (none)\n");
            }

            info.Append("\nUpgrades per slot:\n");

            List<MyUpgradeModuleInfo> upgrades;
            upgradeModule.GetUpgradeList(out upgrades);

            foreach(var item in upgrades)
            {
                info.Append("• ").AppendUpgrade(item).Append('\n');
            }
        }

        void Format_OxygenFarm(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W
            //      Oxygen output: <n> L/min

            info.DetailInfo_CurrentPowerUsage(Sink);
        }

        void Format_Seats(IMyTerminalBlock block, StringBuilder info)
        {
            var cockpit = (IMyCockpit)block;

            // Vanilla info in 1.186.5:
            //     [Main ship cockpit: <name>]

            // TODO charging power stats?

            if(cockpit.OxygenCapacity > 0)
                info.Append("Oxygen: ").ProportionToPercent(cockpit.OxygenFilledRatio).Append(" (").VolumeFormat(cockpit.OxygenCapacity * cockpit.OxygenFilledRatio).Append(" / ").VolumeFormat(cockpit.OxygenCapacity).Append(')').Append('\n');

            Suffix_ShipController(block, info);
        }

        void Format_RemoteControl(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W

            info.DetailInfo_CurrentPowerUsage(Sink);

            Suffix_ShipController(block, info);
        }

        #region ShipController extra stuff
        void Suffix_ShipController(IMyTerminalBlock block, StringBuilder info)
        {
            if(Inv != null)
            {
                info.DetailInfo_Inventory(Inv, GameData.Hardcoded.Cockpit_InventoryVolume);
            }

            var def = (MyShipControllerDefinition)block.SlimBlock.BlockDefinition;

            if(def.EnableShipControl)
            {
                var internalController = (MyShipController)block;
                var distributor = internalController.GridResourceDistributor;
                var state = distributor.ResourceStateByType(MyResourceDistributorComponent.ElectricityId);
                var required = distributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
                var available = distributor.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId);

                FindPowerSources(block.CubeGrid);

                info.Append("\nShip power statistics:\n");
                info.Append("  Status: ");

                switch(state)
                {
                    case MyResourceStateEnum.NoPower: info.Append("No power!"); break;
                    case MyResourceStateEnum.Ok: info.Append("OK"); break;
                    case MyResourceStateEnum.OverloadAdaptible: info.Append("Minor Overload!"); break;
                    case MyResourceStateEnum.OverloadBlackout: info.Append("Heavy Overload!"); break;
                }

                info.Append('\n');

                info.Append("  Total required: ").PowerFormat(required).Append('\n');
                info.Append("  Total available: ").PowerFormat(available).Append('\n');

                info.Append("  Reactors: ");
                if(reactors == 0)
                    info.Append("N/A\n");
                else
                    info.Append(reactorsWorking).Append(" of ").Append(reactors).Append(" working\n");

                info.Append("  Batteries: ");
                if(batteries == 0)
                    info.Append("N/A\n");
                else
                    info.Append(batteriesWorking).Append(" of ").Append(batteries).Append(" working\n");

                info.Append("  Solar Panels: ");
                if(solarPanels == 0)
                    info.Append("N/A\n");
                else
                    info.Append(solarPanelsWorking).Append(" of ").Append(solarPanels).Append(" working\n");

                if(otherSources > 0)
                    info.Append("  Other power sources: ").Append(otherSourcesWorking).Append(" of ").Append(otherSources).Append(" working\n");
            }
        }

        private int powerSourcesCooldown = 0;
        private bool fullScan = true;

        private int reactors = 0;
        private int reactorsWorking = 0;

        private int batteries = 0;
        private int batteriesWorking = 0;

        private int solarPanels = 0;
        private int solarPanelsWorking = 0;

        private int otherSources = 0;
        private int otherSourcesWorking = 0;

        private List<IMyTerminalBlock> powerSources = new List<IMyTerminalBlock>();

        private void FindPowerSources(IMyCubeGrid grid)
        {
            if(powerSourcesCooldown > 0)
                return;

            powerSourcesCooldown = 60 * 3;

            if(fullScan)
            {
                reactors = 0;
                reactorsWorking = 0;
                batteries = 0;
                batteriesWorking = 0;
                solarPanels = 0;
                solarPanelsWorking = 0;
                otherSources = 0;
                otherSourcesWorking = 0;
                powerSources.Clear();

                // TODO avoid GTS?
                var gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                gts.GetBlocksOfType(powerSources, ComputePowerSourceBlock);
            }
            else
            {
                reactorsWorking = 0;
                batteriesWorking = 0;
                solarPanelsWorking = 0;
                otherSourcesWorking = 0;

                foreach(var block in powerSources)
                {
                    var source = block.Components.Get<MyResourceSourceComponent>();
                    bool working = (source.CurrentOutputByType(MyResourceDistributorComponent.ElectricityId) > 0);

                    if(!working)
                        continue;

                    if(block is IMyReactor)
                        reactorsWorking++;
                    else if(block is IMyBatteryBlock)
                        batteriesWorking++;
                    else if(block is IMySolarPanel)
                        solarPanelsWorking++;
                    else
                        otherSourcesWorking++;
                }
            }
        }

        private bool ComputePowerSourceBlock(IMyTerminalBlock block)
        {
            var source = block.Components.Get<MyResourceSourceComponent>();

            if(source == null)
                return false;

            foreach(var res in source.ResourceTypes)
            {
                if(res == MyResourceDistributorComponent.ElectricityId)
                {
                    bool working = (source.CurrentOutputByType(MyResourceDistributorComponent.ElectricityId) > 0);

                    if(block is IMyReactor)
                    {
                        reactors++;

                        if(working)
                            reactorsWorking++;
                    }
                    else if(block is IMyBatteryBlock)
                    {
                        batteries++;

                        if(working)
                            batteriesWorking++;
                    }
                    else if(block is IMySolarPanel)
                    {
                        solarPanels++;

                        if(working)
                            solarPanelsWorking++;
                    }
                    else
                    {
                        otherSources++;

                        if(working)
                            otherSourcesWorking++;
                    }

                    return true;
                }
            }

            return false;
        }
        #endregion

        void Format_Rotor(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Current angle or status

            info.DetailInfo_InputPower(Sink);
        }

        void Format_Piston(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Current position

            info.DetailInfo_InputPower(Sink);
        }

        void Format_AirVent(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W
            //      Room pressure: <status>

            info.DetailInfo_CurrentPowerUsage(Sink);
        }

        void Format_Reactor(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Max Output: <n> W
            //      Current Output: <n> W

            if(Inv == null)
                return;

            info.Append('\n');

            var reactorDef = (MyReactorDefinition)block.SlimBlock.BlockDefinition;
            var maxVolume = (reactorDef.InventoryMaxVolume > 0 ? reactorDef.InventoryMaxVolume : reactorDef.InventorySize.Volume);

            if(maxVolume <= 0)
                BuildInfo.GetInventoryFromComponent(reactorDef, out maxVolume);

            info.DetailInfo_Inventory(Inv, maxVolume);

            float kgPerSec = GameData.Hardcoded.Reactor_KgPerSec(Source, reactorDef);
            float seconds = (kgPerSec > 0 ? ((float)Inv.CurrentMass / kgPerSec) : 0);

            info.Append('\n');
            info.Append("Requires fuel: ").Append(reactorDef.FuelId.SubtypeName).Append('\n');
            info.Append("Current kg/s: ").AppendFormat("{0:0.##########}", kgPerSec).Append('\n');
            info.Append("Current time left: ").TimeFormat(seconds).Append('\n');
        }

        void Format_Thruster(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W

            var thrust = (IMyThrust)block;
            var thrustInternal = (MyThrust)block;
            var def = thrustInternal.BlockDefinition;

            float currentPowerUsage = thrustInternal.MinPowerConsumption + ((thrustInternal.MaxPowerConsumption - thrustInternal.MinPowerConsumption) * (thrust.CurrentThrust / thrust.MaxThrust));
            float maxPowerUsage = thrustInternal.MaxPowerConsumption;

            info.Append('\n');

            if(thrustInternal.FuelDefinition != null && thrustInternal.FuelDefinition.Id != MyResourceDistributorComponent.ElectricityId)
            {
                // HACK formula from MyEntityThrustComponent.PowerAmountToFuel()
                float eff = (thrustInternal.FuelDefinition.EnergyDensity * thrustInternal.FuelConverterDefinition.Efficiency);
                float currentFuelUsage = currentPowerUsage / eff;
                float maxFuelUsage = maxPowerUsage / eff;

                info.Append("Requires: ").Append(thrustInternal.FuelDefinition.Id.SubtypeName).Append('\n');
                info.Append("Current Usage: ").VolumeFormat(currentFuelUsage).Append("/s").Append('\n');
                info.Append("Max Usage: ").VolumeFormat(maxFuelUsage).Append("/s").Append('\n');
            }
            else
            {
                info.Append("Requires: Electricity").Append('\n');
                info.Append("Current Usage: ").PowerFormat(currentPowerUsage).Append('\n');
                info.Append("Max Usage: ").PowerFormat(maxPowerUsage).Append('\n');
            }

            info.Append('\n');

            if(def.EffectivenessAtMinInfluence < 1.0f || def.EffectivenessAtMaxInfluence < 1.0f)
            {
                info.Append("Current effective Thrust: ").ForceFormat(thrust.MaxEffectiveThrust).Append('\n');
                info.Append("Optimal Thrust: ").ForceFormat(thrust.MaxThrust).Append('\n');

                info.Append("Atmospheric effects:").Append('\n');
                info.Append(' ').ProportionToPercent(def.EffectivenessAtMaxInfluence).Append(" max thrust ");
                if(def.MaxPlanetaryInfluence < 1f)
                    info.Append("in ").ProportionToPercent(def.MaxPlanetaryInfluence).Append(" atmosphere");
                else
                    info.Append("in atmosphere");
                info.Append('\n');

                info.Append(' ').ProportionToPercent(def.EffectivenessAtMinInfluence).Append(" max thrust ");
                if(def.MinPlanetaryInfluence > 0f)
                    info.Append("below ").ProportionToPercent(def.MinPlanetaryInfluence).Append(" atmosphere");
                else
                    info.Append("in space");
                info.Append('\n');
            }
            else
            {
                info.Append("Max Thrust: ").ForceFormat(thrust.MaxThrust).Append('\n');
                info.Append("No effect from atmosphere or ");
            }
        }

        void Format_RadioAntenna(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Current Input: <n> W

            var def = (MyRadioAntennaDefinition)block.SlimBlock.BlockDefinition;

            info.Append("Max Power Usage: ").PowerFormat(GameData.Hardcoded.RadioAntenna_PowerReq(def.MaxBroadcastRadius)).Append('\n');
        }

        void Format_LaserAntenna(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Current Input: <n> W
            //      <LaserAntennaStatus>

            var antenna = (IMyLaserAntenna)block;
            var def = (MyLaserAntennaDefinition)block.SlimBlock.BlockDefinition;

            info.Append('\n');
            info.Append("Power Usage:\n");

            info.Append("  Current: ").PowerFormat(Sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId)).Append('\n');

            info.Append("  At Range: ");
            if(antenna.Range < 1E+08f)
                info.PowerFormat(GameData.Hardcoded.LaserAntenna_PowerUsage(def, antenna.Range));
            else
                info.Append("Infinite.");
            info.Append('\n');

            info.Append("  Max: ");
            if(def.MaxRange > 0)
                info.PowerFormat(GameData.Hardcoded.LaserAntenna_PowerUsage(def, def.MaxRange));
            else
                info.Append("Infinite.");
            info.Append('\n');
        }

        void Format_Beacon(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Current Input: <n> W

            var def = (MyBeaconDefinition)block.SlimBlock.BlockDefinition;

            info.Append("Max Power Usage: ").PowerFormat(GameData.Hardcoded.Beacon_PowerReq(def.MaxBroadcastRadius)).Append('\n');
        }

        void Format_GasGenerator(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W

            info.DetailInfo_CurrentPowerUsage(Sink);
            info.DetailInfo_OutputGasList(Source);
        }

        void Format_GasTank(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W
            //      Filled: <n>% (<current>L / <max>L)

            info.DetailInfo_CurrentPowerUsage(Sink);
            info.DetailInfo_InputGasList(Sink);
            info.DetailInfo_OutputGasList(Source);
        }

        void Format_MedicalRoom(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_InputPower(Sink);
        }

        void Format_ShipWelder(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_Inventory(Inv);
        }

        void Format_ShipGrinder(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //      (nothing)

            info.DetailInfo_Inventory(Inv);
        }

        void Format_Parachute(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //     (nothing)

            var parachute = (IMyParachute)block;

            info.DetailInfo_Inventory(Inv);
            info.Append("Atmosphere density: ").ProportionToPercent(parachute.Atmosphere).Append('\n');
        }

        void Format_Collector(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //     (nothing)

            info.DetailInfo_Inventory(Inv);
        }

        void Format_ButtonPanel(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.186.5:
            //     (nothing)

            info.DetailInfo_InputPower(Sink);
        }
        #endregion

        // called when another mod uses this mod's API, for details see "API Information" file.
        void ModMessageReceived(object obj)
        {
            try
            {
                if(obj is MyDefinitionId)
                {
                    var id = (MyDefinitionId)obj;
                    ignoreModBlocks.Add(id);
                    return;
                }

                Log.Error($"A mod sent an unknwon mod message to this mod; data={obj}");
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}