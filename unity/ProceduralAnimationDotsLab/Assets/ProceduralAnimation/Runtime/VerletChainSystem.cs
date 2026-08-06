using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial struct VerletChainSystem : ISystem
    {
        const int ConstraintIterations = 8;

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (chain, target, points) in SystemAPI.Query<RefRW<VerletChain>, RefRW<ChainTarget>, DynamicBuffer<VerletPoint>>())
            {
                if (points.Length < 2)
                    continue;

                var mutablePoints = points;
                var simulation = chain.ValueRO;
                simulation.Time += deltaTime;

                var root = new float2(-3.5f, 0.5f + math.sin(simulation.Time * 0.9f) * 0.35f);
                var endpoint = new float2(3.0f, math.sin(simulation.Time * 1.7f) * 1.3f);
                target.ValueRW.Position = endpoint;

                for (var index = 1; index < mutablePoints.Length; index++)
                {
                    var point = mutablePoints[index];
                    var velocity = (point.Position - point.PreviousPosition) * simulation.Damping;
                    point.PreviousPosition = point.Position;
                    point.Position += velocity + new float2(0f, -3.5f) * (deltaTime * deltaTime);
                    mutablePoints[index] = point;
                }

                var tip = mutablePoints[mutablePoints.Length - 1];
                tip.Position = math.lerp(tip.Position, endpoint, simulation.MuscleStrength);
                mutablePoints[mutablePoints.Length - 1] = tip;

                for (var iteration = 0; iteration < ConstraintIterations; iteration++)
                {
                    var pinnedRoot = mutablePoints[0];
                    pinnedRoot.Position = root;
                    pinnedRoot.PreviousPosition = root;
                    mutablePoints[0] = pinnedRoot;

                    for (var index = 0; index < mutablePoints.Length - 1; index++)
                    {
                        var first = mutablePoints[index];
                        var second = mutablePoints[index + 1];
                        VerletChainSolver.SatisfyDistance(ref first, ref second, simulation.LinkLength);

                        if (index == 0)
                        {
                            first.Position = root;
                            first.PreviousPosition = root;
                        }

                        mutablePoints[index] = first;
                        mutablePoints[index + 1] = second;
                    }
                }

                chain.ValueRW = simulation;
            }
        }
    }
}
