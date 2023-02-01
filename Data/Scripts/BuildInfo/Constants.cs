﻿using System;
using System.Collections.Generic;
using System.Linq;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace Digi.BuildInfo
{
    public class Constants : ModComponent
    {
        public const int ModVersion = 5; // notifies player of notable changes and links them to workshop's changelog.

        public const int TicksPerSecond = (int)(MyEngineConstants.UPDATE_STEPS_PER_MINUTE / 60);

        public const string WarnPlayerIsNull = "Local Player is null, silly bugs... try again in a few seconds.";

        public static bool ExportVanillaDefinitions = false; // used for exporting vanilla block IDs for AnalyseShip's hardcoded list.

        public readonly HashSet<MyObjectBuilderType> DefaultItemsForMass = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer) // used in inventory formatting to compute min/max mass
        {
            typeof(MyObjectBuilder_Ore),
            typeof(MyObjectBuilder_Ingot),
            typeof(MyObjectBuilder_Component)
        };

        public static readonly Dictionary<MyObjectBuilderType, string> TypeToFriendlyName = new Dictionary<MyObjectBuilderType, string>()
        {
            [typeof(MyObjectBuilder_GasProperties)] = "Gas",

            [typeof(MyObjectBuilder_BlueprintDefinition)] = "Blueprint",
            [typeof(MyObjectBuilder_CompositeBlueprintDefinition)] = "Blueprint",

            // items
            [typeof(MyObjectBuilder_PhysicalGunObject)] = "Hand Tool/Gun",
            [typeof(MyObjectBuilder_AmmoMagazine)] = "Magazine",
            [typeof(MyObjectBuilder_GasContainerObject)] = "Gas Bottle",
            [typeof(MyObjectBuilder_OxygenContainerObject)] = "Oxygen Bottle",

            // blocks
            [typeof(MyObjectBuilder_MotorSuspension)] = "Suspension",
            [typeof(MyObjectBuilder_MotorStator)] = "Rotor Base",
            [typeof(MyObjectBuilder_MotorAdvancedStator)] = "Adv. Rotor Base",
            [typeof(MyObjectBuilder_MotorRotor)] = "Rotor Top",
            [typeof(MyObjectBuilder_MotorAdvancedRotor)] = "Adv. Rotor Top",
            [typeof(MyObjectBuilder_ExtendedPistonBase)] = "Piston",
            [typeof(MyObjectBuilder_PistonBase)] = "Piston",
            [typeof(MyObjectBuilder_PistonTop)] = "Piston Top",

            [typeof(MyObjectBuilder_OxygenGenerator)] = "Gas Generator",
            [typeof(MyObjectBuilder_OxygenTank)] = "Gas Tank",
            [typeof(MyObjectBuilder_HydrogenEngine)] = "Hydrogen Engine",

            [typeof(MyObjectBuilder_LargeGatlingTurret)] = "Gatling Turret",
            [typeof(MyObjectBuilder_LargeMissileTurret)] = "Missile Turret",
            [typeof(MyObjectBuilder_InteriorTurret)] = "Interior Turret",
            [typeof(MyObjectBuilder_SmallGatlingGun)] = "Gatling Gun",
            [typeof(MyObjectBuilder_SmallMissileLauncher)] = "Missile Launcher",
            [typeof(MyObjectBuilder_SmallMissileLauncherReload)] = "Reloadable Missile Launcher",

            [typeof(MyObjectBuilder_ShipConnector)] = "Connector",
            [typeof(MyObjectBuilder_MergeBlock)] = "Merge",
            [typeof(MyObjectBuilder_ExhaustBlock)] = "Exhaust",
            [typeof(MyObjectBuilder_CameraBlock)] = "Camera",
            [typeof(MyObjectBuilder_BatteryBlock)] = "Battery",

            [typeof(MyObjectBuilder_SensorBlock)] = "Sensor",
            [typeof(MyObjectBuilder_ReflectorLight)] = "Spotlight",
            [typeof(MyObjectBuilder_InteriorLight)] = "Interior Light",

            [typeof(MyObjectBuilder_OreDetector)] = "Ore Detector",
            [typeof(MyObjectBuilder_RadioAntenna)] = "Radio Antenna",
            [typeof(MyObjectBuilder_LaserAntenna)] = "Laser Antenna",
            [typeof(MyObjectBuilder_LandingGear)] = "Landing Gear",
            [typeof(MyObjectBuilder_JumpDrive)] = "Jump Drive",
            [typeof(MyObjectBuilder_GravityGenerator)] = "Gravity Generator",
            [typeof(MyObjectBuilder_GravityGeneratorSphere)] = "Spherical Gravity Generator",
            [typeof(MyObjectBuilder_CryoChamber)] = "Cryo Chamber",
            [typeof(MyObjectBuilder_ConveyorSorter)] = "Conveyor Sorter",
            [typeof(MyObjectBuilder_ControlPanel)] = "Control Panel",
            [typeof(MyObjectBuilder_CargoContainer)] = "Cargo Container",
            [typeof(MyObjectBuilder_ButtonPanel)] = "Button Panel",
            [typeof(MyObjectBuilder_AirVent)] = "Air Vent",
            [typeof(MyObjectBuilder_AirtightSlideDoor)] = "Slide Door",
            [typeof(MyObjectBuilder_AirtightHangarDoor)] = "Hangar Door",
            [typeof(MyObjectBuilder_AdvancedDoor)] = "Advanced Door",

            [typeof(MyObjectBuilder_ShipGrinder)] = "Grinder",
            [typeof(MyObjectBuilder_ShipWelder)] = "Welder",

            [typeof(MyObjectBuilder_TextPanel)] = "LCD",
            [typeof(MyObjectBuilder_LCDPanelsBlock)] = "Decorative with LCD",
        };

        public readonly MyStringId[] ToolbarSlotControlIds = new MyStringId[]
        {
            MyControlsSpace.SLOT0, // do not edit order
            MyControlsSpace.SLOT1,
            MyControlsSpace.SLOT2,
            MyControlsSpace.SLOT3,
            MyControlsSpace.SLOT4,
            MyControlsSpace.SLOT5,
            MyControlsSpace.SLOT6,
            MyControlsSpace.SLOT7,
            MyControlsSpace.SLOT8,
            MyControlsSpace.SLOT9,
        };

        public readonly MyJoystickButtonsEnum[] DPadValues = new MyJoystickButtonsEnum[]
        {
            MyJoystickButtonsEnum.JDUp, // do not edit order
            MyJoystickButtonsEnum.JDLeft,
            MyJoystickButtonsEnum.JDRight,
            MyJoystickButtonsEnum.JDDown,
        };

        public readonly char[] DPadIcons = new char[]
        {
            '\ue011', // do not edit order
            '\ue010',
            '\ue012',
            '\ue013',
        };

        // need to be static for StatBase as it gets instanced before LoadData()...
        public static readonly MyTuple<float, string>[] UnitMulipliers = new MyTuple<float, string>[]
        {
            new MyTuple<float, string>(1000000000000, "T"),
            new MyTuple<float, string>(1000000000, "G"),
            new MyTuple<float, string>(1000000, "M"),
            new MyTuple<float, string>(1000, "k"),
            new MyTuple<float, string>(1, ""),
            new MyTuple<float, string>(1/1000f, "m"),
            new MyTuple<float, string>(1/1000000f, "µ"),
        };

        // need to be static for StatBase as it gets instanced before LoadData()...
        public static readonly string[] DigitFormats = new string[]
        {
            "0",
            "0.0",
            "0.00",
            "0.000",
            "0.0000",
            "0.00000",
            "0.000000",
        };

        public static string CurrencySuffix = "SC";

        public Constants(BuildInfoMod main) : base(main)
        {
            ComputeResourceGroups();
        }

        public override void RegisterComponent()
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void UnregisterComponent()
        {
        }

        public override void UpdateAfterSim(int tick)
        {
            // HACK: because it can be null in MP: https://support.keenswh.com/spaceengineers/pc/topic/01-190-101modapi-myapigateway-session-player-is-null-for-first-3-ticks-for-mp-clients
            IMyPlayer localPlayer = MyAPIGateway.Session?.Player;
            if(localPlayer != null)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

                // HACK: only way to get currency short name is by getting it from a player or faction's balance string...
                string balanceText = localPlayer.GetBalanceShortString();
                if(balanceText != null)
                {
                    string[] parts = balanceText.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    CurrencySuffix = parts[parts.Length - 1]; // last thing in the string must be the currency name
                }
            }
        }

        #region Resource group priorities
        public int ResourceSinkGroups = 0;
        public int ResourceSourceGroups = 0;
        public readonly Dictionary<MyStringHash, ResourceGroupData> ResourceGroupPriority = new Dictionary<MyStringHash, ResourceGroupData>(MyStringHash.Comparer);

        void ComputeResourceGroups()
        {
            ResourceGroupPriority.Clear();
            ResourceSourceGroups = 0;
            ResourceSinkGroups = 0;

            // from MyResourceDistributorComponent.InitializeMappings()
            ListReader<MyResourceDistributionGroupDefinition> groupDefs = MyDefinitionManager.Static.GetDefinitionsOfType<MyResourceDistributionGroupDefinition>();
            IOrderedEnumerable<MyResourceDistributionGroupDefinition> orderedGroupsEnumerable = groupDefs.OrderBy((def) => def.Priority);

            // compact priorities into an ordered number.
            foreach(MyResourceDistributionGroupDefinition group in orderedGroupsEnumerable)
            {
                int priority = 0;

                if(group.IsSource)
                {
                    ResourceSourceGroups++;
                    priority = ResourceSourceGroups;
                }
                else
                {
                    ResourceSinkGroups++;
                    priority = ResourceSinkGroups;
                }

                ResourceGroupPriority.Add(group.Id.SubtypeId, new ResourceGroupData(group, priority));
            }
        }

        public struct ResourceGroupData
        {
            public readonly MyResourceDistributionGroupDefinition Def;
            public readonly int Priority;

            public ResourceGroupData(MyResourceDistributionGroupDefinition def, int priority)
            {
                Def = def;
                Priority = priority;
            }
        }
        #endregion Resource group priorities
    }
}
