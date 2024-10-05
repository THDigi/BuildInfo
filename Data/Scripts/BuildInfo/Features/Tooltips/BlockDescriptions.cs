using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class BlockDescriptions : ModComponent
    {
        public readonly Dictionary<MyDefinitionId, string> Descriptions = new Dictionary<MyDefinitionId, string>(MyDefinitionId.Comparer);

        public readonly HashSet<MyDefinitionId> IgnoreBlockDefs = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        StringBuilder SB = new StringBuilder(512);

        // HACK: fill in args using the help of game code instead of cloning it.
        MyHudBlockInfo DescriptionParser = new MyHudBlockInfo();

        void DisposeTempObjects()
        {
        }

        public BlockDescriptions(BuildInfoMod main) : base(main)
        {
            Main.TooltipHandler.Setup += Setup;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.TooltipHandler.Setup -= Setup;
        }

        void Setup(bool generate)
        {
            foreach(MyCubeBlockDefinition blockDef in Main.Caches.BlockDefs)
            {
                try
                {
                    HandleDescription(blockDef, generate);
                }
                catch(Exception e)
                {
                    string msg = $"Error modifying description for block: {blockDef?.Id.ToString()}";
                    Log.Error($"{msg}\n{e}", msg);
                }
            }

            if(generate)
            {
                DisposeTempObjects();
            }
        }

        void HandleDescription(MyCubeBlockDefinition blockDef, bool generate)
        {
            string description = null;
            if(generate)
            {
                // generate tooltips and cache them alone
                SB.Clear();
                GenerateDescription(SB, blockDef);
                if(SB.Length > 5)
                {
                    description = SB.ToString();
                    Descriptions[blockDef.Id] = description;
                }
            }
            else
            {
                // retrieve cached tooltip string
                description = Descriptions.GetValueOrDefault(blockDef.Id, null);
            }

            SB.Clear();

            if(blockDef.DescriptionArgs != null)
            {
                // also fixes description in G-menu not getting any replacements.
                DescriptionParser.SetContextHelp(blockDef);
                SB.Append(DescriptionParser.ContextHelp);
                blockDef.DescriptionArgs = null;
            }
            else
            {
                SB.Append(blockDef.DescriptionText); // get existing text, then replace/append to it as needed
            }

            if(description != null)
            {
                // tooltip likely contains the cached tooltip, get rid of it.
                if(SB.Length >= description.Length)
                {
                    SB.Replace(description, "");
                }

                if(Main.Config.ItemTooltipAdditions.Value)
                {
                    SB.Append(description);
                }
            }

            #region internal info
            const string IdLabel = "Id:\u00A0"; // non-space space to not count as whitespace for splitting the words.
            const string IdTypeLabel = "IdType:\u00A0";
            const string IdSubtypeLabel = "IdSub:\u00A0";

            if(SB.Length > 0)
            {
                SB.RemoveLineStartsWith(IdLabel);
                SB.RemoveLineStartsWith(IdTypeLabel);
                SB.RemoveLineStartsWith(IdSubtypeLabel);
            }

            if(Main.Config.InternalInfo.Value)
            {
                SB.Append('\n');

                string typeIdString = blockDef.Id.TypeId.ToString();
                string subtypeIdString = blockDef.Id.SubtypeName;

                const int obPrefixLen = 16; // "MyObjectBuilder_".Length;
                int shortTypeIdLength = (typeIdString.Length - obPrefixLen);

                const int MaxWidth = 32;
                int totalWidth = shortTypeIdLength + 1 + subtypeIdString.Length;
                if(totalWidth > MaxWidth)
                {
                    SB.Append(IdTypeLabel).Append(typeIdString, obPrefixLen, shortTypeIdLength).Append('\n');
                    SB.Append(IdSubtypeLabel).Append(subtypeIdString).Append('\n');
                }
                else
                {
                    SB.Append(IdLabel).Append(typeIdString, obPrefixLen, shortTypeIdLength).Append("/").Append(subtypeIdString).Append('\n');
                }
            }
            #endregion internal info

            SB.TrimEndWhitespace();

            blockDef.DescriptionEnum = null; // prevent this from being used instead of DisplayNameString
            blockDef.DescriptionString = SB.ToString();
        }

        void GenerateDescription(StringBuilder s, MyCubeBlockDefinition blockDef)
        {
            s.Append('\n');

            if(!IgnoreBlockDefs.Contains(blockDef.Id))
            {
                string[] dlcs = blockDef.DLCs;

                if(dlcs != null && dlcs.Length > 0)
                {
                    s.Append("DLC: ");

                    for(int i = 0; i < dlcs.Length; ++i)
                    {
                        string dlcId = dlcs[i];

                        if(i > 0)
                            s.Append(", ");

                        IMyDLC dlc;
                        if(MyAPIGateway.DLC.TryGetDLC(dlcId, out dlc))
                        {
                            s.Append(MyTexts.GetString(dlc.DisplayName));
                        }
                        else
                        {
                            s.Append("(Unknown:").Append(dlcId).Append(")");
                        }
                    }

                    s.Append('\n');
                }
            }

            TooltipHandler.AppendModInfo(s, blockDef);

            // escape text yellowifying markers
            s.Replace("[", "[[");
            s.Replace("]", "]]");

            s.TrimEndWhitespace();
        }
    }
}
