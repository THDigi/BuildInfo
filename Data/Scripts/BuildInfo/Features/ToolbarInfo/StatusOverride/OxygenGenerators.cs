using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarInfo.StatusOverride
{
    internal class OxygenGenerators : StatusOverrideBase
    {
        public OxygenGenerators(ToolbarStatusProcessor processor) : base(processor)
        {
            var type = typeof(MyObjectBuilder_OxygenGenerator);

            processor.AddStatus(type, Refill, "Refill");

            // TODO: group support
            //processor.AddGroupStatus(type, StatusGroup, "Refill");
        }

        bool Refill(StringBuilder sb, ToolbarItem item)
        {
            var generator = (IMyGasGenerator)item.Block;
            int itemsToFill = 0;
            bool hasIce = false;

            if(generator.IsWorking && MyAPIGateway.Session.SessionSettings.EnableOxygen)
            {
                bool makesOxygen = false;
                var generatorDef = (MyOxygenGeneratorDefinition)generator.SlimBlock.BlockDefinition;

                foreach(var gas in generatorDef.ProducedGases)
                {
                    if(gas.Id == MyResourceDistributorComponent.OxygenId)
                    {
                        makesOxygen = true;
                        break;
                    }
                }

                if(makesOxygen)
                {
                    var inv = generator.GetInventory(0) as MyInventory;
                    if(inv != null)
                    {
                        foreach(var physItem in inv.GetItems())
                        {
                            var containerOB = physItem.Content as MyObjectBuilder_GasContainerObject;
                            if(containerOB == null)
                                hasIce = true; // HACK: similar behavior to game's internals: anything non-bottle is fuel
                            else if(containerOB.GasLevel < 1f)
                                itemsToFill++;
                        }
                    }
                }
            }

            if(hasIce && itemsToFill > 0)
            {
                sb.Append("Refill:").Append('\n').Append(itemsToFill);
            }
            else
            {
                sb.Append(hasIce ? "No Bottles" : "No Ice");
            }

            return true;
        }

        //bool StatusGroup(StringBuilder sb, ToolbarItem item, GroupData groupData)
        //{
        //    if(!groupData.GetGroupBlocks<IMyOxygenGenerator>())
        //        return false;

        //    return true;
        //}
    }
}
