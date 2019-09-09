using System;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    /// <summary>
    /// Adds the ctrl+scroll block distance adjust to in-ship mode in creative and creative tools as well as survival (ship or character).
    /// It also overwrites the behavior for creative tools to allow maximum placement distance like in creative mode.
    /// </summary>
    public class PlacementDistance : ClientComponent
    {
        private const float PLACE_MINRANGE = 1;
        private const float PLACE_MAXRANGE = 50;
        private const float PLACE_DIST_ADD = 5;
        private const float PLACE_MIN_SIZE = 2.5f;
        private const float VANILLA_CREATIVE_MAXDIST = 100f;
        private const float VANILLA_SURVIVAL_CREATIVETOOLS_MAXDIST = 12.5f;

        private float VanillaSurvivalDistance => (InShip ? 12.5f : (EquipmentMonitor.BlockDef.CubeSize == MyCubeSize.Large ? 10 : 5));

        private bool InShip => MyAPIGateway.Session.ControlledObject is IMyShipController;
        private bool SurvivalCreativeTools => MyAPIGateway.Session.SurvivalMode && MyAPIGateway.Session.EnableCopyPaste;
        private bool CreativeGameMode => MyAPIGateway.Session.CreativeMode;

        public PlacementDistance(Client mod) : base(mod)
        {
        }

        public override void RegisterComponent()
        {
            EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
        }

        public override void UnregisterComponent()
        {
            EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
        }

        private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            SetFlag(UpdateFlags.UPDATE_INPUT, EquipmentMonitor.IsCubeBuilder);

            // reset survival distance if this feature is disabled
            if(EquipmentMonitor.IsCubeBuilder && !Config.AdjustBuildDistanceSurvival.Value && !CreativeGameMode && !SurvivalCreativeTools)
            {
                MyCubeBuilder.IntersectionDistance = VanillaSurvivalDistance;
            }
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(inMenu || paused || !EquipmentMonitor.IsCubeBuilder || EquipmentMonitor.BlockDef == null)
                return;

            float maxRange = 0f;
            bool inShip = InShip;

            if(CreativeGameMode)
            {
                if(!inShip)
                    return;

                if(!Config.AdjustBuildDistanceShipCreative.Value)
                    return;

                maxRange = VANILLA_CREATIVE_MAXDIST;
            }
            else if(SurvivalCreativeTools)
            {
                if(!Config.AdjustBuildDistanceShipCreative.Value)
                    return;

                maxRange = VANILLA_CREATIVE_MAXDIST;
            }
            else
            {
                if(!Config.AdjustBuildDistanceSurvival.Value)
                    return;

                float blockSizeMeters = Math.Max(EquipmentMonitor.BlockDef.Size.AbsMax() * EquipmentMonitor.BlockGridSize, PLACE_MIN_SIZE);

                // add some extra distance only if the block isn't huge
                float add = PLACE_DIST_ADD;
                float tooLarge = 3 * EquipmentMonitor.BlockGridSize;
                if(blockSizeMeters > tooLarge)
                    add = Math.Max(add - (blockSizeMeters - tooLarge), 0);

                maxRange = MathHelper.Clamp(blockSizeMeters + add, VanillaSurvivalDistance, PLACE_MAXRANGE);
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

            if(Config.Debug.Value)
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
