﻿using System;
using System.Collections.Generic;
using System.IO;
using Digi.BuildInfo.Features;
using Digi.BuildInfo.VanillaData;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Utilities
{
    /// <summary>
    /// Various random utility methods
    /// </summary>
    public static class Utils
    {
        public static bool AssertMainThread(bool throwException = true)
        {
            if(Environment.CurrentManagedThreadId != 1)
            {
                if(throwException)
                    throw new Exception($"Called in thread #{Environment.CurrentManagedThreadId.ToString()}");

                return false;
            }

            return true;
        }

        public static string GetModFullPath(string relativePath)
        {
            if(relativePath.StartsWith("\\") || relativePath.StartsWith("/"))
                relativePath = relativePath.Substring(1); // remove leading slashes

            if(!MyAPIGateway.Utilities.FileExistsInModLocation(relativePath, BuildInfoMod.Instance.Session.ModContext.ModItem))
            {
                Log.Error($"File not found in mod folder: {relativePath}");
            }

            return Path.Combine(BuildInfoMod.Instance.Session.ModContext.ModPath, relativePath);
        }

        // from MySafeZoneAction
        public static readonly object SZADamage = 0x1;
        public static readonly object SZAShooting = 0x2;
        public static readonly object SZADrilling = 0x4;
        public static readonly object SZAWelding = 0x8;
        public static readonly object SZAGrinding = 0x10;
        public static readonly object SZAVoxelHand = 0x20;
        public static readonly object SZABuilding = 0x40;
        public static readonly object SZALandingGearLock = 0x80;
        public static readonly object SZAConvertToStation = 0x100;
        public static readonly object SZABuildingProjections = 0x200;
        public static readonly object SZAAll = 0x3FF;
        public static readonly object SZAAdminIgnore = 0x37E;

        public static bool CheckSafezoneAction(IMySlimBlock block, object actionId, long sourceEntityId = 0)
        {
            ulong steamId = MyAPIGateway.Session?.Player?.SteamUserId ?? 0;
            MyCubeGrid grid = (MyCubeGrid)block.CubeGrid;
            BoundingBoxD box = new BoundingBoxD(block.Min * grid.GridSize - grid.GridSizeHalfVector, block.Max * grid.GridSize + grid.GridSizeHalfVector).TransformFast(grid.PositionComp.WorldMatrixRef);
            return MySessionComponentSafeZones.IsActionAllowed(box, CastHax(MySessionComponentSafeZones.AllowedActions, actionId), sourceEntityId, steamId);
        }

        public static bool CheckSafezoneAction(IMyEntity ent, object actionId, long sourceEntityId = 0)
        {
            ulong steamId = MyAPIGateway.Session?.Player?.SteamUserId ?? 0;
            return MySessionComponentSafeZones.IsActionAllowed((MyEntity)ent, CastHax(MySessionComponentSafeZones.AllowedActions, actionId), sourceEntityId, steamId);
        }

        public static T CastHax<T>(T typeRef, object castObj) => (T)castObj;

        /// <summary>
        /// Because <see cref="Vector3D.Reject(Vector3D, Vector3D)"/> is broken.
        /// </summary>
        public static Vector3D Rejection(Vector3D a, Vector3D b)
        {
            if(Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return Vector3D.Zero;

            return a - (b * (a.Dot(b) / b.LengthSquared()));
        }

        public static MatrixD GetBlockCenteredWorldMatrix(IMySlimBlock block)
        {
            Matrix m;
            Vector3D center;
            block.Orientation.GetMatrix(out m);
            block.ComputeWorldCenter(out center);

            MatrixD wm = m * block.CubeGrid.WorldMatrix;
            wm.Translation = center;
            return wm;
        }

        public static bool GetEquippedBlockMatrix(out MatrixD matrix)
        {
            const bool DebugMessages = false;

            matrix = MatrixD.Identity;

            if(MyCubeBuilder.Static == null || !MyCubeBuilder.Static.IsActivated)
                return false;

            MyOrientedBoundingBoxD box = MyCubeBuilder.Static.GetBuildBoundingBox();
            matrix = MatrixD.CreateFromQuaternion(box.Orientation);

            if(MyCubeBuilder.Static.DynamicMode)
            {
                IMyEntity hitEnt = MyCubeBuilder.Static.HitInfo?.GetHitEnt();
                if(hitEnt == null)
                {
                    // better accuracy that doesn't race with current frame update for cubebuilder making it jitter
                    // cloned from MyDefaultPlacementProvider.RayStart & RayDirection
                    MatrixD rayMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
                    if(MyAPIGateway.Session.ControlledObject != null)
                    {
                        MyCameraControllerEnum cameraControllerEnum = GetCameraControllerEnum();
                        if(cameraControllerEnum == MyCameraControllerEnum.Entity || cameraControllerEnum == MyCameraControllerEnum.ThirdPersonSpectator)
                            rayMatrix = MyAPIGateway.Session.ControlledObject.GetHeadMatrix(includeY: false);
                    }

                    matrix.Translation = rayMatrix.Translation + rayMatrix.Forward * MyCubeBuilder.IntersectionDistance;

                    if(DebugMessages)
                        if(!MyParticlesManager.Paused)
                            MyAPIGateway.Utilities.ShowNotification($"EquippedBlockMatrix: Dynamic; no hit ent", 16, FontsHandler.SEOutlined);
                }
                else if(hitEnt is IMyVoxelBase)
                {
                    matrix.Translation = MyCubeBuilder.Static.HitInfo.Value.GetHitPos(); // required for position to be accurate when aiming at a planet

                    if(DebugMessages)
                        if(!MyParticlesManager.Paused)
                            MyAPIGateway.Utilities.ShowNotification($"EquippedBlockMatrix: Dynamic; hit voxel: {hitEnt}", 16, FontsHandler.SEOutlined);
                }
                else // if(hitEnt is IMyCubeGrid)
                {
                    // TODO: fix jittery position
                    matrix.Translation = box.Center;

                    if(DebugMessages)
                        if(!MyParticlesManager.Paused)
                            MyAPIGateway.Utilities.ShowNotification($"EquippedBlockMatrix: Dynamic; hit grid(?): {hitEnt}", 16, FontsHandler.SEOutlined);
                }
            }
            else
            {
                //drawMatrix.Translation = box.Center;

                // fix for jittery overlays when aiming at a grid.
                Vector3D addPosition;
                MyCubeBuilder.Static.GetAddPosition(out addPosition);
                matrix.Translation = addPosition;

                if(DebugMessages)
                    if(!MyParticlesManager.Paused)
                    {
                        IMyEntity hitEnt = MyCubeBuilder.Static.HitInfo?.GetHitEnt();
                        MyAPIGateway.Utilities.ShowNotification($"EquippedBlockMatrix: grid-locked: {hitEnt}", 16, FontsHandler.SEOutlined);
                    }
            }

            return true;
        }

        static MyCameraControllerEnum GetCameraControllerEnum()
        {
            IMyCameraController camCtrl = MyAPIGateway.Session.CameraController;
            if(camCtrl == null)
                return MyCameraControllerEnum.Spectator;

            string camControllerName = camCtrl.GetType().Name;

            if(camControllerName == "MySpectatorCameraController")
            {
                return MyCameraControllerEnum.Spectator;

                //switch(MySpectatorCameraController.Static.SpectatorCameraMovement)
                //{
                //    case MySpectatorCameraMovementEnum.UserControlled:
                //        return MyCameraControllerEnum.Spectator;
                //    case MySpectatorCameraMovementEnum.ConstantDelta:
                //        return MyCameraControllerEnum.SpectatorDelta;
                //    case MySpectatorCameraMovementEnum.None:
                //        return MyCameraControllerEnum.SpectatorFixed;
                //    case MySpectatorCameraMovementEnum.Orbit:
                //        return MyCameraControllerEnum.SpectatorOrbit;
                //}
            }
            else
            {
                if(camControllerName == "MyThirdPersonSpectator")
                {
                    return MyCameraControllerEnum.ThirdPersonSpectator;
                }

                if(camCtrl is MyEntity || camCtrl is MyEntityRespawnComponentBase)
                {
                    if((!camCtrl.IsInFirstPersonView && !camCtrl.ForceFirstPersonCamera) || !camCtrl.EnableFirstPersonView)
                    {
                        return MyCameraControllerEnum.ThirdPersonSpectator;
                    }

                    return MyCameraControllerEnum.Entity;
                }
            }
            return MyCameraControllerEnum.Spectator;
        }

        /// <summary>
        /// If all 3 axis are less than <paramref name="allMinScale"/> then the whole matrix gets resized to that value.
        /// Otherwise each axis cannot go smaller than <paramref name="individualMinScale"/>.
        /// </summary>
        public static void MatrixMinSize(ref Matrix matrix, float allMinScale, float individualMinScale)
        {
            float rightScale = matrix.Right.Length();
            float upScale = matrix.Up.Length();
            float backScale = matrix.Backward.Length();

            if(rightScale < allMinScale && upScale < allMinScale && backScale < allMinScale)
            {
                matrix.Right *= (allMinScale / rightScale);
                matrix.Up *= (allMinScale / upScale);
                matrix.Backward *= (allMinScale / backScale);
            }
            else
            {
                if(rightScale < individualMinScale)
                    matrix.Right *= (individualMinScale / rightScale);

                if(upScale < individualMinScale)
                    matrix.Up *= (individualMinScale / upScale);

                if(backScale < individualMinScale)
                    matrix.Backward *= (individualMinScale / backScale);
            }

            // iterate Right,Up,Back
            //for(int i = 0; i < 3; i++)
            //{
            //    Base6Directions.Direction dirEnum = Base6Directions.GetBaseAxisDirection((Base6Directions.Axis)i);
            //    Vector3 dirVec = matrix.GetDirectionVector(dirEnum);
            //
            //    float dirScaleSq = dirVec.LengthSquared();
            //    if(dirScaleSq < minScale * minScale)
            //    {
            //        float dirScale = (float)Math.Sqrt(dirScaleSq);
            //        matrix.SetDirectionVector(dirEnum, dirVec * (minScale / dirScale));
            //    }
            //}
        }

        public static IMyModelDummy GetDummy(IMyModel model, string name)
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            try
            {
                if(dummies.Count > 0)
                {
                    Log.Error("Dummies list already had some values in it!");
                    dummies = new Dictionary<string, IMyModelDummy>();
                }

                model.GetDummies(dummies);
                return dummies.GetValueOrDefault(name);
            }
            finally
            {
                dummies.Clear();
            }
        }

        public static Dictionary<string, IMyModelDummy> GetDummies(IMyModel model)
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            if(dummies.Count > 0)
            {
                Log.Error("Dummies list already had some values in it!");
                dummies = new Dictionary<string, IMyModelDummy>();
            }
            model.GetDummies(dummies);
            return dummies;
        }

        /// <summary>
        /// Allows non-normalized vectors and returns angle in radians.
        /// Credit to Whiplash141 for the maffs!
        /// </summary>
        public static double VectorAngleBetween(Vector3D a, Vector3D b)
        {
            if(Vector3D.IsZero(a) || Vector3D.IsZero(b))
                return 0;
            else
                return Math.Acos(MathHelper.Clamp(a.Dot(b) / Math.Sqrt(a.LengthSquared() * b.LengthSquared()), -1, 1));
        }

        public static IMyPlayer GetPlayerFromIdentityId(long identityId)
        {
            List<IMyPlayer> players = BuildInfoMod.Instance.Caches.Players;
            players.Clear();
            MyAPIGateway.Players.GetPlayers(players);

            IMyPlayer returnPlayer = null;

            foreach(IMyPlayer player in players)
            {
                if(player.IdentityId == identityId)
                {
                    returnPlayer = player;
                    break;
                }
            }

            players.Clear();
            return returnPlayer;
        }

        /// <summary>
        /// Chat message with the sender name being colored.
        /// NOTE: this is synchronized to all players but only the intended player(s) will see it.
        /// <para><paramref name="senderFont"/> sets the font for timestamp and sender name.</para>
        /// <para><paramref name="senderColor"/> sets the color for the sender name, multiplies with <paramref name="senderFont"/>'s color.</para>
        /// </summary>
        public static void ShowColoredChatMessage(string from, string message, string senderFont = null, Color? senderColor = null)
        {
            // HACK: SendChatMessageColored() no longer works if sent by MP clients (even to themselves)
            MyAPIGateway.Utilities.ShowMessage(from, message);

            /*
            if(MyAPIGateway.Session?.Player == null)
                return;

            long identityId = MyAPIGateway.Session.Player.IdentityId;

            if(!senderColor.HasValue)
                senderColor = Color.White;

            if(senderFont == null)
                senderFont = FontsHandler.WhiteSh;

            // NOTE: this is sent to all players and only shown if their identityId matches the one sent.
            MyVisualScriptLogicProvider.SendChatMessageColored(message, senderColor.Value, from, identityId, senderFont);
            */
        }

        public static MyOwnershipShareModeEnum GetBlockShareMode(IMyCubeBlock block)
        {
            if(block != null)
            {
                MyCubeBlock internalBlock = (MyCubeBlock)block;

                // Because the game has 2 ownership systems and I've no idea which one is actually used in what case, and it doesn't seem it knows either since it uses both in initialization.
                // HACK MyEntityOwnershipComponent is not whitelisted
                //MyEntityOwnershipComponent ownershipComp = internalBlock.Components.Get<MyEntityOwnershipComponent>();
                //
                //if(ownershipComp != null)
                //    return ownershipComp.ShareMode;

                if(internalBlock.IDModule != null)
                    return internalBlock.IDModule.ShareMode;
            }

            return MyOwnershipShareModeEnum.None;
        }

        /// <summary>
        /// Returns true if specified definition has all faces fully airtight.
        /// The referenced arguments are assigned with the said values which should only really be used if it returns false (due to the quick escape return true).
        /// An fully airtight face means it keeps the grid airtight when the face is the only obstacle between empty void and the ship's interior.
        /// Due to the complexity of airtightness when connecting blocks, this method simply can not indicate that, that's what the mount points view is for.
        /// </summary>
        public static AirTightMode GetAirTightFaces(MyCubeBlockDefinition def, out int airTightFaces, out int totalFaces)
        {
            airTightFaces = 0;
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

                    if(kv2.Value != MyCubeBlockDefinition.MyCubePressurizationMark.NotPressurized)
                        airTightFaces++;

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

        /// <summary>
        /// Get entity component of specified <typeparamref name="TDef"/> type (inhereting <see cref="MyComponentDefinitionBase"/>) from given definitionId.
        /// <para>NOTE: <paramref name="componentObType"/> must be MyObjectBuilder_TheTypeHere, without Definition suffix!</para>
        /// </summary>
        public static TDef GetEntityComponentFromDef<TDef>(MyDefinitionId defId, MyObjectBuilderType componentObType) where TDef : MyComponentDefinitionBase
        {
            MyContainerDefinition containerDef;
            if(MyComponentContainerExtension.TryGetContainerDefinition(defId.TypeId, defId.SubtypeId, out containerDef) && containerDef.DefaultComponents != null)
            {
                foreach(MyContainerDefinition.DefaultComponent compPointer in containerDef.DefaultComponents)
                {
                    if(compPointer.BuilderType != componentObType)
                        continue;

                    MyComponentDefinitionBase compDefBase;
                    if(MyComponentContainerExtension.TryGetComponentDefinition(compPointer.BuilderType, compPointer.SubtypeId.GetValueOrDefault(defId.SubtypeId), out compDefBase))
                    {
                        TDef comp = compDefBase as TDef;
                        if(comp != null)
                            return comp;
                    }
                }
            }

            return null;
        }

        public static bool IsEntityComponentPresent(MyDefinitionId defId, MyObjectBuilderType componentObType)
        {
            MyContainerDefinition containerDef;
            if(MyComponentContainerExtension.TryGetContainerDefinition(defId.TypeId, defId.SubtypeId, out containerDef) && containerDef.DefaultComponents != null)
            {
                foreach(MyContainerDefinition.DefaultComponent compPointer in containerDef.DefaultComponents)
                {
                    if(compPointer.BuilderType == componentObType)
                        return true;
                }
            }

            return false;
        }

        public static MyInventoryComponentDefinition GetInventoryFromComponent(MyDefinitionBase def) => GetEntityComponentFromDef<MyInventoryComponentDefinition>(def.Id, typeof(MyObjectBuilder_Inventory));

        /// <summary>
        /// Gets the inventory volume from the EntityComponents and EntityContainers definitions.
        /// </summary>
        public static bool GetInventoryVolumeFromComponent(MyDefinitionBase def, out float volume)
        {
            MyInventoryComponentDefinition invComp = GetEntityComponentFromDef<MyInventoryComponentDefinition>(def.Id, typeof(MyObjectBuilder_Inventory));

            if(invComp != null)
            {
                volume = invComp.Volume;
                return true;
            }
            else
            {
                volume = 0;
                return false;
            }
        }

        public static string ColorTag(Color color)
        {
            return $"<color={color.R.ToString()},{color.G.ToString()},{color.B.ToString()}>";
        }

        public static string ColorTag(Color color, string value)
        {
            return $"<color={color.R.ToString()},{color.G.ToString()},{color.B.ToString()}>{value}";
        }

        public static string ColorTag(Color color, string value1, string value2)
        {
            return $"<color={color.R.ToString()},{color.G.ToString()},{color.B.ToString()}>{value1}{value2}";
        }

        public static bool CreativeToolsEnabled => MyAPIGateway.Session.CreativeMode || (MyAPIGateway.Session.HasCreativeRights && MyAPIGateway.Session.EnableCopyPaste);

        /// <summary>
        /// HACK: matching vanilla HUD transparency better
        /// </summary>
        public static void FadeColorHUD(ref Color color, float opacity)
        {
            color *= opacity * (opacity * 1.075f);
            color.A = (byte)(opacity * 255);
        }

        public static Color GetIndexColor(int index, int maxIndex)
        {
            return new Vector3((index % maxIndex) / (float)maxIndex, 0.75f, 1f).HSVtoColor();
        }

        // Optimized wireframe draw
        public static void DrawTransparentSphere(ref MatrixD worldMatrix, float radius, ref Color color, MySimpleObjectRasterizer rasterization, int wireDivideRatio, MyStringId material, float lineThickness = -1f, int customViewProjection = -1, BlendTypeEnum blendType = BlendTypeEnum.Standard)
        {
            bool drawWireframe = (rasterization != MySimpleObjectRasterizer.Solid);
            bool drawSolid = (rasterization != MySimpleObjectRasterizer.Wireframe);

            if(lineThickness < 0)
                lineThickness = 0.01f;

            List<Vector3D> vertices = BuildInfoMod.Instance.Caches.Vertices;
            vertices.Clear();
            GetSphereVertices(ref worldMatrix, radius, wireDivideRatio, vertices);
            Vector3D center = worldMatrix.Translation;
            MyQuadD quad;

            for(int i = 0; i < vertices.Count; i += 4)
            {
                quad.Point0 = vertices[i + 1];
                quad.Point1 = vertices[i + 3];
                quad.Point2 = vertices[i + 2];
                quad.Point3 = vertices[i];

                if(drawWireframe)
                {
                    MyTransparentGeometry.AddLineBillboard(material, color, quad.Point0, (Vector3)(quad.Point1 - quad.Point0), 1f, lineThickness, blendType, customViewProjection);
                    MyTransparentGeometry.AddLineBillboard(material, color, quad.Point1, (Vector3)(quad.Point2 - quad.Point1), 1f, lineThickness, blendType, customViewProjection);
                }

                if(drawSolid)
                {
                    MyTransparentGeometry.AddQuad(material, ref quad, color, ref center, customViewProjection, blendType);
                }
            }
        }

        // Added wireframe and blend type as well as optimized.
        public static void DrawTransparentCone(ref MatrixD worldMatrix, float radius, float height, ref Color color, MySimpleObjectRasterizer rasterization, int wireDivideRatio, MyStringId material, float lineThickness = -1, int customViewProjection = -1, BlendTypeEnum blendType = BlendTypeEnum.Standard)
        {
            Vector3D apexPosition = worldMatrix.Translation;
            Vector3D directionVector = worldMatrix.Forward * height;
            Vector3D axisNormalized = worldMatrix.Forward;
            Vector3D baseVector = worldMatrix.Up * radius;

            bool drawWireframe = (rasterization != MySimpleObjectRasterizer.Solid);
            bool drawSolid = (rasterization != MySimpleObjectRasterizer.Wireframe);

            if(lineThickness < 0)
                lineThickness = 0.01f;

            MyQuadD quad;
            Vector3D offset = apexPosition + directionVector;

            double angleStep = (MathHelperD.TwoPi / (double)wireDivideRatio);
            Vector3D prevPoint = offset + Vector3D.Transform(baseVector, MatrixD.CreateFromAxisAngle(axisNormalized, 0)); // angle = (i * angleStep) == 0

            for(int i = 0; i < wireDivideRatio; i++)
            {
                double nextAngle = (i + 1) * angleStep;
                Vector3D nextPoint = offset + Vector3D.Transform(baseVector, MatrixD.CreateFromAxisAngle(axisNormalized, nextAngle));

                if(drawWireframe)
                {
                    MyTransparentGeometry.AddLineBillboard(material, color, prevPoint, (Vector3)(apexPosition - prevPoint), 1f, lineThickness, blendType, customViewProjection);
                    MyTransparentGeometry.AddLineBillboard(material, color, nextPoint, (Vector3)(apexPosition - nextPoint), 1f, lineThickness, blendType, customViewProjection);
                }

                if(drawSolid)
                {
                    quad.Point0 = prevPoint;
                    quad.Point1 = nextPoint;
                    quad.Point2 = apexPosition;
                    quad.Point3 = apexPosition;
                    MyTransparentGeometry.AddQuad(material, ref quad, color, ref Vector3D.Zero, -1, blendType, null);
                }

                prevPoint = nextPoint;
            }
        }

        // Added wireframe and blend type as well as optimized.
        public static void DrawTransparentCapsule(ref MatrixD worldMatrix, float radius, float height, ref Color color, MySimpleObjectRasterizer rasterization, int wireDivideRatio, MyStringId material, float lineThickness = -1, int customViewProjection = -1, BlendTypeEnum blendType = BlendTypeEnum.Standard)
        {
            if(lineThickness < 0)
                lineThickness = 0.01f;

            bool drawWireframe = (rasterization != MySimpleObjectRasterizer.Solid);
            bool drawSolid = (rasterization != MySimpleObjectRasterizer.Wireframe);

            Vector3D center = worldMatrix.Translation;
            double halfHeight = height * 0.5;
            MyQuadD quad;

            #region Sphere halves
            MatrixD sphereMatrix = MatrixD.CreateRotationX(-MathHelperD.PiOver2);
            sphereMatrix.Translation = new Vector3D(0.0, halfHeight, 0.0);
            sphereMatrix *= worldMatrix;

            List<Vector3D> vertices = BuildInfoMod.Instance.Caches.Vertices;
            vertices.Clear();
            GetSphereVertices(ref sphereMatrix, radius, wireDivideRatio, vertices);

            int halfVerts = vertices.Count / 2;
            Vector3D addVec = worldMatrix.Down * height;

            for(int i = 0; i < vertices.Count; i += 4)
            {
                if(i < halfVerts)
                {
                    quad.Point0 = vertices[i + 1];
                    quad.Point1 = vertices[i + 3];
                    quad.Point2 = vertices[i + 2];
                    quad.Point3 = vertices[i];
                }
                else // offset other half by the height of the cylinder
                {
                    quad.Point0 = vertices[i + 1] + addVec;
                    quad.Point1 = vertices[i + 3] + addVec;
                    quad.Point2 = vertices[i + 2] + addVec;
                    quad.Point3 = vertices[i] + addVec;
                }

                if(drawWireframe)
                {
                    MyTransparentGeometry.AddLineBillboard(material, color, quad.Point0, (Vector3)(quad.Point1 - quad.Point0), 1f, lineThickness, blendType, customViewProjection);
                    MyTransparentGeometry.AddLineBillboard(material, color, quad.Point1, (Vector3)(quad.Point2 - quad.Point1), 1f, lineThickness, blendType, customViewProjection);
                }

                if(drawSolid)
                {
                    MyTransparentGeometry.AddQuad(material, ref quad, color, ref center, customViewProjection, blendType);
                }
            }
            #endregion

            #region Cylinder
            double wireDivAngle = MathHelperD.Pi * 2f / (double)wireDivideRatio;
            double angle = 0f;

            for(int k = 0; k < wireDivideRatio; k++)
            {
                angle = k * wireDivAngle;
                double cos = (radius * Math.Cos(angle));
                double sin = (radius * Math.Sin(angle));
                quad.Point0.X = cos;
                quad.Point0.Z = sin;
                quad.Point3.X = cos;
                quad.Point3.Z = sin;

                angle = (k + 1) * wireDivAngle;
                cos = (radius * Math.Cos(angle));
                sin = (radius * Math.Sin(angle));
                quad.Point1.X = cos;
                quad.Point1.Z = sin;
                quad.Point2.X = cos;
                quad.Point2.Z = sin;

                quad.Point0.Y = -halfHeight;
                quad.Point1.Y = -halfHeight;
                quad.Point2.Y = halfHeight;
                quad.Point3.Y = halfHeight;

                quad.Point0 = Vector3D.Transform(quad.Point0, worldMatrix);
                quad.Point1 = Vector3D.Transform(quad.Point1, worldMatrix);
                quad.Point2 = Vector3D.Transform(quad.Point2, worldMatrix);
                quad.Point3 = Vector3D.Transform(quad.Point3, worldMatrix);

                if(drawWireframe)
                {
                    MyTransparentGeometry.AddLineBillboard(material, color, quad.Point0, (Vector3)(quad.Point1 - quad.Point0), 1f, lineThickness, blendType, customViewProjection);
                    MyTransparentGeometry.AddLineBillboard(material, color, quad.Point1, (Vector3)(quad.Point2 - quad.Point1), 1f, lineThickness, blendType, customViewProjection);
                }

                if(drawSolid)
                {
                    MyTransparentGeometry.AddQuad(material, ref quad, color, ref center, customViewProjection, blendType);
                }
            }
            #endregion
        }

        public static void DrawTransparentCylinder(ref MatrixD worldMatrix, float radius, float height, ref Color color, MySimpleObjectRasterizer rasterization, int wireDivideRatio, MyStringId faceMaterial, MyStringId lineMaterial, float lineThickness = -1, int customViewProjection = -1, bool drawCaps = true, BlendTypeEnum blendType = BlendTypeEnum.Standard)
        {
            if(lineThickness < 0)
                lineThickness = 0.01f;

            bool drawWireframe = (rasterization != MySimpleObjectRasterizer.Solid);
            bool drawSolid = (rasterization != MySimpleObjectRasterizer.Wireframe);

            Vector4 triangleColor = (drawCaps ? color.ToVector4().ToLinearRGB() : color.ToVector4()); // HACK: keeping color consistent with other billboards, MyTransparentGeoemtry.CreateBillboard()

            Vector3D center = worldMatrix.Translation;
            Vector3 normal = (Vector3)worldMatrix.Up;

            double halfHeight = height * 0.5;
            MyQuadD quad;

            Vector3D topDir = worldMatrix.Up * halfHeight;
            Vector3D centerTop = center + topDir;
            Vector3D centerBottom = center - topDir;

            double wireDivAngle = MathHelperD.Pi * 2f / (double)wireDivideRatio;
            double angle = 0f;

            Vector2 uv0 = new Vector2(0, 0.5f);
            Vector2 uv1 = new Vector2(1, 0);
            Vector2 uv2 = new Vector2(1, 1);

            for(int k = 0; k < wireDivideRatio; k++)
            {
                angle = k * wireDivAngle;
                double cos = (radius * Math.Cos(angle));
                double sin = (radius * Math.Sin(angle));
                quad.Point0.X = cos;
                quad.Point0.Z = sin;
                quad.Point3.X = cos;
                quad.Point3.Z = sin;

                angle = (k + 1) * wireDivAngle;
                cos = (radius * Math.Cos(angle));
                sin = (radius * Math.Sin(angle));
                quad.Point1.X = cos;
                quad.Point1.Z = sin;
                quad.Point2.X = cos;
                quad.Point2.Z = sin;

                quad.Point0.Y = -halfHeight;
                quad.Point1.Y = -halfHeight;
                quad.Point2.Y = halfHeight;
                quad.Point3.Y = halfHeight;

                quad.Point0 = Vector3D.Transform(quad.Point0, worldMatrix);
                quad.Point1 = Vector3D.Transform(quad.Point1, worldMatrix);
                quad.Point2 = Vector3D.Transform(quad.Point2, worldMatrix);
                quad.Point3 = Vector3D.Transform(quad.Point3, worldMatrix);

                if(drawWireframe)
                {
                    // circle bottom
                    MyTransparentGeometry.AddLineBillboard(lineMaterial, color, quad.Point0, (Vector3)(quad.Point1 - quad.Point0), 1f, lineThickness, blendType, customViewProjection);

                    // vertical
                    MyTransparentGeometry.AddLineBillboard(lineMaterial, color, quad.Point1, (Vector3)(quad.Point2 - quad.Point1), 1f, lineThickness, blendType, customViewProjection);

                    // circle top
                    MyTransparentGeometry.AddLineBillboard(lineMaterial, color, quad.Point3, (Vector3)(quad.Point2 - quad.Point3), 1f, lineThickness, blendType, customViewProjection);
                }

                if(drawSolid)
                {
                    MyTransparentGeometry.AddQuad(faceMaterial, ref quad, color, ref center, customViewProjection, blendType);
                }

                if(drawCaps)
                {
                    if(drawSolid)
                    {
                        // bottom cap
                        MyTransparentGeometry.AddTriangleBillboard(centerBottom, quad.Point0, quad.Point1, normal, normal, normal, uv0, uv1, uv2, faceMaterial, 0, center, triangleColor, blendType);

                        // top cap
                        MyTransparentGeometry.AddTriangleBillboard(centerTop, quad.Point2, quad.Point3, normal, normal, normal, uv0, uv1, uv2, faceMaterial, 0, center, triangleColor, blendType);
                    }

                    if(drawWireframe)
                    {
                        // bottom
                        MyTransparentGeometry.AddLineBillboard(lineMaterial, color, quad.Point0, (Vector3)(centerBottom - quad.Point0), 1f, lineThickness, blendType, customViewProjection);

                        // top
                        MyTransparentGeometry.AddLineBillboard(lineMaterial, color, quad.Point3, (Vector3)(centerTop - quad.Point3), 1f, lineThickness, blendType, customViewProjection);
                    }
                }
            }
        }

        /// <summary>
        /// Copied MyMeshHelper.GenerateSphere() to prevent a game crash from a list being accessed in a thread.
        /// This was also optimized and even removed the need for the troublesome list.
        /// Also added a cache for vertices, indexed by steps.
        /// </summary>
        public static void GetSphereVertices(ref MatrixD worldMatrix, float radius, int steps, List<Vector3D> vertices)
        {
            Dictionary<int, List<Vector3D>> generatedSphereData = BuildInfoMod.Instance.Caches.GeneratedSphereData;
            List<Vector3D> cachedVerts;
            if(!generatedSphereData.TryGetValue(steps, out cachedVerts))
            {
                cachedVerts = new List<Vector3D>();
                generatedSphereData[steps] = cachedVerts;
                GenerateSphere(ref worldMatrix, radius, steps, cachedVerts);
            }

            foreach(Vector3D vert in cachedVerts)
            {
                vertices.Add(Vector3D.Transform(vert * radius, worldMatrix));
            }
        }

        private static void GenerateSphere(ref MatrixD worldMatrix, float radius, int steps, List<Vector3D> vertices)
        {
            double angleStep = MathHelperD.ToRadians(360 / steps);
            double ang1max = MathHelperD.PiOver2 - angleStep;
            double ang2max = MathHelperD.TwoPi - angleStep;
            Vector3D vec;

            for(double ang1 = 0f; ang1 <= ang1max; ang1 += angleStep)
            {
                double ang1sin = Math.Sin(ang1);
                double ang1cos = Math.Cos(ang1);

                for(double ang2 = 0f; ang2 <= ang2max; ang2 += angleStep)
                {
                    double ang2sin = Math.Sin(ang2);
                    double ang2cos = Math.Cos(ang2);

                    double nextAng1sin = Math.Sin(ang1 + angleStep);
                    double nextAng1cos = Math.Cos(ang1 + angleStep);

                    double nextAng2sin = Math.Sin(ang2 + angleStep);
                    double nextAng2cos = Math.Cos(ang2 + angleStep);

                    vec.X = ang2sin * ang1sin;
                    vec.Y = ang2cos * ang1sin;
                    vec.Z = ang1cos;
                    vertices.Add(vec);

                    vec.X = ang2sin * nextAng1sin;
                    vec.Y = ang2cos * nextAng1sin;
                    vec.Z = nextAng1cos;
                    vertices.Add(vec);

                    vec.X = nextAng2sin * ang1sin;
                    vec.Y = nextAng2cos * ang1sin;
                    vec.Z = ang1cos;
                    vertices.Add(vec);

                    vec.X = nextAng2sin * nextAng1sin;
                    vec.Y = nextAng2cos * nextAng1sin;
                    vec.Z = nextAng1cos;
                    vertices.Add(vec);
                }
            }

            // add the other half
            int totalBeforeAdd = vertices.Count;
            for(int i = 0; i < totalBeforeAdd; i++)
            {
                Vector3D v = vertices[i];
                vertices.Add(new Vector3D(v.X, v.Y, 0.0 - v.Z));
            }
        }
    }
}
