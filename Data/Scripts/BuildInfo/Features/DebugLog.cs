using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using VRageMath;

namespace Digi.BuildInfo.Features
{
    /// <summary>
    /// On-screen debug messages for local mod only.
    /// </summary>
    public class DebugLog : ModComponent
    {
        const int MaxMessages = 10;
        const int MessageExpireSeconds = 5;
        const double TextScale = 0.75;

        HudAPIv2.HUDMessage Text;
        Queue<LogMsg> LogList;

        struct LogMsg
        {
            public readonly int LoggedAtTick;
            public readonly string CallerName;
            public readonly string Message;

            public LogMsg(string callerName, string message)
            {
                LoggedAtTick = BuildInfoMod.Instance.Tick;
                CallerName = callerName;
                Message = message;
            }
        }

        public DebugLog(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            Main.TextAPI.Detected += TextAPIDetected;
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.TextAPI.Detected -= TextAPIDetected;
        }

        public static void ClearHUD(object caller)
        {
            Queue<LogMsg> debugMsgs = BuildInfoMod.Instance?.DebugLog?.LogList;
            debugMsgs?.Clear();
            PrintHUD(caller, "(log cleared)");
        }

        public static void PrintHUD(object caller, string message, bool log = false)
        {
            DebugLog instance = BuildInfoMod.Instance?.DebugLog;
            if(instance == null)
                return;

            Queue<LogMsg> debugMsgs = instance.LogList;
            if(debugMsgs == null)
                instance.LogList = debugMsgs = new Queue<LogMsg>(MaxMessages);

            if(debugMsgs.Count > MaxMessages)
                debugMsgs.Dequeue();

            string callerName = "[unspecified]";
            if(caller != null)
                callerName = VRage.TypeExtensions.PrettyName(caller.GetType());

            debugMsgs.Enqueue(new LogMsg(callerName, message));

            instance.UpdateText();

            if(log)
                Log.Info($"{callerName}: {message}");
        }

        void TextAPIDetected()
        {
            Text = TextAPI.CreateHUDText(new StringBuilder(128 * MaxMessages), new Vector2D(-0.98, 0.98), scale: TextScale, hideWithHud: false);
            UpdateText();
        }

        void UpdateText()
        {
            if(Text == null || LogList == null)
                return;

            Text.Visible = true;
            StringBuilder sb = Text.Message.Clear();

            foreach(LogMsg line in LogList)
            {
                TimeSpan time = TimeSpan.FromSeconds(line.LoggedAtTick / 60);

                sb.Color(Color.Gray).Append(time.ToString(@"hh\:mm\:ss")).Append("  ")
                  .Color(new Color(55, 200, 155)).Append(line.CallerName).Append(": ")
                  .Color(Color.White).Append(line.Message).Append('\n');
            }

            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, (LogList.Count > 0));
        }

        public override void UpdateAfterSim(int tick)
        {
            if(LogList == null || LogList.Count == 0)
                return;

            // NOTE: not designed for this to be configurable per-message!
            int messageLife = (Constants.TicksPerSecond * MessageExpireSeconds);

            while(LogList.Count > 0 && (LogList.Peek().LoggedAtTick + messageLife) <= tick)
            {
                LogList.Dequeue();
            }

            UpdateText();
        }
    }
}