using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;
using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;

namespace Digi.BuildInfo.VanillaData
{
    public enum AirTightMode
    {
        USE_MOUNTS,
        SEALED,
        NOT_SEALED
    }

    public static class Pressurization
    {
        // TODO: update this code when changed in game.
        // Last checked on v01_193_019.
        // All these are from Sandbox.Game.GameSystems.MyGridGasSystem.
        // Since that namespace is prohibited I have to copy it and convert it to work with modAPI.

        public static AirTightMode IsAirtightFromDefinition(MyCubeBlockDefinition def, float buildLevelRatio)
        {
            if(def.BuildProgressModels != null && def.BuildProgressModels.Length != 0)
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

            if(b1 == b2)
            {
                if(b1 != null)
                {
                    var def = b1.BlockDefinition as MyCubeBlockDefinition;

                    if(def != null)
                        return (IsAirtightFromDefinition(def, b1.BuildLevelRatio) == AirTightMode.SEALED);

                    return false;
                }

                return false;
            }

            if(b1 != null && IsAirtightBlock(b1, startPos, endPos - startPos))
                return true;

            if(b2 != null)
                return IsAirtightBlock(b2, endPos, startPos - endPos);

            return false;
        }

        private static bool IsAirtightBlock(IMySlimBlock block, Vector3I pos, Vector3 normal)
        {
            var def = block.BlockDefinition as MyCubeBlockDefinition;
            if(def == null)
                return false;

            var airtight = IsAirtightFromDefinition(def, block.BuildLevelRatio);
            if(airtight != AirTightMode.USE_MOUNTS)
                return (airtight == AirTightMode.SEALED);

            Matrix matrix;
            block.Orientation.GetMatrix(out matrix);
            matrix.TransposeRotationInPlace();

            Vector3 position = (block.FatBlock == null ? Vector3.Zero : (pos - block.FatBlock.Position));
            Vector3I cell = Vector3I.Round(Vector3.Transform(position, matrix) + def.Center);
            Vector3I side = Vector3I.Round(Vector3.Transform(normal, matrix));

            if(def.IsCubePressurized[cell][side])
                return true;

            IMyDoor door = block.FatBlock as IMyDoor;

            if(door != null && (door.Status == DoorStatus.Closed || door.Status == DoorStatus.Closing))
                return IsDoorAirtightInternal(def, ref side, door.IsFullyClosed);

            return false;
        }

        public static bool IsDoorAirtight(MyCubeBlockDefinition def, ref Vector3I normalLocal, bool fullyClosed)
        {
            var isAirTight = IsAirtightFromDefinition(def, 1f);

            if(isAirTight != AirTightMode.USE_MOUNTS)
                return (isAirTight == AirTightMode.SEALED);

            return IsDoorAirtightInternal(def, ref normalLocal, fullyClosed);
        }

        private static bool IsDoorAirtightInternal(MyCubeBlockDefinition def, ref Vector3I normalLocal, bool fullyClosed)
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
