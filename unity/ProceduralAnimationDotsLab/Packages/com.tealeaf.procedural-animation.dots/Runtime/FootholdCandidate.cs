using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// One terrain, physics, or custom-world observation that a leg may choose as its next foothold.
    /// Presence in the owning creature's buffer represents a valid observation; gait decides whether
    /// it satisfies support, reach, and directional policy.
    /// </summary>
    [InternalBufferCapacity(2)]
    public struct FootholdCandidate : IBufferElementData
    {
        public byte LegIndex;
        public float2 Point;
        public float2 Normal;
        public Entity Support;
        public float2 SupportLocalPoint;
    }
}
