using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// The last step of the solve. It publishes where every leg is aiming, measured against the
    /// body the solve just finished resolving, so the query adapter running before the next solve
    /// reads the aim rather than reconstructing it.
    ///
    /// It writes nothing but <see cref="FootholdProbe"/> and <see cref="FootholdProbeFrame"/>, and
    /// reads no foot promise — publishing an aim can never move a foot.
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(ProceduralAnimationSolveSystemGroup))]
    [UpdateAfter(typeof(HardResolveSystem))]
    internal partial struct FootholdProbeSystem : ISystem
    {
        ComponentLookup<PlanarHeading> planarHeadings;

        public void OnCreate(ref SystemState state)
        {
            planarHeadings = state.GetComponentLookup<PlanarHeading>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            planarHeadings.Update(ref state);

            foreach (var (gait, gaitLegs, limbs, points, probes, frame, entity) in
                     SystemAPI.Query<RefRO<Gait>, DynamicBuffer<GaitLeg>, DynamicBuffer<Limb2BoneLeg>,
                         DynamicBuffer<VerletPoint>, DynamicBuffer<FootholdProbe>,
                         RefRW<FootholdProbeFrame>>().WithEntityAccess())
            {
                var planar = planarHeadings.HasComponent(entity);
                var forward = planar ? planarHeadings[entity].LastForward : new float2(1f, 0f);
                var legCount = math.min(gaitLegs.Length, limbs.Length);

                var mutableProbes = probes;
                mutableProbes.Clear();

                for (var index = 0; index < legCount; index++)
                {
                    var limbLeg = limbs[index];
                    if (limbLeg.RootPointIndex < 0 || limbLeg.RootPointIndex >= points.Length)
                    {
                        // Keep the buffer index-aligned with the legs: a leg with no valid hip
                        // publishes a zero probe rather than shifting every probe after it.
                        mutableProbes.Add(default);
                        continue;
                    }

                    var hipPoint = points[limbLeg.RootPointIndex];
                    var hip = hipPoint.Position;
                    var bodyVelocity = deltaTime > 0f
                        ? (hipPoint.Position - hipPoint.PreviousPosition) / deltaTime
                        : float2.zero;
                    var home = planar
                        ? PlanarMath.Home(hip, gaitLegs[index].HomeOffset, forward)
                        : hip + gaitLegs[index].HomeOffset;

                    mutableProbes.Add(new FootholdProbe
                    {
                        Home = home,
                        PredictedHome = home + bodyVelocity * gait.ValueRO.StepLead,
                        Hip = hip,
                        Valid = 1,
                    });
                }

                // Zero means "never published", so the counter starts at one and skips back to one
                // rather than wrapping through it.
                var next = frame.ValueRO.FrameId + 1u;
                frame.ValueRW.FrameId = next == 0u ? 1u : next;
                frame.ValueRW.Forward = forward;
            }
        }
    }
}
