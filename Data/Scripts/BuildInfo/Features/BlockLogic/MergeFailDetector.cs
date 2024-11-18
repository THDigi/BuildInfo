using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using VRageRender;

namespace Digi.BuildInfo.Features.BlockLogic
{
    // registered in BlockAttachedLogic
    class MergeFailDetector : BlockAttachedLogic.LogicBase
    {
        IMyShipMergeBlock MergeBlock;
        MyMergeBlockDefinition Def;

        Base6Directions.Direction Forward;
        Base6Directions.Direction Right;
        bool LoadedDummies = false;
        bool IsFailing = false;
        int BlinkState = 0;
        int UpdateCounter = 0;

        IMySlimBlock MarkThis;
        IMySlimBlock MarkOther;

        const float MaxDistanceComputeSq = 200 * 200; // meters squared
        const float MaxDistanceBoxRenderSq = 100 * 100; // meters squared

        const string EmissiveName = "Emissive";
        static readonly Color EmissiveColorBlinkA = Color.Yellow;
        static readonly Color EmissiveColorBlinkB = Color.Red;

        public override void Added()
        {
            MergeBlock = (IMyShipMergeBlock)Block;
            Def = (MyMergeBlockDefinition)MergeBlock.SlimBlock.BlockDefinition;
            MergeBlock.AppendingCustomInfo += CustomInfo;
            MergeBlock.BeforeMerge += BeforeMerge;

            SetUpdate(BlockAttachedLogic.BlockUpdate.Update10, true);
        }

        public override void Removed()
        {
            if(MergeBlock != null)
            {
                MergeBlock.AppendingCustomInfo -= CustomInfo;
                MergeBlock.BeforeMerge -= BeforeMerge;
            }
        }

        public override void Update10()
        {
            if(!LoadedDummies && MergeBlock.SlimBlock.ComponentStack.IsBuilt) // need main model to load dummies
            {
                LoadedDummies = true;
                LoadDummies();
            }

            if(Vector3D.DistanceSquared(MergeBlock.GetPosition(), MyAPIGateway.Session.Camera.Position) > MaxDistanceComputeSq)
                return;

            if(MergeBlock.IsWorking
            && MergeBlock.Other != null // magnetized or connected
            && MergeBlock.Other.CubeGrid != MergeBlock.CubeGrid // not already merged
            && MergeBlock.Other.IsWorking)
            {
                // similar to MyShipMergeBlock's update10
                MergeData data = new MergeData();
                CalculateMergeData(ref data);

                if(data.PositionOk && data.AxisOk && data.RotationOk)
                {
                    if(++UpdateCounter >= 3)
                    {
                        UpdateCounter = 0;

                        IMyShipMergeBlock other = MergeBlock.Other;
                        Vector3I gridOffset = CalculateOtherGridOffset(MergeBlock);
                        Vector3I gridOffset2 = CalculateOtherGridOffset(other);

                        bool canMerge = false;

                        // only update one of them
                        if(MergeBlock.EntityId > other.EntityId)
                        {
                            // block.CubeGrid.CanMergeCubes(other.CubeGrid, gridOffset)
                            canMerge = CanMergeCubes((MyCubeGrid)MergeBlock.CubeGrid, (MyCubeGrid)other.CubeGrid, gridOffset, out MarkThis, out MarkOther);
                        }
                        else
                        {
                            MergeFailDetector otherLogic = Host.Tracked.GetValueOrDefault(other) as MergeFailDetector;
                            if(otherLogic != null)
                            {
                                // mark flipped
                                MarkThis = otherLogic.MarkOther;
                                MarkOther = otherLogic.MarkThis;
                                canMerge = !otherLogic.IsFailing;
                            }
                        }

                        if(!canMerge && !IsFailing)
                        {
                            SetMergeFailing(true);
                        }
                    }
                }
                else
                {
                    UpdateCounter = 0;

                    if(IsFailing)
                        SetMergeFailing(false);
                }
            }
            else
            {
                if(IsFailing)
                    SetMergeFailing(false);
            }

            if(MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel)
            {
                MergeBlock.RefreshCustomInfo();
                MergeBlock.SetDetailedInfoDirty();
            }

            if(IsFailing)
            {
                if(++BlinkState >= 6)
                    BlinkState = 0;

                if(BlinkState == 0 || BlinkState == 3)
                {
                    MergeBlock.SetEmissiveParts(EmissiveName, (BlinkState == 0 ? EmissiveColorBlinkA : EmissiveColorBlinkB), 1);
                }
            }
        }

