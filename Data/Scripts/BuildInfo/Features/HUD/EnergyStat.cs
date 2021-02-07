using Sandbox.Game;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class EnergyStat : StatBase
    {
        public EnergyStat() : base("player_energy")
        {
            ValueWidth = 2;
            UnitSymbol = "Wh";
            MaxValue = MyEnergyConstants.BATTERY_MAX_CAPACITY * 1000000f; // MW to W
        }

        protected override void Update(int tick)
        {
            var chr = MyAPIGateway.Session?.Player?.Character;
            if(chr == null)
                return;

            CurrentValue = chr.SuitEnergyLevel * MaxValue;
        }
    }
}