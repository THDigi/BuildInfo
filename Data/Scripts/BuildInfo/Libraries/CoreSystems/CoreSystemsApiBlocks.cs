using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage;
using VRage.ModAPI;
using VRageMath;

namespace CoreSystems.Api
{
    /// <summary>
    /// https://github.com/sstixrud/CoreSystems/blob/master/BaseData/Scripts/CoreSystems/Api/CoreSystemsApiBlocks.cs
    /// </summary>
    public partial class CoreSystemsAPI
    {
        private Func<IMyTerminalBlock, IDictionary<string, int>, bool> _getBlockWeaponMap;
        private Func<IMyTerminalBlock, int, MyTuple<bool, bool, bool, IMyEntity>> _getWeaponTarget;
        private Action<IMyTerminalBlock, IMyEntity, int> _setWeaponTarget;
        private Action<IMyTerminalBlock, bool, int> _fireWeaponOnce;
        private Action<IMyTerminalBlock, bool, bool, int> _toggleWeaponFire;
        private Func<IMyTerminalBlock, int, bool, bool, bool> _isWeaponReadyToFire;
        private Func<IMyTerminalBlock, int, float> _getMaxWeaponRange;
        private Func<IMyTerminalBlock, ICollection<string>, int, bool> _getTurretTargetTypes;
        private Action<IMyTerminalBlock, ICollection<string>, int> _setTurretTargetTypes;
        private Action<IMyTerminalBlock, float> _setBlockTrackingRange;
        private Func<IMyTerminalBlock, IMyEntity, int, bool> _isTargetAligned;
        private Func<IMyTerminalBlock, IMyEntity, int, MyTuple<bool, Vector3D?>> _isTargetAlignedExtended;
        private Func<IMyTerminalBlock, IMyEntity, int, bool> _canShootTarget;
        private Func<IMyTerminalBlock, IMyEntity, int, Vector3D?> _getPredictedTargetPos;
        private Func<IMyTerminalBlock, float> _getHeatLevel;
        private Func<IMyTerminalBlock, float> _currentPowerConsumption;
        private Action<IMyTerminalBlock> _disableRequiredPower;
        private Func<IMyTerminalBlock, bool> _hasCoreWeapon;
        private Func<IMyTerminalBlock, int, string> _getActiveAmmo;
        private Action<IMyTerminalBlock, int, string> _setActiveAmmo;
        private Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>> _monitorProjectile; // Legacy use base version
        private Action<IMyTerminalBlock, int, Action<long, int, ulong, long, Vector3D, bool>> _unMonitorProjectile; // Legacy use base version
        private Func<IMyTerminalBlock, long> _getPlayerController;
        private Func<IMyTerminalBlock, int, Matrix> _getWeaponAzimuthMatrix;
        private Func<IMyTerminalBlock, int, Matrix> _getWeaponElevationMatrix;
        private Func<IMyTerminalBlock, IMyEntity, bool, bool, bool> _isTargetValid;
        private Func<IMyTerminalBlock, int, MyTuple<Vector3D, Vector3D>> _getWeaponScope;

        public bool GetBlockWeaponMap(IMyTerminalBlock weaponBlock, IDictionary<string, int> collection) =>
            _getBlockWeaponMap?.Invoke(weaponBlock, collection) ?? false;

        public void SetWeaponTarget(IMyTerminalBlock weapon, IMyEntity target, int weaponId = 0) =>
            _setWeaponTarget?.Invoke(weapon, target, weaponId);

        public void FireWeaponOnce(IMyTerminalBlock weapon, bool allWeapons = true, int weaponId = 0) =>
            _fireWeaponOnce?.Invoke(weapon, allWeapons, weaponId);

        public void ToggleWeaponFire(IMyTerminalBlock weapon, bool on, bool allWeapons, int weaponId = 0) =>
            _toggleWeaponFire?.Invoke(weapon, on, allWeapons, weaponId);

