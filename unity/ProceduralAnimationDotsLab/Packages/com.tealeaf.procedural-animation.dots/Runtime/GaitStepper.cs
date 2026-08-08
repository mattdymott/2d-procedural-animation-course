using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// The commitment half of the gait decision: which candidate a permitted foot accepts, how a
    /// swing advances, and where the foot is while it does. Who is <em>allowed</em> to ask is
    /// <see cref="GaitPermission"/>'s job. Implementation detail behind <see cref="Gait"/>.
    /// </summary>
    internal static class GaitStepper
    {
        // Keep authored zero-duration values finite while the edit-time data is being assembled.
        const float MinimumDuration = 0.0001f;

        /// <summary>
        /// Side-view acceptance: a foothold has to face upward enough to hold a foot, sit inside
        /// the limb's reachable annulus, and lie forward of home along the body's travel.
        /// </summary>
        public static bool TryChooseFoothold(
            in FootholdCandidate candidate,
            float2 hip,
            float2 home,
            float2 bodyVelocity,
            float minimumReach,
            float maximumReach,
            in Gait gait,
            out float2 foothold)
        {
            foothold = float2.zero;
            var normal = math.normalizesafe(candidate.Normal, new float2(0f, 1f));
            if (math.dot(normal, new float2(0f, 1f)) < gait.MinimumSupport)
                return false;

            if (!WithinReach(candidate.Point, hip, minimumReach, maximumReach))
                return false;

            if (!MatchesMovementPolicy(candidate.Point, home, bodyVelocity, gait))
                return false;

            foothold = candidate.Point;
            return true;
        }

        /// <summary>
        /// Top-down acceptance. A planar obstacle is not a bad floor normal — it is a reason the
        /// point is not a legal target — so the support-normal test is replaced by the two facts a
        /// planar query reports, and reach and movement policy carry over unchanged.
        /// </summary>
        public static bool TryChoosePlanarFoothold(
            in FootholdCandidate candidate,
            float2 hip,
            float2 home,
            float2 bodyVelocity,
            float minimumReach,
            float maximumReach,
            in Gait gait,
            out float2 foothold)
        {
            foothold = float2.zero;
            if (candidate.Walkable == 0)
                return false;

            if (candidate.PathClear == 0)
                return false;

            if (!WithinReach(candidate.Point, hip, minimumReach, maximumReach))
                return false;

            if (!MatchesMovementPolicy(candidate.Point, home, bodyVelocity, gait))
                return false;

            foothold = candidate.Point;
            return true;
        }

        static bool WithinReach(float2 point, float2 hip, float minimumReach, float maximumReach)
        {
            var distance = math.length(point - hip);
            return distance >= minimumReach && distance <= maximumReach;
        }

        static bool MatchesMovementPolicy(float2 point, float2 home, float2 bodyVelocity, in Gait gait)
        {
            if (math.lengthsq(bodyVelocity) <= 0.000001f)
                return true;

            return math.dot(point - home, math.normalize(bodyVelocity)) >= gait.MinimumForward;
        }

        /// <summary>
        /// Commits one target. Everything the old plant knew about its support is dropped here:
        /// the foot is in the air, and the relation it will inherit belongs to the new candidate.
        /// </summary>
        public static void BeginSwing(ref GaitLeg leg, float2 foothold, Entity support, float2 supportLocalPoint)
        {
            leg.SwingFrom = leg.Plant;
            leg.SwingTo = foothold;
            leg.SwingSupport = support;
            leg.SwingLocalPlant = supportLocalPoint;
            leg.Support = Entity.Null;
            leg.LocalPlant = float2.zero;
            leg.SurfaceOffset = float2.zero;
            leg.CarryVelocity = float2.zero;
            leg.SwingT = 0f;
            leg.State = FootState.Swinging;
        }

        /// <summary>
        /// Advances a swing and lands it when the phase completes. Returns true on the tick the
        /// foot becomes a plant again — the moment a wave cursor is allowed to move on.
        /// </summary>
        public static bool AdvanceSwing(ref GaitLeg leg, in Gait gait, float deltaTime)
        {
            leg.SwingT += deltaTime / math.max(gait.StepDuration, MinimumDuration);
            if (leg.SwingT < 1f)
                return false;

            leg.SwingT = 1f;
            leg.Plant = leg.SwingTo;
            leg.Support = leg.SwingSupport;
            leg.LocalPlant = leg.SwingLocalPlant;
            leg.SurfaceOffset = float2.zero;
            leg.CarryVelocity = float2.zero;
            leg.SwingSupport = Entity.Null;
            leg.SwingLocalPlant = float2.zero;
            leg.State = FootState.Planted;
            return true;
        }

        /// <summary>
        /// Where a swinging foot is on the movement plane: the committed segment, eased. A
        /// top-down creature stops here — its lift is a picture drawn later from this point.
        /// </summary>
        public static float2 EvaluatePlanarSwingTarget(in GaitLeg leg)
        {
            var smoothT = math.smoothstep(0f, 1f, math.saturate(leg.SwingT));
            return math.lerp(leg.SwingFrom, leg.SwingTo, smoothT);
        }

        /// <summary>
        /// The side-view swing target: the planar segment plus the parabolic arc that lifts a foot
        /// over the ground it is stepping across. Here the arc really is world geometry.
        /// </summary>
        public static float2 EvaluateSwingTarget(in GaitLeg leg, in Gait gait)
        {
            var swingT = math.saturate(leg.SwingT);
            var target = EvaluatePlanarSwingTarget(leg);
            target.y += gait.StepHeight * 4f * swingT * (1f - swingT);
            return target;
        }

        /// <summary>The unqueried side-view target: home, led by body velocity, clamped to reach.</summary>
        public static float2 LedTarget(float2 home, float2 hip, float2 bodyVelocity, float maximumReach, in Gait gait) =>
            ClampToReach(home + bodyVelocity * gait.StepLead, hip, maximumReach);

        // ------------------------------------------------------------------------------------
        // Single-leg entry points. They wrap the pieces above with the original partner guard so
        // a caller holding one leg — a test, or a consumer driving a single limb — still has the
        // whole rule in one call.
        // ------------------------------------------------------------------------------------

        public static float2 Update(
            ref GaitLeg leg,
            FootState partnerState,
            float2 hip,
            float2 bodyVelocity,
            float maximumReach,
            in Gait gait,
            float deltaTime)
        {
            return UpdateInternal(
                ref leg,
                partnerState,
                hip,
                bodyVelocity,
                minimumReach: 0f,
                maximumReach,
                gait,
                deltaTime,
                default,
                useFootholdQuery: false,
                hasFootholdCandidate: false);
        }

        public static float2 Update(
            ref GaitLeg leg,
            FootState partnerState,
            float2 hip,
            float2 bodyVelocity,
            float minimumReach,
            float maximumReach,
            in Gait gait,
            float deltaTime,
            bool hasFootholdCandidate,
            in FootholdCandidate footholdCandidate)
        {
            return UpdateInternal(
                ref leg,
                partnerState,
                hip,
                bodyVelocity,
                minimumReach,
                maximumReach,
                gait,
                deltaTime,
                footholdCandidate,
                useFootholdQuery: true,
                hasFootholdCandidate: hasFootholdCandidate);
        }

        static float2 UpdateInternal(
            ref GaitLeg leg,
            FootState partnerState,
            float2 hip,
            float2 bodyVelocity,
            float minimumReach,
            float maximumReach,
            in Gait gait,
            float deltaTime,
            in FootholdCandidate footholdCandidate,
            bool useFootholdQuery,
            bool hasFootholdCandidate)
        {
            if (leg.State == FootState.Planted)
            {
                var home = hip + leg.HomeOffset;
                if (math.distance(leg.Plant, home) <= gait.Comfort || partnerState != FootState.Planted)
                    return leg.Plant;

                var foothold = LedTarget(home, hip, bodyVelocity, maximumReach, gait);
                var support = Entity.Null;
                var supportLocalPoint = float2.zero;
                if (useFootholdQuery)
                {
                    if (!hasFootholdCandidate
                        || !TryChooseFoothold(
                            footholdCandidate,
                            hip,
                            home,
                            bodyVelocity,
                            minimumReach,
                            maximumReach,
                            gait,
                            out foothold))
                        return leg.Plant;

                    support = footholdCandidate.Support;
                    supportLocalPoint = footholdCandidate.SupportLocalPoint;
                }

                var previousPlant = leg.Plant;
                BeginSwing(ref leg, foothold, support, supportLocalPoint);
                return previousPlant;
            }

            return AdvanceSwing(ref leg, gait, deltaTime)
                ? leg.Plant
                : EvaluateSwingTarget(leg, gait);
        }

        static float2 ClampToReach(float2 target, float2 hip, float maximumReach)
        {
            var offset = target - hip;
            var distance = math.length(offset);
            if (distance <= maximumReach)
                return target;

            return hip + math.normalizesafe(offset, new float2(1f, 0f)) * maximumReach;
        }
    }
}
