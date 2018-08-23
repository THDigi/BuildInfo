using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.BlockData;
using Digi.BuildInfo.Extensions;
using Draygo.API;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.BuildInfo
{
    public partial class BuildInfo
    {
        private int drawOverlay = 0;
        private bool overlaysDrawn = false;
        private bool doorAirtightBlink = false;
        private int doorAirtightBlinkTick = 0;

        private OverlayCall selectedOverlayCall;
        private delegate void OverlayCall(MyCubeBlockDefinition def, MatrixD drawMatrix);
        private readonly Dictionary<MyObjectBuilderType, OverlayCall> drawLookup
                   = new Dictionary<MyObjectBuilderType, OverlayCall>(MyObjectBuilderType.Comparer);

        private void InitOverlays()
        {
            drawLookup.Add(typeof(MyObjectBuilder_ShipWelder), DrawOverlay_ShipTool);
            drawLookup.Add(typeof(MyObjectBuilder_ShipGrinder), DrawOverlay_ShipTool);

            drawLookup.Add(typeof(MyObjectBuilder_Drill), DrawOverlay_Drill);

            drawLookup.Add(typeof(MyObjectBuilder_SmallGatlingGun), DrawOverlay_Weapons);
            drawLookup.Add(typeof(MyObjectBuilder_SmallMissileLauncher), DrawOverlay_Weapons);
            drawLookup.Add(typeof(MyObjectBuilder_SmallMissileLauncherReload), DrawOverlay_Weapons);
            drawLookup.Add(typeof(MyObjectBuilder_LargeGatlingTurret), DrawOverlay_Weapons);
            drawLookup.Add(typeof(MyObjectBuilder_LargeMissileTurret), DrawOverlay_Weapons);
            drawLookup.Add(typeof(MyObjectBuilder_InteriorTurret), DrawOverlay_Weapons);

            drawLookup.Add(typeof(MyObjectBuilder_Door), DrawOverlay_Doors);
            drawLookup.Add(typeof(MyObjectBuilder_AdvancedDoor), DrawOverlay_Doors);
            drawLookup.Add(typeof(MyObjectBuilder_AirtightDoorGeneric), DrawOverlay_Doors);
            drawLookup.Add(typeof(MyObjectBuilder_AirtightHangarDoor), DrawOverlay_Doors);
            drawLookup.Add(typeof(MyObjectBuilder_AirtightSlideDoor), DrawOverlay_Doors);

            drawLookup.Add(typeof(MyObjectBuilder_Thrust), DrawOverlay_Thruster);

            drawLookup.Add(typeof(MyObjectBuilder_LandingGear), DrawOverlay_LandingGear);

            drawLookup.Add(typeof(MyObjectBuilder_Collector), DrawOverlay_Collector);
        }

        private void DrawOverlays()
        {
            if(drawOverlay > 0 && (hudVisible || Settings.alwaysVisible) && selectedDef != null)
            {
                // TODO: use?
                //overlayNotification.Text = $"Showing {DRAW_OVERLAY_NAME[drawOverlay]} overlays (Ctrl+{voxelHandSettingsInput} to cycle)";
                //overlayNotification.AliveTime = 32;
                //overlayNotification.Show();

                var def = selectedDef;

                // needed to hide text messages that are no longer used while other stuff still draws
                for(int i = 0; i < textAPILabels.Length; ++i)
                {
                    var msgObj = textAPILabels[i];

                    if(msgObj != null)
                    {
                        msgObj.Visible = false;
                        textAPIShadows[i].Visible = false;
                    }
                }

                overlaysDrawn = true;

                #region DrawMatrix and other needed data
                var drawMatrix = MatrixD.Identity;

                if(selectedBlock == null) // using cubebuilder
                {
                    var box = MyCubeBuilder.Static.GetBuildBoundingBox();
                    drawMatrix = MatrixD.CreateFromQuaternion(box.Orientation);

                    if(MyCubeBuilder.Static.DynamicMode)
                    {
                        var hit = (MyCubeBuilder.Static.HitInfo as IHitInfo);

                        if(hit != null && hit.HitEntity is IMyVoxelBase)
                            drawMatrix.Translation = hit.Position; // HACK: required for position to be accurate when aiming at a planet
                        else
                            drawMatrix.Translation = MyCubeBuilder.Static.FreePlacementTarget; // HACK: required for the position to be 100% accurate when the block is not aimed at anything
                    }
                    else
                    {
                        drawMatrix.Translation = box.Center;
                    }
                }
                else // using welder/grinder
                {
                    Matrix m;
                    Vector3D center;
                    selectedBlock.Orientation.GetMatrix(out m);
                    selectedBlock.ComputeWorldCenter(out center);

                    drawMatrix = m * selectedBlock.CubeGrid.WorldMatrix;
                    drawMatrix.Translation = center;
                }

                #endregion

                #region Draw mount points
                if(TextAPIEnabled)
                {
                    DrawMountPointAxixText(def, selectedGridSize, ref drawMatrix);
                }
                else
                {
                    // HACK re-assigning mount points temporarily to prevent the original mountpoint wireframe from being drawn while keeping the axis information
                    var mp = def.MountPoints;
                    def.MountPoints = BLANK_MOUNTPOINTS;
                    MyCubeBuilder.DrawMountPoints(selectedGridSize, def, ref drawMatrix);
                    def.MountPoints = mp;
                }

                bool blockFunctionalForPressure = true;

                if(selectedBlock != null && selectedDef.BuildProgressModels != null)
                {
                    // HACK condition matching the condition in MyGridGasSystem.IsPressurized(MySlimBlock block, Vector3I pos, Vector3 normal)
                    blockFunctionalForPressure = (selectedBlock.BuildLevelRatio >= selectedDef.BuildProgressModels[selectedDef.BuildProgressModels.Length - 1].BuildRatioUpperBound);
                }

                // draw custom mount point styling
                {
                    var minSize = (def.CubeSize == MyCubeSize.Large ? 0.05 : 0.02); // a minimum size to have some thickness
                    var center = def.Center;
                    var mainMatrix = MatrixD.CreateTranslation((center - (def.Size * 0.5f)) * selectedGridSize) * drawMatrix;
                    var mountPoints = def.GetBuildProgressModelMountPoints(1f);
                    bool drawLabel = Settings.allLabels && TextAPIEnabled;

                    if(drawOverlay == 1)
                    {
                        if(def.IsAirTight) // HACK IsAirTight makes it airtight even if the block is not fully built.
                        {
                            var halfExtents = def.Size * (selectedGridSize * 0.5);
                            var localBB = new BoundingBoxD(-halfExtents, halfExtents).Inflate(MOUNTPOINT_THICKNESS * 0.5);
                            MySimpleObjectDraw.DrawTransparentBox(ref drawMatrix, ref localBB, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE);
                        }
                        else if(blockFunctionalForPressure && mountPoints != null)
                        {
                            var half = Vector3D.One * -(0.5f * selectedGridSize);
                            var corner = (Vector3D)def.Size * -(0.5f * selectedGridSize);
                            var transformMatrix = MatrixD.CreateTranslation(corner - half) * drawMatrix;

                            foreach(var kv in def.IsCubePressurized) // precomputed: [position][normal] = is airtight
                            {
                                foreach(var kv2 in kv.Value)
                                {
                                    if(!kv2.Value) // pos+normal not airtight
                                        continue;

                                    var pos = Vector3D.Transform((Vector3D)(kv.Key * selectedGridSize), transformMatrix);
                                    var dirForward = Vector3.TransformNormal(kv2.Key, drawMatrix);
                                    var dirIndex = (int)Base6Directions.GetDirection(kv2.Key);
                                    var dirUp = Vector3.TransformNormal(DIRECTIONS[((dirIndex + 2) % 6)], drawMatrix);

                                    var m = MatrixD.Identity;
                                    m.Translation = pos + dirForward * (selectedGridSize * 0.5f);
                                    m.Forward = dirForward;
                                    m.Backward = -dirForward;
                                    m.Left = Vector3D.Cross(dirForward, dirUp);
                                    m.Right = -m.Left;
                                    m.Up = dirUp;
                                    m.Down = -dirUp;
                                    var scale = new Vector3D(selectedGridSize, selectedGridSize, MOUNTPOINT_THICKNESS);
                                    MatrixD.Rescale(ref m, ref scale);

                                    MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_COLOR, ref AIRTIGHT_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                                }
                            }
                        }
                    }
                    else if(drawOverlay == 2 && mountPoints != null)
                    {
                        for(int i = 0; i < mountPoints.Length; i++)
                        {
                            var mountPoint = mountPoints[i];

                            if(!mountPoint.Enabled)
                                continue; // ignore all disabled mount points as airtight ones are rendered separate

                            var colorFace = (mountPoint.Default ? MOUNTPOINT_DEFAULT_COLOR : MOUNTPOINT_COLOR);

                            DrawMountPoint(mountPoint, selectedGridSize, ref center, ref mainMatrix, ref colorFace, minSize);
                        }
                    }
                }
                #endregion

                // draw per-block overlays
                selectedOverlayCall?.Invoke(def, drawMatrix);



                // testing real time pressurization display
#if false
                {
                    var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

                    if(def != null && MyCubeBuilder.Static.IsActivated)
                    {
                        var grid = MyCubeBuilder.Static.FindClosestGrid();

                        Vector3D worldAdd;
                        MyCubeBuilder.Static.GetAddPosition(out worldAdd);

                        var bb = MyCubeBuilder.Static.GetBuildBoundingBox();
                        var matrix = Matrix.CreateFromQuaternion(bb.Orientation);

                        var startPos = grid.WorldToGridInteger(worldAdd);

                        for(int i = 0; i < Base6Directions.IntDirections.Length; ++i)
                        {
                            var endPos = startPos + Base6Directions.IntDirections[i];
                            bool airtight = def.IsAirTight || Pressurization.TestPressurize(startPos, endPos - startPos, matrix, def);

                            //if(!airtight)
                            //{
                            //    IMySlimBlock b2 = grid.GetCubeBlock(startPos);
                            //
                            //    if(b2 != null)
                            //    {
                            //        var def2 = (MyCubeBlockDefinition)b2.BlockDefinition;
                            //        airtight = def2.IsAirTight || Pressurization.IsPressurized(b2, endPos, startPos - endPos);
                            //    }
                            //}

                            MyTransparentGeometry.AddLineBillboard(MyStringId.GetOrCompute("Square"), (airtight ? Color.Green : Color.Red), worldAdd, Vector3D.TransformNormal(Base6Directions.IntDirections[i], matrix), 1f, 0.1f);

                            //MyAPIGateway.Utilities.ShowNotification($"{i}. airtight={airtight}", 16); // DEBUG print
                        }
                    }
                }
#endif
            }
            else
            {
                // no block equipped

                if(overlaysDrawn)
                {
                    overlaysDrawn = false;

                    for(int i = 0; i < textAPILabels.Length; ++i)
                    {
                        var msgObj = textAPILabels[i];

                        if(msgObj != null)
                        {
                            msgObj.Visible = false;
                            textAPIShadows[i].Visible = false;
                        }
                    }
                }
            }
        }

        #region Block-specific overlays
        private void DrawOverlay_Doors(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            if(drawOverlay != 1)
                return;

            if(selectedBlock != null)
            {
                doorAirtightBlink = true;
            }
            else if(!MyParticlesManager.Paused && ++doorAirtightBlinkTick >= 60)
            {
                doorAirtightBlinkTick = 0;
                doorAirtightBlink = !doorAirtightBlink;
            }

            var cubeSize = def.Size * (selectedGridSize * 0.5f);
            bool drawLabel = Settings.allLabels && TextAPIEnabled;

            if(!drawLabel && !doorAirtightBlink)
                return;

            bool fullyClosed = true;

            if(selectedBlock != null)
            {
                var door = selectedBlock.FatBlock as IMyDoor;

                if(door != null)
                    fullyClosed = (door.Status == Sandbox.ModAPI.Ingame.DoorStatus.Closed);
            }

            for(int i = 0; i < 6; ++i)
            {
                var normal = DIRECTIONS[i];

                if(Pressurization.IsDoorFacePressurized(def, (Vector3I)normal, fullyClosed))
                {
                    var dirForward = Vector3D.TransformNormal(normal, drawMatrix);
                    var dirLeft = Vector3D.TransformNormal(DIRECTIONS[((i + 4) % 6)], drawMatrix);
                    var dirUp = Vector3D.TransformNormal(DIRECTIONS[((i + 2) % 6)], drawMatrix);

                    var pos = drawMatrix.Translation + dirForward * cubeSize.GetDim((i % 6) / 2);
                    float width = cubeSize.GetDim(((i + 4) % 6) / 2);
                    float height = cubeSize.GetDim(((i + 2) % 6) / 2);

                    if(doorAirtightBlink)
                    {
                        var m = MatrixD.CreateWorld(pos, dirForward, dirUp);
                        m.Right *= width * 2;
                        m.Up *= height * 2;
                        m.Forward *= MOUNTPOINT_THICKNESS;
                        MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref AIRTIGHT_TOGGLE_COLOR, ref AIRTIGHT_TOGGLE_COLOR, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);
                    }

                    if(drawLabel) // only label the first one
                    {
                        drawLabel = false;

                        var labelPos = pos + dirLeft * width + dirUp * height;
                        DrawLineLabelAlternate(TextAPIMsgIds.DOOR_AIRTIGHT, labelPos, labelPos + dirLeft * 0.5, "Airtight when closed", AIRTIGHT_TOGGLE_COLOR, underlineLength: 1.7f);

                        if(!doorAirtightBlink) // no need to iterate further if no faces need to be rendered
                            break;
                    }
                }
            }
        }

        private void DrawOverlay_Weapons(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_Weapon>(def);

            if(data == null)
                return;

            const int wireDivideRatio = 12;
            const float lineHeight = 0.5f;
            var color = Color.Red;
            var colorFace = color * 0.5f;
            var weapon = (MyWeaponBlockDefinition)def;
            var wepDef = MyDefinitionManager.Static.GetWeaponDefinition(weapon.WeaponDefinitionId);
            MyAmmoDefinition ammo = null;

            if(selectedBlock != null)
            {
                var weaponBlock = selectedBlock.FatBlock as IMyGunObject<MyGunBase>;

                if(weaponBlock != null)
                    ammo = weaponBlock.GunBase.CurrentAmmoDefinition;
            }

            if(ammo == null)
            {
                var mag = MyDefinitionManager.Static.GetAmmoMagazineDefinition(wepDef.AmmoMagazinesId[0]);
                ammo = MyDefinitionManager.Static.GetAmmoDefinition(mag.AmmoDefinitionId);
            }

            var height = ammo.MaxTrajectory;
            var tanShotAngle = (float)Math.Tan(wepDef.DeviateShotAngle);
            var accuracyAtMaxRange = tanShotAngle * (height * 2);
            var coneMatrix = data.muzzleLocalMatrix * drawMatrix;

            MyTransparentGeometry.AddPointBillboard(MATERIAL_VANILLA_DOT, color, coneMatrix.Translation, 0.025f, 0, blendType: OVERLAY_BLEND_TYPE); // this is drawn always on top on purpose
            MySimpleObjectDraw.DrawTransparentCone(ref coneMatrix, accuracyAtMaxRange, height, ref colorFace, wireDivideRatio, faceMaterial: MATERIAL_SQUARE);

            //const int circleWireDivideRatio = 20;
            //var accuracyAt100m = tanShotAngle * (100 * 2);
            //var color100m = Color.Green.ToVector4();
            //var circleMatrix = MatrixD.CreateWorld(coneMatrix.Translation + coneMatrix.Forward * 3 + coneMatrix.Left * 3, coneMatrix.Down, coneMatrix.Forward);
            //MySimpleObjectDraw.DrawTransparentCylinder(ref circleMatrix, accuracyAt100m, accuracyAt100m, 0.1f, ref color100m, true, circleWireDivideRatio, 0.05f, MATERIAL_SQUARE);

            if(Settings.allLabels && TextAPIEnabled)
            {
                var labelDir = coneMatrix.Up;
                var labelLineStart = coneMatrix.Translation + coneMatrix.Forward * 3;
                DrawLineLabel(TextAPIMsgIds.ACCURACY_MAX, labelLineStart, labelDir, $"Accuracy cone - {height} m", color, constantTextUpdate: true, lineHeight: lineHeight, underlineLength: 1.75f);

                //var lineStart = circleMatrix.Translation + coneMatrix.Down * accuracyAt100m;
                //var labelStart = lineStart + coneMatrix.Down * 0.3f;
                //DrawLineLabelAlternate(TextAPIMsgIds.ACCURACY_100M, lineStart, labelStart, "At 100m (zoomed)", color100m, underlineLength: 1.5f);
            }
        }

        private void DrawOverlay_Drill(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var drill = (MyShipDrillDefinition)def;

            const float lineHeight = 0.3f;
            const int wireDivRatio = 20;
            var colorMine = Color.Lime;
            var colorMineFace = colorMine * 0.3f;
            var colorCut = Color.Red;
            var colorCutFace = colorCut * 0.3f;
            bool drawLabels = Settings.allLabels && TextAPIEnabled;

            drawMatrix.Translation += drawMatrix.Forward * drill.SensorOffset;
            MySimpleObjectDraw.DrawTransparentSphere(ref drawMatrix, drill.SensorRadius, ref colorMineFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: MATERIAL_SQUARE);

            bool showCutOut = (Math.Abs(drill.SensorRadius - drill.CutOutRadius) > 0.0001f || Math.Abs(drill.SensorOffset - drill.CutOutOffset) > 0.0001f);

            if(drawLabels)
            {
                var labelDir = drawMatrix.Down;
                var sphereEdge = drawMatrix.Translation + (labelDir * drill.SensorRadius);
                DrawLineLabel(TextAPIMsgIds.DRILL_MINE, sphereEdge, labelDir, (showCutOut ? "Mining radius" : "Mining/cutout radius"), colorMine, constantTextUpdate: true, lineHeight: lineHeight, underlineLength: (showCutOut ? 0.75f : 1f));
            }

            if(showCutOut)
            {
                var cutMatrix = drawMatrix;
                cutMatrix.Translation += cutMatrix.Forward * drill.CutOutOffset;
                MySimpleObjectDraw.DrawTransparentSphere(ref cutMatrix, drill.CutOutRadius, ref colorCutFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: MATERIAL_SQUARE);

                if(drawLabels)
                {
                    var labelDir = cutMatrix.Left;
                    var sphereEdge = cutMatrix.Translation + (labelDir * drill.CutOutRadius);
                    DrawLineLabel(TextAPIMsgIds.DRILL_CUTOUT, sphereEdge, labelDir, "Cutout radius", colorCut, lineHeight: lineHeight);
                }
            }
        }

        private void DrawOverlay_ShipTool(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_ShipTool>(def);

            if(data == null)
                return;

            const float lineHeight = 0.3f;
            const int wireDivRatio = 20;
            var color = Color.Lime;
            var colorFace = color * 0.3f;

            var toolDef = (MyShipToolDefinition)def;
            var matrix = data.DummyMatrix;
            var sensorCenter = matrix.Translation + matrix.Forward * toolDef.SensorOffset;
            drawMatrix.Translation = Vector3D.Transform(sensorCenter, drawMatrix);
            var radius = toolDef.SensorRadius;

            MySimpleObjectDraw.DrawTransparentSphere(ref drawMatrix, radius, ref colorFace, MySimpleObjectRasterizer.Solid, wireDivRatio, faceMaterial: MATERIAL_SQUARE);

            if(Settings.allLabels && TextAPIEnabled)
            {
                bool isWelder = def is MyShipWelderDefinition;
                var labelDir = drawMatrix.Down;
                var sphereEdge = drawMatrix.Translation + (labelDir * radius);
                DrawLineLabel(TextAPIMsgIds.SHIP_TOOL, sphereEdge, labelDir, (isWelder ? "Welding radius" : "Grinding radius"), color, constantTextUpdate: true, lineHeight: lineHeight, underlineLength: 0.75f);
            }
        }

        private void DrawOverlay_Thruster(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_Thrust>(def);

            if(data == null)
                return;

            const int wireDivideRatio = 12;
            const float lineHeight = 0.3f;
            var color = Color.Red;
            var colorFace = color * 0.5f;
            var capsuleMatrix = MatrixD.CreateWorld(Vector3D.Zero, drawMatrix.Up, drawMatrix.Backward); // capsule is rotated weirdly (pointing up), needs adjusting
            bool drawLabel = Settings.allLabels && TextAPIEnabled;

            foreach(var flame in data.Flames)
            {
                var start = Vector3D.Transform(flame.LocalFrom, drawMatrix);

                capsuleMatrix.Translation = start + (drawMatrix.Forward * (flame.Height * 0.5));
                MySimpleObjectDraw.DrawTransparentCapsule(ref capsuleMatrix, flame.Radius, flame.Height, ref colorFace, wireDivideRatio, MATERIAL_SQUARE);

                if(drawLabel)
                {
                    drawLabel = false; // label only on the first flame
                    var labelDir = drawMatrix.Down;
                    var labelLineStart = Vector3D.Transform(flame.LocalTo, drawMatrix) + labelDir * flame.Radius;
                    DrawLineLabel(TextAPIMsgIds.THRUST_DAMAGE, labelLineStart, labelDir, "Thrust damage", color, lineHeight: lineHeight, underlineLength: 1.1f);
                }
            }
        }

        private void DrawOverlay_LandingGear(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_LandingGear>(def);

            if(data == null)
                return;

            var color = new Color(20, 255, 155);
            var colorFace = color * 0.5f;
            bool drawLabel = Settings.allLabels && TextAPIEnabled;

            foreach(var obb in data.Magents)
            {
                var localBB = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
                var m = MatrixD.CreateFromQuaternion(obb.Orientation);
                m.Translation = obb.Center;
                m *= drawMatrix;

                MySimpleObjectDraw.DrawTransparentBox(ref m, ref localBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE);

                if(drawLabel)
                {
                    drawLabel = false; // only label the first one
                    var labelDir = drawMatrix.Down;
                    var labelLineStart = m.Translation + (m.Down * localBB.HalfExtents.Y) + (m.Backward * localBB.HalfExtents.Z) + (m.Left * localBB.HalfExtents.X);
                    DrawLineLabel(TextAPIMsgIds.MAGNET, labelLineStart, labelDir, "Magnet", color, lineHeight: 0.5f, underlineLength: 0.7f);
                }
            }
        }

        private void DrawOverlay_Collector(MyCubeBlockDefinition def, MatrixD drawMatrix)
        {
            var data = BData_Base.TryGetDataCached<BData_Collector>(def);

            if(data == null)
                return;

            var color = new Color(20, 255, 100);
            var colorFace = color * 0.5f;
            bool drawLabel = Settings.allLabels && TextAPIEnabled;

            var localBB = new BoundingBoxD(-Vector3.Half, Vector3.Half);
            var m = data.boxLocalMatrix * drawMatrix;

            MySimpleObjectDraw.DrawTransparentBox(ref m, ref localBB, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE);

            if(drawLabel)
            {
                var labelDir = drawMatrix.Down;
                var labelLineStart = m.Translation + (m.Down * localBB.HalfExtents.Y) + (m.Backward * localBB.HalfExtents.Z) + (m.Left * localBB.HalfExtents.X);
                DrawLineLabel(TextAPIMsgIds.COLLECTOR, labelLineStart, labelDir, "Collection Area", color, lineHeight: 0.5f, underlineLength: 0.7f);
            }
        }
        #endregion

        #region Draw helpers
        private void DrawLineLabelAlternate(TextAPIMsgIds id, Vector3D start, Vector3D end, string text, Color color, bool constantTextUpdate = false, float lineThick = 0.005f, float underlineLength = 0.75f)
        {
            var cm = MyAPIGateway.Session.Camera.WorldMatrix;
            var direction = (end - start);

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, start, direction, 1f, lineThick, LABELS_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, LABEL_SHADOW_COLOR, start + cm.Right * LABEL_SHADOW_OFFSET + cm.Down * LABEL_SHADOW_OFFSET + cm.Forward * LABEL_SHADOW_OFFSET_Z, direction, 1f, lineThick, LABELS_BLEND_TYPE);

            if(!Settings.allLabels || (!Settings.axisLabels && (int)id < 3))
                return;

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, end, cm.Right, underlineLength, lineThick, LABELS_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, LABEL_SHADOW_COLOR, end + cm.Right * LABEL_SHADOW_OFFSET + cm.Down * LABEL_SHADOW_OFFSET + cm.Forward * LABEL_SHADOW_OFFSET_Z, cm.Right, underlineLength, lineThick, LABELS_BLEND_TYPE);

            DrawSimpleLabel(id, end, text, color, constantTextUpdate);
        }

        private void DrawLineLabel(TextAPIMsgIds id, Vector3D start, Vector3D direction, string text, Color color, bool constantTextUpdate = false, float lineHeight = 0.3f, float lineThick = 0.005f, float underlineLength = 0.75f)
        {
            var cm = MyAPIGateway.Session.Camera.WorldMatrix;
            var end = start + direction * lineHeight;

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, start, direction, lineHeight, lineThick, LABELS_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, LABEL_SHADOW_COLOR, start + cm.Right * LABEL_SHADOW_OFFSET + cm.Down * LABEL_SHADOW_OFFSET + cm.Forward * LABEL_SHADOW_OFFSET_Z, direction, lineHeight, lineThick, LABELS_BLEND_TYPE);

            if(!Settings.allLabels || (!Settings.axisLabels && (int)id < 3))
                return;

            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, color, end, cm.Right, underlineLength, lineThick, LABELS_BLEND_TYPE);
            MyTransparentGeometry.AddLineBillboard(MATERIAL_SQUARE, LABEL_SHADOW_COLOR, end + cm.Right * LABEL_SHADOW_OFFSET + cm.Down * LABEL_SHADOW_OFFSET + cm.Forward * LABEL_SHADOW_OFFSET_Z, cm.Right, underlineLength, lineThick, LABELS_BLEND_TYPE);

            DrawSimpleLabel(id, end, text, color, constantTextUpdate);
        }

        private void DrawSimpleLabel(TextAPIMsgIds id, Vector3D worldPos, string text, Color textColor, bool updateText = false)
        {
            var i = (int)id;
            var camera = MyAPIGateway.Session.Camera;
            var msgObj = textAPILabels[i];
            HudAPIv2.SpaceMessage shadowObj = textAPIShadows[i];

            if(msgObj == null)
            {
                textAPILabels[i] = msgObj = new HudAPIv2.SpaceMessage(new StringBuilder(), worldPos, Vector3D.Up, Vector3D.Left, 0.1, Blend: LABELS_BLEND_TYPE);
                msgObj.Offset = new Vector2D(0.1, 0.1);

                textAPIShadows[i] = shadowObj = new HudAPIv2.SpaceMessage(new StringBuilder(), worldPos, Vector3D.Up, Vector3D.Left, 0.1, Blend: LABELS_BLEND_TYPE);
                shadowObj.Offset = msgObj.Offset + new Vector2D(LABEL_SHADOW_OFFSET, -LABEL_SHADOW_OFFSET);

                updateText = true;
            }

            if(updateText)
            {
                msgObj.Message.Clear().Color(textColor).Append(text);
                shadowObj.Message.Clear().Color(LABEL_SHADOW_COLOR).Append(text);
            }

            msgObj.Visible = true;
            msgObj.WorldPosition = worldPos;
            msgObj.Left = camera.WorldMatrix.Left;
            msgObj.Up = camera.WorldMatrix.Up;

            shadowObj.Visible = true;
            shadowObj.WorldPosition = worldPos;
            shadowObj.Left = camera.WorldMatrix.Left;
            shadowObj.Up = camera.WorldMatrix.Up;
        }

        private void DrawMountPoint(MyCubeBlockDefinition.MountPoint mountPoint, float cubeSize, ref Vector3I center, ref MatrixD mainMatrix, ref Color colorFace, double minSize)
        {
            var startLocal = mountPoint.Start - center;
            var endLocal = mountPoint.End - center;

            var bb = new BoundingBoxD(Vector3.Min(startLocal, endLocal) * cubeSize, Vector3.Max(startLocal, endLocal) * cubeSize);
            var obb = new MyOrientedBoundingBoxD(bb, mainMatrix);

            var normalAxis = Base6Directions.GetAxis(Base6Directions.GetDirection(ref mountPoint.Normal));

            var m = MatrixD.CreateFromQuaternion(obb.Orientation);
            m.Right *= Math.Max(obb.HalfExtent.X * 2, (normalAxis == Base6Directions.Axis.LeftRight ? MOUNTPOINT_THICKNESS : 0));
            m.Up *= Math.Max(obb.HalfExtent.Y * 2, (normalAxis == Base6Directions.Axis.UpDown ? MOUNTPOINT_THICKNESS : 0));
            m.Forward *= Math.Max(obb.HalfExtent.Z * 2, (normalAxis == Base6Directions.Axis.ForwardBackward ? MOUNTPOINT_THICKNESS : 0));
            m.Translation = obb.Center;

            MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref colorFace, ref colorFace, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MATERIAL_SQUARE, onlyFrontFaces: true, blendType: MOUNTPOINT_BLEND_TYPE);

            //var colorWire = colorFace * 4;
            //MySimpleObjectDraw.DrawTransparentBox(ref m, ref unitBB, ref colorWire, MySimpleObjectRasterizer.Wireframe, 1, lineWidth: 0.005f, lineMaterial: MATERIAL_SQUARE, onlyFrontFaces: true);
        }

        private void DrawMountPointAxixText(MyCubeBlockDefinition def, float gridSize, ref MatrixD drawMatrix)
        {
            var matrix = MatrixD.CreateScale(def.Size * gridSize);
            matrix.Translation = (def.Center - (def.Size * 0.5f));
            matrix = matrix * drawMatrix;

            DrawAxis(TextAPIMsgIds.AXIS_Z, ref Vector3.Forward, Color.Blue, ref drawMatrix, ref matrix);
            DrawAxis(TextAPIMsgIds.AXIS_X, ref Vector3.Right, Color.Red, ref drawMatrix, ref matrix);
            DrawAxis(TextAPIMsgIds.AXIS_Y, ref Vector3.Up, Color.Lime, ref drawMatrix, ref matrix);
        }

        private void DrawAxis(TextAPIMsgIds id, ref Vector3 direction, Color color, ref MatrixD drawMatrix, ref MatrixD matrix)
        {
            var dir = Vector3D.TransformNormal(direction * 0.5f, matrix);
            var text = AXIS_LABELS[(int)id];
            DrawLineLabel(id, drawMatrix.Translation, dir, text, color, lineHeight: 1.5f, underlineLength: text.Length * 0.1f);
        }
        #endregion
    }
}
