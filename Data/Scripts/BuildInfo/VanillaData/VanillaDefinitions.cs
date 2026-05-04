using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using ParallelTasks;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Library.Utils;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.VanillaData
{
    public class VanillaDefinitions : ModComponent
    {
        /// <summary>
        /// NOTE: null until it's been finished processing.
        /// </summary>
        public HashSet<MyDefinitionId> Definitions { get; private set; } = null;

        Task Task;
        int WarnAtTick = 0;

        const string CacheFilePath = @"Data\VanillaDefinitions.txt";

        public VanillaDefinitions(BuildInfoMod main) : base(main)
        {
            Log.Info($"Loading {CacheFilePath} in a thread...");
            Task = MyAPIGateway.Parallel.StartBackground(ParallelProcess, FinishedTask);
        }

        public override void RegisterComponent()
        {
            if(!Task.IsComplete)
            {
                Log.Info($"Parallel task for {CacheFilePath} did not finish fast enough, forcing it to main thread...");

                Stopwatch timer = new Stopwatch();
                timer.Start();
                Task.WaitOrExecute(blocking: true);
                timer.Stop();

                Log.Info($"... done, main thread was paused for {timer.Elapsed.TotalMilliseconds:0.######} ms");
            }
        }

        public override void UnregisterComponent()
        {
        }

        void ParallelProcess()
        {
            if(!MyAPIGateway.Utilities.FileExistsInModLocation(CacheFilePath, Main.Session.ModContext.ModItem))
            {
                Log.Error($"The {CacheFilePath} was not found in current mod's folder!");
                return;
            }

            HashSet<MyDefinitionId> idSet = null;
            int entries = 2048;

            #region Read file char-by-char
            using(TextReader reader = MyAPIGateway.Utilities.ReadFileInModLocation(CacheFilePath, Main.Session.ModContext.ModItem))
            {
                StringBuilder buffer = new StringBuilder(128);
                MyObjectBuilderType currentBlockType = MyObjectBuilderType.Invalid;
                int lineNumber = 1;

                while(true)
                {
                    int read = reader.Read();

                    if(read == -1)
                    {
                        if(!currentBlockType.IsNull)
                        {
                            if(idSet == null)
                                idSet = new HashSet<MyDefinitionId>(entries, MyDefinitionId.Comparer);

                            string str = buffer.ToString();
                            idSet.Add(new MyDefinitionId(currentBlockType, str == "(null)" ? null : str));
                        }

                        break;
                    }

                    if(read == '\r')
                        continue;

                    if(lineNumber <= 2)
                    {
                        if(lineNumber == 1)
                        {
                            while(reader.Read() != '\n') ;
                        }

                        if(lineNumber == 2)
                        {
                            while(reader.Read() != ':') ;

                            while(true)
                            {
                                read = reader.Read();
                                if(read == -1 | read == '\n')
                                    break;

                                buffer.Append((char)read);
                            }

                            entries = int.Parse(buffer.ToString());
                            buffer.Clear();
                        }

                        lineNumber++;
                        continue;
                    }

                    if(read == '/' | read == '\n')
                    {
                        string str = buffer.ToString();

                        if(currentBlockType.IsNull)
                        {
                            if(str.Length > 0)
                            {
                                currentBlockType = MyObjectBuilderType.ParseBackwardsCompatible(str);
                                if(currentBlockType.IsNull)
                                {
                                    Log.Error($"[DEV][{GetType().Name}] couldn't find typeId: {str}");
                                    break;
                                }
                            }
                        }
                        else
                        {
                            if(idSet == null)
                                idSet = new HashSet<MyDefinitionId>(entries, MyDefinitionId.Comparer);

                            idSet.Add(new MyDefinitionId(currentBlockType, str == "(null)" ? null : str));
                        }

                        buffer.Clear();

                        if(read == '\n')
                        {
                            currentBlockType = MyObjectBuilderType.Invalid;
                            lineNumber++;
                        }

                        continue;
                    }

                    buffer.Append((char)read);
                }
            }
            #endregion

            if(idSet != null)
            {
                Definitions = idSet;

                if(idSet.Count == entries)
                    Log.Info($"[{GetType().Name}] Finished parsing {CacheFilePath}, got {idSet.Count} block IDs.");
                else
                    Log.Error($"[{GetType().Name}] Finished parsing {CacheFilePath}, got {idSet.Count} block IDs - WARNING: expected {entries} instead!");
            }
            else
            {
                Log.Error($"[{GetType().Name}] Got nothing from parsing {CacheFilePath}.");
            }
        }

        void FinishedTask()
        {
            bool hadErrors = Log.ReportTaskErrors(Task, nameof(VanillaDefinitions));

            if(BuildInfoMod.IsDevMod)
            {
                MyAPIGateway.Parallel.StartBackground(FindNewBlocks);
            }
        }

        void FindNewBlocks()
        {
            Log.Info($"[DEV][{GetType().Name}] Checking for new vanilla blocks...");

            HashSet<MyDefinitionId> vanillaIDs = null;
            bool needsRegen = (Definitions == null);

            if(!needsRegen)
            {
                vanillaIDs = new HashSet<MyDefinitionId>(2048, MyDefinitionId.Comparer);

                #region read game sbc files
                string gameContentPath = MyAPIGateway.Utilities.GamePaths.ContentPath;
                string[] blockFiles = PathUtils.GetFilesRecursively(Path.Combine(gameContentPath, @"Data\CubeBlocks"), "*.sbc");

                foreach(string filePath in blockFiles)
                {
                    string relativePath = filePath.Substring(gameContentPath.Length + 1);

                    MyObjectBuilder_Definitions sbcOB;
                    if(!MyObjectBuilderSerializer.DeserializeXML(filePath, out sbcOB))
                    {
                        Log.Error($"[DEV][{GetType().Name}] Error deserializing '{relativePath}', SE log should have more details");
                        continue;
                    }

                    if(sbcOB.CubeBlocks != null)
                    {
                        foreach(MyObjectBuilder_CubeBlockDefinition blockOB in sbcOB.CubeBlocks)
                        {
                            vanillaIDs.Add(blockOB.Id);
                        }
                    }

                    if(sbcOB.Definitions != null)
                    {
                        foreach(MyObjectBuilder_DefinitionBase defOB in sbcOB.Definitions)
                        {
                            if(defOB is MyObjectBuilder_CubeBlockDefinition)
                            {
                                Log.Info($"[DEV][{GetType().Name}] WARNING: vanilla block defined without <CubeBlocks> in {relativePath}");
                                vanillaIDs.Add(defOB.Id);
                            }
                        }
                    }
                }
                #endregion

#if false // old way by reading definitions, which relied on being without mods that might change vanilla blocks.
                foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                    if(blockDef == null)
                        continue;

                    if(!blockDef.Context.IsBaseGame)
                        continue;

                    if(!Definitions.Contains(blockDef.Id))
                    {
                        needsRegen = true;
                        if(blockDef.DLCs != null && blockDef.DLCs.Length > 0)
                            Log.Info($"[DEV] New vanilla block: {blockDef.Id} - DLC: {string.Join(", ", blockDef.DLCs.Select(dlc => MyTexts.GetString(MyAPIGateway.DLC.GetDLC(dlc).DisplayName)))}");
                        else
                            Log.Info($"[DEV] New vanilla block: {blockDef.Id}");
                        //break;
                    }
                }
#endif

                foreach(MyDefinitionId id in vanillaIDs)
                {
                    if(!Definitions.Contains(id))
                    {
                        needsRegen = true;

                        MyCubeBlockDefinition blockDef = MyDefinitionManager.Static.GetCubeBlockDefinition(id);
                        if(blockDef.DLCs != null && blockDef.DLCs.Length > 0)
                            Log.Info($"[DEV][{GetType().Name}] New vanilla block: {id.ToShortString()} - DLC: {string.Join(", ", blockDef.DLCs.Select(dlc => MyTexts.GetString(MyAPIGateway.DLC.GetDLC(dlc).DisplayName)))}");
                        else
                            Log.Info($"[DEV][{GetType().Name}] New vanilla block: {id.ToShortString()}");

                        //break;
                    }
                }
            }

            if(needsRegen)
            {
                ExtractVanillaBlocks(vanillaIDs);

                WarnAtTick = MyAPIGateway.Session.GameplayFrameCounter + Constants.TicksPerSecond * 3;
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            if(WarnAtTick > 0 && tick >= WarnAtTick)
            {
                WarnAtTick = 0;
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

                Log.Info($"[DEV][{GetType().Name}] WARNING: some undeclared vanilla blocks detected! exported updated list.", Log.PRINT_MESSAGE);
            }
        }

        void ExtractVanillaBlocks(HashSet<MyDefinitionId> vanillaIDs)
        {
            // requires key with IComparable
            SortedDictionary<string, List<string>> perType = new SortedDictionary<string, List<string>>();

            int characters = 0;

            foreach(var id in vanillaIDs)
            {
                string key = id.TypeId.ToString().Substring("MyObjectBuilder_".Length);

                List<string> subtypes;
                if(!perType.TryGetValue(key, out subtypes))
                {
                    subtypes = new List<string>();
                    perType[key] = subtypes;

                    characters += key.Length + 2; // +2 for \r\n
                }

                string subtype = id.SubtypeName;

                // just like in MyDefinitionId's ToString()
                if(string.IsNullOrEmpty(subtype))
                    subtype = "(null)";

                characters += subtype.Length + 1; // +1 for the slash

                subtypes.Add(subtype);
            }

            int headerSize = "Version: v0.000\r\n".Length + "Total: 9999\r\n".Length;
            int initialCapacity = headerSize + characters + 2;
            StringBuilder sb = new StringBuilder(MathHelper.GetNearestBiggerPowerOfTwo(initialCapacity));

            var v = MyAPIGateway.Session.Version;
            sb.AppendLine($"Version: v{v.Major}.{v.Minor}");
            sb.AppendLine($"Total: {vanillaIDs.Count}");

            foreach(KeyValuePair<string, List<string>> kv in perType)
            {
                sb.AppendLine();
                sb.Append(kv.Key);

                foreach(string subtype in kv.Value)
                {
                    sb.Append('/').Append(subtype);
                }
            }

            //Log.Info($"[DEBUG] initialCapacity={initialCapacity}; len={sb.Length}; cap={sb.Capacity}");

            string fileNoDir = Path.GetFileName(CacheFilePath);

            using(TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(fileNoDir, typeof(VanillaDefinitions)))
            {
                writer.Write(sb.ToString());
            }

            Log.Info($"[DEV][{GetType().Name}] Exported vanilla blocks to local storage as {CacheFilePath}", Log.PRINT_MESSAGE, 10000);
        }
    }
}
