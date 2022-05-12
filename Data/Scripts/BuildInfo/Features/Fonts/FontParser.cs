using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Utils;

namespace Digi.BuildInfo.Features.Fonts
{
    public class FontParser
    {
        public bool Finished { get; private set; } = false;

        public string Name;
        public int Base;
        public int Height;
        public string Face;
        public int Size;

        public readonly List<Bitmap> Bitmaps = new List<Bitmap>(4);
        public readonly List<Glyph> Glyphs = new List<Glyph>(256);
        public readonly List<Kernpair> Kernpairs = new List<Kernpair>(64);

        public struct Bitmap
        {
            public int Id;

            /// <summary>
            /// File name with extension
            /// </summary>
            public string FileName;

            /// <summary>
            /// File name without extension, optimized for TextAPI
            /// </summary>
            public MyStringId MaterialId;

            public int SizeX;
            public int SizeY;
        }

        public struct Glyph
        {
            public char Ch;
            public string Code;
            public int Bitmap;
            public int OriginX;
            public int OriginY;
            public int SizeX;
            public int SizeY;
            public int Aw;
            public int Lsb;
            public bool ForceWhite;
        }

        public struct Kernpair
        {
            public char Left;
            public char Right;
            public int Adjust;
        }

        const StringComparison Compare = StringComparison.OrdinalIgnoreCase;
        string XML;

        public FontParser()
        {
        }

        /// <summary>
        /// Thread-safe, can only be used once
        /// </summary>
        public void Parse(string xml)
        {
            if(Finished)
                throw new Exception("Already parsed this object!");

            XML = xml;
            ReadFont();
            ReadBitmaps();
            ReadGlyphs();
            ReadKernpairs();
            Finished = true;
        }

        void ReadFont()
        {
            /*
<?xml version="1.0" encoding="UTF-8" ?>
<font
	xmlns="http://xna.microsoft.com/bitmapfont"
	xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
	xsi:schemaLocation="http://xna.microsoft.com/bitmapfont bitmapfont.xsd"

	name="FontDataPA" base="30" height="37"
	face="Space Engineers" size="23"
	>
            */

            int idx = FindTag(XML, "font");
            if(idx == -1)
                throw new Exception($"Can't find '<font'");

            int maxIdx = XML.IndexOf('>', idx);
            if(maxIdx == -1)
                throw new Exception($"Can't find ending '>' after finding '<font ' at idx={idx}");

            Name = GetAttributeValue(XML, idx, "name");
            Base = int.Parse(GetAttributeValue(XML, idx, "base"));
            Height = int.Parse(GetAttributeValue(XML, idx, "height"));
            Face = GetAttributeValue(XML, idx, "face");
            Size = int.Parse(GetAttributeValue(XML, idx, "size"));
        }

        static char[] SizeSeparator = new char[] { 'x' };
        static char[] OriginSeparator = new char[] { ',' };

        void ReadBitmaps()
        {
            // <bitmap id="0" name="FontDataSEOutline-0.dds" size="1024x1024" />

            const string tag = "bitmap";
            int idx = 0;
            while(true)
            {
                idx = FindTag(XML, tag, idx);
                if(idx == -1)
                    break;

                int maxIdx = XML.IndexOf('>', idx);
                if(maxIdx == -1)
                    throw new Exception($"Can't find ending '>' after finding '<{tag} ' tag at idx={idx}");

                string[] size = GetAttributeValue(XML, idx, "size").Split(SizeSeparator);

                string name = GetAttributeValue(XML, idx, "name");

                Bitmaps.Add(new Bitmap()
                {
                    Id = int.Parse(GetAttributeValue(XML, idx, "id")),
                    FileName = name,
                    MaterialId = MyStringId.GetOrCompute(name.Substring(0, name.Length - 4)),
                    SizeX = int.Parse(size[0]),
                    SizeY = int.Parse(size[1]),
                });
            }
        }

        void ReadGlyphs()
        {
            // <glyph ch="" code="e001" bm="0" origin="141,630" size="66x60" aw="45" lsb="-10" forcewhite="true" />

            const string tag = "glyph";
            int idx = 0;
            while(true)
            {
                idx = FindTag(XML, tag, idx);
                if(idx == -1)
                    break;

                int maxIdx = XML.IndexOf('>', idx);
                if(maxIdx == -1)
                    throw new Exception($"Can't find ending '>' after finding '<{tag} ' tag at idx={idx}");

                string[] origin = GetAttributeValue(XML, idx, "origin").Split(OriginSeparator);
                string[] size = GetAttributeValue(XML, idx, "size").Split(SizeSeparator);

                Glyphs.Add(new Glyph()
                {
                    Ch = ParseHTMLChar(GetAttributeValue(XML, idx, "ch")),
                    Code = GetAttributeValue(XML, idx, "code"),
                    Bitmap = int.Parse(GetAttributeValue(XML, idx, "bm")),
                    OriginX = int.Parse(origin[0]),
                    OriginY = int.Parse(origin[1]),
                    SizeX = int.Parse(size[0]),
                    SizeY = int.Parse(size[1]),
                    Aw = int.Parse(GetAttributeValue(XML, idx, "aw")),
                    Lsb = int.Parse(GetAttributeValue(XML, idx, "lsb")),
                    ForceWhite = bool.Parse(GetAttributeValue(XML, idx, "forcewhite", defaultValue: "false", optional: true)),
                });
            }
        }

