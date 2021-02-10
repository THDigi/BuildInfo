using Digi.BuildInfo.Systems;
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
    public class QuickMenu : ModComponent
    {
        public const int MENU_TOTAL_ITEMS = 11;

        public bool Shown { get; private set; }
        public bool NeedsUpdate = true;
        public int SelectedItem = 0;

        private IMyHudNotification buildInfoNotification;
        private IMyHudNotification transparencyNotification;
        private IMyHudNotification freezeGizmoNotification;

        public QuickMenu(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_INPUT;
        }

        protected override void RegisterComponent()
        {
            EquipmentMonitor.ToolChanged += EquipmentMonitor_ToolChanged;
        }

        protected override void UnregisterComponent()
        {
            EquipmentMonitor.ToolChanged -= EquipmentMonitor_ToolChanged;
        }

        private void EquipmentMonitor_ToolChanged(MyDefinitionId toolDefId)
        {
            if(Shown && !EquipmentMonitor.IsCubeBuilder && !EquipmentMonitor.IsBuildTool)
            {
                CloseMenu();
            }
        }

        public void ShowMenu()
        {
            Shown = true;
        }

        public void CloseMenu()
        {
            Shown = false;
            NeedsUpdate = true;
        }

        protected override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(inMenu)
                return;

            UpdateHotkeys();

            if(!Shown)
                return;

            if(!anyKeyOrMouse)
                return;

            if(MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Escape))
            {
                CloseMenu();
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
                        CloseMenu();
                        break;
                    case 1:
                        if(EquipmentMonitor.AimedBlock != null)
                        {
                            CloseMenu();
                            Main.PickBlock.PickedBlockDef = EquipmentMonitor.BlockDef;
                        }
                        else
                            MyAPIGateway.Utilities.ShowNotification("This only works with a hand or ship tool.", 3000, FontsHandler.RedSh);
                        break;
                    case 2:
                        if(EquipmentMonitor.BlockDef == null)
                        {
                            MyAPIGateway.Utilities.ShowNotification("Equip/aim at a block that was added by a mod.", 3000, FontsHandler.RedSh);
                        }
                        else if(EquipmentMonitor.BlockDef.Context.IsBaseGame)
                        {
                            MyAPIGateway.Utilities.ShowNotification($"{EquipmentMonitor.BlockDef.DisplayNameText} was not added by a mod.", 3000, FontsHandler.RedSh);
                        }
                        else
                        {
                            CloseMenu();
                            Main.ChatCommandHandler.CommandModLink.ExecuteNoArgs();
                        }
                        break;
                    case 3:
                        CloseMenu();
                        Main.ChatCommandHandler.CommandHelp.ExecuteNoArgs();
                        break;
                    case 4:
                        CloseMenu();
                        Main.ChatCommandHandler.CommandWorkshop.ExecuteNoArgs();
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
                        SetFreezePlacement(!MyAPIGateway.CubeBuilder.FreezeGizmo, showNotification: TextAPIEnabled);
                        break;
                    case 9:
                        ToggleTextAPI();
                        break;
                    case 10:
                        CloseMenu();
                        Main.ChatCommandHandler.CommandReloadConfig.ExecuteNoArgs();
                        break;
                }
            }
        }

        private void UpdateHotkeys()
        {
            bool toolEquipped = (EquipmentMonitor.IsBuildTool || EquipmentMonitor.IsCubeBuilder);
            var context = InputLib.GetCurrentInputContext();

            if(toolEquipped && Config.ToggleTransparencyBind.Value.IsPressed(context))
            {
                if(Config.ToggleTransparencyBind.Value.IsJustPressed(context))
                {
                    NeedsUpdate = true;
                    SetPlacementTransparency(!MyCubeBuilder.Static.UseTransparency);
                }

                return;
            }

            if((toolEquipped || LockOverlay.LockedOnBlock != null) && Config.CycleOverlaysBind.Value.IsPressed(context))
            {
                if(Config.CycleOverlaysBind.Value.IsJustPressed(context))
                {
                    NeedsUpdate = true;
                    Overlays.CycleOverlayMode();
                }

                return;
            }

            if(toolEquipped && Config.FreezePlacementBind.Value.IsPressed(context))
            {
                if(Config.FreezePlacementBind.Value.IsJustPressed(context))
                {
                    NeedsUpdate = true;
                    SetFreezePlacement(!MyAPIGateway.CubeBuilder.FreezeGizmo);
                }

                return;
            }

            if(toolEquipped && Config.MenuBind.Value.IsPressed(context))
            {
                if(Config.MenuBind.Value.IsJustPressed(context))
                {
                    NeedsUpdate = true;

                    if(Shown)
                        CloseMenu();
                    else
                        ShowMenu();
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
                CloseMenu();
                MyAPIGateway.Utilities.ShowNotification("TextAPI mod not detected! (workshop id: 758597413)", 3000, FontsHandler.RedSh);
            }
        }

        public void ToggleTextInfo()
        {
            Config.TextShow.Value = !Config.TextShow.Value;
            Config.Save();

            if(buildInfoNotification == null)
                buildInfoNotification = MyAPIGateway.Utilities.CreateNotification("");

            buildInfoNotification.Hide(); // required since SE v1.194
            buildInfoNotification.Text = (Config.TextShow.Value ? "Text info ON + saved to config" : "Text info OFF + saved to config");
            buildInfoNotification.Show();
        }

        public void SetFreezePlacement(bool value, bool showNotification = true)
        {
            if(freezeGizmoNotification == null)
                freezeGizmoNotification = MyAPIGateway.Utilities.CreateNotification("");

            freezeGizmoNotification.Hide(); // required since SE v1.194

            if(!EquipmentMonitor.IsCubeBuilder)
            {
                freezeGizmoNotification.Text = "Equip a block and aim at a grid.";
                freezeGizmoNotification.Font = FontsHandler.RedSh;
            }
            else if(value && MyCubeBuilder.Static.DynamicMode) // requires a grid target to turn on
            {
                freezeGizmoNotification.Text = "Aim at a grid.";
                freezeGizmoNotification.Font = FontsHandler.RedSh;
            }
            else
            {
                MyCubeBuilder.Static.FreezeGizmo = value;

                freezeGizmoNotification.Text = (value ? "Freeze placement position ON" : "Freeze placement position OFF");
                freezeGizmoNotification.Font = FontsHandler.WhiteSh;

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
                    transparencyNotification = MyAPIGateway.Utilities.CreateNotification("", 2000, FontsHandler.WhiteSh);

                transparencyNotification.Hide(); // required since SE v1.194
                transparencyNotification.Text = (MyCubeBuilder.Static.UseTransparency ? "Placement transparency ON" : "Placement transparency OFF");
                transparencyNotification.Show();
            }
        }
    }
}
