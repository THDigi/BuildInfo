using System;
using Draygo.API;

namespace Digi.BuildInfo.Systems
{
    public class TextAPI : ModComponent
    {
        /// <summary>
        /// Triggered when TextAPI is detected.
        /// </summary>
        public event Action Detected;

        /// <summary>
        /// If TextAPI is detected and user didn't opt out.
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// If TextAPI was detected being installed and running.
        /// </summary>
        public bool WasDetected { get; private set; }

        /// <summary>
        /// False if user chose to not allow TextAPI.
        /// </summary>
        public bool Use
        {
            get { return _use; }
            set
            {
                _use = value;
                IsEnabled = WasDetected && value;
                UseChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// Triggered when <see cref="Use"/> changes.
        /// </summary>
        public event Action<bool> UseChanged;

        private HudAPIv2 api;
        private bool _use = true;

        public TextAPI(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            api = new HudAPIv2(TextAPIDetected);
        }

        public override void UnregisterComponent()
        {
            api?.Close();
            api = null;
        }

        private void TextAPIDetected()
        {
            try
            {
                if(WasDetected)
                {
                    Log.Error("TextAPI sent the register event twice now! Please report to TextAPI author.", Log.PRINT_MESSAGE);
                    return;
                }

                WasDetected = true;
                IsEnabled = Use;
                Detected?.Invoke();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
