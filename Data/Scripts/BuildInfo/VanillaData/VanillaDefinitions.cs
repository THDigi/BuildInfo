﻿using System.Collections.Generic;
using System.IO;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using ObjectBuilders.SafeZone;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;

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

            foreach(MyDefinitionBase def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                MyCubeBlockDefinition blockDef = def as MyCubeBlockDefinition;
                if(blockDef != null && blockDef.Context.IsBaseGame)
                {
                    sb.Append(nameof(Definitions)).Append(".Add(new MyDefinitionId(typeof(").Append(blockDef.Id.TypeId.ToString()).Append("), \"").Append(blockDef.Id.SubtypeName).Append("\"));").AppendLine();
                }
            }

            using(TextWriter writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(FileName, typeof(VanillaDefinitions)))
            {
                writer.Write(sb.ToString());
            }

            Log.Info($"Exported vanilla blocks to Storage/{FileName}", Log.PRINT_MESSAGE, 10000);
        }

        void DefineVanillaBlocks()
        {
            // Auto-generated vanilla definitions from SE v1.200.25
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRailStraight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_DebugSphere1), "DebugSphereLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_DebugSphere2), "DebugSphereLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_DebugSphere3), "DebugSphereLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRoundArmor_Slope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRoundArmor_Corner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRoundArmor_CornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorCornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorCornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHalfArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyHalfArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHalfSlopeArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyHalfSlopeArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HalfArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HeavyHalfArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HalfSlopeArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HeavyHalfSlopeArmorBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorRoundSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorRoundCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorRoundCornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorRoundSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorRoundCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorRoundCornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorRoundSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorRoundCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorRoundCornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorRoundSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorRoundCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorRoundCornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlope2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlope2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCorner2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCorner2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorInvCorner2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorInvCorner2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorSlope2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorSlope2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorCorner2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorCorner2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorInvCorner2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeHeavyBlockArmorInvCorner2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlope2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlope2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCorner2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCorner2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorInvCorner2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorInvCorner2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorSlope2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorSlope2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorCorner2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorCorner2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorInvCorner2Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallHeavyBlockArmorInvCorner2Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCornerSquare"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCornerSquare"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorCornerSquare"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorCornerSquare"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorCornerSquareInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorCornerSquareInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorCornerSquareInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorCornerSquareInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopeCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopeCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopeCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopeCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopeCornerInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopeCornerInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopeCornerInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopeCornerInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopedCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopedCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopedCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopedCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopedCornerBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopedCornerBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopedCornerBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopedCornerBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorHalfSlopeInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorHalfSlopeInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorHalfSlopeInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorHalfSlopeInverted"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlopedCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlopedCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorSlopedCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorSlopedCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlopedCornerBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlopedCornerBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorSlopedCornerBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorSlopedCornerBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockArmorSlopedCornerTip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockArmorSlopedCornerTip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockHeavyArmorSlopedCornerTip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockHeavyArmorSlopedCornerTip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorSlopedSidePanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorSlopedPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorHalfPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorQuarterPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedPanelTipLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedSideBasePanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedSideTipPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedSideBasePanelLightInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedSideTipPanelLightInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorHalfSlopedPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1HalfSlopedPanelLightRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1HalfSlopedTipPanelLightRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1HalfSlopedPanelLightLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1HalfSlopedTipPanelLightLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorSlopedSidePanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorSlopedPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorHalfPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorQuarterPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedPanelTipHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedSideBasePanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedSideTipPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedSideBasePanelHeavyInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1SlopedSideTipPanelHeavyInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmorHalfSlopedPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1HalfSlopedPanelHeavyRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1HalfSlopedTipPanelHeavyRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1HalfSlopedPanelHeavyLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeArmor2x1HalfSlopedTipPanelHeavyLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorSlopedSidePanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorSlopedPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorHalfPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorQuarterPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedPanelTipLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedSideBasePanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedSideTipPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedSideBasePanelLightInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedSideTipPanelLightInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorHalfSlopedPanelLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1HalfSlopedPanelLightRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1HalfSlopedTipPanelLightRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1HalfSlopedPanelLightLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1HalfSlopedTipPanelLightLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorSlopedSidePanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorSlopedPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorHalfPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorQuarterPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedPanelTipHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedSideBasePanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedSideTipPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedSideBasePanelHeavyInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1SlopedSideTipPanelHeavyInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorHalfSlopedPanelHeavy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1HalfSlopedPanelHeavyRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1HalfSlopedTipPanelHeavyRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1HalfSlopedPanelHeavyLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmor2x1HalfSlopedTipPanelHeavyLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MyProgrammableBlock), "SmallProgrammableBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Projector), "LargeProjector"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Projector), "SmallProjector"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SensorBlock), "SmallBlockSensor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SensorBlock), "LargeBlockSensor"));
            Definitions.Add(new MyDefinitionId(Hardcoded.TargetDummyType, "TargetDummy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SoundBlock), "SmallBlockSoundBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SoundBlock), "LargeBlockSoundBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "ButtonPanelLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "ButtonPanelSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TimerBlock), "TimerBlockLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TimerBlock), "TimerBlockSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MyProgrammableBlock), "LargeProgrammableBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RadioAntenna), "LargeBlockRadioAntenna"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Beacon), "LargeBlockBeacon"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Beacon), "SmallBlockBeacon"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RadioAntenna), "SmallBlockRadioAntenna"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RemoteControl), "LargeBlockRemoteControl"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RemoteControl), "SmallBlockRemoteControl"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LaserAntenna), "LargeBlockLaserAntenna"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LaserAntenna), "SmallBlockLaserAntenna"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TerminalBlock), "ControlPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TerminalBlock), "SmallControlPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCockpit"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCockpitSeat"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "SmallBlockCockpit"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "DBSmallBlockFighterCockpit"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "CockpitOpen"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "RoverCockpit"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Gyro), "LargeBlockGyro"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Gyro), "SmallBlockGyro"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "OpenCockpitSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "OpenCockpitLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockDesk"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockDeskCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockDeskChairless"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockDeskChairlessCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Kitchen), "LargeBlockKitchen"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CryoChamber), "LargeBlockBed"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLockerRoom"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLockerRoomCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Planter), "LargeBlockPlanters"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCouch"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCouchCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLockers"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockBathroomOpen"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockBathroom"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockToilet"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Projector), "LargeBlockConsole"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "SmallBlockCockpitIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockCockpitIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_VendingMachine), "FoodDispenser"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Jukebox), "Jukebox"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LCDPanelsBlock), "LabEquipment"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Shower"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "WindowWall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "WindowWallLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "WindowWallRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LCDPanelsBlock), "MedicalStation"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "TransparentLCDLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "TransparentLCDSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Catwalk"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkStraight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkWall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkRailingEnd"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkRailingHalfRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "CatwalkRailingHalfLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "GratedStairs"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "GratedHalfStairs"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "GratedHalfStairsMirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingStraight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingDouble"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingDiagonal"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingHalfRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "RailingHalfLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "RotatingLightLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "RotatingLightSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Freight1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Freight2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Freight3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "SmallDoor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirtightHangarDoor), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirtightSlideDoor), "LargeBlockSlideDoor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "ArmorCenter"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "ArmorCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "ArmorInvCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "ArmorSide"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorCenter"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorInvCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallArmorSide"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_StoreBlock), "StoreBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SafeZoneBlock), "SafeZoneBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ContractBlock), "ContractBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_VendingMachine), "VendingMachine"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_StoreBlock), "AtmBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "LargeBlockBatteryBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "SmallBlockBatteryBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "SmallBlockSmallBatteryBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "SmallBlockSmallGenerator"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "SmallBlockLargeGenerator"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "LargeBlockSmallGenerator"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "LargeBlockLargeGenerator"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_HydrogenEngine), "LargeHydrogenEngine"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_HydrogenEngine), "SmallHydrogenEngine"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_WindTurbine), "LargeBlockWindTurbine"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SolarPanel), "LargeBlockSolarPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SolarPanel), "SmallBlockSolarPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Monolith"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Stereolith"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadAstronaut"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeDeadAstronaut"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_RadioAntenna), "LargeBlockRadioAntennaDish"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "LargeBlockGate"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "LargeBlockOffsetDoor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody01"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody02"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody03"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody04"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody05"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "DeadBody06"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_GravityGenerator), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_GravityGeneratorSphere), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_VirtualMass), "VirtualMassLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_VirtualMass), "VirtualMassSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SpaceBall), "SpaceBallLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SpaceBall), "SpaceBallSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LandingGear), "LargeBlockMagneticPlate"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LandingGear), "SmallBlockMagneticPlate"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLargeIndustrialContainer"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "VerticalButtonPanelLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "VerticalButtonPanelSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "LargeBlockConveyorPipeSeamless"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "LargeBlockConveyorPipeCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Conveyor), "LargeBlockConveyorPipeJunction"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Conveyor), "LargeBlockConveyorPipeIntersection"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "LargeBlockConveyorPipeFlange"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "LargeBlockConveyorPipeEnd"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "LargeHydrogenTankIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "LargeAssemblerIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Refinery), "LargeRefineryIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockCylindricalColumn"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallBlockCylindricalColumn"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorSorter), "LargeBlockConveyorSorterIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlockSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlockRound"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlockSlope2x1Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlockSlope2x1Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlockHalf"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlockHalfSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlockEnd"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlockJunction"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeGridBeamBlockTJunction"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlockSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlockRound"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlockSlope2x1Base"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlockSlope2x1Tip"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlockHalf"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlockHalfSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlockEnd"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlockJunction"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallGridBeamBlockTJunction"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeHydrogenThrustIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallHydrogenThrustIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeHydrogenThrustIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallHydrogenThrustIndustrial"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Passage), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Passage2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Passage2Wall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeStairs"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeRamp"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSteelCatwalk"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSteelCatwalk2Sides"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSteelCatwalkCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSteelCatwalkPlate"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeCoverWall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeCoverWallHalf"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockInteriorWall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeInteriorPillar"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "PassengerSeatLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "PassengerSeatSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "PassengerSeatSmallNew"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "PassengerSeatSmallOffset"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Ladder2), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Ladder2), "LadderSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallTextPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallLCDPanelWide"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallLCDPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeBlockCorner_LCD_1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeBlockCorner_LCD_2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeBlockCorner_LCD_Flat_1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeBlockCorner_LCD_Flat_2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallBlockCorner_LCD_1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallBlockCorner_LCD_2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallBlockCorner_LCD_Flat_1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "SmallBlockCorner_LCD_Flat_2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeTextPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanelWide"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "LargeBlockFrontLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "SmallBlockFrontLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "SmallLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "SmallBlockSmallLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_1corner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "LargeBlockLight_2corner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_1corner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "SmallBlockLight_2corner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "OxygenTankSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "LargeHydrogenTank"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "LargeHydrogenTankSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "SmallHydrogenTank"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenTank), "SmallHydrogenTankSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirVent), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirVent), "SmallAirVent"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "SmallBlockSmallContainer"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "SmallBlockMediumContainer"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "SmallBlockLargeContainer"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockSmallContainer"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockLargeContainer"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Conveyor), "SmallBlockConveyor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Conveyor), "LargeBlockConveyor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Collector), "Collector"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Collector), "CollectorSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipConnector), "Connector"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipConnector), "ConnectorSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipConnector), "ConnectorMedium"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTube"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeMedium"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorFrameMedium"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeCurved"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeSmallCurved"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorConnector), "ConveyorTubeCurvedMedium"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Conveyor), "SmallShipConveyorHub"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorSorter), "LargeBlockConveyorSorter"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorSorter), "MediumBlockConveyorSorter"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ConveyorSorter), "SmallBlockConveyorSorter"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_PistonBase), "LargePistonBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ExtendedPistonBase), "LargePistonBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_PistonTop), "LargePistonTop"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_PistonBase), "SmallPistonBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ExtendedPistonBase), "SmallPistonBase"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_PistonTop), "SmallPistonTop"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorStator), "LargeStator"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorRotor), "LargeRotor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorStator), "SmallStator"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorRotor), "SmallRotor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "LargeAdvancedStator"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "LargeAdvancedRotor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "SmallAdvancedStator"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "SmallAdvancedRotor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "LargeHinge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "LargeHingeHead"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "MediumHinge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "MediumHingeHead"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedStator), "SmallHinge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorAdvancedRotor), "SmallHingeHead"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MedicalRoom), "LargeMedicalRoom"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CryoChamber), "LargeBlockCryoChamber"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CryoChamber), "SmallBlockCryoChamber"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Refinery), "LargeRefinery"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Refinery), "Blast Furnace"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenGenerator), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenGenerator), "OxygenGeneratorSmall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "LargeAssembler"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Assembler), "BasicAssembler"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SurvivalKit), "SurvivalKitLarge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SurvivalKit), "SurvivalKit"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OxygenFarm), "LargeBlockOxygenFarm"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_UpgradeModule), "LargeProductivityModule"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_UpgradeModule), "LargeEffectivenessModule"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_UpgradeModule), "LargeEnergyModule"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ExhaustBlock), "SmallExhaustPipe"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ExhaustBlock), "LargeExhaustPipe"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "BuggyCockpit"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Viewport1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Viewport2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension3x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension3x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension3x3mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension5x5mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSuspension1x1mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension3x3mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension5x5mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "OffroadSmallSuspension1x1mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel1x1mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheelmirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallRealWheel5x5mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel1x1mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheelmirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadRealWheel5x5mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadWheel1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallWheel1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadWheel3x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallWheel3x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadWheel5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "OffroadSmallWheel5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "OffsetLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ReflectorLight), "OffsetSpotlight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BarredWindow"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BarredWindowSlope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BarredWindowSide"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BarredWindowFace"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "StorageShelf1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "StorageShelf2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "StorageShelf3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanel5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanel5x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TextPanel), "LargeLCDPanel3x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockSciFiWall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraight1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraight2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesBendUp"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesBendDown"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraightEnd1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraightEnd2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesStraightDown"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_EmissiveBlock), "LargeNeonTubesU"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TerminalBlock), "LargeBlockSciFiTerminal"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "LargeSciFiButtonTerminal"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "SmallSideDoor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockBarCounter"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeBlockBarCounterCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ButtonPanel), "LargeSciFiButtonPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallThrustSciFi"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeThrustSciFi"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallThrustSciFi"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeThrustSciFi"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeAtmosphericThrustSciFi"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallAtmosphericThrustSciFi"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeAtmosphericThrustSciFi"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallAtmosphericThrustSciFi"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolA"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolB"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolC"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolD"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolE"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolF"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolG"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolH"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolI"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolJ"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolK"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolL"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolM"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolN"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolO"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolP"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolQ"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolR"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolS"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolT"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolU"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolV"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolW"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolX"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolY"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolZ"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolA"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolB"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolC"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolD"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolE"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolF"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolG"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolH"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolI"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolJ"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolK"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolL"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolM"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolN"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolO"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolP"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolQ"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolR"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolS"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolT"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolU"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolV"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolW"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolX"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolY"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolZ"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol0"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol4"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol6"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol7"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol8"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbol9"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol0"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol4"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol6"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol7"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol8"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbol9"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolHyphen"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolUnderscore"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolDot"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolApostrophe"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolAnd"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolColon"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolExclamationMark"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeSymbolQuestionMark"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolHyphen"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolUnderscore"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolDot"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolApostrophe"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolAnd"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolColon"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolExclamationMark"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallSymbolQuestionMark"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeHydrogenThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallHydrogenThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeHydrogenThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallHydrogenThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeAtmosphericThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallAtmosphericThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeAtmosphericThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallAtmosphericThrust"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Drill), "SmallBlockDrill"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Drill), "LargeBlockDrill"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipGrinder), "LargeShipGrinder"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipGrinder), "SmallShipGrinder"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipWelder), "LargeShipWelder"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_ShipWelder), "SmallShipWelder"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OreDetector), "LargeOreDetector"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_OreDetector), "SmallBlockOreDetector"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LandingGear), "LargeBlockLandingGear"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LandingGear), "SmallBlockLandingGear"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LandingGear), "LargeBlockSmallMagneticPlate"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LandingGear), "SmallBlockSmallMagneticPlate"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_JumpDrive), "LargeJumpDrive"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CameraBlock), "SmallCameraBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CameraBlock), "LargeCameraBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MergeBlock), "LargeShipMergeBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MergeBlock), "SmallShipMergeBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MergeBlock), "SmallShipSmallMergeBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Parachute), "LgParachute"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Parachute), "SmParachute"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "LargeBlockWeaponRack"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CargoContainer), "SmallBlockWeaponRack"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "FireCover"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "FireCoverCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HalfWindow"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HalfWindowInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HalfWindowCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "HalfWindowCornerInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Embrasure"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "PassageSciFi"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "PassageSciFiWall"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "PassageSciFiIntersection"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "PassageSciFiGate"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "PassageSciFiLight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "PassageScifiCorner"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "PassageSciFiTjunction"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "PassageSciFiWindow"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BridgeWindow1x1Slope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "BridgeWindow1x1Face"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "PassengerBench"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "LargeLightPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorLight), "SmallLightPanel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "LargeBlockSmallGeneratorWarfare2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "LargeBlockLargeGeneratorWarfare2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "SmallBlockSmallGeneratorWarfare2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Reactor), "SmallBlockLargeGeneratorWarfare2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirtightHangarDoor), "AirtightHangarDoorWarfare2A"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirtightHangarDoor), "AirtightHangarDoorWarfare2B"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_AirtightHangarDoor), "AirtightHangarDoorWarfare2C"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), "SmallMissileLauncherWarfare2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallGatlingGun), "SmallGatlingGunWarfare2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "LargeBlockBatteryBlockWarfare2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_BatteryBlock), "SmallBlockBatteryBlockWarfare2"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "SlidingHatchDoor"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Door), "SlidingHatchDoorHalf"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "SmallBlockStandingCockpit"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "LargeBlockStandingCockpit"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockSmallModularThruster"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "SmallBlockLargeModularThruster"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockSmallModularThruster"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Thrust), "LargeBlockLargeModularThruster"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Warhead), "LargeWarhead"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Warhead), "SmallWarhead"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Decoy), "LargeDecoy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Decoy), "SmallDecoy"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), "SmallGatlingTurret"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallMissileTurret"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_InteriorTurret), "LargeInteriorTurret"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), "LargeMissileLauncher"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncherReload), "SmallRocketLauncherReload"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallGatlingGun), ""));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallGatlingGun), "SmallBlockAutocannon"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncherReload), "SmallBlockMediumCalibreGun"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncher), "LargeBlockLargeCalibreGun"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncherReload), "LargeRailgun"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_SmallMissileLauncherReload), "SmallRailgun"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "LargeCalibreTurret"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "LargeBlockMediumCalibreTurret"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeMissileTurret), "SmallBlockMediumCalibreTurret"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_LargeGatlingTurret), "AutoCannonTurret"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension3x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension3x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension3x3mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension5x5mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "Suspension1x1mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension3x3mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension5x5mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_MotorSuspension), "SmallSuspension1x1mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel1x1mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheelmirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallRealWheel5x5mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel1x1mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheelmirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "RealWheel5x5mirrored"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "Wheel1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallWheel1x1"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "Wheel3x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallWheel3x3"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "Wheel5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Wheel), "SmallWheel5x5"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeWindowSquare"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "LargeWindowEdge"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2Slope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2Inv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2Face"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2SideLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2SideLeftInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2SideRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2SideRightInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Slope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Face"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Side"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1SideInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Inv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2Flat"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x2FlatInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1Flat"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window1x1FlatInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window3x3Flat"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window3x3FlatInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window2x3Flat"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "Window2x3FlatInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2Slope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2Inv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2Face"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2SideLeft"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2SideLeftInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2SideRight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2SideRightInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Slope"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Face"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Side"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1SideInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Inv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2Flat"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x2FlatInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1Flat"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow1x1FlatInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow3x3Flat"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow3x3FlatInv"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow2x3Flat"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), "SmallWindow2x3FlatInv"));

            // HACK: backwards compatible
#if !(VERSION_190 || VERSION_191 || VERSION_192 || VERSION_193 || VERSION_194 || VERSION_195 || VERSION_196 || VERSION_197 || VERSION_198 || VERSION_199)
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Searchlight), "SmallSearchlight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_Searchlight), "LargeSearchlight"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_HeatVentBlock), "LargeHeatVentBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_HeatVentBlock), "SmallHeatVentBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TurretControlBlock), "LargeTurretControlBlock"));
            Definitions.Add(new MyDefinitionId(typeof(MyObjectBuilder_TurretControlBlock), "SmallTurretControlBlock"));
#endif
        }
    }
}
