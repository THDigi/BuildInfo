using System.Collections.Generic;
using Digi.BuildInfo.Features.Overlays.Specialized;
using VRage.ObjectBuilders;

namespace Digi.BuildInfo.Features.Overlays
{
    public class SpecializedOverlays : ModComponent
    {
        readonly Dictionary<MyObjectBuilderType, SpecializedOverlayBase> Overlays = new Dictionary<MyObjectBuilderType, SpecializedOverlayBase>(MyObjectBuilderType.Comparer);

        public SpecializedOverlays(BuildInfoMod main) : base(main)
        {
        }

        public override void RegisterComponent()
        {
            // TODO: add sensor/gravity/etc range overlay that's better than the vanilla one!

            new ButtonPanel(this);
            new Camera(this);
            new Collector(this);
            new ConveyorSorter(this);
            new Door(this);
            new LandingGear(this);
            new LaserAntenna(this);
            new Merge(this);
            new Motor(this);
            new Piston(this);
            new ShipDrill(this);
            new ShipTool(this);
            new Suspension(this);
            new TargetDummy(this);
            new Thruster(this);
            new Weapon(this);
            new WindTurbine(this);
        }

        public override void UnregisterComponent()
        {
        }

        public SpecializedOverlayBase Get(MyObjectBuilderType type) => Overlays.GetValueOrDefault(type, null);

        internal void Add(MyObjectBuilderType type, SpecializedOverlayBase handler) => Overlays.Add(type, handler);
    }
}
