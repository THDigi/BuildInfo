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

        public const string SEOutlined = "BI_SEOutlined";

        public const char IconExplode = '\ue100';
        public const char IconCharacter = '\ue101';
        public const char IconCharacterHead = '\ue102';
        public const char IconBlock = '\ue103';
        public const char IconProjectileGravity = '\ue104';
        public const char IconProjectileNoGravity = '\ue105';
        public const char IconBlockPenetration = '\ue106';
        public const char IconMaxSpeed = '\ue107';
        public const char IconMissile = '\ue108';
        public const char IconSphere = '\ue109';

        // single-use for parsing, do not make public
        List<FontInfo> Fonts = new List<FontInfo>()
        {
            new FontInfo(SEOutlined, $@"Fonts\{SEOutlined}\FontDataPA.xml"),
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

            AddFonts();
        }

        void BackgroundTask()
        {
            foreach(FontInfo fontInfo in Fonts)
            {
                if(Unloaded)
                    break;

                // TODO multi-XML support of sorts?

                string xml = null;

                using(TextReader file = MyAPIGateway.Utilities.ReadFileInModLocation(fontInfo.XMLPath, Main.Session.ModContext.ModItem))
                {
                    xml = file.ReadToEnd();
                }

                fontInfo.Parser = new FontParser();
                fontInfo.Parser.Parse(xml);

                Log.Info($"Parsed font '{fontInfo.Name}' - bitmaps: {fontInfo.Parser.Bitmaps.Count}; glyphs: {fontInfo.Parser.Glyphs.Count}; kernpairs: {fontInfo.Parser.Kernpairs.Count}");

                if(fontInfo.Name == SEOutlined)
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
        }

        void BackgroundTaskDone()
        {
            if(Unloaded)
                return;

            if(Log.TaskHasErrors(Task, $"{nameof(FontsHandler)} Parse XML"))
                return;
        }

        void AddFonts()
        {
            foreach(FontInfo fontInfo in Fonts)
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
                    FontParser.Bitmap bitmap = data.Bitmaps[glyph.Bitmap];
                    font.AddCharacter(glyph.Ch, bitmap.MaterialId, bitmap.SizeX, glyph.Code, glyph.OriginX, glyph.OriginY, glyph.SizeX, glyph.SizeY, glyph.Aw, glyph.Lsb, glyph.ForceWhite);
                }

                foreach(FontParser.Kernpair kp in data.Kernpairs)
                {
                    font.AddKerning(kp.Adjust, kp.Right, kp.Left);
                }

                FontAdded(font, fontInfo);
            }

            Fonts = null;
        }

        void FontAdded(HudAPIv2.FontDefinition font, FontInfo fontInfo)
        {
            if(fontInfo.Name != SEOutlined)
                return;

            MyStringId material = MyStringId.GetOrCompute("BI_FontIcons");
            const int materialSizeX = 256;
            const int iconSize = 32;
            const int iconAw = 32;
            const int iconLsb = 0;
            const bool recolorable = true;

            const int startingChar = '\ue100';
            const int offset = 6; // HACK: hardcoded offset in textAPI which is there to fix something else
            const int maxGridX = materialSizeX / iconSize;
            const int totalIcons = maxGridX * maxGridX;

            int gridX = 0;
            int gridY = 0;

            for(int i = 0; i < totalIcons; i++)
            {
                if(gridX >= maxGridX)
                {
                    gridY++;
                    gridX = 0;
                }

                char c = (char)(startingChar + i);
                string charCode = ((int)c).ToString("X");

                font.AddCharacter(c, material, materialSizeX, charCode, (gridX * iconSize), (gridY * iconSize) - offset, iconSize, iconSize + offset, iconAw, iconLsb, !recolorable);

                gridX++;
            }
        }
    }
}
