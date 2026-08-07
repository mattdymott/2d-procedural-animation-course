using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Burst;
using Unity.Entities;

namespace Tealeaf.ProceduralAnimation.Dots
{
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(ProceduralAnimationSolveSystemGroup))]
    [UpdateAfter(typeof(VerletChainSystem))]
    internal partial struct TwoBoneIkSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (limbs, points) in SystemAPI.Query<DynamicBuffer<Limb2BoneLeg>, DynamicBuffer<VerletPoint>>())
            {
                var mutableLimbs = limbs;
                for (var index = 0; index < mutableLimbs.Length; index++)
                {
                    var limbLeg = mutableLimbs[index];
                    if (limbLeg.RootPointIndex < 0 || limbLeg.RootPointIndex >= points.Length) continue;
                    var limb = limbLeg.Limb;
                    limb.Root = points[limbLeg.RootPointIndex].Position;
                    var pose = TwoBoneIk.Solve(new TwoBoneIkRequest { Root = limb.Root, Target = limb.Target, LengthA = limb.LengthA, LengthB = limb.LengthB, BendSign = limb.BendSign });
                    limb.Knee = pose.Knee;
                    limb.Foot = pose.Foot;
                    limbLeg.Limb = limb;
                    mutableLimbs[index] = limbLeg;
                }
            }
        }
    }
}
