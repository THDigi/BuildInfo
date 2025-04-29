using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.Input;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Definitions.SessionComponents;
using VRage.Game.ModAPI;
using VRage.Input;
using VRageMath;
using InternalControllableEntity = Sandbox.Game.Entities.IMyControllableEntity;

namespace Digi.BuildInfo.Features
{
    /// <summary>
    /// Adds the ctrl+scroll block distance adjust for survival for character as well as in-ship mode for creative.
    /// It also overwrites the behavior for creative tools to allow maximum placement distance like in creative mode.
    /// </summary>
    public class PlacementDistance : ModComponent
    {
        const float MinRange = 0.1f;
        const float SurvivalMaxRange = 50f;
        const float CreativeMaxRange = 100f;

        float CurrentMaxRange = -1;
        float? SwapAtDistance = 15f;

        readonly UndoableEditToolset DefEdits = new UndoableEditToolset();

        public PlacementDistance(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.Config.AdjustBuildDistanceShipCreative.ValueAssigned += ConfigChanged;
            Main.Config.AdjustBuildDistanceSurvival.ValueAssigned += ConfigChanged;

            Main.EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;

            MyCubeBuilder.IntersectionDistance = 10f;
        }

        public override void UnregisterComponent()
        {
            DefEdits.UndoAll();

            MyCubeBuilder.IntersectionDistance = 10f;

            if(!Main.ComponentsRegistered)
                return;

            Main.Config.AdjustBuildDistanceShipCreative.ValueAssigned -= ConfigChanged;
            Main.Config.AdjustBuildDistanceSurvival.ValueAssigned -= ConfigChanged;

            Main.EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
        }

        void ConfigChanged(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void UpdateAfterSim(int tick)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

            EditDefinitions();
        }

        void EditDefinitions()
        {
            DefEdits.UndoAll();

            MyCubeBuilderDefinition def = MyCubeBuilder.CubeBuilderDefinition;
            if(def == null)
            {
                Log.Error($"MyCubeBuilder.CubeBuilderDefinition is null for some reason O.o - placement distance tweaks won't work properly");
                return;
            }

            if(Main.Config.AdjustBuildDistanceSurvival.Value)
            {
                //DefEdits.MakeEdit(def, (d, v) => d.DefaultBlockBuildingDistance = v, def.DefaultBlockBuildingDistance, 20f);
                DefEdits.MakeEdit(def, (d, v) => d.MinBlockBuildingDistance = v, def.MinBlockBuildingDistance, MinRange);
                //DefEdits.MakeEdit(def, (d, v) => d.MaxBlockBuildingDistance = v, def.MaxBlockBuildingDistance, MaxRangeVanillaCreative);
                DefEdits.MakeEdit(def, (d, v) => d.BuildingDistLargeSurvivalCharacter = v, def.BuildingDistLargeSurvivalCharacter, SurvivalMaxRange);
                DefEdits.MakeEdit(def, (d, v) => d.BuildingDistSmallSurvivalCharacter = v, def.BuildingDistSmallSurvivalCharacter, SurvivalMaxRange);
                DefEdits.MakeEdit(def, (d, v) => d.BuildingDistLargeSurvivalShip = v, def.BuildingDistLargeSurvivalShip, SurvivalMaxRange);
                DefEdits.MakeEdit(def, (d, v) => d.BuildingDistSmallSurvivalShip = v, def.BuildingDistSmallSurvivalShip, SurvivalMaxRange);
            }
            else
            {
                MyCubeSize size = Main.EquipmentMonitor?.BlockDef?.CubeSize ?? MyCubeSize.Large;
                MyCubeBuilder.IntersectionDistance = MathHelper.Clamp(MyCubeBuilder.IntersectionDistance, def.MinBlockBuildingDistance, VanillaIntersectionDistance(size));
            }

            ComputeNeedingUpdate();
        }

        void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            ComputeNeedingUpdate();
        }

        void ComputeNeedingUpdate()
        {
            CalculateMaxRange();

            bool update = Main.EquipmentMonitor.IsCubeBuilder && (Main.Config.AdjustBuildDistanceSurvival.Value || Main.Config.AdjustBuildDistanceShipCreative.Value);
            SetUpdateMethods(UpdateFlags.UPDATE_INPUT, update);
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            CalculateMaxRange();

            // game only assigns MyCubeBuilder.IntersectionDistance in survival on block swap
            if(MyAPIGateway.Session.SurvivalMode && CurrentMaxRange > 0)
            {
                // set distance depending on last distance
                // if it was scrolled farthest then SwapAtDistance is null and equipped block is farthest
                // otherwise, it'll maintain the closer distance
                if(SwapAtDistance.HasValue)
                {
                    MyCubeBuilder.IntersectionDistance = SwapAtDistance.Value;
                }
                else
                {
                    MyCubeBuilder.IntersectionDistance = CurrentMaxRange;
                }
            }
        }

