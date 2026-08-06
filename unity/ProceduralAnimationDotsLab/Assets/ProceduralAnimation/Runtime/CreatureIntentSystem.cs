using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(VerletChainSystem))]
    public partial struct CreatureIntentSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (body, intent) in SystemAPI.Query<RefRW<CreatureBody>, RefRO<CreatureIntent>>())
            {
                var carryVelocity = body.ValueRO.CarryVelocity;
                body.ValueRW.RootPosition += (intent.ValueRO.DesiredVelocity + carryVelocity) * deltaTime;
                body.ValueRW.CarryVelocity = math.lerp(carryVelocity, float2.zero, math.saturate(deltaTime * 8f));
            }
        }
    }
}
