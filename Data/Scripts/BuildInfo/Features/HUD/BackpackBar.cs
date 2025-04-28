using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.HUD
{
    public class BackpackBarStat : HudStatBase
    {
        public const string GroupName = "Cargo";
        public const string TextFormat = "###,###,###,###,##0.##";

        int Containers = 0;
        bool WasInShip = false;
        bool UsingGroup;

        static readonly List<IMyCubeGrid> TempGrids = new List<IMyCubeGrid>();
        static readonly List<IMyTerminalBlock> TempBlocks = new List<IMyTerminalBlock>();

        public BackpackBarStat() : base("player_inventory_capacity")
        {
        }

        protected override void UpdateBeforeSim(ref float current, ref float min, ref float max)
        {
            try
            {
                bool enabled = Main.Config.BackpackBarOverride.Value;

                if(enabled && MyAPIGateway.Session?.ControlledObject == null)
                {
                    max = 0f;
                    current = 0f;

                    Containers = 0;
                    WasInShip = false;
                    return;
                }

                IMyTerminalBlock controlled = (enabled ? MyAPIGateway.Session.ControlledObject as IMyTerminalBlock : null);

                if(controlled == null) // if feature is disabled or not in a ship
                {
                    if(WasInShip || BuildInfoMod.Instance.Tick % 10 == 0)
                    {
                        WasInShip = false;

                        IMyInventory inventory = MyAPIGateway.Session?.Player?.Character?.GetInventory();
                        if(inventory != null)
                        {
                            Containers = 1;
                            max = MyFixedPoint.MultiplySafe(inventory.MaxVolume, 1000).ToIntSafe();
                            current = MyFixedPoint.MultiplySafe(inventory.CurrentVolume, 1000).ToIntSafe();
                        }
                    }

                    return;
                }

                if(BuildInfoMod.Instance.Tick % 60 != 0 && WasInShip)
                    return;

                max = 0f;
                current = 0f;

                Containers = 0;
                WasInShip = true;

                float currentTotal = 0;
                float maxTotal = 0;

                IMyGridTerminalSystem gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(controlled.CubeGrid);
                if(gts == null)
                    return;

                IMyBlockGroup group = gts.GetBlockGroupWithName(GroupName);
                UsingGroup = (group != null);
                if(UsingGroup)
                {
                    TempBlocks.Clear();
                    group.GetBlocks(TempBlocks);

                    foreach(IMyTerminalBlock block in TempBlocks)
                    {
                        if(!block.IsSameConstructAs(controlled))
                            continue;

                        Containers++;

                        for(int i = 0; i < block.InventoryCount; i++)
                        {
                            IMyInventory inv = block.GetInventory(i);
                            currentTotal += (float)inv.CurrentVolume * 1000; // add as liters
                            maxTotal += (float)inv.MaxVolume * 1000;
                        }
                    }
                }
                else
                {
                    TempGrids.Clear();
                    MyAPIGateway.GridGroups.GetGroup(controlled.CubeGrid, GridLinkTypeEnum.Logical, TempGrids);

                    foreach(MyCubeGrid grid in TempGrids)
                    {
                        int containers = grid.BlocksCounters.GetValueOrDefault(typeof(MyObjectBuilder_CargoContainer));
                        if(containers == 0)
                            continue;

                        foreach(MyCubeBlock block in grid.GetFatBlocks())
                        {
                            if(!(block is IMyCargoContainer))
                                continue;

                            Containers++;

                            for(int i = 0; i < block.InventoryCount; i++)
                            {
                                IMyInventory inv = block.GetInventory(i);
                                currentTotal += (float)inv.CurrentVolume * 1000; // add as liters
                                maxTotal += (float)inv.MaxVolume * 1000;
                            }
                        }
                    }
                }

                max = maxTotal;
                current = currentTotal;
            }
            finally
            {
                TempGrids.Clear();
                TempBlocks.Clear();
            }
        }

        protected override string ValueAsString()
        {
            bool enabled = BuildInfoMod.Instance.Config.BackpackBarOverride.Value;
            if(!enabled)
                return CurrentValue.ToString(TextFormat);

            // TODO: toggle string formatting?
            if(WasInShip)
            {
                // TODO: force this to show up for vanilla HUD? with toggleable option ofc

                if(UsingGroup)
                    return $"'{GroupName}' group: {CurrentValue.ToString(TextFormat)} / {MaxValue.ToString(TextFormat)}";
                else
                    return $"{Containers.ToString()} containers: {CurrentValue.ToString(TextFormat)} / {MaxValue.ToString(TextFormat)}";
            }
            else
                return $"Backpack: {CurrentValue.ToString(TextFormat)} / {MaxValue.ToString(TextFormat)}";
        }
    }
}
