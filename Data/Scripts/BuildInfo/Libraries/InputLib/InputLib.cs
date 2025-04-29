using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Digi.Input.Devices;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using IMyControllableEntity = VRage.Game.ModAPI.Interfaces.IMyControllableEntity;

namespace Digi.Input
{
    /// <summary>
    /// Replacement for <see cref="MyControlsSpace"/> because it has many that don't exist anymore...
    /// </summary>
    public static class ControlIds
    {
        public static readonly MyStringId FORWARD = MyControlsSpace.FORWARD;
        public static readonly MyStringId BACKWARD = MyControlsSpace.BACKWARD;
        public static readonly MyStringId STRAFE_LEFT = MyControlsSpace.STRAFE_LEFT;
        public static readonly MyStringId STRAFE_RIGHT = MyControlsSpace.STRAFE_RIGHT;
        public static readonly MyStringId ROTATION_LEFT = MyControlsSpace.ROTATION_LEFT;
        public static readonly MyStringId ROTATION_RIGHT = MyControlsSpace.ROTATION_RIGHT;
        public static readonly MyStringId ROTATION_UP = MyControlsSpace.ROTATION_UP;
        public static readonly MyStringId ROTATION_DOWN = MyControlsSpace.ROTATION_DOWN;
        public static readonly MyStringId ROLL_LEFT = MyControlsSpace.ROLL_LEFT;
        public static readonly MyStringId ROLL_RIGHT = MyControlsSpace.ROLL_RIGHT;
        public static readonly MyStringId SPRINT = MyControlsSpace.SPRINT;
        public static readonly MyStringId SWITCH_WALK = MyControlsSpace.SWITCH_WALK;
        public static readonly MyStringId JUMP = MyControlsSpace.JUMP;
        public static readonly MyStringId CROUCH = MyControlsSpace.CROUCH;
        public static readonly MyStringId PRIMARY_TOOL_ACTION = MyControlsSpace.PRIMARY_TOOL_ACTION;
        public static readonly MyStringId SECONDARY_TOOL_ACTION = MyControlsSpace.SECONDARY_TOOL_ACTION;
        public static readonly MyStringId RELOAD = MyControlsSpace.RELOAD;
        public static readonly MyStringId USE = MyControlsSpace.USE;
        public static readonly MyStringId HELMET = MyControlsSpace.HELMET;
        public static readonly MyStringId THRUSTS = MyControlsSpace.THRUSTS;
        public static readonly MyStringId DAMPING = MyControlsSpace.DAMPING;
        public static readonly MyStringId DAMPING_RELATIVE = MyControlsSpace.DAMPING_RELATIVE;
        public static readonly MyStringId BROADCASTING = MyControlsSpace.BROADCASTING;
        public static readonly MyStringId HEADLIGHTS = MyControlsSpace.HEADLIGHTS;
        public static readonly MyStringId TERMINAL = MyControlsSpace.TERMINAL;
        public static readonly MyStringId REMOTE_ACCESS_MENU = MyControlsSpace.REMOTE_ACCESS_MENU;
        public static readonly MyStringId INVENTORY = MyControlsSpace.INVENTORY;
        public static readonly MyStringId SUICIDE = MyControlsSpace.SUICIDE;
        public static readonly MyStringId BUILD_SCREEN = MyControlsSpace.BUILD_SCREEN;
        public static readonly MyStringId CUBE_ROTATE_VERTICAL_POSITIVE = MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE;
        public static readonly MyStringId CUBE_ROTATE_VERTICAL_NEGATIVE = MyControlsSpace.CUBE_ROTATE_VERTICAL_NEGATIVE;
        public static readonly MyStringId CUBE_ROTATE_HORISONTAL_POSITIVE = MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE;
        public static readonly MyStringId CUBE_ROTATE_HORISONTAL_NEGATIVE = MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE;
        public static readonly MyStringId CUBE_ROTATE_ROLL_POSITIVE = MyControlsSpace.CUBE_ROTATE_ROLL_POSITIVE;
        public static readonly MyStringId CUBE_ROTATE_ROLL_NEGATIVE = MyControlsSpace.CUBE_ROTATE_ROLL_NEGATIVE;
        public static readonly MyStringId CUBE_COLOR_CHANGE = MyControlsSpace.CUBE_COLOR_CHANGE;
        public static readonly MyStringId CUBE_DEFAULT_MOUNTPOINT = MyControlsSpace.CUBE_DEFAULT_MOUNTPOINT;
        public static readonly MyStringId USE_SYMMETRY = MyControlsSpace.USE_SYMMETRY;
        public static readonly MyStringId SYMMETRY_SWITCH = MyControlsSpace.SYMMETRY_SWITCH;
        public static readonly MyStringId FREE_ROTATION = MyControlsSpace.FREE_ROTATION;
        public static readonly MyStringId BUILD_PLANNER = MyControlsSpace.BUILD_PLANNER;
        public static readonly MyStringId CUBE_BUILDER_CUBESIZE_MODE = MyControlsSpace.CUBE_BUILDER_CUBESIZE_MODE;
        public static readonly MyStringId CREATE_BLUEPRINT = MyControlsSpace.CREATE_BLUEPRINT;
        public static readonly MyStringId CREATE_BLUEPRINT_DETACHED = MyControlsSpace.CREATE_BLUEPRINT_DETACHED;
        public static readonly MyStringId CREATE_BLUEPRINT_MAGNETIC_LOCKS = MyControlsSpace.CREATE_BLUEPRINT_MAGNETIC_LOCKS;
        public static readonly MyStringId COPY_OBJECT = MyControlsSpace.COPY_OBJECT;
        public static readonly MyStringId COPY_OBJECT_DETACHED = MyControlsSpace.COPY_OBJECT_DETACHED;
        public static readonly MyStringId COPY_OBJECT_MAGNETIC_LOCKS = MyControlsSpace.COPY_OBJECT_MAGNETIC_LOCKS;
        public static readonly MyStringId PASTE_OBJECT = MyControlsSpace.PASTE_OBJECT;
        public static readonly MyStringId CUT_OBJECT = MyControlsSpace.CUT_OBJECT;
        public static readonly MyStringId CUT_OBJECT_DETACHED = MyControlsSpace.CUT_OBJECT_DETACHED;
        public static readonly MyStringId CUT_OBJECT_MAGNETIC_LOCKS = MyControlsSpace.CUT_OBJECT_MAGNETIC_LOCKS;
        public static readonly MyStringId DELETE_OBJECT = MyControlsSpace.DELETE_OBJECT;
        public static readonly MyStringId DELETE_OBJECT_DETACHED = MyControlsSpace.DELETE_OBJECT_DETACHED;
        public static readonly MyStringId DELETE_OBJECT_MAGNETIC_LOCKS = MyControlsSpace.DELETE_OBJECT_MAGNETIC_LOCKS;
        public static readonly MyStringId TOOLBAR_NEXT_ITEM = MyControlsSpace.TOOLBAR_NEXT_ITEM;
        public static readonly MyStringId TOOLBAR_PREV_ITEM = MyControlsSpace.TOOLBAR_PREV_ITEM;
        public static readonly MyStringId SLOT1 = MyControlsSpace.SLOT1;
        public static readonly MyStringId SLOT2 = MyControlsSpace.SLOT2;
        public static readonly MyStringId SLOT3 = MyControlsSpace.SLOT3;
        public static readonly MyStringId SLOT4 = MyControlsSpace.SLOT4;
        public static readonly MyStringId SLOT5 = MyControlsSpace.SLOT5;
        public static readonly MyStringId SLOT6 = MyControlsSpace.SLOT6;
        public static readonly MyStringId SLOT7 = MyControlsSpace.SLOT7;
        public static readonly MyStringId SLOT8 = MyControlsSpace.SLOT8;
        public static readonly MyStringId SLOT9 = MyControlsSpace.SLOT9;
        public static readonly MyStringId SLOT0 = MyControlsSpace.SLOT0;
        public static readonly MyStringId TOOLBAR_UP = MyControlsSpace.TOOLBAR_UP;
        public static readonly MyStringId TOOLBAR_DOWN = MyControlsSpace.TOOLBAR_DOWN;
        public static readonly MyStringId PAGE1 = MyControlsSpace.PAGE1;
        public static readonly MyStringId PAGE2 = MyControlsSpace.PAGE2;
        public static readonly MyStringId PAGE3 = MyControlsSpace.PAGE3;
        public static readonly MyStringId PAGE4 = MyControlsSpace.PAGE4;
        public static readonly MyStringId PAGE5 = MyControlsSpace.PAGE5;
        public static readonly MyStringId PAGE6 = MyControlsSpace.PAGE6;
        public static readonly MyStringId PAGE7 = MyControlsSpace.PAGE7;
        public static readonly MyStringId PAGE8 = MyControlsSpace.PAGE8;
        public static readonly MyStringId PAGE9 = MyControlsSpace.PAGE9;
        public static readonly MyStringId PAGE0 = MyControlsSpace.PAGE0;
        public static readonly MyStringId SWITCH_LEFT = MyControlsSpace.SWITCH_LEFT;
        public static readonly MyStringId SWITCH_RIGHT = MyControlsSpace.SWITCH_RIGHT;
        public static readonly MyStringId LANDING_GEAR = MyControlsSpace.LANDING_GEAR;
        public static readonly MyStringId TOGGLE_REACTORS = MyControlsSpace.TOGGLE_REACTORS;
        public static readonly MyStringId TOGGLE_REACTORS_ALL = MyControlsSpace.TOGGLE_REACTORS_ALL;
        public static readonly MyStringId COLOR_PICKER = MyControlsSpace.COLOR_PICKER;
        public static readonly MyStringId QUICK_PICK_COLOR = MyControlsSpace.QUICK_PICK_COLOR;
        public static readonly MyStringId VOXEL_HAND_SETTINGS = MyControlsSpace.VOXEL_HAND_SETTINGS;
        public static readonly MyStringId VOICE_CHAT = MyControlsSpace.VOICE_CHAT;
        public static readonly MyStringId EXPORT_MODEL = MyControlsSpace.EXPORT_MODEL;
        public static readonly MyStringId QUICK_LOAD_RECONNECT = MyControlsSpace.QUICK_LOAD_RECONNECT;
        public static readonly MyStringId QUICK_SAVE = MyControlsSpace.QUICK_SAVE;
        public static readonly MyStringId PAUSE_GAME = MyControlsSpace.PAUSE_GAME;
        public static readonly MyStringId HELP_SCREEN = MyControlsSpace.HELP_SCREEN;
        public static readonly MyStringId WARNING_SCREEN = MyControlsSpace.WARNING_SCREEN;
        public static readonly MyStringId PLAYERS_SCREEN = MyControlsSpace.PLAYERS_SCREEN;
        public static readonly MyStringId BLUEPRINTS_MENU = MyControlsSpace.BLUEPRINTS_MENU;
        public static readonly MyStringId ADMIN_MENU = MyControlsSpace.ADMIN_MENU;
        public static readonly MyStringId SPAWN_MENU = MyControlsSpace.SPAWN_MENU;
        public static readonly MyStringId CONTROL_MENU = MyControlsSpace.CONTROL_MENU;
        public static readonly MyStringId ACTIVE_CONTRACT_SCREEN = MyControlsSpace.ACTIVE_CONTRACT_SCREEN;
        public static readonly MyStringId CHAT_SCREEN = MyControlsSpace.CHAT_SCREEN;
        public static readonly MyStringId CONSOLE = MyControlsSpace.CONSOLE;
        public static readonly MyStringId SPECTATOR_NONE = MyControlsSpace.SPECTATOR_NONE;
        public static readonly MyStringId SPECTATOR_DELTA = MyControlsSpace.SPECTATOR_DELTA;
        public static readonly MyStringId SPECTATOR_FREE = MyControlsSpace.SPECTATOR_FREE;
        public static readonly MyStringId SPECTATOR_STATIC = MyControlsSpace.SPECTATOR_STATIC;
        public static readonly MyStringId CAMERA_MODE = MyControlsSpace.CAMERA_MODE;
        public static readonly MyStringId LOOKAROUND = MyControlsSpace.LOOKAROUND;
        public static readonly MyStringId SCREENSHOT = MyControlsSpace.SCREENSHOT;
        public static readonly MyStringId TOGGLE_HUD = MyControlsSpace.TOGGLE_HUD;
        public static readonly MyStringId TOGGLE_SIGNALS = MyControlsSpace.TOGGLE_SIGNALS;
        public static readonly MyStringId SPECTATOR_LOCK = MyControlsSpace.SPECTATOR_LOCK;
        public static readonly MyStringId SPECTATOR_SWITCHMODE = MyControlsSpace.SPECTATOR_SWITCHMODE;
        public static readonly MyStringId SPECTATOR_NEXTPLAYER = MyControlsSpace.SPECTATOR_NEXTPLAYER;
        public static readonly MyStringId SPECTATOR_PREVPLAYER = MyControlsSpace.SPECTATOR_PREVPLAYER;
    }

