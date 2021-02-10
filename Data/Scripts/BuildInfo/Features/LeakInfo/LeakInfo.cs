using System;
using System.Collections.Generic;
using Digi.BuildInfo.VanillaData;
using Digi.ComponentLib;
using ParallelTasks;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.Features.LeakInfo
{
    public class LeakInfo : ModComponent
    {
        public bool Enabled => MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization && MyAPIGateway.Session.SessionSettings.EnableOxygen;

        // used in main thread
        public ThreadStatus Status = ThreadStatus.IDLE; // the scan/thread status
        public IMyAirVent UsedFromVent = null; // the vent used to start the air leak scan from
        public IMyTerminalControlOnOffSwitch TerminalControl = null; // the air vent button, used to refresh its state manually when clicking it doesn't actually turn to Find state.
        public IMyAirVent ViewedVentControlPanel = null; // used to constantly update the detail info panel of the viewed air vent only
        private Task task; // the background task
        private IMyHudNotification notify = null;
        private int drawTicks = 0; // ticks until the drawn lines expire
        private bool stopSpawning = false; // used to stop spawning of particles when the first particle reaches the end point and loops back around
        private int delayParticles = 0; // used to count spawning particles in Draw(), isolated from gamelogic (which is affected by pause)
        private readonly List<Particle> particles = new List<Particle>();

        // used in background thread
        private Vector3I startPosition;
        private Breadcrumb crumb = null;
        private readonly MinHeap openList = new MinHeap();
        private readonly HashSet<MoveId> scanned = new HashSet<MoveId>(new MoveIdEqualityComparer());

        // shared between main and background thread, should probably not be accessed if thread is running
        private IMyCubeGrid selectedGrid = null;
        private List<LineI> lines = new List<LineI>();

        // used in both threads, but I've learned this is safe to assign in main and read in the background thread
        private bool cancelTask = false;

        // constants
        public enum ThreadStatus { IDLE, RUNNING, DRAW }
        public const int MIN_DRAW_SECONDS = 30; // cap for the dynamically calculated particle lifetime
        public const int MAX_DRAW_SECONDS = 300;
        public const int DRAW_FADE_OUT_TICKS = 60 * 3; // ticks before the end to start fading out the particles alpha
        public const float DRAW_TRANSPARENCY = 1f; // starting particle alpha
        public const float DRAW_POINT_SIZE = 0.15f; // the start and end point's size
        public const double DRAW_DEPTH = 0.01; // camera distance for overlay particles
        public const float DRAW_DEPTH_F = 0.01f; // float version of the above value
        public readonly Color COLOR_START_OVERLAY = new Color(0, 255, 0); // color of the starting point sprite
        public readonly Color COLOR_START_WORLD = new Color(0, 155, 0);
        public readonly Color COLOR_END_OVERLAY = new Color(255, 0, 0); // color of the ending point sprite
        public readonly Color COLOR_END_WORLD = new Color(155, 0, 0);
        public readonly MyStringId MATERIAL_PARTICLE = MyStringId.GetOrCompute("BuildInfo_LeakInfo_Particle");
        private readonly ParticleData[] particleDataGridSize = new ParticleData[]
        {
            new ParticleData(size: 0.1f, spawnDelay: 30, lerpPos: 0.075, walkSpeed: 0.1f), // largeship
            new ParticleData(size: 0.1f, spawnDelay: 30, lerpPos: 0.25, walkSpeed: 0.4f) // smallship
        };

        public LeakInfo(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_AFTER_SIM | UpdateFlags.UPDATE_DRAW;
        }

        protected override void RegisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
        }

        protected override void UnregisterComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            ClearStatus();
        }

        protected override void UpdateAfterSim(int tick)
        {
            if(selectedGrid != null && selectedGrid.Closed)
            {
                ClearStatus();
            }

            if(Status == ThreadStatus.DRAW) // if the path to air leak is shown then decrease the countdown timer or clear it directly if the grid suddenly vanishes.
            {
                if(--drawTicks <= 0)
                {
                    ClearStatus();
                }
            }

            if(Status == ThreadStatus.RUNNING) // notify the player that a background task is running.
            {
                NotifyHUD("Computing path...", 100, FontsHandler.SkyBlueSh);
            }

            if(tick % 30 == 0) // every half a second, update the custom detail info on the currently selected air vent in the terminal.
            {
                if(ViewedVentControlPanel != null)
                {
                    if(ViewedVentControlPanel.Closed || MyAPIGateway.Gui.ActiveGamePlayScreen == null)
                        ViewedVentControlPanel = null;
                    // no need to update since TerminalInfoComponent already does for airvent
                    //else
                    //    ViewedVentControlPanel.RefreshCustomInfo();
                }
            }
        }

        /// <summary>
        /// Clears the lines, various status data and gracefully stops the processing thread if running.
        /// </summary>
        public void ClearStatus()
        {
            if(Status == ThreadStatus.RUNNING) // gracefully cancel the task by telling it to stop doing stuff and then wait for it to finish so we can clear its data.
            {
                cancelTask = true;
                task.Wait();
            }

            Status = ThreadStatus.IDLE;
            lines.Clear();
            particles.Clear();
            stopSpawning = false;
            drawTicks = 0;
            crumb = null;
            selectedGrid = null;
            UsedFromVent = null;
        }

        public void StartThread(IMyAirVent block, Vector3I startPosition)
        {
            ClearStatus();

            if(!Enabled)
                return;

            UsedFromVent = block;
            selectedGrid = block.CubeGrid;
            this.startPosition = startPosition;

            Status = ThreadStatus.RUNNING;
            task = MyAPIGateway.Parallel.Start(ThreadRun, ThreadFinished);
            ViewedVentControlPanel?.RefreshCustomInfo();
        }

        #region Pathfinding
        /// <summary>
        /// <para>A* Pathfinding - Credit for the original code to Roy T. - http://roy-t.nl/2011/09/24/another-faster-version-of-a-2d3d-in-c.html</para>
        /// <para>This was modified and adapted to suit the needs of this mod, which changes the specific end vector to an "outside of grid" target.</para>
        /// <para>Also made into a separate thread.</para>
        /// </summary>
        private void ThreadRun()
        {
            // generate crumbs and path cost
            crumb = PathfindGenerateCrumbs(selectedGrid, ref startPosition);

            // cleanup after PathfindGenerateCrumbs(), not critical but helps memory a tiny bit
            scanned.Clear();
            openList.Clear();

            if(crumb != null)
            {
                while(crumb.Next != null)
                {
                    if(cancelTask)
                        return;

                    lines.Add(new LineI(crumb.Next.Position, crumb.Position));
                    crumb = crumb.Next;
                }
            }
        }

        /// <summary>Find the closest path towards the exit.
        /// <para>Returns null if no path was found.</para>
        /// <para>NOTE: It can return null if <paramref name="start"/> is inside a closed door's position.</para>
        /// </summary>
        private Breadcrumb PathfindGenerateCrumbs(IMyCubeGrid grid, ref Vector3I start) // used in a background thread
        {
            scanned.Clear();
            openList.Clear();
            openList.Add(new Breadcrumb(start));

            var directions = Base6Directions.IntDirections;

            while(openList.HasNext())
            {
                var crumb = openList.GetFirst();

                if(DistanceToBox(crumb.Position, grid.Min, grid.Max) < 0)
                    return crumb; // found outside of grid, terminate.

                for(int i = 0; i < directions.Length; ++i)
                {
                    if(cancelTask)
                        return null;

                    var dir = directions[i];
                    var moveId = new MoveId(ref crumb.Position, ref dir);

                    if(!scanned.Contains(moveId)) // ensure we didn't already check this position+direction
                    {
                        scanned.Add(moveId);

                        var target = crumb.Position + dir;
                        var distToOutside = DistanceToBox(target, grid.Min, grid.Max) + 1; // adding 1 to offset edges

                        if(distToOutside < 0) // gone outside of edge already, exit
                            return crumb;

                        if(IsInInflatedBounds(grid, target) && !Pressurization.IsAirtightBetweenPositions(grid, crumb.Position, target))
                        {
                            int pathCost = crumb.PathCost + 1; // direction movement cost, always 1 in our case
                            int cost = pathCost + distToOutside; // using distance to box edge instead of a predefined end

                            openList.Add(new Breadcrumb(target, cost, pathCost, crumb));
                        }
                    }
                }
            }

            return null;
        }

        private bool IsInInflatedBounds(IMyCubeGrid grid, Vector3I pos)
        {
            var min = grid.Min - Vector3I.One;
            var max = grid.Max + Vector3I.One;
            return !(min != Vector3I.Min(pos, min)) && !(max != Vector3I.Max(pos, max));
        }

        /// <summary>
        /// <para>Distance to min/max edges while point is inside the box.</para>
        /// <para>Returns negative when point is outside of the box.</para>
        /// <para>Thanks to Phoera for supplying this :]</para>
        /// </summary>
        private int DistanceToBox(Vector3I p, Vector3I min, Vector3I max)
        {
            var s = Vector3I.Min(max - p, p - min);
            return Math.Min(s.X, Math.Min(s.Y, s.Z));
        }
        #endregion Pathfinding

        private void ThreadFinished()
        {
            if(task.Exceptions != null && task.Exceptions.Length > 0)
            {
                foreach(var e in task.Exceptions)
                {
                    Log.Error("(Thread) " + e.ToString());
                }
            }

            if(cancelTask)
            {
                cancelTask = false;
                Status = ThreadStatus.IDLE;
                ClearStatus();

                NotifyHUD("Cancelled.", 1000, FontsHandler.YellowSh);
            }
            else if(crumb == null)
            {
                Status = ThreadStatus.IDLE;
                selectedGrid = null;

                NotifyHUD("No leaks!", 2000, FontsHandler.GreenSh);
            }
            else
            {
                Status = ThreadStatus.DRAW;
                drawTicks = (int)MathHelper.Clamp((lines.Count * 60 * 2 * selectedGrid.GridSize), 60 * MIN_DRAW_SECONDS, 60 * MAX_DRAW_SECONDS);

                NotifyHUD("Found leak, path rendered.", 2000, FontsHandler.YellowSh);
            }

            ViewedVentControlPanel?.RefreshCustomInfo();
        }

        /// <summary>
        /// Gets called when you (local script executer) look at a block in the terminal.
        /// If that happens to be an air vent we need to mark it so we constantly update the custom detail info.
        /// </summary>
        private void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            ViewedVentControlPanel = (block as IMyAirVent);
        }

        /// <summary>
        /// Prints a message to the player, repeated calls to this method will overwrite the previous message on the HUD, therefore preventing spam.
        /// </summary>
        private void NotifyHUD(string message, int time = 3000, string font = FontsHandler.WhiteSh)
        {
            if(notify == null)
                notify = MyAPIGateway.Utilities.CreateNotification(string.Empty);

            notify.Hide(); // required since SE v1.194
            notify.Text = "Air leak scan: " + message;
            notify.AliveTime = time;
            notify.Font = font;
            notify.Show();
        }

        #region Drawing
        protected override void UpdateDraw()
        {
            if(Status != ThreadStatus.DRAW)
                return;

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var camPos = camMatrix.Translation;
            var camFw = camMatrix.Forward;
            var alpha = (drawTicks < DRAW_FADE_OUT_TICKS ? (DRAW_TRANSPARENCY * ((float)drawTicks / (float)DRAW_FADE_OUT_TICKS)) : DRAW_TRANSPARENCY);
            var particleColorWorld = Config.LeakParticleColorWorld.Value * alpha;
            var particleColorOverlay = Config.LeakParticleColorOverlay.Value * alpha;

            // start dot
            var startPointWorld = selectedGrid.GridIntegerToWorld(lines[lines.Count - 1].Start);
            if(IsVisibleFast(ref camPos, ref camFw, ref startPointWorld))
            {
                MyTransparentGeometry.AddPointBillboard(MATERIAL_PARTICLE, COLOR_START_WORLD * alpha, startPointWorld, DRAW_POINT_SIZE, 0);

                var posOverlay = camPos + ((startPointWorld - camPos) * DRAW_DEPTH);
                MyTransparentGeometry.AddPointBillboard(MATERIAL_PARTICLE, COLOR_START_OVERLAY * alpha, posOverlay, DRAW_POINT_SIZE * 0.5f * DRAW_DEPTH_F, 0);
            }

            var particleData = particleDataGridSize[(int)selectedGrid.GridSizeEnum]; // particle settings for the grid cell size

            bool spawnSync = false;

            // timing particles
            if(!MyParticlesManager.Paused && ++delayParticles > particleData.SpawnDelay)
            {
                delayParticles = 0;

                if(stopSpawning)
                    spawnSync = true;
                else
                    particles.Add(new Particle(selectedGrid, lines));
            }

            // draw/move particles
            for(int i = particles.Count - 1; i >= 0; --i)
            {
                var drawReturn = particles[i].Draw(selectedGrid, lines, particleColorWorld, particleColorOverlay, particleData, ref camPos, ref camFw, ref delayParticles, spawnSync);

                if(drawReturn == DrawReturn.STOP_SPAWNING)
                    stopSpawning = true;
            }

            // end dot
            var endPointWorld = selectedGrid.GridIntegerToWorld(lines[0].End);
            if(IsVisibleFast(ref camPos, ref camFw, ref endPointWorld))
            {
                MyTransparentGeometry.AddPointBillboard(MATERIAL_PARTICLE, COLOR_END_WORLD * alpha, endPointWorld, DRAW_POINT_SIZE, 0);

                var posOverlay = camPos + ((endPointWorld - camPos) * DRAW_DEPTH);
                MyTransparentGeometry.AddPointBillboard(MATERIAL_PARTICLE, COLOR_END_OVERLAY * alpha, posOverlay, DRAW_POINT_SIZE * 0.5f * DRAW_DEPTH_F, 0);
            }
        }

        public static bool IsVisibleFast(ref Vector3D camPos, ref Vector3D camFw, ref Vector3D point)
        {
            return (((camPos.X * point.X + camPos.Y * point.Y + camPos.Z * point.Z) - (camPos.X * camFw.X + camPos.Y * camFw.Y + camPos.Z * camFw.Z)) > 0);
        }
        #endregion Drawing
    }
}