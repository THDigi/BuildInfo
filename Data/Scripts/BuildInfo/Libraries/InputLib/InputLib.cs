using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Digi.Input.Devices;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace Digi.Input
{
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
                var combination = Create(displayName, combinationString, out error, invalidInputs);

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

                var str = new StringBuilder();
                var combInputs = new List<InputBase>();

                foreach(var inputId in inputStrings)
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

                var combination = new Combination(displayName, combInputs, str.ToString());

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

                foreach(var input in inputs)
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

                foreach(var input in inputs)
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

                var key = new InputReleaseKey(this, contextId, InputLib.instance.tick);

                foreach(var irk in InputLib.instance.pressedCombinations)
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
            /// <param name="specialChars">Wether to add special characters like the xbox character-images to the string or to use regular characters.</param>
            public string GetBinds(ControlContext contextId = ControlContext.CHARACTER, bool specialChars = true)
            {
                var str = new StringBuilder();
                GetBinds(str, contextId, specialChars);
                return str.ToString();
            }

            /// <summary>
            /// Gets the combination into a parseable format.
            /// </summary>
            /// <param name="output">What to append the info to</param>
            /// <param name="contextId">Control context, <see cref="InputLib.GetCurrentInputContext"/></param>
            /// <param name="specialChars">Wether to add special characters like the xbox character-images to the string or to use regular characters.</param>
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

                foreach(var input in inputs)
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

                if(c1.inputs.Count != c2.inputs.Count)
                    return false;

                // TODO: improvements?
                foreach(var c1input in c1.inputs)
                {
                    bool found = false;

                    foreach(var c2input in c2.inputs)
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
            AddInput(MyControlsSpace.MISSION_SETTINGS, CONTROL_PREFIX + "missionsettings", "Mission Settings");
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

            var def = MyCubeBuilder.Static?.CubeBuilderState?.CurrentBlockDefinition;

            if(def != null && MyCubeBuilder.Static.IsActivated)
            {
                return ControlContext.BUILD;
            }

            var controlled = MyAPIGateway.Session?.ControlledObject;
            var character = controlled as IMyCharacter;

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
            var input = instance.keyToInput.GetValueOrDefault(key, null);
            return input?.GetDisplayName() ?? null;
        }

        public static string GetInputDisplayName(MyMouseButtonsEnum button)
        {
            var input = instance.mouseButtonToInput.GetValueOrDefault(button, null);
            return input?.GetDisplayName() ?? null;
        }

        public static string GetInputDisplayName(MyJoystickAxesEnum axis, bool specialChars = true)
        {
            var input = instance.gamepadAxisToInput.GetValueOrDefault(axis, null);
            return input?.GetDisplayName(specialChars) ?? null;
        }

        public static string GetInputDisplayName(MyJoystickButtonsEnum button, bool specialChars = true)
        {
            var input = instance.gamepadButtonToInput.GetValueOrDefault(button, null);
            return input?.GetDisplayName(specialChars) ?? null;
        }

        public static string GetInputDisplayName(ControlContext contextId, MyStringId controlId, bool specialChars = true)
        {
            var bind = instance.gamepadBindings.GetControl(contextId, controlId);

            if(bind == null)
                return null;

            return bind.GetBind(specialChars);
        }

        public static bool GetGameControlPressed(ControlContext contextId, MyStringId controlId)
        {
            var control = MyAPIGateway.Input.GetGameControl(controlId);

            if(control.IsPressed())
                return true;

            var bind = instance.gamepadBindings.GetControl(contextId, controlId);

            if(bind == null)
                return false;

            return bind.IsPressed();
        }

        public static bool GetGameControlJustPressed(ControlContext contextId, MyStringId controlId)
        {
            var control = MyAPIGateway.Input.GetGameControl(controlId);

            if(control.IsNewPressed())
                return true;

            var bind = instance.gamepadBindings.GetControl(contextId, controlId);

            if(bind == null)
                return false;

            return bind.IsJustPressed();
        }

        public static Vector3 GetRotationInput()
        {
            var rot = MyAPIGateway.Input.GetRotation();
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

            var cacheList = new List<InputBase>();
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.CUSTOM, "Custom inputs (mod-added)", true);
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.KEY, "Keys");
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.MOUSE, "Mouse buttons/axes");
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.GAMEPAD, "Gamepad/joystick");
            AppendInputTypes(str, commentPrefix, cacheList, InputTypeEnum.CONTROL, "Game controls", true);
        }

        private static void AppendInputTypes(StringBuilder str, string commentPrefix, List<InputBase> list, InputTypeEnum type, string title, bool sortAlphabetically = false)
        {
            list.Clear();
            foreach(var input in instance.inputs.Values)
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

                var input = list[index];

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

            var input = new InputKey(key, id, displayName);
            keyToInput.Add(key, input);
            inputs.Add(id, input);
        }

        private void AddInput(MyMouseButtonsEnum button, string id, string displayName = null)
        {
            if(displayName == null)
                displayName = GetFirstUpperIgnorePrefix(id);

            var input = new InputMouseButton(button, id, displayName);
            mouseButtonToInput.Add(button, input);
            inputs.Add(id, input);
        }

        private void AddInput(MyJoystickButtonsEnum button, string id, string displayName = null, char printChar = ' ')
        {
            if(displayName == null)
                displayName = GetFirstUpperIgnorePrefix(id);

            var input = new InputGamepadButton(button, id, displayName, printChar);
            gamepadButtonToInput.Add(button, input);
            inputs.Add(id, input);
        }

        private void AddInput(MyJoystickAxesEnum axis, string id, string displayName = null, char printChar = ' ')
        {
            if(displayName == null)
                displayName = GetFirstUpperIgnorePrefix(id);

            var input = new InputGamepadAxis(axis, id, displayName, printChar);
            gamepadAxisToInput.Add(axis, input);
            inputs.Add(id, input);
        }

        private void AddInput(MyStringId controlId, string id = null, string displayName = null)
        {
            if(id == null)
                id = CONTROL_PREFIX + controlId.String.ToLower();

            if(displayName == null)
                displayName = GetFirstUpperIgnorePrefix(id);

            var input = new InputGameControl(controlId, id, displayName);
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
            var controlNames = new List<MyStringId>
            {
                MyControlsSpace.FORWARD,
                MyControlsSpace.BACKWARD,
                MyControlsSpace.STRAFE_LEFT,
                MyControlsSpace.STRAFE_RIGHT,
                MyControlsSpace.ROLL_LEFT,
                MyControlsSpace.ROLL_RIGHT,
                MyControlsSpace.SPRINT,
                MyControlsSpace.PRIMARY_TOOL_ACTION,
                MyControlsSpace.SECONDARY_TOOL_ACTION,
                MyControlsSpace.JUMP,
                MyControlsSpace.CROUCH,
                MyControlsSpace.SWITCH_WALK,
                MyControlsSpace.USE,
                MyControlsSpace.PICK_UP,
                MyControlsSpace.TERMINAL,
                MyControlsSpace.HELP_SCREEN,
                MyControlsSpace.CONTROL_MENU,
                MyControlsSpace.FACTIONS_MENU,
                MyControlsSpace.ROTATION_LEFT,
                MyControlsSpace.ROTATION_RIGHT,
                MyControlsSpace.ROTATION_UP,
                MyControlsSpace.ROTATION_DOWN,
                MyControlsSpace.HEADLIGHTS,
                MyControlsSpace.SCREENSHOT,
                MyControlsSpace.LOOKAROUND,
                MyControlsSpace.TOGGLE_SIGNALS,
                MyControlsSpace.SWITCH_LEFT,
                MyControlsSpace.SWITCH_RIGHT,
                MyControlsSpace.CUBE_COLOR_CHANGE,
                MyControlsSpace.TOGGLE_REACTORS,
                MyControlsSpace.WHEEL_JUMP,
                MyControlsSpace.BUILD_SCREEN,
                MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE,
                MyControlsSpace.CUBE_ROTATE_VERTICAL_NEGATIVE,
                MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE,
                MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE,
                MyControlsSpace.CUBE_ROTATE_ROLL_POSITIVE,
                MyControlsSpace.CUBE_ROTATE_ROLL_NEGATIVE,
                MyControlsSpace.SYMMETRY_SWITCH,
                MyControlsSpace.USE_SYMMETRY,
                MyControlsSpace.SWITCH_COMPOUND,
                MyControlsSpace.SWITCH_BUILDING_MODE,
                MyControlsSpace.VOXEL_HAND_SETTINGS,
                MyControlsSpace.MISSION_SETTINGS,
                MyControlsSpace.CUBE_BUILDER_CUBESIZE_MODE,
                MyControlsSpace.CUBE_DEFAULT_MOUNTPOINT,
                MyControlsSpace.SLOT1,
                MyControlsSpace.SLOT2,
                MyControlsSpace.SLOT3,
                MyControlsSpace.SLOT4,
                MyControlsSpace.SLOT5,
                MyControlsSpace.SLOT6,
                MyControlsSpace.SLOT7,
                MyControlsSpace.SLOT8,
                MyControlsSpace.SLOT9,
                MyControlsSpace.SLOT0,
                MyControlsSpace.TOOLBAR_UP,
                MyControlsSpace.TOOLBAR_DOWN,
                MyControlsSpace.TOOLBAR_NEXT_ITEM,
                MyControlsSpace.TOOLBAR_PREV_ITEM,
                MyControlsSpace.TOGGLE_HUD,
                MyControlsSpace.DAMPING,
                MyControlsSpace.THRUSTS,
                MyControlsSpace.CAMERA_MODE,
                MyControlsSpace.BROADCASTING,
                MyControlsSpace.HELMET,
                MyControlsSpace.CHAT_SCREEN,
                MyControlsSpace.CONSOLE,
                MyControlsSpace.SUICIDE,
                MyControlsSpace.LANDING_GEAR,
                MyControlsSpace.INVENTORY,
                MyControlsSpace.PAUSE_GAME,
                MyControlsSpace.SPECTATOR_NONE,
                MyControlsSpace.SPECTATOR_DELTA,
                MyControlsSpace.SPECTATOR_FREE,
                MyControlsSpace.SPECTATOR_STATIC,
                MyControlsSpace.FREE_ROTATION,
                MyControlsSpace.VOICE_CHAT,
                MyControlsSpace.VOXEL_PAINT,
                MyControlsSpace.BUILD_MODE,
                MyControlsSpace.NEXT_BLOCK_STAGE,
                MyControlsSpace.PREV_BLOCK_STAGE,
                MyControlsSpace.MOVE_CLOSER,
                MyControlsSpace.MOVE_FURTHER,
                MyControlsSpace.COPY_PASTE_ACTION
            };

            var binds = new Dictionary<string, List<MyStringId>>();
            var invalidBinds = new Dictionary<string, List<MyStringId>>();
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

            foreach(var controlName in controlNames)
            {
                var control = MyAPIGateway.Input.GetGameControl(controlName);

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

            var bindsList = binds.ToList();
            var invalidBindsList = invalidBinds.ToList();

            bindsList.Sort((x, y) =>
            {
                var comp = x.Value.Count.CompareTo(y.Value.Count);
                return (comp == 0 ? x.Key.CompareTo(y.Key) : comp);
            });
            invalidBindsList.Sort((x, y) =>
            {
                var comp = x.Value.Count.CompareTo(y.Value.Count);
                return (comp == 0 ? x.Key.CompareTo(y.Key) : comp);
            });

            bindsList.Reverse();
            invalidBindsList.Reverse();

            output.AppendLine();
            output.AppendLine();
            output.AppendLine("Binds:");

            foreach(var kv in bindsList)
            {
                output.AppendLine($"    {kv.Key} => {string.Join(", ", kv.Value)}");
            }

            output.AppendLine();
            output.AppendLine();
            output.AppendLine("Invalid binds:");

            foreach(var kv in invalidBindsList)
            {
                output.AppendLine($"    {kv.Key} => {string.Join(", ", kv.Value)}");
            }
        }
        #endregion Dev tools
    }
}