using System;
using System.Collections.Generic;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Input;
using VRage.Utils;

namespace Digi.Input.Devices
{
    public enum ControlContext
    {
        NONE = 0,
        BASE,
        GUI,
        CHARACTER,
        VEHICLE,
        BUILD,
        VOXEL_EDIT
    }

    /// <summary>
    /// Copied from MySpaceBindingCreator and converted for use in this class.
    /// </summary>
    public class GamepadBindings
    {
        // pasted from MyControlsGUI, because it's not whitelisted
        private readonly MyStringId GUI_MAIN_MENU = MyStringId.GetOrCompute("MAIN_MENU");
        private readonly MyStringId GUI_MOVE_UP = MyStringId.GetOrCompute("MOVE_NEXT");
        private readonly MyStringId GUI_MOVE_DOWN = MyStringId.GetOrCompute("MOVE_PREV");
        private readonly MyStringId GUI_MOVE_LEFT = MyStringId.GetOrCompute("MOVE_LEFT");
        private readonly MyStringId GUI_MOVE_RIGHT = MyStringId.GetOrCompute("MOVE_RIGHT");
        private readonly MyStringId GUI_ACCEPT = MyStringId.GetOrCompute("ACCEPT");
        private readonly MyStringId GUI_CANCEL = MyStringId.GetOrCompute("CANCEL");

        private readonly ContextData[] contexts;

        public GamepadBindings()
        {
            int contextCount = Enum.GetValues(typeof(ControlContext)).Length;
            contexts = new ContextData[contextCount];

            // HACK needs manual updating
            CreateForBase();
            CreateForGUI();
            CreateForCharacter();
            CreateForSpaceship();
            CreateForBuildMode();
            CreateForVoxelHands();
        }

        public IControl GetControl(ControlContext contextId, MyStringId control)
        {
            int contextIndex = (int)contextId;

            if(contextIndex < 0 || contextIndex >= contexts.Length)
                throw new Exception($"Invalid contextId/index: {contextIndex}");

            return contexts[contextIndex][control];
        }

        #region Binding methods
        private void CreateForBase()
        {
            AddContext(ControlContext.BASE);

            AddControl(ControlContext.BASE, MyControlsSpace.CONTROL_MENU, MyJoystickButtonsEnum.J07);
            AddControl(ControlContext.BASE, GUI_MAIN_MENU, MyJoystickButtonsEnum.J08);
        }

        private void CreateForGUI()
        {
            AddContext(ControlContext.GUI, ControlContext.BASE);

            AddControl(ControlContext.GUI, GUI_ACCEPT, MyJoystickButtonsEnum.J01);
            AddControl(ControlContext.GUI, GUI_CANCEL, MyJoystickButtonsEnum.J02);
            AddControl(ControlContext.GUI, GUI_MOVE_UP, MyJoystickButtonsEnum.JDUp);
            AddControl(ControlContext.GUI, GUI_MOVE_DOWN, MyJoystickButtonsEnum.JDDown);
            AddControl(ControlContext.GUI, GUI_MOVE_LEFT, MyJoystickButtonsEnum.JDLeft);
            AddControl(ControlContext.GUI, GUI_MOVE_RIGHT, MyJoystickButtonsEnum.JDRight);
        }

