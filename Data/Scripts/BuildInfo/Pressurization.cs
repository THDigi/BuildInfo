using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;

namespace Digi.BuildInfo
{
    public static class Pressurization
    {
        // TODO update this code when changed in game.
        // Last checked on v1.186.500.
        // All these are from Sandbox.Game.GameSystems.MyGridGasSystem.
        // Since that namespace is prohibited I have to copy it and convert it to work with modAPI.

        public enum AirTightMode
        {
            USE_MOUNTS,
            SEALED,
            NOT_SEALED
        }

        private static AirTightMode IsAirtightFromDefinition(MyCubeBlockDefinition def, float buildLevelRatio)
        {
            if(def.BuildProgressModels != null && def.BuildProgressModels.Length > 0)
            {
                var progressModel = def.BuildProgressModels[def.BuildProgressModels.Length - 1];

                if(buildLevelRatio < progressModel.BuildRatioUpperBound)
                    return AirTightMode.NOT_SEALED;
            }

            if(!def.IsAirTight.HasValue)
                return AirTightMode.USE_MOUNTS;

            return (def.IsAirTight.Value ? AirTightMode.SEALED : AirTightMode.NOT_SEALED);
        }

        public static bool IsAirtightBetweenPositions(IMyCubeGrid grid, Vector3I startPos, Vector3I endPos)
        {
            var b1 = grid.GetCubeBlock(startPos);
            var b2 = grid.GetCubeBlock(endPos);

            if(b1 != b2)
            {
                return (b1 != null && IsAirtightBlock(b1, startPos, endPos - startPos))
                    || (b2 != null && IsAirtightBlock(b2, endPos, startPos - endPos));
            }

            if(b1 != null)
            {
                var isAirTight = IsAirtightFromDefinition((MyCubeBlockDefinition)b1.BlockDefinition, b1.BuildLevelRatio);

                return (isAirTight == AirTightMode.SEALED);
            }

            return false;
        }

        private static bool IsAirtightBlock(IMySlimBlock block, Vector3I pos, Vector3 normal)
        {
            var def = (MyCubeBlockDefinition)block.BlockDefinition;
            var isAirTight = IsAirtightFromDefinition(def, block.BuildLevelRatio);

            if(isAirTight != AirTightMode.USE_MOUNTS)
                return (isAirTight == AirTightMode.SEALED);

            Matrix matrix;
            block.Orientation.GetMatrix(out matrix);
            matrix.TransposeRotationInPlace();

            var position = (block.FatBlock != null ? pos - block.FatBlock.Position : Vector3.Zero);
            var cell = Vector3I.Round(Vector3.Transform(position, matrix) + def.Center);
            var side = Vector3I.Round(Vector3.Transform(normal, matrix));

            if(def.IsCubePressurized[cell][side])
                return true;

            var door = block.FatBlock as IMyDoor;

            if(door != null)
                return IsDoorAirtight(def, ref side, (door.Status == DoorStatus.Closed));

            return false;
        }

        public static bool IsDoorAirtight(MyCubeBlockDefinition def, ref Vector3I normalLocal, bool fullyClosed)
        {
            if(def is MyAirtightSlideDoorDefinition)
            {
                return (fullyClosed && normalLocal == Vector3I.Forward);
            }
            else if(def is MyAirtightDoorGenericDefinition)
            {
                return (fullyClosed && (normalLocal == Vector3I.Forward || normalLocal == Vector3I.Backward));
            }
            else // any other door
            {
                if(fullyClosed)
                {
                    var mountPoints = def.MountPoints;

                    for(int i = 0; i < mountPoints.Length; i++)
                    {
                        if(normalLocal == mountPoints[i].Normal)
                            return false;
                    }

                    return true;
                }

                return false;
            }
        }
    }
}
