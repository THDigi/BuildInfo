using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;

namespace Digi.BuildInfo
{
    public static class GameData
    {
        /// <summary>
        /// Distance in meters, returns power in MW.
        /// </summary>
        public static float LaserAntennaPowerUsage(MyLaserAntennaDefinition def, double distanceMeters)
        {
            // HACK copied and converted from MyLaserAntenna.UpdatePowerInput()

            double powerRatio = def.PowerInputLasing;
            double maxRange = (def.MaxRange < 0 ? double.MaxValue : def.MaxRange);

            double A = powerRatio / 2.0 / 200000.0;
            double B = powerRatio * 200000.0 - A * 200000.0 * 200000.0;
            double distance = Math.Min(distanceMeters, maxRange);

            if(distance > 200000)
            {
                return (float)((distance * distance) * A + B) / 1000000f;
            }
            else
            {
                return (float)(powerRatio * distance) / 1000000f;
            }
        }
    }
}
