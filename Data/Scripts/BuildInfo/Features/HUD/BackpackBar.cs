﻿using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
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
        static readonly List<IMyCubeGrid> TempGrids = new List<IMyCubeGrid>();
        static readonly List<IMyTerminalBlock> TempBlocks = new List<IMyTerminalBlock>();

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
            finally
            {
                TempBlocks.Clear();
                TempGrids.Clear();
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
