using Sandbox.Game;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class EnergyStat : UnitFormatStatBase
    {
        public EnergyStat() : base("player_energy")
        {
            ValueWidth = 2;
            UnitSymbol = "Wh";
        }

        protected override void Update(int tick, bool enabled)
        {
            var chr = MyAPIGateway.Session?.Player?.Character;
            if(chr == null)
                return;

            if(!enabled)
            {
                MaxValue = 1f;
                CurrentValue = chr.SuitEnergyLevel;
                return;
            }

            MaxValue = MyEnergyConstants.BATTERY_MAX_CAPACITY * 1000000f; // HACK: character battery capacity is hardcoded in game
            CurrentValue = chr.SuitEnergyLevel * MaxValue;
        }
    }
}