        void ReadKernpairs()
        {
            // <kernpair left="Ж" right="в" adjust="-1" />

            const string tag = "kernpair";
            int idx = 0;
            while(true)
            {
                idx = FindTag(XML, tag, idx);
                if(idx == -1)
                    break;

                int maxIdx = XML.IndexOf('>', idx);
                if(maxIdx == -1)
                    throw new Exception($"Can't find ending '>' after finding '<{tag} ' tag at idx={idx}");

                Kernpairs.Add(new Kernpair()
                {
                    Left = char.Parse(GetAttributeValue(XML, idx, "left")),
                    Right = char.Parse(GetAttributeValue(XML, idx, "right")),
                    Adjust = int.Parse(GetAttributeValue(XML, idx, "adjust")),
                });
            }
        }

        /// <summary>
        /// Finds a case-insensitive tag, skips XML comments.
        /// Returns index AFTER tag + a single space.
        /// <paramref name="tag"/> must not include the < nor any spaces
        /// </summary>
        public static int FindTag(string XML, string tag, int startIndex = 0)
        {
            for(int i = 0; i < tag.Length; i++)
            {
                char c = tag[i];
                if(c == '<' || c == '>' || c == ' ')
                    throw new Exception($"Tag='{tag}' must not contain <, > or spaces!");
            }

            const string commentStart = "<!--";
            const string commentEnd = "-->";

            while(true)
            {
                int idx = XML.IndexOf('<', startIndex);
                if(idx == -1)
                    return -1;

                TextPtr p = new TextPtr(XML, idx + 1);
                if(p.StartsWithCaseInsensitive(tag))
                {
                    startIndex = idx + 1 + tag.Length;
                    char afterTag = XML[startIndex];
                    if(!char.IsWhiteSpace(afterTag))
                        continue; // not followed by whitespace, could be a different tag, ignore

                    return startIndex + 1;
                }
                else if(p.StartsWith(commentStart))
                {
                    int endIdx = XML.IndexOf(commentEnd, p.Index);
                    if(endIdx == -1)
                        throw new Exception($"Invalid XML comment starts at {idx} and never gets closed");

                    startIndex = endIdx + commentEnd.Length;
                }
                else
                {
                    startIndex = idx + 1;
                }
            }
        }

        public static char ParseHTMLChar(string text)
        {
            char ch;
            if(char.TryParse(text, out ch))
                return ch;

            const StringComparison compare = StringComparison.OrdinalIgnoreCase;

            if(text.Equals("&amp;", compare)) return '&';
            if(text.Equals("&quot;", compare)) return '"';
            if(text.Equals("&apos;", compare)) return '\'';
            if(text.Equals("&gt;", compare)) return '>';
            if(text.Equals("&lt;", compare)) return '<';
            if(text.Equals("&tab;", compare)) return '\t';
            if(text.Equals("&newline;", compare)) return '\n';
            if(text.Equals("&nbsp;", compare)) return ' ';

            throw new Exception($"Unknown XML escaped character: {text}");
        }

        public static string GetAttributeValue(string XML, int startIndex, string name, string defaultValue = null, bool optional = false)
        {
            int attribIndex = -1;
            for(int i = startIndex; i < XML.Length; i++)
            {
                char c = XML[i];
                if(c == '>')
                    break;

                if(c == name[0])
                {
                    TextPtr p = new TextPtr(XML, i);
                    if(p.StartsWithCaseInsensitive(name))
                    {
                        p += name.Length;
                        if(!p.StartsWith("=\""))
                            throw new Exception($"Found attribute '{name}' but it is not followed by '=\"' !");

                        attribIndex = i + name.Length + 2;
                    }
                }
            }

            if(attribIndex == -1)
            {
                if(optional)
                    return defaultValue;
                else
                    throw new Exception($"Required attribute '{name}' not found, line: {GetLine(XML, startIndex)}");
            }

            int valueEnd = XML.IndexOf('"', attribIndex);
            if(valueEnd == -1)
                throw new Exception($"Ending quotes not found for attrib='{name}', content={GetLine(XML, startIndex)}");

            int valueLen = valueEnd - attribIndex;
            if(valueLen < 0)
                throw new Exception($"Negative length for attrib='{name}', content={GetLine(XML, startIndex)}");

            return XML.Substring(attribIndex, valueLen);
        }

        static string GetLine(string XML, int index)
        {
            return XML.Substring(index, new TextPtr(XML, index).FindEndOfLine().Index);
        }
    }
}
