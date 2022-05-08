using System;
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
        public string ErasePrefix = null;

        public readonly List<string> ParseErrors = new List<string>();
    }

    public class ToolbarCustomLabels : ModComponent
    {
        public const string IniSection = "Toolbar";
        public const string PrefixKey = "Prefix";
        public const char KeySeparator = '-';
        public const int CustomLabelMaxLength = 128;
        public const string IniDivider = "---"; // this is hardcoded in MyIni, do not change

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
            IMyTerminalBlock block = (IMyTerminalBlock)ent;
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
            labelData.ErasePrefix = null;
            labelData.ParseErrors.Clear();

            if(string.IsNullOrWhiteSpace(customData))
            {
                if(hadErrors)
                    RefreshDetailInfo(block);

                return;
            }

            if(!MyIni.HasSection(customData, IniSection))
                return; // required because ini.TryParse() requires the section if specified

            // HACK: no longer using TryParse(customdata, section, out...) because it fails if `---` is in the section.
            MyIniParseResult result;
            if(!IniParser.TryParse(customData, out result))
            {
                labelData.ParseErrors.Add($"Line #{result.LineNo.ToString()}: {result.Error}");
                RefreshDetailInfo(block);
                return;
            }

            IniKeys.Clear();
            IniParser.GetKeys(IniSection, IniKeys);
            if(IniKeys.Count > 0)
            {
                for(int i = 0; i < IniKeys.Count; ++i)
                {
                    MyIniKey key = IniKeys[i];
                    string keyString = key.Name;

                    if(keyString.Equals(PrefixKey, StringComparison.OrdinalIgnoreCase))
                    {
                        string prefix = IniParser.Get(key).ToString();
                        labelData.ErasePrefix = (string.IsNullOrWhiteSpace(prefix) ? null : prefix);
                        continue;
                    }

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
            ToolbarItem[] slots = Main.ToolbarMonitor.Slots;
            for(int index = slots.Length - 1; index >= 0; index--)
            {
                ToolbarItem slot = slots[index];
                slot.CustomLabel = labelData?.CustomLabels.GetValueOrDefault(index, null);
                slot.LabelData = labelData;
            }
        }

        /// <summary>
        /// Sets or clears the slot (1~9)'s custom label for the currently used ship controller and its current page.
        /// Returns null if succeded, otherwise returns the reason it failed.
        /// </summary>
        public string SetSlotLabel(int slot, string label = null)
        {
            if(slot < 1 || slot > 9)
                return "slot not between 1 and 9";

            IMyShipController shipCtrl = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipCtrl == null)
                return "not controlling a cockpit/RC";

            string reason = ParseCustomData(shipCtrl.CustomData, IniParser);
            if(reason != null)
                return reason;

            string key = $"{Main.ToolbarMonitor.ToolbarPage + 1}-{slot}";
            if(label != null)
                IniParser.Set(IniSection, key, label);
            else
                IniParser.Set(IniSection, key, null);

            shipCtrl.CustomData = GetParsedCustomData(IniParser);
            return null;
        }

        /// <summary>
        /// Sets or clears the prefix to be erased for the currently used ship controller
        /// Returns null if succeded, otherwise returns the reason it failed.
        /// </summary>
        public string SetErasePrefix(string prefix)
        {
            IMyShipController shipCtrl = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipCtrl == null)
                return "not controlling a cockpit/RC";

            string reason = ParseCustomData(shipCtrl.CustomData, IniParser);
            if(reason != null)
                return reason;

            if(prefix != null)
                IniParser.Set(IniSection, PrefixKey, prefix);
            else
                IniParser.Set(IniSection, PrefixKey, null);

            shipCtrl.CustomData = GetParsedCustomData(IniParser);
            return null;
        }

        /// <summary>
        /// Converts given parser's data into string for CustomData.
        /// Automatically deletes the section if there's no keys in it, otherwise it gets the given comment (can use null to not have a comment)
        /// </summary>
        string GetParsedCustomData(MyIni iniParser, string comment = " Custom toolbar slot labels, used by BuildInfo mod.")
        {
            IniKeys.Clear();
            iniParser.GetKeys(IniSection, IniKeys);

            if(IniKeys.Count == 0)
                iniParser.DeleteSection(IniSection);
            else
                iniParser.SetSectionComment(IniSection, comment);

            return iniParser.ToString();
        }

        /// <summary>
        /// Returns null if succeeded, or fail reason if not
        /// </summary>
        static string ParseCustomData(string customData, MyIni iniParser)
        {
            iniParser.Clear();

            // determine if a valid --- divider exists, same way MyIni.FindSection() does it
            bool hasDivider = false;
            TextPtr ptr = new TextPtr(customData);
            while(!ptr.IsOutOfBounds())
            {
                ptr = ptr.Find("\n");
                ++ptr;
                if(ptr.Char == '[')
                {
                    // don't care
                }
                else if(ptr.StartsWith(IniDivider))
                {
                    ptr = (ptr + IniDivider.Length).SkipWhitespace();
                    if(ptr.IsEndOfLine())
                    {
                        hasDivider = true;
                        break;
                    }
                }
            }

            // need to parse the entire thing
            MyIniParseResult result;
            if(!iniParser.TryParse(customData, out result))
            {
                if(hasDivider)
                {
                    return $"failed to parse CustomData (before {IniDivider} divider)";
                }
                else
                {
                    // assume the current customdata is not ini and try again with divider
                    customData = IniDivider + "\n" + customData;
                    if(!iniParser.TryParse(customData, out result))
                    {
                        return "failed to parse CustomData";
                    }
                }
            }

            return null;
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
