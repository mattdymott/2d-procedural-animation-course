using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Immutable input for a planar two-bone inverse-kinematics solve.
    /// Both lengths must be greater than zero; BendSign chooses the knee side.
    /// </summary>
    public struct TwoBoneIkRequest
    {
        public float2 Root;
        public float2 Target;
        public float LengthA;
        public float LengthB;
        public float BendSign;
    }

    /// <summary>
    /// The resolved knee and reachable foot point for a two-bone limb.
    /// </summary>
    public struct TwoBoneIkPose
    {
        public float2 Knee;
        public float2 Foot;
    }

    /// <summary>
    /// Allocation-free analytic planar two-bone inverse kinematics.
    /// </summary>
    public static class TwoBoneIk
    {
        const float Epsilon = 0.0001f;

        /// <summary>
        /// Clamps the target to the limb's reachable annulus and resolves its knee and foot.
        /// A target at the root uses positive X as a stable direction.
        /// </summary>
        public static TwoBoneIkPose Solve(in TwoBoneIkRequest request)
        {
            var toTarget = request.Target - request.Root;
            var targetDistance = math.length(toTarget);
            var direction = math.normalizesafe(toTarget, new float2(1f, 0f));
            var minimumReach = math.abs(request.LengthA - request.LengthB) + Epsilon;
            var maximumReach = request.LengthA + request.LengthB - Epsilon;
            var solvedDistance = math.clamp(targetDistance, minimumReach, maximumReach);

            var cosine = (request.LengthA * request.LengthA + solvedDistance * solvedDistance - request.LengthB * request.LengthB)
                / (2f * request.LengthA * solvedDistance);
            var angle = math.acos(math.clamp(cosine, -1f, 1f)) * request.BendSign;
            math.sincos(angle, out var sine, out var cosineAngle);
            var rotatedDirection = new float2(
                direction.x * cosineAngle - direction.y * sine,
                direction.x * sine + direction.y * cosineAngle);

            return new TwoBoneIkPose
            {
                Foot = request.Root + direction * solvedDistance,
                Knee = request.Root + rotatedDirection * request.LengthA,
            };
        }
    }
}
