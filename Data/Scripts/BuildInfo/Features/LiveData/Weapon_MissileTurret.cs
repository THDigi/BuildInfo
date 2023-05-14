using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    // MyLargeMissileTurret
    public class BData_MissileTurret : BData_Turret
    {
        const string DummyBase1 = "MissileTurretBase1";
        const string DummyBase2 = "MissileTurretBarrels";

        // MyLargeMissileTurret.OnModelChange()
        public override bool GetTurretParts(IMyCubeBlock block, out MyEntity subpartBase1, out MyEntity subpartBase2, out MyEntity barrelPart)
        {
            MyCubeBlock internalBlock = (MyCubeBlock)block;

            subpartBase1 = internalBlock.Subparts.GetValueOrDefault(DummyBase1, null);
            subpartBase2 = subpartBase1?.Subparts.GetValueOrDefault(DummyBase2, null);
            barrelPart = subpartBase2;

            return barrelPart != null;
        }

        // MyLargeMissileBarrel.Init()
        protected override void BarrelInit(MyCubeBlock block, MyEntity entity, MyWeaponBlockDefinition def)
        {
            base.BarrelInit(block, entity, def);

            if(!GunBase_HasDummies)
            {
                Matrix lm = Matrix.Identity;
                Vector3 translation = lm.Translation;
                MatrixD worldMatrixRef = entity.PositionComp.WorldMatrixRef;
                lm.Translation = translation + (Vector3)(worldMatrixRef.Forward * 3.0);
                AddMuzzle(block, MyAmmoType.Missile, lm);
            }
        }
    }
}