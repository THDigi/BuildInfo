using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Timers;
using Digi.BuildInfo.Utilities;
using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class VanillaDataCompare : MySessionComponentBase
    {
        const string NAME = "GameChecks";
        const string readFile = "Vanilla Detail Info LastCheck.xml";
        const string writeFile = "Vanilla Detail Info LastCheck.xml";
        const string backupFile = "Vanilla Detail Info Backup.xml";

        public override void BeforeStart()
        {
            if(!BuildInfoMod.IsDevMod)
                return;

            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
        }

        private void MessageEntered(string text, ref bool sendToOthers)
        {
            try
            {
                if(text.StartsWith("/runchecks", StringComparison.InvariantCultureIgnoreCase))
                {
                    sendToOthers = false;
                    MyAPIGateway.Utilities.ShowMessage("GameChecks", $"Running for {BuildInfoMod.ModName}...");

                    CompareVanillaDetailInfo();
                }

                //if(text.StartsWith("/spawnheld"))
                //{
                //    sendToOthers = false;

                //    var def = BuildInfoMod.Client.EquipmentMonitor.BlockDef;

                //    var matrix = MyAPIGateway.Session.Camera.WorldMatrix;
                //    matrix.Translation += matrix.Forward * 2;

                //    //MyCubeBuilder.SpawnStaticGrid(def, null, matrix, new Vector3(0, -1, 0));

                //    MyCubeBuilder.Static.AddBlocksToBuildQueueOrSpawn(def, ref matrix, Vector3I.Zero, Vector3I.One, Vector3I.Zero, Quaternion.Identity);

                //    //MyCubeBuilder.Static.Add();

                //    MyAPIGateway.Utilities.ShowNotification("SPAWNED!");
                //}
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        #region Vanilla detail info check
        Dictionary<MyDefinitionId, string> LastDetailedInfo;
        List<IMyTerminalBlock> SpawnedBlocks;
        List<MyCubeBlockDefinition> ResetStandalone;

        [ProtoContract]
        public class VanillaDetailInfo
        {
            [ProtoMember(2)]
            public string GameVersion;

            [ProtoMember(1)]
            public List<InfoEntry> Blocks;
        }

        [ProtoContract]
        public struct InfoEntry
        {
            [ProtoMember(1)]
            public string Id;

            [ProtoMember(2)]
            public string DetailInfo;
        }

        void CompareVanillaDetailInfo()
        {
            if(!ReadAndBackup())
                return;

            SpawnedBlocks = new List<IMyTerminalBlock>();

            SpawnVanillaBlocks();

            Log.Info($"[{NAME}] Starting timer and waiting...");

            Timer timer = new Timer(3000);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        bool ReadAndBackup()
        {
            Log.Info($"[{NAME}] Reading 'Storage/{readFile}'...");

            if(MyAPIGateway.Utilities.FileExistsInLocalStorage(readFile, typeof(VanillaDataCompare)))
            {
                string xml = null;

                using(TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(readFile, typeof(VanillaDataCompare)))
                {
                    xml = reader.ReadToEnd();
                    VanillaDetailInfo info = MyAPIGateway.Utilities.SerializeFromXML<VanillaDetailInfo>(xml);

                    if(info == null)
                    {
                        Log.Error($"[{NAME}] info = null");
                        return false;
                    }

                    if(info.Blocks == null)
                    {
                        Log.Error($"[{NAME}] info.Blocks = null");
                        return false;
                    }

                    LastDetailedInfo = new Dictionary<MyDefinitionId, string>(MyDefinitionId.Comparer);

                    foreach(InfoEntry block in info.Blocks)
                    {
                        LastDetailedInfo.Add(MyDefinitionId.Parse(block.Id), block.DetailInfo);
                    }
                }

                Log.Info($"[{NAME}] Backing up {readFile} as {backupFile}.");

                using(TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(backupFile, typeof(VanillaDataCompare)))
                {
                    writer.Write(xml);
                }
            }
            else
            {
                Log.Info($"[{NAME}] {readFile} not found.");
                MyAPIGateway.Utilities.ShowMessage(NAME, $"{BuildInfoMod.ModName} WARNING: missing 'Storage/{readFile}' file, nothing to compare to!");
            }

            return true;
        }

        void SpawnVanillaBlocks()
        {
            DictionaryValuesReader<MyDefinitionId, MyDefinitionBase> definitions = MyDefinitionManager.Static.GetAllDefinitions();
            HashSet<MyObjectBuilderType> spawned = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer);

            Log.Info($"[{NAME}] Spawning blocks...");

            MatrixD matrix = MatrixD.Identity;

            ResetStandalone = new List<MyCubeBlockDefinition>();

            foreach(MyDefinitionBase def in definitions)
            {
                try
                {
                    if(def?.Context == null)
                        continue;

                    if(!def.Context.IsBaseGame) // only vanilla definitions
                        continue;

                    MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                    if(blockDef == null)
                        continue;

                    if(!MyAPIGateway.Reflection.IsAssignableFrom(typeof(MyObjectBuilder_TerminalBlock), def.Id.TypeId))
                    {
                        Log.Info($"[{NAME}] skipping non-terminal block {def.Id}");
                        continue;
                    }

                    if(!blockDef.IsStandAlone)
                    {
                        ResetStandalone.Add(blockDef);
                        blockDef.IsStandAlone = true;

                        //Log.Info($"[{NAME}] WARNING: skipping {def.Id} because IsStandAlone=false");
                        //continue;
                    }

                    if(!spawned.Add(def.Id.TypeId)) // if Add() returns false it means the entry already exists
                        continue;

                    MyCubeBuilder.SpawnStaticGrid(blockDef, null, matrix, Vector3.One, MyStringHash.NullOrEmpty, completionCallback: GridSpawned);

                    matrix.Translation += new Vector3D(0, 0, 100);
                }
                catch(Exception e)
                {
                    Log.Error($"[{NAME}] Error in definition loop: {def?.Id}");
                    Log.Error(e);
                }
            }
        }

        void GridSpawned(MyEntity ent)
        {
            try
            {
                IMyCubeGrid grid = (IMyCubeGrid)ent;
                IMyCubeBlock block = grid.GetCubeBlock(Vector3I.Zero)?.FatBlock as IMyCubeBlock;

                if(block == null)
                {
                    RemoveConnectedGrids(grid);
                    Log.Error($"[{NAME}] Can't get block from spawned entity for some unknown block...");
                    //Log.Error($"Can't get block from spawned entity for block: {def.Id} (mod workshopId={def.Context.GetWorkshopID()})");
                    return;
                }

                IMyTerminalBlock terminalBlock = block as IMyTerminalBlock;
                if(terminalBlock == null)
                {
                    Log.Error($"[{NAME}] non-terminal block got spawned! {block.BlockDefinition} - it should've been filtered before, something is wrong.");
                    RemoveConnectedGrids(grid);
                    return;
                }

                block.OnBuildSuccess(0, true);

                SpawnedBlocks.Add(terminalBlock);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void RemoveConnectedGrids(IMyCubeGrid grid)
        {
            List<IMyCubeGrid> grids = null;
            try
            {
                grids = Caches.GetGrids(grid, GridLinkTypeEnum.Physical);

                foreach(IMyCubeGrid g in grids)
                {
                    g.Close();
                }
            }
            finally
            {
                grids?.Clear();
            }
        }

        void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(CompareDetailInfo);

            Timer timer = (Timer)sender;
            timer.Stop();
        }

        void CompareDetailInfo()
        {
            try
            {
                bool newDetected = false;
                VanillaDetailInfo data = new VanillaDetailInfo
                {
                    GameVersion = MyAPIGateway.Session.Version.ToString(),
                    Blocks = new List<InfoEntry>()
                };

                try
                {
                    Log.Info($"[{NAME}] Iterating blocks...");
                    Log.IncreaseIndent();

                    foreach(IMyTerminalBlock block in SpawnedBlocks)
                    {
                        block.SetDetailedInfoDirty();

                        string detailInfo = block.DetailedInfo;
                        bool hasInfo = !string.IsNullOrWhiteSpace(detailInfo);

                        if(hasInfo)
                        {
                            detailInfo = detailInfo.Replace("\r", "");

                            data.Blocks.Add(new InfoEntry()
                            {
                                Id = block.BlockDefinition.ToString(),
                                DetailInfo = detailInfo,
                            });
                        }

                        if(LastDetailedInfo != null)
                        {
                            string prevInfo;
                            if(LastDetailedInfo.TryGetValue(block.BlockDefinition, out prevInfo))
                            {
                                if(string.Compare(prevInfo, detailInfo, true) != 0)
                                {
                                    Log.Info($"{block.BlockDefinition} has different info now\nOld: {ToLiteral(prevInfo)}\nNew: {ToLiteral(detailInfo)}\n");
                                    newDetected = true;
                                }
                            }
                            else if(hasInfo)
                            {
                                Log.Info($"{block.BlockDefinition} did not have info but now it does!\nInfo: {ToLiteral(detailInfo)}\n");
                                newDetected = true;
                            }
                        }

                        RemoveConnectedGrids(block.CubeGrid);
                    }
                }
                finally
                {
                    Log.ResetIndent();
                }

                Log.Info($"[{NAME}] Writing 'Storage/{writeFile}'...");

                string xml = MyAPIGateway.Utilities.SerializeToXML(data);

                using(TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(writeFile, typeof(VanillaDataCompare)))
                {
                    writer.Write(xml);
                    writer.Flush();
                }

                Log.Info($"[{NAME}] Finished.");

                if(LastDetailedInfo == null)
                    MyAPIGateway.Utilities.ShowMessage(NAME, $"Checks finished for {BuildInfoMod.ModName}, nothing to compare to!!");
                else if(newDetected)
                    MyAPIGateway.Utilities.ShowMessage(NAME, $"Checks finished for {BuildInfoMod.ModName}, NEW STUFF DETECTED!!!!!!");
                else
                    MyAPIGateway.Utilities.ShowMessage(NAME, $"Checks finished for {BuildInfoMod.ModName}, nothing new detected.");
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                if(ResetStandalone != null)
                {
                    foreach(MyCubeBlockDefinition def in ResetStandalone)
                    {
                        def.IsStandAlone = false;
                    }
                }

                SpawnedBlocks = null;
                LastDetailedInfo = null;
                ResetStandalone = null;
            }
        }

        static string ToLiteral(string input)
        {
            StringBuilder literal = new StringBuilder(input.Length);

            foreach(char c in input)
            {
                switch(c)
                {
                    case '\0': literal.Append(@"\0"); break;
                    case '\a': literal.Append(@"\a"); break;
                    case '\b': literal.Append(@"\b"); break;
                    case '\f': literal.Append(@"\f"); break;
                    case '\n': literal.Append(@"\n"); break;
                    case '\r': literal.Append(@"\r"); break;
                    case '\t': literal.Append(@"\t"); break;
                    case '\v': literal.Append(@"\v"); break;
                    default:
                        if(char.GetUnicodeCategory(c) != UnicodeCategory.Control)
                            literal.Append(c);
                        else
                            literal.Append(@"\u").Append(((int)c).ToString("x4"));
                        break;
                }
            }

            return literal.ToString();
        }
        #endregion Vanilla detail info check
    }
}
