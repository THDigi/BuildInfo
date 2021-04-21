using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace Digi.ComponentLib
{
    public class CrashGameException : Exception
    {
        public CrashGameException(string message) : base(message) { }
    }

    public abstract class ModBase<TModMain> : IModBase
        where TModMain : class, IModBase
    {
        public static TModMain Instance;

        /// <summary>
        /// The session component tying into the game API.
        /// </summary>
        public BuildInfo_GameSession Session;

        /// <summary>
        /// Gets set to true when world starts right after all components got registered.
        /// </summary>
        public bool ComponentsRegistered { get; private set; }

        /// <summary>
        /// Can be with or without render, but never MP client.
        /// </summary>
        public bool IsServer { get; private set; }

        /// <summary>
        /// Has renderer, can also be server.
        /// </summary>
        public bool IsPlayer { get; private set; }

        /// <summary>
        /// Has no renderer.
        /// </summary>
        public bool IsDedicatedServer { get; private set; }

        /// <summary>
        /// Wether simulation is paused.
        /// </summary>
        public bool IsPaused { get; private set; }

        public bool SessionHasBeforeSim { get; private set; }
        public bool SessionHasAfterSim { get; private set; }

        /// <summary>
        /// Simulation tick from server start.
        /// </summary>
        public int Tick;

        /// <summary>
        /// After all components registered.
        /// </summary>
        public event Action OnWorldStart;

        /// <summary>
        /// Before all components are unregistered.
        /// </summary>
        public event Action OnWorldExit;

        /// <summary>
        /// During world saving process (session.GetObjectBuilder())
        /// </summary>
        public event Action OnWorldSave;

        private readonly List<IComponent> Components = new List<IComponent>();
        private readonly List<IComponent> ComponentRefreshFlags = new List<IComponent>();
        private readonly List<IComponent> ComponentUpdateInput = new List<IComponent>();
        private readonly List<IComponent> ComponentUpdateBeforeSim = new List<IComponent>();
        private readonly List<IComponent> ComponentUpdateAfterSim = new List<IComponent>();
        private readonly List<IComponent> ComponentUpdateDraw = new List<IComponent>();

        private readonly bool RunCriticalOnInput;
        private readonly bool RunCriticalOnBeforeSim;
        private readonly bool RunCriticalOnAfterSim;

        protected ModBase(string modName, BuildInfo_GameSession session, MyUpdateOrder sessionUpdates)
        {
            Log.ModName = modName;
            Log.AutoClose = false;
            IsServer = MyAPIGateway.Multiplayer.IsServer;
            IsDedicatedServer = (IsServer && MyAPIGateway.Utilities.IsDedicated);
            IsPlayer = !IsDedicatedServer;

            Session = session;
            Instance = (TModMain)(IModBase)this;

            Session.SetUpdateOrder(sessionUpdates);

            SessionHasBeforeSim = (Session.UpdateOrder & MyUpdateOrder.BeforeSimulation) != 0;
            SessionHasAfterSim = (Session.UpdateOrder & MyUpdateOrder.AfterSimulation) != 0;

            if((Session.UpdateOrder & MyUpdateOrder.Simulation) != 0)
                throw new CrashGameException("MyUpdateOrder.Simulation not supported by ComponentLib!");

            if(!SessionHasBeforeSim && !SessionHasAfterSim)
                throw new CrashGameException($"Neither BeforeSim nor AfterSim are set in session, this will break Tick and ALL component updating on DS side!");

            // DS doesn't call HandleInput() so the critical updates have to go in the next registered sim update
            RunCriticalOnInput = IsPlayer;
            RunCriticalOnBeforeSim = !RunCriticalOnInput && SessionHasBeforeSim;
            RunCriticalOnAfterSim = !RunCriticalOnInput && !RunCriticalOnBeforeSim && SessionHasAfterSim;

            if(!RunCriticalOnInput && !RunCriticalOnBeforeSim && !RunCriticalOnAfterSim)
                throw new CrashGameException("No critical run executing!");
        }

        void IModBase.WorldStart()
        {
            for(int i = 0; i < Components.Count; ++i)
            {
                try
                {
                    Components[i].RegisterComponent();
                }
                catch(Exception e)
                {
                    if(e is CrashGameException)
                        throw e;

                    Log.Error($"Exception during {Components[i].GetType().Name}.RegisterComponent(): {e.Message}", Log.PRINT_MESSAGE);
                    Log.Error(e);
                }
            }

            ComponentsRegistered = true;
            OnWorldStart?.Invoke();
        }

        void IModBase.WorldExit()
        {
            try
            {
                OnWorldExit?.Invoke();

                if(ComponentsRegistered)
                {
                    for(int i = 0; i < Components.Count; ++i)
                    {
                        try
                        {
                            Components[i].UnregisterComponent();
                        }
                        catch(Exception e)
                        {
                            if(e is CrashGameException)
                                throw e;

                            Log.Error($"Exception during {Components[i].GetType().Name}.UnregisterComponent(): {e.Message}", Log.PRINT_MESSAGE);
                            Log.Error(e);
                        }
                    }
                }

                Components.Clear();
                ComponentRefreshFlags.Clear();
                ComponentUpdateInput.Clear();
                ComponentUpdateBeforeSim.Clear();
                ComponentUpdateAfterSim.Clear();
                ComponentUpdateDraw.Clear();
            }
            finally
            {
                Session = null;
                Instance = null;
                ComponentsRegistered = false;
            }
        }

        void IModBase.UpdateInput()
        {
            try
            {
                if(IsDedicatedServer)
                    return; // just making sure

                if(RunCriticalOnInput)
                {
                    if(!IsPaused)
                    {
                        unchecked
                        {
                            ++Tick;
                        }
                    }

                    if(ComponentRefreshFlags.Count > 0)
                    {
                        for(int i = 0; i < ComponentRefreshFlags.Count; i++)
                        {
                            ComponentRefreshFlags[i].RefreshFlags();
                        }

                        ComponentRefreshFlags.Clear();
                    }
                }

                int comps = ComponentUpdateInput.Count;
                if(comps > 0)
                {
                    bool inMenu = MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible;
                    bool anyKeyOrMouse = MyAPIGateway.Input.IsAnyKeyPress() || MyAPIGateway.Input.IsAnyMousePressed();
                    bool paused = IsPaused;

                    for(int i = 0; i < comps; ++i)
                    {
                        try
                        {
                            ComponentUpdateInput[i].UpdateInput(anyKeyOrMouse, inMenu, paused);
                        }
                        catch(Exception e)
                        {
                            if(e is CrashGameException)
                                throw e;

                            Log.Error($"Exception during {ComponentUpdateInput[i].GetType().Name}.UpdateInput(): {e.Message}", Log.PRINT_MESSAGE);
                            Log.Error(e);
                        }
                    }
                }
            }
            catch(Exception e)
            {
                if(e is CrashGameException)
                    throw e;

                Log.Error(e);
            }
        }

        void IModBase.UpdateBeforeSim()
        {
            try
            {
                IsPaused = false;

                if(RunCriticalOnBeforeSim)
                {
                    unchecked
                    {
                        ++Tick;
                    }

                    if(ComponentRefreshFlags.Count > 0)
                    {
                        for(int i = 0; i < ComponentRefreshFlags.Count; i++)
                        {
                            ComponentRefreshFlags[i].RefreshFlags();
                        }

                        ComponentRefreshFlags.Clear();
                    }
                }

                for(int i = 0; i < ComponentUpdateBeforeSim.Count; ++i)
                {
                    try
                    {
                        ComponentUpdateBeforeSim[i].UpdateBeforeSim(Tick);
                    }
                    catch(Exception e)
                    {
                        if(e is CrashGameException)
                            throw e;

                        Log.Error($"Exception during {ComponentUpdateBeforeSim[i].GetType().Name}.UpdateBeforeSim(): {e.Message}", Log.PRINT_MESSAGE);
                        Log.Error(e);
                    }
                }
            }
            catch(Exception e)
            {
                if(e is CrashGameException)
                    throw e;

                Log.Error(e);
            }
        }

        void IModBase.UpdateAfterSim()
        {
            try
            {
                IsPaused = false;

                if(RunCriticalOnAfterSim)
                {
                    unchecked
                    {
                        ++Tick;
                    }

                    if(ComponentRefreshFlags.Count > 0)
                    {
                        for(int i = 0; i < ComponentRefreshFlags.Count; i++)
                        {
                            ComponentRefreshFlags[i].RefreshFlags();
                        }

                        ComponentRefreshFlags.Clear();
                    }
                }

                for(int i = 0; i < ComponentUpdateAfterSim.Count; ++i)
                {
                    try
                    {
                        ComponentUpdateAfterSim[i].UpdateAfterSim(Tick);
                    }
                    catch(Exception e)
                    {
                        if(e is CrashGameException)
                            throw e;

                        Log.Error($"Exception during {ComponentUpdateAfterSim[i].GetType().Name}.UpdateAfterSim(): {e.Message}", Log.PRINT_MESSAGE);
                        Log.Error(e);
                    }
                }
            }
            catch(Exception e)
            {
                if(e is CrashGameException)
                    throw e;

                Log.Error(e);
            }
        }

        void IModBase.UpdateDraw()
        {
            try
            {
                if(IsDedicatedServer)
                    return; // just making sure

                for(int i = 0; i < ComponentUpdateDraw.Count; ++i)
                {
                    try
                    {
                        ComponentUpdateDraw[i].UpdateDraw();
                    }
                    catch(Exception e)
                    {
                        if(e is CrashGameException)
                            throw e;

                        Log.Error($"Exception during {ComponentUpdateDraw[i].GetType().Name}.Draw(): {e.Message}", Log.PRINT_MESSAGE);
                        Log.Error(e);
                    }
                }
            }
            catch(Exception e)
            {
                if(e is CrashGameException)
                    throw e;

                Log.Error(e);
            }
        }

        void IModBase.UpdateStopped()
        {
            IsPaused = true;
        }

        void IModBase.WorldSave()
        {
            try
            {
                OnWorldSave?.Invoke();
            }
            catch(Exception e)
            {
                if(e is CrashGameException)
                    throw e;

                Log.Error(e);
            }
        }

        void IModBase.ComponentAdd(IComponent component)
        {
            Components.Add(component);
        }

        void IModBase.ComponentScheduleRefresh(IComponent component)
        {
            if(!ComponentRefreshFlags.Contains(component))
            {
                ComponentRefreshFlags.Add(component);
            }
        }

        void IModBase.ComponentSetNewFlags(IComponent component, UpdateFlags newFlags)
        {
            UpdateList(component, ComponentUpdateInput, newFlags, UpdateFlags.UPDATE_INPUT);
            UpdateList(component, ComponentUpdateBeforeSim, newFlags, UpdateFlags.UPDATE_BEFORE_SIM);
            UpdateList(component, ComponentUpdateAfterSim, newFlags, UpdateFlags.UPDATE_AFTER_SIM);
            UpdateList(component, ComponentUpdateDraw, newFlags, UpdateFlags.UPDATE_DRAW);
        }

        void UpdateList(IComponent component, List<IComponent> list, UpdateFlags newFlags, UpdateFlags flag)
        {
            if(component.CurrentUpdateMethods.IsSet(flag) && !newFlags.IsSet(flag))
            {
                list.Remove(component);
            }
            else if(!component.CurrentUpdateMethods.IsSet(flag) && newFlags.IsSet(flag))
            {
                list.Add(component);
            }

            list.Sort((a, b) => a.UpdateOrder.CompareTo(b.UpdateOrder));
        }
    }
}