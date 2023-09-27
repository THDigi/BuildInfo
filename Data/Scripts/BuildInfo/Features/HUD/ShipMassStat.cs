using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class ShipMassStat : HudStatBase
    {
        public const string NumberFormat = "###,###,###,###,###,###,##0";
        public const int UpdateOffset = 29; // offset updates to spread things out more for things that run every second

        public static bool ShowCustomSuffix = false;

        long PrevGridId;
        Config.MassFormat PrevFormatType;

        static readonly HashSet<IMyCubeGrid> TempGrids = new HashSet<IMyCubeGrid>();
        static readonly StringBuilder TempSB = new StringBuilder(32);

        public ShipMassStat() : base("controlled_mass")
        {
        }

        protected override string ValueAsString()
        {
            var format = Main.Config.MassOverride.ValueEnum;
            if(format == Config.MassFormat.Vanilla)
                return CurrentValue.ToString("0.00"); // as per MyStatControlledEntityMass

            if(ShowCustomSuffix && format == Config.MassFormat.RealCustomSuffix)
                return TempSB.Clear().MassFormat(CurrentValue).ToString();

            return CurrentValue.ToString(NumberFormat);
        }

        protected override void UpdateBeforeSim(ref float current, ref float min, ref float max)
        {
            IMyTerminalBlock controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
            if(controlled == null)
            {
                current = 0;
                PrevGridId = 0;
                return;
            }

            MyCubeGrid ctrlGrid = (MyCubeGrid)controlled.CubeGrid;

            // update once a second or if we've changed grids
            if(Main.Tick % 60 != UpdateOffset && PrevGridId == ctrlGrid.EntityId)
                return;

            PrevGridId = ctrlGrid.EntityId;

            Config.MassFormat formatType = Main.Config.MassOverride.ValueEnum;

            if(formatType != PrevFormatType)
                InvalidateStringCache();
            PrevFormatType = formatType;

            if(formatType == Config.MassFormat.Vanilla)
            {
                current = ctrlGrid.IsStatic ? 0f : ctrlGrid.GetCurrentMass();
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
            TempGrids.Clear();
            MyAPIGateway.GridGroups.GetGroup(ctrlGrid, GridLinkTypeEnum.Physical, TempGrids);

            foreach(MyCubeGrid g in TempGrids)
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

            TempGrids.Clear();

            current = mass;
        }
    }

    // prevent HUD from showing "Station" and allows the ShipMassStat to show mass instead.
    public class ShipIsStatic : HudStatBase
    {
        public ShipIsStatic() : base("controlled_is_static")
        {
        }

        protected override void UpdateBeforeSim(ref float current, ref float min, ref float max)
        {
            current = 0f;

            if(Main.Config.MassOverride.ValueEnum == Config.MassFormat.Vanilla)
            {
                IMyTerminalBlock controlled = MyAPIGateway.Session.ControlledObject as IMyTerminalBlock;
                if(controlled != null)
                {
                    current = (controlled.CubeGrid.IsStatic ? 1 : 0);
                }
            }
        }

        protected override string ValueAsString() => (CurrentValue > 0.5f ? "1" : "0");
    }
}