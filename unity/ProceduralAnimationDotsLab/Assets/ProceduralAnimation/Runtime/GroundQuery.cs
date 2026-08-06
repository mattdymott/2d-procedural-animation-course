using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    public static class GroundQuery
    {
        const float BaseHeight = -2.1f;
        const float RampStart = 0f;
        const float RampEnd = 2.5f;
        const float RampRise = 0.35f;
        const float SupportHalfWidth = 1.35f;

        public static GroundHit Sample(byte legIndex, float2 probe)
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

            return new GroundHit
            {
                Exists = 1,
                LegIndex = legIndex,
                Probe = probe,
                Point = new float2(probe.x, height),
                Normal = math.normalizesafe(new float2(-slope, 1f), new float2(0f, 1f)),
                Surface = (byte)(slope == 0f ? 0 : 1),
            };
        }

        public static bool TrySampleSupport(
            byte legIndex,
            float2 probe,
            Entity support,
            in SupportPose pose,
            out GroundHit hit)
        {
            var localProbe = SupportPoseMath.InverseTransformPoint(pose, probe);
            if (math.abs(localProbe.x) > SupportHalfWidth)
            {
                hit = default;
                return false;
            }

            var localPoint = new float2(localProbe.x, 0f);
            hit = new GroundHit
            {
                Exists = 1,
                LegIndex = legIndex,
                Probe = probe,
                Point = SupportPoseMath.TransformPoint(pose, localPoint),
                Normal = SupportPoseMath.TransformDirection(pose, new float2(0f, 1f)),
                Support = support,
                LocalPoint = localPoint,
                Surface = 2,
            };
            return true;
        }
    }
}
