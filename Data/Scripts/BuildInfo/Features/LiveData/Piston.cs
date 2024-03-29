﻿using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Piston : BData_Base
    {
        public MyPistonBaseDefinition PistonDef;
        public MyCubeBlockDefinition TopDef;
        public Matrix TopLocalMatrix;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            bool success = false;

            PistonDef = def as MyPistonBaseDefinition;
            if(PistonDef != null)
            {
                MyCubeBlockDefinitionGroup blockPair = MyDefinitionManager.Static.TryGetDefinitionGroup(PistonDef.TopPart);
                if(blockPair != null)
                    TopDef = blockPair[def.CubeSize];
            }

            // from MyPistonBase.LoadSubparts() and MyPistonBase.GetTopGridMatrix()
            MyCubeBlock blockInternal = (MyCubeBlock)block;
            MyEntitySubpart subpart1 = blockInternal.Subparts?.GetValueOrDefault("PistonSubpart1");
            MyEntitySubpart subpart2 = subpart1?.Subparts?.GetValueOrDefault("PistonSubpart2");
            MyEntitySubpart subpart3 = subpart2?.Subparts?.GetValueOrDefault("PistonSubpart3");

            if(subpart3 != null)
            {
                IMyModelDummy topDummy = Utils.GetDummy((IMyModel)subpart3.Model, "TopBlock");
                Vector3 topLocalPos = (topDummy != null ? topDummy.Matrix.Translation : Vector3.Zero);

                MatrixD subpartWM = subpart3.WorldMatrix;
                subpartWM.Translation = Vector3D.Transform(topLocalPos, subpartWM);
                TopLocalMatrix = subpartWM * block.WorldMatrixInvScaled;
                success = true;
            }

            return base.IsValid(block, def) || success;
        }
    }
}
