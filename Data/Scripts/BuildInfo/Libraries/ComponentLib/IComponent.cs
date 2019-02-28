namespace Digi.ComponentLib
{
    public interface IComponent
    {
        void RegisterComponent();
        void UnregisterComponent();
        void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused);
        void UpdateBeforeSim(int tick);
        void UpdateAfterSim(int tick);
        void UpdateDraw();
        void RefreshFlags();
    }
}