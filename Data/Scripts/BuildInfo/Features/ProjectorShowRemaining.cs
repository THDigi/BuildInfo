using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace Digi.BuildInfo.Features
{
    public class ProjectorShowRemaining : ModComponent
    {
        const int ShowOnBlockAmount = 20;
        const int ExpiresMinutes = 3;
        const int TickRangeForUpdateBalance = 60;

        IMyTerminalControl Control;

        /// <summary>
        /// Keep the toggle state only for the session, doesn't need to be remembered.
        /// </summary>
        Dictionary<IMyTerminalBlock, ProjectorInfo> ShownOn = new Dictionary<IMyTerminalBlock, ProjectorInfo>();
        List<IMyTerminalBlock> TempRemove = new List<IMyTerminalBlock>();

        public ProjectorShowRemaining(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            CreateControls();
            MyAPIGateway.TerminalControls.CustomControlGetter += TerminalCustomControlGetter;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            MyAPIGateway.TerminalControls.CustomControlGetter -= TerminalCustomControlGetter;
        }

        void CreateControls()
        {
            var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyProjector>("BuildInfo.ShowNotFinished");
            c.Title = MyStringId.GetOrCompute("Show Remaining Blocks");
            c.Tooltip = MyStringId.GetOrCompute($"Shows a box around the remaining blocks in 3D space so you can find where they are." +
                                                $"\nDoes not show anything if there's more than {ShowOnBlockAmount} remaining blocks." +
                                                $"\nThis toggle is only for you and only for this session. It also turns itself off after {ExpiresMinutes} minutes." +
                                                $"\n(Added by {BuildInfoMod.ModName} mod)");
            c.OnText = MyStringId.GetOrCompute("Show");
            c.OffText = MyStringId.GetOrCompute("Hide");
            c.SupportsMultipleBlocks = false;
            c.Enabled = (b) =>
            {
                var p = b as IMyProjector;
                if(p == null)
                    return false;

                return p.IsProjecting;
            };
            c.Getter = (b) => ShownOn.ContainsKey(b);
            c.Setter = (b, v) =>
            {
                var p = b as IMyProjector;
                if(p == null)
                    return;

                Toggle(b, v);
            };

            // no adding it to terminal system because CustomControlGetter is used to add it dynamically
            // less prone to issues for something that doesn't need to be seen by other mods.
            Control = c;
        }

        void TerminalCustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            try
            {
                var projector = block as IMyProjector;
                if(projector != null)
                {
                    int insertAtIndex = -1;
                    for(int i = 0; i < controls.Count; i++)
                    {
                        IMyTerminalControl c = controls[i];
                        if(c.Id == "ShowOnHUD")
                        {
                            insertAtIndex = i + 1;
                            break;
                        }
                    }

                    if(insertAtIndex > 0)
                        controls.Insert(insertAtIndex, Control);
                    else
                        controls.Add(Control);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void Toggle(IMyTerminalBlock block, bool on)
        {
            if(on)
            {
                ShownOn.Add(block, new ProjectorInfo((IMyProjector)block));
            }
            else
            {
                ProjectorInfo pi;
                if(ShownOn.TryGetValue(block, out pi))
                {
                    pi.Dispose();
                    ShownOn.Remove(block);
                }
            }

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, ShownOn.Count > 0);
        }

        public override void UpdateDraw()
        {
            int currentOffset = MyAPIGateway.Session.GameplayFrameCounter % TickRangeForUpdateBalance;

            foreach(var kv in ShownOn)
            {
                IMyTerminalBlock block = kv.Key;

                if(block.MarkedForClose)
                {
                    TempRemove.Add(block);
                    continue;
                }

                ProjectorInfo pi = kv.Value;

                if(pi.TickOffset == currentOffset)
                {
                    bool valid = pi.SlowUpdate();
                    if(!valid)
                    {
                        TempRemove.Add(block);
                        continue;
                    }
                }

                if(pi.Marked.Count > 0)
                {
                    pi.Draw();
                }
            }

            if(TempRemove.Count > 0)
            {
                foreach(var key in TempRemove)
                {
                    Toggle(key, false);
                }

                TempRemove.Clear();
            }
        }

        class ProjectorInfo
        {
            public readonly IMyProjector Projector;
            //public readonly int ExpiresAt;
            public int TickOffset;
            public List<IMySlimBlock> Marked = new List<IMySlimBlock>();

            static int TickOffsetTracker;
            static Color BoxColor = Color.Yellow;
            static readonly MyStringId LineMaterial = Constants.Mat_Laser;

            public ProjectorInfo(IMyProjector block)
            {
                Projector = block;
                //ExpiresAt = MyAPIGateway.Session.GameplayFrameCounter + Constants.TicksPerSecond * ExpiresMinutes * 60;

                TickOffsetTracker = (TickOffsetTracker + 1) % TickRangeForUpdateBalance;
                TickOffset = TickOffsetTracker;

                SlowUpdate();
            }

            public void Dispose()
            {
                Marked.Clear();
            }

            public void Draw()
            {
                foreach(IMySlimBlock block in Marked)
                {
                    if(block.IsFullyDismounted)
                        continue;

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

                    MySimpleObjectDraw.DrawTransparentBox(ref blockMatrix, ref boundaries, ref BoxColor,
                        MySimpleObjectRasterizer.Wireframe, 1, lineWidth, null, LineMaterial, intensity: 10, blendType: MyBillboard.BlendTypeEnum.AdditiveTop);
                }
            }

            /// <summary>
            /// Returns false if it should be removed
            /// </summary>
            public bool SlowUpdate()
            {
                if(Projector.MarkedForClose)
                    return false;

                //if(MyAPIGateway.Session.GameplayFrameCounter >= ExpiresAt)
                //    return false;

                BoundingSphereD gridVolume = Projector.CubeGrid.WorldVolume;
                double maxDist = gridVolume.Radius + 200;
                if(Vector3D.DistanceSquared(gridVolume.Center, MyAPIGateway.Session.Camera.Position) > (maxDist * maxDist))
                    return false;

                Marked.Clear();

                if(!Projector.IsProjecting)
                    return true;

                int remainingBlocks = Projector.RemainingBlocks;
                if(remainingBlocks > ShowOnBlockAmount)
                    return true;

                var projectedGrid = (MyCubeGrid)Projector.ProjectedGrid;
                var realGrid = (MyCubeGrid)Projector.CubeGrid;

                foreach(IMySlimBlock projectedSlim in projectedGrid.GetBlocks()) // forced cast
                {
                    Vector3D world = projectedGrid.GridIntegerToWorld(projectedSlim.Position);
                    Vector3I realPos = realGrid.WorldToGridInteger(world);
                    IMySlimBlock realSlim = realGrid.GetCubeBlock(realPos);

                    if(realSlim == null || realSlim.BlockDefinition.Id != projectedSlim.BlockDefinition.Id)
                    {
                        Marked.Add(projectedSlim);
                    }
                }

                return true;
            }
        }
    }
}
