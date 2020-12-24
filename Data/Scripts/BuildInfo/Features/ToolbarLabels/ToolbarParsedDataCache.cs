using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.ModAPI;

namespace Digi.BuildInfo.Features.ToolbarLabels
{
    public class ToolbarParsedDataCache : ModComponent
    {
        private readonly Dictionary<long, string> Names = new Dictionary<long, string>();

        public ToolbarParsedDataCache(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
        }

        protected override void UnregisterComponent()
        {
        }

        /// <summary>
        /// Returns cache if exists, or null.
        /// </summary>
        public string GetNameCache(long entityId)
        {
            return Names.GetValueOrDefault(entityId, null);
        }

        /// <summary>
        /// Stores the specified name for the specified entity.
        /// Cache gets removed once block's customname gets changed!
        /// </summary>
        public void SetNameCache(IMyTerminalBlock block, string parsedName)
        {
            Names[block.EntityId] = parsedName;
            block.CustomNameChanged += Block_CustomNameChanged;
            block.OnMarkForClose += Block_OnMarkForClose;
        }

        void Block_CustomNameChanged(IMyTerminalBlock block)
        {
            Names.Remove(block.EntityId);
        }

        void Block_OnMarkForClose(IMyEntity ent)
        {
            var block = (IMyTerminalBlock)ent;
            block.CustomNameChanged -= Block_CustomNameChanged;
            block.OnMarkForClose -= Block_OnMarkForClose;
            Names.Remove(block.EntityId);
        }
    }
}
