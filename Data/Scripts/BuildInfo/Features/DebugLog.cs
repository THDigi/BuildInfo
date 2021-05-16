using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using VRageMath;
using BlendType = VRageRender.MyBillboard.BlendTypeEnum;

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

        private HudAPIv2.HUDMessage Text;
        private Queue<LogMsg> LogList;

        struct LogMsg
        {
            public readonly int ExpiresAtTick;
            public readonly string CallerName;
            public readonly string Message;

            public LogMsg(string callerName, string message)
            {
                // NOTE: not designed for this to be configurable per-message!
                ExpiresAtTick = BuildInfoMod.Instance.Tick + (Constants.TICKS_PER_SECOND * MessageExpireSeconds);
                CallerName = callerName;
                Message = message;
            }
        }

        public DebugLog(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            if(BuildInfoMod.IsDevMod)
            {
                Main.TextAPI.Detected += TextAPIDetected;
            }
        }

        public override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            Main.TextAPI.Detected -= TextAPIDetected;
        }

        public static void ClearHUD(object caller)
        {
            if(!BuildInfoMod.IsDevMod)
                return;

            var debugMsgs = BuildInfoMod.Instance.DebugLog.LogList;
            debugMsgs?.Clear();
            PrintHUD(caller, "(log cleared)");
        }

        public static void PrintHUD(object caller, string message, bool log = false)
        {
            if(!BuildInfoMod.IsDevMod)
                return;

            var debugMsgs = BuildInfoMod.Instance.DebugLog.LogList;
            if(debugMsgs == null)
                BuildInfoMod.Instance.DebugLog.LogList = debugMsgs = new Queue<LogMsg>(MaxMessages);

            if(debugMsgs.Count > MaxMessages)
                debugMsgs.Dequeue();

            string callerName = caller?.GetType()?.Name ?? "[unspecified]";

            debugMsgs.Enqueue(new LogMsg(callerName, message));

            BuildInfoMod.Instance.DebugLog.UpdateText();

            if(log)
                Log.Info($"{callerName}: {message}");
        }

        void TextAPIDetected()
        {
            Text = new HudAPIv2.HUDMessage(new StringBuilder(128 * MaxMessages), new Vector2D(-0.98, 0.98), Scale: TextScale, Shadowing: true, Blend: BlendType.PostPP);
            Text.Visible = false;
            UpdateText();
        }

        void UpdateText()
        {
            if(Text == null || LogList == null)
                return;

            Text.Visible = true;
            var sb = Text.Message.Clear();

            foreach(var line in LogList)
            {
                sb.Color(new Color(55, 200, 155)).Append(line.CallerName).Append(": ").Color(Color.White).Append(line.Message).Append('\n');
            }

            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, (LogList.Count > 0));
        }

        public override void UpdateAfterSim(int tick)
        {
            if(LogList == null || LogList.Count == 0)
                return;

            while(LogList.Count > 0 && LogList.Peek().ExpiresAtTick <= tick)
            {
                LogList.Dequeue();
            }

            UpdateText();
        }
    }
}