using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.MultiTool.Instruments
{
    public class ConveyorVis : InstrumentBase
    {
        public ConveyorVis() : base("Conveyor Network Visualizer", Constants.MatUI_Icon_ConveyorVis)
        {
            DisplayNameHUD = "Conveyor Network\nVisualizer";

            Main.GUIMonitor.OptionsMenuClosed += RefreshDescription;
            RefreshDescription();
        }

        public override void Dispose()
        {
            Main.GUIMonitor.OptionsMenuClosed -= RefreshDescription;
        }

        public override void Selected()
        {
        }

        public override void Deselected()
        {
            if(Main.ConveyorNetworkView.Compute.GridsForEvents.Count > 0)
            {
                Main.ConveyorNetworkView.StopShowing(notify: false);
            }
        }

        void RefreshDescription()
        {
            var sb = Description.Builder.Clear();

            MultiTool.ControlPrimary.GetBind(sb);
            sb.AppendLine(" towards a grid");

            MultiTool.ControlSecondary.GetBind(sb);
            sb.AppendLine(" to hide");

            bool shown = Main.ConveyorNetworkView.Compute.GridsForEvents.Count > 0;
            if(shown)
            {
                sb.Append("Networks: ").Append(Main.ConveyorNetworkView.Compute.Networks).AppendLine();
                sb.Append("Conveyor blocks: ").Append(Main.ConveyorNetworkView.Compute.ConveyorBlocks).AppendLine();
            }

            Description.UpdateFromBuilder();
        }

        public override void Update(bool inputReadable)
        {
            if(MultiTool.IsUIVisible && Main.Tick % 15 == 0)
            {
                RefreshDescription();
            }

            if(inputReadable)
            {
                bool shown = Main.ConveyorNetworkView.Compute.GridsForEvents.Count > 0;

                if(shown && MultiTool.ControlSecondary.IsJustPressed())
                {
                    Main.ConveyorNetworkView.StopShowing(notify: false);
                }

                if(MultiTool.ControlPrimary.IsJustPressed())
                {
                    IMyCubeGrid aimedGrid = Utils.GetAimedGrid();

                    if(aimedGrid != null)
                    {
                        Main.ConveyorNetworkView.ShowFor(aimedGrid, notify: false);
                    }
                    else
                    {
                        MyAPIGateway.Utilities.ShowNotification("Look at a grid first", 1000, FontsHandler.RedSh);
                    }
                }
            }
        }
    }
}
