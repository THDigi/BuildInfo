using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class HydrogenBottleStat : BottleStatBase
    {
        public HydrogenBottleStat() : base("player_hydrogen_bottles", MyResourceDistributorComponent.HydrogenId)
        {
            UpdateAtTick = 14; // offset updates to spread things out more
        }
    }

    public class OxygenBottleStat : BottleStatBase
    {
        public OxygenBottleStat() : base("player_oxygen_bottles", MyResourceDistributorComponent.OxygenId)
        {
            UpdateAtTick = 44;
        }
    }

    public abstract class BottleStatBase : HudStatBase
    {
        protected MyDefinitionId Gas { get; set; }

        /// <summary>
        /// Logic on this runs once per second regardless, but this field controls which tick it runs at.
        /// Must not be larger than 59.
        /// </summary>
        protected int UpdateAtTick = 0;

        protected BottleStatBase(string id, MyDefinitionId gas) : base(id)
        {
            Gas = gas;
        }

        protected override string ValueAsString() => CurrentValue.ToString();

        protected override void UpdateBeforeSim(ref float current, ref float min, ref float max)
        {
            if(Main.Tick % 60 != UpdateAtTick)
                return;

            IMyCharacter chr = MyAPIGateway.Session?.Player?.Character;
            MyInventory inv = chr?.GetInventory() as MyInventory;

            current = 0;

            if(inv == null)
                return;

            foreach(MyPhysicalInventoryItem item in inv.GetItems())
            {
                MyObjectBuilder_GasContainerObject gasContainer = item.Content as MyObjectBuilder_GasContainerObject;
                if(gasContainer != null && gasContainer.GasLevel > 1e-06f)
                {
                    MyOxygenContainerDefinition def = MyDefinitionManager.Static.GetPhysicalItemDefinition(item.Content.GetId()) as MyOxygenContainerDefinition;
                    if(def != null && def.StoredGasId == Gas)
                    {
                        current += (float)item.Amount;
                    }
                }
            }
        }
    }
}