using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// The gait decision policy: when a planted foot may leave the ground, which candidate it
    /// accepts, and how a swing advances. Implementation detail behind <see cref="GaitSettings"/>.
    /// </summary>
    internal static class GaitStepper
    {
        // Keep authored zero-duration settings finite while the edit-time data is being assembled.
        const float MinimumDuration = 0.0001f;

        public static bool TryChooseFoothold(
            in FootholdCandidate candidate,
            float2 hip,
            float2 home,
            float2 bodyVelocity,
            float minimumReach,
            float maximumReach,
            in GaitSettings settings,
            out float2 foothold)
        {
            foothold = float2.zero;
            var normal = math.normalizesafe(candidate.Normal, new float2(0f, 1f));
            if (math.dot(normal, new float2(0f, 1f)) < settings.MinimumSupport)
                return false;

            var offset = candidate.Point - hip;
            var distance = math.length(offset);
            if (distance < minimumReach || distance > maximumReach)
                return false;

            if (math.lengthsq(bodyVelocity) > 0.000001f)
            {
                var forward = math.dot(candidate.Point - home, math.normalize(bodyVelocity));
                if (forward < settings.MinimumForward)
                    return false;
            }

            foothold = candidate.Point;
            return true;
        }

        public static float2 Update(
            ref GaitLeg leg,
            FootState partnerState,
            float2 hip,
            float2 bodyVelocity,
            float maximumReach,
            in GaitSettings settings,
            float deltaTime)
        {
            return UpdateInternal(
                ref leg,
                partnerState,
                hip,
                bodyVelocity,
                minimumReach: 0f,
                maximumReach,
                settings,
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
            in GaitSettings settings,
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
                settings,
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
            in GaitSettings settings,
            float deltaTime,
            in FootholdCandidate footholdCandidate,
            bool useFootholdQuery,
            bool hasFootholdCandidate)
        {
            if (leg.State == FootState.Planted)
            {
                var home = hip + leg.HomeOffset;
                if (math.distance(leg.Plant, home) > settings.Comfort && partnerState == FootState.Planted)
                {
                    var foothold = home;
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
                                settings,
                                out foothold))
                            return leg.Plant;
                    }

                    leg.SwingFrom = leg.Plant;
                    leg.SwingSupport = useFootholdQuery ? footholdCandidate.Support : Entity.Null;
                    leg.SwingLocalPlant = useFootholdQuery ? footholdCandidate.SupportLocalPoint : float2.zero;
                    leg.Support = Entity.Null;
                    leg.LocalPlant = float2.zero;
                    leg.SurfaceOffset = float2.zero;
                    leg.CarryVelocity = float2.zero;
                    leg.SwingTo = useFootholdQuery
                        ? foothold
                        : ClampToReach(home + bodyVelocity * settings.StepLead, hip, maximumReach);
                    leg.SwingT = 0f;
                    leg.State = FootState.Swinging;
                    return leg.Plant;
                }

                return leg.Plant;
            }

            leg.SwingT += deltaTime / math.max(settings.StepDuration, MinimumDuration);
            if (leg.SwingT >= 1f)
            {
                leg.SwingT = 1f;
                leg.Plant = leg.SwingTo;
                leg.Support = leg.SwingSupport;
                leg.LocalPlant = leg.SwingLocalPlant;
                leg.SurfaceOffset = float2.zero;
                leg.CarryVelocity = float2.zero;
                leg.SwingSupport = Entity.Null;
                leg.SwingLocalPlant = float2.zero;
                leg.State = FootState.Planted;
                return leg.Plant;
            }

            return EvaluateSwingTarget(leg, settings);
        }

        public static float2 EvaluateSwingTarget(in GaitLeg leg, in GaitSettings settings)
        {
            var swingT = math.saturate(leg.SwingT);
            var smoothT = math.smoothstep(0f, 1f, swingT);
            var target = math.lerp(leg.SwingFrom, leg.SwingTo, smoothT);
            target.y += settings.StepHeight * 4f * swingT * (1f - swingT);
            return target;
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
