using System.Collections.Generic;
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
        // Last checked on v01_198_031.
        // All these are from Sandbox.Game.GameSystems.MyGridGasSystem.
        // Since that namespace is prohibited I have to copy it and convert it to work with modAPI.

        public static AirTightMode IsAirtightFromDefinition(MyCubeBlockDefinition def, float buildLevelRatio)
        {
            if(def.BuildProgressModels != null && def.BuildProgressModels.Length != 0)
            {
                MyCubeBlockDefinition.BuildProgressModel progressModel = def.BuildProgressModels[def.BuildProgressModels.Length - 1];

                if(buildLevelRatio < progressModel.BuildRatioUpperBound)
                    return AirTightMode.NOT_SEALED;
            }

            if(!def.IsAirTight.HasValue)
                return AirTightMode.USE_MOUNTS;

            return (def.IsAirTight.Value ? AirTightMode.SEALED : AirTightMode.NOT_SEALED);
        }

        public static bool IsAirtightBetweenPositions(IMyCubeGrid grid, Vector3I startPos, Vector3I endPos)
        {
            IMySlimBlock b1 = grid.GetCubeBlock(startPos);
            IMySlimBlock b2 = grid.GetCubeBlock(endPos);

            if(b1 == b2)
            {
                if(b1 != null)
                {
                    MyCubeBlockDefinition def = b1.BlockDefinition as MyCubeBlockDefinition;

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
            MyCubeBlockDefinition def = block.BlockDefinition as MyCubeBlockDefinition;
            if(def == null)
                return false;

            AirTightMode airtight = IsAirtightFromDefinition(def, block.BuildLevelRatio);
            if(airtight != AirTightMode.USE_MOUNTS)
                return (airtight == AirTightMode.SEALED);

            Matrix matrix;
            block.Orientation.GetMatrix(out matrix);
            matrix.TransposeRotationInPlace();

            Vector3 position = (block.FatBlock == null ? Vector3.Zero : (pos - block.FatBlock.Position));
            Vector3I cell = Vector3I.Round(Vector3.Transform(position, matrix) + def.Center);
            Vector3I side = Vector3I.Round(Vector3.Transform(normal, matrix));

            MyCubeBlockDefinition.MyCubePressurizationMark pressurized = def.IsCubePressurized[cell][side];

            if(pressurized == MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedAlways)
                return true;

            IMyDoor door = block.FatBlock as IMyDoor;

            if(door != null && (door.Status == DoorStatus.Closed || door.Status == DoorStatus.Closing))
            {
                if(pressurized == MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedClosed)
                    return true;
                else
                    return IsDoorAirtightInternal(def, ref side, door.IsFullyClosed);
            }

            return false;
        }

        public static bool IsDoorAirtight(MyCubeBlockDefinition def, ref Vector3I normalLocal, bool fullyClosed)
        {
            AirTightMode isAirTight = IsAirtightFromDefinition(def, 1f);

            if(isAirTight != AirTightMode.USE_MOUNTS)
                return (isAirTight == AirTightMode.SEALED);

            return IsDoorAirtightInternal(def, ref normalLocal, fullyClosed);
        }

        public static bool IsDoorAirtightInternal(MyCubeBlockDefinition def, ref Vector3I normalLocal, bool fullyClosed)
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
                    MyCubeBlockDefinition.MountPoint[] mountPoints = def.MountPoints;

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

        /// <summary>
        /// Gets if the block is fully airtight or not, as well as how many faces are of each kind.
        /// </summary>
        public static AirTightMode GetAirTightFaces(MyCubeBlockDefinition def, out int airTightFaces, out int toggledAirtightFaces, out int totalFaces)
        {
            airTightFaces = 0;
            toggledAirtightFaces = 0;
            totalFaces = 0;

            if(def.IsAirTight.HasValue)
                return (def.IsAirTight.Value ? AirTightMode.SEALED : AirTightMode.NOT_SEALED);

            HashSet<Vector3I> cubes = BuildInfoMod.Instance.Caches.Vector3ISet;
            cubes.Clear();

            foreach(KeyValuePair<Vector3I, Dictionary<Vector3I, MyCubeBlockDefinition.MyCubePressurizationMark>> kv in def.IsCubePressurized)
            {
                cubes.Add(kv.Key);
            }

            foreach(KeyValuePair<Vector3I, Dictionary<Vector3I, MyCubeBlockDefinition.MyCubePressurizationMark>> kv in def.IsCubePressurized)
            {
                foreach(KeyValuePair<Vector3I, MyCubeBlockDefinition.MyCubePressurizationMark> kv2 in kv.Value)
                {
                    if(cubes.Contains(kv.Key + kv2.Key))
                        continue;

                    switch(kv2.Value)
                    {
                        case MyCubeBlockDefinition.MyCubePressurizationMark.NotPressurized:
                            break;

                        case MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedAlways:
                            airTightFaces++;
                            break;

                        case MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedClosed:
                            if(def is MyDoorDefinition)
                                toggledAirtightFaces++;
                            break;
                    }

                    totalFaces++;
                }
            }

            cubes.Clear();

            if(airTightFaces == 0)
                return AirTightMode.NOT_SEALED;

            if(airTightFaces == totalFaces)
                return AirTightMode.SEALED;

            return AirTightMode.USE_MOUNTS;
        }
    }
}
