using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.GUI.Elements;
using Digi.BuildInfo.Features.MultiTool.Instruments;
using Digi.BuildInfo.Features.MultiTool.Instruments.Measure;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.Input;
using Digi.Input.Devices;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static Digi.BuildInfo.Systems.TextAPI;

namespace Digi.BuildInfo.Features.MultiTool
{
    public class MultiToolHandler : ModComponent
    {
        const bool DebugMode = false;
        const bool AllowInShips = false; // TODO validate and maybe allow?

        public bool IsEquipped { get; private set; }
        public InputGameControl ControlPrimary { get; private set; }
        public InputGameControl ControlSecondary { get; private set; }
        public InputGameControl ControlReload { get; private set; }
        public InputGameControl ControlSymmetry { get; private set; }
        public InputGameControl ControlSymmetrySwitch { get; private set; }
        public InputGameControl ControlAlignDefault { get; private set; }

        readonly List<InstrumentBase> Instruments = new List<InstrumentBase>();
        int InstrumentIdx = 0;
        public InstrumentBase Instrument { get; private set; }

        TextPackage ListText;
        CornerBackground ListBox;

        TextPackage InstrumentText;
        CornerBackground InstrumentBox;
        HudAPIv2.BillBoardHUDMessage InstrumentIcon;

        float LineHeight;
        float SpaceWidth;

        public bool IsUIVisible { get; private set; }
        float GUIScale;
        Vector2D HUDPosition = new Vector2D(0.98, 0.448);
        bool ForceRefresh;

        /// <summary>
        /// Only for tracking equipped changes
        /// </summary>
        MyCubeBlockDefinition EquippedDef;

        bool PreviousAlignDefault;

        MyCubeBlockDefinitionGroup MultiToolPair;

        /// <summary>
        /// For dynamic description
        /// </summary>
        MyCubeBlockDefinition FakeDef = new MyCubeBlockDefinition();

        readonly string[] Icons = new string[1];
        public const string BlockPairName = "MultiTool";

        const int BigIconSizePx = 64;

        const int MaxVisibleInstruments = 7;

        public const string WarningIcon = @"Textures\GUI\Icons\HUD 2017\Notification_badge.png";
        public const string MissingIcon = @"Textures\GUI\Icons\Help.dds";

        Color ColorHeader = new Color(55, 200, 255);
        Color ColorSelectedInstrument = new Color(125, 255, 175);
        //Color ColorBind = new Color(200, 255, 230);
        Color ColorBackground = Constants.Color_UIBackground;

        public MultiToolHandler(BuildInfoMod main) : base(main)
        {
            MultiToolPair = MyDefinitionManager.Static.GetDefinitionGroup(BlockPairName);

            if(MultiToolPair != null && MultiToolPair.Any != null)
            {
                ConfigureToolBlock(MultiToolPair.Small);
                ConfigureToolBlock(MultiToolPair.Large);

                var cats = MyDefinitionManager.Static.GetCategories();
                ConfigureCategory(cats.GetValueOrDefault("Section0_Position1_CharacterItems"));
                ConfigureCategory(cats.GetValueOrDefault("Section0_Position2_CharacterTools"));

                MyVisualScriptLogicProvider.BlockBuilt += BlockBuilt;
            }
        }

