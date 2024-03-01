using System;
using System.Collections.Generic;
using System.Text;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Localization;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage;
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
        IMyAirVent block;
        bool init = false;
        byte skip = 0;
        bool dummyIsSet = false;
        Vector3 dummyLocalPosition;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if(BuildInfo_GameSession.GetOrComputeIsKilled(this.GetType().Name))
                return;

            if(BuildInfoMod.Instance?.LeakInfo == null)
                return;

            block = (IMyAirVent)Entity;
            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        bool _roomSealed;
        int _roomSealedExpires;
        public bool IsRoomSealed()
        {
            if(MyAPIGateway.Session.IsServer)
                return block.CanPressurize;

            // HACK: cannot use CanPressurize net-client-side because it uses grid GasSystem that is only available serverside.

            int tick = MyAPIGateway.Session.GameplayFrameCounter;

            if(_roomSealedExpires > tick)
                return _roomSealed;

            block.SetDetailedInfoDirty();
            string detailedInfo = block.DetailedInfo;

            if(string.IsNullOrEmpty(detailedInfo))
                return false; // unknown state, assume not pressurized

            string notPressurized = MyTexts.GetString(MySpaceTexts.Oxygen_NotPressurized);
            _roomSealed = !detailedInfo.Contains(notPressurized);
            _roomSealedExpires = tick + Constants.TicksPerSecond * 1;

            return _roomSealed;
        }

        public override void UpdateAfterSimulation10()
        {
            try
            {
                if(!init)
                {
                    if(MyAPIGateway.Utilities.IsDedicated || block?.CubeGrid?.Physics == null) // ignore DS side and ghost grids
                    {
                        NeedsUpdate = MyEntityUpdateEnum.NONE;
                        return;
                    }

                    LeakInfo leakInfo = BuildInfoMod.Instance?.LeakInfo;
                    if(leakInfo == null) // wait until leak info component is assigned
                        return;

                    init = true;

                    block.AppendingCustomInfo += CustomInfo;

                    if(leakInfo.TerminalControl == null)
                    {
                        // separator
                        MyAPIGateway.TerminalControls.AddControl<IMyAirVent>(MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyAirVent>(string.Empty));

                        // on/off switch
                        IMyTerminalControlOnOffSwitch c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyAirVent>("FindAirLeak");
                        c.Title = MyStringId.GetOrCompute("Air Leak (will be removed)");
                        c.Tooltip = MyStringId.GetOrCompute("NOTE: This feature will be removed in the next major version because there is a standalone version now called 'Leak Finder' !\n\nA client-side pathfinding towards an air leak.\nAdded by Build Info mod.");
                        c.OnText = MyStringId.GetOrCompute("Find");
                        c.OffText = MyStringId.GetOrCompute("Hide");
                        c.Enabled = Terminal_Enabled;
                        c.SupportsMultipleBlocks = false;
                        c.Setter = Terminal_Setter;
                        c.Getter = Terminal_Getter;
                        MyAPIGateway.TerminalControls.AddControl<IMyAirVent>(c);
                        leakInfo.TerminalControl = c;
                    }
                }

                if(!dummyIsSet && block.IsFunctional) // needs to be functional to get the dummy from the main model
                {
                    dummyIsSet = true;

                    Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
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
                    LeakInfo leakInfo = BuildInfoMod.Instance.LeakInfo;
                    if(leakInfo.UsedFromVent == block && leakInfo.Status != InfoStatus.None && IsRoomSealed())
                    {
                        leakInfo.ClearStatus();
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void MarkForClose()
        {
            if(block != null)
                block.AppendingCustomInfo -= CustomInfo;
        }

        #region Terminal control handling
        private static bool Terminal_Enabled(IMyTerminalBlock block)
        {
            if(BuildInfoMod.Instance?.LeakInfo == null)
                return false;

            return BuildInfoMod.Instance.LeakInfo.Enabled;
        }

        private static void Terminal_Setter(IMyTerminalBlock block, bool v)
        {
            try
            {
                if(BuildInfoMod.Instance.IsDedicatedServer)
                    return;

                LeakInfo leakInfo = BuildInfoMod.Instance?.LeakInfo;
                if(leakInfo == null || !leakInfo.Enabled)
                    return;

                IMyAirVent vent = (IMyAirVent)block;
                AirVent logic = block.GameLogic.GetAs<AirVent>();
                if(logic == null)
                    return;

                if(leakInfo.Status != InfoStatus.None)
                {
                    leakInfo.ClearStatus();
                }
                else
                {
                    if(!block.IsWorking || logic.IsRoomSealed())
                    {
                        leakInfo.TerminalControl.UpdateVisual();
                        return;
                    }

                    Vector3I startPosition = block.CubeGrid.WorldToGridInteger(Vector3D.Transform(logic.dummyLocalPosition, block.WorldMatrix));

                    leakInfo.StartThread(vent, startPosition);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private static bool Terminal_Getter(IMyTerminalBlock block)
        {
            LeakInfo leakInfo = BuildInfoMod.Instance.LeakInfo;
            if(leakInfo == null)
                return false;

            return (leakInfo.Status != InfoStatus.None);
        }

        private static void CustomInfo(IMyTerminalBlock block, StringBuilder str)
        {
            try
            {
                LeakInfo leakInfo = BuildInfoMod.Instance?.LeakInfo;
                if(leakInfo == null)
                {
                    //str.Append("Not initialized.");
                    return;
                }

                IMyAirVent vent = (IMyAirVent)block;
                AirVent logic = block.GameLogic.GetAs<AirVent>();

                if(logic == null)
                    return;

                str.Append('\n');
                str.Append("Air leak scan status:\n");

                if(!leakInfo.Enabled)
                {
                    str.Append("Disabled.");
                    return;
                }

                switch(leakInfo.Status)
                {
                    case InfoStatus.None:
                        if(!vent.IsWorking)
                            str.Append("Air vent not working.");
                        else if(logic.IsRoomSealed())
                            str.Append("Area is sealed.");
                        else
                            str.Append("Ready.");
                        break;
                    case InfoStatus.Computing:
                        str.Append("Computing...");
                        break;
                    case InfoStatus.Drawing:
                        if(leakInfo.UsedFromVent != null && leakInfo.UsedFromVent.CubeGrid != block.CubeGrid)
                            str.Append("Leak found and displayed on another grid.");
                        else
                            str.Append("Leak found and displayed.");
                        break;
                }

                str.Append("\n");
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        #endregion Terminal control handling
    }
}