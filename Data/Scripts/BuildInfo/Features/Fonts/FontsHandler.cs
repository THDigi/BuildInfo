﻿using System;
using System.Collections.Generic;
using System.IO;
using Digi.BuildInfo.Features.Fonts;
using Draygo.API;
using ParallelTasks;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;

namespace Digi.BuildInfo.Features
{
    public class FontsHandler : ModComponent
    {
        public const string RedSh = "Red";
        public const string WhiteSh = "Debug";
        public const string GreenSh = "BI_Green";
        public const string SkyBlueSh = "BI_SkyBlue";
        public const string YellowSh = "BI_Yellow";
        public const string GraySh = "BI_Gray";

        public const string BI_SEOutlined = "BI_SEOutlined";
        public const string BI_Monospace = "BI_Monospace";

        public const string TextAPI_NormalFont = "white";
        public const string TextAPI_OutlinedFont = BI_SEOutlined;
        public const string TextAPI_MonospaceFont = BI_Monospace;

        // these are all only in textAPI version of SEOutlined
        public const int IconStartingChar = '\ue200';
        public const char IconExplode = (char)IconStartingChar;
        public const char IconCharacter = (char)(IconStartingChar + 1);
        public const char IconCharacterHead = (char)(IconStartingChar + 2);
        public const char IconBlockDamage = (char)(IconStartingChar + 3);
        public const char IconProjectileGravity = (char)(IconStartingChar + 4);
        public const char IconProjectileNoGravity = (char)(IconStartingChar + 5);
        public const char IconBlockPenetration = (char)(IconStartingChar + 6);
        public const char IconMaxSpeed = (char)(IconStartingChar + 7);
        public const char IconMissile = (char)(IconStartingChar + 8);
        public const char IconSphere = (char)(IconStartingChar + 9);
        public const char IconRicochet = (char)(IconStartingChar + 10);
        public const char IconDice = (char)(IconStartingChar + 11);
        public const char IconTooltip = (char)(IconStartingChar + 12);

        public const char IconCircle = (char)(IconStartingChar + 62);
        public const char IconSquare = (char)(IconStartingChar + 63); // last slot

        static readonly bool DoExportSpecialChars = false;

        // single-use for parsing, do not make public
        List<FontInfo> TextAPIFonts = new List<FontInfo>()
        {
            new FontInfo(BI_SEOutlined, $@"Fonts\{BI_SEOutlined}\FontDataPA.xml"),
            new FontInfo(BI_Monospace, $@"Fonts\{BI_Monospace}\BIMonospace.xml"),
        };

        class FontInfo
        {
            public string Name;
            public string XMLPath;
            public FontParser Parser;

            public FontInfo(string name, string path)
            {
                Name = name;
                XMLPath = path;
            }
        }

        /// <summary>
        /// Must be used when CharSize doesn't have the char you want because it's most likely a chinese character.
        /// </summary>
        public const int DefaultCharSize = 33;

        public readonly Dictionary<char, int> CharSize = new Dictionary<char, int>();

        Task Task;
        bool Unloaded = false;

        public FontsHandler(BuildInfoMod main) : base(main)
        {
            Main.TextAPI.Detected += TextAPI_Detected;

            Task = MyAPIGateway.Parallel.StartBackground(BackgroundTask, BackgroundTaskDone);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Unloaded = true;
            Main.TextAPI.Detected -= TextAPI_Detected;
        }

        void TextAPI_Detected()
        {
            if(!Task.IsComplete)
            {
                Log.Info("Background thread parsing fonts files took too long, waiting for it...");
                Task.Wait(true);
            }

            AddTextAPIFonts();
        }

        void BackgroundTask()
        {
            foreach(FontInfo fontInfo in TextAPIFonts)
            {
                if(Unloaded)
                    break;

                string xml = null;
                try
                {
                    // TODO multi-XML support of sorts?
                    using(TextReader file = MyAPIGateway.Utilities.ReadFileInModLocation(fontInfo.XMLPath, Main.Session.ModContext.ModItem))
                    {
                        xml = file.ReadToEnd();
                    }

                    fontInfo.Parser = new FontParser();
                    fontInfo.Parser.Parse(xml);

                    Log.Info($"Parsed font '{fontInfo.Name}' - bitmaps: {fontInfo.Parser.Bitmaps.Count}; glyphs: {fontInfo.Parser.Glyphs.Count}; kernpairs: {fontInfo.Parser.Kernpairs.Count}");

                    if(fontInfo.Name == BI_SEOutlined)
                    {
                        // HACK: altered height to 32 so that it fits better with the built-in textAPI font (which is 30 height)
                        fontInfo.Parser.Height = 32;

                        // HACK: it's the same font as vanilla but outlined, using this to generate character width dictionary aswell
                        foreach(FontParser.Glyph glyph in fontInfo.Parser.Glyphs)
                        {
                            CharSize[glyph.Ch] = glyph.Aw;
                        }
                    }
                }
                catch(Exception e)
                {
                    Log.Error($"Error parsing font '{fontInfo?.Name}'; xml={xml != null}: {e.ToString()}");
                }
            }
        }

