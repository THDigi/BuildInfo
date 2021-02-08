using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class ShipHydrogenStat : UnitFormatStatBase
    {
        long PrevGridId = 0;
        List<IMyGasTank> HydrogenTanks = new List<IMyGasTank>();

        public ShipHydrogenStat() : base("controlled_hydrogen_capacity")
        {
            UnitSymbol = "L";
        }

        protected override void Update(int tick, bool enabled)
        {
            IMyTerminalBlock controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
            if(controlled == null)
            {
                MaxValue = 0f;
                CurrentValue = 0f;
                HydrogenTanks.Clear();
                return;
            }

            var grid = (MyCubeGrid)controlled.CubeGrid;

            if(PrevGridId != grid.EntityId || tick % 6 * 3 == 0)
            {
                PrevGridId = grid.EntityId;

                HydrogenTanks.Clear();

                // TODO: get ones on rotors/pistons too? remember to disable that via config if adding

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
                MaxValue = 0f;
                CurrentValue = 0f;
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

            if(!enabled)
            {
                MaxValue = max;
                CurrentValue = filled;
                return;
            }

            MaxValue = max * 1000; // m3 to L
            CurrentValue = filled * 1000;
        }
    }
}