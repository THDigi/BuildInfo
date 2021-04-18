using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
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

    public class BData_Base
    {
        //public List<MyTuple<string, Matrix>> Dummies;

        public bool SupportsConveyors = false;
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

            if(IsValid(block, def))
            {
                BuildInfoMod.Instance.LiveDataHandler.BlockData.Add(def.Id, this);
                return true;
            }

            return false;
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

        protected virtual bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool hasStuff = false;

            var internalBlock = (MyCubeBlock)block;

            if(internalBlock.UpgradeValues.Count > 0)
            {
                Upgrades = new List<string>(internalBlock.UpgradeValues.Count);

                foreach(var upgrade in internalBlock.UpgradeValues.Keys)
                {
                    Upgrades.Add(upgrade);
                }
            }

            var dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            const StringComparison CompareType = StringComparison.InvariantCultureIgnoreCase;

            foreach(var dummy in dummies.Values)
            {
                var matrix = dummy.Matrix;
                matrix.Translation += def.ModelOffset;

                string name = dummy.Name;

                if(!name.StartsWith("detector_", CompareType))
                    continue;

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
                    }

                    hasStuff = true;
                }
                else if(detectorType.EqualsIgnoreCase("upgrade"))
                {
                    if(UpgradePorts == null)
                        UpgradePorts = new List<Matrix>();

                    UpgradePorts.Add(matrix);
                    hasStuff = true;
                }
                // from classes that use MyUseObjectAttribute
                else if(detectorType.EqualsIgnoreCase("terminal")
                     || detectorType.EqualsIgnoreCase("inventory")
                     || detectorType.EqualsIgnoreCase("textpanel")
                     || detectorType.EqualsIgnoreCase("advanceddoor")
                     || detectorType.EqualsIgnoreCase("door")
                     || detectorType.EqualsIgnoreCase("block") // medical room heal
                     || detectorType.EqualsIgnoreCase("jukebox") // excluding jukeboxNext/jukeboxPrevious/jukeboxPause
                     || detectorType.EqualsIgnoreCase("vendingMachine") // excluding vendingMachineBuy/vendingMachineNext/vendingMachinePrevious
                     || detectorType.EqualsIgnoreCase("contract")
                     || detectorType.EqualsIgnoreCase("ATM")
                     || detectorType.EqualsIgnoreCase("store"))
                {
                    if(Interactive == null)
                        Interactive = new List<InteractionInfo>();

                    Interactive.Add(new InteractionInfo(matrix, "Terminal/Inventory\n       Access", new Color(55, 255, 220)));
                    hasStuff = true;
                }
                // from classes that use MyUseObjectAttribute
                else if(detectorType.EqualsIgnoreCase("cockpit")
                     || detectorType.EqualsIgnoreCase("cryopod"))
                {
                    if(Interactive == null)
                        Interactive = new List<InteractionInfo>();

                    Interactive.Add(new InteractionInfo(matrix, "Entrance", new Color(25, 100, 155)));
                    hasStuff = true;
                }
                //else if(detectorType.EqualsIgnoreCase("wardrobe")
                //     || detectorType.EqualsIgnoreCase("ladder")
                //     || detectorType.EqualsIgnoreCase("panel") // button panel
                //     || detectorType.EqualsIgnoreCase("jukeboxNext")
                //     || detectorType.EqualsIgnoreCase("jukeboxPrevious")
                //     || detectorType.EqualsIgnoreCase("jukeboxPause")
                //     || detectorType.EqualsIgnoreCase("vendingMachineBuy")
                //     || detectorType.EqualsIgnoreCase("vendingMachineNext")
                //     || detectorType.EqualsIgnoreCase("vendingMachinePrevious"))
                //{
                //    if(TerminalAccess == null)
                //        TerminalAccess = new List<InteractionInfo>();
                //
                //    TerminalAccess.Add(new InteractionInfo(matrix, "Stuff...", new Color(55, 255, 220)));
                //    hasStuff = true;
                //}
                //else
                //{
                //    if(Dummies == null)
                //        Dummies = new List<MyTuple<string, Matrix>>();
                //
                //    Dummies.Add(new MyTuple<string, Matrix>(dummy.Name, matrix));
                //    hasStuff = true;
                //}
            }

            SupportsConveyors = BuildInfoMod.Instance.LiveDataHandler.ConveyorSupportTypes.GetValueOrDefault(block.BlockDefinition.TypeId, false);

            if(ConveyorPorts != null)
                ConveyorPorts.TrimExcess();

            if(InteractableConveyorPorts != null)
                InteractableConveyorPorts.TrimExcess();

            if(UpgradePorts != null)
                UpgradePorts.TrimExcess();

            if(Interactive != null)
                Interactive.TrimExcess();

            dummies.Clear();
            return hasStuff;
        }
    }
}