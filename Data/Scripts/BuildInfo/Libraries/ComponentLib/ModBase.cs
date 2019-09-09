using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace Digi.ComponentLib
{
    public abstract class ModBase<TModMain> : IModBase
        where TModMain : class, IModBase
    {
        public static TModMain Instance;

        /// <summary>
        /// The session component tying into the game API.
        /// </summary>
        public BuildInfo_GameSession Session;

        /// <summary>
        /// Can be with or without render, but never MP client.
        /// </summary>
        public bool IsServer = false;
        bool IModBase.IsServer => IsServer;

        /// <summary>
        /// Has renderer, can also be server.
        /// </summary>
        public bool IsPlayer = false;
        bool IModBase.IsPlayer => IsPlayer;

        /// <summary>
        /// Has no renderer.
        /// </summary>
        public bool IsDedicatedServer = false;
        bool IModBase.IsDedicatedServer => IsDedicatedServer;

        /// <summary>
        /// Wether simulation is paused.
        /// </summary>
        public bool IsPaused => Session.Paused;

        public bool SessionHasBeforeSim => (Session.UpdateOrder & MyUpdateOrder.BeforeSimulation) != 0;
        public bool SessionHasAfterSim => (Session.UpdateOrder & MyUpdateOrder.AfterSimulation) != 0;

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

        private Action RunCriticalOnInput;
        private Action RunCriticalOnBeforeSim;
        private Action RunCriticalOnAfterSim;

        protected ModBase(string modName, BuildInfo_GameSession session)
        {
            Log.ModName = modName;
            Log.AutoClose = false;
            IsServer = MyAPIGateway.Multiplayer.IsServer;
            IsDedicatedServer = (IsServer && MyAPIGateway.Utilities.IsDedicated);
            IsPlayer = !IsDedicatedServer;

            Session = session;
            Instance = (TModMain)(IModBase)this;
        }

        void IModBase.WorldStart()
        {
            try
            {
                if((Session.UpdateOrder & MyUpdateOrder.Simulation) != 0)
                    throw new Exception("MyUpdateOrder.Simulation not supported by ComponentLib!");

                if(!SessionHasBeforeSim && !SessionHasAfterSim)
                    throw new Exception($"Neither BeforeSim nor AfterSim are set in session, this will break Tick and ALL component updating on DS side!");

                RunCriticalOnInput = null;
                RunCriticalOnBeforeSim = null;
                RunCriticalOnAfterSim = null;

                if(!IsDedicatedServer)
                {
                    RunCriticalOnInput = RunCriticalUpdate;
                }
                else // DS doesn't call HandleInput() so the critical updates have to go in the next registered sim update
                {
                    if(SessionHasBeforeSim)
                    {
                        RunCriticalOnBeforeSim = RunCriticalUpdate;
                    }
                    else if(SessionHasAfterSim)
                    {
                        RunCriticalOnAfterSim = RunCriticalUpdate;
                    }
                }

                for(int i = 0; i < Components.Count; ++i)
                {
                    try
                    {
                        Components[i].RegisterComponent();
                    }
                    catch(Exception e)
                    {
                        Log.Error($"Exception during {Components[i].GetType().Name}.RegisterComponent(): {e.Message}", Log.PRINT_MSG);
                        Log.Error(e);
                    }
                }

                OnWorldStart?.Invoke();
            }
            catch(Exception)
            {
                Instance.WorldExit();
                throw;
            }
        }

        void IModBase.WorldExit()
        {
            try
            {
                OnWorldExit?.Invoke();

                for(int i = 0; i < Components.Count; ++i)
                {
                    try
                    {
                        Components[i].UnregisterComponent();
                    }
                    catch(Exception e)
                    {
                        Log.Error($"Exception during {Components[i].GetType().Name}.UnregisterComponent(): {e.Message}", Log.PRINT_MSG);
                        Log.Error(e);
                    }
                }

                Components.Clear();
                ComponentRefreshFlags.Clear();
                ComponentUpdateInput.Clear();
                ComponentUpdateBeforeSim.Clear();
                ComponentUpdateAfterSim.Clear();
                ComponentUpdateDraw.Clear();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
            finally
            {
                Session = null;
                Instance = null;
                Log.Close();
            }
        }

        void IModBase.UpdateInput()
        {
            try
            {
                if(IsDedicatedServer)
                    return; // just making sure

                RunCriticalOnInput?.Invoke();

                int comps = ComponentUpdateInput.Count;
                if(comps > 0)
                {
                    bool inMenu = MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible;
                    bool anyKeyOrMouse = MyAPIGateway.Input.IsAnyKeyPress() || MyAPIGateway.Input.IsAnyMousePressed();
                    bool paused = Session.Paused;

                    for(int i = 0; i < comps; ++i)
                    {
                        ComponentUpdateInput[i].UpdateInput(anyKeyOrMouse, inMenu, paused);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void IModBase.UpdateBeforeSim()
        {
            try
            {
                RunCriticalOnBeforeSim?.Invoke();

                for(int i = 0; i < ComponentUpdateBeforeSim.Count; ++i)
                {
                    ComponentUpdateBeforeSim[i].UpdateBeforeSim(Tick);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void IModBase.UpdateAfterSim()
        {
            try
            {
                RunCriticalOnAfterSim?.Invoke();

                for(int i = 0; i < ComponentUpdateAfterSim.Count; ++i)
                {
                    ComponentUpdateAfterSim[i].UpdateAfterSim(Tick);
                }
            }
            catch(Exception e)
            {
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
                    ComponentUpdateDraw[i].UpdateDraw();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void IModBase.WorldSave()
        {
            OnWorldSave?.Invoke();
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
            if(component.UpdateMethods.IsSet(flag) && !newFlags.IsSet(flag))
            {
                list.Remove(component);
            }
            else if(!component.UpdateMethods.IsSet(flag) && newFlags.IsSet(flag))
            {
                list.Add(component);
            }
        }

        void RunCriticalUpdate()
        {
            unchecked
            {
                ++Tick;
            }

            if(ComponentRefreshFlags.Count > 0)
            {
                foreach(var comp in ComponentRefreshFlags)
                {
                    comp.RefreshFlags();
                }

                ComponentRefreshFlags.Clear();
            }
        }
    }
}