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
        public MyCubeBlockDefinition BlockDef { get; private set; }

        protected ConstructionModelStack ConstructionStack;

        protected readonly BuildInfoMod Main;

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
                ConstructionStack?.RemoveModels();
            }
            finally
            {
                BlockDef = null;
                ConstructionStack = null;
            }
        }

        protected abstract bool Initialized();

        protected abstract void Disposed();

        public abstract void Update(ref MatrixD drawMatrix);

        public virtual void SpawnConstructionModel(ConstructionModelPreview comp)
        {
        }
    }
}
