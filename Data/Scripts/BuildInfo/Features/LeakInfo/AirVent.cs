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
using static Digi.BuildInfo.Features.LeakInfo.LeakInfo;

namespace Digi.BuildInfo.Features.LeakInfo
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_AirVent), useEntityUpdate: false)]
    public class AirVent : MyGameLogicComponent
    {
        private LeakInfo LeakInfo => BuildInfoMod.Client.LeakInfo;

        private IMyAirVent block;
        private bool init = false;
        private byte skip = 0;
        private bool dummyIsSet = false;
        private Vector3 dummyLocalPosition;

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
                    if(!BuildInfoMod.Instance.Started)
                        return;

                    if(BuildInfoMod.Instance.IsDS || block.CubeGrid.Physics == null) // ignore DS side and ghost grids
                    {
                        NeedsUpdate = MyEntityUpdateEnum.NONE;
                        return;
                    }

                    if(LeakInfo == null) // wait until leak info component is assigned
                        return;

                    init = true;

                    block.AppendingCustomInfo += CustomInfo;

                    if(LeakInfo.TerminalControl == null)
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
                        LeakInfo.TerminalControl = c;
                    }
                }

                if(!dummyIsSet && block.IsFunctional) // needs to be functional to get the dummy from the main model
                {
                    dummyIsSet = true;

                    var dummies = BuildInfoMod.Caches.Dummies;
                    dummies.Clear();

                    IMyModelDummy dummy;
                    if(block.Model.GetDummies(dummies) > 0 && dummies.TryGetValue(VanillaData.Hardcoded.AirVent_DummyName, out dummy))
                    {
                        dummyLocalPosition = dummy.Matrix.Translation;
                    }

                    dummies.Clear();
                }

                if(++skip > 6) // every second
                {
                    skip = 0;

                    // if room is sealed and the leak info is running then clear it
                    if(LeakInfo.UsedFromVent == block && LeakInfo.Status != ThreadStatus.IDLE && block.CanPressurize)
                    {
                        LeakInfo.ClearStatus();
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

        #region Terminal control handling
        private static bool Terminal_Enabled(IMyTerminalBlock block)
        {
            return BuildInfoMod.Client.LeakInfo.Enabled;
        }

        private static void Terminal_Setter(IMyTerminalBlock block, bool v)
        {
            try
            {
                if(!BuildInfoMod.Instance.Started || BuildInfoMod.Instance.IsDS)
                    return;

                var leakInfo = BuildInfoMod.Client.LeakInfo;

                if(!leakInfo.Enabled)
                    return;

                var vent = (IMyAirVent)block;
                var logic = block.GameLogic.GetAs<AirVent>();

                if(logic == null)
                    return;

                if(leakInfo.Status != ThreadStatus.IDLE)
                {
                    leakInfo.ClearStatus();
                    leakInfo.ViewedVentControlPanel?.RefreshCustomInfo();
                }
                else
                {
                    if(!block.IsWorking || vent.CanPressurize)
                    {
                        leakInfo.TerminalControl.UpdateVisual();
                        return;
                    }

                    var startPosition = block.CubeGrid.WorldToGridInteger(Vector3D.Transform(logic.dummyLocalPosition, block.WorldMatrix));

                    leakInfo.StartThread(vent, startPosition);
                    leakInfo.ViewedVentControlPanel?.RefreshCustomInfo();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private static bool Terminal_Getter(IMyTerminalBlock block)
        {
            var leakInfo = BuildInfoMod.Client.LeakInfo;
            return (leakInfo.Status != ThreadStatus.IDLE);
        }

        private static void CustomInfo(IMyTerminalBlock block, StringBuilder str)
        {
            try
            {
                var vent = (IMyAirVent)block;
                var logic = block.GameLogic.GetAs<AirVent>();

                if(logic == null)
                    return;

                str.Append('\n');
                str.Append("Air leak scan status:\n");

                var leakInfo = BuildInfoMod.Client.LeakInfo;

                if(!leakInfo.Enabled)
                {
                    str.Append("Disabled.");
                    return;
                }

                switch(leakInfo.Status)
                {
                    case ThreadStatus.IDLE:
                        if(!vent.IsWorking)
                            str.Append("Air vent not working.");
                        else if(vent.CanPressurize)
                            str.Append("Area is sealed.");
                        else
                            str.Append("Ready.");
                        break;
                    case ThreadStatus.RUNNING:
                        str.Append("Computing...");
                        break;
                    case ThreadStatus.DRAW:
                        if(leakInfo.UsedFromVent != null && leakInfo.UsedFromVent.CubeGrid != block.CubeGrid)
                            str.Append("Leak found and displayed on another grid.");
                        else
                            str.Append("Leak found and displayed.");
                        break;
                }

                str.Append("\n\n");
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        #endregion Terminal control handling
    }
}