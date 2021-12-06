using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using MyAssemblerMode = Sandbox.ModAPI.Ingame.MyAssemblerMode;
using MyShipConnectorStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

namespace Digi.BuildInfo.Features.Terminal
{
    public class TerminalInfo : ModComponent
    {
        public const int RefreshMinTicks = 15; // minimum amount of ticks between refresh calls
        private readonly string[] TickerText = { "––––––", "•–––––", "–•––––", "––•–––", "–––•––", "––––•–", "–––––•" };

        public const int TextCharsExpected = 400; // used in calling EnsureCapacity() for CustomInfo event's StringBuilder

        public List<IMyTerminalBlock> SelectedInTerminal = new List<IMyTerminalBlock>();
        private List<IMyTerminalBlock> SelectingList = new List<IMyTerminalBlock>();
        private HashSet<IMyTerminalBlock> SelectingSet = new HashSet<IMyTerminalBlock>();
        private IMyTerminalBlock LastSelected;

        public event Action SelectedChanged;

        public readonly HashSet<MyDefinitionId> IgnoreModBlocks = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        private IMyTerminalBlock viewedInTerminal;
        private int cursorCheckAfterTick = 0;
        private int refreshWaitForTick = 0;

        private int ticker;
        private int tickerUpdateAt;

        private readonly StringBuilder tmp = new StringBuilder(512);
        private readonly StringBuilder tmpInfo = new StringBuilder(512);

        private CustomInfoCall currentFormatCall;
        private delegate void CustomInfoCall(IMyTerminalBlock block, StringBuilder info);
        private readonly Dictionary<MyObjectBuilderType, CustomInfoCall> formatLookup
                   = new Dictionary<MyObjectBuilderType, CustomInfoCall>(MyObjectBuilderType.Comparer);

        private readonly HashSet<long> longSetTemp = new HashSet<long>();
        private readonly List<IMySlimBlock> nearbyBlocksCache = new List<IMySlimBlock>(); // list for reuse only

        readonly ResourceStatsCollector ResourceStats;

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

        public TerminalInfo(BuildInfoMod main) : base(main)
        {
            ResourceStats = new ResourceStatsCollector(Main);
        }

        public override void RegisterComponent()
        {
            RegisterFormats();

            MyAPIGateway.TerminalControls.CustomControlGetter += TerminalCustomControlGetter;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalCustomControlGetter;

            ResourceStats?.Dispose();
        }

        private void RegisterFormats()
        {
            CustomInfoCall action;

            action = Format_LightingBlock;
            Add(typeof(MyObjectBuilder_InteriorLight), action);
            Add(typeof(MyObjectBuilder_ReflectorLight), action);

            action = Format_Doors;
            Add(typeof(MyObjectBuilder_Door), action);
            Add(typeof(MyObjectBuilder_AdvancedDoor), action);
            Add(typeof(MyObjectBuilder_AirtightDoorGeneric), action);
            Add(typeof(MyObjectBuilder_AirtightHangarDoor), action);
            Add(typeof(MyObjectBuilder_AirtightSlideDoor), action);

            Add(typeof(MyObjectBuilder_CargoContainer), Format_CargoContainer);

            Add(typeof(MyObjectBuilder_ConveyorSorter), Format_ConveyorSorter);

            Add(typeof(MyObjectBuilder_ShipWelder), Format_ShipWelder);
            Add(typeof(MyObjectBuilder_ShipGrinder), Format_ShipGrinder);
            Add(typeof(MyObjectBuilder_Drill), Format_ShipDrill);

            action = Format_Piston;
            Add(typeof(MyObjectBuilder_PistonBase), action); // this one is actually ancient and unused?
            Add(typeof(MyObjectBuilder_ExtendedPistonBase), action);

            Add(typeof(MyObjectBuilder_ShipConnector), Format_Connector);

            action = Format_Rotor;
            Add(typeof(MyObjectBuilder_MotorAdvancedStator), action);
            Add(typeof(MyObjectBuilder_MotorStator), action);
            Add(typeof(MyObjectBuilder_MotorSuspension), action);

            Add(typeof(MyObjectBuilder_TimerBlock), Format_TimerBlock);

            Add(typeof(MyObjectBuilder_SoundBlock), Format_SoundBlock);

            Add(typeof(MyObjectBuilder_ButtonPanel), Format_ButtonPanel);

            action = Format_Weapons;
            Add(typeof(MyObjectBuilder_TurretBase), action);
            Add(typeof(MyObjectBuilder_ConveyorTurretBase), action);
            Add(typeof(MyObjectBuilder_SmallGatlingGun), action);
            Add(typeof(MyObjectBuilder_SmallMissileLauncher), action);
            Add(typeof(MyObjectBuilder_SmallMissileLauncherReload), action);
            Add(typeof(MyObjectBuilder_InteriorTurret), action);
            Add(typeof(MyObjectBuilder_LargeGatlingTurret), action);
            Add(typeof(MyObjectBuilder_LargeMissileTurret), action);

            // nothing useful to add, it also has a huge detail info text when a projection is loaded
            //Add(typeof(MyObjectBuilder_Projector),Format_Projector);
            //Add(typeof(MyObjectBuilder_ProjectorBase),Format_Projector);

            Add(typeof(MyObjectBuilder_OreDetector), Format_OreDetector);

            Add(typeof(MyObjectBuilder_Parachute), Format_Parachute);

            action = Format_GasTank;
            Add(typeof(MyObjectBuilder_GasTank), action);
            Add(typeof(MyObjectBuilder_OxygenTank), action);

            action = Format_Seats;
            Add(typeof(MyObjectBuilder_Cockpit), action);
            Add(typeof(MyObjectBuilder_CryoChamber), action);

            Add(typeof(MyObjectBuilder_RemoteControl), Format_RemoteControl);

            //Add(typeof(MyObjectBuilder_Gyro), Format_Gyro);

            Add(typeof(MyObjectBuilder_Thrust), Format_Thruster);

            Add(typeof(MyObjectBuilder_Collector), Format_Collector);

            Add(typeof(MyObjectBuilder_Reactor), Format_Reactor);
            Add(typeof(MyObjectBuilder_BatteryBlock), Format_Battery);
            Add(typeof(MyObjectBuilder_HydrogenEngine), Format_HydrogenEngine);
            Add(typeof(MyObjectBuilder_SolarPanel), Format_SolarPanel);
            Add(typeof(MyObjectBuilder_WindTurbine), Format_WindTurbine);

            action = Format_Production;
            Add(typeof(MyObjectBuilder_Refinery), action);
            Add(typeof(MyObjectBuilder_Assembler), action);
            Add(typeof(MyObjectBuilder_SurvivalKit), action);

            Add(typeof(MyObjectBuilder_UpgradeModule), Format_UpgradeModule);

            Add(typeof(MyObjectBuilder_MedicalRoom), Format_MedicalRoom);

            Add(typeof(MyObjectBuilder_OxygenGenerator), Format_GasGenerator);

            Add(typeof(MyObjectBuilder_OxygenFarm), Format_OxygenFarm);

            Add(typeof(MyObjectBuilder_AirVent), Format_AirVent);

            Add(typeof(MyObjectBuilder_RadioAntenna), Format_RadioAntenna);

            Add(typeof(MyObjectBuilder_LaserAntenna), Format_LaserAntenna);

            Add(typeof(MyObjectBuilder_Beacon), Format_Beacon);

            Add(typeof(MyObjectBuilder_MyProgrammableBlock), Format_ProgrammableBlock);

            //Add(typeof(MyObjectBuilder_JumpDrive), Format_JumpDrive);
        }

