using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    // MyLargeInteriorTurret
    public class BData_InteriorTurret : BData_Turret
    {
        const string DummyBase1 = "InteriorTurretBase1";
        const string DummyBase2 = "InteriorTurretBase2";

        // MyLargeInteriorTurret.OnModelChange()
        public override bool GetTurretParts(IMyCubeBlock block, out MyEntity subpartBase1, out MyEntity subpartBase2, out MyEntity barrelPart)
        {
            MyCubeBlock internalBlock = (MyCubeBlock)block;

            subpartBase1 = internalBlock.Subparts.GetValueOrDefault(DummyBase1, null);
            subpartBase2 = subpartBase1?.Subparts.GetValueOrDefault(DummyBase2, null);
            barrelPart = subpartBase2;

            return barrelPart != null;
        }

        // MyLargeInteriorBarrel.Init()
        protected override void BarrelInit(MyCubeBlock block, MyEntity entity, MyWeaponBlockDefinition def)
        {
            base.BarrelInit(block, entity, def);

            if(!GunBase_HasDummies)
            {
                MatrixD worldMatrixRef = block.PositionComp.WorldMatrixRef;
                Vector3 position = -worldMatrixRef.Forward * 0.8;
                AddMuzzle(block, MyAmmoType.HighSpeed, Matrix.CreateTranslation(position));
            }
        }
    }
}