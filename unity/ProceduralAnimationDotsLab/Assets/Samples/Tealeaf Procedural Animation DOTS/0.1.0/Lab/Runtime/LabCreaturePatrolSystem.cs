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
        // These are this creature's character, not package behaviour. Both are fractions of
        // the chain's own length rather than world units, because the world-unit versions
        // (6.5 and 1.3) only worked for the chain this scene shipped with: shorten the chain
        // and the target moves out of reach, and the tail stops curving and reads as a rigid
        // over-stretched rod. That is the same failure the package removed when it stopped
        // hardcoding a +6.5 tip target — it survived here as a literal until the scene was
        // retuned. Against the shipped 15 x 0.48 chain these reproduce the old values.
        const float TailReachFraction = 0.9f;
        const float TailSwayFraction = 0.18f;
        const float TailSwayFrequency = 1.7f;

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (body, chain, points, locomotion, target, patrol) in SystemAPI.Query<RefRO<CreatureBody>, RefRO<VerletChain>, DynamicBuffer<VerletPoint>, RefRW<CreatureLocomotion>, RefRW<ChainTarget>, RefRW<LabCreaturePatrol>>())
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
                var span = math.max(0, points.Length - 1) * chain.ValueRO.RestLength;
                target.ValueRW.Position = body.ValueRO.RootPosition + new float2(
                    span * TailReachFraction,
                    math.sin(time * TailSwayFrequency) * span * TailSwayFraction);
            }
        }
    }
}
