using System;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo
{
    public static class GameData
    {
        // HACK: various hardcoded data from game sources
        public static class Hardcoded
        {
            public const float GAME_EARTH_GRAVITY = 9.81f;

            // from MyGridConveyorSystem
            public const float Conveyors_PowerReq = MyEnergyConstants.REQUIRED_INPUT_CONVEYOR_LINE;

            // from MyShipConnector
            public const string ShipConnector_PowerGroup = "Conveyors";
            public static float ShipConnector_PowerReq(MyCubeBlockDefinition def)
            {
                var requiredPower = MyEnergyConstants.MAX_REQUIRED_POWER_CONNECTOR;

                if(def.CubeSize == MyCubeSize.Small)
                    requiredPower *= 0.01f;

                return requiredPower;
            }
            public static float ShipConnector_InventoryVolume(MyCubeBlockDefinition def)
            {
                var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                return (def.Size * gridSize * 0.8f).Volume;
            }

            // from MyShipDrill
            public const float ShipDrill_Power = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_DRILL;
            public static readonly MyObjectBuilderType ShipDrill_InventoryConstraint = typeof(MyObjectBuilder_Ore);
            public static float ShipDrill_InventoryVolume(MyCubeBlockDefinition def)
            {
                var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                return (float)(def.Size.X * def.Size.Y * def.Size.Z) * gridSize * gridSize * gridSize * 0.5f;
            }

            // from MyShipToolBase
            public const float ShipTool_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_GRINDER;
            public const string ShipTool_PowerGroup = "Defense";
            public static float ShipTool_InventoryVolume(MyCubeBlockDefinition def)
            {
                var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                return (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize * 0.5f;
            }
            public const float ShipTool_ReachDistance = 4.5f; // MyShipToolBase.DEFAULT_REACH_DISTANCE

            // from MyShipWelder
            public const float ShipWelder_WeldPerSecond = 2f; // MyShipWelder.WELDER_AMOUNT_PER_SECOND; also NOTE: this is used for both weld and grind multiplier for aim info

            // from MyShipGrinder
            public const float ShipGrinder_GrindPerSecond = MyShipGrinderConstants.GRINDER_AMOUNT_PER_SECOND;

            // from MyButtonPanel
            public const float ButtonPanel_PowerReq = 0.0001f;

            // from MySensorBlock
            public static Vector3 Sensor_MaxField(float maxRange) => new Vector3(maxRange * 2);
            public static float Sensor_PowerReq(Vector3 maxField)
            {
                // sensor.RequiredPowerInput exists but is always reporting 0 and it seems ignored in the source code (see: MySensorBlock.CalculateRequiredPowerInput())
                Vector3 minField = Vector3.One;
                return 0.0003f * (float)Math.Pow((maxField - minField).Volume, 1f / 3f);
            }

            // from MyCockpit
            public const float Cockpit_InventoryVolume = 1f; // Vector3.One.Volume;

            // from MyOreDetector
            public const float OreDetector_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_ORE_DETECTOR;

            // from MyDoor
            public const float Door_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_DOOR;
            public const float Door_Closed_DisassembleRatioMultiplier = 3.3f; // both MyDoor and MyAdvanced door override DisassembleRatio and multiply by this when closed
            public static float Door_MoveSpeed(float openingSpeed)
            {
                return (1f / ((MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS / 1000f) * openingSpeed)) / MyEngineConstants.UPDATE_STEPS_PER_SECOND; // computed after MyDoor.UpdateCurrentOpening()
            }
            public static void AdvDoor_MoveSpeed(MyAdvancedDoorDefinition advDoor, out float openTime, out float closeTime)
            {
                openTime = 0;
                closeTime = 0;

                foreach(var seq in advDoor.OpeningSequence)
                {
                    var moveTime = (seq.MaxOpen / seq.Speed);

                    openTime = Math.Max(openTime, seq.OpenDelay + moveTime);
                    closeTime = Math.Max(closeTime, seq.CloseDelay + moveTime);
                }
            }

            // from MyMedicalRoom
            public const float MedicalRoom_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_MEDICAL_ROOM;

            // from MyRadioAntenna
            public static float RadioAntenna_PowerReq(float maxRange) => (maxRange / 500f) * MyEnergyConstants.MAX_REQUIRED_POWER_ANTENNA;

            // from MyBeacon
            public static float Beacon_PowerReq(float maxRange) => (maxRange / 100000f) * MyEnergyConstants.MAX_REQUIRED_POWER_BEACON;

            // from MyReactor
            public static float Reactor_KgPerSec(MyResourceSourceComponent source, MyReactorDefinition reactorDef) => ((source.CurrentOutput / (source.ProductionToCapacityMultiplier * 1000f)) / reactorDef.FuelDefinition.Mass) * 1000;

            // from MyTimerBlock
            public const float Timer_PowerReq = 1E-07f;

            // from MyProgrammableBlock
            public const float ProgrammableBlock_PowerReq = 0.0005f;

            // from MySoundBlock
            public const float SoundBlock_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_SOUNDBLOCK;

            // from MyCargoContainer
            public static float CargoContainer_InventoryVolume(MyCubeBlockDefinition def)
            {
                var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                return (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize;
            }

            // from MyLargeTurretBase
            public const float Turret_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_TURRET;

            // from MyLargeTurretBase.RotationAndElevation() - rotation speed is radisans per milisecond(ish) (since it uses 16, not 1/60)
            public const float Turret_RotationSpeedMul = MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS * 60;

            // from MyLaserAntenna.RotationAndElevation() - rotation speed is radians per milisecond
            public const float LaserAntenna_RotationSpeedMul = 1000;

            /// <summary>
            /// Distance in meters, returns power in MW.
            /// </summary>
            public static float LaserAntenna_PowerUsage(MyLaserAntennaDefinition def, double distanceMeters)
            {
                // HACK copied and converted from MyLaserAntenna.UpdatePowerInput()

                double powerRatio = def.PowerInputLasing;
                double maxRange = (def.MaxRange < 0 ? double.MaxValue : def.MaxRange);

                double A = powerRatio / 2.0 / 200000.0;
                double B = powerRatio * 200000.0 - A * 200000.0 * 200000.0;
                double distance = Math.Min(distanceMeters, maxRange);

                if(distance > 200000)
                {
                    return (float)((distance * distance) * A + B) / 1000000f;
                }
                else
                {
                    return (float)(powerRatio * distance) / 1000000f;
                }
            }

            // used to determine what range is considered infinite
            public const float LaserAntenna_InfiniteRange = 100000000;

            // from MySmallMissileLauncher & MySmallGatlingGun
            public const float ShipGun_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_GUN;

            // from MyMissile.UpdateBeforeSimulation()
            public const float Missile_DesiredSpeedMultiplier = 0.7f;

            // from MyAirVent.VentDummy getter
            public const string AirVent_DummyName = "vent_001";

            public static void Parachute_GetDetails(MyParachuteDefinition parachute, float targetDescendVelocity, out float maxMass, out float disreefAtmosphere)
            {
                // formulas from MyParachute.UpdateParachute()
                float atmosphere = 1.0f;
                float atmosMod = 10.0f * (atmosphere - parachute.ReefAtmosphereLevel);

                if(atmosMod <= 0.5f || double.IsNaN(atmosMod))
                {
                    atmosMod = 0.5f;
                }
                else
                {
                    atmosMod = (float)Math.Log(atmosMod - 0.99f) + 5.0f;

                    if(atmosMod < 0.5f || double.IsNaN(atmosMod))
                        atmosMod = 0.5f;
                }

                float gridSize = MyDefinitionManager.Static.GetCubeSize(parachute.CubeSize);

                // basically the atmosphere level at which atmosMod is above 0.5; finds real atmosphere level at which chute starts to fully open
                // thanks to Equinox for helping with the math here and at maxMass :}
                disreefAtmosphere = ((float)Math.Exp(-4.5) + 1f) / 10 + parachute.ReefAtmosphereLevel;

                float chuteSize = (atmosMod * parachute.RadiusMultiplier * gridSize) / 2.0f;
                float chuteArea = MathHelper.Pi * chuteSize * chuteSize;
                float realAirDensity = (atmosphere * 1.225f);

                maxMass = 2.5f * realAirDensity * (targetDescendVelocity * targetDescendVelocity) * chuteArea * parachute.DragCoefficient / GAME_EARTH_GRAVITY;
            }
        }

        /// <summary>
        /// Returns maximum possible force applied to targetBlock's grid from sourceGrid's grinder.
        /// </summary>
        public static float ShipGrinderImpulseForce(IMyCubeGrid sourceGrid, IMySlimBlock targetBlock)
        {
            var targetGrid = targetBlock.CubeGrid;

            if(MyAPIGateway.Session.SessionSettings.EnableToolShake && targetGrid.Physics != null && !targetGrid.Physics.IsStatic)
            {
                var f = 1.73205078f; // MyUtils.GetRandomVector3()'s max length
                return (f * sourceGrid.GridSize * 500f);
            }

            return 0f;
        }

        /// <summary>
        /// Because the game has 2 ownership systems and I've no idea which one is actually used in what case, and it doesn't seem it knows either since it uses both in initialization
        /// </summary>
        public static MyOwnershipShareModeEnum GetBlockShareMode(IMyCubeBlock block)
        {
            if(block != null)
            {
                var internalBlock = (MyCubeBlock)block;

                // HACK MyEntityOwnershipComponent is not whitelisted
                //var ownershipComp = internalBlock.Components.Get<MyEntityOwnershipComponent>();
                //
                //if(ownershipComp != null)
                //    return ownershipComp.ShareMode;

                if(internalBlock.IDModule != null)
                    return internalBlock.IDModule.ShareMode;
            }

            return MyOwnershipShareModeEnum.None;
        }
    }
}
