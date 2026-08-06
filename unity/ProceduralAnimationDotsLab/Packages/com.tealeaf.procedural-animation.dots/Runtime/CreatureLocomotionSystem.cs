using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(VerletChainSystem))]
    public partial struct CreatureLocomotionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (body, locomotion) in SystemAPI.Query<RefRW<CreatureBody>, RefRO<CreatureLocomotion>>())
            {
                var carryVelocity = body.ValueRO.CarryVelocity;
                body.ValueRW.RootPosition += (locomotion.ValueRO.DesiredVelocity + carryVelocity) * deltaTime;
                body.ValueRW.CarryVelocity = math.lerp(carryVelocity, float2.zero, math.saturate(deltaTime * 8f));
            }
        }
    }
}
