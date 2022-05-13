using Sandbox.Definitions;
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
