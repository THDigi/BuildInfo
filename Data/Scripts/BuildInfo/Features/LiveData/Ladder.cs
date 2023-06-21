using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Features.LiveData
{
    public class BData_Ladder : BData_Base
    {
        public float DistanceBetweenPoles;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            // from MyLadder.OnModelChange()
            IMyModelDummy pole1, pole2;
            if(dummies.TryGetValue("pole_1", out pole1) && dummies.TryGetValue("pole_2", out pole2))
            {
                DistanceBetweenPoles = Math.Abs(pole1.Matrix.Translation.Y - pole2.Matrix.Translation.Y);
            }

            return base.IsValid(block, def) || true;
        }
    }
}