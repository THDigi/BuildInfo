using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Features.Overlays;
using Digi.BuildInfo.Features.Overlays.Specialized;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Input;
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
        readonly Color BackgroundColor = Constants.Color_UIBackground;
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
            Label = new TextAPI.TextPackage(256, useShadow: UseShadowMessage, backgroundTexture: Constants.MatUI_Square);
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

                    StringBuilder slot1 = new StringBuilder(128);
                    StringBuilder slot2 = new StringBuilder(128);
                    StringBuilder note = new StringBuilder(128);
                    GetEventControllerSlotInfo(eventController, slot1, slot2, note);

                    sb.Color(SlotColor).Append("Left slots").ResetFormatting().Append(": when ").AppendStringBuilder(slot1).Append('\n');
                    sb.Color(SlotColor).Append("Right slots").ResetFormatting().Append(": when ").AppendStringBuilder(slot2).Append('\n');

                    if(note.Length > 0)
                        sb.Append("<color=255,220,155>Note:<reset> ").AppendStringBuilder(note).Append('\n');
                }
                else
                {
                    RenderBoxHeader(sb, blocks.Count, "'(No event)' toolbar for ");
                    sb.Color(SlotColor).Append("Left slots").ResetFormatting().Append(": when condition is true").Append('\n');
                    sb.Color(SlotColor).Append("Right slots").ResetFormatting().Append(": when condition is false").Append('\n');
                }

                sb.NewCleanLine();
                sb.Append("Hint: The same action can be used on both slots by using different pages.\n");

                // TODO: need a way to know when a slot is triggered multiple times, some might even trigger in quick succession to even be possible in emissive...
                sb.Append("Hint: Block's lights change color depending on triggered side: ")
                    .Color(Hardcoded.EmissivePreset_EventController_State0).Append("left<reset>, ")
                    .Color(Hardcoded.EmissivePreset_EventController_State1).Append("right<reset>.\n");
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
                sb.Append("Hint: The same action can be used on both slots by using different pages.\n");
                return true;
            }

            IMySensorBlock sensor = TargetBlock as IMySensorBlock;
            if(sensor != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": on first detection\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": when nothing is detected anymore\n");
                sb.Append("Hint: The same action can be used on both slots by using different pages.\n");
                return true;
            }

            IMyTargetDummyBlock targetDummy = TargetBlock as IMyTargetDummyBlock;
            if(targetDummy != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": dummy is hit (or destroyed)\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": dummy is destroyed\n");
                sb.Append("Hint: The same action can be used on both slots by using different pages.\n");
                return true;
            }

            IMyShipController shipCtrl = TargetBlock as IMyShipController;
            if(shipCtrl != null)
            {
                if(Main.EventToolbarMonitor.LastOpenedToolbarType == EventToolbarMonitor.ToolbarType.LockOnVictim)
                {
                    RenderBoxHeader(sb, blocks.Count);
                    sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": once this ship is locked on\n");
                    sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": no longer locked on\n");
                    sb.Append("Hint: The same action can be used on both slots by using different pages.\n");
                    return true;
                }

                if(TargetBlock is IMyRemoteControl && Main.EventToolbarMonitor.LastOpenedToolbarType == EventToolbarMonitor.ToolbarType.RCWaypoint)
                {
                    string title = "Unknown waypoint on ";

                    var apc = TargetBlock.Components.Get<MyAutopilotComponent>();
                    var waypoints = apc?.SelectedWaypoints;
                    if(waypoints != null && waypoints.Count > 0)
                    {
                        title = $"\"{waypoints[0].Name}\" waypoint on ";
                    }

                    RenderBoxHeader(sb, blocks.Count, title);

                    sb.Color(SlotColor).Append("All slots").ResetFormatting().Append(": waypoint reached\n");
                    sb.Append("Note: only one waypoint is being configured even if multiple are selected!\n");
                    return true;
                }
            }

            IMyTimerBlock timer = TargetBlock as IMyTimerBlock;
            if(timer != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("All slots").ResetFormatting().Append(": timer countdown reached\n");
                return true;
            }

            IMyTurretControlBlock tcb = TargetBlock as IMyTurretControlBlock;
            if(tcb != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": turret aligned with target (angle deviation)\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": turret no longer aligned with target\n");
                sb.Append("Hint: The same action can be used on both slots by using different pages.\n");
                return true;
            }

            IMyDefensiveCombatBlock defensiveCombat = TargetBlock as IMyDefensiveCombatBlock;
            if(defensiveCombat != null)
            {
                RenderBoxHeader(sb, blocks.Count);

                sb.Color(SlotColor).Append("Slot 1").ResetFormatting().Append(": first enemy detected\n");
                sb.Color(SlotColor).Append("Slot 2").ResetFormatting().Append(": no more enemites detected\n");
                sb.Append("Hint: The same action can be used on both slots by using different pages.\n");
                return true;
            }

            // IMyTransponder and anything else that uses this component
            IMySignalReceiverEntityComponent transponderComp;
            if(TargetBlock.Components.TryGet(out transponderComp))
            {
                RenderBoxHeader(sb, blocks.Count);
                sb.Color(SlotColor).Append("All slots").ResetFormatting().Append(": signal received\n");
                return true;
            }

            // IMyPathRecorder and anything else using this component
            MyPathRecorderComponent pathRecordComp;
            if(TargetBlock.Components.TryGet(out pathRecordComp))
            {
                string title = "Unknown waypoint on ";

                var waypoints = pathRecordComp?.Waypoints;
                if(waypoints != null && waypoints.Count > 0)
                {
                    foreach(var wp in waypoints)
                    {
                        if(wp.SelectedForDraw)
                        {
                            title = $"\"{wp.Name}\" waypoint on ";
                            break; // HACK: same behavior MyPathRecorderComponent.SelectedWaypointsChanged(), first in order gets the toolbar
                        }
                    }
                }

                RenderBoxHeader(sb, blocks.Count, title);

                sb.Color(SlotColor).Append("All slots").ResetFormatting().Append(": waypoint reached\n");
                sb.Append("Note: only one waypoint is being configured even if multiple are selected!\n");
                return true;
            }

            // IMyBasicMissionBlock and anything else using this component
            IMyBasicMissionAutopilot autopilotComp;
            if(TargetBlock.Components.TryGet(out autopilotComp) && autopilotComp.IsSelected)
            {
                string title = "Unknown waypoint on ";

                // TODO: this does not work, find some alternative...
                //TempWaypoints.Clear();
                //try
                //{
                //    autopilotComp.GetWaypoints(TempWaypoints);
                //    foreach(var wp in TempWaypoints)
                //    {
                //        var internalWp = (MyAutopilotWaypoint)wp;
                //        if(internalWp.SelectedForDraw)
                //        {
                //            title = $"\"{wp.Name}\" waypoint on ";
                //            break; // HACK: same as in ID_AUTOPILOT_SETUP_ACTION_BUTTON in MyBasicMissionAutopilot.CreateTerminalControls() - only the first selected is configured.
                //        }
                //    }
                //}
                //finally
                //{
                //    TempWaypoints.Clear();
                //}

                RenderBoxHeader(sb, blocks.Count, title);

                sb.Color(SlotColor).Append("All slots").ResetFormatting().Append(": waypoint reached\n");
                sb.Append("Note: only one waypoint is being configured even if multiple are selected!\n");
                return true;
            }

            return false; // unknown block, don't draw
        }

        //List<Sandbox.ModAPI.Ingame.IMyAutopilotWaypoint> TempWaypoints = new List<Sandbox.ModAPI.Ingame.IMyAutopilotWaypoint>();

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

        /// <summary>
        /// Appends short precise descriptions of what each slot triggers from depending on the event controller's event and settings.
        /// If event is unknown or combination of settings cannot be identified, the <paramref name="slot1"/> and <paramref name="slot2"/> will get a generic info and the method returns false.
        /// The <paramref name="note"/> is optional (can be null) and occassionaly has some extra info.
        /// </summary>
        public static bool GetEventControllerSlotInfo(IMyEventControllerBlock eventController, StringBuilder slot1, StringBuilder slot2, StringBuilder note = null)
        {
            IMyEventControllerEntityComponent eventComp = eventController.SelectedEvent;
            if(eventComp == null)
            {
                slot1.Append("condition is true");
                slot2.Append("condition is false");
                return false;
            }

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

            // add result the end of the switch to quickly verify if any are new, the ones without a red highlight.
            */

            // only for IMyEventComponentWithGui.IsBlocksListUsed
            bool andMode = eventController.IsAndModeEnabled;

            // only for IMyEventComponentWithGui.IsConditionSelectionUsed
            string conditionInfoTrue = (eventController.IsLowerOrEqualCondition ? "<= " : ">= ");
            string conditionInfoFalse = (eventController.IsLowerOrEqualCondition ? "> " : "< ");

            string typeName = eventComp.GetType().Name;
            switch(typeName)
            {
                case "MyEventBlockAddedRemoved":
                {
                    slot1.Append("a block is added");
                    slot2.Append("a block is removed/destroyed");
                    break;
                }

                case "MyEventBlockOnOff":
                {
                    if(andMode)
                    {
                        slot1.Append("all blocks are ");
                        slot2.Append("each block is ");
                    }
                    else // default
                    {
                        slot1.Append("each block is ");
                        slot2.Append("all blocks are ");
                    }

                    slot1.Append("turned on");
                    slot2.Append("turned off");
                    break;
                }

                case "MyEventCockpitOccupied":
                {
                    if(andMode)
                    {
                        slot1.Append("all seats are ");
                        slot2.Append("each seat is ");
                    }
                    else // default
                    {
                        slot1.Append("each seat is ");
                        slot2.Append("all seats are ");
                    }

                    slot1.Append("occupied");
                    slot2.Append("emptied");
                    break;
                }

                case "MyEventConnectorConnected":
                case "MyEventConnectorReadyToLock":
                {
                    if(andMode)
                    {
                        slot1.Append("all connectors are ");
                        slot2.Append("each connector is ");
                    }
                    else // default
                    {
                        slot1.Append("each connector is ");
                        slot2.Append("all connectors are ");
                    }

                    if(typeName == "MyEventConnectorConnected")
                    {
                        slot1.Append("connected");
                        slot2.Append("disconnected");
                    }
                    else
                    {
                        slot1.Append("ready to lock");
                        slot2.Append("turned idle");
                    }
                    break;
                }

                case "MyEventDoorOpened":
                {
                    if(andMode)
                    {
                        slot1.Append("all doors are ");
                        slot2.Append("each door is ");
                    }
                    else // default
                    {
                        slot1.Append("each door is ");
                        slot2.Append("all doors are ");
                    }

                    slot1.Append("fully open");
                    slot2.Append("fully closed");
                    break;
                }

                case "MyEventGridSpeedChanged":
                {
                    float speed = eventController.GetValue<float>("Speed");
                    slot1.Append("speed ").Append(conditionInfoTrue).SpeedFormat(speed, 2);
                    slot2.Append("speed ").Append(conditionInfoFalse).SpeedFormat(speed, 2);
                    break;
                }

                case "MyEventLandingGearLocked":
                case "MyEventMagneticLockReady":
                {
                    if(andMode)
                    {
                        slot1.Append("all landing gears are ");
                        slot2.Append("each landing gear is ");
                    }
                    else // default
                    {
                        slot1.Append("each landing gear is ");
                        slot2.Append("all landing gears are ");
                    }

                    if(typeName == "MyEventLandingGearLocked")
                    {
                        slot1.Append("locked");
                        slot2.Append("unlocked");
                    }
                    else
                    {
                        slot1.Append("ready to lock");
                        slot2.Append("turned idle");
                    }
                    break;
                }

                case "MyEventMerged":
                {
                    if(andMode)
                    {
                        slot1.Append("all blocks are ");
                        slot2.Append("each block is ");
                    }
                    else // default
                    {
                        slot1.Append("each block is ");
                        slot2.Append("all blocks are ");
                    }

                    slot1.Append("merged");
                    slot2.Append("unmerged");
                    break;
                }

                case "MyEventRotorHingeAttachedDetached":
                {
                    if(andMode)
                    {
                        slot1.Append("all blocks are ");
                        slot2.Append("each block is ");
                    }
                    else // default
                    {
                        slot1.Append("each block is ");
                        slot2.Append("all blocks are ");
                    }

                    slot1.Append("attached");
                    slot2.Append("detached");
                    break;
                }

                case "MyEventDistanceToLockedTarget":
                {
                    if(andMode)
                    {
                        slot1.Append("all locked-on targets are ");
                        slot2.Append("each locked-on target is ");
                    }
                    else // default
                    {
                        slot1.Append("each locked-on target is ");
                        slot2.Append("all locked-on targets are ");
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
                        slot1.Append("all blocks are ");
                        slot2.Append("each block is ");
                    }
                    else // default
                    {
                        slot1.Append("each block is ");
                        slot2.Append("all blocks are ");
                    }

                    float angle = eventController.GetValue<float>("Angle");
                    slot1.Append(conditionInfoTrue).AngleFormatDeg(angle, 1);
                    slot2.Append(conditionInfoFalse).AngleFormatDeg(angle, 1);
                    break;
                }

                case "MyEventSurfaceHeight":
                {
                    float height = eventController.GetValue<float>("SurfaceheightSlider");
                    slot1.Append("altitude ").Append(conditionInfoTrue).DistanceFormat(height, 2);
                    slot2.Append("altitude ").Append(conditionInfoFalse).DistanceFormat(height, 2);
                    break;
                }

                case "MyEventNaturalGravityChanged":
                {
                    float g = eventController.GetValue<float>("NaturalGravityChangedSlider");
                    slot1.Append("gravity ").Append(conditionInfoTrue).RoundedNumber(g, 2).Append(" g");
                    slot2.Append("gravity ").Append(conditionInfoFalse).RoundedNumber(g, 2).Append(" g");
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
                            note?.Append("This acts per inventory, for example Refinery has 2.");
                            break;
                        case "MyEventGasTankFilled": singular = "tank"; plural = "tanks"; suffix = " filled"; break;
                        case "MyEventPistonPosition": singular = "piston's position"; plural = "pistons' position"; break;
                        case "MyEventPowerOutput": suffix = " power output"; break;
                        case "MyEventStoredPower": suffix = " stored"; break;
                        case "MyEventThrustPercentage": singular = "thruster"; plural = "thrusters"; suffix = " thrust"; break;
                    }

                    if(andMode)
                    {
                        slot1.Append("all ").Append(plural).Append(" are ");
                        slot2.Append("each ").Append(singular).Append(" is ");
                    }
                    else // default
                    {
                        slot1.Append("each ").Append(singular).Append(" is ");
                        slot2.Append("all ").Append(plural).Append(" are ");
                    }

                    float threshold = eventController.Threshold;
                    slot1.Append(conditionInfoTrue).ProportionToPercent(threshold, 2).Append(suffix);
                    slot2.Append(conditionInfoFalse).ProportionToPercent(threshold, 2).Append(suffix);
                    break;
                }

                // primarily for mod-added ones but can be useful for newly added game ones too
                default:
                {
                    if(BuildInfoMod.IsDevMod)
                    {
                        Log.Error($"[DEV] New(?) EC event: {typeName} (unless it's from a mod then ignore this)");
                    }

                    var compWithGUI = eventComp as IMyEventComponentWithGui;
                    if(compWithGUI != null)
                    {
                        if(compWithGUI.IsBlocksListUsed)
                        {
                            if(compWithGUI.IsThresholdUsed && compWithGUI.IsConditionSelectionUsed)
                            {
                                if(andMode)
                                {
                                    slot1.Append("all blocks are ");
                                    slot2.Append("each block is ");
                                }
                                else // default
                                {
                                    slot1.Append("each block is ");
                                    slot2.Append("all blocks are ");
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

            bool hasInfo = slot1.Length > 0 && slot2.Length > 0;
            if(!hasInfo)
            {
                slot1.Clear().Append("condition is true");
                slot2.Clear().Append("condition is false");
            }

            return hasInfo;
        }
    }
}
