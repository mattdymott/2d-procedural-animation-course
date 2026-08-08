using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots.LowLevel
{
    /// <summary>Authored look of a top-down foot. None of it is simulation input.</summary>
    public struct FootPresentationPolicy
    {
        /// <summary>Peak screen-space rise of a swinging foot. Zero draws the foot on its shadow.</summary>
        public float VisualStepHeight;

        /// <summary>Screen-up in world units — the direction a lifted foot sprite is offset along.</summary>
        public float2 ScreenUp;

        /// <summary>Scales the planar depth that becomes a sort key.</summary>
        public float SortScale;

        /// <summary>Added to a swinging foot's sort key, so occlusion is a chosen rule.</summary>
        public float SwingSortBias;
    }

    /// <summary>Transient view data derived from one resolved planar foot point.</summary>
    public struct FootPresentation
    {
        /// <summary>The committed planar point — where the foot actually is.</summary>
        public float2 ShadowPoint;

        /// <summary>Where the foot sprite is drawn: the shadow, offset along screen-up.</summary>
        public float2 FootPoint;

        public float VisualLift;

        /// <summary>Depth key taken from the planar point, never from the lifted sprite.</summary>
        public float SortKey;
    }

    /// <summary>
    /// Turns one planar foot point into a readable picture. A top-down swing is mechanically
    /// perfect and visually invisible without it, so lift, shadow, and sort order exist — but
    /// they are derived here, at the end, and nothing reads them back.
    /// </summary>
    public static class FootPresentationMath
    {
        /// <summary>
        /// The familiar parabola, reused as a screen-space height curve. It is zero at both
        /// endpoints, so a foot lifts off and lands on its committed planar point without a snap.
        /// </summary>
        public static float Lift(float swingT, float visualStepHeight)
        {
            var t = math.saturate(swingT);
            return visualStepHeight * 4f * t * (1f - t);
        }

        public static FootPresentation Derive(
            float2 planarFoot,
            FootState state,
            float swingT,
            in FootPresentationPolicy policy)
        {
            var swinging = state == FootState.Swinging;
            var lift = swinging ? Lift(swingT, policy.VisualStepHeight) : 0f;
            var screenUp = math.normalizesafe(policy.ScreenUp, new float2(0f, 1f));

            return new FootPresentation
            {
                ShadowPoint = planarFoot,
                FootPoint = planarFoot + screenUp * lift,
                VisualLift = lift,
                SortKey = -math.dot(planarFoot, screenUp) * policy.SortScale
                          + (swinging ? policy.SwingSortBias : 0f),
            };
        }
    }
}
