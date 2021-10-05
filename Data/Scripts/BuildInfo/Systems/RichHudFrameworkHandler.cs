using System;
using System.Collections.Generic;
using Digi.BuildInfo.Utilities;
using RichHudFramework.Client;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.ObjectBuilders;
using WeaponCore.Api;

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
            Log.Info("RichHud handler registered");
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
                Log.Info("RichHud init");

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
            Log.Info("RichHud reset");
        }
    }
}