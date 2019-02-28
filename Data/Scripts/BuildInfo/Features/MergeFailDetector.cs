using System;
using System.Text;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MergeBlock), useEntityUpdate: false)]
    public class MergeBlock : MyGameLogicComponent
    {
        private const string EMISSIVE_NAME = "Emissive";
        private readonly Color COLOR_BLINK_ON = Color.Yellow;
        private readonly Color COLOR_BLINK_OFF = Color.Red;

        private IMyShipMergeBlock block;
        private MyMergeBlockDefinition def;
        private Base6Directions.Direction forward;
        private Base6Directions.Direction right;
        private bool loadedDummies = false;
        private bool mergeFailing = false;
        private bool blinkSwitch = false;
        private ushort frameCounter = 0;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            block = (IMyShipMergeBlock)Entity;
            def = (MyMergeBlockDefinition)block.SlimBlock.BlockDefinition;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if(!BuildInfoMod.Instance.Started
             || BuildInfoMod.Instance.IsDS
             || block.CubeGrid?.Physics == null)
                return;

            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;

            block.AppendingCustomInfo += CustomInfo;
        }

        public override void Close()
        {
            block.AppendingCustomInfo -= CustomInfo;
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
                    block.SetEmissiveParts(EMISSIVE_NAME, (blinkSwitch ? COLOR_BLINK_ON : COLOR_BLINK_OFF), 1);
                    blinkSwitch = !blinkSwitch;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private void SetMergeFailing(bool failing)
        {
            if(mergeFailing == failing)
                return;

            mergeFailing = failing;
            block.RefreshCustomInfo();

            if(!failing)
            {
                blinkSwitch = false;
                ((MyCubeBlock)block).CheckEmissiveState(true);
            }
        }

        private bool CheckOther()
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

        private void CustomInfo(IMyTerminalBlock b, StringBuilder str)
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
                    str.Append("WARNING: Obstruction detected.\n");
                    str.Append("Blocks bounding box would intersect\n");
                    str.Append(" after merge.\n");
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        // HACK copied from MyShipMergeBlock
        #region Converted vanilla code
        private void LoadDummies()
        {
            var dummies = BuildInfoMod.Caches.Dummies;
            dummies.Clear();

            foreach(var kv in dummies)
            {
                if(kv.Key.ToLower().Contains("merge"))
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

        private void Update10()
        {
            var data = new MergeData();
            CalculateMergeData(ref data);

            if(data.PositionOk && data.AxisOk && data.RotationOk)
            {
                if(++frameCounter >= 3)
                {
                    frameCounter = 0;

                    var other = block.Other;
                    Vector3I gridOffset = CalculateOtherGridOffset(block);
                    Vector3I gridOffset2 = CalculateOtherGridOffset(other);

                    if(!block.CubeGrid.CanMergeCubes(other.CubeGrid, gridOffset))
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

        private void CalculateMergeData(ref MergeData data)
        {
            var other = block.Other;

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

            Vector3 vector = other.PositionComp.GetPosition() - block.PositionComp.GetPosition();
            Vector3 value = block.WorldMatrix.GetDirectionVector(forward);

            var otherRight = other.WorldMatrix.GetClosestDirection(block.WorldMatrix.GetDirectionVector(right));

            data.Distance = vector.Length();
            data.PositionOk = (data.Distance < block.CubeGrid.GridSize + 0.17f);
            data.AxisDelta = (float)(value + other.WorldMatrix.GetDirectionVector(forward)).Length();
            data.AxisOk = (data.AxisDelta < 0.1f);
            data.RotationDelta = (float)(block.WorldMatrix.GetDirectionVector(right) - other.WorldMatrix.GetDirectionVector(otherRight)).Length();
            data.RotationOk = (data.RotationDelta < 0.08f);
        }

        private static Vector3I CalculateOtherGridOffset(IMyShipMergeBlock b)
        {
            var blockLogic = b.GameLogic.GetAs<MergeBlock>();
            var other = b.Other;
            var otherLogic = other.GameLogic.GetAs<MergeBlock>();

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

        private static Vector3 ConstraintPositionInGridSpace(IMyShipMergeBlock b, Base6Directions.Direction forward)
        {
            return b.Position * b.CubeGrid.GridSize + b.PositionComp.LocalMatrix.GetDirectionVector(forward) * (b.CubeGrid.GridSize * 0.5f);
        }

        private struct MergeData
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
        #endregion
    }
}