        void CalculateMaxRange()
        {
            if(!Main.EquipmentMonitor.IsCubeBuilder || Main.EquipmentMonitor.BlockDef == null)
                return;

            CurrentMaxRange = -1;

            bool survival = !Utils.CreativeToolsEnabled;
            bool inShip = (MyAPIGateway.Session.ControlledObject is IMyCubeBlock);

            if(!survival && Main.Config.AdjustBuildDistanceShipCreative.Value)
            {
                if(inShip) // fix for no distance adjustment in cockpit build mode in creative mode/tools
                {
                    CurrentMaxRange = CreativeMaxRange;
                }
                else if(!MyAPIGateway.Session.CreativeMode) // fix for creative tools having shorter max range than creative mode
                {
                    CurrentMaxRange = CreativeMaxRange;
                }
            }

            // fix for survival (ship or not) having no distance adjust
            if(survival && Main.Config.AdjustBuildDistanceSurvival.Value)
            {
                Vector3 sizeM = Main.EquipmentMonitor.BlockDef.Size * Main.EquipmentMonitor.BlockGridSize;
                BoundingSphere encasingSphere = BoundingSphere.CreateFromBoundingBox(new BoundingBox(Vector3.Zero, sizeM));
                float diagonal = encasingSphere.Radius * 2;
                float add = (inShip ? 20f : 10f);
                CurrentMaxRange = MathHelper.Clamp(diagonal + add, MinRange, SurvivalMaxRange);
            }
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(inMenu || paused || !Main.EquipmentMonitor.IsCubeBuilder || Main.EquipmentMonitor.BlockDef == null)
                return;

            if(Main.Tick % 60 == 0)
            {
                CalculateMaxRange();
            }

            if(CurrentMaxRange < 0)
                return; // other cases (creative mode on foot) have distance adjust so those are ignored

            int move = 0;

            if(MyAPIGateway.Input.IsAnyCtrlKeyPressed())
            {
                move = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
            }

            if(move == 0)
            {
                // used by gamepad, has to be read this way
                var ctrl = MyAPIGateway.Session.ControlledObject as InternalControllableEntity;
                if(ctrl != null)
                {
                    if(MyAPIGateway.Input.IsControl(ctrl.ControlContext, GamepadControlIds.MOVE_FURTHER, MyControlStateType.PRESSED))
                        move = 1;
                    else if(MyAPIGateway.Input.IsControl(ctrl.ControlContext, GamepadControlIds.MOVE_CLOSER, MyControlStateType.PRESSED))
                        move = -1;
                }
            }

            if(move != 0)
            {
                if(move > 0)
                    MyCubeBuilder.IntersectionDistance *= 1.1f; // consistent with how the game moves it
                else
                    MyCubeBuilder.IntersectionDistance /= 1.1f;

                if(MyCubeBuilder.IntersectionDistance >= CurrentMaxRange)
                    SwapAtDistance = null;
                else
                    SwapAtDistance = MyCubeBuilder.IntersectionDistance;
            }

            MyCubeBuilder.IntersectionDistance = MathHelper.Clamp(MyCubeBuilder.IntersectionDistance, MinRange, CurrentMaxRange);

            if(Main.Config.Debug.Value)
                MyAPIGateway.Utilities.ShowNotification($"(DEBUG PlacementDistance: setDist={MyCubeBuilder.IntersectionDistance.ToString("0.##")}; max={CurrentMaxRange.ToString("0.##")})", 17);
        }

        static float VanillaIntersectionDistance(MyCubeSize size)
        {
            MyCubeBuilderDefinition def = MyCubeBuilder.CubeBuilderDefinition;

            if(MyAPIGateway.Session.SurvivalMode)
            {
                if(MyAPIGateway.Session.ControlledObject is IMyShipController)
                    return (float)(size == MyCubeSize.Small ? def.BuildingDistSmallSurvivalShip : def.BuildingDistLargeSurvivalShip);
                else
                    return (float)(size == MyCubeSize.Small ? def.BuildingDistSmallSurvivalCharacter : def.BuildingDistLargeSurvivalCharacter);
            }
            else
            {
                return def.MaxBlockBuildingDistance;
            }
        }
    }
}
