using System;
using RichHudFramework.Client;

namespace Digi.BuildInfo.Systems
{
    public class RichHudFrameworkHandler : ModComponent
    {
        /// <summary>
        /// If RichHudFramework was detected and replied.
        /// </summary>
        public bool IsRunning = false;

        /// <summary>
        /// When RichHudFramework initialized.
        /// </summary>
        public event Action Initialized;

        public RichHudFrameworkHandler(BuildInfoMod main) : base(main)
        {
            RichHudClient.Init(Log.ModName, InitCallback, ResetCallback);
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            IsRunning = false;

            // gets called automatically
            //RichHudClient.Reset();
        }

        void InitCallback()
        {
            try
            {
                IsRunning = true;
                Initialized?.Invoke();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void ResetCallback()
        {
        }
    }
}