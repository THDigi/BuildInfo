using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Weapon : BData_Base
    {
        public Matrix muzzleLocalMatrix;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var gun = (IMyGunObject<MyGunBase>)block;
            muzzleLocalMatrix = gun.GunBase.GetMuzzleLocalMatrix();
            return base.IsValid(block, def) || true;
        }
    }
}