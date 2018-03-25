using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity.EntityComponents;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

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

        /// <summary>
        /// Returns maximum possible force applied to targetBlock's grid from sourceGrid's grinder.
        /// </summary>
        public static float ShipGrinderImpulseForce(IMyCubeGrid sourceGrid, IMySlimBlock targetBlock)
        {
            var targetGrid = targetBlock.CubeGrid;

            if(MyAPIGateway.Session.SessionSettings.EnableToolShake && targetGrid.Physics != null && !targetGrid.Physics.IsStatic)
            {
                var f = 1.73205078f; // MyUtils.GetRandomVector3()'s max length
                return (f * sourceGrid.GridSize * 500f);
            }

            return 0f;
        }

        /// <summary>
        /// Because the game has 2 ownership systems and I've no idea which one is actually used in what case, and it doesn't seem it knows either since it uses both in initialization
        /// </summary>
        public static MyOwnershipShareModeEnum GetBlockShareMode(IMyCubeBlock block)
        {
            if(block != null)
            {
                var internalBlock = (MyCubeBlock)block;

                // HACK MyEntityOwnershipComponent is not whitelisted
                //var ownershipComp = internalBlock.Components.Get<MyEntityOwnershipComponent>();
                //
                //if(ownershipComp != null)
                //    return ownershipComp.ShareMode;

                if(internalBlock.IDModule != null)
                    return internalBlock.IDModule.ShareMode;
            }

            return MyOwnershipShareModeEnum.None;
        }
    }
}
