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
            var supportPoses = state.GetComponentLookup<SupportPose>(true);
            var supportMotions = state.GetComponentLookup<SupportMotion>(true);

            foreach (var (settings, body, gaitLegs, limbs, points, groundHits) in SystemAPI.Query<RefRO<GaitSettings>, RefRW<CreatureBody>, DynamicBuffer<GaitLeg>, DynamicBuffer<Limb2BoneLeg>, DynamicBuffer<VerletPoint>, DynamicBuffer<GroundHit>>())
            {
                var mutableGaitLegs = gaitLegs;
                var mutableLimbs = limbs;
                var carryVelocity = body.ValueRO.CarryVelocity;
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
                    var wasPlanted = gaitLeg.State == FootState.Planted;
                    var liftoffSupport = gaitLeg.Support;
                    if (gaitLeg.State == FootState.Planted
                        && gaitLeg.Support != Entity.Null
                        && supportPoses.HasComponent(gaitLeg.Support))
                    {
                        if (supportMotions.HasComponent(gaitLeg.Support))
                            gaitLeg.SurfaceOffset += supportMotions[gaitLeg.Support].BeltVelocityLocal * deltaTime;
                        gaitLeg.Plant = SupportPoseMath.TransformPoint(supportPoses[gaitLeg.Support], gaitLeg.LocalPlant + gaitLeg.SurfaceOffset);
                    }
                    var liftoffLocalPoint = gaitLeg.LocalPlant + gaitLeg.SurfaceOffset;

                    var bodyVelocity = deltaTime > 0f
                        ? (hipPoint.Position - hipPoint.PreviousPosition) / deltaTime
                        : float2.zero;
                    var partnerState = GetPartnerState(mutableGaitLegs, gaitLeg.PartnerIndex);
                    var groundHit = GetGroundHit(groundHits, (byte)index);
                    var target = GaitStepper.Update(
                        ref gaitLeg,
                        partnerState,
                        hip,
                        bodyVelocity,
                        math.abs(limb.LengthA - limb.LengthB),
                        limb.LengthA + limb.LengthB,
                        settings.ValueRO,
                        deltaTime,
                        groundHit);
                    if (wasPlanted
                        && gaitLeg.State == FootState.Swinging
                        && liftoffSupport != Entity.Null
                        && supportPoses.HasComponent(liftoffSupport)
                        && supportMotions.HasComponent(liftoffSupport))
                    {
                        var liftoffVelocity = SupportVelocityMath.PointVelocity(
                            supportPoses[liftoffSupport],
                            supportMotions[liftoffSupport],
                            liftoffLocalPoint);
                        gaitLeg.CarryVelocity = liftoffVelocity;
                        carryVelocity += liftoffVelocity;
                    }
                    if (gaitLeg.State == FootState.Planted
                        && gaitLeg.Support != Entity.Null
                        && supportPoses.HasComponent(gaitLeg.Support))
                    {
                        gaitLeg.Plant = SupportPoseMath.TransformPoint(supportPoses[gaitLeg.Support], gaitLeg.LocalPlant + gaitLeg.SurfaceOffset);
                        target = gaitLeg.Plant;
                    }

                    limb.Target = target;
                    mutableGaitLegs[index] = gaitLeg;
                    limbLeg.Limb = limb;
                    mutableLimbs[index] = limbLeg;
                }

                body.ValueRW.CarryVelocity = carryVelocity;
            }
        }

        static FootState GetPartnerState(DynamicBuffer<GaitLeg> legs, sbyte partnerIndex)
        {
            return partnerIndex >= 0 && partnerIndex < legs.Length
                ? legs[partnerIndex].State
                : FootState.Swinging;
        }

        static GroundHit GetGroundHit(DynamicBuffer<GroundHit> hits, byte legIndex)
        {
            for (var index = 0; index < hits.Length; index++)
            {
                if (hits[index].LegIndex == legIndex)
                    return hits[index];
            }

            return default;
        }
    }
}
