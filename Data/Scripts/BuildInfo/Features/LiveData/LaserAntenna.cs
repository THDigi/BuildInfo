using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_LaserAntenna : BData_Base
    {
        public Vector3D YawLocalPos;
        public Vector3D PitchLocalPos;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            // from MyLaserAntenna.OnModelChange()
            bool valid = BData_WeaponTurret.GetTurretData(block, def, "LaserComTurret", "LaserCom", out YawLocalPos, out PitchLocalPos);
            return base.IsValid(block, def) || valid;
        }
    }
}