        private void Add(MyObjectBuilderType blockType, CustomInfoCall call)
        {
            formatLookup.Add(blockType, call);
        }

        public override void UpdateAfterSim(int tick)
        {
            if(LastSelected != null)
            {
                SelectedInTerminal.Clear();

                if(LastSelected != viewedInTerminal)
                {
                    ViewedBlockChanged(viewedInTerminal, LastSelected);

                    if(viewedInTerminal != null)
                    {
                        // HACK: required to avoid getting 2 blocks as selected when starting from a fast-refreshing block (e.g. airvent) and selecting a non-refreshing one (e.g. cargo container)
                        bool orig = viewedInTerminal.ShowInToolbarConfig;
                        viewedInTerminal.ShowInToolbarConfig = !orig;
                        viewedInTerminal.ShowInToolbarConfig = orig;
                    }
                }

                LastSelected = null;

                MyUtils.Swap(ref SelectingList, ref SelectedInTerminal); // swap references, faster than adding to the 2nd list and clearing

                SelectingList.Clear();
                SelectingSet.Clear();

                SelectedChanged?.Invoke();
            }

            if(viewedInTerminal == null)
                return;

            // check if the block is still valid or if the player exited the menu
            // NOTE: IsCursorVisible reacts slowly to the menu being opened, an ignore period is needed
            if(viewedInTerminal.MarkedForClose || (cursorCheckAfterTick <= Main.Tick && !MyAPIGateway.Gui.IsCursorVisible))
            {
                SelectedInTerminal.Clear();
                ViewedBlockChanged(viewedInTerminal, null);
                return;
            }

            // only actively refresh if viewing the block list
            if(Main.Tick % RefreshMinTicks == 0
            && !Main.GUIMonitor.InAnyToolbarGUI
            && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                UpdateDetailInfo();
            }
        }

        // Gets called when local client clicks on a block in the terminal.
        // Used to know the currently viewed block in the terminal.
        void TerminalCustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if(SelectingSet.Add(block))
            {
                SelectingList.Add(block);
            }

            LastSelected = block;

            UpdateMethods |= UpdateFlags.UPDATE_AFTER_SIM;
        }

