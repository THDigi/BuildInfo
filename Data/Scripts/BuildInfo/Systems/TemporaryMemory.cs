using System;
using System.Collections.Generic;
using Digi.BuildInfo.Features;
using Digi.ComponentLib;
using Sandbox.ModAPI;
using TypeExtensions = VRage.TypeExtensions; // HACK: some people have ambiguity on this, probably linux or such

namespace Digi.BuildInfo.Systems
{
    /// <summary>
    /// To use, instantiate a <see cref="Container{T}"/>.
    /// </summary>
    public class TemporaryMemory : ModComponent
    {
        const bool Verbose = false;

        interface IContainer
        {
            bool Expired { get; }
            int ExpiresAt { get; }
            void AllocateOrExtend();
            void ReleaseData();
        }

        /// <summary>
        /// Usage:
        /// 1. Instantiate this with the large data type
        /// 2. call <see cref="AllocateOrExtend"/> right before using the data
        /// </summary>
        public class Container<T> : IContainer where T : class
        {
            public T Data { get; private set; }
            public int ExpiresAt { get; private set; }
            public bool Expired { get; private set; }

            readonly float AliveForMinutes;

            readonly Func<T> Allocate;

            public Container(Func<T> allocate, float aliveMinutes)
            {
                if(allocate == null)
                    throw new ArgumentException("Callback to allocate is required", nameof(allocate));

                Expired = true;
                Allocate = allocate;
                AliveForMinutes = aliveMinutes;
            }

            /// <summary>
            /// Required
            /// </summary>
            public void AllocateOrExtend()
            {
                if(Expired)
                {
                    Expired = false;
                    var handler = BuildInfoMod.Instance.TemporaryMemory;
                    handler.Storage.Add(this);
                    handler.SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);

                    Data = Allocate.Invoke();

                    if(Verbose)
                        DebugLog.PrintHUD(this, $"({TypeExtensions.PrettyName(typeof(T))}) <color=red>allocated!", log: true);
                }
                else
                {
                    if(Verbose)
                        DebugLog.PrintHUD(this, $"({TypeExtensions.PrettyName(typeof(T))}) memory life extended", log: true);
                }

                ExpiresAt = MyAPIGateway.Session.GameplayFrameCounter + (int)(Constants.TicksPerSecond * 60 * AliveForMinutes);
            }

            /// <summary>
            /// Does not need to be manually called.
            /// </summary>
            public void ReleaseData()
            {
                Expired = true;
                Data = null;

                if(Verbose)
                    DebugLog.PrintHUD(this, $"({TypeExtensions.PrettyName(typeof(T))}) <color=lime>expired", log: true);
            }
        }

        readonly List<IContainer> Storage = new List<IContainer>();

        public TemporaryMemory(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }

        public override void UpdateAfterSim(int tick)
        {
            if(tick % 60 == 0)
                return;

            for(int i = Storage.Count - 1; i >= 0; i--)
            {
                IContainer storage = Storage[i];
                if(storage.ExpiresAt <= tick)
                {
                    storage.ReleaseData();
                    Storage.RemoveAtFast(i);

                    if(Storage.Count == 0)
                        SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);
                }
            }
        }
    }
}
