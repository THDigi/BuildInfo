using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Blocks
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SmallGatlingGun), useEntityUpdate: false)]
    public class BlockGatlingGun : BlockBase<BlockDataWeapons> { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SmallMissileLauncher), useEntityUpdate: false)]
    public class BlockMissileLauncher : BlockBase<BlockDataWeapons> { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_SmallMissileLauncherReload), useEntityUpdate: false)]
    public class BlockMissileLauncherReload : BlockBase<BlockDataWeapons> { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeGatlingTurret), useEntityUpdate: false)]
    public class BlockGatlingTurret : BlockBase<BlockDataWeapons> { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_LargeMissileTurret), useEntityUpdate: false)]
    public class BlockMissileTurret : BlockBase<BlockDataWeapons> { }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_InteriorTurret), useEntityUpdate: false)]
    public class BlockInteriorTurret : BlockBase<BlockDataWeapons> { }

    public class BlockDataWeapons : BlockDataBase
    {
        public Matrix muzzleLocalMatrix;

        public override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            var gun = (IMyGunObject<MyGunBase>)block;
            muzzleLocalMatrix = gun.GunBase.GetMuzzleLocalMatrix();
            return true;
        }
    }
}