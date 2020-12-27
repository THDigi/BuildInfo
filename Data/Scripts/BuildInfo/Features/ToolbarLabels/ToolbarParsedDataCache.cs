using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public class ToolbarParsedDataCache : ModComponent
    {
        private readonly Dictionary<long, string> BlockNames = new Dictionary<long, string>();

        private readonly Dictionary<int, string> ActionNames = new Dictionary<int, string>();

        private long previousControlledEntId;

        public ToolbarParsedDataCache(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            EquipmentMonitor.UpdateControlled += EquipmentMonitor_UpdateControlled;
        }

        protected override void UnregisterComponent()
        {
            EquipmentMonitor.UpdateControlled -= EquipmentMonitor_UpdateControlled;
        }

        /// <summary>
        /// Returns cache if exists, or null.
        /// </summary>
        public string GetActionNameCache(int index)
        {
            return ActionNames.GetValueOrDefault(index, null);
        }

        /// <summary>
        /// Stores the specified name for the specified index.
        /// Cache gets removed once player uses any GUI or changes cockpits.
        /// </summary>
        public void SetActionNameCache(int index, string parsedName)
        {
            ActionNames[index] = parsedName;
        }

        private void EquipmentMonitor_UpdateControlled(IMyCharacter character, IMyShipController shipController, IMyControllableEntity controlled, int tick)
        {
            if(shipController == null)
                return;

            if(previousControlledEntId != shipController.EntityId)
            {
                previousControlledEntId = shipController.EntityId;
                ActionNames.Clear();
                return;
            }

            if(MyAPIGateway.Gui.IsCursorVisible && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                ActionNames.Clear();
                return;
            }
        }

        /// <summary>
        /// Returns cache if exists, or null.
        /// </summary>
        public string GetBlockNameCache(long entityId)
        {
            return BlockNames.GetValueOrDefault(entityId, null);
        }

        /// <summary>
        /// Stores the specified name for the specified entity.
        /// Cache gets removed once block's customname gets changed!
        /// </summary>
        public void SetBlockNameCache(IMyTerminalBlock block, string parsedName)
        {
            BlockNames[block.EntityId] = parsedName;
            block.CustomNameChanged += Block_CustomNameChanged;
            block.OnMarkForClose += Block_OnMarkForClose;
        }

        void Block_CustomNameChanged(IMyTerminalBlock block)
        {
            BlockNames.Remove(block.EntityId);
        }

        void Block_OnMarkForClose(IMyEntity ent)
        {
            var block = (IMyTerminalBlock)ent;
            block.CustomNameChanged -= Block_CustomNameChanged;
            block.OnMarkForClose -= Block_OnMarkForClose;
            BlockNames.Remove(block.EntityId);
        }
    }
}
