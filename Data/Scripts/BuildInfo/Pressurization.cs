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
        // TODO update this code when changed in game
        // all these are from Sandbox.Game.GameSystems.MyGridGasSystem
        // since that namespace is prohibited I have to copy it and convert it to work with modAPI

        public static bool IsPressurized(IMyCubeGrid grid, Vector3I startPos, Vector3I endPos)
        {
            IMySlimBlock b1 = grid.GetCubeBlock(startPos);
            IMySlimBlock b2 = grid.GetCubeBlock(endPos);

            if(b1 == b2)
                return b1 != null && ((MyCubeBlockDefinition)b1.BlockDefinition).IsAirTight;

            return (b1 != null && (((MyCubeBlockDefinition)b1.BlockDefinition).IsAirTight || IsPressurized(b1, startPos, endPos - startPos))) || (b2 != null && (((MyCubeBlockDefinition)b2.BlockDefinition).IsAirTight || IsPressurized(b2, endPos, startPos - endPos)));
        }

        public static bool IsPressurized(IMySlimBlock block, Vector3I pos, Vector3 normal)
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

            Vector3 vector = Vector3.Transform(normal, matrix);
            Vector3 position = Vector3.Zero;

            if(block.FatBlock != null)
                position = pos - block.FatBlock.Position;

            Vector3 value = Vector3.Transform(position, matrix) + def.Center;

            if(def.IsCubePressurized[Vector3I.Round(value)][Vector3I.Round(vector)])
                return true;

            if(block.FatBlock != null)
            {
                MyCubeBlock fatBlock = (MyCubeBlock)block.FatBlock;
                bool result;

                if(fatBlock is MyDoor)
                {
                    MyDoor myDoor = fatBlock as MyDoor;

                    if(!myDoor.Open)
                    {
                        MyCubeBlockDefinition.MountPoint[] mountPoints = def.MountPoints;

                        for(int i = 0; i < mountPoints.Length; i++)
                        {
                            MyCubeBlockDefinition.MountPoint mountPoint = mountPoints[i];

                            if(vector == mountPoint.Normal)
                            {
                                result = false;
                                return result;
                            }
                        }

                        return true;
                    }

                    return false;
                }
                else if(fatBlock is MyAdvancedDoor)
                {
                    MyAdvancedDoor myAdvancedDoor = fatBlock as MyAdvancedDoor;

                    if(myAdvancedDoor.FullyClosed)
                    {
                        MyCubeBlockDefinition.MountPoint[] mountPoints2 = def.MountPoints;
                        for(int j = 0; j < mountPoints2.Length; j++)
                        {
                            MyCubeBlockDefinition.MountPoint mountPoint2 = mountPoints2[j];

                            if(vector == mountPoint2.Normal)
                            {
                                result = false;
                                return result;
                            }
                        }

                        return true;
                    }

                    return false;
                }
                else if(fatBlock is MyAirtightSlideDoor)
                {
                    MyAirtightDoorGeneric myAirtightDoorGeneric = fatBlock as MyAirtightDoorGeneric;

                    if(myAirtightDoorGeneric.IsFullyClosed && vector == Vector3.Forward)
                        return true;

                    return false;
                }
                else
                {
                    if(!(fatBlock is MyAirtightDoorGeneric))
                        return false;

                    MyAirtightDoorGeneric myAirtightDoorGeneric2 = fatBlock as MyAirtightDoorGeneric;

                    if(myAirtightDoorGeneric2.IsFullyClosed && (vector == Vector3.Forward || vector == Vector3.Backward))
                        return true;

                    return false;
                }
            }

            return false;
        }

#if false
        public static bool TestPressurize(Vector3I pos, Vector3 normal, Matrix matrix, MyCubeBlockDefinition def, float buildLevelRatio = 1f)
        {
            if(def.BuildProgressModels.Length > 0)
            {
                MyCubeBlockDefinition.BuildProgressModel buildProgressModel = def.BuildProgressModels[def.BuildProgressModels.Length - 1];

                if(buildLevelRatio < buildProgressModel.BuildRatioUpperBound)
                    return false;
            }

            matrix.TransposeRotationInPlace();

            Vector3 vector = Vector3.Transform(normal, matrix);
            Vector3 position = Vector3.Zero;

            //if(block.FatBlock != null)
            //    position = pos - block.FatBlock.Position;

            Vector3 value = Vector3.Transform(position, matrix) + def.Center;

            // DEBUG
            {
                if(!def.IsCubePressurized.ContainsKey(Vector3I.Round(value)))
                {
                    MyAPIGateway.Utilities.ShowNotification($"can't find 1st: {Vector3I.Round(value)}", 16);
                    return false;
                }

                if(!def.IsCubePressurized[Vector3I.Round(value)].ContainsKey(Vector3I.Round(vector)))
                {
                    MyAPIGateway.Utilities.ShowNotification($"can't find 2nd: {Vector3I.Round(vector)}", 16);
                    return false;
                }
            }

            if(def.IsCubePressurized[Vector3I.Round(value)][Vector3I.Round(vector)])
                return true;

            if(def is MyDoorDefinition)
            {
                // assuming door closed

                for(int i = 0; i < def.MountPoints.Length; i++)
                {
                    if(vector == def.MountPoints[i].Normal)
                        return false;
                }

                return true;
            }
            else if(def is MyAdvancedDoorDefinition)
            {
                // assuming door closed

                for(int j = 0; j < def.MountPoints.Length; j++)
                {
                    if(vector == def.MountPoints[j].Normal)
                        return false;
                }

                return true;
            }
            else if(def is MyAirtightSlideDoorDefinition)
            {
                // assuming door closed

                if(vector == Vector3.Forward)
                    return true;
            }
            else if(def is MyAirtightDoorGenericDefinition)
            {
                // assuming door closed

                if(vector == Vector3.Forward || vector == Vector3.Backward)
                    return true;
            }

            return false;
        }
#endif
    }
}
