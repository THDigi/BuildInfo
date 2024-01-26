using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

using ModId = VRage.MyTuple<ulong, string, string>;

namespace Digi.BuildInfo.Features
{
    public class AnalyseShip : ModComponent
    {
        class Objects
        {
            public int Blocks;
            public int SkinnedBlocks;
        }

        // temporary data to generate info
        readonly StringBuilder SB = new StringBuilder(512);
        readonly Dictionary<string, Objects> DLCs = new Dictionary<string, Objects>();
        readonly Dictionary<ModId, Objects> Mods = new Dictionary<ModId, Objects>();
        readonly Dictionary<ModId, Objects> ModsChangingVanilla = new Dictionary<ModId, Objects>();
        readonly Dictionary<MyDefinitionId, Objects> Inexistent = new Dictionary<MyDefinitionId, Objects>(MyDefinitionId.Comparer);

        void ResetLists()
        {
            DLCs.Clear();
            Mods.Clear();
            ModsChangingVanilla.Clear();
            Inexistent.Clear();
        }

        readonly Dictionary<MyStringHash, string> ArmorSkinDLC = new Dictionary<MyStringHash, string>(MyStringHash.Comparer);
        readonly Dictionary<MyStringHash, ModId> ArmorSkinMods = new Dictionary<MyStringHash, ModId>(MyStringHash.Comparer);

        MyObjectBuilder_Projector ProjectorOB;

        const string AddControlsAfterThisId = "Blueprint";
        IMyTerminalControlButton ProjectorBpInfoButton;
        IMyTerminalControlListbox ProjectorInfoLines;
        List<MyTerminalControlListBoxItem> InfoContents = new List<MyTerminalControlListBoxItem>(4);

        public AnalyseShip(BuildInfoMod main) : base(main)
        {
            GetArmorSkinDefinitions();
        }

        public override void RegisterComponent()
        {
            CreateTerminalControls();

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomTerminal;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomTerminal;
        }

