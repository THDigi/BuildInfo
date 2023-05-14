using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    // MyLargeGatlingTurret
    public class BData_GatlingGun : BData_Weapon
    {
        const string DummyBarrel = "Barrel";

        public override bool GetBarrelPart(IMyCubeBlock block, out MyEntity barrelPart)
        {
            MyCubeBlock internalBlock = (MyCubeBlock)block;

            barrelPart = internalBlock.Subparts.GetValueOrDefault(DummyBarrel, null);

            // MyGunBase.TrySubscribeToEntityEvents() is where it falls back to block
            if(barrelPart == null)
                barrelPart = internalBlock;

            return barrelPart != null;
        }

        // MySmallGatlingGun.OnModelChange()/GetBarrelAndMuzzle()
        protected override bool CheckWeapon(MyCubeBlock block, MyWeaponBlockDefinition def)
        {
            if(!block.IsBuilt)
                return false;

            MyEntity barrel = block.Subparts.GetValueOrDefault(DummyBarrel, null);

            Dictionary<string, IMyModelDummy> dummies = (barrel ?? block).Model.GetDummies();

            GunBase_LoadDummies(block, dummies, def.DummyNames);

            if(!GunBase_HasDummies)
            {
                const string Muzzle = "Muzzle";
                if(dummies.ContainsKey(Muzzle))
                {
                    AddMuzzle(block, MyAmmoType.HighSpeed, dummies[Muzzle].Matrix, Muzzle);
                }
                else
                {
                    Matrix localMatrix = Matrix.CreateTranslation(new Vector3(0f, 0f, -1f));
                    AddMuzzle(block, MyAmmoType.HighSpeed, localMatrix, Muzzle);
                }
            }

            return true;
        }
    }
}