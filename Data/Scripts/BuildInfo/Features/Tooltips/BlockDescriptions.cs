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
            const string IdLabel = "\nId:\u00A0"; // non-space space to not count as whitespace for splitting the words.
            const string IdTypeLabel = "\nIdType:\u00A0";
            const string IdSubtypeLabel = "\nIdSub:\u00A0";

            if(SB.Length > 0)
            {
                SB.RemoveLineStartsWith(IdLabel);
                SB.RemoveLineStartsWith(IdTypeLabel);
                SB.RemoveLineStartsWith(IdSubtypeLabel);
            }

            if(Main.Config.InternalInfo.Value)
            {
                string typeIdString = blockDef.Id.TypeId.ToString();
                string subtypeIdString = blockDef.Id.SubtypeName;

                const int obPrefixLen = 16; // "MyObjectBuilder_".Length;
                int shortTypeIdLength = (typeIdString.Length - obPrefixLen);

                const int MaxWidth = 32;
                int totalWidth = shortTypeIdLength + 1 + subtypeIdString.Length;
                if(totalWidth > MaxWidth)
                {
                    SB.Append(IdTypeLabel).Append(typeIdString, obPrefixLen, shortTypeIdLength);
                    SB.Append(IdSubtypeLabel).Append(subtypeIdString);
                }
                else
                {
                    SB.Append(IdLabel).Append(typeIdString, obPrefixLen, shortTypeIdLength).Append("/").Append(subtypeIdString);
                }
            }
            #endregion internal info

            blockDef.DescriptionEnum = null; // prevent this from being used instead of DisplayNameString
            blockDef.DescriptionString = SB.ToString();
        }

        void GenerateDescription(StringBuilder s, MyCubeBlockDefinition blockDef)
        {
            SB.Append('\n');

            if(!IgnoreBlockDefs.Contains(blockDef.Id))
            {
                if(blockDef.DLCs != null && blockDef.DLCs.Length > 0)
                {
                    s.Append("\nDLC: ");

                    bool multiDLC = blockDef.DLCs.Length > 1;
                    for(int i = 0; i < blockDef.DLCs.Length; ++i)
                    {
                        string dlcId = blockDef.DLCs[i];

                        if(multiDLC && i > 0)
                        {
                            if(Main.TextAPI.IsEnabled)
                                s.Append("\n   | ");
                            else
                                s.Append(", ");
                        }

                        IMyDLC dlc;
                        if(MyAPIGateway.DLC.TryGetDLC(dlcId, out dlc))
                        {
                            s.Append(MyTexts.GetString(dlc.DisplayName));
                        }
                        else
                        {
                            s.Append("(Unknown: ").Append(dlcId).Append(")");
                        }
                    }
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
