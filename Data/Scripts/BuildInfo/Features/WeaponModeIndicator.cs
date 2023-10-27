using System;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.BuildInfo.Features
{
    public class WeaponModeIndicator : ModComponent
    {
        MyCockpit InCockpit;
        bool PrevModeSingle;

        HudAPIv2.BillBoardHUDMessage UI_IconBg;
        HudAPIv2.BillBoardHUDMessage UI_Icon;
        HudAPIv2.HUDMessage UI_Bind;
        BoxDragging Drag;

        bool SuppressNextSound = true;

        const float TextScale = 0.6f;
        const float BackgroundOpacityHoverMin = 0.8f;
        readonly Color BackgroundColor = Color.White;
        readonly Color BackgroundColorSelected = new Color(0, 255, 100);

        public WeaponModeIndicator(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Drag = new BoxDragging(MyMouseButtonsEnum.Left);
            Drag.BoxSelected += ShowOrUpdateIcon;
            Drag.BoxDeselected += ShowOrUpdateIcon;
            Drag.Dragging += (newPos) =>
            {
                int scroll = MyAPIGateway.Input.DeltaMouseScrollWheelValue();
                if(scroll != 0)
                {
                    ConfigLib.FloatSetting setting = Main.Config.WeaponModeIndicatorScale;
                    float scale = setting.Value + (scroll > 0 ? 0.05f : -0.05f);
                    scale = MathHelper.Clamp(scale, setting.Min, setting.Max);
                    setting.Value = (float)Math.Round(scale, 3);
                }

                Main.Config.WeaponModeIndicatorPosition.Value = newPos;
                ShowOrUpdateIcon();
            };
            Drag.FinishedDragging += (finalPos) =>
            {
                ShowOrUpdateIcon();

                Main.Config.Save();
                Main.ConfigMenuHandler.RefreshAll();
            };

            Main.EquipmentMonitor.ControlledChanged += ControlledChanged;
            Main.GUIMonitor.OptionsMenuClosed += OptionsMenuClosed;
            Main.TextAPI.Detected += TextAPI_Detected;
            Main.GameConfig.HudVisibleChanged += HudVisibleChanged;
            Main.GameConfig.HudStateChanged += HudStateChanged;
            Main.EquipmentMonitor.BlockChanged += EquipmentMonitor_BlockChanged;
            Main.Config.WeaponModeIndicatorScale.ValueAssigned += Config_ScaleChanged;
            Main.Config.WeaponModeIndicatorPosition.ValueAssigned += Config_PositionChanged;
            Main.Config.HudFontOverride.ValueAssigned += Config_FontOverrideChanged;
        }

        public override void UnregisterComponent()
        {
            Drag = null;

            if(!Main.ComponentsRegistered)
                return;

            Main.EquipmentMonitor.ControlledChanged -= ControlledChanged;
            Main.GUIMonitor.OptionsMenuClosed -= OptionsMenuClosed;
            Main.TextAPI.Detected -= TextAPI_Detected;
            Main.GameConfig.HudVisibleChanged -= HudVisibleChanged;
            Main.GameConfig.HudStateChanged -= HudStateChanged;
            Main.EquipmentMonitor.BlockChanged -= EquipmentMonitor_BlockChanged;
            Main.Config.WeaponModeIndicatorScale.ValueAssigned -= Config_ScaleChanged;
            Main.Config.WeaponModeIndicatorPosition.ValueAssigned -= Config_PositionChanged;
            Main.Config.HudFontOverride.ValueAssigned -= Config_FontOverrideChanged;
        }

        void TextAPI_Detected()
        {
            ControlledChanged(MyAPIGateway.Session.ControlledObject);
        }

        void ControlledChanged(IMyControllableEntity controlled)
        {
            InCockpit = controlled as MyCockpit;
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, InCockpit != null);

            SuppressNextSound = true;

            if(InCockpit != null)
            {
                ShowOrUpdateIcon();
            }
            else
            {
                HideIcon();
            }
        }

        void OptionsMenuClosed()
        {
            RefreshIconIfVisible();
        }

        void HudVisibleChanged()
        {
            RefreshIconIfVisible();
        }

        void HudStateChanged(HudStateChangedInfo info)
        {
            RefreshIconIfVisible();
        }

        void EquipmentMonitor_BlockChanged(MyCubeBlockDefinition def, IMySlimBlock slimBlock)
        {
            RefreshIconIfVisible();
        }

        void Config_ScaleChanged(float oldValue, float newValue, ConfigLib.SettingBase<float> setting)
        {
            RefreshIconIfVisible();
        }

        void Config_PositionChanged(Vector2D oldValue, Vector2D newValue, ConfigLib.SettingBase<Vector2D> setting)
        {
            RefreshIconIfVisible();
        }

        void Config_FontOverrideChanged(bool oldValue, bool newValue, ConfigLib.SettingBase<bool> setting)
        {
            RefreshIconIfVisible();
        }

        void RefreshIconIfVisible()
        {
            if(InCockpit != null)
            {
                ShowOrUpdateIcon();
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            if(InCockpit == null || InCockpit.MarkedForClose)
            {
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                return;
            }

            bool singleWeaponMode = InCockpit.SingleWeaponMode;
            if(PrevModeSingle != singleWeaponMode)
            {
                PrevModeSingle = singleWeaponMode;

                ShowOrUpdateIcon();

                if(!SuppressNextSound)
                    Main.HUDSounds.PlayClick();
            }

            SuppressNextSound = false;

            if(UI_IconBg != null)
            {
                if(Main.TextAPI.InModMenu)
                {
                    Vector2D center = UI_IconBg.Origin;
                    Vector2D halfSize = new Vector2D(UI_IconBg.Width, UI_IconBg.Height) / 2;
                    Drag.DragHitbox = new BoundingBox2D(center - halfSize, center + halfSize);
                    Drag.Position = center;
                    Drag.Update();
                }
                else
                {
                    if(Drag.Hovered)
                        Drag.Unhover();
                }
            }
        }

        void HideIcon()
        {
            if(UI_IconBg != null)
            {
                UI_IconBg.Visible = false;
                UI_Icon.Visible = false;
                UI_Bind.Visible = false;
            }
        }

        void ShowOrUpdateIcon()
        {
            float scale = Main.Config.WeaponModeIndicatorScale.Value;
            if(scale <= 0 || !Main.GameConfig.IsHudVisible || !Main.TextAPI.IsEnabled || (Main.Config.CockpitBuildHideRightHud.Value && MyCubeBuilder.Static.IsActivated))
            {
                HideIcon();
                return;
            }

            if(UI_IconBg == null)
            {
                UI_IconBg = TextAPI.CreateHUDTexture(MyStringId.GetOrCompute("BuildInfo_UI_HudHotkeyBackground"), Color.White, Vector2D.Zero, hideWithHud: true);
                UI_Icon = TextAPI.CreateHUDTexture(MyStringId.GetOrCompute("BuildInfo_UI_HudWeaponModeAll"), Color.White, Vector2D.Zero, hideWithHud: true);
                UI_Bind = TextAPI.CreateHUDText(new StringBuilder(32), Vector2D.Zero, hideWithHud: true);
            }

            bool showBind = Main.GameConfig.HudState == HudState.HINTS && !MyAPIGateway.Input.IsJoystickLastUsed;

            Vector2 pxSize = (Vector2)HudAPIv2.APIinfo.ScreenPositionOnePX;

            Color hudColor = (Drag.Hovered ? BackgroundColorSelected : BackgroundColor);
            float hudOpacity = (Drag.Hovered ? Math.Max(Main.GameConfig.HudBackgroundOpacity, BackgroundOpacityHoverMin) : Main.GameConfig.HudBackgroundOpacity);
            Utils.FadeColorHUD(ref hudColor, hudOpacity);

            UI_IconBg.Origin = Main.Config.WeaponModeIndicatorPosition.Value;
            UI_IconBg.Scale = scale;
            UI_IconBg.Width = pxSize.X * 60;
            UI_IconBg.Height = pxSize.Y * 60;
            UI_IconBg.BillBoardColor = hudColor;

            UI_Icon.Material = MyStringId.GetOrCompute(PrevModeSingle ? "BuildInfo_UI_HudWeaponModeSingle" : "BuildInfo_UI_HudWeaponModeAll");
            UI_Icon.Offset = UI_IconBg.Origin; // "parent" it to the bg
            UI_Icon.Width = UI_IconBg.Width;
            UI_Icon.Height = UI_IconBg.Height;
            UI_Icon.Scale = scale;

            UI_IconBg.Visible = true;
            UI_Icon.Visible = true;
            UI_Bind.Visible = showBind;

            if(showBind)
            {
                UI_Bind.Offset = UI_IconBg.Origin; // "parent" it to the bg
                UI_Bind.Scale = scale * TextScale;
                UI_Bind.Font = (Main.Config.HudFontOverride.Value ? FontsHandler.TextAPI_OutlinedFont : FontsHandler.TextAPI_NormalFont);

                StringBuilder sb = UI_Bind.Message.Clear();

                IMyControl control = MyAPIGateway.Input.GetGameControl(MyControlsSpace.CUBE_COLOR_CHANGE);

                string bindKb = control.GetControlButtonName(MyGuiInputDeviceEnum.Keyboard);
                if(string.IsNullOrEmpty(bindKb))
                    bindKb = control.GetControlButtonName(MyGuiInputDeviceEnum.KeyboardSecond);

                string bindMouse = control.GetControlButtonName(MyGuiInputDeviceEnum.Mouse);

                if(!string.IsNullOrEmpty(bindKb) && !string.IsNullOrEmpty(bindMouse))
                    sb.Append(bindKb).Append('/').Append(bindMouse);
                else if(!string.IsNullOrEmpty(bindKb))
                    sb.Append(bindKb);
                else if(!string.IsNullOrEmpty(bindMouse))
                    sb.Append(bindMouse);
                else
                    sb.Append("(unk)");

                Vector2D textSize = UI_Bind.GetTextLength();

                UI_Bind.Origin = new Vector2D(
                    textSize.X / -2, // centered
                    pxSize.Y * 24 * scale); // px
            }
        }
    }
}
