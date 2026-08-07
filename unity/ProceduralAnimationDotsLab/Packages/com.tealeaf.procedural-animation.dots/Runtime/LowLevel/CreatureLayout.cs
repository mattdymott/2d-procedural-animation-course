using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.LowLevel
{
    /// <summary>
    /// The rest layout of a chain body. Separate bakers need to agree on where a chain point
    /// starts — the chain baker to place the points, the leg and gait bakers to place a foot
    /// under its attachment point — so the formula lives in exactly one place.
    /// </summary>
    public static class CreatureLayout
    {
        /// <summary>Rest position of chain point <paramref name="index"/>, laid out along +X.</summary>
        public static float2 PointPosition(float2 root, float restLength, int index) =>
            root + new float2(index * restLength, 0f);
    }
}
