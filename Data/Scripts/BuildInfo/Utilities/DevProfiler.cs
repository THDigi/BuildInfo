using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Digi
{
    public struct DevProfiler : IDisposable
    {
        private readonly string name;
        private readonly long start;
        private readonly int notify;
        private readonly DevProfilerData dataOut;

        public DevProfiler(string name = "unnamed", int notify = 0, DevProfilerData dataOut = null)
        {
            this.name = name;
            this.notify = notify;
            this.dataOut = dataOut;
            start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            var end = Stopwatch.GetTimestamp();
            var timespan = new TimeSpan(end - start);

            if(dataOut != null)
            {
                dataOut.AddTime(timespan.TotalMilliseconds);
                dataOut.PrintStats(name);
            }
            else
            {
                var text = $"PROFILE: {name}: {timespan.TotalMilliseconds.ToString("0.##########")} ms";

                Log.Info(text);

                if(notify > 0)
                    MyAPIGateway.Utilities.ShowNotification(text, notify, MyFontEnum.Debug);
            }
        }
    }

    public class DevProfilerData
    {
        public readonly Queue<double> Times;
        public readonly int MaxHistory;

        public int NotifyMs;
        private IMyHudNotification notify;

        public double LastTime;

        public DevProfilerData(int notifyMs = 0, int maxHistory = 60 * 5)
        {
            NotifyMs = notifyMs;
            MaxHistory = maxHistory;
            Times = new Queue<double>(maxHistory);
        }

        public void AddTime(double ms)
        {
            LastTime = ms;

            if((Times.Count + 1) > MaxHistory)
                Times.Dequeue();

            Times.Enqueue(ms);
        }

        public void PrintStats(string name)
        {
            var avg = Times.Average();
            var min = Times.Min();
            var max = Times.Max();
            var text = $"PROFILE: {name}: {LastTime.ToString("N10")} ms; avg: {avg.ToString("N10")} ms; min: {min.ToString("N10")} ms; max: {max.ToString("N10")} ms";

            Log.Info(text);

            if(NotifyMs > 0)
            {
                if(notify == null)
                {
                    notify = MyAPIGateway.Utilities.CreateNotification(text, NotifyMs, MyFontEnum.Debug);
                }
                else
                {
                    notify.Hide();
                    notify.Text = text;
                    notify.Show();
                }
            }
        }
    }
}