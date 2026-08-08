using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// One terrain, physics, or custom-world observation that a leg may choose as its next foothold.
    /// Presence in the owning creature's buffer represents a valid observation; gait decides whether
    /// it satisfies support, reach, and directional policy.
    /// </summary>
    /// <remarks>
    /// A buffer may hold several candidates for the same leg. Gait accepts at most one, and only
    /// at the tick a swing begins — a candidate that appears later never moves a planted foot.
    /// </remarks>
    [InternalBufferCapacity(6)]
    public struct FootholdCandidate : IBufferElementData
    {
        public byte LegIndex;
        public float2 Point;

        /// <summary>Surface normal. Read by the side-view support test; ignored on a movement plane.</summary>
        public float2 Normal;

        /// <summary>
        /// Non-zero when the point is inside walkable space. Top-down only: a blocked tile is not
        /// a bad floor normal, so a planar creature rejects on this fact instead.
        /// </summary>
        public byte Walkable;

        /// <summary>Non-zero when nothing blocks the route from the current plant to this point.</summary>
        public byte PathClear;

        public Entity Support;
        public float2 SupportLocalPoint;
    }
}
