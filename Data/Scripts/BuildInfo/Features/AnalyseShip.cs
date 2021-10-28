using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
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

        // per ship data
        private readonly StringBuilder SB = new StringBuilder(512);
        private readonly Dictionary<string, Objects> DLCs = new Dictionary<string, Objects>();
        private readonly Dictionary<ModId, Objects> Mods = new Dictionary<ModId, Objects>();
        private readonly Dictionary<ModId, Objects> ModsChangingVanilla = new Dictionary<ModId, Objects>();

        private readonly Dictionary<MyStringHash, string> ArmorSkinDLC = new Dictionary<MyStringHash, string>(MyStringHash.Comparer);
        private readonly Dictionary<MyStringHash, ModId> ArmorSkinMods = new Dictionary<MyStringHash, ModId>(MyStringHash.Comparer);

        private IMyTerminalControlButton ProjectorButton;

        private Action<ResultEnum> WindowClosedAction;

        public AnalyseShip(BuildInfoMod main) : base(main)
        {
            GetArmorSkinDefinitions();

            WindowClosedAction = new Action<ResultEnum>(WindowClosed);
        }

        public override void RegisterComponent()
        {
            CreateProjectorButton();

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
                if(block is IMyProjector && ProjectorButton != null)
                {
                    controls?.Add(ProjectorButton);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void CreateProjectorButton()
        {
            IMyTerminalControlButton c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("BuildInfo.ShowBlueprintMods");
            c.Title = MyStringId.GetOrCompute("Show Blueprint Mods");
            c.Tooltip = MyStringId.GetOrCompute("Shows DLCs and mods used by the projected blueprint.");
            c.SupportsMultipleBlocks = false;
            c.Enabled = (block) =>
            {
                IMyProjector projector = (IMyProjector)block;
                return (projector.ProjectedGrid != null);
            };
            c.Action = (block) =>
            {
                IMyProjector projector = (IMyProjector)block;
                if(projector.ProjectedGrid != null)
                    Analyse(projector.ProjectedGrid);
            };
            ProjectorButton = c;
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
                        ArmorSkinMods.Add(assetDef.Id.SubtypeId, new ModId(assetDef.Context.GetWorkshopID(), assetDef.Context.ModServiceName, assetDef.Context.ModName));
                    }
                }
            }
        }

        public bool Analyse(IMyCubeGrid mainGrid)
        {
            List<IMyCubeGrid> grids = null;
            try
            {
                if(MyAPIGateway.Session?.Player == null)
                {
                    Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, Constants.WarnPlayerIsNull, FontsHandler.RedSh);
                    return true; // must be true to avoid showing other messages
                }

                DLCs.Clear();
                Mods.Clear();
                ModsChangingVanilla.Clear();

                grids = Caches.GetGrids(mainGrid, GridLinkTypeEnum.Mechanical);

                foreach(IMyCubeGrid grid in grids)
                {
                    if(grid.BigOwners != null && grid.BigOwners.Count > 0)
                    {
                        foreach(long owner in grid.BigOwners)
                        {
                            if(MyAPIGateway.Session.Player.GetRelationTo(owner) == MyRelationsBetweenPlayerAndBlock.Enemies)
                            {
                                return false;
                            }
                        }
                    }
                }

                GetInfoFromGrids(grids);
                GenerateShipInfo(mainGrid, grids);
            }
            finally
            {
                DLCs.Clear();
                Mods.Clear();
                ModsChangingVanilla.Clear();
                grids?.Clear();
            }

            return true;
        }

        void GetInfoFromGrids(List<IMyCubeGrid> grids)
        {
            foreach(MyCubeGrid grid in grids)
            {
                foreach(IMySlimBlock block in grid.GetBlocks())
                {
                    MyCubeBlockDefinition def = (MyCubeBlockDefinition)block.BlockDefinition;

                    if(block.SkinSubtypeId != MyStringHash.NullOrEmpty)
                    {
                        string dlc;
                        if(ArmorSkinDLC.TryGetValue(block.SkinSubtypeId, out dlc))
                        {
                            Objects objects = DLCs.GetOrAdd(dlc);
                            objects.SkinnedBlocks++;
                        }
                        else
                        {
                            ModId modId;
                            if(ArmorSkinMods.TryGetValue(block.SkinSubtypeId, out modId))
                            {
                                Objects objects = Mods.GetOrAdd(modId);
                                objects.SkinnedBlocks++;
                            }
                        }
                    }

                    if(def.DLCs != null && def.DLCs.Length != 0)
                    {
                        foreach(string dlc in def.DLCs)
                        {
                            Objects objects = DLCs.GetOrAdd(dlc);
                            objects.Blocks++;
                        }
                    }

                    if(!def.Context.IsBaseGame)
                    {
                        ModId modId = new ModId(def.Context.GetWorkshopID(), def.Context.ModServiceName, def.Context.ModName);

                        if(Main.VanillaDefinitions.Definitions.Contains(def.Id))
                        {
                            if(!Mods.ContainsKey(modId))
                            {
                                Objects objects = ModsChangingVanilla.GetOrAdd(modId);
                                objects.Blocks++;
                            }
                        }
                        else
                        {
                            Objects objects = Mods.GetOrAdd(modId);
                            objects.Blocks++;
                        }
                    }
                }
            }
        }

        void GenerateShipInfo(IMyCubeGrid mainGrid, List<IMyCubeGrid> grids)
        {
            SB.Clear();
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
                    MyDLCs.MyDLC dlc;
                    if(MyDLCs.TryGetDLC(dlcId, out dlc))
                    {
                        displayName = MyTexts.GetString(dlc.DisplayName);
                    }

                    SB.Append("- ").Append(displayName).NewLine();

                    SB.Append("    ").Append(objects.Blocks).Append(" block").Append(objects.Blocks == 1 ? " and " : "s and ")
                        .Append(objects.SkinnedBlocks).Append(" skin").Append(objects.SkinnedBlocks == 1 ? "." : "s.").NewLine();
                }
            }

            SB.NewLine();
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

            shipInfoText = SB.ToString();

            SB.Clear();
            SB.Append(mainGrid.CustomName);
            if(grids.Count > 1)
                SB.Append(" + ").Append(grids.Count - 1).Append(" subgrids");

            shipInfoTitle = SB.ToString();

            MyAPIGateway.Utilities.ShowMissionScreen("Mods and DLCs used by:", shipInfoTitle, string.Empty, shipInfoText, WindowClosedAction, "Export info to file\n(Press [Esc] to not export)");
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

        StringBuilder fileNameSb = new StringBuilder(128);
        string shipInfoTitle;
        string shipInfoText;

        void WindowClosed(ResultEnum result)
        {
            try
            {
                if(result == ResultEnum.OK)
                {
                    fileNameSb.Clear();
                    fileNameSb.Append("ShipInfo '");
                    fileNameSb.Append(shipInfoTitle);
                    fileNameSb.Append("' - ");
                    fileNameSb.Append(DateTime.Now.ToString("yyyy-MM-dd HHmm"));
                    fileNameSb.Append(".txt");

                    char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

                    foreach(char invalidChar in invalidFileNameChars)
                    {
                        fileNameSb.Replace(invalidChar, '_');
                    }

                    string fileName = fileNameSb.ToString();
                    string modStorageName = MyAPIGateway.Utilities.GamePaths.ModScopeName;

                    TextWriter writer = null;
                    try
                    {
                        writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(fileName, typeof(AnalyseShip));
                        writer.Write(shipInfoText);
                        writer.Flush();

                        Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, $"Exported ship info to: %appdata%/SpaceEngineers/Storage/{modStorageName}/{fileName}", FontsHandler.GreenSh);
                    }
                    catch(Exception e)
                    {
                        Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, $"Failed to export ship info! Exception: {e.Message}; see SE log for details.", FontsHandler.RedSh);
                        Log.Error(e);
                    }

                    writer?.Dispose();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}