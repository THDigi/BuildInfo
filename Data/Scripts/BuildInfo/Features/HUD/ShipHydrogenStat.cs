using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class ShipHydrogenStat : StatBase
    {
        long PrevGridId = 0;
        List<IMyGasTank> HydrogenTanks = new List<IMyGasTank>();

        public ShipHydrogenStat() : base("controlled_hydrogen_capacity")
        {
            UnitSymbol = "L";
        }

        protected override void Update(int tick)
        {
            IMyTerminalBlock controlled = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(controlled == null)
                controlled = MyAPIGateway.Session.ControlledObject as IMyLargeTurretBase;

            if(controlled == null)
            {
                CurrentValue = 0f;
                MaxValue = 0f;
                HydrogenTanks.Clear();
                return;
            }

            var grid = (MyCubeGrid)controlled.CubeGrid;

            if(PrevGridId != grid.EntityId || tick % 6 * 3 == 0)
            {
                PrevGridId = grid.EntityId;

                HydrogenTanks.Clear();

                foreach(var block in grid.GetFatBlocks())
                {
                    var tank = block as IMyGasTank;
                    if(tank == null)
                        continue;

                    var sink = block.Components.Get<MyResourceSinkComponent>();
                    if(sink == null)
                        continue;

                    foreach(var gasId in sink.AcceptedResources)
                    {
                        if(gasId == MyResourceDistributorComponent.HydrogenId)
                        {
                            HydrogenTanks.Add(tank);
                            break;
                        }
                    }
                }
            }

            if(HydrogenTanks.Count == 0)
            {
                CurrentValue = 0f;
                MaxValue = 0f;
                return;
            }

            float max = 0f;
            float filled = 0f;

            for(int i = (HydrogenTanks.Count - 1); i >= 0; i--)
            {
                IMyGasTank tank = HydrogenTanks[i];
                if(tank.MarkedForClose)
                {
                    HydrogenTanks.RemoveAtFast(i);
                    continue;
                }

                float capacity = tank.Capacity;
                max += tank.Capacity;
                filled += ((float)tank.FilledRatio * capacity);
            }

            MaxValue = max;
            CurrentValue = filled;
        }
    }
}