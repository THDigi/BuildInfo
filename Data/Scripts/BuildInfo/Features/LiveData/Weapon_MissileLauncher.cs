using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    // MySmallMissileLauncher/MySmallMissileLauncherReload
    public class BData_MissileLauncher : BData_Weapon
    {
        public override bool GetBarrelPart(IMyCubeBlock block, out MyEntity barrelPart)
        {
            barrelPart = (MyCubeBlock)block;
            return true;
        }

        // MySmallMissileLauncher.LoadDummies()
        protected override bool CheckWeapon(MyCubeBlock block, MyWeaponBlockDefinition def)
        {
            //MyModel modelOnlyDummies = MyModels.GetModelOnlyDummies(BlockDefinition.Model);

            if(!block.IsBuilt)
                return false;

            Dictionary<string, IMyModelDummy> dummies = block.Model.GetDummies();

            GunBase_LoadDummies(block, dummies, def.DummyNames);

            if(!GunBase_HasDummies)
            {
                foreach(IMyModelDummy dummy in dummies.Values)
                {
                    if(dummy.Name.IndexOf("barrel", StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        AddMuzzle(block, MyAmmoType.Missile, dummy.Matrix);
                    }
                }
            }

            return true;
        }
    }
}