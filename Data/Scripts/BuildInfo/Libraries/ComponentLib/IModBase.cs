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

        void ComponentAdd(IComponent component);
        void ComponentScheduleRefresh(IComponent component);
        void ComponentSetNewFlags(IComponent component, UpdateFlags newFlags);

        bool SessionHasBeforeSim { get; }
        bool SessionHasAfterSim { get; }
    }
}