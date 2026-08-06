using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct MovingSupportSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (motion, pose) in SystemAPI.Query<RefRW<SupportMotion>, RefRW<SupportPose>>())
            {
                var supportMotion = motion.ValueRO;
                var previousPose = pose.ValueRO;
                supportMotion.Time += deltaTime;

                var phase = supportMotion.Time * supportMotion.Frequency;
                var nextPose = new SupportPose
                {
                    Position = supportMotion.Origin + supportMotion.Amplitude * new float2(math.sin(phase), math.sin(phase + math.PI * 0.5f)),
                    Rotation = math.sin(phase * 0.7f) * 0.06f,
                };
                supportMotion.WorldVelocity = deltaTime > 0f
                    ? (nextPose.Position - previousPose.Position) / deltaTime
                    : float2.zero;
                supportMotion.AngularVelocity = deltaTime > 0f
                    ? (nextPose.Rotation - previousPose.Rotation) / deltaTime
                    : 0f;
                motion.ValueRW = supportMotion;
                pose.ValueRW = nextPose;
            }
        }
    }
}
