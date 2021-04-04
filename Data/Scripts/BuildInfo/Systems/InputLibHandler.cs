using Digi.BuildInfo.Features.Config;
using Digi.ComponentLib;
using Digi.Input;

namespace Digi.BuildInfo.Systems
{
    public class InputLibHandler : ModComponent
    {
        public InputLib InputLib;

        public InputLibHandler(BuildInfoMod main) : base(main)
        {
            UpdateMethods = UpdateFlags.UPDATE_INPUT;
            InputLib = new InputLib();
            InputLib.AddCustomInput(new MenuCustomInput());
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            InputLib.Dispose();
        }

        public override void UpdateInput(bool anyKeyOrMouse, bool inMenu, bool paused)
        {
            if(inMenu || paused)
                return;

            InputLib.UpdateInput();
        }
    }
}
