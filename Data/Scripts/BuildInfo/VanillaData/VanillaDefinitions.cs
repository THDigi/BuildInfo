using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using ObjectBuilders.SafeZone;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.VanillaData
{
    public class VanillaDefinitions : ModComponent
    {
        public readonly HashSet<MyDefinitionId> Definitions = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        int WarnAtTick = 0;

        public VanillaDefinitions(BuildInfoMod main) : base(main)
        {
            if(Constants.ExportVanillaDefinitions)
            {
                ExtractVanillaBlocks();
            }

            DefineVanillaBlocks();
            CheckDefinitions();
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        void CheckDefinitions()
        {
            if(!BuildInfoMod.IsDevMod)
                return;

            bool needsRegen = false;

            foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                if(blockDef == null)
                    continue;

                if(!blockDef.Context.IsBaseGame)
                    continue;

                if(!Definitions.Contains(blockDef.Id))
                {
                    needsRegen = true;
                    Log.Info($"New vanilla block: {blockDef.Id}");
                    //break;
                }

                if(blockDef.MountPoints != null && blockDef.MountPoints.Length > 0)
                {
                    foreach(MyCubeBlockDefinition.MountPoint mount in blockDef.MountPoints)
                    {
                        if(mount.ExclusionMask > 3 || mount.PropertiesMask > 3)
                        {
                            Log.Info($"Vanilla block '{def.Id.ToString()}' has mountpoint with >3 masks: exclusionMask={mount.ExclusionMask}; propertiesMask={mount.PropertiesMask}!");
                        }
                    }
                }
            }

            if(needsRegen)
            {
                ExtractVanillaBlocks();

                if(BuildInfoMod.IsDevMod)
                {
                    WarnAtTick = Constants.TicksPerSecond * 3;
                    SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
                }
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            if(WarnAtTick > 0 && tick >= WarnAtTick)
            {
                WarnAtTick = 0;
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                Log.Info($"WARNING: some undeclared vanilla blocks detected! exported updated list.", Log.PRINT_MESSAGE);
            }
        }

        void ExtractVanillaBlocks()
        {
            const string FileName = "VanillaDefinitions.txt";

            StringBuilder sb = new StringBuilder();

            sb.Append("// Auto-generated vanilla definitions from SE v").Append(MyAPIGateway.Session.Version.ToString()).NewLine();

            Dictionary<MyObjectBuilderType, List<string>> perType = new Dictionary<MyObjectBuilderType, List<string>>();

            foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                if(blockDef == null)
                    continue;

                if(blockDef.Context == null)
                {
                    Log.Error($"Definition {def.Id} has null Context, how come?");
                    continue;
                }

                if(blockDef.Context.IsBaseGame)
                {
                    perType.GetOrAdd(def.Id.TypeId).Add(def.Id.SubtypeName);
                }
            }

            foreach(KeyValuePair<MyObjectBuilderType, List<string>> kv in perType.OrderBy(e => e.Key.ToString()))
            {
                sb.Append(nameof(AddBlocks)).Append('(');

                if(kv.Key == Hardcoded.TargetDummyType)
                    sb.Append(nameof(Hardcoded)).Append('.').Append(nameof(Hardcoded.TargetDummyType));
                else
                    sb.Append("typeof(").Append(kv.Key.ToString()).Append(')');

                sb.Append(", ");

                foreach(string subtype in kv.Value)
                {
                    sb.Append('"').Append(subtype).Append("\", ");
                }

                sb.Length -= 2; // remove last comma+space
                sb.Append(");\n");
            }

            using(TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(FileName, typeof(VanillaDefinitions)))
            {
                writer.Write(sb.ToString());
            }

            Log.Info($"Exported vanilla blocks to Storage/{FileName}", Log.PRINT_MESSAGE, 10000);
        }

        void AddBlocks(MyObjectBuilderType type, params string[] subtypes)
        {
            foreach(string subtype in subtypes)
            {
                Definitions.Add(new MyDefinitionId(type, subtype));
            }
        }

        void DefineVanillaBlocks()
        {
            // Auto-generated vanilla definitions from SE v1.201.12
            AddBlocks(typeof(MyObjectBuilder_CubeBlock), "LargeRailStraight", "LargeBlockArmorBlock", "LargeBlockArmorSlope", "LargeBlockArmorCorner", "LargeBlockArmorCornerInv", "LargeRoundArmor_Slope", "LargeRoundArmor_Corner", "LargeRoundArmor_CornerInv", "LargeHeavyBlockArmorBlock", "LargeHeavyBlockArmorSlope", "LargeHeavyBlockArmorCorner", "LargeHeavyBlockArmorCornerInv", "SmallBlockArmorBlock", "SmallBlockArmorSlope", "SmallBlockArmorCorner", "SmallBlockArmorCornerInv", "SmallHeavyBlockArmorBlock", "SmallHeavyBlockArmorSlope", "SmallHeavyBlockArmorCorner", "SmallHeavyBlockArmorCornerInv", "LargeHalfArmorBlock", "LargeHeavyHalfArmorBlock", "LargeHalfSlopeArmorBlock", "LargeHeavyHalfSlopeArmorBlock", "HalfArmorBlock", "HeavyHalfArmorBlock", "HalfSlopeArmorBlock", "HeavyHalfSlopeArmorBlock", "LargeBlockArmorRoundSlope", "LargeBlockArmorRoundCorner", "LargeBlockArmorRoundCornerInv", "LargeHeavyBlockArmorRoundSlope", "LargeHeavyBlockArmorRoundCorner", "LargeHeavyBlockArmorRoundCornerInv", "SmallBlockArmorRoundSlope", "SmallBlockArmorRoundCorner", "SmallBlockArmorRoundCornerInv", "SmallHeavyBlockArmorRoundSlope", "SmallHeavyBlockArmorRoundCorner", "SmallHeavyBlockArmorRoundCornerInv", "LargeBlockArmorSlope2Base", "LargeBlockArmorSlope2Tip", "LargeBlockArmorCorner2Base", "LargeBlockArmorCorner2Tip", "LargeBlockArmorInvCorner2Base", "LargeBlockArmorInvCorner2Tip", "LargeHeavyBlockArmorSlope2Base", "LargeHeavyBlockArmorSlope2Tip", "LargeHeavyBlockArmorCorner2Base", "LargeHeavyBlockArmorCorner2Tip", "LargeHeavyBlockArmorInvCorner2Base", "LargeHeavyBlockArmorInvCorner2Tip", "SmallBlockArmorSlope2Base", "SmallBlockArmorSlope2Tip", "SmallBlockArmorCorner2Base", "SmallBlockArmorCorner2Tip", "SmallBlockArmorInvCorner2Base", "SmallBlockArmorInvCorner2Tip", "SmallHeavyBlockArmorSlope2Base", "SmallHeavyBlockArmorSlope2Tip", "SmallHeavyBlockArmorCorner2Base", "SmallHeavyBlockArmorCorner2Tip", "SmallHeavyBlockArmorInvCorner2Base", "SmallHeavyBlockArmorInvCorner2Tip", "LargeBlockArmorCornerSquare", "SmallBlockArmorCornerSquare", "LargeBlockHeavyArmorCornerSquare", "SmallBlockHeavyArmorCornerSquare", "LargeBlockArmorCornerSquareInverted", "SmallBlockArmorCornerSquareInverted", "LargeBlockHeavyArmorCornerSquareInverted", "SmallBlockHeavyArmorCornerSquareInverted", "LargeBlockArmorHalfCorner", "SmallBlockArmorHalfCorner", "LargeBlockHeavyArmorHalfCorner", "SmallBlockHeavyArmorHalfCorner", "LargeBlockArmorHalfSlopeCorner", "SmallBlockArmorHalfSlopeCorner", "LargeBlockHeavyArmorHalfSlopeCorner", "SmallBlockHeavyArmorHalfSlopeCorner", "LargeBlockArmorHalfSlopeCornerInverted", "SmallBlockArmorHalfSlopeCornerInverted", "LargeBlockHeavyArmorHalfSlopeCornerInverted", "SmallBlockHeavyArmorHalfSlopeCornerInverted", "LargeBlockArmorHalfSlopedCorner", "SmallBlockArmorHalfSlopedCorner", "LargeBlockHeavyArmorHalfSlopedCorner", "SmallBlockHeavyArmorHalfSlopedCorner", "LargeBlockArmorHalfSlopedCornerBase", "SmallBlockArmorHalfSlopedCornerBase", "LargeBlockHeavyArmorHalfSlopedCornerBase", "SmallBlockHeavyArmorHalfSlopedCornerBase", "LargeBlockArmorHalfSlopeInverted", "SmallBlockArmorHalfSlopeInverted", "LargeBlockHeavyArmorHalfSlopeInverted", "SmallBlockHeavyArmorHalfSlopeInverted", "LargeBlockArmorSlopedCorner", "SmallBlockArmorSlopedCorner", "LargeBlockHeavyArmorSlopedCorner", "SmallBlockHeavyArmorSlopedCorner", "LargeBlockArmorSlopedCornerBase", "SmallBlockArmorSlopedCornerBase", "LargeBlockHeavyArmorSlopedCornerBase", "SmallBlockHeavyArmorSlopedCornerBase", "LargeBlockArmorSlopedCornerTip", "SmallBlockArmorSlopedCornerTip", "LargeBlockHeavyArmorSlopedCornerTip", "SmallBlockHeavyArmorSlopedCornerTip", "LargeBlockArmorRaisedSlopedCorner", "SmallBlockArmorRaisedSlopedCorner", "LargeBlockHeavyArmorRaisedSlopedCorner", "SmallBlockHeavyArmorRaisedSlopedCorner", "LargeBlockArmorSlopeTransition", "SmallBlockArmorSlopeTransition", "LargeBlockHeavyArmorSlopeTransition", "SmallBlockHeavyArmorSlopeTransition", "LargeBlockArmorSlopeTransitionBase", "SmallBlockArmorSlopeTransitionBase", "LargeBlockHeavyArmorSlopeTransitionBase", "SmallBlockHeavyArmorSlopeTransitionBase", "LargeBlockArmorSlopeTransitionBaseMirrored", "SmallBlockArmorSlopeTransitionBaseMirrored", "LargeBlockHeavyArmorSlopeTransitionBaseMirrored", "SmallBlockHeavyArmorSlopeTransitionBaseMirrored", "LargeBlockArmorSlopeTransitionMirrored", "SmallBlockArmorSlopeTransitionMirrored", "LargeBlockHeavyArmorSlopeTransitionMirrored", "SmallBlockHeavyArmorSlopeTransitionMirrored", "LargeBlockArmorSlopeTransitionTip", "SmallBlockArmorSlopeTransitionTip", "LargeBlockHeavyArmorSlopeTransitionTip", "SmallBlockHeavyArmorSlopeTransitionTip", "LargeBlockArmorSlopeTransitionTipMirrored", "SmallBlockArmorSlopeTransitionTipMirrored", "LargeBlockHeavyArmorSlopeTransitionTipMirrored", "SmallBlockHeavyArmorSlopeTransitionTipMirrored", "LargeBlockArmorSquareSlopedCornerBase", "SmallBlockArmorSquareSlopedCornerBase", "LargeBlockHeavyArmorSquareSlopedCornerBase", "SmallBlockHeavyArmorSquareSlopedCornerBase", "LargeBlockArmorSquareSlopedCornerTip", "SmallBlockArmorSquareSlopedCornerTip", "LargeBlockHeavyArmorSquareSlopedCornerTip", "SmallBlockHeavyArmorSquareSlopedCornerTip", "LargeBlockArmorSquareSlopedCornerTipInv", "SmallBlockArmorSquareSlopedCornerTipInv", "LargeBlockHeavyArmorSquareSlopedCornerTipInv", "SmallBlockHeavyArmorSquareSlopedCornerTipInv", "LargeArmorPanelLight", "LargeArmorSlopedSidePanelLight", "LargeArmorSlopedPanelLight", "LargeArmorHalfPanelLight", "LargeArmorQuarterPanelLight", "LargeArmor2x1SlopedPanelLight", "LargeArmor2x1SlopedPanelTipLight", "LargeArmor2x1SlopedSideBasePanelLight", "LargeArmor2x1SlopedSideTipPanelLight", "LargeArmor2x1SlopedSideBasePanelLightInv", "LargeArmor2x1SlopedSideTipPanelLightInv", "LargeArmorHalfSlopedPanelLight", "LargeArmor2x1HalfSlopedPanelLightRight", "LargeArmor2x1HalfSlopedTipPanelLightRight", "LargeArmor2x1HalfSlopedPanelLightLeft", "LargeArmor2x1HalfSlopedTipPanelLightLeft", "LargeArmorPanelHeavy", "LargeArmorSlopedSidePanelHeavy", "LargeArmorSlopedPanelHeavy", "LargeArmorHalfPanelHeavy", "LargeArmorQuarterPanelHeavy", "LargeArmor2x1SlopedPanelHeavy", "LargeArmor2x1SlopedPanelTipHeavy", "LargeArmor2x1SlopedSideBasePanelHeavy", "LargeArmor2x1SlopedSideTipPanelHeavy", "LargeArmor2x1SlopedSideBasePanelHeavyInv", "LargeArmor2x1SlopedSideTipPanelHeavyInv", "LargeArmorHalfSlopedPanelHeavy", "LargeArmor2x1HalfSlopedPanelHeavyRight", "LargeArmor2x1HalfSlopedTipPanelHeavyRight", "LargeArmor2x1HalfSlopedPanelHeavyLeft", "LargeArmor2x1HalfSlopedTipPanelHeavyLeft", "SmallArmorPanelLight", "SmallArmorSlopedSidePanelLight", "SmallArmorSlopedPanelLight", "SmallArmorHalfPanelLight", "SmallArmorQuarterPanelLight", "SmallArmor2x1SlopedPanelLight", "SmallArmor2x1SlopedPanelTipLight", "SmallArmor2x1SlopedSideBasePanelLight", "SmallArmor2x1SlopedSideTipPanelLight", "SmallArmor2x1SlopedSideBasePanelLightInv", "SmallArmor2x1SlopedSideTipPanelLightInv", "SmallArmorHalfSlopedPanelLight", "SmallArmor2x1HalfSlopedPanelLightRight", "SmallArmor2x1HalfSlopedTipPanelLightRight", "SmallArmor2x1HalfSlopedPanelLightLeft", "SmallArmor2x1HalfSlopedTipPanelLightLeft", "SmallArmorPanelHeavy", "SmallArmorSlopedSidePanelHeavy", "SmallArmorSlopedPanelHeavy", "SmallArmorHalfPanelHeavy", "SmallArmorQuarterPanelHeavy", "SmallArmor2x1SlopedPanelHeavy", "SmallArmor2x1SlopedPanelTipHeavy", "SmallArmor2x1SlopedSideBasePanelHeavy", "SmallArmor2x1SlopedSideTipPanelHeavy", "SmallArmor2x1SlopedSideBasePanelHeavyInv", "SmallArmor2x1SlopedSideTipPanelHeavyInv", "SmallArmorHalfSlopedPanelHeavy", "SmallArmor2x1HalfSlopedPanelHeavyRight", "SmallArmor2x1HalfSlopedTipPanelHeavyRight", "SmallArmor2x1HalfSlopedPanelHeavyLeft", "SmallArmor2x1HalfSlopedTipPanelHeavyLeft", "LargeBlockDeskChairless", "LargeBlockDeskChairlessCorner", "LargeBlockDeskChairlessCornerInv", "Shower", "WindowWall", "WindowWallLeft", "WindowWallRight", "Catwalk", "CatwalkCorner", "CatwalkStraight", "CatwalkWall", "CatwalkRailingEnd", "CatwalkRailingHalfRight", "CatwalkRailingHalfLeft", "CatwalkHalf", "CatwalkHalfRailing", "CatwalkHalfCenterRailing", "CatwalkHalfOuterRailing", "GratedStairs", "GratedHalfStairs", "GratedHalfStairsMirrored", "RailingStraight", "RailingDouble", "RailingCorner", "RailingDiagonal", "RailingHalfRight", "RailingHalfLeft", "RailingCenter", "Railing2x1Right", "Railing2x1Left", "Freight1", "Freight2", "Freight3", "ArmorCenter", "ArmorCorner", "ArmorInvCorner", "ArmorSide", "SmallArmorCenter", "SmallArmorCorner", "SmallArmorInvCorner", "SmallArmorSide", "Monolith", "Stereolith", "DeadAstronaut", "LargeDeadAstronaut", "EngineerPlushie", "DeadBody01", "DeadBody02", "DeadBody03", "DeadBody04", "DeadBody05", "DeadBody06", "LargeBlockCylindricalColumn", "SmallBlockCylindricalColumn", "LargeGridBeamBlock", "LargeGridBeamBlockSlope", "LargeGridBeamBlockRound", "LargeGridBeamBlockSlope2x1Base", "LargeGridBeamBlockSlope2x1Tip", "LargeGridBeamBlockHalf", "LargeGridBeamBlockHalfSlope", "LargeGridBeamBlockEnd", "LargeGridBeamBlockJunction", "LargeGridBeamBlockTJunction", "SmallGridBeamBlock", "SmallGridBeamBlockSlope", "SmallGridBeamBlockRound", "SmallGridBeamBlockSlope2x1Base", "SmallGridBeamBlockSlope2x1Tip", "SmallGridBeamBlockHalf", "SmallGridBeamBlockHalfSlope", "SmallGridBeamBlockEnd", "SmallGridBeamBlockJunction", "SmallGridBeamBlockTJunction", "Passage2", "Passage2Wall", "LargeStairs", "LargeRamp", "LargeSteelCatwalk", "LargeSteelCatwalk2Sides", "LargeSteelCatwalkCorner", "LargeSteelCatwalkPlate", "LargeCoverWall", "LargeCoverWallHalf", "LargeCoverWallHalfMirrored", "LargeBlockInteriorWall", "LargeInteriorPillar", "Viewport1", "Viewport2", "BarredWindow", "BarredWindowSlope", "BarredWindowSide", "BarredWindowFace", "StorageShelf1", "StorageShelf2", "StorageShelf3", "LargeBlockSciFiWall", "LargeBlockBarCounter", "LargeBlockBarCounterCorner", "LargeSymbolA", "LargeSymbolB", "LargeSymbolC", "LargeSymbolD", "LargeSymbolE", "LargeSymbolF", "LargeSymbolG", "LargeSymbolH", "LargeSymbolI", "LargeSymbolJ", "LargeSymbolK", "LargeSymbolL", "LargeSymbolM", "LargeSymbolN", "LargeSymbolO", "LargeSymbolP", "LargeSymbolQ", "LargeSymbolR", "LargeSymbolS", "LargeSymbolT", "LargeSymbolU", "LargeSymbolV", "LargeSymbolW", "LargeSymbolX", "LargeSymbolY", "LargeSymbolZ", "SmallSymbolA", "SmallSymbolB", "SmallSymbolC", "SmallSymbolD", "SmallSymbolE", "SmallSymbolF", "SmallSymbolG", "SmallSymbolH", "SmallSymbolI", "SmallSymbolJ", "SmallSymbolK", "SmallSymbolL", "SmallSymbolM", "SmallSymbolN", "SmallSymbolO", "SmallSymbolP", "SmallSymbolQ", "SmallSymbolR", "SmallSymbolS", "SmallSymbolT", "SmallSymbolU", "SmallSymbolV", "SmallSymbolW", "SmallSymbolX", "SmallSymbolY", "SmallSymbolZ", "LargeSymbol0", "LargeSymbol1", "LargeSymbol2", "LargeSymbol3", "LargeSymbol4", "LargeSymbol5", "LargeSymbol6", "LargeSymbol7", "LargeSymbol8", "LargeSymbol9", "SmallSymbol0", "SmallSymbol1", "SmallSymbol2", "SmallSymbol3", "SmallSymbol4", "SmallSymbol5", "SmallSymbol6", "SmallSymbol7", "SmallSymbol8", "SmallSymbol9", "LargeSymbolHyphen", "LargeSymbolUnderscore", "LargeSymbolDot", "LargeSymbolApostrophe", "LargeSymbolAnd", "LargeSymbolColon", "LargeSymbolExclamationMark", "LargeSymbolQuestionMark", "SmallSymbolHyphen", "SmallSymbolUnderscore", "SmallSymbolDot", "SmallSymbolApostrophe", "SmallSymbolAnd", "SmallSymbolColon", "SmallSymbolExclamationMark", "SmallSymbolQuestionMark", "FireCover", "FireCoverCorner", "HalfWindow", "HalfWindowInv", "HalfWindowCorner", "HalfWindowCornerInv", "HalfWindowDiagonal", "HalfWindowRound", "Embrasure", "PassageSciFi", "PassageSciFiWall", "PassageSciFiIntersection", "PassageSciFiGate", "PassageScifiCorner", "PassageSciFiTjunction", "PassageSciFiWindow", "BridgeWindow1x1Slope", "BridgeWindow1x1Face", "BridgeWindow1x1FaceInverted", "LargeWindowSquare", "LargeWindowEdge", "Window1x2Slope", "Window1x2Inv", "Window1x2Face", "Window1x2SideLeft", "Window1x2SideLeftInv", "Window1x2SideRight", "Window1x2SideRightInv", "Window1x1Slope", "Window1x1Face", "Window1x1Side", "Window1x1SideInv", "Window1x1Inv", "Window1x2Flat", "Window1x2FlatInv", "Window1x1Flat", "Window1x1FlatInv", "Window3x3Flat", "Window3x3FlatInv", "Window2x3Flat", "Window2x3FlatInv", "SmallWindow1x2Slope", "SmallWindow1x2Inv", "SmallWindow1x2Face", "SmallWindow1x2SideLeft", "SmallWindow1x2SideLeftInv", "SmallWindow1x2SideRight", "SmallWindow1x2SideRightInv", "SmallWindow1x1Slope", "SmallWindow1x1Face", "SmallWindow1x1Side", "SmallWindow1x1SideInv", "SmallWindow1x1Inv", "SmallWindow1x2Flat", "SmallWindow1x2FlatInv", "SmallWindow1x1Flat", "SmallWindow1x1FlatInv", "SmallWindow3x3Flat", "SmallWindow3x3FlatInv", "SmallWindow2x3Flat", "SmallWindow2x3FlatInv", "WindowRound", "WindowRoundInv", "WindowRoundCorner", "WindowRoundCornerInv", "WindowRoundFace", "WindowRoundFaceInv", "WindowRoundInwardsCorner", "WindowRoundInwardsCornerInv", "SmallWindowRound", "SmallWindowRoundInv", "SmallWindowRoundCorner", "SmallWindowRoundCornerInv", "SmallWindowRoundFace", "SmallWindowRoundFaceInv", "SmallWindowRoundInwardsCorner", "SmallWindowRoundInwardsCornerInv");
            AddBlocks(typeof(MyObjectBuilder_DebugSphere1), "DebugSphereLarge");
            AddBlocks(typeof(MyObjectBuilder_DebugSphere2), "DebugSphereLarge");
            AddBlocks(typeof(MyObjectBuilder_DebugSphere3), "DebugSphereLarge");
            AddBlocks(typeof(MyObjectBuilder_MyProgrammableBlock), "SmallProgrammableBlock", "LargeProgrammableBlock");
            AddBlocks(typeof(MyObjectBuilder_Projector), "LargeProjector", "SmallProjector", "LargeBlockConsole");
            AddBlocks(typeof(MyObjectBuilder_SensorBlock), "SmallBlockSensor", "LargeBlockSensor");
            AddBlocks(Hardcoded.TargetDummyType, "TargetDummy");
            AddBlocks(typeof(MyObjectBuilder_SoundBlock), "SmallBlockSoundBlock", "LargeBlockSoundBlock");
            AddBlocks(typeof(MyObjectBuilder_ButtonPanel), "ButtonPanelLarge", "ButtonPanelSmall", "VerticalButtonPanelLarge", "VerticalButtonPanelSmall", "LargeSciFiButtonTerminal", "LargeSciFiButtonPanel");
            AddBlocks(typeof(MyObjectBuilder_TimerBlock), "TimerBlockLarge", "TimerBlockSmall");
            AddBlocks(typeof(MyObjectBuilder_TurretControlBlock), "LargeTurretControlBlock", "SmallTurretControlBlock");
            AddBlocks(typeof(MyObjectBuilder_RadioAntenna), "LargeBlockRadioAntenna", "SmallBlockRadioAntenna", "LargeBlockRadioAntennaDish");
            AddBlocks(typeof(MyObjectBuilder_Beacon), "LargeBlockBeacon", "SmallBlockBeacon");
            AddBlocks(typeof(MyObjectBuilder_RemoteControl), "LargeBlockRemoteControl", "SmallBlockRemoteControl");
            AddBlocks(typeof(MyObjectBuilder_LaserAntenna), "LargeBlockLaserAntenna", "SmallBlockLaserAntenna");
            AddBlocks(typeof(MyObjectBuilder_TerminalBlock), "ControlPanel", "SmallControlPanel", "LargeBlockSciFiTerminal");
            AddBlocks(typeof(MyObjectBuilder_Cockpit), "LargeBlockCockpit", "LargeBlockCockpitSeat", "SmallBlockCockpit", "DBSmallBlockFighterCockpit", "CockpitOpen", "RoverCockpit", "OpenCockpitSmall", "OpenCockpitLarge", "LargeBlockDesk", "LargeBlockDeskCorner", "LargeBlockDeskCornerInv", "LargeBlockCouch", "LargeBlockCouchCorner", "LargeBlockBathroomOpen", "LargeBlockBathroom", "LargeBlockToilet", "SmallBlockCockpitIndustrial", "LargeBlockCockpitIndustrial", "PassengerSeatLarge", "PassengerSeatSmall", "PassengerSeatSmallNew", "PassengerSeatSmallOffset", "BuggyCockpit", "PassengerBench", "SmallBlockStandingCockpit", "LargeBlockStandingCockpit");
            AddBlocks(typeof(MyObjectBuilder_Gyro), "LargeBlockGyro", "SmallBlockGyro");
            AddBlocks(typeof(MyObjectBuilder_Kitchen), "LargeBlockKitchen");
            AddBlocks(typeof(MyObjectBuilder_CryoChamber), "LargeBlockBed", "LargeBlockCryoChamber", "SmallBlockCryoChamber");
            AddBlocks(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLockerRoom", "LargeBlockLockerRoomCorner", "LargeBlockLockers", "LargeBlockLargeIndustrialContainer", "SmallBlockSmallContainer", "SmallBlockMediumContainer", "SmallBlockLargeContainer", "LargeBlockSmallContainer", "LargeBlockLargeContainer", "LargeBlockWeaponRack", "SmallBlockWeaponRack");
            AddBlocks(typeof(MyObjectBuilder_Planter), "LargeBlockPlanters");
            AddBlocks(typeof(MyObjectBuilder_VendingMachine), "FoodDispenser", "VendingMachine");
            AddBlocks(typeof(MyObjectBuilder_Jukebox), "Jukebox");
            AddBlocks(typeof(MyObjectBuilder_LCDPanelsBlock), "LabEquipment", "MedicalStation");
            AddBlocks(typeof(MyObjectBuilder_TextPanel), "TransparentLCDLarge", "TransparentLCDSmall", "SmallTextPanel", "SmallLCDPanelWide", "SmallLCDPanel", "LargeBlockCorner_LCD_1", "LargeBlockCorner_LCD_2", "LargeBlockCorner_LCD_Flat_1", "LargeBlockCorner_LCD_Flat_2", "SmallBlockCorner_LCD_1", "SmallBlockCorner_LCD_2", "SmallBlockCorner_LCD_Flat_1", "SmallBlockCorner_LCD_Flat_2", "LargeTextPanel", "LargeLCDPanel", "LargeLCDPanelWide", "LargeLCDPanel5x5", "LargeLCDPanel5x3", "LargeLCDPanel3x3");
            AddBlocks(typeof(MyObjectBuilder_ReflectorLight), "RotatingLightLarge", "RotatingLightSmall", "LargeBlockFrontLight", "SmallBlockFrontLight", "OffsetSpotlight");
            AddBlocks(typeof(MyObjectBuilder_Door), "", "SmallDoor", "LargeBlockGate", "LargeBlockOffsetDoor", "SmallSideDoor", "SlidingHatchDoor", "SlidingHatchDoorHalf");
            AddBlocks(typeof(MyObjectBuilder_AirtightHangarDoor), "", "AirtightHangarDoorWarfare2A", "AirtightHangarDoorWarfare2B", "AirtightHangarDoorWarfare2C");
            AddBlocks(typeof(MyObjectBuilder_AirtightSlideDoor), "LargeBlockSlideDoor");
            AddBlocks(typeof(MyObjectBuilder_StoreBlock), "StoreBlock", "AtmBlock");
            AddBlocks(typeof(MyObjectBuilder_SafeZoneBlock), "SafeZoneBlock");
            AddBlocks(typeof(MyObjectBuilder_ContractBlock), "ContractBlock");
            AddBlocks(typeof(MyObjectBuilder_BatteryBlock), "LargeBlockBatteryBlock", "SmallBlockBatteryBlock", "SmallBlockSmallBatteryBlock", "LargeBlockBatteryBlockWarfare2", "SmallBlockBatteryBlockWarfare2");
            AddBlocks(typeof(MyObjectBuilder_Reactor), "SmallBlockSmallGenerator", "SmallBlockLargeGenerator", "LargeBlockSmallGenerator", "LargeBlockLargeGenerator", "LargeBlockSmallGeneratorWarfare2", "LargeBlockLargeGeneratorWarfare2", "SmallBlockSmallGeneratorWarfare2", "SmallBlockLargeGeneratorWarfare2");
            AddBlocks(typeof(MyObjectBuilder_HydrogenEngine), "LargeHydrogenEngine", "SmallHydrogenEngine");
            AddBlocks(typeof(MyObjectBuilder_WindTurbine), "LargeBlockWindTurbine");
            AddBlocks(typeof(MyObjectBuilder_SolarPanel), "LargeBlockSolarPanel", "SmallBlockSolarPanel");
            AddBlocks(typeof(MyObjectBuilder_GravityGenerator), "");
            AddBlocks(typeof(MyObjectBuilder_GravityGeneratorSphere), "");
            AddBlocks(typeof(MyObjectBuilder_VirtualMass), "VirtualMassLarge", "VirtualMassSmall");
            AddBlocks(typeof(MyObjectBuilder_SpaceBall), "SpaceBallLarge", "SpaceBallSmall");
            AddBlocks(typeof(MyObjectBuilder_LandingGear), "LargeBlockMagneticPlate", "SmallBlockMagneticPlate", "LargeBlockLandingGear", "SmallBlockLandingGear", "LargeBlockSmallMagneticPlate", "SmallBlockSmallMagneticPlate");
            AddBlocks(typeof(MyObjectBuilder_ConveyorConnector), "LargeBlockConveyorPipeSeamless", "LargeBlockConveyorPipeCorner", "LargeBlockConveyorPipeFlange", "LargeBlockConveyorPipeEnd", "ConveyorTube", "ConveyorTubeDuct", "ConveyorTubeDuctCurved", "ConveyorTubeSmall", "ConveyorTubeDuctSmall", "ConveyorTubeDuctSmallCurved", "ConveyorTubeMedium", "ConveyorFrameMedium", "ConveyorTubeCurved", "ConveyorTubeSmallCurved", "ConveyorTubeCurvedMedium");
            AddBlocks(typeof(MyObjectBuilder_Conveyor), "LargeBlockConveyorPipeJunction", "LargeBlockConveyorPipeIntersection", "LargeBlockConveyorPipeT", "SmallBlockConveyor", "SmallBlockConveyorConverter", "LargeBlockConveyor", "ConveyorTubeDuctT", "ConveyorTubeDuctSmallT", "SmallShipConveyorHub", "ConveyorTubeSmallT", "ConveyorTubeT");
            AddBlocks(typeof(MyObjectBuilder_OxygenTank), "LargeHydrogenTankIndustrial", "OxygenTankSmall", "", "LargeHydrogenTank", "LargeHydrogenTankSmall", "SmallHydrogenTank", "SmallHydrogenTankSmall");
            AddBlocks(typeof(MyObjectBuilder_Assembler), "LargeAssemblerIndustrial", "LargeAssembler", "BasicAssembler");
            AddBlocks(typeof(MyObjectBuilder_Refinery), "LargeRefineryIndustrial", "LargeRefinery", "Blast Furnace");
            AddBlocks(typeof(MyObjectBuilder_ConveyorSorter), "LargeBlockConveyorSorterIndustrial", "LargeBlockConveyorSorter", "MediumBlockConveyorSorter", "SmallBlockConveyorSorter");
            AddBlocks(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeHydrogenThrustIndustrial", "LargeBlockSmallHydrogenThrustIndustrial", "SmallBlockLargeHydrogenThrustIndustrial", "SmallBlockSmallHydrogenThrustIndustrial", "SmallBlockSmallThrustSciFi", "SmallBlockLargeThrustSciFi", "LargeBlockSmallThrustSciFi", "LargeBlockLargeThrustSciFi", "LargeBlockLargeAtmosphericThrustSciFi", "LargeBlockSmallAtmosphericThrustSciFi", "SmallBlockLargeAtmosphericThrustSciFi", "SmallBlockSmallAtmosphericThrustSciFi", "SmallBlockSmallThrust", "SmallBlockLargeThrust", "LargeBlockSmallThrust", "LargeBlockLargeThrust", "LargeBlockLargeHydrogenThrust", "LargeBlockSmallHydrogenThrust", "SmallBlockLargeHydrogenThrust", "SmallBlockSmallHydrogenThrust", "LargeBlockLargeAtmosphericThrust", "LargeBlockSmallAtmosphericThrust", "SmallBlockLargeAtmosphericThrust", "SmallBlockSmallAtmosphericThrust", "SmallBlockSmallModularThruster", "SmallBlockLargeModularThruster", "LargeBlockSmallModularThruster", "LargeBlockLargeModularThruster");
            AddBlocks(typeof(MyObjectBuilder_Passage), "");
            AddBlocks(typeof(MyObjectBuilder_Ladder2), "", "LadderShaft", "LadderSmall");
            AddBlocks(typeof(MyObjectBuilder_InteriorLight), "SmallLight", "SmallBlockSmallLight", "LargeBlockLight_1corner", "LargeBlockLight_2corner", "SmallBlockLight_1corner", "SmallBlockLight_2corner", "OffsetLight", "PassageSciFiLight", "LargeLightPanel", "SmallLightPanel");
            AddBlocks(typeof(MyObjectBuilder_AirVent), "", "SmallAirVent");
            AddBlocks(typeof(MyObjectBuilder_Collector), "Collector", "CollectorSmall");
            AddBlocks(typeof(MyObjectBuilder_ShipConnector), "Connector", "ConnectorSmall", "ConnectorMedium");
            AddBlocks(typeof(MyObjectBuilder_PistonBase), "LargePistonBase", "SmallPistonBase");
            AddBlocks(typeof(MyObjectBuilder_ExtendedPistonBase), "LargePistonBase", "SmallPistonBase");
            AddBlocks(typeof(MyObjectBuilder_PistonTop), "LargePistonTop", "SmallPistonTop");
            AddBlocks(typeof(MyObjectBuilder_MotorStator), "LargeStator", "SmallStator");
            AddBlocks(typeof(MyObjectBuilder_MotorRotor), "LargeRotor", "SmallRotor");
            AddBlocks(typeof(MyObjectBuilder_MotorAdvancedStator), "LargeAdvancedStator", "SmallAdvancedStator", "SmallAdvancedStatorSmall", "LargeHinge", "MediumHinge", "SmallHinge");
            AddBlocks(typeof(MyObjectBuilder_MotorAdvancedRotor), "LargeAdvancedRotor", "SmallAdvancedRotor", "SmallAdvancedRotorSmall", "LargeHingeHead", "MediumHingeHead", "SmallHingeHead");
            AddBlocks(typeof(MyObjectBuilder_MedicalRoom), "LargeMedicalRoom");
            AddBlocks(typeof(MyObjectBuilder_OxygenGenerator), "", "OxygenGeneratorSmall");
            AddBlocks(typeof(MyObjectBuilder_SurvivalKit), "SurvivalKitLarge", "SurvivalKit");
            AddBlocks(typeof(MyObjectBuilder_OxygenFarm), "LargeBlockOxygenFarm");
            AddBlocks(typeof(MyObjectBuilder_UpgradeModule), "LargeProductivityModule", "LargeEffectivenessModule", "LargeEnergyModule");
            AddBlocks(typeof(MyObjectBuilder_ExhaustBlock), "SmallExhaustPipe", "LargeExhaustPipe");
            AddBlocks(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension3x3", "OffroadSuspension5x5", "OffroadSuspension1x1", "OffroadSmallSuspension3x3", "OffroadSmallSuspension5x5", "OffroadSmallSuspension1x1", "OffroadSuspension3x3mirrored", "OffroadSuspension5x5mirrored", "OffroadSuspension1x1mirrored", "OffroadSmallSuspension3x3mirrored", "OffroadSmallSuspension5x5mirrored", "OffroadSmallSuspension1x1mirrored", "Suspension3x3", "Suspension5x5", "Suspension1x1", "SmallSuspension3x3", "SmallSuspension5x5", "SmallSuspension1x1", "Suspension3x3mirrored", "Suspension5x5mirrored", "Suspension1x1mirrored", "SmallSuspension3x3mirrored", "SmallSuspension5x5mirrored", "SmallSuspension1x1mirrored");
            AddBlocks(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel1x1", "OffroadSmallRealWheel", "OffroadSmallRealWheel5x5", "OffroadRealWheel1x1", "OffroadRealWheel", "OffroadRealWheel5x5", "OffroadSmallRealWheel1x1mirrored", "OffroadSmallRealWheelmirrored", "OffroadSmallRealWheel5x5mirrored", "OffroadRealWheel1x1mirrored", "OffroadRealWheelmirrored", "OffroadRealWheel5x5mirrored", "OffroadWheel1x1", "OffroadSmallWheel1x1", "OffroadWheel3x3", "OffroadSmallWheel3x3", "OffroadWheel5x5", "OffroadSmallWheel5x5", "SmallRealWheel1x1", "SmallRealWheel", "SmallRealWheel5x5", "RealWheel1x1", "RealWheel", "RealWheel5x5", "SmallRealWheel1x1mirrored", "SmallRealWheelmirrored", "SmallRealWheel5x5mirrored", "RealWheel1x1mirrored", "RealWheelmirrored", "RealWheel5x5mirrored", "Wheel1x1", "SmallWheel1x1", "Wheel3x3", "SmallWheel3x3", "Wheel5x5", "SmallWheel5x5");
            AddBlocks(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraight1", "LargeNeonTubesStraight2", "LargeNeonTubesCorner", "LargeNeonTubesBendUp", "LargeNeonTubesBendDown", "LargeNeonTubesStraightEnd1", "LargeNeonTubesStraightEnd2", "LargeNeonTubesStraightDown", "LargeNeonTubesU", "LargeNeonTubesT", "LargeNeonTubesCircle", "SmallNeonTubesStraight1", "SmallNeonTubesStraight2", "SmallNeonTubesCorner", "SmallNeonTubesBendUp", "SmallNeonTubesBendDown", "SmallNeonTubesStraightDown", "SmallNeonTubesStraightEnd1", "SmallNeonTubesU", "SmallNeonTubesT", "SmallNeonTubesCircle");
            AddBlocks(typeof(MyObjectBuilder_Drill), "SmallBlockDrill", "LargeBlockDrill");
            AddBlocks(typeof(MyObjectBuilder_ShipGrinder), "LargeShipGrinder", "SmallShipGrinder");
            AddBlocks(typeof(MyObjectBuilder_ShipWelder), "LargeShipWelder", "SmallShipWelder");
            AddBlocks(typeof(MyObjectBuilder_OreDetector), "LargeOreDetector", "SmallBlockOreDetector");
            AddBlocks(typeof(MyObjectBuilder_JumpDrive), "LargeJumpDrive");
            AddBlocks(typeof(MyObjectBuilder_CameraBlock), "SmallCameraBlock", "LargeCameraBlock");
            AddBlocks(typeof(MyObjectBuilder_MergeBlock), "LargeShipMergeBlock", "SmallShipMergeBlock", "SmallShipSmallMergeBlock");
            AddBlocks(typeof(MyObjectBuilder_Parachute), "LgParachute", "SmParachute");
            AddBlocks(typeof(MyObjectBuilder_SmallMissileLauncher), "SmallMissileLauncherWarfare2", "", "LargeMissileLauncher", "LargeBlockLargeCalibreGun");
            AddBlocks(typeof(MyObjectBuilder_SmallGatlingGun), "SmallGatlingGunWarfare2", "", "SmallBlockAutocannon");
            AddBlocks(typeof(MyObjectBuilder_Searchlight), "SmallSearchlight", "LargeSearchlight");
            AddBlocks(typeof(MyObjectBuilder_HeatVentBlock), "LargeHeatVentBlock", "SmallHeatVentBlock");
            AddBlocks(typeof(MyObjectBuilder_Warhead), "LargeWarhead", "SmallWarhead");
            AddBlocks(typeof(MyObjectBuilder_Decoy), "LargeDecoy", "SmallDecoy");
            AddBlocks(typeof(MyObjectBuilder_LargeGatlingTurret), "", "SmallGatlingTurret", "AutoCannonTurret");
            AddBlocks(typeof(MyObjectBuilder_LargeMissileTurret), "", "SmallMissileTurret", "LargeCalibreTurret", "LargeBlockMediumCalibreTurret", "SmallBlockMediumCalibreTurret");
            AddBlocks(typeof(MyObjectBuilder_InteriorTurret), "LargeInteriorTurret");
            AddBlocks(typeof(MyObjectBuilder_SmallMissileLauncherReload), "SmallRocketLauncherReload", "SmallBlockMediumCalibreGun", "LargeRailgun", "SmallRailgun");
        }
    }
}
