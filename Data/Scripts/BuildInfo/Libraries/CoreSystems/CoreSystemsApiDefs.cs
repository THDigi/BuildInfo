using System.Collections.Generic;
using ProtoBuf;
using VRageMath;

namespace CoreSystems.Api
{
    // Modified to fit the needs of this mod
    // Grab the originals instead: https://github.com/Ash-LikeSnow/WeaponCore/tree/master/Data/Scripts/CoreSystems/Api

    public static class CoreSystemsDef
    {
        [ProtoContract]
        public class ContainerDefinition
        {
            [ProtoMember(1)] public WeaponDefinition[] WeaponDefs;
            [ProtoMember(2)] public ArmorDefinition[] ArmorDefs;
            [ProtoMember(3)] public UpgradeDefinition[] UpgradeDefs;
            [ProtoMember(4)] public SupportDefinition[] SupportDefs;
        }

        [ProtoContract]
        public class ConsumeableDef
        {
            [ProtoMember(1)] public string ItemName;
            [ProtoMember(2)] public string InventoryItem;
            [ProtoMember(3)] public int ItemsNeeded;
            [ProtoMember(4)] public bool Hybrid;
            [ProtoMember(5)] public float EnergyCost;
            [ProtoMember(6)] public float Strength;
        }

        [ProtoContract]
        public class UpgradeDefinition
        {
            [ProtoMember(1)] public ModelAssignmentsDef Assignments;
            [ProtoMember(2)] public HardPointDef HardPoint;
            [ProtoMember(3)] public WeaponDefinition.AnimationDef Animations;
            [ProtoMember(4)] public string ModPath;
            [ProtoMember(5)] public ConsumeableDef[] Consumable;

            [ProtoContract]
            public struct ModelAssignmentsDef
            {
                [ProtoMember(1)] public MountPointDef[] MountPoints;

                [ProtoContract]
                public struct MountPointDef
                {
                    [ProtoMember(1)] public string SubtypeId;
                    [ProtoMember(2)] public float DurabilityMod;
                    [ProtoMember(3)] public string IconName;
                }
            }

            [ProtoContract]
            public struct HardPointDef
            {
                [ProtoMember(1)] public string PartName;
                [ProtoMember(2)] public HardwareDef HardWare;
                [ProtoMember(3)] public UiDef Ui;
                [ProtoMember(4)] public OtherDef Other;

                [ProtoContract]
                public struct UiDef
                {
                    [ProtoMember(1)] public bool StrengthModifier;
                }

                [ProtoContract]
                public struct HardwareDef
                {
                    public enum HardwareType
                    {
                        Default,
                    }

                    [ProtoMember(1)] public float InventorySize;
                    [ProtoMember(2)] public HardwareType Type;
                    [ProtoMember(3)] public int BlockDistance;

                }

                [ProtoContract]
                public struct OtherDef
                {
                    [ProtoMember(1)] public int ConstructPartCap;
                    [ProtoMember(2)] public int EnergyPriority;
                    [ProtoMember(3)] public bool Debug;
                    [ProtoMember(4)] public double RestrictionRadius;
                    [ProtoMember(5)] public bool CheckInflatedBox;
                    [ProtoMember(6)] public bool CheckForAnySupport;
                    [ProtoMember(7)] public bool StayCharged;
                }
            }
        }

        [ProtoContract]
        public class SupportDefinition
        {
            [ProtoMember(1)] public ModelAssignmentsDef Assignments;
            [ProtoMember(2)] public HardPointDef HardPoint;
            [ProtoMember(3)] public WeaponDefinition.AnimationDef Animations;
            [ProtoMember(4)] public string ModPath;
            [ProtoMember(5)] public ConsumeableDef[] Consumable;
            [ProtoMember(6)] public SupportEffect Effect;

            [ProtoContract]
            public struct ModelAssignmentsDef
            {
                [ProtoMember(1)] public MountPointDef[] MountPoints;

                [ProtoContract]
                public struct MountPointDef
                {
                    [ProtoMember(1)] public string SubtypeId;
                    [ProtoMember(2)] public float DurabilityMod;
                    [ProtoMember(3)] public string IconName;
                }
            }
            [ProtoContract]
            public struct HardPointDef
            {
                [ProtoMember(1)] public string PartName;
                [ProtoMember(2)] public HardwareDef HardWare;
                [ProtoMember(3)] public UiDef Ui;
                [ProtoMember(4)] public OtherDef Other;

                [ProtoContract]
                public struct UiDef
                {
                    [ProtoMember(1)] public bool ProtectionControl;
                }

                [ProtoContract]
                public struct HardwareDef
                {
                    [ProtoMember(1)] public float InventorySize;
                }

                [ProtoContract]
                public struct OtherDef
                {
                    [ProtoMember(1)] public int ConstructPartCap;
                    [ProtoMember(2)] public int EnergyPriority;
                    [ProtoMember(3)] public bool Debug;
                    [ProtoMember(4)] public double RestrictionRadius;
                    [ProtoMember(5)] public bool CheckInflatedBox;
                    [ProtoMember(6)] public bool CheckForAnySupport;
                    [ProtoMember(7)] public bool StayCharged;
                }
            }

            [ProtoContract]
            public struct SupportEffect
            {
                public enum AffectedBlocks
                {
                    Armor,
                    ArmorPlus,
                    PlusFunctional,
                    All,
                }

                public enum Protections
                {
                    KineticProt,
                    EnergeticProt,
                    GenericProt,
                    Regenerate,
                    Structural,
                }

                [ProtoMember(1)] public Protections Protection;
                [ProtoMember(2)] public AffectedBlocks Affected;
                [ProtoMember(3)] public int BlockRange;
                [ProtoMember(4)] public int MaxPoints;
                [ProtoMember(5)] public int PointsPerCharge;
                [ProtoMember(6)] public int UsablePerSecond;
                [ProtoMember(7)] public int UsablePerMinute;
                [ProtoMember(8)] public float Overflow;
                [ProtoMember(9)] public float Effectiveness;
                [ProtoMember(10)] public float ProtectionMin;
                [ProtoMember(11)] public float ProtectionMax;
            }
        }

        [ProtoContract]
        public class ArmorDefinition
        {
            public enum ArmorType
            {
                Light,
                Heavy,
                NonArmor,
            }

            [ProtoMember(1)] public string[] SubtypeIds;
            [ProtoMember(2)] public ArmorType Kind;
            [ProtoMember(3)] public double KineticResistance;
            [ProtoMember(4)] public double EnergeticResistance;
        }

        [ProtoContract]
        public class WeaponDefinition
        {
            [ProtoMember(1)] public ModelAssignmentsDef Assignments;
            [ProtoMember(2)] public TargetingDef Targeting;
            [ProtoMember(3)] public AnimationDef Animations;
            [ProtoMember(4)] public HardPointDef HardPoint;
            [ProtoMember(5)] public AmmoDef[] Ammos;
            [ProtoMember(6)] public string ModPath;
            [ProtoMember(7)] public Dictionary<string, UpgradeValues[]> Upgrades;

