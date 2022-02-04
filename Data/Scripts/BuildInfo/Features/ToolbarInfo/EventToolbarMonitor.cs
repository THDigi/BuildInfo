using System;
using System.Collections.Generic;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;
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

        public EventToolbarMonitor(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            MyCharacterDetectorComponent.OnInteractiveObjectChanged += UseObjectChanged;

            Main.GUIMonitor.ScreenAdded += GUIScreenAdded;
            Main.GUIMonitor.ScreenRemoved += GUIScreenRemoved;
        }

        public override void UnregisterComponent()
        {
            MyCharacterDetectorComponent.OnInteractiveObjectChanged -= UseObjectChanged;

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
            }
        }
    }
}
