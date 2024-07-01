using System;
using System.Collections.Generic;
using Digi;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Entity;

namespace CoreSystems.Api
{
    // Modified to fit the needs of this mod
    // Grab the originals instead: https://github.com/Ash-LikeSnow/WeaponCore/tree/master/Data/Scripts/CoreSystems/Api

    public partial class CoreSystemsAPI
    {
        /// <summary>
        /// True if CoreSystems replied when <see cref="Load"/> got called.
        /// </summary>
        public bool IsReady { get; private set; }

        public MyTuple<bool, bool, bool, MyEntity> GetWeaponTarget(MyEntity weapon, int weaponId = 0) => _getWeaponTarget?.Invoke(weapon, weaponId) ?? new MyTuple<bool, bool, bool, MyEntity>();
        public float GetMaxWeaponRange(MyEntity weapon, int weaponId) => _getMaxWeaponRange?.Invoke(weapon, weaponId) ?? 0f;

        public float GetHeatLevel(MyEntity weapon) => _getHeatLevel?.Invoke(weapon) ?? 0f;
        public float GetCurrentPower(MyEntity weapon) => _currentPowerConsumption?.Invoke(weapon) ?? 0f;
        public bool HasCoreWeapon(MyEntity weapon) => _hasCoreWeapon?.Invoke(weapon) ?? false;
        public string GetActiveAmmo(MyEntity weapon, int weaponId) => _getActiveAmmo?.Invoke(weapon, weaponId) ?? null;
        public long GetPlayerController(MyEntity weapon) => _getPlayerController?.Invoke(weapon) ?? -1;

        public void GetAllWeaponDefinitions(IList<byte[]> collection) => _getAllWeaponDefinitions?.Invoke(collection);
        public void GetAllCoreArmors(IList<byte[]> collection) => _getCoreArmors?.Invoke(collection);

        public float GetMaxPower(MyDefinitionId weaponDef) => _getMaxPower?.Invoke(weaponDef) ?? 0f;
        public bool HasAi(MyEntity entity) => _hasAi?.Invoke(entity) ?? false;
        public float GetOptimalDps(MyEntity entity) => _getOptimalDps?.Invoke(entity) ?? 0f;

        public float GetConstructEffectiveDps(MyEntity entity) => _getConstructEffectiveDps?.Invoke(entity) ?? 0f;

        bool _apiInit;

        Action<IList<byte[]>> _getAllWeaponDefinitions;
        Action<IList<byte[]>> _getCoreArmors;

        Func<MyEntity, bool> _hasAi;
        Func<MyEntity, bool> _hasCoreWeapon;

        Func<MyEntity, long> _getPlayerController;
        Func<MyDefinitionId, float> _getMaxPower;

        Func<MyEntity, float> _getOptimalDps;
        Func<MyEntity, float> _getConstructEffectiveDps;

        Func<MyEntity, int, MyTuple<bool, bool, bool, MyEntity>> _getWeaponTarget;
        Func<MyEntity, int, float> _getMaxWeaponRange;
        Func<MyEntity, float> _getHeatLevel;
        Func<MyEntity, float> _currentPowerConsumption;
        Func<MyEntity, int, string> _getActiveAmmo;

        const long Channel = 67549756549;
        bool _isRegistered;
        Action _readyCallback;

        /// <summary>
        /// Ask CoreSystems to send the API methods.
        /// <para>Throws an exception if it gets called more than once per session without <see cref="Unload"/>.</para>
        /// </summary>
        /// <param name="readyCallback">Method to be called when CoreSystems replies.</param>
        /// <param name="getWeaponDefinitions">Set to true to fill <see cref="WeaponDefinitions"/>.</param>
        public void Load(Action readyCallback = null, bool getWeaponDefinitions = false)
        {
            if(_isRegistered)
                throw new Exception($"{GetType().Name}.Load() should not be called multiple times!");

            _readyCallback = readyCallback;
            _isRegistered = true;
            MyAPIGateway.Utilities.RegisterMessageHandler(Channel, HandleMessage);
            MyAPIGateway.Utilities.SendModMessage(Channel, "ApiEndpointRequest");
        }

        public void Unload()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, HandleMessage);

            ApiAssign(null);

            _isRegistered = false;
            _apiInit = false;
            IsReady = false;
        }

        void HandleMessage(object obj)
        {
            try
            {
                if(_apiInit || obj is string) // the sent "ApiEndpointRequest" will also be received here, explicitly ignoring that
                    return;

                var dict = obj as IReadOnlyDictionary<string, Delegate>;
                if(dict == null)
                    return;

                ApiAssign(dict);

                IsReady = true;
                _readyCallback?.Invoke();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            _apiInit = (delegates != null);

            /// base methods
            AssignMethod(delegates, "GetAllWeaponDefinitions", ref _getAllWeaponDefinitions);
            AssignMethod(delegates, "GetCoreArmors", ref _getCoreArmors);

            AssignMethod(delegates, "GetMaxPower", ref _getMaxPower);
            AssignMethod(delegates, "HasGridAiBase", ref _hasAi);
            AssignMethod(delegates, "GetOptimalDpsBase", ref _getOptimalDps);
            AssignMethod(delegates, "GetConstructEffectiveDpsBase", ref _getConstructEffectiveDps);

            /// block methods
            AssignMethod(delegates, "GetWeaponTargetBase", ref _getWeaponTarget);
            AssignMethod(delegates, "GetMaxWeaponRangeBase", ref _getMaxWeaponRange);
            AssignMethod(delegates, "GetHeatLevelBase", ref _getHeatLevel);
            AssignMethod(delegates, "GetCurrentPowerBase", ref _currentPowerConsumption);
            AssignMethod(delegates, "HasCoreWeaponBase", ref _hasCoreWeapon);
            AssignMethod(delegates, "GetActiveAmmoBase", ref _getActiveAmmo);
            AssignMethod(delegates, "GetPlayerControllerBase", ref _getPlayerController);
        }

        void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if(delegates == null)
            {
                field = null;
                return;
            }

            Delegate del;
            if(!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");

            field = del as T;

            if(field == null)
                throw new Exception($"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }
    }
}