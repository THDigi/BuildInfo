using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using VRage.Game.Entity;

namespace Digi.BuildInfo.Systems
{
    public class PlanetMonitor : ModComponent
    {
        public List<MyPlanet> Planets = new List<MyPlanet>();

        public PlanetMonitor(BuildInfoMod main) : base(main)
        {
            MyEntities.OnEntityAdd += EntityAdded;
        }

        public override void RegisterComponent()
        {
        }

        public override void UnregisterComponent()
        {
            MyEntities.OnEntityAdd -= EntityAdded;
        }

        void EntityAdded(MyEntity ent)
        {
            try
            {
                var planet = ent as MyPlanet;
                if(planet != null && !Planets.Contains(planet))
                {
                    Planets.Add(planet);
                    planet.OnMarkForClose += PlanetMarkedForClose;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void PlanetMarkedForClose(MyEntity ent)
        {
            try
            {
                var planet = (MyPlanet)ent;
                Planets.Remove(planet);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}