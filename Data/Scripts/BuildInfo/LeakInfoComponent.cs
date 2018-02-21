using System;
using System.Collections.Generic;
using System.Text;
using ParallelTasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace Digi.BuildInfo
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_AirVent), useEntityUpdate: false)]
    public class AirVent : MyGameLogicComponent
    {
        private byte init = 0; // init states, 0 no init, 1 init events, 2 init with main model (for dummyLocation)
        private byte skip = 0;
        private Vector3 dummyLocation;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
        }

        public override void UpdateAfterSimulation10()
        {
            try
            {
                if(BuildInfo.instance == null || BuildInfo.instance.isThisDS)
                    return;

                var leakInfo = BuildInfo.instance.leakInfo;

                if(leakInfo == null)
                    return;

                var block = (IMyAirVent)Entity;

                if(init == 0)
                {
                    if(block.CubeGrid.Physics == null || leakInfo == null)
                        return;

                    init = 1;
                    block.AppendingCustomInfo += CustomInfo;

                    if(leakInfo.terminalControl == null)
                    {
                        // separator
                        MyAPIGateway.TerminalControls.AddControl<IMyAirVent>(MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, IMyAirVent>(string.Empty));

                        // on/off switch
                        var c = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, IMyAirVent>("FindAirLeak");
                        c.Title = MyStringId.GetOrCompute("Air leak scan");
                        //c.Tooltip = MyStringId.GetOrCompute("Finds the path towards an air leak and displays it as blue lines, for a maximum of " + LeakInfoComponent.MAX_DRAW_SECONDS + " seconds.\nTo find the leak it first requires the air vent to be powered, functional, enabled and the room not sealed.\nIt only searches once and doesn't update in realtime. If you alter the ship or open/close doors you need to start it again.\nThe lines are only shown to the player that requests the air leak scan.\nDepending on ship size the computation might take a while, you can cancel at any time however.\nAll air vents control the same system, therefore you can start it from one and stop it from another.\n\nAdded by the Build Info mod.");
                        c.Tooltip = MyStringId.GetOrCompute("A client-side pathfinding towards an air leak.\nAdded by Build Info mod.");
                        c.OnText = MyStringId.GetOrCompute("Find");
                        c.OffText = MyStringId.GetOrCompute("Stop");
                        //c.Enabled = Terminal_Enabled;
                        c.SupportsMultipleBlocks = false;
                        c.Setter = Terminal_Setter;
                        c.Getter = Terminal_Getter;
                        MyAPIGateway.TerminalControls.AddControl<IMyAirVent>(c);
                        leakInfo.terminalControl = c;
                    }
                }

                if(init < 2 && block.IsFunctional) // needs to be functional to get the dummy from the main model
                {
                    init = 2;
                    const string DUMMY_NAME = "vent_001"; // HACK hardcoded from MyAirVent.VentDummy property

                    var dummies = leakInfo.dummies;
                    dummies.Clear();

                    IMyModelDummy dummy;
                    if(block.Model.GetDummies(dummies) > 0 && dummies.TryGetValue(DUMMY_NAME, out dummy))
                        dummyLocation = dummy.Matrix.Translation;

                    dummies.Clear();
                }

                if(++skip > 6) // every second
                {
                    skip = 0;

                    // clear the air leak visual display or stop the running thread if the air vent's room is sealed.
                    if(leakInfo.usedFromVent == block && leakInfo.status != LeakInfo.Status.IDLE && block.CanPressurize)
                        leakInfo.ClearStatus();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void Close()
        {
            var block = (IMyTerminalBlock)Entity;
            block.AppendingCustomInfo -= CustomInfo;
        }

        // disabled because otherwise you can't stop it manually after you fix the leak and the air vent pressurizes
        //private bool Terminal_Enabled(IMyTerminalBlock b)
        //{
        //    var block = (IMyAirVent)b;
        //    return (block.IsWorking && !block.CanPressurize);
        //}

        private void Terminal_Setter(IMyTerminalBlock b, bool v)
        {
            try
            {
                var leakInfo = BuildInfo.instance.leakInfo;

                if(BuildInfo.instance.isThisDS || leakInfo == null)
                    return;

                if(leakInfo.status != LeakInfo.Status.IDLE)
                {
                    leakInfo.ClearStatus();

                    if(leakInfo.viewedVentControlPanel != null)
                        leakInfo.viewedVentControlPanel.RefreshCustomInfo();
                }
                else
                {
                    var block = (IMyAirVent)b;

                    if(!block.IsWorking || block.CanPressurize)
                    {
                        leakInfo.terminalControl.UpdateVisual();
                        return;
                    }

                    //if(!block.IsWorking)
                    //{
                    //    leakInfo.NotifyHUD("Air vent is not working!", font: MyFontEnum.Red);
                    //    return;
                    //}
                    //
                    //if(block.CanPressurize)
                    //{
                    //    leakInfo.NotifyHUD("Area is already sealed!", font: MyFontEnum.Green);
                    //    return;
                    //}

                    var dummies = new Dictionary<string, IMyModelDummy>();
                    var start = block.CubeGrid.WorldToGridInteger(Vector3D.Transform(dummyLocation, block.WorldMatrix));

                    leakInfo.StartThread(block.CubeGrid, block.Position);
                    leakInfo.usedFromVent = block;

                    if(leakInfo.viewedVentControlPanel != null)
                        leakInfo.viewedVentControlPanel.RefreshCustomInfo();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        private bool Terminal_Getter(IMyTerminalBlock b)
        {
            var leakInfo = BuildInfo.instance.leakInfo;
            return leakInfo != null && leakInfo.status != LeakInfo.Status.IDLE;
        }

        private void CustomInfo(IMyTerminalBlock b, StringBuilder str)
        {
            try
            {
                var block = (IMyAirVent)b;
                var leakInfo = BuildInfo.instance.leakInfo;

                if(leakInfo != null)
                {
                    str.Append('\n');
                    str.Append("Air leak scan status:\n");

                    switch(leakInfo.status)
                    {
                        case LeakInfo.Status.IDLE:
                            if(!block.IsWorking)
                                str.Append("Air vent not working.");
                            else if(block.CanPressurize)
                                str.Append("Area is sealed.");
                            else
                                str.Append("Ready to scan.");
                            break;
                        case LeakInfo.Status.RUNNING:
                            str.Append("Computing...");
                            break;
                        case LeakInfo.Status.DRAW:
                            str.Append("Leak found and displayed.");
                            break;
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    public class LeakInfo
    {
        // used in main thread
        public Status status = Status.IDLE; // the scan/thread status
        public IMyAirVent usedFromVent = null; // the vent used to start the air leak scan from
        private Task task; // the background task
        private IMyHudNotification notify = null;
        private int drawTicks = 0; // ticks until the drawn lines expire
        private bool stopSpawning = false; // used to stop spawning of particles when the first particle reaches the end point and loops back around
        private int delayParticles = 0; // used to count spawning particles in Draw(), isolated from gamelogic (which is affected by pause)
        private readonly List<Particle> particles = new List<Particle>();
        public IMyTerminalControlOnOffSwitch terminalControl = null; // the air vent button, used to refresh its state manually when clicking it doesn't actually turn to Find state.
        private int skipUpdates = 0; // used to refresh the viewedVentControlPanel at a lower frequency
        public IMyAirVent viewedVentControlPanel = null; // used to constantly update the detail info panel of the viewed air vent only
        public readonly Dictionary<string, IMyModelDummy> dummies = new Dictionary<string, IMyModelDummy>(); // used to temporarily store dummies in the air vent class

        // used in background thread
        private Vector3I startPosition;
        private Breadcrumb crumb = null;
        private readonly MinHeap openList = new MinHeap();
        private readonly HashSet<MoveId> scanned = new HashSet<MoveId>();
        private bool ShouldCancelTask { get { return cancelTask || selectedGrid == null || selectedGrid.Closed; } }

        // shared between main and background thread, should probably not be accessed if thread is running
        private IMyCubeGrid selectedGrid = null;
        private List<Line> lines = new List<Line>();

        // used in both threads, but I've learned this is safe to assign in main and read in the background thread
        private bool cancelTask = false;

        // constants
        public enum Status { IDLE, RUNNING, DRAW }
        private const int MAX_DRAW_SECONDS = 300; // a cap for the dynamically calculated line lifetime
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

        #region Constructor and destructor
        public LeakInfo()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
        }

        public void Close()
        {
            MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            dummies.Clear();
            ClearStatus();
        }
        #endregion

        #region Line drawing
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

            public Particle(IMyCubeGrid grid, List<Line> lines)
            {
                var l = lines[lines.Count - 1];
                position = grid.GridIntegerToWorld(l.Start);
                walk = lines.Count - 1;
            }

            /// <summary>
            /// Returns true if it should spawn more particles, false otherwise (reached the end and looping back)
            /// </summary>
            public bool Draw(IMyCubeGrid grid, List<Line> lines, ref Color color, ParticleData pd, ref Vector3D camPos, ref Vector3D camFw)
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

                    position = Vector3D.Lerp(position, targetPosition, pd.LerpPos);
                    var drawPosition = camPos + ((position - camPos) * DRAW_DEPTH);
                    
                    if(walk < 0)
                        MyTransparentGeometry.AddPointBillboard(BuildInfo.instance.leakInfo.MATERIAL_DOT, color * (1f - Math.Abs(walk)), drawPosition, pd.Size * DRAW_DEPTH_F, 0);
                    else
                        MyTransparentGeometry.AddPointBillboard(BuildInfo.instance.leakInfo.MATERIAL_DOT, color, drawPosition, pd.Size * DRAW_DEPTH_F, 0);
                }

                walk -= pd.WalkSpeed; // walk on the lines

                if(walk < -1) // go back to the start and tell the component to stop spawning new ones
                {
                    l = lines[lines.Count - 1];
                    position = grid.GridIntegerToWorld(l.Start);
                    walk = lines.Count - 1;
                    return false;
                }

                return true;
            }
        }

        public void Draw()
        {
            if(status != Status.DRAW || selectedGrid == null || selectedGrid.Closed || lines.Count == 0)
                return;

            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            var camPos = camMatrix.Translation;
            var camFw = camMatrix.Forward;
            var alpha = (drawTicks < DRAW_FADE_OUT_TICKS ? (DRAW_TRANSPARENCY * ((float)drawTicks / (float)DRAW_FADE_OUT_TICKS)) : DRAW_TRANSPARENCY);
            var particleColor = COLOR_PARTICLES * alpha;

            // start dot
            var point = selectedGrid.GridIntegerToWorld(lines[lines.Count - 1].Start);
            if(IsVisibleFast(ref camPos, ref camFw, ref point))
            {
                var startPos = camPos + ((point - camPos) * DRAW_DEPTH);
                MyTransparentGeometry.AddPointBillboard(MATERIAL_DOT, Color.Green * alpha, startPos, DRAW_POINT_SIZE * DRAW_DEPTH_F, 0);
            }

            var pd = particleDataGridSize[(int)selectedGrid.GridSizeEnum]; // particle settings for the grid cell size

            // spawning particles
            if(!stopSpawning && ++delayParticles > pd.SpawnDelay)
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
            point = selectedGrid.GridIntegerToWorld(lines[0].End);
            if(IsVisibleFast(ref camPos, ref camFw, ref point))
            {
                var endPos = camPos + ((point - camPos) * DRAW_DEPTH);
                MyTransparentGeometry.AddPointBillboard(MATERIAL_DOT, Color.Red * alpha, endPos, DRAW_POINT_SIZE * DRAW_DEPTH_F, 0);
            }
        }

        private static bool IsVisibleFast(ref Vector3D camPos, ref Vector3D camFw, ref Vector3D point)
        {
            return (((camPos.X * point.X + camPos.Y * point.Y + camPos.Z * point.Z) - (camPos.X * camFw.X + camPos.Y * camFw.Y + camPos.Z * camFw.Z)) > 0);
        }
        #endregion

        #region Gamelogic update
        public void Update()
        {
            if(status == Status.DRAW) // if the path to air leak is shown then decrease the countdown timer or clear it directly if the grid suddenly vanishes.
            {
                if(--drawTicks <= 0 || selectedGrid.Closed)
                    ClearStatus();
            }

            if(status == Status.RUNNING) // notify the player that a background task is running.
                NotifyHUD("Computing path...", 100, MyFontEnum.Blue);

            if(++skipUpdates > 30) // every half a second, update the custom detail info on the currently selected air vent in the terminal.
            {
                skipUpdates = 0;

                if(viewedVentControlPanel != null)
                {
                    if(viewedVentControlPanel.Closed || MyAPIGateway.Gui.ActiveGamePlayScreen == null)
                        viewedVentControlPanel = null;
                    else
                        viewedVentControlPanel.RefreshCustomInfo();
                }
            }
        }
        #endregion

        /// <summary>
        /// Gets called when you (local script executer) look at a block in the terminal.
        /// If that happens to be an air vent we need to mark it so we constantly update the custom detail info.
        /// </summary>
        private void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            viewedVentControlPanel = (block as IMyAirVent);
        }

        /// <summary>
        /// Prints a message to the player, repeated calls to this method will overwrite the previous message on the HUD, therefore preventing spam.
        /// </summary>
        public void NotifyHUD(string message, int time = 3000, string font = MyFontEnum.White)
        {
            if(notify == null)
                notify = MyAPIGateway.Utilities.CreateNotification(string.Empty);

            notify.Text = "Air leak scan: " + message;
            notify.AliveTime = time;
            notify.Font = font;
            notify.Show();
        }

        /// <summary>
        /// Clears the lines, various status data and gracefully stops the processing thread if running.
        /// </summary>
        public void ClearStatus()
        {
            if(status == Status.RUNNING) // gracefully cancel the task by telling it to stop doing stuff and then wait for it to finish so we can clear its data.
            {
                cancelTask = true;
                task.Wait();
            }

            status = Status.IDLE;
            lines.Clear();
            particles.Clear();
            stopSpawning = false;
            drawTicks = 0;
            crumb = null;
            selectedGrid = null;
            usedFromVent = null;
        }

        private bool IsInInflatedBounds(IMyCubeGrid grid, Vector3I pos)
        {
            var min = grid.Min - Vector3I.One;
            var max = grid.Max + Vector3I.One;
            return !(min != Vector3I.Min(pos, min)) && !(max != Vector3I.Max(pos, max));
        }

        private bool IsInBounds(IMyCubeGrid grid, Vector3I pos)
        {
            return !(grid.Min != Vector3I.Min(pos, grid.Min)) && !(grid.Max != Vector3I.Max(pos, grid.Max));
        }

        public void StartThread(IMyCubeGrid grid, Vector3I start)
        {
            ClearStatus();

            selectedGrid = grid;
            startPosition = start;

            status = Status.RUNNING;
            task = MyAPIGateway.Parallel.Start(ThreadRun, ThreadFinished);
        }

        #region A* pathfinding
        /// <summary>
        /// <para>Credit for the original code to Roy T. - http://roy-t.nl/2011/09/24/another-faster-version-of-a-2d3d-in-c.html</para>
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
                    if(ShouldCancelTask)
                        return;

                    lines.Add(new Line()
                    {
                        Start = crumb.Next.Position,
                        End = crumb.Position,
                    });

                    crumb = crumb.Next;
                }
            }
        }

        private void ThreadFinished()
        {
            if(task.Exceptions != null && task.Exceptions.Length > 0)
            {
                foreach(var e in task.Exceptions)
                {
                    Log.Error("(Thread) " + e.ToString());
                }
            }

            if(ShouldCancelTask)
            {
                cancelTask = false;
                status = Status.IDLE;
                ClearStatus();

                NotifyHUD("Cancelled.", 1000, MyFontEnum.White);
            }
            else if(crumb == null)
            {
                status = Status.IDLE;
                selectedGrid = null;

                NotifyHUD("No leaks!", 2000, MyFontEnum.Green);
            }
            else
            {
                status = Status.DRAW;
                drawTicks = (int)Math.Min(lines.Count * 60 * 2 * selectedGrid.GridSize, 60 * MAX_DRAW_SECONDS);

                NotifyHUD("Found leak, follow blue lines.", 2000, MyFontEnum.White);
            }

            if(viewedVentControlPanel != null)
                viewedVentControlPanel.RefreshCustomInfo();
        }

        /// <summary>Find the closest path towards the exit.
        /// <para>Returns null if no path was found.</para>
        /// <para>NOTE: It can return null if <paramref name="start"/> is inside a closed door's position.</para>
        /// </summary>
        private Breadcrumb PathfindGenerateCrumbs(IMyCubeGrid grid, ref Vector3I start) // used in a background thread
        {
            scanned.Clear();
            openList.Clear();

            var startBreadcrumb = new Breadcrumb(start);
            openList.Add(startBreadcrumb);

            var directions = Base6Directions.IntDirections;

            while(openList.HasNext())
            {
                var crumb = openList.GetFirst();

                if(DistanceToBox(crumb.Position, grid.Min, grid.Max) < 0)
                    return crumb; // found outside of grid, terminate.

                for(int i = 0; i < directions.Length; ++i)
                {
                    if(ShouldCancelTask)
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

                        if(IsInInflatedBounds(grid, target) && !Pressurization.IsPressurized(grid, crumb.Position, target))
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
        #endregion

        /// <summary>
        /// Identifier for block position combined with direction.
        /// <para>Used for pathfinding to mark a certain move (from position towards direction) as already explored or not due to how the pressurization system works.</para>
        /// </summary>
        struct MoveId : IEquatable<MoveId>
        {
            private readonly Vector3I position;
            private readonly Vector3I direction;
            private readonly int hashCode;

            public MoveId(ref Vector3I position, ref Vector3I direction)
            {
                this.position = position;
                this.direction = direction;

                unchecked
                {
                    hashCode = 17;
                    hashCode = hashCode * 31 + position.GetHashCode();
                    hashCode = hashCode * 31 + direction.GetHashCode();
                }
            }

            public override int GetHashCode()
            {
                return hashCode;
            }

            public override bool Equals(object obj)
            {
                return obj is MoveId && Equals((MoveId)obj);
            }

            public bool Equals(MoveId other)
            {
                return position.Equals(other.position) && direction.Equals(other.direction);
            }
        }

        struct Line
        {
            public Vector3I Start;
            public Vector3I End;
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
    }
}