using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Timers;
using Digi.BuildInfo.Extensions;
using ProtoBuf;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.ComponentSystem;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class GameDataCheck : MySessionComponentBase
    {
        const string NAME = "GameChecks";
        const string LAST_CHECKED_VERSION = "v1.187.87";

        string readFile;
        string writeFile;

        public override void BeforeStart()
        {
            // ensuring this component doesn't run unless it's local and offline
            if(MyAPIGateway.Session.OnlineMode != MyOnlineModeEnum.OFFLINE || Log.WorkshopId != 0)
                return;

            readFile = $"{Log.ModName} VanillaDetailInfo {LAST_CHECKED_VERSION}.xml";
            writeFile = $"{Log.ModName} VanillaDetailInfo v{MyAPIGateway.Session.Version}.xml";

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
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        #region Vanilla detail info check
        Dictionary<MyDefinitionId, string> vanillaDetailInfo;
        List<IMyTerminalBlock> spawnedBlocks;
        Vector3D spawnPosition = new Vector3D(-1000, -1000, 0);

        [ProtoContract]
        public class VanillaDetailInfo
        {
            [ProtoMember]
            public List<InfoEntry> Blocks;
        }

        [ProtoContract]
        public struct InfoEntry
        {
            [ProtoMember]
            public string Id;

            [ProtoMember]
            public string DetailInfo;
        }

        void CompareVanillaDetailInfo()
        {
            spawnedBlocks = new List<IMyTerminalBlock>();

            Log.Info($"{NAME}: Reading 'Storage/{readFile}'...");

            if(MyAPIGateway.Utilities.FileExistsInGlobalStorage(readFile))
            {
                using(var reader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(readFile))
                {
                    var xml = reader.ReadToEnd();
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
            }
            else
            {
                Log.Info($"{NAME}: File not found!");
                MyAPIGateway.Utilities.ShowMessage(NAME, $"{Log.ModName} WARNING: missing 'Storage/{readFile}' file, nothing to compare to!");
            }

            var definitions = MyDefinitionManager.Static.GetAllDefinitions();
            var spawned = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer);

            Log.Info($"{NAME}: Spawning blocks...");

            foreach(var def in definitions)
            {
                if(!def.Context.IsBaseGame) // only vanilla definitions
                    continue;

                var blockDef = def as MyCubeBlockDefinition;

                if(blockDef == null)
                    continue;

                if(!spawned.Add(def.Id.TypeId)) // if Add() returns false it means the entry already exists
                    continue;

                var block = SpawnTerminalBlock(blockDef);

                if(block == null)
                    continue;

                spawnedBlocks.Add(block);
            }

            Log.Info($"{NAME}: Starting timer and waiting...");

            var timer = new Timer(2000);
            timer.Elapsed += Timer_Elapsed;
            timer.Start();
        }

        private IMyTerminalBlock SpawnTerminalBlock(MyCubeBlockDefinition def)
        {
            IMyCubeGrid grid = null;

            try
            {
                var worldMatrix = MatrixD.Identity;
                worldMatrix.Translation = spawnPosition;

                spawnPosition.Z += 100;

                //var gridObj = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_CubeGrid>();

                var offset = Vector3.TransformNormal(MyCubeBlock.GetBlockGridOffset(def), worldMatrix);

                var gridObj = new MyObjectBuilder_CubeGrid()
                {
                    EntityId = 0,
                    CreatePhysics = true,
                    GridSizeEnum = def.CubeSize,
                    PositionAndOrientation = new MyPositionAndOrientation(worldMatrix.Translation - offset, worldMatrix.Forward, worldMatrix.Up),
                    PersistentFlags = (MyPersistentEntityFlags2.Enabled | MyPersistentEntityFlags2.InScene),
                    IsStatic = true,
                    Editable = false,
                    DestructibleBlocks = false,
                    IsRespawnGrid = false,
                    Name = "BuildInfo_TemporaryGrid",
                    CubeBlocks = new List<MyObjectBuilder_CubeBlock>(1),
                };

                var blockObj = (MyObjectBuilder_CubeBlock)MyObjectBuilderSerializer.CreateNewObject(def.Id);
                blockObj.EntityId = 0;
                blockObj.Orientation = Quaternion.CreateFromForwardUp(Vector3I.Forward, Vector3I.Up);
                blockObj.Min = (def.Size / 2) - def.Size + Vector3I.One;
                //blockObj.ColorMaskHSV = color;
                //blockObj.BuiltBy = builtBy;
                //blockObj.Owner = builtBy;

                // HACK these types do not check if their fields are null in their Remap() method.
                var timer = blockObj as MyObjectBuilder_TimerBlock;
                if(timer != null)
                    timer.Toolbar = new MyObjectBuilder_Toolbar();

                var button = blockObj as MyObjectBuilder_ButtonPanel;
                if(button != null)
                    button.Toolbar = new MyObjectBuilder_Toolbar();

                var sensor = blockObj as MyObjectBuilder_SensorBlock;
                if(sensor != null)
                    sensor.Toolbar = new MyObjectBuilder_Toolbar();

                if(def.EntityComponents != null)
                {
                    if(blockObj.ComponentContainer == null)
                        blockObj.ComponentContainer = new MyObjectBuilder_ComponentContainer();

                    foreach(var kv in def.EntityComponents)
                    {
                        blockObj.ComponentContainer.Components.Add(new MyObjectBuilder_ComponentContainer.ComponentData
                        {
                            TypeId = kv.Key.ToString(),
                            Component = kv.Value
                        });
                    }
                }

                gridObj.CubeBlocks.Add(blockObj);

                MyAPIGateway.Entities.RemapObjectBuilder(gridObj);

                var ent = (MyEntity)MyAPIGateway.Entities.CreateFromObjectBuilder(gridObj);

                if(ent == null)
                {
                    Log.Error($"Can't get spawned entity for block: {def.Id}");
                    return null;
                }

                ent.IsPreview = true;
                ent.Save = false;
                MyAPIGateway.Entities.AddEntity(ent, true);

                grid = (IMyCubeGrid)ent;
                var block = grid.GetCubeBlock(Vector3I.Zero)?.FatBlock as IMyCubeBlock;

                if(block == null)
                {
                    RemoveConnectedGrids(grid);
                    Log.Error($"Can't get block from spawned entity for block: {def.Id} (mod workshopId={def.Context.GetWorkshopID()})");
                    return null;
                }

                var terminalBlock = block as IMyTerminalBlock;

                if(terminalBlock == null)
                {
                    RemoveConnectedGrids(grid);
                    return null;
                }

                block.OnBuildSuccess(0, true);

                return terminalBlock;
            }
            catch(Exception e)
            {
                if(grid != null)
                    RemoveConnectedGrids(grid);

                Log.Error(e);
            }

            return null;
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

                using(var writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(writeFile))
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
