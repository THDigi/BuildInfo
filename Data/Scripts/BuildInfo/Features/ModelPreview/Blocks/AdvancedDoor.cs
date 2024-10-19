﻿using System.Collections.Generic;
using System.IO;
using Digi.BuildInfo.Features.LiveData;
using Sandbox.Definitions;

namespace Digi.BuildInfo.Features.ModelPreview.Blocks
{
    // TODO: does parachute block also need this?

    public class AdvancedDoor : MultiSubpartBase
    {
        BData_AdvancedDoor Data;
        bool HasSBCParts = false;

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
                PreviewEntityWrapper ent = new PreviewEntityWrapper(info.Model, info.LocalMatrix, info.Name);
                Parts.Add(ent);
            }

            HasSBCParts = true;
            HasParts = true;
            return true;
        }

        public override void SpawnConstructionModel(ConstructionModelPreview comp)
        {
            if(HasSBCParts)
            {
                MyCubeBlockDefinition.BuildProgressModel[] buildModels = BlockDef.BuildProgressModels;
                if(buildModels != null && buildModels.Length > 0)
                {
                    foreach(SubpartInfo info in Data.DoorSubparts)
                    {
                        ConstructionModelStack stack = new ConstructionModelStack();
                        comp.Stacks.Add(stack);

                        string subpartModelFileName = Path.GetFileName(info.Model);

                        for(int i = (buildModels.Length - 1); i >= 0; i--) // reverse order to start from the fully built stage and work backwards
                        {
                            // the game looks for subpart models in the same folder as the block's build stage model
                            string buildModelPath = Path.GetFullPath(Path.GetDirectoryName(buildModels[i].File));

                            stack.Models.Add(new PreviewEntityWrapper(Path.Combine(buildModelPath, subpartModelFileName), info.LocalMatrix));
                        }
                    }
                }
            }
        }
    }
}
