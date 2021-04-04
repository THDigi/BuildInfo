using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Digi.BuildInfo.Utilities;
using ObjectBuilders.SafeZone;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features
{
    public class AnalyseShip : ModComponent
    {
        struct ModId
        {
            public class ModInfoComparer : IEqualityComparer<ModId>
            {
                public bool Equals(ModId x, ModId y)
                {
                    return x.ModName == y.ModName;
                }

                public int GetHashCode(ModId obj)
                {
                    return obj.GetHashCode();
                }
            }

            public static readonly ModInfoComparer Comparer = new ModInfoComparer();

            public readonly string ModName;
            public readonly ulong WorkshopId;

            private readonly int hashCode;

            public ModId(MyModContext mod)
            {
                ModName = mod.ModName;
                WorkshopId = mod.GetWorkshopID();
                hashCode = ModName.GetHashCode();
            }

            public override int GetHashCode()
            {
                return hashCode;
            }
        }

        class Objects
        {
            public int Blocks;
            public int SkinnedBlocks;
        }

        // per ship data
        private readonly StringBuilder sb = new StringBuilder(512);
        private readonly Dictionary<string, Objects> dlcs = new Dictionary<string, Objects>();
        private readonly Dictionary<ModId, Objects> mods = new Dictionary<ModId, Objects>(ModId.Comparer);
        private readonly Dictionary<ModId, Objects> modsChangingVanilla = new Dictionary<ModId, Objects>(ModId.Comparer);

        // definition data
        private readonly HashSet<MyDefinitionId> vanillaDefinitions = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);
        private readonly Dictionary<MyStringHash, string> armorSkinDLC = new Dictionary<MyStringHash, string>(MyStringHash.Comparer);
        private readonly Dictionary<MyStringHash, ModId> armorSkinMods = new Dictionary<MyStringHash, ModId>(MyStringHash.Comparer);

        private IMyTerminalControlButton projectorButton;

        private Action<ResultEnum> WindowClosedAction;

        public AnalyseShip(BuildInfoMod main) : base(main)
        {
            if(Constants.EXPORT_VANILLA_BLOCKS)
            {
                ExtractVanillaBlocks();
            }

            DefineVanillaBlocks();
            GetArmorSkinDefinitions();
            CheckVanillaHardcoded();

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

        void CheckVanillaHardcoded()
        {
            bool notify = (Log.WorkshopId == 0); // notify on HUD only if it's the local mod

            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var blockDef = def as MyCubeBlockDefinition;
                if(blockDef == null)
                    continue;

                if(blockDef.Context.IsBaseGame && !vanillaDefinitions.Contains(blockDef.Id))
                {
                    Log.Info($"WARNING: {blockDef.Id.ToString()} is vanilla but not in hardcoded list, needs update!", notify ? Log.PRINT_MESSAGE : null);
                }
            }
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
                        armorSkinMods.Add(assetDef.Id.SubtypeId, new ModId(assetDef.Context));
                    }
                }
            }
        }

        void DefineVanillaBlocks()
        {
            // Auto-generated vanilla definitions from SE v1.197.72
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRailStraight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_DebugSphere1), "DebugSphereLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_DebugSphere2), "DebugSphereLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_DebugSphere3), "DebugSphereLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCornerInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRoundArmor_Slope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRoundArmor_Corner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRoundArmor_CornerInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorSlope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorCornerInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCornerInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorSlope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorCornerInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHalfArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyHalfArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHalfSlopeArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyHalfSlopeArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HalfArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HeavyHalfArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HalfSlopeArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HeavyHalfSlopeArmorBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorRoundSlope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorRoundCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorRoundCornerInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorRoundSlope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorRoundCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorRoundCornerInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorRoundSlope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorRoundCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorRoundCornerInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorRoundSlope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorRoundCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorRoundCornerInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlope2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlope2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCorner2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCorner2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorInvCorner2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorInvCorner2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorSlope2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorSlope2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorCorner2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorCorner2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorInvCorner2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorInvCorner2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlope2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlope2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCorner2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCorner2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorInvCorner2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorInvCorner2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorSlope2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorSlope2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorCorner2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorCorner2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorInvCorner2Base"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorInvCorner2Tip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCornerSquare"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCornerSquare"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorCornerSquare"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorCornerSquare"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCornerSquareInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCornerSquareInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorCornerSquareInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorCornerSquareInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopeCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopeCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopeCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopeCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopeCornerInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopeCornerInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopeCornerInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopeCornerInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopedCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopedCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopedCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopedCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopedCornerBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopedCornerBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopedCornerBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopedCornerBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopeInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopeInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopeInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopeInverted"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlopedCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlopedCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorSlopedCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorSlopedCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlopedCornerBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlopedCornerBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorSlopedCornerBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorSlopedCornerBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlopedCornerTip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlopedCornerTip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorSlopedCornerTip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorSlopedCornerTip"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MyProgrammableBlock), "SmallProgrammableBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Projector), "LargeProjector"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Projector), "SmallProjector"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SensorBlock), "SmallBlockSensor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SensorBlock), "LargeBlockSensor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SoundBlock), "SmallBlockSoundBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SoundBlock), "LargeBlockSoundBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "ButtonPanelLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "ButtonPanelSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TimerBlock), "TimerBlockLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TimerBlock), "TimerBlockSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MyProgrammableBlock), "LargeProgrammableBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RadioAntenna), "LargeBlockRadioAntenna"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Beacon), "LargeBlockBeacon"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Beacon), "SmallBlockBeacon"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RadioAntenna), "SmallBlockRadioAntenna"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RemoteControl), "LargeBlockRemoteControl"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RemoteControl), "SmallBlockRemoteControl"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LaserAntenna), "LargeBlockLaserAntenna"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LaserAntenna), "SmallBlockLaserAntenna"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TerminalBlock), "ControlPanel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TerminalBlock), "SmallControlPanel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCockpit"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCockpitSeat"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "SmallBlockCockpit"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "DBSmallBlockFighterCockpit"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "CockpitOpen"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "RoverCockpit"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Gyro), "LargeBlockGyro"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Gyro), "SmallBlockGyro"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "OpenCockpitSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "OpenCockpitLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockDesk"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockDeskCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockDeskChairless"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockDeskChairlessCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Kitchen), "LargeBlockKitchen"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CryoChamber), "LargeBlockBed"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLockerRoom"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLockerRoomCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Planter), "LargeBlockPlanters"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCouch"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCouchCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLockers"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockBathroomOpen"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockBathroom"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockToilet"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Projector), "LargeBlockConsole"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "SmallBlockCockpitIndustrial"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCockpitIndustrial"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_VendingMachine), "FoodDispenser"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Jukebox), "Jukebox"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LCDPanelsBlock), "LabEquipment"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Shower"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "WindowWall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "WindowWallLeft"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "WindowWallRight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LCDPanelsBlock), "MedicalStation"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "TransparentLCDLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "TransparentLCDSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Catwalk"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkStraight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkWall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkRailingEnd"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkRailingHalfRight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkRailingHalfLeft"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "GratedStairs"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "GratedHalfStairs"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "GratedHalfStairsMirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingStraight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingDouble"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingDiagonal"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingHalfRight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingHalfLeft"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "RotatingLightLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "RotatingLightSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Freight1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Freight2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Freight3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "SmallDoor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirtightHangarDoor), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirtightSlideDoor), "LargeBlockSlideDoor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "ArmorCenter"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "ArmorCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "ArmorInvCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "ArmorSide"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorCenter"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorInvCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorSide"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_StoreBlock), "StoreBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SafeZoneBlock), "SafeZoneBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ContractBlock), "ContractBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_VendingMachine), "VendingMachine"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_StoreBlock), "AtmBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "LargeBlockBatteryBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "SmallBlockBatteryBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "SmallBlockSmallBatteryBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "SmallBlockSmallGenerator"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "SmallBlockLargeGenerator"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "LargeBlockSmallGenerator"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "LargeBlockLargeGenerator"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_HydrogenEngine), "LargeHydrogenEngine"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_HydrogenEngine), "SmallHydrogenEngine"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_WindTurbine), "LargeBlockWindTurbine"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SolarPanel), "LargeBlockSolarPanel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SolarPanel), "SmallBlockSolarPanel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Monolith"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Stereolith"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadAstronaut"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeDeadAstronaut"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RadioAntenna), "LargeBlockRadioAntennaDish"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "LargeBlockGate"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "LargeBlockOffsetDoor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody01"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody02"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody03"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody04"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody05"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody06"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_GravityGenerator), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_GravityGeneratorSphere), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_VirtualMass), "VirtualMassLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_VirtualMass), "VirtualMassSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SpaceBall), "SpaceBallLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SpaceBall), "SpaceBallSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Passage), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeStairs"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRamp"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSteelCatwalk"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSteelCatwalk2Sides"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSteelCatwalkCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSteelCatwalkPlate"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeCoverWall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeCoverWallHalf"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockInteriorWall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeInteriorPillar"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "PassengerSeatLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "PassengerSeatSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Ladder2), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Ladder2), "LadderSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallTextPanel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallLCDPanelWide"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallLCDPanel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeBlockCorner_LCD_1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeBlockCorner_LCD_2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeBlockCorner_LCD_Flat_1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeBlockCorner_LCD_Flat_2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallBlockCorner_LCD_1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallBlockCorner_LCD_2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallBlockCorner_LCD_Flat_1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallBlockCorner_LCD_Flat_2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeTextPanel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanelWide"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "LargeBlockFrontLight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "SmallBlockFrontLight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "SmallLight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "SmallBlockSmallLight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_1corner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_2corner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_1corner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_2corner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "OxygenTankSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "LargeHydrogenTank"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "LargeHydrogenTankSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "SmallHydrogenTank"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "SmallHydrogenTankSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirVent), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirVent), "SmallAirVent"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "SmallBlockSmallContainer"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "SmallBlockMediumContainer"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "SmallBlockLargeContainer"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockSmallContainer"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLargeContainer"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Conveyor), "SmallBlockConveyor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Conveyor), "LargeBlockConveyor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Collector), "Collector"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Collector), "CollectorSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipConnector), "Connector"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipConnector), "ConnectorSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipConnector), "ConnectorMedium"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTube"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeMedium"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorFrameMedium"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeCurved"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeSmallCurved"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeCurvedMedium"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Conveyor), "SmallShipConveyorHub"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorSorter), "LargeBlockConveyorSorter"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorSorter), "MediumBlockConveyorSorter"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorSorter), "SmallBlockConveyorSorter"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_PistonBase), "LargePistonBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ExtendedPistonBase), "LargePistonBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_PistonTop), "LargePistonTop"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_PistonBase), "SmallPistonBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ExtendedPistonBase), "SmallPistonBase"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_PistonTop), "SmallPistonTop"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorStator), "LargeStator"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorRotor), "LargeRotor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorStator), "SmallStator"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorRotor), "SmallRotor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "LargeAdvancedStator"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "LargeAdvancedRotor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "SmallAdvancedStator"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "SmallAdvancedRotor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "LargeHinge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "LargeHingeHead"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "MediumHinge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "MediumHingeHead"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "SmallHinge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "SmallHingeHead"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MedicalRoom), "LargeMedicalRoom"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CryoChamber), "LargeBlockCryoChamber"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CryoChamber), "SmallBlockCryoChamber"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Refinery), "LargeRefinery"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Refinery), "Blast Furnace"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenGenerator), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenGenerator), "OxygenGeneratorSmall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "LargeAssembler"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "BasicAssembler"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SurvivalKit), "SurvivalKitLarge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SurvivalKit), "SurvivalKit"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenFarm), "LargeBlockOxygenFarm"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_UpgradeModule), "LargeProductivityModule"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_UpgradeModule), "LargeEffectivenessModule"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_UpgradeModule), "LargeEnergyModule"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ExhaustBlock), "SmallExhaustPipe"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ExhaustBlock), "LargeExhaustPipe"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "BuggyCockpit"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Viewport1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Viewport2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension3x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension3x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension3x3mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension5x5mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension1x1mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension3x3mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension5x5mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension1x1mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel1x1mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheelmirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel5x5mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel1x1mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheelmirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel5x5mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadWheel1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallWheel1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadWheel3x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallWheel3x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadWheel5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallWheel5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "OffsetLight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "OffsetSpotlight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BarredWindow"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BarredWindowSlope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BarredWindowSide"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BarredWindowFace"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "StorageShelf1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "StorageShelf2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "StorageShelf3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanel5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanel5x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanel3x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockSciFiWall"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraight1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraight2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesBendUp"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesBendDown"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraightEnd1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraightEnd2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraightDown"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesU"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TerminalBlock), "LargeBlockSciFiTerminal"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "LargeSciFiButtonTerminal"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "SmallSideDoor"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockBarCounter"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockBarCounterCorner"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "LargeSciFiButtonPanel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallThrustSciFi"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeThrustSciFi"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallThrustSciFi"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeThrustSciFi"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeAtmosphericThrustSciFi"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallAtmosphericThrustSciFi"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeAtmosphericThrustSciFi"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallAtmosphericThrustSciFi"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolA"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolB"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolC"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolD"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolE"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolF"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolG"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolH"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolI"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolJ"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolK"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolL"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolM"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolN"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolO"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolP"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolQ"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolR"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolS"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolT"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolU"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolV"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolW"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolX"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolY"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolZ"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolA"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolB"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolC"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolD"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolE"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolF"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolG"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolH"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolI"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolJ"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolK"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolL"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolM"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolN"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolO"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolP"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolQ"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolR"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolS"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolT"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolU"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolV"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolW"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolX"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolY"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolZ"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol0"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol4"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol6"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol7"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol8"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol9"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol0"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol2"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol4"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol6"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol7"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol8"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol9"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolHyphen"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolUnderscore"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolDot"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolApostrophe"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolAnd"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolColon"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolExclamationMark"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolQuestionMark"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolHyphen"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolUnderscore"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolDot"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolApostrophe"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolAnd"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolColon"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolExclamationMark"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolQuestionMark"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeHydrogenThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallHydrogenThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeHydrogenThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallHydrogenThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeAtmosphericThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallAtmosphericThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeAtmosphericThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallAtmosphericThrust"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Drill), "SmallBlockDrill"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Drill), "LargeBlockDrill"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipGrinder), "LargeShipGrinder"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipGrinder), "SmallShipGrinder"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipWelder), "LargeShipWelder"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipWelder), "SmallShipWelder"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OreDetector), "LargeOreDetector"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OreDetector), "SmallBlockOreDetector"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LandingGear), "LargeBlockLandingGear"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LandingGear), "SmallBlockLandingGear"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_JumpDrive), "LargeJumpDrive"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CameraBlock), "SmallCameraBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CameraBlock), "LargeCameraBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MergeBlock), "LargeShipMergeBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MergeBlock), "SmallShipMergeBlock"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Parachute), "LgParachute"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Parachute), "SmParachute"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Warhead), "LargeWarhead"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Warhead), "SmallWarhead"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Decoy), "LargeDecoy"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Decoy), "SmallDecoy"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), "SmallGatlingTurret"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallMissileTurret"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorTurret), "LargeInteriorTurret"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), "LargeMissileLauncher"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncherReload), "SmallRocketLauncherReload"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallGatlingGun), ""));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension3x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension3x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension3x3mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension5x5mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension1x1mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension3x3mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension5x5mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension1x1mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel1x1mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheelmirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel5x5mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel1x1mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheelmirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel5x5mirrored"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "Wheel1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallWheel1x1"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "Wheel3x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallWheel3x3"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "Wheel5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallWheel5x5"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeWindowSquare"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeWindowEdge"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2Slope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2Inv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2Face"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2SideLeft"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2SideLeftInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2SideRight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2SideRightInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Slope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Face"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Side"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1SideInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Inv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2Flat"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2FlatInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Flat"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1FlatInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window3x3Flat"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window3x3FlatInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window2x3Flat"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window2x3FlatInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2Slope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2Inv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2Face"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2SideLeft"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2SideLeftInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2SideRight"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2SideRightInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Slope"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Face"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Side"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1SideInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Inv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2Flat"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2FlatInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Flat"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1FlatInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow3x3Flat"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow3x3FlatInv"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow2x3Flat"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow2x3FlatInv"));
        }

        void ExtractVanillaBlocks()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("// Auto-generated vanilla definitions from SE v").Append(MyAPIGateway.Session.Version.ToString()).NewLine();

            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var blockDef = def as MyCubeBlockDefinition;

                if(blockDef != null && blockDef.Context.IsBaseGame)
                {
                    sb.Append("vanillaDefinitions.Add(new MyDefinitionId(typeof(").Append(blockDef.Id.TypeId.ToString()).Append("), \"").Append(blockDef.Id.SubtypeName).Append("\"));").AppendLine();
                }
            }

            const string FILE_NAME = "VanillaDefinitions.txt";

            using(var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(FILE_NAME, typeof(AnalyseShip)))
            {
                writer.Write(sb.ToString());
            }

            Log.Info($"Exported vanilla blocks to Storage/{FILE_NAME}", Log.PRINT_MESSAGE, 10000);
        }

        public bool Analyse(IMyCubeGrid mainGrid)
        {
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

                var grids = Caches.GetGrids(mainGrid, GridLinkTypeEnum.Mechanical);

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
                grids.Clear();
            }
            finally
            {
                dlcs.Clear();
                mods.Clear();
                modsChangingVanilla.Clear();
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
                            var objects = GetOrAddObjects(dlcs, dlc);
                            objects.SkinnedBlocks++;
                        }
                        else
                        {
                            ModId modId;
                            if(armorSkinMods.TryGetValue(block.SkinSubtypeId, out modId))
                            {
                                var objects = GetOrAddObjects(mods, modId);
                                objects.SkinnedBlocks++;
                            }
                        }
                    }

                    if(def.DLCs != null && def.DLCs.Length != 0)
                    {
                        foreach(var dlc in def.DLCs)
                        {
                            var objects = GetOrAddObjects(dlcs, dlc);
                            objects.Blocks++;
                        }
                    }

                    if(!def.Context.IsBaseGame)
                    {
                        var modId = new ModId(def.Context);

                        if(vanillaDefinitions.Contains(def.Id))
                        {
                            if(!mods.ContainsKey(modId))
                            {
                                var objects = GetOrAddObjects(modsChangingVanilla, modId);
                                objects.Blocks++;
                            }
                        }
                        else
                        {
                            var objects = GetOrAddObjects(mods, modId);
                            objects.Blocks++;
                        }
                    }
                }
            }
        }

        Objects GetOrAddObjects<TKey>(Dictionary<TKey, Objects> dictionary, TKey key)
        {
            Objects objects;
            if(!dictionary.TryGetValue(key, out objects))
            {
                objects = new Objects();
                dictionary.Add(key, objects);
            }
            return objects;
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
                    if(modId.WorkshopId != 0)
                        sb.Append("(").Append(modId.WorkshopId.ToString()).Append(") ");
                    sb.Append(modId.ModName).NewLine();

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
                    if(modId.WorkshopId != 0)
                        sb.Append("(").Append(modId.WorkshopId.ToString()).Append(") ");
                    sb.Append(modId.ModName).NewLine();

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