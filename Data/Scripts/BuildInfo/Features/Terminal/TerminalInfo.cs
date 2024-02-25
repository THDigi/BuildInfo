using System;
using System.Collections.Generic;
using System.Text;
using CoreSystems.Api;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using IMyControllableEntityModAPI = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;
using MyAssemblerMode = Sandbox.ModAPI.Ingame.MyAssemblerMode;
using MyShipConnectorStatus = Sandbox.ModAPI.Ingame.MyShipConnectorStatus;

namespace Digi.BuildInfo.Features.Terminal
{
    public class TerminalInfo : ModComponent
    {
        public const int RefreshMinTicks = 30; // minimum amount of ticks between refresh calls
        private readonly string[] TickerText = { "––––––", "•–––––", "–•––––", "––•–––", "–––•––", "––––•–", "–––––•" };

        public const int TextCharsExpected = 400; // used in calling EnsureCapacity() for CustomInfo event's StringBuilder

        public List<IMyTerminalBlock> SelectedInTerminal = new List<IMyTerminalBlock>();
        private List<IMyTerminalBlock> SelectingList = new List<IMyTerminalBlock>();
        private HashSet<IMyTerminalBlock> SelectingSet = new HashSet<IMyTerminalBlock>();
        private IMyTerminalBlock LastSelected;

        public bool AutoRefresh = true;

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

            Add(typeof(MyObjectBuilder_TurretControlBlock), Format_CTC);

            Add(typeof(MyObjectBuilder_Searchlight), Format_Searchlight);

            // nothing useful to add, it also has a huge detail info text when a projection is loaded
            //Add(typeof(MyObjectBuilder_Projector), Format_Projector);
            //Add(typeof(MyObjectBuilder_ProjectorBase), Format_Projector);

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

        void Add(MyObjectBuilderType blockType, CustomInfoCall call)
        {
            formatLookup.Add(blockType, call);
        }

