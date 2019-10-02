using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Sandbox.Game;
using VRage.Game;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo
{
    public class Constants : ModComponent
    {
        public readonly Vector2 BLOCKINFO_SIZE = new Vector2(0.02164f, 0.00076f);
        public const float ASPECT_RATIO_54_FIX = 0.938f;
        public const float BLOCKINFO_TEXT_PADDING = 0.001f;

        public readonly MyStringHash COMPUTER_COMPONENT_NAME = MyStringHash.GetOrCompute("Computer");

        public const bool BLOCKPICKER_IN_MP = false; // FIXME now block picker does nothing in MP, see comments in: https://support.keenswh.com/spaceengineers/general/topic/187-2-modapi-settoolbarslottoitem-causes-everyone-in-server-to-disconnect
        public const string BLOCKPICKER_DISABLED_CONFIG = "NOTE: This feature is disabled in MP because of issues, see: https://support.keenswh.com/spaceengineers/general/topic/187-2-modapi-settoolbarslottoitem-causes-everyone-in-server-to-disconnect";
        public const string BLOCKPICKER_DISABLED_CHAT = "Pick block feature disabled in MP because of issues, see workshop page for details.";

        public static bool EXPORT_VANILLA_BLOCKS = false; // used for exporting vanilla block IDs for AnalyseShip's hardcoded list.

        public readonly HashSet<MyObjectBuilderType> DEFAULT_ALLOWED_TYPES = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer) // used in inventory formatting if type argument is null
        {
            typeof(MyObjectBuilder_Ore),
            typeof(MyObjectBuilder_Ingot),
            typeof(MyObjectBuilder_Component)
        };

        public readonly MyStringId[] CONTROL_SLOTS = new MyStringId[]
        {
            MyControlsSpace.SLOT0,
            MyControlsSpace.SLOT1,
            MyControlsSpace.SLOT2,
            MyControlsSpace.SLOT3,
            MyControlsSpace.SLOT4,
            MyControlsSpace.SLOT5,
            MyControlsSpace.SLOT6,
            MyControlsSpace.SLOT7,
            MyControlsSpace.SLOT8,
            MyControlsSpace.SLOT9,
        };

        public Constants(BuildInfoMod main) : base(main)
        {
            ComputeCharacterSizes();
            ComputeResourceGroups();
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
        }

        #region Resource group priorities
        public int resourceSinkGroups = 0;
        public int resourceSourceGroups = 0;
        public readonly Dictionary<MyStringHash, ResourceGroupData> resourceGroupPriority
                  = new Dictionary<MyStringHash, ResourceGroupData>(MyStringHash.Comparer);

        private void ComputeResourceGroups()
        {
            resourceGroupPriority.Clear();
            resourceSourceGroups = 0;
            resourceSinkGroups = 0;

            var groupDefs = MyDefinitionManager.Static.GetDefinitionsOfType<MyResourceDistributionGroupDefinition>();
            var orderedGroupsEnumerable = groupDefs.OrderBy((def) => def.Priority);

            // compact priorities into an ordered number.
            foreach(var group in orderedGroupsEnumerable)
            {
                int priority = 0;

                if(group.IsSource)
                {
                    resourceSourceGroups++;
                    priority = resourceSourceGroups;
                }
                else
                {
                    resourceSinkGroups++;
                    priority = resourceSinkGroups;
                }

                resourceGroupPriority.Add(group.Id.SubtypeId, new ResourceGroupData(group, priority));
            }
        }

        public struct ResourceGroupData
        {
            public readonly MyResourceDistributionGroupDefinition Def;
            public readonly int Priority;

            public ResourceGroupData(MyResourceDistributionGroupDefinition def, int priority)
            {
                Def = def;
                Priority = priority;
            }
        }
        #endregion Resource group priorities

        #region Character sizes for padding HUD notifications
        public readonly Dictionary<char, int> charSize = new Dictionary<char, int>();

        private void ComputeCharacterSizes()
        {
            charSize.Clear();

            // generated from fonts/white_shadow/FontData.xml
            AddCharsSize(" !I`ijl ¡¨¯´¸ÌÍÎÏìíîïĨĩĪīĮįİıĵĺļľłˆˇ˘˙˚˛˜˝ІЇії‹›∙", 8);
            AddCharsSize("\"-rª­ºŀŕŗř", 10);
            AddCharsSize("#0245689CXZ¤¥ÇßĆĈĊČŹŻŽƒЁЌАБВДИЙПРСТУХЬ€", 19);
            AddCharsSize("$&GHPUVY§ÙÚÛÜÞĀĜĞĠĢĤĦŨŪŬŮŰŲОФЦЪЯжы†‡", 20);
            AddCharsSize("%ĲЫ", 24);
            AddCharsSize("'|¦ˉ‘’‚", 6);
            AddCharsSize("(),.1:;[]ft{}·ţťŧț", 9);
            AddCharsSize("*²³¹", 11);
            AddCharsSize("+<=>E^~¬±¶ÈÉÊË×÷ĒĔĖĘĚЄЏЕНЭ−", 18);
            AddCharsSize("/ĳтэє", 14);
            AddCharsSize("3FKTabdeghknopqsuy£µÝàáâãäåèéêëðñòóôõöøùúûüýþÿāăąďđēĕėęěĝğġģĥħĶķńņňŉōŏőśŝşšŢŤŦũūŭůűųŶŷŸșȚЎЗКЛбдекруцяёђћўџ", 17);
            AddCharsSize("7?Jcz¢¿çćĉċčĴźżžЃЈЧавийнопсъьѓѕќ", 16);
            AddCharsSize("@©®мшњ", 25);
            AddCharsSize("ABDNOQRSÀÁÂÃÄÅÐÑÒÓÔÕÖØĂĄĎĐŃŅŇŌŎŐŔŖŘŚŜŞŠȘЅЊЖф□", 21);
            AddCharsSize("L_vx«»ĹĻĽĿŁГгзлхчҐ–•", 15);
            AddCharsSize("MМШ", 26);
            AddCharsSize("WÆŒŴ—…‰", 31);
            AddCharsSize("\\°“”„", 12);
            AddCharsSize("mw¼ŵЮщ", 27);
            AddCharsSize("½Щ", 29);
            AddCharsSize("¾æœЉ", 28);
            AddCharsSize("ю", 23);
            AddCharsSize("ј", 7);
            AddCharsSize("љ", 22);
            AddCharsSize("ґ", 13);
            AddCharsSize("™", 30);
            AddCharsSize("", 40);
            AddCharsSize("", 41);
            AddCharsSize("", 32);
            AddCharsSize("", 34);
        }

        private void AddCharsSize(string chars, int size)
        {
            for(int i = 0; i < chars.Length; i++)
            {
                charSize.Add(chars[i], size);
            }
        }
        #endregion Character sizes for padding HUD notifications
    }
}
