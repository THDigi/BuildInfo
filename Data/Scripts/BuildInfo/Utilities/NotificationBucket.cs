using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace Digi.BuildInfo.Utilities
{
    /// <summary>
    /// A group of persistent notifications that can be used with a custom enum
    /// </summary>
    /// <typeparam name="T">An enum for the channel</typeparam>
    public class NotificationBucket<T> where T : struct
    {
        readonly Dictionary<T, IMyHudNotification> Lookup = new Dictionary<T, IMyHudNotification>();

        public NotificationBucket()
        {
            // left here to crash if something is wrong
            T[] values = (T[])Enum.GetValues(typeof(T));

            // no real need
            //foreach(T val in values)
            //{
            //    Lookup.Add(val, MyAPIGateway.Utilities.CreateNotification(string.Empty));
            //}
        }

        public void Configure(T channel, int aliveTimeMs = 2000, string font = FontsHandler.WhiteSh)
        {
            IMyHudNotification notify;
            if(!Lookup.TryGetValue(channel, out notify))
            {
                notify = MyAPIGateway.Utilities.CreateNotification(string.Empty);
                Lookup.Add(channel, notify);
            }

            notify.Font = font;
            notify.AliveTime = aliveTimeMs;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <param name="tempAliveOverride">optional, temporary alive time for this message only</param>
        /// <param name="tempFontOverride">optional; temporary font for this message only</param>
        public void Show(T channel, string message, int? tempAliveOverride = null, string tempFontOverride = null)
        {
            // TODO: is this required?
            //if(Main.IsPaused)
            //    return;

            IMyHudNotification notify;
            if(!Lookup.TryGetValue(channel, out notify))
            {
                notify = MyAPIGateway.Utilities.CreateNotification(string.Empty);
                Lookup.Add(channel, notify);
            }

            notify.Hide(); // HACK: required to avoid bugs with notification not updating

            if(tempFontOverride != null)
            {
                string prev = notify.Font;
                notify.Font = tempFontOverride;
                tempFontOverride = prev;
            }

            if(tempAliveOverride != null)
            {
                int prev = notify.AliveTime;
                notify.AliveTime = tempAliveOverride.Value;
                tempAliveOverride = prev;
            }

            notify.Text = message;
            notify.Show();

            if(tempAliveOverride != null)
                notify.AliveTime = tempAliveOverride.Value;

            if(tempFontOverride != null)
                notify.Font = tempFontOverride;
        }

        public void Hide(T channel)
        {
            IMyHudNotification notify;
            if(Lookup.TryGetValue(channel, out notify))
            {
                notify.Hide();
            }
        }

        public void HideAll()
        {
            foreach(var notify in Lookup.Values)
            {
                notify.Hide();
            }
        }
    }
}
