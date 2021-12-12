using Sandbox.ModAPI;
using VRage.Game;

namespace Digi.BuildInfo.Systems
{
    public class ModDetector : ModComponent
    {
        public bool DetectedAwwScrap = false;

        public ModDetector(BuildInfoMod main) : base(main)
        {
            foreach(MyObjectBuilder_Checkpoint.ModItem modInfo in MyAPIGateway.Session.Mods)
            {
                if(modInfo.PublishedFileId == 1542310718)
                {
                    DetectedAwwScrap = true;
                }
            }
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
        }
    }
}