using Tealeaf.ProceduralAnimation.Dots.LowLevel;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// The gait stage. It runs in four passes so the decisions stay in the order the rules need:
    /// resolve where every foot currently is, decide who may step, let exactly those legs commit
    /// one target each, then publish the targets IK will chase.
    ///
    /// It reads body pose and world facts, and writes only foot promises: state, plant, swing
    /// target, phase, and the support relation a plant was committed against.
    /// </summary>
    [BurstCompile]
    [DisableAutoCreation]
    [UpdateInGroup(typeof(ProceduralAnimationSolveSystemGroup))]
    [UpdateAfter(typeof(VerletChainSystem))]
    [UpdateBefore(typeof(TwoBoneIkSystem))]
    internal partial struct GaitSystem : ISystem
    {
        /// <summary>Per-leg working data for one tick. Never persists.</summary>
        struct LegFrame
        {
            public float2 Hip;
            public float2 Home;
            public float2 BodyVelocity;
            public float MinimumReach;
            public float MaximumReach;
            public byte Valid;
        }

        ComponentLookup<SupportPose> supportPoses;
        ComponentLookup<SupportKinematics> supportKinematics;
        ComponentLookup<PlanarHeading> planarHeadings;
        ComponentLookup<GaitSupportPolicy> supportPolicies;
        ComponentLookup<GaitCadenceState> cadenceStates;
        ComponentLookup<WaveGaitState> waveStates;
        ComponentLookup<GaitRecoveryRequest> recoveryRequests;
        ComponentLookup<CreatureLocomotion> locomotions;
        BufferLookup<WaveOrder> waveOrders;

        public void OnCreate(ref SystemState state)
        {
            supportPoses = state.GetComponentLookup<SupportPose>(true);
            supportKinematics = state.GetComponentLookup<SupportKinematics>(true);
            planarHeadings = state.GetComponentLookup<PlanarHeading>(true);
            supportPolicies = state.GetComponentLookup<GaitSupportPolicy>(true);
            cadenceStates = state.GetComponentLookup<GaitCadenceState>(false);
            waveStates = state.GetComponentLookup<WaveGaitState>(false);
            recoveryRequests = state.GetComponentLookup<GaitRecoveryRequest>(false);
            locomotions = state.GetComponentLookup<CreatureLocomotion>(true);
            waveOrders = state.GetBufferLookup<WaveOrder>(true);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            supportPoses.Update(ref state);
            supportKinematics.Update(ref state);
            planarHeadings.Update(ref state);
            supportPolicies.Update(ref state);
            cadenceStates.Update(ref state);
            waveStates.Update(ref state);
            recoveryRequests.Update(ref state);
            locomotions.Update(ref state);
            waveOrders.Update(ref state);

            foreach (var (gait, body, gaitLegs, limbs, points, footholdCandidates, entity) in SystemAPI.Query<RefRO<Gait>, RefRW<CreatureBody>, DynamicBuffer<GaitLeg>, DynamicBuffer<Limb2BoneLeg>, DynamicBuffer<VerletPoint>, DynamicBuffer<FootholdCandidate>>().WithEntityAccess())
            {
                var mutableLimbs = limbs;
                var legCount = math.min(math.min(gaitLegs.Length, mutableLimbs.Length), GaitPermission.MaximumLegs);
                if (legCount <= 0)
                    continue;

                var legs = gaitLegs.AsNativeArray();
                var frames = new NativeArray<LegFrame>(legCount, Allocator.Temp);
                var urgency = new NativeArray<float>(legCount, Allocator.Temp);

                var planar = planarHeadings.HasComponent(entity);
                var forward = planar ? planarHeadings[entity].LastForward : new float2(1f, 0f);

                var landed = ResolveFeet(
                    legs, mutableLimbs, points, frames, urgency,
                    legCount, planar, forward, gait.ValueRO, deltaTime);

                var cadence = ResolveCadence(entity, legs, legCount);
                var minimumPlantedFeet = supportPolicies.HasComponent(entity)
                    ? supportPolicies[entity].MinimumPlantedFeet
                    : 0;
                var cursorLegIndex = ResolveWaveCursor(entity, landed, legCount);

                var permitted = GaitPermission.Permitted(
                    legs, urgency, gait.ValueRO.Comfort, cadence, minimumPlantedFeet, cursorLegIndex);

                Commit(
                    entity, legs, frames, footholdCandidates, ref body.ValueRW,
                    legCount, permitted, planar, forward, gait.ValueRO);

                PublishTargets(legs, mutableLimbs, frames, legCount, planar, gait.ValueRO);

                frames.Dispose();
                urgency.Dispose();
            }
        }

        /// <summary>
        /// Pass one. Re-evaluates support-relative plants, advances swings, and measures how far
        /// each planted foot has drifted from its home. Returns the legs that landed this tick;
        /// they are excluded from stepping again immediately.
        /// </summary>
        uint ResolveFeet(
            NativeArray<GaitLeg> legs,
            DynamicBuffer<Limb2BoneLeg> limbs,
            DynamicBuffer<VerletPoint> points,
            NativeArray<LegFrame> frames,
            NativeArray<float> urgency,
            int legCount,
            bool planar,
            float2 forward,
            in Gait gait,
            float deltaTime)
        {
            var landed = 0u;
            for (var index = 0; index < legCount; index++)
            {
                urgency[index] = -1f;
                var limbLeg = limbs[index];
                if (limbLeg.RootPointIndex < 0 || limbLeg.RootPointIndex >= points.Length)
                    continue;

                var leg = legs[index];
                var hipPoint = points[limbLeg.RootPointIndex];
                var hip = hipPoint.Position;

                // A planted foot on a moving support keeps travelling with it — including the
                // material running through a conveyor — without ever re-querying the world.
                if (leg.State == FootState.Planted
                    && leg.Support != Entity.Null
                    && supportPoses.HasComponent(leg.Support)
                    && supportKinematics.HasComponent(leg.Support))
                    leg.SurfaceOffset += supportKinematics[leg.Support].SurfaceVelocityLocal * deltaTime;

                if (leg.State == FootState.Swinging && GaitStepper.AdvanceSwing(ref leg, gait, deltaTime))
                    landed |= 1u << index;

                if (leg.State == FootState.Planted
                    && leg.Support != Entity.Null
                    && supportPoses.HasComponent(leg.Support))
                    leg.Plant = SupportMath.TransformPoint(supportPoses[leg.Support], leg.LocalPlant + leg.SurfaceOffset);

                var home = planar
                    ? PlanarMath.Home(hip, leg.HomeOffset, forward)
                    : hip + leg.HomeOffset;

                legs[index] = leg;
                frames[index] = new LegFrame
                {
                    Hip = hip,
                    Home = home,
                    BodyVelocity = deltaTime > 0f
                        ? (hipPoint.Position - hipPoint.PreviousPosition) / deltaTime
                        : float2.zero,
                    MinimumReach = math.abs(limbLeg.Limb.LengthA - limbLeg.Limb.LengthB),
                    MaximumReach = limbLeg.Limb.LengthA + limbLeg.Limb.LengthB,
                    Valid = 1,
                };

                if (leg.State == FootState.Planted && (landed & (1u << index)) == 0u)
                    urgency[index] = math.distance(leg.Plant, home);
            }

            return landed;
        }

        /// <summary>
        /// Pass two, first half. Intent may request a different cadence, but the change is applied
        /// only at a synchronisation point: no leg in the air. Plants are never reseeded.
        /// </summary>
        GaitCadence ResolveCadence(Entity entity, NativeArray<GaitLeg> legs, int legCount)
        {
            if (!cadenceStates.HasComponent(entity))
                return GaitCadence.Partner;

            var cadence = cadenceStates[entity];
            if (supportPolicies.HasComponent(entity) && locomotions.HasComponent(entity))
            {
                var policy = supportPolicies[entity];
                var speed = math.length(locomotions[entity].DesiredVelocity);
                var requested = cadence.Active;
                if (speed >= policy.EnterSpeed)
                    requested = policy.FastCadence;
                else if (speed <= policy.ExitSpeed)
                    requested = policy.SlowCadence;

                cadence.Pending = requested;
            }

            if (cadence.Pending != cadence.Active && NoLegIsSwinging(legs, legCount))
                cadence.Active = cadence.Pending;

            cadenceStates[entity] = cadence;
            return cadence.Active;
        }

        static bool NoLegIsSwinging(NativeArray<GaitLeg> legs, int legCount)
        {
            for (var index = 0; index < legCount; index++)
            {
                if (legs[index].State == FootState.Swinging)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Pass two, second half. The cursor advances on a landing and on nothing else — a leg
        /// that finds nowhere to step holds its turn rather than passing it on.
        /// </summary>
        int ResolveWaveCursor(Entity entity, uint landed, int legCount)
        {
            if (!waveStates.HasComponent(entity) || !waveOrders.HasBuffer(entity))
                return -1;

            var order = waveOrders[entity];
            if (order.Length == 0)
                return -1;

            var wave = waveStates[entity];
            if (wave.Cursor >= order.Length)
                wave.Cursor = 0;

            var currentLeg = order[wave.Cursor].LegIndex;
            if (currentLeg < legCount && (landed & (1u << currentLeg)) != 0u)
            {
                wave.Cursor = (byte)((wave.Cursor + 1) % order.Length);
                currentLeg = order[wave.Cursor].LegIndex;
            }

            waveStates[entity] = wave;
            return currentLeg < legCount ? currentLeg : -1;
        }

        /// <summary>
        /// Pass three. Each permitted leg filters its own candidates and commits at most one
        /// target. A leg with no legal option keeps its plant and asks locomotion for help.
        /// </summary>
        void Commit(
            Entity entity,
            NativeArray<GaitLeg> legs,
            NativeArray<LegFrame> frames,
            DynamicBuffer<FootholdCandidate> candidates,
            ref CreatureBody body,
            int legCount,
            uint permitted,
            bool planar,
            float2 forward,
            in Gait gait)
        {
            var blockedLegIndex = -1;
            var preferredTurn = forward;

            for (var index = 0; index < legCount; index++)
            {
                if ((permitted & (1u << index)) == 0u)
                    continue;

                var frame = frames[index];
                if (frame.Valid == 0)
                    continue;

                var leg = legs[index];
                if (!TrySelectCandidate(candidates, (byte)index, frame, planar, gait, out var candidate, out var foothold))
                {
                    if (blockedLegIndex < 0)
                    {
                        blockedLegIndex = index;
                        preferredTurn = PreferredTurn(leg, forward);
                    }

                    continue;
                }

                // Velocity at the contact point, captured before the relation is cleared: a foot
                // leaving a moving support takes that motion with it.
                var liftoffLocalPoint = leg.LocalPlant + leg.SurfaceOffset;
                var liftoffSupport = leg.Support;

                GaitStepper.BeginSwing(ref leg, foothold, candidate.Support, candidate.SupportLocalPoint);

                if (liftoffSupport != Entity.Null
                    && supportPoses.HasComponent(liftoffSupport)
                    && supportKinematics.HasComponent(liftoffSupport))
                {
                    var liftoffVelocity = SupportMath.PointVelocity(
                        supportPoses[liftoffSupport],
                        supportKinematics[liftoffSupport],
                        liftoffLocalPoint);
                    leg.CarryVelocity = liftoffVelocity;
                    body.CarryVelocity += liftoffVelocity;
                }

                legs[index] = leg;
            }

            if (recoveryRequests.HasComponent(entity))
            {
                recoveryRequests[entity] = new GaitRecoveryRequest
                {
                    State = blockedLegIndex < 0 ? GaitRecovery.None : GaitRecovery.HoldingForFoothold,
                    SlowDown = (byte)(blockedLegIndex < 0 ? 0 : 1),
                    PreferredTurn = preferredTurn,
                    BlockedLegIndex = (byte)(blockedLegIndex < 0 ? 255 : blockedLegIndex),
                };
            }
        }

        /// <summary>
        /// A heading that turns away from the side that ran out of ground, so the blocked leg's
        /// home swings back over space the creature can stand on. It is a request, not a command:
        /// locomotion decides what to do with it.
        /// </summary>
        /// <remarks>
        /// Turning to relieve the blocked leg's <em>stress</em> instead is a trap — the shortest
        /// way to close the gap between a plant and its home routinely points further into
        /// whatever blocked the leg in the first place.
        /// </remarks>
        static float2 PreferredTurn(in GaitLeg leg, float2 forward)
        {
            var awayFromBlockedSide = leg.HomeOffset.y >= 0f ? -1f : 1f;
            return math.normalizesafe(
                forward + PlanarMath.Perpendicular(forward) * awayFromBlockedSide * 0.5f,
                forward);
        }

        /// <summary>
        /// Filters this leg's candidates and returns the best legal one — nearest to where the
        /// step was heading. Selection happens here, once, at the transition into a swing.
        /// </summary>
        static bool TrySelectCandidate(
            DynamicBuffer<FootholdCandidate> candidates,
            byte legIndex,
            in LegFrame frame,
            bool planar,
            in Gait gait,
            out FootholdCandidate chosen,
            out float2 foothold)
        {
            chosen = default;
            foothold = float2.zero;
            var predictedHome = frame.Home + frame.BodyVelocity * gait.StepLead;
            var bestDistance = float.PositiveInfinity;
            var found = false;

            for (var index = 0; index < candidates.Length; index++)
            {
                var candidate = candidates[index];
                if (candidate.LegIndex != legIndex)
                    continue;

                float2 point;
                var accepted = planar
                    ? GaitStepper.TryChoosePlanarFoothold(
                        candidate, frame.Hip, frame.Home, frame.BodyVelocity,
                        frame.MinimumReach, frame.MaximumReach, gait, out point)
                    : GaitStepper.TryChooseFoothold(
                        candidate, frame.Hip, frame.Home, frame.BodyVelocity,
                        frame.MinimumReach, frame.MaximumReach, gait, out point);
                if (!accepted)
                    continue;

                var distance = math.distancesq(point, predictedHome);
                if (found && distance >= bestDistance)
                    continue;

                bestDistance = distance;
                chosen = candidate;
                foothold = point;
                found = true;

                // Side-view creatures were served one candidate per leg and committed to it;
                // ranking is a top-down affordance, so keep the first-hit behaviour there.
                if (!planar)
                    break;
            }

            return found;
        }

        /// <summary>
        /// Pass four. Publishes one target per limb. A top-down swing stops at the movement plane:
        /// its lift belongs to presentation, which derives it from this same point.
        /// </summary>
        void PublishTargets(
            NativeArray<GaitLeg> legs,
            DynamicBuffer<Limb2BoneLeg> limbs,
            NativeArray<LegFrame> frames,
            int legCount,
            bool planar,
            in Gait gait)
        {
            for (var index = 0; index < legCount; index++)
            {
                if (frames[index].Valid == 0)
                    continue;

                var leg = legs[index];
                float2 target;
                if (leg.State == FootState.Planted)
                {
                    if (leg.Support != Entity.Null && supportPoses.HasComponent(leg.Support))
                        leg.Plant = SupportMath.TransformPoint(
                            supportPoses[leg.Support], leg.LocalPlant + leg.SurfaceOffset);

                    target = leg.Plant;
                }
                else
                {
                    target = planar
                        ? GaitStepper.EvaluatePlanarSwingTarget(leg)
                        : GaitStepper.EvaluateSwingTarget(leg, gait);
                }

                legs[index] = leg;
                var limbLeg = limbs[index];
                limbLeg.Limb.Target = target;
                limbs[index] = limbLeg;
            }
        }
    }
}
