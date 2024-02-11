using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.Overlays.Specialized;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    /// <summary>
    /// TextAPI-based event-toolbar info (for example, show what each slot does in airvent's Setup Actions)
    /// </summary>
    public class EventToolbarInfo : ModComponent
    {
        public bool DrawingOverlays { get; private set; } = false;

        readonly Color HeaderColor = new Color(255, 240, 220);
        readonly Color SlotColor = new Color(55, 200, 155);
        readonly Color ModNameColor = Color.Gray;

        const float BackgroundOpacityMul = 0.98f;
        const float BackgroundOpacityHoverMin = 0.8f / BackgroundOpacityMul;
        readonly Color BackgroundColor = new Color(41, 54, 62);
        readonly Color BackgroundColorSelected = new Color(40, 80, 65);

        const string TextFont = FontsHandler.BI_SEOutlined;
        const bool UseShadowMessage = false;
        const double TextScaleMultiplier = 0.75;
        const double ShadowOffset = 0.002;
        const float BackgroundPadding = 0.03f;

        double GUIScale;

        TextAPI.TextPackage Label;

        IMyTerminalBlock TargetBlock;
        ListReader<IMyTerminalBlock> LastSeenBlocks = ListReader<IMyTerminalBlock>.Empty; // required or NRE's on Count

        OverlayDrawInstance DrawInstance;
        BoxDragging BoxDrag;

        public EventToolbarInfo(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EventToolbarMonitor.OpenedToolbarConfig += OpenedToolbarConfig;
            Main.EventToolbarMonitor.ClosedToolbarConfig += ClosedToolbarConfig;

            Main.TextAPI.Detected += TextAPIDetected;

            Main.Config.EventToolbarInfoPosition.ValueAssigned += ConfigPositionChanged;
            Main.Config.EventToolbarInfoScale.ValueAssigned += ConfigFloatChanged;
            Main.GUIMonitor.OptionsMenuClosed += UpdateFromConfig;

            DrawInstance = new OverlayDrawInstance(Main.Overlays, GetType().Name);
            DrawInstance.LabelRender.ForceDrawLabel = true;

            BoxDrag = new BoxDragging(MyMouseButtonsEnum.Left);
            BoxDrag.BoxSelected += () => UpdateBgOpacity(Math.Max(Main.GameConfig.UIBackgroundOpacity, BackgroundOpacityHoverMin) * BackgroundOpacityMul, BackgroundColorSelected);
            BoxDrag.BoxDeselected += () => UpdateBgOpacity(Main.GameConfig.UIBackgroundOpacity * BackgroundOpacityMul);
            BoxDrag.Dragging += (newPos) =>
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    ConfigLib.FloatSetting setting = Main.Config.EventToolbarInfoScale;
                    float scale = setting.Value + (scroll > 0 ? 0.05f : -0.05f);
                    scale = MathHelper.Clamp(scale, setting.Min, setting.Max);
                    setting.SetValue((float)Math.Round(scale, 3));
                }

                Main.Config.EventToolbarInfoPosition.SetValue(newPos);
                UpdateFromConfig();
            };
            BoxDrag.FinishedDragging += (finalPos) =>
            {
                Main.Config.Save();
                Main.ConfigMenuHandler.RefreshAll();
            };
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EventToolbarMonitor.OpenedToolbarConfig -= OpenedToolbarConfig;
            Main.EventToolbarMonitor.ClosedToolbarConfig -= ClosedToolbarConfig;

            Main.TextAPI.Detected -= TextAPIDetected;

            Main.Config.EventToolbarInfoPosition.ValueAssigned -= ConfigPositionChanged;
            Main.Config.EventToolbarInfoScale.ValueAssigned -= ConfigFloatChanged;
            Main.GUIMonitor.OptionsMenuClosed -= UpdateFromConfig;

            DrawInstance = null;
            BoxDrag = null;
        }

        void TextAPIDetected()
        {
            Label = new TextAPI.TextPackage(256, useShadow: UseShadowMessage, backgroundTexture: MyStringId.GetOrCompute("BuildInfo_UI_Square"));
            Label.Font = TextFont;
            Label.Scale = GUIScale;
            Label.Background.Width = 0f;
            Label.Background.Height = 0f;

            UpdateFromConfig();

            // render if toolbar menu is already open
            if(LastSeenBlocks.Count > 0 && Main.Config.EventToolbarInfo.Value && Main.TextAPI.IsEnabled)
            {
                RenderBox(LastSeenBlocks);
            }
        }

        void ConfigFloatChanged(float oldValue, float newValue, ConfigLib.SettingBase<float> setting)
        {
            UpdateFromConfig();
        }

        void ConfigPositionChanged(Vector2D oldValue, Vector2D newValue, ConfigLib.SettingBase<Vector2D> setting)
        {
            UpdateFromConfig();
        }

        void UpdateFromConfig()
        {
            GUIScale = (float)(TextScaleMultiplier * Main.Config.EventToolbarInfoScale.Value);

            if(Label != null)
            {
                Label.Scale = GUIScale;

                UpdateBgOpacity(Main.GameConfig.UIBackgroundOpacity * BackgroundOpacityMul);
                UpdateBoxPosition();
                UpdateScale();
            }
        }

        void UpdateBoxPosition()
        {
            if(Label != null)
            {
                Label.Position = Main.Config.EventToolbarInfoPosition.Value;
            }
        }

        void UpdateScale()
        {
            Vector2D textSize = Label.Text.GetTextLength();

            float edge = (float)(BackgroundPadding * GUIScale);
            Vector2D textOffset = new Vector2D(-textSize.X, 0) - new Vector2D(edge / 2); // top-right pivot and offset by background margin aswell
            Label.Text.Offset = textOffset;
            if(Label.Shadow != null)
                Label.Shadow.Offset = textOffset + new Vector2D(ShadowOffset, -ShadowOffset);

            float bgWidth = (float)Math.Abs(textSize.X) + edge;
            float bgHeight = (float)Math.Abs(textSize.Y) + edge;

            Label.Background.Width = bgWidth;
            Label.Background.Height = bgHeight;
            Label.Background.Offset = new Vector2D(bgWidth * -0.5, bgHeight * -0.5); // make it centered
        }

        void UpdateBgOpacity(float opacity, Color? colorOverride = null)
        {
            if(Label == null)
                return;

            Color color = (colorOverride ?? BackgroundColor);
            Utils.FadeColorHUD(ref color, opacity);

            Label.Background.BillBoardColor = color;
        }

        void OpenedToolbarConfig(ListReader<IMyTerminalBlock> blocks)
        {
            LastSeenBlocks = blocks;

            if(Main.Config.EventToolbarInfo.Value && Main.TextAPI.IsEnabled && Label != null)
            {
                RenderBox(blocks);
            }
        }

        void ClosedToolbarConfig(ListReader<IMyTerminalBlock> blocks)
        {
            LastSeenBlocks = ListReader<IMyTerminalBlock>.Empty;
            TargetBlock = null;
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
            DrawingOverlays = false;

            if(Label != null)
            {
                Label.Visible = false;
            }
        }

        void RenderBox(ListReader<IMyTerminalBlock> blocks) // called once
        {
            TargetBlock = blocks[0];
            StringBuilder sb = Label.Text.Message.Clear();

            if(!RenderBoxContent(sb, blocks))
                return;

            //if(Main.Config.ToolbarLabelsHeader.Value)
            //{
            sb.Color(ModNameColor).Append("<i>(").Append(BuildInfoMod.ModName).Append(" Mod)").NewCleanLine();
            //}

            sb.TrimEndWhitespace();

            if(UseShadowMessage)
                TextAPI.CopyWithoutColor(sb, Label.Shadow.Message);

            UpdateScale();

            Label.Visible = true;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true); // for dragging and overlay
        }

        bool RenderBoxContent(StringBuilder sb, ListReader<IMyTerminalBlock> blocks)
        {
            IMyEventControllerBlock eventController = TargetBlock as IMyEventControllerBlock;
            if(eventController != null)
            {
                IMyEventControllerEntityComponent eventComp = eventController.SelectedEvent;

                //foreach(MyComponentBase comp in eventController.Components)
                //{
                //    IMyEventControllerEntityComponent ev = comp as IMyEventControllerEntityComponent;
                //    if(ev != null)
                //    {
                //        var compGui = ev as IMyEventComponentWithGui;
                //        if(compGui != null)
                //        {
                //            DebugLog.PrintHUD(this, $"{ev.GetType().Name,-34} uses BlockList={compGui.IsBlocksListUsed,-5} | Threshold={compGui.IsThresholdUsed,-5} | Condition={compGui.IsConditionSelectionUsed,-5}", log: true);
                //        }
                //        else
                //        {
                //            DebugLog.PrintHUD(this, $"{ev.GetType().Name,-34} does does NOT implement IMyEventComponentWithGui", log: true);
                //        }
                //    }
                //}
                /* results of above code:
                    MyEventBlockAddedRemoved           does does NOT implement IMyEventComponentWithGui
                    
                    MyEventSurfaceHeight               uses BlockList=False | Threshold=False | Condition=True 
                    MyEventGridSpeedChanged            uses BlockList=False | Threshold=False | Condition=True 
                    MyEventNaturalGravityChanged       uses BlockList=False | Threshold=False | Condition=True 
                    
                    MyEventCargoFilledEntityComponent  uses BlockList=True  | Threshold=True  | Condition=True 
                    MyEventBlockIntegrity              uses BlockList=True  | Threshold=True  | Condition=True 
                    MyEventGasTankFilled               uses BlockList=True  | Threshold=True  | Condition=True 
                    MyEventStoredPower                 uses BlockList=True  | Threshold=True  | Condition=True 
                    MyEventPistonPosition              uses BlockList=True  | Threshold=True  | Condition=True 
                    MyEventPowerOutput                 uses BlockList=True  | Threshold=True  | Condition=True 
                    MyEventThrustPercentage            uses BlockList=True  | Threshold=True  | Condition=True 
                    
                    MyEventAngleChanged                uses BlockList=True  | Threshold=False | Condition=True 
                    MyEventDistanceToLockedTarget      uses BlockList=True  | Threshold=False | Condition=True 
                    
                    MyEventCockpitOccupied             uses BlockList=True  | Threshold=False | Condition=False
                    MyEventConnectorConnected          uses BlockList=True  | Threshold=False | Condition=False
                    MyEventLandingGearLocked           uses BlockList=True  | Threshold=False | Condition=False
                    MyEventDoorOpened                  uses BlockList=True  | Threshold=False | Condition=False
                    MyEventBlockOnOff                  uses BlockList=True  | Threshold=False | Condition=False
                    MyEventRotorHingeAttachedDetached  uses BlockList=True  | Threshold=False | Condition=False
                    MyEventMerged                      uses BlockList=True  | Threshold=False | Condition=False
                    MyEventMagneticLockReady           uses BlockList=True  | Threshold=False | Condition=False
                    MyEventConnectorReadyToLock        uses BlockList=True  | Threshold=False | Condition=False
                */

                if(eventComp != null)
                {
                    string eventName = MyTexts.GetString(eventComp.EventDisplayName);

                    const int TitleMaxLen = 32;
                    string title;
                    if(eventName.Length > TitleMaxLen)
                        title = $"'{eventName.Substring(0, TitleMaxLen)}...' toolbar for ";
                    else
                        title = $"'{eventName}' toolbar for ";

                    RenderBoxHeader(sb, blocks.Count, title);

                    // only for IMyEventComponentWithGui.IsBlocksListUsed
                    bool andMode = eventController.IsAndModeEnabled;

                    // only for IMyEventComponentWithGui.IsConditionSelectionUsed
                    string conditionInfoTrue = (eventController.IsLowerOrEqualCondition ? "<= " : ">= ");
                    string conditionInfoFalse = (eventController.IsLowerOrEqualCondition ? "> " : "< ");

                    string note = null;

                    StringBuilder slot1 = new StringBuilder(256);
                    StringBuilder slot2 = new StringBuilder(256);

                    // HACK: hardcoded events, needs updating when new vanilla ones get added
                    /* dumped via C# interactive:

                    #r "C:\Steam\steamapps\common\SpaceEngineers\Bin64\SpaceEngineers.Game.dll"
                    using Sandbox.ModAPI;
                    var types = typeof(SpaceEngineers.Game.EntityComponents.Blocks.MyEventAngleChanged).Assembly.GetTypes();

                    foreach(var type in types)
                    {
                        if(typeof(IMyEventControllerEntityComponent).IsAssignableFrom(type))
                        {
                            Console.WriteLine($"case \"{type.Name}\":");
                        }
                    }
                    */

                    string typeName = eventComp.GetType().Name;
                    switch(typeName)
                    {
                        case "MyEventBlockAddedRemoved":
                        {
                            slot1.Append("when a block is added");
                            slot2.Append("when a block is removed/destroyed");
                            break;
                        }

                        case "MyEventBlockOnOff":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all blocks are ");
                                slot2.Append("when each block is ");
                            }
                            else // default
                            {
                                slot1.Append("when each block is ");
                                slot2.Append("when all blocks are ");
                            }

                            slot1.Append("turned on");
                            slot2.Append("turned off");
                            break;
                        }

                        case "MyEventCockpitOccupied":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all cockpits are ");
                                slot2.Append("when each cockpit is ");
                            }
                            else // default
                            {
                                slot1.Append("when each cockpit is ");
                                slot2.Append("when all cockpits are ");
                            }

                            slot1.Append("occupied");
                            slot2.Append("emptied");
                            break;
                        }

                        case "MyEventConnectorConnected":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all connectors are ");
                                slot2.Append("when each connector is ");
                            }
                            else // default
                            {
                                slot1.Append("when each connector is ");
                                slot2.Append("when all connectors are ");
                            }

                            slot1.Append("connected");
                            slot2.Append("disconnected");
                            break;
                        }

                        case "MyEventConnectorReadyToLock":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all connectors are ");
                                slot2.Append("when each connector is ");
                            }
                            else // default
                            {
                                slot1.Append("when each connector is ");
                                slot2.Append("when all connectors are ");
                            }

                            slot1.Append("ready to lock");
                            slot2.Append("turned idle");
                            break;
                        }

                        case "MyEventDoorOpened":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all doors are ");
                                slot2.Append("when each door is ");
                            }
                            else // default
                            {
                                slot1.Append("when each door is ");
                                slot2.Append("when all doors are ");
                            }

                            slot1.Append("opened");
                            slot2.Append("closed");
                            break;
                        }

                        case "MyEventGridSpeedChanged":
                        {
                            float speed = eventController.GetValue<float>("Speed");
                            slot1.Append("when speed ").Append(conditionInfoTrue).SpeedFormat(speed, 2);
                            slot2.Append("when speed ").Append(conditionInfoFalse).SpeedFormat(speed, 2);
                            break;
                        }

                        case "MyEventLandingGearLocked":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all landing gears are ");
                                slot2.Append("when each landing gear is ");
                            }
                            else // default
                            {
                                slot1.Append("when each landing gear is ");
                                slot2.Append("when all landing gears are ");
                            }

                            slot1.Append("locked");
                            slot2.Append("unlocked");
                            break;
                        }

                        case "MyEventMagneticLockReady":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all landing gears are ");
                                slot2.Append("when each landing gear is ");
                            }
                            else // default
                            {
                                slot1.Append("when each landing gear is ");
                                slot2.Append("when all landing gears are ");
                            }

                            slot1.Append("ready to lock");
                            slot2.Append("turned idle");
                            break;
                        }

                        case "MyEventMerged":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all blocks are ");
                                slot2.Append("when each block is ");
                            }
                            else // default
                            {
                                slot1.Append("when each block is ");
                                slot2.Append("when all blocks are ");
                            }

                            slot1.Append("merged");
                            slot2.Append("unmerged");
                            break;
                        }

                        case "MyEventRotorHingeAttachedDetached":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all blocks are ");
                                slot2.Append("when each block is ");
                            }
                            else // default
                            {
                                slot1.Append("when each block is ");
                                slot2.Append("when all blocks are ");
                            }

                            slot1.Append("attached");
                            slot2.Append("detached");
                            break;
                        }

                        case "MyEventDistanceToLockedTarget":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all locked-on targets are ");
                                slot2.Append("when each locked-on target is ");
                            }
                            else // default
                            {
                                slot1.Append("when each locked-on target is ");
                                slot2.Append("when all locked-on targets are ");
                            }

                            // HACK: the game adds 2x NaturalGravityChangedSlider and the 2nd one is this comp's...
                            ITerminalProperty sliderProp = null;
                            {
                                var props = new List<ITerminalProperty>(2);
                                eventController.GetProperties(props, (p) => p.Id == "NaturalGravityChangedSlider");
                                if(props.Count >= 2)
                                    sliderProp = props[1];
                            }

                            if(sliderProp == null)
                            {
                                slot1.Append(conditionInfoTrue).Append("<distance slider>");
                                slot2.Append(conditionInfoFalse).Append("<distance slider>");
                            }
                            else
                            {
                                float distance = sliderProp.As<float>().GetValue(eventController);
                                slot1.Append(conditionInfoTrue).DistanceFormat(distance, 2);
                                slot2.Append(conditionInfoFalse).DistanceFormat(distance, 2);
                            }
                            break;
                        }

                        case "MyEventAngleChanged":
                        {
                            if(andMode)
                            {
                                slot1.Append("when all blocks are ");
                                slot2.Append("when each block is ");
                            }
                            else // default
                            {
                                slot1.Append("when each block is ");
                                slot2.Append("when all blocks are ");
                            }

                            float angle = eventController.GetValue<float>("Angle");
                            slot1.Append(conditionInfoTrue).AngleFormat(angle, 1);
                            slot2.Append(conditionInfoFalse).AngleFormat(angle, 1);
                            break;
                        }

                        case "MyEventSurfaceHeight":
                        {
                            float height = eventController.GetValue<float>("SurfaceheightSlider");
                            slot1.Append("when altitude ").Append(conditionInfoTrue).DistanceFormat(height, 2);
                            slot2.Append("when altitude ").Append(conditionInfoFalse).DistanceFormat(height, 2);
                            break;
                        }

                        case "MyEventNaturalGravityChanged":
                        {
                            float g = eventController.GetValue<float>("NaturalGravityChangedSlider");
                            slot1.Append("when gravity ").Append(conditionInfoTrue).RoundedNumber(g, 2).Append(" g");
                            slot2.Append("when gravity ").Append(conditionInfoFalse).RoundedNumber(g, 2).Append(" g");
                            break;
                        }

                        case "MyEventBlockIntegrity":
                        case "MyEventCargoFilledEntityComponent":
                        case "MyEventGasTankFilled":
                        case "MyEventPistonPosition":
                        case "MyEventPowerOutput":
                        case "MyEventStoredPower":
                        case "MyEventThrustPercentage":
                        {
                            string singular = "block";
                            string plural = "blocks";
                            string suffix = "";

                            switch(typeName)
                            {
                                case "MyEventBlockIntegrity": singular = "block"; plural = "blocks"; suffix = " integrity"; break;
                                case "MyEventCargoFilledEntityComponent":
                                    singular = "inventory"; plural = "inventories"; suffix = " filled";
                                    note = "NOTE: This acts per inventory, Refinery for example has 2.";
                                    break;
                                case "MyEventGasTankFilled": singular = "tank"; plural = "tanks"; suffix = " filled"; break;
                                case "MyEventPistonPosition": singular = "piston's position"; plural = "pistons' position"; break;
                                case "MyEventPowerOutput": suffix = " power output"; break;
                                case "MyEventStoredPower": suffix = " stored"; break;
                                case "MyEventThrustPercentage": singular = "thruster"; plural = "thrusters"; suffix = " thrust"; break;
                            }

                            if(andMode)
                            {
                                slot1.Append("when all ").Append(plural).Append(" are ");
                                slot2.Append("when each ").Append(singular).Append(" is ");
                            }
                            else // default
                            {
                                slot1.Append("when each ").Append(singular).Append(" is ");
                                slot2.Append("when all ").Append(plural).Append(" are ");
                            }

                            float threshold = eventController.Threshold;
                            slot1.Append(conditionInfoTrue).ProportionToPercent(threshold, 2).Append(suffix);
                            slot2.Append(conditionInfoFalse).ProportionToPercent(threshold, 2).Append(suffix);
                            break;
                        }

                        default:
                        {
                            var compWithGUI = eventComp as IMyEventComponentWithGui;
                            if(compWithGUI != null)
                            {
                                if(compWithGUI.IsBlocksListUsed)
                                {
                                    if(compWithGUI.IsThresholdUsed && compWithGUI.IsConditionSelectionUsed)
                                    {
                                        if(andMode)
                                        {
                                            slot1.Append("when all blocks are ");
                                            slot2.Append("when each block is ");
                                        }
                                        else // default
                                        {
                                            slot1.Append("when each block is ");
                                            slot2.Append("when all blocks are ");
                                        }

                                        float threshold = eventController.Threshold;
                                        slot1.Append(conditionInfoTrue).ProportionToPercent(threshold, 2);
                                        slot2.Append(conditionInfoFalse).ProportionToPercent(threshold, 2);
                                    }
                                    else if(compWithGUI.IsThresholdUsed && compWithGUI.IsConditionSelectionUsed)
                                    {
                                        // not much to guess here, likely has threshold as a custom slider or something else entirely
                                    }
                                    else
                                    {
                                        // not much to guess here
                                    }
                                }
                                else
                                {
                                    // not much to guess here
                                }
                            }
                            else
                            {
                                // not much to guess here
                            }
                            break;
                        }
                    }

                    if(slot1.Length == 0)
                        slot1.Append("when condition is true");

                    if(slot2.Length == 0)
                        slot2.Append("when condition is false");

                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": ").AppendStringBuilder(slot1).Append('\n');
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": ").AppendStringBuilder(slot2).Append('\n');

                    if(note != null)
                        sb.Append(note).Append('\n');
                }
                else
                {
                    RenderBoxHeader(sb, blocks.Count, "'(No event)' toolbar for ");
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": ").Append("when condition is true").Append('\n');
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": ").Append("when condition is false").Append('\n');
                }

                sb.Append("Same action can be used in both slots by using different pages.\n");
                return true;
            }

            IMyButtonPanel button = TargetBlock as IMyButtonPanel;
            if(button != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Append("Each slot is a button.\n");

                MyButtonPanelDefinition buttonDef = TargetBlock.SlimBlock.BlockDefinition as MyButtonPanelDefinition;

                string dummyName = Main.EventToolbarMonitor.LastAimedUseObject?.Dummy?.Name;
                BData_ButtonPanel data = Main.LiveDataHandler.Get<BData_ButtonPanel>(buttonDef);
                BData_ButtonPanel.ButtonInfo buttonInfo;
                if(dummyName != null && data != null && data.ButtonInfoByDummyName.TryGetValue(dummyName, out buttonInfo))
                {
                    sb.Append("Aimed button is ").Color(SlotColor).Append("slot ").Append(buttonInfo.Index + 1).ResetFormatting().Append('\n');
                }

                if(blocks.Count > 1)
                {
                    int buttonCountMin = int.MaxValue;
                    int buttonCountMax = int.MinValue;

                    for(int i = 0; i < blocks.Count; i++)
                    {
                        MyButtonPanelDefinition def = blocks[i].SlimBlock.BlockDefinition as MyButtonPanelDefinition;
                        if(def != null)
                        {
                            buttonCountMin = Math.Min(def.ButtonCount, buttonCountMin);
                            buttonCountMax = Math.Max(def.ButtonCount, buttonCountMax);
                        }
                    }

                    if(buttonDef.ButtonCount > buttonCountMin)
                    {
                        sb.Color(Color.Yellow).Append("NOTE: ").ResetFormatting().Append("some blocks have less buttons, lowest: ").Append(buttonCountMin).Append('\n');
                    }
                    else if(buttonDef.ButtonCount < buttonCountMax)
                    {
                        sb.Color(Color.Yellow).Append("NOTE: ").ResetFormatting().Append("some blocks have more buttons, highest: ").Append(buttonCountMax).Append('\n');
                    }
                }

                return true;
            }

            IMyAirVent vent = TargetBlock as IMyAirVent;
            if(vent != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": room pressurized\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": room no longer pressurized\n");
                return true;
            }

            IMySensorBlock sensor = TargetBlock as IMySensorBlock;
            if(sensor != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": on first detection\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": when nothing is detected anymore\n");
                return true;
            }

            IMyTargetDummyBlock targetDummy = TargetBlock as IMyTargetDummyBlock;
            if(targetDummy != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": dummy is hit (or destroyed)\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": dummy is destroyed\n");
                return true;
            }

            IMyShipController shipCtrl = TargetBlock as IMyShipController;
            if(shipCtrl != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                if(Main.EventToolbarMonitor.LastOpenedToolbarType == EventToolbarMonitor.ToolbarType.LockOnVictim)
                {
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": once this ship is locked on\n");
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": no longer locked on\n");
                    return true;
                }

                if(TargetBlock is IMyRemoteControl && Main.EventToolbarMonitor.LastOpenedToolbarType == EventToolbarMonitor.ToolbarType.RCWaypoint)
                {
                    sb.Append("All slots: waypoint reached\n");
                    return true;
                }
            }

            IMyTimerBlock timer = TargetBlock as IMyTimerBlock;
            if(timer != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Append("All slots: timer countdown reached\n");
                return true;
            }

            IMyTurretControlBlock tcb = TargetBlock as IMyTurretControlBlock;
            if(tcb != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": turret aligned with target (angle deviation)\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": turret no longer aligned with target\n");
                return true;
            }

            return false; // unknown block, don't draw
        }

        void RenderBoxHeader(StringBuilder sb, int blockCount, string customTitle = null, bool includeBlocks = true)
        {
            sb.Color(HeaderColor);

            if(customTitle != null)
            {
                sb.Append(customTitle);
            }
            else
            {
                switch(Main.EventToolbarMonitor.LastOpenedToolbarType)
                {
                    default: sb.Append("Event Toolbar for "); break;
                    case EventToolbarMonitor.ToolbarType.RCWaypoint: sb.Append("Waypoint Event Toolbar for "); break;
                    case EventToolbarMonitor.ToolbarType.LockOnVictim: sb.Append("LockOn Event Toolbar for "); break;
                }
            }

            if(includeBlocks)
            {
                if(blockCount > 1)
                {
                    sb.Append(blockCount).Append(" blocks");
                }
                else
                {
                    sb.Append("\"").AppendMaxLength(TargetBlock.CustomName, 24).Append("\"");
                }
            }

            sb.ResetFormatting().Append('\n');
        }

        public override void UpdateDraw()
        {
            if(TargetBlock == null) // redundancy
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
                DrawingOverlays = false;
                return;
            }

            #region Draggable box update
            Vector2D center = Label.Background.Origin + Label.Background.Offset;
            Vector2D halfExtents = new Vector2D(Label.Background.Width, Label.Background.Height) / 2;
            BoxDrag.DragHitbox = new BoundingBox2D(center - halfExtents, center + halfExtents);
            BoxDrag.Position = Label.Text.Origin;
            BoxDrag.Update();
            #endregion

            #region Overlays
            IMyButtonPanel button = TargetBlock as IMyButtonPanel;
            if(button != null)
            {
                SpecializedOverlayBase overlay = Main.SpecializedOverlays.Get(TargetBlock.BlockDefinition.TypeId);
                if(overlay != null)
                {
                    MatrixD drawMatrix = Utils.GetBlockCenteredWorldMatrix(TargetBlock.SlimBlock);
                    overlay.Draw(ref drawMatrix, DrawInstance, (MyCubeBlockDefinition)TargetBlock.SlimBlock.BlockDefinition, TargetBlock.SlimBlock);

                    DrawingOverlays = true;
                }
            }
            #endregion
        }
    }
}
