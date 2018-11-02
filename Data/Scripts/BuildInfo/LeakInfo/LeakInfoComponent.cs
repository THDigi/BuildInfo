using System;
using System.Collections.Generic;
using ParallelTasks;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo.LeakInfo
{
    public class LeakInfoComponent
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
        private int skipUpdates = 0; // used to refresh the viewedVentControlPanel at a lower frequency

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
        private const int MIN_DRAW_SECONDS = 30; // cap for the dynamically calculated line lifetime
        private const int MAX_DRAW_SECONDS = 300;
        private const double DRAW_DEPTH = 0.01; // how far from the camera to draw the lines/dots (always on top trick)
        private const float DRAW_DEPTH_F = 0.01f; // float version of the above value
        private const float DRAW_POINT_SIZE = 0.09f; // the start and end point's size
        private const float DRAW_TRANSPARENCY = 1f; // drawn line alpha
        private const int DRAW_FADE_OUT_TICKS = 60 * 3; // ticks before the end to start fading out the line alpha
        private readonly Color COLOR_PARTICLES = new Color(0, 155, 255); // particles color
        private readonly MyStringId MATERIAL_DOT = MyStringId.GetOrCompute("WhiteDot");
        private readonly ParticleData[] particleDataGridSize = new ParticleData[]
        {
            new ParticleData(size: 0.05f, spawnDelay: 6, lerpPos: 0.075, walkSpeed: 0.1f), // largeship
            new ParticleData(size: 0.05f, spawnDelay: 6, lerpPos: 0.25, walkSpeed: 0.4f) // smallship
        };

        #region Init and close
        public LeakInfoComponent()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
        }

        public void Close()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            ClearStatus();
        }
        #endregion

        public void Update()
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
                NotifyHUD("Computing path...", 100, MyFontEnum.Blue);

            if(++skipUpdates > 30) // every half a second, update the custom detail info on the currently selected air vent in the terminal.
            {
                skipUpdates = 0;

                if(ViewedVentControlPanel != null)
                {
                    if(ViewedVentControlPanel.Closed || MyAPIGateway.Gui.ActiveGamePlayScreen == null)
                        ViewedVentControlPanel = null;
                    else
                        ViewedVentControlPanel.RefreshCustomInfo();
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

        /// <summary>
        /// Used by the pathfinding algorithm to... I'm not sure xD
        /// </summary>
        class MinHeap
        {
            private Breadcrumb ListHead;

            public bool HasNext()
            {
                return ListHead != null;
            }

            public void Add(Breadcrumb item)
            {
                if(ListHead == null)
                {
                    ListHead = item;
                }
                else if(ListHead.Next == null && item.Cost <= ListHead.Cost)
                {
                    item.NextListElem = ListHead;
                    ListHead = item;
                }
                else
                {
                    var pointer = ListHead;

                    while(pointer.NextListElem != null && pointer.NextListElem.Cost < item.Cost)
                    {
                        pointer = pointer.NextListElem;
                    }

                    item.NextListElem = pointer.NextListElem;
                    pointer.NextListElem = item;
                }
            }

            public Breadcrumb GetFirst()
            {
                var result = ListHead;
                ListHead = ListHead.NextListElem;
                return result;
            }

            public void Clear()
            {
                ListHead = null;
            }
        }

        /// <summary>
        /// Used by the pathfinding algorithm to keep track of checked path cost and route.
        /// </summary>
        class Breadcrumb
        {
            public Vector3I Position;
            public int Cost;
            public int PathCost;
            public Breadcrumb Next;
            public Breadcrumb NextListElem;

            public Breadcrumb(Vector3I position)
            {
                Position = position;
            }

            public Breadcrumb(Vector3I position, int cost, int pathCost, Breadcrumb next)
            {
                Position = position;
                Cost = cost;
                PathCost = pathCost;
                Next = next;
            }
        }

        /// <summary>
        /// Identifier for block position combined with direction.
        /// <para>Used for pathfinding to mark a certain move (from position towards direction) as already explored or not due to how the pressurization system works.</para>
        /// </summary>
        struct MoveId
        {
            public readonly Vector3I Position;
            public readonly Vector3I Direction;
            public readonly int HashCode;

            public MoveId(ref Vector3I position, ref Vector3I direction)
            {
                Position = position;
                Direction = direction;

                unchecked
                {
                    HashCode = 17;
                    HashCode = HashCode * 31 + position.GetHashCode();
                    HashCode = HashCode * 31 + direction.GetHashCode();
                }
            }
        }

        class MoveIdEqualityComparer : IEqualityComparer<MoveId>
        {
            public bool Equals(MoveId x, MoveId y)
            {
                return x.Position.Equals(y.Position) && x.Direction.Equals(y.Direction);
            }

            public int GetHashCode(MoveId x)
            {
                return x.HashCode;
            }
        }

        struct LineI
        {
            public readonly Vector3I Start;
            public readonly Vector3I End;

            public LineI(Vector3I start, Vector3I end)
            {
                Start = start;
                End = end;
            }
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
        #endregion

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

                NotifyHUD("Cancelled.", 1000, MyFontEnum.White);
            }
            else if(crumb == null)
            {
                Status = ThreadStatus.IDLE;
                selectedGrid = null;

                NotifyHUD("No leaks!", 2000, MyFontEnum.Green);
            }
            else
            {
                Status = ThreadStatus.DRAW;
                drawTicks = (int)MathHelper.Clamp((lines.Count * 60 * 2 * selectedGrid.GridSize), 60 * MIN_DRAW_SECONDS, 60 * MAX_DRAW_SECONDS);

                NotifyHUD("Found leak, path rendered.", 2000, MyFontEnum.White);
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
        private void NotifyHUD(string message, int time = 3000, string font = MyFontEnum.White)
        {
            if(notify == null)
                notify = MyAPIGateway.Utilities.CreateNotification(string.Empty);

            notify.Text = "Air leak scan: " + message;
            notify.AliveTime = time;
            notify.Font = font;
            notify.Show();
        }

        #region Drawing
        class ParticleData
        {
            public readonly float Size;
            public readonly int SpawnDelay;
            public readonly double LerpPos;
            public readonly float WalkSpeed;

            public ParticleData(float size, int spawnDelay, double lerpPos, float walkSpeed)
            {
                Size = size;
                SpawnDelay = spawnDelay;
                LerpPos = lerpPos;
                WalkSpeed = walkSpeed;
            }
        }

        class Particle
        {
            private Vector3D position;
            private float walk;

            public Particle(IMyCubeGrid grid, List<LineI> lines)
            {
                var l = lines[lines.Count - 1];
                position = grid.GridIntegerToWorld(l.Start);
                walk = lines.Count - 1;
            }

            /// <summary>
            /// Returns true if it should spawn more particles, false otherwise (reached the end and looping back)
            /// </summary>
            public bool Draw(IMyCubeGrid grid, List<LineI> lines, ref Color color, ParticleData pd, ref Vector3D camPos, ref Vector3D camFw)
            {
                var i = (walk < 0 ? 0 : (int)Math.Floor(walk));
                var l = lines[i];
                var lineStart = grid.GridIntegerToWorld(l.Start);
                var lineEnd = grid.GridIntegerToWorld(l.End);

                if(IsVisibleFast(ref camPos, ref camFw, ref lineStart) || IsVisibleFast(ref camPos, ref camFw, ref lineEnd))
                {
                    var lineDir = (lineEnd - lineStart);
                    var fraction = (1 - (walk - i));
                    var targetPosition = lineStart + lineDir * fraction;

                    if(!MyParticlesManager.Paused)
                        position = Vector3D.Lerp(position, targetPosition, pd.LerpPos);

                    var drawPosition = camPos + ((position - camPos) * DRAW_DEPTH);

                    if(walk < 0)
                        MyTransparentGeometry.AddPointBillboard(BuildInfo.Instance.LeakInfoComp.MATERIAL_DOT, color * (1f - Math.Abs(walk)), drawPosition, pd.Size * DRAW_DEPTH_F, 0);
                    else
                        MyTransparentGeometry.AddPointBillboard(BuildInfo.Instance.LeakInfoComp.MATERIAL_DOT, color, drawPosition, pd.Size * DRAW_DEPTH_F, 0);
                }

                if(!MyParticlesManager.Paused)
                {
                    walk -= pd.WalkSpeed; // walk on the lines

                    if(walk < -1) // go back to the start and tell the component to stop spawning new ones
                    {
                        l = lines[lines.Count - 1];
                        position = grid.GridIntegerToWorld(l.Start);
                        walk = lines.Count - 1;
                        return false;
                    }
                }

                return true;
            }
        }

        public void Draw()
        {
            if(Status != ThreadStatus.DRAW)
                return;

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var camPos = camMatrix.Translation;
            var camFw = camMatrix.Forward;
            var alpha = (drawTicks < DRAW_FADE_OUT_TICKS ? (DRAW_TRANSPARENCY * ((float)drawTicks / (float)DRAW_FADE_OUT_TICKS)) : DRAW_TRANSPARENCY);
            var particleColor = COLOR_PARTICLES * alpha;

            // start dot
            var startPointWorld = selectedGrid.GridIntegerToWorld(lines[lines.Count - 1].Start);
            if(IsVisibleFast(ref camPos, ref camFw, ref startPointWorld))
            {
                var startPos = camPos + ((startPointWorld - camPos) * DRAW_DEPTH);
                MyTransparentGeometry.AddPointBillboard(MATERIAL_DOT, Color.Green * alpha, startPos, DRAW_POINT_SIZE * DRAW_DEPTH_F, 0);
            }

            var pd = particleDataGridSize[(int)selectedGrid.GridSizeEnum]; // particle settings for the grid cell size

            // spawning particles
            if(!stopSpawning && !MyParticlesManager.Paused && ++delayParticles > pd.SpawnDelay)
            {
                delayParticles = 0;
                particles.Add(new Particle(selectedGrid, lines));
            }

            // draw/move particles
            for(int i = particles.Count - 1; i >= 0; --i)
            {
                if(!particles[i].Draw(selectedGrid, lines, ref particleColor, pd, ref camPos, ref camFw))
                    stopSpawning = true;
            }

            // end dot
            var endPointWorld = selectedGrid.GridIntegerToWorld(lines[0].End);
            if(IsVisibleFast(ref camPos, ref camFw, ref endPointWorld))
            {
                var endPos = camPos + ((endPointWorld - camPos) * DRAW_DEPTH);
                MyTransparentGeometry.AddPointBillboard(MATERIAL_DOT, Color.Red * alpha, endPos, DRAW_POINT_SIZE * DRAW_DEPTH_F, 0);
            }
        }

        private static bool IsVisibleFast(ref Vector3D camPos, ref Vector3D camFw, ref Vector3D point)
        {
            return (((camPos.X * point.X + camPos.Y * point.Y + camPos.Z * point.Z) - (camPos.X * camFw.X + camPos.Y * camFw.Y + camPos.Z * camFw.Z)) > 0);
        }
        #endregion
    }
}