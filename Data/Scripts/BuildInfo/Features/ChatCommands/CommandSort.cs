using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
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
            sb.Append(PrimaryCommand).Append(" blocks <mass|hp|volume|cells|density|hpmassratio> [sg|lg] [desc]").NewLine();
            sb.Append(PrimaryCommand).Append(" comps <mass|hp|volume|density> [desc]").NewLine();
            sb.Append(PrimaryCommand).Append(" items <mass|volume|density> [desc]").NewLine();
            sb.Append(PrimaryCommand).Append(" lcds [group]").NewLine();
            sb.Append("  Shows a sorted list of the specified things; can be exported to file.").NewLine();
        }

        const string Separator = "  |  ";

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
                ComputeLCDs(type, sort);
                return;
            }

            if(args.Count < 2 || type == null || sort == null)
            {
                PrintChat("Sort by what?", FontsHandler.RedSh);
                PrintHelpToChat();
                return;
            }

            if(type.Equals("blocks", ChatCommandHandler.StringCompare))
            {
                ComputeBlocks(args, type, sort);
                return;
            }

            bool compsOnly = type.Equals("comps", ChatCommandHandler.StringCompare);
            if(compsOnly || type.Equals("items", ChatCommandHandler.StringCompare))
            {
                ComputeItems(args, type, sort, compsOnly);
                return;
            }

            PrintChat($"Unknown type arg (1st): {type}", FontsHandler.RedSh);
        }

        struct Details
        {
            public string SizeName;
            public MyCubeSize? SizeFilter;
            public bool IsDescending;
            public string FileName;
        }

        Details GetDetails(Arguments args, string type, string sort)
        {
            var data = new Details();
            data.SizeName = string.Empty;
            data.SizeFilter = null;
            data.IsDescending = false;

            for(int i = 2; i < args.Count; i++)
            {
                GetFlags(args.Get(i), ref data.SizeName, ref data.SizeFilter, ref data.IsDescending);
            }

            data.FileName = $"Sorted {data.SizeName}{type} by {sort} {(data.IsDescending ? "descending" : "ascending")}"; // also used as file name

            return data;
        }

        void ComputeLCDs(string type, string sort)
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
                            .Append(Separator).Append("surface #").Append(defInfo.ScreenIndex)
                            .Append(Separator).Append("surfacesize: ").RoundedNumber(defInfo.Info.SurfaceSize.X, 4).Append("x").RoundedNumber(defInfo.Info.SurfaceSize.Y, 4)
                            .Append(Separator).Append("texturesize: ").RoundedNumber(defInfo.Info.TextureSize.X, 4).Append("x").RoundedNumber(defInfo.Info.TextureSize.Y, 4)
                            .Append(Separator).Append("aspectratio: ").RoundedNumber(defInfo.Info.AspectRatio.X, 4).Append(":").RoundedNumber(defInfo.Info.AspectRatio.Y, 4)
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
                            .Append(Separator).Append("surface: ").RoundedNumber(info.SurfaceSize.X, 4).Append(" x ").RoundedNumber(info.SurfaceSize.Y, 4)
                            .Append(Separator).Append("texture: ").RoundedNumber(info.TextureSize.X, 4).Append(" x ").RoundedNumber(info.TextureSize.Y, 4)
                            .Append(Separator).Append("aspect: ").RoundedNumber(info.AspectRatio.X, 4).Append(":").RoundedNumber(info.AspectRatio.Y, 4)
                            .Append(Separator).Append("name: ").Append(MyTexts.GetString(screen.DisplayName));

                        if(!string.IsNullOrEmpty(screen.Script))
                            sb.Append(Separator).Append("TSS: ").Append(screen.Script);

                        //.Append(Separator).Append("material: ").Append(screen.Name) // this is material name, not useful to anyone reading this

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
        }

        void ComputeBlocks(Arguments args, string type, string sort)
        {
            var data = GetDetails(args, type, sort);

            IEnumerable<MyCubeBlockDefinition> defs;
            List<MyCubeBlockDefinition> list = Main.Caches.BlockDefs;

            switch(sort)
            {
                case "mass": defs = list.OrderBy((d) => (d.HasCollider() ? d.Mass : 0)); break;
                case "hp": defs = list.OrderBy((d) => d.MaxIntegrity); break;
                case "volume": defs = list.OrderBy((d) => (d.Size * MyDefinitionManager.Static.GetCubeSize(d.CubeSize)).Volume); break;
                case "cells": defs = list.OrderBy((d) => d.Size.Volume()); break;
                case "density":
                {
                    defs = list.OrderBy((d) =>
                    {
                        if(!d.HasCollider())
                            return float.PositiveInfinity;

                        float volume = (d.Size * MyDefinitionManager.Static.GetCubeSize(d.CubeSize)).Volume;
                        return d.Mass / volume;
                    });
                    break;
                }
                case "hpmassratio":
                {
                    defs = list.OrderBy((d) =>
                    {
                        if(!d.HasCollider())
                            return float.PositiveInfinity;

                        return d.MaxIntegrity / d.Mass;
                    });
                    break;
                }

                default:
                    PrintChat($"Unknown sort arg (2nd): {sort}", FontsHandler.RedSh);
                    return;
            }

            if(data.IsDescending)
                defs = defs.Reverse();

            StringBuilder sb = new StringBuilder(1024);

            int num = 1;
            foreach(MyCubeBlockDefinition blockDef in defs)
            {
                if(!blockDef.Public || (MyAPIGateway.Session.SurvivalMode && !blockDef.AvailableInSurvival))
                    continue;

                if(data.SizeFilter.HasValue && data.SizeFilter.Value != blockDef.CubeSize)
                    continue;

                float cellSize = MyDefinitionManager.Static.GetCubeSize(blockDef.CubeSize);

                sb.Append(num++).Append(". ");

                if(data.SizeFilter == null)
                    sb.Append(blockDef.CubeSize == MyCubeSize.Large ? "[Large] " : "[Small] ");

                sb.Append(blockDef.DisplayNameText);

                bool hasCollider = blockDef.HasCollider();

                if(hasCollider)
                    sb.Append(Separator).ExactMassFormat(blockDef.Mass);
                else
                    sb.Append(Separator).Append("No mass");

                sb.Append(Separator).Size3DFormat(blockDef.Size);

                sb.Append(Separator).VolumeFormat((blockDef.Size * cellSize).Volume);

                sb.Append(Separator).Number(hasCollider ? blockDef.Mass / (blockDef.Size * cellSize).Volume : float.PositiveInfinity).Append(" density");

                sb.Append(Separator).Number(blockDef.MaxIntegrity).Append(" hp");

                sb.Append(Separator).Number(hasCollider ? blockDef.MaxIntegrity / blockDef.Mass : float.PositiveInfinity).Append(" hp-mass-ratio");

                sb.Append('\n');
            }

            Display(data.FileName, sb.ToString());
        }

        void ComputeItems(Arguments args, string type, string sort, bool compsOnly)
        {
            var data = GetDetails(args, type, sort);

            if(data.SizeFilter.HasValue)
                PrintChat($"Grid size filtering not applicable for {type} type.", FontsHandler.YellowSh);

            IEnumerable<MyPhysicalItemDefinition> defs;

            if(compsOnly)
            {
                IEnumerable<MyComponentDefinition> list = Main.Caches.ItemDefs.OfType<MyComponentDefinition>();

                switch(sort)
                {
                    case "mass": defs = list.OrderBy((d) => d.Mass); break;
                    case "volume": defs = list.OrderBy((d) => d.Volume); break;
                    case "density": defs = list.OrderBy((d) => (d.Mass / d.Volume)); break;
                    case "hp": defs = list.OrderBy((d) => d.MaxIntegrity); break;

                    default:
                        PrintChat($"Unknown sort arg (2nd): {sort}", FontsHandler.RedSh);
                        return;
                }
            }
            else
            {
                List<MyPhysicalItemDefinition> list = Main.Caches.ItemDefs;

                switch(sort)
                {
                    case "mass": defs = list.OrderBy((d) => d.Mass); break;
                    case "volume": defs = list.OrderBy((d) => d.Volume); break;
                    case "density": defs = list.OrderBy((d) => (d.Mass / d.Volume)); break;

                    default:
                        PrintChat($"Unknown sort arg (2nd): {sort}", FontsHandler.RedSh);
                        return;
                }
            }

            if(data.IsDescending)
                defs = defs.Reverse();

            StringBuilder sb = new StringBuilder(1024);

            int num = 1;
            foreach(var itemDef in defs)
            {
                if(!itemDef.Public || (MyAPIGateway.Session.SurvivalMode && !itemDef.AvailableInSurvival))
                    continue;

                sb.Append(num++).Append(". ").Append(itemDef.DisplayNameText);

                sb.Append(Separator).ExactMassFormat(itemDef.Mass);
                sb.Append(Separator).VolumeFormat(itemDef.Volume);
                sb.Append(Separator).Number(itemDef.Mass / itemDef.Volume).Append(" density");

                var compDef = itemDef as MyComponentDefinition;
                if(compDef != null)
                    sb.Append(Separator).Number(compDef.MaxIntegrity).Append(" hp");

                sb.Append('\n');
            }

            Display(data.FileName, sb.ToString());
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
            // TODO: make use of the fake-GUI screens to display these things in a nicer table
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