using System;
using System.Collections.Generic;
using Sandbox.ModAPI;

namespace Digi.BuildInfo.Systems
{
    public class DefenseShieldsDetector : ModComponent
    {
        /// <summary>
        /// If DefenseShields was detected and replied.
        /// </summary>
        public bool IsRunning = false;

        const long Channel = 1365616918;

        private bool Registered;

        public DefenseShieldsDetector(BuildInfoMod main) : base(main)
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(Channel, HandleMessage);
            MyAPIGateway.Utilities.SendModMessage(Channel, "ApiEndpointRequest");
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            IsRunning = false;
            MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, HandleMessage);
        }

        void HandleMessage(object o)
        {
            IReadOnlyDictionary<string, Delegate> dict = o as IReadOnlyDictionary<string, Delegate>;
            if(dict == null)
                return;

            IsRunning = true;
        }
    }
}