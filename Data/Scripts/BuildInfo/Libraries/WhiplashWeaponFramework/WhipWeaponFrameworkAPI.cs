using System;
using System.Collections.Concurrent;
using Digi;
using Digi.BuildInfo;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;

namespace Whiplash.WeaponFramework
{
    /// <summary>
    /// Extracted from https://gitlab.com/whiplash141/Revived-Railgun-Mod/-/blob/develop/Data/Scripts/WeaponFramework/WhipsWeaponFramework/FrameworkWeaponAPI.cs
    /// </summary>
    public class WhipWeaponFrameworkAPI : ModComponent
    {
        public bool IsRunning { get; private set; } = false;

        public readonly ConcurrentDictionary<MyDefinitionId, WeaponConfig> Weapons = new ConcurrentDictionary<MyDefinitionId, WeaponConfig>(MyDefinitionId.Comparer);

        const long FIXED_GUN_REGESTRATION_NETID = 1411;
        const long TURRET_REGESTRATION_NETID = 1412;

        const ushort FIXED_GUN_CONFIG_SYNC_NETID = 50211;
        const ushort TURRET_CONFIG_SYNC_NETID = 58847;
        const ushort CLIENT_CONFIG_SYNC_REQUEST_NETID = 19402;
        const ushort SERVER_CONFIG_SYNC_REQUEST_FINISHED_NETID = 62508;

        public WhipWeaponFrameworkAPI(BuildInfoMod main) : base(main)
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(FIXED_GUN_REGESTRATION_NETID, HandleFixedGunRegistration);
            MyAPIGateway.Utilities.RegisterMessageHandler(TURRET_REGESTRATION_NETID, HandleTurretRegistration);

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(FIXED_GUN_CONFIG_SYNC_NETID, HandleFixedGunConfigSyncMessage);
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(TURRET_CONFIG_SYNC_NETID, HandleTurretConfigSyncMessage);
            //MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(SERVER_CONFIG_SYNC_REQUEST_FINISHED_NETID, OnClientConfigSyncFinished);

            if(Main.IsServer)
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(FIXED_GUN_REGESTRATION_NETID, HandleFixedGunRegistration);
            MyAPIGateway.Utilities.UnregisterMessageHandler(TURRET_REGESTRATION_NETID, HandleTurretRegistration);

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(FIXED_GUN_CONFIG_SYNC_NETID, HandleFixedGunConfigSyncMessage);
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(TURRET_CONFIG_SYNC_NETID, HandleTurretConfigSyncMessage);
            //MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(SERVER_CONFIG_SYNC_REQUEST_FINISHED_NETID, OnClientConfigSyncFinished);
        }

        void HandleFixedGunRegistration(object o)
        {
            try
            {
                byte[] binaryMsg = o as byte[];
                if(binaryMsg == null)
                    return;

                WeaponConfig config = MyAPIGateway.Utilities.SerializeFromBinary<WeaponConfig>(binaryMsg);
                UpdateWeapon(config, isFixed: true);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void HandleTurretRegistration(object o)
        {
            try
            {
                byte[] binaryMsg = o as byte[];
                if(binaryMsg == null)
                    return;

                TurretWeaponConfig config = MyAPIGateway.Utilities.SerializeFromBinary<TurretWeaponConfig>(binaryMsg);
                UpdateWeapon(config, isFixed: false);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            // clients will automatically ask and this mod will receive, but server does not so I must ask it myself.
            if(tick >= 100)
            {
                //if(BuildInfoMod.IsDevMod)
                //    Log.Info($"{nameof(WhipWeaponFrameworkAPI)}: asking server for weapon details...");

                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                SendClientConfigSyncRequest();
            }
        }

        void SendClientConfigSyncRequest()
        {
            ulong clientId = MyAPIGateway.Multiplayer.MyId;
            byte[] bytes = MyAPIGateway.Utilities.SerializeToBinary(clientId);
            MyAPIGateway.Multiplayer.SendMessageToServer(CLIENT_CONFIG_SYNC_REQUEST_NETID, bytes);
        }

        void HandleFixedGunConfigSyncMessage(ushort channelId, byte[] data, ulong senderSteamId, bool isSenderServer)
        {
            try
            {
                if(data == null)
                    return;

                WeaponConfig config = MyAPIGateway.Utilities.SerializeFromBinary<WeaponConfig>(data);
                UpdateWeapon(config, isFixed: true);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void HandleTurretConfigSyncMessage(ushort channelId, byte[] data, ulong senderSteamId, bool isSenderServer)
        {
            try
            {
                if(data == null)
                    return;

                TurretWeaponConfig config = MyAPIGateway.Utilities.SerializeFromBinary<TurretWeaponConfig>(data);
                UpdateWeapon(config, isFixed: false);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        //void OnClientConfigSyncFinished(ushort channelId, byte[] data, ulong senderSteamId, bool isSenderServer)
        //{
        //}

        void UpdateWeapon(WeaponConfig config, bool isFixed)
        {
            if(string.IsNullOrWhiteSpace(config.BlockSubtype) || string.IsNullOrWhiteSpace(config.ConfigID))
                return;

            MyCubeBlockDefinition foundDef = null;

            foreach(MyCubeBlockDefinition def in Main.Caches.BlockDefs)
            {
                if(def is MyWeaponBlockDefinition && def.Id.SubtypeName == config.BlockSubtype)
                {
                    foundDef = def;
                    break;
                }
            }

            if(foundDef == null)
            {
                Log.Info($"{nameof(WhipWeaponFrameworkAPI)} WARNING: Couldn't find any weapon block definition with subtype: {config.BlockSubtype}");
                return;
            }

            Weapons[foundDef.Id] = config;

            //if(BuildInfoMod.IsDevMod)
            //    Log.Info($"{nameof(WhipWeaponFrameworkAPI)}: Registered {(isFixed ? "fixed" : "turret")} weapon - blockDefId={foundDef.Id.ToString()}");
        }
    }
}