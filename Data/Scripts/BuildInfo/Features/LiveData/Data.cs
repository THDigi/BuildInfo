using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class TurretInfo
    {
        public Vector3 YawLocalPos;
        public Vector3 YawModelCenter;
        public Vector3 PitchLocalPos;

        public void AssignData(MyCubeBlock block, MyEntity subpartYaw, MyEntity subpartPitch)
        {
            if(subpartYaw != null)
            {
                YawLocalPos = (Vector3)Vector3D.Transform(subpartYaw.WorldMatrix.Translation, block.PositionComp.WorldMatrixInvScaled);
                YawModelCenter = (Vector3)Vector3D.Transform(subpartYaw.PositionComp.WorldAABB.Center, block.PositionComp.WorldMatrixInvScaled);

                // avoid y-fighting if it's a multiple of grid size
                int y = (int)(YawLocalPos.Y * 100);
                int gs = (int)(block.CubeGrid.GridSize * 100);
                if(y % gs == 0)
                    YawLocalPos += new Vector3(0, 0.05f, 0);
            }

            if(subpartPitch != null)
            {
                PitchLocalPos = (Vector3)Vector3D.Transform(subpartPitch.WorldMatrix.Translation, block.PositionComp.WorldMatrixInvScaled);
            }
        }
    }

    public class TurretAttachmentInfo
    {
        public Matrix? RelativeSubpart;
        public Matrix? RelativePreview;

        public void AssignData(MyEntity subpart, MyCubeBlock block, string dummyName)
        {
            if(subpart?.Model != null)
            {
                IMyModelDummy cameraDummy = subpart.Model.GetDummies().GetValueOrDefault(dummyName, null);
                if(cameraDummy != null)
                {
                    RelativeSubpart = Matrix.Normalize(cameraDummy.Matrix);

                    MatrixD worldspace = RelativeSubpart.Value * subpart.WorldMatrix;
                    RelativePreview = worldspace * block.PositionComp.WorldMatrixInvScaled; // to block-local
                }
            }
        }
    }
}