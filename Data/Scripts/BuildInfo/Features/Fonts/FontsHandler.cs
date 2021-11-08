using System.Collections.Generic;
using Digi.BuildInfo.Features.Fonts;

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

        /// <summary>
        /// Works on textAPI aswell.
        /// </summary>
        public const string SEOutlined = "BI_SEOutlined";

        /// <summary>
        /// Must be used when CharSize doesn't have the char you want because it's most likely a chinese character.
        /// </summary>
        public const int DefaultCharSize = 33;
        public readonly Dictionary<char, int> CharSize = new Dictionary<char, int>();

        readonly TextAPIFont_SEOutlined TextAPIFont_SEOutlined = new TextAPIFont_SEOutlined();

        public FontsHandler(BuildInfoMod main) : base(main)
        {
            Main.TextAPI.Detected += TextAPI_Detected;

            ComputeCharacterSizes();

            //ParseFonts();
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            Main.TextAPI.Detected -= TextAPI_Detected;
        }

        void TextAPI_Detected()
        {
            TextAPIFont_SEOutlined.AddFont();
        }

        void ComputeCharacterSizes()
        {
            CharSize.Clear();

            AddCharsSize(0, "\n\r\t");

            // generated from fonts/white_shadow/FontDataPA.xml+FontDataCH.xml, size is the "aw" property.
            AddCharsSize(6, "'|¦ˉ‘’‚");
            AddCharsSize(7, "ј");
            AddCharsSize(8, " !I`ijl ¡¨¯´¸ÌÍÎÏìíîïĨĩĪīĮįİıĵĺļľłˆˇ˘˙˚˛˜˝ІЇії‹›∙！");
            AddCharsSize(9, "(),.1:;[]ft{}·ţťŧț（）：《》，。、；【】");
            AddCharsSize(10, "\"-rª­ºŀŕŗř");
            AddCharsSize(11, "*²³¹");
            AddCharsSize(12, "\\°“”„");
            AddCharsSize(13, "ґ");
            AddCharsSize(14, "/ĳтэє");
            AddCharsSize(15, "L_vx«»ĹĻĽĿŁГгзлхчҐ–•");
            AddCharsSize(16, "7?Jcz¢¿çćĉċčĴźżžЃЈЧавийнопсъьѓѕќ？");
            AddCharsSize(17, "3FKTabdeghknopqsuy£µÝàáâãäåèéêëðñòóôõöøùúûüýþÿāăąďđēĕėęěĝğġģĥħĶķńņňŉōŏőśŝşšŢŤŦũūŭůűųŶŷŸșȚЎЗКЛбдекруцяёђћўџ");
            AddCharsSize(18, "+<=>E^~¬±¶ÈÉÊË×÷ĒĔĖĘĚЄЏЕНЭ−");
            AddCharsSize(19, "#0245689CXZ¤¥ÇßĆĈĊČŹŻŽƒЁЌАБВДИЙПРСТУХЬ€");
            AddCharsSize(20, "$&GHPUVY§ÙÚÛÜÞĀĜĞĠĢĤĦŨŪŬŮŰŲОФЦЪЯжы†‡￥");
            AddCharsSize(21, "ABDNOQRSÀÁÂÃÄÅÐÑÒÓÔÕÖØĂĄĎĐŃŅŇŌŎŐŔŖŘŚŜŞŠȘЅЊЖф□");
            AddCharsSize(22, "љ");
            AddCharsSize(23, "ю");
            AddCharsSize(24, "%ĲЫ");
            AddCharsSize(25, "@©®мшњ");
            AddCharsSize(26, "MМШ");
            AddCharsSize(27, "mw¼ŵЮщ");
            AddCharsSize(28, "¾æœЉ");
            AddCharsSize(29, "½Щ");
            AddCharsSize(30, "™");
            AddCharsSize(31, "WÆŒŴ—…‰");
            AddCharsSize(32, "");
            AddCharsSize(34, "");
            AddCharsSize(37, "");
            AddCharsSize(40, "");
            AddCharsSize(41, "");
            AddCharsSize(45, "");
            AddCharsSize(46, "");
            AddCharsSize(57, "");
        }

        void AddCharsSize(int size, string chars)
        {
            for(int i = 0; i < chars.Length; i++)
            {
                char chr = chars[i];
                int existingSize;
                if(CharSize.TryGetValue(chr, out existingSize))
                {
                    //Log.Error($"Character '{chr.ToString()}' ({((int)chr).ToString()}) already exists for size: {existingSize.ToString()} --- line: AddCharsSize({size.ToString()}, {chars})");
                    continue;
                }

                CharSize.Add(chr, size);
            }
        }

        // parsing the game font files, for dev use only.
