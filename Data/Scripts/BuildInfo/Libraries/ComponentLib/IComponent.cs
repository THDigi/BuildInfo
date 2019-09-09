namespace Digi.ComponentLib
{
    /// <summary>
    /// Component interface for internal use.
    /// </summary>
    public interface IComponent
    {
        UpdateFlags UpdateMethods { get; set; }
        UpdateFlags CurrentUpdateMethods { get; }
        void RegisterComponent();
        void UnregisterComponent();
        void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused);
        void UpdateBeforeSim(int tick);
        void UpdateAfterSim(int tick);
        void UpdateDraw();
        void RefreshFlags();
    }
}