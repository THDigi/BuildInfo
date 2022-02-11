using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Definitions.GUI;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Features
{
    public class HUDEditor : ModComponent
    {
        public const float VanillaToolbarTextScale = 0.46f;
        public const float CustomToolbarTextScale = 0.46f; // also the default for ToolbarStatusTextScale setting
        public const string SetFont = "BI_SEOutlined"; // also the default for ToolbarStatusCustomFont setting
        public const string HudDefForRefresh = "BI_HudForReload";

        readonly List<IDefinitionEdit> Edits = new List<IDefinitionEdit>();

        int ApplyHUDEdits = 0;
        bool RefreshHudOnNextCockpit = false;
        const int CanReloadAfterThisTick = Constants.TicksPerSecond * 2;

        public HUDEditor(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            EditHUDDefinitions();

            Main.Config.ToolbarStatusFontOverride.ValueAssigned += ToolbarStatusCustomFont_ValueAssigned;
            Main.Config.ToolbarStatusTextScaleOverride.ValueAssigned += ToolbarStatusTextScale_ValueAssigned;

            MyVisualScriptLogicProvider.PlayerEnteredCockpit += PlayerEnteredCockpit;
        }

        public override void UnregisterComponent()
        {
            foreach(IDefinitionEdit edit in Edits)
            {
                edit.Restore();
            }

            MyVisualScriptLogicProvider.PlayerEnteredCockpit -= PlayerEnteredCockpit;

            if(!Main.ComponentsRegistered)
                return;

            Main.Config.ToolbarStatusFontOverride.ValueAssigned -= ToolbarStatusCustomFont_ValueAssigned;
            Main.Config.ToolbarStatusTextScaleOverride.ValueAssigned -= ToolbarStatusTextScale_ValueAssigned;
        }

        void ToolbarStatusCustomFont_ValueAssigned(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            if(oldValue == newValue || Main.Tick < CanReloadAfterThisTick)
                return;

            ApplyHUDEdits = Constants.TicksPerSecond / 4;
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        void ToolbarStatusTextScale_ValueAssigned(float oldValue, float newValue, ConfigLib.SettingBase<float> setting)
        {
            if(oldValue == newValue || Main.Tick < CanReloadAfterThisTick)
                return;

            ApplyHUDEdits = Constants.TicksPerSecond * 1;
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public void PlayerEnteredCockpit(string entityName, long playerId, string gridName)
        {
            try
            {
                if(RefreshHudOnNextCockpit && MyAPIGateway.Session?.Player != null && MyAPIGateway.Session.Player.IdentityId == playerId)
                {
                    RefreshHudOnNextCockpit = false;

                    MyCockpit cockpit = MyAPIGateway.Session?.ControlledObject as MyCockpit;
                    if(cockpit == null)
                        cockpit = MyAPIGateway.Session?.Player?.Character?.Parent as MyCockpit;

                    if(cockpit == null)
                    {
                        long localIdentityId = MyAPIGateway.Session.Player.IdentityId;
                        Log.Error($"Player entered cockpit but game says he does not control any cockpits... entName={entityName}; playerId={playerId}; gridName={gridName}; localIdentityId={localIdentityId}");
                        return;
                    }

                    ForceHudRefresh(cockpit);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            if(ApplyHUDEdits > 0 && --ApplyHUDEdits == 0)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                EditHUDDefinitions();
            }
        }

        void EditHUDDefinitions()
        {
            if(Edits.Count > 0)
            {
                foreach(IDefinitionEdit edit in Edits)
                {
                    edit.Restore();
                }

                Edits.Clear();
            }

            foreach(MyHudDefinition hudDef in MyDefinitionManager.Static.GetAllDefinitions<MyHudDefinition>())
            {
                if(hudDef.Id.SubtypeName == HudDefForRefresh)
                    continue;

                if(hudDef?.Toolbar?.ItemStyle != null)
                {
                    if(Main.Config.ToolbarStatusTextScaleOverride.Value > 0)
                    {
                        Edits.Add(DefinitionEdit.Create(hudDef, (d, v) => d.Toolbar.ItemStyle.TextScale = v, hudDef.Toolbar.ItemStyle.TextScale, Main.Config.ToolbarStatusTextScaleOverride.Value));

                        //Log.Info($"Editing HUD definition '{hudDef.Id.SubtypeName}' to change toolbar status text scale to '{Main.Config.ToolbarStatusTextScale.Value.ToString("0.######")}'.");
                    }

                    if(Main.Config.ToolbarStatusFontOverride.Value)
                    {
                        Edits.Add(DefinitionEdit.Create(hudDef, (d, v) => d.Toolbar.ItemStyle.FontNormal = v, hudDef.Toolbar.ItemStyle.FontNormal, SetFont));
                        Edits.Add(DefinitionEdit.Create(hudDef, (d, v) => d.Toolbar.ItemStyle.FontHighlight = v, hudDef.Toolbar.ItemStyle.FontHighlight, SetFont));

                        //Log.Info($"Editing HUD definition '{hudDef.Id.SubtypeName}' to change toolbar status text font to '{SetFont}'.");
                    }
                }
            }

            // force reload if it's mid-game change
            if(Main.Tick >= CanReloadAfterThisTick)
            {
                MyCockpit cockpit = MyAPIGateway.Session?.ControlledObject as MyCockpit;
                if(cockpit == null)
                    cockpit = MyAPIGateway.Session?.Player?.Character?.Parent as MyCockpit;

                if(cockpit != null)
                {
                    ForceHudRefresh(cockpit);
                }
                else
                {
                    RefreshHudOnNextCockpit = true;
                }
            }
        }

        // HACK: HUD refresh hackery
        #region
        NotMyRealCockpit FakeCockpit;

        void ForceHudRefresh(MyCockpit realCockpit)
        {
            // using a custom object to override a method to prevent extra unwanted effects
            if(FakeCockpit == null)
                FakeCockpit = new NotMyRealCockpit();

            // need to borrow a cockpit's MySlimBlock to feed in the definition
            FakeCockpit.SlimBlock = realCockpit.SlimBlock;

            // OnAssumeControl() calls MyHud.SetHudDefinition(BlockDefinition.HUD);

            string cockpitOriginalHUD = FakeCockpit.BlockDefinition.HUD;

            FakeCockpit.BlockDefinition.HUD = HudDefForRefresh;
            FakeCockpit.OnAssumeControl(null);

            FakeCockpit.BlockDefinition.HUD = cockpitOriginalHUD;
            FakeCockpit.OnAssumeControl(null);

            FakeCockpit.SlimBlock = null;
        }

        class NotMyRealCockpit : MyCockpit
        {
            protected override void UpdateCameraAfterChange(bool resetHeadLocalAngle = true)
            {
                // override whatever vanilla code does here to reduce potential problems for calling OnAssumeControl()
            }
        }
        #endregion
    }
}
