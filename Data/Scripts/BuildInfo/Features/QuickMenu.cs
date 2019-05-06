using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utils;
using Digi.ComponentLib;
using Digi.Input;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Input;

namespace Digi.BuildInfo.Features
{
    public class QuickMenu : ClientComponent
    {
        public const int MENU_TOTAL_ITEMS = 11;

        public bool Shown = false;
        public bool NeedsUpdate = true;
        public int SelectedItem = 0;

        private IMyHudNotification buildInfoNotification;
        private IMyHudNotification transparencyNotification;
        private IMyHudNotification freezeGizmoNotification;

        public QuickMenu(Client mod) : base(mod)
        {
            Flags = UpdateFlags.UPDATE_INPUT | UpdateFlags.UPDATE_AFTER_SIM;
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
            if(Shown && !EquipmentMonitor.IsCubeBuilder && !EquipmentMonitor.IsBuildTool)
                Shown = false;
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(EquipmentMonitor.IsBuildTool || EquipmentMonitor.IsCubeBuilder)
            {
                UpdateHotkeys();
            }

            if(!Shown)
                return;

            if(!anyKeyOrMouse || inMenu)
                return;

            if(MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Escape))
            {
                Shown = false;
                NeedsUpdate = true;
                return;
            }

            if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE))
            {
                NeedsUpdate = true;

                if(++SelectedItem >= MENU_TOTAL_ITEMS)
                    SelectedItem = 0;
            }

            if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE))
            {
                NeedsUpdate = true;

                if(--SelectedItem < 0)
                    SelectedItem = (MENU_TOTAL_ITEMS - 1);
            }

            if(MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.CUBE_ROTATE_VERTICAL_NEGATIVE) || MyAPIGateway.Input.IsNewGameControlPressed(MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE))
            {
                NeedsUpdate = true;

                switch(SelectedItem)
                {
                    case 0:
                        Shown = false;
                        break;
                    case 1:
                        if(EquipmentMonitor.AimedBlock != null)
                        {
                            Shown = false;
                            Mod.PickBlock.BlockDef = EquipmentMonitor.BlockDef;
                        }
                        else
                            MyAPIGateway.Utilities.ShowNotification("This only works with a hand or ship tool.", 3000, MyFontEnum.Red);
                        break;
                    case 2:
                        if(EquipmentMonitor.BlockDef == null)
                        {
                            MyAPIGateway.Utilities.ShowNotification("Equip/aim at a block that was added by a mod.", 3000, MyFontEnum.Red);
                        }
                        else if(EquipmentMonitor.BlockDef.Context.IsBaseGame)
                        {
                            MyAPIGateway.Utilities.ShowNotification($"{EquipmentMonitor.BlockDef.DisplayNameText} was not added by a mod.", 3000, MyFontEnum.Red);
                        }
                        else
                        {
                            Shown = false;
                            Mod.ChatCommands.ShowSelectedBlocksModWorkshop();
                        }
                        break;
                    case 3:
                        Shown = false;
                        Mod.ChatCommands.ShowHelp();
                        break;
                    case 4:
                        Shown = false;
                        Mod.ChatCommands.ShowBuildInfoWorkshop();
                        break;
                    case 5:
                        ToggleTextInfo();
                        break;
                    case 6:
                        Overlays.CycleOverlayMode(showNotification: false);
                        break;
                    case 7:
                        SetPlacementTransparency(!MyCubeBuilder.Static.UseTransparency, showNotification: false);
                        break;
                    case 8:
                        SetFreezePlacement(!MyAPIGateway.CubeBuilder.FreezeGizmo, showNotification: false);
                        break;
                    case 9:
                        ToggleTextAPI();
                        break;
                    case 10:
                        Shown = false;
                        ReloadConfig(Log.ModName);
                        break;
                }
            }
        }

        private void UpdateHotkeys()
        {
            var context = InputLib.GetCurrentInputContext();

            if(Config.ToggleTransparencyBind.Value.IsPressed(context))
            {
                if(Config.ToggleTransparencyBind.Value.IsJustPressed(context))
                {
                    NeedsUpdate = true;
                    SetPlacementTransparency(!MyCubeBuilder.Static.UseTransparency);
                }

                return;
            }

            if(Config.CycleOverlaysBind.Value.IsPressed(context))
            {
                if(Config.CycleOverlaysBind.Value.IsJustPressed(context))
                {
                    NeedsUpdate = true;
                    Overlays.CycleOverlayMode();
                }

                return;
            }

            if(Config.FreezePlacementBind.Value.IsPressed(context))
            {
                if(Config.FreezePlacementBind.Value.IsJustPressed(context))
                {
                    NeedsUpdate = true;
                    SetFreezePlacement(!MyAPIGateway.CubeBuilder.FreezeGizmo);
                }

                return;
            }

            if(Config.MenuBind.Value.IsPressed(context))
            {
                if(Config.MenuBind.Value.IsJustPressed(context))
                {
                    NeedsUpdate = true;
                    Shown = !Shown;
                }

                return;
            }
        }

        public void ToggleTextAPI()
        {
            if(TextAPI.WasDetected)
            {
                TextAPI.Use = !TextAPI.Use;

                TextGeneration.cache = null;
                TextGeneration.HideText();
            }
            else
            {
                Shown = false;
                MyAPIGateway.Utilities.ShowNotification("TextAPI mod not detected! (workshop id: 758597413)", 3000, MyFontEnum.Red);
            }
        }

        public void ReloadConfig(string caller)
        {
            if(Config.Load())
                Utilities.ShowColoredChatMessage(caller, "Config loaded.", MyFontEnum.Green);
            else
                Utilities.ShowColoredChatMessage(caller, "Config created and loaded default settings.", MyFontEnum.Green);

            Config.Save();
            TextGeneration.OnConfigReloaded();
        }

        public void ToggleTextInfo()
        {
            Config.TextShow.Value = !Config.TextShow.Value;
            Config.Save();

            if(buildInfoNotification == null)
                buildInfoNotification = MyAPIGateway.Utilities.CreateNotification("");
            buildInfoNotification.Text = (Config.TextShow ? "Text info ON + saved to config" : "Text info OFF + saved to config");
            buildInfoNotification.Show();
        }

        public void SetFreezePlacement(bool value, bool showNotification = true)
        {
            if(freezeGizmoNotification == null)
                freezeGizmoNotification = MyAPIGateway.Utilities.CreateNotification("");

            if(!EquipmentMonitor.IsCubeBuilder)
            {
                freezeGizmoNotification.Text = "Equip a block and aim at a grid.";
                freezeGizmoNotification.Font = MyFontEnum.Red;
            }
            else if(value && MyCubeBuilder.Static.DynamicMode) // requires a grid target to turn on
            {
                freezeGizmoNotification.Text = "Aim at a grid.";
                freezeGizmoNotification.Font = MyFontEnum.Red;
            }
            else
            {
                // HACK using this method instead of MyAPIGateway.CubeBuilder.FreezeGizmo's setter because that one ignores the value and sets it to true.
                MyCubeBuilder.Static.FreezeGizmo = value;

                freezeGizmoNotification.Text = (value ? "Freeze placement position ON" : "Freeze placement position OFF");
                freezeGizmoNotification.Font = MyFontEnum.White;

                if(value) // store the frozen position to check distance for auto-unfreeze
                    MyCubeBuilder.Static.GetAddPosition(out TextGeneration.lastGizmoPosition);
            }

            if(showNotification)
                freezeGizmoNotification.Show();
        }

        public void SetPlacementTransparency(bool value, bool showNotification = true)
        {
            MyCubeBuilder.Static.UseTransparency = value;

            if(showNotification)
            {
                if(transparencyNotification == null)
                    transparencyNotification = MyAPIGateway.Utilities.CreateNotification("");

                transparencyNotification.Text = (MyCubeBuilder.Static.UseTransparency ? "Placement transparency ON" : "Placement transparency OFF");
                transparencyNotification.Font = MyFontEnum.White;
                transparencyNotification.Show();
            }
        }
    }
}
