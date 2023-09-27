using System;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi.BuildInfo.Features.HUD
{
    public abstract class HudStatBase : IMyHudStat
    {
        // required by IMyHudStat
        public MyStringHash Id { get; private set; }
        public float CurrentValue { get; private set; } = 0f;
        public float MinValue { get; private set; } = 0f;
        public float MaxValue { get; private set; } = 1f;

        string CurrentValueString = null;

        /// <summary>
        /// Invoked if any of the 3 values are different.
        /// </summary>
        protected event Action ValuesChanged;

        /// <summary>
        /// Called per tick; mutate the ref values to change the data.
        /// </summary>
        protected abstract void UpdateBeforeSim(ref float current, ref float min, ref float max);

        /// <summary>
        /// Only gets called if value is different than last time.
        /// Return the relevant interpretation of <see cref="CurrentValue"/> as a string.
        /// </summary>
        protected abstract string ValueAsString();

        public const float CompareEpsilon = 0.00001f;

        protected BuildInfoMod Main { get; private set; }

        /// <summary>
        /// Input the stat id to override.
        /// It will be ignored if buildinfo is killed.
        /// </summary>
        public HudStatBase(string id)
        {
            if(!BuildInfo_GameSession.GetOrComputeIsKilled(this.GetType().Name))
            {
                Id = MyStringHash.GetOrCompute(id); // overwrites this stat's script
            }
        }

        protected void InvalidateStringCache()
        {
            CurrentValueString = null;
        }

        void IMyHudStat.Update()
        {
            if(BuildInfo_GameSession.IsKilled)
                return;

            try
            {
                if(MyAPIGateway.Session == null || MyAPIGateway.Session.GameplayFrameCounter <= 0)
                    return;

                // balanced between mod profiler hits and convenience

                Main = BuildInfoMod.Instance;

                float current = CurrentValue;
                float min = MinValue;
                float max = MaxValue;

                UpdateBeforeSim(ref current, ref min, ref max);

                bool anyChanged = false;

                if(Math.Abs(CurrentValue - current) > CompareEpsilon)
                {
                    CurrentValue = current;
                    CurrentValueString = null; // recalculate next time it's requested
                    anyChanged = true;
                }

                if(Math.Abs(MinValue - min) > CompareEpsilon)
                {
                    MinValue = min;
                    anyChanged = true;
                }

                if(Math.Abs(MaxValue - max) > CompareEpsilon)
                {
                    MaxValue = max;
                    anyChanged = true;
                }

                if(anyChanged)
                    ValuesChanged?.Invoke();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        string IMyHudStat.GetValueString()
        {
            try
            {
                if(CurrentValueString == null)
                {
                    Main = BuildInfoMod.Instance;
                    CurrentValueString = ValueAsString();
                }

                return CurrentValueString ?? "NULL"; // must never return null here as it causes weird bugs, like softlocking in MP connecting
            }
            catch(Exception e)
            {
                Log.Error(e);
                return "ERROR";
            }
        }
    }
}
