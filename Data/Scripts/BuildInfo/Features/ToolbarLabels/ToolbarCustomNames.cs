using System.Collections.Generic;
using Digi.BuildInfo.Features.Config;
using Digi.ConfigLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.ModAPI;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public class ToolbarCustomNames : ModComponent
    {
        public const string IniSection = "Toolbar";
        public const char KeySeparator = '-';
        public const int CustomLabelMaxLength = 60;

        ToolbarActionLabelsMode Mode;

        long PreviousControlledEntId = 0;
        string PreviousCustomDataParse = null;

        long LastViewedTerminalEntId;

        int SortedIndex = 0;
        List<ToolbarItemData> SortedData = new List<ToolbarItemData>(9 * 9);

        MyIni IniParser = new MyIni();
        List<MyIniKey> IniKeys = new List<MyIniKey>();

        Dictionary<long, BlockToolbarData> Blocks = new Dictionary<long, BlockToolbarData>();

        public ToolbarCustomNames(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            Mode = (ToolbarActionLabelsMode)Main.Config.ToolbarActionLabels.Value;

            EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
            EquipmentMonitor.ShipControllerOBChanged += EquipmentMonitor_ShipControllerOBChanged;

            Main.Config.ToolbarActionLabels.ValueAssigned += ToolbarActionLabelModeChanged;

            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
        }

        protected override void UnregisterComponent()
        {
            EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
            EquipmentMonitor.ShipControllerOBChanged -= EquipmentMonitor_ShipControllerOBChanged;

            Main.Config.ToolbarActionLabels.ValueAssigned -= ToolbarActionLabelModeChanged;

            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
        }

        public ToolbarItemData NextToolbarItem(string expectedActionId)
        {
            int nextIndex = SortedIndex + 1;

            if(nextIndex >= SortedData.Count)
                return default(ToolbarItemData);

            var item = SortedData[nextIndex];
            if(item.ActionId != expectedActionId)
                return default(ToolbarItemData);

            SortedIndex++;
            return item;
        }

        public List<ToolbarItemData> ToolbarSlotData => SortedData;

        public BlockToolbarData GetBlockData(long entityId) => Blocks.GetValueOrDefault(entityId, null);

        void ToolbarActionLabelModeChanged(int oldValue, int newValue, SettingBase<int> setting)
        {
            if(oldValue != newValue)
            {
                Mode = (ToolbarActionLabelsMode)newValue;
            }
        }

        void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(shipController == null)
                return;

            SortedIndex = 0;

            if(PreviousControlledEntId != shipController.EntityId)
            {
                PreviousControlledEntId = shipController.EntityId;
                ShipControllerChanged(shipController);
            }

            // HACK: because CustomDataChanged doesn't work for sender
            if(tick % 60 == 0 && MyAPIGateway.Gui.IsCursorVisible)
            {
                ParseCustomData(shipController);
            }
        }

        void EquipmentMonitor_ShipControllerOBChanged(MyObjectBuilder_ShipController ob)
        {
            SortedData.Clear();

            if(Mode == ToolbarActionLabelsMode.Off)
                return;

            var shipController = MyAPIGateway.Session.ControlledObject as IMyShipController;
            if(shipController == null)
            {
                Log.Error($"EquipmentMonitor_ShipControllerOBChanged :: no ship controller?!");
                return;
            }

            ParseCustomData(shipController);

            if(ob == null || ob.EntityId != shipController.EntityId)
            {
                if(ob == null)
                    Log.Error($"EquipmentMonitor.ShipControllerOB is null!");
                else
                    Log.Error($"EquipmentMonitor.ShipControllerOB is for a different entity!");

                //ob = shipController.GetObjectBuilderCubeBlock(false) as MyObjectBuilder_ShipController;
                return;
            }

            if(ob.Toolbar?.Slots == null)
                return;

            var data = Blocks.GetValueOrDefault(shipController.EntityId, null);

            if(ToolbarActionLabels.ToolbarDebugLogging)
                Log.Info($"Toolbar OB for {shipController.CustomName}:");

            for(int i = 0; i < ob.Toolbar.Slots.Count; i++)
            {
                var item = ob.Toolbar.Slots[i];
                var blockItem = item.Data as MyObjectBuilder_ToolbarItemTerminal;
                if(blockItem != null)
                {
                    string actionId = blockItem?._Action;
                    string customLabel = data?.GetCustomLabel(item.Index);
                    string groupName = (item.Data as MyObjectBuilder_ToolbarItemTerminalGroup)?.GroupName;

                    if(ToolbarActionLabels.ToolbarDebugLogging)
                        Log.Info($"    {item.Index.ToString(),-4} data={item.Data.GetType().Name,-48}, item={item.Item,-12}, customLabel={customLabel,-32}, groupName={groupName}");

                    // must add even if there's no useful data to keep the numbered order in sync.
                    SortedData.Add(new ToolbarItemData(item.Index, actionId, customLabel, groupName, blockItem));
                }
                else
                {
                    if(ToolbarActionLabels.ToolbarDebugLogging)
                        Log.Info($"    {item.Index.ToString(),-4} data={item.Data.GetType().Name,-48}, item={item.Item,-12}");
                }
            }

            if(ToolbarActionLabels.ToolbarDebugLogging)
                Log.Info($"End toolbar");
        }

        void ShipControllerChanged(IMyShipController block)
        {
            // already hooked for events
            if(Blocks.ContainsKey(block.EntityId))
                return;

            //block.CustomDataChanged += Block_CustomDataChanged;
            block.OnMarkForClose += Block_OnMarkForClose;

            ParseCustomData(block);
        }

        //void Block_CustomDataChanged(IMyTerminalBlock block)
        //{
        //    ParseCustomData(block);
        //}

        void Block_OnMarkForClose(IMyEntity ent)
        {
            var block = (IMyTerminalBlock)ent;
            //block.CustomDataChanged -= Block_CustomDataChanged;
            block.OnMarkForClose -= Block_OnMarkForClose;

            Blocks.Remove(block.EntityId);
        }

        void ParseCustomData(IMyTerminalBlock block)
        {
            if(Mode == ToolbarActionLabelsMode.Off)
                return;

            string customData = block.CustomData;
            if(object.ReferenceEquals(customData, PreviousCustomDataParse))
                return;

            //if(customData.Equals(previousCustomDataParse))
            //{
            //    previousCustomDataParse = customData;
            //    return;
            //}

            PreviousCustomDataParse = customData;

            BlockToolbarData blockData;
            if(!Blocks.TryGetValue(block.EntityId, out blockData))
                Blocks[block.EntityId] = blockData = new BlockToolbarData();

            bool hadErrors = (blockData.ParseErrors.Count > 0);
            blockData.StartParse();

            if(string.IsNullOrWhiteSpace(customData))
            {
                if(hadErrors)
                    RefreshDetailInfo(block);
                return;
            }

            MyIniParseResult result;
            if(!IniParser.TryParse(customData, IniSection, out result))
            {
                blockData.AddError($"Line #{result.LineNo.ToString()}: {result.Error}");
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
                        blockData.AddError($"'{keyString}' is wrong, format: 1{KeySeparator.ToString()}1 = Label");
                        continue;
                    }

                    // char to integer conversion
                    int page = (keyString[0] - '0');
                    int slot = (keyString[2] - '0');

                    if(page < 1 || page > 9 || slot < 1 || slot > 9)
                    {
                        blockData.AddError($"'{keyString}' wrong numbers, each must be 1 and 9.");
                        continue;
                    }

                    // must match the index from toolbar item's OB which is 0 to 80
                    int index = ((page - 1) * 9) + (slot - 1);

                    blockData.SetLabel(index, IniParser.Get(key).ToString());
                }
            }

            if(hadErrors || blockData.ParseErrors.Count > 0)
            {
                RefreshDetailInfo(block);
            }

            //EquipmentMonitor_ShipControllerOBChanged(EquipmentMonitor.ShipControllerOB);
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