        public bool IsWeaponReadyToFire(IMyTerminalBlock weapon, int weaponId = 0, bool anyWeaponReady = true,
            bool shootReady = false) =>
            _isWeaponReadyToFire?.Invoke(weapon, weaponId, anyWeaponReady, shootReady) ?? false;

        public float GetMaxWeaponRange(IMyTerminalBlock weapon, int weaponId) =>
            _getMaxWeaponRange?.Invoke(weapon, weaponId) ?? 0f;

        public bool GetTurretTargetTypes(IMyTerminalBlock weapon, IList<string> collection, int weaponId = 0) =>
            _getTurretTargetTypes?.Invoke(weapon, collection, weaponId) ?? false;

        public void SetTurretTargetTypes(IMyTerminalBlock weapon, IList<string> collection, int weaponId = 0) =>
            _setTurretTargetTypes?.Invoke(weapon, collection, weaponId);

        public void SetBlockTrackingRange(IMyTerminalBlock weapon, float range) =>
            _setBlockTrackingRange?.Invoke(weapon, range);

        public bool IsTargetAligned(IMyTerminalBlock weapon, IMyEntity targetEnt, int weaponId) =>
            _isTargetAligned?.Invoke(weapon, targetEnt, weaponId) ?? false;

        public MyTuple<bool, Vector3D?> IsTargetAlignedExtended(IMyTerminalBlock weapon, IMyEntity targetEnt, int weaponId) =>
            _isTargetAlignedExtended?.Invoke(weapon, targetEnt, weaponId) ?? new MyTuple<bool, Vector3D?>();

        public bool CanShootTarget(IMyTerminalBlock weapon, IMyEntity targetEnt, int weaponId) =>
            _canShootTarget?.Invoke(weapon, targetEnt, weaponId) ?? false;

        public Vector3D? GetPredictedTargetPosition(IMyTerminalBlock weapon, IMyEntity targetEnt, int weaponId) =>
            _getPredictedTargetPos?.Invoke(weapon, targetEnt, weaponId) ?? null;

        public float GetHeatLevel(IMyTerminalBlock weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;
        public float GetCurrentPower(IMyTerminalBlock weapon) => _currentPowerConsumption?.Invoke(weapon) ?? 0f;
        public void DisableRequiredPower(IMyTerminalBlock weapon) => _disableRequiredPower?.Invoke(weapon);
        public bool HasCoreWeapon(IMyTerminalBlock weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;

        public string GetActiveAmmo(IMyTerminalBlock weapon, int weaponId) =>
            _getActiveAmmo?.Invoke(weapon, weaponId) ?? null;

        public void SetActiveAmmo(IMyTerminalBlock weapon, int weaponId, string ammoType) =>
            _setActiveAmmo?.Invoke(weapon, weaponId, ammoType);

        public void MonitorProjectileCallback(IMyTerminalBlock weapon, int weaponId, Action<long, int, ulong, long, Vector3D, bool> action) =>
            _monitorProjectile?.Invoke(weapon, weaponId, action);

        public void UnMonitorProjectileCallback(IMyTerminalBlock weapon, int weaponId, Action<long, int, ulong, long, Vector3D, bool> action) =>
            _unMonitorProjectile?.Invoke(weapon, weaponId, action);

        public long GetPlayerController(IMyTerminalBlock weapon) => _getPlayerController?.Invoke(weapon) ?? -1;

        public Matrix GetWeaponAzimuthMatrix(IMyTerminalBlock weapon, int weaponId) =>
            _getWeaponAzimuthMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;

        public Matrix GetWeaponElevationMatrix(IMyTerminalBlock weapon, int weaponId) =>
            _getWeaponElevationMatrix?.Invoke(weapon, weaponId) ?? Matrix.Zero;

        public bool IsTargetValid(IMyTerminalBlock weapon, IMyEntity target, bool onlyThreats, bool checkRelations) =>
            _isTargetValid?.Invoke(weapon, target, onlyThreats, checkRelations) ?? false;
    }
}