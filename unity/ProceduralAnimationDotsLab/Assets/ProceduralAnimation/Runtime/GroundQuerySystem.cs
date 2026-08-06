using Unity.Entities;
using Unity.Mathematics;
using Tealeaf.ProceduralAnimation.Dots;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(VerletChainSystem))]
    [UpdateAfter(typeof(MovingSupportSystem))]
    [UpdateBefore(typeof(GaitSystem))]
    public partial struct GroundQuerySystem : ISystem
    {
        EntityQuery supportQuery;

        public void OnCreate(ref SystemState state)
        {
            supportQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<SupportKinematics>(),
                ComponentType.ReadOnly<SupportPose>());
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var supportEntity = Entity.Null;
            var supportPose = default(SupportPose);
            if (!supportQuery.IsEmptyIgnoreFilter)
            {
                supportEntity = supportQuery.GetSingletonEntity();
                supportPose = state.EntityManager.GetComponentData<SupportPose>(supportEntity);
            }

            foreach (var (settings, gaitLegs, limbs, points, candidates, debugHits) in SystemAPI.Query<RefRO<GaitSettings>, DynamicBuffer<GaitLeg>, DynamicBuffer<Limb2BoneLeg>, DynamicBuffer<VerletPoint>, DynamicBuffer<FootholdCandidate>, DynamicBuffer<GroundQueryDebugHit>>())
            {
                var mutableCandidates = candidates;
                var mutableDebugHits = debugHits;
                mutableCandidates.Clear();
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
                    var probe = hipPoint.Position + gaitLegs[index].HomeOffset + bodyVelocity * settings.ValueRO.StepLead;
                    var legIndex = (byte)index;
                    if (supportEntity != Entity.Null
                        && GroundQuery.TrySampleSupport(legIndex, probe, supportEntity, supportPose, out var supportCandidate))
                    {
                        mutableCandidates.Add(supportCandidate);
                        mutableDebugHits.Add(GroundQuery.CreateDebugHit(legIndex, probe, supportCandidate));
                    }
                    else
                    {
                        var candidate = GroundQuery.Sample(legIndex, probe);
                        mutableCandidates.Add(candidate);
                        mutableDebugHits.Add(GroundQuery.CreateDebugHit(legIndex, probe, candidate));
                    }
                }
            }
        }
    }
}
