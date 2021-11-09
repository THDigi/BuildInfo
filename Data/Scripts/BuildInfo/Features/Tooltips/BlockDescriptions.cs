using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Gui;
using VRage;
using VRage.Game;

namespace Digi.BuildInfo.Features.Tooltips
{
    public class BlockDescriptions : ModComponent
    {
        public readonly Dictionary<MyDefinitionId, string> Descriptions = new Dictionary<MyDefinitionId, string>(MyDefinitionId.Comparer);

        public readonly HashSet<MyDefinitionId> IgnoreBlockDefs = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        StringBuilder SB = new StringBuilder(512);

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
                HandleDescription(blockDef, generate);
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
                // HACK: fill in args using the help of game code instead of cloning it.
                // also fixes description in G-menu not getting any replacements.
                MyHud.BlockInfo.SetContextHelp(blockDef);
                SB.Append(MyHud.BlockInfo.ContextHelp);
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
            const string IdLabel = "\nId: ";

            if(SB.Length > 0)
            {
                SB.RemoveLineStartsWith(IdLabel);
            }

            if(Main.Config.InternalInfo.Value)
            {
                int obPrefixLen = "MyObjectBuilder_".Length;
                string typeIdString = blockDef.Id.TypeId.ToString();
                SB.Append(IdLabel).Append(typeIdString, obPrefixLen, (typeIdString.Length - obPrefixLen)).Append("/").Append(blockDef.Id.SubtypeName);
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

                        MyDLCs.MyDLC dlc;
                        if(MyDLCs.TryGetDLC(dlcId, out dlc))
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

            s.TrimEndWhitespace();
        }
    }
}