            [ProtoContract]
            public struct ModelAssignmentsDef
            {
                [ProtoMember(1)] public MountPointDef[] MountPoints;
                [ProtoMember(2)] public string[] Muzzles;
                [ProtoMember(3)] public string Ejector;
                [ProtoMember(4)] public string Scope;

                [ProtoContract]
                public struct MountPointDef
                {
                    [ProtoMember(1)] public string SubtypeId;
                    [ProtoMember(2)] public string SpinPartId;
                    [ProtoMember(3)] public string MuzzlePartId;
                    [ProtoMember(4)] public string AzimuthPartId;
                    [ProtoMember(5)] public string ElevationPartId;
                    [ProtoMember(6)] public float DurabilityMod;
                    [ProtoMember(7)] public string IconName;
                }
            }

            [ProtoContract]
            public struct TargetingDef
            {
                public enum Threat
                {
                    Projectiles,
                    Characters,
                    Grids,
                    Neutrals,
                    Meteors,
                    Other,
                    ScanNeutralGrid,
                    ScanFriendlyGrid,
                    ScanFriendlyCharacter,
                    ScanRoid,
                    ScanPlanet,
                    ScanEnemyCharacter,
                    ScanEnemyGrid,
                    ScanNeutralCharacter,
                    ScanUnOwnedGrid,
                    ScanOwnersGrid
                }

                public enum BlockTypes
                {
                    Any,
                    Offense,
                    Utility,
                    Power,
                    Production,
                    Thrust,
                    Jumping,
                    Steering
                }

                [ProtoMember(1)] public int TopTargets;
                [ProtoMember(2)] public int TopBlocks;
                [ProtoMember(3)] public double StopTrackingSpeed;
                [ProtoMember(4)] public float MinimumDiameter;
                [ProtoMember(5)] public float MaximumDiameter;
                [ProtoMember(6)] public bool ClosestFirst;
                [ProtoMember(7)] public BlockTypes[] SubSystems;
                [ProtoMember(8)] public Threat[] Threats;
                [ProtoMember(9)] public float MaxTargetDistance;
                [ProtoMember(10)] public float MinTargetDistance;
                [ProtoMember(11)] public bool IgnoreDumbProjectiles;
                [ProtoMember(12)] public bool LockedSmartOnly;
                [ProtoMember(13)] public bool UniqueTargetPerWeapon;
                [ProtoMember(14)] public int MaxTrackingTime;
                [ProtoMember(15)] public bool ShootBlanks;
                [ProtoMember(19)] public CommunicationDef Communications;
                [ProtoMember(20)] public bool FocusOnly;
                [ProtoMember(21)] public bool EvictUniqueTargets;
                [ProtoMember(22)] public int CycleTargets;
                [ProtoMember(23)] public int CycleBlocks;

                [ProtoContract]
                public struct CommunicationDef
                {
                    public enum Comms
                    {
                        NoComms,
                        BroadCast,
                        Relay,
                        Jamming,
                        RelayAndBroadCast,
                    }

                    public enum SecurityMode
                    {
                        Public,
                        Private,
                        Secure,
                    }

                    [ProtoMember(1)] public bool StoreTargets;
                    [ProtoMember(2)] public int StorageLimit;
                    [ProtoMember(3)] public string StorageLocation;
                    [ProtoMember(4)] public Comms Mode;
                    [ProtoMember(5)] public SecurityMode Security;
                    [ProtoMember(6)] public string BroadCastChannel;
                    [ProtoMember(7)] public double BroadCastRange;
                    [ProtoMember(8)] public double JammingStrength;
                    [ProtoMember(9)] public string RelayChannel;
                    [ProtoMember(10)] public double RelayRange;
                    [ProtoMember(11)] public bool TargetPersists;
                    [ProtoMember(12)] public bool StoreLimitPerBlock;
                    [ProtoMember(13)] public int MaxConnections;
                }
            }


            [ProtoContract]
            public struct AnimationDef
            {
                [ProtoMember(1)] public PartAnimationSetDef[] AnimationSets;
                [ProtoMember(2)] public PartEmissive[] Emissives;
                [ProtoMember(3)] public string[] HeatingEmissiveParts;
                [ProtoMember(4)] public Dictionary<PartAnimationSetDef.EventTriggers, EventParticle[]> EventParticles;

                [ProtoContract(IgnoreListHandling = true)]
                public struct PartAnimationSetDef
                {
                    public enum EventTriggers
                    {
                        Reloading,
                        Firing,
                        Tracking,
                        Overheated,
                        TurnOn,
                        TurnOff,
                        BurstReload,
                        NoMagsToLoad,
                        PreFire,
                        EmptyOnGameLoad,
                        StopFiring,
                        StopTracking,
                        LockDelay,
                    }

                    public enum ResetConditions
                    {
                        None,
                        Home,
                        Off,
                        On,
                        Reloaded
                    }

                    [ProtoMember(1)] public string[] SubpartId;
                    [ProtoMember(2)] public string BarrelId;
                    [ProtoMember(3)] public uint StartupFireDelay;
                    [ProtoMember(4)] public Dictionary<EventTriggers, uint> AnimationDelays;
                    [ProtoMember(5)] public EventTriggers[] Reverse;
                    [ProtoMember(6)] public EventTriggers[] Loop;
                    [ProtoMember(7)] public Dictionary<EventTriggers, RelMove[]> EventMoveSets;
                    [ProtoMember(8)] public EventTriggers[] TriggerOnce;
                    [ProtoMember(9)] public EventTriggers[] ResetEmissives;
                    [ProtoMember(10)] public ResetConditions Resets;

                }

                [ProtoContract]
                public struct PartEmissive
                {
                    [ProtoMember(1)] public string EmissiveName;
                    [ProtoMember(2)] public string[] EmissivePartNames;
                    [ProtoMember(3)] public bool CycleEmissivesParts;
                    [ProtoMember(4)] public bool LeavePreviousOn;
                    [ProtoMember(5)] public Vector4[] Colors;
                    [ProtoMember(6)] public float[] IntensityRange;
                }
                [ProtoContract]
                public struct EventParticle
                {
                    [ProtoMember(1)] public string[] EmptyNames;
                    [ProtoMember(2)] public string[] MuzzleNames;
                    [ProtoMember(3)] public ParticleDef Particle;
                    [ProtoMember(4)] public uint StartDelay;
                    [ProtoMember(5)] public uint LoopDelay;
                    [ProtoMember(6)] public bool ForceStop;
                }
                [ProtoContract]
                public struct RelMove
                {
                    public enum MoveType
                    {
                        Linear,
                        ExpoDecay,
                        ExpoGrowth,
                        Delay,
                        Show, //instant or fade
                        Hide, //instant or fade
                    }

