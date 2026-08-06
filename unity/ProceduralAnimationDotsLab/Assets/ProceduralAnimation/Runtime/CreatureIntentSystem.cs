using Unity.Entities;

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
                body.ValueRW.RootPosition += intent.ValueRO.DesiredVelocity * deltaTime;
        }
    }
}
