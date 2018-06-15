using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo
{
    public static class Pressurization
    {
        // TODO update this code when changed in game.
        // Last checked on v1.186.500.
        // All these are from Sandbox.Game.GameSystems.MyGridGasSystem.
        // Since that namespace is prohibited I have to copy it and convert it to work with modAPI.

        public static bool IsPressurized(IMyCubeGrid grid, Vector3I startPos, Vector3I endPos)
        {
            IMySlimBlock b1 = grid.GetCubeBlock(startPos);
            IMySlimBlock b2 = grid.GetCubeBlock(endPos);

            if(b1 == b2)
                return b1 != null && ((MyCubeBlockDefinition)b1.BlockDefinition).IsAirTight;

            return (b1 != null && (((MyCubeBlockDefinition)b1.BlockDefinition).IsAirTight || IsPressurized(b1, startPos, endPos - startPos)))
                || (b2 != null && (((MyCubeBlockDefinition)b2.BlockDefinition).IsAirTight || IsPressurized(b2, endPos, startPos - endPos)));
        }

        public static bool IsPressurized(IMySlimBlock block, Vector3I gridPos, Vector3 normal)
        {
            var def = (MyCubeBlockDefinition)block.BlockDefinition;

            if(def.BuildProgressModels.Length > 0)
            {
                MyCubeBlockDefinition.BuildProgressModel buildProgressModel = def.BuildProgressModels[def.BuildProgressModels.Length - 1];

                if(block.BuildLevelRatio < buildProgressModel.BuildRatioUpperBound)
                    return false;
            }

            Matrix matrix;
            block.Orientation.GetMatrix(out matrix);
            matrix.TransposeRotationInPlace();

            Vector3I cellNormal = Vector3I.Round(Vector3.Transform(normal, matrix));
            Vector3 position = (block.FatBlock != null ? (gridPos - block.FatBlock.Position) : Vector3.Zero);
            Vector3I cellPosition = Vector3I.Round(Vector3.Transform(position, matrix) + def.Center);

            if(def.IsCubePressurized[cellPosition][cellNormal])
                return true;

            var door = block.FatBlock as IMyDoor;

            if(door != null)
                return IsDoorFacePressurized(def, cellNormal, door.OpenRatio <= 0.05f);

            return false;
        }

        public static bool IsDoorFacePressurized(MyCubeBlockDefinition def, Vector3I normalLocal, bool fullyClosed)
        {
            if(def is MyDoorDefinition || def is MyAdvancedDoorDefinition)
            {
                if(fullyClosed)
                {
                    var mountPoints = def.MountPoints;

                    for(int i = 0; i < mountPoints.Length; i++)
                    {
                        if(normalLocal == mountPoints[i].Normal)
                        {
                            return false;
                        }
                    }

                    return true;
                }

                return false;
            }
            else if(def is MyAirtightSlideDoorDefinition)
            {
                return (fullyClosed && normalLocal == Vector3I.Forward);
            }
            else if(def is MyAirtightDoorGenericDefinition)
            {
                return (fullyClosed && (normalLocal == Vector3I.Forward || normalLocal == Vector3I.Backward));
            }

            return false;
        }
    }
}
