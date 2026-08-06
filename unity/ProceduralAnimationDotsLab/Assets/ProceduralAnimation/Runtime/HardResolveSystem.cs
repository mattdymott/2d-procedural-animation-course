using Unity.Entities;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(TwoBoneIkSystem))]
    public partial struct HardResolveSystem : ISystem
    {
        const int ConstraintIterations = 2;

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (chain, body, points, contacts) in SystemAPI.Query<RefRO<VerletChain>, RefRO<CreatureBody>, DynamicBuffer<VerletPoint>, DynamicBuffer<ContactPlane>>())
            {
                if (points.Length < 2)
                    continue;

                var mutablePoints = points;
                var root = VerletChainSolver.ResolveRoot(body.ValueRO.RootPosition, chain.ValueRO.Time);
                for (var iteration = 0; iteration < ConstraintIterations; iteration++)
                {
                    var pinnedRoot = mutablePoints[0];
                    VerletChainSolver.Pin(ref pinnedRoot, root);
                    mutablePoints[0] = pinnedRoot;

                    for (var index = 0; index < mutablePoints.Length - 1; index++)
                    {
                        var first = mutablePoints[index];
                        var second = mutablePoints[index + 1];
                        VerletChainSolver.SatisfyDistance(ref first, ref second, chain.ValueRO.LinkLength);
                        if (index == 0)
                            VerletChainSolver.Pin(ref first, root);

                        mutablePoints[index] = first;
                        mutablePoints[index + 1] = second;
                    }

                    for (var pointIndex = 1; pointIndex < mutablePoints.Length; pointIndex++)
                    {
                        var point = mutablePoints[pointIndex];
                        for (var contactIndex = 0; contactIndex < contacts.Length; contactIndex++)
                            VerletContactSolver.ProjectAgainstPlane(ref point, contacts[contactIndex]);

                        mutablePoints[pointIndex] = point;
                    }
                }
            }
        }
    }
}
