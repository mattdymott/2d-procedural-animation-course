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
    ///
    /// It reads the package's published <see cref="FootholdProbe"/> buffer rather than working out
    /// where each leg is aiming, so the aim it offers around is the same one gait will judge
    /// against — the two can no longer drift apart. Stamping each candidate with the frame it was
    /// observed on is what lets gait notice if this adapter ever falls behind.
    ///
    /// It leaves <c>FootholdCandidate.Support</c> and <c>.SupportLocalPoint</c> default because
    /// this arena is static — not because a planar creature cannot ride a moving support. It can;
    /// the side-view Lab sample demonstrates it and <c>PlanarGaitTests</c> pins it on a creature
    /// carrying <c>PlanarHeading</c>.
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
        BufferLookup<PlanarQueryDebugHit> debugHitLookup;

        public void OnCreate(ref SystemState state)
        {
            islandQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlanarIsland>());
            debugHitLookup = state.GetBufferLookup<PlanarQueryDebugHit>();
        }

        public void OnUpdate(ref SystemState state)
        {
            debugHitLookup.Update(ref state);
            var island = islandQuery.IsEmptyIgnoreFilter
                ? new PlanarIsland { Radius = -1f }
                : islandQuery.GetSingleton<PlanarIsland>();

            // The debug buffer is looked up, never queried on. Asking for it in the query tuple
            // would let a presentation-only concern decide which creatures get footholds at all —
            // drop the debug component and the creature silently stops walking.
            //
            // PlanarHeading is a filter rather than a read: it is what marks a creature top-down,
            // and a side-view creature has its own adapter.
            foreach (var (frameRef, probes, candidates, entity) in
                     SystemAPI.Query<RefRO<FootholdProbeFrame>, DynamicBuffer<FootholdProbe>,
                         DynamicBuffer<FootholdCandidate>>()
                         .WithAll<PlanarHeading>().WithEntityAccess())
            {
                var mutableCandidates = candidates;
                mutableCandidates.Clear();

                var recordDebug = debugHitLookup.HasBuffer(entity);
                var mutableDebug = recordDebug ? debugHitLookup[entity] : default;
                if (recordDebug)
                    mutableDebug.Clear();

                // Nothing has been published yet on the very first tick. Offering a fan around an
                // aim that does not exist would be worse than offering nothing.
                var frame = frameRef.ValueRO;
                if (frame.FrameId == 0u)
                    continue;

                var forward = frame.Forward;
                var lateral = PlanarMath.Perpendicular(forward);

                for (var index = 0; index < probes.Length; index++)
                {
                    var probe = probes[index];
                    if (probe.Valid == 0)
                        continue;

                    for (var fan = 0; fan < FanSize; fan++)
                    {
                        var point = probe.PredictedHome + FanOffset(fan, forward, lateral);
                        var walkable = !Inside(island, point);

                        // The route is measured from the hip, not from the current plant. A
                        // blocked leg's plant goes stale while it waits, and a stale plant makes
                        // an ever-longer segment that clips the obstacle from further and further
                        // away — the leg would be locked out permanently by its own waiting.
                        var pathClear = !SegmentHitsIsland(island, probe.Hip, point);

                        mutableCandidates.Add(new FootholdCandidate
                        {
                            LegIndex = (byte)index,
                            Point = point,
                            Normal = new float2(0f, 1f),
                            Walkable = (byte)(walkable ? 1 : 0),
                            PathClear = (byte)(pathClear ? 1 : 0),
                            ObservedFrame = frame.FrameId,
                        });
                        if (recordDebug)
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
