using Sandbox.Definitions;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_LaserAntenna : BData_Base
    {
        public TurretData Turret;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            // from MyLaserAntenna.OnModelChange()
            bool valid = BData_Weapon.GetTurretData(block, out Turret, "LaserComTurret", "LaserCom");
            return base.IsValid(block, def) || valid;
        }
    }
}