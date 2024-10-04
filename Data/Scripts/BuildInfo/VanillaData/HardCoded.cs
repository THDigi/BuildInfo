using System;
using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using SpaceEngineers.Game.Definitions.SafeZone;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Definitions;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.VanillaData
{
    // HACK: various hardcoded data from game sources
    /// <summary>
    /// Various hardcoded data and behavior extracted from game source because it's not directly accessible.
    /// </summary>
    public static class Hardcoded
    {
        public static void CleanRefs()
        {
            GameDefinition = null;
            ReloadableBlockTypes = null;
            PlatformIcon.List = null;
            ProgrammableBlock_DefaultScript = null;
            CustomTargetingOptionName = null;
            TargetOptionsSorted = null;
            MountPointMaskNames = null;
            MountPointMaskValues = null;
        }

        // from VRage.GameServices.PlatformIcon
        public static class PlatformIcon
        {
            public static readonly char PC = '\ue030';
            public static readonly char PS = '\ue031';
            public static readonly char XBOX = '\ue032';
            public static List<char> List = new List<char>() { PC, PS, XBOX };
        }

        // from MyGravityProviderSystem.G
        public const float EarthGravity = 9.81f;

        // from MyPhysics.CreateWorldCInfo()
        public const int SolverIterationsMin = 8;

        // from MyGridConveyorSystem
        public const float Conveyors_PowerReqPerGrid = 1e-07f;
        public const string Conveyors_PowerGroup = "Conveyors"; // in ctor()

        // from MyGridConveyorSystem.NeedsLargeTube()
        public static bool Conveyors_ItemNeedsLargeTube(MyPhysicalItemDefinition physicalItemDefinition)
        {
            if(physicalItemDefinition.Id.TypeId == typeof(MyObjectBuilder_PhysicalGunObject))
                return false;

            return physicalItemDefinition.Size.AbsMax() > 0.25f;
        }

        // from MyCharacter.UpdateBuildPlanner() and MakeBlueprintFromBuildPlanItem()
        public const string BuildPlanner_BPClassSubtype = "BuildPlanner";
        public const string BuildPlanner_BPSubtypePrefix = "BuildPlanItem_";

        // from MyAssembler.CalculateBlueprintProductionTime()
        public static float Assembler_BpProductionTime(MyBlueprintDefinitionBase bp, MyAssemblerDefinition assemblerDef, IMyAssembler assembler = null)
        {
            float upgrades = assembler?.UpgradeValues["Productivity"] ?? 0; // defaults to 0 in MyAssembler.Init()
            float result = (float)Math.Round(bp.BaseProductionTimeInSeconds * 1000f / (MyAPIGateway.Session.AssemblerSpeedMultiplier * assemblerDef.AssemblySpeed + upgrades));
            return result / 1000f;
        }

        // from MyRefinery.ProcessQueueItems()
        public static float Refinery_BpProductionTime(MyBlueprintDefinitionBase bp, MyRefineryDefinition refineryDef, IMyRefinery refinery = null)
        {
            float upgrades = refinery?.UpgradeValues["Productivity"] ?? 0; // defaults to 0 in MyRefinery.Init()
            float refineSpeed = ((refineryDef.RefineSpeed + upgrades) * MyAPIGateway.Session.RefinerySpeedMultiplier);
            return bp.BaseProductionTimeInSeconds / refineSpeed;
        }

        // from MyAssembler.GetEfficiencyMultiplierForBlueprint() + overrides from MySurvivalKit
        public static float Assembler_GetEfficiencyMultiplierForBlueprint(MyDefinitionBase defBase, MyBlueprintDefinitionBase blueprint)
        {
            if(defBase is MySurvivalKitDefinition)
            {
                foreach(MyBlueprintDefinitionBase.Item item in blueprint.Prerequisites)
                {
                    if(item.Id.TypeId == typeof(MyObjectBuilder_Ore))
                    {
                        return 1f;
                    }
                }
            }

            return MyAPIGateway.Session.AssemblerEfficiencyMultiplier;
        }

        // from MyShipConnector.Init()
        public static float ShipConnector_InventoryVolume(MyCubeBlockDefinition def)
        {
            float gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
            return (def.Size * gridSize * 0.8f).Volume;
        }

        // from MyShipConnector.LoadDummies()
        public const string Connector_Connect_DummyName = "connector";
        public const string Connector_SmallPort_DummyName = "small_connector";
        public const string Connector_Ejector_DummyName = "ejector";
        // from MyShipConnector.TryAttach()
        public static readonly float Connector_ConnectAngleMinMax = MathHelper.ToRadians(45);
        public static readonly float Connector_ConnectMaxDistance = (float)Math.Sqrt(0.35f);

        // from MyShipMergeBlock.LoadDummies()
        public const string Merge_DummyName = "merge"; // see MergeFailDetector too!

        // from MyShipMergeBlock.CalculateMergeData()
        public const float Merge_StrengthMulSmallGrid = 0.1f;
        public const float Merge_StrengthMulLargeGrid = 0.005f;

        // from MyEngineerToolBase
        public const float EngineerToolBase_DefaultReachDistance = 2f;

        // from MyHandDrill.Init()
        public const float HandDrill_DefaultRadius = 0.35f;

        // from MyWelder.WeldAmount
        public static float HandWelder_GetWeldPerSec(float speedMultiplier) => MyAPIGateway.Session.WelderSpeedMultiplier * speedMultiplier * HandWelder_WeldAmountPerSecond;
        const float HandWelder_WeldAmountPerSecond = 1f;

        // from MyAngleGrinder.GrinderAmount
        public static float HandGrinder_GetGrindPerSec(float speedMultiplier) => MyAPIGateway.Session.GrinderSpeedMultiplier * speedMultiplier * HandGrinder_GrindAmountPerSecond;
        const float HandGrinder_GrindAmountPerSecond = 2f;

        // from MyShipDrill
        public const float ShipDrill_Power = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_DRILL;
        public static readonly MyObjectBuilderType ShipDrill_InventoryConstraint = typeof(MyObjectBuilder_Ore);
        public static float ShipDrill_InventoryVolume(MyCubeBlockDefinition def)
        {
            float gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
            return def.Size.X * def.Size.Y * def.Size.Z * gridSize * gridSize * gridSize * 0.5f;
        }
        public const float ShipDrill_VoxelVisualAdd = 0.6f; // based on visual tests

        // applies to both ship and hand drills
        public const float HandDrill_MineVoxelNoOreRadiusMul = 3f; // MyDrillBase.TryDrillVoxels() - still hardcoded for hand-tools
        public const float Drill_MineFloatingObjectRadiusMul = 1.33f; // MyDrillBase.DrillFloatingObject()
        public const float Drill_MineCharacterRadiusMul = 0.8f; // MyDrillBase.DrillCharacter()

        // from MyShipToolBase
        public const float ShipTool_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_GRINDER;
        public const string ShipTool_PowerGroup = "Defense";
        public static float ShipTool_InventoryVolume(MyCubeBlockDefinition def)
        {
            float gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
            return (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize * 0.5f;
        }
        public const float ShipTool_ReachDistance = 4.5f; // MyShipToolBase.DEFAULT_REACH_DISTANCE

        const float ShipTool_ActivationsPerSecond = 3; // got from testing
        // TODO investigate ^ should be 6 as it gets called in update10() ... hmm

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

        // from MyShipGrinder.ApplyImpulse()+UpdateAfterSimulation10()
        /// <summary>
        /// Returns maximum possible force applied to targetBlock's grid from sourceGrid's grinder.
        /// </summary>
        public static float ShipGrinderImpulseForce(IMyCubeGrid sourceGrid, IMySlimBlock targetBlock)
        {
            IMyCubeGrid targetGrid = targetBlock.CubeGrid;

            if(MyAPIGateway.Session.SessionSettings.EnableToolShake && targetGrid.Physics != null && !targetGrid.Physics.IsStatic)
            {
                float f = 1.73205078f; // MyUtils.GetRandomVector3()'s max length
                return (f * sourceGrid.GridSize * 500f);
            }

            return 0f;
        }

        // from MyButtonPanel.Init()
        public const float ButtonPanel_PowerReq = 0.0001f;

        // from MyUseObjectPanelButton + how detectors are found: MyUseObjectsComponent.LoadDetectorsFromModel()
        public const string ButtonPanel_DummyName = "detector_panel";

        // from MySensorBlock
        public static readonly Vector3 Sensor_MinField = new Vector3(0.1f); // MySensorBlock.MIN_RANGE = 0.1f;
        public static Vector3 Sensor_MaxField(float maxRange) => new Vector3(maxRange * 2);
        public static float Sensor_PowerReq(Vector3 maxField)
        {
            // sensor.RequiredPowerInput exists but is always reporting 0 and it seems ignored in the source code (see: MySensorBlock.CalculateRequiredPowerInput())
            return 0.0003f * (float)Math.Pow((maxField - Sensor_MinField).Volume, 1d / 3d);
        }

        // from MyCockpit.Init(), applies to cryos too
        public const float Cockpit_InventoryVolume = 1f; // Vector3.One.Volume;
        public static float Cockpit_PowerRequired(MyCockpitDefinition def, bool isFunctional = true) => (isFunctional && def.EnableShipControl ? 0.003f : 0f);

        // from MyOreDetector.Init()
        public const float OreDetector_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_ORE_DETECTOR; // 0.002f

        // from MyCollector.LoadDummies()
        public static string Collector_DummyName = "collector"; // see BData_Collector too!

        // from MyCameraBlock.GetViewMatrix()
        public static string Camera_DummyName = "camera";

        // from MyLandingGear.LoadDummies()
        public static string LandingGear_DummyName = "gear_lock"; // see BData_LandingGear too!

        public const float DefaultCharacterGravityMultiplier = 2f; // from SpaceEngineersGame.SetupPerGameSettings()

        // from MyCharacter.GetOnLadder_Implementation()/ProceedLadderMovement()/UpdateLadder()
        /// <summary>
        /// in m/s
        /// </summary>
        public static float LadderClimbSpeed(float distanceBetweenPoles)
        {
            const float ladderSpeed = 2f;
            //const float stepsPerAnimation = 59;
            //float stepIncrement = ladderSpeed * distanceBetweenPoles / stepsPerAnimation;
            //float speed = stepIncrement * stepsPerAnimation;

            return distanceBetweenPoles * ladderSpeed;
        }

        // from MyDoor.Init()
        public const float Door_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_DOOR; // 3E-05f

        // from MyDoor.DisassembleRatio
        public const float Door_DisassembleRatioMultiplier = 3.3f; // MyDoor override DisassembleRatio and multiplies definition by this no matter of open/close state

        // from MyAdvancedDoor.DisassembleRatio
        public const float AdvDoor_Closed_DisassembleRatioMultiplier = 3.3f; // MyAdvancedDoor override DisassembleRatio and multiplies definition by this when closed

        // simplified from MyDoor.UpdateCurrentOpening()
        public static float Door_MoveSpeed(float openingSpeed, float travelDistance = 1f)
        {
            return travelDistance / openingSpeed;
        }

        // simplified from MyAdvancedDoor.UpdateCurrentOpening()
        public static void AdvDoor_MoveSpeed(MyAdvancedDoorDefinition advDoor, out float openTime, out float closeTime)
        {
            openTime = 0;
            closeTime = 0;

            foreach(MyObjectBuilder_AdvancedDoorDefinition.Opening seq in advDoor.OpeningSequence)
            {
                float moveTime = (seq.MaxOpen / seq.Speed);
                openTime = Math.Max(openTime, seq.OpenDelay + moveTime);
                closeTime = Math.Max(closeTime, seq.CloseDelay + moveTime);
            }
        }

        // from MyMedicalRoom.Init()
        public const float MedicalRoom_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_MEDICAL_ROOM; // 0.002f

        // from MyRadioAntenna.UpdatePowerInput()
        public static float RadioAntenna_PowerReq(float range, bool broadcasting = true)
        {
            if(!broadcasting)
                range = 1f;

            return (range / 500f) * MyEnergyConstants.MAX_REQUIRED_POWER_ANTENNA; // 0.002f
        }

        // from MyBeacon.UpdatePowerInput()
        public static float Beacon_PowerReq(MyBeaconDefinition def, float? range = null)
        {
            if(range.HasValue)
                return (range.Value / def.MaxBroadcastRadius) * (def.MaxBroadcastPowerDrainkW / 1000f);
            else
                return (def.MaxBroadcastPowerDrainkW / 1000f);
        }

        // from MyChatBroadcastEntityComponent
        public const int BroadcastController_MaxMessageCount = 8;
        public const int BroadcastController_MaxMessageLength = 128;

        // from MyMyProjectorBase.AllowWelding
        public static bool Projector_AllowWelding(MyProjectorDefinition def) => def.AllowWelding && !def.AllowScaling && !def.IgnoreSize;

        // from EmissiveColors.sbc; no way to get emissive color presets
        public static readonly Color EmissivePreset_EventController_State0 = new Color(0, 255, 255); // "lightblue", but it's cyan xD
        public static readonly Color EmissivePreset_EventController_State1 = new Color(0, 0, 255);

        // from MyTimerBlock.Init()
        public const float Timer_PowerReq = 1E-07f;

        // from MyProgrammableBlock.Init()
        public const float ProgrammableBlock_PowerReq = 0.0005f;

        // from MyProgrammableBlock.OpenEditor()
        public static string ProgrammableBlock_DefaultScript = PB_ComputeDefaultScript();
        static string PB_ComputeDefaultScript()
        {
            string ctorComment = PB_ToIndentedComment(MyTexts.GetString(MySpaceTexts.ProgrammableBlock_DefaultScript_Constructor).Trim());
            string saveComment = PB_ToIndentedComment(MyTexts.GetString(MySpaceTexts.ProgrammableBlock_DefaultScript_Save).Trim());
            string mainComment = PB_ToIndentedComment(MyTexts.GetString(MySpaceTexts.ProgrammableBlock_DefaultScript_Main).Trim());
            string script = $"public Program()\r\n{{\r\n{ctorComment}\r\n}}\r\n\r\npublic void Save()\r\n{{\r\n{saveComment}\r\n}}\r\n\r\npublic void Main(string argument, UpdateType updateSource)\r\n{{\r\n{mainComment}\r\n}}\r\n";

            script = script.Replace("\r", ""); // HACK: the editor GUI does this in backend

            return script;
        }
        static string PB_ToIndentedComment(string input)
        {
            string[] NEW_LINES = new string[] { "\r\n", "\n" };
            string[] value = input.Split(NEW_LINES, StringSplitOptions.None);
            return "    // " + string.Join("\n    // ", value);
        }

        // from MySoundBlock.UpdateRequiredPowerInput()
        public const float SoundBlock_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_SOUNDBLOCK; // 0.0002f

        // from <weapon>.CanShoot() taking ShotsInBurst into account.
        public static HashSet<MyObjectBuilderType> ReloadableBlockTypes = new HashSet<MyObjectBuilderType>()
        {
            typeof(MyObjectBuilder_LargeGatlingTurret),
            typeof(MyObjectBuilder_LargeMissileTurret),
            typeof(MyObjectBuilder_SmallMissileLauncher),
            typeof(MyObjectBuilder_SmallMissileLauncherReload),  

            // HACK: these block types don't support reloading
            //typeof(MyObjectBuilder_InteriorTurret), // MyLargeInteriorBarrel.StartShooting()
            //typeof(MyObjectBuilder_SmallGatlingGun), // MySmallGatlingGun.CanShoot()
        };

        public static MyGameDefinition GameDefinition = GetGameDefinition();

        // from MySession.LoadGameDefinition()
        static MyGameDefinition GetGameDefinition()
        {
            MyDefinitionId? defId = null; // TODO: need to get definition from session... maybe GetCheckpoint() in LoadData().
            if(!defId.HasValue)
                defId = MyGameDefinition.Default;

            MyGameDefinition def = MyDefinitionManager.Static.GetDefinition<MyGameDefinition>(defId.Value);
            if(def == null)
                def = MyGameDefinition.DefaultDefinition;

            return def;
        }

        // from MyCubeBlock.CalculateStoredExplosiveDamage() and CalculateStoredExplosiveRadius()
        public static bool GetAmmoInventoryExplosion(MyPhysicalItemDefinition magazineDef, MyAmmoDefinition ammoDef, int amount, out float damage) //, out float radius)
        {
            if(ammoDef.ExplosiveDamageMultiplier == 0)
            {
                damage = 0;
                //radius = 0;
                return false;
            }

            float volume = amount * magazineDef.Volume * 1000f;
            damage = volume * GameDefinition.ExplosionDamagePerLiter * ammoDef.ExplosiveDamageMultiplier;

            //float volumeCap = MyMath.Clamp((volume - gameDef.ExplosionAmmoVolumeMin) / gameDef.ExplosionAmmoVolumeMax, 0f, 1f);
            //radius = GameDefinition.ExplosionRadiusMin + volumeCap * (gameDef.ExplosionRadiusMax - gameDef.ExplosionRadiusMin);

            return true;
        }

        // from MyWarhead.MarkForExplosion()
        public const float WarheadMaxRadius = 30f;
        public const float WarheadRadiusRatioPerOther = 0.024f;
        public const float WarheadScanRadiusGridMul = 4f;
        public const float WarheadScanRadiusShrink = 0.85f;
        public static float WarheadSearchRadius(MyCubeBlockDefinition def) => MyDefinitionManager.Static.GetCubeSize(def.CubeSize) * WarheadScanRadiusGridMul * WarheadScanRadiusShrink;

        // from MySectorWeatherComponent.ProcessDecoys() where it does a hardcoded 3rd check for decoys giving them 50m radius, effectively clamping them to 50m
        public const float Decoy_MinLightningRodDistance = 50;

        // from MySectorWeatherComponent.ProcessDecoys(), sqrt(400)
        public const float Tree_LightningRodRadius = 20;

        // from MyReflectorLight.CreateTerminalControls()
        public const float Spotlight_RotationSpeedToRPM = 20f;
        public const float Spotlight_RadiansPerSecondMul = Spotlight_RotationSpeedToRPM * 6f;

        // from MyLargeTurretBase.Init()
        public const float Turret_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_TURRET; // 0.002f

        // from MyLargeTurretBase.RotationAndElevation() - rotation speed is radisans per milisecond(ish) (since it uses 16, not 1/60)
        public const float Turret_RotationSpeedMul = MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS * 60;

        static readonly MyObjectBuilder_TargetingFlags DefaultTargetingFlags = new MyObjectBuilder_TargetingFlags();
        static readonly MyTurretTargetingOptions MyTurretTargetingOptions_None = (MyTurretTargetingOptions)0;

        public static bool CTC_AutoTarget = true;

        // from MyTurretControlBlock.HiddenTargetingOptions
        public static readonly MyTurretTargetingOptions CTC_TargetOptionsHidden = MyTurretTargetingOptions_None;

        // from MyTurretControlBlock.Init()
        public static readonly MyTurretTargetingOptions CTC_TargetOptionsDefault =
              (DefaultTargetingFlags.TargetMeteors ? MyTurretTargetingOptions.Asteroids : MyTurretTargetingOptions_None)
            | (DefaultTargetingFlags.TargetMissiles ? MyTurretTargetingOptions.Missiles : MyTurretTargetingOptions_None)
            | (DefaultTargetingFlags.TargetCharacters ? MyTurretTargetingOptions.Players : MyTurretTargetingOptions_None)
            | (DefaultTargetingFlags.TargetSmallGrids ? MyTurretTargetingOptions.SmallShips : MyTurretTargetingOptions_None)
            | (DefaultTargetingFlags.TargetLargeGrids ? MyTurretTargetingOptions.LargeShips : MyTurretTargetingOptions_None)
            | (DefaultTargetingFlags.TargetStations ? MyTurretTargetingOptions.Stations : MyTurretTargetingOptions_None)
            | (DefaultTargetingFlags.TargetNeutrals ? MyTurretTargetingOptions.Neutrals : MyTurretTargetingOptions_None)
            | (DefaultTargetingFlags.TargetFriends ? MyTurretTargetingOptions.Friends : MyTurretTargetingOptions_None)
            | (DefaultTargetingFlags.TargetEnemies ? MyTurretTargetingOptions.Enemies : MyTurretTargetingOptions_None);

        // from MySearchlight.HiddenTargetingOptions
        public static readonly MyTurretTargetingOptions Searchlight_TargetOptionsHidden = MyTurretTargetingOptions_None;

        // from MySearchlight.Init()
        public static readonly MyTurretTargetingOptions Searchlight_TargetOptionsDefault = CTC_TargetOptionsDefault;

        // from MySearchlight.RotationAndElevation()
        public const float Searchlight_RotationSpeedMul = Turret_RotationSpeedMul;

        public static Dictionary<MyTurretTargetingOptions, string> CustomTargetingOptionName = new Dictionary<MyTurretTargetingOptions, string>()
        {
            [MyTurretTargetingOptions.Asteroids] = "Meteors",
            [MyTurretTargetingOptions.Missiles] = "Rockets",
            [MyTurretTargetingOptions.Players] = "Characters",
            [MyTurretTargetingOptions.SmallShips] = "SmallGrids",
            [MyTurretTargetingOptions.LargeShips] = "LargeGrids",
        };

        public static List<MyTurretTargetingOptions> TargetOptionsSorted = SortTargetOptions();
        static List<MyTurretTargetingOptions> SortTargetOptions()
        {
            List<MyTurretTargetingOptions> list = new List<MyTurretTargetingOptions>(MyEnum<MyTurretTargetingOptions>.Values);

            if(list.Count > 8)
            {
                MyTurretTargetingOptions friends = list[7];
                MyTurretTargetingOptions enemies = list[8];

                // make the friends value last to be more noticeable
                list[7] = enemies;
                list[8] = friends;
            }

            return list;
        }

        // MyTargetLockingBlockComponent.GetTimeToLockIsSec
        public static float TargetLocking_SecondsToLock(Vector3D shooter, Vector3D target, MyCubeSize targetSize, MyTargetLockingBlockComponentDefinition def)
        {
            double distanceRatio = Vector3D.Distance(shooter, target) / def.FocusSearchMaxDistance;
            double targetSizeModifier = (targetSize == MyCubeSize.Small) ? def.LockingModifierSmallGrid : def.LockingModifierLargeGrid;
            return (float)MathHelper.Clamp(distanceRatio * def.LockingModifierDistance * targetSizeModifier, def.LockingTimeMin, def.LockingTimeMax);
        }

        public const MyExplosionFlags MissileDefaultExplosionFlags = (MyExplosionFlags.AFFECT_VOXELS | MyExplosionFlags.APPLY_FORCE_AND_DAMAGE | MyExplosionFlags.CREATE_DECALS | MyExplosionFlags.CREATE_SHRAPNELS | MyExplosionFlags.APPLY_DEFORMATION | MyExplosionFlags.CREATE_PARTICLE_DEBRIS);

        public static MyExplosionFlags GetMissileExplosionFlags(MyMissileAmmoDefinition missileDef)
        {
            // as per MyMissile.ExecuteExplosion()
            return missileDef.ExplosionFlags ?? MissileDefaultExplosionFlags;

            // NOTE: MyExplosionFlags.CREATE_PARTICLE_EFFECT is force-added if not marked destroyed (by impacting safezones or destroyed by other explosions).
        }

        // from MyMissile.GetRicochetProbability()
        /// <summary>
        /// Determines the probability of ricochet given a certain impact angle.
        /// </summary>
        /// <remarks>
        /// Probability linearly ramps between the min and max probabilities along
        /// the range of impact angles between the min and max ricochet angle.
        /// Any impact angle less than the min ricochet angle has NO probability of ricochet.
        /// Any impact angle more than the max ricochet angle is capped at the max ricochet probability. 
        /// </remarks>
        /// <example>
        /// Probability
        ///     ^
        ///     │
        /// Max │                  _____________________
        ///     │                 ╱
        ///     │                ╱
        ///     │               ╱
        ///     │              ╱
        ///     │             ╱
        /// Min │            ╱
        ///     │           │ 
        ///    0└───────────┴───────────────────────────&gt; Impact Angle
        ///              Min      Max
        /// </example>
        /// <param name="impactAngle"></param>
        /// <returns>Probability value from 0 to max ricochet probability.</returns>
        public static float Missile_GetRicochetProbability(MyMissileAmmoDefinition missileDef, float impactAngle)
        {
            // from MyMissile.Init()
            float m_ricochetMinAngle = MathHelper.ToRadians(missileDef.MissileMinRicochetAngle);
            float m_ricochetMaxAngle = MathHelper.Max(m_ricochetMinAngle, MathHelper.ToRadians(missileDef.MissileMaxRicochetAngle));
            float m_ricochetMinProbability = MathHelper.Clamp(missileDef.MissileMinRicochetProbability, 0f, 1f);
            float m_ricochetMaxProbability = MathHelper.Clamp(missileDef.MissileMaxRicochetProbability, m_ricochetMinProbability, 1f);

            // from MyMissile.GetRicochetProbability()
            if(impactAngle < m_ricochetMinAngle)
                return 0f;

            if(impactAngle > m_ricochetMaxAngle)
                return m_ricochetMaxProbability;

            float num = m_ricochetMaxAngle - m_ricochetMinAngle;
            if(Math.Abs(num) < 1E-06f)
                return m_ricochetMaxProbability;

            float num2 = (m_ricochetMaxProbability - m_ricochetMinProbability) / num;
            return m_ricochetMinProbability + num2 * (impactAngle - m_ricochetMinAngle);
        }

        /// <summary>
        /// Because the game works in tick steps, fire rate can be quite different because it has to shoot the next tick.
        /// <para>Gatling gun for example has 85ms between shots, but 85/16.666666 = ~5.1 so it has to do 6 ticks between shots
        ///   which results in 100ms between shots, 600 RPM instead of 700 declared in sbc.</para>
        /// </summary>
        /// <param name="shootIntervalMs">the <see cref="MyWeaponDefinition.MyWeaponAmmoData.ShootIntervalInMiliseconds"/> from weapon definition</param>
        /// <returns>Rounds per second</returns>
        public static float WeaponRealRPS(int shootIntervalMs)
        {
            const double TickMs = ((1d / MyEngineConstants.UPDATE_STEPS_PER_SECOND) * 1000d);
            double ticks = shootIntervalMs / TickMs;
            double actualMsBetweenShots = Math.Ceiling(ticks) * TickMs;
            return (float)(1000d / actualMsBetweenShots);
        }

        // from MySafeZoneComponent.GetPowerDrain()
        public static float SafeZone_GetPowerDrain(MySafeZoneBlockDefinition def, bool on, bool isSphere, Vector3 size)
        {
            if(!on)
                return 1E-06f;

            float drainDiff = def.MaxSafeZonePowerDrainkW - def.MinSafeZonePowerDrainkW;
            float radiusDiff = def.MaxSafeZoneRadius - def.MinSafeZoneRadius;

            float result;
            if(isSphere)
            {
                result = (size.X - def.MinSafeZoneRadius) / radiusDiff * drainDiff / 1000f;
            }
            else // cube
            {
                radiusDiff = def.MaxSafeZoneRadius * 2f - def.MinSafeZoneRadius;
                float x = (size.X - def.MinSafeZoneRadius) / radiusDiff / 3f;
                float y = (size.Y - def.MinSafeZoneRadius) / radiusDiff / 3f;
                float z = (size.Z - def.MinSafeZoneRadius) / radiusDiff / 3f;
                result = (x + y + z) * drainDiff / 1000f;
            }

            return def.MinSafeZonePowerDrainkW / 1000f + result;
        }

        // from MyLaserAntenna @ bool RotationAndElevation(float needRotation, float needElevation) - rotation speed is radians per milisecond
        public const float LaserAntenna_RotationSpeedMul = 1000f;

        // copied and converted from MyLaserAntenna.UpdatePowerInput()
        /// <summary>
        /// Distance in meters, returns power in MW.
        /// </summary>
        public static float LaserAntenna_PowerUsage(MyLaserAntennaDefinition def, double distanceMeters)
        {
            double powerRatio = def.PowerInputLasing;
            double maxRange = (def.MaxRange < 0 ? double.MaxValue : def.MaxRange);
            double distance = Math.Min(distanceMeters, maxRange);

            const double LinearUpToMeters = 200000;
            if(distance > LinearUpToMeters)
            {
                double a = powerRatio / 2.0 / LinearUpToMeters;
                double b = powerRatio * LinearUpToMeters - a * LinearUpToMeters * LinearUpToMeters;
                return (float)(((distance * distance) * a + b) / 1000.0 / 1000.0);
            }
            else
            {
                return (float)((powerRatio * distance) / 1000.0 / 1000.0);
            }
        }

        // MyLaserAntenna.INFINITE_RANGE - used to determine what range is considered infinite
        public const float LaserAntenna_InfiniteRange = 1E+08f;

        // from MySpaceBall.Init()
        public const float SpaceBall_RadioBroadcastRange = 50f;

        // from MySmallMissileLauncher.Init() & from MySmallGatlingGun.Init()
        public const float ShipGun_PowerReq = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_GUN; // 0.0002f

        // from MyEntityCapacitorComponent.StorePower()
        public const float CapacitorChargeMultiplier = 0.8f;

        // from MyProjectile.Start()
        public const float Projectile_RandomRangeMin = 0.8f;
        public const float Projectile_RandomRangeMax = 1f;

        // from MyProjectile.OnHit()

        /// <summary>
        /// gets multiplied by this regardless of type!
        /// </summary>
        public const float Projectile_HitImpulsePreMultiplier = 0.5f;

        /// <summary>
        /// gets multiplied by this only if victim is a character
        /// </summary>
        public const float Projectile_HitImpulseCharacterMultiplier = 100f;

        // from MyGravityGeneratorSphere.CalculateRequiredPowerInputForRadius()
        public static float SphericalGravGen_PowerReq(MyGravityGeneratorSphereDefinition def, float radius, float gravityAcceleration)
        {
            float currentVolume = (float)(Math.Pow(radius, def.ConsumptionPower) * Math.PI * 0.75);
            float defaultVolume = (float)(Math.Pow(100, def.ConsumptionPower) * Math.PI * 0.75);
            return currentVolume / defaultVolume * def.BasePowerInput * (Math.Abs(gravityAcceleration) / EarthGravity);
        }

        public const float Thrust_DamageRangeRandomMin = 0.6f;
        public const float Thrust_DamageCapsuleRadiusAdd = 0.05f; // visual tweak to match what the physics engine hits

        // not from the game but it's relevant to mods using excessive values
        public static bool Thrust_HasSaneLimits(MyThrustDefinition def)
        {
            return (def.MinPlanetaryInfluence >= 0 && def.MinPlanetaryInfluence <= 1f
                 && def.MaxPlanetaryInfluence >= 0 && def.MaxPlanetaryInfluence <= 1f
                 && def.EffectivenessAtMinInfluence >= 0
                 && def.EffectivenessAtMaxInfluence >= 0);
        }

        public struct ThrustInfo
        {
            public readonly MyThrustDefinition Def;
            public readonly MyThrust Internal;
            public readonly MyDefinitionId Fuel;
            public readonly float CurrentUsage;
            public readonly float MaxUsage;
            public readonly float CurrentConsumptionMul;
            public readonly float EarthConsumpationMul;

            public ThrustInfo(MyThrustDefinition def, MyThrust internalBlock, MyDefinitionId fuel, float currentUsage, float maxUsage, float currentConsumptionMul, float earthConsumpationMul)
            {
                Def = def;
                Internal = internalBlock;
                Fuel = fuel;
                CurrentUsage = currentUsage;
                MaxUsage = maxUsage;
                CurrentConsumptionMul = currentConsumptionMul;
                EarthConsumpationMul = earthConsumpationMul;
            }
        }

        public static ThrustInfo Thrust_GetUsage(IMyThrust thrust)
        {
            MyThrust thrustInternal = (MyThrust)thrust;
            MyThrustDefinition def = thrustInternal.BlockDefinition;

            float currentPowerUsage = 0;
            if(thrust.IsWorking)
            {
                // could use as an alternate autopilot check, but needs more iterations/monitoring/hacks:
                // base.CubeGrid.GridSystems.ControlSystem.GetShipController().Priority == ControllerPriority.AutoPilot

                // from MyThrusterBlockThrustComponent.RecomputeOverriddenParameters() and UpdateThrustStrength()
                if(thrustInternal.IsOverridden) // from IsOverridden(), NOTE: missing fast autopilot check
                    currentPowerUsage = thrustInternal.CurrentStrength * thrustInternal.MaxPowerConsumption;
                else
                    currentPowerUsage = thrustInternal.MinPowerConsumption + ((thrustInternal.MaxPowerConsumption - thrustInternal.MinPowerConsumption) * thrustInternal.CurrentStrength);
            }

            // IMyThrust.PowerConsumptionMultiplier is included in MyThrust.Min/MaxPowerConsumption
            float maxPowerUsage = thrustInternal.MaxPowerConsumption * Math.Max(def.EffectivenessAtMinInfluence, def.EffectivenessAtMaxInfluence);
            float gravityLength = BuildInfoMod.Instance.Caches.GetGravityLengthAtGrid(thrust.CubeGrid);

            // from MyEntityThrustComponent.RecomputeTypeThrustParameters() + calling CalculateConsumptionMultiplier()
            // HACK: ConsumptionFactorPerG is NOT per g. Game gives gravity multiplier (g) to method, not acceleration; remove the last single division when it's fixed.
            float consumptionMultiplier = 1f + def.ConsumptionFactorPerG * (gravityLength / Hardcoded.EarthGravity / Hardcoded.EarthGravity);
            float earthConsumptionMultipler = 1f + def.ConsumptionFactorPerG * (Hardcoded.EarthGravity / Hardcoded.EarthGravity / Hardcoded.EarthGravity);

            if(thrustInternal.FuelDefinition != null && thrustInternal.FuelDefinition.Id != MyResourceDistributorComponent.ElectricityId)
            {
                // formula from MyEntityThrustComponent.PowerAmountToFuel()
                float eff = (thrustInternal.FuelConverterDefinition.Efficiency * thrustInternal.FuelDefinition.EnergyDensity);
                float currentFuelUsage = (thrust.IsWorking ? (currentPowerUsage / eff) : 0);
                float maxFuelUsage = (maxPowerUsage / eff);

                return new ThrustInfo(def, thrustInternal, thrustInternal.FuelDefinition.Id, currentFuelUsage * consumptionMultiplier, maxFuelUsage * earthConsumptionMultipler, consumptionMultiplier, earthConsumptionMultipler);
            }
            else
            {
                return new ThrustInfo(def, thrustInternal, MyResourceDistributorComponent.ElectricityId, currentPowerUsage * consumptionMultiplier, maxPowerUsage * earthConsumptionMultipler, consumptionMultiplier, earthConsumptionMultipler);
            }
        }

        // from MyThrust.LoadDummies()
        public const string Thrust_DummyPrefix = "thruster_flame";
        public const string Thrust_DummyNoDamageSuffix = "_nodamage";
        public const string Thrust_DummyGlareSuffix = "_glare";

        // from MyThrust.DamageGrid()
        public static bool CanThrustDamageBlock(MyCubeSize thrustSize, MyCubeBlockDefinition victimDef)
        {
            return (thrustSize == MyCubeSize.Large || victimDef.DeformationRatio > 0.25);
        }

        // from MyAirVent.VentDummy
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

            maxMass = 2.5f * realAirDensity * (targetDescendVelocity * targetDescendVelocity) * chuteArea * parachute.DragCoefficient / EarthGravity;
        }

        public struct LCDRenderDistanceInfo
        {
            public float RenderDistanceRaw;
            public float TextureQualityMultiplier;
        }

        public static LCDRenderDistanceInfo TextSurface_MaxRenderDistance(MyCubeBlockDefinition def)
        {
            LCDRenderDistanceInfo info = new LCDRenderDistanceInfo();

            // as per MySessionComponentPanels.GetDrawDistanceMultiplierForQuality(), texture quality affects LCD render/update distance.
            MyTextureQuality? textureQuality = (MyTextureQuality?)MyAPIGateway.Session?.Config?.TextureQuality;
            switch(textureQuality)
            {
                case MyTextureQuality.LOW: info.TextureQualityMultiplier = (1f / 3f); break;
                case MyTextureQuality.MEDIUM: info.TextureQualityMultiplier = (2f / 3f); break;
                case MyTextureQuality.HIGH: info.TextureQualityMultiplier = 1f; break;
                default: info.TextureQualityMultiplier = (2f / 3f); break;
            }

            var lcdDef = def as MyTextPanelDefinition;
            if(lcdDef != null)
            {
                info.RenderDistanceRaw = lcdDef.MaxScreenRenderDistance;
            }
            else
            {
                // where MyMultiTextPanelComponent.Init() is called in MyFunctionalBlock.InitLcdComponent() and MyCockpit.Init()
                info.RenderDistanceRaw = 120f;

                MyContainerDefinition containerDef;
                if(MyComponentContainerExtension.TryGetContainerDefinition(def.Id.TypeId, def.Id.SubtypeId, out containerDef) && containerDef.DefaultComponents != null)
                {
                    foreach(MyContainerDefinition.DefaultComponent compPointer in containerDef.DefaultComponents)
                    {
                        MyComponentDefinitionBase compDefBase;
                        if(!MyComponentContainerExtension.TryGetComponentDefinition(compPointer.BuilderType, compPointer.SubtypeId ?? def.Id.SubtypeId, out compDefBase))
                            continue;

                        var multiLcdCompDef = compDefBase as MyMultiTextPanelComponentDefinition;
                        if(multiLcdCompDef != null)
                        {
                            info.RenderDistanceRaw = multiLcdCompDef.MaxRenderDistance;
                            break;
                        }
                    }
                }
            }

            return info;
        }

        enum MyTextureQuality
        {
            LOW,
            MEDIUM,
            HIGH
        }

        // where VRage.Network.DistanceRadiusAttribute is appled to MyFunctionalBlock, MyCockpit, MyTextPanel
        public const float TextSurfaceMaxSyncDistance = 32f;

        public struct TextSurfaceInfo
        {
            public readonly Vector2I TextureSize;
            public readonly Vector2 AspectRatio;
            public readonly Vector2 SurfaceSize;

            public TextSurfaceInfo(Vector2I textureSize, Vector2 aspectRatio, Vector2 surfaceSize)
            {
                TextureSize = textureSize;
                AspectRatio = aspectRatio;
                SurfaceSize = surfaceSize;
            }
        }

        public static TextSurfaceInfo TextSurface_GetInfo(int width, int height, int textureResolution)
        {
            // from MyTextPanelComponent.GetTextureResolutionForAspectRatio()
            Vector2I textureRes;
            if(width == height)
            {
                textureRes = new Vector2I(textureResolution, textureResolution);
            }
            else if(width > height)
            {
                int n = MathHelper.Pow2(MathHelper.Log2(width / height));
                textureRes = new Vector2I(textureResolution * n, textureResolution);
            }
            else
            {
                int n = MathHelper.Pow2(MathHelper.Log2(height / width));
                textureRes = new Vector2I(textureResolution, textureResolution * n);
            }

            // from MyTextPanelComponent.ctor()
            Vector2 aspectRatio;
            if(width > height)
                aspectRatio = new Vector2(1f, 1f * (float)height / (float)width);
            else
                aspectRatio = new Vector2(1f * (float)width / (float)height, 1f);

            // from MyRenderComponentScreenAreas.CalcAspectFactor()
            Vector2 aspectFactor;
            if(textureRes.X > textureRes.Y)
                aspectFactor = aspectRatio * new Vector2(1f, textureRes.X / textureRes.Y);
            else
                aspectFactor = aspectRatio * new Vector2(textureRes.Y / textureRes.X, 1f);

            return new TextSurfaceInfo(textureRes, aspectRatio, textureRes * aspectFactor);
        }

        // from MyEntityThrustComponent.MAX_DISTANCE_RELATIVE_DAMPENING and MAX_DISTANCE_RELATIVE_DAMPENING_SQ
        public const double RelativeDampeners_MaxDistance = 100f;
        public const double RelativeDampeners_MaxDistanceSq = RelativeDampeners_MaxDistance * RelativeDampeners_MaxDistance;

        // from MyEntityThrustComponent.UpdateRelativeDampeningEntity()
        // NOTE: required because MyPlayerCollection.SetDampeningEntity() has 1000m detection, this mod's RelativeDampenerInfo.cs fixes it.
        public static bool RelativeDampeners_DistanceCheck(IMyEntity controlledEnt, IMyEntity relativeEnt)
        {
            return relativeEnt.PositionComp.WorldAABB.DistanceSquared(controlledEnt.PositionComp.GetPosition()) <= RelativeDampeners_MaxDistanceSq;
        }

        // from MyGridJumpDriveSystem @ void Jump(Vector3D jumpTarget, long userId)
        public const float JumpDriveJumpDelay = 10f;

        // from MyBlockBuilderRenderData.Transparency
        public const float CubeBuilderTransparency = 0.25f;

        public static bool DetectorIsOpenCloseDoor(string detectorName, IMyEntity entity)
        {
            // from MyUseObjectsComponent.CreateInteractiveObject()
            // can't use `is IMyDoor` because it's implemented by all doors, while MyDoor is just the classic door.
            return entity is MyDoor && detectorName == "terminal";
        }

        // from MyCubeBlockDefinition.Init() and ContainsComputer(), MyDefinitionManager.InitCubeBlocks()
        // also note that detector_ownership is used and messes with ownership, mainly overriding what computer does.
        public static readonly MyDefinitionId ComputerComponentId = new MyDefinitionId(typeof(MyObjectBuilder_Component), MyStringHash.GetOrCompute("Computer"));

        public static readonly MyObjectBuilderType TargetDummyType = MyObjectBuilderType.Parse("MyObjectBuilder_TargetDummyBlock"); // HACK: MyObjectBuilder_TargetDummyBlock not whitelisted
        public static readonly MyObjectBuilderType HydrogenEngineType = typeof(MyObjectBuilder_HydrogenEngine); // TODO: use the interface when one is added
        public static readonly MyObjectBuilderType WindTurbineType = typeof(MyObjectBuilder_WindTurbine);

        // from https://github.com/KeenSoftwareHouse/SpaceEngineers/blob/master/Sources/Sandbox.Game/Definitions/MyCubeBlockDefinition.cs#L196-L204
        // VanillaDefinitions checks vanilla definitions if any has >3 to alert me if they're using a new flag.
        public enum MountPointMask : byte
        {
            Extruding = (1 << 0),
            Thin = (1 << 1),
            CustomBit3 = (1 << 2),
            CustomBit4 = (1 << 3),
            CustomBit5 = (1 << 4),
            CustomBit6 = (1 << 5),
            CustomBit7 = (1 << 6),
            CustomBit8 = (1 << 7),
        }

        public static string[] MountPointMaskNames = Enum.GetNames(typeof(MountPointMask));

        public static byte[] MountPointMaskValues = (byte[])Enum.GetValues(typeof(MountPointMask));

        // clone of MyCubeBlockDefinition.UntransformMountPointPosition() because it's internal
        /// <summary>
        /// Ignore Z on result.
        /// </summary>
        public static void UntransformMountPointPosition(ref Vector3 position, int wallIndex, Vector3I cubeSize, out Vector3 result)
        {
            Vector3 newPos = position - MountPointWallOffsets[wallIndex] * cubeSize;
            Matrix matrixInv = MountPointTransformsInverted[wallIndex];
            Vector3.Transform(ref newPos, ref matrixInv, out result);
            result.Z = 0;
        }

        static Vector3[] MountPointWallOffsets = new Vector3[6]
        {
            new Vector3(1f, 0f, 1f),
            new Vector3(0f, 1f, 1f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 1f)
        };

        static Matrix[] MountPointTransformsInverted = new Matrix[6]
        {
            Matrix.Invert(Matrix.CreateFromDir(Vector3.Right, Vector3.Up) * Matrix.CreateScale(1f, 1f, -1f)),
            Matrix.Invert(Matrix.CreateFromDir(Vector3.Up, Vector3.Forward) * Matrix.CreateScale(-1f, 1f, 1f)),
            Matrix.Invert(Matrix.CreateFromDir(Vector3.Forward, Vector3.Up) * Matrix.CreateScale(-1f, 1f, 1f)),
            Matrix.Invert(Matrix.CreateFromDir(Vector3.Left, Vector3.Up) * Matrix.CreateScale(1f, 1f, -1f)),
            Matrix.Invert(Matrix.CreateFromDir(Vector3.Down, Vector3.Backward) * Matrix.CreateScale(-1f, 1f, 1f)),
            Matrix.Invert(Matrix.CreateFromDir(Vector3.Backward, Vector3.Up) * Matrix.CreateScale(-1f, 1f, 1f))
        };

        // from MyCubeGrid.UpgradeCubeBlock()
        public static bool IsBlockReplaced(MyDefinitionId id, out MyCubeBlockDefinition newDef)
        {
            if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(id, out newDef))
            {
                return false;
            }
            else
            {
                // from MyCubeGrid.FindDefinitionUpgrade()
                foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                    if(blockDef == null)
                        continue;

                    if(blockDef.Id.SubtypeId == id.SubtypeId && !string.IsNullOrEmpty(id.SubtypeName))
                    {
                        newDef = blockDef;
                        return true;
                    }
                }

                string[] array = new string[7]
                {
                    "Red",
                    "Yellow",
                    "Blue",
                    "Green",
                    "Black",
                    "White",
                    "Gray"
                };

                for(int i = 0; i < array.Length; i++)
                {
                    if(id.SubtypeName.EndsWith(array[i], StringComparison.InvariantCultureIgnoreCase))
                    {
                        string subtypeName = id.SubtypeName.Substring(0, id.SubtypeName.Length - array[i].Length);
                        MyDefinitionId defId = new MyDefinitionId(id.TypeId, subtypeName);

                        if(MyDefinitionManager.Static.TryGetCubeBlockDefinition(defId, out newDef))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
