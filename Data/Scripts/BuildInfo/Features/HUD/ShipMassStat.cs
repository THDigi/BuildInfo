using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.HUD
{
    public class ShipMassStat : IMyHudStat
    {
        public const string NumberFormat = "###,###,###,###,###,###,##0";

        public MyStringHash Id { get; private set; }
        public float MinValue { get; } = 0f;
        public float MaxValue { get; } = 1f;
        public string GetValueString() => StringValueCache ?? ""; // must never be null

        private float _currentValue = -1;
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
        private string StringValueCache = "...";

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
                IMyTerminalBlock controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
                if(controlled == null)
                {
                    CurrentValue = 0f;
                    PrevGridId = 0;
                    return;
                }

                BuildInfoMod Main = BuildInfoMod.Instance;
                int tick = Main.Tick;
                MyCubeGrid ctrlGrid = (MyCubeGrid)controlled.CubeGrid;

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

                    // HACK: physics mass can be 0 if ship is landing gear'd to another ship and you're a MP client, and who knows what other cases.
                    // this gets the mass of non-static grids and works for MP clients, but it's affected by inventory multiplier so it's not real mass.
                    float baseMass;
                    float physMass;
                    float mass = ctrlGrid.GetCurrentMass(out baseMass, out physMass);

                    // remove the ship inventory multiplier from the number.
                    float invMultiplier = MyAPIGateway.Session.BlocksInventorySizeMultiplier;
                    if(invMultiplier != 1f)
                        mass = ((mass - baseMass) / invMultiplier) + baseMass;

                    // then add static grids' masses + remove pilot inv mass
                    Grids.Clear();
                    MyAPIGateway.GridGroups.GetGroup(ctrlGrid, GridLinkTypeEnum.Physical, Grids);

                    foreach(MyCubeGrid g in Grids)
                    {
                        if(g.Physics == null || !g.Physics.Enabled)
                            continue;

                        // reminder that GetCurrentMass() ignores static grids
                        if(g.IsStatic)
                        {
                            mass += Main.GridMassCompute.GetGridMass(g);
                        }
                        else
                        {
                            // remove pilot's inventory mass
                            foreach(MyCockpit block in g.OccupiedBlocks)
                            {
                                MyInventory inv = block.Pilot?.GetInventory(0);
                                if(inv != null)
                                    mass -= (float)inv.CurrentMass;
                            }
                        }
                    }

                    Grids.Clear();

                    // must be kept as kg because of the "<value> Kg" format in the HUD definition.
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
        public float MinValue { get; } = 0f;
        public float MaxValue { get; } = 1f;
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
                    IMyTerminalBlock controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
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