using Digi.BuildInfo.Features.Config;
using Digi.ComponentLib;
using Digi.Input;

namespace Digi.BuildInfo.Systems
{
    public class InputLibHandler : ClientComponent
    {
        public InputLib InputLib;

        public InputLibHandler(Client mod) : base(mod)
        {
            Flags = UpdateFlags.UPDATE_INPUT;
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
