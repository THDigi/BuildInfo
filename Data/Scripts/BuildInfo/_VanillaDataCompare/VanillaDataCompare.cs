using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Timers;
using ProtoBuf;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
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
        const string LAST_CHECKED_VERSION = "v1.189.41";

        const string readFile = "Vanilla Detail Info LastCheck.xml";
        const string writeFile = "Vanilla Detail Info LastCheck.xml";
        const string backupFile = "Vanilla Detail Info Backup.xml";

        public override void BeforeStart()
        {
            // ensuring this component doesn't run unless it's local and offline
            if(MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE || Log.WorkshopId != 0)
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
                    MyAPIGateway.Utilities.ShowMessage("GameChecks", $"Running for {Log.ModName}...");

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
        Dictionary<MyDefinitionId, string> vanillaDetailInfo;
        List<IMyTerminalBlock> spawnedBlocks;

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
            spawnedBlocks = new List<IMyTerminalBlock>();

            Log.Info($"{NAME}: Reading 'Storage/{readFile}'...");

            if(MyAPIGateway.Utilities.FileExistsInLocalStorage(readFile, typeof(VanillaDataCompare)))
            {
                string xml = null;

                using(var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(readFile, typeof(VanillaDataCompare)))
                {
                    xml = reader.ReadToEnd();
                    var info = MyAPIGateway.Utilities.SerializeFromXML<VanillaDetailInfo>(xml);

                    if(info == null)
                    {
                        Log.Error($"{NAME}: info = null");
                        return;
                    }

                    if(info.Blocks == null)
                    {
                        Log.Error($"{NAME}: info.Blocks = null");
                        return;
                    }

                    vanillaDetailInfo = new Dictionary<MyDefinitionId, string>();

                    foreach(var block in info.Blocks)
                    {
                        vanillaDetailInfo.Add(MyDefinitionId.Parse(block.Id), block.DetailInfo);
                    }
                }

                Log.Info($"{NAME}: Backing up {readFile} as {backupFile}.");

                using(var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(backupFile, typeof(VanillaDataCompare)))
                {
                    writer.Write(xml);
                }
            }
            else
            {
                Log.Info($"{NAME}: {readFile} not found.");
                MyAPIGateway.Utilities.ShowMessage(NAME, $"{Log.ModName} WARNING: missing 'Storage/{readFile}' file, nothing to compare to!");
            }

            var definitions = MyDefinitionManager.Static.GetAllDefinitions();
            var spawned = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer);

            Log.Info($"{NAME}: Spawning blocks...");

            var matrix = MatrixD.Identity;

            foreach(var def in definitions)
            {
                if(!def.Context.IsBaseGame) // only vanilla definitions
                    continue;

                var blockDef = def as MyCubeBlockDefinition;

                if(blockDef == null)
                    continue;

                if(!spawned.Add(def.Id.TypeId)) // if Add() returns false it means the entry already exists
                    continue;

                MyCubeBuilder.SpawnStaticGrid(blockDef, null, matrix, Vector3.One, MyStringHash.NullOrEmpty, completionCallback: GridSpawned);

                matrix.Translation += new Vector3D(0, 0, 100);
            }

            Log.Info($"{NAME}: Starting timer and waiting...");

            var timer = new Timer(3000);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        private void GridSpawned(MyEntity ent)
        {
            var grid = (IMyCubeGrid)ent;
            var block = grid.GetCubeBlock(Vector3I.Zero)?.FatBlock as IMyCubeBlock;

            if(block == null)
            {
                RemoveConnectedGrids(grid);
                Log.Error($"Can't get block from spawned entity for some unknown block...");
                //Log.Error($"Can't get block from spawned entity for block: {def.Id} (mod workshopId={def.Context.GetWorkshopID()})");
                return;
            }

            var terminalBlock = block as IMyTerminalBlock;

            if(terminalBlock == null)
            {
                Log.Info($"Spawned block is not a terminal block: {block.BlockDefinition}");
                RemoveConnectedGrids(grid);
                return;
            }

            block.OnBuildSuccess(0, true);

            spawnedBlocks.Add(terminalBlock);
        }

        private void RemoveConnectedGrids(IMyCubeGrid grid)
        {
            var grids = MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical);

            foreach(var g in grids)
            {
                g.Close();
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            MyAPIGateway.Utilities.InvokeOnGameThread(CompareDetailInfo);

            var timer = (Timer)sender;
            timer.Stop();
        }

        private void CompareDetailInfo()
        {
            try
            {
                bool newDetected = false;
                var data = new VanillaDetailInfo
                {
                    GameVersion = MyAPIGateway.Session.Version.ToString(),
                    Blocks = new List<InfoEntry>()
                };

                try
                {
                    Log.Info($"{NAME}: Iterating blocks...");
                    Log.IncreaseIndent();

                    foreach(var block in spawnedBlocks)
                    {
                        var detailInfo = block.DetailedInfo;
                        var hasInfo = !string.IsNullOrWhiteSpace(detailInfo);

                        if(hasInfo)
                        {
                            detailInfo = detailInfo.Replace("\r", "");

                            data.Blocks.Add(new InfoEntry()
                            {
                                Id = block.BlockDefinition.ToString(),
                                DetailInfo = detailInfo,
                            });
                        }

                        if(vanillaDetailInfo != null)
                        {
                            string prevInfo;
                            if(vanillaDetailInfo.TryGetValue(block.BlockDefinition, out prevInfo))
                            {
                                if(string.Compare(prevInfo, detailInfo, true) != 0)
                                {
                                    Log.Info($"{NAME}: {block.BlockDefinition} has different info now\nOld: {ToLiteral(prevInfo)}\nNew: {ToLiteral(detailInfo)}\n");
                                    newDetected = true;
                                }
                            }
                            else if(hasInfo)
                            {
                                Log.Info($"{NAME}: {block.BlockDefinition} did not have info but now it does!\nInfo: {ToLiteral(detailInfo)}\n");
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

                Log.Info($"{NAME}: Writing 'Storage/{writeFile}'...");

                var xml = MyAPIGateway.Utilities.SerializeToXML(data);

                using(var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(writeFile, typeof(VanillaDataCompare)))
                {
                    writer.Write(xml);
                    writer.Flush();
                }

                Log.Info($"{NAME}: Finished.");

                if(vanillaDetailInfo == null)
                    MyAPIGateway.Utilities.ShowMessage(NAME, $"Checks finished for {Log.ModName}, nothing to compare to.");
                else if(newDetected)
                    MyAPIGateway.Utilities.ShowMessage(NAME, $"Checks finished for {Log.ModName}, NEW STUFF DETECTED!!!!!!");
                else
                    MyAPIGateway.Utilities.ShowMessage(NAME, $"Checks finished for {Log.ModName}, nothing new detected.");
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                spawnedBlocks = null;
                vanillaDetailInfo = null;
            }
        }

        private static string ToLiteral(string input)
        {
            var literal = new StringBuilder(input.Length);

            foreach(var c in input)
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
                        if(Char.GetUnicodeCategory(c) != UnicodeCategory.Control)
                            literal.Append(c);
                        else
                            literal.Append(@"\u").Append(((int)c).ToString("x4"));
                        break;
                }
            }

            return literal.ToString();
        }
        #endregion
    }
}
