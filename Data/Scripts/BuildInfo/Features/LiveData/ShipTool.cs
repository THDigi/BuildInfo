using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.LiveData
{
    /// <summary>
    /// Welders and grinders only
    /// </summary>
    public class BData_ShipTool : BData_Base
    {
        /// <summary>
        /// Tool weld/grind sphere relative to block.
        /// <para>NOTE: the game's m_detectorSphere is relative to the grid!</para>
        /// </summary>
        public BoundingSphere SensorLocal;

        protected override bool IsValid(IMyCubeBlock block, MyCubeBlockDefinition def)
        {
            Dictionary<string, IMyModelDummy> dummies = BuildInfoMod.Instance.Caches.Dummies;
            dummies.Clear();
            block.Model.GetDummies(dummies);

            var toolDef = (MyShipToolDefinition)def;

            bool success = false;

            foreach(KeyValuePair<string, IMyModelDummy> kv in dummies)
            {
                // dummy name from Sandbox.Game.Weapons.MyShipToolBase.LoadDummies()
                if(kv.Key.ContainsIgnoreCase("detector_shiptool"))
                {
                    var m = kv.Value.Matrix;

                    // the game stores this as grid-relative, we're storing it block-relative instead because we need it for more purposes.
                    SensorLocal = new BoundingSphere(m.Translation + m.Forward * toolDef.SensorOffset, toolDef.SensorRadius);

                    success = true;
                    break;
                }
            }

            dummies.Clear();
            return base.IsValid(block, def) | success;
        }
    }
}