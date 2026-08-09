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

        /// <summary>
        /// Non-zero when nothing blocks the route a swing would take to this point. Top-down only,
        /// like <see cref="Walkable"/>. The adapter chooses what the route is measured from —
        /// measuring from the current plant is a trap, because a blocked leg's plant goes stale
        /// while it waits and the lengthening segment locks the leg out permanently.
        /// </summary>
        public byte PathClear;

        /// <summary>
        /// The entity carrying <c>SupportPose</c> that this point sits on, and the same point in
        /// that support's local space. Optional in either projection: leave them default for
        /// static ground. Set them and the plant travels with a platform or conveyor without ever
        /// being re-queried.
        /// </summary>
        public Entity Support;

        /// <inheritdoc cref="Support"/>
        public float2 SupportLocalPoint;

        /// <summary>
        /// The <see cref="FootholdProbeFrame.FrameId"/> this observation was made against, if the
        /// adapter read one. Zero — the default — means unstamped: gait judges the candidate
        /// against the live body exactly as it always has.
        /// </summary>
        /// <remarks>
        /// A stamped candidate is judged against the aim it was observed with, so the adapter and
        /// gait cannot disagree about where the step was going. It is also how gait spots evidence
        /// older than <see cref="Gait.MaximumEvidenceAge"/> and reports
        /// <see cref="GaitRecovery.HoldingForFreshEvidence"/> rather than silently stepping on it.
        /// </remarks>
        public uint ObservedFrame;
    }
}