                    [ProtoMember(1)] public MoveType MovementType;
                    [ProtoMember(2)] public XYZ[] LinearPoints;
                    [ProtoMember(3)] public XYZ Rotation;
                    [ProtoMember(4)] public XYZ RotAroundCenter;
                    [ProtoMember(5)] public uint TicksToMove;
                    [ProtoMember(6)] public string CenterEmpty;
                    [ProtoMember(7)] public bool Fade;
                    [ProtoMember(8)] public string EmissiveName;

                    [ProtoContract]
                    public struct XYZ
                    {
                        [ProtoMember(1)] public double x;
                        [ProtoMember(2)] public double y;
                        [ProtoMember(3)] public double z;
                    }
                }
            }

            [ProtoContract]
            public struct UpgradeValues
            {
                [ProtoMember(1)] public string[] Ammo;
                [ProtoMember(2)] public Dependency[] Dependencies;
                [ProtoMember(3)] public int RateOfFireMod;
                [ProtoMember(4)] public int BarrelsPerShotMod;
                [ProtoMember(5)] public int ReloadMod;
                [ProtoMember(6)] public int MaxHeatMod;
                [ProtoMember(7)] public int HeatSinkRateMod;
                [ProtoMember(8)] public int ShotsInBurstMod;
                [ProtoMember(9)] public int DelayAfterBurstMod;
                [ProtoMember(10)] public int AmmoPriority;

                [ProtoContract]
                public struct Dependency
                {
                    public string SubtypeId;
                    public int Quanity;
                }
            }

            [ProtoContract]
            public struct HardPointDef
            {
                public enum Prediction
                {
                    Off,
                    Basic,
                    Accurate,
                    Advanced,
                }

                [ProtoMember(1)] public string PartName;
                [ProtoMember(2)] public int DelayCeaseFire;
                [ProtoMember(3)] public float DeviateShotAngle;
                [ProtoMember(4)] public double AimingTolerance;
                [ProtoMember(5)] public Prediction AimLeadingPrediction;
                [ProtoMember(6)] public LoadingDef Loading;
                [ProtoMember(7)] public AiDef Ai;
                [ProtoMember(8)] public HardwareDef HardWare;
                [ProtoMember(9)] public UiDef Ui;
                [ProtoMember(10)] public HardPointAudioDef Audio;
                [ProtoMember(11)] public HardPointParticleDef Graphics;
                [ProtoMember(12)] public OtherDef Other;
                [ProtoMember(13)] public bool AddToleranceToTracking;
                [ProtoMember(14)] public bool CanShootSubmerged;
                [ProtoMember(15)] public bool NpcSafe;
                [ProtoMember(16)] public bool ScanTrackOnly;

                [ProtoContract]
                public struct LoadingDef
                {
                    [ProtoMember(1)] public int ReloadTime;
                    [ProtoMember(2)] public int RateOfFire;
                    [ProtoMember(3)] public int BarrelsPerShot;
                    [ProtoMember(4)] public int SkipBarrels;
                    [ProtoMember(5)] public int TrajectilesPerBarrel;
                    [ProtoMember(6)] public int HeatPerShot;
                    [ProtoMember(7)] public int MaxHeat;
                    [ProtoMember(8)] public int HeatSinkRate;
                    [ProtoMember(9)] public float Cooldown;
                    [ProtoMember(10)] public int DelayUntilFire;
                    [ProtoMember(11)] public int ShotsInBurst;
                    [ProtoMember(12)] public int DelayAfterBurst;
                    [ProtoMember(13)] public bool DegradeRof;
                    [ProtoMember(14)] public int BarrelSpinRate;
                    [ProtoMember(15)] public bool FireFull;
                    [ProtoMember(16)] public bool GiveUpAfter;
                    [ProtoMember(17)] public bool DeterministicSpin;
                    [ProtoMember(18)] public bool SpinFree;
                    [ProtoMember(19)] public bool StayCharged;
                    [ProtoMember(20)] public int MagsToLoad;
                    [ProtoMember(21)] public int MaxActiveProjectiles;
                    [ProtoMember(22)] public int MaxReloads;
                    [ProtoMember(23)] public bool GoHomeToReload;
                    [ProtoMember(24)] public bool DropTargetUntilLoaded;
                }


                [ProtoContract]
                public struct UiDef
                {
                    [ProtoMember(1)] public bool RateOfFire;
                    [ProtoMember(2)] public bool DamageModifier;
                    [ProtoMember(3)] public bool ToggleGuidance;
                    [ProtoMember(4)] public bool EnableOverload;
                    [ProtoMember(5)] public bool AlternateUi;
                    [ProtoMember(6)] public bool DisableStatus;
                }


                [ProtoContract]
                public struct AiDef
                {
                    [ProtoMember(1)] public bool TrackTargets;
                    [ProtoMember(2)] public bool TurretAttached;
                    [ProtoMember(3)] public bool TurretController;
                    [ProtoMember(4)] public bool PrimaryTracking;
                    [ProtoMember(5)] public bool LockOnFocus;
                    [ProtoMember(6)] public bool SuppressFire;
                    [ProtoMember(7)] public bool OverrideLeads;
                    [ProtoMember(8)] public int DefaultLeadGroup;
                    [ProtoMember(9)] public bool TargetGridCenter;
                }

                [ProtoContract]
                public struct HardwareDef
                {
                    public enum HardwareType
                    {
                        BlockWeapon = 0,
                        HandWeapon = 1,
                        Phantom = 6,
                    }

                    [ProtoMember(1)] public float RotateRate;
                    [ProtoMember(2)] public float ElevateRate;
                    [ProtoMember(3)] public Vector3D Offset;
                    [ProtoMember(4)] public bool FixedOffset;
                    [ProtoMember(5)] public int MaxAzimuth;
                    [ProtoMember(6)] public int MinAzimuth;
                    [ProtoMember(7)] public int MaxElevation;
                    [ProtoMember(8)] public int MinElevation;
                    [ProtoMember(9)] public float InventorySize;
                    [ProtoMember(10)] public HardwareType Type;
                    [ProtoMember(11)] public int HomeAzimuth;
                    [ProtoMember(12)] public int HomeElevation;
                    [ProtoMember(13)] public CriticalDef CriticalReaction;
                    [ProtoMember(14)] public float IdlePower;

                    [ProtoContract]
                    public struct CriticalDef
                    {
                        [ProtoMember(1)] public bool Enable;
                        [ProtoMember(2)] public int DefaultArmedTimer;
                        [ProtoMember(3)] public bool PreArmed;
                        [ProtoMember(4)] public bool TerminalControls;
                        [ProtoMember(5)] public string AmmoRound;
                    }
                }

                [ProtoContract]
                public struct HardPointAudioDef
                {
                    [ProtoMember(1)] public string ReloadSound;
                    [ProtoMember(2)] public string NoAmmoSound;
                    [ProtoMember(3)] public string HardPointRotationSound;
                    [ProtoMember(4)] public string BarrelRotationSound;
                    [ProtoMember(5)] public string FiringSound;
                    [ProtoMember(6)] public bool FiringSoundPerShot;
                    [ProtoMember(7)] public string PreFiringSound;
                    [ProtoMember(8)] public uint FireSoundEndDelay;
                    [ProtoMember(9)] public bool FireSoundNoBurst;
                }

