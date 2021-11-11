﻿using Draygo.API;

namespace Digi.BuildInfo.Features.Fonts
{
    public class TextAPIFont_SEOutlined : TextAPIFont
    {
        // HACK: altered height to 32 from 37 so that it fits better with the built-in textAPI font (which is 30 height)

        protected override string FontName { get; } = "BI_SEOutlined";
        protected override string FontXML { get; } = @"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<font
	xmlns=""http://xna.microsoft.com/bitmapfont""
	xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
	xsi:schemaLocation=""http://xna.microsoft.com/bitmapfont bitmapfont.xsd""

	name=""FontDataPA"" base=""30"" height=""32""
	face=""Space Engineers"" size=""23""
	>
  <bitmaps>
    <bitmap id=""0"" name=""FontDataSEOutline-0.dds"" size=""1024x1024"" />
  </bitmaps>
  <glyphs>
    <glyph ch="" "" code=""0020"" bm=""0"" origin=""0,0"" size=""15x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""!"" code=""0021"" bm=""0"" origin=""15,0"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""&quot;"" code=""0022"" bm=""0"" origin=""39,0"" size=""25x45"" aw=""10"" lsb=""-7"" />
    <glyph ch=""#"" code=""0023"" bm=""0"" origin=""64,0"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""$"" code=""0024"" bm=""0"" origin=""99,0"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""%"" code=""0025"" bm=""0"" origin=""135,0"" size=""39x45"" aw=""24"" lsb=""-7"" />
    <glyph ch=""&amp;"" code=""0026"" bm=""0"" origin=""174,0"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""'"" code=""0027"" bm=""0"" origin=""209,0"" size=""22x45"" aw=""6"" lsb=""-7"" />
    <glyph ch=""("" code=""0028"" bm=""0"" origin=""231,0"" size=""24x45"" aw=""9"" lsb=""-7"" />
    <glyph ch="")"" code=""0029"" bm=""0"" origin=""255,0"" size=""24x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""*"" code=""002a"" bm=""0"" origin=""279,0"" size=""26x45"" aw=""11"" lsb=""-7"" />
    <glyph ch=""+"" code=""002b"" bm=""0"" origin=""305,0"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch="","" code=""002c"" bm=""0"" origin=""339,0"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""-"" code=""002d"" bm=""0"" origin=""364,0"" size=""25x45"" aw=""10"" lsb=""-7"" />
    <glyph ch=""."" code=""002e"" bm=""0"" origin=""389,0"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""/"" code=""002f"" bm=""0"" origin=""414,0"" size=""30x45"" aw=""14"" lsb=""-7"" />
    <glyph ch=""0"" code=""0030"" bm=""0"" origin=""444,0"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""1"" code=""0031"" bm=""0"" origin=""479,0"" size=""24x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""2"" code=""0032"" bm=""0"" origin=""503,0"" size=""34x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""3"" code=""0033"" bm=""0"" origin=""537,0"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""4"" code=""0034"" bm=""0"" origin=""570,0"" size=""34x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""5"" code=""0035"" bm=""0"" origin=""604,0"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""6"" code=""0036"" bm=""0"" origin=""639,0"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""7"" code=""0037"" bm=""0"" origin=""674,0"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""8"" code=""0038"" bm=""0"" origin=""705,0"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""9"" code=""0039"" bm=""0"" origin=""740,0"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch="":"" code=""003a"" bm=""0"" origin=""775,0"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch="";"" code=""003b"" bm=""0"" origin=""800,0"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""&lt;"" code=""003c"" bm=""0"" origin=""825,0"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""="" code=""003d"" bm=""0"" origin=""859,0"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""&gt;"" code=""003e"" bm=""0"" origin=""893,0"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""?"" code=""003f"" bm=""0"" origin=""927,0"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""@"" code=""0040"" bm=""0"" origin=""958,0"" size=""40x45"" aw=""25"" lsb=""-7"" />
    <glyph ch=""A"" code=""0041"" bm=""0"" origin=""0,45"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""B"" code=""0042"" bm=""0"" origin=""37,45"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""C"" code=""0043"" bm=""0"" origin=""74,45"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""D"" code=""0044"" bm=""0"" origin=""109,45"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""E"" code=""0045"" bm=""0"" origin=""146,45"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""F"" code=""0046"" bm=""0"" origin=""180,45"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""G"" code=""0047"" bm=""0"" origin=""212,45"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""H"" code=""0048"" bm=""0"" origin=""248,45"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""I"" code=""0049"" bm=""0"" origin=""283,45"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""J"" code=""004a"" bm=""0"" origin=""307,45"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""K"" code=""004b"" bm=""0"" origin=""338,45"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""L"" code=""004c"" bm=""0"" origin=""371,45"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""M"" code=""004d"" bm=""0"" origin=""401,45"" size=""42x45"" aw=""26"" lsb=""-7"" />
    <glyph ch=""N"" code=""004e"" bm=""0"" origin=""443,45"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""O"" code=""004f"" bm=""0"" origin=""480,45"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""P"" code=""0050"" bm=""0"" origin=""517,45"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""Q"" code=""0051"" bm=""0"" origin=""552,45"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""R"" code=""0052"" bm=""0"" origin=""589,45"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""S"" code=""0053"" bm=""0"" origin=""626,45"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""T"" code=""0054"" bm=""0"" origin=""663,45"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""U"" code=""0055"" bm=""0"" origin=""695,45"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""V"" code=""0056"" bm=""0"" origin=""731,45"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""W"" code=""0057"" bm=""0"" origin=""766,45"" size=""47x45"" aw=""31"" lsb=""-7"" />
    <glyph ch=""X"" code=""0058"" bm=""0"" origin=""813,45"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Y"" code=""0059"" bm=""0"" origin=""848,45"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""Z"" code=""005a"" bm=""0"" origin=""884,45"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""["" code=""005b"" bm=""0"" origin=""919,45"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""\"" code=""005c"" bm=""0"" origin=""944,45"" size=""28x45"" aw=""12"" lsb=""-7"" />
    <glyph ch=""]"" code=""005d"" bm=""0"" origin=""972,45"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""^"" code=""005e"" bm=""0"" origin=""0,90"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""_"" code=""005f"" bm=""0"" origin=""34,90"" size=""31x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""`"" code=""0060"" bm=""0"" origin=""65,90"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""a"" code=""0061"" bm=""0"" origin=""88,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""b"" code=""0062"" bm=""0"" origin=""121,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""c"" code=""0063"" bm=""0"" origin=""154,90"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""d"" code=""0064"" bm=""0"" origin=""186,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""e"" code=""0065"" bm=""0"" origin=""219,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""f"" code=""0066"" bm=""0"" origin=""252,90"" size=""24x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""g"" code=""0067"" bm=""0"" origin=""276,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""h"" code=""0068"" bm=""0"" origin=""309,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""i"" code=""0069"" bm=""0"" origin=""342,90"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""j"" code=""006a"" bm=""0"" origin=""365,90"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""k"" code=""006b"" bm=""0"" origin=""388,90"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""l"" code=""006c"" bm=""0"" origin=""420,90"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""m"" code=""006d"" bm=""0"" origin=""443,90"" size=""42x45"" aw=""27"" lsb=""-7"" />
    <glyph ch=""n"" code=""006e"" bm=""0"" origin=""485,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""o"" code=""006f"" bm=""0"" origin=""518,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""p"" code=""0070"" bm=""0"" origin=""551,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""q"" code=""0071"" bm=""0"" origin=""584,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""r"" code=""0072"" bm=""0"" origin=""617,90"" size=""25x45"" aw=""10"" lsb=""-7"" />
    <glyph ch=""s"" code=""0073"" bm=""0"" origin=""642,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""t"" code=""0074"" bm=""0"" origin=""675,90"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""u"" code=""0075"" bm=""0"" origin=""700,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""v"" code=""0076"" bm=""0"" origin=""733,90"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""w"" code=""0077"" bm=""0"" origin=""763,90"" size=""42x45"" aw=""27"" lsb=""-7"" />
    <glyph ch=""x"" code=""0078"" bm=""0"" origin=""805,90"" size=""31x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""y"" code=""0079"" bm=""0"" origin=""836,90"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""z"" code=""007a"" bm=""0"" origin=""869,90"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""{"" code=""007b"" bm=""0"" origin=""900,90"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""|"" code=""007c"" bm=""0"" origin=""925,90"" size=""22x45"" aw=""6"" lsb=""-7"" />
    <glyph ch=""}"" code=""007d"" bm=""0"" origin=""947,90"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""~"" code=""007e"" bm=""0"" origin=""972,90"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch="" "" code=""00a0"" bm=""0"" origin=""0,135"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""¡"" code=""00a1"" bm=""0"" origin=""23,135"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""¢"" code=""00a2"" bm=""0"" origin=""47,135"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""£"" code=""00a3"" bm=""0"" origin=""79,135"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""¤"" code=""00a4"" bm=""0"" origin=""112,135"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""¥"" code=""00a5"" bm=""0"" origin=""147,135"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""¦"" code=""00a6"" bm=""0"" origin=""182,135"" size=""22x45"" aw=""6"" lsb=""-7"" />
    <glyph ch=""§"" code=""00a7"" bm=""0"" origin=""204,135"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""¨"" code=""00a8"" bm=""0"" origin=""240,135"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""©"" code=""00a9"" bm=""0"" origin=""263,135"" size=""40x45"" aw=""25"" lsb=""-7"" />
    <glyph ch=""ª"" code=""00aa"" bm=""0"" origin=""303,135"" size=""26x45"" aw=""10"" lsb=""-7"" />
    <glyph ch=""«"" code=""00ab"" bm=""0"" origin=""329,135"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""¬"" code=""00ac"" bm=""0"" origin=""359,135"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""­"" code=""00ad"" bm=""0"" origin=""393,135"" size=""14x8"" aw=""10"" lsb=""-7"" />
    <glyph ch=""®"" code=""00ae"" bm=""0"" origin=""407,135"" size=""40x45"" aw=""25"" lsb=""-7"" />
    <glyph ch=""¯"" code=""00af"" bm=""0"" origin=""447,135"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""°"" code=""00b0"" bm=""0"" origin=""470,135"" size=""27x45"" aw=""12"" lsb=""-7"" />
    <glyph ch=""±"" code=""00b1"" bm=""0"" origin=""497,135"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""²"" code=""00b2"" bm=""0"" origin=""531,135"" size=""27x45"" aw=""11"" lsb=""-7"" />
    <glyph ch=""³"" code=""00b3"" bm=""0"" origin=""558,135"" size=""27x45"" aw=""11"" lsb=""-7"" />
    <glyph ch=""´"" code=""00b4"" bm=""0"" origin=""585,135"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""µ"" code=""00b5"" bm=""0"" origin=""608,135"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""¶"" code=""00b6"" bm=""0"" origin=""641,135"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""·"" code=""00b7"" bm=""0"" origin=""675,135"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""¸"" code=""00b8"" bm=""0"" origin=""700,135"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""¹"" code=""00b9"" bm=""0"" origin=""723,135"" size=""27x45"" aw=""11"" lsb=""-7"" />
    <glyph ch=""º"" code=""00ba"" bm=""0"" origin=""750,135"" size=""26x45"" aw=""10"" lsb=""-7"" />
    <glyph ch=""»"" code=""00bb"" bm=""0"" origin=""776,135"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""¼"" code=""00bc"" bm=""0"" origin=""806,135"" size=""43x45"" aw=""27"" lsb=""-7"" />
    <glyph ch=""½"" code=""00bd"" bm=""0"" origin=""849,135"" size=""45x45"" aw=""29"" lsb=""-7"" />
    <glyph ch=""¾"" code=""00be"" bm=""0"" origin=""894,135"" size=""43x45"" aw=""28"" lsb=""-7"" />
    <glyph ch=""¿"" code=""00bf"" bm=""0"" origin=""937,135"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""À"" code=""00c0"" bm=""0"" origin=""968,135"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Á"" code=""00c1"" bm=""0"" origin=""0,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Â"" code=""00c2"" bm=""0"" origin=""37,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Ã"" code=""00c3"" bm=""0"" origin=""74,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Ä"" code=""00c4"" bm=""0"" origin=""111,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Å"" code=""00c5"" bm=""0"" origin=""148,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Æ"" code=""00c6"" bm=""0"" origin=""185,180"" size=""47x45"" aw=""31"" lsb=""-7"" />
    <glyph ch=""Ç"" code=""00c7"" bm=""0"" origin=""232,180"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""È"" code=""00c8"" bm=""0"" origin=""267,180"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""É"" code=""00c9"" bm=""0"" origin=""301,180"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""Ê"" code=""00ca"" bm=""0"" origin=""335,180"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""Ë"" code=""00cb"" bm=""0"" origin=""369,180"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""Ì"" code=""00cc"" bm=""0"" origin=""403,180"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Í"" code=""00cd"" bm=""0"" origin=""427,180"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Î"" code=""00ce"" bm=""0"" origin=""451,180"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ï"" code=""00cf"" bm=""0"" origin=""475,180"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ð"" code=""00d0"" bm=""0"" origin=""499,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Ñ"" code=""00d1"" bm=""0"" origin=""536,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Ò"" code=""00d2"" bm=""0"" origin=""573,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Ó"" code=""00d3"" bm=""0"" origin=""610,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Ô"" code=""00d4"" bm=""0"" origin=""647,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Õ"" code=""00d5"" bm=""0"" origin=""684,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Ö"" code=""00d6"" bm=""0"" origin=""721,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""×"" code=""00d7"" bm=""0"" origin=""758,180"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""Ø"" code=""00d8"" bm=""0"" origin=""792,180"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Ù"" code=""00d9"" bm=""0"" origin=""829,180"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""Ú"" code=""00da"" bm=""0"" origin=""865,180"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""Û"" code=""00db"" bm=""0"" origin=""901,180"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""Ü"" code=""00dc"" bm=""0"" origin=""937,180"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""Ý"" code=""00dd"" bm=""0"" origin=""973,180"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Þ"" code=""00de"" bm=""0"" origin=""0,225"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ß"" code=""00df"" bm=""0"" origin=""35,225"" size=""34x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""à"" code=""00e0"" bm=""0"" origin=""69,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""á"" code=""00e1"" bm=""0"" origin=""102,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""â"" code=""00e2"" bm=""0"" origin=""135,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ã"" code=""00e3"" bm=""0"" origin=""168,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ä"" code=""00e4"" bm=""0"" origin=""201,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""å"" code=""00e5"" bm=""0"" origin=""234,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""æ"" code=""00e6"" bm=""0"" origin=""267,225"" size=""44x45"" aw=""28"" lsb=""-7"" />
    <glyph ch=""ç"" code=""00e7"" bm=""0"" origin=""311,225"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""è"" code=""00e8"" bm=""0"" origin=""343,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""é"" code=""00e9"" bm=""0"" origin=""376,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ê"" code=""00ea"" bm=""0"" origin=""409,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ë"" code=""00eb"" bm=""0"" origin=""442,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ì"" code=""00ec"" bm=""0"" origin=""475,225"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""í"" code=""00ed"" bm=""0"" origin=""498,225"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""î"" code=""00ee"" bm=""0"" origin=""521,225"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""ï"" code=""00ef"" bm=""0"" origin=""544,225"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""ð"" code=""00f0"" bm=""0"" origin=""567,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ñ"" code=""00f1"" bm=""0"" origin=""600,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ò"" code=""00f2"" bm=""0"" origin=""633,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ó"" code=""00f3"" bm=""0"" origin=""666,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ô"" code=""00f4"" bm=""0"" origin=""699,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""õ"" code=""00f5"" bm=""0"" origin=""732,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ö"" code=""00f6"" bm=""0"" origin=""765,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""÷"" code=""00f7"" bm=""0"" origin=""798,225"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""ø"" code=""00f8"" bm=""0"" origin=""832,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ù"" code=""00f9"" bm=""0"" origin=""865,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ú"" code=""00fa"" bm=""0"" origin=""898,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""û"" code=""00fb"" bm=""0"" origin=""931,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ü"" code=""00fc"" bm=""0"" origin=""964,225"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ý"" code=""00fd"" bm=""0"" origin=""0,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""þ"" code=""00fe"" bm=""0"" origin=""33,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ÿ"" code=""00ff"" bm=""0"" origin=""66,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ā"" code=""0100"" bm=""0"" origin=""99,270"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ā"" code=""0101"" bm=""0"" origin=""134,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ă"" code=""0102"" bm=""0"" origin=""167,270"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ă"" code=""0103"" bm=""0"" origin=""204,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ą"" code=""0104"" bm=""0"" origin=""237,270"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ą"" code=""0105"" bm=""0"" origin=""274,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ć"" code=""0106"" bm=""0"" origin=""307,270"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""ć"" code=""0107"" bm=""0"" origin=""342,270"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""Ĉ"" code=""0108"" bm=""0"" origin=""374,270"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""ĉ"" code=""0109"" bm=""0"" origin=""409,270"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""Ċ"" code=""010a"" bm=""0"" origin=""441,270"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""ċ"" code=""010b"" bm=""0"" origin=""476,270"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""Č"" code=""010c"" bm=""0"" origin=""508,270"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""č"" code=""010d"" bm=""0"" origin=""543,270"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""Ď"" code=""010e"" bm=""0"" origin=""575,270"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ď"" code=""010f"" bm=""0"" origin=""612,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Đ"" code=""0110"" bm=""0"" origin=""645,270"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""đ"" code=""0111"" bm=""0"" origin=""682,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ē"" code=""0112"" bm=""0"" origin=""715,270"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""ē"" code=""0113"" bm=""0"" origin=""749,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ĕ"" code=""0114"" bm=""0"" origin=""782,270"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""ĕ"" code=""0115"" bm=""0"" origin=""816,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ė"" code=""0116"" bm=""0"" origin=""849,270"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""ė"" code=""0117"" bm=""0"" origin=""883,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ę"" code=""0118"" bm=""0"" origin=""916,270"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""ę"" code=""0119"" bm=""0"" origin=""950,270"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ě"" code=""011a"" bm=""0"" origin=""983,270"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""ě"" code=""011b"" bm=""0"" origin=""0,315"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ĝ"" code=""011c"" bm=""0"" origin=""33,315"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ĝ"" code=""011d"" bm=""0"" origin=""69,315"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ğ"" code=""011e"" bm=""0"" origin=""102,315"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ğ"" code=""011f"" bm=""0"" origin=""138,315"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ġ"" code=""0120"" bm=""0"" origin=""171,315"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ġ"" code=""0121"" bm=""0"" origin=""207,315"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ģ"" code=""0122"" bm=""0"" origin=""240,315"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ģ"" code=""0123"" bm=""0"" origin=""276,315"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ĥ"" code=""0124"" bm=""0"" origin=""309,315"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ĥ"" code=""0125"" bm=""0"" origin=""344,315"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ħ"" code=""0126"" bm=""0"" origin=""377,315"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ħ"" code=""0127"" bm=""0"" origin=""412,315"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ĩ"" code=""0128"" bm=""0"" origin=""445,315"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""ĩ"" code=""0129"" bm=""0"" origin=""469,315"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ī"" code=""012a"" bm=""0"" origin=""492,315"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""ī"" code=""012b"" bm=""0"" origin=""516,315"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Į"" code=""012e"" bm=""0"" origin=""539,315"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""į"" code=""012f"" bm=""0"" origin=""563,315"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""İ"" code=""0130"" bm=""0"" origin=""586,315"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""ı"" code=""0131"" bm=""0"" origin=""610,315"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ĳ"" code=""0132"" bm=""0"" origin=""633,315"" size=""40x45"" aw=""24"" lsb=""-7"" />
    <glyph ch=""ĳ"" code=""0133"" bm=""0"" origin=""673,315"" size=""29x45"" aw=""14"" lsb=""-7"" />
    <glyph ch=""Ĵ"" code=""0134"" bm=""0"" origin=""702,315"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""ĵ"" code=""0135"" bm=""0"" origin=""733,315"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ķ"" code=""0136"" bm=""0"" origin=""756,315"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ķ"" code=""0137"" bm=""0"" origin=""789,315"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ĺ"" code=""0139"" bm=""0"" origin=""821,315"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""ĺ"" code=""013a"" bm=""0"" origin=""851,315"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ļ"" code=""013b"" bm=""0"" origin=""874,315"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""ļ"" code=""013c"" bm=""0"" origin=""904,315"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ľ"" code=""013d"" bm=""0"" origin=""927,315"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""ľ"" code=""013e"" bm=""0"" origin=""957,315"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ŀ"" code=""013f"" bm=""0"" origin=""980,315"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""ŀ"" code=""0140"" bm=""0"" origin=""0,360"" size=""26x45"" aw=""10"" lsb=""-7"" />
    <glyph ch=""Ł"" code=""0141"" bm=""0"" origin=""26,360"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""ł"" code=""0142"" bm=""0"" origin=""56,360"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ń"" code=""0143"" bm=""0"" origin=""79,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ń"" code=""0144"" bm=""0"" origin=""116,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ņ"" code=""0145"" bm=""0"" origin=""149,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ņ"" code=""0146"" bm=""0"" origin=""186,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ň"" code=""0147"" bm=""0"" origin=""219,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ň"" code=""0148"" bm=""0"" origin=""256,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ŉ"" code=""0149"" bm=""0"" origin=""289,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ō"" code=""014c"" bm=""0"" origin=""322,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ō"" code=""014d"" bm=""0"" origin=""359,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ŏ"" code=""014e"" bm=""0"" origin=""392,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ŏ"" code=""014f"" bm=""0"" origin=""429,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ő"" code=""0150"" bm=""0"" origin=""462,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ő"" code=""0151"" bm=""0"" origin=""499,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Œ"" code=""0152"" bm=""0"" origin=""532,360"" size=""47x45"" aw=""31"" lsb=""-7"" />
    <glyph ch=""œ"" code=""0153"" bm=""0"" origin=""579,360"" size=""44x45"" aw=""28"" lsb=""-7"" />
    <glyph ch=""Ŕ"" code=""0154"" bm=""0"" origin=""623,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ŕ"" code=""0155"" bm=""0"" origin=""660,360"" size=""25x45"" aw=""10"" lsb=""-7"" />
    <glyph ch=""Ŗ"" code=""0156"" bm=""0"" origin=""685,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ŗ"" code=""0157"" bm=""0"" origin=""722,360"" size=""25x45"" aw=""10"" lsb=""-7"" />
    <glyph ch=""Ř"" code=""0158"" bm=""0"" origin=""747,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ř"" code=""0159"" bm=""0"" origin=""784,360"" size=""25x45"" aw=""10"" lsb=""-7"" />
    <glyph ch=""Ś"" code=""015a"" bm=""0"" origin=""809,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ś"" code=""015b"" bm=""0"" origin=""846,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ŝ"" code=""015c"" bm=""0"" origin=""879,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ŝ"" code=""015d"" bm=""0"" origin=""916,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ş"" code=""015e"" bm=""0"" origin=""949,360"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ş"" code=""015f"" bm=""0"" origin=""986,360"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Š"" code=""0160"" bm=""0"" origin=""0,405"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""š"" code=""0161"" bm=""0"" origin=""37,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ţ"" code=""0162"" bm=""0"" origin=""70,405"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ţ"" code=""0163"" bm=""0"" origin=""102,405"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""Ť"" code=""0164"" bm=""0"" origin=""127,405"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ť"" code=""0165"" bm=""0"" origin=""159,405"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""Ŧ"" code=""0166"" bm=""0"" origin=""184,405"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ŧ"" code=""0167"" bm=""0"" origin=""216,405"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""Ũ"" code=""0168"" bm=""0"" origin=""241,405"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ũ"" code=""0169"" bm=""0"" origin=""277,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ū"" code=""016a"" bm=""0"" origin=""310,405"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ū"" code=""016b"" bm=""0"" origin=""346,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ŭ"" code=""016c"" bm=""0"" origin=""379,405"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ŭ"" code=""016d"" bm=""0"" origin=""415,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ů"" code=""016e"" bm=""0"" origin=""448,405"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ů"" code=""016f"" bm=""0"" origin=""484,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ű"" code=""0170"" bm=""0"" origin=""517,405"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ű"" code=""0171"" bm=""0"" origin=""553,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ų"" code=""0172"" bm=""0"" origin=""586,405"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ų"" code=""0173"" bm=""0"" origin=""622,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ŵ"" code=""0174"" bm=""0"" origin=""655,405"" size=""47x45"" aw=""31"" lsb=""-7"" />
    <glyph ch=""ŵ"" code=""0175"" bm=""0"" origin=""702,405"" size=""42x45"" aw=""27"" lsb=""-7"" />
    <glyph ch=""Ŷ"" code=""0176"" bm=""0"" origin=""744,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ŷ"" code=""0177"" bm=""0"" origin=""777,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ÿ"" code=""0178"" bm=""0"" origin=""810,405"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ź"" code=""0179"" bm=""0"" origin=""843,405"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""ź"" code=""017a"" bm=""0"" origin=""878,405"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""Ż"" code=""017b"" bm=""0"" origin=""909,405"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""ż"" code=""017c"" bm=""0"" origin=""944,405"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""Ž"" code=""017d"" bm=""0"" origin=""975,405"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""ž"" code=""017e"" bm=""0"" origin=""0,450"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""ƒ"" code=""0192"" bm=""0"" origin=""31,450"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Ș"" code=""0218"" bm=""0"" origin=""66,450"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""ș"" code=""0219"" bm=""0"" origin=""103,450"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ț"" code=""021a"" bm=""0"" origin=""136,450"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ț"" code=""021b"" bm=""0"" origin=""168,450"" size=""25x45"" aw=""9"" lsb=""-7"" />
    <glyph ch=""ˆ"" code=""02c6"" bm=""0"" origin=""193,450"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""ˇ"" code=""02c7"" bm=""0"" origin=""216,450"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""ˉ"" code=""02c9"" bm=""0"" origin=""239,450"" size=""22x45"" aw=""6"" lsb=""-7"" />
    <glyph ch=""˘"" code=""02d8"" bm=""0"" origin=""261,450"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""˙"" code=""02d9"" bm=""0"" origin=""284,450"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""˚"" code=""02da"" bm=""0"" origin=""307,450"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""˛"" code=""02db"" bm=""0"" origin=""330,450"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""˜"" code=""02dc"" bm=""0"" origin=""353,450"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""˝"" code=""02dd"" bm=""0"" origin=""376,450"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ё"" code=""0401"" bm=""0"" origin=""399,450"" size=""34x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Ѓ"" code=""0403"" bm=""0"" origin=""433,450"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""Є"" code=""0404"" bm=""0"" origin=""465,450"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""Ѕ"" code=""0405"" bm=""0"" origin=""499,450"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""І"" code=""0406"" bm=""0"" origin=""536,450"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ї"" code=""0407"" bm=""0"" origin=""560,450"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""Ј"" code=""0408"" bm=""0"" origin=""584,450"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""Љ"" code=""0409"" bm=""0"" origin=""615,450"" size=""43x45"" aw=""28"" lsb=""-7"" />
    <glyph ch=""Њ"" code=""040a"" bm=""0"" origin=""658,450"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""Ќ"" code=""040c"" bm=""0"" origin=""695,450"" size=""34x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Ў"" code=""040e"" bm=""0"" origin=""729,450"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Џ"" code=""040f"" bm=""0"" origin=""761,450"" size=""33x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""А"" code=""0410"" bm=""0"" origin=""794,450"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Б"" code=""0411"" bm=""0"" origin=""829,450"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""В"" code=""0412"" bm=""0"" origin=""864,450"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Г"" code=""0413"" bm=""0"" origin=""899,450"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""Д"" code=""0414"" bm=""0"" origin=""929,450"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Е"" code=""0415"" bm=""0"" origin=""964,450"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""Ж"" code=""0416"" bm=""0"" origin=""0,495"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""З"" code=""0417"" bm=""0"" origin=""37,495"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""И"" code=""0418"" bm=""0"" origin=""70,495"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Й"" code=""0419"" bm=""0"" origin=""105,495"" size=""34x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""К"" code=""041a"" bm=""0"" origin=""139,495"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Л"" code=""041b"" bm=""0"" origin=""172,495"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""М"" code=""041c"" bm=""0"" origin=""205,495"" size=""42x45"" aw=""26"" lsb=""-7"" />
    <glyph ch=""Н"" code=""041d"" bm=""0"" origin=""247,495"" size=""33x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""О"" code=""041e"" bm=""0"" origin=""280,495"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""П"" code=""041f"" bm=""0"" origin=""315,495"" size=""34x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Р"" code=""0420"" bm=""0"" origin=""349,495"" size=""34x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""С"" code=""0421"" bm=""0"" origin=""383,495"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Т"" code=""0422"" bm=""0"" origin=""418,495"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""У"" code=""0423"" bm=""0"" origin=""453,495"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Ф"" code=""0424"" bm=""0"" origin=""488,495"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""Х"" code=""0425"" bm=""0"" origin=""524,495"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Ц"" code=""0426"" bm=""0"" origin=""559,495"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""Ч"" code=""0427"" bm=""0"" origin=""595,495"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""Ш"" code=""0428"" bm=""0"" origin=""627,495"" size=""42x45"" aw=""26"" lsb=""-7"" />
    <glyph ch=""Щ"" code=""0429"" bm=""0"" origin=""669,495"" size=""45x45"" aw=""29"" lsb=""-7"" />
    <glyph ch=""Ъ"" code=""042a"" bm=""0"" origin=""714,495"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""Ы"" code=""042b"" bm=""0"" origin=""749,495"" size=""40x45"" aw=""24"" lsb=""-7"" />
    <glyph ch=""Ь"" code=""042c"" bm=""0"" origin=""789,495"" size=""34x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""Э"" code=""042d"" bm=""0"" origin=""823,495"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""Ю"" code=""042e"" bm=""0"" origin=""857,495"" size=""42x45"" aw=""27"" lsb=""-7"" />
    <glyph ch=""Я"" code=""042f"" bm=""0"" origin=""899,495"" size=""35x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""а"" code=""0430"" bm=""0"" origin=""934,495"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""б"" code=""0431"" bm=""0"" origin=""966,495"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""в"" code=""0432"" bm=""0"" origin=""0,540"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""г"" code=""0433"" bm=""0"" origin=""31,540"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""д"" code=""0434"" bm=""0"" origin=""61,540"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""е"" code=""0435"" bm=""0"" origin=""94,540"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ж"" code=""0436"" bm=""0"" origin=""127,540"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""з"" code=""0437"" bm=""0"" origin=""163,540"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""и"" code=""0438"" bm=""0"" origin=""193,540"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""й"" code=""0439"" bm=""0"" origin=""225,540"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""к"" code=""043a"" bm=""0"" origin=""257,540"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""л"" code=""043b"" bm=""0"" origin=""289,540"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""м"" code=""043c"" bm=""0"" origin=""319,540"" size=""41x45"" aw=""25"" lsb=""-7"" />
    <glyph ch=""н"" code=""043d"" bm=""0"" origin=""360,540"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""о"" code=""043e"" bm=""0"" origin=""391,540"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""п"" code=""043f"" bm=""0"" origin=""423,540"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""р"" code=""0440"" bm=""0"" origin=""455,540"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""с"" code=""0441"" bm=""0"" origin=""487,540"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""т"" code=""0442"" bm=""0"" origin=""519,540"" size=""30x45"" aw=""14"" lsb=""-7"" />
    <glyph ch=""у"" code=""0443"" bm=""0"" origin=""549,540"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ф"" code=""0444"" bm=""0"" origin=""582,540"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch=""х"" code=""0445"" bm=""0"" origin=""619,540"" size=""31x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""ц"" code=""0446"" bm=""0"" origin=""650,540"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ч"" code=""0447"" bm=""0"" origin=""683,540"" size=""31x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""ш"" code=""0448"" bm=""0"" origin=""714,540"" size=""41x45"" aw=""25"" lsb=""-7"" />
    <glyph ch=""щ"" code=""0449"" bm=""0"" origin=""755,540"" size=""42x45"" aw=""27"" lsb=""-7"" />
    <glyph ch=""ъ"" code=""044a"" bm=""0"" origin=""797,540"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""ы"" code=""044b"" bm=""0"" origin=""828,540"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""ь"" code=""044c"" bm=""0"" origin=""864,540"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""э"" code=""044d"" bm=""0"" origin=""895,540"" size=""30x45"" aw=""14"" lsb=""-7"" />
    <glyph ch=""ю"" code=""044e"" bm=""0"" origin=""925,540"" size=""39x45"" aw=""23"" lsb=""-7"" />
    <glyph ch=""я"" code=""044f"" bm=""0"" origin=""964,540"" size=""32x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ё"" code=""0451"" bm=""0"" origin=""0,585"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ђ"" code=""0452"" bm=""0"" origin=""33,585"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ѓ"" code=""0453"" bm=""0"" origin=""66,585"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""є"" code=""0454"" bm=""0"" origin=""97,585"" size=""30x45"" aw=""14"" lsb=""-7"" />
    <glyph ch=""ѕ"" code=""0455"" bm=""0"" origin=""127,585"" size=""32x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""і"" code=""0456"" bm=""0"" origin=""159,585"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""ї"" code=""0457"" bm=""0"" origin=""182,585"" size=""23x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""ј"" code=""0458"" bm=""0"" origin=""205,585"" size=""22x45"" aw=""7"" lsb=""-7"" />
    <glyph ch=""љ"" code=""0459"" bm=""0"" origin=""227,585"" size=""38x45"" aw=""22"" lsb=""-7"" />
    <glyph ch=""њ"" code=""045a"" bm=""0"" origin=""265,585"" size=""41x45"" aw=""25"" lsb=""-7"" />
    <glyph ch=""ћ"" code=""045b"" bm=""0"" origin=""306,585"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""ќ"" code=""045c"" bm=""0"" origin=""339,585"" size=""31x45"" aw=""16"" lsb=""-7"" />
    <glyph ch=""ў"" code=""045e"" bm=""0"" origin=""370,585"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""џ"" code=""045f"" bm=""0"" origin=""403,585"" size=""33x45"" aw=""17"" lsb=""-7"" />
    <glyph ch=""Ґ"" code=""0490"" bm=""0"" origin=""436,585"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""ґ"" code=""0491"" bm=""0"" origin=""466,585"" size=""29x45"" aw=""13"" lsb=""-7"" />
    <glyph ch=""–"" code=""2013"" bm=""0"" origin=""495,585"" size=""30x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""—"" code=""2014"" bm=""0"" origin=""525,585"" size=""46x45"" aw=""31"" lsb=""-7"" />
    <glyph ch=""‘"" code=""2018"" bm=""0"" origin=""571,585"" size=""22x45"" aw=""6"" lsb=""-7"" />
    <glyph ch=""’"" code=""2019"" bm=""0"" origin=""593,585"" size=""22x45"" aw=""6"" lsb=""-7"" />
    <glyph ch=""‚"" code=""201a"" bm=""0"" origin=""615,585"" size=""22x45"" aw=""6"" lsb=""-7"" />
    <glyph ch=""“"" code=""201c"" bm=""0"" origin=""637,585"" size=""27x45"" aw=""12"" lsb=""-7"" />
    <glyph ch=""”"" code=""201d"" bm=""0"" origin=""664,585"" size=""27x45"" aw=""12"" lsb=""-7"" />
    <glyph ch=""„"" code=""201e"" bm=""0"" origin=""691,585"" size=""27x45"" aw=""12"" lsb=""-7"" />
    <glyph ch=""†"" code=""2020"" bm=""0"" origin=""718,585"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""‡"" code=""2021"" bm=""0"" origin=""754,585"" size=""36x45"" aw=""20"" lsb=""-7"" />
    <glyph ch=""•"" code=""2022"" bm=""0"" origin=""790,585"" size=""31x45"" aw=""15"" lsb=""-7"" />
    <glyph ch=""…"" code=""2026"" bm=""0"" origin=""821,585"" size=""47x45"" aw=""31"" lsb=""-7"" />
    <glyph ch=""‰"" code=""2030"" bm=""0"" origin=""868,585"" size=""47x45"" aw=""31"" lsb=""-7"" />
    <glyph ch=""‹"" code=""2039"" bm=""0"" origin=""915,585"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""›"" code=""203a"" bm=""0"" origin=""939,585"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""€"" code=""20ac"" bm=""0"" origin=""963,585"" size=""35x45"" aw=""19"" lsb=""-7"" />
    <glyph ch=""™"" code=""2122"" bm=""0"" origin=""0,630"" size=""46x45"" aw=""30"" lsb=""-7"" />
    <glyph ch=""−"" code=""2212"" bm=""0"" origin=""46,630"" size=""34x45"" aw=""18"" lsb=""-7"" />
    <glyph ch=""∙"" code=""2219"" bm=""0"" origin=""80,630"" size=""24x45"" aw=""8"" lsb=""-7"" />
    <glyph ch=""□"" code=""25a1"" bm=""0"" origin=""104,630"" size=""37x45"" aw=""21"" lsb=""-7"" />
    <glyph ch="""" code=""e001"" bm=""0"" origin=""141,630"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e002"" bm=""0"" origin=""207,630"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e003"" bm=""0"" origin=""273,630"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e004"" bm=""0"" origin=""339,630"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e005"" bm=""0"" origin=""405,630"" size=""66x60"" aw=""46"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e006"" bm=""0"" origin=""471,630"" size=""66x60"" aw=""46"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e007"" bm=""0"" origin=""537,630"" size=""66x60"" aw=""37"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e008"" bm=""0"" origin=""603,630"" size=""66x60"" aw=""37"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e009"" bm=""0"" origin=""669,630"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e00a"" bm=""0"" origin=""735,630"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e00b"" bm=""0"" origin=""801,630"" size=""66x60"" aw=""57"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e00c"" bm=""0"" origin=""867,630"" size=""66x60"" aw=""57"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e00d"" bm=""0"" origin=""933,630"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e00e"" bm=""0"" origin=""0,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e00f"" bm=""0"" origin=""66,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e010"" bm=""0"" origin=""132,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e011"" bm=""0"" origin=""198,690"" size=""66x60"" aw=""37"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e012"" bm=""0"" origin=""264,690"" size=""66x60"" aw=""46"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e013"" bm=""0"" origin=""330,690"" size=""66x60"" aw=""37"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e014"" bm=""0"" origin=""396,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e015"" bm=""0"" origin=""462,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e016"" bm=""0"" origin=""528,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e017"" bm=""0"" origin=""594,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e018"" bm=""0"" origin=""660,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e019"" bm=""0"" origin=""726,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e020"" bm=""0"" origin=""792,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e021"" bm=""0"" origin=""858,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e022"" bm=""0"" origin=""924,690"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e023"" bm=""0"" origin=""0,750"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e024"" bm=""0"" origin=""66,750"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e025"" bm=""0"" origin=""132,750"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e026"" bm=""0"" origin=""198,750"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
    <glyph ch="""" code=""e027"" bm=""0"" origin=""264,750"" size=""66x60"" aw=""45"" lsb=""-10"" forcewhite=""true"" />
  </glyphs>
  <kernpairs>
    <kernpair left=""Ж"" right=""в"" adjust=""-1"" />
    <kernpair left=""Ж"" right=""ж"" adjust=""-1"" />
    <kernpair left=""ъ"" right=""в"" adjust=""-2"" />
    <kernpair left=""ь"" right=""в"" adjust=""-2"" />
    <kernpair left=""Ґ"" right="","" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""-"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""."" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""‚"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""„"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""…"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""–"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""—"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""š"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""œ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""à"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""á"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""â"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ã"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ä"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""å"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""æ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ç"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""è"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""é"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ê"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ë"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ð"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ñ"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ò"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ó"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ô"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""õ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ö"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ø"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ù"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ú"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""û"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ü"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ž"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ā"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ă"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ą"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ć"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ĉ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""č"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ď"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""đ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ě"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ē"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ĕ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ę"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ĝ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ğ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ń"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ň"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ō"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ŏ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ŕ"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ř"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ś"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ŝ"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ş"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ș"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""ũ"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ū"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ŭ"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ů"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ų"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ŵ"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ź"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""Ц"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""Ш"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""Ы"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""Ю"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""б"" adjust=""-2"" />
    <kernpair left=""Ґ"" right=""в"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""ж"" adjust=""-1"" />
    <kernpair left=""Ґ"" right=""н"" adjust=""-2"" />
  </kernpairs>
</font>";
    }
}