using System;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class PlacementDistance : ClientComponent
    {
        public const float CUBEBUILDER_PLACE_MINRANGE = 1;
        public const float CUBEBUILDER_PLACE_MAXRANGE = 50;
        public const float CUBEBUILDER_PLACE_DIST_ADD = 5;
        public const float CUBEBUILDER_PLACE_MIN_SIZE = 2.5f;

        public PlacementDistance(Client mod) : base(mod)
        {
        }

        public override void RegisterComponent()
        {
            EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
        }

        public override void UnregisterComponent()
        {
            EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            SetFlag(UpdateFlags.UPDATE_INPUT, EquipmentMonitor.IsCubeBuilder);
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            if(def == null)
                return;

            if(EquipmentMonitor.IsCubeBuilder && !ShouldOverrideDistance()) // applies to creative too
            {
                MyCubeBuilder.IntersectionDistance = GetVanillaSurvivalDistance();
            }
        }

        private float GetVanillaSurvivalDistance()
        {
            return (MyAPIGateway.Session.ControlledObject is IMyShipController ? 12.5f : (EquipmentMonitor.BlockDef.CubeSize == MyCubeSize.Large ? 10 : 5));
        }

        private bool ShouldOverrideDistance()
        {
            // these are the same checks as the game for determining if ctrl+scroll should not work
            // EnableCopyPaste is used as SpaceMaster creative tools toggle check.
            bool survival = MyAPIGateway.Session.SurvivalMode && !MyAPIGateway.Session.EnableCopyPaste && !MyCubeBuilder.SpectatorIsBuilding;

            return (Config.AdjustBuildDistanceSurvival && survival) || (Config.AdjustBuildDistanceShipCreative && !survival);
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(inMenu || paused || !EquipmentMonitor.IsCubeBuilder || EquipmentMonitor.BlockDef == null)
                return;

            if(ShouldOverrideDistance())
            {
                float blockSizeMeters = Math.Max(EquipmentMonitor.BlockDef.Size.AbsMax() * EquipmentMonitor.BlockGridSize, CUBEBUILDER_PLACE_MIN_SIZE);

                // add some extra distance only if the block isn't huge
                float add = CUBEBUILDER_PLACE_DIST_ADD;
                float tooLarge = 3 * EquipmentMonitor.BlockGridSize;
                if(blockSizeMeters > tooLarge)
                    add = Math.Max(add - (blockSizeMeters - tooLarge), 0);

                float min = GetVanillaSurvivalDistance();
                float maxRange = MathHelper.Clamp(blockSizeMeters + add, min, CUBEBUILDER_PLACE_MAXRANGE);

                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();

                if(scroll != 0 && MyAPIGateway.Input.IsAnyCtrlKeyPressed())
                {
                    if(scroll > 0)
                        MyCubeBuilder.IntersectionDistance *= 1.1f; // consistent with how the game moves it
                    else
                        MyCubeBuilder.IntersectionDistance /= 1.1f;
                }

                if(MyCubeBuilder.IntersectionDistance < CUBEBUILDER_PLACE_MINRANGE)
                    MyCubeBuilder.IntersectionDistance = CUBEBUILDER_PLACE_MINRANGE;
                else if(MyCubeBuilder.IntersectionDistance > maxRange)
                    MyCubeBuilder.IntersectionDistance = maxRange;

                if(Config.Debug)
                    MyAPIGateway.Utilities.ShowNotification($"(DEBUG: Enabled IntersectionDistance set to {MyCubeBuilder.IntersectionDistance:0.##}; max={maxRange:0.##})", 17);
            }
            else if(Config.Debug)
                MyAPIGateway.Utilities.ShowNotification($"(DEBUG: no manual scroll adjust override)", 17);
        }
    }
}