    public static class GamepadControlIds
    {
        public static readonly MyStringId MOVE_FURTHER = MyControlsSpace.MOVE_FURTHER;
        public static readonly MyStringId MOVE_CLOSER = MyControlsSpace.MOVE_CLOSER;
    }

    /// <summary>
    /// <para>Usage: create an instance in BeforeStart(); call Update() in any simulation update; Dispose() in UnloadData().</para>
    /// </summary>
    public class InputLib : IDisposable
    {
        #region Input combination inner class
        /// <summary>
        /// Usage: <see cref="Create(string, out string)"/>
        /// </summary>
        public class Combination
        {
            #region Factory static methods
            /// <summary>
            /// Creates an input combination object which can be used to easily check if that combination is pressed.
            /// <para>Throws an exception for errors, if you want the errors as a string instead use <seealso cref="Create(string, out string)"/></para>.
            /// </summary>
            /// <param name="displayName">Name of combination, shown when has no bindings.</param>
            /// <param name="combinationString">Space separated input IDs (not case sensitive).</param>
            /// <param name="invalidInputs">supply inputs that can't be used in this combination, for example custom mod-added ones in themselves!</param>
            public static Combination Create(string displayName, string combinationString, List<string> invalidInputs = null)
            {
                string error;
                Combination combination = Create(displayName, combinationString, out error, invalidInputs);

                if(error != null)
                    throw new Exception(error);

                return combination;
            }

            /// <summary>
            /// Creates an input combination object which can be used to easily check if that combination is pressed.
            /// </summary>
            /// <param name="displayName">Name of combination, shown when has no bindings.</param>
            /// <param name="combinationString">Space separated input IDs (not case sensitive).</param>
            /// <param name="error">If an error occurs this will be non-null.</param>
            /// <param name="invalidInputs">supply inputs that can't be used in this combination, for example custom mod-added ones in themselves!</param>
            /// <returns>null if there are any errors.</returns>
            public static Combination Create(string displayName, string combinationString, out string error, List<string> invalidInputs = null)
            {
                if(InputLib.instance == null)
                    throw new Exception($"{typeof(InputLib).Name} was not initialized! Create an instance of it in your mod first.");

                if(string.IsNullOrWhiteSpace(combinationString))
                {
                    error = null;
                    return new Combination(displayName); // valid empty combination
                }

                string[] inputStrings = combinationString.ToLowerInvariant().Split(InputLib.instance.CHAR_ARRAY, StringSplitOptions.RemoveEmptyEntries);

                StringBuilder str = new StringBuilder();
                List<InputBase> combInputs = new List<InputBase>();

                foreach(string inputId in inputStrings)
                {
                    InputBase input;

                    if(invalidInputs != null && invalidInputs.Contains(inputId))
                    {
                        error = $"Input '{inputId}' not allowed for this input combination!";
                        return null;
                    }

                    if(!InputLib.instance.inputs.TryGetValue(inputId, out input))
                    {
                        error = $"Can't find inputId: {inputId}";
                        return null;
                    }

                    combInputs.Add(input);

                    if(str.Length > 0)
                        str.Append(' ');

                    str.Append(inputId);
                }

                if(combInputs.Count == 0)
                    combInputs = null;

                Combination combination = new Combination(displayName, combInputs, str.ToString());

                error = null;
                return combination;
            }
            #endregion Factory static methods

            private readonly List<InputBase> inputs = null;
            public readonly string CombinationString;
            public readonly string DisplayName;

            /// <summary>
            /// Not to be used directly.
            /// Instead, use <see cref="Create(string, string, List{string})"/> or <seealso cref="Create(string, string, out string, List{string})"/>.
            /// </summary>
            private Combination(string displayName, List<InputBase> inputs = null, string combinationString = "")
            {
                this.inputs = inputs;
                DisplayName = displayName;
                CombinationString = combinationString;
            }

            /// <summary>
            /// Checks if combination has inputs and if all of them are binded to anything.
            /// </summary>
            public bool IsAssigned(ControlContext contextId = ControlContext.CHARACTER)
            {
                if(inputs == null)
                    return false;

                foreach(InputBase input in inputs)
                {
                    if(!input.IsAssigned())
                        return false;
                }

                return true;
            }

            /// <summary>
            /// Returns true if all the inputs are being pressed/held.
            /// </summary>
            public bool IsPressed(ControlContext contextId = ControlContext.CHARACTER)
            {
                if(inputs == null)
                    return false;

                foreach(InputBase input in inputs)
                {
                    if(!input.IsPressed(contextId))
                        return false;
                }

                return true;
            }

