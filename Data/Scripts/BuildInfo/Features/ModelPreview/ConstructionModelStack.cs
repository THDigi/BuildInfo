using System.Collections.Generic;
using Digi.BuildInfo.Features.ModelPreview.Blocks;
using Sandbox.Definitions;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview
{
    /// <summary>
    /// Represents a horizontal stack of models.
    /// One stack per subpart or top part of a block, each having a build stage model in <see cref="Models"/>
    /// </summary>
    public class ConstructionModelStack
    {
        public readonly MyCubeBlockDefinition Def;
        public readonly List<PreviewEntityWrapper> Models = new List<PreviewEntityWrapper>();
        public readonly float? Transparency;

        public static ConstructionModelStack CreateAndAdd(List<ConstructionModelStack> stacks, MyCubeBlockDefinition def, Matrix? localMatrix = null, float? transparency = null)
        {
            MyCubeBlockDefinition.BuildProgressModel[] buildModels = def.BuildProgressModels;
            if(buildModels != null && buildModels.Length > 0)
            {
                ConstructionModelStack stack = new ConstructionModelStack(def, localMatrix, transparency);
                stacks.Add(stack);
                return stack;
            }
            return null;
        }

        public ConstructionModelStack(float? transparency = null)
        {
            Def = null;
            Transparency = transparency;
        }

        public ConstructionModelStack(MyCubeBlockDefinition def, Matrix? localMatrix = null, float? transparency = null)
        {
            Def = def;
            Transparency = transparency;

            MyCubeBlockDefinition.BuildProgressModel[] buildModels = def.BuildProgressModels;
            if(buildModels != null && buildModels.Length > 0)
            {
                for(int i = (buildModels.Length - 1); i >= 0; i--) // reverse order to start from the fully built stage and work backwards
                {
                    Models.Add(new PreviewEntityWrapper(buildModels[i].File, localMatrix));
                }
            }
        }

        public void SetLocalMatrix(Matrix localMatrix)
        {
            foreach(PreviewEntityWrapper model in Models)
            {
                model.LocalMatrix = localMatrix;
            }
        }

        public void RemoveModels()
        {
            foreach(PreviewEntityWrapper model in Models)
            {
                model.Close();
            }

            Models.Clear();
        }
    }
}