        void ViewedBlockChanged(IMyTerminalBlock oldBlock, IMyTerminalBlock newBlock)
        {
            if(oldBlock != null)
            {
                oldBlock.AppendingCustomInfo -= CustomInfo;
                oldBlock.PropertiesChanged -= PropertiesChanged;
            }

            viewedInTerminal = null;
            currentFormatCall = null;

            ClearCaches(); // block changed so caches are no longer relevant

            if(newBlock != null)
            {
                if(!formatLookup.TryGetValue(newBlock.BlockDefinition.TypeId, out currentFormatCall))
                    return; // ignore blocks that don't need stats

                viewedInTerminal = newBlock;

                cursorCheckAfterTick = Main.Tick + 10;

                newBlock.AppendingCustomInfo += CustomInfo;
                newBlock.PropertiesChanged += PropertiesChanged;

                UpdateDetailInfo(force: true);

                if(oldBlock == null || oldBlock.CubeGrid != newBlock.CubeGrid)
                {
                    ResourceStats.Reset(oldBlock == null ? "no prev block" : "grid changed");
                }
            }
            else
            {
                ResourceStats.Reset("deselected");
            }

            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, (viewedInTerminal != null));
        }

        int RefreshedAtTick = 0;
        void UpdateDetailInfo(bool force = false)
        {
            if(RefreshedAtTick == Main.Tick)
                return; // prevent some infinite loop cases with other mods

            RefreshedAtTick = Main.Tick;

            // disabled because it can leave info outdated when it suddenly stops updating

            //if(!force && !Main.Config.TerminalDetailInfoAdditions.Value)
            //    return;

            //if(!force && refreshWaitForTick > Main.Tick)
            //    return;

            //refreshWaitForTick = (Main.Tick + RefreshMinTicks);
            viewedInTerminal.RefreshCustomInfo();
        }

        void PropertiesChanged(IMyTerminalBlock block)
        {
            UpdateDetailInfo();
        }

        // Called by AppendingCustomInfo's invoker: RefreshCustomInfo()
        void CustomInfo(IMyTerminalBlock block, StringBuilder info)
        {
            try
            {
                if(currentFormatCall == null)
                    return;

                if(IgnoreModBlocks.Contains(block.BlockDefinition))
                    return;

                // Append other mod's info after my own.
                // This is possible since this event is surely executed last as it's hooked when block is clicked
                //   and because the same StringBuiler is given to all mods.
                string otherModInfo = (info.Length > 0 ? info.ToString() : null);

                info.Clear(); // mods shouldn't do this here, but I'm storing existing data and writing it back!
                int capacity = info.Capacity;

                bool addedCustomInfo = false;
                bool header = Main.Config.TerminalDetailInfoHeader.Value;

                if(Main.Config.TerminalDetailInfoAdditions.Value)
                {
                    info.EnsureCapacity(TextCharsExpected + (otherModInfo == null ? 0 : otherModInfo.Length));
                    capacity = info.Capacity;

                    if(header)
                    {
                        tmpInfo.Clear();
                        currentFormatCall.Invoke(block, tmpInfo);

                        if(tmpInfo.Length > 0)
                        {
                            addedCustomInfo = true;

                            string text = TickerText[ticker];

                            info.Append(text, 0, 3).Append("( BuildInfo | /bi )").Append(text, 3, 3).Append('\n');
                            info.AppendStringBuilder(tmpInfo);
                        }
                    }
                    else
                    {
                        currentFormatCall.Invoke(block, info);
                        addedCustomInfo = (info.Length > 0);
                    }

                    info.TrimEndWhitespace();
                }

                if(otherModInfo != null)
                {
                    if(addedCustomInfo)
                        info.Append('\n');

                    info.Append(otherModInfo);
                }

                if(!header && addedCustomInfo)
                {
                    info.Append('\n').Append(TickerText[ticker]);
                }

                if(tickerUpdateAt <= Main.Tick)
                {
                    ticker = (ticker + 1) % TickerText.Length;
                    tickerUpdateAt = Main.Tick + RefreshMinTicks;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
                info?.Append($"\n[ {BuildInfoMod.ModName} ERROR; SEND GAME LOG! ]");
            }
        }

        #region Text formatting per block type
        void Format_LightingBlock(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
        }

        void Format_Doors(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
        }

        void Format_OreDetector(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
        }

        void Format_TimerBlock(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.194.211:
            //      Time to trigger: 00:00:00

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
        }

        void Format_SoundBlock(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
        }

        void Format_CargoContainer(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      (nothing)

            MyCargoContainerDefinition cargoDef = (MyCargoContainerDefinition)block.SlimBlock.BlockDefinition;

            info.DetailInfo_Type(block);
            info.DetailInfo_Inventory(Inv, cargoDef.InventorySize.Volume);
        }

        void Format_ConveyorSorter(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Current Input: <n> W

            // Conveyor sorters can be used as a base block for WeaponCore.
            if(Main.CoreSystemsAPIHandler.Weapons.ContainsKey(block.BlockDefinition))
            {
                Format_WeaponCore(block, info);
                return;
            }

            MyConveyorSorterDefinition sorterDef = (MyConveyorSorterDefinition)block.SlimBlock.BlockDefinition;

            info.DetailInfo_MaxPowerUsage(Sink);
            info.DetailInfo_Inventory(Inv, sorterDef.InventorySize.Volume);
        }

        void Format_Connector(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);

            MyCubeBlockDefinition def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;
            float volume;

            if(Utils.GetInventoryVolumeFromComponent(def, out volume))
            {
                info.DetailInfo_Inventory(Inv, volume);
            }
            else
            {
                info.DetailInfo_Inventory(Inv, Hardcoded.ShipConnector_InventoryVolume(def));
            }

            BData_Connector data = Main.LiveDataHandler.Get<BData_Connector>(def);
            if(data != null && data.CanConnect)
            {
                IMyShipConnector connector = (IMyShipConnector)block;

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

                    if(connector.OtherConnector.GetValue<bool>("Trading")) // HACK: replace with interface property if that ever gets added
                    {
                        info.Append("Target is in Trade-Mode").Append('\n');
                    }

                    info.Append("Ship: ").Append(connector.OtherConnector.CubeGrid.CustomName).Append('\n');
                }
                else
                {
                    info.Append("Status: Not connected\n");
                    info.Append("Target: N/A\n");
                    info.Append("Ship: N/A\n");
                }
            }
        }

        void Format_Weapons(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      (nothing)

            if(Main.CoreSystemsAPIHandler.Weapons.ContainsKey(block.BlockDefinition))
            {
                Format_WeaponCore(block, info);
                return;
            }

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);

            MyWeaponBlockDefinition weaponBlockDef = block?.SlimBlock?.BlockDefinition as MyWeaponBlockDefinition;
            if(weaponBlockDef == null || Inv == null)
                return;

            float maxVolume;
            if(!Utils.GetInventoryVolumeFromComponent(weaponBlockDef, out maxVolume))
                maxVolume = weaponBlockDef.InventoryMaxVolume;

            info.DetailInfo_Inventory(Inv, maxVolume);

            IMyGunObject<MyGunBase> gun = block as IMyGunObject<MyGunBase>;
            if(gun?.GunBase?.CurrentAmmoMagazineDefinition == null || !gun.GunBase.HasAmmoMagazines)
                return;

            int loadedAmmo = gun.GunBase.CurrentAmmo;
            int mags = gun.GunBase.GetInventoryAmmoMagazinesCount();
            MyAmmoMagazineDefinition magDef = gun.GunBase.CurrentAmmoMagazineDefinition;

            // assume one mag is loaded for simplicty sake
            if(loadedAmmo == 0 && mags > 0)
            {
                loadedAmmo = magDef.Capacity;
                mags -= 1;
            }

            info.Append("Ammo: ").Append(loadedAmmo).Append(" loaded + ").Append(mags * magDef.Capacity).Append(" in mags\n");

            ReloadTracker.TrackedWeapon weaponTracker = Main.ReloadTracking.WeaponLookup.GetValueOrDefault(block.EntityId, null);
            if(weaponTracker != null)
            {
                info.Append("Shots until reload: ");

                if(weaponTracker.ReloadUntilTick > 0)
                    info.Append("Reloading");
                else
                    info.Append(weaponTracker.ShotsUntilReload);

                info.Append(" / ").Append(weaponTracker.InternalMagazineCapacity).Append('\n');
            }

            const int MaxMagNameLength = 26;

            MyWeaponDefinition weaponDef = MyDefinitionManager.Static.GetWeaponDefinition(weaponBlockDef.WeaponDefinitionId);

            if(weaponDef == null || weaponDef.AmmoMagazinesId.Length == 1)
            {
                info.Append("Type: ").AppendMaxLength(magDef.DisplayNameText, MaxMagNameLength).Append('\n');
            }
            else
            {
                info.Append("Types:\n");
                info.Append(">").AppendMaxLength(magDef.DisplayNameText, MaxMagNameLength).Append('\n');

                foreach(MyDefinitionId otherMagId in weaponDef.AmmoMagazinesId)
                {
                    if(otherMagId == magDef.Id)
                        continue;

                    MyAmmoMagazineDefinition otherMagDef = MyDefinitionManager.Static.GetAmmoMagazineDefinition(otherMagId);
                    if(otherMagDef != null)
                        info.Append("  ").AppendMaxLength(otherMagDef.DisplayNameText, MaxMagNameLength).Append('\n');
                }
            }
        }

        void Format_WeaponCore(IMyTerminalBlock block, StringBuilder info)
        {
        }

        void Format_Production(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W
            //      Current Input: <n> W
            //      Productivity: <n>%
            //      Effectiveness: <n>%
            //      Power efficiency: <n>%
            //      Used upgrade module slots: <n> / <n>

            MyProductionBlockDefinition productionDef = (MyProductionBlockDefinition)block.SlimBlock.BlockDefinition;
            float volume = (productionDef.InventoryMaxVolume > 0 ? productionDef.InventoryMaxVolume : productionDef.InventorySize.Volume);
            info.DetailInfo_Inventory(Inv, volume, "Inventory In");
            info.DetailInfo_Inventory(Inv2, volume, "Inventory Out");

            IMyProductionBlock production = (IMyProductionBlock)block;
            IMyAssembler assembler = block as IMyAssembler;
            IMyRefinery refinery = block as IMyRefinery;

            if(assembler != null)
            {
                info.Append("Mode: ");
                switch(assembler.Mode)
                {
                    case MyAssemblerMode.Assembly: info.Append("Assemble"); break;
                    case MyAssemblerMode.Disassembly: info.Append("Disassemble"); break;
                    default: info.Append(assembler.Mode.ToString()); break;
                }
                info.Append('\n');

                info.Append("Loop queue: ").Append(assembler.Repeating ? "On" : "Off").Append('\n');
            }

            if(production.IsQueueEmpty)
            {
                info.Append("Queue: (empty)\n");
            }
            else
            {
                info.Append("Queue: ").Append(production.IsProducing ? "Working..." : "STOPPED").Append('\n');

                List<MyProductionQueueItem> queue = production.GetQueue(); // TODO: avoid alloc here somehow?

                if(assembler != null || refinery != null)
                {
                    MyAssemblerDefinition assemblerDef = productionDef as MyAssemblerDefinition;
                    MyRefineryDefinition refineryDef = productionDef as MyRefineryDefinition;

                    tmp.Clear();
                    float totalTime = 0;

                    for(int i = 0; i < queue.Count; ++i)
                    {
                        MyProductionQueueItem item = queue[i];
                        MyBlueprintDefinitionBase bp = (MyBlueprintDefinitionBase)item.Blueprint;
                        float amount = (float)item.Amount;

                        float time = 0;
                        if(assembler != null)
                        {
                            time = Hardcoded.Assembler_BpProductionTime(bp, assemblerDef, assembler);
                            time *= amount;
                            totalTime += time;

                            // need access to MyAssembler.CurrentItemIndex to determine which queue item is actually being built

                            tmp.Append("• x").ShortNumber(amount);
                        }
                        else // refinery
                        {
                            time = Hardcoded.Refinery_BpProductionTime(bp, refineryDef, refinery);
                            time *= amount;
                            totalTime += time;

                            tmp.Append(i + 1).Append('.');

                            if(bp.Prerequisites.Length == 1 && bp.Results.Length == 1)
                            {
                                amount = (float)bp.Results[0].Amount / (float)bp.Prerequisites[0].Amount;
                                tmp.Append(" x").ShortNumber(amount);
                            }
                        }

                        tmp.Append(' ').Append(item.Blueprint.DisplayNameText).Append(" (").TimeFormat(time).Append(")\n");
                    }

                    info.Append("Total time: ").TimeFormat(totalTime).Append('\n');
                    info.AppendStringBuilder(tmp);
                    tmp.Clear();
                }
                else // unknown production block
                {
                    for(int i = 0; i < queue.Count; ++i)
                    {
                        MyProductionQueueItem item = queue[i];
                        float amount = (float)item.Amount;

                        info.Append("• x").Number(amount).Append(" ").Append(item.Blueprint.DisplayNameText).Append('\n');
                    }
                }
            }
        }

        void Format_UpgradeModule(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      (nothing)

            info.DetailInfo_Type(block);

            IMyUpgradeModule upgradeModule = (IMyUpgradeModule)block;
            MyUpgradeModuleDefinition def = (MyUpgradeModuleDefinition)upgradeModule.SlimBlock.BlockDefinition;

            if(def.Upgrades == null) // required as UpgradeCount throws NRE if block has no Upgrades tag at all (empty tag would be fine)
                return;

            if(upgradeModule.UpgradeCount == 0) // probably a platform for something else and not an actual upgrade module, therefore skip
                return;

            MyUpgradeModuleDefinition upgradeModuleDef = (MyUpgradeModuleDefinition)block.SlimBlock.BlockDefinition;

            info.Append("Connections:");

            if(upgradeModule.Connections > 0)
            {
                info.Append('\n');

                // since upgrade module doesn't expose what blocks it's connected to, I'll look for nearby blocks that have this upgrade module listed in their upgrades.
                longSetTemp.Clear();

                nearbyBlocksCache.Clear();
                upgradeModule.SlimBlock.GetNeighbours(nearbyBlocksCache);

                foreach(IMySlimBlock nearSlim in nearbyBlocksCache)
                {
                    if(nearSlim?.FatBlock == null)
                        continue;

                    if(longSetTemp.Contains(nearSlim.FatBlock.EntityId)) // already processed this item
                        continue;

                    longSetTemp.Add(nearSlim.FatBlock.EntityId);

                    MyCubeBlock nearCube = (MyCubeBlock)nearSlim.FatBlock;

                    if(nearCube.CurrentAttachedUpgradeModules == null)
                        continue;

                    foreach(MyCubeBlock.AttachedUpgradeModule module in nearCube.CurrentAttachedUpgradeModules.Values)
                    {
                        if(module.Block == upgradeModule)
                        {
                            string name = ((nearCube as IMyTerminalBlock)?.CustomName ?? nearCube.DisplayNameText);
                            info.Append("• ").Append(module.SlotCount).Append("x ").Append(name);

                            if(!module.Compatible)
                                info.Append(" (incompatible)");

                            info.Append('\n');
                            break;
                        }
                    }
                }

                nearbyBlocksCache.Clear();
                longSetTemp.Clear();
            }
            else
            {
                info.Append(" (none)\n");
            }

            info.Append("\nUpgrades per slot:\n");

            List<MyUpgradeModuleInfo> upgrades;
            upgradeModule.GetUpgradeList(out upgrades);

            foreach(MyUpgradeModuleInfo item in upgrades)
            {
                info.Append("• ").AppendUpgrade(item).Append('\n');
            }
        }

        void Format_OxygenFarm(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W
            //      Oxygen output: <n> L/min

            info.DetailInfo_CurrentPowerUsage(Sink);
        }

        void Format_Seats(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //     [Main ship cockpit: <name>]

            IMyCockpit cockpit = (IMyCockpit)block;

            MyCockpitDefinition def = (MyCockpitDefinition)cockpit.SlimBlock.BlockDefinition;
            if(Hardcoded.Cockpit_PowerRequired(def, cockpit.IsFunctional) > 0)
                info.DetailInfo_CurrentPowerUsage(Sink);

            if(cockpit.OxygenCapacity > 0)
                info.Append("Oxygen: ").ProportionToPercent(cockpit.OxygenFilledRatio).Append(" (").VolumeFormat(cockpit.OxygenCapacity * cockpit.OxygenFilledRatio).Append(" / ").VolumeFormat(cockpit.OxygenCapacity).Append(")\n");

            Suffix_ShipController(block, info);
        }

        void Format_RemoteControl(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W

            info.DetailInfo_CurrentPowerUsage(Sink);

            Suffix_ShipController(block, info);
        }

        #region ShipController extra stuff
        void Suffix_ShipController(IMyTerminalBlock block, StringBuilder info)
        {
            info.DetailInfo_Inventory(Inv, Hardcoded.Cockpit_InventoryVolume);

            ToolbarInfo.CustomToolbarData blockToolbarData = Main.ToolbarCustomLabels.BlockData.GetValueOrDefault(block.EntityId, null);
            if(blockToolbarData != null && blockToolbarData.ParseErrors.Count > 0)
            {
                info.Append("\nToolbar CustomData Errors:");

                foreach(string line in blockToolbarData.ParseErrors)
                {
                    info.Append('\n').Append(line);
                }

                info.Append('\n');
            }

            MyShipControllerDefinition def = (MyShipControllerDefinition)block.SlimBlock.BlockDefinition;
            if(def.EnableShipControl)
            {
                MyShipController internalController = (MyShipController)block;
                MyResourceDistributorComponent distributor = internalController.GridResourceDistributor;
                Suffix_PowerSources(block, info, distributor);
            }
        }

        void Suffix_PowerSources(IMyTerminalBlock block, StringBuilder info, MyResourceDistributorComponent distributor, bool lite = false)
        {
#if VERSION_190 || VERSION_191 || VERSION_192 || VERSION_193 || VERSION_194 || VERSION_195 || VERSION_196 || VERSION_197 || VERSION_198 // HACK: backwards compatible
            MyResourceStateEnum state = distributor.ResourceStateByType(MyResourceDistributorComponent.ElectricityId);
            float required = distributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId);
            float available = distributor.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId);
            float hoursLeft = distributor.RemainingFuelTimeByType(MyResourceDistributorComponent.ElectricityId);
