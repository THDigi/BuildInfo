using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class MultiSubpartBase : PreviewInstanceBase
    {
        protected bool HasParts;
        protected BData_Base BaseData;
        protected List<PreviewEntityWrapper> Parts;
        protected bool HideRootSubparts = true;

        protected override void Initialized()
        {
            HasParts = false;
            BaseData = Main.LiveDataHandler.Get<BData_Base>(BlockDef);
            if(BaseData == null || BaseData.Subparts == null || BaseData.Subparts.Count == 0)
                return;

            // only first layer, the next layers are automatically spawned by the game.
            Parts = new List<PreviewEntityWrapper>(BaseData.Subparts.Count);
            foreach(SubpartInfo info in BaseData.Subparts)
            {
                Parts.Add(new PreviewEntityWrapper(info.Model, info.LocalMatrix));
            }

            HasParts = true;
        }

        protected override void Disposed()
        {
            HasParts = false;
            BaseData = null;

            if(Parts != null)
            {
                foreach(PreviewEntityWrapper subpart in Parts)
                {
                    subpart.Close();
                }

                Parts.Clear();
            }
        }

        public override void Update(ref MatrixD drawMatrix)
        {
            if(!HasParts)
                return;

            foreach(PreviewEntityWrapper part in Parts)
            {
                MatrixD relativeMatrix = part.LocalMatrix.Value * drawMatrix;

                // invisible root subparts because game spawns them too
                part.Update(ref relativeMatrix, invisibleRoot: HideRootSubparts);
            }
        }
    }
}
