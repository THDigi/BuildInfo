using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Digi
{
    public static class InputWrapper
    {
        static HashSet<MyStringId> ErrorControls = new HashSet<MyStringId>(MyStringId.Comparer);

        /// <summary>
        /// Quick and dirty replacement for <see cref="IMyInput.IsGameControlPressed(MyStringId)"/>.
        /// </summary>
        public static bool IsControlPressed(MyStringId controlId)
        {
            IMyControl control = MyAPIGateway.Input.GetGameControl(controlId);

            if(control == null)
            {
                if(ErrorControls.Add(controlId))
                    Log.Error($"Could not get game control for ID: {controlId}");

                return false;
            }

#if VERSION_200 || VERSION_201 || VERSION_202 || VERSION_203 || VERSION_204 || VERSION_205 // some backwards compatibility
            return control.IsPressed();
#else
            bool origEnabled = control.IsEnabled;
            try
            {
                control.IsEnabled = true;
                return control.IsPressed();
            }
            finally
            {
                control.IsEnabled = origEnabled;
            }
#endif
        }

        /// <summary>
        /// Quick and dirty replacement for <see cref="IMyInput.IsNewGameControlPressed(MyStringId)"/>.
        /// </summary>
        public static bool IsControlJustPressed(MyStringId controlId)
        {
            IMyControl control = MyAPIGateway.Input.GetGameControl(controlId);

            if(control == null)
            {
                if(ErrorControls.Add(controlId))
                    Log.Error($"Could not get game control for ID: {controlId}");

                return false;
            }

#if VERSION_200 || VERSION_201 || VERSION_202 || VERSION_203 || VERSION_204 || VERSION_205 // some backwards compatibility
            return control.IsNewPressed();
#else
            bool origEnabled = control.IsEnabled;
            try
            {
                control.IsEnabled = true;
                return control.IsNewPressed();
            }
            finally
            {
                control.IsEnabled = origEnabled;
            }
#endif
        }

        /// <summary>
        /// Quick and dirty replacement for <see cref="IMyInput.IsGameControlReleased(MyStringId)"/> and <see cref="IMyInput.IsNewGameControlReleased(MyStringId)"/> (which are both the same thing btw).
        /// </summary>
        public static bool IsControlJustReleased(MyStringId controlId)
        {
            IMyControl control = MyAPIGateway.Input.GetGameControl(controlId);

            if(control == null)
            {
                if(ErrorControls.Add(controlId))
                    Log.Error($"Could not get game control for ID: {controlId}");

                return false;
            }

#if VERSION_200 || VERSION_201 || VERSION_202 || VERSION_203 || VERSION_204 || VERSION_205 // some backwards compatibility
            return control.IsNewReleased();
#else
            bool origEnabled = control.IsEnabled;
            try
            {
                control.IsEnabled = true;
                return control.IsNewReleased();
            }
            finally
            {
                control.IsEnabled = origEnabled;
            }
#endif
        }
    }
}