                [ProtoContract]
                public struct OtherDef
                {
                    [ProtoMember(1)] public int ConstructPartCap;
                    [ProtoMember(2)] public int EnergyPriority;
                    [ProtoMember(3)] public int RotateBarrelAxis;
                    [ProtoMember(4)] public bool MuzzleCheck;
                    [ProtoMember(5)] public bool Debug;
                    [ProtoMember(6)] public double RestrictionRadius;
                    [ProtoMember(7)] public bool CheckInflatedBox;
                    [ProtoMember(8)] public bool CheckForAnyWeapon;
                    [ProtoMember(9)] public bool DisableLosCheck;
                    [ProtoMember(10)] public bool NoVoxelLosCheck;
                }

                [ProtoContract]
                public struct HardPointParticleDef
                {
                    [ProtoMember(1)] public ParticleDef Effect1;
                    [ProtoMember(2)] public ParticleDef Effect2;
                }
            }

            [ProtoContract]
            public class AmmoDef
            {
                [ProtoMember(1)] public string AmmoMagazine;
                [ProtoMember(2)] public string AmmoRound;
                [ProtoMember(3)] public bool HybridRound;
                [ProtoMember(4)] public float EnergyCost;
                [ProtoMember(5)] public float BaseDamage;
                [ProtoMember(6)] public float Mass;
                [ProtoMember(7)] public float Health;
                [ProtoMember(8)] public float BackKickForce;
                [ProtoMember(9)] public DamageScaleDef DamageScales;
                [ProtoMember(10)] public ShapeDef Shape;
                [ProtoMember(11)] public ObjectsHitDef ObjectsHit;
                [ProtoMember(12)] public TrajectoryDef Trajectory;
                [ProtoMember(13)] public AreaDamageDef AreaEffect;
                [ProtoMember(14)] public BeamDef Beams;
                [ProtoMember(15)] public FragmentDef Fragment;
                [ProtoMember(16)] public GraphicDef AmmoGraphics;
                [ProtoMember(17)] public AmmoAudioDef AmmoAudio;
                [ProtoMember(18)] public bool HardPointUsable;
                [ProtoMember(19)] public PatternDef Pattern;
                [ProtoMember(20)] public int EnergyMagazineSize;
                [ProtoMember(21)] public float DecayPerShot;
                [ProtoMember(22)] public EjectionDef Ejection;
                [ProtoMember(23)] public bool IgnoreWater;
                [ProtoMember(24)] public AreaOfDamageDef AreaOfDamage;
                [ProtoMember(25)] public EwarDef Ewar;
                [ProtoMember(26)] public bool IgnoreVoxels;
                [ProtoMember(27)] public bool Synchronize;
                [ProtoMember(28)] public double HeatModifier;
                [ProtoMember(29)] public bool NpcSafe;
                [ProtoMember(30)] public SynchronizeDef Sync;
                [ProtoMember(31)] public bool NoGridOrArmorScaling;

                [ProtoContract]
                public struct SynchronizeDef
                {
                    [ProtoMember(1)] public bool Full;
                    [ProtoMember(2)] public bool PointDefense;
                    [ProtoMember(3)] public bool OnHitDeath;
                }

                [ProtoContract]
                public struct DamageScaleDef
                {

                    [ProtoMember(1)] public float MaxIntegrity;
                    [ProtoMember(2)] public bool DamageVoxels;
                    [ProtoMember(3)] public float Characters;
                    [ProtoMember(4)] public bool SelfDamage;
                    [ProtoMember(5)] public GridSizeDef Grids;
                    [ProtoMember(6)] public ArmorDef Armor;
                    [ProtoMember(7)] public CustomScalesDef Custom;
                    [ProtoMember(8)] public ShieldDef Shields;
                    [ProtoMember(9)] public FallOffDef FallOff;
                    [ProtoMember(10)] public double HealthHitModifier;
                    [ProtoMember(11)] public double VoxelHitModifier;
                    [ProtoMember(12)] public DamageTypes DamageType;
                    [ProtoMember(13)] public DeformDef Deform;

                    [ProtoContract]
                    public struct FallOffDef
                    {
                        [ProtoMember(1)] public float Distance;
                        [ProtoMember(2)] public float MinMultipler;
                    }

                    [ProtoContract]
                    public struct GridSizeDef
                    {
                        [ProtoMember(1)] public float Large;
                        [ProtoMember(2)] public float Small;
                    }

                    [ProtoContract]
                    public struct ArmorDef
                    {
                        [ProtoMember(1)] public float Armor;
                        [ProtoMember(2)] public float Heavy;
                        [ProtoMember(3)] public float Light;
                        [ProtoMember(4)] public float NonArmor;
                    }

                    [ProtoContract]
                    public struct CustomScalesDef
                    {
                        public enum SkipMode
                        {
                            NoSkip,
                            Inclusive,
                            Exclusive,
                        }

                        [ProtoMember(1)] public CustomBlocksDef[] Types;
                        [ProtoMember(2)] public bool IgnoreAllOthers;
                        [ProtoMember(3)] public SkipMode SkipOthers;
                    }

                    [ProtoContract]
                    public struct DamageTypes
                    {
                        public enum Damage
                        {
                            Energy,
                            Kinetic,
                        }

                        [ProtoMember(1)] public Damage Base;
                        [ProtoMember(2)] public Damage AreaEffect;
                        [ProtoMember(3)] public Damage Detonation;
                        [ProtoMember(4)] public Damage Shield;
                    }

                    [ProtoContract]
                    public struct ShieldDef
                    {
                        public enum ShieldType
                        {
                            Default,
                            Heal,
                            Bypass,
                            EmpRetired,
                        }

                        [ProtoMember(1)] public float Modifier;
                        [ProtoMember(2)] public ShieldType Type;
                        [ProtoMember(3)] public float BypassModifier;
                        [ProtoMember(4)] public double HeatModifier;
                    }

                    [ProtoContract]
                    public struct DeformDef
                    {
                        public enum DeformTypes
                        {
                            HitBlock,
                            AllDamagedBlocks,
                            NoDeform,
                        }

                        [ProtoMember(1)] public DeformTypes DeformType;
                        [ProtoMember(2)] public int DeformDelay;
                    }
                }

                [ProtoContract]
                public struct ShapeDef
                {
                    public enum Shapes
                    {
                        LineShape,
                        SphereShape,
                    }

                    [ProtoMember(1)] public Shapes Shape;
                    [ProtoMember(2)] public double Diameter;
                }

                [ProtoContract]
                public struct ObjectsHitDef
                {
                    [ProtoMember(1)] public int MaxObjectsHit;
                    [ProtoMember(2)] public bool CountBlocks;
                }


                [ProtoContract]
                public struct CustomBlocksDef
                {
                    [ProtoMember(1)] public string SubTypeId;
                    [ProtoMember(2)] public float Modifier;
                }

