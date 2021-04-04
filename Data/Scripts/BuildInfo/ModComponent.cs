using Digi.ComponentLib;

namespace Digi.BuildInfo
{
    /// <summary>
    /// Pass-through component.
    /// NOTE: only player-side (non-DS).
    /// </summary>
    public abstract class ModComponent : ComponentBase<BuildInfoMod>
    {
        public ModComponent(BuildInfoMod main) : base(main)
        {
        }
    }
}
