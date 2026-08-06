using Unity.Entities;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(VerletChainSystem))]
    public partial struct TwoBoneIkSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (limb, points) in SystemAPI.Query<RefRW<Limb2Bone>, DynamicBuffer<VerletPoint>>())
            {
                if (points.Length == 0)
                    continue;

                var solvedLimb = limb.ValueRO;
                solvedLimb.Root = points[points.Length / 2].Position;
                TwoBoneIkSolver.Solve(ref solvedLimb);
                limb.ValueRW = solvedLimb;
            }
        }
    }
}
