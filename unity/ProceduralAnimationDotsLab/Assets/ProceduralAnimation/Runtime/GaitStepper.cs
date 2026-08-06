using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    public static class GaitStepper
    {
        // Keep authored zero-duration settings finite while the edit-time data is being assembled.
        const float MinimumDuration = 0.0001f;

        public static float2 Update(
            ref GaitLeg leg,
            FootState partnerState,
            float2 hip,
            float2 bodyVelocity,
            float maximumReach,
            in GaitSettings settings,
            float deltaTime)
        {
            if (leg.State == FootState.Planted)
            {
                var home = hip + leg.HomeOffset;
                if (math.distance(leg.Plant, home) > settings.Comfort && partnerState == FootState.Planted)
                {
                    leg.SwingFrom = leg.Plant;
                    leg.SwingTo = ClampToReach(home + bodyVelocity * settings.StepLead, hip, maximumReach);
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
