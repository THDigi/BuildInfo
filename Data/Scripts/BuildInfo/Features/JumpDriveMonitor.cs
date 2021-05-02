using System.Collections.Generic;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using Sandbox.Game;

namespace Digi.BuildInfo.Features
{
    public class JumpDriveMonitor : ModComponent
    {
        private readonly Dictionary<long, int> JumpStartAt = new Dictionary<long, int>();
        private int CleanAtTick = -1;

        public JumpDriveMonitor(BuildInfoMod main) : base(main)
        {
        }

        /// <summary>
        /// Gets the seconds until the given grid's jumpdrives jump or -1 if not currently jumping.
        /// </summary>
        public float GetJumpCountdown(long gridEntityId)
        {
            if(JumpStartAt.Count == 0)
                return -1;

            int jumpsAt;
            if(!JumpStartAt.TryGetValue(gridEntityId, out jumpsAt))
                return -1;

            if(Main.Tick > jumpsAt)
                return -1;

            return (jumpsAt - Main.Tick) / (float)Constants.TICKS_PER_SECOND;
        }

        public override void RegisterComponent()
        {
            MyVisualScriptLogicProvider.GridJumped += JumpCountdownStart;
        }

        public override void UnregisterComponent()
        {
            MyVisualScriptLogicProvider.GridJumped -= JumpCountdownStart;
        }

        void JumpCountdownStart(long identityId, string gridName, long gridEntityId)
        {
            int jumpsAtTick = Main.Tick + (int)(Hardcoded.JumpDriveJumpDelay * Constants.TICKS_PER_SECOND);
            JumpStartAt[gridEntityId] = jumpsAtTick;
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);

            CleanAtTick = jumpsAtTick; // override clean at tick so it can only trigger once all jumpdrives are done
        }

        public override void UpdateAfterSim(int tick)
        {
            if(CleanAtTick > 0 && CleanAtTick <= tick)
            {
                CleanAtTick = -1;
                JumpStartAt.Clear();
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
            }
        }
    }
}
