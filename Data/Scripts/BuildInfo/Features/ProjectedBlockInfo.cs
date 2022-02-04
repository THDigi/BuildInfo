using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Digi.ConfigLib;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.Gui;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    // FIXME: ship welder/grinder needs to select projected blocks too (all states).
    public class ProjectedBlockInfo : ModComponent
    {
        readonly MyStringId MaterialGizmoRedLine = MyStringId.GetOrCompute("GizmoDrawLineRed");

        public ProjectedBlockInfo(BuildInfoMod main) : base(main)
        {
            UpdateOrder = -50; /// over overlays but before <see cref="GameBlockInfoHandler.UpdateDraw"/>
        }

        public override void RegisterComponent()
        {
            Main.EquipmentMonitor.BlockChanged += BlockChanged;
            Main.EquipmentMonitor.ToolChanged += ToolChanged;
            Main.Config.SelectAllProjectedBlocks.ValueAssigned += ConfigValueSet;

            //Main.GameBlockInfoHandler.RegisterHudChangedEvent(HudInfoChanged, 25);
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.BlockChanged -= BlockChanged;
            Main.Config.SelectAllProjectedBlocks.ValueAssigned -= ConfigValueSet;
        }

        void ConfigValueSet(bool oldValue, bool newValue, SettingBase<bool> setting)
        {
            ComputeNeedingUpdate();
            Main.GameBlockInfoHandler.ForceResetBlockInfo();
        }

        void BlockChanged(MyCubeBlockDefinition def, IMySlimBlock block)
        {
            ComputeNeedingUpdate();
        }

        void ComputeNeedingUpdate()
        {
            if(!Main.Config.SelectAllProjectedBlocks.Value)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);
                return;
            }

            EquipmentMonitor eq = Main.EquipmentMonitor;
            bool aimingAtProjected = (eq.AimedBlock != null && eq.AimedProjectedBy != null);
            bool aimedProjectedBuildable = (eq.IsAnyWelder && eq.AimedProjectedCanBuild == BuildCheckResult.OK);
            bool update = aimingAtProjected && !aimedProjectedBuildable;

            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, update);

            if(!update)
                Main.GameBlockInfoHandler.ForceResetBlockInfo();
        }

        //void HudInfoChanged(MyHudBlockInfo hud)
        //{
        //    EquipmentMonitor eq = Main.EquipmentMonitor;
        //
        //    if(eq.BlockDef != null
        //    && eq.AimedProjectedBy != null
        //    && eq.BlockDef.Id != hud.DefinitionId
        //    && (eq.IsAnyGrinder || eq.AimedProjectedCanBuild != BuildCheckResult.OK))
        //    {
        //        Main.GameBlockInfoHandler.SetHudInfoForBlock(eq.BlockDef);
        //
        //        MyAPIGateway.Utilities.ShowNotification($"{GetType().Name}: re-set info for block!", 3000, FontsHandler.YellowSh); // DEBUG notify
        //    }
        //}

        void ToolChanged(MyDefinitionId toolDefId)
        {
            Main.GameBlockInfoHandler.RedrawBlockInfo();
        }

        public override void UpdateDraw()
        {
            EquipmentMonitor eq = Main.EquipmentMonitor;

            if(Main.Config.SelectAllProjectedBlocks.Value)
            {
                MyHudBlockInfo hud = MyHud.BlockInfo;

                if(eq.BlockDef == null)
                {
                    // not necessary, also breaks hud when swapping grinder/welder while aiming at a buildable projected block
                    //IMyHandheldGunObject<MyToolBase> tool = eq.HandTool as IMyHandheldGunObject<MyToolBase>;
                    //MyPhysicalItemDefinition toolDef = tool?.PhysicalItemDefinition;
                    //if(toolDef != null && hud.DefinitionId != toolDef.Id)
                    //{
                    //    Main.GameBlockInfoHandler.SetHudInfoForTool(toolDef);
                    //}
                }
                else if(hud.DefinitionId != eq.BlockDef.Id && eq.AimedProjectedBy != null && (eq.IsAnyGrinder || eq.AimedProjectedCanBuild != BuildCheckResult.OK))
                {
                    Main.GameBlockInfoHandler.SetHudInfoForBlock(eq.BlockDef);
                }

                // FIXME: find a way to remove misleading MissingComponentIndex=0 on status=Ok projected blocks
                //if(hud.MissingComponentIndex >= 0 && eq.IsAnyWelder && eq.AimedProjectedBy != null)
                //{
                //    hud.MissingComponentIndex = -1;
                //
                //    // mark first component red if it's not in inventory
                //    if(eq.AimedProjectedCanBuild == BuildCheckResult.OK && !Utils.CreativeToolsEnabled)
                //    {
                //        MyHudBlockInfo.ComponentInfo firstComp = hud.Components[0];
                //        IMyCharacter chr = MyAPIGateway.Session.ControlledObject as IMyCharacter;
                //        if(chr != null && chr.HasInventory)
                //        {
                //            IMyInventory inv = chr.GetInventory(0);
                //            if(inv.GetItemAmount(firstComp.DefinitionId) <= 0)
                //            {
                //                hud.MissingComponentIndex = 0;
                //            }
                //        }
                //    }
                //
                //    Main.GameBlockInfoHandler.RedrawBlockInfo();
                //}
            }

            #region draw the vanilla-looking selection if buildinfo's fancy selection box is disabled
            if(!Main.Config.OverrideToolSelectionDraw.Value && Main.GameConfig.HudState != HudState.OFF && eq.BlockDef != null)
            {
                IMySlimBlock aimedBlock = eq.AimedBlock;
                IMyProjector projector = eq.AimedProjectedBy;
                if(aimedBlock != null && projector != null)
                {
                    // HACK: MyWelder.FindProjectedBlock() only returns for Ok status.
                    // buildable blocks already have a selection box
                    if(!eq.IsAnyWelder || eq.AimedProjectedCanBuild != BuildCheckResult.OK)
                    {
                        MyCubeGrid grid = (MyCubeGrid)aimedBlock.CubeGrid;
                        MyCubeBuilder.DrawSemiTransparentBox(aimedBlock.Min, aimedBlock.Max, grid, Color.White, onlyWireframe: true, lineMaterial: MaterialGizmoRedLine);
                    }
                }
            }
            #endregion
        }
    }
}
