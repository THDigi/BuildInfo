using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Definitions;
using VRage.Utils;

namespace Digi.BuildInfo.Features
{
    public class ItemTooltips : ModComponent
    {
        public ItemTooltips(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.NONE;
        }

        protected override void RegisterComponent()
        {
            foreach(var def in MyDefinitionManager.Static.GetAllDefinitions())
            {
                var physDef = def as MyPhysicalItemDefinition;

                if(physDef == null)
                    continue;

                if(Hardcoded.Conveyors_ItemNeedsLargeTube(physDef))
                {
                    physDef.ExtraInventoryTooltipLine.Append("\n\n* Item can not pass through small conveyors.");

                    if(physDef.IconSymbol.HasValue)
                        physDef.IconSymbol = MyStringId.GetOrCompute($"{physDef.IconSymbol.Value.String}\n*");
                    else
                        physDef.IconSymbol = MyStringId.GetOrCompute("*");
                }
            }
        }

        protected override void UnregisterComponent()
        {
            // apparently the items get reset by themselves so I don't have to reset them... yet.
        }
    }
}
