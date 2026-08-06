using Tealeaf.ProceduralAnimation.Dots;
using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralAnimationPackageConsumer
{
    public struct SampleCreaturePatrol : IComponentData
    {
        public float Speed;
        public float Direction;
        public float MinimumX;
        public float MaximumX;
    }

    public struct SampleFlatGround : IComponentData
    {
        public float Height;
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(CreatureLocomotionSystem))]
    public partial struct SampleCreaturePatrolSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (body, locomotion, patrol) in SystemAPI.Query<RefRO<CreatureBody>, RefRW<CreatureLocomotion>, RefRW<SampleCreaturePatrol>>())
            {
                var direction = patrol.ValueRO.Direction;
                if (body.ValueRO.RootPosition.x <= patrol.ValueRO.MinimumX)
                    direction = 1f;
                else if (body.ValueRO.RootPosition.x >= patrol.ValueRO.MaximumX)
                    direction = -1f;

                patrol.ValueRW.Direction = direction;
                locomotion.ValueRW.DesiredVelocity = new float2(patrol.ValueRO.Speed * direction, 0f);
            }
        }
    }

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(VerletChainSystem))]
    [UpdateBefore(typeof(GaitSystem))]
    public partial struct FlatGroundFootholdAdapterSystem : ISystem
    {
        EntityQuery groundQuery;

        public void OnCreate(ref SystemState state)
        {
            groundQuery = state.GetEntityQuery(ComponentType.ReadOnly<SampleFlatGround>());
        }

        public void OnUpdate(ref SystemState state)
        {
            if (groundQuery.IsEmptyIgnoreFilter)
                return;

            var ground = state.EntityManager.GetComponentData<SampleFlatGround>(groundQuery.GetSingletonEntity());
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (settings, gaitLegs, limbs, points, candidates) in SystemAPI.Query<RefRO<GaitSettings>, DynamicBuffer<GaitLeg>, DynamicBuffer<Limb2BoneLeg>, DynamicBuffer<VerletPoint>, DynamicBuffer<FootholdCandidate>>())
            {
                candidates.Clear();
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
                    candidates.Add(new FootholdCandidate
                    {
                        LegIndex = (byte)index,
                        Point = new float2(probe.x, ground.Height),
                        Normal = new float2(0f, 1f),
                    });
                }
            }
        }
    }
}
