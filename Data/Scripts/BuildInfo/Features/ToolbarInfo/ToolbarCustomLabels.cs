using System.Collections.Generic;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.ModAPI;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features.ToolbarInfo
{
    public class CustomToolbarData
    {
        public readonly Dictionary<int, string> CustomLabels = new Dictionary<int, string>();
        public readonly List<string> ParseErrors = new List<string>();
    }

    public class ToolbarCustomLabels : ModComponent
    {
        public const string IniSection = "Toolbar";
        public const char KeySeparator = '-';
        public const int CustomLabelMaxLength = 128;

        public readonly Dictionary<long, CustomToolbarData> BlockData = new Dictionary<long, CustomToolbarData>();

        string PreviousCustomDataParse;
        long PreviousControlledEntId;
        long LastViewedTerminalEntId;

        MyIni IniParser = new MyIni();
        List<MyIniKey> IniKeys = new List<MyIniKey>();

        public ToolbarCustomLabels(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;

            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
        }

        void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(shipController == null)
                return;

            if(PreviousControlledEntId != shipController.EntityId)
            {
                PreviousControlledEntId = shipController.EntityId;
                ShipControllerChanged(shipController);
            }

            // HACK: because CustomDataChanged doesn't work for sender
            if(tick % 30 == 0)
            {
                ParseCustomData(shipController);
            }
        }

        void ShipControllerChanged(IMyShipController block)
        {
            // already hooked for events
            if(BlockData.ContainsKey(block.EntityId))
                return;

            block.OnMarkForClose += Block_OnMarkForClose;

            ParseCustomData(block);
        }

        void Block_OnMarkForClose(IMyEntity ent)
        {
            var block = (IMyTerminalBlock)ent;
            block.OnMarkForClose -= Block_OnMarkForClose;
            BlockData.Remove(block.EntityId);
        }

        public void ParseCustomData(IMyTerminalBlock block)
        {
            if(Main.Config.ToolbarLabels.Value == 0)
                return;

            string customData = block.CustomData;
            if(object.ReferenceEquals(customData, PreviousCustomDataParse))
                return;

            PreviousCustomDataParse = customData;

            CustomToolbarData labelData;
            if(!BlockData.TryGetValue(block.EntityId, out labelData))
                BlockData[block.EntityId] = labelData = new CustomToolbarData();

            bool hadErrors = (labelData.ParseErrors.Count > 0);
            labelData.CustomLabels.Clear();
            labelData.ParseErrors.Clear();

            if(string.IsNullOrWhiteSpace(customData))
            {
                if(hadErrors)
                    RefreshDetailInfo(block);

                return;
            }

            if(!MyIni.HasSection(customData, IniSection))
                return; // required because ini.TryParse() requires the section if specified

            MyIniParseResult result;
            if(!IniParser.TryParse(customData, IniSection, out result))
            {
                labelData.ParseErrors.Add($"Line #{result.LineNo.ToString()}: {result.Error}");
                RefreshDetailInfo(block);
                return;
            }

            IniParser.GetKeys(IniSection, IniKeys);
            if(IniKeys.Count > 0)
            {
                for(int i = 0; i < IniKeys.Count; ++i)
                {
                    MyIniKey key = IniKeys[i];
                    string keyString = key.Name;

                    if(keyString.Length != 3 || keyString[1] != KeySeparator)
                    {
                        labelData.ParseErrors.Add($"'{keyString}' is wrong, format: 1{KeySeparator.ToString()}1 = Label");
                        continue;
                    }

                    // char to integer conversion
                    int page = (keyString[0] - '0');
                    int slot = (keyString[2] - '0');

                    if(page < 1 || page > 9 || slot < 1 || slot > 9)
                    {
                        labelData.ParseErrors.Add($"'{keyString}' wrong numbers, each must be 1 and 9.");
                        continue;
                    }

                    // must match the index from toolbar item's OB which is 0 to 80
                    int index = ((page - 1) * 9) + (slot - 1);

                    string label = IniParser.Get(key).ToString();

                    if(label.Length > CustomLabelMaxLength)
                    {
                        label = label.Substring(0, CustomLabelMaxLength);
                        labelData.ParseErrors.Add($"WARNING: '{keyString}' has more than {CustomLabelMaxLength.ToString()} chars, trimmed.");
                    }

                    if(label.IndexOf('\n') != -1)
                    {
                        label = label.Replace('\n', ' ');
                        labelData.ParseErrors.Add($"WARNING: '{keyString}' has multi-line, not supported!");
                    }

                    labelData.CustomLabels[index] = label;
                }
            }

            if(hadErrors || labelData.ParseErrors.Count > 0)
            {
                RefreshDetailInfo(block);
            }

            // refresh slot labels
            var slots = Main.ToolbarMonitor.Slots;
            for(int index = slots.Length - 1; index >= 0; index--)
            {
                var slot = slots[index];
                slot.CustomLabel = labelData?.CustomLabels.GetValueOrDefault(index, null);
            }
        }

        void RefreshDetailInfo(IMyTerminalBlock block)
        {
            // HACK forcing refresh of detailed info panel

            // only trigger it for player that is viewing this block in terminal
            if(LastViewedTerminalEntId == block.EntityId && MyAPIGateway.Gui.IsCursorVisible && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                // this sends network messages
                bool original = block.ShowInToolbarConfig;
                block.ShowInToolbarConfig = !original;
                block.ShowInToolbarConfig = original;
            }
        }

        void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            LastViewedTerminalEntId = (block == null ? 0 : block.EntityId);
        }
    }
}
