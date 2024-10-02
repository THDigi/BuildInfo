using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Features.Toolbars.FakeAPI;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.Toolbars
{
    public class ToolbarInfoInMenu : ModComponent
    {
        ToolbarRender Render = new ToolbarRender();
        ToolbarRender Render2 = new ToolbarRender();
        Toolbar Toolbar;
        IMyTerminalBlock TargetBlock;

        HashSet<MyDefinitionId> Alerted = new HashSet<MyDefinitionId>(MyDefinitionId.Comparer);

        public ToolbarInfoInMenu(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.TextAPI.Detected += CreateUI;
            Main.EventToolbarMonitor.OpenedToolbarConfig += EventToolbarMonitor_OpenedToolbarConfig;
            Main.EventToolbarMonitor.ClosedToolbarConfig += EventToolbarMonitor_ClosedToolbarConfig;
        }

        public override void UnregisterComponent()
        {
            Main.TextAPI.Detected -= CreateUI;
            Main.EventToolbarMonitor.OpenedToolbarConfig -= EventToolbarMonitor_OpenedToolbarConfig;
            Main.EventToolbarMonitor.ClosedToolbarConfig -= EventToolbarMonitor_ClosedToolbarConfig;

            if(Alerted.Count > 0)
            {
                Log.Info("All block IDs found to be missing from toolbar reader:");
                foreach(var id in Alerted)
                {
                    Log.Info($"  {id}");
                }
            }
        }

        void CreateUI()
        {
            Render.CreateUI();
            Render2.CreateUI();

            Render.BoxDrag.Dragging += RenderDragging;
        }

        void EventToolbarMonitor_OpenedToolbarConfig(ListReader<IMyTerminalBlock> blocks)
        {
            if(!Render.IsInit)
                return;

            TargetBlock = blocks[0];
            if(TargetBlock != null)
                LoadToolbar();

            if(Toolbar != null)
            {
                // enable per-tick update for toolbar.CheckPageInputs()
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            }
        }

        void EventToolbarMonitor_ClosedToolbarConfig(ListReader<IMyTerminalBlock> blocks)
        {
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
            Toolbar = null;
            TargetBlock = null;
            Render.SetVisible(false);
            Render2.SetVisible(false);
        }

        void LoadToolbar()
        {
            Toolbar = null;
            Render.SetVisible(false);
            Render2.SetVisible(false);

            ToolbarHolder th;
            if(!Main.ToolbarTracker.EntitiesWithToolbars.TryGetValue(TargetBlock, out th))
            {
                if(Alerted.Add(TargetBlock.BlockDefinition))
                {
                    string msg = $"{TargetBlock.BlockDefinition} not in tracked toolbars! Please inform author.";
                    MyAPIGateway.Utilities.ShowMessage(Log.ModName, msg);
                    Log.Info(msg);
                    MyLog.Default.WriteLine($"{BuildInfoMod.ModName} warning: {msg}");
                }

                return;
            }

            ToolbarId toolbarId = ToolbarId.Normal;

            if(th.MultipleToolbars != null)
            {
                switch(Main.EventToolbarMonitor.LastOpenedToolbarType)
                {
                    case ToolbarInfo.EventToolbarMonitor.ToolbarType.RCWaypoint: toolbarId = ToolbarId.Waypoint; break;
                    case ToolbarInfo.EventToolbarMonitor.ToolbarType.LockOnVictim: toolbarId = ToolbarId.LockedOn; break;
                }

                Toolbar = th.MultipleToolbars.GetValueOrDefault(toolbarId);
            }
            else
            {
                Toolbar = th.SingleToolbar;
            }

            if(Toolbar == null)
            {
                Log.Error($"Couldn't find {toolbarId} toolbar for {TargetBlock}!");
                return;
            }

            MyObjectBuilder_Toolbar toolbarOB = ToolbarTracker.GetToolbarOBFromEntity(TargetBlock, toolbarId);
            if(toolbarOB == null)
            {
                //Log.Error($"Couldn't get {toolbarId} toolbar OB from {TargetBlock}!");
                return;
            }

            Toolbar.LoadFromOB(toolbarOB);

            RefreshToolbar();
        }

        void RefreshToolbar()
        {
            if(Toolbar == null)
                return;

            Render.Reset();
            Render2.Reset();

            StringBuilder sb = Render.TextSB;

            //if(BuildInfoMod.IsDevMod)
            //{
            //    for(int i = 0; i < Toolbar.Items.Length; i++)
            //    {
            //        ToolbarItem item = Toolbar.Items[i];
            //        if(item == null)
            //            continue;
            //
            //        DebugLog.PrintHUD(this, $"#{i} = {item} / valid={item?.IsValid}");
            //    }
            //}

            float opacity = 1f;

            sb.ColorA(ToolbarRender.HeaderColor * opacity).Append("Toolbar Info");

            if(TargetBlock is IMyEventControllerBlock && Toolbar.PageCount > 1 && Toolbar.SlotsPerPage == 2)
            {
                DesignEventCompact(sb, opacity);
            }
            else
            {
                DesignNormal(sb, opacity);
            }

            Render.HUDPosition = Main.Config.ToolbarLabelsMenuPosition.Value;
            Render.GUIScale = (float)(ToolbarRender.ScaleMultiplier * Main.Config.ToolbarLabelsMenuScale.Value);
            Render.UpdateProperties();
            Render.SetVisible(true);
        }

        /// <summary>
        /// Expects exactly 2 slots per page and multiple pages
        /// </summary>
        void DesignEventCompact(StringBuilder sb, float opacity)
        {
            sb.Append(" - Left side slots:");
            sb.NewCleanLine();

            int max = Toolbar.PageCount * Toolbar.SlotsPerPage;

            var slots = Toolbar.Items;

            for(int index = 0; index < max; index += 2)
            {
                if(index >= slots.Length)
                {
                    Log.Error($"Toolbar render error: index={index}; slots={slots.Length}; page={Toolbar.CurrentPageIndex}; using event-toolbar render");
                    return;
                }

                var item = slots[index];

                sb.ResetFormatting();

                if(item == null)
                    sb.ColorA(Color.Gray * opacity);

                //sb.Append(index + 1).Append(". ");
                sb.Append("  ");

                if(item == null)
                {
                    sb.Append("—");
                }
                else
                {
                    item.AppendFancyRender(sb, opacity);
                }

                sb.NewCleanLine();
            }

            sb = Render2.TextSB;

            sb.ColorA(ToolbarRender.HeaderColor * opacity).Append("Right side slots");
            sb.NewCleanLine();

            for(int index = 1; index < max; index += 2)
            {
                if(index >= slots.Length)
                {
                    Log.Error($"Toolbar render error: index={index}; slots={slots.Length}; page={Toolbar.CurrentPageIndex}; using event-toolbar render");
                    return;
                }

                var item = slots[index];

                sb.ResetFormatting();

                if(item == null)
                    sb.ColorA(Color.Gray * opacity);

                //sb.Append(index + 1).Append(". ");
                sb.Append("  ");

                if(item == null)
                {
                    sb.Append("—");
                }
                else
                {
                    item.AppendFancyRender(sb, opacity);
                }

                sb.NewCleanLine();
            }

            Render2.HUDPosition = Render.HUDPosition + new Vector2D(Render.Text.Text.GetTextLength().X + 0.02, 0);
            Render2.GUIScale = Render.GUIScale;
            Render2.UpdateProperties();
            Render2.SetVisible(true);
        }

        void DesignNormal(StringBuilder sb, float opacity)
        {
            bool gamepadHUD = false;
            int toolbarPage = Toolbar.CurrentPageIndex;
            var slots = Toolbar.Items;

            if(Toolbar.PageCount > 1)
            {
                Color pageSeleced = new Color(0, 155, 255);
                Color pageHasItems = new Color(125, 255, 155);

                //sb.Append(" - Page ").Append(toolbarPage + 1);

                sb.Append(" - Pages: ");

                for(int p = 0; p < Toolbar.PageCount; p++)
                {
                    int start = p * Toolbar.SlotsPerPage;
                    int max = start + Toolbar.SlotsPerPage - 1;
                    bool hasThings = false;

                    for(int i = 0; i < Toolbar.SlotsPerPage; i++)
                    {
                        int idx = start + i;
                        if(idx >= slots.Length)
                            break;

                        var item = slots[idx];
                        if(item != null)
                        {
                            //Log.Info($"[debug] found item {item.GetType().Name} at {idx}; p={p}; i={i}; start={start}; max={max}");
                            hasThings = true;
                            break;
                        }
                    }

                    bool isSelected = (toolbarPage == p);

                    if(isSelected)
                    {
                        if(sb[sb.Length - 1] == ' ')
                            sb.Length -= 1;

                        sb.ColorA(pageSeleced * opacity).Append("[");
                    }

                    if(hasThings)
                        sb.ColorA(pageHasItems * opacity);
                    else if(isSelected)
                        sb.Append("<reset>");

                    sb.Append(p + 1);

                    if(hasThings && !isSelected)
                        sb.Append("<reset>");

                    if(isSelected)
                        sb.ColorA(pageSeleced * opacity).Append("]<reset>");
                    else
                        sb.Append(" ");
                }
            }

            //sb.Append(" <i>").ColorA(Color.Gray * opacity).Append("(").Append(BuildInfoMod.ModName).Append(" Mod)");
            sb.NewCleanLine();

            int startIndex = toolbarPage * Toolbar.SlotsPerPage;
            int maxIndexPage = startIndex + Toolbar.SlotsPerPage - 1;

            for(int i = 0; i < Toolbar.SlotsPerPage; i++)
            {
                int index = startIndex + i;
                if(index >= slots.Length)
                {
                    Log.Error($"Toolbar render error: index={index}; slots={slots.Length}; page={toolbarPage}");
                    return;
                }

                var item = slots[index];

                sb.ResetFormatting();

                if(item == null)
                    sb.ColorA(Color.Gray * opacity);

                if(gamepadHUD)
                    sb.Append(Main.Constants.DPadIcons[i]).Append("  ");
                else
                    sb.Append(i + 1).Append(". ");

                if(item == null)
                {
                    sb.Append("—");
                }
                else
                {
                    item.AppendFancyRender(sb, opacity);
                }

                sb.NewCleanLine();
            }
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % 90 == 0)
            {
                LoadToolbar();
            }

            Render.HUDPosition = Main.Config.ToolbarLabelsMenuPosition.Value;
            Render.GUIScale = (float)(ToolbarRender.ScaleMultiplier * Main.Config.ToolbarLabelsMenuScale.Value);
            Render.UpdateBoxDrag();

            if(Toolbar?.CheckPageInputs() ?? false)
            {
                RefreshToolbar();
            }
        }

        void RenderDragging(Vector2D pos)
        {
            if(Render2.IsVisible)
            {
                Render2.HUDPosition = Render.HUDPosition + new Vector2D(Render.Text.Text.GetTextLength().X + 0.02, 0);
                Render2.GUIScale = Render.GUIScale;
                Render2.UpdateProperties();
            }
        }
    }
}