#if false
        private void ParseFonts()
        {
            Dictionary<int, HashSet<char>> charsBySize = new Dictionary<int, HashSet<char>>();

            if(!ParseFontFile("FontDataPA.xml", charsBySize) | !ParseFontFile("FontDataCH.xml", charsBySize))
                return;

            var sizes = charsBySize.Keys.ToList();
            sizes.Sort();

            var sb = new System.Text.StringBuilder();

            foreach(var size in sizes)
            {
                sb.Append("AddCharsSize(").Append(size).Append(", \"");

                var characters = charsBySize[size];

                foreach(var chr in characters)
                {
                    // escape characters used in code for simpler paste
                    if(chr == '\\')
                        sb.Append("\\\\");
                    else if(chr == '"')
                        sb.Append("\\\"");
                    else
                        sb.Append(chr);
                }

                sb.Append("\");").AppendLine();
            }

            using(var writer = Sandbox.ModAPI.MyAPIGateway.Utilities.WriteFileInLocalStorage("FontSizes.txt", typeof(Constants)))
            {
                writer.Write(sb);
            }
        }

        private bool ParseFontFile(string file, Dictionary<int, HashSet<char>> addTo)
        {
            if(!Sandbox.ModAPI.MyAPIGateway.Utilities.FileExistsInLocalStorage(file, typeof(Constants)))
                return false;

            using(var reader = Sandbox.ModAPI.MyAPIGateway.Utilities.ReadFileInLocalStorage(file, typeof(Constants)))
            {
                string line;

                while((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();

                    if(line.Equals("</glyphs>"))
                        break;

                    if(!line.StartsWith("<glyph "))
                        continue;

                    var ch = GetInBetween(line, "ch=\"", "\"");
                    var aw = GetInBetween(line, "aw=\"", "\"");

                    ch = unescape.GetValueOrDefault(ch, ch); // stuff like &lt; to be converted to <, etc.

                    var character = ch[0]; // this is how SE is doing it too; some of their ch="" have 2 characters...
                    var width = int.Parse(aw);

                    HashSet<char> set;
                    if(!addTo.TryGetValue(width, out set))
                    {
                        set = new HashSet<char>();
                        addTo.Add(width, set);
                    }
                    set.Add(character);
                }
            }

            return true;
        }

        private string GetInBetween(string content, string start, string end, int startFrom = 0)
        {
            int startIndex = content.IndexOf(start, startFrom);

            if(startIndex == -1)
                throw new System.Exception($"Couldn't find '{start}' after {startFrom.ToString()} in line: {content}");

            startIndex += start.Length;
            int endIndex = content.IndexOf(end, startIndex);

            if(endIndex == -1)
                throw new System.Exception($"Couldn't find '{end}' after {startIndex.ToString()} in line: {content}");

            return content.Substring(startIndex, (endIndex - startIndex));
        }

        // workaround for HttpUtility.HtmlDecode() not being available
        Dictionary<string, string> unescape = new Dictionary<string, string>()
        {
            ["&lt;"] = "<",
            ["&gt;"] = ">",
            ["&quot;"] = "\"",
            ["&amp;"] = "&",
            ["&apos;"] = "'",
        };
#endif
    }
}
