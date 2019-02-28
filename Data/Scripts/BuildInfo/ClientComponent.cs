using Digi.BuildInfo.Features;
using Digi.BuildInfo.Features.Config;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;

namespace Digi.BuildInfo
{
    /// <summary>
    /// Pass-through component to assign aliases
    /// </summary>
    public abstract class ClientComponent : ComponentBase<Client>
    {
        protected TextAPI TextAPI => Mod.TextAPI;
        protected Config Config => Mod.Config;
        protected Overlays Overlays => Mod.Overlays;
        protected DrawUtils DrawUtils => Mod.DrawUtils;
        protected Constants Constants => Mod.Constants;
        protected QuickMenu QuickMenu => Mod.QuickMenu;
        protected GameConfig GameConfig => Mod.GameConfig;
        protected BlockMonitor BlockMonitor => Mod.BlockMonitor;
        protected ChatCommands ChatCommands => Mod.ChatCommands;
        protected TextGeneration TextGeneration => Mod.TextGeneration;
        protected EquipmentMonitor EquipmentMonitor => Mod.EquipmentMonitor;

        protected bool TextAPIEnabled => Mod.TextAPI.IsEnabled;

        public ClientComponent(Client mod) : base(mod)
        {
        }
    }
}
