using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.BlockData
{
    public class BData_Weapons : BData_Base
    {
        public Matrix muzzleLocalMatrix;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var gun = (IMyGunObject<MyGunBase>)block;
            muzzleLocalMatrix = gun.GunBase.GetMuzzleLocalMatrix();
            return true;
        }
    }
}