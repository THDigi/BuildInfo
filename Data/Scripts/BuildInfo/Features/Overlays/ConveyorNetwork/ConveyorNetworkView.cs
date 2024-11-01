using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.GUI.Elements;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.Features.Overlays.ConveyorNetwork
{
    public class ConveyorNetworkView : ModComponent
    {
        public const int RescanPeriodically = Constants.TicksPerSecond * 30; // 0 to disable

        static readonly Color BackgroundColor = Constants.Color_UIBackground * 0.96f;

        internal ConveyorNetworkCompute Compute;
        internal ConveyorNetworkRender Render;

        internal HashSet<IMyCubeGrid> TempGrids = new HashSet<IMyCubeGrid>();

        IMyCubeGrid TargetGrid;
        public IMySlimBlock TargetBlock;

        IMyHudNotification Notification;

        public int RescanAtTick;

        bool TextBoxVisible = false;
        CornerTextBox TextBox;
        bool TextNeedsRefresh = true;

        public ConveyorNetworkView(BuildInfoMod main) : base(main)
        {
            Compute = new ConveyorNetworkCompute(this);
            Render = new ConveyorNetworkRender(this);
        }

        public override void RegisterComponent()
        {
            Compute.Init();
            Render.Init();
        }

        public override void UnregisterComponent()
        {
            TextBox?.Dispose();

            if(!Main.ComponentsRegistered)
                return;
        }

        public void Reset()
        {
            TargetGrid = null;
            TargetBlock = null;
            Render.Reset();
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, false);

            TextNeedsRefresh = true;
            TextBoxVisible = false;
            TextBox?.SetVisible(false);
        }

        public void Notify(string text, int aliveTimeMs = 2000, string font = FontsHandler.BI_SEOutlined)
        {
            if(Notification == null)
                Notification = MyAPIGateway.Utilities.CreateNotification(string.Empty);

            Notification.Hide(); // required otherwise it breaks
            Notification.Text = $"ConveyorVis: {text}";
            Notification.AliveTime = aliveTimeMs;
            Notification.Font = font;
            Notification.Show();
        }

        public void ShowFor(IMyCubeGrid grid, IMySlimBlock traceFrom = null, bool notify = true)
        {
            Reset();

            TempGrids.Clear();
            try
            {
                MyAPIGateway.GridGroups.GetGroup(grid, GridLinkTypeEnum.Physical, TempGrids);
                if(!Utils.IsShipFriendly(TempGrids))
                {
                    //if(notify)
                    Notify("Cannot show, unfriendly ship!", 4000, FontsHandler.RedSh);

                    return;
                }

                bool shouldRender = Compute.FindConveyorNetworks(TempGrids, traceFrom, notify);
                if(shouldRender)
                {
                    TargetGrid = grid;
                    TargetBlock = traceFrom;

                    if(RescanPeriodically > 0)
                        RescanAtTick = Main.Tick + RescanPeriodically;

                    SetUpdateMethods(UpdateFlags.UPDATE_DRAW, true);
                }
            }
            finally
            {
                TempGrids.Clear();
            }
        }

        public void StopShowing(string customMessage = null, bool notify = true)
        {
            Reset();
            Compute.Reset();

            if(notify)
                Notify(customMessage ?? "Stopped showing");
        }

        public void ScheduleRescan()
        {
            RescanAtTick = Main.Tick + 30;
        }

        public override void UpdateDraw()
        {
            if(TargetGrid == null)
                return;

            if(!Render.IsValid())
            {
                StopShowing();
                return;
            }

            if(RescanAtTick > 0 && RescanAtTick <= Main.Tick)
            {
                if(RescanPeriodically > 0)
                    RescanAtTick = Main.Tick + RescanPeriodically;
                else
                    RescanAtTick = 0;

                ShowFor(TargetGrid, TargetBlock, notify: false);
            }

            //if(!Main.IsPaused)
            //using(new DevProfiler("render", 16)) 
            {
                Render.Draw();
            }

            // hide only legend with HUD
            if(Main.GameConfig.HudState == Systems.HudState.OFF)
                return;

            #region Create HUD element
            if(TextBox == null)
            {
                const float Scale = 0.75f;
                TextBox = new CornerTextBox(BackgroundColor, CornerFlag.All, Scale);

                // to the right, to clear "Game paused" top banner
                TextBox.Position = new Vector2D(0.12, 0.985);
                TextBox.CornerSizes = new CornerBackground.CornerSize(12 * Scale, 48 * Scale, 12 * Scale, 12 * Scale);

                TextBox.Text.Text.SkipLinearRGB = true; // as colors are already premultiplied in this particular case
            }
            #endregion

            bool shouldBeVisible = true;
            if(Main.SpectatorControlInfo.Visible)
            {
                shouldBeVisible = false;
            }

            if(TextBoxVisible != shouldBeVisible)
            {
                TextBoxVisible = shouldBeVisible;
                //TextBox?.SetVisible(shouldBeVisible);
            }

            if(TextBoxVisible)
            {
                TextBox.UpdatePosition();
                TextBox.Draw();
            }

            if(TextNeedsRefresh)
            {
                TextNeedsRefresh = false;

                #region Update text
                StringBuilder sb = TextBox.TextSB.Clear();

                sb.Append("    ConveyorVis color meaning").NewCleanLine();
                const string Prefix = "";

                sb.Append(Prefix);
                foreach(Vector4 vecColor in ConveyorNetworkRender.NetworkColors)
                {
                    sb.Color(new Color(vecColor)).Append(FontsHandler.IconSquare);
                }
                sb.Append("<reset> are networks").NewCleanLine();

                sb.Append(Prefix).Color(new Color(ConveyorNetworkRender.IsolatedColor))
                    .Append(FontsHandler.IconSquare).Append("<reset> is not connected or unfinished or broken").NewCleanLine();

                sb.Append(Prefix).Color(new Color(ConveyorNetworkRender.ConnectableColor))
                    .Append(FontsHandler.IconCircle).Append("<reset> indicates it can connect to another grid").NewCleanLine();

                sb.Append(Prefix).Append("Network-colored ").Append(FontsHandler.IconCircle).Append(" indicate a dead end.").NewCleanLine();
                sb.Append(Prefix).Append("Network-colored transluscent box indicates block has inventory.").NewCleanLine();
                sb.Append(Prefix).Append("Arrows indicate one-way conveyor path.").NewCleanLine();

                bool allPowered = true;
                bool somePowered = false;

                foreach(IMyCubeGrid grid in Compute.GridsForEvents)
                {
                    var state = grid.ResourceDistributor.ResourceState;
                    bool hasPower = (state == MyResourceStateEnum.Ok || state == MyResourceStateEnum.OverloadAdaptible);

                    if(hasPower)
                        somePowered = true;
                    else
                        allPowered = false;
                }

                if(!allPowered)
                {
                    if(somePowered)
                        sb.Color(Color.Red).Append("Warning: some grids are not powered!");
                    else
                        sb.Color(Color.Red).Append("Warning: ship is not powered!");
                }

                sb.Length -= 1; // remove last newline
                #endregion
            }
        }
    }
}
