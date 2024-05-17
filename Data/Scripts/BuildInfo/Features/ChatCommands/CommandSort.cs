using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandSort : Command
    {
        public CommandSort() : base("sort")
        {
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(PrimaryCommand).Append(" blocks <mass|hp|volume> [sg|lg] [desc]").NewLine();
            sb.Append(PrimaryCommand).Append(" comps <mass|hp|volume> [desc]").NewLine();
            sb.Append(PrimaryCommand).Append(" lcds [group]").NewLine();
            sb.Append("  Shows a sorted list of the specified things; can be exported to file.").NewLine();
        }

        struct LCDDefInfo
        {
            public readonly MyFunctionalBlockDefinition Definition;
            public readonly int ScreenIndex;
            public readonly Hardcoded.TextSurfaceInfo Info;

            public LCDDefInfo(MyFunctionalBlockDefinition definition, int screenIndex, Hardcoded.TextSurfaceInfo info)
            {
                Definition = definition;
                ScreenIndex = screenIndex;
                Info = info;
            }
        }

        public override void Execute(Arguments args)
        {
            string type = args.Get(0);
            string sort = args.Get(1);

            if(args.Count < 1 || type == null)
            {
                PrintChat("Sort what?", FontsHandler.RedSh);
                PrintHelpToChat();
                return;
            }

            if(type.Equals("lcds", ChatCommandHandler.StringCompare))
            {
                IEnumerable<MyFunctionalBlockDefinition> defs = Main.Caches.BlockDefs.OfType<MyFunctionalBlockDefinition>().Where(d => d.ScreenAreas != null && d.ScreenAreas.Count > 0);

                if(sort != null && sort.Equals("group", ChatCommandHandler.StringCompare))
                {
                    Dictionary<Vector2I, List<LCDDefInfo>> grouped = new Dictionary<Vector2I, List<LCDDefInfo>>();

                    foreach(MyFunctionalBlockDefinition fd in defs)
                    {
                        for(int i = 0; i < fd.ScreenAreas.Count; i++)
                        {
                            ScreenArea screen = fd.ScreenAreas[i];
                            Hardcoded.TextSurfaceInfo info = Hardcoded.TextSurface_GetInfo(screen.ScreenWidth, screen.ScreenHeight, screen.TextureResolution);

                            Vector2I key = Vector2I.Round(info.SurfaceSize);
                            grouped.GetValueOrNew(key).Add(new LCDDefInfo(fd, i, info));
                        }
                    }

                    StringBuilder sb = new StringBuilder(1024);

                    foreach(KeyValuePair<Vector2I, List<LCDDefInfo>> kv in grouped)
                    {
                        Vector2I size = kv.Key;
                        sb.Append(" === ").Append(size.X).Append(" x ").Append(size.Y).Append(" ").Append('=', 30).Append('\n');

                        sb.Append('\n');

                        foreach(LCDDefInfo defInfo in kv.Value.OrderBy(d => d.Definition.DisplayNameText))
                        {
                            sb.Append(defInfo.Definition.CubeSize == MyCubeSize.Large ? "[Large] " : "[Small] ")
                                .Append(defInfo.Definition.DisplayNameText)
                                .Append(" | surface #").Append(defInfo.ScreenIndex)
                                .Append(" | surfacesize: ").RoundedNumber(defInfo.Info.SurfaceSize.X, 4).Append("x").RoundedNumber(defInfo.Info.SurfaceSize.Y, 4)
                                .Append(" | texturesize: ").RoundedNumber(defInfo.Info.TextureSize.X, 4).Append("x").RoundedNumber(defInfo.Info.TextureSize.Y, 4)
                                .Append(" | aspectratio: ").RoundedNumber(defInfo.Info.AspectRatio.X, 4).Append(":").RoundedNumber(defInfo.Info.AspectRatio.Y, 4)
                                .Append('\n');
                        }

                        sb.Append('\n');
                        sb.Append('\n');
                    }

                    Display("LCDs grouped by size", sb.ToString());
                }
                else
                {
                    StringBuilder sb = new StringBuilder(1024);

                    Dictionary<Vector2I, int> uniqueSizes = new Dictionary<Vector2I, int>();

                    foreach(MyFunctionalBlockDefinition fd in defs)
                    {
                        sb.Append(fd.CubeSize == MyCubeSize.Large ? "[Large] " : "[Small] ")
                            .Append(fd.DisplayNameText)
                            .Append('\n');

                        if(fd is MyTextPanelDefinition && fd.ScreenAreas.Count == 4)
                        {
                            sb.Append("    (these surfaces are used based on the LCD rotation slider)\n");
                        }

                        for(int i = 0; i < fd.ScreenAreas.Count; i++)
                        {
                            ScreenArea screen = fd.ScreenAreas[i];
                            Hardcoded.TextSurfaceInfo info = Hardcoded.TextSurface_GetInfo(screen.ScreenWidth, screen.ScreenHeight, screen.TextureResolution);

                            Vector2I key = Vector2I.Round(info.SurfaceSize);
                            uniqueSizes[key] = uniqueSizes.GetValueOrDefault(key, 0) + 1;

                            sb.Append("    #").Append(i)
                                .Append(" | surface: ").RoundedNumber(info.SurfaceSize.X, 4).Append(" x ").RoundedNumber(info.SurfaceSize.Y, 4)
                                .Append(" | texture: ").RoundedNumber(info.TextureSize.X, 4).Append(" x ").RoundedNumber(info.TextureSize.Y, 4)
                                .Append(" | aspect: ").RoundedNumber(info.AspectRatio.X, 4).Append(":").RoundedNumber(info.AspectRatio.Y, 4)
                                .Append(" | name: ").Append(MyTexts.GetString(screen.DisplayName));

                            if(!string.IsNullOrEmpty(screen.Script))
                                sb.Append(" | TSS: ").Append(screen.Script);

                            //.Append(" | material: ").Append(screen.Name) // this is material name, not useful to anyone reading this

                            sb.Append('\n');
                        }

                        sb.Append('\n');
                    }

                    sb.Append('\n');
                    sb.Append('\n');
                    sb.Append("=== Unique surface sizes (rounded to integer) ===").Append('\n');

                    foreach(KeyValuePair<Vector2I, int> kv in uniqueSizes.OrderByDescending(kv => kv.Value)) // sorted by popularity
                    {
                        Vector2I size = kv.Key;
                        sb.Append("  ").Append(size.X).Append(" x ").Append(size.Y).Append("  (").Append(kv.Value).Append(" surfaces)").Append('\n');
                    }

                    Display("LCDs", sb.ToString());
                }

                return;
            }

            if(args.Count < 2 || type == null || sort == null)
            {
                PrintChat("Sort what?", FontsHandler.RedSh);
                PrintHelpToChat();
                return;
            }

            string sizeName = "";
            MyCubeSize? sizeFilter = null;
            bool isDescending = false;

            for(int i = 2; i < args.Count; i++)
            {
                GetFlags(args.Get(i), ref sizeName, ref sizeFilter, ref isDescending);
            }

            string title = $"Sorted {sizeName}{type} by {sort} {(isDescending ? "descending" : "ascending")}"; // also used as file name

            if(type.Equals("blocks", ChatCommandHandler.StringCompare))
            {
                IEnumerable<MyCubeBlockDefinition> defs;

                switch(sort)
                {
                    case "mass": defs = Main.Caches.BlockDefs.OrderBy((d) => (d.HasPhysics ? d.Mass : 0)); break;
                    case "hp": defs = Main.Caches.BlockDefs.OrderBy((d) => d.MaxIntegrity); break;
                    case "volume": defs = Main.Caches.BlockDefs.OrderBy((d) => (d.Size * MyDefinitionManager.Static.GetCubeSize(d.CubeSize)).Volume); break;

                    default:
                        PrintChat($"Unknown sort arg (2nd): {sort}", FontsHandler.RedSh);
                        return;
                }

                if(isDescending)
                    defs = defs.Reverse();

                StringBuilder sb = new StringBuilder(1024);

                int num = 1;
                foreach(MyCubeBlockDefinition blockDef in defs)
                {
                    if(!blockDef.Public || (MyAPIGateway.Session.SurvivalMode && !blockDef.AvailableInSurvival))
                        continue;

                    if(sizeFilter.HasValue && sizeFilter.Value != blockDef.CubeSize)
                        continue;

                    float cellSize = MyDefinitionManager.Static.GetCubeSize(blockDef.CubeSize);

                    sb.Append(num++).Append(". ");

                    if(sizeFilter == null)
                        sb.Append(blockDef.CubeSize == MyCubeSize.Large ? "[Large] " : "[Small] ");

                    sb.Append(blockDef.DisplayNameText)
                        .Append(" | ").ExactMassFormat(blockDef.HasPhysics ? blockDef.Mass : 0)
                        .Append(" | ").Number(blockDef.MaxIntegrity).Append(" hp")
                        .Append(" | ").VolumeFormat((blockDef.Size * cellSize).Volume)
                        .Append('\n');
                }

                Display(title, sb.ToString());
                return;
            }

            if(type.Equals("comps", ChatCommandHandler.StringCompare))
            {
                if(sizeFilter.HasValue)
                    PrintChat($"Grid size filtering not applicable for {type} type.", FontsHandler.YellowSh);

                IEnumerable<MyComponentDefinition> defs;

                switch(sort)
                {
                    case "mass": defs = Main.Caches.ItemDefs.OfType<MyComponentDefinition>().OrderBy((d) => d.Mass); break;
                    case "hp": defs = Main.Caches.ItemDefs.OfType<MyComponentDefinition>().OrderBy((d) => d.MaxIntegrity); break;
                    case "volume": defs = Main.Caches.ItemDefs.OfType<MyComponentDefinition>().OrderBy((d) => d.Volume); break;

                    default:
                        PrintChat($"Unknown sort arg (2nd): {sort}", FontsHandler.RedSh);
                        return;
                }

                if(isDescending)
                    defs = defs.Reverse();

                StringBuilder sb = new StringBuilder(1024);

                int num = 1;
                foreach(MyComponentDefinition compDef in defs)
                {
                    if(!compDef.Public || (MyAPIGateway.Session.SurvivalMode && !compDef.AvailableInSurvival))
                        continue;

                    sb.Append(num++).Append(". ")
                        .Append(compDef.DisplayNameText)
                        .Append(" | ").ExactMassFormat(compDef.Mass)
                        .Append(" | ").Number(compDef.MaxIntegrity).Append(" hp")
                        .Append(" | ").VolumeFormat(compDef.Volume)
                        .Append('\n');
                }

                Display(title, sb.ToString());
                return;
            }

            PrintChat($"Unknown type arg (1st): {type}", FontsHandler.RedSh);
        }

        void GetFlags(string arg, ref string sizeName, ref MyCubeSize? sizeFilter, ref bool isDescending)
        {
            if(arg == null)
                return;

            if(arg.Equals("sg", ChatCommandHandler.StringCompare))
            {
                sizeName = "smallgrid ";
                sizeFilter = MyCubeSize.Small;
                return;
            }

            if(arg.Equals("lg", ChatCommandHandler.StringCompare))
            {
                sizeName = "largegrid ";
                sizeFilter = MyCubeSize.Large;
                return;
            }

            if(arg.Equals("desc", ChatCommandHandler.StringCompare))
            {
                isDescending = true;
                return;
            }
        }

        void Display(string title, string text)
        {
            MyAPIGateway.Utilities.ShowMissionScreen(title, "", "", text, (res) =>
            {
                if(res == ResultEnum.OK)
                    Export(title, text);
            }, "Export info to file\n(Press [Esc] to not export)");
        }

        void Export(string title, string text)
        {
            StringBuilder tempSB = new StringBuilder(4096);

            tempSB.Clear();
            tempSB.Append(title);
            tempSB.Append(" ");
            tempSB.Append(DateTime.Now.ToString("yyyy-MM-dd HHmm"));
            tempSB.Append(".txt");

            foreach(char invalidChar in MyUtils.GetFixedInvalidFileNameChars())
            {
                tempSB.Replace(invalidChar, '_');
            }

            while(tempSB.IndexOf("__") != -1)
            {
                tempSB.Replace("__", "_");
            }

            string fileName = tempSB.ToString();


            tempSB.Clear();
            tempSB.Append("Date: ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append('\n');
            tempSB.Append("Session name: ").Append(MyAPIGateway.Session.Name).Append('\n');
            tempSB.Append("Game version: ").Append(MyAPIGateway.Session.Version.ToString()).Append('\n');
            tempSB.Append("Mods: ").Append(MyAPIGateway.Session.Mods.Count).Append('\n');
            tempSB.Append('\n');
            tempSB.Append(title);
            tempSB.Append('\n');
            tempSB.Append(text);


            TextWriter writer = null;
            try
            {
                writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(fileName, typeof(AnalyseShip));
                writer.Write(tempSB);
                writer.Flush();

                string modStorageName = MyAPIGateway.Utilities?.GamePaths?.ModScopeName ?? "(ERROR)";
                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, $"Exported to: %appdata%/SpaceEngineers/Storage/{modStorageName}/{fileName}", FontsHandler.GreenSh);
            }
            catch(Exception e)
            {
                Log.Error(e, null);
                Utils.ShowColoredChatMessage(BuildInfoMod.ModName, $"Failed to export! Exception: {e.Message}; see SE log for details.", FontsHandler.RedSh);
            }
            finally
            {
                writer?.Dispose();
            }
        }
    }
}