#else
            MyCubeGrid internalGrid = (MyCubeGrid)block.CubeGrid;
            MyResourceStateEnum state = distributor.ResourceStateByType(MyResourceDistributorComponent.ElectricityId, grid: internalGrid);
            float required = distributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId, grid: internalGrid);
            float available = distributor.MaxAvailableResourceByType(MyResourceDistributorComponent.ElectricityId, grid: internalGrid);
            float hoursLeft = distributor.RemainingFuelTimeByType(MyResourceDistributorComponent.ElectricityId, grid: internalGrid);
#endif

            if(info.Length > 0)
                info.Append('\n');

            info.Append("Ship power: ");
            switch(state)
            {
                case MyResourceStateEnum.Disconnected: info.Append("Disconnected!"); break;
                case MyResourceStateEnum.NoPower: info.Append("No power!"); break;
                case MyResourceStateEnum.Ok: info.Append("OK"); break;
                case MyResourceStateEnum.OverloadAdaptible: info.Append("Minor Overload!"); break;
                case MyResourceStateEnum.OverloadBlackout: info.Append("Heavy Overload!"); break;
                default: info.Append(state.ToString()); break;
            }
            info.Append('\n');

#if true
            // TODO: find an alternate way of getting power required as it's buggy (usually negative or 0) if there's only solar panels (prob wind turbines too)
            info.Append("  Total required: ").PowerFormat(required).Append('\n');
            info.Append("  Total available: ").PowerFormat(available).Append('\n');
            info.Append("  Time left: ").TimeFormat(hoursLeft * 60 * 60).Append('\n');