        private void CustomTerminal(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            try
            {
                if(block == null || controls == null || ProjectorBpInfoButton == null)
                    return;

                IMyProjector projector = block as IMyProjector;

                if(projector != null)
                {
                    if(projector.ProjectedGrid != null)
                    {
                        int bpButtonIndex = controls.Count;

                        #region Find button index
                        for(int i = 0; i < controls.Count; i++)
                        {
                            IMyTerminalControl c = controls[i];
                            if(c != null && c.Id == AddControlsAfterThisId)
                            {
                                bpButtonIndex = i + 1;
                                break;
                            }
                        }
                        #endregion

                        #region Projection info (listbox)
                        // maintain an OB cache of the viewed projector block, to access its projected grids as OBs (because of limited modAPI)
                        if(ProjectorOB == null || ProjectorOB.EntityId != projector.EntityId)
                        {
                            ProjectorOB = (MyObjectBuilder_Projector)projector.GetObjectBuilderCubeBlock(copy: false);
                        }

                        int projections = 0;
                        int projectedPCU = 0;
                        int builtPCU = 0;
                        int unknownBlocks = 0;

                        foreach(MyObjectBuilder_CubeGrid gridOB in ProjectorOB.ProjectedGrids) // no need to check ProjectedGrid because this OB is generated, the deeper ones are not.
                        {
                            projectedPCU += gridOB.CubeBlocks.Count;

                            foreach(MyObjectBuilder_CubeBlock blockOB in gridOB.CubeBlocks)
                            {
                                MyCubeBlockDefinition def;
                                if(!MyDefinitionManager.Static.TryGetCubeBlockDefinition(blockOB.GetId(), out def))
                                {
                                    unknownBlocks++;
                                    continue;
                                }

                                builtPCU += def.PCU;

                                // any projector that is going to be built is going to project its own stuff which also add PCU, but no deeper than this!
                                MyObjectBuilder_Projector projectorOB = blockOB as MyObjectBuilder_Projector;
                                if(projectorOB != null)
                                {
                                    if(projectorOB.ProjectedGrids != null)
                                    {
                                        foreach(MyObjectBuilder_CubeGrid deeperGridOB in projectorOB.ProjectedGrids)
                                        {
                                            builtPCU += deeperGridOB.CubeBlocks.Count;
                                        }
                                    }
                                    else if(projectorOB.ProjectedGrid != null)
                                    {
                                        builtPCU += projectorOB.ProjectedGrid.CubeBlocks.Count;
                                    }
                                }
                            }

                            FindProjections(gridOB, ref projections);
                        }

                        using(InfoLines info = new InfoLines(ProjectorInfoLines, InfoContents))
                        {
                            info.Add($"Projection PCU: {projectedPCU}");
                            info.Add($"Fully built PCU: {builtPCU}");

                            if(unknownBlocks > 0)
                                info.Add($"WARNING: {unknownBlocks} unknown blocks");

                            if(projections >= 2)
                                info.Add($"WARNING: {projections} layers of projectors!");
                        }

                        if(ProjectorInfoLines.VisibleRowsCount > 0)
                        {
                            ProjectorInfoLines.RedrawControl();
                            controls.Insert(bpButtonIndex++, ProjectorInfoLines);
                        }
                        #endregion

                        controls.Insert(bpButtonIndex++, ProjectorBpInfoButton);
                    }
                    else
                    {
                        ProjectorOB = null;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        static void FindProjections(MyObjectBuilder_CubeGrid startingGridOB, ref int projectedLayers)
        {
            foreach(MyObjectBuilder_CubeBlock blockOB in startingGridOB.CubeBlocks)
            {
                MyObjectBuilder_Projector projectorOB = blockOB as MyObjectBuilder_Projector;
                if(projectorOB == null)
                    continue;

                if(projectorOB.ProjectedGrids != null)
                {
                    projectedLayers++; // not adding per projected grid because this is still one projector

                    foreach(MyObjectBuilder_CubeGrid projectedGridOB in projectorOB.ProjectedGrids)
                    {
                        FindProjections(projectedGridOB, ref projectedLayers);
                    }
                }
                else if(projectorOB.ProjectedGrid != null)
                {
                    projectedLayers++;

                    FindProjections(projectorOB.ProjectedGrid, ref projectedLayers);
                }
            }
        }

        struct InfoLines : IDisposable
        {
            IMyTerminalControlListbox Listbox;
            List<MyTerminalControlListBoxItem> Lines;
            int Line;

            public InfoLines(IMyTerminalControlListbox listbox, List<MyTerminalControlListBoxItem> lines)
            {
                Listbox = listbox;
                Lines = lines;
                Line = 0;
            }

            public void Add(string text) //, string tooltip = null)
            {
                MyTerminalControlListBoxItem line;
                if(Lines.Count <= Line)
                {
                    line = new MyTerminalControlListBoxItem(MyStringId.NullOrEmpty, MyStringId.NullOrEmpty, null);
                    Lines.Add(line);
                }
                else
                {
                    line = Lines[Line];
                }

                line.Text = MyStringId.GetOrCompute(text);
                //line.Tooltip = (tooltip != null ? MyStringId.GetOrCompute(tooltip) : MyStringId.NullOrEmpty);

                Line++;
            }

            public void Dispose()
            {
                Listbox.VisibleRowsCount = Line;
            }
        }

        void CreateTerminalControls()
        {
            {
                IMyTerminalControlButton c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("BuildInfo.ShowBlueprintMods");
                c.Title = MyStringId.GetOrCompute("Show DLC/Mods");
                c.Tooltip = MyStringId.GetOrCompute("Opens a window listing the DLCs, mods and unknown blocks in the projected blueprint.\n(Added by BuildInfo mod)");
                c.SupportsMultipleBlocks = false;
                c.Enabled = (block) =>
                {
                    IMyProjector projector = (IMyProjector)block;
                    return (projector.ProjectedGrid != null);
                };
                c.Action = (block) =>
                {
                    if(ProjectorOB?.ProjectedGrids != null && ProjectorOB.ProjectedGrids.Count > 0)
                    {
                        AnalyseGridOBs(ProjectorOB.ProjectedGrids, ProjectorOB.ProjectedGrids[0].DisplayName);
                    }
                };
                ProjectorBpInfoButton = c;
            }
            {
                IMyTerminalControlListbox c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, IMyProjector>("BuildInfo.ProjectionInfo");
                c.ItemSelected = (b, selected) => { };
                c.ListContent = (b, content, preselected) =>
                {
                    for(int i = 0; i < ProjectorInfoLines.VisibleRowsCount; i++)
                    {
                        content.Add(InfoContents[i]);
                    }
                };
                c.Multiselect = false;
                c.VisibleRowsCount = 0;
                c.Tooltip = MyStringId.GetOrCompute("Various quick info about the projected blueprint.\nSelecting does nothing, this control is used just for its UI compactness.\n(Added by BuildInfo mod)");
                ProjectorInfoLines = c;
            }
        }

        void GetArmorSkinDefinitions()
        {
            foreach(MyAssetModifierDefinition assetDef in MyDefinitionManager.Static.GetAssetModifierDefinitions())
            {
                if(assetDef.Id.SubtypeName.EndsWith("_Armor"))
                {
                    if(assetDef.DLCs != null && assetDef.DLCs.Length != 0)
                    {
                        foreach(string dlc in assetDef.DLCs)
                        {
                            ArmorSkinDLC.Add(assetDef.Id.SubtypeId, dlc);
                        }
                    }
                    else if(!assetDef.Context.IsBaseGame)
                    {
                        ArmorSkinMods.Add(assetDef.Id.SubtypeId, new ModId(assetDef.Context.ModItem.PublishedFileId, assetDef.Context.ModServiceName, assetDef.Context.ModName));
                    }
                }
            }
        }

        public bool AnalyseRealGrid(IMyCubeGrid targetGrid)
        {
            List<IMyCubeGrid> grids = null;
            try
            {
                if(MyAPIGateway.Session?.Player == null)
                {
                    Utils.ShowColoredChatMessage(BuildInfoMod.ModName, Constants.WarnPlayerIsNull, FontsHandler.RedSh);
                    return true; // must be true to avoid showing other messages
                }

                ResetLists();

                grids = Caches.GetGrids(targetGrid, GridLinkTypeEnum.Mechanical);

                int totalBlocks = 0;

                foreach(MyCubeGrid grid in grids)
                {
                    if(grid.BigOwners != null && grid.BigOwners.Count > 0)
                    {
                        foreach(long owner in grid.BigOwners)
                        {
                            if(MyAPIGateway.Session.Player.GetRelationTo(owner) == MyRelationsBetweenPlayerAndBlock.Enemies)
                                return false;
                        }
                    }

                    totalBlocks += grid.BlocksCount;

                    foreach(IMySlimBlock block in grid.GetBlocks())
                    {
                        MyDefinitionId defId = block.BlockDefinition?.Id ?? new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BlockDefinition is null!!!");
                        GetInfoFromBlockDef(block.BlockDefinition as MyCubeBlockDefinition, defId, block.SkinSubtypeId);
                    }
                }

                GenerateShipInfo(targetGrid.CustomName, totalBlocks, grids.Count);
            }
            finally
            {
                ResetLists();
                grids?.Clear();
            }

            return true;
        }

        public bool AnalyseGridOBs(List<MyObjectBuilder_CubeGrid> gridOBs, string shipName)
        {
            try
            {
                if(MyAPIGateway.Session?.Player == null)
                {
                    Utils.ShowColoredChatMessage(BuildInfoMod.ModName, Constants.WarnPlayerIsNull, FontsHandler.RedSh);
                    return true; // must be true to avoid showing other messages
                }

                ResetLists();

                // no ownership check because it's given via projector which supposedly has access to press terminal buttons...

                int totalBlocks = 0;

                foreach(MyObjectBuilder_CubeGrid gridOB in gridOBs)
                {
                    totalBlocks += gridOB.CubeBlocks.Count;

                    foreach(MyObjectBuilder_CubeBlock blockOB in gridOB.CubeBlocks)
                    {
                        MyDefinitionId defId = blockOB.GetId();
                        GetInfoFromBlockDef(MyDefinitionManager.Static.GetCubeBlockDefinition(defId), defId, MyStringHash.GetOrCompute(blockOB.SkinSubtypeId));
                    }
                }

                GenerateShipInfo(shipName, totalBlocks, gridOBs.Count);
            }
            finally
            {
                ResetLists();
            }

            return true;
        }

        void GetInfoFromBlockDef(MyCubeBlockDefinition def, MyDefinitionId defId, MyStringHash skin)
        {
            if(def == null)
            {
                Objects objects = Inexistent.GetValueOrNew(defId);
                objects.Blocks++;
                return;
            }

            if(skin != MyStringHash.NullOrEmpty)
            {
                string dlc;
                if(ArmorSkinDLC.TryGetValue(skin, out dlc))
                {
                    Objects objects = DLCs.GetValueOrNew(dlc);
                    objects.SkinnedBlocks++;
                }
                else
                {
                    ModId modId;
                    if(ArmorSkinMods.TryGetValue(skin, out modId))
                    {
                        Objects objects = Mods.GetValueOrNew(modId);
                        objects.SkinnedBlocks++;
                    }
                }
            }

            if(def.DLCs != null && def.DLCs.Length != 0)
            {
                foreach(string dlc in def.DLCs)
                {
                    Objects objects = DLCs.GetValueOrNew(dlc);
                    objects.Blocks++;
                }
            }

            if(!def.Context.IsBaseGame)
            {
                ModId modId = new ModId(def.Context.ModItem.PublishedFileId, def.Context.ModServiceName, def.Context.ModName);

                if(Main.VanillaDefinitions.Definitions.Contains(def.Id))
                {
                    if(!Mods.ContainsKey(modId))
                    {
                        Objects objects = ModsChangingVanilla.GetValueOrNew(modId);
                        objects.Blocks++;
                    }
                }
                else
                {
                    Objects objects = Mods.GetValueOrNew(modId);
                    objects.Blocks++;
                }
            }
        }

        void GenerateShipInfo(string shipName, int totalBlocks, int gridCount)
        {
            SB.Clear();

            SB.Append("Total blocks: ").Append(totalBlocks).Append("\n\n");

            #region Inexistent blocks
            AppendTitle(SB, "Unknown block types", Inexistent.Count);

            if(Inexistent.Count == 0)
            {
                SB.Append(" (None)").NewLine();
            }
            else
            {
                foreach(KeyValuePair<MyDefinitionId, Objects> kv in Inexistent)
                {
                    MyDefinitionId defId = kv.Key;
                    Objects objects = kv.Value;

                    SB.Append("- ").IdTypeSubtypeFormat(defId).NewLine();
                    SB.Append("    ").Append(objects.Blocks).Append(" ").Append(objects.Blocks == 1 ? "block" : "blocks").NewLine();

                    MyCubeBlockDefinition newDef;
                    if(Hardcoded.IsBlockReplaced(defId, out newDef))
                    {
                        SB.Append("    NOTE: Spawns as: ").IdTypeSubtypeFormat(newDef.Id).NewLine();
                    }
                }
            }
            SB.NewLine();
            #endregion

            #region from DLC
            AppendTitle(SB, "Blocks or skins from DLCs", DLCs.Count);

            if(DLCs.Count == 0)
            {
                SB.Append(" (None)").NewLine();
            }
            else
            {
                foreach(KeyValuePair<string, Objects> kv in DLCs)
                {
                    string dlcId = kv.Key;
                    Objects objects = kv.Value;

                    string displayName = dlcId;
                    IMyDLC dlc;
                    if(MyAPIGateway.DLC.TryGetDLC(dlcId, out dlc))
                    {
                        displayName = MyTexts.GetString(dlc.DisplayName);
                    }

                    SB.Append("- ").Append(displayName).NewLine();

                    SB.Append("    ").Append(objects.Blocks).Append(" block").Append(objects.Blocks == 1 ? " and " : "s and ")
                        .Append(objects.SkinnedBlocks).Append(" skin").Append(objects.SkinnedBlocks == 1 ? "." : "s.").NewLine();
                }
            }
            SB.NewLine();
            #endregion

            #region from Mods
            AppendTitle(SB, "Blocks or skins from mods", Mods.Count);

            if(Mods.Count == 0)
            {
                SB.Append(" (None)").NewLine();
            }
            else
            {
                foreach(KeyValuePair<ModId, Objects> kv in Mods)
                {
                    ModId modId = kv.Key;
                    Objects objects = kv.Value;

                    SB.Append("- ");
                    if(modId.Item1 != 0)
                        SB.Append("(").Append(modId.Item2).Append(":").Append(modId.Item1).Append(") ");
                    SB.Append(modId.Item3).NewLine();

                    SB.Append("    ").Append(objects.Blocks).Append(" block").Append(objects.Blocks == 1 ? " and " : "s and ")
                        .Append(objects.SkinnedBlocks).Append(" skin").Append(objects.SkinnedBlocks == 1 ? "." : "s.").NewLine();
                }
            }
            SB.NewLine();
            #endregion

            #region Vanilla altered by mods
            AppendTitle(SB, "Vanilla blocks altered by mods", ModsChangingVanilla.Count);
            SB.Append("NOTE: This list can't show mods that alter blocks only with scripts.").NewLine();

            if(ModsChangingVanilla.Count == 0)
            {
                SB.Append(" (None)").NewLine();
            }
            else
            {
                foreach(KeyValuePair<ModId, Objects> kv in ModsChangingVanilla)
                {
                    ModId modId = kv.Key;
                    Objects objects = kv.Value;

                    SB.Append("- ");
                    if(modId.Item1 != 0)
                        SB.Append("(").Append(modId.Item2).Append(":").Append(modId.Item1).Append(") ");
                    SB.Append(modId.Item3).NewLine();

                    SB.Append("    ").Append(objects.Blocks).Append(" block").Append(objects.Blocks == 1 ? "." : "s.").NewLine();

                    SB.NewLine();
                }
            }
            SB.NewLine();
            #endregion

            ShipInfoText = SB.TrimEndWhitespace().ToString();

            SB.Clear();
            SB.Append(shipName);
            if(gridCount > 1)
                SB.Append(" + ").Append(gridCount - 1).Append(" subgrids");

            ShipInfoTitle = SB.ToString();

            MyAPIGateway.Utilities.ShowMissionScreen("Mods and DLCs used by:", ShipInfoTitle, string.Empty, ShipInfoText, WindowClosed, "Export info to file\n(Press [Esc] to not export)");
        }

        void AppendTitle(StringBuilder sb, string title, int count = -1)
        {
            //const int TotalWidth = 50;
            //int len = sb.Length;

            sb.Append('=', 10).Append(' ').Append(title).Append(' ');

            if(count >= 0)
                sb.Append("(").Append(count).Append(") ");

            //int suffixSize = TotalWidth - (sb.Length - len);
            //if(suffixSize > 0)
            //    sb.Append('=', suffixSize);

            sb.NewLine();
        }

        StringBuilder FileNameSB = new StringBuilder(128);
        string ShipInfoTitle;
        string ShipInfoText;

        void WindowClosed(ResultEnum result)
        {
            try
            {
                if(result == ResultEnum.OK)
                {
                    FileNameSB.Clear();
                    FileNameSB.Append("ShipInfo '");
                    FileNameSB.Append(ShipInfoTitle);
                    FileNameSB.Append("' - ");
                    FileNameSB.Append(DateTime.Now.ToString("yyyy-MM-dd HHmm"));
                    FileNameSB.Append(".txt");

                    foreach(char invalidChar in Path.GetInvalidFileNameChars())
                    {
                        FileNameSB.Replace(invalidChar, '_');
                    }

                    while(FileNameSB.IndexOf("__") != -1)
                    {
                        FileNameSB.Replace("__", "_");
                    }

                    string fileName = FileNameSB.ToString();
                    string modStorageName = MyAPIGateway.Utilities.GamePaths.ModScopeName;

                    TextWriter writer = null;
                    try
                    {
                        writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(fileName, typeof(AnalyseShip));
                        writer.Write(ShipInfoText);
                        writer.Flush();

                        Utils.ShowColoredChatMessage(BuildInfoMod.ModName, $"Exported ship info to: %appdata%/SpaceEngineers/Storage/{modStorageName}/{fileName}", FontsHandler.GreenSh);
                    }
                    catch(Exception e)
                    {
                        Log.Error(e, null);
                        Utils.ShowColoredChatMessage(BuildInfoMod.ModName, $"Failed to export ship info! Exception: {e.Message}; see SE log for details.", FontsHandler.RedSh);
                    }
                    finally
                    {
                        writer?.Dispose();
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}