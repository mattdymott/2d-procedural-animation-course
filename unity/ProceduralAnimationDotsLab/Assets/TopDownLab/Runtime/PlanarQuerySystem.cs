using Tealeaf.ProceduralAnimation.Dots;
using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Entities;
using Unity.Mathematics;

namespace TopDownLab
{
    /// <summary>
    /// Deterministic lesson stand-in for a tilemap, navmesh, or physics overlap. It reports facts
    /// about points a leg could step to and nothing else: it does not rank them, does not know
    /// which leg is about to swing, and never writes a plant.
    ///
    /// Replace this file with a real query backend and nothing else in the project changes.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(TopDownIntentSystem))]
    [UpdateBefore(typeof(ProceduralAnimationSolveSystemGroup))]
    public partial struct PlanarQuerySystem : ISystem
    {
        // A small fan around the predicted home: one straight ahead, then offsets along and
        // across the heading. Gait picks whichever legal one lands closest to where it aimed.
        const int FanSize = 5;
        const float FanSpread = 0.22f;

        EntityQuery islandQuery;

        public void OnCreate(ref SystemState state)
        {
            islandQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlanarIsland>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var island = islandQuery.IsEmptyIgnoreFilter
                ? new PlanarIsland { Radius = -1f }
                : islandQuery.GetSingleton<PlanarIsland>();

            foreach (var (gait, headingRef, gaitLegs, limbs, points, candidates, debugHits) in
                     SystemAPI.Query<RefRO<Gait>, RefRO<PlanarHeading>, DynamicBuffer<GaitLeg>,
                         DynamicBuffer<Limb2BoneLeg>, DynamicBuffer<VerletPoint>,
                         DynamicBuffer<FootholdCandidate>, DynamicBuffer<PlanarQueryDebugHit>>())
            {
                var mutableCandidates = candidates;
                var mutableDebug = debugHits;
                mutableCandidates.Clear();
                mutableDebug.Clear();

                var forward = headingRef.ValueRO.LastForward;
                var lateral = PlanarMath.Perpendicular(forward);
                var legCount = math.min(gaitLegs.Length, limbs.Length);

                for (var index = 0; index < legCount; index++)
                {
                    var limbLeg = limbs[index];
                    if (limbLeg.RootPointIndex < 0 || limbLeg.RootPointIndex >= points.Length)
                        continue;

                    var hipPoint = points[limbLeg.RootPointIndex];
                    var bodyVelocity = deltaTime > 0f
                        ? (hipPoint.Position - hipPoint.PreviousPosition) / deltaTime
                        : float2.zero;
                    var home = PlanarMath.Home(hipPoint.Position, gaitLegs[index].HomeOffset, forward);
                    var predictedHome = home + bodyVelocity * gait.ValueRO.StepLead;

                    for (var fan = 0; fan < FanSize; fan++)
                    {
                        var point = predictedHome + FanOffset(fan, forward, lateral);
                        var walkable = !Inside(island, point);

                        // The route is measured from the hip, not from the current plant. A
                        // blocked leg's plant goes stale while it waits, and a stale plant makes
                        // an ever-longer segment that clips the obstacle from further and further
                        // away — the leg would be locked out permanently by its own waiting.
                        var pathClear = !SegmentHitsIsland(island, hipPoint.Position, point);

                        mutableCandidates.Add(new FootholdCandidate
                        {
                            LegIndex = (byte)index,
                            Point = point,
                            Normal = new float2(0f, 1f),
                            Walkable = (byte)(walkable ? 1 : 0),
                            PathClear = (byte)(pathClear ? 1 : 0),
                        });
                        mutableDebug.Add(new PlanarQueryDebugHit
                        {
                            LegIndex = (byte)index,
                            Point = point,
                            Legal = (byte)(walkable && pathClear ? 1 : 0),
                        });
                    }
                }
            }
        }

        static float2 FanOffset(int fan, float2 forward, float2 lateral) => fan switch
        {
            1 => lateral * FanSpread,
            2 => -lateral * FanSpread,
            3 => forward * FanSpread,
            4 => -forward * FanSpread,
            _ => float2.zero,
        };

        static bool Inside(in PlanarIsland island, float2 point) =>
            island.Radius > 0f && math.distance(point, island.Centre) < island.Radius;

        /// <summary>Does the straight route from the leg's hip to a candidate cross the blocked region?</summary>
        static bool SegmentHitsIsland(in PlanarIsland island, float2 from, float2 to)
        {
            if (island.Radius <= 0f)
                return false;

            var segment = to - from;
            var lengthSq = math.lengthsq(segment);
            var t = lengthSq > 1e-8f
                ? math.saturate(math.dot(island.Centre - from, segment) / lengthSq)
                : 0f;
            return math.distance(from + segment * t, island.Centre) < island.Radius;
        }
    }
}
