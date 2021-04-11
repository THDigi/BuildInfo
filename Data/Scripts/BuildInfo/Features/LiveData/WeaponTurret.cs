using System;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_WeaponTurret : BData_Weapon
    {
        public Vector3D YawLocalPos;
        public Vector3D PitchLocalPos;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool valid = (block is IMyLargeTurretBase && ((MyCubeBlock)block).IsBuilt);
            if(valid)
            {
                if(block is IMyLargeGatlingTurret)
                {
                    valid = GetTurretData(block, def, "GatlingTurretBase1", "GatlingTurretBase2", out YawLocalPos, out PitchLocalPos);
                    // there's also GatlingBarrel but don't need it.
                }
                else if(block is IMyLargeMissileTurret)
                {
                    valid = GetTurretData(block, def, "MissileTurretBase1", "MissileTurretBarrels", out YawLocalPos, out PitchLocalPos);
                }
                else if(block is IMyLargeInteriorTurret)
                {
                    valid = GetTurretData(block, def, "InteriorTurretBase1", "InteriorTurretBase2", out YawLocalPos, out PitchLocalPos);
                }
                else
                {
                    Log.Info($"WARNING: Unknown turret type: {def.Id.ToString()}. This can cause overlay to be inaccurate.");
                    valid = false;
                }
            }

            return base.IsValid(block, def) && valid;
        }

        public static bool GetTurretData(IMyCubeBlock block, MyCubeBlockDefinition def, string yawName, string pitchName, out Vector3D yawLocal, out Vector3D pitchLocal)
        {
            yawLocal = default(Vector3D);
            pitchLocal = default(Vector3D);

            MyEntitySubpart subpartYaw;
            if(block.TryGetSubpart(yawName, out subpartYaw))
            {
                yawLocal = Vector3D.Transform(subpartYaw.WorldMatrix.Translation, block.WorldMatrixInvScaled);

                // avoid y-fighting if it's a multiple of grid size
                int y = (int)(yawLocal.Y * 100);
                int gs = (int)(block.CubeGrid.GridSize * 100);
                if(y % gs == 0)
                    yawLocal += new Vector3D(0, 0.05f, 0);
            }
            else
            {
                Log.Error($"Couldn't find {yawName} in block; def={def.Id.ToString()}");
                return false;
            }

            MyEntitySubpart subpartPitch;
            if(subpartYaw.TryGetSubpart(pitchName, out subpartPitch))
            {
                pitchLocal = Vector3D.Transform(subpartPitch.WorldMatrix.Translation, block.WorldMatrixInvScaled);
            }
            else
            {
                Log.Error($"Couldn't find {pitchName} in yaw subpart; def={def.Id.ToString()}");
                return false;
            }

            return true;
        }
    }
}