        // Gets called when local client clicks on a block in the terminal.
        // Used to know the currently viewed block in the terminal.
        void TerminalCustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            try
            {
                if(SelectingSet.Add(block))
                {
                    SelectingList.Add(block);
                }

                LastSelected = block;

                // can't know when this event stops being called in rapid succession (for multi-select), so have to check next tick.
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            if(LastSelected != null)
            {
                SelectedInTerminal.Clear();

                if(LastSelected != viewedInTerminal)
                {
                    ViewedBlockChanged(viewedInTerminal, LastSelected);

                    // HACK: required to avoid false-multiselect from a block that just updated.
                    // Repro: airvent in sealed room, selecting it makes it trigger CustomControlGetter every tick; then select a cargo container.
                    if(viewedInTerminal != null && SelectingList.Count > 1)
                    {
                        // NOTE: cannot be replaced by SetDetailedInfoDirty()
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
            if(viewedInTerminal.Closed || viewedInTerminal.MarkedForClose || (cursorCheckAfterTick <= Main.Tick && !MyAPIGateway.Gui.IsCursorVisible))
            {
                SelectedInTerminal.Clear();
                ViewedBlockChanged(viewedInTerminal, null);
                return;
            }

            // only actively refresh if viewing the block list
            if(AutoRefresh
            && Main.Tick % RefreshMinTicks == 0
            && !Main.GUIMonitor.InAnyToolbarGUI
            && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                UpdateDetailInfo();
            }
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
                viewedInTerminal = newBlock;

                cursorCheckAfterTick = Main.Tick + 10;

                if(formatLookup.TryGetValue(newBlock.BlockDefinition.TypeId, out currentFormatCall))
                {
                    newBlock.AppendingCustomInfo += CustomInfo;
                    newBlock.PropertiesChanged += PropertiesChanged;

                    UpdateDetailInfo(force: true);
                }

                if(oldBlock == null || oldBlock.CubeGrid != newBlock.CubeGrid)
                {
                    ResourceStats.Reset(oldBlock == null ? "no prev block" : "grid changed");
                }
            }
            else
            {
                ResourceStats.Reset("deselected");
            }

            bool needsUpdate = (viewedInTerminal != null);
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, needsUpdate);
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

            // just to track down some edge case issues, although it shouldn't ever reach this for either of these.
            if(viewedInTerminal == null)
            {
                Log.Error("[DEBUG] UpdateDetailInfo(): viewedInTerminal is null");
                return;
            }

            if(viewedInTerminal.MarkedForClose || viewedInTerminal.Closed)
            {
                Log.Error($"[DEBUG] UpdateDetailInfo(): viewedInTerminal is closed! marked={viewedInTerminal.MarkedForClose}; closed={viewedInTerminal.Closed}");
                return;
            }

            //refreshWaitForTick = (Main.Tick + RefreshMinTicks);
            viewedInTerminal.RefreshCustomInfo();

            if(viewedInTerminal is IMyProgrammableBlock) // HACK: PB clears its detailed info if asked to refresh it (like it does when you click it in terminal)
            {
                StringBuilder echo = viewedInTerminal.GetDetailedInfo();

                if(echo == null || echo.Length == 0) // for custominfo to refresh if detailedinfo is empty
                {
                    viewedInTerminal.SetDetailedInfoDirty();
                }
            }
            else
            {
                viewedInTerminal.SetDetailedInfoDirty();
            }
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

                const string separatorColor = "";
                const string separatorColorEnd = "";

                // FIXME: turned off coloring because it's extremely unreliable in that it causes auto-scroll issue
                //#if VERSION_195 || VERSION_196 || VERSION_197 || VERSION_198 || VERSION_199 || VERSION_200 || VERSION_201 // HACK: backwards compatible
                // const string separatorColor = ""; 
                // const string separatorColorEnd = "";
                //#else
                // const string separatorColor = "[color=#55999999]"; // ARGB
                // const string separatorColorEnd = "[/color]";
                //#endif

                if(Main.Config.TerminalDetailInfoAdditions.Value)
                {
                    info.EnsureCapacity(TextCharsExpected + (otherModInfo == null ? 0 : otherModInfo.Length));
                    capacity = info.Capacity;

                    string text = TickerText[ticker];

                    if(header)
                    {
                        tmpInfo.Clear();
                        currentFormatCall.Invoke(block, tmpInfo);

                        if(tmpInfo.Length > 0)
                        {
                            addedCustomInfo = true;

                            info.Append(separatorColor).Append(text, 0, 3).Append("( BuildInfo )").Append(text, 3, 3).Append(separatorColorEnd).Append('\n');

                            info.AppendStringBuilder(tmpInfo);
                        }
                    }
                    else
                    {
                        currentFormatCall.Invoke(block, info);
                        addedCustomInfo = (info.Length > 0);
                    }

                    if(addedCustomInfo)
                    {
                        info.TrimEndWhitespace().Append('\n');
                        info.Append(separatorColor).Append(text).Append(separatorColorEnd);
                    }
                }

                if(otherModInfo != null)
                {
                    // skip leading newlines
                    int startIdx = 0;
                    for(int i = 0; i < otherModInfo.Length; i++)
                    {
                        char c = otherModInfo[i];
                        if(c != '\n' && c != '\r')
                        {
                            startIdx = i;
                            break;
                        }
                    }

                    if(startIdx < otherModInfo.Length)
                    {
                        if(addedCustomInfo)
                        {
                            info.Append('\n');

                            if(!header)
                                info.Append('\n'); // separator missing, have at least an empty line
                        }

                        info.Append(otherModInfo, startIdx, otherModInfo.Length - startIdx);
                    }
                }

                if(tickerUpdateAt <= Main.Tick)
                {
                    ticker = (ticker + 1) % TickerText.Length;
                    tickerUpdateAt = Main.Tick + RefreshMinTicks;
                }
            }
            catch(Exception e)
            {
                Log.Error($"Error during terminal detailed info for blockDefId={block?.BlockDefinition}"
                    + $"\ndetailinfo additions config={(Main?.Config?.TerminalDetailInfoAdditions?.Value.ToString() ?? "NULL")}"
                    + $"\nmain={Main != null}; config={Main?.Config != null}; header={(Main?.Config?.TerminalDetailInfoHeader?.Value.ToString() ?? "NULL")}"
                    + "\n" + e);
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
            List<CoreSystemsDef.WeaponDefinition> csDefs;
            if(Main.CoreSystemsAPIHandler.Weapons.TryGetValue(block.BlockDefinition, out csDefs))
            {
                Format_WeaponCore(block, info, csDefs);
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
            if(data != null && data.IsConnector)
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

            List<CoreSystemsDef.WeaponDefinition> csDefs;
            if(Main.CoreSystemsAPIHandler.Weapons.TryGetValue(block.BlockDefinition, out csDefs))
            {
                Format_WeaponCore(block, info, csDefs);
                return;
            }

            info.DetailInfo_Type(block);
            info.DetailInfo_InputPower(Sink);

            Suffix_ControlledBy(block, info);

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

            // FIXME: this is not synchronized properly
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

                if(weaponTracker.ReloadUntilTick > Main.Tick)
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

                    MyAmmoMagazineDefinition otherMagDef = Utils.TryGetMagazineDefinition(otherMagId, weaponDef.Context);
                    if(otherMagDef != null)
                        info.Append("  ").AppendMaxLength(otherMagDef.DisplayNameText, MaxMagNameLength).Append('\n');
                }
            }
        }

