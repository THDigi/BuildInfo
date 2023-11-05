using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Digi.Input;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    public class HangarDoor : MultiSubpartBase
    {
        MyAirtightDoorGenericDefinition Def;
        float OpenRatio = 0f;

        protected override bool Initialized()
        {
            Def = (MyAirtightDoorGenericDefinition)BlockDef;
            OpenRatio = 0f;
            HasParts = false;

            BaseData = Main.LiveDataHandler.Get<BData_Base>(BlockDef);
            if(BaseData == null || BaseData.Subparts == null || BaseData.Subparts.Count == 0)
                return false;

            // removed hasLayeredParts check because this door needs to have its door bits duplicated to close

            Parts = new List<PreviewEntityWrapper>(BaseData.Subparts.Count);
            foreach(SubpartInfo info in BaseData.Subparts)
            {
                Parts.Add(new PreviewEntityWrapper(info.Model, info.LocalMatrix, info.Name, modelVisible: true));
            }

            HasParts = true;
            return true;
        }

        public override void Update(ref MatrixD blockWorldMatrix)
        {
            if(!HasParts)
                return;

            if(MyAPIGateway.Input.IsAnyShiftKeyPressed() && InputLib.IsInputReadable())
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    float perScroll = Def.OpeningSpeed; // 1f / Parts.Count;
                    if(scroll < 0)
                        OpenRatio += perScroll;
                    else if(scroll > 0)
                        OpenRatio -= perScroll;

                    OpenRatio = MathHelper.Clamp(OpenRatio, 0f, 1f);
                }
            }

            // math from MyAirtightHangarDoor.UpdateDoorPosition()
            float offset = (OpenRatio - 1f) * Parts.Count * Def.SubpartMovementDistance;
            float totalTravel = 0f;

            foreach(PreviewEntityWrapper part in Parts)
            {
                part.BaseModelVisible = (OpenRatio < 1f);

                totalTravel -= Def.SubpartMovementDistance;
                float move = (offset < totalTravel) ? totalTravel : offset;

                //Matrix local = Matrix.CreateTranslation(new Vector3(0f, move, 0f)) * part.LocalMatrix.Value;
                Matrix local = part.LocalMatrix.Value;
                local.Translation += local.Up * move;

                MatrixD relativeMatrix = local * blockWorldMatrix;
                part.Update(ref relativeMatrix);
            }
        }
    }
}