using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.HUD
{
    public class ShipMassStat : IMyHudStat
    {
        public const string NumberFormat = "###,###,###,###,###,###,###";

        public MyStringHash Id { get; private set; }
        public float MinValue => 0f;
        public float MaxValue => 1f;
        public string GetValueString() => StringValueCache ?? ""; // must never be null

        private float _currentValue;
        public float CurrentValue
        {
            get { return _currentValue; }
            set
            {
                if(_currentValue != value)
                {
                    _currentValue = value;
                    StringValueCache = value.ToString(NumberFormat);
                }
            }
        }

        private long PrevGridId;
        private HashSet<IMyCubeGrid> Grids = new HashSet<IMyCubeGrid>();
        private string StringValueCache = "";

        public ShipMassStat()
        {
            if(!BuildInfo_GameSession.IsKilled)
                Id = MyStringHash.GetOrCompute("controlled_mass");
        }

        public void Update()
        {
            if(BuildInfo_GameSession.IsKilled)
                return;

            try
            {
                var controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
                if(controlled == null)
                {
                    CurrentValue = 0f;
                    PrevGridId = 0;
                    return;
                }

                var Main = BuildInfoMod.Instance;
                int tick = Main.Tick;
                var ctrlGrid = (MyCubeGrid)controlled.CubeGrid;

                if(PrevGridId != ctrlGrid.EntityId || tick % 60 == 0)
                {
                    PrevGridId = ctrlGrid.EntityId;

                    if(!Main.Config.HudStatOverrides.Value)
                    {
                        if(!ctrlGrid.IsStatic)
                            CurrentValue = ctrlGrid.GetCurrentMass();
                        else
                            CurrentValue = 0;
                        return;
                    }

                    float mass = 0;

                    Grids.Clear();
                    MyAPIGateway.GridGroups.GetGroup(ctrlGrid, GridLinkTypeEnum.Physical, Grids);

                    foreach(IMyCubeGrid g in Grids)
                    {
                        if(g.Physics == null || !g.Physics.Enabled)
                            continue;

                        float physMass = g.Physics.Mass;

                        if(g.IsStatic && physMass == 0)
                        {
                            mass += Main.StaticGridMassCache.GetStaticGridMass(g);
                        }
                        else
                        {
                            mass += physMass;
                        }
                    }

                    Grids.Clear();

                    // must be kept as kg because of the "<value> Kg" in the HUD definition.
                    CurrentValue = mass;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    // prevent HUD from showing "Station" and allows the ShipMassStat to show mass instead.
    public class ShipIsStatic : IMyHudStat
    {
        public MyStringHash Id { get; private set; }
        public float CurrentValue { get; private set; }
        public float MinValue => 0f;
        public float MaxValue => 1f;
        public string GetValueString() => (CurrentValue > 0.5f ? "1" : "0");

        public ShipIsStatic()
        {
            if(!BuildInfo_GameSession.IsKilled)
                Id = MyStringHash.GetOrCompute("controlled_is_static");
        }

        public void Update()
        {
            if(BuildInfo_GameSession.IsKilled)
                return;

            try
            {
                CurrentValue = 0;

                if(!BuildInfoMod.Instance.Config.HudStatOverrides.Value)
                {
                    var controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
                    if(controlled != null)
                    {
                        CurrentValue = (controlled.CubeGrid.IsStatic ? 1 : 0);
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