using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.Overlays.ConveyorNetwork
{
    public class ConveyorNetworkView : ModComponent
    {
        public const int RescanPeriodically = Constants.TicksPerSecond * 30; // 0 to disable

        internal ConveyorNetworkCompute Compute;
        internal ConveyorNetworkRender Render;

        internal HashSet<IMyCubeGrid> TempGrids = new HashSet<IMyCubeGrid>();

        IMyCubeGrid TargetGrid;
        public IMySlimBlock TargetBlock;

        public int RescanAtTick;

        public ConveyorNetworkView(BuildInfoMod main) : base(main)
        {
            Compute = new ConveyorNetworkCompute(this);
            Render = new ConveyorNetworkRender(this);
        }

        public override void RegisterComponent()
        {
            Compute.Init();
            Render.Init();
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;
        }

        public void Reset()
        {
            TargetGrid = null;
            TargetBlock = null;
            Render.Reset();
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
        }

        public void ShowFor(IMyCubeGrid grid, IMySlimBlock traceFrom = null, bool notify = true)
        {
            Reset();

            TempGrids.Clear();
            try
            {
                MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical, TempGrids);
                if(!Utils.IsShipFriendly(TempGrids))
                {
                    //if(notify)
                    MyAPIGateway.Utilities.ShowNotification($"{ConveyorNetworkCompute.NotifyPrefix}Cannot show, unfriendly ship!", 4000, FontsHandler.RedSh);

                    return;
                }

                bool shouldRender = Compute.FindConveyorNetworks(TempGrids, traceFrom, notify);
                if(shouldRender)
                {
                    TargetGrid = grid;
                    TargetBlock = traceFrom;

                    if(RescanPeriodically > 0)
                        RescanAtTick = Main.Tick + RescanPeriodically;

                    SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
                }
            }
            finally
            {
                TempGrids.Clear();
            }
        }

        public void StopShowing(string customMessage = null)
        {
            Reset();
            Compute.Reset();
            MyAPIGateway.Utilities.ShowNotification($"{ConveyorNetworkCompute.NotifyPrefix}{customMessage ?? "stopped showing"}");
        }

        public void ScheduleRescan()
        {
            RescanAtTick = Main.Tick + 30;
        }

        public override void UpdateDraw()
        {
            if(!Render.IsValid())
            {
                StopShowing();
                return;
            }

            if(RescanAtTick > 0 && RescanAtTick <= Main.Tick)
            {
                if(RescanPeriodically > 0)
                    RescanAtTick = Main.Tick + RescanPeriodically;
                else
                    RescanAtTick = 0;

                ShowFor(TargetGrid, TargetBlock, notify: false);
            }

            if(Main.GameConfig.HudState == Systems.HudState.OFF)
                return;

            //if(!Main.IsPaused)
            //using(new DevProfiler("render", 16)) 
            {
                Render.Draw();
            }
        }
    }
}
