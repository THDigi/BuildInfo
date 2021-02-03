#if false
using System;
using System.Collections.Generic;
using System.Text;
using Digi.BuildInfo.Systems;
using Digi.BuildInfo.Utilities;
using Digi.ComponentLib;
using Draygo.API;
using VRageMath;
using BlendType = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features
{
    public class DebugLog : ModComponent
    {
        const int MaxMessages = 10;
        const int MessageExpireSeconds = 5;
        const double TextScale = 0.6;

        private HudAPIv2.HUDMessage text;
        private Queue<LogMsg> logList = new Queue<LogMsg>(MaxMessages);

        public DebugLog(BuildInfoMod main) : base(main)
        {
        }

        protected override void RegisterComponent()
        {
            TextAPI.Detected += TextAPIDetected;
        }

        protected override void UnregisterComponent()
        {
            if(!Main.ComponentsRegistered)
                return;

            TextAPI.Detected -= TextAPIDetected;
        }

        void TextAPIDetected()
        {
            text = new HudAPIv2.HUDMessage(new StringBuilder(128 * MaxMessages), new Vector2D(-0.98, 0.98), Scale: TextScale, Shadowing: true, Blend: BlendType.PostPP);
            text.Visible = false;
            UpdateText();
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
        }

        public static void PrintHUD(object caller, string message)
        {
            var debugMsgs = BuildInfoMod.Instance.DebugLog.logList;

            if(debugMsgs.Count > MaxMessages)
                debugMsgs.Dequeue();

            int expiresAt = BuildInfoMod.Instance.Tick + (Constants.TICKS_PER_SECOND * MessageExpireSeconds);

            debugMsgs.Enqueue(new LogMsg(expiresAt, caller.GetType(), message));

            BuildInfoMod.Instance.DebugLog.UpdateText();
        }

        void UpdateText()
        {
            if(text == null)
                return;

            text.Visible = true;
            var sb = text.Message.Clear();

            foreach(var msg in logList)
            {
                sb.Color(Color.Gray).Append(msg.Type.Name).Append(": ").Color(Color.White).Append(msg.Message).Append('\n');
            }
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(logList.Count == 0)
                return;

            while(logList.Peek().ExpiresAtTick <= tick)
            {
                logList.Dequeue();
            }

            UpdateText();
        }

        struct LogMsg
        {
            public readonly int ExpiresAtTick;
            public readonly Type Type;
            public readonly string Message;

            public LogMsg(int expiresAtTick, Type type, string message)
            {
                ExpiresAtTick = expiresAtTick;
                Type = type;
                Message = message;
            }
        }
    }
}
#endif