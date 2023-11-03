using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Serialization;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    // MyLargeGatlingTurret
    public class BData_GatlingTurret : BData_Turret
    {
        public static readonly char[] TurretSubpartSeparator = new[] { '/' };

        // MyLargeGatlingTurret.OnModelChange()
        public override bool GetTurretParts(IMyCubeBlock block, out MyEntity subpartBase1, out MyEntity subpartBase2, out MyEntity barrelPart)
        {
            MyCubeBlock internalBlock = (MyCubeBlock)block;
            MyLargeTurretBaseDefinition turretDef = (MyLargeTurretBaseDefinition)internalBlock.BlockDefinition;

            if(turretDef?.SubpartPairing != null)
            {
                subpartBase1 = GetTurretSubpart(internalBlock, turretDef.SubpartPairing, "Base1");
                subpartBase2 = GetTurretSubpart(internalBlock, turretDef.SubpartPairing, "Base2");

                barrelPart = GetTurretSubpart(internalBlock, turretDef.SubpartPairing, "Barrel");
            }
            else
            {
                subpartBase1 = internalBlock.Subparts.GetValueOrDefault("GatlingTurretBase1", null);
                subpartBase2 = subpartBase1?.Subparts.GetValueOrDefault("GatlingTurretBase2", null);
                barrelPart = null;

                if(subpartBase2 == null)
                    return false;

                MyEntitySubpart value;
                if(subpartBase2.Subparts.TryGetValue("GatlingBarrel", out value))
                    barrelPart = value;
                else if(subpartBase2 != null)
                    barrelPart = subpartBase2;
                else if(subpartBase1 != null)
                    barrelPart = subpartBase1;
            }

            return barrelPart != null;
        }

        // MyLargeGatlingBarrel.Init()
        protected override void BarrelInit(MyCubeBlock block, MyEntity entity, MyWeaponBlockDefinition def)
        {
            base.BarrelInit(block, entity, def);

            if(!GunBase_HasDummies)
            {
                MatrixD worldMatrixRef = entity.PositionComp.WorldMatrixRef;
                Vector3 position = 2.0 * worldMatrixRef.Forward;
                AddMuzzle(block, MyAmmoType.HighSpeed, Matrix.CreateTranslation(position));
            }
        }

        // HACK: also hardcoded in ModelPreview\Blocks\TurretBase.cs
        public static MyEntitySubpart GetTurretSubpart(MyCubeBlock block, SerializableDictionary<string, string> pairing, string key)
        {
            string value;
            if(!pairing.Dictionary.TryGetValue(key, out value))
                return null;

            MyEntity entity = block;

            string[] splits = value.Split(TurretSubpartSeparator);
            foreach(string part in splits)
            {
                MyEntitySubpart value2;
                if(!entity.Subparts.TryGetValue(part, out value2))
                    return null;

                entity = value2;
            }

            return entity as MyEntitySubpart;
        }
    }
}