        void BeforeMerge()
        {
            SetMergeFailing(false);
        }

        void SetMergeFailing(bool fail)
        {
            if(IsFailing == fail)
                return;

            IsFailing = fail;
            SetUpdate(BlockAttachedLogic.BlockUpdate.Update1, fail);

            if(!fail)
            {
                MarkThis = null;
                MarkOther = null;
                ((MyCubeBlock)MergeBlock).CheckEmissiveState(true);
            }
        }

        void CustomInfo(IMyTerminalBlock b, StringBuilder str)
        {
            try
            {
                IMyShipMergeBlock other = MergeBlock.Other;

                if(other == null)
                {
                    if(MergeBlock.IsWorking)
                        str.Append("Status: Idle.\n");

                    return;
                }

                if(other.CubeGrid == MergeBlock.CubeGrid)
                {
                    str.Append("Status: Merged.\n");
                    return;
                }

                str.Append("Status: Attempting to merge...\n");

                if(IsFailing)
                {
                    str.Append("FAILED: Obstruction detected between:\n");

                    if(MarkThis != null && MarkOther != null)
                    {
                        str.Append("  ");
                        AppendBlockname(str, MarkThis);

                        str.Append("\nand other grid's:\n  ");
                        AppendBlockname(str, MarkOther);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        static void AppendBlockname(StringBuilder sb, IMySlimBlock block)
        {
            IMyTerminalBlock terminalBlock = block.FatBlock as IMyTerminalBlock;
            if(terminalBlock != null)
            {
                sb.Append('"').Append(terminalBlock.CustomName).Append('"');
            }
            else
            {
                sb.Append(block.BlockDefinition.DisplayNameText);
            }
        }

        public override void Update1()
        {
            IMyCubeBlock other = MergeBlock.Other;
            IMySlimBlock anyBlock = (MarkThis ?? MarkOther);
            if(other == null || anyBlock == null)
                return;

            Vector3D anyCenter;
            anyBlock.ComputeWorldCenter(out anyCenter);
            if(Vector3D.DistanceSquared(anyCenter, MyAPIGateway.Session.Camera.Position) > MaxDistanceBoxRenderSq)
                return;

            if(anyBlock.CubeGrid.BigOwners != null && anyBlock.CubeGrid.BigOwners.Count > 0 && MyAPIGateway.Session.Player != null)
            {
                MyRelationsBetweenPlayers relation = MyIDModule.GetRelationPlayerPlayer(anyBlock.CubeGrid.BigOwners[0], MyAPIGateway.Session.Player.IdentityId);
                if(relation == MyRelationsBetweenPlayers.Enemies)
                    return;
            }

            Vector3D mergeCenter = MergeBlock.WorldAABB.Center;
            mergeCenter += (other.WorldAABB.Center - mergeCenter) * 0.5f; // between the merge blocks

            MyTransparentGeometry.AddLineBillboard(Constants.Mat_Laser, Color.Yellow.ToVector4() * 10f, anyCenter, (mergeCenter - anyCenter), 1f, 0.05f, MyBillboard.BlendTypeEnum.AdditiveTop);

            if(MarkThis != null)
            {
                DrawBox(MarkThis, Color.Red);
            }

            if(MarkOther != null)
            {
                DrawBox(MarkOther, Color.Orange);
            }
        }

        static void DrawBox(IMySlimBlock block, Color color)
        {
            bool isLarge = (block.CubeGrid.GridSizeEnum == MyCubeSize.Large);
            float lineWidth = (isLarge ? 0.02f : 0.016f);

            MyCubeBlockDefinition def = (MyCubeBlockDefinition)block.BlockDefinition;
            MyCubeGrid grid = (MyCubeGrid)block.CubeGrid;

            Vector3 halfSize = def.Size * grid.GridSizeHalf;
            BoundingBoxD boundaries = new BoundingBoxD(-halfSize, halfSize);

            Matrix localMatrix;
            block.Orientation.GetMatrix(out localMatrix);
            localMatrix.Translation = (block.Max + block.Min) * grid.GridSizeHalf; // local block float-center
            MatrixD blockMatrix = localMatrix * grid.WorldMatrix;

            MySimpleObjectDraw.DrawTransparentBox(ref blockMatrix, ref boundaries, ref color,
                MySimpleObjectRasterizer.Wireframe, 1, lineWidth, null, Constants.Mat_Laser, intensity: 10, blendType: MyBillboard.BlendTypeEnum.AdditiveTop);
        }

        // HACK: copied from MyShipMergeBlock + modified
        #region Converted vanilla code
        void LoadDummies()
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();

            MergeBlock.Model.GetDummies(dummies);

            foreach(KeyValuePair<string, IMyModelDummy> kv in dummies)
            {
                if(kv.Key.ContainsIgnoreCase(Hardcoded.Merge_DummyName))
                {
                    Matrix matrix = kv.Value.Matrix;
                    Vector3 vector = matrix.Scale / 2f;
                    Vector3 vec = Vector3.DominantAxisProjection(matrix.Translation / vector);
                    vec.Normalize();
                    Forward = Base6Directions.GetDirection(vec);
                    Right = Base6Directions.GetPerpendicular(Forward);
                    break;
                }
            }

            dummies.Clear();
        }

        void CalculateMergeData(ref MergeData data)
        {
            IMyShipMergeBlock other = MergeBlock.Other;

            float num = (Def != null) ? Def.Strength : 0.1f;

            data.Distance = (float)(MergeBlock.WorldMatrix.Translation - other.WorldMatrix.Translation).Length() - MergeBlock.CubeGrid.GridSize;
            data.StrengthFactor = (float)Math.Exp((double)(-(double)data.Distance / MergeBlock.CubeGrid.GridSize));

            float num2 = MathHelper.Lerp(0f, num * ((MergeBlock.CubeGrid.GridSizeEnum == MyCubeSize.Large) ? 0.005f : 0.1f), data.StrengthFactor);

            Vector3 velocityAtPoint = MergeBlock.CubeGrid.Physics.GetVelocityAtPoint(MergeBlock.PositionComp.GetPosition());
            Vector3 velocityAtPoint2 = other.CubeGrid.Physics.GetVelocityAtPoint(other.PositionComp.GetPosition());

            data.RelativeVelocity = velocityAtPoint2 - velocityAtPoint;

            float num3 = data.RelativeVelocity.Length();
            float num4 = Math.Max(3.6f / ((num3 > 0.1f) ? num3 : 0.1f), 1f);

            data.ConstraintStrength = num2 / num4;

            Vector3D vector = other.PositionComp.GetPosition() - MergeBlock.PositionComp.GetPosition();
            Vector3D value = MergeBlock.WorldMatrix.GetDirectionVector(Forward);

            Base6Directions.Direction otherRight = other.WorldMatrix.GetClosestDirection(MergeBlock.WorldMatrix.GetDirectionVector(Right));

            data.Distance = (float)vector.Length();
            data.PositionOk = (data.Distance < MergeBlock.CubeGrid.GridSize + 0.17f);
            data.AxisDelta = (float)(value + other.WorldMatrix.GetDirectionVector(Forward)).Length();
            data.AxisOk = (data.AxisDelta < 0.1f);
            data.RotationDelta = (float)(MergeBlock.WorldMatrix.GetDirectionVector(Right) - other.WorldMatrix.GetDirectionVector(otherRight)).Length();
            data.RotationOk = (data.RotationDelta < 0.08f);
        }

        static Vector3I CalculateOtherGridOffset(IMyShipMergeBlock b)
        {
            IMyShipMergeBlock other = b.Other;

            var lookup = BuildInfoMod.Instance.BlockAttachedLogic.Tracked;
            MergeFailDetector blockLogic = lookup.GetValueOrDefault(b) as MergeFailDetector;
            MergeFailDetector otherLogic = lookup.GetValueOrDefault(other) as MergeFailDetector;

            Vector3 value = ConstraintPositionInGridSpace(b, blockLogic.Forward) / b.CubeGrid.GridSize;
            Vector3 vector = -ConstraintPositionInGridSpace(other, blockLogic.Forward) / other.CubeGrid.GridSize;

            Base6Directions.Direction direction = b.Orientation.TransformDirection(blockLogic.Right);
            Base6Directions.Direction newB = b.Orientation.TransformDirection(blockLogic.Forward);
            Base6Directions.Direction flippedDirection = Base6Directions.GetFlippedDirection(other.Orientation.TransformDirection(otherLogic.Forward));
            Base6Directions.Direction closestDirection = other.CubeGrid.WorldMatrix.GetClosestDirection(b.CubeGrid.WorldMatrix.GetDirectionVector(direction));

            MatrixI matrixI = MatrixI.CreateRotation(closestDirection, flippedDirection, direction, newB);
            Vector3 value2;
            Vector3.Transform(ref vector, ref matrixI, out value2);

            return Vector3I.Round(value + value2);
        }

        static Vector3 ConstraintPositionInGridSpace(IMyShipMergeBlock b, Base6Directions.Direction forward)
        {
            return b.Position * b.CubeGrid.GridSize + b.LocalMatrix.GetDirectionVector(forward) * (b.CubeGrid.GridSize * 0.5f);
        }

        struct MergeData
        {
            public bool PositionOk;
            public bool RotationOk;
            public bool AxisOk;
            public float Distance;
            public float RotationDelta;
            public float AxisDelta;
            public float ConstraintStrength;
            public float StrengthFactor;
            public Vector3 RelativeVelocity;
        }

        static bool CanMergeCubes(MyCubeGrid thisGrid, MyCubeGrid gridToMerge, Vector3I gridOffset, out IMySlimBlock collideThis, out IMySlimBlock collideOther)
        {
            MatrixI transform = thisGrid.CalculateMergeTransform(gridToMerge, gridOffset);

            collideThis = null;
            collideOther = null;

            foreach(IMySlimBlock slimOther in gridToMerge.GetBlocks())
            {
                Vector3I start = slimOther.Min;
                Vector3I end = slimOther.Max;
                Vector3I_RangeIterator iterator = new Vector3I_RangeIterator(ref start, ref end);

                while(iterator.IsValid())
                {
                    Vector3I posMerge = iterator.Current;
                    iterator.MoveNext();

                    Vector3I posThis = Vector3I.Transform(posMerge, transform);
                    IMySlimBlock slimThis = thisGrid.GetCubeBlock(posThis);
                    if(slimThis == null)
                        continue;

                    collideThis = slimThis;
                    collideOther = slimOther;

                    // removed compound block checks here as SE does not support that.

                    if(slimThis?.FatBlock != null && slimOther?.FatBlock != null)
                    {
                        IMyPistonBase pistonBase = slimOther.FatBlock as IMyPistonBase ?? slimThis.FatBlock as IMyPistonBase;
                        IMyPistonTop pistonTop = slimOther.FatBlock as IMyPistonTop ?? slimThis.FatBlock as IMyPistonTop;

                        if(pistonBase == null || pistonTop == null)
                            return false;

                        if((pistonBase.Top != null && pistonBase.Top.EntityId == pistonTop.EntityId)
                        || (pistonTop.Base != null && pistonTop.Base.EntityId == pistonBase.EntityId))
                            continue;
                    }

                    return false;
                }
            }

            return true;
        }
        #endregion Converted vanilla code
    }
}