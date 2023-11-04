using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public struct MuzzleData
    {
        public readonly Matrix Matrix_RelativeBarrel;
        public readonly Matrix Matrix_RelativePreview;
        public readonly bool IsMissile;

        public MuzzleData(Matrix local, Matrix preview, bool isMissile = false)
        {
            Matrix_RelativeBarrel = Matrix.Normalize(local);
            Matrix_RelativePreview = Matrix.Normalize(preview);
            IsMissile = isMissile;
        }
    }

    public abstract class BData_Weapon : BData_Base
    {
        public List<MuzzleData> Muzzles { get; private set; } = null;
        public bool GunBase_HasDummies { get; private set; } = false;

        public abstract bool GetBarrelPart(IMyCubeBlock block, out MyEntity barrelPart);

        protected abstract bool CheckWeapon(MyCubeBlock block, MyWeaponBlockDefinition def);

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool valid = true;

            // ignore weaponcore blocks
            if(!BuildInfoMod.Instance.CoreSystemsAPIHandler.Weapons.ContainsKey(def.Id))
            {
                try
                {
                    valid = CheckWeapon((MyCubeBlock)block, (MyWeaponBlockDefinition)def);
                }
                catch(Exception e)
                {
                    valid = false;
                    Log.Error($"Error processing weapon {def.Id} - it likely will crash the game when placed.\nIf the weapon is fine, report this to {BuildInfoMod.ModName} author.\n{e}");
                }
            }

            return base.IsValid(block, def) || valid;
        }

        protected void AddMuzzle(MyCubeBlock block, MyAmmoType ammoType, Matrix localMatrix, string dummyName = "")
        {
            if(Muzzles == null)
                Muzzles = new List<MuzzleData>();

            Matrix preview = Matrix.Identity;

            MyEntity barrelPart;
            if(GetBarrelPart(block, out barrelPart))
            {
                MatrixD world = localMatrix * barrelPart.WorldMatrix;
                preview = world * block.PositionComp.WorldMatrixInvScaled;
            }

            Muzzles.Add(new MuzzleData(localMatrix, preview, ammoType == MyAmmoType.Missile));
            GunBase_HasDummies = true;
        }

        protected void GunBase_LoadDummies(MyCubeBlock block, Dictionary<string, IMyModelDummy> dummies, Dictionary<int, string> dummyKeys)
        {
            foreach(KeyValuePair<string, IMyModelDummy> dummy in dummies)
            {
                if(DummyNameCheck(dummyKeys, MyGunBase.DUMMY_KEY_PROJECTILE, dummy.Key, "muzzle_projectile"))
                {
                    AddMuzzle(block, MyAmmoType.HighSpeed, dummy.Value.Matrix, dummy.Key);

                    //m_holdingDummyMatrix = Matrix.Invert(Matrix.CreateScale(1f / dummy.Value.Matrix.Scale) * dummy.Value.Matrix);
                }

                if(DummyNameCheck(dummyKeys, MyGunBase.DUMMY_KEY_MISSILE, dummy.Key, "muzzle_missile"))
                {
                    AddMuzzle(block, MyAmmoType.Missile, dummy.Value.Matrix, dummy.Key);
                }

                //if(DummyNameCheck(dummyKeys, MyGunBase.DUMMY_KEY_HOLDING, dummy.Key, "holding_dummy") || dummy.Key.IndexOf("holdingdummy", StringComparison.OrdinalIgnoreCase) != -1)
                //{
                //    m_holdingDummyMatrix = Matrix.Normalize(dummy.Value.Matrix);
                //}
            }
        }

        static bool DummyNameCheck(Dictionary<int, string> dummyNames, int key, string dummyName, string defaultValue)
        {
            string value;
            if(dummyNames != null
            // HACK: this is buggy in keen code (needs to be key instead of hardcoded missile), not changing it (last checked v202.112)
            && dummyNames.TryGetValue(MyGunBase.DUMMY_KEY_MISSILE, out value)
            && dummyName.IndexOf(value, StringComparison.OrdinalIgnoreCase) != -1)
                return true;

            return dummyName.IndexOf(defaultValue, StringComparison.OrdinalIgnoreCase) != -1;
        }
    }
}