#else
            ResourceStats.Update(block.CubeGrid);
            ResourceStatsCollector.Stats stats = ResourceStats.ComputedStats;

            //info.Append("  Total required: ").PowerFormat(required).Append('\n');
            //info.Append("  Total available: ").PowerFormat(available).Append('\n');

            //info.Append("  Input: ").PowerFormat(PS.PowerInput).Append(" / ").PowerFormat(PS.PowerRequired).Append('\n');
            //info.Append("  Output: ").PowerFormat(PS.PowerOutput).Append(" / ").PowerFormat(PS.PowerMaxOutput).Append('\n');

            info.Append("  Total required: ").PowerFormat(stats.PowerRequired).Append('\n');
            info.Append("  Total available: ").PowerFormat(stats.PowerOutputCapacity).Append('\n');


            info.Append("  Full: ").Append(ResourceStats.RefreshFullMs.ToString("0.##########")).Append(" ms").Append('\n'); 
            info.Append("  Working: ").Append(ResourceStats.RefreshWorkingMs.ToString("0.##########")).Append(" ms").Append('\n');
            info.Append($"  Blocks: {ResourceStats.Blocks.Count}\n");

            info.Append("  Time left: ").TimeFormat(hoursLeft * 60 * 60).Append('\n');

            if(lite)
                return;

            //info.Append("  Consumers: ");
            //if(ResourceStats.Consumers == 0)
            //    info.Append("None\n");
            //else
            //    info.Append(ResourceStats.ConsumersWorking).Append(" of ").Append(ResourceStats.Consumers).Append(" working\n");

            info.Append("  Reactors: ");
            if(stats.Reactors == 0)
                info.Append("None\n");
            else
                info.Append(stats.ReactorsWorking).Append(" of ").Append(stats.Reactors).Append(" working\n");

            info.Append("  Engines: ");
            if(stats.Engines == 0)
                info.Append("None\n");
            else
                info.Append(stats.EnginesWorking).Append(" of ").Append(stats.Engines).Append(" working\n");

            info.Append("  Batteries: ");
            if(stats.Batteries == 0)
                info.Append("None\n");
            else
                info.Append(stats.BatteriesWorking).Append(" of ").Append(stats.Batteries).Append(" working\n");

            info.Append("  Solar Panels: ");
            if(stats.SolarPanels == 0)
                info.Append("None\n");
            else
                info.Append(stats.SolarPanelsWorking).Append(" of ").Append(stats.SolarPanels).Append(" working\n");

            info.Append("  Wind Turbines: ");
            if(stats.WindTurbines == 0)
                info.Append("None\n");
            else
                info.Append(stats.WindTurbinesWorking).Append(" of ").Append(stats.WindTurbines).Append(" working\n");

            if(stats.OtherProducers > 0)
                info.Append("  Other power sources: ").Append(stats.OtherProducersWorking).Append(" of ").Append(stats.OtherProducers).Append(" working\n");
