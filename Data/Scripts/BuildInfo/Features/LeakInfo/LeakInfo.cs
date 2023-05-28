using System;
using System.Collections.Generic;
using System.Threading;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using ParallelTasks;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.LeakInfo
{
    public class LeakInfo : ModComponent
    {
        public bool Enabled => MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization && MyAPIGateway.Session.SessionSettings.EnableOxygen;

        // used in main thread
        public InfoStatus Status { get; private set; } // the scan/thread status
        public IMyAirVent UsedFromVent { get; private set; } // the vent used to start the air leak scan from
        public IMyTerminalControlOnOffSwitch TerminalControl; // the air vent button, used to refresh its state manually when clicking it doesn't actually turn to Find state.
        IMyAirVent VentInTerminal = null; // used to constantly update the detail info panel of the viewed air vent only
        Task Task;
        IMyHudNotification Notify = null;
        int DrawUntilTick;
        bool StopSpawning = false; // used to stop spawning of particles when the first particle reaches the end point and loops back around
        int DelayParticles = 0; // used to count spawning particles in Draw(), isolated from gamelogic (which is affected by pause)
        readonly List<Particle> Particles = new List<Particle>();
        MyConcurrentPool<Particle> ParticlePool = new MyConcurrentPool<Particle>(expectedAllocations: 0);
        ParticleData CurrentParticleData = null;
        int ReleaseMemoryAt = 0; // for memory releasing

        // used in background thread
        Vector3I StartPosition;
        BreadcrumbHeap.Crumb Crumb = null;
        readonly BreadcrumbHeap Crumbs = new BreadcrumbHeap();
        readonly HashSet<Vector3I> Scanned = new HashSet<Vector3I>(Vector3I.Comparer);

        // shared between main and background thread but not at the same time.
        IMyCubeGrid SelectedGrid = null;
        readonly List<MyTuple<Vector3I, Vector3I>> DrawLines = new List<MyTuple<Vector3I, Vector3I>>();

        // used in both threads, but I've learned this is safe to assign in main and read in the background thread
        bool CancelTask = false;
        bool TaskProcessing = false;
        bool TaskGeneratingLines = false;
        int TaskComputedCells = 0;

        public enum InfoStatus { None, Computing, Drawing }

        public const int DrawMinSeconds = 30; // cap for the dynamically calculated particle lifetime
        public const int DrawMaxSeconds = 300;
        public const int DrawFadeOutTicks = Constants.TicksPerSecond * 3; // fade out particles over this many ticks before they vanish

        public readonly MyStringId ParticleMaterial = MyStringId.GetOrCompute("BuildInfo_LeakInfo_Particle");
        public const float EndPointSize = 0.15f;
        public readonly Color StartPointColorOverlay = new Color(0, 255, 0);
        public readonly Color StartPointColorWorld = new Color(0, 155, 0);
        public readonly Color EndPointColorOverlay = new Color(255, 0, 0);
        public readonly Color EndPointColorWorld = new Color(155, 0, 0);

        public const double DrawDepth = 0.01; // for see-through-walls
        public const float DrawDepthF = (float)DrawDepth;

        public const int ReleaseMemoryAfterTicks = Constants.TicksPerSecond * 60 * 5;

        readonly ParticleData[] ParticleDataGridSize = new ParticleData[]
        {
            new ParticleData(size: 0.1f, spawnDelay: 30, lerpPos: 0.075f, walkSpeed: 0.1f), // largeship
            new ParticleData(size: 0.1f, spawnDelay: 30, lerpPos: 0.25f, walkSpeed: 0.4f) // smallship
        };

        public LeakInfo(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
        }

        public override void UnregisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;

            if(!Main.ComponentsRegistered)
                return;

            ClearStatus();
        }

        /// <summary>
        /// Gets called when you (local script executer) look at a block in the terminal.
        /// If that happens to be an air vent we need to mark it so we constantly update the custom detail info.
        /// </summary>
        void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            if(block == VentInTerminal)
                return;

            if(VentInTerminal != null)
            {
                VentInTerminal.OnMarkForClose -= ViewedVentMarkedForClose;
            }

            VentInTerminal = (block as IMyAirVent);

            if(VentInTerminal != null)
            {
                VentInTerminal.OnMarkForClose += ViewedVentMarkedForClose;
            }
        }

        void ViewedVentMarkedForClose(IMyEntity ent)
        {
            VentInTerminal.OnMarkForClose -= ViewedVentMarkedForClose;
            VentInTerminal = null;
        }

        public override void UpdateAfterSim(int tick)
        {
            if(Status == InfoStatus.Computing)
            {
                if(tick % 15 == 0)
                {
                    if(!TaskProcessing)
                    {
                        NotifyHUD($"Waiting for available thread.", 1000, FontsHandler.SkyBlueSh);
                    }
                    else if(TaskGeneratingLines)
                    {
                        NotifyHUD($"Computing path, finalizing...", 1000, FontsHandler.SkyBlueSh);
                    }
                    else
                    {
                        float totalCells = (SelectedGrid.Max - SelectedGrid.Min).Volume();
                        int progress = (int)Math.Round((TaskComputedCells / totalCells) * 100);
                        NotifyHUD($"Computing path, {progress.ToString()}% of volume walked...", 1000, FontsHandler.SkyBlueSh);
                    }
                }
            }
            else if(Status == InfoStatus.Drawing) // if the path to air leak is shown then decrease the countdown timer or clear it directly if the grid suddenly vanishes.
            {
                if(DrawUntilTick <= tick)
                {
                    ClearStatus();
                }
            }
            else if(Status == InfoStatus.None && ReleaseMemoryAt > 0 && ReleaseMemoryAt < tick)
            {
                ReleaseMemoryAt = 0;
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, false);

                Particles.Capacity = 0;
                ParticlePool = new MyConcurrentPool<Particle>(expectedAllocations: 0);

                Crumbs.ClearPool();
            }
        }

        void SetStatus(InfoStatus status)
        {
            bool different = (Status != status);

            Status = status;
            SetUpdateMethods(UpdateFlags.UPDATE_DRAW, (status == InfoStatus.Drawing));

            if(status == InfoStatus.Drawing) // on only, it turns off itself after cleaning
                SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);

            if(different && VentInTerminal != null)
            {
                VentInTerminal.RefreshCustomInfo();
                VentInTerminal.SetDetailedInfoDirty();
            }
        }

        /// <summary>
        /// Clears the lines, various status data and gracefully stops the processing thread if running.
        /// </summary>
        public void ClearStatus()
        {
            if(Status == InfoStatus.Computing) // gracefully cancel the task by telling it to stop doing stuff and then wait for it to finish so we can clear its data.
            {
                CancelTask = true;
                Task.WaitOrExecute(blocking: true);
            }

            if(SelectedGrid != null)
                SelectedGrid.OnMarkForClose -= SelectedGridMarkedForClose;

            SetStatus(InfoStatus.None);

            ReleaseMemoryAt = Main.Tick + ReleaseMemoryAfterTicks;
            DrawLines.Clear();
            StopSpawning = false;
            DrawUntilTick = 0;
            Crumb = null;
            SelectedGrid = null;
            CurrentParticleData = null;
            UsedFromVent = null;

            TaskProcessing = false;
            TaskGeneratingLines = false;
            TaskComputedCells = 0;

            foreach(Particle particle in Particles)
            {
                ParticlePool.Return(particle);
            }
            Particles.Clear();
        }

        public void StartThread(IMyAirVent block, Vector3I startPosition)
        {
            ClearStatus();

            if(!Enabled)
                return;

            CancelTask = false;
            UsedFromVent = block;
            SelectedGrid = block.CubeGrid;
            SelectedGrid.OnMarkForClose += SelectedGridMarkedForClose;
            CurrentParticleData = ParticleDataGridSize[(int)SelectedGrid.GridSizeEnum]; // particle settings for the grid cell size
            StartPosition = startPosition;

            //int gridCells = (SelectedGrid.Max - SelectedGrid.Min).Volume();
            //if(gridCells <= 50000)
            //    Task = MyAPIGateway.Parallel.Start(ThreadRun, ThreadFinished);
            //else
            Task = MyAPIGateway.Parallel.StartBackground(ThreadRun, ThreadFinished);

            SetStatus(InfoStatus.Computing);
            SetUpdateMethods(UpdateFlags.UPDATE_AFTER_SIM, true);
            NotifyHUD("Computing path...", 1000 * 5, FontsHandler.SkyBlueSh);
        }

        void SelectedGridMarkedForClose(IMyEntity ent)
        {
            ent.OnMarkForClose -= SelectedGridMarkedForClose;
            ClearStatus();
        }

        #region Pathfinding
        /// <summary>
        /// <para>A* Pathfinding - Credit for the original code to Roy T. - http://roy-t.nl/2011/09/24/another-faster-version-of-a-2d3d-in-c.html</para>
        /// <para>This was modified and adapted to suit the needs of this mod, which changes the specific end vector to an "outside of grid" target.</para>
        /// <para>Also made into a separate thread.</para>
        /// </summary>
        private void ThreadRun()
        {
            try
            {
                TaskProcessing = true;

                // generate crumbs and path cost
                Crumb = PathfindGenerateCrumbs(SelectedGrid, ref StartPosition);

                if(Crumb != null)
                {
                    TaskGeneratingLines = true;

                    while(Crumb.Next != null)
                    {
                        DrawLines.Add(MyTuple.Create(Crumb.Next.Position, Crumb.Position));
                        Crumb = Crumb.Next;
                    }
                }
            }
            finally
            {
                // cleanup after PathfindGenerateCrumbs()
                Scanned.Clear();
                Crumbs.Clear();
            }
        }

        /// <summary>Find the closest path towards the exit.
        /// <para>Returns null if no path was found.</para>
        /// <para>NOTE: It can return null if <paramref name="start"/> is inside a closed door's position.</para>
        /// </summary>
        private BreadcrumbHeap.Crumb PathfindGenerateCrumbs(IMyCubeGrid grid, ref Vector3I start) // used in a background thread
        {
            Scanned.Clear();
            Crumbs.Clear();
            Crumbs.Add(start);

            Vector3I[] directions = Base6Directions.IntDirections;
            const int DirLen = 6;

            Vector3I min = grid.Min;
            Vector3I max = grid.Max;
            BoundingBoxI gridBB = new BoundingBoxI(min - Vector3I.One, max + Vector3I.One);

            while(Crumbs.ListHead != null) // HACK: HasNext() inlined
            {
                if(CancelTask)
                    return null;

                // HACK: GetFirst() inlined
                BreadcrumbHeap.Crumb crumb = Crumbs.ListHead;
                Crumbs.ListHead = crumb.NextListElem;

                {
                    Vector3I s = Vector3I.Min(max - crumb.Position, crumb.Position - min);
                    if(Math.Min(s.X, Math.Min(s.Y, s.Z)) < 0)
                        return crumb;
                }
                // HACK: manually inlined ^v
                //if(DistanceToBox(crumb.Position, grid.Min, grid.Max) < 0)
                //    return crumb; // found outside of grid, terminate.

                if(!Scanned.Add(crumb.Position))
                    continue;

                Interlocked.Increment(ref TaskComputedCells);

                for(int dirIdx = 0; dirIdx < DirLen; ++dirIdx)
                {
                    Vector3I target = crumb.Position + directions[dirIdx];
                    Vector3I s = Vector3I.Min(max - target, target - min);
                    int distToOutside = Math.Min(s.X, Math.Min(s.Y, s.Z)) + 1;
                    if(distToOutside < 0)
                        return crumb;
                    // HACK: manually inlined ^v
                    //int distToOutside = DistanceToBox(target, grid.Min, grid.Max) + 1; // adding 1 to offset edges
                    //if(distToOutside < 0) // gone outside of edge already, exit
                    //    return crumb;

                    if(gridBB.Contains(target) == ContainmentType.Contains && !Pressurization.IsAirtightBetweenPositions(grid, crumb.Position, target))
                    {
                        int pathCost = crumb.PathCost + 1; // direction movement cost, always 1 in our case
                        int cost = pathCost + distToOutside; // using distance to box edge instead of a predefined end

                        Crumbs.Add(target, cost, pathCost, crumb);
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// <para>Distance to min/max edges while point is inside the box.</para>
        /// <para>Returns negative when point is outside of the box.</para>
        /// <para>Thanks to Phoera for supplying this :]</para>
        /// </summary>
        //private int DistanceToBox(Vector3I pos, Vector3I min, Vector3I max)
        //{
        //    Vector3I s = Vector3I.Min(max - pos, pos - min);
        //    return Math.Min(s.X, Math.Min(s.Y, s.Z));
        //}
        #endregion Pathfinding

        private void ThreadFinished()
        {
            try
            {
                if(Task.Exceptions != null && Task.Exceptions.Length > 0)
                {
                    foreach(Exception e in Task.Exceptions)
                    {
                        Log.Error("(Thread) " + e.ToString());
                    }
                }

                if(CancelTask)
                {
                    ClearStatus();

                    NotifyHUD("Cancelled.", 2000, FontsHandler.YellowSh);
                }
                else if(Crumb == null)
                {
                    ClearStatus();

                    NotifyHUD("No leaks!", 2000, FontsHandler.GreenSh);
                }
                else
                {
                    int drawSeconds = (int)MathHelper.Clamp((DrawLines.Count * 2 * SelectedGrid.GridSize), DrawMinSeconds, DrawMaxSeconds);
                    DrawUntilTick = Main.Tick + (Constants.TicksPerSecond * drawSeconds);

                    SetStatus(InfoStatus.Drawing);

                    NotifyHUD($"Found leak, path rendered (visible for {drawSeconds.ToString()}s)", 3000, FontsHandler.YellowSh);
                }

                CancelTask = false;
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Prints a message to the player, repeated calls to this method will overwrite the previous message on the HUD, therefore preventing spam.
        /// </summary>
        private void NotifyHUD(string message, int time = 3000, string font = FontsHandler.WhiteSh)
        {
            if(time < 1000 && Main.IsPaused)
                return; // HACK: avoid notification glitching out if showing them continuously when game is paused

            if(Notify == null)
                Notify = MyAPIGateway.Utilities.CreateNotification(string.Empty);

            Notify.Hide(); // required since SE v1.194
            Notify.Text = "Air leak scan: " + message;
            Notify.AliveTime = time;
            Notify.Font = font;
            Notify.Show();
        }

        #region Drawing
        public override void UpdateDraw()
        {
            if(Status != InfoStatus.Drawing)
                return;

            BoundingSphereD gridSphere = SelectedGrid.WorldVolume;

            MatrixD camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            Vector3D camPos = camMatrix.Translation;

            double distSq = gridSphere.Radius + 500;
            distSq *= distSq;
            if(Vector3D.DistanceSquared(gridSphere.Center, camPos) > distSq)
            {
                NotifyHUD("Too far, hidden.", 2000, FontsHandler.YellowSh);
                ClearStatus();
                return;
            }

            Vector3D camFw = camMatrix.Forward;
            double dotPosFw = Vector3D.Dot(camPos, camFw);

            float alpha = 1f;
            Color particleColorWorld = Main.Config.LeakParticleColorWorld.Value;
            Color particleColorOverlay = Main.Config.LeakParticleColorOverlay.Value;

            int drawTicksLeft = DrawUntilTick - Main.Tick;
            if(drawTicksLeft < DrawFadeOutTicks)
            {
                alpha = (drawTicksLeft / (float)DrawFadeOutTicks);
                particleColorWorld *= alpha;
                particleColorOverlay *= alpha;
            }

            // start dot
            Vector3D startPointWorld = SelectedGrid.GridIntegerToWorld(DrawLines[DrawLines.Count - 1].Item1);
            //if(IsVisibleFast(ref camPos, ref camFw, ref startPointWorld))
            // HACK: manually inlined ^v
            if(Vector3D.Dot(camPos, startPointWorld) - dotPosFw > 0)
            {
                MyTransparentGeometry.AddPointBillboard(ParticleMaterial, StartPointColorWorld * alpha, startPointWorld, EndPointSize, 0);

                Vector3D posOverlay = camPos + ((startPointWorld - camPos) * DrawDepth);
                MyTransparentGeometry.AddPointBillboard(ParticleMaterial, StartPointColorOverlay * alpha, posOverlay, EndPointSize * 0.5f * DrawDepthF, 0);
            }

            bool spawnSync = false;

            // timing particles
            if(!MyParticlesManager.Paused && ++DelayParticles > CurrentParticleData.SpawnDelay)
            {
                DelayParticles = 0;

                if(StopSpawning)
                    spawnSync = true;
                else
                    Particles.Add(ParticlePool.Get().Init(SelectedGrid, DrawLines));
            }

            // draw/move particles
            for(int i = Particles.Count - 1; i >= 0; --i)
            {
                DrawReturn result = Particles[i].Draw(SelectedGrid, DrawLines, particleColorWorld, particleColorOverlay, CurrentParticleData, ref camPos, dotPosFw, spawnSync);
                if(result == DrawReturn.StopSpawning)
                    StopSpawning = true;
            }

            // end dot
            Vector3D endPointWorld = SelectedGrid.GridIntegerToWorld(DrawLines[0].Item2);
            //if(IsVisibleFast(ref camPos, ref camFw, ref endPointWorld))
            // HACK: manually inlined ^v
            if(Vector3D.Dot(camPos, endPointWorld) - dotPosFw > 0)
            {
                MyTransparentGeometry.AddPointBillboard(ParticleMaterial, EndPointColorWorld * alpha, endPointWorld, EndPointSize, 0);

                Vector3D posOverlay = camPos + ((endPointWorld - camPos) * DrawDepth);
                MyTransparentGeometry.AddPointBillboard(ParticleMaterial, EndPointColorOverlay * alpha, posOverlay, EndPointSize * 0.5f * DrawDepthF, 0);
            }
        }

        //public static bool IsVisibleFast(ref Vector3D camPos, ref Vector3D camFw, ref Vector3D point)
        //{
        //    // Vector3D.Dot(camPos, point) - Vector3D.Dot(camPos, camFw) > 0
        //    return (((camPos.X * point.X + camPos.Y * point.Y + camPos.Z * point.Z) - (camPos.X * camFw.X + camPos.Y * camFw.Y + camPos.Z * camFw.Z)) > 0);
        //}
        #endregion Drawing
    }
}