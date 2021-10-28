using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.HUD
{
    public class BackpackBarStat : IMyHudStat
    {
        public const string GroupName = "Cargo";
        public const int UpdateFrequencyTicks = (int)(Constants.TicksPerSecond * 1.0);
        public const string TextFormat = "###,###,###,###,##0.##";

        public MyStringHash Id { get; private set; }
        public float MinValue { get; private set; }
        public float MaxValue { get; private set; }
        public string GetValueString() => StringValueCache ?? ""; // must never be null

        private float _currentValue;
        public float CurrentValue
        {
            get { return _currentValue; }
            set
            {
                if(BuildInfo_GameSession.IsKilled)
                    return;

                if(_currentValue != value)
                {
                    _currentValue = value;
                    StringValueCache = ComputeLabel();
                }
            }
        }

        string StringValueCache = "...";
        int Containers = 0;
        bool WasInShip = false;
        bool UsingGroup;

        public BackpackBarStat()
        {
            if(!BuildInfo_GameSession.IsKilled)
                Id = MyStringHash.GetOrCompute("player_inventory_capacity");
        }

        public void Update()
        {
            if(BuildInfo_GameSession.IsKilled)
                return;

            try
            {
                bool enabled = BuildInfoMod.Instance.Config.BackpackBarOverride.Value;
                if(enabled && MyAPIGateway.Session?.ControlledObject == null)
                {
                    MaxValue = 0f;
                    CurrentValue = 0f;
                    Containers = 0;
                    WasInShip = false;
                    return;
                }

                IMyTerminalBlock controlled = (enabled ? MyAPIGateway.Session.ControlledObject as IMyTerminalBlock : null);
                if(controlled != null)
                {
                    if(!WasInShip || BuildInfoMod.Instance.Tick % UpdateFrequencyTicks == 0)
                    {
                        MaxValue = 0f;
                        CurrentValue = 0f;
                        Containers = 0;
                        WasInShip = true;

                        float currentTotal = 0;
                        float maxTotal = 0;

                        IMyGridTerminalSystem gts = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(controlled.CubeGrid);
                        if(gts == null)
                            return;

                        List<IMyTerminalBlock> blocks = BuildInfoMod.Instance.ShipToolInventoryBar.Blocks;
                        blocks.Clear();

                        IMyBlockGroup group = gts.GetBlockGroupWithName(GroupName);
                        UsingGroup = (group != null);
                        if(UsingGroup)
                            group.GetBlocks(blocks);
                        else
                            gts.GetBlocksOfType<IMyCargoContainer>(blocks);

                        foreach(IMyTerminalBlock block in blocks)
                        {
                            //if(!UsingGroup && !(block is IMyCargoContainer || block is IMyShipConnector || block is IMyCollector))
                            //    continue;

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

                        blocks.Clear();

                        MaxValue = maxTotal;
                        CurrentValue = currentTotal;
                    }
                }
                else
                {
                    if(WasInShip || BuildInfoMod.Instance.Tick % 10 == 0)
                    {
                        WasInShip = false;

                        IMyInventory inventory = MyAPIGateway.Session?.Player?.Character?.GetInventory();
                        if(inventory != null)
                        {
                            Containers = 1;
                            MaxValue = MyFixedPoint.MultiplySafe(inventory.MaxVolume, 1000).ToIntSafe();
                            CurrentValue = MyFixedPoint.MultiplySafe(inventory.CurrentVolume, 1000).ToIntSafe();
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        string ComputeLabel()
        {
            bool enabled = BuildInfoMod.Instance.Config.BackpackBarOverride.Value;
            if(!enabled)
                return CurrentValue.ToString(TextFormat);

            // TODO: toggle string formatting?
            if(WasInShip)
            {
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
