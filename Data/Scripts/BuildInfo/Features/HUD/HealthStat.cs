using Sandbox.Game.Components;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class HealthStat : StatBase
    {
        public HealthStat() : base("player_health")
        {
        }

        protected override void Update(int tick)
        {
            var chr = MyAPIGateway.Session?.Player?.Character;
            if(chr == null)
                return;

            var statComp = chr.Components.Get<MyEntityStatComponent>() as MyCharacterStatComponent;
            if(statComp == null)
                return;

            if(!BuildInfoMod.Instance.Config.HudStatOverrides.Value)
            {
                MaxValue = 1f;
                MinValue = 0f;
                CurrentValue = statComp.HealthRatio;
                return;
            }

            MaxValue = statComp.Health.MaxValue; // NOTE: max must be set first to declare unit multipliers
            MinValue = statComp.Health.MinValue;
            CurrentValue = statComp.Health.Value;
        }
    }
}
