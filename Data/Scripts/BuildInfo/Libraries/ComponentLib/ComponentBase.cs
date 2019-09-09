namespace Digi.ComponentLib
{
    public abstract class ComponentBase<TModBase> : IComponent where TModBase : IModBase
    {
        /// <summary>
        /// The parent main class.
        /// </summary>
        public readonly TModBase Main;

        /// <summary>
        /// Defines what Update*() methods will be called.
        /// </summary>
        public UpdateFlags UpdateMethods
        {
            get { return _flags; }
            set
            {
                if(value.IsSet(UpdateFlags.UPDATE_BEFORE_SIM) && !Main.SessionHasBeforeSim)
                    Log.Error($"{GetType().Name} component can't work with UPDATE_BEFORE_SIM because session is not set to call it.");

                if(value.IsSet(UpdateFlags.UPDATE_AFTER_SIM) && !Main.SessionHasAfterSim)
                    Log.Error($"{GetType().Name} Component can't work with UPDATE_AFTER_SIM because session is not set to call it.");

                if(_newFlags == value)
                    return;

                Main.ComponentScheduleRefresh(this);
                _newFlags = value;
            }
        }
        private UpdateFlags _flags = UpdateFlags.NONE;
        private UpdateFlags _newFlags = UpdateFlags.INVALID;

        /// <summary>
        /// Same as <see cref="UpdateMethods"/> but simpler for toggling.
        /// </summary>
        /// <param name="flag">The flag to change.</param>
        /// <param name="on">true to add, false to remove.</param>
        public void SetUpdateMethods(UpdateFlags flag, bool on)
        {
            if(on)
                UpdateMethods = UpdateMethods | flag;
            else
                UpdateMethods = UpdateMethods & ~flag;
        }

        /// <summary>
        /// Called in LoadData()
        /// </summary>
        /// <param name="main">Must be the main class.</param>
        protected ComponentBase(TModBase main)
        {
            Main = main;
            Main.ComponentAdd(this);
        }

        /// <summary>
        /// Called in BeforeStart().
        /// </summary>
        protected abstract void RegisterComponent();

        /// <summary>
        /// Called in UnloadData().
        /// </summary>
        protected abstract void UnregisterComponent();

        /// <summary>
        /// Called in HandleInput(), even when game is paused.
        /// Do not call base!
        /// </summary>
        protected virtual void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            Log.Error($"UpdateInput() is enabled but not overwritten for {GetType().Name} component!");
        }

        /// <summary>
        /// Called in UpdateBeforeSimulation(), respects pause.
        /// Do not call base!
        /// </summary>
        protected virtual void UpdateBeforeSim(int tick)
        {
            Log.Error($"UpdateBeforeSim() is enabled but not overwritten for {GetType().Name} component!");
        }

        /// <summary>
        /// Called in UpdateAfterSimulation(), respects pause.
        /// Do not call base!
        /// </summary>
        protected virtual void UpdateAfterSim(int tick)
        {
            Log.Error($"UpdateAfterSim() is enabled but not overwritten for {GetType().Name} component!");
        }

        /// <summary>
        /// Called in Draw(), even when game is paused.
        /// Do not call base!
        /// </summary>
        protected virtual void UpdateDraw()
        {
            Log.Error($"UpdateDraw() is enabled but not overwritten for {GetType().Name} component!");
        }

        void IComponent.RegisterComponent() => RegisterComponent();
        void IComponent.UnregisterComponent() => UnregisterComponent();
        void IComponent.UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused) => UpdateInput(anyKeyOrMouse, inMenu, paused);
        void IComponent.UpdateBeforeSim(int tick) => UpdateBeforeSim(tick);
        void IComponent.UpdateAfterSim(int tick) => UpdateAfterSim(tick);
        void IComponent.UpdateDraw() => UpdateDraw();
        void IComponent.RefreshFlags()
        {
            Main.ComponentSetNewFlags(this, _newFlags);
            _flags = _newFlags;
            _newFlags = UpdateFlags.INVALID;
        }
    }
}