#endif
        }
        #endregion ShipController extra stuff

        void Format_Rotor(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Current angle or status

            info.DetailInfo_InputPower(Sink);

            // NOTE: this includes suspensions too

            IMyMotorStator rotorStator = block as IMyMotorStator;
            if(rotorStator != null)
            {
                IMyCubeGrid topGrid = rotorStator.TopGrid;
                float attachedMass = 0;

                if(topGrid?.Physics != null)
                {
                    attachedMass = topGrid.Physics.Mass;
                    if(!topGrid.IsStatic && attachedMass <= 0)
                    {
                        // need mass for clients in MP too, like when grids are LG'd
                        attachedMass = BuildInfoMod.Instance.GridMassCompute.GetGridMass(topGrid);
                    }
                }

                info.Append("Attached Mass: ");
                if(topGrid == null)
                    info.Append("(not available)\n");
                else if(attachedMass > 0)
                    info.MassFormat(attachedMass).Append('\n');
                else
                    info.Append("(unknown)\n");

                MyMotorStatorDefinition statorDef = block.SlimBlock.BlockDefinition as MyMotorStatorDefinition;
                if(statorDef != null)
                {
                    bool isSmall = (block.CubeGrid.GridSizeEnum == MyCubeSize.Small);
                    float minDisplacement = (isSmall ? statorDef.RotorDisplacementMinSmall : statorDef.RotorDisplacementMin);
                    float maxDisplacement = (isSmall ? statorDef.RotorDisplacementMaxSmall : statorDef.RotorDisplacementMax);

                    if(minDisplacement < maxDisplacement)
                    {
                        BData_Motor data = Main.LiveDataHandler.Get<BData_Motor>(statorDef);
                        if(data != null)
                        {
                            float displacement = minDisplacement - statorDef.RotorDisplacementInModel;
                            float totalTravel = (maxDisplacement - minDisplacement);

                            MatrixD topMatrix = data.GetRotorMatrix(block.LocalMatrix, block.WorldMatrix, block.CubeGrid.WorldMatrix, displacement);

                            float aligned = GetNearestGridAlign(block, topMatrix, 0, totalTravel);

                            info.Label("Grid-aligned displacement");
                            if(aligned < 0 || aligned > totalTravel)
                            {
                                info.Append("Impossible").Append('\n');
                            }
                            else
                            {
                                aligned = minDisplacement + aligned;
                                info.RoundedNumber(aligned, 5).Append("m\n");
                            }
                        }
                    }
                }

                if(Main.Config.InternalInfo.Value)
                {
                    info.Append("\nAPI Angle: ").RoundedNumber(rotorStator.Angle, 2).Append(" radians\n");
                }
            }
        }

        void Format_Piston(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Current position

            info.DetailInfo_InputPower(Sink);

            IMyPistonBase piston = block as IMyPistonBase;
            if(piston == null)
                return;

            MyPistonBaseDefinition def = block.SlimBlock.BlockDefinition as MyPistonBaseDefinition;
            if(def == null)
                return;

            BData_Piston data = Main.LiveDataHandler.Get<BData_Piston>(def);
            if(data != null)
            {
                MatrixD topMatrix = data.TopLocalMatrix * Utils.GetBlockCenteredWorldMatrix(block.SlimBlock);

                float minAligned = GetNearestGridAlign(block, topMatrix, def.Minimum, piston.MinLimit);
                float maxAligned = GetNearestGridAlign(block, topMatrix, def.Minimum, piston.MaxLimit);

                info.Append("Nearest grid-aligned limits:\n");
                info.Append("  Min: ").RoundedNumber(minAligned, 5).Append("m\n");
                info.Append("  Max: ").RoundedNumber(maxAligned, 5).Append("m\n");
            }

            if(Main.Config.InternalInfo.Value)
            {
                info.Append("API Position: ").DistanceFormat(piston.CurrentPosition, 5).Append('\n');
            }
        }

        static float GetNearestGridAlign(IMyTerminalBlock block, MatrixD topMatrix, float minOffset, float currentOffset)
        {
            float nearest = 0;
            float cellOffset = 0;
            Vector3D startVec = topMatrix.Translation + topMatrix.Up * minOffset;

            do
            {
                Vector3D limitVec = topMatrix.Translation + topMatrix.Up * (currentOffset + cellOffset);
                Vector3D alignedVec = block.CubeGrid.GridIntegerToWorld(block.CubeGrid.WorldToGridInteger(limitVec));

                nearest = (float)Vector3D.Dot(topMatrix.Up, (alignedVec - startVec));

                cellOffset += block.CubeGrid.GridSize;
            }
            while(nearest < 0);

            return nearest;
        }

        void Format_AirVent(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.198:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W
            //      Room pressure: <status>

            MyResourceSinkComponent sink = Sink;
            info.DetailInfo_CurrentPowerUsage(sink);

            IMyAirVent airVent = (IMyAirVent)block;
            if(airVent.Depressurize)
            {
                info.Append("Depressurizing:\n");
                info.DetailInfo_OutputGasList(Source, "  ");
            }
            else
            {
                info.Append("Pressurizing:\n");
                info.DetailInfo_InputGasList(sink, "  ");
            }
        }

        void Format_Reactor(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Output: <n> W
            //      Current Output: <n> W

            if(Inv == null)
                return;

            MyReactorDefinition reactorDef = (MyReactorDefinition)block.SlimBlock.BlockDefinition;

            if(reactorDef.FuelInfos != null && reactorDef.FuelInfos.Length > 0)
            {
                float ratio = Source.CurrentOutput / reactorDef.MaxPowerOutput;

                if(reactorDef.FuelInfos.Length == 1)
                {
                    MyReactorDefinition.FuelInfo fuel = reactorDef.FuelInfos[0];
                    float perSec = ratio * fuel.ConsumptionPerSecond_Items;
                    float seconds = (perSec > 0 ? ((float)Inv.CurrentMass / perSec) : 0);

                    info.Append("Current Usage: ").MassFormat(perSec).Append("/s\n");
                    info.Append("Time Left: ").TimeFormat(seconds).Append('\n');
                    info.Append("Uses Fuel: ").ItemName(fuel.FuelId).Append('\n');
                }
                else
                {
                    tmp.Clear();
                    float perSec = 0;

                    foreach(MyReactorDefinition.FuelInfo fuel in reactorDef.FuelInfos)
                    {
                        tmp.Append("  ").ItemName(fuel.FuelId).Append(" (").MassFormat(fuel.ConsumptionPerSecond_Items).Append("/s)\n");

                        perSec += ratio * fuel.ConsumptionPerSecond_Items;
                    }

                    float seconds = (perSec > 0 ? ((float)Inv.CurrentMass / perSec) : 0);

                    info.Append("Current Usage: ").MassFormat(perSec).Append("/s\n");
                    info.Append("Time Left: ").TimeFormat(seconds).Append('\n');
                    info.Append("Uses Combined Fuels: ").Append('\n');
                    info.AppendStringBuilder(tmp);
                    tmp.Clear();
                }
            }

            float maxVolume;
            if(!Utils.GetInventoryVolumeFromComponent(reactorDef, out maxVolume))
                maxVolume = (reactorDef.InventoryMaxVolume > 0 ? reactorDef.InventoryMaxVolume : reactorDef.InventorySize.Volume);

            info.DetailInfo_Inventory(Inv, maxVolume);

            Suffix_PowerSourceGridStats(block, info);
        }

        void Format_Battery(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.199.025:
            //      Type: <BlockDefName>
            //      Max Output: <n> W
            //      Max Required Input: <n> W
            //      Max Stored Power: <n> Wh
            //      Current Input: <n> W
            //      Current Output: <n> W
            //      Stored Power: <n> Wh
            //      Fully depleted/recharged in: <time>

            Suffix_PowerSourceGridStats(block, info);
        }

        void Format_HydrogenEngine(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Output: 5.00 MW
            //      Current Output: 0 W
            //      Filled: 0.0% (0L/500000L)

            info.DetailInfo_InputHydrogen(Sink);

            Suffix_PowerSourceGridStats(block, info);
        }

        void Format_WindTurbine(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Output: 5.00 MW
            //      Current Output: 0 W
            //      Wind Clearance: <Text>

            MyWindTurbineDefinition turbineDef = (MyWindTurbineDefinition)block.SlimBlock.BlockDefinition;

            float optimalOutput = turbineDef.MaxPowerOutput; // * ((turbineDef.RaycasterSize - 1) * turbineDef.RaycastersToFullEfficiency);

            info.Append("Optimal Output: ").PowerFormat(optimalOutput).Append('\n');

            IMyCubeGrid grid = block.CubeGrid;
            Vector3D position = grid.Physics?.CenterOfMassWorld ?? grid.PositionComp.GetPosition();
            MyPlanet planet = MyGamePruningStructure.GetClosestPlanet(position);

            info.Append("Current wind speed: ");
            if(planet != null && planet.PositionComp.WorldAABB.Contains(position) != ContainmentType.Disjoint)
            {
                float windSpeed = planet.GetWindSpeed(position) * MyAPIGateway.Session.WeatherEffects.GetWindMultiplier(position);
                info.Append(windSpeed.ToString("0.##"));
            }
            else
            {
                info.Append("N/A");
            }

            Suffix_PowerSourceGridStats(block, info);
        }

        void Format_SolarPanel(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Output: 5.00 MW
            //      Current Output: 0 W

            MySolarPanelDefinition solarDef = (MySolarPanelDefinition)block.SlimBlock.BlockDefinition;
            info.Append("Max Possible Output: ").PowerFormat(solarDef.MaxPowerOutput).Append('\n');

            Suffix_PowerSourceGridStats(block, info);
        }

        MyShipController _fakeController = new MyShipController();
        void Suffix_PowerSourceGridStats(IMyTerminalBlock block, StringBuilder info)
        {
            // HACK: trickery to get resource distributor
            _fakeController.SlimBlock = Utils.CastHax(_fakeController.SlimBlock, block.SlimBlock);
            MyResourceDistributorComponent distributor = _fakeController.GridResourceDistributor;
            Suffix_PowerSources(block, info, distributor, lite: true);
        }

        //void Format_Gyro(IMyTerminalBlock block, StringBuilder info)
        //{
        //    // Vanilla info in 1.198.027:
        //    //      Type: <BlockDefName>
        //    //      Max Required Input: <n> W
        //
        //}

        void Format_Thruster(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W

            IMyThrust thrust = (IMyThrust)block;
            Hardcoded.ThrustInfo thrustInfo = Hardcoded.Thrust_GetUsage(thrust);
            MyThrustDefinition def = thrustInfo.Def;

            if(thrustInfo.Fuel != MyResourceDistributorComponent.ElectricityId)
            {
                info.Append("Requires: ").Append(thrustInfo.Fuel.SubtypeName).Append('\n');

                info.Append("Current Usage*: ").VolumeFormat(thrustInfo.CurrentUsage).Append("/s");
                if(Math.Abs(thrustInfo.CurrentConsumptionMul - 1) > 0.001f)
                    info.Append(" (x").RoundedNumber(thrustInfo.CurrentConsumptionMul, 2).Append(")");
                info.Append('\n');

                info.Append("Max Usage*: ").VolumeFormat(thrustInfo.MaxUsage).Append("/s");
                if(Math.Abs(thrustInfo.EarthConsumpationMul - 1) > 0.001f)
                    info.Append(" (x").RoundedNumber(thrustInfo.EarthConsumpationMul, 2).Append(" @ 1g)");
                info.Append('\n');
            }
            else
            {
                info.Append("Requires: Electricity\n");

                info.Append("Current Usage*: ").PowerFormat(thrustInfo.CurrentUsage);
                if(Math.Abs(thrustInfo.CurrentConsumptionMul - 1) > 0.001f)
                    info.Append(" (x").RoundedNumber(thrustInfo.CurrentConsumptionMul, 2).Append(")");
                info.Append('\n');

                info.Append("Max Usage: ").PowerFormat(thrustInfo.MaxUsage);
                if(Math.Abs(thrustInfo.EarthConsumpationMul - 1) > 0.001f)
                    info.Append(" (x").RoundedNumber(thrustInfo.EarthConsumpationMul, 2).Append(" @ 1g)");
                info.Append('\n');
            }

            info.Append('\n');

            // HACK NOTE: def.NeedsAtmosphereForInfluence does nothing, influence is always air density
            if(def.EffectivenessAtMinInfluence < 1.0f || def.EffectivenessAtMaxInfluence < 1.0f)
            {
                // renamed to what they actually are for simpler code
                float minAir = def.MinPlanetaryInfluence;
                float maxAir = def.MaxPlanetaryInfluence;
                float thrustAtMinAir = def.EffectivenessAtMinInfluence;
                float thrustAtMaxAir = def.EffectivenessAtMaxInfluence;

                info.Append("Current Max Thrust: ").ForceFormat(thrust.MaxEffectiveThrust).Append('\n');
                info.Append("Optimal Max Thrust: ").ForceFormat(thrust.MaxThrust).Append('\n');
                info.Append("Limits:\n");

                // if mod has weird values, can't really present them in an understandable manner so just printing the values instead
                if(!Hardcoded.Thrust_HasSaneLimits(def))
                {
                    info.Append(" Min air density: ").ProportionToPercent(minAir).Append('\n');
                    info.Append(" Max air density: ").ProportionToPercent(maxAir).Append('\n');
                    info.Append(" Thrust at min air: ").ProportionToPercent(thrustAtMinAir).Append('\n');
                    info.Append(" Thrust at max air: ").ProportionToPercent(thrustAtMaxAir).Append('\n');

                    if(def.NeedsAtmosphereForInfluence)
                        info.Append(" No atmosphere causes 'thrust at min air'.\n");
                }
                else
                {
                    info.Append("  ").ProportionToPercent(thrustAtMaxAir).Append(" thrust ");
                    if(maxAir <= 0f)
                        info.Append("in vacuum.");
                    else if(maxAir < 1f)
                        info.Append("in ").ProportionToPercent(maxAir).Append(" air density.");
                    else
                        info.Append("in atmosphere.");
                    info.Append('\n');

                    info.Append("  ").ProportionToPercent(thrustAtMinAir).Append(" thrust ");
                    if(def.NeedsAtmosphereForInfluence || minAir <= 0f)
                        info.Append("in vacuum.");
                    else
                        info.Append("below ").ProportionToPercent(minAir).Append(" air density.");
                    info.Append('\n');
                }
            }
            else
            {
                info.Append("Max Thrust: ").ForceFormat(thrust.MaxThrust).Append('\n');
                info.Append("No atmosphere or vacuum limits.\n");
            }
        }

        void Format_RadioAntenna(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Current Input: <n> W

            MyRadioAntennaDefinition def = (MyRadioAntennaDefinition)block.SlimBlock.BlockDefinition;

            info.Append("Max Power Usage: ").PowerFormat(Hardcoded.RadioAntenna_PowerReq(def.MaxBroadcastRadius)).Append('\n');
        }

        void Format_LaserAntenna(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Current Input: <n> W
            //      <LaserAntennaStatus>

            IMyLaserAntenna antenna = (IMyLaserAntenna)block;
            MyLaserAntennaDefinition def = (MyLaserAntennaDefinition)block.SlimBlock.BlockDefinition;

            info.Append("Power Usage:\n");

            info.Append("  Current: ").PowerFormat(Sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId)).Append('\n');

            info.Append("  At Range: ");
            if(antenna.Range < 1E+08f)
                info.PowerFormat(Hardcoded.LaserAntenna_PowerUsage(def, antenna.Range));
            else
                info.Append("Infinite.");
            info.Append('\n');

            info.Append("  Max: ");
            if(def.MaxRange > 0)
                info.PowerFormat(Hardcoded.LaserAntenna_PowerUsage(def, def.MaxRange));
            else
                info.Append("Infinite.");
            info.Append('\n');
        }

        void Format_Beacon(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.194.211:
            //      Type: <BlockDefName>
            //      Current Input: <n> W

            MyBeaconDefinition def = (MyBeaconDefinition)block.SlimBlock.BlockDefinition;

            info.Append("Max Power Usage: ").PowerFormat(Hardcoded.Beacon_PowerReq(def)).Append('\n');
        }

        void Format_GasGenerator(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W

            info.DetailInfo_CurrentPowerUsage(Sink);
            info.DetailInfo_OutputGasList(Source);
        }

        void Format_GasTank(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W
            //      Filled: <n>% (<current>L / <max>L)

            info.DetailInfo_CurrentPowerUsage(Sink);
            info.DetailInfo_InputGasList(Sink);
            info.DetailInfo_OutputGasList(Source);
        }

        void Format_MedicalRoom(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
        }

        void Format_ShipWelder(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.194.211:
            //      (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
            info.DetailInfo_Inventory(Inv);
        }

        void Format_ShipGrinder(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.194.211:
            //      (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
            info.DetailInfo_Inventory(Inv);
        }

        void Format_ShipDrill(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.194.211:
            //      Type: <BlockDefName>
            //      Max Required Input: <Power>

            info.DetailInfo_InputPower(Sink);
            info.DetailInfo_Inventory(Inv);
        }

        void Format_Parachute(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //     (nothing)

            IMyParachute parachute = (IMyParachute)block;

            info.DetailInfo_Type(block);
            info.DetailInfo_Inventory(Inv);
            info.Append("Atmosphere density: ").ProportionToPercent(parachute.Atmosphere).Append('\n');
        }

        void Format_Collector(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //     (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
            info.DetailInfo_Inventory(Inv);
        }

        void Format_ButtonPanel(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //     (nothing)

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);
        }

        void Format_ProgrammableBlock(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041 (and probably forever):
            //     (nothing)

            if(!MyAPIGateway.Session.SessionSettings.EnableIngameScripts)
            {
                info.Append("In-game Scripts are disabled in world settings!\n");
                return;
            }

            // FIXME: this is detected as empty when it's not actually empty in the frame that it updates (PB with manual run only).
            string echoText = block.DetailedInfo;
            if(!string.IsNullOrEmpty(echoText))
                return; // only print something if PB itself doesn't

            if(MyAPIGateway.Session.SessionSettings.EnableScripterRole && MyAPIGateway.Session?.Player != null && MyAPIGateway.Session.Player.PromoteLevel < MyPromoteLevel.Scripter)
            {
                info.Append("Scripter role is required to use in-game scripts.\n");
            }

            if(!block.GetPlayerRelationToOwner().IsFriendly())
                return;

            // HACK: MP clients only get PB detailed info when in terminal, making this feature even more unreliable for tracking last info.
            PBData pbd;
            if(MyAPIGateway.Multiplayer.IsServer && Main.PBMonitor.PBData.TryGetValue(block.EntityId, out pbd))
            {
                float sec = (float)Math.Round((Main.Tick - pbd.SavedAtTick) / 60f);
                info.Append(Main.Config.TerminalDetailInfoHeader.Value ? "(Text from " : "(BuildInfo: text from ").TimeFormat(sec).Append(" ago)\n\n");
                info.Append(pbd.EchoText).Append('\n');
            }
        }

        // TODO: finish jumpdrive terminal info when jump destination can be retrieved
