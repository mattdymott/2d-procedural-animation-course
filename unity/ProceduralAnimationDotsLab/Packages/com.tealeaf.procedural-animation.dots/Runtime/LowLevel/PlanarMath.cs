using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.LowLevel
{
    /// <summary>
    /// Heading maths for a top-down creature: the movement plane replaces world-down, so a leg's
    /// authored home is a local offset that rotates with the body rather than a world vector.
    /// </summary>
    public static class PlanarMath
    {
        /// <summary>The perpendicular of <paramref name="forward"/>, rotated a quarter turn.</summary>
        public static float2 Perpendicular(float2 forward) => new(-forward.y, forward.x);

        /// <summary>
        /// Rotates an authored local offset — x along the heading, y across it — into world space.
        /// </summary>
        public static float2 Rotate(float2 localOffset, float2 forward) =>
            forward * localOffset.x + Perpendicular(forward) * localOffset.y;

        /// <summary>
        /// A leg's heading-relative home. The origin is the hip, not the body centre, so the
        /// comfort radius and the IK reach annulus are measured from the same point.
        /// </summary>
        public static float2 Home(float2 hip, float2 localHome, float2 forward) =>
            hip + Rotate(localHome, forward);

        /// <summary>
        /// The heading to use this tick. An explicit facing wins, travel is the fallback, and a
        /// creature doing neither keeps the heading it had — which is what lets it turn on the
        /// spot and still stress its plants honestly.
        /// </summary>
        public static float2 Advance(float2 previousForward, float2 velocity, float2 desiredHeading)
        {
            var fallback = math.lengthsq(previousForward) > 1e-8f
                ? math.normalize(previousForward)
                : new float2(1f, 0f);
            return math.normalizesafe(desiredHeading, math.normalizesafe(velocity, fallback));
        }
    }
}
