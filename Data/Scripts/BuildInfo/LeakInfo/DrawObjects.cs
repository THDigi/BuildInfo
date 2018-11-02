using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;

namespace Digi.BuildInfo.LeakInfo
{
    public class ParticleData
    {
        public readonly float Size;
        public readonly int SpawnDelay;
        public readonly double LerpPos;
        public readonly float WalkSpeed;

        public ParticleData(float size, int spawnDelay, double lerpPos, float walkSpeed)
        {
            Size = size;
            SpawnDelay = spawnDelay;
            LerpPos = lerpPos;
            WalkSpeed = walkSpeed;
        }
    }

    public class Particle
    {
        private Vector3D position;
        private float walk;

        public Particle(IMyCubeGrid grid, List<LineI> lines)
        {
            var l = lines[lines.Count - 1];
            position = grid.GridIntegerToWorld(l.Start);
            walk = lines.Count - 1;
        }

        /// <summary>
        /// Returns true if it should spawn more particles, false otherwise (reached the end and looping back)
        /// </summary>
        public bool Draw(IMyCubeGrid grid, List<LineI> lines, ref Color color, ParticleData pd, ref Vector3D camPos, ref Vector3D camFw)
        {
            var i = (walk < 0 ? 0 : (int)Math.Floor(walk));
            var l = lines[i];
            var lineStart = grid.GridIntegerToWorld(l.Start);
            var lineEnd = grid.GridIntegerToWorld(l.End);

            if(LeakInfoComponent.IsVisibleFast(ref camPos, ref camFw, ref lineStart) || LeakInfoComponent.IsVisibleFast(ref camPos, ref camFw, ref lineEnd))
            {
                var lineDir = (lineEnd - lineStart);
                var fraction = (1 - (walk - i));
                var targetPosition = lineStart + lineDir * fraction;

                if(!MyParticlesManager.Paused)
                    position = Vector3D.Lerp(position, targetPosition, pd.LerpPos);

                var drawPosition = camPos + ((position - camPos) * LeakInfoComponent.DRAW_DEPTH);

                if(walk < 0)
                    MyTransparentGeometry.AddPointBillboard(BuildInfo.Instance.LeakInfoComp.MATERIAL_DOT, color * (1f - Math.Abs(walk)), drawPosition, pd.Size * LeakInfoComponent.DRAW_DEPTH_F, 0);
                else
                    MyTransparentGeometry.AddPointBillboard(BuildInfo.Instance.LeakInfoComp.MATERIAL_DOT, color, drawPosition, pd.Size * LeakInfoComponent.DRAW_DEPTH_F, 0);
            }

            if(!MyParticlesManager.Paused)
            {
                walk -= pd.WalkSpeed; // walk on the lines

                if(walk < -1) // go back to the start and tell the component to stop spawning new ones
                {
                    l = lines[lines.Count - 1];
                    position = grid.GridIntegerToWorld(l.Start);
                    walk = lines.Count - 1;
                    return false;
                }
            }

            return true;
        }
    }
}
