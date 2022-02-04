using System.Collections.Generic;
using System.IO;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public abstract class PreviewInstanceBase
    {
        protected readonly BuildInfoMod Main;

        public MyCubeBlockDefinition BlockDef { get; private set; }

        public PreviewInstanceBase()
        {
            Main = BuildInfoMod.Instance;
        }

        public bool Setup(MyCubeBlockDefinition def)
        {
            BlockDef = def;
            return Initialized();
        }

        public void Dispose()
        {
            try
            {
                Disposed();
            }
            finally
            {
                BlockDef = null;
            }
        }

        protected abstract bool Initialized();

        protected abstract void Disposed();

        public abstract void Update(ref MatrixD drawMatrix);
    }
}
