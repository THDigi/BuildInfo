using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Digi.BuildInfo.Features
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MergeBlock), useEntityUpdate: false)]
    public class MergeBlock : MyGameLogicComponent
    {
        IMyShipMergeBlock block;
        MyMergeBlockDefinition def;
        Base6Directions.Direction forward;
        Base6Directions.Direction right;
        bool loadedDummies = false;
        bool mergeFailing = false;
        bool blinkSwitch = false;
        ushort frameCounter = 0;

        IMySlimBlock MarkThis;
        IMySlimBlock MarkOther;

        const float MaxDistanceBoxRenderSq = 100 * 100; // meters squared

        const string EmissiveName = "Emissive";
        static readonly Color EmissiveColorBlinkA = Color.Yellow;
        static readonly Color EmissiveColorBlinkB = Color.Red;
        static readonly MyStringId LineMaterial = MyStringId.GetOrCompute("BuildInfo_Laser");

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if(BuildInfo_GameSession.IsKilled)
                return;

            block = (IMyShipMergeBlock)Entity;
            def = (MyMergeBlockDefinition)block.SlimBlock.BlockDefinition;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if(MyAPIGateway.Utilities.IsDedicated || block?.CubeGrid?.Physics == null)
                return;

            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;

            block.AppendingCustomInfo += CustomInfo;

            block.BeforeMerge += BeforeMerge;
        }

        public override void MarkForClose()
        {
            if(block != null)
            {
                block.AppendingCustomInfo -= CustomInfo;
                block.BeforeMerge -= BeforeMerge;
            }
        }

        public override void UpdateAfterSimulation10()
        {
            try
            {
                if(!loadedDummies && block.IsFunctional && block.Model.AssetName == def.Model) // need main model to load dummies
                {
                    loadedDummies = true;
                    LoadDummies();
                }

                if(CheckOther())
                    Update10();
                else
                    SetMergeFailing(false);

                if(mergeFailing)
                {
                    block.SetEmissiveParts(EmissiveName, (blinkSwitch ? EmissiveColorBlinkA : EmissiveColorBlinkB), 1);
                    blinkSwitch = !blinkSwitch;
                }

                block.RefreshCustomInfo();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void BeforeMerge()
        {
            SetMergeFailing(false);
        }

        void SetMergeFailing(bool failing)
        {
            if(mergeFailing == failing)
                return;

            mergeFailing = failing;

            if(failing)
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            else
                NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;

            if(!failing)
            {
                blinkSwitch = false;
                ((MyCubeBlock)block).CheckEmissiveState(true);
            }
        }

        bool CheckOther()
        {
            if(!block.IsWorking)
                return false;

            if(block.Other == null)
                return false; // not magnetized or connected

            if(!block.Other.IsWorking)
                return false;

            if(block.Other.CubeGrid == block.CubeGrid)
                return false; // already merged

            return true;
        }

        void CustomInfo(IMyTerminalBlock b, StringBuilder str)
        {
            try
            {
                if(block.Other == null)
                {
                    if(block.IsWorking)
                        str.Append("Status: Idle.\n");

                    return;
                }

                if(block.Other.CubeGrid == block.CubeGrid)
                {
                    str.Append("Status: Merged.\n");
                    return;
                }

                str.Append("Status: Attempting to merge...\n");

                if(mergeFailing)
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

        public override void UpdateAfterSimulation()
        {
            if(MarkThis == null && MarkOther == null)
                return;

            IMySlimBlock anyBlock = (MarkThis ?? MarkOther);

            Vector3D center;
            anyBlock.ComputeWorldCenter(out center);
            if(Vector3D.DistanceSquared(center, MyAPIGateway.Session.Camera.Position) > MaxDistanceBoxRenderSq)
                return;

            if(anyBlock.CubeGrid.BigOwners != null && anyBlock.CubeGrid.BigOwners.Count > 0 && MyAPIGateway.Session.Player != null)
            {
                MyRelationsBetweenPlayers relation = MyIDModule.GetRelationPlayerPlayer(anyBlock.CubeGrid.BigOwners[0], MyAPIGateway.Session.Player.IdentityId);
                if(relation == MyRelationsBetweenPlayers.Enemies)
                    return;
            }

            if(MarkThis != null)
            {
                DrawBox(MarkThis);
            }

            if(MarkOther != null)
            {
                DrawBox(MarkOther);
            }
        }

        static void DrawBox(IMySlimBlock block)
        {
            Color color = Color.Red;
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
                MySimpleObjectRasterizer.Wireframe, 1, lineWidth, null, LineMaterial, blendType: MyBillboard.BlendTypeEnum.AdditiveTop);
        }

        // HACK: copied from MyShipMergeBlock + modified
        #region Converted vanilla code
        void LoadDummies()
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();

            block.Model.GetDummies(dummies);

            foreach(KeyValuePair<string, IMyModelDummy> kv in dummies)
            {
                if(kv.Key.ContainsIgnoreCase("merge"))
                {
                    Matrix matrix = kv.Value.Matrix;
                    Vector3 vector = matrix.Scale / 2f;
                    Vector3 vec = Vector3.DominantAxisProjection(matrix.Translation / vector);
                    vec.Normalize();
                    forward = Base6Directions.GetDirection(vec);
                    right = Base6Directions.GetPerpendicular(forward);
                    break;
                }
            }

            dummies.Clear();
        }

        void Update10()
        {
            MergeData data = new MergeData();
            CalculateMergeData(ref data);

            if(data.PositionOk && data.AxisOk && data.RotationOk)
            {
                if(++frameCounter >= 3)
                {
                    frameCounter = 0;

                    IMyShipMergeBlock other = block.Other;
                    Vector3I gridOffset = CalculateOtherGridOffset(block);
                    Vector3I gridOffset2 = CalculateOtherGridOffset(other);

                    //if(!block.CubeGrid.CanMergeCubes(other.CubeGrid, gridOffset))
                    if(!CanMergeCubes((MyCubeGrid)block.CubeGrid, (MyCubeGrid)other.CubeGrid, gridOffset, out MarkThis, out MarkOther))
                    {
                        SetMergeFailing(true);
                    }
                }
            }
            else
            {
                frameCounter = 0;
                SetMergeFailing(false);
            }
        }

        void CalculateMergeData(ref MergeData data)
        {
            IMyShipMergeBlock other = block.Other;

            float num = (def != null) ? def.Strength : 0.1f;

            data.Distance = (float)(block.WorldMatrix.Translation - other.WorldMatrix.Translation).Length() - block.CubeGrid.GridSize;
            data.StrengthFactor = (float)Math.Exp((double)(-(double)data.Distance / block.CubeGrid.GridSize));

            float num2 = MathHelper.Lerp(0f, num * ((block.CubeGrid.GridSizeEnum == MyCubeSize.Large) ? 0.005f : 0.1f), data.StrengthFactor);

            Vector3 velocityAtPoint = block.CubeGrid.Physics.GetVelocityAtPoint(block.PositionComp.GetPosition());
            Vector3 velocityAtPoint2 = other.CubeGrid.Physics.GetVelocityAtPoint(other.PositionComp.GetPosition());

            data.RelativeVelocity = velocityAtPoint2 - velocityAtPoint;

            float num3 = data.RelativeVelocity.Length();
            float num4 = Math.Max(3.6f / ((num3 > 0.1f) ? num3 : 0.1f), 1f);

            data.ConstraintStrength = num2 / num4;

            Vector3D vector = other.PositionComp.GetPosition() - block.PositionComp.GetPosition();
            Vector3D value = block.WorldMatrix.GetDirectionVector(forward);

            Base6Directions.Direction otherRight = other.WorldMatrix.GetClosestDirection(block.WorldMatrix.GetDirectionVector(right));

            data.Distance = (float)vector.Length();
            data.PositionOk = (data.Distance < block.CubeGrid.GridSize + 0.17f);
            data.AxisDelta = (float)(value + other.WorldMatrix.GetDirectionVector(forward)).Length();
            data.AxisOk = (data.AxisDelta < 0.1f);
            data.RotationDelta = (float)(block.WorldMatrix.GetDirectionVector(right) - other.WorldMatrix.GetDirectionVector(otherRight)).Length();
            data.RotationOk = (data.RotationDelta < 0.08f);
        }

        static Vector3I CalculateOtherGridOffset(IMyShipMergeBlock b)
        {
            MergeBlock blockLogic = b.GameLogic.GetAs<MergeBlock>();
            IMyShipMergeBlock other = b.Other;
            MergeBlock otherLogic = other.GameLogic.GetAs<MergeBlock>();

            Vector3 value = ConstraintPositionInGridSpace(b, blockLogic.forward) / b.CubeGrid.GridSize;
            Vector3 vector = -ConstraintPositionInGridSpace(other, blockLogic.forward) / other.CubeGrid.GridSize;

            Base6Directions.Direction direction = b.Orientation.TransformDirection(blockLogic.right);
            Base6Directions.Direction newB = b.Orientation.TransformDirection(blockLogic.forward);
            Base6Directions.Direction flippedDirection = Base6Directions.GetFlippedDirection(other.Orientation.TransformDirection(otherLogic.forward));
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
            collideThis = null;
            collideOther = null;

            MatrixI transform = thisGrid.CalculateMergeTransform(gridToMerge, gridOffset);

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

                    // removed compound block checks as SE does not support it

                    if(slimThis?.FatBlock != null && slimOther?.FatBlock != null)
                    {
                        IMyPistonTop pistonTop;
                        if(slimOther.FatBlock is IMyPistonTop)
                        {
                            pistonTop = (IMyPistonTop)slimOther.FatBlock;
                        }
                        else
                        {
                            if(!(slimThis.FatBlock is IMyPistonTop))
                                return false;

                            pistonTop = (IMyPistonTop)slimThis.FatBlock;
                        }

                        IMyPistonBase pistonBase;
                        if(slimOther.FatBlock is IMyPistonBase)
                        {
                            pistonBase = (IMyPistonBase)slimOther.FatBlock;
                        }
                        else
                        {
                            if(!(slimThis.FatBlock is IMyPistonBase))
                                return false;

                            pistonBase = (IMyPistonBase)slimThis.FatBlock;
                        }

                        if((pistonBase.Top != null && pistonBase.Top.EntityId == pistonTop.EntityId)
                        || (pistonTop.Base != null && pistonTop.Base.EntityId == pistonBase.EntityId))
                        {
                            continue;
                        }

                        return false;
                    }
                }
            }

            return true;
        }
        #endregion Converted vanilla code
    }
}