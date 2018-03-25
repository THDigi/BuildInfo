using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace Digi.BuildInfo.Blocks
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_AirVent), useEntityUpdate: false)]
    public class AirVent : MyGameLogicComponent
    {
        private byte init = 0; // init states, 0 no init, 1 init events, 2 init with main model (for dummyLocation)
        private byte skip = 0;
        //private Vector3 dummyLocation;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateAfterSimulation10()
        {
            try
            {
                if(BuildInfo.instance == null || BuildInfo.instance.isThisDS)
                    return;

                var leakInfo = BuildInfo.instance.leakInfoComp;

                if(leakInfo == null)
                    return;

                var block = (IMyAirVent)Entity;

                if(init == 0)
                {
                    if(block.CubeGrid.Physics == null || leakInfo == null)
                        return;

                    init = 1;
                    block.AppendingCustomInfo += CustomInfo;

                    if(leakInfo.terminalControl == null)
                    {
                        // separator
                        MyAPIGateway.TerminalControls.AddControl<IMyAirVent>(MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyAirVent>(string.Empty));

                        // on/off switch
                        var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyAirVent>("FindAirLeak");
                        c.Title = MyStringId.GetOrCompute("Air leak scan");
                        //c.Tooltip = MyStringId.GetOrCompute("Finds the path towards an air leak and displays it as blue lines, for a maximum of " + LeakInfoComponent.MAX_DRAW_SECONDS + " seconds.\nTo find the leak it first requires the air vent to be powered, functional, enabled and the room not sealed.\nIt only searches once and doesn't update in realtime. If you alter the ship or open/close doors you need to start it again.\nThe lines are only shown to the player that requests the air leak scan.\nDepending on ship size the computation might take a while, you can cancel at any time however.\nAll air vents control the same system, therefore you can start it from one and stop it from another.\n\nAdded by the Build Info mod.");
                        c.Tooltip = MyStringId.GetOrCompute("A client-side pathfinding towards an air leak.\nAdded by Build Info mod.");
                        c.OnText = MyStringId.GetOrCompute("Find");
                        c.OffText = MyStringId.GetOrCompute("Stop");
                        //c.Enabled = Terminal_Enabled;
                        c.SupportsMultipleBlocks = false;
                        c.Setter = Terminal_Setter;
                        c.Getter = Terminal_Getter;
                        MyAPIGateway.TerminalControls.AddControl<IMyAirVent>(c);
                        leakInfo.terminalControl = c;
                    }
                }

                if(init < 2 && block.IsFunctional) // needs to be functional to get the dummy from the main model
                {
                    init = 2;
                    //const string DUMMY_NAME = "vent_001"; // HACK hardcoded from MyAirVent.VentDummy property
                    //
                    //var dummies = leakInfo.dummies;
                    //dummies.Clear();
                    //
                    //IMyModelDummy dummy;
                    //if(block.Model.GetDummies(dummies) > 0 && dummies.TryGetValue(DUMMY_NAME, out dummy))
                    //    dummyLocation = dummy.Matrix.Translation;
                    //
                    //dummies.Clear();
                }

                if(++skip > 6) // every second
                {
                    skip = 0;

                    // clear the air leak visual display or stop the running thread if the air vent's room is sealed.
                    if(leakInfo.usedFromVent == block && leakInfo.status != LeakInfoComponent.Status.IDLE && block.CanPressurize)
                        leakInfo.ClearStatus();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Close()
        {
            var block = (IMyTerminalBlock)Entity;
            block.AppendingCustomInfo -= CustomInfo;
        }

        // disabled because otherwise you can't stop it manually after you fix the leak and the air vent pressurizes
        //private bool Terminal_Enabled(IMyTerminalBlock b)
        //{
        //    var block = (IMyAirVent)b;
        //    return (block.IsWorking && !block.CanPressurize);
        //}

        private void Terminal_Setter(IMyTerminalBlock b, bool v)
        {
            try
            {
                var leakInfo = BuildInfo.instance.leakInfoComp;

                if(BuildInfo.instance.isThisDS || leakInfo == null)
                    return;

                if(leakInfo.status != LeakInfoComponent.Status.IDLE)
                {
                    leakInfo.ClearStatus();

                    if(leakInfo.viewedVentControlPanel != null)
                        leakInfo.viewedVentControlPanel.RefreshCustomInfo();
                }
                else
                {
                    var block = (IMyAirVent)b;

                    if(!block.IsWorking || block.CanPressurize)
                    {
                        leakInfo.terminalControl.UpdateVisual();
                        return;
                    }

                    //if(!block.IsWorking)
                    //{
                    //    leakInfo.NotifyHUD("Air vent is not working!", font: MyFontEnum.Red);
                    //    return;
                    //}
                    //
                    //if(block.CanPressurize)
                    //{
                    //    leakInfo.NotifyHUD("Area is already sealed!", font: MyFontEnum.Green);
                    //    return;
                    //}

                    //var start = block.CubeGrid.WorldToGridInteger(Vector3D.Transform(dummyLocation, block.WorldMatrix));

                    leakInfo.StartThread(block.CubeGrid, block.Position);
                    leakInfo.usedFromVent = block;

                    if(leakInfo.viewedVentControlPanel != null)
                        leakInfo.viewedVentControlPanel.RefreshCustomInfo();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private bool Terminal_Getter(IMyTerminalBlock b)
        {
            var leakInfo = BuildInfo.instance.leakInfoComp;
            return leakInfo != null && leakInfo.status != LeakInfoComponent.Status.IDLE;
        }

        private void CustomInfo(IMyTerminalBlock b, StringBuilder str)
        {
            try
            {
                var block = (IMyAirVent)b;
                var leakInfo = BuildInfo.instance.leakInfoComp;

                if(leakInfo != null)
                {
                    str.Append('\n');
                    str.Append("Air leak scan status:\n");

                    switch(leakInfo.status)
                    {
                        case LeakInfoComponent.Status.IDLE:
                            if(!block.IsWorking)
                                str.Append("Air vent not working.");
                            else if(block.CanPressurize)
                                str.Append("Area is sealed.");
                            else
                                str.Append("Ready to scan.");
                            break;
                        case LeakInfoComponent.Status.RUNNING:
                            str.Append("Computing...");
                            break;
                        case LeakInfoComponent.Status.DRAW:
                            str.Append("Leak found and displayed.");
                            break;
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