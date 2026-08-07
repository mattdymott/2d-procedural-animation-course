using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(ProceduralAnimationSolveSystemGroup))]
    internal partial struct VerletChainSystem : ISystem
    {
        const int ConstraintIterations = 8;

        // Muscles are optional, so the target must not be part of the query — a chain composed
        // without them still has to integrate.
        ComponentLookup<ChainTarget> chainTargets;

        public void OnCreate(ref SystemState state)
        {
            chainTargets = state.GetComponentLookup<ChainTarget>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            chainTargets.Update(ref state);

            foreach (var (chain, body, points, entity) in SystemAPI.Query<RefRW<VerletChain>, RefRO<CreatureBody>, DynamicBuffer<VerletPoint>>().WithEntityAccess())
            {
                if (points.Length < 2) continue;
                var mutablePoints = points;
                var simulation = chain.ValueRO;
                simulation.Time += deltaTime;
                var root = VerletChainSolver.ResolveRoot(
                    body.ValueRO.RootPosition,
                    simulation.Time,
                    simulation.RootBobAmplitude,
                    simulation.RootBobFrequency);

                for (var index = 1; index < mutablePoints.Length; index++)
                {
                    var point = mutablePoints[index];
                    var velocity = (point.Position - point.PreviousPosition) * simulation.Damping;
                    point.PreviousPosition = point.Position;
                    point.Position += velocity + simulation.Gravity * (deltaTime * deltaTime);
                    mutablePoints[index] = point;
                }

                // Nothing pulls the tip of a chain that was composed without muscles, which is
                // what keeps a plain rope from being dragged toward a stale target.
                if (chainTargets.HasComponent(entity))
                {
                    var muscle = chainTargets[entity];
                    var tip = mutablePoints[mutablePoints.Length - 1];
                    tip.Position = math.lerp(tip.Position, muscle.Position, math.saturate(muscle.Strength));
                    mutablePoints[mutablePoints.Length - 1] = tip;
                }

                for (var iteration = 0; iteration < ConstraintIterations; iteration++)
                {
                    var pinned = mutablePoints[0];
                    VerletChainSolver.Pin(ref pinned, root);
                    mutablePoints[0] = pinned;
                    for (var index = 0; index < mutablePoints.Length - 1; index++)
                    {
                        var first = mutablePoints[index];
                        var second = mutablePoints[index + 1];
                        VerletChainSolver.SatisfyDistance(ref first, ref second, simulation.RestLength);
                        if (index == 0) VerletChainSolver.Pin(ref first, root);
                        mutablePoints[index] = first;
                        mutablePoints[index + 1] = second;
                    }
                }

                chain.ValueRW = simulation;
            }
        }
    }
}
