using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage;
using VRage.Utils;
using VRageMath;
using static Digi.BuildInfo.Systems.TextAPI;

namespace Digi.BuildInfo.Features
{
    public class ProfilerDisplay : ModComponent
    {
        public bool Show
        {
            get { return ShowMeasurements; }
            set
            {
                ShowMeasurements = value;

                if(ProfileText != null)
                    ProfileText.Visible = value;

                Main.OnDrawEnd -= DrawEnd;
                if(value)
                    Main.OnDrawEnd += DrawEnd;
            }
        }

        bool ShowMeasurements;
        double AllUpdatesMs;
        List<MyTuple<IComponent, ProfileMeasure>> CompAndTimesList = new List<MyTuple<IComponent, ProfileMeasure>>();

        TextPackage ProfileText;

        const string MeasureFormat = "0.000000";

        public ProfilerDisplay(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        void DrawEnd()
        {
            if(Main.Tick % 30 != 0)
                return;

            if(!ShowMeasurements)
                return;

            if(!Main.TextAPI.IsEnabled)
                return;

            if(ProfileText == null)
            {
                ProfileText = new TextPackage(new StringBuilder(512), useShadow: false, backgroundTexture: MyStringId.GetOrCompute("Square"));
                ProfileText.HideWithHUD = false;
                ProfileText.Position = new Vector2D(-0.99, 0.98);
                ProfileText.Scale = 0.75;
                ProfileText.Visible = true;
                ProfileText.Background.BillBoardColor = Color.Black * 0.9f;

                // TODO: make my own outlined monospace font for this and toolbar staus too
                //ProfileText.Font = "monospace";
            }

            StringBuilder sb = ProfileText.TextStringBuilder.Clear();

            sb.Append("Sim speed: ").Append((MyAPIGateway.Physics.SimulationRatio * 100).ToString("0")).Append(" %\n");

            bool append = MyAPIGateway.Input.IsAnyShiftKeyPressed();

            AllUpdatesMs = 0;
            ProfileUpdates root = Main.RootMeasurements;

            AppendType(sb, "Input", Main.ComponentUpdateInput, (c) => c.Profiled.MeasuredInput, root.MeasuredInput.MovingAvg, append);

            if(Main.SessionHasBeforeSim)
                AppendType(sb, "BeforeSim", Main.ComponentUpdateBeforeSim, (c) => c.Profiled.MeasuredBeforeSim, root.MeasuredBeforeSim.MovingAvg, append);

            if(Main.SessionHasAfterSim)
                AppendType(sb, "AfterSim", Main.ComponentUpdateAfterSim, (c) => c.Profiled.MeasuredAfterSim, root.MeasuredAfterSim.MovingAvg, append);

            AppendType(sb, "Draw", Main.ComponentUpdateDraw, (c) => c.Profiled.MeasuredDraw, root.MeasuredDraw.MovingAvg, append);

            double allTotal = root.MeasuredInput.MovingAvg + root.MeasuredBeforeSim.MovingAvg + root.MeasuredAfterSim.MovingAvg + root.MeasuredDraw.MovingAvg;
            double frameworkTotal = (allTotal - AllUpdatesMs);
            double textAPIdraw = TextAPI.DrawCost.MovingAvg;

            if(!append)
            {
                sb.Append("(hold Shift to show per-component)\n");
                sb.Append("Components Total: ").Append(AllUpdatesMs.ToString(MeasureFormat)).Append(" ms\n");
            }

            sb.Append("TextAPI draw avg: ").Append(textAPIdraw.ToString(MeasureFormat)).Append(" ms\n");

            sb.Append("Framework avg: ").Append(frameworkTotal.ToString(MeasureFormat)).Append(" ms\n");

            sb.Append("Total avg: ").Append(allTotal.ToString(MeasureFormat)).Append(" ms\n");

            sb.Length -= 1; // remove last newline

            ProfileText.UpdateBackgroundSize(padding: 0.02f);

            //ProfileText.Draw();
        }

        void AppendType(StringBuilder sb, string title, List<IComponent> components, Func<IComponent, ProfileMeasure> getField, double rootTotal, bool append)
        {
            CompAndTimesList.Clear();

            double totalMs = 0;
            foreach(IComponent comp in components)
            {
                if(comp == this)
                    continue;

                ProfileMeasure profiled = getField(comp);
                CompAndTimesList.Add(MyTuple.Create(comp, profiled));
                totalMs += profiled.MovingAvg;
            }

            AllUpdatesMs += totalMs;

            if(append)
            {
                sb.Append(title).Append(' ', Math.Max(0, 9 - title.Length)).Append(" | components: ").Append(totalMs.ToString(MeasureFormat)).Append("ms | framework: ").Append(Math.Max(0, rootTotal - totalMs).ToString(MeasureFormat)).Append(" | total: ").Append(rootTotal.ToString(MeasureFormat));
                sb.Append('\n');

                CompAndTimesList.Sort((a, b) => b.Item2.MovingAvg.CompareTo(a.Item2.MovingAvg)); // sort descending

                foreach(MyTuple<IComponent, ProfileMeasure> kv in CompAndTimesList)
                {
                    string name = kv.Item1.GetType().Name;
                    ProfileMeasure profiled = kv.Item2;
                    string avg = profiled.MovingAvg.ToString(MeasureFormat);
                    string min = profiled.Min.ToString(MeasureFormat);
                    string max = profiled.Max.ToString(MeasureFormat);

                    sb.Append("  ").Append(avg).Append(" ms | extremes: ").Append(min).Append(" to ").Append(max).Append(" | ").Append(name).Append('\n');
                }
            }
        }
    }
}