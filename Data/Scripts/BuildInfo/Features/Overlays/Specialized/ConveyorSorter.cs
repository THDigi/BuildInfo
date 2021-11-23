using System.Collections.Generic;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Api;

namespace Digi.BuildInfo.Features.Overlays.Specialized
{
    public class ConveyorSorter : SpecializedOverlayBase
    {
        public ConveyorSorter(SpecializedOverlays processor) : base(processor)
        {
            //Add(typeof(MyObjectBuilder_ConveyorSorter)); // also used by WeaponCore
        }

        public override void Draw(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block)
        {
            List<WcApiDef.WeaponDefinition> wcDefs;
            if(Main.WeaponCoreAPIHandler.Weapons.TryGetValue(def.Id, out wcDefs))
            {
                DrawWeaponCoreWeapon(ref drawMatrix, drawInstance, def, block, wcDefs);
                return;
            }
        }

        internal static void DrawWeaponCoreWeapon(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block, List<WcApiDef.WeaponDefinition> wcDefs)
        {
        }
    }
}
