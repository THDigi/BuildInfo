using System;
using System.Collections.Generic;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Digi.BuildInfo.Features.LeakInfo
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

    public enum DrawReturn { None, StopSpawning }

    public class Particle
    {
        Vector3 LocalPos;
        float Walk;
        bool StopTrailSpawn = false;
        readonly List<ParticleTrail> Trails = new List<ParticleTrail>();

        struct ParticleTrail
        {
            public readonly Vector3 LocalPos;
            public readonly int ExpiresAtTick;

            public ParticleTrail(Vector3 localPos, int expiresAtTick)
            {
                LocalPos = localPos;
                ExpiresAtTick = expiresAtTick;
            }
        }

        const int TrailLifeTicks = 10;
        const int TrailPointsPerTick = 5;
        const float TrailLerpPerPoint = (1f / TrailPointsPerTick);

        const float TrailWorldColorMul = 1f;
        const float TrailOverlayColorMul = 1f;
        const BlendTypeEnum BlendType = BlendTypeEnum.Standard;

        public Particle Init(IMyCubeGrid grid, List<MyTuple<Vector3I, Vector3I>> lines)
        {
            MyTuple<Vector3I, Vector3I> l = lines[lines.Count - 1];
            LocalPos = l.Item1 * grid.GridSize;
            Walk = lines.Count - 1;
            StopTrailSpawn = false;
            Trails.Clear();
            return this;
        }

        public DrawReturn Draw(IMyCubeGrid grid, List<MyTuple<Vector3I, Vector3I>> lines, Color colorWorld, Color colorOverlay, ParticleData particleData, ref Vector3D camPos, double dotPosFw, bool spawnSync)
        {
            LeakInfo leakInfo = BuildInfoMod.Instance.LeakInfo;

            int lineIndex = (Walk < 0 ? 0 : (int)Math.Floor(Walk));
            MyTuple<Vector3I, Vector3I> line = lines[lineIndex];
            Vector3 lineStartLocal = line.Item1 * grid.GridSize;
            Vector3 lineEndLocal = line.Item2 * grid.GridSize;

            Vector3 prevLocalPos = LocalPos;

            Vector3 lineDirLocal = (lineEndLocal - lineStartLocal);
            float fraction = (1 - ((Walk < 0 ? 0 : Walk) - lineIndex));
            Vector3 targetPosLocal = lineStartLocal + lineDirLocal * fraction;

            int tick = BuildInfoMod.Instance.Tick;
            bool paused = MyParticlesManager.Paused;
            if(!paused)
                LocalPos = Vector3D.Lerp(LocalPos, targetPosLocal, particleData.LerpPos);

            Vector3D posWorld = Vector3D.Transform(LocalPos, grid.WorldMatrix);

            //bool pointVisible = LeakInfo.IsVisibleFast(ref camPos, ref camFw, ref posWorld);
            // HACK: manually inlined ^v
            bool pointVisible = (Vector3D.Dot(camPos, posWorld) - dotPosFw > 0);

            if(!StopTrailSpawn && !paused)
            {
                for(int t = 0; t < TrailPointsPerTick; t++)
                {
                    float lerp = (t % TrailPointsPerTick) * TrailLerpPerPoint;
                    Trails.Add(new ParticleTrail(Vector3D.Lerp(prevLocalPos, LocalPos, lerp), tick + TrailLifeTicks));
                }
            }

            if(pointVisible)
            {
                Vector3D posOverlay = camPos + ((posWorld - camPos) * LeakInfo.DrawDepth);
                float alpha = 1f;

                if(Walk < 0)
                    alpha = (1f - Math.Abs(Walk));

                if(colorWorld.A > 0)
                    MyTransparentGeometry.AddPointBillboard(leakInfo.ParticleMaterial, (colorWorld * alpha).ToVector4() * TrailWorldColorMul, posWorld, particleData.Size, 0, blendType: BlendType);

                if(colorOverlay.A > 0)
                    MyTransparentGeometry.AddPointBillboard(leakInfo.ParticleMaterial, (colorOverlay * alpha).ToVector4() * TrailOverlayColorMul, posOverlay, particleData.Size * LeakInfo.DrawDepthF, 0, blendType: BlendType);
            }

            for(int t = Trails.Count - 1; t >= 0; --t)
            {
                ParticleTrail trail = Trails[t];

                if(!paused && trail.ExpiresAtTick <= tick) // trail expired
                {
                    float lerp = (t % TrailPointsPerTick) * TrailLerpPerPoint;
                    trail = new ParticleTrail(Vector3D.Lerp(prevLocalPos, LocalPos, lerp), tick + TrailLifeTicks);
                    Trails[t] = trail;
                    StopTrailSpawn = true;
                }

                if(pointVisible && trail.ExpiresAtTick > tick)
                {
                    float lifeRatio = ((trail.ExpiresAtTick - tick) / (float)TrailLifeTicks);
                    float alpha = lifeRatio;
                    float size = 0.8f + (0.2f * lifeRatio);

                    Vector3D trailPosWorld = Vector3D.Transform(trail.LocalPos, grid.WorldMatrix);
                    Vector3D trailPosOverlay = camPos + ((trailPosWorld - camPos) * LeakInfo.DrawDepth);

                    if(colorWorld.A > 0)
                        MyTransparentGeometry.AddPointBillboard(leakInfo.ParticleMaterial, (colorWorld * alpha).ToVector4() * TrailWorldColorMul, trailPosWorld, particleData.Size * size, 0, blendType: BlendType);

                    if(colorOverlay.A > 0)
                        MyTransparentGeometry.AddPointBillboard(leakInfo.ParticleMaterial, (colorOverlay * alpha).ToVector4() * TrailOverlayColorMul, trailPosOverlay, particleData.Size * size * LeakInfo.DrawDepthF, 0, blendType: BlendType);
                }
            }

            if(!paused)
            {
                Walk -= particleData.WalkSpeed; // walk on the lines

                if(Walk < -1) // go back to the start and tell the component to stop spawning new ones
                {
                    if(spawnSync) // keep the spawn timing
                    {
                        LocalPos = lines[lines.Count - 1].Item1 * grid.GridSize;
                        Walk = lines.Count - 1;
                    }

                    return DrawReturn.StopSpawning;
                }
            }

            return DrawReturn.None;
        }
    }
}
