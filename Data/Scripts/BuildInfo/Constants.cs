using System.Collections.Generic;
using System.Linq;
using Digi.BuildInfo.VanillaData;
using Digi.Input;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Definitions.SessionComponents;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo
{
    public class Constants : ModComponent
    {
        public const int ModVersion = 8; // notifies player of notable changes and links them to workshop's changelog.

        public const int TicksPerSecond = (int)MyEngineConstants.UPDATE_STEPS_PER_SECOND;

        public static readonly MyStringId Mat_Square = MyStringId.GetOrCompute("BuildInfo_Square");

        public static readonly MyStringId Mat_LineShadow = MyStringId.GetOrCompute("BuildInfo_ShadowedLine");

        public static readonly MyStringId Mat_Dot = MyStringId.GetOrCompute("WhiteDot"); // TODO: what's the difference between vanilla and mine?
        public static readonly MyStringId Mat_Dot2 = MyStringId.GetOrCompute("BuildInfo_Dot");
        public static readonly MyStringId Mat_DotShadow = MyStringId.GetOrCompute("BuildInfo_ShadowedDot");

        public static readonly MyStringId Mat_Laser = MyStringId.GetOrCompute("BuildInfo_Laser");
        public static readonly MyStringId Mat_LaserGradient = MyStringId.GetOrCompute("BuildInfo_LaserGradientSRGB");

        public static readonly MyStringId Mat_LaserDot = MyStringId.GetOrCompute("BuildInfo_LaserDot");

        public static readonly MyStringId Mat_Arrow = MyStringId.GetOrCompute("BuildInfo_Arrow");
        public static readonly MyStringId Mat_ArrowShadow = MyStringId.GetOrCompute("BuildInfo_ShadowedArrow");

        public static readonly MyStringId Mat_Gradient = MyStringId.GetOrCompute("BuildInfo_TransparentGradient");
        public static readonly MyStringId Mat_GradientSRGB = MyStringId.GetOrCompute("BuildInfo_TransparentGradientSRGB");

        public static readonly MyStringId MatUI_Square = MyStringId.GetOrCompute("BuildInfo_UI_Square");

        public static readonly MyStringId MatUI_SquareHollow = MyStringId.GetOrCompute("BuildInfo_UI_SquareHollow");

        public static readonly MyStringId MatUI_Corner = MyStringId.GetOrCompute("BuildInfo_UI_Corner");

        public static readonly MyStringId MatUI_ButtonBg = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBg");
        public static readonly MyStringId MatUI_ButtonBgHover = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgHover");
        public static readonly MyStringId MatUI_ButtonBgActivate = MyStringId.GetOrCompute("BuildInfo_UI_ButtonBgActivate");

        public static readonly MyStringId MatUI_TextAPICursor = MyStringId.GetOrCompute("MouseCursor"); // HACK: MouseCursor material is from textAPI

        public static readonly MyStringId MatUI_Icon_Measure = MyStringId.GetOrCompute("BuildInfo_Icon_Measure");
        public static readonly MyStringId MatUI_Icon_ConveyorVis = MyStringId.GetOrCompute("BuildInfo_Icon_ConveyorVis");
        public static readonly MyStringId MatUI_Icon_PhysicsSnapshot = MyStringId.GetOrCompute("BuildInfo_Icon_PhysicsSnapshot");

        public static readonly MyStringId MatUI_HotkeyBackground = MyStringId.GetOrCompute("BuildInfo_UI_HudHotkeyBackground");
        public static readonly MyStringId MatUI_IconWeaponModeAll = MyStringId.GetOrCompute("BuildInfo_UI_HudWeaponModeAll");
        public static readonly MyStringId MatUI_IconWeaponModeSingle = MyStringId.GetOrCompute("BuildInfo_UI_HudWeaponModeSingle");

        public static readonly MyStringId MatUI_InventoryBar = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryBar");
        public static readonly MyStringId MatUI_InventoryBarDrill = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryDrillIcon");
        public static readonly MyStringId MatUI_InventoryBarGrinder = MyStringId.GetOrCompute("BuildInfo_UI_ToolInventoryGrinderIcon");

        public static readonly Color Color_UIBackground = new Color(41, 54, 62);

        public const string WarnPlayerIsNull = "Local Player is null, silly bugs... try again in a few seconds.";

        public static bool ForceExportVanillaDefinitions = false; // used for exporting vanilla block IDs for AnalyseShip's hardcoded list.

        public readonly HashSet<MyObjectBuilderType> DefaultItemsForMass = new HashSet<MyObjectBuilderType>(MyObjectBuilderType.Comparer) // used in inventory formatting to compute min/max mass
        {
            typeof(MyObjectBuilder_Ore),
            typeof(MyObjectBuilder_Ingot),
            typeof(MyObjectBuilder_Component)
        };

        public static Dictionary<MyObjectBuilderType, string> TypeToFriendlyName = new Dictionary<MyObjectBuilderType, string>()
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
            ControlIds.SLOT1, // do not edit order
            ControlIds.SLOT2,
            ControlIds.SLOT3,
            ControlIds.SLOT4,
            ControlIds.SLOT5,
            ControlIds.SLOT6,
            ControlIds.SLOT7,
            ControlIds.SLOT8,
            ControlIds.SLOT9,
            ControlIds.SLOT0,
        };

        public readonly MyStringId[] ToolbarPageControlIds = new MyStringId[]
        {
            ControlIds.PAGE1, // do not edit order
            ControlIds.PAGE2,
            ControlIds.PAGE3,
            ControlIds.PAGE4,
            ControlIds.PAGE5,
            ControlIds.PAGE6,
            ControlIds.PAGE7,
            ControlIds.PAGE8,
            ControlIds.PAGE9,
            ControlIds.PAGE0,
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

        public struct UnitInfo
        {
            public readonly float Multiplier;
            public readonly string Suffix;

            public UnitInfo(float multiplier, string suffix)
            {
                Multiplier = multiplier;
                Suffix = suffix;
            }
        }

        // need to be static for StatBase as it gets instanced before LoadData()...
        public static readonly UnitInfo[] UnitMulipliers = new UnitInfo[] // NOTE: must be from largest to smallest
        {
            new UnitInfo(1000000000000, "T"),
            new UnitInfo(1000000000, "G"),
            new UnitInfo(1000000, "M"),
            new UnitInfo(1000, "k"),
            new UnitInfo(1, ""),
            new UnitInfo(1/1000f, "m"),
            new UnitInfo(1/1000000f, "µ"),
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

        public static string CurrencyShortName { get; private set; } = "SC";

        public Constants(BuildInfoMod main) : base(main)
        {
            ComputeResourceGroups();
            Hardcoded.LoadData();
        }

        public override void RegisterComponent()
        {
            GetBankingInfo();
        }

        public override void UnregisterComponent()
        {
            TypeToFriendlyName = null;
            Hardcoded.Unload();
        }

        void GetBankingInfo()
        {
            var bankingId = MyDefinitionId.Parse("MyObjectBuilder_BankingSystemDefinition/BankingSystem");
            var bankingDef = MyDefinitionManager.Static.GetDefinition(bankingId) as MyBankingSystemDefinition;
            if(bankingDef != null)
            {
                CurrencyShortName = bankingDef.CurrencyShortName.String;
                Log.Info($"Found banking system definition, currency name: '{bankingDef.CurrencyFullName}', short name: '{bankingDef.CurrencyShortName}'");
            }
            else
            {
                Log.Error($"Can't find banking definition from id: {bankingId}");
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
            public bool IsSource => Def.IsSource;

            public ResourceGroupData(MyResourceDistributionGroupDefinition def, int priority)
            {
                Def = def;
                Priority = priority;
            }
        }
        #endregion Resource group priorities
    }
}
