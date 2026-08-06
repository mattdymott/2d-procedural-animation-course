using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    public static class VerletChainSolver
    {
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
