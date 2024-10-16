﻿using System;
using Digi.BuildInfo.Features.HUD;
using Digi.BuildInfo.Features.LiveData;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Definitions.GUI;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    public class HUDEditor : ModComponent
    {
        public const float VanillaToolbarTextScale = 0.46f;
        public const float CustomToolbarTextScale = 0.45f; // also the default for ToolbarStatusTextScaleOverride setting
        public const string SetFont = FontsHandler.BI_Monospace;

        readonly UndoableEditToolset DefEdits = new UndoableEditToolset();

        int ApplyHUDEditsInTicks = 0;
        const int CanReloadAfterThisTick = Constants.TicksPerSecond * 2;

        RefreshHudHandler RefreshHudHandler;

        public HUDEditor(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.GUIMonitor.ResolutionChanged += ResolutionChanged;

            Main.Config.ToolbarStatusFontOverride.ValueAssigned += ConfigValueChanged_Bool;
            Main.Config.ToolbarStatusTextScaleOverride.ValueAssigned += ConfigValueChanged_Float;
            Main.Config.HudFontOverride.ValueAssigned += ConfigValueChanged_Bool;
            Main.Config.MassOverride.ValueAssigned += ConfigValueChanged_Enum;

            RefreshHudHandler = new RefreshHudHandler();

            EditHUDDefinitions();
        }

        public override void UnregisterComponent()
        {
            DefEdits.UndoAll();

            if(!Main.ComponentsRegistered)
                return;

            Main.GUIMonitor.ResolutionChanged -= ResolutionChanged;

            Main.Config.ToolbarStatusFontOverride.ValueAssigned -= ConfigValueChanged_Bool;
            Main.Config.ToolbarStatusTextScaleOverride.ValueAssigned -= ConfigValueChanged_Float;
            Main.Config.HudFontOverride.ValueAssigned -= ConfigValueChanged_Bool;
            Main.Config.MassOverride.ValueAssigned -= ConfigValueChanged_Enum;

            RefreshHudHandler?.Dispose();
        }

        void ResolutionChanged()
        {
            ReEditHUD();
        }

        void ConfigValueChanged_Float(float oldValue, float newValue, ConfigLib.SettingBase<float> setting)
        {
            if(oldValue != newValue)
                ReEditHUD();
        }

        void ConfigValueChanged_Bool(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            if(oldValue != newValue)
                ReEditHUD();
        }

        void ConfigValueChanged_Enum(int oldValue, int newValue, ConfigLib.SettingBase<int> setting)
        {
            if(oldValue != newValue)
                ReEditHUD();
        }

        void ReEditHUD()
        {
            if(Main.Tick < CanReloadAfterThisTick)
                return;

            ApplyHUDEditsInTicks = Constants.TicksPerSecond / 4;
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public override void UpdateAfterSim(int tick)
        {
            if(ApplyHUDEditsInTicks > 0 && --ApplyHUDEditsInTicks == 0)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                EditHUDDefinitions();
            }
        }

        void EditHUDDefinitions()
        {
            DefEdits.UndoAll();

            foreach(MyHudDefinition hudDef in MyDefinitionManager.Static.GetAllDefinitions<MyHudDefinition>())
            {
                if(hudDef.Id.SubtypeName == RefreshHudHandler.HudDefIdForRefresh
                || hudDef.Id.SubtypeName == "Base") // can't be used directly, game crashes
                    continue;

                ModifyToolbarStyle(hudDef);

                // remove the hardcoded "kg" suffix from the HUD definition to allow HUD stat to add its own unit suffix
                var massFormat = Main.Config.MassOverride.ValueEnum;
                if(massFormat == Config.MassFormat.CustomMetric || massFormat == Config.MassFormat.CustomSI)
                {
                    ModifyMassFormat(hudDef);
                }

                if(Main.Config.HudFontOverride.Value)
                {
                    ModifyFonts(hudDef);
                }
            }

            // force reload if it's mid-game change
            if(Main.Tick >= CanReloadAfterThisTick)
            {
                RefreshHudHandler.RefreshHUD();
            }
        }

        void ModifyToolbarStyle(MyHudDefinition hudDef)
        {
            if(hudDef.Toolbar?.ItemStyle == null)
                return;

            // HACK: modifying hud definitions to use custom font and maintain a fixed width character amount

            if(Main.Config.ToolbarStatusFontOverride.Value)
            {
                //Log.Info($"Editing HUD definition '{hudDef.Id.SubtypeName}' to change Toolbar.ItemStyle.FontNormal from '{hudDef.Toolbar.ItemStyle.FontNormal}' to '{SetFont}'.");

                DefEdits.MakeEdit(hudDef, (d, v) => d.Toolbar.ItemStyle.FontNormal = v, hudDef.Toolbar.ItemStyle.FontNormal, SetFont);
                DefEdits.MakeEdit(hudDef, (d, v) => d.Toolbar.ItemStyle.FontHighlight = v, hudDef.Toolbar.ItemStyle.FontHighlight, SetFont);
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

                    Log.Info($"Current language ({langData.Name}) has text scale of {langScale.ToString()}, multiplying toolbar text scale of HUD definition '{hudDef.Id.SubtypeName}' to counteract.");
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
                //Vector2 GUI_OPTIMAL_SIZE = new Vector2(1600f, 1200f);
                //Vector2 itemPaddingSizeChange = new Vector2(4f / GUI_OPTIMAL_SIZE.X, 3f / GUI_OPTIMAL_SIZE.Y) * 2; // *2 because of SizeChange above

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

                DefEdits.MakeEdit(hudDef, (d, v) => d.Toolbar.ItemStyle.TextScale = v, hudDef.Toolbar.ItemStyle.TextScale, scaleOverride);
            }
        }

        void ModifyMassFormat(MyHudDefinition hudDef)
        {
            try
            {
                if(hudDef.StatControls == null)
                    return;

                ShipMassStat.ShowCustomSuffix = false;

                MyStringHash controlledMassId = MyStringHash.GetOrCompute("controlled_mass");
                const string Format = "{STAT}";

                foreach(MyObjectBuilder_StatControls control in hudDef.StatControls)
                {
                    foreach(MyObjectBuilder_StatVisualStyle style in control.StatStyles)
                    {
                        var textStyle = style as MyObjectBuilder_TextStatVisualStyle; // gets captured
                        if(textStyle != null && style.StatId == controlledMassId)
                        {
                            if(textStyle.Text != null && textStyle.Text.EndsWith("Kg"))
                            {
                                ShipMassStat.ShowCustomSuffix = true;

                                DefEdits.MakeEdit(hudDef, (d, v) => textStyle.Text = v, textStyle.Text, Format);

                                //DefEdits.MakeEdit(hudDef, (d, v) => textStyle.ColorMask = v, textStyle.ColorMask, null);
                                return;
                            }
                        }
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error($"Failed to modify mass format for {hudDef.Id.SubtypeName}\n{e}");
            }
        }

        void ModifyFonts(MyHudDefinition hudDef)
        {
            try
            {
                if(hudDef.StatControls != null)
                {
                    foreach(MyObjectBuilder_StatControls control in hudDef.StatControls)
                    {
                        ModifyFontsOf(hudDef, control.StatStyles);
                    }
                }

                if(hudDef.Toolbar?.StatControls != null)
                {
                    foreach(var control in hudDef.Toolbar.StatControls)
                    {
                        ModifyFontsOf(hudDef, control.StatStyles);
                    }
                }

                ModifyFontsOf(hudDef, hudDef.Crosshair?.StatStyles);
                ModifyFontsOf(hudDef, hudDef.TargetingMarkers?.StatStyles);
            }
            catch(Exception e)
            {
                Log.Error($"Failed to modify fonts for {hudDef.Id.SubtypeName}\n{e}");
            }
        }

        void ModifyFontsOf(MyHudDefinition hudDef, MyObjectBuilder_StatVisualStyle[] styles)
        {
            if(styles == null)
                return;

            foreach(MyObjectBuilder_StatVisualStyle style in styles)
            {
                var styleText = style as MyObjectBuilder_TextStatVisualStyle; // gets captured
                if(styleText != null)
                {
                    DefEdits.MakeEdit(hudDef, (d, v) => styleText.Font = v, styleText.Font, FontsHandler.BI_Monospace);
                }
            }
        }
    }

    // HACK: only way currently to force HUD to refresh is by tricking the game into thinking you entered a cockpit that has a different HUD definition
    class RefreshHudHandler
    {
        NotMyRealCockpit FakeCockpit;

        public const string HudDefIdForRefresh = "BI_HudForReload";
        static readonly MyDefinitionId FakeCockpitDefId = new MyDefinitionId(typeof(MyObjectBuilder_Cockpit), "BuildInfo_FakeCockpit");

        public RefreshHudHandler()
        {
            MyCockpitDefinition cockpitDef = null;

            foreach(MyCubeBlockDefinition def in BuildInfoMod.Instance.Caches.BlockDefs)
            {
                cockpitDef = def as MyCockpitDefinition;
                if(cockpitDef != null && cockpitDef.IsStandAlone)
                    break;
            }

            if(cockpitDef == null)
            {
                Log.Error("This world has no cockpit blocks? O.o HUD refreshing disabled.");
                return;
            }

            Log.Info($"Using '{cockpitDef.Id}' for HUD refreshing.");

            // using a custom object to override a method to prevent extra unwanted effects
            FakeCockpit = new NotMyRealCockpit();

            TempBlockSpawn.Spawn(cockpitDef, true, Spawned);
        }

        public void Dispose()
        {
        }

        void Spawned(IMySlimBlock slim)
        {
            FakeCockpit.SlimBlock = Utils.CastHax(FakeCockpit.SlimBlock, slim);

            var defId = FakeCockpitDefId;
            MyDefinitionManager.Static.Definitions.RemoveDefinition(ref defId);
        }

        public void RefreshHUD()
        {
            try
            {
                if(FakeCockpit?.SlimBlock == null)
                {
                    Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "Couldn't refresh HUD - no fake cockpit spawned (mod isuse, report to author)", FontsHandler.RedSh);
                    return;
                }

                string currentHud = null; // null is also a valid value for HUD which will use Default.

                IMyCharacter character = MyAPIGateway.Session?.Player?.Character;
                if(character != null)
                {
                    MyCockpit cockpit = character.Parent as MyCockpit;
                    if(cockpit != null)
                        currentHud = cockpit.BlockDefinition?.HUD;
                    else
                        currentHud = (character.Definition as MyCharacterDefinition)?.HUD;
                }
                else
                {
                    // while character is not really necessary, if local player is not yet spawned they also don't have a HUD anyway.
                    Utils.ShowColoredChatMessage(BuildInfoMod.ModName, "Couldn't refresh HUD - no character", FontsHandler.YellowSh);
                    return;
                }

                string originalDefHUD = FakeCockpit.BlockDefinition.HUD;

                try
                {
                    // OnAssumeControl() calls MyHud.SetHudDefinition(BlockDefinition.HUD) - which is prohibited so gotta do this hackery instead.
                    FakeCockpit.BlockDefinition.HUD = HudDefIdForRefresh;
                    FakeCockpit.OnAssumeControl(null);

                    // required to change back to the actual HUD in use
                    FakeCockpit.BlockDefinition.HUD = currentHud;
                    FakeCockpit.OnAssumeControl(null);
                }
                finally
                {
                    FakeCockpit.BlockDefinition.HUD = originalDefHUD;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        class NotMyRealCockpit : MyCockpit
        {
            protected override void UpdateCameraAfterChange(bool resetHeadLocalAngle = true)
            {
                // erase whatever vanilla code does here to make OnAssumeControl() result in only calling MyHud.SetHudDefinition().
            }
        }
    }
}
