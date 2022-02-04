using System;
using System.Collections.Generic;
using System.IO;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_AdvancedDoor : BData_Base
    {
        public List<SubpartInfo> DoorSubparts;

        static readonly HashSet<string> ExistingSubparts = new HashSet<string>();

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            MyAdvancedDoorDefinition doorDef = def as MyAdvancedDoorDefinition;
            if(doorDef == null)
            {
                Log.Error($"Block '{def.Id.ToString()}' is not {nameof(MyAdvancedDoorDefinition)}, probably missing `<Definition xsi:type=\"MyObjectBuilder_AdvancedDoorDefinition\">` in its definition?");
                return base.IsValid(block, def) || false;
            }

            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            ExistingSubparts.Clear();

            // only first layer needed
            foreach(IMyModelDummy dummy in dummies.Values)
            {
                if(!dummy.Name.StartsWith("subpart_", StringComparison.OrdinalIgnoreCase))
                    continue;

                object fileNameObj;
                if(dummy.CustomData == null || !dummy.CustomData.TryGetValue("file", out fileNameObj))
                    continue;

                string fileNoExt = fileNameObj as string;
                if(fileNoExt == null)
                    continue;

                // MyEntitySubpart.GetSubpartFromDummy()
                ExistingSubparts.Add(dummy.Name.Substring("subpart_".Length));
            }

            foreach(MyObjectBuilder_AdvancedDoorDefinition.SubpartDefinition partDef in doorDef.Subparts)
            {
                if(ExistingSubparts.Contains(partDef.Name))
                    continue; // ignore subparts already in the model

                if(DoorSubparts == null)
                    DoorSubparts = new List<SubpartInfo>();

                Matrix localMatrix = Matrix.Identity;

                // HACK: MyAdvancedDoor.InitSubparts() also reads bones, those are inaccessible to modAPI.

                if(partDef.PivotPosition.HasValue)
                    localMatrix.Translation = partDef.PivotPosition.Value;

                // same way MyAdvancedDoor.LoadSubpartFromName() does it
                string modelName = Path.Combine(Path.GetDirectoryName(doorDef.Model), partDef.Name) + ".mwm";

                DoorSubparts.Add(new SubpartInfo(localMatrix, modelName, null));
            }

            DoorSubparts?.TrimExcess();

            dummies.Clear();
            return base.IsValid(block, def) || true;
        }
    }
}
