using System;
using System.Collections.Generic;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Collections;
using VRage.Game.Entity.UseObject;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    public class EventToolbarMonitor : ModComponent
    {
        public event Action<ListReader<IMyTerminalBlock>> OpenedToolbarConfig;
        public event Action<ListReader<IMyTerminalBlock>> ClosedToolbarConfig;

        public IMyUseObject LastAimedUseObject { get; private set; }

        readonly List<IMyTerminalBlock> TargetBlocks = new List<IMyTerminalBlock>();

        public ToolbarType LastOpenedToolbarType = ToolbarType.None;

        readonly HashSet<IMyTerminalControl> OverwrittenControls = new HashSet<IMyTerminalControl>();

        public enum ToolbarType
        {
            None,
            RCWaypoint,
            LockOnVictim,
        }

        public EventToolbarMonitor(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            MyCharacterDetectorComponent.OnInteractiveObjectChanged += UseObjectChanged;

            MyAPIGateway.TerminalControls.CustomControlGetter += TerminalControlGetter;

            Main.GUIMonitor.ScreenAdded += GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved += GUIScreenRemoved;
        }

        public override void UnregisterComponent()
        {
            MyCharacterDetectorComponent.OnInteractiveObjectChanged -= UseObjectChanged;

            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalControlGetter;

            if(!Main.ComponentsRegistered)
                return;

            Main.GUIMonitor.ScreenAdded -= GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved -= GUIScreenRemoved;
        }

        void UseObjectChanged(IMyUseObject useObject)
        {
            LastAimedUseObject = useObject;
        }

        void GUIScreenAdded(string screenTypeName)
        {
            if(!Main.GUIMonitor.InAnyToolbarGUI)
                return;

            if(TargetBlocks.Count > 0)
            {
                ClosedToolbarConfig?.Invoke(TargetBlocks);
            }

            TargetBlocks.Clear();

            if(Main.GUIMonitor.InTerminal && Main.TerminalInfo.SelectedInTerminal.Count > 0)
            {
                TargetBlocks.AddList(Main.TerminalInfo.SelectedInTerminal);
            }
            else
            {
                IMyTerminalBlock targetBlock = LastAimedUseObject?.Owner as IMyTerminalBlock;
                if(targetBlock != null)
                    TargetBlocks.Add(targetBlock);
            }

            if(TargetBlocks.Count > 0)
            {
                OpenedToolbarConfig?.Invoke(TargetBlocks);
            }
        }

        void GUIScreenRemoved(string screenTypeName)
        {
            if(TargetBlocks.Count > 0 && !Main.GUIMonitor.InAnyToolbarGUI)
            {
                ClosedToolbarConfig?.Invoke(TargetBlocks);

                TargetBlocks.Clear();

                LastOpenedToolbarType = ToolbarType.None;
            }
        }

        void TerminalControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            try
            {
                if(block is IMyShipController)
                {
                    foreach(IMyTerminalControl control in controls)
                    {
                        if(OverwrittenControls.Contains(control))
                            continue;

                        IMyTerminalControlButton button = control as IMyTerminalControlButton;
                        if(button != null)
                        {
                            if(button.Id == "Open Toolbar") // waypoint reached action toolbar
                            {
                                OverwrittenControls.Add(control);

                                Action<IMyTerminalBlock> originalAction = button.Action;
                                button.Action = (b) =>
                                {
                                    LastOpenedToolbarType = ToolbarType.RCWaypoint;
                                    originalAction?.Invoke(b);
                                };
                            }
                            else if(button.Id == "OpenToolbar") // lock-on action toolbar
                            {
                                OverwrittenControls.Add(control);

                                Action<IMyTerminalBlock> originalAction = button.Action;
                                button.Action = (b) =>
                                {
                                    LastOpenedToolbarType = ToolbarType.LockOnVictim;
                                    originalAction?.Invoke(b);
                                };
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
    }
}