                [ProtoContract]
                public struct GraphicDef
                {
                    [ProtoMember(1)] public bool ShieldHitDraw;
                    [ProtoMember(2)] public float VisualProbability;
                    [ProtoMember(3)] public string ModelName;
                    [ProtoMember(4)] public AmmoParticleDef Particles;
                    [ProtoMember(5)] public LineDef Lines;
                    [ProtoMember(6)] public DecalDef Decals;

                    [ProtoContract]
                    public struct AmmoParticleDef
                    {
                        [ProtoMember(1)] public ParticleDef Ammo;
                        [ProtoMember(2)] public ParticleDef Hit;
                        [ProtoMember(3)] public ParticleDef Eject;
                    }

                    [ProtoContract]
                    public struct LineDef
                    {
                        public enum Texture
                        {
                            Normal,
                            Cycle,
                            Chaos,
                            Wave,
                        }
                        public enum FactionColor
                        {
                            DontUse,
                            Foreground,
                            Background,
                        }

                        [ProtoMember(1)] public TracerBaseDef Tracer;
                        [ProtoMember(2)] public string TracerMaterial;
                        [ProtoMember(3)] public Randomize ColorVariance;
                        [ProtoMember(4)] public Randomize WidthVariance;
                        [ProtoMember(5)] public TrailDef Trail;
                        [ProtoMember(6)] public OffsetEffectDef OffsetEffect;
                        [ProtoMember(7)] public bool DropParentVelocity;

                        [ProtoContract]
                        public struct OffsetEffectDef
                        {
                            [ProtoMember(1)] public double MaxOffset;
                            [ProtoMember(2)] public double MinLength;
                            [ProtoMember(3)] public double MaxLength;
                        }

                        [ProtoContract]
                        public struct TracerBaseDef
                        {
                            [ProtoMember(1)] public bool Enable;
                            [ProtoMember(2)] public float Length;
                            [ProtoMember(3)] public float Width;
                            [ProtoMember(4)] public Vector4 Color;
                            [ProtoMember(5)] public uint VisualFadeStart;
                            [ProtoMember(6)] public uint VisualFadeEnd;
                            [ProtoMember(7)] public SegmentDef Segmentation;
                            [ProtoMember(8)] public string[] Textures;
                            [ProtoMember(9)] public Texture TextureMode;
                            [ProtoMember(10)] public bool AlwaysDraw;
                            [ProtoMember(11)] public FactionColor FactionColor;

                            [ProtoContract]
                            public struct SegmentDef
                            {
                                [ProtoMember(1)] public string Material; //retired
                                [ProtoMember(2)] public double SegmentLength;
                                [ProtoMember(3)] public double SegmentGap;
                                [ProtoMember(4)] public double Speed;
                                [ProtoMember(5)] public Vector4 Color;
                                [ProtoMember(6)] public double WidthMultiplier;
                                [ProtoMember(7)] public bool Reverse;
                                [ProtoMember(8)] public bool UseLineVariance;
                                [ProtoMember(9)] public Randomize ColorVariance;
                                [ProtoMember(10)] public Randomize WidthVariance;
                                [ProtoMember(11)] public string[] Textures;
                                [ProtoMember(12)] public bool Enable;
                                [ProtoMember(13)] public FactionColor FactionColor;
                            }
                        }

                        [ProtoContract]
                        public struct TrailDef
                        {
                            [ProtoMember(1)] public bool Enable;
                            [ProtoMember(2)] public string Material;
                            [ProtoMember(3)] public int DecayTime;
                            [ProtoMember(4)] public Vector4 Color;
                            [ProtoMember(5)] public bool Back;
                            [ProtoMember(6)] public float CustomWidth;
                            [ProtoMember(7)] public bool UseWidthVariance;
                            [ProtoMember(8)] public bool UseColorFade;
                            [ProtoMember(9)] public string[] Textures;
                            [ProtoMember(10)] public Texture TextureMode;
                            [ProtoMember(11)] public bool AlwaysDraw;
                            [ProtoMember(12)] public FactionColor FactionColor;
                        }
                    }

                    [ProtoContract]
                    public struct DecalDef
                    {

                        [ProtoMember(1)] public int MaxAge;
                        [ProtoMember(2)] public TextureMapDef[] Map;

                        [ProtoContract]
                        public struct TextureMapDef
                        {
                            [ProtoMember(1)] public string HitMaterial;
                            [ProtoMember(2)] public string DecalMaterial;
                        }
                    }
                }

                [ProtoContract]
                public struct BeamDef
                {
                    [ProtoMember(1)] public bool Enable;
                    [ProtoMember(2)] public bool ConvergeBeams;
                    [ProtoMember(3)] public bool VirtualBeams;
                    [ProtoMember(4)] public bool RotateRealBeam;
                    [ProtoMember(5)] public bool OneParticle;
                    [ProtoMember(6)] public bool FakeVoxelHits;
                }

                [ProtoContract]
                public struct FragmentDef
                {
                    [ProtoMember(1)] public string AmmoRound;
                    [ProtoMember(2)] public int Fragments;
                    [ProtoMember(3)] public float Radial;
                    [ProtoMember(4)] public float BackwardDegrees;
                    [ProtoMember(5)] public float Degrees;
                    [ProtoMember(6)] public bool Reverse;
                    [ProtoMember(7)] public bool IgnoreArming;
                    [ProtoMember(8)] public bool DropVelocity;
                    [ProtoMember(9)] public float Offset;
                    [ProtoMember(10)] public int MaxChildren;
                    [ProtoMember(11)] public TimedSpawnDef TimedSpawns;
                    [ProtoMember(12)] public bool FireSound;
                    [ProtoMember(13)] public Vector3D AdvOffset;
                    [ProtoMember(14)] public bool ArmWhenHit;

                    [ProtoContract]
                    public struct TimedSpawnDef
                    {
                        public enum PointTypes
                        {
                            Direct,
                            Lead,
                            Predict,
                        }

                        [ProtoMember(1)] public bool Enable;
                        [ProtoMember(2)] public int Interval;
                        [ProtoMember(3)] public int StartTime;
                        [ProtoMember(4)] public int MaxSpawns;
                        [ProtoMember(5)] public double Proximity;
                        [ProtoMember(6)] public bool ParentDies;
                        [ProtoMember(7)] public bool PointAtTarget;
                        [ProtoMember(8)] public int GroupSize;
                        [ProtoMember(9)] public int GroupDelay;
                        [ProtoMember(10)] public PointTypes PointType;
                    }
                }

                [ProtoContract]
                public struct PatternDef
                {
                    public enum PatternModes
                    {
                        Never,
                        Weapon,
                        Fragment,
                        Both,
                    }


                    [ProtoMember(1)] public string[] Patterns;
                    [ProtoMember(2)] public bool Enable;
                    [ProtoMember(3)] public float TriggerChance;
                    [ProtoMember(4)] public bool SkipParent;
                    [ProtoMember(5)] public bool Random;
                    [ProtoMember(6)] public int RandomMin;
                    [ProtoMember(7)] public int RandomMax;
                    [ProtoMember(8)] public int PatternSteps;
                    [ProtoMember(9)] public PatternModes Mode;
                }