        private void CreateForCharacter()
        {
            AddContext(ControlContext.CHARACTER, ControlContext.BASE);

            AddControl(ControlContext.CHARACTER, MyControlsSpace.FORWARD, MyJoystickAxesEnum.Yneg);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.BACKWARD, MyJoystickAxesEnum.Ypos);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.STRAFE_LEFT, MyJoystickAxesEnum.Xneg);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.STRAFE_RIGHT, MyJoystickAxesEnum.Xpos);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.PRIMARY_TOOL_ACTION, MyJoystickAxesEnum.Zneg);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.SECONDARY_TOOL_ACTION, MyJoystickAxesEnum.Zpos);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.COPY_PASTE_ACTION, MyJoystickAxesEnum.Zneg);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.ROTATION_LEFT, MyJoystickAxesEnum.RotationXneg);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.ROTATION_RIGHT, MyJoystickAxesEnum.RotationXpos);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.ROTATION_UP, MyJoystickAxesEnum.RotationYneg);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.ROTATION_DOWN, MyJoystickAxesEnum.RotationYpos);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.JUMP, MyJoystickButtonsEnum.J01);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.CROUCH, MyJoystickButtonsEnum.J02);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.USE, MyJoystickButtonsEnum.J03);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.THRUSTS, MyJoystickButtonsEnum.J04);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.ROLL_LEFT, MyJoystickButtonsEnum.J05);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.ROLL_RIGHT, MyJoystickButtonsEnum.J06);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.SPRINT, MyJoystickButtonsEnum.J08);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.SPRINT, MyJoystickButtonsEnum.J09);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.CAMERA_MODE, MyJoystickButtonsEnum.J10);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.TOOLBAR_UP, MyJoystickButtonsEnum.JDUp);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.TOOLBAR_DOWN, MyJoystickButtonsEnum.JDDown);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.TOOLBAR_NEXT_ITEM, MyJoystickButtonsEnum.JDRight);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.TOOLBAR_PREV_ITEM, MyJoystickButtonsEnum.JDLeft);
            AddControl(ControlContext.CHARACTER, MyControlsSpace.BUILD_MODE, MyJoystickButtonsEnum.J09);
        }

        private void CreateForSpaceship()
        {
            AddContext(ControlContext.VEHICLE, ControlContext.CHARACTER);

            AddControl(ControlContext.VEHICLE, MyControlsSpace.LANDING_GEAR, MyJoystickButtonsEnum.J02);
            AddControl(ControlContext.VEHICLE, MyControlsSpace.TOGGLE_REACTORS, MyJoystickButtonsEnum.J04);
        }

        private void CreateForBuildMode()
        {
            AddContext(ControlContext.BUILD, ControlContext.CHARACTER);

            AddControl(ControlContext.BUILD, MyControlsSpace.CUBE_COLOR_CHANGE, MyJoystickButtonsEnum.J01);
            AddControl(ControlContext.BUILD, MyControlsSpace.USE_SYMMETRY, MyJoystickButtonsEnum.J03);
            AddControl(ControlContext.BUILD, MyControlsSpace.SYMMETRY_SWITCH, MyJoystickButtonsEnum.J04);
            AddControl(ControlContext.BUILD, MyControlsSpace.CUBE_ROTATE_ROLL_POSITIVE, MyJoystickButtonsEnum.J05);
            AddControl(ControlContext.BUILD, MyControlsSpace.CUBE_ROTATE_ROLL_NEGATIVE, MyJoystickButtonsEnum.J06);
            AddControl(ControlContext.BUILD, MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE, MyJoystickAxesEnum.Xneg);
            AddControl(ControlContext.BUILD, MyControlsSpace.CUBE_ROTATE_VERTICAL_NEGATIVE, MyJoystickAxesEnum.Xpos);
            AddControl(ControlContext.BUILD, MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE, MyJoystickAxesEnum.Yneg);
            AddControl(ControlContext.BUILD, MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE, MyJoystickAxesEnum.Ypos);
            AddControl(ControlContext.BUILD, MyControlsSpace.BUILD_MODE, MyJoystickButtonsEnum.J09);
        }

        private void CreateForVoxelHands()
        {
            AddContext(ControlContext.VOXEL_EDIT, ControlContext.CHARACTER);

            AddControl(ControlContext.VOXEL_EDIT, MyControlsSpace.VOXEL_PAINT, MyJoystickButtonsEnum.J01);
            AddControl(ControlContext.VOXEL_EDIT, MyControlsSpace.SWITCH_LEFT, MyJoystickButtonsEnum.J03);
            AddControl(ControlContext.VOXEL_EDIT, MyControlsSpace.VOXEL_HAND_SETTINGS, MyJoystickButtonsEnum.J04);
            AddControl(ControlContext.VOXEL_EDIT, MyControlsSpace.CUBE_ROTATE_ROLL_POSITIVE, MyJoystickButtonsEnum.J05);
            AddControl(ControlContext.VOXEL_EDIT, MyControlsSpace.CUBE_ROTATE_ROLL_NEGATIVE, MyJoystickButtonsEnum.J06);
            AddControl(ControlContext.VOXEL_EDIT, MyControlsSpace.CUBE_ROTATE_VERTICAL_POSITIVE, MyJoystickAxesEnum.Xneg);
            AddControl(ControlContext.VOXEL_EDIT, MyControlsSpace.CUBE_ROTATE_VERTICAL_NEGATIVE, MyJoystickAxesEnum.Xpos);
            AddControl(ControlContext.VOXEL_EDIT, MyControlsSpace.CUBE_ROTATE_HORISONTAL_POSITIVE, MyJoystickAxesEnum.Yneg);
            AddControl(ControlContext.VOXEL_EDIT, MyControlsSpace.CUBE_ROTATE_HORISONTAL_NEGATIVE, MyJoystickAxesEnum.Ypos);

            NullControl(ControlContext.VOXEL_EDIT, MyControlsSpace.CROUCH);
        }
        #endregion Binding methods

        #region Private binding adding methods
        private void AddContext(ControlContext contextId, ControlContext parentContextId = ControlContext.NONE)
        {
            ContextData contextData = new ContextData();
            contexts[(int)contextId] = contextData;

            if(parentContextId != ControlContext.NONE)
                contextData.ParentContext = contexts[(int)parentContextId];
        }

        private void AddControl(ControlContext contextId, MyStringId controlId, MyJoystickAxesEnum axis)
        {
            contexts[(int)contextId][controlId] = new AxisControl(axis);
        }

        private void AddControl(ControlContext contextId, MyStringId controlId, MyJoystickButtonsEnum button)
        {
            contexts[(int)contextId][controlId] = new ButtonControl(button);
        }

        private void NullControl(ControlContext contextId, MyStringId controlId)
        {
            contexts[(int)contextId][controlId] = null;
        }
        #endregion Private binding adding methods

        public interface IControl
        {
            string GetBind(bool specialChars = true);
            bool IsPressed();
            bool IsJustPressed();
        }

        private class AxisControl : IControl
        {
            public readonly MyJoystickAxesEnum Axis;

            public AxisControl(MyJoystickAxesEnum axis)
            {
                Axis = axis;
            }

            public string GetBind(bool specialChars = true) => InputLib.GetInputDisplayName(Axis, specialChars);
            public bool IsPressed() => MyAPIGateway.Input.IsJoystickAxisPressed(Axis);
            public bool IsJustPressed() => MyAPIGateway.Input.IsJoystickAxisNewPressed(Axis);
        }

        private class ButtonControl : IControl
        {
            public readonly MyJoystickButtonsEnum Button;

            public ButtonControl(MyJoystickButtonsEnum button)
            {
                Button = button;
            }

            public string GetBind(bool specialChars = true) => InputLib.GetInputDisplayName(Button, specialChars);
            public bool IsPressed() => MyAPIGateway.Input.IsJoystickButtonPressed(Button);
            public bool IsJustPressed() => MyAPIGateway.Input.IsJoystickButtonNewPressed(Button);
        }

        private class ContextData
        {
            public ContextData ParentContext;
            private readonly Dictionary<MyStringId, IControl> bindings = new Dictionary<MyStringId, IControl>(MyStringId.Comparer);

            public IControl this[MyStringId controlId]
            {
                get
                {
                    IControl control;
                    if(bindings.TryGetValue(controlId, out control))
                        return control;

                    if(ParentContext != null)
                        return ParentContext[controlId];

                    return null;
                }
                set
                {
                    bindings[controlId] = value;
                }
            }
        }
    }
}
