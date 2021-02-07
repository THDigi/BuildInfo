using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.HUD
{
    public class ShipMassStat : IMyHudStat
    {
        public MyStringHash Id { get; private set; } = MyStringHash.GetOrCompute("controlled_mass");
        public float CurrentValue { get; private set; }
        public float MinValue => 0f;
        public float MaxValue => 1f;

        private long PrevGridId;
        private HashSet<IMyCubeGrid> Grids = new HashSet<IMyCubeGrid>();

        public ShipMassStat()
        {
        }

        public string GetValueString()
        {
            // HACK: can't add unit because HUD hardcodes unit into itself.
            return CurrentValue.ToString("###,###,###,###,###,###,###");
        }

        public void Update()
        {
            var controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
            if(controlled == null || controlled.CubeGrid.IsStatic)
            {
                CurrentValue = 0f;
                PrevGridId = 0;
                return;
            }

            int tick = BuildInfoMod.Instance.Tick;
            var ctrlGrid = (MyCubeGrid)controlled.CubeGrid;

            if(PrevGridId != ctrlGrid.EntityId || tick % 60 == 0)
            {
                PrevGridId = ctrlGrid.EntityId;

                // TODO: use
                //float mass = 0;

                //Grids.Clear();
                //MyAPIGateway.GridGroups.GetGroup(ctrlGrid, GridLinkTypeEnum.Physical, Grids);

                //foreach(IMyCubeGrid g in Grids)
                //{
                //    if(g.Physics == null || !g.Physics.Enabled)
                //        continue;

                //    if(g.IsStatic)
                //    {
                //        //using(new DevProfiler("CalculateStaticGridMass()")) // DEBUG profiling
                //        //{
                //        //    mass += Utils.CalculateStaticGridMass(g); // DEBUG << needs caching or something...
                //        //}

                //        using(new DevProfiler("StaticGridMassCache")) // DEBUG profiling
                //        {
                //            mass += BuildInfoMod.Instance.StaticGridMassCache.GetStaticGridMass(g);
                //        }
                //    }
                //    else
                //    {
                //        mass += g.Physics.Mass;
                //    }
                //}

                //Grids.Clear();

                float baseMass;
                float physMass;
                float mass = ctrlGrid.GetCurrentMass(out baseMass, out physMass);

                // must be kept as kg because of the "<value> Kg" in the HUD definition.
                CurrentValue = physMass;
            }
        }
    }

    // TODO: use
    // prevent HUD from showing "Station" and allows the ShipMassStat to show mass instead.
    //public class ShipIsStatic : IMyHudStat
    //{
    //    public MyStringHash Id { get; private set; } = MyStringHash.GetOrCompute("controlled_is_static");
    //    public float CurrentValue => 0;
    //    public float MinValue => 0f;
    //    public float MaxValue => 1f;
    //    public string GetValueString() => "0";

    //    public ShipIsStatic() { }
    //    public void Update() { }
    //}
}