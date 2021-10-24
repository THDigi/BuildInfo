using System;
using System.Collections.Generic;
using System.Diagnostics;
using VRage;

namespace Digi.ComponentLib
{
    /// <summary>
    /// Main class interface for internal use.
    /// </summary>
    public interface IModBase
    {
        bool IsServer { get; }
        bool IsDedicatedServer { get; }
        bool IsPlayer { get; }
        bool IsPaused { get; }

        void WorldStart();
        void WorldExit();
        void UpdateInput();
        void UpdateBeforeSim();
        void UpdateAfterSim();
        void UpdateDraw();
        void WorldSave();
        void UpdateStopped();

        void ComponentAdd(IComponent component);
        void ComponentScheduleRefresh(IComponent component);
        void ComponentSetNewFlags(IComponent component, UpdateFlags newFlags);

        bool SessionHasBeforeSim { get; }
        bool SessionHasAfterSim { get; }

        bool Profile { get; set; }
        void FinishProfilingConstructors();
    }
}