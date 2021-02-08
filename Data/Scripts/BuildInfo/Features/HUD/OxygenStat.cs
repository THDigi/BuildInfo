using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class OxygenStat : StatBase
    {
        public OxygenStat() : base("player_oxygen")
        {
            UnitSymbol = "L";
        }

        protected override void Update(int tick)
        {
            var chr = MyAPIGateway.Session?.Player?.Character;
            if(chr == null)
                return;

            var comp = chr.Components.Get<MyCharacterOxygenComponent>();
            if(comp == null)
                return;

            if(!BuildInfoMod.Instance.Config.HudStatOverrides.Value)
            {
                MaxValue = 1f;
                CurrentValue = comp.SuitOxygenLevel;
                return;
            }

            MaxValue = comp.OxygenCapacity; // NOTE: max must be set first to declare unit multipliers
            CurrentValue = comp.SuitOxygenAmount;
        }
    }
}