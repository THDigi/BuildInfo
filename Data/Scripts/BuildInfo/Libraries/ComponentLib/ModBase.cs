using System;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.ModAPI;
using VRage.Game;
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
        /// NOTE: requires ModBase to have simulation updates.
        /// </summary>
        public bool IsPaused { get; private set; }

        public bool SessionHasBeforeSim { get; private set; }

        public bool SessionHasAfterSim { get; private set; }

        /// <summary>
        /// Simulation tick from session start on local machine.
        /// </summary>
        public int Tick;

        public static bool IsLocalMod { get; private set; }

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

        /// <summary>
        /// Called when Draw() call finishes running all components... mainly used for profiler draw.
        /// </summary>
        public event Action OnDrawEnd;

        public readonly List<IComponent> Components = new List<IComponent>();
        private readonly HashSet<IComponent> ComponentRefreshFlags = new HashSet<IComponent>();
        public readonly List<IComponent> ComponentUpdateInput = new List<IComponent>();
        public readonly List<IComponent> ComponentUpdateBeforeSim = new List<IComponent>();
        public readonly List<IComponent> ComponentUpdateAfterSim = new List<IComponent>();
        public readonly List<IComponent> ComponentUpdateDraw = new List<IComponent>();

        private readonly bool RunCriticalOnInput;
        private readonly bool RunCriticalOnBeforeSim;
        private readonly bool RunCriticalOnAfterSim;

        public bool Profile { get; set; } = false;
        public const double NewMeasureWeight = 0.01;
        public const string MeasureFormat = "0.000000";

        string MeasuredFor { get; set; }
        readonly Stopwatch Stopwatch = new Stopwatch();
        readonly List<ProfileData> MeasuredResults = new List<ProfileData>();

        readonly Stopwatch StopwatchRoot = new Stopwatch();
        public readonly ProfileUpdates RootMeasurements = new ProfileUpdates();

        protected ModBase(string modName, BuildInfo_GameSession session, MyUpdateOrder sessionUpdates)
        {
            MeasuredFor = "ModBase";
            Stopwatch.Restart();

            // mod is a local mod
            foreach(MyObjectBuilder_Checkpoint.ModItem mod in MyAPIGateway.Session.Mods)
            {
                if(mod.PublishedFileId == 0 && mod.Name == session.ModContext.ModId)
                {
                    IsLocalMod = true;
                    break;
                }
            }

            Profile = true; // IsLocalMod;

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

        void IModBase.FinishProfilingConstructors()
        {
            if(Profile)
            {
                // add last ctor's results
                Stopwatch.Stop();
                MeasuredResults.Add(new ProfileData(MeasuredFor, Stopwatch.Elapsed.TotalMilliseconds));

                MeasuredResults.Sort((a, b) => b.MeasuredMs.CompareTo(a.MeasuredMs)); // sort descending

                double totalMs = 0;
                foreach(ProfileData data in MeasuredResults)
                {
                    totalMs += data.MeasuredMs;
                }

                Log.Info($"Profiled component constructors, total: {totalMs.ToString(ModBase<IModBase>.MeasureFormat)} ms");
                Log.IncreaseIndent();
                foreach(ProfileData data in MeasuredResults)
                {
                    Log.Info($"{data.MeasuredMs.ToString(ModBase<IModBase>.MeasureFormat)} ms for {data.Name}");
                }
                Log.DecreaseIndent();

                MeasuredResults.Clear();
            }
        }

        void IModBase.WorldStart()
        {
            if(Profile)
            {
                MeasuredResults.Clear();
            }

            for(int i = 0; i < Components.Count; ++i)
            {
                try
                {
                    IComponent comp = Components[i];

                    if(Profile)
                    {
                        Stopwatch.Restart();

                        comp.RegisterComponent();

                        Stopwatch.Stop();
                        MeasuredResults.Add(new ProfileData($"{comp.GetType().Name}.RegisterComponent()", Stopwatch.Elapsed.TotalMilliseconds));
                    }
                    else
                    {
                        comp.RegisterComponent();
                    }
                }
                catch(Exception e)
                {
                    if(e is CrashGameException)
                        throw e;

                    string msg = $"Exception during {Components[i].GetType().Name}.RegisterComponent()";
                    Log.Error($"{msg}: {e}", msg);
                }
            }

            ComponentsRegistered = true;

            if(Profile)
            {
                Stopwatch.Restart();

                OnWorldStart?.Invoke();

                Stopwatch.Stop();
                MeasuredResults.Add(new ProfileData("OnWorldStart event", Stopwatch.Elapsed.TotalMilliseconds));

                MeasuredResults.Sort((a, b) => b.MeasuredMs.CompareTo(a.MeasuredMs)); // sort descending

                double totalMs = 0;
                foreach(ProfileData data in MeasuredResults)
                {
                    totalMs += data.MeasuredMs;
                }

                Log.Info($"Profiled component registering, total: {totalMs.ToString(MeasureFormat)} ms");
                Log.IncreaseIndent();
                foreach(ProfileData data in MeasuredResults)
                {
                    Log.Info($"{data.MeasuredMs.ToString(MeasureFormat)} ms for {data.Name}");
                }
                Log.DecreaseIndent();
            }
            else
            {
                OnWorldStart?.Invoke();
            }
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

                            string msg = $"Exception during {Components[i].GetType().Name}.UnregisterComponent()";
                            Log.Error($"{msg}: {e}", msg);
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
                if(Profile)
                    StopwatchRoot.Restart();

                if(IsDedicatedServer || !ComponentsRegistered)
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
                        foreach(IComponent comp in ComponentRefreshFlags)
                        {
                            comp.RefreshFlags();
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

                    if(Profile)
                    {
                        for(int i = 0; i < comps; ++i)
                        {
                            try
                            {
                                Stopwatch.Restart();

                                IComponent comp = ComponentUpdateInput[i];
                                comp.UpdateInput(anyKeyOrMouse, inMenu, paused);

                                Stopwatch.Stop();
                                double ms = Stopwatch.Elapsed.TotalMilliseconds;
                                ProfileMeasure profiled = comp.Profiled.MeasuredInput;
                                profiled.Min = Math.Min(profiled.Min, ms);
                                profiled.Max = Math.Max(profiled.Max, ms);
                                profiled.MovingAvg = (profiled.MovingAvg * (1 - NewMeasureWeight)) + (ms * NewMeasureWeight);
                            }
                            catch(Exception e)
                            {
                                if(e is CrashGameException)
                                    throw e;

                                string msg = $"Exception during {ComponentUpdateInput[i].GetType().Name}.UpdateInput()";
                                Log.Error($"{msg}: {e}", msg);
                            }
                        }
                    }
                    else
                    {
                        for(int i = 0; i < comps; ++i)
                        {
                            try
                            {
                                IComponent comp = ComponentUpdateInput[i];
                                comp.UpdateInput(anyKeyOrMouse, inMenu, paused);
                            }
                            catch(Exception e)
                            {
                                if(e is CrashGameException)
                                    throw e;

                                string msg = $"Exception during {ComponentUpdateInput[i].GetType().Name}.UpdateInput()";
                                Log.Error($"{msg}: {e}", msg);
                            }
                        }
                    }
                }

                if(Profile)
                {
                    StopwatchRoot.Stop();
                    double ms = StopwatchRoot.Elapsed.TotalMilliseconds;
                    ProfileMeasure profiled = RootMeasurements.MeasuredInput;
                    profiled.Min = Math.Min(profiled.Min, ms);
                    profiled.Max = Math.Max(profiled.Max, ms);
                    profiled.MovingAvg = (profiled.MovingAvg * (1 - NewMeasureWeight)) + (ms * NewMeasureWeight);
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
                if(Profile)
                    StopwatchRoot.Restart();

                IsPaused = false;

                if(RunCriticalOnBeforeSim)
                {
                    unchecked
                    {
                        ++Tick;
                    }

                    if(ComponentRefreshFlags.Count > 0)
                    {
                        foreach(IComponent comp in ComponentRefreshFlags)
                        {
                            comp.RefreshFlags();
                        }

                        ComponentRefreshFlags.Clear();
                    }
                }

                int comps = ComponentUpdateBeforeSim.Count;
                if(comps > 0)
                {
                    if(Profile)
                    {
                        for(int i = 0; i < comps; ++i)
                        {
                            try
                            {
                                Stopwatch.Restart();

                                IComponent comp = ComponentUpdateBeforeSim[i];
                                comp.UpdateBeforeSim(Tick);

                                Stopwatch.Stop();
                                double ms = Stopwatch.Elapsed.TotalMilliseconds;
                                ProfileMeasure profiled = comp.Profiled.MeasuredBeforeSim;
                                profiled.Min = Math.Min(profiled.Min, ms);
                                profiled.Max = Math.Max(profiled.Max, ms);
                                profiled.MovingAvg = (profiled.MovingAvg * (1 - NewMeasureWeight)) + (ms * NewMeasureWeight);
                            }
                            catch(Exception e)
                            {
                                if(e is CrashGameException)
                                    throw e;

                                string msg = $"Exception during {ComponentUpdateBeforeSim[i].GetType().Name}.UpdateBeforeSim()";
                                Log.Error($"{msg}: {e}", msg);
                            }
                        }
                    }
                    else
                    {
                        for(int i = 0; i < comps; ++i)
                        {
                            try
                            {
                                IComponent comp = ComponentUpdateBeforeSim[i];
                                comp.UpdateBeforeSim(Tick);
                            }
                            catch(Exception e)
                            {
                                if(e is CrashGameException)
                                    throw e;

                                string msg = $"Exception during {ComponentUpdateBeforeSim[i].GetType().Name}.UpdateBeforeSim()";
                                Log.Error($"{msg}: {e}", msg);
                            }
                        }
                    }
                }

                if(Profile)
                {
                    StopwatchRoot.Stop();
                    double ms = StopwatchRoot.Elapsed.TotalMilliseconds;
                    ProfileMeasure profiled = RootMeasurements.MeasuredBeforeSim;
                    profiled.Min = Math.Min(profiled.Min, ms);
                    profiled.Max = Math.Max(profiled.Max, ms);
                    profiled.MovingAvg = (profiled.MovingAvg * (1 - NewMeasureWeight)) + (ms * NewMeasureWeight);
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
                if(Profile)
                    StopwatchRoot.Restart();

                IsPaused = false;

                if(RunCriticalOnAfterSim)
                {
                    unchecked
                    {
                        ++Tick;
                    }

                    if(ComponentRefreshFlags.Count > 0)
                    {
                        foreach(IComponent comp in ComponentRefreshFlags)
                        {
                            comp.RefreshFlags();
                        }

                        ComponentRefreshFlags.Clear();
                    }
                }

                int comps = ComponentUpdateAfterSim.Count;
                if(comps > 0)
                {
                    if(Profile)
                    {
                        for(int i = 0; i < comps; ++i)
                        {
                            try
                            {
                                Stopwatch.Restart();

                                IComponent comp = ComponentUpdateAfterSim[i];
                                comp.UpdateAfterSim(Tick);

                                Stopwatch.Stop();
                                double ms = Stopwatch.Elapsed.TotalMilliseconds;
                                ProfileMeasure profiled = comp.Profiled.MeasuredAfterSim;
                                profiled.Min = Math.Min(profiled.Min, ms);
                                profiled.Max = Math.Max(profiled.Max, ms);
                                profiled.MovingAvg = (profiled.MovingAvg * (1 - NewMeasureWeight)) + (ms * NewMeasureWeight);
                            }
                            catch(Exception e)
                            {
                                if(e is CrashGameException)
                                    throw e;

                                string msg = $"Exception during {ComponentUpdateAfterSim[i].GetType().Name}.UpdateAfterSim()";
                                Log.Error($"{msg}: {e}", msg);
                            }
                        }
                    }
                    else
                    {
                        for(int i = 0; i < comps; ++i)
                        {
                            try
                            {
                                IComponent comp = ComponentUpdateAfterSim[i];
                                comp.UpdateAfterSim(Tick);
                            }
                            catch(Exception e)
                            {
                                if(e is CrashGameException)
                                    throw e;

                                string msg = $"Exception during {ComponentUpdateAfterSim[i].GetType().Name}.UpdateAfterSim()";
                                Log.Error($"{msg}: {e}", msg);
                            }
                        }
                    }
                }

                if(Profile)
                {
                    StopwatchRoot.Stop();
                    double ms = StopwatchRoot.Elapsed.TotalMilliseconds;
                    ProfileMeasure profiled = RootMeasurements.MeasuredAfterSim;
                    profiled.Min = Math.Min(profiled.Min, ms);
                    profiled.Max = Math.Max(profiled.Max, ms);
                    profiled.MovingAvg = (profiled.MovingAvg * (1 - NewMeasureWeight)) + (ms * NewMeasureWeight);
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
                if(Profile)
                    StopwatchRoot.Restart();

                if(IsDedicatedServer)
                    return; // just making sure

                int comps = ComponentUpdateDraw.Count;
                if(comps > 0)
                {
                    if(Profile)
                    {
                        for(int i = 0; i < comps; ++i)
                        {
                            try
                            {
                                Stopwatch.Restart();

                                IComponent comp = ComponentUpdateDraw[i];
                                comp.UpdateDraw();

                                Stopwatch.Stop();
                                double ms = Stopwatch.Elapsed.TotalMilliseconds;
                                ProfileMeasure profiled = comp.Profiled.MeasuredDraw;
                                profiled.Min = Math.Min(profiled.Min, ms);
                                profiled.Max = Math.Max(profiled.Max, ms);
                                profiled.MovingAvg = (profiled.MovingAvg * (1 - NewMeasureWeight)) + (ms * NewMeasureWeight);
                            }
                            catch(Exception e)
                            {
                                if(e is CrashGameException)
                                    throw e;

                                string msg = $"Exception during {ComponentUpdateDraw[i].GetType().Name}.UpdateDraw()";
                                Log.Error($"{msg}: {e}", msg);
                            }
                        }
                    }
                    else
                    {
                        for(int i = 0; i < comps; ++i)
                        {
                            try
                            {
                                IComponent comp = ComponentUpdateDraw[i];
                                comp.UpdateDraw();
                            }
                            catch(Exception e)
                            {
                                if(e is CrashGameException)
                                    throw e;

                                string msg = $"Exception during {ComponentUpdateDraw[i].GetType().Name}.UpdateDraw()";
                                Log.Error($"{msg}: {e}", msg);
                            }
                        }
                    }
                }

                if(Profile)
                {
                    StopwatchRoot.Stop();
                    double ms = StopwatchRoot.Elapsed.TotalMilliseconds;
                    ProfileMeasure profiled = RootMeasurements.MeasuredDraw;
                    profiled.Min = Math.Min(profiled.Min, ms);
                    profiled.Max = Math.Max(profiled.Max, ms);
                    profiled.MovingAvg = (profiled.MovingAvg * (1 - NewMeasureWeight)) + (ms * NewMeasureWeight);
                }

                OnDrawEnd?.Invoke();
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

            if(Profile)
            {
                Stopwatch.Stop();
                MeasuredResults.Add(new ProfileData(MeasuredFor, Stopwatch.Elapsed.TotalMilliseconds));

                MeasuredFor = $"{component.GetType().Name}.ctor()";
                Stopwatch.Restart();
            }
        }

        void IModBase.ComponentScheduleRefresh(IComponent component)
        {
            ComponentRefreshFlags.Add(component);
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