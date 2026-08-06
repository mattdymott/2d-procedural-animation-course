using Unity.Entities;
using Unity.Mathematics;
using Tealeaf.ProceduralAnimation.Dots;

namespace ProceduralAnimationDotsLab
{
    public static class GroundQuery
    {
        const float BaseHeight = -2.1f;
        const float RampStart = 0f;
        const float RampEnd = 2.5f;
        const float RampRise = 0.35f;
        const float SupportHalfWidth = 1.35f;

        public static FootholdCandidate Sample(byte legIndex, float2 probe)
        {
            var slope = 0f;
            var height = BaseHeight;
            if (probe.x > RampStart && probe.x < RampEnd)
            {
                slope = RampRise / (RampEnd - RampStart);
                height += (probe.x - RampStart) * slope;
            }
            else if (probe.x >= RampEnd)
            {
                height += RampRise;
            }

            return new FootholdCandidate
            {
                LegIndex = legIndex,
                Point = new float2(probe.x, height),
                Normal = math.normalizesafe(new float2(-slope, 1f), new float2(0f, 1f)),
            };
        }

        public static bool TrySampleSupport(
            byte legIndex,
            float2 probe,
            Entity support,
            in SupportPose pose,
            out FootholdCandidate candidate)
        {
            var localProbe = SupportMath.InverseTransformPoint(pose, probe);
            if (math.abs(localProbe.x) > SupportHalfWidth)
            {
                candidate = default;
                return false;
            }

            var localPoint = new float2(localProbe.x, 0f);
            candidate = new FootholdCandidate
            {
                LegIndex = legIndex,
                Point = SupportMath.TransformPoint(pose, localPoint),
                Normal = SupportMath.TransformDirection(pose, new float2(0f, 1f)),
                Support = support,
                SupportLocalPoint = localPoint,
            };
            return true;
        }

        public static GroundQueryDebugHit CreateDebugHit(
            byte legIndex,
            float2 probe,
            in FootholdCandidate candidate)
        {
            return new GroundQueryDebugHit
            {
                Exists = 1,
                LegIndex = legIndex,
                Probe = probe,
                Point = candidate.Point,
                Normal = candidate.Normal,
            };
        }
    }
}
