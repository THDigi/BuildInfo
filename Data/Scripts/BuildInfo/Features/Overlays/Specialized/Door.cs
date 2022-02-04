using System.Collections.Generic;
using Digi.BuildInfo.VanillaData;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using DoorStatus = Sandbox.ModAPI.Ingame.DoorStatus;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class Door : SpecializedOverlayBase
    {
        bool DoorAirtightBlink = true;
        //int DoorAirtightBlinkTick = 0;

        static Color AirtightToggleColor = new Color(0, 255, 155) * OverlayDrawInstance.MountpointAlpha;

        public Door(SpecializedOverlays processor) : base(processor)
        {
            Add(typeof(MyObjectBuilder_Door));
            Add(typeof(MyObjectBuilder_AdvancedDoor));
            Add(typeof(MyObjectBuilder_AirtightDoorGeneric));
            Add(typeof(MyObjectBuilder_AirtightHangarDoor));
            Add(typeof(MyObjectBuilder_AirtightSlideDoor));
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            if(!drawInstance.BlockFunctionalForPressure)
                return;

            //if(block != null)
            //{
            //    doorAirtightBlink = true;
            //}
            //else if(!MyParticlesManager.Paused && ++doorAirtightBlinkTick >= 60)
            //{
            //    doorAirtightBlinkTick = 0;
            //    doorAirtightBlink = !doorAirtightBlink;
            //}

            Vector3 cubeSize = def.Size * drawInstance.CellSizeHalf;
            bool drawLabel = drawInstance.LabelRender.CanDrawLabel();

            //if(!drawLabel && !DoorAirtightBlink)
            //    return;

            bool fullyClosed = true;
            if(block != null)
            {
                IMyDoor door = block.FatBlock as IMyDoor;
                if(door != null)
                    fullyClosed = (door.Status == DoorStatus.Closed);
            }

            AirTightMode isAirTight = Pressurization.IsAirtightFromDefinition(def, 1f);
            if(isAirTight == AirTightMode.SEALED)
                return; // if block is entirely sealed anyway, don't bother with door specifics

            #region Draw sealed sides
            for(int i = 0; i < 6; ++i)
            {
                Base6Directions.Direction dir = OverlayDrawInstance.CycledDirections[i];
                Vector3I normalI = Base6Directions.GetIntVector(dir);

                if(Pressurization.IsDoorAirtightInternal(def, ref normalI, true))
                {
                    Vector3D dirForward = drawMatrix.GetDirectionVector(dir);
                    Vector3D dirLeft = drawMatrix.GetDirectionVector(OverlayDrawInstance.CycledDirections[((i + 4) % 6)]);
                    Vector3D dirUp = drawMatrix.GetDirectionVector(OverlayDrawInstance.CycledDirections[((i + 2) % 6)]);

                    Vector3D pos = drawMatrix.Translation + dirForward * cubeSize.GetDim((i % 6) / 2);
                    float width = cubeSize.GetDim(((i + 4) % 6) / 2);
                    float height = cubeSize.GetDim(((i + 2) % 6) / 2);

                    if(DoorAirtightBlink)
                    {
                        MatrixD m = MatrixD.CreateWorld(pos, dirForward, dirUp);
                        m.Right *= width * 2;
                        m.Up *= height * 2;
                        m.Forward *= OverlayDrawInstance.MountpointThickness;

                        if(fullyClosed)
                            MySimpleObjectDraw.DrawTransparentBox(ref m, ref OverlayDrawInstance.UnitBB, ref AirtightToggleColor, ref AirtightToggleColor, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MaterialSquare, onlyFrontFaces: true, blendType: OverlayDrawInstance.MountpointBlendType);
                        else
                            MySimpleObjectDraw.DrawTransparentBox(ref m, ref OverlayDrawInstance.UnitBB, ref AirtightToggleColor, ref AirtightToggleColor, MySimpleObjectRasterizer.Wireframe, 4, lineWidth: 0.01f, lineMaterial: MaterialLaser, onlyFrontFaces: true, blendType: OverlayDrawInstance.MountpointBlendType);
                    }

                    if(drawLabel) // only label the first one
                    {
                        drawLabel = false;

                        Vector3D labelPos = pos + dirLeft * width + dirUp * height;
                        drawInstance.LabelRender.DrawLineLabel(LabelType.AirtightWhenClosed, labelPos, dirLeft, AirtightToggleColor, "Airtight when closed");

                        if(!DoorAirtightBlink) // no need to iterate further if no faces need to be rendered
                            break;
                    }
                }
            }
            #endregion

            #region Find door-toggled airtight sides
            MyCubeBlockDefinition.MountPoint[] mountPoints = def.GetBuildProgressModelMountPoints(1f);
            if(mountPoints != null)
            {
                Vector3D half = Vector3D.One * -drawInstance.CellSizeHalf;
                Vector3D corner = (Vector3D)def.Size * -drawInstance.CellSizeHalf;
                MatrixD transformMatrix = MatrixD.CreateTranslation(corner - half) * drawMatrix;

                foreach(KeyValuePair<Vector3I, Dictionary<Vector3I, MyCubeBlockDefinition.MyCubePressurizationMark>> kv in def.IsCubePressurized) // precomputed: [position][normal] = airtight type
                {
                    foreach(KeyValuePair<Vector3I, MyCubeBlockDefinition.MyCubePressurizationMark> kv2 in kv.Value)
                    {
                        // only look for cell sides that are pressurized when doors are closed
                        if(kv2.Value != MyCubeBlockDefinition.MyCubePressurizationMark.PressurizedClosed)
                            continue;

                        Vector3D pos = Vector3D.Transform((Vector3D)(kv.Key * drawInstance.CellSize), transformMatrix);
                        Vector3 dirForward = Vector3.TransformNormal(kv2.Key, drawMatrix);

                        int dirIndex = (int)Base6Directions.GetDirection(kv2.Key);
                        Vector3 dirUp = drawMatrix.GetDirectionVector(OverlayDrawInstance.CycledDirections[((dirIndex + 2) % 6)]);

                        if(DoorAirtightBlink)
                        {
                            MatrixD m = MatrixD.Identity;
                            m.Translation = pos + dirForward * drawInstance.CellSizeHalf;
                            m.Forward = dirForward;
                            m.Backward = -dirForward;
                            m.Left = Vector3D.Cross(dirForward, dirUp);
                            m.Right = -m.Left;
                            m.Up = dirUp;
                            m.Down = -dirUp;
                            Vector3D scale = new Vector3D(drawInstance.CellSize, drawInstance.CellSize, OverlayDrawInstance.MountpointThickness);
                            MatrixD.Rescale(ref m, ref scale);

                            if(fullyClosed)
                                MySimpleObjectDraw.DrawTransparentBox(ref m, ref OverlayDrawInstance.UnitBB, ref AirtightToggleColor, ref AirtightToggleColor, MySimpleObjectRasterizer.Solid, 1, faceMaterial: MaterialSquare, onlyFrontFaces: true, blendType: OverlayDrawInstance.MountpointBlendType);
                            else
                                MySimpleObjectDraw.DrawTransparentBox(ref m, ref OverlayDrawInstance.UnitBB, ref AirtightToggleColor, ref AirtightToggleColor, MySimpleObjectRasterizer.Wireframe, 4, lineWidth: 0.01f, lineMaterial: MaterialLaser, onlyFrontFaces: true, blendType: OverlayDrawInstance.MountpointBlendType);
                        }

                        if(drawLabel) // only label the first one
                        {
                            drawLabel = false;

                            Vector3D dirLeft = drawMatrix.GetDirectionVector(OverlayDrawInstance.CycledDirections[((dirIndex + 4) % 6)]);
                            float width = cubeSize.GetDim(((dirIndex + 4) % 6) / 2);
                            float height = cubeSize.GetDim(((dirIndex + 2) % 6) / 2);

                            Vector3D labelPos = pos + dirLeft * width + dirUp * height;
                            drawInstance.LabelRender.DrawLineLabel(LabelType.AirtightWhenClosed, labelPos, dirLeft, AirtightToggleColor, "Airtight when closed");

                            if(!DoorAirtightBlink) // no need to iterate further if no faces need to be rendered
                                break;
                        }
                    }
                }
            }
            #endregion
        }
    }
}
