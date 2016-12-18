using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using Digi.Utils;

namespace Digi.BuildInfo
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class BuildInfo : MySessionComponentBase
    {
        public override void LoadData()
        {
            Log.SetUp("Build Info", 514062285, "BuildInfo");
        }

        private bool init = false;
        private bool isThisDS = false;

        private bool showBuildInfo = true;
        private bool showMountPoints = false;

        private IMyHudNotification buildInfoNotification = null;
        private IMyHudNotification mountPointsNotification = null;

        private short skip = 0;
        private int maxLineWidthPx = 0;
        private long lastScroll = 0;
        private const int SCROLL_FROM_LINE = 2;
        private int atLine = SCROLL_FROM_LINE;
        private const int MAX_LINES = 8;

        private List<IMyHudNotification> hudLines;
        private HashSet<Vector3I> cubes = new HashSet<Vector3I>();
        private StringBuilder str = new StringBuilder();
        private MyObjectBuilderType lastTypeId;
        private MyStringHash lastSubTypeId;

        private const string INT_FORMAT = "0";
        private const string FLOAT_FORMAT = "0.00";
        private const string FLOAT3_FORMAT = "0.000";
        private const string NUMBER_FORMAT = "N";

        private HashSet<string> blendingSubTypeIds = new HashSet<string>()
        {
            "LargeBlockArmorRoundedSlope",
            "LargeBlockArmorRoundedCorner",
            "LargeHeavyBlockArmorRoundedSlope",
            "LargeHeavyBlockArmorRoundedCorner",
            "LargeBlockArmorAngledSlope",
            "LargeBlockArmorAngledCorner",
            "LargeHeavyBlockArmorAngledSlope",
            "LargeHeavyBlockArmorAngledCorner",
            "LargeBlockArmorSlope2BaseSmooth",
            "LargeBlockArmorSlope2TipSmooth",
            "LargeBlockArmorCorner2BaseSmooth",
            "LargeBlockArmorCorner2TipSmooth",
            "LargeBlockArmorInvCorner2BaseSmooth",
            "LargeBlockArmorInvCorner2TipSmooth",
            "LargeHeavyBlockArmorSlope2BaseSmooth",
            "LargeHeavyBlockArmorSlope2TipSmooth",
            "LargeHeavyBlockArmorCorner2BaseSmooth",
            "LargeHeavyBlockArmorCorner2TipSmooth",
            "LargeHeavyBlockArmorInvCorner2BaseSmooth",
            "LargeHeavyBlockArmorInvCorner2TipSmooth",
            "SmallBlockArmorRoundedSlope",
            "SmallBlockArmorRoundedCorner",
            "SmallHeavyBlockArmorRoundedSlope",
            "SmallHeavyBlockArmorRoundedCorner",
            "SmallBlockArmorAngledSlope",
            "SmallBlockArmorAngledCorner",
            "SmallHeavyBlockArmorAngledSlope",
            "SmallHeavyBlockArmorAngledCorner",
            "SmallBlockArmorSlope2BaseSmooth",
            "SmallBlockArmorSlope2TipSmooth",
            "SmallBlockArmorCorner2BaseSmooth",
            "SmallBlockArmorCorner2TipSmooth",
            "SmallBlockArmorInvCorner2BaseSmooth",
            "SmallBlockArmorInvCorner2TipSmooth",
            "SmallHeavyBlockArmorSlope2BaseSmooth",
            "SmallHeavyBlockArmorSlope2TipSmooth",
            "SmallHeavyBlockArmorCorner2BaseSmooth",
            "SmallHeavyBlockArmorCorner2TipSmooth",
            "SmallHeavyBlockArmorInvCorner2BaseSmooth",
            "SmallHeavyBlockArmorInvCorner2TipSmooth",
        };

        private static Dictionary<char, int> charSize = new Dictionary<char, int>();
        private const int SPACE_SIZE = 8;

        static BuildInfo()
        {
            charSize.Clear();

            // generated from fonts/white_shadow/FontData.xml
            AddCharsSize(" !I`ijl ¡¨¯´¸ÌÍÎÏìíîïĨĩĪīĮįİıĵĺļľłˆˇ˘˙˚˛˜˝ІЇії‹›∙", 8);
            AddCharsSize("\"-rª­ºŀŕŗř", 10);
            AddCharsSize("#0245689CXZ¤¥ÇßĆĈĊČŹŻŽƒЁЌАБВДИЙПРСТУХЬ€", 19);
            AddCharsSize("$&GHPUVY§ÙÚÛÜÞĀĜĞĠĢĤĦŨŪŬŮŰŲОФЦЪЯжы†‡", 20);
            AddCharsSize("%ĲЫ", 24);
            AddCharsSize("'|¦ˉ‘’‚", 6);
            AddCharsSize("(),.1:;[]ft{}·ţťŧț", 9);
            AddCharsSize("*²³¹", 11);
            AddCharsSize("+<=>E^~¬±¶ÈÉÊË×÷ĒĔĖĘĚЄЏЕНЭ−", 18);
            AddCharsSize("/ĳтэє", 14);
            AddCharsSize("3FKTabdeghknopqsuy£µÝàáâãäåèéêëðñòóôõöøùúûüýþÿāăąďđēĕėęěĝğġģĥħĶķńņňŉōŏőśŝşšŢŤŦũūŭůűųŶŷŸșȚЎЗКЛбдекруцяёђћўџ", 17);
            AddCharsSize("7?Jcz¢¿çćĉċčĴźżžЃЈЧавийнопсъьѓѕќ", 16);
            AddCharsSize("@©®мшњ", 25);
            AddCharsSize("ABDNOQRSÀÁÂÃÄÅÐÑÒÓÔÕÖØĂĄĎĐŃŅŇŌŎŐŔŖŘŚŜŞŠȘЅЊЖф□", 21);
            AddCharsSize("L_vx«»ĹĻĽĿŁГгзлхчҐ–•", 15);
            AddCharsSize("MМШ", 26);
            AddCharsSize("WÆŒŴ—…‰", 31);
            AddCharsSize("\\°“”„", 12);
            AddCharsSize("mw¼ŵЮщ", 27);
            AddCharsSize("½Щ", 29);
            AddCharsSize("¾æœЉ", 28);
            AddCharsSize("ю", 23);
            AddCharsSize("ј", 7);
            AddCharsSize("љ", 22);
            AddCharsSize("ґ", 13);
            AddCharsSize("™", 30);
            AddCharsSize("", 40);
            AddCharsSize("", 41);
            AddCharsSize("", 32);
            AddCharsSize("", 34);
        }

        private static void AddCharsSize(string chars, int size)
        {
            for(int i = 0; i < chars.Length; i++)
            {
                charSize.Add(chars[i], size);
            }
        }

        private static int GetStringSize(string text)
        {
            int size = 0;
            int len;
            for(int i = 0; i < text.Length; i++)
            {
                if(charSize.TryGetValue(text[i], out len))
                {
                    size += len;
                }
                //else
                //{
                //    Log.Error("No character size for "+text[i]);
                //}
            }

            return size;
        }

        public void Init()
        {
            Log.Init();
            init = true;
            isThisDS = (MyAPIGateway.Multiplayer.IsServer && MyAPIGateway.Utilities.IsDedicated);

            MyAPIGateway.Utilities.MessageEntered += MessageEntered;
        }

        protected override void UnloadData()
        {
            try
            {
                if(init)
                {
                    init = false;
                    cubes = null;
                    hudLines = null;

                    MyAPIGateway.Utilities.MessageEntered -= MessageEntered;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            Log.Close();
        }

        public override void HandleInput()
        {
            try
            {
                if(!init)
                    return;

                if(MyCubeBuilder.Static != null && MyCubeBuilder.Static.IsActivated)
                {
                    var GUI = MyAPIGateway.Gui;

                    if(!GUI.ChatEntryVisible && GUI.GetCurrentScreen == MyTerminalPageEnum.None)
                    {
                        if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.VOXEL_HAND_SETTINGS) && !GUI.InGameplayMenu())
                        {
                            showBuildInfo = !showBuildInfo;

                            if(buildInfoNotification == null)
                                buildInfoNotification = MyAPIGateway.Utilities.CreateNotification("");

                            buildInfoNotification.Text = showBuildInfo ? "Build info ON" : "Build info OFF";
                            buildInfoNotification.Show();
                        }

                        if(showBuildInfo && MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.CubeBuilderState != null && MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition != null)
                        {
                            if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.CUBE_DEFAULT_MOUNTPOINT) && !GUI.InGameplayMenu())
                            {
                                showMountPoints = !showMountPoints;

                                if(mountPointsNotification == null)
                                    mountPointsNotification = MyAPIGateway.Utilities.CreateNotification("");

                                mountPointsNotification.Text = showMountPoints ? "Mount points mode ON" : "Mount points mode OFF";
                                mountPointsNotification.Show();
                            }
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Draw()
        {
            try
            {
                if(!init)
                    return;

                if(showBuildInfo && showMountPoints && MyCubeBuilder.Static != null && MyCubeBuilder.Static.IsActivated && MyCubeBuilder.Static.DynamicMode && MyCubeBuilder.Static.CubeBuilderState != null && MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition != null)
                {
                    var def = MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition;
                    var box = MyCubeBuilder.Static.GetBuildBoundingBox();
                    var m = MatrixD.CreateFromQuaternion(box.Orientation);
                    m.Translation = box.Center;
                    MyCubeBuilder.DrawMountPoints(MyDefinitionManager.Static.GetCubeSize(def.CubeSize), def, ref m);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!init)
                {
                    if(MyAPIGateway.Session == null)
                        return;

                    Init();
                }

                if(isThisDS)
                    return;

                if(showBuildInfo && MyCubeBuilder.Static != null && MyCubeBuilder.Static.IsActivated && MyCubeBuilder.Static.CubeBuilderState != null && MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition != null)
                {
                    if(++skip < 6)
                        return;

                    skip = 0;
                    var def = MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition;
                    long now = DateTime.UtcNow.Ticks;

                    if(def.Id.SubtypeId != lastSubTypeId || def.Id.TypeId != lastTypeId)
                    {
                        lastTypeId = def.Id.TypeId;
                        lastSubTypeId = def.Id.SubtypeId;

                        var defType = def.Id.TypeId.ToString();
                        bool isDoor = false;

                        switch(defType)
                        {
                            case "MyObjectBuilder_AdvancedDoor":
                            case "MyObjectBuilder_AirtightDoorGeneric":
                            case "MyObjectBuilder_AirtightHangarDoor":
                            case "MyObjectBuilder_Door":
                                isDoor = true;
                                break;
                        }

                        int airTightFaces = 0;
                        int totalFaces = 0;
                        bool airTight = (isDoor || IsAirTight(def, ref airTightFaces, ref totalFaces));
                        bool deformable = def.BlockTopology == MyBlockTopology.Cube;
                        int assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
                        bool buildModels = def.BuildProgressModels != null && def.BuildProgressModels.Length > 0;
                        bool blending = blendingSubTypeIds.Contains(def.Id.SubtypeName);

                        maxLineWidthPx = 0;
                        int line = 0;

                        var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
                        var weldTime = (assembleTime / weldMul);
                        var grindRatio = def.DisassembleRatio;

                        if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition) // HACK hardcoded; from MyDoor & MyAdvancedDoor
                            grindRatio *= 3.3f;

                        SetText(line++, MassFormat(def.Mass) + ", " + VectorFormat(def.Size) + ", " + TimeFormat(weldTime) + MultiplierFormat(weldMul) + (grindRatio > 1 ? ", Deconstruct speed: " + PercentFormat(1f / grindRatio) : "") + (blending ? ", Blends with armor!" : "") + (buildModels ? "" : " (No construction models)"), (blending ? MyFontEnum.DarkBlue : MyFontEnum.White));
                        SetText(line++, "Integrity: " + def.MaxIntegrity.ToString("#,###,###,###,###") + ", Deformable: " + (deformable ? "Yes (" + def.DeformationRatio.ToString(FLOAT_FORMAT) + ")" : "No"), (deformable ? MyFontEnum.Blue : MyFontEnum.White));
                        SetText(line++, "Air-tight faces: " + (airTight ? "All" : (airTightFaces == 0 ? "None" : airTightFaces + " of " + totalFaces)), (isDoor || airTight ? MyFontEnum.Green : (airTightFaces == 0 ? MyFontEnum.Red : MyFontEnum.DarkBlue)));

                        // TODO when VoxelPlacementSettings is whitelisted:
                        //if(def.VoxelPlacement.HasValue)
                        //{
                        //    var vp = def.VoxelPlacement.Value;
                        //    SetText(line++, "Voxel rules - Dynamic: " + vp.DynamicMode.PlacementMode + ", Static: " + vp.StaticMode.PlacementMode);
                        //}

                        if(defType != "MyObjectBuilder_CubeBlock")
                            GetExtraInfo(ref line, def, defType);

                        if(!def.Context.IsBaseGame)
                            SetText(line++, "Mod: " + def.Context.ModName, MyFontEnum.Blue);

                        lastScroll = now + TimeSpan.TicksPerSecond;
                        atLine = SCROLL_FROM_LINE;

                        for(int l = 0; l < hudLines.Count; l++)
                        {
                            var hud = hudLines[l];

                            if(l >= line)
                            {
                                hud.Text = "";
                                continue;
                            }

                            int textWidthPx = GetStringSize(hud.Text);

                            textWidthPx = maxLineWidthPx - textWidthPx;

                            int fillchars = (int)Math.Floor((float)textWidthPx / (float)SPACE_SIZE);

                            if(fillchars < 1)
                                continue;

                            string fill = new String(' ', fillchars);

                            hud.Text += fill;
                        }
                    }

                    int lines = 0;

                    foreach(var hud in hudLines)
                    {
                        if(hud.Text.Length > 0)
                            lines++;

                        hud.Hide();
                    }

                    if(lines > MAX_LINES)
                    {
                        int l;

                        for(l = 0; l < lines; l++)
                        {
                            var hud = hudLines[l];

                            if(l < SCROLL_FROM_LINE)
                            {
                                hud.ResetAliveTime();
                                hud.Show();
                            }
                        }

                        int d = SCROLL_FROM_LINE;
                        l = atLine;

                        while(d < MAX_LINES)
                        {
                            var hud = hudLines[l];

                            if(hud.Text.Length == 0)
                                break;

                            hud.ResetAliveTime();
                            hud.Show();

                            if(++l >= lines)
                                l = SCROLL_FROM_LINE;

                            d++;
                        }

                        if(lastScroll < now)
                        {
                            if(++atLine >= lines)
                                atLine = SCROLL_FROM_LINE;

                            lastScroll = now + TimeSpan.TicksPerSecond;
                        }
                    }
                    else
                    {
                        for(int l = 0; l < lines; l++)
                        {
                            var hud = hudLines[l];
                            hud.ResetAliveTime();
                            hud.Show();
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private bool IsAirTight(MyCubeBlockDefinition def, ref int airTightFaces, ref int totalFaces)
        {
            if(def.IsAirTight)
                return true;

            airTightFaces = 0;
            totalFaces = 0;

            if(!def.IsAirTight)
            {
                cubes.Clear();

                foreach(var kv in def.IsCubePressurized)
                {
                    cubes.Add(kv.Key);
                }

                foreach(var kv in def.IsCubePressurized)
                {
                    foreach(var kv2 in kv.Value)
                    {
                        if(cubes.Contains(kv.Key + kv2.Key))
                            continue;

                        if(kv2.Value)
                            airTightFaces++;

                        totalFaces++;
                    }
                }
            }

            return (def.IsAirTight || airTightFaces == totalFaces);
        }

        private void GetExtraInfo(ref int line, MyCubeBlockDefinition def, string defType)
        {
            switch(defType) // TODO convert these to object 'as' checking when their interfaces are not internal anymore
            {
                case "MyObjectBuilder_TerminalBlock": // control panel block
                    {
                        SetText(line++, "Requires power*: No", MyFontEnum.Green);
                        return;
                    }
                case "MyObjectBuilder_Conveyor": // conveyor blocks
                case "MyObjectBuilder_ConveyorConnector": // conveyor tubes
                    {
                        // HACK hardcoded; from MyGridConveyorSystem
                        float requiredPower = 2E-05f;
                        string powerGroup = "Conveyors";

                        SetText(line++, "Requires power*: " + PowerFormat(requiredPower) + ", Sink group*: " + ResourceGroup(powerGroup), MyFontEnum.Green);
                        return;
                    }
                case "MyObjectBuilder_ShipWelder":
                case "MyObjectBuilder_ShipGrinder":
                    {
                        // HACK hardcoded; from MyShipToolBase
                        float requiredPower = 0.002f;
                        string powerGroup = "Defense";
                        var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                        var volume = (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize * 0.5f;

                        SetText(line++, "Requires power*: " + PowerFormat(requiredPower) + ", Sink group*: " + ResourceGroup(powerGroup));
                        SetText(line++, "Inventory*: " + InventoryFormat(volume * 1000));

                        if(defType == "MyObjectBuilder_ShipWelder")
                        {
                            float weld = 2; // HACK hardcoded; from MyShipWelder
                            var mul = MyAPIGateway.Session.WelderSpeedMultiplier;
                            SetText(line++, "Weld speed*: " + PercentFormat(weld * mul) + " split accross targets" + MultiplierFormat(mul));
                        }
                        else
                        {
                            float grind = 2; // HACK hardcoded; from MyShipGrinder
                            var mul = MyAPIGateway.Session.GrinderSpeedMultiplier;
                            SetText(line++, "Grind speed*: " + PercentFormat(grind * mul) + " split accross targets" + MultiplierFormat(mul));
                        }
                        return;
                    }
                case "MyObjectBuilder_Drill":
                    {
                        // HACK hardcoded; from MyShipDrill
                        float requiredPower = 0.002f;
                        var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                        float volume = (float)(def.Size.X * def.Size.Y * def.Size.Z) * gridSize * gridSize * gridSize * 0.5f;

                        SetText(line++, "Requires power*: " + PowerFormat(requiredPower));
                        SetText(line++, "Inventory*: " + InventoryFormat(volume * 1000));
                        return;
                    }
                case "MyObjectBuilder_ShipConnector":
                    {
                        // HACK hardcoded; from MyShipConnector
                        var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                        var inventorySize = def.Size * gridSize * 0.8f;

                        SetText(line++, "Inventory*: " + InventoryFormat(inventorySize.Volume * 1000));
                        return;
                    }
                    // TODO when MyObjectBuilder_UpgradeModuleDefinition is whitelisted or it has a non-objectbuilder definition
                    /*
                case "MyObjectBuilder_UpgradeModule":
                    {
                        var obj = def.GetObjectBuilder() as MyObjectBuilder_UpgradeModuleDefinition; // prohibited
                        
                        SetText(line++, "upgrades: " + obj.Upgrades[0].ModifierType);
                        return;
                    }
                    */
            }

            var piston = def as MyPistonBaseDefinition;
            var motor = def as MyMotorStatorDefinition;

            if(piston != null || motor != null)
            {
                if(piston != null)
                {
                    SetText(line++, "Requires power: " + PowerFormat(piston.RequiredPowerInput) + ", Sink group: " + ResourceGroup(piston.ResourceSinkGroup));
                    SetText(line++, "Extended length: " + DistanceFormat(piston.Maximum));
                    SetText(line++, "Max speed: " + SpeedFormat(piston.MaxVelocity));
                }

                if(motor != null)
                {
                    SetText(line++, "Requires power: " + PowerFormat(motor.RequiredPowerInput) + ", Sink group: " + ResourceGroup(motor.ResourceSinkGroup));

                    if(!(def is MyMotorSuspensionDefinition))
                    {
                        SetText(line++, "Max force: " + ForceFormat(motor.MaxForceMagnitude));

                        if(motor.RotorDisplacementMin < motor.RotorDisplacementMax)
                            SetText(line++, "Displacement: " + DistanceFormat(motor.RotorDisplacementMin) + " to " + DistanceFormat(motor.RotorDisplacementMax));
                    }

                    var suspension = def as MyMotorSuspensionDefinition;
                    if(suspension != null)
                    {
                        SetText(line++, "Force: " + ForceFormat(suspension.PropulsionForce) + ", Steer speed: " + TorqueFormat(suspension.SteeringSpeed * 100) + ", Steer angle: " + RadAngleFormat(suspension.MaxSteer));
                        SetText(line++, "Height: " + DistanceFormat(suspension.MinHeight) + " to " + DistanceFormat(suspension.MaxHeight));
                    }
                }

                var topPart = (motor != null ? motor.TopPart : piston.TopPart);
                var group = MyDefinitionManager.Static.TryGetDefinitionGroup(topPart);

                if(group == null)
                    return;

                var partDef = (def.CubeSize == MyCubeSize.Large ? group.Large : group.Small);
                var airTightFaces = 0;
                var totalFaces = 0;
                var airTight = IsAirTight(partDef, ref airTightFaces, ref totalFaces);
                var deformable = def.BlockTopology == MyBlockTopology.Cube;
                var buildModels = def.BuildProgressModels != null && def.BuildProgressModels.Length > 0;
                var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
                var weldTime = ((def.MaxIntegrity / def.IntegrityPointsPerSec) / weldMul);
                var grindRatio = def.DisassembleRatio;

                SetText(line++, "Part: " + MassFormat(partDef.Mass) + ", " + VectorFormat(partDef.Size) + ", " + TimeFormat(weldTime) + MultiplierFormat(weldMul) + (grindRatio > 1 ? ", Deconstruct speed: " + PercentFormat(1f / grindRatio) : "") + (buildModels ? "" : " (No construction models)"));
                SetText(line++, "      - Integrity: " + partDef.MaxIntegrity.ToString("#,###,###,###,###") + (deformable ? ", Deformable (" + partDef.DeformationRatio.ToString(FLOAT_FORMAT) + ")" : "") + ", Air-tight faces: " + (airTight ? "All" : (airTightFaces == 0 ? "None" : airTightFaces + " of " + totalFaces)));
                return;
            }

            var shipController = def as MyShipControllerDefinition;
            if(shipController != null)
            {
                SetText(line++, "Ship controls: " + (shipController.EnableShipControl ? "Yes" : "No"), (shipController.EnableShipControl ? MyFontEnum.Green : MyFontEnum.Red));

                var cockpit = def as MyCockpitDefinition;
                if(cockpit != null)
                {
                    float volume = Vector3.One.Volume; // HACK hardcoded; from MyCockpit

                    SetText(line++, "Inventory*: " + InventoryFormat(volume * 1000));
                    SetText(line++, (cockpit.IsPressurized ? "Pressurized: Yes, Oxygen capacity: " + cockpit.OxygenCapacity.ToString(NUMBER_FORMAT) + " O2" : "Pressurized: No"), (cockpit.IsPressurized ? MyFontEnum.Green : MyFontEnum.Red));
                }

                var rc = def as MyRemoteControlDefinition;
                if(rc != null)
                {
                    SetText(line++, "Requires power: " + PowerFormat(rc.RequiredPowerInput) + ", Sink group: " + ResourceGroup(rc.ResourceSinkGroup));
                }

                return;
            }

            var thrust = def as MyThrustDefinition;
            if(thrust != null)
            {
                SetText(line++, "Usage - Idle: " + PowerFormat(thrust.MinPowerConsumption) + ", Max: " + PowerFormat(thrust.MaxPowerConsumption) + ", Sink group: " + ResourceGroup(thrust.ResourceSinkGroup));
                SetText(line++, "Force: " + ForceFormat(thrust.ForceMagnitude) + ", Dampener factor: " + thrust.SlowdownFactor.ToString(FLOAT_FORMAT));

                if(!thrust.FuelConverter.FuelId.IsNull())
                {
                    SetText(line++, "Requires fuel: " + thrust.FuelConverter.FuelId.SubtypeId + ", Efficiency: " + Math.Round(thrust.FuelConverter.Efficiency * 100, 2) + "%");
                }

                if(thrust.EffectivenessAtMinInfluence < 1.0f || thrust.EffectivenessAtMaxInfluence < 1.0f)
                {
                    if(thrust.NeedsAtmosphereForInfluence)
                    {
                        SetText(line++, PercentFormat(thrust.EffectivenessAtMaxInfluence) + " max thrust " + (thrust.MaxPlanetaryInfluence < 1f ? "in " + PercentFormat(thrust.MaxPlanetaryInfluence) + " atmosphere" : "in atmosphere"), thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                        SetText(line++, PercentFormat(thrust.EffectivenessAtMinInfluence) + " max thrust " + (thrust.MinPlanetaryInfluence > 0f ? "below " + PercentFormat(thrust.MinPlanetaryInfluence) + " atmosphere" : "in space"), thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                    }
                    else
                    {
                        SetText(line++, PercentFormat(thrust.EffectivenessAtMaxInfluence) + " max thrust " + (thrust.MaxPlanetaryInfluence < 1f ? "in " + PercentFormat(thrust.MaxPlanetaryInfluence) + " planet influence" : "on planets"), thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                        SetText(line++, PercentFormat(thrust.EffectivenessAtMinInfluence) + " max thrust " + (thrust.MinPlanetaryInfluence > 0f ? "below " + PercentFormat(thrust.MinPlanetaryInfluence) + " planet influence" : "in space"), thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                    }
                }
                else
                {
                    SetText(line++, "No thrust limits in space or planets", MyFontEnum.Green);
                }

                if(thrust.ConsumptionFactorPerG > 0)
                    SetText(line++, "Extra gravity consumption: +" + PercentFormat(thrust.ConsumptionFactorPerG) + " per g");

                SetText(line++, "Thrust damage scale: " + thrust.FlameDamage.ToString(FLOAT_FORMAT) + ", Distance scale: " + thrust.FlameDamageLengthScale.ToString(NUMBER_FORMAT));
                return;
            }

            var lg = def as MyLandingGearDefinition;
            if(lg != null)
            {
                SetText(line++, "Requires power*: No", MyFontEnum.Green);
                return;
            }

            var vent = def as MyAirVentDefinition;
            if(vent != null)
            {
                SetText(line++, "Usage - Idle: " + PowerFormat(vent.StandbyPowerConsumption) + ", Operational: " + PowerFormat(vent.OperationalPowerConsumption) + ", Sink group: " + ResourceGroup(vent.ResourceSinkGroup));
                SetText(line++, "Output - Rate: " + vent.VentilationCapacityPerSecond.ToString(NUMBER_FORMAT) + " O2/s, Source group: " + ResourceGroup(vent.ResourceSourceGroup));
                return;
            }

            var light = def as MyLightingBlockDefinition;
            if(light != null)
            {
                var radius = light.LightRadius;
                var spotlight = def as MyReflectorBlockDefinition;
                if(spotlight != null)
                    radius = light.LightReflectorRadius;

                SetText(line++, "Requires power: " + PowerFormat(light.RequiredPowerInput) + ", Sink group: " + ResourceGroup(light.ResourceSinkGroup));
                SetText(line++, "Radius: " + DistanceFormat(radius.Min) + " to " + DistanceFormat(radius.Max) + ", Default: " + DistanceFormat(radius.Default));
                SetText(line++, "Intensity: " + light.LightIntensity.Min.ToString(FLOAT_FORMAT) + " to " + light.LightIntensity.Max.ToString(FLOAT_FORMAT) + ", Default: " + light.LightIntensity.Default.ToString(FLOAT_FORMAT));
                SetText(line++, "Falloff: " + light.LightFalloff.Min.ToString(FLOAT_FORMAT) + " to " + light.LightFalloff.Max.ToString(FLOAT_FORMAT) + ", Default: " + light.LightFalloff.Default.ToString(FLOAT_FORMAT));

                if(spotlight == null)
                    SetText(line++, "Physical collisions: " + (light.HasPhysics ? "On" : "Off"), MyFontEnum.DarkBlue);

                return;
            }

            var oreDetector = def as MyOreDetectorDefinition;
            if(oreDetector != null)
            {
                var requiredPowerInput = 0.002f; // HACK hardcoded; from MyOreDetector

                SetText(line++, "Requires power*: " + PowerFormat(requiredPowerInput) + ", Sink group: " + ResourceGroup(oreDetector.ResourceSinkGroup));
                SetText(line++, "Max range: " + DistanceFormat(oreDetector.MaximumRange));
                return;
            }

            var gyro = def as MyGyroDefinition;
            if(gyro != null)
            {
                SetText(line++, "Requires power: " + PowerFormat(gyro.RequiredPowerInput) + ", Sink group: " + ResourceGroup(gyro.ResourceSinkGroup));
                SetText(line++, "Force: " + ForceFormat(gyro.ForceMagnitude));
                return;
            }

            var projector = def as MyProjectorDefinition;
            if(projector != null)
            {
                SetText(line++, "Requires power: " + PowerFormat(projector.RequiredPowerInput) + ", Sink group: " + ResourceGroup(projector.ResourceSinkGroup));
                return;
            }

            var door = def as MyDoorDefinition;
            if(door != null)
            {
                float requiredPowerInput = 3E-05f; // HACK hardcoded; from MyDoor

                SetText(line++, "Requires power*: " + PowerFormat(requiredPowerInput) + ", Sink group: " + ResourceGroup(door.ResourceSinkGroup));
                SetText(line++, "Move time: " + TimeFormat(door.MaxOpen / door.OpeningSpeed));
                SetText(line++, "Move distance: " + DistanceFormat(door.MaxOpen));
                return;
            }

            var airTightDoor = def as MyAirtightDoorGenericDefinition; // does not extend MyDoorDefinition
            if(airTightDoor != null)
            {
                SetText(line++, "Usage - Idle: " + PowerFormat(airTightDoor.PowerConsumptionIdle) + ", Moving: " + PowerFormat(airTightDoor.PowerConsumptionMoving) + ", Sink group: " + ResourceGroup(airTightDoor.ResourceSinkGroup));

                // needs subpart count to reliably compute move time
                //SetText(line++, "Move time: " + TimeFormat((airTightDoor.SubpartMovementDistance * subparts) / airTightDoor.OpeningSpeed)); // <<< math not quite correct
                //SetText(line++, "Extending length: " + DistanceFormat(airTightDoor.SubpartMovementDistance * subparts));
                // also different calculation required for sliding doors than hangar doors

                return;
            }

            var advDoor = def as MyAdvancedDoorDefinition; // does not extend MyDoorDefinition
            if(advDoor != null)
            {
                SetText(line++, "Usage - Idle: " + PowerFormat(advDoor.PowerConsumptionIdle) + ", Moving: " + PowerFormat(advDoor.PowerConsumptionMoving) + ", Sink group: " + ResourceGroup(advDoor.ResourceSinkGroup));

                float openTime = 0;
                float closeTime = 0;

                foreach(var seq in advDoor.OpeningSequence)
                {
                    var moveTime = (seq.MaxOpen / seq.Speed);

                    openTime = Math.Max(openTime, seq.OpenDelay + moveTime);
                    closeTime = Math.Max(closeTime, seq.CloseDelay + moveTime);
                }

                SetText(line++, "Move time - Opening: " + TimeFormat(openTime) + ", Closing: " + TimeFormat(closeTime));
                return;
            }

            var production = def as MyProductionBlockDefinition;
            if(production != null)
            {
                SetText(line++, "Usage - Idle: " + PowerFormat(production.StandbyPowerConsumption) + ", Operational: " + PowerFormat(production.OperationalPowerConsumption) + ", Sink group: " + ResourceGroup(production.ResourceSinkGroup));

                var assembler = def as MyAssemblerDefinition;
                if(assembler != null)
                {
                    var mulSpeed = MyAPIGateway.Session.AssemblerSpeedMultiplier;
                    var mulEff = MyAPIGateway.Session.AssemblerEfficiencyMultiplier;

                    SetText(line++, "Assembly speed: " + PercentFormat(assembler.AssemblySpeed * mulSpeed) + MultiplierFormat(mulSpeed) + ", Efficiency: " + PercentFormat(1 / mulEff) + MultiplierFormat(mulEff));
                }

                var refinery = def as MyRefineryDefinition;
                if(refinery != null)
                {
                    var mul = MyAPIGateway.Session.RefinerySpeedMultiplier;

                    SetText(line++, "Refine speed: " + PercentFormat(refinery.RefineSpeed * mul) + MultiplierFormat(mul) + ", Efficiency: " + PercentFormat(refinery.MaterialEfficiency));
                }

                var gasTank = def as MyGasTankDefinition;
                if(gasTank != null)
                {
                    SetText(line++, "Stores: " + gasTank.StoredGasId.SubtypeName + ", Capacity: " + gasTank.Capacity.ToString(NUMBER_FORMAT));
                }

                var oxygenGenerator = def as MyOxygenGeneratorDefinition;
                if(oxygenGenerator != null)
                {
                    SetText(line++, "Ice consumption: " + MassFormat(oxygenGenerator.IceConsumptionPerSecond) + " per second");

                    str.Clear();

                    foreach(var gas in oxygenGenerator.ProducedGases)
                    {
                        str.Append(gas.Id.SubtypeName).Append(" (ratio ").Append(gas.IceToGasRatio).Append("), ");
                    }

                    str.Length -= 2;

                    SetText(line++, "Produces: " + str);
                }

                SetText(line++, "Inventory: " + InventoryFormat((production.InventoryMaxVolume > 0 ? production.InventoryMaxVolume : production.InventorySize.Volume) * 1000));

                if(production.BlueprintClasses != null)
                {
                    if(production.BlueprintClasses.Count == 0)
                    {
                        SetText(line++, "Has no blueprint classes (?!)", MyFontEnum.Red);
                    }
                    else
                    {
                        str.Clear();

                        foreach(var bp in production.BlueprintClasses)
                        {
                            str.Append(bp.DisplayNameText).Append(',');
                        }

                        str.Length -= 1;

                        if(def is MyRefineryDefinition)
                            SetText(line++, "Refines: " + str);
                        else if(def is MyGasTankDefinition)
                            SetText(line++, "Refills: " + str);
                        else if(def is MyAssemblerDefinition)
                            SetText(line++, "Builds: " + str);
                        else
                            SetText(line++, "Blueprints: " + str);
                    }
                }

                return;
            }

            var powerProducer = def as MyPowerProducerDefinition;
            if(powerProducer != null)
            {
                SetText(line++, "Power output: " + PowerFormat(powerProducer.MaxPowerOutput) + ", Group: " + ResourceGroup(powerProducer.ResourceSourceGroup));

                var reactor = def as MyReactorDefinition;
                if(reactor != null)
                {
                    if(reactor.FuelDefinition != null)
                    {
                        SetText(line++, "Requires fuel: " + reactor.FuelId.SubtypeId);
                    }

                    SetText(line++, "Inventory: " + InventoryFormat((reactor.InventoryMaxVolume > 0 ? reactor.InventoryMaxVolume : reactor.InventorySize.Volume) * 1000));

                    var invLimit = reactor.InventoryConstraint;

                    if(invLimit != null)
                    {
                        SetText(line++, "Inventory items " + (invLimit.IsWhitelist ? "allowed" : "NOT allowed") + ":");

                        foreach(var id in invLimit.ConstrainedIds)
                        {
                            string typeName = id.TypeId.ToString();
                            typeName = typeName.Substring(typeName.IndexOf('_') + 1);

                            SetText(line++, "       - " + typeName + " / " + id.SubtypeName);
                        }

                        foreach(var type in invLimit.ConstrainedTypes)
                        {
                            string typeName = type.ToString();
                            typeName = typeName.Substring(typeName.IndexOf('_') + 1);

                            SetText(line++, "       - All of type: " + typeName);
                        }
                    }
                }

                var battery = def as MyBatteryBlockDefinition;
                if(battery != null)
                {
                    SetText(line++, "Power input: " + PowerFormat(battery.RequiredPowerInput) + (battery.AdaptibleInput ? " (adaptable)" : "") + ", Group: " + ResourceGroup(battery.ResourceSinkGroup));
                    SetText(line++, "Power capacity: " + PowerStorageFormat(battery.MaxStoredPower) + ", Initial: " + PowerStorageFormat(battery.MaxStoredPower * battery.InitialStoredPowerRatio) + " (" + Math.Round(battery.InitialStoredPowerRatio * 100, 2) + "%)");
                    return;
                }

                var solarPanel = def as MySolarPanelDefinition;
                if(solarPanel != null)
                {
                    SetText(line++, (solarPanel.IsTwoSided ? "Two-sided" : "One-sided"), (solarPanel.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red));
                }

                return;
            }

            var oxygenFarm = def as MyOxygenFarmDefinition;
            if(oxygenFarm != null)
            {
                SetText(line++, "Power usage: " + PowerFormat(oxygenFarm.OperationalPowerConsumption) + ", Sink group: " + ResourceGroup(oxygenFarm.ResourceSinkGroup));
                SetText(line++, "Produces: " + oxygenFarm.MaxGasOutput.ToString(NUMBER_FORMAT) + " " + oxygenFarm.ProducedGas.SubtypeName + " per second, Source group: " + ResourceGroup(oxygenFarm.ResourceSourceGroup));
                SetText(line++, (oxygenFarm.IsTwoSided ? "Two-sided" : "One-sided"), (oxygenFarm.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red));
                return;
            }

            var radioAntenna = def as MyRadioAntennaDefinition;
            if(radioAntenna != null)
            {
                // HACK hardcoded; from MyRadioAntenna
                float maxRadius = (def.CubeSize == MyCubeSize.Large ? 50000f : 5000f);
                float requiredPowerInput = (maxRadius / 500f) * 0.002f;

                SetText(line++, "Max required power*: " + PowerFormat(requiredPowerInput) + ", Sink group: " + ResourceGroup(radioAntenna.ResourceSinkGroup));
                SetText(line++, "Max radius*: " + DistanceFormat(maxRadius));
                return;
            }

            var laserAntenna = def as MyLaserAntennaDefinition;
            if(laserAntenna != null)
            {
                SetText(line++, "Usage - Idle: " + PowerFormat(laserAntenna.PowerInputIdle) + ", Turning: " + PowerFormat(laserAntenna.PowerInputTurning) + ", Connected: " + PowerFormat(laserAntenna.PowerInputLasing));
                SetText(line++, "Range: " + DistanceFormat(laserAntenna.MaxRange) + ", Line-of-sight: " + (laserAntenna.RequireLineOfSight ? "Required" : "Not required!"), (laserAntenna.RequireLineOfSight ? MyFontEnum.White : MyFontEnum.Green));
                SetText(line++, "Rotation Pitch: " + AngleFormat(laserAntenna.MinElevationDegrees) + " to " + AngleFormat(laserAntenna.MaxElevationDegrees) + ", Yaw: " + AngleFormat(laserAntenna.MinAzimuthDegrees) + " to " + AngleFormat(laserAntenna.MaxAzimuthDegrees));
                return;
            }

            var beacon = def as MyBeaconDefinition;
            if(beacon != null)
            {
                // HACK hardcoded; from MyBeacon
                float maxRadius = (def.CubeSize == MyCubeSize.Large ? 50000f : 5000f);
                float requiredPowerInput = (maxRadius / 100000f) * 0.02f;

                SetText(line++, "Max required power*: " + PowerFormat(requiredPowerInput) + ", Sink group: " + ResourceGroup(beacon.ResourceSinkGroup));
                SetText(line++, "Max radius*: " + DistanceFormat(maxRadius));
                return;
            }

            var timer = def as MyTimerBlockDefinition;
            if(timer != null)
            {
                // HACK hardcoded; from MyTimerBlock
                float requiredPowerInput = 1E-07f;

                SetText(line++, "Requires power*: " + PowerFormat(requiredPowerInput) + ", Sink group: " + ResourceGroup(timer.ResourceSinkGroup));
                return;
            }

            var pb = def as MyProgrammableBlockDefinition;
            if(pb != null)
            {
                // HACK hardcoded; from MyProgrammableBlock
                float requiredPowerInput = 0.0005f;

                SetText(line++, "Requires power*: " + PowerFormat(requiredPowerInput) + ", Sink group: " + ResourceGroup(pb.ResourceSinkGroup));
                return;
            }

            var sound = def as MySoundBlockDefinition;
            if(sound != null)
            {
                // HACK hardcoded; from MySoundBlock
                float requiredPowerInput = 0.0002f;

                SetText(line++, "Requires power*: " + PowerFormat(requiredPowerInput) + ", Sink group: " + ResourceGroup(sound.ResourceSinkGroup));
                return;
            }

            var sensor = def as MySensorBlockDefinition;
            if(sensor != null)
            {
                // HACK hardcoded; from MySensorBlock
                // sensor.RequiredPowerInput exists but is always reporting 0 and it seems ignored in the source code
                Vector3 minField = Vector3.One;
                Vector3 maxField = new Vector3(sensor.MaxRange * 2);
                float requiredPower = 0.0003f * (float)Math.Pow((maxField - minField).Volume, 1f / 3f);

                SetText(line++, "Max required power*: " + PowerFormat(requiredPower) + ", Sink group: " + ResourceGroup(sensor.ResourceSinkGroup));
                SetText(line++, "Max area: " + VectorFormat(maxField));
                return;
            }

            var artificialMass = def as MyVirtualMassDefinition;
            if(artificialMass != null)
            {
                SetText(line++, "Requires power: " + PowerFormat(artificialMass.RequiredPowerInput) + ", Sink group: " + ResourceGroup(artificialMass.ResourceSinkGroup));
                SetText(line++, "Artificial mass: " + MassFormat(artificialMass.VirtualMass));
                return;
            }

            var spaceBall = def as MySpaceBallDefinition; // this doesn't extend MyVirtualMassDefinition
            if(spaceBall != null)
            {
                SetText(line++, "Requires power*: No", MyFontEnum.Green);
                SetText(line++, "Max artificial mass: " + MassFormat(spaceBall.MaxVirtualMass));
                return;
            }

            var warhead = def as MyWarheadDefinition;
            if(warhead != null)
            {
                SetText(line++, "Requires power*: No", MyFontEnum.Green);
                SetText(line++, "Radius: " + DistanceFormat(warhead.ExplosionRadius));
                SetText(line++, "Damage: " + warhead.WarheadExplosionDamage.ToString("#,###,###,###,###.##"));
                return;
            }

            var button = def as MyButtonPanelDefinition;
            if(button != null)
            {
                // HACK hardcoded; from MyButtonPanel
                float requiredPowerInput = 0.0001f;

                SetText(line++, "Requires power*: " + PowerFormat(requiredPowerInput) + ", Sink group: " + ResourceGroup(button.ResourceSinkGroup));
                SetText(line++, "Button count: " + button.ButtonCount);
                return;
            }

            var lcd = def as MyTextPanelDefinition;
            if(lcd != null)
            {
                SetText(line++, "Requires power: " + PowerFormat(lcd.RequiredPowerInput) + ", Sink group: " + ResourceGroup(lcd.ResourceSinkGroup));
                SetText(line++, "Screen resolution: " + (lcd.TextureResolution * lcd.TextureAspectRadio) + "x" + lcd.TextureResolution);
                return;
            }

            var camera = def as MyCameraBlockDefinition;
            if(camera != null)
            {
                SetText(line++, "Requires power: " + PowerFormat(camera.RequiredPowerInput) + ", Sink group: " + ResourceGroup(camera.ResourceSinkGroup));
                SetText(line++, "Field of view: " + RadAngleFormat(camera.MinFov) + " to " + RadAngleFormat(camera.MaxFov));

                //var index = Math.Max(camera.OverlayTexture.LastIndexOf('/'), camera.OverlayTexture.LastIndexOf('\\')); // last / or \ char
                //SetText(line++, "Overlay texture: " + camera.OverlayTexture.Substring(index + 1));
                return;
            }

            var cargo = def as MyCargoContainerDefinition;
            if(cargo != null)
            {
                var poweredCargo = def as MyPoweredCargoContainerDefinition;
                if(poweredCargo != null)
                {
                    SetText(line++, "Requires power: " + PowerFormat(poweredCargo.RequiredPowerInput) + ", Sink group: " + ResourceGroup(poweredCargo.ResourceSinkGroup));
                }

                float maxVolume = cargo.InventorySize.Volume;

                if(maxVolume == 0)
                {
                    // HACK hardcoded; from MyCargoContainer
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    maxVolume = (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize;

                    SetText(line++, "Inventory*: " + InventoryFormat(maxVolume * 1000));
                }
                else
                {
                    SetText(line++, "Inventory: " + InventoryFormat(maxVolume * 1000));
                }

                return;
            }

            var sorter = def as MyConveyorSorterDefinition;
            if(sorter != null)
            {
                SetText(line++, "Requires power: " + PowerFormat(sorter.PowerInput) + ", Sink group: " + ResourceGroup(sorter.ResourceSinkGroup));
                SetText(line++, "Inventory: " + InventoryFormat(sorter.InventorySize.Volume * 1000));
                return;
            }

            var gravity = def as MyGravityGeneratorBaseDefinition;
            if(gravity != null)
            {
                var gravityFlat = def as MyGravityGeneratorDefinition;
                if(gravityFlat != null)
                {
                    SetText(line++, "Requires power: " + PowerFormat(gravityFlat.RequiredPowerInput) + ", Sink group: " + ResourceGroup(gravityFlat.ResourceSinkGroup));
                    SetText(line++, "Field size: " + VectorFormat(gravityFlat.MinFieldSize) + " to " + VectorFormat(gravityFlat.MaxFieldSize));
                }

                var gravitySphere = def as MyGravityGeneratorSphereDefinition;
                if(gravitySphere != null)
                {
                    SetText(line++, "Base power usage: " + PowerFormat(gravitySphere.BasePowerInput) + ", Consumption: " + PowerFormat(gravitySphere.ConsumptionPower) + ", Sink group: " + ResourceGroup(gravitySphere.ResourceSinkGroup));
                    SetText(line++, "Radius: " + DistanceFormat(gravitySphere.MinRadius) + " to " + DistanceFormat(gravitySphere.MaxRadius));
                }

                SetText(line++, "Acceleration: " + ForceFormat(gravity.MinGravityAcceleration) + " to " + ForceFormat(gravity.MaxGravityAcceleration));
                return;
            }

            var jumpDrive = def as MyJumpDriveDefinition;
            if(jumpDrive != null)
            {
                SetText(line++, "Power required: " + PowerFormat(jumpDrive.RequiredPowerInput) + ", For jump: " + PowerFormat(jumpDrive.PowerNeededForJump) + ", Sink group: " + ResourceGroup(jumpDrive.ResourceSinkGroup));
                SetText(line++, "Max distance: " + DistanceFormat((float)jumpDrive.MaxJumpDistance));
                SetText(line++, "Max mass: " + MassFormat((float)jumpDrive.MaxJumpMass));
                SetText(line++, "Jump delay: " + TimeFormat(jumpDrive.JumpDelay));
                return;
            }

            var merger = def as MyMergeBlockDefinition;
            if(merger != null)
            {
                SetText(line++, "Requires power*: No", MyFontEnum.Green);
                SetText(line++, "Magnetic force: " + ForceFormat(merger.Strength));
                return;
            }

            var weapon = def as MyWeaponBlockDefinition;
            if(weapon != null)
            {
                float requiredPowerInput = -1;

                if(def is MyLargeTurretBaseDefinition)
                {
                    requiredPowerInput = 0.002f; // HACK hardcoded; from MyLargeTurretBase
                }
                else
                {
                    switch(defType)
                    {
                        case "MyObjectBuilder_SmallGatlingGun":
                        case "MyObjectBuilder_SmallMissileLauncher":
                        case "MyObjectBuilder_SmallMissileLauncherReload":
                            requiredPowerInput = 0.0002f; // HACK hardcoded; from MySmallMissileLauncher & MySmallGatlingGun
                            break;
                    }
                }

                if(requiredPowerInput > 0)
                    SetText(line++, "Requires power*: " + PowerFormat(requiredPowerInput) + ", Sink group: " + ResourceGroup(weapon.ResourceSinkGroup));
                else
                    SetText(line++, "Power group: " + ResourceGroup(weapon.ResourceSinkGroup));

                SetText(line++, "Inventory: " + InventoryFormat(weapon.InventoryMaxVolume * 1000));

                var largeTurret = def as MyLargeTurretBaseDefinition;
                if(largeTurret != null)
                {
                    SetText(line++, "Range: " + DistanceFormat(largeTurret.MaxRangeMeters) + ", AI: " + (largeTurret.AiEnabled ? "Yes" : "No") + (largeTurret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)"));
                    SetText(line++, "Speed - Pitch: " + TorqueFormat(largeTurret.ElevationSpeed * 100) + ", Yaw: " + TorqueFormat(largeTurret.RotationSpeed * 100));
                    SetText(line++, "Rotation - Pitch: " + AngleFormat(largeTurret.MinElevationDegrees) + " to " + AngleFormat(largeTurret.MaxElevationDegrees) + ", Yaw: " + AngleFormat(largeTurret.MinAzimuthDegrees) + " to " + AngleFormat(largeTurret.MaxAzimuthDegrees));
                }

                var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);

                str.Clear();

                for(int i = 0; i < wepDef.AmmoMagazinesId.Length; i++)
                {
                    var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[i]);
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                    var weaponData = wepDef.WeaponAmmoDatas[(int)ammo.AmmoType];

                    str.Append(mag.Id.SubtypeName).Append(" (").Append(weaponData.RateOfFire).Append(" RPM)").Append(", ");
                }

                str.Length -= 2;

                SetText(line++, "Ammo: " + str.ToString());
                return;
            }
        }

        private string ResourceGroup(MyStringHash hash)
        {
            return ResourceGroup(hash == null ? "" : hash.String);
        }

        private string ResourceGroup(string group) // some ResourceSinkGroup fields are MyStringHash while others are string type :/
        {
            return (group == null || group.Length == 0 ? "(UNDEFINED)" : group);
        }

        private string ForceFormat(float N)
        {
            if(N > 1000000)
                return (N / 1000000).ToString(FLOAT_FORMAT) + " MN";

            if(N > 1000)
                return (N / 1000).ToString(FLOAT_FORMAT) + " kN";

            return N.ToString(FLOAT_FORMAT) + " N";
        }

        private string TorqueFormat(float N)
        {
            return N.ToString(NUMBER_FORMAT) + " NM";
        }

        private string PowerFormat(float MW)
        {
            float W = MW * 1000000f;

            if(W > 1000000)
                return MW.ToString(FLOAT_FORMAT) + " MW";
            if(W > 1000)
                return (W / 1000f).ToString(FLOAT_FORMAT) + " kW";

            return W.ToString(FLOAT_FORMAT) + " W";
        }

        private string PowerStorageFormat(float MW)
        {
            return PowerFormat(MW) + "h";
        }

        private string DistanceFormat(float m)
        {
            if(m > 1000)
                return (m / 1000).ToString(FLOAT_FORMAT) + " km";

            if(m < 10)
                return m.ToString(FLOAT_FORMAT) + " m";

            return m.ToString(INT_FORMAT) + " m";
        }

        private string MassFormat(float kg)
        {
            if(kg > 1000000)
                return (kg / 1000000).ToString("#,###,###,###,###.##") + " MT";

            if(kg > 1000)
                return (kg / 1000).ToString(FLOAT_FORMAT) + " T";

            if(kg < 1f)
                return (kg * 1000).ToString(FLOAT3_FORMAT) + " g";

            return kg.ToString("#,###,###") + " kg";
        }

        private string InventoryFormat(float liters)
        {
            var mul = MyAPIGateway.Session.InventoryMultiplier;
            var str = new StringBuilder();

            if(liters > 1000)
                str.Append(((liters / 1000) * mul).ToString(NUMBER_FORMAT)).Append(" m³");
            else if(liters > 100)
                str.Append(((liters / 100) * mul).ToString(NUMBER_FORMAT)).Append(" hL");
            else
                str.Append((liters * mul).ToString(NUMBER_FORMAT)).Append(" L");

            if(Math.Abs(mul - 1) > 0.001f)
                str.Append(" (x").Append(Math.Round(mul, 2)).Append(")");

            var items = MyDefinitionManager.Static.GetPhysicalItemDefinitions();
            float minMass = float.MaxValue;
            float maxMass = 0;

            foreach(var item in items)
            {
                if(item.Mass <= 0 || item.Volume <= 0) // skip physics defining items
                    continue;

                var mass = item.Mass * (liters / item.Volume);
                minMass = Math.Min(mass, minMass);
                maxMass = Math.Max(mass, maxMass);
            }

            str.Append(", Expected cargo mass: ").Append(MassFormat(minMass)).Append(" to ").Append(MassFormat(maxMass));
            return str.ToString();
        }

        private string TimeFormat(int seconds)
        {
            return String.Format("{0:00}:{1:00}", (seconds / 60), (seconds % 60));
        }

        private string TimeFormat(float seconds)
        {
            return String.Format("{0:0.##}s", seconds);
        }

        private string RadAngleFormat(float radians)
        {
            return AngleFormat(MathHelper.ToDegrees(radians));
        }

        private string AngleFormat(float degrees)
        {
            return degrees.ToString(INT_FORMAT) + '°';
        }

        private string VectorFormat(Vector3 vec)
        {
            return vec.X + "x" + vec.Y + "x" + vec.Z;
        }

        private string SpeedFormat(float mps)
        {
            return mps.ToString(FLOAT_FORMAT) + " m/s";
        }

        private string PercentFormat(float ratio)
        {
            return (int)(ratio * 100) + "%";
        }

        private string MultiplierFormat(float mul)
        {
            return (Math.Abs(mul - 1f) > 0.001f ? " (x" + Math.Round(mul, 2) + ")" : "");
        }
        
        private void SetText(int line, string text, string font = MyFontEnum.White, int aliveTime = 100)
        {
            if(hudLines == null)
                hudLines = new List<IMyHudNotification>();

            if(line >= hudLines.Count)
                hudLines.Add(MyAPIGateway.Utilities.CreateNotification(""));

            text = "• " + text;
            maxLineWidthPx = Math.Max(maxLineWidthPx, GetStringSize(text));

            hudLines[line].Font = font;
            hudLines[line].Text = text;
            hudLines[line].AliveTime = aliveTime;
        }

        public void MessageEntered(string msg, ref bool send)
        {
            if(msg.StartsWith("/buildinfo", StringComparison.InvariantCultureIgnoreCase))
            {
                send = false;
                showBuildInfo = !showBuildInfo;
                MyAPIGateway.Utilities.ShowMessage(Log.modName, "Display turned " + (showBuildInfo ? "on" : "off"));
            }
        }
    }

    public static class Workarounds
    {
        public static bool InGameplayMenu(this IMyGui GUI) // TODO REMOVE once ActiveGamePlayScreen is fixed in both branches
        {
            try
            {
                return GUI.ActiveGamePlayScreen != null;
            }
            catch(Exception)
            {
                return false;
            }
        }
    }
}