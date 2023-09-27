using Sandbox.Game.Components;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class HealthStat : UnitFormatStatBase
    {
        public HealthStat() : base("player_health")
        {
        }

        protected override void UpdateBeforeSim(ref float current, ref float min, ref float max)
        {
            IMyCharacter chr = MyAPIGateway.Session?.Player?.Character;
            if(chr == null)
                return;

            MyCharacterStatComponent statComp = chr.Components.Get<MyEntityStatComponent>() as MyCharacterStatComponent;
            if(statComp == null)
                return;

            if(!Main.Config.HealthOverride.Value)
            {
                current = statComp.HealthRatio;
                min = 0;
                max = 1;
            }
            else
            {
                current = statComp.Health.Value;
                min = statComp.Health.MinValue;
                max = statComp.Health.MaxValue;
            }
        }

        protected override string ValueAsString()
        {
            if(!Main.Config.HealthOverride.Value)
                return $"{CurrentValue * 100f:0}"; // as per MyStatPlayerHealth

            return base.ValueAsString();
        }
    }
}