            /// <summary>
            /// Returns true the first time this gets called while all inputs are being held, gets reset when any of them are released (updated outside of this method)
            /// <para>NOTE: <see cref="InputLib"/> requires <see cref="UpdateInput"/> to be called in HandleInput() for this method to work properly.</para>
            /// </summary>
            public bool IsJustPressed(ControlContext contextId = ControlContext.CHARACTER)
            {
                if(inputs == null)
                    return false;

                InputReleaseKey key = new InputReleaseKey(this, contextId, InputLib.instance.tick);

                foreach(InputReleaseKey irk in InputLib.instance.pressedCombinations)
                {
                    if(irk.Equals(key))
                    {
                        return (irk.Tick == key.Tick); // same tick multiple-check fix
                    }
                }

                bool allHeld = IsPressed(contextId);

                if(allHeld)
                {
                    InputLib.instance.pressedCombinations.Add(key);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Same as <seealso cref="GetBinds(StringBuilder, ControlContext, bool)"/> but with a string allocation.
            /// </summary>
            /// <param name="contextId">Control context, <see cref="InputLib.GetCurrentInputContext"/></param>
            /// <param name="specialChars">Whether to add special characters like the xbox character-images to the string or to use regular characters.</param>
            public string GetBinds(ControlContext contextId = ControlContext.CHARACTER, bool specialChars = true)
            {
                StringBuilder str = new StringBuilder();
                GetBinds(str, contextId, specialChars);
                return str.ToString();
            }

            /// <summary>
            /// Gets the combination into a parseable format.
            /// </summary>
            /// <param name="output">What to append the info to</param>
            /// <param name="contextId">Control context, <see cref="InputLib.GetCurrentInputContext"/></param>
            /// <param name="specialChars">Whether to add special characters like the xbox character-images to the string or to use regular characters.</param>
            public void GetBinds(StringBuilder output, ControlContext contextId = ControlContext.CHARACTER, bool specialChars = true)
            {
                if(output == null)
                    throw new ArgumentNullException("output must not be null.");

                if(inputs == null)
                {
                    output.Append("<Unassigned>");
                    return;
                }

                bool first = true;

                foreach(InputBase input in inputs)
                {
                    if(!first)
                        output.Append(InputLib.INPUT_PRINT_SEPARATOR);

                    input.GetBind(output, contextId, specialChars);
                    first = false;
                }
            }

            /// <summary>
            /// Returns the combination string.
            /// </summary>
            public override string ToString()
            {
                return (string.IsNullOrWhiteSpace(CombinationString) ? "<Unassigned>" : CombinationString);
            }

            public static bool CombinationEqual(Combination c1, Combination c2)
            {
                if(c1 == c2)
                    return true;

                if(c1 == null || c2 == null)
                    return false;

                if(c1.inputs == c2.inputs)
                    return true;

                if(c1.inputs == null || c2.inputs == null)
                    return false;

                if(c1.inputs.Count != c2.inputs.Count)
                    return false;

                // TODO: improvements?
                foreach(InputBase c1input in c1.inputs)
                {
                    bool found = false;

                    foreach(InputBase c2input in c2.inputs)
                    {
                        if(c1input.Id == c2input.Id)
                        {
                            found = true;
                            break;
                        }
                    }

                    if(!found)
                        return false;
                }

                return true;
            }
        }
        #endregion Input combination inner class

        public const string MOUSE_PREFIX = "m.";
        public const string GAMEPAD_PREFIX = "g.";
        public const string CONTROL_PREFIX = "c.";

        public const string INPUT_PRINT_SEPARATOR = " ";

        public const float EPSILON = 0.000001f;

        private static InputLib instance;

        private readonly Dictionary<string, InputBase> inputs = new Dictionary<string, InputBase>();
        private readonly Dictionary<MyKeys, InputBase> keyToInput = new Dictionary<MyKeys, InputBase>();
        private readonly Dictionary<MyMouseButtonsEnum, InputBase> mouseButtonToInput = new Dictionary<MyMouseButtonsEnum, InputBase>();
        private readonly Dictionary<MyJoystickAxesEnum, InputBase> gamepadAxisToInput = new Dictionary<MyJoystickAxesEnum, InputBase>();
        private readonly Dictionary<MyJoystickButtonsEnum, InputBase> gamepadButtonToInput = new Dictionary<MyJoystickButtonsEnum, InputBase>();
        private readonly Dictionary<MyStringId, InputBase> gameControlToInput = new Dictionary<MyStringId, InputBase>(MyStringId.Comparer);

        private readonly GamepadBindings gamepadBindings;

        public readonly char[] CHAR_ARRAY = { ' ' };

        public static readonly MyStringId CX_BASE = MyStringId.GetOrCompute("BASE");
        public static readonly MyStringId CX_GUI = MyStringId.GetOrCompute("GUI");
        /// <summary>
        /// just for future ref... ideally use Sandbox.Game.Entities.IMyControllableEntity's ControlContext instead
        /// </summary>
        public static readonly MyStringId CX_CHARACTER = MyStringId.GetOrCompute("CHARACTER");
        public static readonly MyStringId CX_JETPACK = MyStringId.GetOrCompute("JETPACK");
        public static readonly MyStringId CX_SPACESHIP = MyStringId.GetOrCompute("SPACESHIP");
        public static readonly MyStringId CX_SPECTATOR = MyStringId.GetOrCompute("SPECTATOR");

        private int tick;
        private List<InputReleaseKey> pressedCombinations = new List<InputReleaseKey>(); // Used by InputCombination to monitor releases for IsJustPressed().

        #region Required methods
        public InputLib()
        {
            if(instance != null)
                throw new Exception($"Multiple instances of {typeof(InputLib).Name} are not supported!");

            instance = this;
            gamepadBindings = new GamepadBindings();

            #region Keyboard inputs
            AddInput(MyKeys.Control, "ctrl");
            AddInput(MyKeys.LeftControl, "leftctrl", "Left Ctrl");
            AddInput(MyKeys.RightControl, "rightctrl", "Right Ctrl");
            AddInput(MyKeys.Shift, "shift");
            AddInput(MyKeys.LeftShift, "leftshift", "Left Shift");
            AddInput(MyKeys.RightShift, "rightshift", "Right Shift");
            AddInput(MyKeys.Alt, "alt");
            AddInput(MyKeys.LeftAlt, "leftalt", "Left Alt");
            AddInput(MyKeys.RightAlt, "rightalt", "Right Alt");
            AddInput(MyKeys.Apps);
            AddInput(MyKeys.Up, null, "Arrow Up");
            AddInput(MyKeys.Down, null, "Arrow Down");
            AddInput(MyKeys.Left, null, "Arrow Left");
            AddInput(MyKeys.Right, null, "Arrow Right");
            AddInput(MyKeys.A);
            AddInput(MyKeys.B);
            AddInput(MyKeys.C);
            AddInput(MyKeys.D);
            AddInput(MyKeys.E);
            AddInput(MyKeys.F);
            AddInput(MyKeys.G);
            AddInput(MyKeys.H);
            AddInput(MyKeys.I);
            AddInput(MyKeys.J);
            AddInput(MyKeys.K);
            AddInput(MyKeys.L);
            AddInput(MyKeys.M);
            AddInput(MyKeys.N);
            AddInput(MyKeys.O);
            AddInput(MyKeys.P);
            AddInput(MyKeys.Q);
            AddInput(MyKeys.R);
            AddInput(MyKeys.S);
            AddInput(MyKeys.T);
            AddInput(MyKeys.U);
            AddInput(MyKeys.V);
            AddInput(MyKeys.W);
            AddInput(MyKeys.X);
            AddInput(MyKeys.Y);
            AddInput(MyKeys.Z);
            AddInput(MyKeys.D0, "0");
            AddInput(MyKeys.D1, "1");
            AddInput(MyKeys.D2, "2");
            AddInput(MyKeys.D3, "3");
            AddInput(MyKeys.D4, "4");
            AddInput(MyKeys.D5, "5");
            AddInput(MyKeys.D6, "6");
            AddInput(MyKeys.D7, "7");
            AddInput(MyKeys.D8, "8");
            AddInput(MyKeys.D9, "9");
            AddInput(MyKeys.F1);
            AddInput(MyKeys.F2);
            AddInput(MyKeys.F3);
            AddInput(MyKeys.F4);
            AddInput(MyKeys.F5);
            AddInput(MyKeys.F6);
            AddInput(MyKeys.F7);
            AddInput(MyKeys.F8);
            AddInput(MyKeys.F9);
            AddInput(MyKeys.F10);
            AddInput(MyKeys.F11);
            AddInput(MyKeys.F12);
            AddInput(MyKeys.NumLock);
            AddInput(MyKeys.NumPad0, "num0", "Numpad 0");
            AddInput(MyKeys.NumPad1, "num1", "Numpad 1");
            AddInput(MyKeys.NumPad2, "num2", "Numpad 2");
            AddInput(MyKeys.NumPad3, "num3", "Numpad 3");
            AddInput(MyKeys.NumPad4, "num4", "Numpad 4");
            AddInput(MyKeys.NumPad5, "num5", "Numpad 5");
            AddInput(MyKeys.NumPad6, "num6", "Numpad 6");
            AddInput(MyKeys.NumPad7, "num7", "Numpad 7");
            AddInput(MyKeys.NumPad8, "num8", "Numpad 8");
            AddInput(MyKeys.NumPad9, "num9", "Numpad 9");
            AddInput(MyKeys.Multiply, "nummultiply", "Numpad *");
            AddInput(MyKeys.Subtract, "numsubtract", "Numpad -");
            AddInput(MyKeys.Add, "numadd", "Numpad +");
            AddInput(MyKeys.Divide, "numdivide", "Numpad /");
            AddInput(MyKeys.Decimal, "numdecimal", "Numpad .");
            AddInput(MyKeys.OemBackslash, "backslash", "/");
            AddInput(MyKeys.OemComma, "comma", ",");
            AddInput(MyKeys.OemMinus, "minus", "-");
            AddInput(MyKeys.OemPeriod, "period", ".");
            AddInput(MyKeys.OemPipe, "pipe", "|");
            AddInput(MyKeys.OemPlus, "plus", "+");
            AddInput(MyKeys.OemQuestion, "question", "?");
            AddInput(MyKeys.OemQuotes, "quotes", "\"");
            AddInput(MyKeys.OemSemicolon, "semicolon", ";");
            AddInput(MyKeys.OemOpenBrackets, "openbrackets", "{");
            AddInput(MyKeys.OemCloseBrackets, "closebrackets", "}");
            AddInput(MyKeys.OemTilde, "tilde", "`");
            AddInput(MyKeys.Tab);
            AddInput(MyKeys.CapsLock, null, "Caps Lock");
            AddInput(MyKeys.Enter);
            AddInput(MyKeys.Back, "backspace");
            AddInput(MyKeys.Space);
            AddInput(MyKeys.Delete);
            AddInput(MyKeys.Insert);
            AddInput(MyKeys.Home);
            AddInput(MyKeys.End);
            AddInput(MyKeys.PageUp, null, "Page Up");
            AddInput(MyKeys.PageDown, null, "Page Down");
            AddInput(MyKeys.ScrollLock, null, "Scroll Lock");
            AddInput(MyKeys.Pause);
            #endregion Keyboard inputs

            #region Mouse inputs
            AddInput(MyMouseButtonsEnum.Left, MOUSE_PREFIX + "left", "Mouse LeftClick");
            AddInput(MyMouseButtonsEnum.Right, MOUSE_PREFIX + "right", "Mouse RightClick");
            AddInput(MyMouseButtonsEnum.Middle, MOUSE_PREFIX + "middle", "Mouse MiddleClick");
            AddInput(MyMouseButtonsEnum.XButton1, MOUSE_PREFIX + "button4", "Mouse Button 4");
            AddInput(MyMouseButtonsEnum.XButton2, MOUSE_PREFIX + "button5", "Mouse Button 5");
            AddInput(new InputMouseAnalog());
            AddInput(new InputMouseScroll());
            AddInput(new InputMouseScrollUp());
            AddInput(new InputMouseScrollDown());
            AddInput(new InputMouseX());
            AddInput(new InputMouseY());
            AddInput(new InputMouseXPos());
            AddInput(new InputMouseXNeg());
            AddInput(new InputMouseYPos());
            AddInput(new InputMouseYNeg());
            #endregion Mouse inputs

            #region Gamepad inputs
            AddInput(MyJoystickButtonsEnum.J01, GAMEPAD_PREFIX + "a", printChar: '\xe001');
            AddInput(MyJoystickButtonsEnum.J02, GAMEPAD_PREFIX + "b", printChar: '\xe003');
            AddInput(MyJoystickButtonsEnum.J03, GAMEPAD_PREFIX + "x", printChar: '\xe002');
            AddInput(MyJoystickButtonsEnum.J04, GAMEPAD_PREFIX + "y", printChar: '\xe004');

            AddInput(MyJoystickButtonsEnum.J05, GAMEPAD_PREFIX + "lb", "Left Bumper", printChar: '\xe005');
            AddInput(MyJoystickButtonsEnum.J06, GAMEPAD_PREFIX + "rb", "Right Bumper", printChar: '\xe006');

            AddInput(MyJoystickButtonsEnum.J07, GAMEPAD_PREFIX + "back", printChar: '\xe00d');
            AddInput(MyJoystickButtonsEnum.J08, GAMEPAD_PREFIX + "start", printChar: '\xe00e');

            AddInput(MyJoystickButtonsEnum.J09, GAMEPAD_PREFIX + "ls", "Left Stick Click", printChar: '\xe00b');
            AddInput(MyJoystickButtonsEnum.J10, GAMEPAD_PREFIX + "rs", "Right Stick Click", printChar: '\xe00c');

            AddInput(MyJoystickButtonsEnum.J11, GAMEPAD_PREFIX + "j11");
            AddInput(MyJoystickButtonsEnum.J12, GAMEPAD_PREFIX + "j12");
            AddInput(MyJoystickButtonsEnum.J13, GAMEPAD_PREFIX + "j13");
            AddInput(MyJoystickButtonsEnum.J14, GAMEPAD_PREFIX + "j14");
            AddInput(MyJoystickButtonsEnum.J15, GAMEPAD_PREFIX + "j15");
            AddInput(MyJoystickButtonsEnum.J16, GAMEPAD_PREFIX + "j16");

            AddInput(MyJoystickButtonsEnum.JDUp, GAMEPAD_PREFIX + "dpadup", "D-Pad Up", printChar: '\xe011');
            AddInput(MyJoystickButtonsEnum.JDDown, GAMEPAD_PREFIX + "dpaddown", "D-Pad Down", printChar: '\xe013');
            AddInput(MyJoystickButtonsEnum.JDLeft, GAMEPAD_PREFIX + "dpadleft", "D-Pad Left", printChar: '\xe010');
            AddInput(MyJoystickButtonsEnum.JDRight, GAMEPAD_PREFIX + "dpadright", "D-Pad Right", printChar: '\xe012');

            AddInput(new InputGamepadLeftTrigger());
            AddInput(new InputGamepadRightTrigger());
            AddInput(MyJoystickAxesEnum.Zpos, GAMEPAD_PREFIX + "lt", "Left Trigger", printChar: '\xe008');
            AddInput(MyJoystickAxesEnum.Zneg, GAMEPAD_PREFIX + "rt", "Right Trigger", printChar: '\xe007');

            AddInput(new InputGamepadLeftStick());
            AddInput(MyJoystickAxesEnum.Xpos, GAMEPAD_PREFIX + "lsright", "Left Stick Right", printChar: '\xe015');
            AddInput(MyJoystickAxesEnum.Xneg, GAMEPAD_PREFIX + "lsleft", "Left Stick Left", printChar: '\xe016');
            AddInput(MyJoystickAxesEnum.Ypos, GAMEPAD_PREFIX + "lsdown", "Left Stick Down", printChar: '\xe014');
            AddInput(MyJoystickAxesEnum.Yneg, GAMEPAD_PREFIX + "lsup", "Left Stick Up", printChar: '\xe017');

            AddInput(new InputGamepadRightStick());
            AddInput(MyJoystickAxesEnum.RotationXpos, GAMEPAD_PREFIX + "rsright", "Right Stick Right", printChar: '\xe019');
            AddInput(MyJoystickAxesEnum.RotationXneg, GAMEPAD_PREFIX + "rsleft", "Right Stick Left", printChar: '\xe020');
            AddInput(MyJoystickAxesEnum.RotationYpos, GAMEPAD_PREFIX + "rsdown", "Right Stick Down", printChar: '\xe018');
            AddInput(MyJoystickAxesEnum.RotationYneg, GAMEPAD_PREFIX + "rsup", "Right Stick Up", printChar: '\xe021');

            AddInput(MyJoystickAxesEnum.RotationZpos, GAMEPAD_PREFIX + "rotz+", "Rotation Z+");
            AddInput(MyJoystickAxesEnum.RotationZneg, GAMEPAD_PREFIX + "rotz-", "Rotation Z-");

            AddInput(MyJoystickAxesEnum.Slider1pos, GAMEPAD_PREFIX + "slider1+");
            AddInput(MyJoystickAxesEnum.Slider1neg, GAMEPAD_PREFIX + "slider1-");

            AddInput(MyJoystickAxesEnum.Slider2pos, GAMEPAD_PREFIX + "slider2+");
            AddInput(MyJoystickAxesEnum.Slider2neg, GAMEPAD_PREFIX + "slider2-");
            #endregion Gamepad inputs

            #region Game controls inputs
            AddInput(new InputGameControlMovement());
            AddInput(new InputGameControlRotation());
            AddInput(MyControlsSpace.FORWARD);
            AddInput(MyControlsSpace.BACKWARD);
            AddInput(MyControlsSpace.STRAFE_LEFT, CONTROL_PREFIX + "strafeleft", "Strafe Left");
            AddInput(MyControlsSpace.STRAFE_RIGHT, CONTROL_PREFIX + "straferight", "Strafe Right");
            AddInput(MyControlsSpace.ROLL_LEFT, CONTROL_PREFIX + "rollleft", "Roll Left");
            AddInput(MyControlsSpace.ROLL_RIGHT, CONTROL_PREFIX + "rollright", "Roll Right");
            AddInput(MyControlsSpace.SPRINT);
            AddInput(MyControlsSpace.PRIMARY_TOOL_ACTION, CONTROL_PREFIX + "primaryaction", "Use tool/fire weapon");
            AddInput(MyControlsSpace.SECONDARY_TOOL_ACTION, CONTROL_PREFIX + "secondaryaction", "Secondary fire/aim");
            AddInput(MyControlsSpace.JUMP);
            AddInput(MyControlsSpace.CROUCH);
            AddInput(MyControlsSpace.SWITCH_WALK, CONTROL_PREFIX + "walk", "Toggle Walk");
            AddInput(MyControlsSpace.USE, null, "Use/Interact");
            AddInput(MyControlsSpace.RELOAD);
            AddInput(MyControlsSpace.TERMINAL, null, "Terminal/Inventory");
            AddInput(MyControlsSpace.INVENTORY);
            AddInput(MyControlsSpace.CONTROL_MENU, CONTROL_PREFIX + "controlmenu", "Control Menu");
            AddInput(MyControlsSpace.ROTATION_LEFT, CONTROL_PREFIX + "lookleft", "Look Left");
            AddInput(MyControlsSpace.ROTATION_RIGHT, CONTROL_PREFIX + "lookright", "Look Right");
            AddInput(MyControlsSpace.ROTATION_UP, CONTROL_PREFIX + "lookup", "Look Up");
            AddInput(MyControlsSpace.ROTATION_DOWN, CONTROL_PREFIX + "lookdown", "Look Down");
            AddInput(MyControlsSpace.HEADLIGHTS, CONTROL_PREFIX + "light", "Lights");
            AddInput(MyControlsSpace.HELMET);
            AddInput(MyControlsSpace.THRUSTS, null, "Thrusters");
            AddInput(MyControlsSpace.DAMPING, null, "Dampeners");
            AddInput(MyControlsSpace.BROADCASTING);
            AddInput(MyControlsSpace.TOGGLE_REACTORS, CONTROL_PREFIX + "reactors", "Toggle Ship Power");
            AddInput(MyControlsSpace.LANDING_GEAR, CONTROL_PREFIX + "landinggear", "Landing Gear/Color Menu");
            AddInput(MyControlsSpace.LOOKAROUND, null, "Look Around");
            AddInput(MyControlsSpace.CAMERA_MODE, CONTROL_PREFIX + "cameramode", "Camera Mode");
            AddInput(MyControlsSpace.BUILD_SCREEN, CONTROL_PREFIX + "buildmenu", "Build Screen");
            AddInput(MyControlsSpace.CUBE_COLOR_CHANGE, CONTROL_PREFIX + "paint", "Paint/Weapon Mode");
            AddInput(MyControlsSpace.SWITCH_LEFT, CONTROL_PREFIX + "switchleft", "Prev Color/Camera");
            AddInput(MyControlsSpace.SWITCH_RIGHT, CONTROL_PREFIX + "switchright", "Next Color/Camera");
            AddInput(MyControlsSpace.SLOT1, null, "Slot 0/Unequip");
            AddInput(MyControlsSpace.SLOT2, null, "Slot 1");
            AddInput(MyControlsSpace.SLOT3, null, "Slot 2");
            AddInput(MyControlsSpace.SLOT4, null, "Slot 3");
            AddInput(MyControlsSpace.SLOT5, null, "Slot 4");
            AddInput(MyControlsSpace.SLOT6, null, "Slot 5");
            AddInput(MyControlsSpace.SLOT7, null, "Slot 6");
            AddInput(MyControlsSpace.SLOT8, null, "Slot 7");
            AddInput(MyControlsSpace.SLOT9, null, "Slot 8");
            AddInput(MyControlsSpace.SLOT0, null, "Slot 9");
            AddInput(MyControlsSpace.TOOLBAR_UP, CONTROL_PREFIX + "nexttoolbar", "Next Toolbar");
            AddInput(MyControlsSpace.TOOLBAR_DOWN, CONTROL_PREFIX + "prevtoolbar", "Prev Toolbar");
            AddInput(MyControlsSpace.TOOLBAR_NEXT_ITEM, CONTROL_PREFIX + "nextitem", "Next Toolbar Item");
            AddInput(MyControlsSpace.TOOLBAR_PREV_ITEM, CONTROL_PREFIX + "previtem", "Prev Toolbar Item");
            AddInput(MyControlsSpace.CUBE_BUILDER_CUBESIZE_MODE, CONTROL_PREFIX + "cubesizemode", "Cube Size Mode");
            AddInput(MyControlsSpace.FREE_ROTATION, CONTROL_PREFIX + "freerotation", "Free Rotation");
            AddInput(MyControlsSpace.CUBE_DEFAULT_MOUNTPOINT, CONTROL_PREFIX + "stationrotation", "Station Rotation");
            AddInput(MyControlsSpace.SYMMETRY_SWITCH, CONTROL_PREFIX + "cyclesymmetry", "Cycle Symmetry");
            AddInput(MyControlsSpace.USE_SYMMETRY, CONTROL_PREFIX + "symmetry", "Toggle Symmetry");
            AddInput(MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE, CONTROL_PREFIX + "cuberotatey+", "Cube Rotate Y+");
            AddInput(MyControlsSpace.CUBE_ROTATE_VERTICAL_NEGATIVE, CONTROL_PREFIX + "cuberotatey-", "Cube Rotate Y-");
            AddInput(MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE, CONTROL_PREFIX + "cuberotatex+", "Cube Rotate X+");
            AddInput(MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE, CONTROL_PREFIX + "cuberotatex-", "Cube Rotate X-");
            AddInput(MyControlsSpace.CUBE_ROTATE_ROLL_POSITIVE, CONTROL_PREFIX + "cuberotatez+", "Cube Rotate Z+");
            AddInput(MyControlsSpace.CUBE_ROTATE_ROLL_NEGATIVE, CONTROL_PREFIX + "cuberotatez-", "Cube Rotate Z-");
            AddInput(MyControlsSpace.TOGGLE_HUD, CONTROL_PREFIX + "togglehud", "Toggle HUD");
            AddInput(MyControlsSpace.TOGGLE_SIGNALS, CONTROL_PREFIX + "togglesignals", "Toggle Signals");
            AddInput(MyControlsSpace.SUICIDE);
            AddInput(MyControlsSpace.CHAT_SCREEN, CONTROL_PREFIX + "chat", "Chat");
            AddInput(MyControlsSpace.PAUSE_GAME, CONTROL_PREFIX + "pause");
            AddInput(MyControlsSpace.SCREENSHOT);
            AddInput(MyControlsSpace.CONSOLE);
            AddInput(MyControlsSpace.HELP_SCREEN, CONTROL_PREFIX + "help", "Help Screen");
            AddInput(MyControlsSpace.SPECTATOR_NONE, CONTROL_PREFIX + "specnone", "Spectator None");
            AddInput(MyControlsSpace.SPECTATOR_DELTA, CONTROL_PREFIX + "specdelta", "Spectator Delta");
            AddInput(MyControlsSpace.SPECTATOR_FREE, CONTROL_PREFIX + "specfree", "Spectator Free");
            AddInput(MyControlsSpace.SPECTATOR_STATIC, CONTROL_PREFIX + "specstatic", "Spectator Static");
            AddInput(MyControlsSpace.VOICE_CHAT, CONTROL_PREFIX + "voicechat", "Voice Chat");
            AddInput(MyControlsSpace.VOXEL_HAND_SETTINGS, CONTROL_PREFIX + "voxelhandsettings", "Voxel Hand Settings");

            // TODO: add DAMPING_RELATIVE and other new stuff..

            //Control PICK_UP doesn't exist.
            //Control FACTIONS_MENU doesn't exist.
            //Control SWITCH_COMPOUND doesn't exist.
            //Control SWITCH_BUILDING_MODE doesn't exist.
            //Control VOXEL_PAINT doesn't exist.
            //Control BUILD_MODE doesn't exist.
            //Control NEXT_BLOCK_STAGE doesn't exist.
            //Control PREV_BLOCK_STAGE doesn't exist.
            //Control MOVE_CLOSER doesn't exist.
            //Control MOVE_FURTHER doesn't exist.
            //Control PRIMARY_BUILD_ACTION doesn't exist.
            //Control SECONDARY_BUILD_ACTION doesn't exist.
            //Control COPY_PASTE_ACTION doesn't exist.
            #endregion Game controls inputs
        }

        public void Dispose()
        {
            instance = null;
        }

        public void UpdateInput()
        {
            // used for identifying same-tick combination checks to not return different values
            unchecked
            {
                ++tick;
            }

            // monitor the release of combinations that used IsJustPressed() recently
            if(pressedCombinations.Count > 0)
            {
                for(int i = pressedCombinations.Count - 1; i >= 0; --i)
                {
                    if(!pressedCombinations[i].ShouldKeep())
                    {
                        pressedCombinations.RemoveAtFast(i);
                    }
                }
            }
        }
        #endregion Required methods

        #region Public methods
        public void AddCustomInput(InputCustomBase custom)
        {
            if(inputs.ContainsKey(custom.Id))
                throw new Exception($"{custom.Id} already exists!");

            inputs.Add(custom.Id, custom);
        }
        #endregion Public methods

        #region Public static methods
        public static bool IsInputReadable(bool checkSpectator = false)
        {
            if(MyAPIGateway.Gui.IsCursorVisible || MyAPIGateway.Gui.ChatEntryVisible)
                return false;

            if(checkSpectator && MyAPIGateway.Session.IsCameraUserControlledSpectator)
                return false;

            return true;
        }

        public static ControlContext GetCurrentInputContext()
        {
            if(MyAPIGateway.Gui.ChatEntryVisible || MyAPIGateway.Gui.IsCursorVisible)
            {
                return ControlContext.GUI;
            }

            MyCubeBlockDefinition def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

            if(def != null && MyCubeBuilder.Static.IsActivated)
            {
                return ControlContext.BUILD;
            }

            IMyControllableEntity controlled = MyAPIGateway.Session?.ControlledObject;
            IMyCharacter character = controlled as IMyCharacter;

            if(character != null)
            {
                // TODO: voxel hands don't show up in character.EquippedTool, no idea how to get them
                return ControlContext.CHARACTER;
            }

            if(controlled is IMyShipController)
            {
                // TODO: what context when controlling turrets?
                return ControlContext.VEHICLE;
            }

            // TODO: what context when spectating?

            return ControlContext.BASE;
        }

        public static InputKey GetInput(MyKeys key)
        {
            return (InputKey)instance.keyToInput.GetValueOrDefault(key, null);
        }

        public static InputMouseButton GetInput(MyMouseButtonsEnum button)
        {
            return (InputMouseButton)instance.mouseButtonToInput.GetValueOrDefault(button, null);
        }

        public static InputGamepadAxis GetInput(MyJoystickAxesEnum axis)
        {
            return (InputGamepadAxis)instance.gamepadAxisToInput.GetValueOrDefault(axis, null);
        }

        public static InputGamepadButton GetInput(MyJoystickButtonsEnum button)
        {
            return (InputGamepadButton)instance.gamepadButtonToInput.GetValueOrDefault(button, null);
        }

        public static InputGameControl GetInput(ControlContext contextId, MyStringId controlId)
        {
            return (InputGameControl)instance.gameControlToInput.GetValueOrDefault(controlId);
        }

        public static string GetInputDisplayName(MyKeys key)
        {
            InputBase input = instance.keyToInput.GetValueOrDefault(key, null);
            return input?.GetDisplayName() ?? null;
        }

        public static string GetInputDisplayName(MyMouseButtonsEnum button)
        {
            InputBase input = instance.mouseButtonToInput.GetValueOrDefault(button, null);
            return input?.GetDisplayName() ?? null;
        }

        public static string GetInputDisplayName(MyJoystickAxesEnum axis, bool specialChars = true)
        {
            InputBase input = instance.gamepadAxisToInput.GetValueOrDefault(axis, null);
            return input?.GetDisplayName(specialChars) ?? null;
        }

        public static string GetInputDisplayName(MyJoystickButtonsEnum button, bool specialChars = true)
        {
            InputBase input = instance.gamepadButtonToInput.GetValueOrDefault(button, null);
            return input?.GetDisplayName(specialChars) ?? null;
        }

        public static string GetInputDisplayName(ControlContext contextId, MyStringId controlId, bool specialChars = true)
        {
            GamepadBindings.IControl bind = instance.gamepadBindings.GetControl(contextId, controlId);

            if(bind == null)
                return null;

            return bind.GetBind(specialChars);
        }

        public static bool GetGameControlPressed(ControlContext contextId, MyStringId controlId)
        {
            IMyControl control = MyAPIGateway.Input.GetGameControl(controlId);

#if VERSION_200 || VERSION_201 || VERSION_202 || VERSION_203 || VERSION_204 || VERSION_205 // some backwards compatibility
            if(control.IsPressed())
                return true;
#else
            bool origEnabled = control.IsEnabled;
            try
            {
                control.IsEnabled = true;
                if(control.IsPressed())
                    return true;
            }
            finally
            {
                control.IsEnabled = origEnabled;
            }
#endif

            GamepadBindings.IControl bind = instance.gamepadBindings.GetControl(contextId, controlId);

            if(bind == null)
                return false;

            return bind.IsPressed();
        }

        public static bool GetGameControlJustPressed(ControlContext contextId, MyStringId controlId)
        {
            IMyControl control = MyAPIGateway.Input.GetGameControl(controlId);

#if VERSION_200 || VERSION_201 || VERSION_202 || VERSION_203 || VERSION_204 || VERSION_205 // some backwards compatibility
            if(control.IsNewPressed())
                return true;
#else
            bool origEnabled = control.IsEnabled;
            try
            {
                control.IsEnabled = true;
                if(control.IsNewPressed())
                    return true;
            }
            finally
            {
                control.IsEnabled = origEnabled;
            }
#endif

            GamepadBindings.IControl bind = instance.gamepadBindings.GetControl(contextId, controlId);

            if(bind == null)
                return false;

            return bind.IsJustPressed();
        }

        public static Vector3 GetRotationInput()
        {
            Vector2 rot = MyAPIGateway.Input.GetRotation();
            return new Vector3(rot.X, rot.Y, MyAPIGateway.Input.GetRoll());
        }

        public static Vector3 GetMovementInput()
        {
            return MyAPIGateway.Input.GetPositionDelta();
        }

        public static void AppendInputBindingInstructions(StringBuilder str, string commentPrefix)
        {
            str.AppendLine().Append(commentPrefix).Append(" === Input binding ===");
            str.AppendLine().Append(commentPrefix).Append(" Separate multiple keys/buttons/controls with spaces to form a combination, example: rightctrl w r");
            str.AppendLine().Append(commentPrefix).Append(" To unassign simply leave it empty.");
            str.AppendLine().Append(commentPrefix).Append(" The available inputs are listed below:");
            str.AppendLine().Append(commentPrefix);

            List<InputBase> cacheList = new List<InputBase>();
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.CUSTOM, "Custom inputs (mod-added)", true);
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.KEY, "Keys");
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.MOUSE, "Mouse buttons/axes");
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.GAMEPAD, "Gamepad/joystick");
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.CONTROL, "Game controls", true);
        }

        private static void AppendInputTypes(StringBuilder str, string commentPrefix, List<InputBase> list, InputTypeEnum type, string title, bool sortAlphabetically = false)
        {
            list.Clear();
            foreach(InputBase input in instance.inputs.Values)
            {
                if(input.Type != type)
                    continue;

                list.Add(input);
            }

            if(list.Count == 0)
                return; // nothing to print for this type

            if(sortAlphabetically)
                list.Sort((a, b) => a.Id.CompareTo(b.Id));

            const int COLUMNS = 4; // NOTE: an extra column is added with the remains
            const int COLUMN_WIDTH = 20;
            const string LINE_START = "     ";
            int entries = 0;

            str.AppendLine().Append(commentPrefix).Append(" ").Append(title).Append(": ");
            str.AppendLine().Append(commentPrefix).Append(LINE_START);

            int skipCount = list.Count / COLUMNS;
            int index = 0;
            int prevStart = 0;

            for(int i = 0; i < list.Count; ++i)
            {
                if(index >= list.Count)
                {
                    index = prevStart + 1;
                    prevStart = index;
                    str.AppendLine().Append(commentPrefix).Append(LINE_START);
                }

                InputBase input = list[index];

                str.Append(input.Id).Append(' ', Math.Max(COLUMN_WIDTH - input.Id.Length, 0));
                index += skipCount;
            }

            if(entries == 0)
                str.Length -= (Environment.NewLine.Length + commentPrefix.Length + LINE_START.Length); // remove last line start

            str.AppendLine().Append(commentPrefix);
        }
        #endregion Public static methods

        #region Private AddInput()
        private void AddInput(MyKeys key, string id = null, string displayName = null)
        {
            if(id == null)
                id = Enum.GetName(typeof(MyKeys), key).ToLower();

            if(displayName == null)
                displayName = GetFirstUpper(id);

            InputKey input = new InputKey(key, id, displayName);
            keyToInput.Add(key, input);
            inputs.Add(id, input);
        }

        private void AddInput(MyMouseButtonsEnum button, string id, string displayName = null)
        {
            if(displayName == null)
                displayName = GetFirstUpperIgnorePrefix(id);

            InputMouseButton input = new InputMouseButton(button, id, displayName);
            mouseButtonToInput.Add(button, input);
            inputs.Add(id, input);
        }

        private void AddInput(MyJoystickButtonsEnum button, string id, string displayName = null, char printChar = ' ')
        {
            if(displayName == null)
                displayName = GetFirstUpperIgnorePrefix(id);

            InputGamepadButton input = new InputGamepadButton(button, id, displayName, printChar);
            gamepadButtonToInput.Add(button, input);
            inputs.Add(id, input);
        }

        private void AddInput(MyJoystickAxesEnum axis, string id, string displayName = null, char printChar = ' ')
        {
            if(displayName == null)
                displayName = GetFirstUpperIgnorePrefix(id);

            InputGamepadAxis input = new InputGamepadAxis(axis, id, displayName, printChar);
            gamepadAxisToInput.Add(axis, input);
            inputs.Add(id, input);
        }

        private void AddInput(MyStringId controlId, string id = null, string displayName = null)
        {
            if(id == null)
                id = CONTROL_PREFIX + controlId.String.ToLower();

            if(displayName == null)
                displayName = GetFirstUpperIgnorePrefix(id);

            InputGameControl input = new InputGameControl(controlId, id, displayName);
            gameControlToInput.Add(controlId, input);
            inputs.Add(id, input);
        }

        private void AddInput(InputAdvancedBase custom)
        {
            inputs.Add(custom.Id, custom);
        }
        #endregion Private AddInput()

        #region Private utils
        private string GetFirstUpper(string id)
        {
            return char.ToUpper(id[0]) + id.Substring(1);
        }

        private string GetFirstUpperIgnorePrefix(string id)
        {
            return char.ToUpper(id[2]) + id.Substring(3);
        }
        #endregion Private utils

        #region Dev tools
        /// <summary>
        /// <para>NOTE: dev tool, not optimized!</para>
        /// <para>Dumps all keys and mouse buttons along with the controls that are assigned to them.</para>
        /// <para>Does not include gamepad binds as those aren't dynamically configurable, see MySpaceBindingCreator instead.</para>
        /// </summary>
        public static void DumpCurrentBinds(StringBuilder output)
        {
            output.AppendLine("Dumping binds...");




            // pasted from MyControlsSpace
            List<MyStringId> controlNames = new List<MyStringId>
            {
                MyControlsSpace.FORWARD, // named 'Forward' defaults to: W
                MyControlsSpace.BACKWARD, // named 'Backward' defaults to: S
                MyControlsSpace.STRAFE_LEFT, // named 'Strafe left' defaults to: A
                MyControlsSpace.STRAFE_RIGHT, // named 'Strafe right' defaults to: D
                MyControlsSpace.ROTATION_LEFT, // named 'Rotate left' defaults to: Left
                MyControlsSpace.ROTATION_RIGHT, // named 'Rotate right' defaults to: Right
                MyControlsSpace.ROTATION_UP, // named 'Rotate up' defaults to: Up
                MyControlsSpace.ROTATION_DOWN, // named 'Rotate down' defaults to: Down
                MyControlsSpace.ROLL_LEFT, // named 'Roll left (ship / jetpack)' defaults to: Q
                MyControlsSpace.ROLL_RIGHT, // named 'Roll right (ship / jetpack)' defaults to: E
                MyControlsSpace.SPRINT, // named 'Hold to sprint' defaults to: Shift
                MyControlsSpace.SWITCH_WALK, // named 'Toggle walk' defaults to: Caps Lock
                MyControlsSpace.JUMP, // named 'Up / Jump' defaults to: Space
                MyControlsSpace.CROUCH, // named 'Down / Crouch' defaults to: C
                MyControlsSpace.PRIMARY_TOOL_ACTION, // named 'Use tool / Fire weapon' defaults to: LMB
                MyControlsSpace.SECONDARY_TOOL_ACTION, // named 'Secondary mode' defaults to: RMB
                MyControlsSpace.RELOAD, // named 'Reload' defaults to: R
                MyControlsSpace.USE, // named 'Use / Interact' defaults to: F
                MyControlsSpace.HELMET, // named 'Helmet' defaults to: J
                MyControlsSpace.THRUSTS, // named 'Jetpack on / off' defaults to: X
                MyControlsSpace.DAMPING, // named 'Inertia dampeners on / off' defaults to: Z
                MyControlsSpace.DAMPING_RELATIVE, // named 'Toggle Relative dampeners' defaults to: Ctrl+Z
                MyControlsSpace.BROADCASTING, // named 'Broadcasting' defaults to: O
                MyControlsSpace.HEADLIGHTS, // named 'Lights on / off' defaults to: L
                MyControlsSpace.TERMINAL, // named 'Terminal / Inventory' defaults to: K
                MyControlsSpace.REMOTE_ACCESS_MENU, // named 'Remote Access' defaults to: Shift+K
                MyControlsSpace.INVENTORY, // named 'Inventory' defaults to: I
                MyControlsSpace.SUICIDE, // named 'Respawn' defaults to: Backsp.
                MyControlsSpace.BUILD_SCREEN, // named 'Toolbar config' defaults to: G
                MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE, // named 'Rotate block vertical +' defaults to: PgDown, Ctrl+D
                MyControlsSpace.CUBE_ROTATE_VERTICAL_NEGATIVE, // named 'Rotate block vertical -' defaults to: Delete, Ctrl+A
                MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE, // named 'Rotate block horizontal +' defaults to: Home, Ctrl+W
                MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE, // named 'Rotate block horizontal -' defaults to: End, Ctrl+S
                MyControlsSpace.CUBE_ROTATE_ROLL_POSITIVE, // named 'Rotate block roll +' defaults to: Insert, Ctrl+E
                MyControlsSpace.CUBE_ROTATE_ROLL_NEGATIVE, // named 'Rotate block roll -' defaults to: PgUp, Ctrl+Q
                MyControlsSpace.CUBE_COLOR_CHANGE, // named 'Repaint block' defaults to: MMB
                MyControlsSpace.CUBE_DEFAULT_MOUNTPOINT, // named 'Reset orientation' defaults to: T
                MyControlsSpace.USE_SYMMETRY, // named 'Toggle symmetry' defaults to: N
                MyControlsSpace.SYMMETRY_SWITCH, // named 'Symmetry setup' defaults to: M
                MyControlsSpace.FREE_ROTATION, // named 'Cycle placement modes' defaults to: B
                MyControlsSpace.BUILD_PLANNER, // named 'Build Planner' defaults to: MMB
                MyControlsSpace.CUBE_BUILDER_CUBESIZE_MODE, // named 'Cube size toggle button' defaults to: R
                MyControlsSpace.CREATE_BLUEPRINT, // named 'Create/manage blueprints' defaults to: Ctrl+B
                MyControlsSpace.CREATE_BLUEPRINT_DETACHED, // named 'Create blueprint detached' defaults to: Ctrl+Shift+B
                MyControlsSpace.CREATE_BLUEPRINT_MAGNETIC_LOCKS, // named 'Blueprint with locks' defaults to: Ctrl+Alt+B
                MyControlsSpace.COPY_OBJECT, // named 'Copy object' defaults to: Ctrl+C
                MyControlsSpace.COPY_OBJECT_DETACHED, // named 'Copy object detached' defaults to: Ctrl+Shift+C
                MyControlsSpace.COPY_OBJECT_MAGNETIC_LOCKS, // named 'Copy with locks' defaults to: Ctrl+Alt+C
                MyControlsSpace.PASTE_OBJECT, // named 'Paste object' defaults to: Ctrl+V
                MyControlsSpace.CUT_OBJECT, // named 'Cut object' defaults to: Ctrl+X
                MyControlsSpace.CUT_OBJECT_DETACHED, // named 'Cut object detached' defaults to: Ctrl+Shift+X
                MyControlsSpace.CUT_OBJECT_MAGNETIC_LOCKS, // named 'Cut with locks' defaults to: Ctrl+Alt+X
                MyControlsSpace.DELETE_OBJECT, // named 'Delete object' defaults to: Ctrl+Delete
                MyControlsSpace.DELETE_OBJECT_DETACHED, // named 'Delete object detached' defaults to: Ctrl+Shift+Delete
                MyControlsSpace.DELETE_OBJECT_MAGNETIC_LOCKS, // named 'Delete with locks' defaults to: Ctrl+Shift+Alt+Delete
                MyControlsSpace.TOOLBAR_NEXT_ITEM, // named 'Next toolbar item' defaults to: NA
                MyControlsSpace.TOOLBAR_PREV_ITEM, // named 'Previous toolbar item' defaults to: NA
                MyControlsSpace.SLOT1, // named 'Equip item from slot 1' defaults to: 1
                MyControlsSpace.SLOT2, // named 'Equip item from slot 2' defaults to: 2
                MyControlsSpace.SLOT3, // named 'Equip item from slot 3' defaults to: 3
                MyControlsSpace.SLOT4, // named 'Equip item from slot 4' defaults to: 4
                MyControlsSpace.SLOT5, // named 'Equip item from slot 5' defaults to: 5
                MyControlsSpace.SLOT6, // named 'Equip item from slot 6' defaults to: 6
                MyControlsSpace.SLOT7, // named 'Equip item from slot 7' defaults to: 7
                MyControlsSpace.SLOT8, // named 'Equip item from slot 8' defaults to: 8
                MyControlsSpace.SLOT9, // named 'Equip item from slot 9' defaults to: 9
                MyControlsSpace.SLOT0, // named 'Unequip' defaults to: 0, ~
                MyControlsSpace.TOOLBAR_UP, // named 'Next toolbar' defaults to: Period
                MyControlsSpace.TOOLBAR_DOWN, // named 'Previous toolbar' defaults to: Comma
                MyControlsSpace.PAGE1, // named 'Switch to page 1' defaults to: Ctrl+1
                MyControlsSpace.PAGE2, // named 'Switch to page 2' defaults to: Ctrl+2
                MyControlsSpace.PAGE3, // named 'Switch to page 3' defaults to: Ctrl+3
                MyControlsSpace.PAGE4, // named 'Switch to page 4' defaults to: Ctrl+4
                MyControlsSpace.PAGE5, // named 'Switch to page 5' defaults to: Ctrl+5
                MyControlsSpace.PAGE6, // named 'Switch to page 6' defaults to: Ctrl+6
                MyControlsSpace.PAGE7, // named 'Switch to page 7' defaults to: Ctrl+7
                MyControlsSpace.PAGE8, // named 'Switch to page 8' defaults to: Ctrl+8
                MyControlsSpace.PAGE9, // named 'Switch to page 9' defaults to: Ctrl+9
                MyControlsSpace.PAGE0, // named 'Switch to page 0' defaults to: Ctrl+0
                MyControlsSpace.SWITCH_LEFT, // named 'Prev. color or camera' defaults to: [
                MyControlsSpace.SWITCH_RIGHT, // named 'Next color or camera' defaults to: ]
                MyControlsSpace.LANDING_GEAR, // named 'Park' defaults to: P
                MyControlsSpace.TOGGLE_REACTORS, // named 'Local power switch on / off' defaults to: Y
                MyControlsSpace.TOGGLE_REACTORS_ALL, // named 'Power switch on / off' defaults to: Ctrl+Y
                MyControlsSpace.COLOR_PICKER, // named 'Color picker' defaults to: P
                MyControlsSpace.QUICK_PICK_COLOR, // named 'Quick Pick Color' defaults to: Shift+P
                MyControlsSpace.VOXEL_HAND_SETTINGS, // named 'Open voxel hand settings' defaults to: K
                MyControlsSpace.VOICE_CHAT, // named 'Voice chat' defaults to: U
                MyControlsSpace.EXPORT_MODEL, // named 'Export model' defaults to: Ctrl+Alt+E
                MyControlsSpace.QUICK_LOAD_RECONNECT, // named 'Quick Load' defaults to: F5
                MyControlsSpace.QUICK_SAVE, // named 'Quick Save' defaults to: Shift+F5
                MyControlsSpace.PAUSE_GAME, // named 'Pause game' defaults to: Pause
                MyControlsSpace.HELP_SCREEN, // named 'Help' defaults to: F1
                MyControlsSpace.WARNING_SCREEN, // named 'Open Warning screen' defaults to: Shift+F1
                MyControlsSpace.PLAYERS_SCREEN, // named 'Open Players Screen' defaults to: F3
                MyControlsSpace.BLUEPRINTS_MENU, // named 'Blueprints Screen' defaults to: F10
                MyControlsSpace.ADMIN_MENU, // named 'Open Admin Menu' defaults to: Alt+F10
                MyControlsSpace.SPAWN_MENU, // named 'Open Spawn Menu' defaults to: Shift+F10
                MyControlsSpace.CONTROL_MENU, // named 'Open control menu' defaults to: Minus
                MyControlsSpace.ACTIVE_CONTRACT_SCREEN, // named 'Open Contract screen' defaults to: Colon
                MyControlsSpace.CHAT_SCREEN, // named 'Chat screen' defaults to: Enter
                MyControlsSpace.CONSOLE, // named 'Console' defaults to: ~
                MyControlsSpace.SPECTATOR_NONE, // named 'Player control' defaults to: F6
                MyControlsSpace.SPECTATOR_DELTA, // named 'Delta spectator' defaults to: F7
                MyControlsSpace.SPECTATOR_FREE, // named 'Free spectator' defaults to: F8
                MyControlsSpace.SPECTATOR_STATIC, // named 'Static spectator' defaults to: F9
                MyControlsSpace.CAMERA_MODE, // named 'First-person / Third-person' defaults to: V
                MyControlsSpace.LOOKAROUND, // named 'Hold to look around' defaults to: Alt
                MyControlsSpace.SCREENSHOT, // named 'Screenshot' defaults to: F4
                MyControlsSpace.TOGGLE_HUD, // named 'HUD on / off' defaults to: Tab
                MyControlsSpace.TOGGLE_SIGNALS, // named 'Toggle signal mode' defaults to: H
                MyControlsSpace.SPECTATOR_LOCK, // named 'Lock/unlock to entity' defaults to: Num *
                MyControlsSpace.SPECTATOR_SWITCHMODE, // named 'Next camera mode' defaults to: Num /
                MyControlsSpace.SPECTATOR_NEXTPLAYER, // named 'Next player' defaults to: Num +
                MyControlsSpace.SPECTATOR_PREVPLAYER, // named 'Previous player' defaults to: Num −
            };

            Dictionary<string, List<MyStringId>> binds = new Dictionary<string, List<MyStringId>>();
            Dictionary<string, List<MyStringId>> invalidBinds = new Dictionary<string, List<MyStringId>>();
            Dictionary<string, List<MyStringId>> addTo;

            foreach(MyKeys key in Enum.GetValues(typeof(MyKeys)))
            {
                if(MyAPIGateway.Input.IsKeyValid(key))
                    addTo = binds;
                else
                    addTo = invalidBinds;

                if(addTo.ContainsKey(key.ToString()))
                {
                    output.AppendLine($"Key {key} was already added...");
                    continue;
                }

                addTo.Add(key.ToString(), new List<MyStringId>());
            }

            foreach(MyMouseButtonsEnum button in Enum.GetValues(typeof(MyMouseButtonsEnum)))
            {
                if(MyAPIGateway.Input.IsMouseButtonValid(button))
                    addTo = binds;
                else
                    addTo = invalidBinds;

                if(addTo.ContainsKey("(mouse) " + button.ToString()))
                {
                    output.AppendLine($"Mouse button {button} was already added...");
                    continue;
                }
                addTo.Add("(mouse) " + button.ToString(), new List<MyStringId>());
            }

            foreach(MyStringId controlName in controlNames)
            {
                VRage.ModAPI.IMyControl control = MyAPIGateway.Input.GetGameControl(controlName);

                if(control == null)
                {
                    output.AppendLine($"Control {controlName} doesn't exist.");
                    continue;
                }

                if(control.GetKeyboardControl() != MyKeys.None)
                {
                    if(MyAPIGateway.Input.IsKeyValid(control.GetKeyboardControl()))
                        addTo = binds;
                    else
                        addTo = invalidBinds;

                    addTo[control.GetKeyboardControl().ToString()].Add(controlName);
                }

                if(control.GetSecondKeyboardControl() != MyKeys.None)
                {
                    if(MyAPIGateway.Input.IsKeyValid(control.GetSecondKeyboardControl()))
                        addTo = binds;
                    else
                        addTo = invalidBinds;

                    addTo[control.GetSecondKeyboardControl().ToString()].Add(controlName);
                }

                if(control.GetMouseControl() != MyMouseButtonsEnum.None)
                {
                    if(MyAPIGateway.Input.IsMouseButtonValid(control.GetMouseControl()))
                        addTo = binds;
                    else
                        addTo = invalidBinds;

                    addTo["(mouse) " + control.GetMouseControl().ToString()].Add(controlName);
                }
            }

            List<KeyValuePair<string, List<MyStringId>>> bindsList = binds.ToList();
            List<KeyValuePair<string, List<MyStringId>>> invalidBindsList = invalidBinds.ToList();

            bindsList.Sort((x, y) =>
            {
                int comp = x.Value.Count.CompareTo(y.Value.Count);
                return (comp == 0 ? x.Key.CompareTo(y.Key) : comp);
            });
            invalidBindsList.Sort((x, y) =>
            {
                int comp = x.Value.Count.CompareTo(y.Value.Count);
                return (comp == 0 ? x.Key.CompareTo(y.Key) : comp);
            });

            bindsList.Reverse();
            invalidBindsList.Reverse();

            output.AppendLine();
            output.AppendLine();
            output.AppendLine("Binds:");

            foreach(KeyValuePair<string, List<MyStringId>> kv in bindsList)
            {
                output.AppendLine($"    {kv.Key} => {string.Join(", ", kv.Value)}");
            }

            output.AppendLine();
            output.AppendLine();
            output.AppendLine("Invalid binds:");

            foreach(KeyValuePair<string, List<MyStringId>> kv in invalidBindsList)
            {
                output.AppendLine($"    {kv.Key} => {string.Join(", ", kv.Value)}");
            }
        }
        #endregion Dev tools
    }
}