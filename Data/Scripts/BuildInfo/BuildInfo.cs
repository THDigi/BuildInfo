using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Input;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

using Draygo.API;

namespace Digi.BuildInfo
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class BuildInfo : MySessionComponentBase
    {
        private const long WORKSHOP_ID = 514062285;

        public override void LoadData()
        {
            Log.SetUp("Build Info", WORKSHOP_ID, "BuildInfo");
        }

        private bool init = false;
        private bool isThisDS = false;

        private HUDTextAPI textAPI = null;
        private bool textAPIenabled = false;
        private bool textShown = false;
        private Vector2D textPosition2D = Vector2D.Zero;
        private Vector2D menuPosition2D = Vector2D.Zero;
        private HUDTextAPI.HUDMessage textObject;
        private float textLengthMeters = 0.1f;
        private int textLines = 0;
        private bool hudVisible = true;
        private double aspectRatio = 1;
        private bool rotationHints = true;

        private bool showMenu = false;
        private bool menuNeedsUpdate = true;
        private int menuSelectedItem = 0;
        private Vector3 previousMove = Vector3.Zero;

        private bool showBuildInfo = true;
        private bool showMountPoints = false;
        private bool useTextAPI = true;

        private List<IMyHudNotification> hudLines;
        private MyObjectBuilderType lastTypeId;
        private MyStringHash lastSubTypeId;

        private IMyHudNotification buildInfoNotification = null;
        private IMyHudNotification mountPointsNotification = null;
        private IMyHudNotification transparencyNotification = null;

        private short skip = 0;
        private int maxLineWidthPx = 0;
        private long lastScroll = 0;
        private const int SCROLL_FROM_LINE = 2;
        private int atLine = SCROLL_FROM_LINE;
        private const int MAX_LINES = 8;

        private const double TEXT_SCALE = 0.8;
        private static readonly Vector2D TEXT_HUDPOS = new Vector2D(-0.9825, 0.8);
        private static readonly Vector2D TEXT_HUDPOS_TRIPPLE = new Vector2D(TEXT_HUDPOS.X / 3f, TEXT_HUDPOS.Y);
        private static readonly Vector2D MENU_HUDPOS = new Vector2D(-0.3, 0.4);
        private static readonly Vector2D MENU_HUDPOS_TRIPPLE = new Vector2D(MENU_HUDPOS.X / 3f, MENU_HUDPOS.Y);

        private static readonly MyStringId MATERIAL_SQUARE = MyStringId.GetOrCompute("Square");
        private static readonly MyStringId MATERIAL_BACKGROUND = MyStringId.GetOrCompute("BuildInfo_TextBackground");

        private const string INT_FORMAT = "0";
        private const string FLOAT_FORMAT = "0.00";
        private const string FLOAT3_FORMAT = "0.000";
        private const string NUMBER_FORMAT = "N";

        private readonly HashSet<Vector3I> cubes = new HashSet<Vector3I>();
        private readonly StringBuilder str = new StringBuilder();
        private readonly StringBuilder str2 = new StringBuilder();
        private readonly Dictionary<MyStringHash, ResourceGroupData> resourceGroupPriority = new Dictionary<MyStringHash, ResourceGroupData>();
        private int resourceSinkGroups = 0;
        private int resourceSourceGroups = 0;

        public static readonly Dictionary<MyDefinitionId, IBlockData> blockData = new Dictionary<MyDefinitionId, IBlockData>();

        public interface IBlockData { }

        public class BlockDataThrust : IBlockData
        {
            public readonly float radius;
            public readonly float distance;
            public readonly int flames;

            public BlockDataThrust(MyThrust thrust)
            {
                var def = thrust.BlockDefinition;
                double distSq = 0;

                // HACK hardcoded; from MyThrust.UpdateThrustFlame()
                thrust.ThrustLengthRand = 10f * def.FlameLengthScale; // make the GetDamageCapsuleLine() method think it thrusts at max and with no random

                var m = thrust.WorldMatrix;

                foreach(var flame in thrust.Flames)
                {
                    var flameLine = thrust.GetDamageCapsuleLine(flame, ref m);
                    var flameDistSq = (flameLine.From - flameLine.To).LengthSquared();

                    if(flameDistSq > distSq)
                    {
                        distSq = flameDistSq;
                        radius = flame.Radius;
                    }
                }

                distance = (float)Math.Sqrt(distSq);
                flames = thrust.Flames.Count;

                blockData.Add(def.Id, this);
            }
        }

        private static readonly Dictionary<char, int> charSize = new Dictionary<char, int>();
        private const int SPACE_SIZE = 8;

        struct ResourceGroupData
        {
            public MyResourceDistributionGroupDefinition def;
            public int priority;
        }

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

            if(!isThisDS)
            {
                textAPI = new HUDTextAPI(WORKSHOP_ID);

                MyAPIGateway.Gui.GuiControlRemoved += GuiControlRemoved;

                UpdateConfigValues();

                #region Resource Group Priority precompute
                resourceGroupPriority.Clear();
                resourceSourceGroups = 0;
                resourceSinkGroups = 0;

                var groupDefs = MyDefinitionManager.Static.GetDefinitionsOfType<MyResourceDistributionGroupDefinition>();
                var orderedGroups = from def in groupDefs
                                    orderby def.Priority
                                    select def;

                foreach(var group in orderedGroups)
                {
                    resourceGroupPriority.Add(group.Id.SubtypeId, new ResourceGroupData()
                    {
                        def = group,
                        priority = (group.IsSource ? ++resourceSourceGroups : ++resourceSinkGroups),
                    });
                }
                #endregion
            }
        }

        protected override void UnloadData()
        {
            try
            {
                if(init)
                {
                    init = false;
                    hudLines = null;

                    MyAPIGateway.Utilities.MessageEntered -= MessageEntered;

                    MyAPIGateway.Gui.GuiControlRemoved -= GuiControlRemoved;

                    if(textAPI != null)
                    {
                        textAPI.Close();
                        textAPI = null;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }

            Log.Close();

            cubes.Clear();
            blockData.Clear();
        }

        private void GuiControlRemoved(object obj)
        {
            try
            {
                if(obj.ToString().EndsWith("ScreenOptionsSpace")) // closing options menu just assumes you changed something so it'll re-check config settings
                {
                    UpdateConfigValues();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void UpdateConfigValues()
        {
            var cfg = MyAPIGateway.Session.Config;
            hudVisible = !cfg.MinimalHud;
            aspectRatio = (double)cfg.ScreenWidth / (double)cfg.ScreenHeight;
            bool tempRotationHints = cfg.RotationHints;

            if(rotationHints != tempRotationHints)
            {
                rotationHints = tempRotationHints;
                lastTypeId = typeof(MyObjectBuilder_World); // force re-draw
            }

            textObject.message = null;
            textPosition2D = (aspectRatio > 5 ? TEXT_HUDPOS_TRIPPLE : TEXT_HUDPOS);
            menuPosition2D = (aspectRatio > 5 ? MENU_HUDPOS_TRIPPLE : MENU_HUDPOS);
        }

        public override void HandleInput()
        {
            try
            {
                if(!init || MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible)
                    return;

                if(MyCubeBuilder.Static != null && MyCubeBuilder.Static.IsActivated)
                {
                    var input = MyAPIGateway.Input;

                    if(showMenu)
                    {
                        bool canUseTextAPI = (textAPI != null && textAPI.Heartbeat);
                        int usableMenuItems = (canUseTextAPI ? 5 : 4);
                        var move = Vector3.Round(input.GetPositionDelta(), 1);

                        if(previousMove.Z == 0)
                        {
                            if(move.Z > 0.2f)
                            {
                                menuNeedsUpdate = true;

                                if(++menuSelectedItem >= usableMenuItems)
                                    menuSelectedItem = 0;
                            }

                            if(move.Z < -0.2f)
                            {
                                menuNeedsUpdate = true;

                                if(--menuSelectedItem < 0)
                                    menuSelectedItem = (usableMenuItems - 1);
                            }
                        }

                        if(previousMove.X == 0 && Math.Abs(move.X) > 0.2f)
                        {
                            menuNeedsUpdate = true;

                            switch(menuSelectedItem)
                            {
                                case 0:
                                    showMenu = false;
                                    menuNeedsUpdate = true;
                                    break;
                                case 1:
                                    showBuildInfo = !showBuildInfo;
                                    if(buildInfoNotification == null)
                                        buildInfoNotification = MyAPIGateway.Utilities.CreateNotification("");
                                    buildInfoNotification.Text = (showBuildInfo ? "Build info ON" : "Build info OFF");
                                    buildInfoNotification.Show();
                                    break;
                                case 2:
                                    showMountPoints = !showMountPoints;
                                    break;
                                case 3:
                                    MyCubeBuilder.Static.UseTransparency = !MyCubeBuilder.Static.UseTransparency;
                                    break;
                                case 4:
                                    if(textShown)
                                        HideText();
                                    useTextAPI = !useTextAPI;
                                    break;
                            }
                        }

                        previousMove = move;
                    }

                    if(input.IsNewGameControlPressed(MyControlsSpace.VOXEL_HAND_SETTINGS))
                    {
                        if(input.IsAnyShiftKeyPressed())
                        {
                            MyCubeBuilder.Static.UseTransparency = !MyCubeBuilder.Static.UseTransparency;
                            menuNeedsUpdate = true;

                            if(transparencyNotification == null)
                                transparencyNotification = MyAPIGateway.Utilities.CreateNotification("");

                            transparencyNotification.Text = (MyCubeBuilder.Static.UseTransparency ? "Placement transparency ON" : "Placement transparency OFF");
                            transparencyNotification.Show();
                        }
                        else if(input.IsAnyCtrlKeyPressed())
                        {
                            showMountPoints = !showMountPoints;
                            menuNeedsUpdate = true;

                            if(mountPointsNotification == null)
                                mountPointsNotification = MyAPIGateway.Utilities.CreateNotification("");

                            mountPointsNotification.Text = (showMountPoints ? "Mount points view ON" : "Mount points view OFF");
                            mountPointsNotification.Show();
                        }
                        else
                        {
                            showMenu = !showMenu;
                            menuNeedsUpdate = true;
                        }
                    }
                }
                else if(showMenu)
                {
                    showMenu = false;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void HideText()
        {
            textShown = false;
            skip = 60; // forces instant check when re-equipping block

            if(textAPI != null && textAPI.Heartbeat)
            {
                textAPI.CreateAndSend(1, 0, Vector2D.Zero, string.Empty);
                textAPI.CreateAndSend(2, 0, Vector2D.Zero, string.Empty);
            }

            foreach(var hud in hudLines)
            {
                hud.Hide();
            }
        }

        public override void Draw()
        {
            try
            {
                if(!init)
                    return;

                // background for the textAPI's text
                if(textAPIenabled && textShown && hudVisible)
                {
                    var camera = MyAPIGateway.Session.Camera;
                    var camMatrix = camera.WorldMatrix;
                    var fov = camera.FovWithZoom;

                    var localscale = 0.1735 * Math.Tan(fov / 2);
                    var localXmul = (aspectRatio * 9d / 16d);
                    var localYmul = (1 / aspectRatio) * localXmul;

                    var origin = (showMenu ? menuPosition2D : textPosition2D);
                    origin.X *= localscale * localXmul;
                    origin.Y *= localscale * localYmul;
                    var textpos = Vector3D.Transform(new Vector3D(origin.X, origin.Y, -0.1), camMatrix);

                    var widthStep = localscale * 9d * 0.001075;
                    var width = textLengthMeters * widthStep;
                    var heightStep = localscale * 9d * 0.001;
                    var height = (textLines + 1) * heightStep;
                    textpos += camMatrix.Right * (width - (widthStep * 2)) + camMatrix.Down * (height - (heightStep * 2));

                    // TODO use HUD background transparency once that is a thing
                    MyTransparentGeometry.AddBillboardOriented(MATERIAL_BACKGROUND, Color.White * 0.75f, textpos, camMatrix.Left, camMatrix.Up, (float)width, (float)height);
                }

                if(showMountPoints && hudVisible && MyCubeBuilder.Static.DynamicMode)
                {
                    var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

                    if(def != null && MyCubeBuilder.Static.IsActivated)
                    {
                        var box = MyCubeBuilder.Static.GetBuildBoundingBox();
                        var m = MatrixD.CreateFromQuaternion(box.Orientation);
                        m.Translation = box.Center;

                        // TODO find a way to get gizmo center when placed on grids...
                        //var grid = MyCubeBuilder.Static.FindClosestGrid();
                        //
                        //if(grid != null)
                        //{
                        //    var gridPos = grid.WorldMatrix.Translation;
                        //    var localPos = (box.Center - gridPos) * grid.GridSize;
                        //    m.Translation = gridPos + localPos;
                        //
                        //    if(def.Size.Size > 1)
                        //    {
                        //        //var center = Vector3D.Transform(def.Center * grid.GridSize, m);
                        //        //MyAPIGateway.Utilities.ShowNotification("dot=" + Math.Round(Vector3D.Dot(m.Translation, m.Up), 3), 16);
                        //
                        //        var extraSize = (def.Size - Vector3I.One) * grid.GridSizeHalf;
                        //        m.Translation += m.Left * extraSize.X;
                        //        m.Translation += m.Up * extraSize.Y;
                        //        m.Translation += m.Backward * extraSize.Z;
                        //
                        //        //var extraSize = def.Size - Vector3I.One;
                        //        //var dotX = (Vector3D.Dot(m., m.Right) > 0 ? 1 : -1);
                        //        //var dotY = (Vector3D.Dot(m.Translation, m.Up) > 0 ? 1 : -1);
                        //        //var dotZ = (Vector3D.Dot(m.Translation, m.Backward) > 0 ? 1 : -1);
                        //        //m.Translation += m.Right * (grid.GridSizeHalf * extraSize.X * dotX);
                        //        //m.Translation += m.Up * (grid.GridSizeHalf * extraSize.Y * dotY);
                        //        //m.Translation += m.Backward * (grid.GridSizeHalf * extraSize.Z * dotZ);
                        //    }
                        //}

                        MyCubeBuilder.DrawMountPoints(MyDefinitionManager.Static.GetCubeSize(def.CubeSize), def, ref m);
                    }
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

                // required here because it gets the previous value if used in HandleInput()
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.TOGGLE_HUD))
                    hudVisible = !MyAPIGateway.Session.Config.MinimalHud;

                textAPIenabled = (useTextAPI && textAPI != null && textAPI.Heartbeat);

                #region Cube builder active
                if(MyCubeBuilder.Static != null && MyCubeBuilder.Static.IsActivated && MyCubeBuilder.Static.CubeBuilderState != null && MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition != null)
                {
                    bool textUpdated = false;
                    int line = 0;

                    if(showMenu)
                    {
                        if(menuNeedsUpdate)
                        {
                            bool canUseTextAPI = (textAPI != null && textAPI.Heartbeat);
                            lastTypeId = typeof(MyObjectBuilder_World); // intentinoally invalid
                            lastSubTypeId = MyStringHash.NullOrEmpty;
                            int showMenuItems = 5;
                            menuNeedsUpdate = false;
                            textUpdated = true;

                            var control = MyAPIGateway.Input.GetGameControl(MyControlsSpace.VOXEL_HAND_SETTINGS);
                            string inputName = null;

                            if(control.GetKeyboardControl() != MyKeys.None)
                                inputName = control.GetKeyboardControl().ToString();
                            else if(control.GetSecondKeyboardControl() != MyKeys.None)
                                inputName = control.GetSecondKeyboardControl().ToString();
                            else if(control.GetMouseControl() != MyMouseButtonsEnum.None)
                                inputName = MyAPIGateway.Input.GetName(control.GetMouseControl());

                            SetText(line++, "Build info settings:", MyFontEnum.DarkBlue);

                            for(int i = 0; i < showMenuItems; i++)
                            {
                                str.Clear();
                                str.Append("[ ");

                                switch(i)
                                {
                                    case 0:
                                        str.Append("Close menu").Append(inputName == null ? "" : "   (" + inputName + ")");
                                        break;
                                    case 1:
                                        str.Append("Show build info: ").Append(showBuildInfo ? "ON" : "OFF");
                                        break;
                                    case 2:
                                        str.Append("Show mount points: ").Append(showMountPoints ? "ON" : "OFF").Append(inputName == null ? "" : "   (Ctrl+" + inputName + ")");
                                        break;
                                    case 3:
                                        str.Append("Transparent model: ").Append(MyCubeBuilder.Static.UseTransparency ? "ON" : "OFF").Append(inputName == null ? "" : "   (Shift+" + inputName + ")");
                                        break;
                                    case 4:
                                        str.Append("Use TextAPI: ");
                                        if(canUseTextAPI)
                                            str.Append(useTextAPI ? "ON" : "OFF");
                                        else
                                            str.Append("OFF (Mod not detected)");
                                        break;
                                }

                                str.Append(" ]");
                                SetText(line++, str.ToString(), (menuSelectedItem == i ? MyFontEnum.Green : ((i == 4 && !canUseTextAPI) || (i == 5 && rotationHints) ? MyFontEnum.Red : MyFontEnum.White)));
                            }

                            str.Clear();

                            SetText(line++, "Use movement controls to navigate and edit settings.", MyFontEnum.Blue);

                            if(inputName == null)
                                SetText(line++, "The 'Open voxel hand settings' control is not assigned!", MyFontEnum.ErrorMessageBoxCaption);
                        }
                    }
                    else if(showBuildInfo)
                    {
                        if(++skip >= 6)
                        {
                            skip = 0;

                            var def = MyCubeBuilder.Static.CubeBuilderState.CurrentBlockDefinition;
                            var defTypeId = def.Id.TypeId;

                            if(defTypeId != lastTypeId || def.Id.SubtypeId != lastSubTypeId)
                            {
                                lastTypeId = defTypeId;
                                lastSubTypeId = def.Id.SubtypeId;

                                bool isDoor = (defTypeId == typeof(MyObjectBuilder_AdvancedDoor)
                                            || defTypeId == typeof(MyObjectBuilder_AirtightDoorGeneric)
                                            || defTypeId == typeof(MyObjectBuilder_AirtightHangarDoor)
                                            || defTypeId == typeof(MyObjectBuilder_AirtightSlideDoor)
                                            || defTypeId == typeof(MyObjectBuilder_Door));

                                int airTightFaces = 0;
                                int totalFaces = 0;
                                bool airTight = (isDoor || IsAirTight(def, ref airTightFaces, ref totalFaces));
                                bool deformable = def.BlockTopology == MyBlockTopology.Cube;
                                int assembleTime = (int)(def.MaxIntegrity / def.IntegrityPointsPerSec);
                                bool buildModels = def.BuildProgressModels != null && def.BuildProgressModels.Length > 0;

                                maxLineWidthPx = 0;

                                var weldMul = MyAPIGateway.Session.WelderSpeedMultiplier;
                                var grindRatio = def.DisassembleRatio;

                                if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition) // HACK hardcoded; from MyDoor & MyAdvancedDoor's overridden DisassembleRatio
                                    grindRatio *= 3.3f;

                                textUpdated = true;

                                if(textAPIenabled)
                                {
                                    str.Clear().Append(def.DisplayNameText);
                                    var stages = def.BlockStages;

                                    if(stages != null && stages.Length > 0)
                                    {
                                        str.Append("  <color=0,200,0>(Variant 1 of ").Append(stages.Length + 1).Append(")");
                                    }
                                    else
                                    {
                                        stages = MyCubeBuilder.Static.ToolbarBlockDefinition.BlockStages;

                                        if(stages != null && stages.Length > 0)
                                        {
                                            int num = 0;

                                            for(int i = 0; i < stages.Length; ++i)
                                            {
                                                if(def.Id == stages[i])
                                                {
                                                    num = i + 2; // +2 because the 1st is not in the list
                                                    break;
                                                }
                                            }

                                            str.Append("  <color=0,200,0>(Variant ").Append(num).Append(" of ").Append(stages.Length + 1).Append(")");
                                        }
                                    }

                                    SetText(line++, str.ToString(), MyFontEnum.DarkBlue);
                                    str.Clear();
                                }

                                SetText(line++, MassFormat(def.Mass) + ", " + VectorFormat(def.Size) + ", " + TimeFormat(assembleTime / weldMul) + MultiplierFormat(weldMul) + (grindRatio > 1 ? ", Deconstruct speed: " + PercentFormat(1f / grindRatio) : "") + (buildModels ? "" : " (No construction models)"), MyFontEnum.White);
                                SetText(line++, "Integrity: " + def.MaxIntegrity.ToString("#,###,###,###,###") + ", Deformable: " + (deformable ? "Yes (" + def.DeformationRatio.ToString(FLOAT_FORMAT) + ")" : "No"), (deformable ? MyFontEnum.Blue : MyFontEnum.White));
                                SetText(line++, "Air-tight faces: " + (airTight ? "All" : (airTightFaces == 0 ? "None" : airTightFaces + " of " + totalFaces)), (isDoor || airTight ? MyFontEnum.Green : (airTightFaces == 0 ? MyFontEnum.Red : MyFontEnum.DarkBlue)));

                                bool componentLossLabelShown = false;
                                foreach(var comp in def.Components)
                                {
                                    if(comp.DeconstructItem != comp.Definition)
                                    {
                                        if(!componentLossLabelShown)
                                        {
                                            SetText(line++, "Grinding loss: " + comp.Definition.DisplayNameText + " turns into " + comp.DeconstructItem.DisplayNameText, MyFontEnum.Red);
                                            componentLossLabelShown = true;
                                        }
                                        else
                                            SetText(line++, (textAPIenabled ? "   " : "") + "                      - " + comp.Definition.DisplayNameText + " turns into " + comp.DeconstructItem.DisplayNameText, MyFontEnum.Red);
                                    }
                                }

                                // TODO when VoxelPlacementSettings and VoxelPlacementMode are whitelisted:
                                //if(def.VoxelPlacement.HasValue)
                                //{
                                //    var vp = def.VoxelPlacement.Value;
                                //    SetText(line++, "Voxel rules - Dynamic: " + vp.DynamicMode.PlacementMode + ", Static: " + vp.StaticMode.PlacementMode);
                                //}

                                if(defTypeId != typeof(MyObjectBuilder_CubeBlock))
                                    GetExtraInfo(ref line, def);

                                if(!def.Context.IsBaseGame)
                                    SetText(line++, "Mod: " + def.Context.ModName, MyFontEnum.Blue);
                            }
                        }
                    }
                    else
                    {
                        if(textShown)
                            HideText();

                        return;
                    }

                    if(textUpdated)
                    {
                        if(textAPIenabled)
                        {
                            // text API message sent only once when text changes

                            str.Clear();

                            string longestLine = string.Empty;

                            for(int l = 0; l < hudLines.Count; l++)
                            {
                                var hud = hudLines[l];

                                if(l >= line)
                                    continue;

                                var text = hud.Text;

                                // HACK lazy hack to prevent the title+variant to be longer - to fix later
                                var compare = text.Replace("<color=0,200,0>", "");
                                if(compare.Length > longestLine.Length)
                                    longestLine = compare;

                                switch(hud.Font)
                                {
                                    case MyFontEnum.White: str.Append("<color=white>"); break;
                                    case MyFontEnum.Red: str.Append("<color=255,100,0>"); break;
                                    case MyFontEnum.Green: str.Append("<color=0,200,0>"); break;
                                    case MyFontEnum.Blue: str.Append("<color=180,210,230>"); break;
                                    case MyFontEnum.DarkBlue: str.Append("<color=110,180,225>"); break;
                                    case MyFontEnum.ErrorMessageBoxCaption: str.Append("<color=255,0,0>"); break;
                                }

                                str.Append(text).Append('\n');
                            }

                            textLines = line;
                            textLengthMeters = (float)textAPI.GetLineLength(longestLine, TEXT_SCALE);

                            if(!showMenu && !rotationHints)
                            {
                                // TODO could use some optimization...

                                textPosition2D.X = 0;
                                textPosition2D.Y = 0.98;

                                var camera = MyAPIGateway.Session.Camera;
                                var camMatrix = camera.WorldMatrix;
                                var fov = camera.FovWithZoom;

                                var localscale = 0.1735 * Math.Tan(fov / 2);
                                var localXmul = localscale * (aspectRatio * 9d / 16d);
                                var localYmul = localscale * (1 / aspectRatio) * localXmul;

                                var textpos = Vector3D.Transform(new Vector3D(textPosition2D.X * localXmul, textPosition2D.Y * localYmul, -0.1), camMatrix);

                                var widthStep = localscale * 9d * 0.001075;
                                var width = textLengthMeters * widthStep;

                                var screenPosStart = camera.WorldToScreen(ref textpos);
                                textpos += camMatrix.Right * width;
                                var screenPosEnd = camera.WorldToScreen(ref textpos);
                                var panelSize = screenPosEnd - screenPosStart;

                                textPosition2D.X = (1.01 - (panelSize.X * 1.99));

                                if(aspectRatio > 5)
                                    textPosition2D /= 3.0;
                            }

                            textObject = new HUDTextAPI.HUDMessage(1, int.MaxValue, (showMenu ? menuPosition2D : textPosition2D), TEXT_SCALE, true, false, Color.Black, str.ToString());
                            textAPI.Send(textObject);
                            textShown = true;
                            str.Clear();
                        }
                        else
                        {
                            // HUD notification requires padding to look aligned and must be printed more constant to update the scrolling and all that

                            long now = DateTime.UtcNow.Ticks;
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
                    }

                    if(textAPIenabled)
                    {
                        // show last generated block info message
                        if(!textShown && !string.IsNullOrEmpty(textObject.message))
                        {
                            textAPI.Send(textObject);
                            textShown = true;
                        }
                    }
                    else if(hudLines != null)
                    {
                        // print and scroll through HUD notification types of messages, not needed for text API
                        textShown = true;
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

                            long now = DateTime.UtcNow.Ticks;

                            if(lastScroll < now)
                            {
                                if(++atLine >= lines)
                                    atLine = SCROLL_FROM_LINE;

                                lastScroll = now + (long)(TimeSpan.TicksPerSecond * 1.5f);
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

                    return;
                }
                #endregion Cube builder active

                // hide text if shown
                if(textShown)
                {
                    showMenu = false;
                    HideText();
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

        private void GetExtraInfo(ref int line, MyCubeBlockDefinition def)
        {
            // TODO convert these if conditions to 'as' checking when their interfaces are not internal anymore

            var defTypeId = def.Id.TypeId;

            if(defTypeId == typeof(MyObjectBuilder_TerminalBlock)) // control panel block
            {
                SetText(line++, "Power required*: No", MyFontEnum.Green);
                return;
            }

            if(defTypeId == typeof(MyObjectBuilder_Conveyor) || defTypeId == typeof(MyObjectBuilder_ConveyorConnector)) // conveyor hubs and tubes
            {
                // HACK hardcoded; from MyGridConveyorSystem
                float requiredPower = MyEnergyConstants.REQUIRED_INPUT_CONVEYOR_LINE;
                var powerGroup = MyStringHash.GetOrCompute("Conveyors");

                SetText(line++, "Power required*: " + PowerFormat(requiredPower) + ", " + ResourcePriority(powerGroup, true), MyFontEnum.White);
                return;
            }

            if(defTypeId == typeof(MyObjectBuilder_Drill))
            {
                // HACK hardcoded; from MyShipDrill
                float requiredPower = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_DRILL;
                float volume;

                SetText(line++, "Power required*: " + PowerFormat(requiredPower));

                if(GetInventoryFromComponent(def, out volume))
                {
                    SetText(line++, "Inventory: " + InventoryFormat(volume, typeof(MyObjectBuilder_Ore)));
                }
                else
                {
                    // HACK hardcoded; from MyShipDrill
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    volume = (float)(def.Size.X * def.Size.Y * def.Size.Z) * gridSize * gridSize * gridSize * 0.5f;
                    SetText(line++, "Inventory*: " + InventoryFormat(volume, typeof(MyObjectBuilder_Ore)));
                }

                // HACK MyShipDrillDefinition is internal :/
                var defObj = (MyObjectBuilder_ShipDrillDefinition)def.GetObjectBuilder();

                SetText(line++, "Mine radius: " + DistanceFormat(defObj.SensorRadius) + ", Front offset: " + DistanceFormat(defObj.SensorOffset));
                SetText(line++, "Alternate (no ore) radius: " + DistanceFormat(defObj.CutOutRadius) + ", Front offset: " + DistanceFormat(defObj.CutOutOffset));
                return;
            }

            if(defTypeId == typeof(MyObjectBuilder_ShipConnector))
            {
                // HACK hardcoded; from MyShipConnector
                float requiredPower = MyEnergyConstants.MAX_REQUIRED_POWER_CONNECTOR;
                if(def.CubeSize == MyCubeSize.Small)
                    requiredPower *= 0.01f;
                var powerGroup = MyStringHash.GetOrCompute("Conveyors");
                float volume;

                SetText(line++, "Power required*: " + PowerFormat(requiredPower) + ", " + ResourcePriority(powerGroup, true));

                if(GetInventoryFromComponent(def, out volume))
                {
                    SetText(line++, "Inventory: " + InventoryFormat(volume));
                }
                else
                {
                    // HACK hardcoded; from MyShipConnector
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    var inventorySize = def.Size * gridSize * 0.8f;
                    SetText(line++, "Inventory*: " + InventoryFormat(inventorySize.Volume));
                }
                return;
            }

            var shipWelder = def as MyShipWelderDefinition;
            var shipGrinder = def as MyShipGrinderDefinition;
            if(shipWelder != null || shipGrinder != null)
            {
                // HACK hardcoded; from MyShipToolBase
                float requiredPower = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_GRINDER;
                var powerGroup = MyStringHash.GetOrCompute("Defense");
                float volume;

                SetText(line++, "Power required*: " + PowerFormat(requiredPower) + ", " + ResourcePriority(powerGroup, true));

                if(GetInventoryFromComponent(def, out volume))
                {
                    SetText(line++, "Inventory: " + InventoryFormat(volume));
                }
                else
                {
                    // HACK hardcoded; from MyShipToolBase
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    volume = (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize * 0.5f;
                    SetText(line++, "Inventory*: " + InventoryFormat(volume));
                }

                if(shipWelder != null)
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

            var piston = def as MyPistonBaseDefinition;
            var motor = def as MyMotorStatorDefinition;
            if(piston != null || motor != null)
            {
                if(piston != null)
                {
                    SetText(line++, "Power required: " + PowerFormat(piston.RequiredPowerInput) + ", " + ResourcePriority(piston.ResourceSinkGroup));
                    SetText(line++, "Extended length: " + DistanceFormat(piston.Maximum));
                    SetText(line++, "Max speed: " + SpeedFormat(piston.MaxVelocity));
                }

                if(motor != null)
                {
                    SetText(line++, "Power required: " + PowerFormat(motor.RequiredPowerInput) + ", " + ResourcePriority(motor.ResourceSinkGroup));

                    if(!(def is MyMotorSuspensionDefinition))
                    {
                        SetText(line++, "Max force: " + ForceFormat(motor.MaxForceMagnitude));

                        if(motor.RotorDisplacementMin < motor.RotorDisplacementMax)
                            SetText(line++, "Displacement: " + DistanceFormat(motor.RotorDisplacementMin) + " to " + DistanceFormat(motor.RotorDisplacementMax));
                    }

                    var suspension = def as MyMotorSuspensionDefinition;
                    if(suspension != null)
                    {
                        SetText(line++, "Force: " + ForceFormat(suspension.PropulsionForce) + ", Steer speed: " + RotationSpeed(suspension.SteeringSpeed * 60) + ", Steer angle: " + AngleFormat(suspension.MaxSteer));
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
                    float volume;

                    if(GetInventoryFromComponent(def, out volume))
                    {
                        SetText(line++, "Inventory: " + InventoryFormat(volume));
                    }
                    else
                    {
                        volume = Vector3.One.Volume; // HACK hardcoded; from MyCockpit
                        SetText(line++, "Inventory*: " + InventoryFormat(volume));
                    }

                    SetText(line++, (cockpit.IsPressurized ? "Pressurized: Yes, Oxygen capacity: " + cockpit.OxygenCapacity.ToString(NUMBER_FORMAT) + " oxy" : "Pressurized: No"), (cockpit.IsPressurized ? MyFontEnum.Green : MyFontEnum.Red));
                }

                var rc = def as MyRemoteControlDefinition;
                if(rc != null)
                {
                    SetText(line++, "Power required: " + PowerFormat(rc.RequiredPowerInput) + ", " + ResourcePriority(rc.ResourceSinkGroup));
                }

                return;
            }

            var thrust = def as MyThrustDefinition;
            if(thrust != null)
            {
                if(!thrust.FuelConverter.FuelId.IsNull())
                {
                    SetText(line++, "Requires power to be controlled, " + ResourcePriority(thrust.ResourceSinkGroup));
                    SetText(line++, "Requires fuel: " + thrust.FuelConverter.FuelId.SubtypeId + ", Efficiency: " + Math.Round(thrust.FuelConverter.Efficiency * 100, 2) + "%");
                }
                else
                {
                    SetText(line++, "Power: " + PowerFormat(thrust.MaxPowerConsumption) + ", Idle: " + PowerFormat(thrust.MinPowerConsumption) + ", " + ResourcePriority(thrust.ResourceSinkGroup));
                }

                SetText(line++, "Force: " + ForceFormat(thrust.ForceMagnitude) + ", Dampener factor: " + thrust.SlowdownFactor.ToString(FLOAT_FORMAT));

                if(thrust.EffectivenessAtMinInfluence < 1.0f || thrust.EffectivenessAtMaxInfluence < 1.0f)
                {
                    //if(thrust.NeedsAtmosphereForInfluence) // seems to be a pointless var
                    //{
                    SetText(line++, PercentFormat(thrust.EffectivenessAtMaxInfluence) + " max thrust " + (thrust.MaxPlanetaryInfluence < 1f ? "in " + PercentFormat(thrust.MaxPlanetaryInfluence) + " atmosphere" : "in atmosphere"), thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                    SetText(line++, PercentFormat(thrust.EffectivenessAtMinInfluence) + " max thrust " + (thrust.MinPlanetaryInfluence > 0f ? "below " + PercentFormat(thrust.MinPlanetaryInfluence) + " atmosphere" : "in space"), thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                    //}
                    //else
                    //{
                    //    SetText(line++, PercentFormat(thrust.EffectivenessAtMaxInfluence) + " max thrust " + (thrust.MaxPlanetaryInfluence < 1f ? "in " + PercentFormat(thrust.MaxPlanetaryInfluence) + " planet influence" : "on planets"), thrust.EffectivenessAtMaxInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                    //    SetText(line++, PercentFormat(thrust.EffectivenessAtMinInfluence) + " max thrust " + (thrust.MinPlanetaryInfluence > 0f ? "below " + PercentFormat(thrust.MinPlanetaryInfluence) + " planet influence" : "in space"), thrust.EffectivenessAtMinInfluence < 1f ? MyFontEnum.Red : MyFontEnum.White);
                    //}
                }
                else
                {
                    SetText(line++, "No thrust limits in space or planets", MyFontEnum.Green);
                }

                if(thrust.ConsumptionFactorPerG > 0)
                    SetText(line++, "Extra consumption: +" + PercentFormat(thrust.ConsumptionFactorPerG) + " per natural g acceleration");

                var data = (BlockDataThrust)blockData.GetValueOrDefault(def.Id, null);

                if(data == null)
                {
                    var fakeBlock = SpawnFakeBlock(def) as MyThrust;

                    if(fakeBlock == null)
                    {
                        var error = "Couldn't get block data from fake entity!";
                        Log.Error(error, error);
                    }
                    else
                    {
                        data = new BlockDataThrust(fakeBlock);
                    }
                }

                if(data == null)
                {
                    var error = "Couldn't get block data for: " + def.Id;
                    Log.Error(error, error);
                }
                else
                {
                    var flameDistance = data.distance * Math.Max(1, thrust.SlowdownFactor); // if dampeners are stronger than normal thrust then the flame will be longer... not sure if this scaling is correct though

                    // HACK hardcoded; from MyThrust.DamageGrid() and MyThrust.ThrustDamage()
                    var damage = thrust.FlameDamage * data.flames;
                    var flameShipDamage = damage * 30f;
                    var flameDamage = damage * 10f * data.radius;

                    SetText(line++, (data.flames > 1 ? "Flames: " + data.flames + ", Max distance: " : "Flame max distance: ") + DistanceFormat(flameDistance) + ", Max damage: " + flameShipDamage.ToString(FLOAT_FORMAT) + " to ships, " + flameDamage.ToString(FLOAT_FORMAT) + " to the rest");
                }

                return;
            }

            var lg = def as MyLandingGearDefinition;
            if(lg != null)
            {
                SetText(line++, "Power required*: No", MyFontEnum.Green);
                return;
            }

            var light = def as MyLightingBlockDefinition;
            if(light != null)
            {
                var radius = light.LightRadius;
                var spotlight = def as MyReflectorBlockDefinition;
                if(spotlight != null)
                    radius = light.LightReflectorRadius;

                SetText(line++, "Power required: " + PowerFormat(light.RequiredPowerInput) + ", " + ResourcePriority(light.ResourceSinkGroup));
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

                SetText(line++, "Power required*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(oreDetector.ResourceSinkGroup));
                SetText(line++, "Max range: " + DistanceFormat(oreDetector.MaximumRange));
                return;
            }

            var gyro = def as MyGyroDefinition;
            if(gyro != null)
            {
                SetText(line++, "Power required: " + PowerFormat(gyro.RequiredPowerInput) + ", " + ResourcePriority(gyro.ResourceSinkGroup));
                SetText(line++, "Force: " + ForceFormat(gyro.ForceMagnitude));
                return;
            }

            var projector = def as MyProjectorDefinition;
            if(projector != null)
            {
                SetText(line++, "Power required: " + PowerFormat(projector.RequiredPowerInput) + ", " + ResourcePriority(projector.ResourceSinkGroup));
                return;
            }

            var door = def as MyDoorDefinition;
            if(door != null)
            {
                float requiredPowerInput = MyEnergyConstants.MAX_REQUIRED_POWER_DOOR; // HACK hardcoded; from MyDoor
                float moveTime = (1f / ((MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS / 1000f) * door.OpeningSpeed)) / MyEngineConstants.UPDATE_STEPS_PER_SECOND; // computed after MyDoor.UpdateCurrentOpening()

                SetText(line++, "Power required*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(door.ResourceSinkGroup));
                SetText(line++, "Move time: " + TimeFormat(moveTime) + ", Distance: " + DistanceFormat(door.MaxOpen));
                return;
            }

            var airTightDoor = def as MyAirtightDoorGenericDefinition; // does not extend MyDoorDefinition
            if(airTightDoor != null)
            {
                float moveTime = (1f / ((MyEngineConstants.UPDATE_STEP_SIZE_IN_MILLISECONDS / 1000f) * airTightDoor.OpeningSpeed)) / MyEngineConstants.UPDATE_STEPS_PER_SECOND; // computed after MyDoor.UpdateCurrentOpening()

                SetText(line++, "Power: " + PowerFormat(airTightDoor.PowerConsumptionMoving) + ", Idle: " + PowerFormat(airTightDoor.PowerConsumptionIdle) + ", " + ResourcePriority(airTightDoor.ResourceSinkGroup));
                SetText(line++, "Move time: " + TimeFormat(moveTime));
                return;
            }

            var advDoor = def as MyAdvancedDoorDefinition; // does not extend MyDoorDefinition
            if(advDoor != null)
            {
                SetText(line++, "Power: " + PowerFormat(advDoor.PowerConsumptionMoving) + ", Idle: " + PowerFormat(advDoor.PowerConsumptionIdle) + ", " + ResourcePriority(advDoor.ResourceSinkGroup));

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
                SetText(line++, "Power: " + PowerFormat(production.OperationalPowerConsumption) + ", Idle: " + PowerFormat(production.StandbyPowerConsumption) + ", " + ResourcePriority(production.ResourceSinkGroup));

                var assembler = def as MyAssemblerDefinition;
                if(assembler != null)
                {
                    var mulSpeed = MyAPIGateway.Session.AssemblerSpeedMultiplier;
                    var mulEff = MyAPIGateway.Session.AssemblerEfficiencyMultiplier;

                    SetText(line++, "Assembly speed: " + PercentFormat(assembler.AssemblySpeed * mulSpeed) + MultiplierFormat(mulSpeed) + ", Efficiency: " + PercentFormat(mulEff) + MultiplierFormat(mulEff));
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
                    str.Append("Produces: ");

                    foreach(var gas in oxygenGenerator.ProducedGases)
                    {
                        str.Append(gas.Id.SubtypeName).Append(" (ratio: ").AppendFormat("{0:0.00}", gas.IceToGasRatio).Append("), ");
                    }

                    str.Length -= 2;
                    SetText(line++, str.ToString());
                    str.Clear();
                }

                var volume = (production.InventoryMaxVolume > 0 ? production.InventoryMaxVolume : production.InventorySize.Volume);

                if(refinery != null || assembler != null)
                {
                    SetText(line++, "In+out inventories: " + InventoryFormat(volume * 2, production.InputInventoryConstraint, production.OutputInventoryConstraint));
                }
                else
                {
                    SetText(line++, "Inventory: " + InventoryFormat(volume, production.InputInventoryConstraint));
                }

                if(production.BlueprintClasses != null)
                {
                    if(production.BlueprintClasses.Count == 0)
                    {
                        SetText(line++, "Has no blueprint classes.", MyFontEnum.Red);
                    }
                    else
                    {
                        str.Clear();

                        if(def is MyRefineryDefinition)
                            str.Append("Refines: ");
                        else if(def is MyGasTankDefinition)
                            str.Append("Refills: ");
                        else if(def is MyAssemblerDefinition)
                            str.Append("Builds: ");
                        else
                            str.Append("Blueprints: ");

                        foreach(var bp in production.BlueprintClasses)
                        {
                            str.Append(bp.DisplayNameText).Append(", ");
                        }

                        str.Length -= 2;
                        SetText(line++, str.ToString());
                        str.Clear();
                    }
                }

                return;
            }

            var upgradeModule = def as MyUpgradeModuleDefinition;
            if(upgradeModule != null)
            {
                if(upgradeModule.Upgrades == null || upgradeModule.Upgrades.Length == 0)
                {
                    SetText(line++, "Upgrade: N/A", MyFontEnum.Red);
                }
                else
                {
                    bool singleLine = upgradeModule.Upgrades.Length == 1;

                    if(singleLine)
                    {
                        str.Clear();
                        str.Append("Upgrade: ");
                    }
                    else
                        SetText(line++, "Upgrades:");

                    foreach(var upgrade in upgradeModule.Upgrades)
                    {
                        if(!singleLine)
                            str.Append("      ");

                        str.Append(upgrade.UpgradeType).Append("; ");

                        switch(upgrade.ModifierType)
                        {
                            case MyUpgradeModifierType.Additive: str.Append("+").Append(upgrade.Modifier).Append(" added"); break;
                            case MyUpgradeModifierType.Multiplicative: str.Append("multiplied by ").Append(upgrade.Modifier); break;
                            default: str.Append(upgrade.Modifier).Append(" (").Append(upgrade.ModifierType).Append(")"); break;
                        }

                        str.Append(" per slot");

                        SetText(line++, str.ToString());

                        if(!singleLine)
                            str.Clear();
                    }
                }

                return;
            }

            var powerProducer = def as MyPowerProducerDefinition;
            if(powerProducer != null)
            {
                SetText(line++, "Power output: " + PowerFormat(powerProducer.MaxPowerOutput) + ", " + ResourcePriority(powerProducer.ResourceSourceGroup));

                var reactor = def as MyReactorDefinition;
                if(reactor != null)
                {
                    if(reactor.FuelDefinition != null)
                    {
                        SetText(line++, "Requires fuel: " + IdTypeSubtypeFormat(reactor.FuelId));
                    }

                    var volume = (reactor.InventoryMaxVolume > 0 ? reactor.InventoryMaxVolume : reactor.InventorySize.Volume);
                    var invLimit = reactor.InventoryConstraint;

                    if(invLimit != null)
                    {
                        SetText(line++, "Inventory: " + InventoryFormat(volume, reactor.InventoryConstraint));
                        SetText(line++, "Inventory items " + (invLimit.IsWhitelist ? "allowed" : "NOT allowed") + ":");

                        foreach(var id in invLimit.ConstrainedIds)
                        {
                            SetText(line++, "       - " + IdTypeSubtypeFormat(id));
                        }

                        foreach(var type in invLimit.ConstrainedTypes)
                        {
                            SetText(line++, "       - All of type: " + IdTypeFormat(type));
                        }
                    }
                    else
                    {
                        SetText(line++, "Inventory: " + InventoryFormat(volume));
                    }
                }

                var battery = def as MyBatteryBlockDefinition;
                if(battery != null)
                {
                    SetText(line++, "Power input: " + PowerFormat(battery.RequiredPowerInput) + (battery.AdaptibleInput ? " (adaptable)" : " (minimum required)") + ", " + ResourcePriority(battery.ResourceSinkGroup), (battery.AdaptibleInput ? MyFontEnum.White : MyFontEnum.Red));
                    SetText(line++, "Power capacity: " + PowerStorageFormat(battery.MaxStoredPower) + ", Pre-charged: " + PowerStorageFormat(battery.MaxStoredPower * battery.InitialStoredPowerRatio) + " (" + Math.Round(battery.InitialStoredPowerRatio * 100, 2) + "%)");
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
                SetText(line++, "Power: " + PowerFormat(oxygenFarm.OperationalPowerConsumption) + ", " + ResourcePriority(oxygenFarm.ResourceSinkGroup));
                SetText(line++, "Produces: " + oxygenFarm.MaxGasOutput.ToString(NUMBER_FORMAT) + " " + oxygenFarm.ProducedGas.SubtypeName + " per second, : " + ResourcePriority(oxygenFarm.ResourceSourceGroup));
                SetText(line++, (oxygenFarm.IsTwoSided ? "Two-sided" : "One-sided"), (oxygenFarm.IsTwoSided ? MyFontEnum.White : MyFontEnum.Red));
                return;
            }

            var vent = def as MyAirVentDefinition;
            if(vent != null)
            {
                SetText(line++, "Idle: " + PowerFormat(vent.StandbyPowerConsumption) + ", Operational: " + PowerFormat(vent.OperationalPowerConsumption) + ", " + ResourcePriority(vent.ResourceSinkGroup));
                SetText(line++, "Output - Rate: " + vent.VentilationCapacityPerSecond.ToString(NUMBER_FORMAT) + " oxy/s, " + ResourcePriority(vent.ResourceSourceGroup));
                return;
            }

            var medicalRoom = def as MyMedicalRoomDefinition;
            if(medicalRoom != null)
            {
                // HACK hardcoded; from MyMedicalRoom
                var requiredPowerInput = MyEnergyConstants.MAX_REQUIRED_POWER_MEDICAL_ROOM;

                SetText(line++, "Power*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(medicalRoom.ResourceSinkGroup));
                SetText(line++, "Respawn: " + (medicalRoom.RespawnAllowed ? "Yes" : "No") + ", " + (medicalRoom.RespawnAllowed && medicalRoom.ForceSuitChangeOnRespawn ? "Forced suit: " + medicalRoom.RespawnSuitName : "Forced suit: (No)"), (medicalRoom.ForceSuitChangeOnRespawn ? MyFontEnum.Blue : (!medicalRoom.RespawnAllowed ? MyFontEnum.Red : MyFontEnum.White)));
                SetText(line++, "Healing: " + (medicalRoom.HealingAllowed ? "Yes" : "No"), (medicalRoom.HealingAllowed ? MyFontEnum.White : MyFontEnum.Red));
                SetText(line++, "Recharge: " + (medicalRoom.RefuelAllowed ? "Yes" : "No"), (medicalRoom.RefuelAllowed ? MyFontEnum.White : MyFontEnum.Red));
                SetText(line++, "Suit change: " + (medicalRoom.SuitChangeAllowed ? "Yes" : "No"));

                if(medicalRoom.CustomWardrobesEnabled && medicalRoom.CustomWardrobeNames != null && medicalRoom.CustomWardrobeNames.Count > 0)
                {
                    SetText(line++, "Usable suits:", MyFontEnum.Blue);

                    foreach(var charName in medicalRoom.CustomWardrobeNames)
                    {
                        MyCharacterDefinition charDef;
                        if(!MyDefinitionManager.Static.Characters.TryGetValue(charName, out charDef))
                            SetText(line++, "    " + charName + " (not found in definitions)", MyFontEnum.Red);
                        else
                            SetText(line++, "    " + charDef.DisplayNameText);
                    }
                }
                else
                    SetText(line++, "Usable suits: (all)");
            }

            var radioAntenna = def as MyRadioAntennaDefinition;
            if(radioAntenna != null)
            {
                // HACK hardcoded; from MyRadioAntenna
                float maxRadius = (def.CubeSize == MyCubeSize.Large ? 50000f : 5000f);
                float requiredPowerInput = (maxRadius / 500f) * 0.002f;

                SetText(line++, "Max required power*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(radioAntenna.ResourceSinkGroup));
                SetText(line++, "Max radius*: " + DistanceFormat(maxRadius));
                return;
            }

            var laserAntenna = def as MyLaserAntennaDefinition;
            if(laserAntenna != null)
            {
                SetText(line++, "Power: " + PowerFormat(laserAntenna.PowerInputLasing) + ", Turning: " + PowerFormat(laserAntenna.PowerInputTurning) + ", Idle: " + PowerFormat(laserAntenna.PowerInputIdle) + ", " + ResourcePriority(laserAntenna.ResourceSinkGroup));
                SetText(line++, "Range: " + DistanceFormat(laserAntenna.MaxRange) + ", Line-of-sight: " + (laserAntenna.RequireLineOfSight ? "Required" : "Not required"), (laserAntenna.RequireLineOfSight ? MyFontEnum.White : MyFontEnum.Green));
                SetText(line++, "Rotation Pitch: " + AngleFormatDeg(laserAntenna.MinElevationDegrees) + " to " + AngleFormatDeg(laserAntenna.MaxElevationDegrees) + ", Yaw: " + AngleFormatDeg(laserAntenna.MinAzimuthDegrees) + " to " + AngleFormatDeg(laserAntenna.MaxAzimuthDegrees));
                SetText(line++, "Rotation Speed: " + RotationSpeed(laserAntenna.RotationRate * 60));
                return;
            }

            var beacon = def as MyBeaconDefinition;
            if(beacon != null)
            {
                // HACK hardcoded; from MyBeacon
                float maxRadius = (def.CubeSize == MyCubeSize.Large ? 50000f : 5000f);
                float requiredPowerInput = (maxRadius / 100000f) * 0.02f;

                SetText(line++, "Max required power*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(beacon.ResourceSinkGroup));
                SetText(line++, "Max radius*: " + DistanceFormat(maxRadius));
                return;
            }

            var timer = def as MyTimerBlockDefinition;
            if(timer != null)
            {
                // HACK hardcoded; from MyTimerBlock
                float requiredPowerInput = 1E-07f;

                SetText(line++, "Power required*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(timer.ResourceSinkGroup));
                return;
            }

            var pb = def as MyProgrammableBlockDefinition;
            if(pb != null)
            {
                // HACK hardcoded; from MyProgrammableBlock
                float requiredPowerInput = 0.0005f;

                SetText(line++, "Power required*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(pb.ResourceSinkGroup));
                return;
            }

            var sound = def as MySoundBlockDefinition;
            if(sound != null)
            {
                // HACK hardcoded; from MySoundBlock
                float requiredPowerInput = 0.0002f;

                SetText(line++, "Power required*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(sound.ResourceSinkGroup));
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

                SetText(line++, "Max required power*: " + PowerFormat(requiredPower) + ", " + ResourcePriority(sensor.ResourceSinkGroup));
                SetText(line++, "Max area: " + VectorFormat(maxField));
                return;
            }

            var artificialMass = def as MyVirtualMassDefinition;
            if(artificialMass != null)
            {
                SetText(line++, "Power required: " + PowerFormat(artificialMass.RequiredPowerInput) + ", " + ResourcePriority(artificialMass.ResourceSinkGroup));
                SetText(line++, "Artificial mass: " + MassFormat(artificialMass.VirtualMass));
                return;
            }

            var spaceBall = def as MySpaceBallDefinition; // this doesn't extend MyVirtualMassDefinition
            if(spaceBall != null)
            {
                SetText(line++, "Power required*: No", MyFontEnum.Green); // HACK hardcoded
                SetText(line++, "Max artificial mass: " + MassFormat(spaceBall.MaxVirtualMass));
                return;
            }

            var warhead = def as MyWarheadDefinition;
            if(warhead != null)
            {
                SetText(line++, "Power required*: No", MyFontEnum.Green); // HACK hardcoded
                SetText(line++, "Radius: " + DistanceFormat(warhead.ExplosionRadius));
                SetText(line++, "Damage: " + warhead.WarheadExplosionDamage.ToString("#,###,###,###,###.##"));
                return;
            }

            var button = def as MyButtonPanelDefinition;
            if(button != null)
            {
                // HACK hardcoded; from MyButtonPanel
                float requiredPowerInput = 0.0001f;

                SetText(line++, "Power required*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(button.ResourceSinkGroup));
                SetText(line++, "Button count: " + button.ButtonCount);
                return;
            }

            var lcd = def as MyTextPanelDefinition;
            if(lcd != null)
            {
                SetText(line++, "Power required: " + PowerFormat(lcd.RequiredPowerInput) + ", " + ResourcePriority(lcd.ResourceSinkGroup));
                SetText(line++, "Screen resolution: " + (lcd.TextureResolution * lcd.TextureAspectRadio) + "x" + lcd.TextureResolution);
                return;
            }

            var camera = def as MyCameraBlockDefinition;
            if(camera != null)
            {
                SetText(line++, "Power required: " + PowerFormat(camera.RequiredPowerInput) + ", " + ResourcePriority(camera.ResourceSinkGroup));
                SetText(line++, "Field of view: " + AngleFormat(camera.MinFov) + " to " + AngleFormat(camera.MaxFov));

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
                    SetText(line++, "Power required: " + PowerFormat(poweredCargo.RequiredPowerInput) + ", " + ResourcePriority(poweredCargo.ResourceSinkGroup));
                }

                float volume = cargo.InventorySize.Volume;

                if(Math.Abs(volume) > 0.0001f || GetInventoryFromComponent(def, out volume))
                {
                    SetText(line++, "Inventory: " + InventoryFormat(volume));
                }
                else
                {
                    // HACK hardcoded; from MyCargoContainer
                    var gridSize = MyDefinitionManager.Static.GetCubeSize(def.CubeSize);
                    volume = (float)def.Size.X * gridSize * (float)def.Size.Y * gridSize * (float)def.Size.Z * gridSize;

                    SetText(line++, "Inventory*: " + InventoryFormat(volume));
                }

                return;
            }

            var sorter = def as MyConveyorSorterDefinition;
            if(sorter != null)
            {
                SetText(line++, "Power required: " + PowerFormat(sorter.PowerInput) + ", " + ResourcePriority(sorter.ResourceSinkGroup));
                SetText(line++, "Inventory: " + InventoryFormat(sorter.InventorySize.Volume));
                return;
            }

            var gravity = def as MyGravityGeneratorBaseDefinition;
            if(gravity != null)
            {
                var gravityFlat = def as MyGravityGeneratorDefinition;
                if(gravityFlat != null)
                {
                    SetText(line++, "Max power use: " + PowerFormat(gravityFlat.RequiredPowerInput) + ", " + ResourcePriority(gravityFlat.ResourceSinkGroup));
                    SetText(line++, "Field size: " + VectorFormat(gravityFlat.MinFieldSize) + " to " + VectorFormat(gravityFlat.MaxFieldSize));
                }

                var gravitySphere = def as MyGravityGeneratorSphereDefinition;
                if(gravitySphere != null)
                {
                    SetText(line++, "Base power usage: " + PowerFormat(gravitySphere.BasePowerInput) + ", Consumption: " + PowerFormat(gravitySphere.ConsumptionPower) + ", " + ResourcePriority(gravitySphere.ResourceSinkGroup));
                    SetText(line++, "Radius: " + DistanceFormat(gravitySphere.MinRadius) + " to " + DistanceFormat(gravitySphere.MaxRadius));
                }

                SetText(line++, "Acceleration: " + ForceFormat(gravity.MinGravityAcceleration) + " to " + ForceFormat(gravity.MaxGravityAcceleration));
                return;
            }

            var jumpDrive = def as MyJumpDriveDefinition;
            if(jumpDrive != null)
            {
                SetText(line++, "Power required: " + PowerFormat(jumpDrive.RequiredPowerInput) + ", For jump: " + PowerFormat(jumpDrive.PowerNeededForJump) + ", " + ResourcePriority(jumpDrive.ResourceSinkGroup));
                SetText(line++, "Max distance: " + DistanceFormat((float)jumpDrive.MaxJumpDistance));
                SetText(line++, "Max mass: " + MassFormat((float)jumpDrive.MaxJumpMass));
                SetText(line++, "Jump delay: " + TimeFormat(jumpDrive.JumpDelay));
                return;
            }

            var merger = def as MyMergeBlockDefinition;
            if(merger != null)
            {
                SetText(line++, "Power required*: No", MyFontEnum.Green);
                SetText(line++, "Pull strength: " + merger.Strength.ToString("###,###,###.0######"));
                return;
            }

            var weapon = def as MyWeaponBlockDefinition;
            if(weapon != null)
            {
                var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);

                float requiredPowerInput = -1;

                if(def is MyLargeTurretBaseDefinition)
                {
                    requiredPowerInput = MyEnergyConstants.MAX_REQUIRED_POWER_TURRET; // HACK hardcoded; from MyLargeTurretBase
                }
                else
                {
                    if(defTypeId == typeof(MyObjectBuilder_SmallGatlingGun)
                    || defTypeId == typeof(MyObjectBuilder_SmallMissileLauncher)
                    || defTypeId == typeof(MyObjectBuilder_SmallMissileLauncherReload))
                        requiredPowerInput = MyEnergyConstants.MAX_REQUIRED_POWER_SHIP_GUN; // HACK hardcoded; from MySmallMissileLauncher & MySmallGatlingGun
                }

                if(requiredPowerInput > 0)
                    SetText(line++, "Power required*: " + PowerFormat(requiredPowerInput) + ", " + ResourcePriority(weapon.ResourceSinkGroup));
                else
                    SetText(line++, "Power priority: " + ResourcePriority(weapon.ResourceSinkGroup));

                SetText(line++, "Inventory: " + InventoryFormat(weapon.InventoryMaxVolume, wepDef.AmmoMagazinesId));

                var largeTurret = def as MyLargeTurretBaseDefinition;
                if(largeTurret != null)
                {
                    SetText(line++, "Auto-target: " + (largeTurret.AiEnabled ? "Yes" : "No") + (largeTurret.IdleRotation ? " (With idle rotation)" : "(No idle rotation)") + ", Range: " + DistanceFormat(largeTurret.MaxRangeMeters));
                    SetText(line++, "Speed - Pitch: " + RotationSpeed(largeTurret.ElevationSpeed * 60) + ", Yaw: " + RotationSpeed(largeTurret.RotationSpeed * 60));
                    SetText(line++, "Rotation - Pitch: " + AngleFormatDeg(largeTurret.MinElevationDegrees) + " to " + AngleFormatDeg(largeTurret.MaxElevationDegrees) + ", Yaw: " + AngleFormatDeg(largeTurret.MinAzimuthDegrees) + " to " + AngleFormatDeg(largeTurret.MaxAzimuthDegrees));
                }

                SetText(line++, "Reload time: " + TimeFormat(wepDef.ReloadTime / 1000));

                str.Clear().Append("Ammo: ");
                for(int i = 0; i < wepDef.AmmoMagazinesId.Length; i++)
                {
                    var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[i]);
                    var ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
                    var weaponData = wepDef.WeaponAmmoDatas[(int)ammo.AmmoType];

                    if(i > 0 && i % 2 == 0)
                    {
                        SetText(line++, str.ToString());
                        str.Clear().Append("       ");
                    }

                    str.Append(mag.Id.SubtypeName).Append(" (").Append(weaponData.RateOfFire).Append(" RPM)").Append(", ");
                }

                str.Length -= 2;
                SetText(line++, str.ToString());
                str.Clear();
                return;
            }
        }

        private string ResourcePriority(string groupName) // HACK some ResourceSinkGroup are string type for SOME reason
        {
            return ResourcePriority(MyStringHash.GetOrCompute(groupName));
        }

        private string ResourcePriority(MyStringHash groupId, bool hardcoded = false)
        {
            str2.Clear();
            str2.Append("Priority");
            if(hardcoded)
                str2.Append("*");
            str2.Append(": ");
            ResourceGroupData data;

            if(groupId == null || !resourceGroupPriority.TryGetValue(groupId, out data))
                str2.Append("(Undefined)");
            else
                str2.Append(groupId.String).Append(" (" + data.priority + "/" + (data.def.IsSource ? resourceSourceGroups : resourceSinkGroups) + ")");

            return str2.ToString();
        }

        private string ForceFormat(float N)
        {
            if(N > 1000000)
                return (N / 1000000).ToString(FLOAT_FORMAT) + " MN";

            if(N > 1000)
                return (N / 1000).ToString(FLOAT_FORMAT) + " kN";

            return N.ToString(FLOAT_FORMAT) + " N";
        }

        private string RotationSpeed(float radPerSecond)
        {
            return Math.Round(MathHelper.ToDegrees(radPerSecond), 2) + "°/s";
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

        private bool GetInventoryFromComponent(MyDefinitionBase def, out float volume)
        {
            var comps = MyDefinitionManager.Static.GetEntityComponentDefinitions();
            volume = 0;

            foreach(var comp in comps)
            {
                var invComp = comp as MyInventoryComponentDefinition;

                if(invComp != null && invComp.Id.SubtypeId == def.Id.SubtypeId)
                {
                    volume = invComp.Volume;
                    return true;
                }
            }

            return false;
        }

        private string InventoryFormat(float volume, MyInventoryConstraint inputConstraint, MyInventoryConstraint outputConstraint)
        {
            var types = new HashSet<MyObjectBuilderType>(inputConstraint.ConstrainedTypes);
            types.UnionWith(outputConstraint.ConstrainedTypes);

            var items = new HashSet<MyDefinitionId>(inputConstraint.ConstrainedIds);
            items.UnionWith(outputConstraint.ConstrainedIds);

            return InventoryFormat(volume, types: types, items: items, isWhitelist: inputConstraint.IsWhitelist); // HACK only using input constraint's whitelist status, not sure if output inventory's whitelist is needed
        }

        private string InventoryFormat(float volume, MyInventoryConstraint inventoryConstraint)
        {
            return InventoryFormat(volume,
                types: new HashSet<MyObjectBuilderType>(inventoryConstraint.ConstrainedTypes),
                items: new HashSet<MyDefinitionId>(inventoryConstraint.ConstrainedIds),
                isWhitelist: inventoryConstraint.IsWhitelist);
        }

        private string InventoryFormat(float volume, params MyObjectBuilderType[] allowedTypesParams)
        {
            return InventoryFormat(volume, types: new HashSet<MyObjectBuilderType>(allowedTypesParams));
        }

        private string InventoryFormat(float volume, params MyDefinitionId[] allowedItems)
        {
            return InventoryFormat(volume, items: new HashSet<MyDefinitionId>(allowedItems));
        }

        private string InventoryFormat(float volume, HashSet<MyObjectBuilderType> types = null, HashSet<MyDefinitionId> items = null, bool isWhitelist = true)
        {
            var mul = MyAPIGateway.Session.InventoryMultiplier;
            str2.Clear();

            MyValueFormatter.AppendVolumeInBestUnit(volume * mul, str2);

            if(Math.Abs(mul - 1) > 0.001f)
                str2.Append(" (x").Append(Math.Round(mul, 2)).Append(")");

            if(types == null && items == null)
                types = DEFAULT_ALLOWED_TYPES;

            var physicalItems = MyDefinitionManager.Static.GetPhysicalItemDefinitions();
            var minMass = float.MaxValue;
            var maxMass = 0f;

            foreach(var item in physicalItems)
            {
                if(!item.Public || item.Mass <= 0 || item.Volume <= 0)
                    continue; // skip hidden and physically impossible items

                if((types != null && isWhitelist == types.Contains(item.Id.TypeId)) || (items != null && isWhitelist == items.Contains(item.Id)))
                {
                    var fillMass = item.Mass * (volume / item.Volume);
                    minMass = Math.Min(fillMass, minMass);
                    maxMass = Math.Max(fillMass, maxMass);
                }
            }

            if(minMass != float.MaxValue && maxMass != 0)
            {
                if(Math.Abs(minMass - maxMass) > 0.00001f)
                    str2.Append(", Cargo mass: ").Append(MassFormat(minMass)).Append(" to ").Append(MassFormat(maxMass));
                else
                    str2.Append(", Max cargo mass: ").Append(MassFormat(minMass));
            }

            return str2.ToString();
        }

        private static readonly HashSet<MyObjectBuilderType> DEFAULT_ALLOWED_TYPES = new HashSet<MyObjectBuilderType>()
        {
            typeof(MyObjectBuilder_Ore),
            typeof(MyObjectBuilder_Ingot),
            typeof(MyObjectBuilder_Component)
        };

        private string TimeFormat(float seconds)
        {
            //if(seconds > 60)
            //    return String.Format("{0:0}m {1:0.##}s", (seconds / 60), (seconds % 60));
            //else
            return String.Format("{0:0.##}s", seconds);
        }

        private string AngleFormat(float radians)
        {
            return AngleFormatDeg(MathHelper.ToDegrees(radians));
        }

        private string AngleFormatDeg(float degrees)
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

        private string IdTypeFormat(MyObjectBuilderType type)
        {
            var typeName = type.ToString();
            return typeName.Substring(typeName.IndexOf('_') + 1);
        }

        private string IdTypeSubtypeFormat(MyDefinitionId id)
        {
            return IdTypeFormat(id.TypeId) + "/" + id.SubtypeName;
        }

        private void SetText(int line, string text, string font = MyFontEnum.White, int aliveTime = 100)
        {
            if(hudLines == null)
                hudLines = new List<IMyHudNotification>();

            if(line >= hudLines.Count)
                hudLines.Add(MyAPIGateway.Utilities.CreateNotification(""));

            if(!textAPIenabled)
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
                showMenu = true;
            }
        }

        private static MyCubeBlock SpawnFakeBlock(MyCubeBlockDefinition def)
        {
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var spawnPos = camMatrix.Translation + camMatrix.Backward * 10;
            var fakeGridObj = new MyObjectBuilder_CubeGrid()
            {
                CreatePhysics = false,
                PersistentFlags = MyPersistentEntityFlags2.None,
                IsStatic = true,
                GridSizeEnum = def.CubeSize,
                Editable = false,
                DestructibleBlocks = false,
                IsRespawnGrid = false,
                PositionAndOrientation = new MyPositionAndOrientation(spawnPos, Vector3.Forward, Vector3.Up),
                CubeBlocks = new List<MyObjectBuilder_CubeBlock>()
                {
                    new MyObjectBuilder_CubeBlock()
                    {
                        SubtypeName = def.Id.SubtypeName,
                    }
                },
            };

            MyEntities.RemapObjectBuilder(fakeGridObj);
            var fakeEnt = MyEntities.CreateFromObjectBuilderNoinit(fakeGridObj);
            fakeEnt.IsPreview = true;
            fakeEnt.Save = false;
            fakeEnt.Render.Visible = false;
            fakeEnt.Flags = EntityFlags.None;
            MyEntities.InitEntity(fakeGridObj, ref fakeEnt);

            var fakeGrid = (IMyCubeGrid)fakeEnt;
            var fakeBlock = fakeGrid.GetCubeBlock(Vector3I.Zero)?.FatBlock as MyCubeBlock;
            fakeGrid.Close();
            return fakeBlock;
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), useEntityUpdate: true)]
    public class ThrustBlock : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            try
            {
                var block = (MyThrust)Entity;

                if(!BuildInfo.blockData.ContainsKey(block.BlockDefinition.Id) && ((IMyModel)block.Model).AssetName == block.BlockDefinition.Model)
                {
                    new BuildInfo.BlockDataThrust(block);
                }

                block.Components.Remove<ThrustBlock>();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}