using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_WeaponTurret : BData_Weapon
    {
        public Vector3D YawLocalPos { get; private set; }
        public Vector3D PitchLocalPos { get; private set; }

        bool GetTurretData(IMyCubeBlock block, MyCubeBlockDefinition def, string yawName, string pitchName)
        {
            MyEntitySubpart subpartYaw;
            if(block.TryGetSubpart(yawName, out subpartYaw))
            {
                YawLocalPos = Vector3D.Transform(subpartYaw.WorldMatrix.Translation, block.WorldMatrixInvScaled);
            }
            else
            {
                Log.Error($"Couldn't find {yawName} in block; def={def.Id.ToString()}");
                return false;
            }

            MyEntitySubpart subpartPitch;
            if(subpartYaw.TryGetSubpart(pitchName, out subpartPitch))
            {
                PitchLocalPos = Vector3D.Transform(subpartPitch.WorldMatrix.Translation, block.WorldMatrixInvScaled);
            }
            else
            {
                Log.Error($"Couldn't find {pitchName} in yaw subpart; def={def.Id.ToString()}");
                return false;
            }

            return true;
        }

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool valid = (block is IMyLargeTurretBase && ((MyCubeBlock)block).IsBuilt);
            if(valid)
            {
                if(block is IMyLargeGatlingTurret)
                {
                    valid = GetTurretData(block, def, "GatlingTurretBase1", "GatlingTurretBase2");
                    // there's also GatlingBarrel but don't need it.
                }
                else if(block is IMyLargeMissileTurret)
                {
                    valid = GetTurretData(block, def, "MissileTurretBase1", "MissileTurretBarrels");
                }
                else if(block is IMyLargeInteriorTurret)
                {
                    valid = GetTurretData(block, def, "InteriorTurretBase1", "InteriorTurretBase2");
                }
                else
                {
                    Log.Info($"WARNING: Unknown turret type: {def.Id.ToString()}. This can cause overlay to be inaccurate.");
                    valid = false;
                }
            }

            return base.IsValid(block, def) && valid;
        }
    }
}