﻿using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    [Flags]
    public enum ConveyorFlags : byte
    {
        None = 0,
        Small = (1 << 0),
        In = (1 << 1),
        Out = (1 << 2),
    }

    public struct ConveyorInfo
    {
        public readonly Matrix LocalMatrix;
        public readonly ConveyorFlags Flags;

        public ConveyorInfo(Matrix localMatrix, ConveyorFlags flags = ConveyorFlags.None)
        {
            LocalMatrix = localMatrix;
            Flags = flags;
        }
    }

    public struct InteractionInfo
    {
        public readonly Matrix LocalMatrix;
        public readonly string Name;
        public readonly Color Color;

        public InteractionInfo(Matrix localMatrix, string name, Color color)
        {
            LocalMatrix = localMatrix;
            Name = name;
            Color = color;
        }
    }

    [Flags]
    public enum BlockHas : byte
    {
        Nothing = 0,
        ConveyorSupport = (1 << 0),
        Terminal = (1 << 1),
        Inventory = (1 << 2),
        TerminalAndInventoryAccess = (1 << 3),
    }

    public class BData_Base
    {
        //public List<MyTuple<string, Matrix>> Dummies;

        public BlockHas Has = BlockHas.Nothing;
        public List<ConveyorInfo> ConveyorPorts;
        public List<ConveyorInfo> InteractableConveyorPorts;
        public List<InteractionInfo> Interactive;
        public List<Matrix> UpgradePorts;
        public List<string> Upgrades;

        public BData_Base()
        {
        }

        public bool CheckAndAdd(IMyCubeBlock block)
        {
            var def = (MyCubeBlockDefinition)block.SlimBlock.BlockDefinition;

            ComputeUpgrades(block);
            ComputeHas(block);
            ComputeDummies(block, def);

            bool isValid = IsValid(block, def);

            // now always valid because of the base data

            BuildInfoMod.Instance.LiveDataHandler.BlockData.Add(def.Id, this);
            return true;
        }

        protected virtual bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def) => false;

        void ComputeUpgrades(IMyCubeBlock block)
        {
            var internalBlock = (MyCubeBlock)block;
            if(internalBlock.UpgradeValues.Count > 0)
            {
                Upgrades = new List<string>(internalBlock.UpgradeValues.Count);

                foreach(var upgrade in internalBlock.UpgradeValues.Keys)
                {
                    Upgrades.Add(upgrade);
                }
            }
        }

        void ComputeHas(IMyCubeBlock block)
        {
            if(BuildInfoMod.Instance.LiveDataHandler.ConveyorSupportTypes.GetValueOrDefault(block.BlockDefinition.TypeId, false))
                Has |= BlockHas.ConveyorSupport;

            if(block is IMyTerminalBlock)
                Has |= BlockHas.Terminal;

            if(block.HasInventory)
                Has |= BlockHas.Inventory;
        }

        void ComputeDummies(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            if(dummies.Count == 0)
                return;

            var colorTerminalOnly = new Color(55, 255, 220);
            var colorInteractiveAndTerminal = new Color(50, 255, 150);
            var colorInteractiveOnly = new Color(25, 100, 155);
            const StringComparison CompareType = StringComparison.InvariantCultureIgnoreCase;

            Interactive = new List<InteractionInfo>(8);

            foreach(var dummy in dummies.Values)
            {
                string name = dummy.Name;
                if(!name.StartsWith("detector_", CompareType))
                    continue;

                var matrix = dummy.Matrix;
                matrix.Translation += def.ModelOffset;

                int index = 9; // "detector_".Length
                StringSegment detectorType = GetNextSection(name, ref index); // detector_<here>_small_in
                TextPtr detectorPtr = new TextPtr(detectorType.Text, detectorType.Start);
                StringSegment part1 = GetNextSection(name, ref index); // detector_conveyorline_<here>_in
                StringSegment part2 = GetNextSection(name, ref index); // detector_conveyorline_small_<here>

                if(detectorPtr.StartsWith("conveyor"))
                {
                    var flags = ConveyorFlags.None;

                    // from MyConveyorLine.GetBlockLinePositions()
                    if(part1.EqualsIgnoreCase("small"))
                        flags |= ConveyorFlags.Small;

                    if(part1.EqualsIgnoreCase("out") || part2.EqualsIgnoreCase("out"))
                        flags |= ConveyorFlags.Out;
                    else if(part1.EqualsIgnoreCase("in") || part2.EqualsIgnoreCase("in"))
                        flags |= ConveyorFlags.In;

                    if(detectorPtr.StartsWith("conveyorline"))
                    {
                        if(ConveyorPorts == null)
                            ConveyorPorts = new List<ConveyorInfo>();

                        ConveyorPorts.Add(new ConveyorInfo(matrix, flags));
                    }
                    else
                    {
                        if(InteractableConveyorPorts == null)
                            InteractableConveyorPorts = new List<ConveyorInfo>();

                        InteractableConveyorPorts.Add(new ConveyorInfo(matrix, flags));
                        Has |= BlockHas.TerminalAndInventoryAccess;
                    }
                }
                else if(detectorType.EqualsIgnoreCase("upgrade"))
                {
                    if(UpgradePorts == null)
                        UpgradePorts = new List<Matrix>();

                    UpgradePorts.Add(matrix);
                }
                // from classes that use MyUseObjectAttribute
                else if(detectorType.EqualsIgnoreCase("terminal"))
                {
                    // HACK: MyUseObjectsComponent.CreateInteractiveObject() hardcodes 'detector_terminal' to be door open/close if it's MyDoor type.
                    // HACK: Can't use `is IMyDoor` because it's implemented by all doors
                    if(block.GetType().Name == "MyDoor")
                        Interactive.Add(new InteractionInfo(matrix, "Open/Close\n+Terminal access", colorInteractiveAndTerminal));
                    else
                        Interactive.Add(new InteractionInfo(matrix, "Terminal/inventory access", colorTerminalOnly));

                    Has |= BlockHas.TerminalAndInventoryAccess;
                }
                else if(detectorType.EqualsIgnoreCase("inventory")
                     || detectorType.EqualsIgnoreCase("vendingMachine") // excluding vendingMachineBuy/vendingMachineNext/vendingMachinePrevious; clicking this one just opens terminal
                     || detectorType.EqualsIgnoreCase("jukebox")) // excluding jukeboxNext/jukeboxPrevious/jukeboxPause; clicking this one just opens terminal
                {
                    Interactive.Add(new InteractionInfo(matrix, "Terminal/inventory access", colorTerminalOnly));
                }
                else if(detectorType.EqualsIgnoreCase("textpanel"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Edit LCD\n+Terminal access", colorInteractiveAndTerminal));
                }
                else if(detectorType.EqualsIgnoreCase("advanceddoor")
                     || detectorType.EqualsIgnoreCase("door"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Open/Close\n+Terminal access", colorInteractiveAndTerminal));
                    Has |= BlockHas.TerminalAndInventoryAccess;
                }
                else if(detectorType.EqualsIgnoreCase("block")) // medical room/survival kit heal
                {
                    Interactive.Add(new InteractionInfo(matrix, "Recharge\n+Terminal access", colorInteractiveAndTerminal));
                    Has |= BlockHas.TerminalAndInventoryAccess;
                }
                else if(detectorType.EqualsIgnoreCase("contract"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Open Contracts\n+Terminal access", colorInteractiveAndTerminal));
                    Has |= BlockHas.TerminalAndInventoryAccess;
                }
                else if(detectorType.EqualsIgnoreCase("store"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Open Store\n+Terminal access", colorInteractiveAndTerminal));
                    Has |= BlockHas.TerminalAndInventoryAccess;
                }
                else if(detectorType.EqualsIgnoreCase("ATM"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Open Transactions\n+Terminal access", colorInteractiveAndTerminal));
                    Has |= BlockHas.TerminalAndInventoryAccess;
                }
                // from here only interactive things that can't open terminal
                else if(detectorType.EqualsIgnoreCase("cockpit")
                     || detectorType.EqualsIgnoreCase("cryopod"))
                {
                    Interactive.Add(new InteractionInfo(matrix, "Entrance", colorInteractiveOnly));
                }
                //else if(detectorType.EqualsIgnoreCase("wardrobe")
                //     || detectorType.EqualsIgnoreCase("ladder")
                //     || detectorType.EqualsIgnoreCase("respawn") // medical room/survival kit respawn point
                //     || detectorType.EqualsIgnoreCase("panel") // button panel
                //     || detectorType.EqualsIgnoreCase("jukeboxNext")
                //     || detectorType.EqualsIgnoreCase("jukeboxPrevious")
                //     || detectorType.EqualsIgnoreCase("jukeboxPause")
                //     || detectorType.EqualsIgnoreCase("vendingMachineBuy")
                //     || detectorType.EqualsIgnoreCase("vendingMachineNext")
                //     || detectorType.EqualsIgnoreCase("vendingMachinePrevious"))
                //{
                //    Interactive.Add(new InteractionInfo(matrix, "Stuff...", colorInteractiveOnly));
                //}
                //else
                //{
                //    if(Dummies == null)
                //        Dummies = new List<MyTuple<string, Matrix>>();
                //
                //    Dummies.Add(new MyTuple<string, Matrix>(dummy.Name, matrix));
                //}
            }

            if(ConveyorPorts != null)
                ConveyorPorts.TrimExcess();

            if(InteractableConveyorPorts != null)
                InteractableConveyorPorts.TrimExcess();

            if(UpgradePorts != null)
                UpgradePorts.TrimExcess();

            if(Interactive != null)
            {
                if(Interactive.Count > 0)
                    Interactive.TrimExcess();
                else
                    Interactive = null;
            }

            dummies.Clear();
        }

        static StringSegment GetNextSection(string text, ref int startIndex)
        {
            if(startIndex >= text.Length)
                return default(StringSegment);

            int sepIndex = text.IndexOf('_', startIndex);
            if(sepIndex > -1)
            {
                var segment = new StringSegment(text, startIndex, sepIndex - startIndex);
                startIndex = sepIndex + 1;
                return segment;
            }
            else
            {
                var segment = new StringSegment(text, startIndex, text.Length - startIndex);
                startIndex = text.Length;
                return segment;
            }
        }
    }
}