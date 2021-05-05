using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.VanillaData
{
    // HACK: various hardcoded data from game sources
    /// <summary>
    /// Various hardcoded data and behavior extracted from game source because it's not directly accessible.
    /// </summary>
    public static class Hardcoded
    {
        public const float GAME_EARTH_GRAVITY = 9.81f;

        // from MyGridConveyorSystem
        public const float Conveyors_PowerReqPerGrid = 0.0000001f; /// <see cref="Features.ChatCommands.CommandHelp.Footer"/>
        public const string Conveyors_PowerGroup = "Conveyors";

        // from MyGridConveyorSystem.NeedsLargeTube()
        public static bool Conveyors_ItemNeedsLargeTube(MyPhysicalItemDefinition physicalItemDefinition)
        {
            if(physicalItemDefinition.Id.TypeId == typeof(MyObjectBuilder_PhysicalGunObject))
                return false;

            return physicalItemDefinition.Size.AbsMax() > 0.25f;
        }

        // from MyAssembler.CalculateBlueprintProductionTime()
        public static float Assembler_BpProductionTime(MyBlueprintDefinitionBase bp, MyAssemblerDefinition assemblerDef, IMyAssembler assembler)
        {
            float speed = (assemblerDef.AssemblySpeed + assembler.UpgradeValues["Productivity"]) * MyAPIGateway.Session.AssemblerSpeedMultiplier;
            return (bp.BaseProductionTimeInSeconds / speed);
        }

        // from MyRefinery.ProcessQueueItems
        public static float Refinery_BpProductionTime(MyBlueprintDefinitionBase bp, MyRefineryDefinition refineryDef, IMyRefinery refinery)
        {
            float speed = (refineryDef.RefineSpeed + refinery.UpgradeValues["Productivity"]) * MyAPIGateway.Session.RefinerySpeedMultiplier;
            return (bp.BaseProductionTimeInSeconds / speed);
        }

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

        // from MyEngineerToolBase
        public const float EngineerToolBase_DefaultReachDistance = 2f;

        // from MyHandDrill.Init()
        public const float HandDrill_DefaultRadius = 0.35f;

        // from MyWelder.WeldAmount
        public static float HandWelder_GetWeldPerSec(float speedMultiplier) => MyAPIGateway.Session.WelderSpeedMultiplier * speedMultiplier * HandWelder_WeldAmountPerSecond;
        private const float HandWelder_WeldAmountPerSecond = 1f;

        // from MyAngleGrinder.GrinderAmount
        public static float HandGrinder_GetGrindPerSec(float speedMultiplier) => MyAPIGateway.Session.GrinderSpeedMultiplier * speedMultiplier * HandGrinder_GrindAmountPerSecond;
        private const float HandGrinder_GrindAmountPerSecond = 2f;

        // from MyShipDrill
        public const float ShipDrill_Power = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_DRILL;
        public static readonly MyObjectBuilderType ShipDrill_InventoryConstraint = typeof(MyObjectBuilder_Ore);
        public static float ShipDrill_InventoryVolume(MyCubeBlockDefinition def)
        {
            var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
            return def.Size.X * def.Size.Y * def.Size.Z * gridSize * gridSize * gridSize * 0.5f;
        }
        public const float ShipDrill_VoxelVisualAdd = 0.6f; // based on visual tests

        // applies to both ship and hand drills
        public const float Drill_MineVoelNoOreRadiusMul = 3f; // MyDrillBase.TryDrillVoxels()
        public const float Drill_MineFloatingObjectRadiusMul = 1.33f; // MyDrillBase.DrillFloatingObject()
        public const float Drill_MineCharacterRadiusMul = 0.8f; // MyDrillBase.DrillCharacter()

        // from MyShipToolBase
        public const float ShipTool_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_GRINDER;
        public const string ShipTool_PowerGroup = "Defense";
        public static float ShipTool_InventoryVolume(MyCubeBlockDefinition def)
        {
            var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
            return (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize * 0.5f;
        }
        public const float ShipTool_ReachDistance = 4.5f; // MyShipToolBase.DEFAULT_REACH_DISTANCE

        // from MyShipWelder.Activate()
        public const int ShipWelder_DivideByTargets = 4;
        public static float ShipWelder_WeldPerSec(int targets)
        {
            float time = MyShipGrinderConstants.GRINDER_COOLDOWN_IN_MILISECONDS / 1000f;
            float coefficient = time / MathHelper.Clamp(targets, 1, 4);
            return ShipTool_ActivationsPerSecond * coefficient * MyAPIGateway.Session.WelderSpeedMultiplier * 4; // MyShipWelder.WELDER_AMOUNT_PER_SECOND
        }

        // from MyShipGrinder.Activate()
        public const int ShipGrinder_DivideByTargets = 4;
        public static float ShipGrinder_GrindPerSec(int targets)
        {
            float time = MyShipGrinderConstants.GRINDER_COOLDOWN_IN_MILISECONDS / 1000f;
            float coefficient = time / MathHelper.Clamp(targets, 1, 4);
            return ShipTool_ActivationsPerSecond * coefficient * MyAPIGateway.Session.GrinderSpeedMultiplier * MyShipGrinderConstants.GRINDER_AMOUNT_PER_SECOND;
        }

        // from testing
        private const float ShipTool_ActivationsPerSecond = 3;

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

        // from MyButtonPanel
        public const float ButtonPanel_PowerReq = 0.0001f;

        // from MySensorBlock
        public static readonly Vector3 Sensor_MinField = new Vector3(0.1f); // MySensorBlock.MIN_RANGE = 0.1f;
        public static Vector3 Sensor_MaxField(float maxRange) => new Vector3(maxRange * 2);
        public static float Sensor_PowerReq(Vector3 maxField)
        {
            // sensor.RequiredPowerInput exists but is always reporting 0 and it seems ignored in the source code (see: MySensorBlock.CalculateRequiredPowerInput())
            return 0.0003f * (float)Math.Pow((maxField - Sensor_MinField).Volume, 1d / 3d);
        }

        // from MyCockpit
        public const float Cockpit_InventoryVolume = 1f; // Vector3.One.Volume;

        // from MyOreDetector
        public const float OreDetector_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_ORE_DETECTOR;

        // from MyDoor
        public const float Door_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_DOOR;
        public const float Door_Closed_DisassembleRatioMultiplier = 3.3f; // both MyDoor and MyAdvanced door override DisassembleRatio and multiply by this when closed
        public static float Door_MoveSpeed(float openingSpeed, float travelDistance = 1f)
        {
            return travelDistance / openingSpeed;
            //return (1f / ((MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS / 1000f) * openingSpeed)) / MyEngineConstants.UPDATE_STEPS_PER_SECOND; // computed after MyDoor.UpdateCurrentOpening()
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

        // from MyBeacon.UpdatePowerInput()
        public static float Beacon_PowerReq(MyBeaconDefinition def, float? range = null)
        {
            if(range.HasValue)
                return (range.Value / def.MaxBroadcastRadius) * (def.MaxBroadcastPowerDrainkW / 1000f);
            else
                return (def.MaxBroadcastPowerDrainkW / 1000f);
        }

        // from MyTimerBlock
        public const float Timer_PowerReq = 1E-07f;

        // from MyProgrammableBlock
        public const float ProgrammableBlock_PowerReq = 0.0005f;

        // from MySoundBlock
        public const float SoundBlock_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_SOUNDBLOCK;

        // from tracking ShotsInBurst
        public static readonly HashSet<MyObjectBuilderType> NoReloadTypes = new HashSet<MyObjectBuilderType>()
        {
            typeof(MyObjectBuilder_InteriorTurret),
            typeof(MyObjectBuilder_SmallGatlingGun),
        };

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

        // from Sandbox.Game.Weapons.MyProjectile.Start()
        public const float Projectile_RangeMultiplier_Min = 0.8f;
        public const float Projectile_RangeMultiplier_Max = 1f;

        // from MyGravityGeneratorSphere.CalculateRequiredPowerInputForRadius()
        public static float SphericalGravGen_PowerReq(MyGravityGeneratorSphereDefinition def, float radius, float gravityAcceleration)
        {
            float currentVolume = (float)(Math.Pow(radius, def.ConsumptionPower) * Math.PI * 0.75);
            float defaultVolume = (float)(Math.Pow(100, def.ConsumptionPower) * Math.PI * 0.75);
            return currentVolume / defaultVolume * def.BasePowerInput * (Math.Abs(gravityAcceleration) / GAME_EARTH_GRAVITY);
        }

        public const float Thrust_DamageCapsuleRadiusAdd = 0.05f; // visual tweak to match what the physics engine hits

        // not from the game but it's relevant to mods using excessive values
        public static bool Thrust_HasSaneLimits(MyThrustDefinition def)
        {
            return (def.MinPlanetaryInfluence >= 0 && def.MinPlanetaryInfluence <= 1f
                 && def.MaxPlanetaryInfluence >= 0 && def.MaxPlanetaryInfluence <= 1f
                 && def.EffectivenessAtMinInfluence >= 0 && def.EffectivenessAtMinInfluence <= 1f
                 && def.EffectivenessAtMaxInfluence >= 0 && def.EffectivenessAtMaxInfluence <= 1f);
        }

        // from MyAirVent.VentDummy getter
        public const string AirVent_DummyName = "vent_001";

        // from MyParachute.UpdateParachute()
        public static void Parachute_GetLoadEstimate(MyParachuteDefinition parachute, float targetDescendVelocity, out float maxMass, out float disreefAtmosphere)
        {
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

        // from MyTextPanelComponent.GetTextureResolutionForAspectRatio()
        public static Vector2I TextSurface_GetResolution(int width, int height, int textureSize)
        {
            if(width == height)
                return new Vector2I(textureSize, textureSize);

            if(width > height)
            {
                int n = MathHelper.Pow2(MathHelper.Log2(width / height));
                return new Vector2I(textureSize * n, textureSize);
            }
            else
            {
                int n = MathHelper.Pow2(MathHelper.Log2(height / width));
                return new Vector2I(textureSize, textureSize * n);
            }
        }

        // from MyEntityThrustComponent
        public const double RelativeDampeners_MaxDistance = 100f;
        public const double RelativeDampeners_MaxDistanceSq = RelativeDampeners_MaxDistance * RelativeDampeners_MaxDistance;

        // from MyEntityThrustComponent.UpdateRelativeDampeningEntity()
        public static bool RelativeDampeners_DistanceCheck(IMyEntity controlledEnt, IMyEntity relativeEnt)
        {
            return relativeEnt.PositionComp.WorldAABB.DistanceSquared(controlledEnt.PositionComp.GetPosition()) <= RelativeDampeners_MaxDistanceSq;
        }

        // from MyBatteryBlock.StorePower()
        public const float BatteryRechargeMultiplier = 0.8f;

        // from MyJumpDrive.StorePower()
        public const float JumpDriveRechargeMultiplier = 0.8f;

        // from MyGridJumpDriveSystem.Jump()
        public const float JumpDriveJumpDelay = 10f;

        // from MyMotorStator.UpdateBeforeSimulation()
        public static float RotorTorqueLimit(IMyMotorStator stator, out float mass)
        {
            mass = 0;
            var topGrid = stator.TopGrid;
            if(topGrid == null)
                return 0;

            float torque = stator.Torque;

            mass = topGrid.Physics.Mass;
            if(!topGrid.IsStatic && mass <= 0)
            {
                // need mass for clients in MP too, like when grids are LG'd
                mass = BuildInfoMod.Instance.GridMassCompute.GetGridMass(stator.TopGrid);
            }

            return Math.Min(torque, (mass > 0f ? (mass * mass) : torque));
        }
    }
}
