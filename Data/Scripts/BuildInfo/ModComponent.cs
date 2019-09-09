using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;

namespace Digi.BuildInfo
{
    /// <summary>
    /// Pass-through component to assign aliases.
    /// NOTE: only player-side (non-DS).
    /// </summary>
    public abstract class ModComponent : ComponentBase<BuildInfoMod>
    {
        protected TextAPI TextAPI => Main.TextAPI;
        protected Config Config => Main.Config;
        protected Overlays Overlays => Main.Overlays;
        protected DrawUtils DrawUtils => Main.DrawUtils;
        protected Constants Constants => Main.Constants;
        protected QuickMenu QuickMenu => Main.QuickMenu;
        protected GameConfig GameConfig => Main.GameConfig;
        protected BlockMonitor BlockMonitor => Main.BlockMonitor;
        protected ChatCommands ChatCommands => Main.ChatCommands;
        protected TerminalInfo TerminalInfo => Main.TerminalInfo;
        protected TextGeneration TextGeneration => Main.TextGeneration;
        protected LiveDataHandler LiveDataHandler => Main.LiveDataHandler;
        protected EquipmentMonitor EquipmentMonitor => Main.EquipmentMonitor;

        protected bool TextAPIEnabled => Main.TextAPI.IsEnabled;

        public ModComponent(BuildInfoMod main) : base(main)
        {
        }
    }
}
