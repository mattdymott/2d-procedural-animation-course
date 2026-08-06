using Unity.Entities;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(VerletChainSystem))]
    public partial struct TwoBoneIkSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (limbs, points) in SystemAPI.Query<DynamicBuffer<Limb2BoneLeg>, DynamicBuffer<VerletPoint>>())
            {
                var mutableLimbs = limbs;
                for (var index = 0; index < mutableLimbs.Length; index++)
                {
                    var limbLeg = mutableLimbs[index];
                    if (limbLeg.RootPointIndex < 0 || limbLeg.RootPointIndex >= points.Length)
                        continue;

                    var solvedLimb = limbLeg.Limb;
                    solvedLimb.Root = points[limbLeg.RootPointIndex].Position;
                    TwoBoneIkSolver.Solve(ref solvedLimb);
                    limbLeg.Limb = solvedLimb;
                    mutableLimbs[index] = limbLeg;
                }
            }
        }
    }
}
