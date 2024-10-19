using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.ModAPI;
using VRage;
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

                Main.OnDrawEnd -= DrawEnd;
                if(value)
                    Main.OnDrawEnd += DrawEnd;
                else
                    Hide();
            }
        }

        public bool Advanced { get; set; }

        bool ShowMeasurements;
        double UpdatesAvgMs;
        double TotalAvgMs;
        List<MyTuple<IComponent, ProfileMeasure>> CompAndTimesList = new List<MyTuple<IComponent, ProfileMeasure>>();

        TextPackage ProfileText;

        TextPackage GraphText;
        double[] GraphTimesMs = new double[Constants.TicksPerSecond];
        HudAPIv2.BillBoardHUDMessage[] GraphBars = new HudAPIv2.BillBoardHUDMessage[Constants.TicksPerSecond];
        List<HudAPIv2.BillBoardHUDMessage> GraphNotches = new List<HudAPIv2.BillBoardHUDMessage>();

        const int MaxSpikeEntries = 10;
        const int SpikeFadeGray = 2;
        const int SpikeVanish = 10;
        List<MyTuple<int, string>> SpikeLog = new List<MyTuple<int, string>>();

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

        void Hide()
        {
            for(int i = 0; i < GraphTimesMs.Length; i++)
            {
                GraphTimesMs[i] = 0;
            }

            if(ProfileText != null)
            {
                ProfileText.Visible = false;
                ProfileText.TextStringBuilder.Clear();
            }

            if(GraphText != null)
            {
                GraphText.Visible = false;
            }
        }

        void DrawEnd()
        {
            if(!ShowMeasurements)
                return;

            int secTick = Main.Tick % 60;

            ProfileUpdates root = Main.RootMeasurements;

            TotalAvgMs = root.MeasuredInput.MovingAvg + root.MeasuredBeforeSim.MovingAvg + root.MeasuredAfterSim.MovingAvg + root.MeasuredDraw.MovingAvg;

            GraphTimesMs[secTick] = root.MeasuredInput.LastRead + root.MeasuredBeforeSim.LastRead + root.MeasuredAfterSim.LastRead + root.MeasuredDraw.LastRead;

            if(!Main.TextAPI.IsEnabled)
                return;

            if(Advanced)
            {
                DrawGraph();
            }

            if(Main.Tick % 30 == 0)
            {
                DrawText();
            }
        }

        void DrawText()
        {
            if(ProfileText == null)
            {
                ProfileText = new TextPackage(new StringBuilder(512), useShadow: false, backgroundTexture: Constants.MatUI_Square);
                ProfileText.HideWithHUD = false;
                ProfileText.Position = new Vector2D(-0.99, 0.98);
                ProfileText.Scale = 0.65;
                ProfileText.Visible = true;
                ProfileText.Background.BillBoardColor = Color.Black * 0.9f;

                // TODO: make my own outlined monospace font for this and toolbar staus too
                //ProfileText.Font = "monospace";
                ProfileText.Font = FontsHandler.TextAPI_MonospaceFont;
            }

            StringBuilder sb = ProfileText.TextStringBuilder.Clear();

            sb.Append("Sim speed: ").Append((int)(MyAPIGateway.Physics.SimulationRatio * 100)).Append("%\n");

            UpdatesAvgMs = 0;

            ProfileUpdates root = Main.RootMeasurements;
            bool detailed = Advanced;

            AppendType(sb, "Input", Main.ComponentUpdateInput, (c) => c.Profiled.MeasuredInput, root.MeasuredInput.MovingAvg, detailed);

            if(Main.SessionHasBeforeSim)
                AppendType(sb, "BeforeSim", Main.ComponentUpdateBeforeSim, (c) => c.Profiled.MeasuredBeforeSim, root.MeasuredBeforeSim.MovingAvg, detailed);

            if(Main.SessionHasAfterSim)
                AppendType(sb, "AfterSim", Main.ComponentUpdateAfterSim, (c) => c.Profiled.MeasuredAfterSim, root.MeasuredAfterSim.MovingAvg, detailed);

            AppendType(sb, "Draw", Main.ComponentUpdateDraw, (c) => c.Profiled.MeasuredDraw, root.MeasuredDraw.MovingAvg, detailed);

            double frameworkTotal = (TotalAvgMs - UpdatesAvgMs);
            double textAPIdraw = TextAPI.DrawCost.MovingAvg;

            if(!detailed)
            {
                sb.Append("Components Total: ").Append(UpdatesAvgMs.ToString(MeasureFormat)).Append(" ms avg\n");
            }

            sb.Append("TextAPI manual Draw(): ").Append(textAPIdraw.ToString(MeasureFormat)).Append(" ms avg | ").Append((int)((textAPIdraw / TotalAvgMs) * 100)).Append("% of total\n");

            sb.Append("Framework: ").Append(frameworkTotal.ToString(MeasureFormat)).Append(" ms avg | ").Append((int)((frameworkTotal / TotalAvgMs) * 100)).Append("% of total\n");

            sb.Append("Total: ").Append(TotalAvgMs.ToString(MeasureFormat)).Append(" ms avg | ");

            const double MsPerTick = (1000.0 / Constants.TicksPerSecond);
            float ratioOfTick = (float)(TotalAvgMs / MsPerTick);

            const float BadRatio = 0.2f; // reach red at 20%, which is ~3.2ms
            float ratioForColor = MathHelper.Clamp(MathHelper.Lerp(0, 1f / BadRatio, ratioOfTick), 0, 1);

            sb.Color(Color.Lerp(Color.Lime, Color.Red, ratioForColor)).Append((ratioOfTick * 100).ToString("0.##")).Append("%<reset> of tick budget\n");

            if(detailed)
            {
                sb.Append("Spike log:\n");

                if(SpikeLog.Count > 0)
                {
                    for(int i = SpikeLog.Count - 1; i >= 0; i--)
                    {
                        MyTuple<int, string> entry = SpikeLog[i];
                        var relativeTime = TimeSpan.FromSeconds((Main.Tick - entry.Item1) / 60d);

                        if(relativeTime.TotalSeconds > SpikeVanish)
                        {
                            SpikeLog.RemoveAt(i);
                            continue;
                        }

                        Color color = Color.Lerp(Color.White, new Color(75, 75, 75), (float)MathHelper.Clamp((relativeTime.TotalSeconds / SpikeFadeGray), 0, 1));

                        sb.Append($"<color={color.R},{color.G},{color.B}>(")
                            .Append(relativeTime.TotalSeconds < 10 ? " " : "").Append((int)relativeTime.TotalSeconds).Append("s ago) ")
                            .Append(entry.Item2).Append("<reset>\n");
                    }
                }
                else
                    sb.Append("<color=gray>(Empty)<reset>\n");
            }

            sb.Length -= 1; // remove last newline

            ProfileText.UpdateBackgroundSize(padding: 0.02f);

            ProfileText.Visible = true;
            //ProfileText.Draw();
        }

        void AppendType(StringBuilder sb, string title, List<IComponent> components, Func<IComponent, ProfileMeasure> getField, double totalMs, bool detailed)
        {
            CompAndTimesList.Clear();

            double componentsMs = 0;
            foreach(IComponent comp in components)
            {
                if(comp == this)
                    continue;

                ProfileMeasure profiled = getField(comp);
                CompAndTimesList.Add(MyTuple.Create(comp, profiled));
                componentsMs += profiled.MovingAvg;
            }

            UpdatesAvgMs += componentsMs;

            if(detailed)
            {
                const string NF = ",10:" + MeasureFormat;

                {
                    const string format = "{0,-9}|{1" + NF + "} ms total avg\n";
                    sb.AppendFormat(format, title, totalMs);

                    //double frameworkMs = totalMs - componentsMs;
                    //const string format = "{0,-9}|{1" + NF + "} ms avg |<color=gray>{2" + NF + "} components,{3" + NF + "} framework<reset>\n";
                    //sb.AppendFormat(format, title, totalMs, componentsMs, frameworkMs);

                    //sb.Append(title).Append(' ', Math.Max(0, 9 - title.Length)).Append(" | components: ").Append(totalMs.ToString(MeasureFormat))
                    //  .Append("ms | framework: ").Append(Math.Max(0, rootTotal - totalMs).ToString(MeasureFormat))
                    //  .Append(" | total: ").Append(rootTotal.ToString(MeasureFormat))
                    //  .Append('\n');
                }

                CompAndTimesList.Sort((a, b) => b.Item2.MovingAvg.CompareTo(a.Item2.MovingAvg)); // sort descending

                foreach(MyTuple<IComponent, ProfileMeasure> kv in CompAndTimesList)
                {
                    string name = kv.Item1.GetType().Name;
                    ProfileMeasure profiled = kv.Item2;

                    //string avg = profiled.MovingAvg.ToString(MeasureFormat);
                    //string min = profiled.Min.ToString(MeasureFormat);
                    //string max = profiled.Max.ToString(MeasureFormat);
                    //sb.Append("  ").Append(avg).Append(" ms | extremes: ").Append(min).Append(" to ").Append(max).Append(" | ").Append(name).Append('\n');

                    int percent = (int)((profiled.MovingAvg / TotalAvgMs) * 100);

                    const string format = "{0,7}% |{1" + NF + "} ms avg |<color=gray>{2" + NF + "} min,{3" + NF + "} max <reset>| {4}\n";
                    sb.AppendFormat(format, percent, profiled.MovingAvg, profiled.Min, profiled.Max, name);
                }
            }
        }

        void DrawGraph()
        {
            int secTick = Main.Tick % 60;

            const int BarRangeMs = 8;
            const double BarRedMs = 2;
            const float BarHeight = (800f / 1080f);
            const float BarWidth = (8f / 1920f);
            const float BarSpacing = (2f / 1920f);

            const float NotchWidth = (16f / 1920f);
            const float NotchHeight = (4f / 1920f);

            if(GraphText == null)
            {
                GraphText = new TextPackage(new StringBuilder(128), useShadow: false, backgroundTexture: null);
                GraphText.HideWithHUD = false;
                GraphText.Position = new Vector2D(-0.4, 0.98);
                GraphText.Scale = 0.65;
                GraphText.Visible = true;
                GraphText.Font = FontsHandler.TextAPI_MonospaceFont;

                StringBuilder sb = GraphText.TextStringBuilder.Clear();

                sb.Clear().Append($"Graph shows total mod time per tick, over a second | Height range: {BarRangeMs:0.##}ms");
            }

            GraphText.Visible = true;

            Vector2D pos = new Vector2D(-0.4, 0.92);

            if(GraphNotches.Count == 0)
            {
                for(int i = 0; i <= BarRangeMs; i++)
                {
                    var notch = CreateHUDTexture(Constants.MatUI_Square, Color.White, Vector2D.Zero, false);

                    notch.Width = NotchWidth;
                    notch.Height = NotchHeight;
                    notch.Origin = pos + new Vector2D(0, (BarHeight / BarRangeMs) * -i);
                    notch.BillBoardColor = Color.Lerp(Color.Lime, Color.Red, (float)(i / BarRedMs));

                    GraphNotches.Add(notch);
                }
            }

            for(int i = 0; i < GraphNotches.Count; i++)
            {
                GraphNotches[i].Draw();
            }

            pos += new Vector2D(NotchWidth + BarSpacing, 0);

            for(int i = 0; i < GraphBars.Length; i++)
            {
                HudAPIv2.BillBoardHUDMessage bar = GraphBars[i];
                if(bar == null)
                {
                    bar = CreateHUDTexture(Constants.MatUI_Square, Color.White, Vector2D.Zero, false);
                    GraphBars[i] = bar;
                }

                double ms = GraphTimesMs[i];

                Color color = Color.Lerp(Color.Lime, Color.Red, (float)(ms / BarRedMs));
                //color *= secTick ...

                if(secTick == i)
                    color = Color.Blue;

                bar.BillBoardColor = color;

                float ratio = (float)MathHelper.Clamp((ms / BarRangeMs), 0, 1);

                bar.Height = BarHeight * ratio;
                bar.Width = BarWidth;

                if(ms >= BarRedMs)
                {
                    const double SpikeMs = 1;

                    FindSpike("Input", Main.ComponentUpdateInput, (c) => c.Profiled.MeasuredInput, SpikeMs);
                    FindSpike("BeforeSim", Main.ComponentUpdateBeforeSim, (c) => c.Profiled.MeasuredBeforeSim, SpikeMs);
                    FindSpike("AfterSim", Main.ComponentUpdateAfterSim, (c) => c.Profiled.MeasuredAfterSim, SpikeMs);
                    FindSpike("Draw", Main.ComponentUpdateDraw, (c) => c.Profiled.MeasuredDraw, SpikeMs);

                    if(SpikeLog.Count > MaxSpikeEntries)
                        SpikeLog.RemoveRange(0, SpikeLog.Count - MaxSpikeEntries);
                }

                bar.Origin = pos + new Vector2D(i * (BarWidth + BarSpacing), bar.Height / -2f);

                bar.Draw();
            }
        }

        void FindSpike(string updateName, List<IComponent> components, Func<IComponent, ProfileMeasure> getField, double spikeMs)
        {
            foreach(IComponent comp in components)
            {
                if(comp == this)
                    continue;

                ProfileMeasure profiled = getField(comp);
                if(profiled.LastRead >= spikeMs)
                {
                    SpikeLog.Add(MyTuple.Create(Main.Tick, $"{comp.GetType().Name} spiked {profiled.LastRead:0.##} ms in {updateName}"));
                }
            }
        }
    }
}