using System;
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
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.VanillaData
{
    public class VanillaDefinitions : ModComponent
    {
        /// <summary>
        /// NOTE: Null until it's been finished processing.
        /// </summary>
        public HashSet<MyDefinitionId> Definitions { get; private set; } = null;

        const string FileName = @"Data\VanillaDefinitions.txt";

        int WarnAtTick = 0;

        Task Task;

        public VanillaDefinitions(BuildInfoMod main) : base(main)
        {
            if(Constants.ForceExportVanillaDefinitions)
            {
                ExtractVanillaBlocks();
            }

            Log.Info($"Loading {FileName} in a thread...");
            Task = MyAPIGateway.Parallel.StartBackground(ParallelProcess, FinishedTask);
        }

        public override void RegisterComponent()
        {
            if(!Task.IsComplete)
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();
                Task.WaitOrExecute(blocking: true);
                timer.Stop();

                Log.Info($"Parallel task for {FileName} did not finish fast enough! Main thread was paused for {timer.Elapsed.TotalMilliseconds:0.######} ms");
            }

            if(BuildInfoMod.IsDevMod)
            {
                FindNewBlocks();
            }
        }

        public override void UnregisterComponent()
        {
        }

        void ParallelProcess()
        {
            if(!MyAPIGateway.Utilities.FileExistsInModLocation(FileName, Main.Session.ModContext.ModItem))
                return;

            char[] separators = new char[] { '/' };
            HashSet<MyDefinitionId> set = new HashSet<MyDefinitionId>(512, MyDefinitionId.Comparer);

            using(TextReader reader = MyAPIGateway.Utilities.ReadFileInModLocation(FileName, Main.Session.ModContext.ModItem))
            {
                int lineNumber = 0;
                string line;
                while((line = reader.ReadLine()) != null)
                {
                    lineNumber++;

                    if(line.StartsWith("//"))
                        continue;

                    string[] data = line.Split(separators);

                    if(data.Length <= 1)
                        throw new Exception($"Error processing {FileName}: line#{lineNumber} has no separators.\nLikely corrupted, please re-download mod!");

                    MyObjectBuilderType type;
                    if(!MyObjectBuilderType.TryParse(data[0], out type))
                    {
                        throw new Exception($"Error processing {FileName}: line#{lineNumber} has inexistent block type: {data[0]}.\nLikely corrupted, please re-download mod!");
                    }

                    for(int i = 1; i < data.Length; i++)
                    {
                        set.Add(new MyDefinitionId(type, data[i]));
                    }
                }
            }

            Definitions = set;

            Log.Info($"Finished parsing {FileName}, got {set.Count} block IDs.");
        }

        void FinishedTask()
        {
            if(Log.TaskHasErrors(Task, nameof(VanillaDefinitions)))
                return;
        }

        void FindNewBlocks()
        {
            bool needsRegen = Definitions == null;

            if(!needsRegen)
            {
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

                    if(blockDef.MountPoints != null && blockDef.MountPoints.Length > 0)
                    {
                        foreach(MyCubeBlockDefinition.MountPoint mount in blockDef.MountPoints)
                        {
                            if(mount.ExclusionMask > 3 || mount.PropertiesMask > 3)
                            {
                                Log.Info($"[DEV] Vanilla block '{def.Id.ToString()}' has mountpoint with >3 masks: exclusionMask={mount.ExclusionMask}; propertiesMask={mount.PropertiesMask}!");
                            }
                        }
                    }
                }
            }

            if(needsRegen)
            {
                if(!Constants.ForceExportVanillaDefinitions)
                    ExtractVanillaBlocks();

                if(BuildInfoMod.IsDevMod)
                {
                    WarnAtTick = Constants.TicksPerSecond * 3;
                    SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
                }
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            if(WarnAtTick > 0 && tick >= WarnAtTick)
            {
                WarnAtTick = 0;
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                Log.Info($"WARNING: some undeclared vanilla blocks detected! exported updated list.", Log.PRINT_MESSAGE);
            }
        }

        void ExtractVanillaBlocks()
        {
            // requires key with IComparable
            SortedDictionary<string, List<string>> perType = new SortedDictionary<string, List<string>>();

            foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                if(blockDef == null)
                    continue;

                if(blockDef.Context == null)
                {
                    Log.Error($"Definition {def.Id} has null Context, how come?");
                    continue;
                }

                if(blockDef.Context.IsBaseGame)
                {
                    string key = def.Id.TypeId.ToString().Substring("MyObjectBuilder_".Length);

                    List<string> subtypes;
                    if(!perType.TryGetValue(key, out subtypes))
                    {
                        subtypes = new List<string>();
                        perType[key] = subtypes;
                    }

                    subtypes.Add(def.Id.SubtypeName);
                }
            }

            StringBuilder sb = new StringBuilder(1024 * 1024);

            sb.Append("// Vanilla definitions list generated from SE v").Append(MyAPIGateway.Session.Version.ToString()).NewLine();

            foreach(KeyValuePair<string, List<string>> kv in perType)
            {
                sb.Append(kv.Key);

                foreach(string subtype in kv.Value)
                {
                    sb.Append('/').Append(subtype);
                }

                sb.AppendLine();
            }

            string fileNoDir = Path.GetFileName(FileName);

            using(TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(fileNoDir, typeof(VanillaDefinitions)))
            {
                writer.Write(sb.ToString());
            }

            Log.Info($"[DEV] Exported vanilla blocks to Storage/{FileName}", Log.PRINT_MESSAGE, 10000);
        }
    }
}
