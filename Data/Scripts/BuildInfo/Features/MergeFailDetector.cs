using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.BuildInfo.VanillaData;
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
        IMyShipMergeBlock Block;
        MyMergeBlockDefinition Def;
        Base6Directions.Direction Forward;
        Base6Directions.Direction Right;
        bool LoadedDummies = false;
        bool IsFailing = false;
        bool BlinkSwitch = false;
        int UpdateCounter = 0;

        IMySlimBlock MarkThis;
        IMySlimBlock MarkOther;

        const float MaxDistanceComputeSq = 200 * 200; // meters squared
        const float MaxDistanceBoxRenderSq = 100 * 100; // meters squared

        const string EmissiveName = "Emissive";
        static readonly Color EmissiveColorBlinkA = Color.Yellow;
        static readonly Color EmissiveColorBlinkB = Color.Red;
        static readonly MyStringId LineMaterial = MyStringId.GetOrCompute("BuildInfo_Laser");

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            if(BuildInfo_GameSession.GetOrComputeIsKilled(this.GetType().Name))
                return;

            Block = (IMyShipMergeBlock)Entity;
            Def = (MyMergeBlockDefinition)Block.SlimBlock.BlockDefinition;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if(MyAPIGateway.Utilities.IsDedicated || Block?.CubeGrid?.Physics == null)
                return;

            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;

            Block.AppendingCustomInfo += CustomInfo;
            Block.BeforeMerge += BeforeMerge;
        }

        public override void MarkForClose()
        {
            if(Block != null)
            {
                Block.AppendingCustomInfo -= CustomInfo;
                Block.BeforeMerge -= BeforeMerge;
            }
        }

        public override void UpdateAfterSimulation10()
        {
            try
            {
                if(!LoadedDummies && Block.SlimBlock.ComponentStack.IsBuilt) // need main model to load dummies
                {
                    LoadedDummies = true;
                    LoadDummies();
                }

                if(Vector3D.DistanceSquared(Block.GetPosition(), MyAPIGateway.Session.Camera.Position) > MaxDistanceComputeSq)
                    return;

                if(Block.IsWorking
                && Block.Other != null // magnetized or connected
                && Block.Other.CubeGrid != Block.CubeGrid // not already merged
                && Block.Other.IsWorking)
                {
                    Update10();
                }
                else
                {
                    if(IsFailing)
                        SetMergeFailing(false);
                }

                if(IsFailing)
                {
                    Block.SetEmissiveParts(EmissiveName, (BlinkSwitch ? EmissiveColorBlinkA : EmissiveColorBlinkB), 1);
                    BlinkSwitch = !BlinkSwitch;
                }
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

        void SetMergeFailing(bool fail)
        {
            if(IsFailing == fail)
                return;

            IsFailing = fail;

            if(fail)
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            else
                NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;

            if(!fail)
            {
                MarkThis = null;
                MarkOther = null;
                BlinkSwitch = false;
                ((MyCubeBlock)Block).CheckEmissiveState(true);
            }
        }

        void CustomInfo(IMyTerminalBlock b, StringBuilder str)
        {
            try
            {
                if(Block.Other == null)
                {
                    if(Block.IsWorking)
                        str.Append("Status: Idle.\n");

                    return;
                }

                if(Block.Other.CubeGrid == Block.CubeGrid)
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
                MySimpleObjectRasterizer.Wireframe, 1, lineWidth, null, LineMaterial, intensity: 10, blendType: MyBillboard.BlendTypeEnum.AdditiveTop);
        }

        // HACK: copied from MyShipMergeBlock + modified
        #region Converted vanilla code
        void LoadDummies()
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();

            Block.Model.GetDummies(dummies);

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

        void Update10()
        {
            MergeData data = new MergeData();
            CalculateMergeData(ref data);

            if(data.PositionOk && data.AxisOk && data.RotationOk)
            {
                if(++UpdateCounter >= 3)
                {
                    UpdateCounter = 0;

                    IMyShipMergeBlock other = Block.Other;
                    Vector3I gridOffset = CalculateOtherGridOffset(Block);
                    Vector3I gridOffset2 = CalculateOtherGridOffset(other);

                    // block.CubeGrid.CanMergeCubes(other.CubeGrid, gridOffset)
                    bool canMerge = CanMergeCubes((MyCubeGrid)Block.CubeGrid, (MyCubeGrid)other.CubeGrid, gridOffset, out MarkThis, out MarkOther);

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

        void CalculateMergeData(ref MergeData data)
        {
            IMyShipMergeBlock other = Block.Other;

            float num = (Def != null) ? Def.Strength : 0.1f;

            data.Distance = (float)(Block.WorldMatrix.Translation - other.WorldMatrix.Translation).Length() - Block.CubeGrid.GridSize;
            data.StrengthFactor = (float)Math.Exp((double)(-(double)data.Distance / Block.CubeGrid.GridSize));

            float num2 = MathHelper.Lerp(0f, num * ((Block.CubeGrid.GridSizeEnum == MyCubeSize.Large) ? 0.005f : 0.1f), data.StrengthFactor);

            Vector3 velocityAtPoint = Block.CubeGrid.Physics.GetVelocityAtPoint(Block.PositionComp.GetPosition());
            Vector3 velocityAtPoint2 = other.CubeGrid.Physics.GetVelocityAtPoint(other.PositionComp.GetPosition());

            data.RelativeVelocity = velocityAtPoint2 - velocityAtPoint;

            float num3 = data.RelativeVelocity.Length();
            float num4 = Math.Max(3.6f / ((num3 > 0.1f) ? num3 : 0.1f), 1f);

            data.ConstraintStrength = num2 / num4;

            Vector3D vector = other.PositionComp.GetPosition() - Block.PositionComp.GetPosition();
            Vector3D value = Block.WorldMatrix.GetDirectionVector(Forward);

            Base6Directions.Direction otherRight = other.WorldMatrix.GetClosestDirection(Block.WorldMatrix.GetDirectionVector(Right));

            data.Distance = (float)vector.Length();
            data.PositionOk = (data.Distance < Block.CubeGrid.GridSize + 0.17f);
            data.AxisDelta = (float)(value + other.WorldMatrix.GetDirectionVector(Forward)).Length();
            data.AxisOk = (data.AxisDelta < 0.1f);
            data.RotationDelta = (float)(Block.WorldMatrix.GetDirectionVector(Right) - other.WorldMatrix.GetDirectionVector(otherRight)).Length();
            data.RotationOk = (data.RotationDelta < 0.08f);
        }

        static Vector3I CalculateOtherGridOffset(IMyShipMergeBlock b)
        {
            MergeBlock blockLogic = b.GameLogic.GetAs<MergeBlock>();
            IMyShipMergeBlock other = b.Other;
            MergeBlock otherLogic = other.GameLogic.GetAs<MergeBlock>();

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