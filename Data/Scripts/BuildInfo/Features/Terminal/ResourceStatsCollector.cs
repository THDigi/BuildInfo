using System;
using System.Collections.Generic;
using System.Diagnostics;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.Terminal
{
    // FIXME: needs re-do in a reliable and performant way...
    public class ResourceStatsCollector : IDisposable
    {
        public ResourceStatsCollector(BuildInfoMod main) { }
        public void Dispose() { }
        public void Reset(string debugReason) { }

#if false
        public class Stats
        {
            public int Reactors;
            public int ReactorsWorking;

            public int Engines;
            public int EnginesWorking;

            public int Batteries;
            public int BatteriesWorking;

            public int SolarPanels;
            public int SolarPanelsWorking;

            public int WindTurbines;
            public int WindTurbinesWorking;

            public int OtherProducers;
            public int OtherProducersWorking;

            public float PowerRequired;
            public float PowerOutputCapacity;

            public void Reset()
            {
                Reactors = 0;
                ReactorsWorking = 0;
                Engines = 0;
                EnginesWorking = 0;
                Batteries = 0;
                BatteriesWorking = 0;
                SolarPanels = 0;
                SolarPanelsWorking = 0;
                WindTurbines = 0;
                WindTurbinesWorking = 0;
                OtherProducers = 0;
                OtherProducersWorking = 0;
                PowerRequired = 0;
                PowerOutputCapacity = 0;
            }
        }

        public Stats ComputedStats = new Stats();
        Stats ProcessingStats = new Stats();

        bool FullScan = true;
        int RecheckAfterTick = 0;
        IMyCubeGrid LastGrid;

        readonly List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
        public readonly List<MyCubeBlock> Blocks = new List<MyCubeBlock>(); // DEBUG should not be public
        int BlocksVersion = 0;
        IEnumerator<int> StateMachine;

        readonly BuildInfoMod Main;

        // DEBUG diagnostics
        public double RefreshFullMs = 0;
        public double RefreshWorkingMs = 0;
        readonly Stopwatch Stopwatch = new Stopwatch();

        public ResourceStatsCollector(BuildInfoMod main)
        {
            Main = main;
            StateMachine = RefreshWorking().GetEnumerator();
        }

        public void Dispose()
        {
            StateMachine?.Dispose();
        }

        public void Reset(string debugReason)
        {
            DebugLog.PrintHUD(this, $"Reset, reason={debugReason}", log: true); // DEBUG

            RecheckAfterTick = 0; // remove cooldown to instantly rescan
            LastGrid = null;

            FullScan = true;

            foreach(IMyCubeGrid grid in Grids)
            {
                grid.OnBlockAdded -= GridBlockAdded;
                grid.OnBlockRemoved -= GridBlockRemoved;
            }

            Grids.Clear(); // NOTE: don't clear it anywhere else without unhooking events ^

            Blocks.Clear();
            BlocksVersion++;

            ComputedStats.Reset();
            ProcessingStats.Reset();
        }

        public void Update(IMyCubeGrid startFromGrid)
        {
            if(startFromGrid == null || RecheckAfterTick > Main.Tick)
                return;

            if(startFromGrid != LastGrid)
            {
                Reset("grid changed in local update");
                LastGrid = startFromGrid;
            }

            RecheckAfterTick = Constants.TICKS_PER_SECOND * 3;

            Stopwatch.Restart();
            if(FullScan)
            {
                MyAPIGateway.GridGroups.GetGroup(startFromGrid, GridLinkTypeEnum.Electrical, Grids);

                for(int i = 0; i < Grids.Count; i++)
                {
                    IMyCubeGrid grid = Grids[i];
                    MyCubeGrid gridInternal = (MyCubeGrid)grid;

                    grid.OnBlockAdded += GridBlockAdded;
                    grid.OnBlockRemoved += GridBlockRemoved;

                    foreach(MyCubeBlock block in gridInternal.GetFatBlocks())
                    {
                        MyThrust thrustInternal = block as MyThrust; // HACK: thrusters have shared sinks and not reliable to get
                        MyGyro gyroInternal = (thrustInternal == null ? block as MyGyro : null); // HACK: gyro has no sink 
                        IMyBatteryBlock battery = (thrustInternal == null && gyroInternal == null ? block as IMyBatteryBlock : null); // HACK: batteries need special handling

                        if(gyroInternal != null)
                        {
                            Blocks.Add(block);
                        }
                        else if(thrustInternal != null)
                        {
                            MyThrustDefinition def = thrustInternal.BlockDefinition;
                            if(def.FuelConverter == null || def.FuelConverter.FuelId == MyResourceDistributorComponent.ElectricityId)
                            {
                                Blocks.Add(block);
                            }
                        }
                        else if(battery != null)
                        {
                            Blocks.Add(block);
                        }
                        else
                        {
                            MyResourceSinkComponent sink = block.Components?.Get<MyResourceSinkComponent>();
                            if(sink != null && sink.AcceptedResources.IndexOf(MyResourceDistributorComponent.ElectricityId) != -1)
                            {
                                Blocks.Add(block);
                            }
                        }

                        MyResourceSourceComponent source = block.Components?.Get<MyResourceSourceComponent>();
                        if(source != null && source.ResourceTypes.IndexOf(MyResourceDistributorComponent.ElectricityId) != -1)
                        {
                            Blocks.Add(block);

                            if(battery != null)
                                ProcessingStats.Batteries++;
                            else if(block.BlockDefinition.Id.TypeId == Constants.HydrogenEngineType)
                                ProcessingStats.Engines++;
                            else if(block.BlockDefinition.Id.TypeId == Constants.WindTurbineType)
                                ProcessingStats.WindTurbines++;
                            else if(block is IMyReactor)
                                ProcessingStats.Reactors++;
                            else if(block is IMySolarPanel)
                                ProcessingStats.SolarPanels++;
                            else
                                ProcessingStats.OtherProducers++;
                        }
                    }
                }

                BlocksVersion++;
                FullScan = false;

                Stopwatch.Stop();
                RefreshFullMs = Stopwatch.Elapsed.TotalMilliseconds;
            }

            Stopwatch.Restart();

            StateMachine.MoveNext();

            Stopwatch.Stop();
            RefreshWorkingMs = Stopwatch.Elapsed.TotalMilliseconds;
        }

        IEnumerable<int> RefreshWorking()
        {
            while(true)
            {
                DebugLog.PrintHUD(this, $"statemachine start", log: true); // DEBUG

                ProcessingStats.ReactorsWorking = 0;
                ProcessingStats.EnginesWorking = 0;
                ProcessingStats.BatteriesWorking = 0;
                ProcessingStats.SolarPanelsWorking = 0;
                ProcessingStats.WindTurbinesWorking = 0;
                ProcessingStats.OtherProducersWorking = 0;

                ProcessingStats.PowerRequired = Hardcoded.Conveyors_PowerReqPerGrid * Grids.Count;
                ProcessingStats.PowerOutputCapacity = 0;

                int processPerRun = Math.Max(Blocks.Count / 10, 1000);
                int processed = 0;

                int startedOnVersion = BlocksVersion;

                for(int i = Blocks.Count - 1; i >= 0; i--)
                {
                    MyCubeBlock block = Blocks[i];
                    MyThrust thrustInternal = block as MyThrust; // HACK: thrusters have shared sinks and not reliable to get
                    MyGyro gyroInternal = (thrustInternal == null ? block as MyGyro : null); // HACK: gyro has no sink

                    if(gyroInternal != null)
                    {
                        IMyGyro gyro = gyroInternal;
                        if(gyro.IsFunctional && gyro.Enabled)
                        {
                            ProcessingStats.PowerRequired += gyroInternal.RequiredPowerInput;
                        }
                    }
                    else if(thrustInternal != null)
                    {
                        IMyThrust thrust = thrustInternal;
                        if(thrust.IsFunctional && thrust.Enabled)
                        {
                            Hardcoded.ThrustInfo thrustInfo = Hardcoded.Thrust_GetUsage(thrust);
                            ProcessingStats.PowerRequired += thrustInfo.CurrentUsage;
                        }
                    }
                    else
                    {
                        MyResourceSinkComponent sink = block.Components?.Get<MyResourceSinkComponent>();
                        if(sink != null)
                        {
                            float required;
                            if(block is IMyBatteryBlock)
                                required = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
                            else
                                required = sink.RequiredInputByType(MyResourceDistributorComponent.ElectricityId);

                            ProcessingStats.PowerRequired += required;
                        }
                    }

                    MyResourceSourceComponent source = block.Components?.Get<MyResourceSourceComponent>();
                    if(source != null && source.ProductionEnabledByType(MyResourceDistributorComponent.ElectricityId))
                    {
                        float maxOutput = source.MaxOutputByType(MyResourceDistributorComponent.ElectricityId);
                        ProcessingStats.PowerOutputCapacity += maxOutput;

                        float output = source.CurrentOutputByType(MyResourceDistributorComponent.ElectricityId);
                        if(output > 0)
                        {
                            if(block.BlockDefinition.Id.TypeId == Constants.HydrogenEngineType)
                                ProcessingStats.EnginesWorking++;
                            else if(block.BlockDefinition.Id.TypeId == Constants.WindTurbineType)
                                ProcessingStats.WindTurbinesWorking++;
                            else if(block is IMyReactor)
                                ProcessingStats.ReactorsWorking++;
                            else if(block is IMyBatteryBlock)
                                ProcessingStats.BatteriesWorking++;
                            else if(block is IMySolarPanel)
                                ProcessingStats.SolarPanelsWorking++;
                            else
                                ProcessingStats.OtherProducersWorking++;
                        }
                    }

                    // DEBUG hardcoded
                    if(++processed > processPerRun)
                    {
                        DebugLog.PrintHUD(this, $"statemachine processed {processed}, yielded...", log: true); // DEBUG
                        processed = 0;

                        yield return 0;

                        if(BlocksVersion != startedOnVersion)
                        {
                            DebugLog.PrintHUD(this, $"statemachine reset?", log: true); // DEBUG
                            break; // reset from the start 
                        }
                    }
                }

                // done computing, push results
                MyUtils.Swap(ref ProcessingStats, ref ComputedStats);

                // sync the totals
                ProcessingStats.Batteries = ComputedStats.Batteries;
                ProcessingStats.Engines = ComputedStats.Engines;
                ProcessingStats.WindTurbines = ComputedStats.WindTurbines;
                ProcessingStats.Reactors = ComputedStats.Reactors;
                ProcessingStats.SolarPanels = ComputedStats.SolarPanels;
                ProcessingStats.OtherProducers = ComputedStats.OtherProducers;

                if(processed > 0)
                    yield return 0;
            }
        }

        void GridBlockAdded(IMySlimBlock slimBlock)
        {
            try
            {
                MyCubeBlock block = slimBlock.FatBlock as MyCubeBlock;
                if(block == null)
                    return;

                // Copy of RefreshGatherBlocks()'s loop content
                // NOTE: BlocksVersion++; was added after each .Add()!

                MyThrust thrustInternal = block as MyThrust; // HACK: thrusters have shared sinks and not reliable to get
                MyGyro gyroInternal = (thrustInternal == null ? block as MyGyro : null); // HACK: gyro has no sink 
                IMyBatteryBlock battery = (thrustInternal == null && gyroInternal == null ? block as IMyBatteryBlock : null); // HACK: batteries need special handling

                if(gyroInternal != null)
                {
                    Blocks.Add(block);
                    BlocksVersion++;
                }
                else if(thrustInternal != null)
                {
                    MyThrustDefinition def = thrustInternal.BlockDefinition;
                    if(def.FuelConverter == null || def.FuelConverter.FuelId == MyResourceDistributorComponent.ElectricityId)
                    {
                        Blocks.Add(block);
                        BlocksVersion++;
                    }
                }
                else if(battery != null)
                {
                    Blocks.Add(block);
                    BlocksVersion++;
                }
                else
                {
                    MyResourceSinkComponent sink = block.Components?.Get<MyResourceSinkComponent>();
                    if(sink != null && sink.AcceptedResources.IndexOf(MyResourceDistributorComponent.ElectricityId) != -1)
                    {
                        Blocks.Add(block);
                        BlocksVersion++;
                    }
                }

                MyResourceSourceComponent source = block.Components?.Get<MyResourceSourceComponent>();
                if(source != null && source.ResourceTypes.IndexOf(MyResourceDistributorComponent.ElectricityId) != -1)
                {
                    Blocks.Add(block);
                    BlocksVersion++;

                    if(battery != null)
                        ProcessingStats.Batteries++;
                    else if(block.BlockDefinition.Id.TypeId == Constants.HydrogenEngineType)
                        ProcessingStats.Engines++;
                    else if(block.BlockDefinition.Id.TypeId == Constants.WindTurbineType)
                        ProcessingStats.WindTurbines++;
                    else if(block is IMyReactor)
                        ProcessingStats.Reactors++;
                    else if(block is IMySolarPanel)
                        ProcessingStats.SolarPanels++;
                    else
                        ProcessingStats.OtherProducers++;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void GridBlockRemoved(IMySlimBlock slimBlock)
        {
            try
            {
                MyCubeBlock block = slimBlock.FatBlock as MyCubeBlock;
                if(block == null)
                    return;

                Blocks.Remove(block);
                BlocksVersion++;
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
#endif
    }
}
