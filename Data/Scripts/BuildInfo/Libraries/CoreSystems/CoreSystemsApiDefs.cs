using ProtoBuf;
using VRageMath;

namespace CoreSystems.Api
{
    // Modified to fit the needs of this mod
    // Grab the originals instead: https://github.com/Ash-LikeSnow/WeaponCore/tree/master/Data/Scripts/CoreSystems/Api

    public static class CoreSystemsDef
    {
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
            [ProtoMember(4)] public HardPointDef HardPoint;
            [ProtoMember(6)] public string ModPath;

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
            public struct HardPointDef
            {
                [ProtoMember(1)] public string PartName;
                [ProtoMember(2)] public int DelayCeaseFire;
                [ProtoMember(3)] public float DeviateShotAngle;
                [ProtoMember(4)] public double AimingTolerance;
                [ProtoMember(8)] public HardwareDef HardWare;
                [ProtoMember(13)] public bool AddToleranceToTracking;
                [ProtoMember(14)] public bool CanShootSubmerged;
                [ProtoMember(15)] public bool NpcSafe;
                [ProtoMember(16)] public bool ScanTrackOnly;

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
                    [ProtoMember(14)] public float IdlePower;
                }
            }
        }
    }
}