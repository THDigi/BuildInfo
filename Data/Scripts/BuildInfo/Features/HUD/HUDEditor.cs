using System;
using System.Collections.Generic;
using Digi.ComponentLib;
using Sandbox.Definitions;
using Sandbox.Definitions.GUI;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class HUDEditor : ModComponent
    {
        public const float VanillaToolbarTextScale = 0.46f;
        public const float CustomToolbarTextScale = 0.45f; // also the default for ToolbarStatusTextScaleOverride setting
        public const string SetFont = FontsHandler.Monospace;
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

            Main.GUIMonitor.ResolutionChanged += ResolutionChanged;

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

            Main.GUIMonitor.ResolutionChanged -= ResolutionChanged;

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

            ApplyHUDEdits = Constants.TicksPerSecond / 4;
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

        void ResolutionChanged()
        {
            ApplyHUDEdits = Constants.TicksPerSecond / 4;
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
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


            /* cloned code to follow it along
            
            Vector2 ToolbarItemSize = ItemSize - itemPadding.SizeChange;
            MyGuiBorderThickness itemPadding = new MyGuiBorderThickness(4f / MyGuiConstants.GUI_OPTIMAL_SIZE.X, 3f / MyGuiConstants.GUI_OPTIMAL_SIZE.Y);
            itemPadding.SizeChange is:
                SizeChange => new Vector2(HorizontalSum, VerticalSum);
                HorizontalSum => Left + Right;
                VerticalSum => Top + Bottom;

            ItemSize = m_styleDef.ItemTexture.SizeGui;
            SizeGui = m_sizePx / MyGuiConstants.GUI_OPTIMAL_SIZE;

            MyGuiControlToolbar.SetupToolbarStyle()
            Vector2 sizePx = new Vector2((float)texture.SizePx.X * m_style.ItemStyle.ItemTextureScale.X, (float)texture.SizePx.Y * m_style.ItemStyle.ItemTextureScale.Y);
            */

            // MyGuiConstants
            Vector2 GUI_OPTIMAL_SIZE = new Vector2(1600f, 1200f);
            Vector2 itemPaddingSizeChange = new Vector2(4f / GUI_OPTIMAL_SIZE.X, 3f / GUI_OPTIMAL_SIZE.Y) * 2; // *2 because of SizeChange above

            foreach(MyHudDefinition hudDef in MyDefinitionManager.Static.GetAllDefinitions<MyHudDefinition>())
            {
                if(hudDef.Id.SubtypeName == HudDefForRefresh
                || hudDef.Id.SubtypeName == "Base") // can't be used directly, game crashes
                    continue;

                // HACK: modifying hud definitions to use custom font and maintain a fixed width character amount

                if(hudDef.Toolbar?.ItemStyle != null)
                {
                    if(Main.Config.ToolbarStatusFontOverride.Value)
                    {
                        //Log.Info($"Editing HUD definition '{hudDef.Id.SubtypeName}' to change Toolbar.ItemStyle.FontNormal from '{hudDef.Toolbar.ItemStyle.FontNormal}' to '{SetFont}'.");

                        Edits.Add(DefinitionEdit.Create(hudDef, (d, v) => d.Toolbar.ItemStyle.FontNormal = v, hudDef.Toolbar.ItemStyle.FontNormal, SetFont));
                        Edits.Add(DefinitionEdit.Create(hudDef, (d, v) => d.Toolbar.ItemStyle.FontHighlight = v, hudDef.Toolbar.ItemStyle.FontHighlight, SetFont));
                    }

                    float scaleOverride = Main.Config.ToolbarStatusTextScaleOverride.Value;
                    if(scaleOverride > 0)
                    {
                        Vector2 textureScale = hudDef.Toolbar.ItemStyle.ItemTextureScale;

                        MyLanguagesEnum langEnum = MyAPIGateway.Session?.Config?.Language ?? MyLanguagesEnum.English;

                        MyTexts.MyLanguageDescription langData;
                        if(!MyTexts.Languages.TryGetValue(langEnum, out langData))
                            langData = MyTexts.Languages[MyLanguagesEnum.English];
                        float langScale = langData.GuiTextScale;

                        if(langScale > 1f)
                        {
                            float multiplier = langScale / 1f;
                            scaleOverride *= multiplier;

                            Log.Info($"Current language ({langData.Name}) has text scale of {langScale}, multiplying toolbar text scale of HUD definition '{hudDef.Id.SubtypeName}' to counteract.");
                        }

                        /*
                        Vector2 itemMargin = itemPaddingSizeChange;
                        if(hudDef.Toolbar.ItemStyle.Margin != null)
                        {
                            MyGuiOffset margin = hudDef.Toolbar.ItemStyle.Margin.Value;
                            itemMargin = new Vector2(margin.Left + margin.Right, margin.Top + margin.Botton);
                        }

                        // textures\gui\icons\hud 2017\toolbariteminactive.png is 86x87px
                        Vector2 sizePx = new Vector2(86 * textureScale.X, 87 * textureScale.Y);
                        Vector2 itemSize = sizePx / GUI_OPTIMAL_SIZE;
                        Vector2 toolbarItemSize = itemSize - itemMargin;

                        // MyGuiManager.DrawString()
                        float maxTextWidth = toolbarItemSize.X;
                        float maxWidthPx = Main.GUIMonitor.GetScreenSizeFromNormalizedSize(new Vector2(maxTextWidth, 0)).X;

                        float actualTextScale = VanillaToolbarTextScale * langScale * Main.GUIMonitor.SafeScreenScale;
                        string SampleText = new string('A', ToolbarStatusProcessor.MaxChars);
                        Vector2 measuredTextA = MyFontDefinition.MeasureStringRaw(SetFont, SampleText, actualTextScale, false);
                        Vector2 measuredTextB = MyFontDefinition.MeasureStringRaw(SetFont, SampleText, actualTextScale, true);

                        if(BuildInfoMod.IsDevMod)
                            Log.Info($"[DEV] {hudDef.Id} :: res={MyAPIGateway.Session.Camera.ViewportSize.X}x{MyAPIGateway.Session.Camera.ViewportSize.Y}; itemSize={toolbarItemSize}; maxWidthPx={maxWidthPx}; actualTextScale={actualTextScale}; measuredText={measuredTextA}; b={measuredTextB}; safeGUI={Main.GUIMonitor.SafeGUIRectangle}; SafeScreenScale={Main.GUIMonitor.SafeScreenScale};");
                        */

                        // Default :: res=1920x1080; itemSize={X:0.029625 Y:0.04875}; maxWidthPx=42.68962; actualTextScale=0.4600000; measuredText={X:65.78 Y:17.02}; b={X:51.20173 Y:13.248}; safeGUI={X:240 Y:0 Width:1440 Height:1080}; SafeScreenScale=1;
                        // Default :: res=1280x1024; itemSize={X:0.029625 Y:0.04875}; maxWidthPx=37.94962; actualTextScale=0.4361481; measuredText={X:62.36918 Y:16.13748}; b={X:48.54682 Y:12.56107}; safeGUI={X:0 Y:0 Width:1280 Height:1024}; SafeScreenScale=0.9481481;

                        // texture scale affects available characters width, tweaking text size to keep it consistent.
                        const float TextureScaleDefault = 0.7f;
                        if(Math.Abs(textureScale.X - TextureScaleDefault) > 0.0001f)
                        {
                            float multiplier = textureScale.X / TextureScaleDefault;
                            scaleOverride *= multiplier;

                            Log.Info($"HUD definition '{hudDef.Id.SubtypeName}' has Toolbar.ItemStype.TextureScale X={textureScale.X:0.######} (default {TextureScaleDefault:0.######}), because of this, multiplying TextScale by {multiplier:0.######} to maintain characters that can fit in the box.");
                        }

                        //Log.Info($"Editing HUD definition '{hudDef.Id.SubtypeName}' to change Toolbar.ItemStyle.TextScale '{hudDef.Toolbar.ItemStyle.TextScale}' to '{scaleOverride.ToString("0.######")}'.");

                        Edits.Add(DefinitionEdit.Create(hudDef, (d, v) => d.Toolbar.ItemStyle.TextScale = v, hudDef.Toolbar.ItemStyle.TextScale, scaleOverride));
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
