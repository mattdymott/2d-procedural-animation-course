using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    public static class TwoBoneIkSolver
    {
        const float Epsilon = 0.0001f;

        public static void Solve(ref Limb2Bone limb)
        {
            var toTarget = limb.Target - limb.Root;
            var targetDistance = math.length(toTarget);
            var direction = math.normalizesafe(toTarget, new float2(1f, 0f));
            var minimumReach = math.abs(limb.LengthA - limb.LengthB) + Epsilon;
            var maximumReach = limb.LengthA + limb.LengthB - Epsilon;
            var solvedDistance = math.clamp(targetDistance, minimumReach, maximumReach);

            limb.Foot = limb.Root + direction * solvedDistance;

            var cosine = (limb.LengthA * limb.LengthA + solvedDistance * solvedDistance - limb.LengthB * limb.LengthB)
                / (2f * limb.LengthA * solvedDistance);
            var angle = math.acos(math.clamp(cosine, -1f, 1f)) * limb.BendSign;
            math.sincos(angle, out var sine, out var cosineAngle);
            var rotatedDirection = new float2(
                direction.x * cosineAngle - direction.y * sine,
                direction.x * sine + direction.y * cosineAngle);

            limb.Knee = limb.Root + rotatedDirection * limb.LengthA;
        }
    }
}
