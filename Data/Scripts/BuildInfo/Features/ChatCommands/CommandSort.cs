using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.ChatCommands
{
    public class CommandSort : Command
    {
        public CommandSort() : base("sort")
        {
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

        public override void Execute(Arguments args)
        {
            string type = args.Get(0);
            string sort = args.Get(1);

            if(args.Count < 2 || type == null || sort == null)
            {
                PrintChat("Requires 2 arguments! First is the kind of things to list and second is what to sort them by.", FontsHandler.RedSh);
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
                        sb.Append(blockDef.CubeSize == MyCubeSize.Large ? "[LargeGrid] " : "[SmallGrid] ");

                    sb.Append(blockDef.DisplayNameText)
                        .Append(" | ").MassFormat(blockDef.HasPhysics ? blockDef.Mass : 0)
                        .Append(" | ").Number(blockDef.MaxIntegrity).Append(" hp")
                        .Append(" | ").VolumeFormat((blockDef.Size * cellSize).Volume)
                        .Append("\n");
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
                        .Append(" | ").MassFormat(compDef.Mass)
                        .Append(" | ").Number(compDef.MaxIntegrity).Append(" hp")
                        .Append(" | ").VolumeFormat(compDef.Volume)
                        .Append("\n");
                }

                Display(title, sb.ToString());
                return;
            }

            PrintChat($"Unknown type arg (1st): {type}", FontsHandler.RedSh);
        }

        public override void PrintHelp(StringBuilder sb)
        {
            sb.Append(MainAlias).Append(" <blocks|comps> <mass|hp|volume> [sg|lg] [desc]").NewLine();
            sb.Append("  Shows a list of arg1 sorted by arg2 ascending.").NewLine();
            sb.Append("  Optionally can filter by grid size and/or sort descending (desc can be 3rd arg too).").NewLine();
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
            StringBuilder FileNameSB = new StringBuilder(256);
            FileNameSB.Append(title);
            FileNameSB.Append(" ");
            FileNameSB.Append(DateTime.Now.ToString("yyyy-MM-dd HHmm"));
            FileNameSB.Append(".txt");

            foreach(char invalidChar in MyUtils.GetFixedInvalidFileNameChars())
            {
                FileNameSB.Replace(invalidChar, '_');
            }

            while(FileNameSB.IndexOf("__") != -1)
            {
                FileNameSB.Replace("__", "_");
            }

            string fileName = FileNameSB.ToString();
            string modStorageName = MyAPIGateway.Utilities.GamePaths.ModScopeName;

            TextWriter writer = null;
            try
            {
                writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(fileName, typeof(AnalyseShip));
                writer.Write(text);
                writer.Flush();

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