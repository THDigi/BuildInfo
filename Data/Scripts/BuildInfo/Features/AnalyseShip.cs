using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
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

            WindowClosedAction = new Action<ResultEnum>(WindowClosed);
        }

        protected override void RegisterComponent()
        {
            CreateProjectorButton();

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomTerminal;
        }

        protected override void UnregisterComponent()
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
                        armorSkinMods.Add(assetDef.Id.SubtypeId, new ModId(assetDef.Context));
                    }
                }
            }
        }

        void DefineVanillaBlocks()
        {
            // Auto-generated vanilla definitions from SE v1.192.022
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
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Gyro), "LargeBlockGyro"));
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Gyro), "SmallBlockGyro"));
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
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), ""));
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
            vanillaDefinitions.Add(MyDefinitionId.Parse("MyObjectBuilder_SafeZoneBlock/SafeZoneBlock"));
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
            vanillaDefinitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "SmallHydrogenTank"));
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

            Log.Info($"Exported vanilla blocks to Storage/{FILE_NAME}", Log.PRINT_MSG, 10000);
        }

        public void Analyse(IMyCubeGrid mainGrid)
        {
            try
            {
                dlcs.Clear();
                mods.Clear();
                modsChangingVanilla.Clear();

                var grids = GetInfoFromGrids(mainGrid);
                GenerateShipInfo(mainGrid, grids);
            }
            finally
            {
                dlcs.Clear();
                mods.Clear();
                modsChangingVanilla.Clear();
            }
        }

        List<IMyCubeGrid> GetInfoFromGrids(IMyCubeGrid mainGrid)
        {
            var grids = MyAPIGateway.GridGroups.GetGroup(mainGrid, GridLinkTypeEnum.Physical);

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

            return grids;
        }

        void GenerateShipInfo(IMyCubeGrid mainGrid, List<IMyCubeGrid> grids)
        {
            sb.Clear();
            sb.Append("##### Blocks or skins from DLCs:").NewLine();

            if(dlcs.Count == 0)
            {
                sb.Append(" (None)").NewLine();
            }
            else
            {
                foreach(var kv in dlcs)
                {
                    var dlc = kv.Key;
                    var objects = kv.Value;

                    sb.Append("- ").Append(dlc).NewLine();

                    sb.Append("    ").Append(objects.Blocks).Append(" blocks and ").Append(objects.SkinnedBlocks).Append(" skinned blocks.").NewLine();
                    sb.NewLine();
                }
            }

            sb.NewLine();
            sb.Append("##### Blocks or skins from mods:").NewLine();

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

                    sb.Append("    ").Append(objects.Blocks).Append(" blocks and ").Append(objects.SkinnedBlocks).Append(" skinned blocks.").NewLine();
                    sb.NewLine();
                }
            }

            sb.NewLine();
            sb.Append("##### Vanilla blocks altered by mods:").NewLine();
            sb.Append("NOTE: This list doesn't include mods that alter blocks via scripts.").NewLine();
            sb.NewLine();

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

                    sb.Append("    ").Append(objects.Blocks).Append(" blocks").NewLine();
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

                        Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, $"Exported ship info to: %appdata%/SpaceEngineers/Storage/{modStorageName}/{fileName}", MyFontEnum.Green);
                    }
                    catch(Exception e)
                    {
                        Utils.ShowColoredChatMessage(BuildInfoMod.MOD_NAME, $"Failed to export ship info! Exception: {e.Message}; see SE log for details.", MyFontEnum.Red);
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
    }
}