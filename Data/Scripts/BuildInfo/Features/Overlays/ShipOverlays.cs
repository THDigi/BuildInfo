using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features.Overlays
{
    // TODO: show CoM lines up to edges of the ship
    // TODO: show mass per grid?
    public class ShipOverlays : ModComponent
    {
        static readonly Color ColorLabel = new Color(0, 255, 0);
        static readonly Vector4 ColorPoint = ColorLabel.ToVector4();

        static readonly Color ColorMassLabel = new Color(255, 155, 0);
        static readonly Vector4 ColorCoMPoint = ColorMassLabel.ToVector4();

        static readonly double ShowLabelAngleRad = MathHelper.ToRadians(10);

        const BlendTypeEnum BlendType = BlendTypeEnum.PostPP;

        const double ScanRadius = 30;
        const double VolumeSeeExtraRadius = 20;

        LabelRendering LabelRender;

        readonly List<MyEntity> Entities = new List<MyEntity>();
        readonly HashSet<IMyGridGroupData> Ships = new HashSet<IMyGridGroupData>();
        readonly List<IMyCubeGrid> Grids = new List<IMyCubeGrid>();
        readonly List<IHitInfo> Hits = new List<IHitInfo>();
        readonly HashSet<long> Owners = new HashSet<long>();

        public ShipOverlays(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            LabelRender = new LabelRendering(Main);

            MyAPIGateway.GridGroups.OnGridGroupDestroyed += GridGroupDestroyed;

            Main.GUIMonitor.ScreenRemoved += ScreenRemoved;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, MyCubeGrid.ShowCenterOfMass);
        }

        public override void UnregisterComponent()
        {
            // throws exceptions in the event removal, I guess it's not required...
            //MyAPIGateway.GridGroups.OnGridGroupDestroyed -= GridGroupDestroyed;

            if(!Main.ComponentsRegistered)
                return;

            Main.GUIMonitor.ScreenRemoved -= ScreenRemoved;
        }

        void GridGroupDestroyed(IMyGridGroupData ship)
        {
            Ships.Remove(ship);
        }

        void ScreenRemoved(string name)
        {
            if(name.EndsWith("GuiScreenTerminal"))
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, MyCubeGrid.ShowCenterOfMass);
            }
        }

        public override void UpdateDraw()
        {
            if(MyAPIGateway.Session?.Player == null)
                return;

            if(!MyCubeGrid.ShowCenterOfMass)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
                return;
            }

            if(Main.Tick % 6 == 0) // update 10 times a second
            {
                BoundingSphereD sphere = new BoundingSphereD(MyAPIGateway.Session.Camera.Position, ScanRadius);

                Ships.Clear();
                Entities.Clear();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, Entities, MyEntityQueryType.Both);

                foreach(MyEntity ent in Entities)
                {
                    IMyCubeGrid grid = ent as IMyCubeGrid;
                    if(grid?.Physics == null)
                        continue;

                    double distSq = Vector3D.DistanceSquared(sphere.Center, grid.Physics.CenterOfMassWorld);
                    double radius = grid.WorldVolume.Radius + VolumeSeeExtraRadius;
                    if(distSq > radius * radius)
                        continue;

                    IMyGridGroupData group = grid.GetGridGroup(GridLinkTypeEnum.Physical);
                    if(group == null)
                        throw new System.Exception($"{grid} ({grid.EntityId.ToString()}) returns null physical grid-group for some reason.");

                    Ships.Add(group);
                }
            }

            if(Ships.Count > 0)
            {
                long localIdentityId = MyAPIGateway.Session.Player.IdentityId;

                bool drawLabel = LabelRender.CanDrawLabel();

                double closestAngleRad = ShowLabelAngleRad;
                Vector3D? closestCoM = null;

                Vector3D camPos = MyAPIGateway.Session.Camera.Position;
                Vector3D camForward = MyAPIGateway.Session.Camera.WorldMatrix.Forward;

                foreach(IMyGridGroupData ship in Ships)
                {
                    Grids.Clear();
                    ship.GetGrids(Grids);

                    // TODO: optimize by checking more rarely
                    // gather all unique owners to reduce the calls on GetRelationPlayerPlayer()
                    Owners.Clear();
                    foreach(IMyCubeGrid grid in Grids)
                    {
                        foreach(long owner in grid.BigOwners)
                        {
                            Owners.Add(owner);
                        }
                    }

                    bool shipFriendly = false;
                    if(Owners.Count == 0)
                    {
                        shipFriendly = true;
                    }
                    else
                    {
                        foreach(long owner in Owners)
                        {
                            MyRelationsBetweenPlayers relation = MyIDModule.GetRelationPlayerPlayer(owner, localIdentityId);
                            if(relation == MyRelationsBetweenPlayers.Allies || relation == MyRelationsBetweenPlayers.Self)
                            {
                                shipFriendly = true;
                                break;
                            }
                        }
                    }

                    if(!shipFriendly)
                        continue;

                    // has known weird issues so I'd rather just compute it myself when I can
                    //Vector3D shipCoM = MyGridPhysicalGroupData.GetGroupSharedProperties((MyCubeGrid)Grids[0]).CoMWorld;

                    Vector3D shipCoM = new Vector3D(0);
                    double totalMass = 0;
                    float biggestGridVolume = 0;
                    IMyCubeGrid biggestGrid = null;

                    foreach(IMyCubeGrid grid in Grids)
                    {
                        float mass = grid.Physics.Mass;
                        if(mass <= 0)
                        {
                            // need mass for clients in MP too, like when grids are LG'd
                            mass = BuildInfoMod.Instance.GridMassCompute.GetGridMass(grid);
                        }

                        Vector3D gridCoM = grid.Physics.CenterOfMassWorld;

                        // weight each vector by mass to play a factor in the final vector
                        totalMass += mass;
                        shipCoM += gridCoM * mass;

                        float volume = grid.LocalAABB.Volume();
                        if(volume > biggestGridVolume)
                        {
                            biggestGrid = grid;
                            biggestGridVolume = volume;
                        }

                        // TODO: optimize by checking more rarely
                        bool gridFriendly = false;
                        if(grid.BigOwners == null || grid.BigOwners.Count == 0)
                        {
                            gridFriendly = true;
                        }
                        else
                        {
                            foreach(long owner in grid.BigOwners)
                            {
                                MyRelationsBetweenPlayers relation = MyIDModule.GetRelationPlayerPlayer(grid.BigOwners[0], localIdentityId);
                                if(relation == MyRelationsBetweenPlayers.Allies || relation == MyRelationsBetweenPlayers.Self)
                                {
                                    gridFriendly = true;
                                    break;
                                }
                            }
                        }

                        if(!gridFriendly)
                            continue;

                        {
                            MatrixD matrix = biggestGrid.WorldMatrix;
                            matrix.Translation = gridCoM;
                            float rescale = OverlayDrawInstance.ConvertToAlwaysOnTop(ref matrix);

                            MyTransparentGeometry.AddPointBillboard(OverlayDrawInstance.MaterialDot, ColorCoMPoint, matrix.Translation, 0.05f * rescale, 0, blendType: BlendType);
                        }

                        if(drawLabel && Utils.VectorAngleBetween((gridCoM - camPos), camForward) <= ShowLabelAngleRad)
                        {
                            int shapes = ((MyCubeGrid)grid).ShapeCount;
                            const int maxShapes = (ushort.MaxValue + 1);

                            Color colorShapes = ColorMassLabel;

                            if(shapes > (maxShapes * 0.95f))
                                colorShapes = Color.Red;
                            else if(shapes > (maxShapes * 0.8f))
                                colorShapes = Color.Yellow;

                            Vector3D labelDir = MyAPIGateway.Session.Camera.WorldMatrix.Right;

                            LabelRender.DynamicLabel.Clear().Color(ColorMassLabel).Append(grid.CustomName).Append(grid.IsStatic ? " <color=gray>(Static)" : "")
                                .Color(ColorMassLabel).Append("\nMass: ").MassFormat(mass)
                                .Color(colorShapes).Append("\nPhysical shapes: ").RoundedNumber(shapes, 0).Append("<color=gray> / ").Append(maxShapes);

                            LabelRender.DrawLineLabel(LabelType.DynamicLabel, gridCoM, labelDir, ColorMassLabel, alwaysOnTop: true);
                        }
                    }

                    if(Grids.Count <= 1)
                        continue; // don't show ship CoM as there's only one grid

                    shipCoM /= totalMass; // important to get an actual world-space vector

                    BoundingSphereD point = new BoundingSphereD(shipCoM, 1f);
                    if(!MyAPIGateway.Session.Camera.IsInFrustum(ref point))
                        continue;

                    Vector3D camToCoM = (shipCoM - camPos);

                    {
                        float distanceToCoM = (float)camToCoM.Length();
                        float distScale = MathHelper.Lerp(1f, 9f, distanceToCoM / 200f);

                        Vector4 lineColor = new Color(255, 0, 255);

                        MatrixD matrix = biggestGrid.WorldMatrix;
                        matrix.Translation = shipCoM;

                        float rescale = OverlayDrawInstance.ConvertToAlwaysOnTop(ref matrix);

                        float halfLength = 1f * distScale; // no rescale here as matrix directions are already scaled
                        float thick = 0.015f * distScale * rescale;

                        MyTransparentGeometry.AddLineBillboard(OverlayDrawInstance.MaterialLaser, lineColor, matrix.Translation - matrix.Forward * halfLength, matrix.Forward, halfLength * 2, thick, BlendType);
                        MyTransparentGeometry.AddLineBillboard(OverlayDrawInstance.MaterialLaser, lineColor, matrix.Translation - matrix.Right * halfLength, matrix.Right, halfLength * 2, thick, BlendType);
                        MyTransparentGeometry.AddLineBillboard(OverlayDrawInstance.MaterialLaser, lineColor, matrix.Translation - matrix.Up * halfLength, matrix.Up, halfLength * 2, thick, BlendType);

                        MyTransparentGeometry.AddPointBillboard(OverlayDrawInstance.MaterialDot, ColorPoint, matrix.Translation, 0.05f * distScale * rescale, 0, blendType: BlendType);
                    }

                    double angleRad = Utils.VectorAngleBetween(camToCoM, camForward);
                    if(angleRad < closestAngleRad)
                    {
                        closestCoM = shipCoM;
                        closestAngleRad = angleRad;
                    }
                }

                Grids.Clear();

                if(drawLabel && closestCoM.HasValue)
                {
                    Vector3D labelDir = MyAPIGateway.Session.Camera.WorldMatrix.Down;
                    LabelRender.DrawLineLabel(LabelType.ShipCenterOfMass, closestCoM.Value, labelDir, ColorLabel, "Ship's Center of Mass", alwaysOnTop: true);
                }
            }
        }
    }
}
