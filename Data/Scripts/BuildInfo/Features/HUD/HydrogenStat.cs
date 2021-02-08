using Sandbox.Definitions;
using Sandbox.Game.Entities.Character.Components;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class HydrogenStat : StatBase
    {
        long PrevCharId;

        public HydrogenStat() : base("player_hydrogen")
        {
            UnitSymbol = "L";
        }

        protected override void Update(int tick, bool enabled)
        {
            var chr = MyAPIGateway.Session?.Player?.Character;
            if(chr == null)
                return;

            var comp = chr.Components.Get<MyCharacterOxygenComponent>();
            if(comp == null)
                return;

            if(!enabled)
            {
                MaxValue = 1f;
                CurrentValue = comp.GetGasFillLevel(MyCharacterOxygenComponent.HydrogenId);
                return;
            }

            if(PrevCharId != chr.EntityId || tick % 6 == 0)
            {
                PrevCharId = chr.EntityId;
                MaxValue = 1f;
                var def = (MyCharacterDefinition)chr.Definition;

                foreach(var storage in def.SuitResourceStorage)
                {
                    if(storage.Id == MyCharacterOxygenComponent.HydrogenId)
                    {
                        MaxValue = storage.MaxCapacity;
                        break;
                    }
                }
            }

            CurrentValue = comp.GetGasFillLevel(MyCharacterOxygenComponent.HydrogenId) * MaxValue;
        }
    }
}