                [ProtoContract]
                public struct EjectionDef
                {
                    public enum SpawnType
                    {
                        Item,
                        Particle,
                    }
                    [ProtoMember(1)] public float Speed;
                    [ProtoMember(2)] public float SpawnChance;
                    [ProtoMember(3)] public SpawnType Type;
                    [ProtoMember(4)] public ComponentDef CompDef;

                    [ProtoContract]
                    public struct ComponentDef
                    {
                        [ProtoMember(1)] public string ItemName;
                        [ProtoMember(2)] public int ItemLifeTime;
                        [ProtoMember(3)] public int Delay;
                    }
                }

                [ProtoContract]
                public struct AreaOfDamageDef
                {
                    public enum Falloff
                    {
                        Legacy,
                        NoFalloff,
                        Linear,
                        Curve,
                        InvCurve,
                        Squeeze,
                        Pooled,
                        Exponential,
                    }

                    public enum AoeShape
                    {
                        Round,
                        Diamond,
                    }

                    [ProtoMember(1)] public ByBlockHitDef ByBlockHit;
                    [ProtoMember(2)] public EndOfLifeDef EndOfLife;

                    [ProtoContract]
                    public struct ByBlockHitDef
                    {
                        [ProtoMember(1)] public bool Enable;
                        [ProtoMember(2)] public double Radius;
                        [ProtoMember(3)] public float Damage;
                        [ProtoMember(4)] public float Depth;
                        [ProtoMember(5)] public float MaxAbsorb;
                        [ProtoMember(6)] public Falloff Falloff;
                        [ProtoMember(7)] public AoeShape Shape;
                    }

                    [ProtoContract]
                    public struct EndOfLifeDef
                    {
                        [ProtoMember(1)] public bool Enable;
                        [ProtoMember(2)] public double Radius;
                        [ProtoMember(3)] public float Damage;
                        [ProtoMember(4)] public float Depth;
                        [ProtoMember(5)] public float MaxAbsorb;
                        [ProtoMember(6)] public Falloff Falloff;
                        [ProtoMember(7)] public bool ArmOnlyOnHit;
                        [ProtoMember(8)] public int MinArmingTime;
                        [ProtoMember(9)] public bool NoVisuals;
                        [ProtoMember(10)] public bool NoSound;
                        [ProtoMember(11)] public float ParticleScale;
                        [ProtoMember(12)] public string CustomParticle;
                        [ProtoMember(13)] public string CustomSound;
                        [ProtoMember(14)] public AoeShape Shape;
                    }
                }

                [ProtoContract]
                public struct EwarDef
                {
                    public enum EwarType
                    {
                        AntiSmart,
                        JumpNull,
                        EnergySink,
                        Anchor,
                        Emp,
                        Offense,
                        Nav,
                        Dot,
                        Push,
                        Pull,
                        Tractor,
                    }

                    public enum EwarMode
                    {
                        Effect,
                        Field,
                    }

                    [ProtoMember(1)] public bool Enable;
                    [ProtoMember(2)] public EwarType Type;
                    [ProtoMember(3)] public EwarMode Mode;
                    [ProtoMember(4)] public float Strength;
                    [ProtoMember(5)] public double Radius;
                    [ProtoMember(6)] public int Duration;
                    [ProtoMember(7)] public bool StackDuration;
                    [ProtoMember(8)] public bool Depletable;
                    [ProtoMember(9)] public int MaxStacks;
                    [ProtoMember(10)] public bool NoHitParticle;
                    [ProtoMember(11)] public PushPullDef Force;
                    [ProtoMember(12)] public FieldDef Field;


                    [ProtoContract]
                    public struct FieldDef
                    {
                        [ProtoMember(1)] public int Interval;
                        [ProtoMember(2)] public int PulseChance;
                        [ProtoMember(3)] public int GrowTime;
                        [ProtoMember(4)] public bool HideModel;
                        [ProtoMember(5)] public bool ShowParticle;
                        [ProtoMember(6)] public double TriggerRange;
                        [ProtoMember(7)] public ParticleDef Particle;
                    }

                    [ProtoContract]
                    public struct PushPullDef
                    {
                        public enum Force
                        {
                            ProjectileLastPosition,
                            ProjectileOrigin,
                            HitPosition,
                            TargetCenter,
                            TargetCenterOfMass,
                        }

                        [ProtoMember(1)] public Force ForceFrom;
                        [ProtoMember(2)] public Force ForceTo;
                        [ProtoMember(3)] public Force Position;
                        [ProtoMember(4)] public bool DisableRelativeMass;
                        [ProtoMember(5)] public double TractorRange;
                        [ProtoMember(6)] public bool ShooterFeelsForce;
                    }
                }


                [ProtoContract]
                public struct AreaDamageDef
                {
                    public enum AreaEffectType
                    {
                        Disabled,
                        Explosive,
                        Radiant,
                        AntiSmart,
                        JumpNullField,
                        EnergySinkField,
                        AnchorField,
                        EmpField,
                        OffenseField,
                        NavField,
                        DotField,
                        PushField,
                        PullField,
                        TractorField,
                    }

                    [ProtoMember(1)] public double AreaEffectRadius;
                    [ProtoMember(2)] public float AreaEffectDamage;
                    [ProtoMember(3)] public AreaEffectType AreaEffect;
                    [ProtoMember(4)] public PulseDef Pulse;
                    [ProtoMember(5)] public DetonateDef Detonation;
                    [ProtoMember(6)] public ExplosionDef Explosions;
                    [ProtoMember(7)] public EwarFieldsDef EwarFields;
                    [ProtoMember(8)] public AreaInfluence Base;

                    [ProtoContract]
                    public struct AreaInfluence
                    {
                        [ProtoMember(1)] public double Radius;
                        [ProtoMember(2)] public float EffectStrength;
                    }


                    [ProtoContract]
                    public struct PulseDef
                    {
                        [ProtoMember(1)] public int Interval;
                        [ProtoMember(2)] public int PulseChance;
                        [ProtoMember(3)] public int GrowTime;
                        [ProtoMember(4)] public bool HideModel;
                        [ProtoMember(5)] public bool ShowParticle;
                        [ProtoMember(6)] public ParticleDef Particle;
                    }

                    [ProtoContract]
                    public struct EwarFieldsDef
                    {
                        [ProtoMember(1)] public int Duration;
                        [ProtoMember(2)] public bool StackDuration;
                        [ProtoMember(3)] public bool Depletable;
                        [ProtoMember(4)] public double TriggerRange;
                        [ProtoMember(5)] public int MaxStacks;
                        [ProtoMember(6)] public PushPullDef Force;
                        [ProtoMember(7)] public bool DisableParticleEffect;

