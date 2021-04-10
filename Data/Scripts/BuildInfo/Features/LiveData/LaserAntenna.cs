using Sandbox.Definitions;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_LaserAntenna : BData_Base
    {
        public Vector3D YawLocalPos { get; private set; }
        public Vector3D PitchLocalPos { get; private set; }

        bool GetTurretData(IMyCubeBlock block, MyCubeBlockDefinition def, string yawName, string pitchName)
        {
            MyEntitySubpart subpartYaw;
            if(block.TryGetSubpart(yawName, out subpartYaw))
            {
                YawLocalPos = Vector3D.Transform(subpartYaw.WorldMatrix.Translation, block.WorldMatrixInvScaled);
            }
            else
            {
                Log.Error($"Couldn't find {yawName} in block; def={def.Id.ToString()}");
                return false;
            }

            MyEntitySubpart subpartPitch;
            if(subpartYaw.TryGetSubpart(pitchName, out subpartPitch))
            {
                PitchLocalPos = Vector3D.Transform(subpartPitch.WorldMatrix.Translation, block.WorldMatrixInvScaled);
            }
            else
            {
                Log.Error($"Couldn't find {pitchName} in yaw subpart; def={def.Id.ToString()}");
                return false;
            }

            return true;
        }

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            // from MyLaserAntenna.OnModelChange()
            bool valid = GetTurretData(block, def, "LaserComTurret", "LaserCom");
            return base.IsValid(block, def) || valid;
        }
    }
}