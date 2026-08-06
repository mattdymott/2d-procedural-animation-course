using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    public static class VerletChainSolver
    {
        public static float2 ResolveRoot(float2 bodyPosition, float time)
        {
            return bodyPosition + new float2(0f, math.sin(time * 0.9f) * 0.35f);
        }

        public static void Pin(ref VerletPoint point, float2 position)
        {
            point.Position = position;
            point.PreviousPosition = position;
        }

        public static void SatisfyDistance(ref VerletPoint first, ref VerletPoint second, float restLength)
        {
            var offset = second.Position - first.Position;
            var distance = math.length(offset);

            if (distance < 0.0001f)
                return;

            var correction = offset * ((distance - restLength) / distance);
            first.Position += correction * 0.5f;
            second.Position -= correction * 0.5f;
        }
    }
}
