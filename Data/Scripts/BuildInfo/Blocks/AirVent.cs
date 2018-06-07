using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Blocks
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_AirVent), useEntityUpdate: false)]
    public class BlockAirVent : MyGameLogicComponent
    {
        private IMyAirVent block;
        private LeakInfoComponent leakInfoComp;
        private bool init = false;
        private byte skip = 0;
        private Vector3 dummyLocalPosition;
        private bool dummyIsSet = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyAirVent)Entity;
            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateAfterSimulation10()
        {
            try
            {
                if(!init)
                {
                    if(BuildInfo.Instance == null || !BuildInfo.Instance.IsInitialized)
                        return;

                    if(!BuildInfo.Instance.IsPlayer || block.CubeGrid.Physics == null) // ignore DS side and ghost grids
                    {
                        NeedsUpdate = MyEntityUpdateEnum.NONE;
                        return;
                    }

                    if(leakInfoComp == null)
                    {
                        leakInfoComp = BuildInfo.Instance.LeakInfoComp;

                        if(leakInfoComp == null) // wait until leak info component is assigned
                            return;
                    }

                    init = true;

                    block.AppendingCustomInfo += CustomInfo;

                    if(leakInfoComp.TerminalControl == null)
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
                        c.Enabled = Terminal_Enabled;
                        c.SupportsMultipleBlocks = false;
                        c.Setter = Terminal_Setter;
                        c.Getter = Terminal_Getter;
                        MyAPIGateway.TerminalControls.AddControl<IMyAirVent>(c);
                        leakInfoComp.TerminalControl = c;
                    }
                }

                if(!dummyIsSet && block.IsFunctional) // needs to be functional to get the dummy from the main model
                {
                    dummyIsSet = true;

                    var dummies = BuildInfo.Instance.dummies;
                    dummies.Clear();

                    IMyModelDummy dummy;
                    if(block.Model.GetDummies(dummies) > 0 && dummies.TryGetValue(GameData.Hardcoded.AirVent_DummyName, out dummy))
                    {
                        dummyLocalPosition = dummy.Matrix.Translation;
                    }

                    dummies.Clear();
                }

                if(++skip > 6) // every second
                {
                    skip = 0;

                    // if room is sealed and the leak info is running then clear it
                    if(leakInfoComp.UsedFromVent == block && leakInfoComp.Status != LeakInfoComponent.ThreadStatus.IDLE && block.CanPressurize)
                    {
                        leakInfoComp.ClearStatus();
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Close()
        {
            block.AppendingCustomInfo -= CustomInfo;
        }

        private bool Terminal_Enabled(IMyTerminalBlock b)
        {
            return leakInfoComp.Enabled;
        }

        private void Terminal_Setter(IMyTerminalBlock b, bool v)
        {
            try
            {
                if(BuildInfo.Instance == null || !BuildInfo.Instance.IsPlayer || leakInfoComp == null || !leakInfoComp.Enabled)
                    return;

                var block = (IMyAirVent)b;

                if(leakInfoComp.Status != LeakInfoComponent.ThreadStatus.IDLE)
                {
                    leakInfoComp.ClearStatus();
                    leakInfoComp.ViewedVentControlPanel?.RefreshCustomInfo();
                }
                else
                {
                    if(!block.IsWorking || block.CanPressurize)
                    {
                        leakInfoComp.TerminalControl.UpdateVisual();
                        return;
                    }

                    var startPosition = block.CubeGrid.WorldToGridInteger(Vector3D.Transform(dummyLocalPosition, block.WorldMatrix));

                    leakInfoComp.StartThread(block, startPosition);
                    leakInfoComp.ViewedVentControlPanel?.RefreshCustomInfo();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private bool Terminal_Getter(IMyTerminalBlock b)
        {
            return leakInfoComp != null && leakInfoComp.Status != LeakInfoComponent.ThreadStatus.IDLE;
        }

        private void CustomInfo(IMyTerminalBlock b, StringBuilder str)
        {
            try
            {
                var block = (IMyAirVent)b;

                if(leakInfoComp != null)
                {
                    str.Append('\n');
                    str.Append("Air leak scan status:\n");

                    if(!leakInfoComp.Enabled)
                    {
                        str.Append("Disabled.");
                        return;
                    }

                    switch(leakInfoComp.Status)
                    {
                        case LeakInfoComponent.ThreadStatus.IDLE:
                            if(!block.IsWorking)
                                str.Append("Air vent not working.");
                            else if(block.CanPressurize)
                                str.Append("Area is sealed.");
                            else
                                str.Append("Ready to scan.");
                            break;
                        case LeakInfoComponent.ThreadStatus.RUNNING:
                            str.Append("Computing...");
                            break;
                        case LeakInfoComponent.ThreadStatus.DRAW:
                            str.Append("Leak found and displayed.");
                            break;
                    }

                    str.Append("\n\n");
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}