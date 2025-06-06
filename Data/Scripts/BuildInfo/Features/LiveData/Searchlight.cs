using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Searchlight : BData_Base
    {
        public TurretInfo TurretInfo;
        public TurretAttachmentInfo Camera;
        public TurretAttachmentInfo LightSubpart;
        public LightLogicData LightLogicData;

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
            var searchlightDef = def as MySearchlightDefinition;
            if(searchlightDef != null)
            {
                // data matching MyLightingLogic.ctor(..., MySearchlightDefinition)
                LightLogicData = new LightLogicData(block, searchlightDef.LightDummyName, searchlightDef.LightReflectorRadius,
                    searchlightDef.LightReflectorRadius.Max, searchlightDef.LightOffset, searchlightDef.ReflectorConeDegrees);
            }
            else
                Log.Error($"Unexpected for '{def.Id}' to not have a searchlight definition! Might cause issues in general, check definition xsi:type.");

            bool valid = false;
            MyEntity subpartBase1;
            MyEntity subpartBase2;
            MyEntity barrelPart;
            if(GetTurretParts(block, out subpartBase1, out subpartBase2, out barrelPart))
            {
                MySearchlightDefinition lightDef = (MySearchlightDefinition)def;

                MyCubeBlock internalBlock = (MyCubeBlock)block;

                TurretInfo = new TurretInfo();
                TurretInfo.AssignData(internalBlock, subpartBase1, subpartBase2);

                Camera = new TurretAttachmentInfo();
                Camera.AssignData(internalBlock, subpartBase2, "camera", lightDef.ForwardCameraOffset, lightDef.UpCameraOffset);

                LightSubpart = new TurretAttachmentInfo();
                LightSubpart.AssignData(internalBlock, subpartBase2, lightDef.LightDummyName);

                valid = true;
            }

            return base.IsValid(block, def) || valid;
        }
    }
}