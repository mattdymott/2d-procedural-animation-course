using Unity.Entities;
using Unity.Mathematics;
using Tealeaf.ProceduralAnimation.Dots;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(MovingSupportSystem))]
    [UpdateBefore(typeof(ProceduralAnimationSolveSystemGroup))]
    public partial struct GroundQuerySystem : ISystem
    {
        EntityQuery supportQuery;
        BufferLookup<GroundQueryDebugHit> debugHitLookup;

        public void OnCreate(ref SystemState state)
        {
            supportQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<SupportKinematics>(),
                ComponentType.ReadOnly<SupportPose>());
            debugHitLookup = state.GetBufferLookup<GroundQueryDebugHit>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            debugHitLookup.Update(ref state);
            var supportEntity = Entity.Null;
            var supportPose = default(SupportPose);
            if (!supportQuery.IsEmptyIgnoreFilter)
            {
                supportEntity = supportQuery.GetSingletonEntity();
                supportPose = state.EntityManager.GetComponentData<SupportPose>(supportEntity);
            }

            // The debug buffer is looked up, never queried on. Asking for it in the query tuple
            // would let a presentation-only concern decide which creatures get footholds at all —
            // drop the debug component and the creature silently stops walking.
            //
            // LabTerrainAdapter is the opt-in that scopes this adapter. The package's gait buffers
            // alone are not enough to identify a creature as ours: a top-down creature in the same
            // world carries every one of them, and would have its own footholds overwritten.
            foreach (var (gait, gaitLegs, limbs, points, candidates, entity) in SystemAPI.Query<RefRO<Gait>, DynamicBuffer<GaitLeg>, DynamicBuffer<Limb2BoneLeg>, DynamicBuffer<VerletPoint>, DynamicBuffer<FootholdCandidate>>().WithAll<LabTerrainAdapter>().WithEntityAccess())
            {
                var mutableCandidates = candidates;
                mutableCandidates.Clear();

                var recordDebug = debugHitLookup.HasBuffer(entity);
                var mutableDebugHits = recordDebug ? debugHitLookup[entity] : default;
                if (recordDebug)
                    mutableDebugHits.Clear();

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
                    var probe = hipPoint.Position + gaitLegs[index].HomeOffset + bodyVelocity * gait.ValueRO.StepLead;
                    var legIndex = (byte)index;
                    if (supportEntity != Entity.Null
                        && GroundQuery.TrySampleSupport(legIndex, probe, supportEntity, supportPose, out var supportCandidate))
                    {
                        mutableCandidates.Add(supportCandidate);
                        if (recordDebug)
                            mutableDebugHits.Add(GroundQuery.CreateDebugHit(legIndex, probe, supportCandidate));
                    }
                    else
                    {
                        var candidate = GroundQuery.Sample(legIndex, probe);
                        mutableCandidates.Add(candidate);
                        if (recordDebug)
                            mutableDebugHits.Add(GroundQuery.CreateDebugHit(legIndex, probe, candidate));
                    }
                }
            }
        }
    }
}
