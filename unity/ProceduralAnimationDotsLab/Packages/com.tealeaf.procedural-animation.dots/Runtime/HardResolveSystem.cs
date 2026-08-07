using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Burst;
using Unity.Entities;

namespace Tealeaf.ProceduralAnimation.Dots
{
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(ProceduralAnimationSolveSystemGroup))]
    [UpdateAfter(typeof(TwoBoneIkSystem))]
    internal partial struct HardResolveSystem : ISystem
    {
        const int ConstraintIterations = 2;

        // Contact planes are an optional feature, but the final constraint pass is not: a creature
        // authored without them still needs its chain repaired after the legs have moved.
        BufferLookup<ContactPlane> contactPlanes;

        public void OnCreate(ref SystemState state)
        {
            contactPlanes = state.GetBufferLookup<ContactPlane>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            contactPlanes.Update(ref state);

            foreach (var (chain, body, points, entity) in SystemAPI.Query<RefRO<VerletChain>, RefRO<CreatureBody>, DynamicBuffer<VerletPoint>>().WithEntityAccess())
            {
                if (points.Length < 2) continue;
                var mutablePoints = points;
                var hasContacts = contactPlanes.HasBuffer(entity);
                var root = VerletChainSolver.ResolveRoot(
                    body.ValueRO.RootPosition,
                    chain.ValueRO.Time,
                    chain.ValueRO.RootBobAmplitude,
                    chain.ValueRO.RootBobFrequency);
                for (var iteration = 0; iteration < ConstraintIterations; iteration++)
                {
                    var pinnedRoot = mutablePoints[0]; VerletChainSolver.Pin(ref pinnedRoot, root); mutablePoints[0] = pinnedRoot;
                    for (var index = 0; index < mutablePoints.Length - 1; index++)
                    {
                        var first = mutablePoints[index]; var second = mutablePoints[index + 1];
                        VerletChainSolver.SatisfyDistance(ref first, ref second, chain.ValueRO.RestLength);
                        if (index == 0) VerletChainSolver.Pin(ref first, root);
                        mutablePoints[index] = first; mutablePoints[index + 1] = second;
                    }

                    if (!hasContacts) continue;
                    var contacts = contactPlanes[entity];
                    for (var pointIndex = 1; pointIndex < mutablePoints.Length; pointIndex++)
                    {
                        var point = mutablePoints[pointIndex];
                        for (var contactIndex = 0; contactIndex < contacts.Length; contactIndex++) VerletContactSolver.ProjectAgainstPlane(ref point, contacts[contactIndex]);
                        mutablePoints[pointIndex] = point;
                    }
                }
            }
        }
    }
}
