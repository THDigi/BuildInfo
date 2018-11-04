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

    public class ParticleTrail
    {
        public Vector3 PosLocal;
        public int Life;
        public float Lerp;

        public ParticleTrail(Vector3 posLocal, int life, float lerp)
        {
            PosLocal = posLocal;
            Life = life;
            Lerp = lerp;
        }
    }

    public enum DrawReturn { NONE, STOP_SPAWNING }

    public class Particle
    {
        private Vector3 posLocal;
        private float walk;
        private readonly List<ParticleTrail> trails = new List<ParticleTrail>();
        private bool stopTrailSpawn = false;

        private const int TRAIL_LIFE = 10;

        public Particle(IMyCubeGrid grid, List<LineI> lines)
        {
            var l = lines[lines.Count - 1];
            posLocal = l.Start * grid.GridSize;
            walk = lines.Count - 1;
        }

        /// <summary>
        /// Returns true if it should spawn more particles, false otherwise (reached the end and looping back)
        /// </summary>
        public DrawReturn Draw(IMyCubeGrid grid, List<LineI> lines, Color colorWorld, Color colorOverlay, ParticleData particleData, ref Vector3D camPos, ref Vector3D camFw, ref int delayParticles, bool spawnSync)
        {
            var paused = MyParticlesManager.Paused;
            var leakInfoComp = BuildInfo.Instance.LeakInfoComp;

            var lineIndex = (walk < 0 ? 0 : (int)Math.Floor(walk));
            var line = lines[lineIndex];
            var lineStartLocal = line.Start * grid.GridSize;
            var lineEndLocal = line.End * grid.GridSize;

            var prevPosLocal = posLocal;

            var lineDirLocal = (lineEndLocal - lineStartLocal);
            var fraction = (1 - ((walk < 0 ? 0 : walk) - lineIndex));
            var targetPosLocal = lineStartLocal + lineDirLocal * fraction;

            if(!paused)
                posLocal = Vector3D.Lerp(posLocal, targetPosLocal, particleData.LerpPos);

            var posWorld = Vector3D.Transform(posLocal, grid.WorldMatrix);
            var pointVisible = LeakInfoComponent.IsVisibleFast(ref camPos, ref camFw, ref posWorld);

            if(!stopTrailSpawn && !paused)
            {
                for(float r = 0f; r < 1f; r += 0.2f)
                {
                    trails.Add(new ParticleTrail(Vector3D.Lerp(prevPosLocal, posLocal, r), TRAIL_LIFE, r));
                }
            }

            if(pointVisible)
            {
                var posOverlay = camPos + ((posWorld - camPos) * LeakInfoComponent.DRAW_DEPTH);
                float alpha = 1f;

                if(walk < 0)
                    alpha = (1f - Math.Abs(walk));

                MyTransparentGeometry.AddPointBillboard(leakInfoComp.MATERIAL_PARTICLE, (colorWorld * alpha).ToVector4() * 2, posWorld, particleData.Size, 0);
                MyTransparentGeometry.AddPointBillboard(leakInfoComp.MATERIAL_PARTICLE, (colorOverlay * alpha), posOverlay, particleData.Size * LeakInfoComponent.DRAW_DEPTH_F, 0);
            }

            for(int t = trails.Count - 1; t >= 0; --t)
            {
                var trail = trails[t];

                if(!paused && --trail.Life <= 0)
                {
                    trail.Life = TRAIL_LIFE;
                    trail.PosLocal = Vector3D.Lerp(prevPosLocal, posLocal, trail.Lerp);

                    stopTrailSpawn = true;
                }

                if(pointVisible && trail.Life > 0)
                {
                    var lifeRatio = ((float)trail.Life / (float)TRAIL_LIFE);

                    var trailPosWorld = Vector3D.Transform(trail.PosLocal, grid.WorldMatrix);
                    var trailPosOverlay = camPos + ((trailPosWorld - camPos) * LeakInfoComponent.DRAW_DEPTH);

                    var alpha = lifeRatio * 0.75f;
                    var size = 0.8f + (lifeRatio * 0.2f);

                    MyTransparentGeometry.AddPointBillboard(leakInfoComp.MATERIAL_PARTICLE, (colorWorld * alpha).ToVector4() * 2, trailPosWorld, particleData.Size * size, 0);
                    MyTransparentGeometry.AddPointBillboard(leakInfoComp.MATERIAL_PARTICLE, (colorOverlay * alpha), trailPosOverlay, particleData.Size * size * LeakInfoComponent.DRAW_DEPTH_F, 0);
                }
            }

            if(!paused)
            {
                walk -= particleData.WalkSpeed; // walk on the lines

                if(walk < -1) // go back to the start and tell the component to stop spawning new ones
                {
                    if(spawnSync) // keep the spawn timing
                    {
                        posLocal = lines[lines.Count - 1].Start * grid.GridSize;
                        walk = lines.Count - 1;
                    }

                    return DrawReturn.STOP_SPAWNING;
                }
            }

            return DrawReturn.NONE;
        }
    }
}
