using ProtoBuf;
using VRageMath;

namespace Whiplash.WeaponFramework
{
    /// <summary>
    /// https://gitlab.com/whiplash141/Revived-Railgun-Mod/-/blob/develop/Data/Scripts/WeaponFramework/WhipsWeaponFramework/WeaponConfig.cs
    /// </summary>
    [ProtoContract]
    [ProtoInclude(1000, typeof(TurretWeaponConfig))]
    public class WeaponConfig
    {
        // Not ini configurable
        [ProtoMember(1)]
        public string BlockSubtype;
        [ProtoMember(2)]
        public string ConfigID;
        [ProtoMember(3)]
        public string ConfigFileName;
        [ProtoMember(4)]
        public string FireSoundName;
        [ProtoMember(5)]
        public bool DrawMuzzleFlash;
        [ProtoMember(6)]
        public string MuzzleFlashSpriteName;
        [ProtoMember(7)]
        public float MuzzleFlashDuration;
        [ProtoMember(8)]
        public float MuzzleFlashScale;
        [ProtoMember(9)]
        public float BulletSpawnForwardOffsetMeters;
        [ProtoMember(10)]
        public bool ShowReloadMessage;
        [ProtoMember(11)]
        public string ReloadMessage;
        [ProtoMember(12)]
        public float FireSoundVolumeMultiplier;
        [ProtoMember(13)]
        public float HitImpulse;

        // Ini configurable
        [ProtoMember(14)]
        public Vector3 TracerColor;
        [ProtoMember(15)]
        public float TracerScale;
        [ProtoMember(16)]
        public float ArtificialGravityMultiplier;
        [ProtoMember(17)]
        public float NaturalGravityMultiplier;
        [ProtoMember(18)]
        public bool ShouldDrawProjectileTrails;
        [ProtoMember(19)]
        public float ProjectileTrailFadeRatio;
        [ProtoMember(20)]
        public bool ExplodeOnContact;
        [ProtoMember(21)]
        public float ContactExplosionRadius;
        [ProtoMember(22)]
        public float ContactExplosionDamage;
        [ProtoMember(23)]
        public bool PenetrateOnContact;
        [ProtoMember(24)]
        public float PenetrationDamage;
        [ProtoMember(25)]
        public float PenetrationRange;
        [ProtoMember(26)]
        public bool ExplodePostPenetration;
        [ProtoMember(27)]
        public float PenetrationExplosionRadius;
        [ProtoMember(28)]
        public float PenetrationExplosionDamage;
        [ProtoMember(29)]
        public float IdlePowerDrawBase;
        [ProtoMember(30)]
        public float ReloadPowerDraw;
        [ProtoMember(31)]
        public float MuzzleVelocity;
        [ProtoMember(32)]
        public float MaxRange;
        [ProtoMember(33)]
        public float DeviationAngleDeg;
        [ProtoMember(34)]
        public float RecoilImpulse;
        [ProtoMember(35)]
        public float ShieldDamageMultiplier;
        [ProtoMember(36)]
        public float RateOfFireRPM;

        // Not ini configurable
        [ProtoMember(37)]
        public bool DrawImpactSprite;
        [ProtoMember(38)]
        public string ImpactSpriteName;
        [ProtoMember(39)]
        public float ImpactSpriteDuration;
        [ProtoMember(40)]
        public float ImpactSpriteScale;

        [ProtoMember(41)]
        public float ProximityDetonationRange;
        [ProtoMember(42)]
        public bool ShouldProximityDetonate;
        [ProtoMember(43)]
        public float ProximityDetonationArmingRange;

        // Leave space for proximity detonation vars
        /// <summary>
        /// If this key is specified, the config file will be regenerated if it does not have a key that matches this.
        /// </summary>
        [ProtoMember(44)]
        public string ConfigVersionKey;
    }

    [ProtoContract]
    public class TurretWeaponConfig : WeaponConfig
    {
        [ProtoMember(1)]
        public float IdlePowerDrawMax;
    }
}