                        [ProtoContract]
                        public struct PushPullDef
                        {
                            public enum Force
                            {
                                ProjectileLastPosition,
                                ProjectileOrigin,
                                HitPosition,
                                TargetCenter,
                                TargetCenterOfMass,
                            }

                            [ProtoMember(1)] public Force ForceFrom;
                            [ProtoMember(2)] public Force ForceTo;
                            [ProtoMember(3)] public Force Position;
                            [ProtoMember(4)] public bool DisableRelativeMass;
                            [ProtoMember(5)] public double TractorRange;
                            [ProtoMember(6)] public bool ShooterFeelsForce;
                        }
                    }

                    [ProtoContract]
                    public struct DetonateDef
                    {
                        [ProtoMember(1)] public bool DetonateOnEnd;
                        [ProtoMember(2)] public bool ArmOnlyOnHit;
                        [ProtoMember(3)] public float DetonationRadius;
                        [ProtoMember(4)] public float DetonationDamage;
                        [ProtoMember(5)] public int MinArmingTime;
                    }

                    [ProtoContract]
                    public struct ExplosionDef
                    {
                        [ProtoMember(1)] public bool NoVisuals;
                        [ProtoMember(2)] public bool NoSound;
                        [ProtoMember(3)] public float Scale;
                        [ProtoMember(4)] public string CustomParticle;
                        [ProtoMember(5)] public string CustomSound;
                        [ProtoMember(6)] public bool NoShrapnel;
                        [ProtoMember(7)] public bool NoDeformation;
                    }
                }

                [ProtoContract]
                public struct AmmoAudioDef
                {
                    [ProtoMember(1)] public string TravelSound;
                    [ProtoMember(2)] public string HitSound;
                    [ProtoMember(3)] public float HitPlayChance;
                    [ProtoMember(4)] public bool HitPlayShield;
                    [ProtoMember(5)] public string VoxelHitSound;
                    [ProtoMember(6)] public string PlayerHitSound;
                    [ProtoMember(7)] public string FloatingHitSound;
                    [ProtoMember(8)] public string ShieldHitSound;
                    [ProtoMember(9)] public string ShotSound;
                }

                [ProtoContract]
                public struct TrajectoryDef
                {
                    public enum GuidanceType
                    {
                        None,
                        Remote,
                        TravelTo,
                        Smart,
                        DetectTravelTo,
                        DetectSmart,
                        DetectFixed,
                        DroneAdvanced,
                    }

                    [ProtoMember(1)] public float MaxTrajectory;
                    [ProtoMember(2)] public float AccelPerSec;
                    [ProtoMember(3)] public float DesiredSpeed;
                    [ProtoMember(4)] public float TargetLossDegree;
                    [ProtoMember(5)] public int TargetLossTime;
                    [ProtoMember(6)] public int MaxLifeTime;
                    [ProtoMember(7)] public int DeaccelTime;
                    [ProtoMember(8)] public Randomize SpeedVariance;
                    [ProtoMember(9)] public Randomize RangeVariance;
                    [ProtoMember(10)] public GuidanceType Guidance;
                    [ProtoMember(11)] public SmartsDef Smarts;
                    [ProtoMember(12)] public MinesDef Mines;
                    [ProtoMember(13)] public float GravityMultiplier;
                    [ProtoMember(14)] public uint MaxTrajectoryTime;
                    [ProtoMember(15)] public ApproachDef[] Approaches;
                    [ProtoMember(16)] public double TotalAcceleration;

                    [ProtoContract]
                    public struct SmartsDef
                    {
                        [ProtoMember(1)] public double Inaccuracy;
                        [ProtoMember(2)] public double Aggressiveness;
                        [ProtoMember(3)] public double MaxLateralThrust;
                        [ProtoMember(4)] public double TrackingDelay;
                        [ProtoMember(5)] public int MaxChaseTime;
                        [ProtoMember(6)] public bool OverideTarget;
                        [ProtoMember(7)] public int MaxTargets;
                        [ProtoMember(8)] public bool NoTargetExpire;
                        [ProtoMember(9)] public bool Roam;
                        [ProtoMember(10)] public bool KeepAliveAfterTargetLoss;
                        [ProtoMember(11)] public float OffsetRatio;
                        [ProtoMember(12)] public int OffsetTime;
                        [ProtoMember(13)] public bool CheckFutureIntersection;
                        [ProtoMember(14)] public double NavAcceleration;
                        [ProtoMember(15)] public bool AccelClearance;
                        [ProtoMember(16)] public double SteeringLimit;
                        [ProtoMember(17)] public bool FocusOnly;
                        [ProtoMember(18)] public double OffsetMinRange;
                        [ProtoMember(19)] public bool FocusEviction;
                        [ProtoMember(20)] public double ScanRange;
                        [ProtoMember(21)] public bool NoSteering;
                        [ProtoMember(22)] public double FutureIntersectionRange;
                        [ProtoMember(23)] public double MinTurnSpeed;
                        [ProtoMember(24)] public bool NoTargetApproach;
                        [ProtoMember(25)] public bool AltNavigation;
                    }

                    [ProtoContract]
                    public struct ApproachDef
                    {
                        public enum ReInitCondition
                        {
                            Wait,
                            MoveToPrevious,
                            MoveToNext,
                            ForceRestart,
                        }

                        public enum Conditions
                        {
                            Ignore,
                            Spawn,
                            DistanceFromPositionC,
                            Lifetime,
                            DesiredElevation,
                            MinTravelRequired,
                            MaxTravelRequired,
                            Deadtime,
                            DistanceToPositionC,
                            NextTimedSpawn,
                            RelativeLifetime,
                            RelativeDeadtime,
                            SinceTimedSpawn,
                            RelativeSpawns,
                            EnemyTargetLoss,
                            RelativeHealthLost,
                            HealthRemaining,
                            DistanceFromPositionB,
                            DistanceToPositionB,
                            DistanceFromTarget,
                            DistanceToTarget,
                            DistanceFromEndTrajectory,
                            DistanceToEndTrajectory,
                        }

                        public enum UpRelativeTo
                        {
                            UpRelativeToBlock,
                            UpRelativeToGravity,
                            UpTargetDirection,
                            UpTargetVelocity,
                            UpStoredStartDontUse,
                            UpStoredEndDontUse,
                            UpStoredStartPosition,
                            UpStoredEndPosition,
                            UpStoredStartLocalPosition,
                            UpStoredEndLocalPosition,
                            UpRelativeToShooter,
                            UpOriginDirection,
                            UpElevationDirection,
                        }

                        public enum FwdRelativeTo
                        {
                            ForwardElevationDirection,
                            ForwardRelativeToBlock,
                            ForwardRelativeToGravity,
                            ForwardTargetDirection,
                            ForwardTargetVelocity,
                            ForwardStoredStartDontUse,
                            ForwardStoredEndDontUse,
                            ForwardStoredStartPosition,
                            ForwardStoredEndPosition,
                            ForwardStoredStartLocalPosition,
                            ForwardStoredEndLocalPosition,
                            ForwardRelativeToShooter,
                            ForwardOriginDirection,
                        }

