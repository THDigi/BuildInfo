using System.Collections.Generic;
using Digi.BuildInfo.Features.LiveData;
using Sandbox.Definitions;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    // TODO: does parachute block also need this?

    public class AdvancedDoor : MultiSubpartBase
    {
        BData_AdvancedDoor Data;

        protected override bool Initialized()
        {
            bool baseReturn = base.Initialized();

            MyAdvancedDoorDefinition doorDef = BlockDef as MyAdvancedDoorDefinition;
            if(doorDef == null)
                return baseReturn;

            Data = Main.LiveDataHandler.Get<BData_AdvancedDoor>(BlockDef);
            if(Data == null || Data.DoorSubparts == null)
                return baseReturn;

            // expanding on MultiSubpartBase's code
            if(Parts == null)
                Parts = new List<PreviewEntityWrapper>(Data.DoorSubparts.Count);

            foreach(SubpartInfo info in Data.DoorSubparts)
            {
                PreviewEntityWrapper ent = new PreviewEntityWrapper(info.Model, info.LocalMatrix, BlockDef);
                Parts.Add(ent);
            }

            HasParts = true;
            return true;
        }
    }
}
