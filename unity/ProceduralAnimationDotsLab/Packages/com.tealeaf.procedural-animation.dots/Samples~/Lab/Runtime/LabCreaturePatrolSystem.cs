using Unity.Entities;
using Unity.Mathematics;
using Tealeaf.ProceduralAnimation.Dots;

namespace ProceduralAnimationDotsLab
{
    /// <summary>
    /// The lesson creature's own motion policy: where it walks, and where its tail reaches.
    /// Both are consumer concerns — the package applies locomotion, owns <c>CreatureBody</c>,
    /// and draws the chain tip toward whatever <c>ChainTarget</c> this system last wrote.
    /// </summary>
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(ProceduralAnimationSolveSystemGroup))]
    public partial struct LabCreaturePatrolSystem : ISystem
    {
        // The lesson tail reach: how far ahead of the body the tip aims, and how it sways.
        // These are this creature's character, not package behaviour.
        const float TailReach = 6.5f;
        const float TailSwayAmplitude = 1.3f;
        const float TailSwayFrequency = 1.7f;

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (body, locomotion, target, patrol) in SystemAPI.Query<RefRO<CreatureBody>, RefRW<CreatureLocomotion>, RefRW<ChainTarget>, RefRW<LabCreaturePatrol>>())
            {
                var direction = patrol.ValueRO.Direction;
                if (body.ValueRO.RootPosition.x <= patrol.ValueRO.MinimumX)
                    direction = 1f;
                else if (body.ValueRO.RootPosition.x >= patrol.ValueRO.MaximumX)
                    direction = -1f;

                patrol.ValueRW.Direction = direction;
                locomotion.ValueRW.DesiredVelocity = new float2(patrol.ValueRO.Speed * direction, 0f);

                var time = patrol.ValueRO.Time + deltaTime;
                patrol.ValueRW.Time = time;
                target.ValueRW.Position = body.ValueRO.RootPosition + new float2(
                    TailReach,
                    math.sin(time * TailSwayFrequency) * TailSwayAmplitude);
            }
        }
    }
}