        void BackgroundTaskDone()
        {
            if(Unloaded)
                return;

            if(Log.TaskHasErrors(Task, $"{nameof(FontsHandler)} Parse XML"))
                return;
        }

        void AddTextAPIFonts()
        {
            foreach(FontInfo fontInfo in TextAPIFonts)
            {
                try
                {
                    HudAPIv2.FontDefinition font = HudAPIv2.APIinfo.GetFontDefinition(MyStringId.GetOrCompute(fontInfo.Name));
                    FontParser data = fontInfo.Parser;

                    foreach(FontParser.Bitmap bitmap in data.Bitmaps)
                    {
                        MyDefinitionBase def = MyDefinitionManager.Static.GetDefinition(new MyDefinitionId(typeof(MyObjectBuilder_TransparentMaterialDefinition), bitmap.MaterialId.String));
                        if(def == null)
                            Log.Error($"Font '{fontInfo.Name}' for textAPI does not have the transparent material '{bitmap.MaterialId.String}' declared in SBC!");
                    }

                    font.DefineFont(data.Base, data.Height, data.Size);

                    foreach(FontParser.Glyph glyph in data.Glyphs)
                    {
                        if(glyph.Bitmap < 0 || glyph.Bitmap >= data.Bitmaps.Count)
                        {
                            Log.Error($"Font '{fontInfo.Name}' has glyph '{glyph.Ch}'/'{glyph.Code}' with inexistent bitmap index: {glyph.Bitmap}; max bitmap index: {data.Bitmaps.Count - 1}");
                            continue;
                        }

                        FontParser.Bitmap bitmap = data.Bitmaps[glyph.Bitmap];
                        font.AddCharacter(glyph.Ch, bitmap.MaterialId, bitmap.SizeX, glyph.Code, glyph.OriginX, glyph.OriginY, glyph.SizeX, glyph.SizeY, glyph.Aw, glyph.Lsb, glyph.ForceWhite);
                    }

                    foreach(FontParser.Kernpair kp in data.Kernpairs)
                    {
                        font.AddKerning(kp.Adjust, kp.Right, kp.Left);
                    }

                    FontAdded(font, fontInfo);
                }
                catch(Exception e)
                {
                    Log.Error($"Error adding font '{fontInfo?.Name}': {e.ToString()}");
                }
            }

            TextAPIFonts = null;
        }

        void FontAdded(HudAPIv2.FontDefinition font, FontInfo fontInfo)
        {
            if(fontInfo.Name != BI_SEOutlined)
                return;

            MyStringId material = MyStringId.GetOrCompute("BI_FontIcons");
            const int materialSizeX = 256;
            const int iconSize = 32;
            const int iconAw = 32;
            const int iconLsb = 0;
            const bool recolorable = true;

            const int offset = 6; // HACK: hardcoded offset in textAPI which is there to fix something else
            const int maxGridX = materialSizeX / iconSize;
            const int totalIcons = maxGridX * maxGridX;

            int gridX = 0;
            int gridY = 0;

            TextWriter writer = null;
            try
            {
                int iconBitmapId = 1;
                bool export = BuildInfoMod.IsDevMod && DoExportSpecialChars;
                if(export)
                {
                    writer = MyAPIGateway.Utilities.WriteFileInLocalStorage("font special characters.txt", typeof(FontsHandler));

                    writer.WriteLine($"<bitmap id=\"{iconBitmapId}\" name=\"{material.String}.dds\" size=\"{materialSizeX}x{materialSizeX}\" />");
                    writer.WriteLine();
                }

                for(int i = 0; i < totalIcons; i++)
                {
                    if(gridX >= maxGridX)
                    {
                        gridY++;
                        gridX = 0;
                    }

                    char c = (char)(IconStartingChar + i);
                    string charCode = ((int)c).ToString("X");

                    int x = (gridX * iconSize);
                    int y = (gridY * iconSize);
                    int sizeX = iconSize;
                    int sizeY = iconSize;

                    font.AddCharacter(c, material, materialSizeX, charCode, x, y - offset, sizeX, sizeY + offset, iconAw, iconLsb, !recolorable);

                    if(export)
                    {
                        writer.WriteLine($"<glyph ch=\"{c}\" code=\"{charCode.ToLower()}\" bm=\"{iconBitmapId}\" origin=\"{x},{y}\" size=\"{sizeX}x{sizeY}\" aw=\"17\" lsb=\"-4\" forcewhite=\"{(recolorable ? "false" : "true")}\" />");
                    }

                    gridX++;
                }
            }
            finally
            {
                writer?.Dispose();
            }
        }
    }
}