#if false
        void Format_JumpDrive(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.197.181:
            //      Type: <BlockDefName>
            //      Max Required Input: <Power>
            //      Max Stored Power: <Power>
            //      Current Input: <Power>
            //      Stored Power: <Power>
            //      Fully recharged in: <Time>
            //      Max jump distance: <Distance>
            //      [Current jump: <Percent>] (if GPS selected)

            var jumpDrive = block as MyJumpDrive;
            if(jumpDrive == null)
                return;

            var player = MyAPIGateway.Session?.Player;
            if(player == null)
                return;

            float powerCost = 0f;
            double distance = GetSelectedJumpDistance(jumpDrive);

            // from MyGridJumpDriveSystem.DepleteJumpDrives()
            double mass = jumpDrive.CubeGrid.GetCurrentMass();

            // DEBUG TODO: optimize
            foreach(var b in jumpDrive.CubeGrid.GetFatBlocks())
            {
                var jd = b as MyJumpDrive;
                if(jd == null || !jd.CanJumpAndHasAccess(player.IdentityId))
                    continue;

                double massRatio = Math.Min(jd.BlockDefinition.MaxJumpMass / mass, 1.0);
                double jumpDistance = jd.BlockDefinition.MaxJumpDistance * massRatio;

                if(jumpDistance < distance)
                {
                    distance -= jumpDistance;
                    if(b == block)
                    {
                        powerCost = jd.BlockDefinition.RequiredPowerInput;
                        break; // don't care about the rest of the blocks
                    }
                }
                else
                {
                    if(b == block)
                    {
                        double ratio = distance / jumpDistance;
                        powerCost = jd.BlockDefinition.RequiredPowerInput * (float)ratio;
                    }
                    break; // this is where game stops it
                }
            }

            info.Append("Jump Drain: ").PowerStorageFormat(powerCost);
        }

        double GetSelectedJumpDistance(MyJumpDrive jumpDrive)
        {
            var tb = (IMyTerminalBlock)jumpDrive;
            var prop = (IMyTerminalControl)tb.GetProperty("JumpDistance");
            bool hasJumpTarget = prop.Enabled.Invoke(jumpDrive);

            if(hasJumpTarget)
            {
                // DEBUG TODO ...
                return 5000;
            }
            else
            {
                return ComputeMaxDistance(jumpDrive);
            }
        }

        double ComputeMaxDistance(MyJumpDrive jumpDrive)
        {
            double maxJumpDistance = GetMaxJumpDistance(jumpDrive, jumpDrive.OwnerId);
            if(maxJumpDistance < 5000.0)
                return 5000.0;

            float jumpDistanceRatio = jumpDrive.GetValue<float>("JumpDistance");
            return 5001.0 + (maxJumpDistance - 5000.0) * jumpDistanceRatio;
        }

        double GetMaxJumpDistance(MyJumpDrive jumpDrive, long userId)
        {
            double absoluteMaxDistance = 0.0;
            double maxDistance = 0.0;
            double mass = jumpDrive.CubeGrid.GetCurrentMass();

            // DEBUG TODO: optimize
            foreach(var b in jumpDrive.CubeGrid.GetFatBlocks())
            {
                var jd = b as MyJumpDrive;
                if(jd == null || !jd.CanJumpAndHasAccess(userId))
                    continue;

                absoluteMaxDistance += jd.BlockDefinition.MaxJumpDistance;
                maxDistance += jd.BlockDefinition.MaxJumpDistance * (jd.BlockDefinition.MaxJumpMass / mass);
            }

            return Math.Min(absoluteMaxDistance, maxDistance);
        }
#endif

        #endregion Text formatting per block type
    }
}