        void Format_WeaponCore(IMyTerminalBlock block, StringBuilder info, List<CoreSystemsDef.WeaponDefinition> csDefs)
        {
            // TODO: test with WC first
            /*
            bool isControlled = false;
            long identityId = Main.CoreSystemsAPIHandler.API.GetPlayerController(block);

            info.Append("Control: ");

            if(identityId == -1 || identityId == 0) // WC for some reason has -1 as no identity
            {
                MyTuple<bool, bool, bool, IMyEntity> targetInfo = Main.CoreSystemsAPIHandler.API.GetWeaponTarget(block);

                //bool hasTarget = targetInfo.Item1;
                //bool isTargetProjectile = targetInfo.Item2;
                bool isManualOrPainter = targetInfo.Item3;

                if(!isManualOrPainter)
                {
                    info.Append("<AI>");
                    isControlled = true;
                }
            }
            else
            {
                IMyPlayer player = Utils.GetPlayerFromIdentityId(identityId);
                if(player != null)
                {
                    info.AppendMaxLength(player.DisplayName, 24);
                    isControlled = true;
                }
            }

            if(!isControlled)
                info.Append("-");

            info.Append('\n');
            */
        }

        void Format_CTC(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.202.112:
            //      Status:
            //      <errors and stuff>

            Suffix_ControlledBy(block, info);

            //IMyTurretControlBlock ctc = (IMyTurretControlBlock)block;

            // ctc.Target is always itself xD
            //info.Append("Target: ");
            //if(ctc.HasTarget && ctc.Target != null)
            //    info.Append($"{ctc.Target.DisplayName} / {ctc.Target.GetType()} / self={ctc.Target == ctc}");
            //else
            //    info.Append("-");
            //info.Append('\n');
        }

        void Format_Searchlight(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.202.112:
            //      (nothing)

            Suffix_ControlledBy(block, info);

            // target inaccessible
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
                List<MyProductionQueueItem> queue = production.GetQueue(); // TODO: avoid alloc here by using PB API? but then I need to lookup blueprints by ID...

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

                        float time;
                        if(assembler != null)
                        {
                            time = amount * Hardcoded.Assembler_BpProductionTime(bp, assemblerDef, assembler);
                            totalTime += time;

                            tmp.Append("• ").ShortNumber(amount).Append("x ");

                            // assembler.CurrentProgress is for the currently processed item but it can be confusing for most items as they build super fast and the number is going to be similar and drifting slightly

                            // need access to MyAssembler.CurrentItemIndex to determine which queue item is actually being built
                        }
                        else // refinery
                        {
                            time = amount * Hardcoded.Refinery_BpProductionTime(bp, refineryDef, refinery);
                            totalTime += time;

                            tmp.Append(i + 1).Append(". ");

                            //if(bp.Prerequisites.Length == 1 && bp.Results.Length == 1)
                            //{
                            //    amount = (float)bp.Results[0].Amount / (float)bp.Prerequisites[0].Amount;
                            //    tmp.Append(" x").ShortNumber(amount);
                            //}
                        }

                        tmp.DefinitionName(item.Blueprint).Append(" (").TimeFormat(time).Append(")\n");
                    }

