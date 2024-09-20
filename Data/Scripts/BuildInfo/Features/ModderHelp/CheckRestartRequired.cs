using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ModderHelp
{
    public class CheckRestartRequired : ModComponent
    {
        // NOTE: don't change types on existing protomembers, remove them and add new numbers instead!

        [ProtoContract(UseProtoMembersOnly = true)]
        class Storage
        {
            [ProtoMember(1)]
            public List<BlockData> BlockData = null;
        }

        [ProtoContract(UseProtoMembersOnly = true)]
        class BlockData
        {
            [ProtoMember(1)]
            public SerializableDefinitionId DefId;

            [ProtoMember(2)]
            public Vector3I? Center = null;

            /// <summary>
            /// AssetName key, version value
            /// </summary>
            [ProtoMember(10)]
            public Dictionary<string, int> Models = null;

            public BlockData()
            {
            }
        }

        const string DataFile = "CheckRestartData.bin";
        const string RestartCheckFile = "CheckRestart.txt";
        const string LogPrefix = "[CheckRestartRequired]: ";
        const string Signature = BuildInfoMod.ModName + "/CheckRestart";
        const string GenericMessage = "Recommended to restart game! A mod updated a model or value which is cached which can cause issues.";

        bool InformedToRestart = false;
        bool InformedIsLocalMod = false;

        readonly Dictionary<MyDefinitionId, BlockData> PerDefId = new Dictionary<MyDefinitionId, BlockData>(MyDefinitionId.Comparer);

        readonly Type TypeForAssembly = typeof(CheckRestartRequired);

        public CheckRestartRequired(BuildInfoMod main) : base(main)
        {
            string currentLogPath = MyLog.Default.GetFilePath();
            string lastLogPath = GetLastLogPath();
            bool isFreshSession = lastLogPath != currentLogPath;

            if(isFreshSession)
            {
                Log.Info($"{LogPrefix}New game session detected, deleting stored data.");
                DeleteStoredData();
            }
            else
            {
                LoadStoredData();
            }
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            SaveLastLogPath();
            SaveStoredData();
        }

        void LoadStoredData()
        {
            Storage loaded = null;
            int size = 0;

            if(MyAPIGateway.Utilities.FileExistsInLocalStorage(DataFile, TypeForAssembly))
            {
                using(BinaryReader reader = MyAPIGateway.Utilities.ReadBinaryFileInLocalStorage(DataFile, TypeForAssembly))
                {
                    size = (int)reader.BaseStream.Length;
                    if(size > 0)
                    {
                        byte[] allBytes = reader.ReadBytes(size);
                        loaded = MyAPIGateway.Utilities.SerializeFromBinary<Storage>(allBytes);
                    }
                }
            }

            if(loaded != null)
            {
                Log.Info($"{LogPrefix}Succesfully loaded '{DataFile}' ({size / 1024}kb). Same game session detected, loading data.");

                foreach(BlockData data in loaded.BlockData)
                {
                    PerDefId[data.DefId] = data;
                }
            }
        }

        void DeleteStoredData()
        {
            if(MyAPIGateway.Utilities.FileExistsInLocalStorage(DataFile, TypeForAssembly))
                MyAPIGateway.Utilities.DeleteFileInLocalStorage(DataFile, TypeForAssembly);
        }

        void SaveStoredData()
        {
            var obj = new Storage();
            obj.BlockData = PerDefId.Values.ToList();

            byte[] bytes = MyAPIGateway.Utilities.SerializeToBinary(obj);

            using(BinaryWriter writer = MyAPIGateway.Utilities.WriteBinaryFileInLocalStorage(DataFile, TypeForAssembly))
            {
                writer.Write(bytes);
            }

            Log.Info($"{LogPrefix}Saved data to '{DataFile}' ({bytes.Length / 1024}kb).");
        }

        string GetLastLogPath()
        {
            string lastLogPath = null;

            if(MyAPIGateway.Utilities.FileExistsInLocalStorage(RestartCheckFile, TypeForAssembly))
            {
                using(TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(RestartCheckFile, TypeForAssembly))
                {
                    lastLogPath = reader.ReadToEnd();
                }
            }

            return lastLogPath;
        }

        void SaveLastLogPath()
        {
            using(TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(RestartCheckFile, TypeForAssembly))
            {
                writer.Write(MyLog.Default.GetFilePath());
            }

            Log.Info($"{LogPrefix}Saved restart detection info to '{RestartCheckFile}'.");
        }

        internal void CheckBlock(IMyCubeBlock block, MyCubeBlockDefinition def, bool hasConveyors)
        {
            if(InformedToRestart)
                return;

            if(def.Context == null || def.Context.IsBaseGame)
                return; // only modded content can normally change between world reloads.

            string assetName = block.Model?.AssetName;

            if(block.Model == null)
            {
                Log.Error($"{LogPrefix}{def.Id} has null model or asset name!");
                return;
            }

            int assetVersion = block.Model.DataVersion;

            BlockData data;
            if(!PerDefId.TryGetValue(def.Id, out data))
            {
                data = new BlockData();
                data.DefId = def.Id;
                data.Models = new Dictionary<string, int>();

                if(hasConveyors)
                    data.Center = def.Center;

                data.Models[assetName] = assetVersion;

                PerDefId[def.Id] = data;
                return;
            }

            if(hasConveyors && data.Center != null && data.Center != def.Center)
            {
                // don't update data

                Alert(def.Context.IsLocal(), $"{def.Id} has Center tag changed which causes problems for its conveyor functionality, because ports were calculated and cached relative to the old Center value, and this cache does not get replaced on world reload.");
            }

            int prevVersion;
            if(data.Models.TryGetValue(assetName, out prevVersion))
            {
                if(assetVersion != prevVersion)
                {
                    // don't update data

                    Alert(def.Context.IsLocal(), $"{def.Id} had one of its model files changed, which might be a problem if those changes were to the collider or empties because those don't get loaded again, requiring a full game restart to have the changes.");
                }
            }
            else
            {
                // different path entirely, store this too (can be build stage for example)
                data.Models[assetName] = assetVersion;
            }
        }

        void Alert(bool localMod, string reasonMessage)
        {
            InformedToRestart = true;
            InformedIsLocalMod = localMod;

            if(localMod)
                reasonMessage = "\n" + reasonMessage;
            else
                reasonMessage = string.Empty;

            MyDefinitionErrors.Add(null, $"{Signature}: {GenericMessage}{reasonMessage}", TErrorSeverity.Warning, writeToLog: false);
            MyLog.Default.WriteLine($"{Signature}: {GenericMessage}{reasonMessage}");
            Log.Info($"{LogPrefix}{GenericMessage}{reasonMessage}");

            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void UpdateAfterSim(int tick)
        {
            if(MyAPIGateway.Session?.Player?.Character != null)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

                if(!InformedIsLocalMod)
                    Utils.ShowColoredChatMessage(Signature, GenericMessage, FontsHandler.RedSh);
                if(Main.ModderHelpMain.IsF11MenuAccessible)
                    Utils.ShowColoredChatMessage(Signature, GenericMessage + " Details in F11 menu or SE log.", FontsHandler.RedSh);
                else
                    Utils.ShowColoredChatMessage(Signature, GenericMessage + " Details in SE log.", FontsHandler.RedSh);
            }
        }
    }
}