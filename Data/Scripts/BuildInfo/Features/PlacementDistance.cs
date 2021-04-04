using System;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    /// <summary>
    /// Adds the ctrl+scroll block distance adjust to in-ship mode in creative and creative tools as well as survival (ship or character).
    /// It also overwrites the behavior for creative tools to allow maximum placement distance like in creative mode.
    /// </summary>
    public class PlacementDistance : ModComponent
    {
        private const float PLACE_MINRANGE = 1;
        private const float PLACE_MAXRANGE = 50;
        private const float PLACE_DIST_ADD = 5;
        private const float PLACE_MIN_SIZE = 2.5f;
        private const float VANILLA_CREATIVE_MAXDIST = 100f;
        private const float VANILLA_SURVIVAL_CREATIVETOOLS_MAXDIST = 12.5f;

        private float VanillaSurvivalDistance()
        {
            // HACK hardcoded: from Data/Game/SessionComponents.sbc

            var def = Main.EquipmentMonitor.BlockDef;
            if(def == null)
                return 20f;

            if(MyAPIGateway.Session.ControlledObject is IMyCubeBlock)
                return 12.5f;

            return (def.CubeSize == MyCubeSize.Large ? 10f : 5f);
        }

        public static void ResetDefaults()
        {
            if(!MyAPIGateway.Session.CreativeMode)
                MyCubeBuilder.IntersectionDistance = 10f;
        }

        public PlacementDistance(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
        }

        protected override void UnregisterComponent()
        {
            Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, Main.EquipmentMonitor.IsCubeBuilder);
        }

        private void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            // reset survival distance if this feature is disabled
            if(def != null && Main.EquipmentMonitor.IsCubeBuilder && !Main.Config.AdjustBuildDistanceSurvival.Value && !MyAPIGateway.Session.CreativeMode && !Utils.CreativeToolsEnabled)
            {
                MyCubeBuilder.IntersectionDistance = VanillaSurvivalDistance();
            }
        }

        protected override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(inMenu || paused || !Main.EquipmentMonitor.IsCubeBuilder || Main.EquipmentMonitor.BlockDef == null)
                return;

            float maxRange = 0f;
            bool inShip = (MyAPIGateway.Session.ControlledObject is IMyCubeBlock);

            if(MyAPIGateway.Session.CreativeMode)
            {
                if(!inShip)
                    return;

                if(!Main.Config.AdjustBuildDistanceShipCreative.Value)
                    return;

                maxRange = VANILLA_CREATIVE_MAXDIST;
            }
            else if(Utils.CreativeToolsEnabled)
            {
                if(!Main.Config.AdjustBuildDistanceShipCreative.Value)
                    return;

                maxRange = VANILLA_CREATIVE_MAXDIST;
            }
            else
            {
                if(!Main.Config.AdjustBuildDistanceSurvival.Value)
                    return;

                float blockSizeMeters = Math.Max(Main.EquipmentMonitor.BlockDef.Size.AbsMax() * Main.EquipmentMonitor.BlockGridSize, PLACE_MIN_SIZE);

                // add some extra distance only if the block isn't huge
                float add = PLACE_DIST_ADD;
                float tooLarge = 3 * Main.EquipmentMonitor.BlockGridSize;
                if(blockSizeMeters > tooLarge)
                    add = Math.Max(add - (blockSizeMeters - tooLarge), 0);

                maxRange = MathHelper.Clamp(blockSizeMeters + add, VanillaSurvivalDistance(), PLACE_MAXRANGE);
            }

            int move = GetDistanceAdjustInputValue();

            if(move != 0)
            {
                if(move > 0)
                    MyCubeBuilder.IntersectionDistance *= 1.1f; // consistent with how the game moves it
                else
                    MyCubeBuilder.IntersectionDistance /= 1.1f;
            }

            MyCubeBuilder.IntersectionDistance = MathHelper.Clamp(MyCubeBuilder.IntersectionDistance, PLACE_MINRANGE, maxRange);

            if(Main.Config.Debug.Value)
                MyAPIGateway.Utilities.ShowNotification($"(DEBUG PlacementDistance: setDist={MyCubeBuilder.IntersectionDistance.ToString("0.##")}; max={maxRange.ToString("0.##")})", 17);
        }

        private int GetDistanceAdjustInputValue()
        {
            int move = 0;

            if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
            {
                move = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
            }

            if(move == 0)
            {
                if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.MOVE_FURTHER))
                    move = 1;
                else if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.MOVE_CLOSER))
                    move = -1;
            }

            return move;
        }
    }
}