                    info.Append("Queue: ").Append(production.IsProducing ? "Working..." : "STOPPED").Append('\n');
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
                        info.Append("• ").Number(amount).Append("x ").DefinitionName(item.Blueprint).Append('\n');
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

            Suffix_ControlledBy(block, info, "In seat: ");

            Suffix_ShipController(block, info);
        }

        void Format_RemoteControl(IMyTerminalBlock block, StringBuilder info)
        {
            // Vanilla info in 1.189.041:
            //      Type: <BlockDefName>
            //      Max Required Input: <n> W

            info.DetailInfo_CurrentPowerUsage(Sink);

            Suffix_ControlledBy(block, info);

            Suffix_ShipController(block, info);
        }

        void Suffix_ControlledBy(IMyTerminalBlock block, StringBuilder info, string label = "Control: ")
        {
            IMyControllableEntityModAPI ctrl = block as IMyControllableEntityModAPI;
            if(ctrl != null)
            {
                info.Append(label);

                IMyPlayer player = MyAPIGateway.Players.GetPlayerControllingEntity(block);
                if(player != null)
                    info.AppendMaxLength(player.DisplayName, 24);
                else
                    info.Append("-");

                info.Append('\n');
                return;
            }
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
                            float displacement = minDisplacement;
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
                MatrixD topMatrix = data.TopLocalMatrix * block.WorldMatrix;

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

            IMyAirVent airVent = block as IMyAirVent;
            if(airVent == null)
                return;

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

            // can't use block.CubeGrid.GasSystem clientside as it is null.
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

            // H2 engine only uses one fuel but can be any gas.
            info.DetailInfo_InputGasList(Sink);

            IMyPowerProducer producer = block as IMyPowerProducer;
            MyHydrogenEngineDefinition def = block.SlimBlock.BlockDefinition as MyHydrogenEngineDefinition;
            if(producer != null && def != null)
            {
                MyDefinitionId fuelId = def.Fuel.FuelId;
                float filledGasTank = Source.RemainingCapacityByType(fuelId);

                // HACK: hardcoded h2 fuel consumption
                float maxBurnRate = (def.MaxPowerOutput / def.FuelProductionToCapacityMultiplier);
                float burnRate = (producer.CurrentOutput / producer.MaxOutput) * maxBurnRate;

                info.DetailInfo_CustomGas("Uses", fuelId, burnRate, maxBurnRate);

                if(filledGasTank < def.FuelCapacity)
                {
                    float input = Sink.CurrentInputByType(fuelId);

                    if(input > burnRate)
                    {
                        float filling = (input - burnRate);
                        double remainingGas = def.FuelCapacity - filledGasTank;
                        float timeToFill = (float)(remainingGas / filling);
                        info.Append("Filled in: ").TimeFormat(timeToFill).Append('\n');
                    }
                    else if(burnRate > input)
                    {
                        float draining = (burnRate - input);
                        float timeToEmpty = (float)(filledGasTank / draining);
                        info.Append("Empty in: ").TimeFormat(timeToEmpty).Append('\n');
                    }
                }
            }

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

                MyThrust thrustInternal = (MyThrust)block;
                info.Append("Connected to fuel: ").Append(thrustInternal.IsPowered ? "Yes" : "No - WARNING!").Append('\n');

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

            //info.Append('\n');

            float effMulMax = Math.Max(def.EffectivenessAtMinInfluence, def.EffectivenessAtMaxInfluence);

            // HACK NOTE: def.NeedsAtmosphereForInfluence does nothing, influence is always air density
            if(def.EffectivenessAtMinInfluence != 1.0f || def.EffectivenessAtMaxInfluence != 1.0f)
            {
                // renamed to what they actually are for simpler code
                float minAir = def.MinPlanetaryInfluence;
                float maxAir = def.MaxPlanetaryInfluence;
                float thrustAtMinAir = def.EffectivenessAtMinInfluence;
                float thrustAtMaxAir = def.EffectivenessAtMaxInfluence;

                // flip values if they're in wrong order
                if(def.InvDiffMinMaxPlanetaryInfluence < 0)
                {
                    minAir = def.MaxPlanetaryInfluence;
                    maxAir = def.MinPlanetaryInfluence;
                    thrustAtMinAir = def.EffectivenessAtMaxInfluence;
                    thrustAtMaxAir = def.EffectivenessAtMinInfluence;
                }

                thrustAtMinAir /= effMulMax;
                thrustAtMaxAir /= effMulMax;

                info.Append("Current Max Thrust: ").ForceFormat(thrust.MaxEffectiveThrust).Append('\n'); // already multiplied by planetary effectiviness
                info.Append("Optimal Max Thrust: ").ForceFormat(thrust.MaxThrust * effMulMax).Append('\n');
                info.Append("Limits:\n");

                // if mod has weird values, can't really present them in an understandable manner so just printing the values instead
                if(!Hardcoded.Thrust_HasSaneLimits(def))
                {
                    info.Append(" Min air density: ").ProportionToPercent(minAir).Append('\n');
                    info.Append(" Max air density: ").ProportionToPercent(maxAir).Append('\n');
                    info.Append(" Thrust at min air: ").ProportionToPercent(thrustAtMinAir).Append('\n');
                    info.Append(" Thrust at max air: ").ProportionToPercent(thrustAtMaxAir).Append('\n');
                    //info.Append(" NeedsAtmosphereForInfluence: ").Append(def.NeedsAtmosphereForInfluence).Append(".\n");
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
                    if(minAir <= 0f) // || def.NeedsAtmosphereForInfluence
                        info.Append("in vacuum.");
                    else
                        info.Append("below ").ProportionToPercent(minAir).Append(" air density.");
                    info.Append('\n');
                }
            }
            else
            {
                info.Append("Max Thrust: ").ForceFormat(thrust.MaxThrust * effMulMax).Append('\n');
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

            info.Append("Distance: ");
            if(antenna.Other != null)
                info.DistanceFormat((float)Vector3D.Distance(antenna.GetPosition(), antenna.Other.GetPosition()));
            else
                info.Append("N/A");
            info.Append('\n');

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

            IMyGasGenerator generator = block as IMyGasGenerator;
            MyOxygenGeneratorDefinition def = block?.SlimBlock?.BlockDefinition as MyOxygenGeneratorDefinition;
            if(generator == null || def == null)
                return;

            info.DetailInfo_CurrentPowerUsage(Sink);

            MyInventory inv = block.GetInventory(0) as MyInventory;

            MyResourceSourceComponent source = Source;
            if(source != null)
            {
                // HACK: hardcoded from MyGasGenerator
                float totalConsume = 0;
                foreach(MyOxygenGeneratorDefinition.MyGasGeneratorResourceInfo gas in def.ProducedGases)
                {
                    float output = source.CurrentOutputByType(gas.Id) * generator.ProductionCapacityMultiplier; // like in DoUpdateTimerTick() and GasOutputPerSecond()
                    float iceToGasRatio = (source.DefinedOutputByType(gas.Id) / def.IceConsumptionPerSecond); // like in IceToGasRatio()
                    float consume = output / iceToGasRatio; // like in GasToIce()
                    totalConsume += consume;
                }

                MyDefinitionId? consumedItemDefId = null;
                if(inv != null)
                {
                    // HACK: like in ConsumeFuel(), gas generator just grabs any non-bottle item, no point in going through definition then
                    foreach(MyPhysicalInventoryItem item in inv.GetItems())
                    {
                        if(item.Content == null || item.Content is MyObjectBuilder_GasContainerObject)
                            continue;

                        consumedItemDefId = item.Content.GetId();
                        break;
                    }
                }

                if(consumedItemDefId.HasValue)
                    info.Append("Consumes: ").ItemName(consumedItemDefId.Value).Append(" x").Number(totalConsume).Append("/s\n");
                else
                    info.Append("Consumes: 0/s\n");

                info.DetailInfo_OutputGasList(source);
            }

            if(inv != null)
            {
                int bottlesFull = 0;
                int bottlesToFill = 0;

                foreach(MyPhysicalInventoryItem item in inv.GetItems())
                {
                    var bottleOB = item.Content as MyObjectBuilder_GasContainerObject;
                    if(bottleOB != null)
                    {
                        if(bottleOB.GasLevel < 1f)
                            bottlesToFill++;
                        else
                            bottlesFull++;
                    }
                }

                info.Append("Bottles: ");

                if(bottlesToFill == 0 && bottlesFull == 0)
                {
                    info.Append('0');
                }
                else
                {
                    if(bottlesToFill > 0)
                    {
                        info.Append(bottlesToFill).Append(" empty");
                    }

                    if(bottlesFull > 0)
                    {
                        if(bottlesToFill > 0)
                            info.Append(", ");

                        info.Append(bottlesFull).Append(" full");
                    }
                }

                info.Append('\n');
            }

            info.DetailInfo_Inventory(Inv);
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

            IMyGasTank tank = block as IMyGasTank;
            MyGasTankDefinition tankDef = block.SlimBlock.BlockDefinition as MyGasTankDefinition;
            if(tank != null && tank.FilledRatio < 1 && tankDef != null && Sink != null && Source != null)
            {
                float input = Sink.CurrentInputByType(tankDef.StoredGasId);
                float output = Source.CurrentOutputByType(tankDef.StoredGasId);
                double filledGas = tank.FilledRatio * tank.Capacity;

                float leak = 0;
                if(!block.IsFunctional)
                {
                    float ratioPerSec = (tankDef.LeakPercent / 100f) * 60f; // HACK: LeakPercent gets subtracted every 100 ticks
                    leak = ratioPerSec * tankDef.Capacity;

                    if(leak > 0)
                    {
                        info.Append("Leaking: -").VolumeFormat(leak).Append("/s\n");
                        output += leak;
                    }
                    else if(leak < 0)
                    {
                        info.Append("Magic refill: +").VolumeFormat(Math.Abs(leak)).Append("/s\n");
                        input += leak;
                    }
                }

                if(input == output)
                {
                }
                else if(input > output)
                {
                    float filling = (input - output);
                    double remainingGas = tank.Capacity - filledGas;
                    float timeToFill = (float)(remainingGas / filling);
                    info.Append("Filled in: ").TimeFormat(timeToFill).Append('\n');
                }
                else
                {
                    float draining = (output - input);
                    float timeToEmpty = (float)(filledGas / draining);
                    info.Append("Empty in: ").TimeFormat(timeToEmpty).Append('\n');
                }
            }
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
            // Vanilla info is empty, only the actual scripts print to detailed info.

            StringBuilder echoText = block.GetDetailedInfo();
            if(echoText != null && echoText.Length > 0)
                return; // only print something if PB itself doesn't

            if(!MyAPIGateway.Session.SessionSettings.EnableIngameScripts)
            {
                info.Append("In-game Scripts are disabled in world settings!\n");
                return;
            }

            //if(MyAPIGateway.Session.SessionSettings.EnableScripterRole && MyAPIGateway.Session?.Player != null && MyAPIGateway.Session.Player.PromoteLevel < MyPromoteLevel.Scripter)
            //{
            //    info.Append("Scripter role is required to use in-game scripts.\n");
            //}

            // janky fix for PB clearing its detailed info for player-host/SP; non-issue for MP clients (because of the message arriving after the clear)
            PBEcho pbe;
            if(MyAPIGateway.Multiplayer.IsServer && Main.PBMonitor.PBEcho.TryGetValue(block.EntityId, out pbe))
            {
                float sec = (float)Math.Round((Main.Tick - pbe.AtTick) / 60f);
                info.Append(Main.Config.TerminalDetailInfoHeader.Value ? "(Text from " : "(BuildInfo: text from ").TimeFormat(sec).Append(" ago)\n\n");
                info.Append(pbe.EchoText).Append('\n');

                //echoText.Append(pbe.EchoText);
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