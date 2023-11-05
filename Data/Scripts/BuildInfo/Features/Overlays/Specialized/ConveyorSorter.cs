using System.Collections.Generic;
using CoreSystems.Api;
using Sandbox.Definitions;
using VRage.Game.ModAPI;
using VRageMath;

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
            List<CoreSystemsDef.WeaponDefinition> wcDefs;
            if(Main.CoreSystemsAPIHandler.Weapons.TryGetValue(def.Id, out wcDefs))
            {
                DrawWeaponCoreWeapon(ref drawMatrix, drawInstance, def, block, wcDefs);
                return;
            }

            //MatrixD blockWorldMatrix = drawMatrix;
            //blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);
        }

        internal static void DrawWeaponCoreWeapon(ref MatrixD drawMatrix, OverlayDrawInstance drawInstance, MyCubeBlockDefinition def, IMySlimBlock block, List<CoreSystemsDef.WeaponDefinition> weaponDefs)
        {
            //MatrixD blockWorldMatrix = drawMatrix;
            //blockWorldMatrix.Translation = Vector3D.Transform(def.ModelOffset, blockWorldMatrix);
        }
    }
}
