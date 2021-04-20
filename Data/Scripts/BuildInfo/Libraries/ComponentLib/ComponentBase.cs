using System;

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
            get { return (_newFlags != UpdateFlags.INVALID ? _newFlags : CurrentUpdateMethods); }
            set
            {
                if(value.IsSet(UpdateFlags.UPDATE_BEFORE_SIM) && !Main.SessionHasBeforeSim)
                    throw new CrashGameException($"Component '{GetType().FullName}' can't work with UPDATE_BEFORE_SIM because session is not set to call it.");

                if(value.IsSet(UpdateFlags.UPDATE_AFTER_SIM) && !Main.SessionHasAfterSim)
                    throw new CrashGameException($"Component '{GetType().FullName}' can't work with UPDATE_AFTER_SIM because session is not set to call it.");

                if(_newFlags != value)
                {
                    Main.ComponentScheduleRefresh(this);
                    _newFlags = value;
                }
            }
        }

        public int UpdateOrder { get; set; }

        public UpdateFlags CurrentUpdateMethods { get; private set; } = UpdateFlags.NONE;

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
        public abstract void RegisterComponent();

        /// <summary>
        /// Called in UnloadData().
        /// </summary>
        public abstract void UnregisterComponent();

        /// <summary>
        /// Called in HandleInput(), even when game is paused.
        /// Do not call base!
        /// </summary>
        public virtual void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            Log.Error($"UpdateInput() is enabled but not overwritten for {GetType().Name} component!");
        }

        /// <summary>
        /// Called in UpdateBeforeSimulation(), respects pause.
        /// Do not call base!
        /// </summary>
        public virtual void UpdateBeforeSim(int tick)
        {
            Log.Error($"UpdateBeforeSim() is enabled but not overwritten for {GetType().Name} component!");
        }

        /// <summary>
        /// Called in UpdateAfterSimulation(), respects pause.
        /// Do not call base!
        /// </summary>
        public virtual void UpdateAfterSim(int tick)
        {
            Log.Error($"UpdateAfterSim() is enabled but not overwritten for {GetType().Name} component!");
        }

        /// <summary>
        /// Called in Draw(), even when game is paused.
        /// Do not call base!
        /// </summary>
        public virtual void UpdateDraw()
        {
            Log.Error($"UpdateDraw() is enabled but not overwritten for {GetType().Name} component!");
        }

        void IComponent.RefreshFlags()
        {
            Main.ComponentSetNewFlags(this, _newFlags);
            CurrentUpdateMethods = _newFlags;
            _newFlags = UpdateFlags.INVALID;
        }
    }
}