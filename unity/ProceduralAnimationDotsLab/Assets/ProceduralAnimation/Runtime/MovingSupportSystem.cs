using Unity.Entities;
using Unity.Mathematics;
using Tealeaf.ProceduralAnimation.Dots;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct MovingSupportSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (motion, pose, kinematics) in SystemAPI.Query<RefRW<DemoMovingSupport>, RefRW<SupportPose>, RefRW<SupportKinematics>>())
            {
                var supportMotion = motion.ValueRO;
                var previousPose = pose.ValueRO;
                supportMotion.Time += deltaTime;

                var phase = supportMotion.Time * supportMotion.Frequency;
                var nextPose = new SupportPose
                {
                    Position = supportMotion.Origin + supportMotion.Amplitude * new float2(math.sin(phase), math.sin(phase + math.PI * 0.5f)),
                    RotationRadians = math.sin(phase * 0.7f) * 0.06f,
                };
                var nextKinematics = kinematics.ValueRO;
                nextKinematics.LinearVelocity = deltaTime > 0f
                    ? (nextPose.Position - previousPose.Position) / deltaTime
                    : float2.zero;
                nextKinematics.AngularVelocityRadians = deltaTime > 0f
                    ? (nextPose.RotationRadians - previousPose.RotationRadians) / deltaTime
                    : 0f;
                nextKinematics.SurfaceVelocityLocal = supportMotion.SurfaceVelocityLocal;
                motion.ValueRW = supportMotion;
                pose.ValueRW = nextPose;
                kinematics.ValueRW = nextKinematics;
            }
        }
    }
}
