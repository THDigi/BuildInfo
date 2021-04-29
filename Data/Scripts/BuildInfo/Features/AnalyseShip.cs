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

using ModId = System.ValueTuple<ulong, string, string>;

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
        private readonly StringBuilder sb = new StringBuilder(512);
        private readonly Dictionary<string, Objects> dlcs = new Dictionary<string, Objects>();
        private readonly Dictionary<ModId, Objects> mods = new Dictionary<ModId, Objects>();
        private readonly Dictionary<ModId, Objects> modsChangingVanilla = new Dictionary<ModId, Objects>();

        private readonly Dictionary<MyStringHash, string> armorSkinDLC = new Dictionary<MyStringHash, string>(MyStringHash.Comparer);
        private readonly Dictionary<MyStringHash, ModId> armorSkinMods = new Dictionary<MyStringHash, ModId>(MyStringHash.Comparer);

        private IMyTerminalControlButton projectorButton;

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
                if(block is IMyProjector && projectorButton != null)
                {
                    controls?.Add(projectorButton);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void CreateProjectorButton()
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, IMyProjector>("BuildInfo.ShowBlueprintMods");
            c.Title = MyStringId.GetOrCompute("Show Blueprint Mods");
            c.Tooltip = MyStringId.GetOrCompute("Shows DLCs and mods used by the projected blueprint.");
            c.SupportsMultipleBlocks = false;
            c.Enabled = (block) =>
            {
                var projector = (IMyProjector)block;
                return (projector.ProjectedGrid != null);
            };
            c.Action = (block) =>
            {
                var projector = (IMyProjector)block;
                if(projector.ProjectedGrid != null)
                    Analyse(projector.ProjectedGrid);
            };
            projectorButton = c;
        }

        void GetArmorSkinDefinitions()
        {
            foreach(var assetDef in MyDefinitionManager.Static.GetAssetModifierDefinitions())
            {
                if(assetDef.Id.SubtypeName.EndsWith("_Armor"))
                {
                    if(assetDef.DLCs != null && assetDef.DLCs.Length != 0)
                    {
                        foreach(var dlc in assetDef.DLCs)
                        {
                            armorSkinDLC.Add(assetDef.Id.SubtypeId, dlc);
                        }
                    }
                    else if(!assetDef.Context.IsBaseGame)
                    {
                        armorSkinMods.Add(assetDef.Id.SubtypeId, new ModId(assetDef.Context.GetWorkshopID(), assetDef.Context.ModServiceName, assetDef.Context.ModName));
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
                    Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, Constants.PLAYER_IS_NULL, FontsHandler.RedSh);
                    return true; // must be true to avoid showing other messages
                }

                dlcs.Clear();
                mods.Clear();
                modsChangingVanilla.Clear();

                grids = Caches.GetGrids(mainGrid, GridLinkTypeEnum.Mechanical);

                foreach(var grid in grids)
                {
                    if(grid.BigOwners != null && grid.BigOwners.Count > 0)
                    {
                        foreach(var owner in grid.BigOwners)
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
                dlcs.Clear();
                mods.Clear();
                modsChangingVanilla.Clear();
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
                    var def = (MyCubeBlockDefinition)block.BlockDefinition;

                    if(block.SkinSubtypeId != MyStringHash.NullOrEmpty)
                    {
                        string dlc;
                        if(armorSkinDLC.TryGetValue(block.SkinSubtypeId, out dlc))
                        {
                            var objects = dlcs.GetOrAdd(dlc);
                            objects.SkinnedBlocks++;
                        }
                        else
                        {
                            ModId modId;
                            if(armorSkinMods.TryGetValue(block.SkinSubtypeId, out modId))
                            {
                                var objects = mods.GetOrAdd(modId);
                                objects.SkinnedBlocks++;
                            }
                        }
                    }

                    if(def.DLCs != null && def.DLCs.Length != 0)
                    {
                        foreach(var dlc in def.DLCs)
                        {
                            var objects = dlcs.GetOrAdd(dlc);
                            objects.Blocks++;
                        }
                    }

                    if(!def.Context.IsBaseGame)
                    {
                        var modId = new ModId(def.Context.GetWorkshopID(), def.Context.ModServiceName, def.Context.ModName);

                        if(Main.VanillaDefinitions.Definitions.Contains(def.Id))
                        {
                            if(!mods.ContainsKey(modId))
                            {
                                var objects = modsChangingVanilla.GetOrAdd(modId);
                                objects.Blocks++;
                            }
                        }
                        else
                        {
                            var objects = mods.GetOrAdd(modId);
                            objects.Blocks++;
                        }
                    }
                }
            }
        }

        void GenerateShipInfo(IMyCubeGrid mainGrid, List<IMyCubeGrid> grids)
        {
            sb.Clear();
            AppendTitle(sb, "Blocks or skins from DLCs", dlcs.Count);

            if(dlcs.Count == 0)
            {
                sb.Append(" (None)").NewLine();
            }
            else
            {
                foreach(var kv in dlcs)
                {
                    var dlcId = kv.Key;
                    var objects = kv.Value;

                    string displayName = dlcId;
                    MyDLCs.MyDLC dlc;
                    if(MyDLCs.TryGetDLC(dlcId, out dlc))
                    {
                        displayName = MyTexts.GetString(dlc.DisplayName);
                    }

                    sb.Append("- ").Append(displayName).NewLine();

                    sb.Append("    ").Append(objects.Blocks).Append(" block").Append(objects.Blocks == 1 ? " and " : "s and ")
                        .Append(objects.SkinnedBlocks).Append(" skin").Append(objects.SkinnedBlocks == 1 ? "." : "s.").NewLine();
                }
            }

            sb.NewLine();
            AppendTitle(sb, "Blocks or skins from mods", mods.Count);

            if(mods.Count == 0)
            {
                sb.Append(" (None)").NewLine();
            }
            else
            {
                foreach(var kv in mods)
                {
                    var modId = kv.Key;
                    var objects = kv.Value;

                    sb.Append("- ");
                    if(modId.Item1 != 0)
                        sb.Append("(").Append(modId.Item1.ToString()).Append(") ");
                    sb.Append(modId.Item3).NewLine();

                    sb.Append("    ").Append(objects.Blocks).Append(" block").Append(objects.Blocks == 1 ? " and " : "s and ")
                        .Append(objects.SkinnedBlocks).Append(" skin").Append(objects.SkinnedBlocks == 1 ? "." : "s.").NewLine();
                }
            }

            sb.NewLine();
            AppendTitle(sb, "Vanilla blocks altered by mods", modsChangingVanilla.Count);
            sb.Append("NOTE: This list can't show mods that alter blocks only with scripts.").NewLine();

            if(modsChangingVanilla.Count == 0)
            {
                sb.Append(" (None)").NewLine();
            }
            else
            {
                foreach(var kv in modsChangingVanilla)
                {
                    var modId = kv.Key;
                    var objects = kv.Value;

                    sb.Append("- ");
                    if(modId.Item1 != 0)
                        sb.Append("(").Append(modId.Item1.ToString()).Append(") ");
                    sb.Append(modId.Item3).NewLine();

                    sb.Append("    ").Append(objects.Blocks).Append(" block").Append(objects.Blocks == 1 ? "." : "s.").NewLine();

                    sb.NewLine();
                }
            }

            shipInfoText = sb.ToString();

            sb.Clear();
            sb.Append(mainGrid.CustomName);
            if(grids.Count > 1)
                sb.Append(" + ").Append((grids.Count - 1).ToString()).Append(" subgrids");

            shipInfoTitle = sb.ToString();

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

                    var invalidFileNameChars = Path.GetInvalidFileNameChars();

                    foreach(char invalidChar in invalidFileNameChars)
                    {
                        fileNameSb.Replace(invalidChar, '_');
                    }

                    var fileName = fileNameSb.ToString();
                    var modStorageName = MyAPIGateway.Utilities.GamePaths.ModScopeName;

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