#if false
using VRage.Game.Entity;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class InteriorTurret : MultiSubpartBase
    {
        protected override bool Initialized()
        {
            if(!base.Initialized())
                return false;

            // find yaw and pitch subparts and rotate them properly
            foreach(PreviewEntityWrapper part in Parts)
            {
                MyEntitySubpart subpart;
                if(part.Entity.TryGetSubpart("InteriorTurretBase2", out subpart))
                {
                    Matrix lm = subpart.PositionComp.LocalMatrixRef;
                    Vector3 pos = lm.Translation;
                    lm = lm * Matrix.CreateRotationX(-MathHelper.PiOver2);
                    lm.Translation = pos;
                    subpart.PositionComp.SetLocalMatrix(ref lm);

                    lm = part.LocalMatrix.Value;
                    pos = lm.Translation;
                    lm = lm * MatrixD.CreateRotationX(-MathHelper.PiOver2);
                    lm.Translation = pos;
                    part.LocalMatrix = lm;

                    HideRootSubparts = false; // don't hide root subpart as we've fixed its orientation
                    break;
                }
            }

            return true;
        }
    }
}
#endif