        public override void RegisterComponent()
        {
            if(MultiToolPair == null || MultiToolPair.Any == null)
                return;

            ControlPrimary = InputLib.GetInput(ControlContext.CHARACTER, ControlIds.PRIMARY_TOOL_ACTION);
            ControlSecondary = InputLib.GetInput(ControlContext.CHARACTER, ControlIds.SECONDARY_TOOL_ACTION);
            ControlReload = InputLib.GetInput(ControlContext.CHARACTER, ControlIds.RELOAD);
            ControlSymmetry = InputLib.GetInput(ControlContext.CHARACTER, ControlIds.USE_SYMMETRY);
            ControlSymmetrySwitch = InputLib.GetInput(ControlContext.CHARACTER, ControlIds.SYMMETRY_SWITCH);
            ControlAlignDefault = InputLib.GetInput(ControlContext.CHARACTER, ControlIds.CUBE_DEFAULT_MOUNTPOINT);

            Main.TextAPI.Detected += CreateUI;
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;

            try
            {
                RegisterInstrument(new Measure());
                RegisterInstrument(new PhysicsSnapshot());
                RegisterInstrument(new ConveyorVis());
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UnregisterComponent()
        {
            MyVisualScriptLogicProvider.BlockBuilt -= BlockBuilt;

            if(!Main.ComponentsRegistered)
                return;

            Main.TextAPI.Detected -= CreateUI;
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;

            foreach(InstrumentBase instrument in Instruments)
            {
                instrument.Dispose();
            }
        }

        // HACK: maybe can catch some new ones...
        void BlockBuilt(string typeId, string subtypeId, string gridName, long blockId)
        {
            try
            {
                if(typeId == nameof(MyObjectBuilder_CubeBlock) && subtypeId != null && subtypeId.StartsWith("BuildInfo_MultiTool"))
                {
                    var block = MyEntities.GetEntityById(blockId) as MyCubeBlock;
                    var grid = MyEntities.GetEntityByName(gridName) as MyCubeGrid;

                    string gridInfo = (grid != null ? $"'{grid.DisplayName}' ({grid.EntityId})" : "(grid not found!)");

                    if(block != null)
                    {
                        if(Vector3D.DistanceSquared(MyAPIGateway.Session.Camera.Position, block.PositionComp.GetPosition()) < 100 * 100)
                        {
                            Log.Error($"Multitool's block was able to be placed nearby, how?! please report the way to reproduce! grid: {gridInfo}");
                        }
                    }
                    else
                    {
                        Log.Error($"Multitool's block was able to be placed somewhere, how?! please report the way to reproduce! grid: {gridInfo} (block was not retrievable by id)");
                    }

                    Checker.Check(MyDefinitionManager.Static.GetCubeBlockDefinition(new MyDefinitionId(typeof(MyObjectBuilder_CubeBlock), subtypeId)));
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        // HACK: maybe some mod/plugin is setting blocks' IsStandAlone to true again?
        [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
        class Checker : MySessionComponentBase // session comp to run on DS too to detect this...
        {
            public override void BeforeStart()
            {
                try
                {
                    MyCubeBlockDefinitionGroup pair = MyDefinitionManager.Static.GetDefinitionGroup(BlockPairName);
                    if(pair != null && pair.Any != null)
                    {
                        Check(pair.Small);
                        Check(pair.Large);
                    }
                }
                catch(Exception e)
                {
                    MyLog.Default.WriteLine(e);
                }
            }

            public static void Check(MyCubeBlockDefinition def)
            {
                if(def == null)
                    return;

                if(def.IsStandAlone)
                {
                    def.IsStandAlone = false;

                    string msg = $"MultiTool's {def.Id.SubtypeName} was forced IsStandAlone=true by something!";
                    MyLog.Default.Error($"{BuildInfoMod.ModName} mod: {msg}");
                    MyLog.Default.WriteLineToConsole($"{BuildInfoMod.ModName} mod: {msg}");

                    if(MyAPIGateway.Session?.Player != null)
                    {
                        //MyAPIGateway.Utilities.ShowNotification(msg, 10000, MyFontEnum.Red);
                        MyAPIGateway.Utilities.ShowMessage(BuildInfoMod.ModName, $"ERROR: {msg}");
                    }
                }
            }
        }

        void ConfigureToolBlock(MyCubeBlockDefinition def)
        {
            if(def == null)
                return;

            def.GuiVisible = false;
            def.Public = true;
            def.DescriptionEnum = null;
            def.DescriptionArgs = null;
            def.DescriptionString = "A tool that hosts various instruments";
        }

        void ConfigureCategory(MyGuiBlockCategoryDefinition catDef)
        {
            if(catDef == null)
                return;

            if(MultiToolPair?.AnyPublic != null)
            {
                catDef.SearchBlocks = true;
                catDef.ItemIds.Add(MultiToolPair.AnyPublic.Id.ToString());
            }
        }

        #region UI Handling
        internal void GenerateIcons(HudAPIv2.FontDefinition font)
        {
            const int StartingChar = FontsHandler.MultiToolIconStart;
            const int offset = 6; // HACK: hardcoded offset in textAPI which is there to fix something else
            const int materialSizeX = 32; // HACK: texture is actually 128x but doing both smaller would rescale it
            const int iconSize = 32;
            const int iconAw = 32;
            const int iconLsb = 0;

            for(int i = 0; i < Instruments.Count; i++)
            {
                InstrumentBase instrument = Instruments[i];

                char c = (char)(StartingChar + i);
                string charCode = ((int)c).ToString("X");

                int x = 0;
                int y = 0;
                int sizeX = iconSize;
                int sizeY = iconSize;

                instrument.IconChar = c;

                font.AddCharacter(c, instrument.BillboardIcon, materialSizeX, charCode, x, y - offset, sizeX, sizeY + offset, iconAw, iconLsb, forcewhite: false);
            }
        }

        void CreateUI()
        {
            Utils.FadeColorHUD(ref ColorBackground, 0.75f);

            ListBox = new CornerBackground(color: ColorBackground, corners: CornerFlag.TopRight);
            InstrumentBox = new CornerBackground(color: ColorBackground, corners: CornerFlag.BottomRight);

            InstrumentIcon = new HudAPIv2.BillBoardHUDMessage(Constants.MatUI_Square, Vector2D.Zero, Color.White);
            InstrumentIcon.Visible = false;
            InstrumentIcon.SkipLinearRGB = true;

            ListText = new TextPackage(512);
            ListText.Font = FontsHandler.TextAPI_OutlinedFont;

            ListText.TextStringBuilder.Append(" ");
            Vector2D textSize = ListText.Text.GetTextLength();
            LineHeight = (float)Math.Abs(textSize.Y);
            SpaceWidth = (float)Math.Abs(textSize.X);

            InstrumentText = new TextPackage(512);
            InstrumentText.Font = FontsHandler.TextAPI_OutlinedFont;

            UpdateUIProperties();
        }

        void UpdateUIProperties()
        {
            GUIScale = 1f;
            // where to use MyAPIGateway.Input.GetMouseAreaSize().Y / 1080f; ...

            ListText.TextStringBuilder.TrimEndWhitespace();
            InstrumentText.TextStringBuilder.TrimEndWhitespace();

            ListText.Scale = GUIScale;
            InstrumentText.Scale = GUIScale;

            Vector2D px = HudAPIv2.APIinfo.ScreenPositionOnePX;
            Vector2D padding = (new Vector2D(12, -12) * GUIScale * px);

            Vector2D bottomPos;

            {
                Vector2D boxSize = ListText.Text.GetTextLength();
                boxSize += padding * 2;
                boxSize = new Vector2D(Math.Abs(boxSize.X), Math.Abs(boxSize.Y));
                boxSize = Vector2D.Max(boxSize, px * new Vector2(324, 80) * GUIScale); // min box size

                Vector2D pos = HUDPosition + new Vector2D(-boxSize.X, boxSize.Y); // bottom-right pivot

                ListText.Position = pos + padding;

                Vector2 posPx = (Vector2)Main.DrawUtils.TextAPIHUDToPixels(pos);

                Vector2 boxSizePx = (Vector2)(boxSize / px);

                bottomPos = HUDPosition;

                ListBox.SetProperties(posPx, boxSizePx, new CornerBackground.CornerSize(24 * GUIScale));
            }

            bool showInstrumentsBox = Main.GameConfig.HudState == HudState.BASIC;

            if(showInstrumentsBox)
            {
                Vector2D boxSize = InstrumentText.Text.GetTextLength();
                boxSize += padding * 2;
                boxSize = new Vector2D(Math.Abs(boxSize.X), Math.Abs(boxSize.Y));
                boxSize = Vector2D.Max(boxSize, px * new Vector2(324, 200) * GUIScale); // min box size

                Vector2D pos = bottomPos + new Vector2D(0, -(px.Y * 4 * GUIScale)) + new Vector2D(-boxSize.X, 0); // top-right pivot

                InstrumentText.Position = pos + padding + new Vector2D(0, -(px.Y * 8 * GUIScale));

                Vector2 posPx = (Vector2)Main.DrawUtils.TextAPIHUDToPixels(pos);

                Vector2 boxSizePx = (Vector2)(boxSize / px);

                InstrumentBox.SetProperties(posPx, boxSizePx, new CornerBackground.CornerSize(24 * GUIScale));

                Vector2 iconSize = (Vector2)(new Vector2D(BigIconSizePx * GUIScale) * px);
                InstrumentIcon.Width = iconSize.X;
                InstrumentIcon.Height = iconSize.Y;
                InstrumentIcon.Origin = pos - new Vector2D(iconSize.X * -0.5, iconSize.Y * 0.5) + padding * 0.5;
            }

            //if(InstrumentText.Visible != showInstrumentsBox)
            //{
            //    InstrumentText.Visible = showInstrumentsBox;
            //    InstrumentBox.SetVisible(showInstrumentsBox);
            //    InstrumentIcon.Visible = showInstrumentsBox;
            //}
        }

        void SetUIVisible(bool visible)
        {
            if(DebugMode)
                DebugLog.PrintHUD(this, $"{nameof(SetUIVisible)}={visible}", log: true);

            if(ListBox == null || IsUIVisible == visible)
                return;

            IsUIVisible = visible;

            //ListBox.SetVisible(visible);
            //ListText.Visible = visible;
            //
            //if(!visible)
            //{
            //    InstrumentText.Visible = visible;
            //    InstrumentBox.SetVisible(visible);
            //    InstrumentIcon.Visible = visible;
            //}
        }
        #endregion

        #region Tool handling
        void RegisterInstrument(InstrumentBase instrument)
        {
            Instruments.Add(instrument);
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            if(slimBlock == null && def != null && def.BlockPairName == BlockPairName)
            {
                // only turn it on, because this event gets called when the block deactivation is triggered too
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
                CheckEquipped();
            }
        }

        void CheckEquipped()
        {
            MyCubeBlockDefinition def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
            if(def != EquippedDef)
            {
                EquippedDef = def;

                bool isMultiTool = def?.BlockPairName == BlockPairName;

                if(DebugMode)
                    DebugLog.PrintHUD(this, $"equipped def changed: {def?.Id.ToString() ?? "null"}; isMultiTool={isMultiTool}", log: true);

                if(IsEquipped != isMultiTool)
                {
                    if(isMultiTool)
                    {
                        DeactivateCubeBuilder();
                        Equip();
                        RefreshBlockInfo();
                    }
                    else
                    {
                        Holster();
                        SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                    }
                }
            }
        }

        void DeactivateCubeBuilder()
        {
            // HACK: trick to keep cubebuilder equipped to prevent click from doing interaction
            MyCubeBuilder.Static.DeactivateBlockCreation();

            // HACK: to prevent "You need <Material> to build <ThisTool>"
            MyCubeBuilder.Static.ToolType = MyCubeBuilderToolType.ColorTool;
        }

        public override void UpdateAfterSim(int tick)
        {
            CheckEquipped();

            if(!IsEquipped)
                return;

            // re-enables itself on pressing R
            if(MyCubeBuilder.Static.BlockCreationIsActivated)
            {
                DeactivateCubeBuilder();
            }

            if(Instruments.Count <= 0)
                return;

            bool shouldBeVisible = !MyAPIGateway.Gui.IsCursorVisible && Main.GameConfig.HudState != HudState.OFF;

            if(!AllowInShips && Main.EquipmentMonitor.IsCockpitBuildMode)
            {
                shouldBeVisible = false;

                if(ForceRefresh)
                {
                    RefreshBlockInfo();
                }

                //MyAPIGateway.Utilities.ShowNotification("MultiTool not available for ships", 16, FontsHandler.RedSh);
            }

            if(shouldBeVisible != IsUIVisible)
            {
                SetUIVisible(shouldBeVisible);
            }

            if(!IsUIVisible)
                return;

            bool inputReadable = InputLib.IsInputReadable();
            if(inputReadable)
                HandleInputs();

            if(ForceRefresh || tick % 30 == 0)
            {
                RefreshInstrumentsListUI();

                if(Main.GameConfig.HudState == HudState.BASIC)
                {
                    RefreshInstrumentDescriptionUI();
                }
                else
                {
                    RefreshBlockInfo();
                }

                UpdateUIProperties();
            }

            Instrument.Update(inputReadable);
        }

        void RefreshBlockInfo()
        {
            var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;
            if(def == null)
                return;

            if(def.BlockPairName != BlockPairName)
                throw new Exception("Unexpected, equipped tool is not multitool...");

            string displayName;

            FakeDef.DescriptionEnum = null;
            FakeDef.DescriptionArgs = null;

            if(!AllowInShips && Main.EquipmentMonitor.IsCockpitBuildMode)
            {
                Icons[0] = WarningIcon;
                displayName = def.DisplayNameText;
                FakeDef.DescriptionString = "Cannot be used in ships";
            }
            else
            {
                if(Instrument != null)
                {
                    Icons[0] = Instrument.HUDIcon;
                    displayName = Instrument.DisplayNameHUD;
                    FakeDef.DescriptionString = Instrument.Description.Text;
                }
                else
                {
                    Icons[0] = def.Icons[0];
                    displayName = def.DisplayNameText;
                    FakeDef.DescriptionString = string.Empty;
                }
            }

            var blockInfo = Sandbox.Game.Gui.MyHud.BlockInfo;

            // HACK: WhoWantsInfoDisplayed not whitelisted, so I have to allow the blockinfo to appear...
            //blockInfo.RemoveDisplayer(Sandbox.Game.Gui.MyHudBlockInfo.WhoWantsInfoDisplayed.CubeBuilder);

            blockInfo.DefinitionId = def.Id;
            blockInfo.BlockIcons = Icons;
            blockInfo.BlockName = displayName;
            blockInfo.SetContextHelp(FakeDef);
            blockInfo.PCUCost = 0;
            blockInfo.BlockIntegrity = 0f;
            blockInfo.CriticalComponentIndex = 0;
            blockInfo.CriticalIntegrity = 1f;
            blockInfo.OwnershipIntegrity = 1f;
            blockInfo.MissingComponentIndex = -1;
            blockInfo.GridSize = def.CubeSize;
            blockInfo.Components.Clear();
            blockInfo.BlockBuiltBy = 0;
        }

        void Equip()
        {
            if(DebugMode)
                DebugLog.PrintHUD(this, nameof(Equip), log: true);

            if(IsEquipped)
                return;

            IsEquipped = true;
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);

            PreviousAlignDefault = MyCubeBuilder.Static.AlignToDefault;

            ForceRefresh = true;

            if(Instruments.Count <= 0)
            {
                Log.Error("No instruments present, likely an error during init, please submit log");
                return;
            }

            Instrument = Instruments[InstrumentIdx];
            Instrument.Selected();
        }

        void Holster()
        {
            if(DebugMode)
                DebugLog.PrintHUD(this, nameof(Holster), log: true);

            if(!IsEquipped)
                return;

            IsEquipped = false;
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
            SetUIVisible(false);

            Instrument?.Deselected();

            MyCubeBuilder.Static.AlignToDefault = PreviousAlignDefault;
        }

        public override void UpdateDraw()
        {
            if(IsEquipped)
            {
                Instrument?.Draw();

                if(IsUIVisible)
                {
                    ListBox.Draw();
                    ListText.Draw();

                    if(Main.GameConfig.HudState == HudState.BASIC)
                    {
                        InstrumentBox.Draw();
                        InstrumentIcon.Draw();
                        InstrumentText.Draw();
                    }
                }
            }
        }

        void HandleInputs()
        {
            // revert this to avoid it changing in HUD too
            if(ControlAlignDefault.IsJustPressed())
            {
                MyCubeBuilder.Static.AlignToDefault = PreviousAlignDefault;
            }

            // only unmodified scroll, leave the modifiers available for the instruments
            if(Instruments.Count > 0 && !MyAPIGateway.Input.IsAnyCtrlKeyPressed() && !MyAPIGateway.Input.IsAnyAltKeyPressed() && !MyAPIGateway.Input.IsAnyShiftKeyPressed())
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    if(scroll < 0)
                    {
                        if(++InstrumentIdx >= Instruments.Count)
                            InstrumentIdx = 0;
                    }
                    else
                    {
                        if(--InstrumentIdx < 0)
                            InstrumentIdx = Instruments.Count - 1;
                    }

                    Instrument?.Deselected();
                    Instrument = Instruments[InstrumentIdx];
                    Instrument.Selected();
                    ForceRefresh = true;
                }
            }
        }

        void RefreshInstrumentsListUI()
        {
            StringBuilder sb = ListText.TextStringBuilder.Clear();

            sb.Color(ColorHeader).Append("Instruments ").Color(Color.Gray).Append("(scroll to change)").NewCleanLine();

            if(Instruments.Count <= 0)
            {
                sb.Color(Color.Red).Append("ERROR: zero instruments");
                return;
            }

            if(Instruments.Count >= MaxVisibleInstruments)
            {
                int startIndex = InstrumentIdx - (MaxVisibleInstruments / 2);
                while(startIndex < 0)
                    startIndex += Instruments.Count;

                int endIndex = InstrumentIdx + (MaxVisibleInstruments / 2);
                while(endIndex >= Instruments.Count)
                    endIndex -= Instruments.Count;

                int index = startIndex;

                for(int n = 0; n < MaxVisibleInstruments; n++)
                {
                    var instrument = Instruments[index];

                    sb.Append(instrument.IconChar).Append(" ");

                    if(InstrumentIdx == index)
                        sb.Color(ColorSelectedInstrument);

                    sb.Append(instrument.DisplayName).NewCleanLine();

                    if(++index >= Instruments.Count)
                        index = 0;
                }
            }
            else
            {
                for(int i = 0; i < Instruments.Count; i++)
                {
                    var instrument = Instruments[i];

                    sb.Append(instrument.IconChar).Append(" ");

                    if(InstrumentIdx == i)
                        sb.Color(ColorSelectedInstrument);

                    sb.Append(instrument.DisplayName).NewCleanLine();
                }
            }
        }

        void RefreshInstrumentDescriptionUI()
        {
            if(Instrument == null)
                return;

            StringBuilder sb = InstrumentText.TextStringBuilder.Clear();

            InstrumentIcon.Material = Instrument.BillboardIcon;

            Vector2 iconSizeWithPadding = (Vector2)(new Vector2D(BigIconSizePx * GUIScale + 4) * HudAPIv2.APIinfo.ScreenPositionOnePX);

            int paddingForLines = (int)Math.Ceiling(iconSizeWithPadding.Y / LineHeight) - 1;
            int padSpaces = (int)Math.Ceiling(iconSizeWithPadding.X / SpaceWidth);

            sb.Append(' ', padSpaces).Color(ColorHeader).Append(Instrument.DisplayName).NewCleanLine();
            //sb.Append(' ', padSpaces);

            for(int i = 0; i < paddingForLines - 1; i++)
            {
                sb.Append(' ', padSpaces).Append('\n');
            }

            sb.Append(Instrument.Description.Text);
            sb.TrimEndWhitespace();

            //for(int i = 0; i < Instrument.Description.Text.Length; i++)
            //{
            //    var c = Instrument.Description.Text[i];
            //
            //    if(c == '\n')
            //    {
            //        sb.Append(c);
            //
            //        if(--paddingForLines > 0)
            //        {
            //            sb.Append(' ', padSpaces);
            //        }
            //        continue;
            //    }
            //
            //    sb.Append(c);
            //}
        }
        #endregion
    }
}
