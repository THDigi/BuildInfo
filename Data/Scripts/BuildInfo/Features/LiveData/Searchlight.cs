using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Searchlight : BData_Base
    {
        public TurretInfo TurretInfo;
        public TurretAttachmentInfo Camera;
        public TurretAttachmentInfo Light;
        public float LightRadius;

        const string DummyBase1 = "InteriorTurretBase1";
        const string DummyBase2 = "InteriorTurretBase2";

        // from MyLaserAntenna.OnModelChange()
        public bool GetTurretParts(IMyCubeBlock block, out MyEntity subpartBase1, out MyEntity subpartBase2, out MyEntity barrelPart)
        {
            MyCubeBlock internalBlock = (MyCubeBlock)block;

            subpartBase1 = internalBlock.Subparts.GetValueOrDefault(DummyBase1, null);
            subpartBase2 = subpartBase1?.Subparts.GetValueOrDefault(DummyBase2, null);
            barrelPart = subpartBase2;

            return barrelPart != null;
        }

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool valid = false;
            MyEntity subpartBase1;
            MyEntity subpartBase2;
            MyEntity barrelPart;
            if(GetTurretParts(block, out subpartBase1, out subpartBase2, out barrelPart))
            {
                MySearchlightDefinition lightDef = (MySearchlightDefinition)def;

                Vector3 size = subpartBase2.PositionComp.LocalAABB.Size;
                LightRadius = Math.Min(size.X, size.Y) / 2f;

                MyCubeBlock internalBlock = (MyCubeBlock)block;

                TurretInfo = new TurretInfo();
                TurretInfo.AssignData(internalBlock, subpartBase1, subpartBase2);

                Camera = new TurretAttachmentInfo();
                Camera.AssignData(subpartBase2, internalBlock, "camera");

                Light = new TurretAttachmentInfo();
                Light.AssignData(subpartBase2, internalBlock, lightDef.LightDummyName);

                valid = true;
            }

            return base.IsValid(block, def) || valid;
        }
    }
}