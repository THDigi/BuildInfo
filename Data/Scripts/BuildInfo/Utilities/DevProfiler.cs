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
        private readonly string Name;
        private readonly long Start;
        private readonly int NotifyMs;
        private readonly DevProfilerData DataOut;

        public const string NumberFormat = "0.#########";

        public static readonly double TickFrequency = 10000000.0 / Stopwatch.Frequency;

        static DevProfiler()
        {
            if(!Stopwatch.IsHighResolution)
                Log.Info("WARNING: Stopwatch is not high resolution!");
            else
                Log.Info("Stopwatch is high resolution");
        }

        public DevProfiler(string name = "unnamed", int notify = 0, DevProfilerData dataOut = null)
        {
            Name = name;
            NotifyMs = notify;
            DataOut = dataOut;
            Start = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            long end = Stopwatch.GetTimestamp();
            long diff = end - Start;
            var timespan = TimeSpan.FromTicks((long)(diff * TickFrequency));

            if(DataOut != null)
            {
                DataOut.AddTime(timespan.TotalMilliseconds);
            }
            else
            {
                var text = $"PROFILE: {Name}: {timespan.TotalMilliseconds.ToString(NumberFormat)} ms";

                Log.Info(text);

                if(NotifyMs > 0)
                    MyAPIGateway.Utilities.ShowNotification(text, NotifyMs, MyFontEnum.Debug);
            }
        }
    }

    public class DevProfilerData
    {
        public struct FrameInfo
        {
            public readonly int Frame;
            public readonly double TimeMs;

            public FrameInfo(int frame, double time)
            {
                Frame = frame;
                TimeMs = time;
            }
        }

        public readonly string Name;
        public readonly Queue<FrameInfo> Times;
        public readonly int MaxHistory;

        public int TotalCalls;
        public int PrintEveryNAdd;

        public int NotifyMs;
        private IMyHudNotification notify;

        public double LastMeasure;
        public DateTime LastProfiledAt;

        public DevProfilerData(string name = "unnamed", int printEveryNadd = -1, int notifyMs = 0, int maxHistory = 60 * 5)
        {
            Name = name;
            PrintEveryNAdd = printEveryNadd;
            NotifyMs = notifyMs;
            MaxHistory = maxHistory;
            Times = new Queue<FrameInfo>(maxHistory);
        }

        public void AddTime(double ms)
        {
            LastMeasure = ms;
            LastProfiledAt = DateTime.UtcNow;

            if((Times.Count + 1) > MaxHistory)
                Times.Dequeue();

            TotalCalls++;
            Times.Enqueue(new FrameInfo(TotalCalls, ms));

            if(PrintEveryNAdd > -1 && TotalCalls % PrintEveryNAdd == 0)
            {
                PrintStats();
            }
        }

        public void Reset()
        {
            LastMeasure = 0;
            Times.Clear();
        }

        public void PrintStats()
        {
            string text;
            if(Times.Count == 0)
            {
                text = $"PROFILE: {Name,48}: --- No data ---";
            }
            else
            {
                double avg = Times.Average(i => i.TimeMs);
                var minFrame = GetMinFrame();
                var maxFrame = GetMaxFrame();
                double total = Times.Sum(i => i.TimeMs);

                string ago;
                var timeAgo = (DateTime.UtcNow - LastProfiledAt);
                if(timeAgo.TotalMilliseconds < 1000)
                    ago = timeAgo.TotalMilliseconds.ToString("0.0") + "ms ago";
                else if(timeAgo.TotalSeconds < 60)
                    ago = timeAgo.TotalSeconds.ToString("0.0") + "sec ago";
                else
                    ago = timeAgo.TotalMinutes.ToString("0") + "min ago";

                text = $"PROFILE: {Name,42}: last={LastMeasure.ToString(DevProfiler.NumberFormat),12} ms @ {ago,12}; avg={avg.ToString(DevProfiler.NumberFormat),12} ms; min={minFrame.TimeMs.ToString(DevProfiler.NumberFormat),12} ms @ {minFrame.Frame.ToString(),8}; max={maxFrame.TimeMs.ToString(DevProfiler.NumberFormat),12} ms @ {maxFrame.Frame.ToString(),8}; total={total.ToString(DevProfiler.NumberFormat),12} ms over {TotalCalls.ToString()} calls;";
            }

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

        public FrameInfo GetMinFrame()
        {
            FrameInfo minFrame = default(FrameInfo);
            double minTime = double.MaxValue;

            foreach(var frame in Times)
            {
                if(frame.TimeMs < minTime)
                {
                    minTime = frame.TimeMs;
                    minFrame = frame;
                }
            }

            return minFrame;
        }

        public FrameInfo GetMaxFrame()
        {
            FrameInfo maxFrame = default(FrameInfo);
            double maxTime = double.MinValue;

            foreach(var frame in Times)
            {
                if(frame.TimeMs > maxTime)
                {
                    maxTime = frame.TimeMs;
                    maxFrame = frame;
                }
            }

            return maxFrame;
        }
    }
}