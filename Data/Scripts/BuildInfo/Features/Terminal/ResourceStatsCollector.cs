using System.Collections.Generic;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.Terminal
{
    public class ResourceStatsCollector
    {
        public int Reactors { get; private set; }
        public int ReactorsWorking { get; private set; }

        public int Engines { get; private set; }
        public int EnginesWorking { get; private set; }

        public int Batteries { get; private set; }
        public int BatteriesWorking { get; private set; }

        public int SolarPanels { get; private set; }
        public int SolarPanelsWorking { get; private set; }

        public int WindTurbines { get; private set; }
        public int WindTurbinesWorking { get; private set; }

        public int Other { get; private set; }
        public int OthersWorking { get; private set; }

        public int Consumers { get; private set; }
        public int ConsumersWorking { get; private set; }

        public float PowerInput { get; private set; }
        public float PowerRequired { get; private set; }

        public float PowerOutput { get; private set; }
        public float PowerMaxOutput { get; private set; }

        bool FullScan = true;
        int RecheckAfterTick = 0;
        IMyCubeGrid LastGrid;

        readonly List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();

        readonly List<MyCubeBlock> ProducerBlocks = new List<MyCubeBlock>();

        readonly List<MyCubeBlock> ConsumerBlocks = new List<MyCubeBlock>();
        readonly List<IMyGyro> ConsumerGyros = new List<IMyGyro>();
        readonly List<IMyThrust> ConsumerThrusts = new List<IMyThrust>();

        readonly BuildInfoMod Main;

        // TODO: use the interface when one is added
        readonly MyObjectBuilderType HydrogenEngineType = typeof(MyObjectBuilder_HydrogenEngine);
        readonly MyObjectBuilderType WindTurbineType = typeof(MyObjectBuilder_WindTurbine);

        public ResourceStatsCollector(BuildInfoMod main)
        {
            Main = main;
        }

        public void Reset()
        {
            RecheckAfterTick = 0; // remove cooldown to instantly rescan
            FullScan = true;
            LastGrid = null;

            Grids.Clear();

            ProducerBlocks.Clear();

            ConsumerBlocks.Clear();
            ConsumerGyros.Clear();
            ConsumerThrusts.Clear();
        }

        public void Update(IMyCubeGrid grid)
        {
            if(grid == null || RecheckAfterTick > Main.Tick)
                return;

            if(grid != LastGrid)
            {
                Reset();
                LastGrid = grid;
            }

            RecheckAfterTick = Constants.TICKS_PER_SECOND * 3;

            if(FullScan)
                RefreshEntirely(grid);
            else
                RefreshWorking(grid);
        }

        void RefreshEntirely(IMyCubeGrid startFromGrid)
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
            Other = 0;
            OthersWorking = 0;

            Consumers = 0;
            ConsumersWorking = 0;

            PowerInput = 0;
            PowerRequired = 0;

            PowerOutput = 0;
            PowerMaxOutput = 0;

            ProducerBlocks.Clear();
            ConsumerBlocks.Clear();
            ConsumerGyros.Clear();
            ConsumerThrusts.Clear();

            Grids.Clear();
            MyAPIGateway.GridGroups.GetGroup(startFromGrid, GridLinkTypeEnum.Electrical, Grids);

            for(int i = 0; i < Grids.Count; i++)
            {
                MyCubeGrid grid = (MyCubeGrid)Grids[i];

                foreach(MyCubeBlock block in grid.GetFatBlocks())
                {
                    IMyThrust thrust = block as IMyThrust; // HACK: thrusters have shared sinks and not reliable to get
                    IMyGyro gyro = (thrust == null ? block as IMyGyro : null); // HACK: gyro has no sink 

                    if(gyro != null)
                    {
                        ConsumerGyros.Add(gyro);

                        if(gyro.IsFunctional && gyro.Enabled)
                        {
                            MyGyro internalGyro = (MyGyro)gyro;
                            MyGyroDefinition gyroDef = (MyGyroDefinition)internalGyro.BlockDefinition;

                            PowerInput += internalGyro.RequiredPowerInput;
                            PowerRequired += gyroDef.RequiredPowerInput * gyro.PowerConsumptionMultiplier;
                        }

                        if(block.IsWorking)
                            ConsumersWorking++;
                    }
                    else if(thrust != null)
                    {
                        ConsumerThrusts.Add(thrust);

                        if(thrust.IsFunctional && thrust.Enabled)
                        {
                            Hardcoded.ThrustInfo thrustInfo = Hardcoded.Thrust_GetUsage(thrust);

                            PowerInput += thrustInfo.CurrentUsage;
                            PowerRequired += thrustInfo.MaxUsage;
                        }

                        if(block.IsWorking)
                            ConsumersWorking++;
                    }
                    else
                    {
                        MyResourceSinkComponent sink = block.Components?.Get<MyResourceSinkComponent>();
                        if(sink != null)
                        {
                            ConsumerBlocks.Add(block);

                            float input = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
                            float required = sink.RequiredInputByType(MyResourceDistributorComponent.ElectricityId);

                            PowerInput += input;
                            PowerRequired += required;

                            if(block.IsWorking)
                                ConsumersWorking++;
                        }
                    }

                    MyResourceSourceComponent source = block.Components?.Get<MyResourceSourceComponent>();
                    if(source != null)
                    {
                        float output = source.CurrentOutputByType(MyResourceDistributorComponent.ElectricityId);
                        float maxOutput = source.MaxOutputByType(MyResourceDistributorComponent.ElectricityId);

                        PowerOutput += output;
                        PowerMaxOutput += maxOutput;

                        bool working = (output > 0);

                        if(block is IMyReactor)
                        {
                            Reactors++;
                            if(working)
                                ReactorsWorking++;
                        }
                        else if(block.BlockDefinition.Id.TypeId == HydrogenEngineType)
                        {
                            Engines++;
                            if(working)
                                EnginesWorking++;
                        }
                        else if(block is IMyBatteryBlock)
                        {
                            Batteries++;
                            if(working)
                                BatteriesWorking++;
                        }
                        else if(block is IMySolarPanel)
                        {
                            SolarPanels++;
                            if(working)
                                SolarPanelsWorking++;
                        }
                        else if(block.BlockDefinition.Id.TypeId == WindTurbineType)
                        {
                            WindTurbines++;
                            if(working)
                                WindTurbinesWorking++;
                        }
                        else
                        {
                            Other++;
                            if(working)
                                OthersWorking++;
                        }
                    }
                }
            }

            Consumers = ConsumerBlocks.Count + ConsumerGyros.Count + ConsumerThrusts.Count;
            Grids.Clear();
        }

        void RefreshWorking(IMyCubeGrid grid)
        {
            ReactorsWorking = 0;
            EnginesWorking = 0;
            BatteriesWorking = 0;
            SolarPanelsWorking = 0;
            WindTurbinesWorking = 0;
            OthersWorking = 0;

            ConsumersWorking = 0;

            PowerInput = 0;
            PowerRequired = 0;

            PowerOutput = 0;
            PowerMaxOutput = 0;

            for(int i = (ConsumerGyros.Count - 1); i >= 0; i--)
            {
                IMyGyro gyro = ConsumerGyros[i];
                if(gyro.MarkedForClose)
                {
                    ConsumerGyros.RemoveAtFast(i);
                    continue;
                }

                if(gyro.IsFunctional && gyro.Enabled)
                {
                    MyGyro internalGyro = (MyGyro)gyro;
                    MyGyroDefinition gyroDef = (MyGyroDefinition)internalGyro.BlockDefinition;

                    PowerInput += internalGyro.RequiredPowerInput;
                    PowerRequired += gyroDef.RequiredPowerInput * gyro.PowerConsumptionMultiplier;
                }

                if(gyro.IsWorking)
                    ConsumersWorking++;
            }

            for(int i = (ConsumerThrusts.Count - 1); i >= 0; i--)
            {
                IMyThrust thrust = ConsumerThrusts[i];
                if(thrust.MarkedForClose)
                {
                    ConsumerThrusts.RemoveAtFast(i);
                    continue;
                }

                if(thrust.IsFunctional && thrust.Enabled)
                {
                    Hardcoded.ThrustInfo thrustInfo = Hardcoded.Thrust_GetUsage(thrust);

                    PowerInput += thrustInfo.CurrentUsage;
                    PowerRequired += thrustInfo.MaxUsage;
                }

                if(thrust.IsWorking)
                    ConsumersWorking++;
            }

            for(int i = (ConsumerBlocks.Count - 1); i >= 0; i--)
            {
                MyCubeBlock block = ConsumerBlocks[i];
                MyResourceSinkComponent sink = block.Components?.Get<MyResourceSinkComponent>();
                if(block.MarkedForClose || sink == null)
                {
                    ConsumerBlocks.RemoveAtFast(i);
                    continue;
                }

                float input = sink.CurrentInputByType(MyResourceDistributorComponent.ElectricityId);
                float required = sink.RequiredInputByType(MyResourceDistributorComponent.ElectricityId);

                PowerInput += input;
                PowerRequired += required;

                if(block.IsWorking)
                    ConsumersWorking++;
            }

            Consumers = ConsumerBlocks.Count + ConsumerGyros.Count + ConsumerThrusts.Count;

            for(int i = (ProducerBlocks.Count - 1); i >= 0; i--)
            {
                MyCubeBlock block = ProducerBlocks[i];
                if(block.MarkedForClose)
                {
                    ProducerBlocks.RemoveAtFast(i);
                    continue;
                }

                MyResourceSourceComponent source = block.Components?.Get<MyResourceSourceComponent>();
                if(source == null)
                    continue;

                float output = source.CurrentOutputByType(MyResourceDistributorComponent.ElectricityId);
                float maxOutput = source.MaxOutputByType(MyResourceDistributorComponent.ElectricityId);

                PowerOutput += output;
                PowerMaxOutput += maxOutput;

                if(output > 0)
                {
                    if(block is IMyReactor)
                        ReactorsWorking++;
                    else if(block.BlockDefinition.Id.TypeId == HydrogenEngineType)
                        EnginesWorking++;
                    else if(block is IMyBatteryBlock)
                        BatteriesWorking++;
                    else if(block is IMySolarPanel)
                        SolarPanelsWorking++;
                    else if(block.BlockDefinition.Id.TypeId == WindTurbineType)
                        WindTurbinesWorking++;
                    else
                        OthersWorking++;
                }
            }
        }
    }
}
