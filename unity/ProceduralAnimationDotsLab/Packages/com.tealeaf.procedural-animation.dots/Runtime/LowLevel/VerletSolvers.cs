using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.LowLevel
{
    /// <summary>
    /// Pure Verlet chain repair: pin a point and satisfy one distance constraint.
    /// </summary>
    public static class VerletChainSolver
    {
        // A decorative vertical bob on the pinned root. Authored per creature; a zero amplitude
        // is the default and makes this the identity.
        internal static float2 ResolveRoot(float2 bodyPosition, float time, float bobAmplitude, float bobFrequency) =>
            bodyPosition + new float2(0f, math.sin(time * bobFrequency) * bobAmplitude);
        public static void Pin(ref VerletPoint point, float2 position)
        {
            point.Position = position;
            point.PreviousPosition = position;
        }

        public static void SatisfyDistance(ref VerletPoint first, ref VerletPoint second, float restLength)
        {
            var offset = second.Position - first.Position;
            var distance = math.length(offset);
            if (distance < 0.0001f) return;
            var correction = offset * ((distance - restLength) / distance);
            first.Position += correction * 0.5f;
            second.Position -= correction * 0.5f;
        }
    }

    /// <summary>
    /// Pure one-sided contact projection for a Verlet point against a plane.
    /// </summary>
    public static class VerletContactSolver
    {
        public static bool ProjectAgainstPlane(ref VerletPoint point, in ContactPlane plane)
        {
            var normal = math.normalizesafe(plane.Normal, new float2(0f, 1f));
            var penetration = math.max(plane.Radius, 0f) - math.dot(point.Position - plane.Point, normal);
            if (penetration <= 0f) return false;
            point.Position += normal * penetration;
            var velocity = point.Position - point.PreviousPosition;
            var normalVelocity = math.max(math.dot(velocity, normal), 0f);
            var tangentVelocity = velocity - normal * math.dot(velocity, normal);
            point.PreviousPosition = point.Position - (normal * normalVelocity + tangentVelocity * (1f - math.saturate(plane.Friction)));
            return true;
        }
    }
}
