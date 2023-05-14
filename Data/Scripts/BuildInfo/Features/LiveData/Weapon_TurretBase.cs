using System;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public abstract class BData_Turret : BData_Weapon
    {
        public TurretInfo TurretInfo;
        public TurretAttachmentInfo Camera;

        public abstract bool GetTurretParts(IMyCubeBlock block, out MyEntity subpartBase1, out MyEntity subpartBase2, out MyEntity barrelPart);

        public override bool GetBarrelPart(IMyCubeBlock block, out MyEntity barrelPart)
        {
            MyEntity subpartBase1;
            MyEntity subpartBase2;
            return GetTurretParts(block, out subpartBase1, out subpartBase2, out barrelPart);
        }

        protected override bool CheckWeapon(MyCubeBlock block, MyWeaponBlockDefinition def)
        {
            if(!block.IsBuilt)
                return false;

            if(Environment.CurrentManagedThreadId == 1)
            {
                // HACK: make turret update its model sooner, fixes interior turret in particular.
                block.UpdateOnceBeforeFrame();
            }

            MyEntity subpartBase1;
            MyEntity subpartBase2;
            MyEntity barrelPart;
            if(!GetTurretParts(block, out subpartBase1, out subpartBase2, out barrelPart))
                return false;

            MyLargeTurretBaseDefinition turretDef = (MyLargeTurretBaseDefinition)def;

            BarrelInit(block, barrelPart, turretDef);

            TurretInfo = new TurretInfo();
            TurretInfo.AssignData(block, subpartBase1, subpartBase2);

            Camera = new TurretAttachmentInfo();
            Camera.AssignData(subpartBase2, block, turretDef.CameraDummyName);

            return true;
        }

        protected virtual void BarrelInit(MyCubeBlock block, MyEntity entity, MyWeaponBlockDefinition def)
        {
            if(entity.Model != null)
            {
                // MyLargeBarrelBase.CameraDummy is not used
                //Dictionary<string, IMyModelDummy> dummies = entity.Model.GetDummies();
                //IMyModelDummy cameraDummy = dummies.GetValueOrDefault("camera", null);

                GunBase_LoadDummies(block, entity.Model.GetDummies(), def.DummyNames);
            }
        }
    }
}