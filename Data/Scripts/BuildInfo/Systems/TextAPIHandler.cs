using System;
using Digi.BuildInfo.Features;
using Digi.ComponentLib;
using Draygo.API;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Systems
{
    public class TextAPI : ClientComponent
    {
        /// <summary>
        /// Triggered when TextAPI is detected.
        /// </summary>
        public event Action Detected;

        /// <summary>
        /// If TextAPI is detected and user didn't opt out.
        /// </summary>
        public bool IsEnabled => WasDetected && Use;

        /// <summary>
        /// If TextAPI was detected being installed and running.
        /// </summary>
        public bool WasDetected { get; private set; } = false;

        /// <summary>
        /// False if user chose to not allow TextAPI.
        /// </summary>
        public bool Use
        {
            get { return _use; }
            set
            {
                _use = value;
                UseChanged?.Invoke(value);
            }
        }

        /// <summary>
        /// Triggered when <see cref="Use"/> changes.
        /// </summary>
        public event Action<bool> UseChanged;

        private HudAPIv2 api;
        private bool _use = true;

        public TextAPI(Client mod) : base(mod)
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
                    Log.Error("TextAPI sent the register event twice now! Please report to TextAPI author.", Log.PRINT_MSG);
                    return;
                }

                WasDetected = true;
                Detected?.Invoke();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
