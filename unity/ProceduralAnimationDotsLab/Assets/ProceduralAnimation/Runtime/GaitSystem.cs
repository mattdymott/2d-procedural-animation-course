using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(VerletChainSystem))]
    [UpdateBefore(typeof(TwoBoneIkSystem))]
    public partial struct GaitSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (settings, gaitLegs, limbs, points) in SystemAPI.Query<RefRO<GaitSettings>, DynamicBuffer<GaitLeg>, DynamicBuffer<Limb2BoneLeg>, DynamicBuffer<VerletPoint>>())
            {
                var mutableGaitLegs = gaitLegs;
                var mutableLimbs = limbs;
                var legCount = math.min(mutableGaitLegs.Length, mutableLimbs.Length);
                for (var index = 0; index < legCount; index++)
                {
                    var gaitLeg = mutableGaitLegs[index];
                    var limbLeg = mutableLimbs[index];
                    if (limbLeg.RootPointIndex < 0 || limbLeg.RootPointIndex >= points.Length)
                        continue;

                    var limb = limbLeg.Limb;
                    var hipPoint = points[limbLeg.RootPointIndex];
                    var hip = hipPoint.Position;
                    var bodyVelocity = deltaTime > 0f
                        ? (hipPoint.Position - hipPoint.PreviousPosition) / deltaTime
                        : float2.zero;
                    var partnerState = GetPartnerState(mutableGaitLegs, gaitLeg.PartnerIndex);
                    limb.Target = GaitStepper.Update(
                        ref gaitLeg,
                        partnerState,
                        hip,
                        bodyVelocity,
                        limb.LengthA + limb.LengthB,
                        settings.ValueRO,
                        deltaTime);
                    mutableGaitLegs[index] = gaitLeg;
                    limbLeg.Limb = limb;
                    mutableLimbs[index] = limbLeg;
                }
            }
        }

        static FootState GetPartnerState(DynamicBuffer<GaitLeg> legs, sbyte partnerIndex)
        {
            return partnerIndex >= 0 && partnerIndex < legs.Length
                ? legs[partnerIndex].State
                : FootState.Swinging;
        }
    }
}