                        public enum RelativeTo
                        {
                            Origin,
                            Shooter,
                            Target,
                            Surface,
                            MidPoint,
                            PositionA,
                            Nothing,
                            StoredStartDontUse,
                            StoredEndDontUse,
                            StoredStartPosition,
                            StoredEndPosition,
                            StoredStartLocalPosition,
                            StoredEndLocalPosition,
                        }

                        public enum ConditionOperators
                        {
                            StartEnd_And,
                            StartEnd_Or,
                            StartAnd_EndOr,
                            StartOr_EndAnd,
                        }

                        public enum StageEvents
                        {
                            DoNothing,
                            EndProjectile,
                            EndProjectileOnRestart,
                            StoreDontUse,
                            StorePositionDontUse,
                            Refund,
                            StorePositionA,
                            StorePositionB,
                            StorePositionC,
                        }

                        [ProtoContract]
                        public struct WeightedIdListDef
                        {

                            [ProtoMember(1)] public int ApproachId;
                            [ProtoMember(2)] public Randomize Weight;
                            [ProtoMember(3)] public double End1WeightMod;
                            [ProtoMember(4)] public double End2WeightMod;
                            [ProtoMember(5)] public int MaxRuns;
                            [ProtoMember(6)] public double End3WeightMod;
                        }

                        [ProtoMember(1)] public ReInitCondition RestartCondition;
                        [ProtoMember(2)] public Conditions StartCondition1;
                        [ProtoMember(3)] public Conditions EndCondition1;
                        [ProtoMember(4)] public UpRelativeTo Up;
                        [ProtoMember(5)] public RelativeTo PositionB;
                        [ProtoMember(6)] public double AngleOffset;
                        [ProtoMember(7)] public double Start1Value;
                        [ProtoMember(8)] public double End1Value;
                        [ProtoMember(9)] public double LeadDistance;
                        [ProtoMember(10)] public double DesiredElevation;
                        [ProtoMember(11)] public double AccelMulti;
                        [ProtoMember(12)] public double SpeedCapMulti;
                        [ProtoMember(13)] public bool AdjustPositionC;
                        [ProtoMember(14)] public bool CanExpireOnceStarted;
                        [ProtoMember(15)] public ParticleDef AlternateParticle;
                        [ProtoMember(16)] public string AlternateSound;
                        [ProtoMember(17)] public string AlternateModel;
                        [ProtoMember(18)] public int OnRestartRevertTo;
                        [ProtoMember(19)] public ParticleDef StartParticle;
                        [ProtoMember(20)] public bool AdjustPositionB;
                        [ProtoMember(21)] public bool AdjustUp;
                        [ProtoMember(22)] public bool PushLeadByTravelDistance;
                        [ProtoMember(23)] public double TrackingDistance;
                        [ProtoMember(24)] public Conditions StartCondition2;
                        [ProtoMember(25)] public double Start2Value;
                        [ProtoMember(26)] public Conditions EndCondition2;
                        [ProtoMember(27)] public double End2Value;
                        [ProtoMember(28)] public RelativeTo Elevation;
                        [ProtoMember(29)] public double ElevationTolerance;
                        [ProtoMember(30)] public ConditionOperators Operators;
                        [ProtoMember(31)] public StageEvents StartEvent;
                        [ProtoMember(32)] public StageEvents EndEvent;
                        [ProtoMember(33)] public double TotalAccelMulti;
                        [ProtoMember(34)] public double DeAccelMulti;
                        [ProtoMember(35)] public bool Orbit;
                        [ProtoMember(36)] public double OrbitRadius;
                        [ProtoMember(37)] public int OffsetTime;
                        [ProtoMember(38)] public double OffsetMinRadius;
                        [ProtoMember(39)] public bool NoTimedSpawns;
                        [ProtoMember(40)] public double OffsetMaxRadius;
                        [ProtoMember(41)] public bool ForceRestart;
                        [ProtoMember(42)] public RelativeTo PositionC;
                        [ProtoMember(43)] public bool DisableAvoidance;
                        [ProtoMember(44)] public int StoredStartId;
                        [ProtoMember(45)] public int StoredEndId;
                        [ProtoMember(46)] public WeightedIdListDef[] RestartList;
                        [ProtoMember(47)] public RelativeTo StoredStartType;
                        [ProtoMember(48)] public RelativeTo StoredEndType;
                        [ProtoMember(49)] public bool LeadRotateElevatePositionB;
                        [ProtoMember(50)] public bool LeadRotateElevatePositionC;
                        [ProtoMember(51)] public bool NoElevationLead;
                        [ProtoMember(52)] public bool IgnoreAntiSmart;
                        [ProtoMember(53)] public double HeatRefund;
                        [ProtoMember(54)] public Randomize AngleVariance;
                        [ProtoMember(55)] public bool ReloadRefund;
                        [ProtoMember(56)] public int ModelRotateTime;
                        [ProtoMember(57)] public FwdRelativeTo Forward;
                        [ProtoMember(58)] public bool AdjustForward;
                        [ProtoMember(59)] public bool ToggleIngoreVoxels;
                        [ProtoMember(60)] public bool SelfAvoidance;
                        [ProtoMember(61)] public bool TargetAvoidance;
                        [ProtoMember(62)] public bool SelfPhasing;
                        [ProtoMember(63)] public bool TrajectoryRelativeToB;
                        [ProtoMember(64)] public Conditions EndCondition3;
                        [ProtoMember(65)] public double End3Value;
                        [ProtoMember(66)] public bool SwapNavigationType;
                        [ProtoMember(67)] public bool ElevationRelativeToC;
                    }

                    [ProtoContract]
                    public struct MinesDef
                    {
                        [ProtoMember(1)] public double DetectRadius;
                        [ProtoMember(2)] public double DeCloakRadius;
                        [ProtoMember(3)] public int FieldTime;
                        [ProtoMember(4)] public bool Cloak;
                        [ProtoMember(5)] public bool Persist;
                    }
                }

                [ProtoContract]
                public struct Randomize
                {
                    [ProtoMember(1)] public float Start;
                    [ProtoMember(2)] public float End;
                }
            }

            [ProtoContract]
            public struct ParticleOptionDef
            {
                [ProtoMember(1)] public float Scale;
                [ProtoMember(2)] public float MaxDistance;
                [ProtoMember(3)] public float MaxDuration;
                [ProtoMember(4)] public bool Loop;
                [ProtoMember(5)] public bool Restart;
                [ProtoMember(6)] public float HitPlayChance;
            }


            [ProtoContract]
            public struct ParticleDef
            {
                [ProtoMember(1)] public string Name;
                [ProtoMember(2)] public Vector4 Color;
                [ProtoMember(3)] public Vector3D Offset;
                [ProtoMember(4)] public ParticleOptionDef Extras;
                [ProtoMember(5)] public bool ApplyToShield;
                [ProtoMember(6)] public bool DisableCameraCulling;
            }
        }
    }

}