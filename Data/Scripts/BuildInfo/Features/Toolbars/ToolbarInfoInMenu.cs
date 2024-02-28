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
        }

        void LoadToolbar()
        {
            Toolbar = null;
            Render.SetVisible(false);

            ToolbarHolder th;
            if(!Main.ToolbarTracker.EntitiesWithToolbars.TryGetValue(TargetBlock, out th))
            {
                if(Alerted.Add(TargetBlock.BlockDefinition))
                {
                    string msg = $"{TargetBlock.BlockDefinition} not in tracked toolbars! Please inform author.";
                    MyAPIGateway.Utilities.ShowMessage(Log.ModName, msg);
                    Log.Info(msg);
                    MyLog.Default.WriteLine($"{Log.ModName} warning: {msg}");
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

            MyObjectBuilder_Toolbar toolbarOB = ToolbarTracker.GetToolbarFromEntity(TargetBlock, toolbarId);
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

            StringBuilder sb = Render.TextSB;

            float opacity = 1f;
            bool gamepadHUD = false;
            int toolbarPage = Toolbar.CurrentPageIndex;
            int startIndex = toolbarPage * Toolbar.SlotsPerPage;
            int maxIndexPage = startIndex + Toolbar.SlotsPerPage - 1;

            var slots = Toolbar.Items;

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

            sb.ColorA(ToolbarRender.HeaderColor * opacity).Append("Toolbar Info");

            if(Toolbar.PageCount > 1)
                sb.Append(" - Page ").Append(toolbarPage + 1);

            sb.Append(" <i>").ColorA(Color.Gray * opacity).Append("(").Append(BuildInfoMod.ModName).Append(" Mod)");
            sb.NewCleanLine();

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

            Render.UpdateProperties();
            Render.SetVisible(true);
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % 90 == 0)
            {
                LoadToolbar();
            }

            Render.UpdateBoxDrag();

            if(Toolbar?.CheckPageInputs() ?? false)
            {
                RefreshToolbar();
            }
        }
    }
}