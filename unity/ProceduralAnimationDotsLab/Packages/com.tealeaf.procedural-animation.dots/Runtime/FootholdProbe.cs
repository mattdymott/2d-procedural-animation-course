using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
    /// <summary>
    /// Where one leg is aiming its next step, published by the package at the end of the solve so a
    /// query adapter can read it instead of deriving it. Read-only to consumers: the package
    /// rewrites the whole buffer every tick.
    /// </summary>
    /// <remarks>
    /// An adapter that ignores this and computes its own probe still works — see
    /// <see cref="FootholdCandidate.ObservedFrame"/> for how the two paths stay honest.
    /// </remarks>
    [InternalBufferCapacity(6)]
    public struct FootholdProbe : IBufferElementData
    {
        /// <summary>Where this foot wants to be, resolved against the body. Heading-rotated on a movement plane.</summary>
        public float2 Home;

        /// <summary>
        /// <see cref="Home"/> carried forward by the body's velocity — where a step beginning now
        /// should aim. This is the point gait ranks candidates against.
        /// </summary>
        public float2 PredictedHome;

        /// <summary>The hip <see cref="Home"/> was measured from.</summary>
        public float2 Hip;

        /// <summary>
        /// Zero when this leg has no valid hip to measure from. The buffer stays index-aligned
        /// with the legs either way, so an adapter skips these rather than offering a foothold at
        /// the origin.
        /// </summary>
        public byte Valid;
    }

    /// <summary>
    /// Stamps the <see cref="FootholdProbe"/> buffer with the body pose it was derived from, so a
    /// candidate can record which aim it was observed against.
    /// </summary>
    /// <remarks>
    /// Published as the last step of the package solve, which means an adapter running before the
    /// next solve reads a frame that is one solve old. That is deliberate: both the adapter and
    /// gait then judge a step against the <em>same</em> aim, which they cannot do while each
    /// derives its own from a body that moves in between.
    /// </remarks>
    public struct FootholdProbeFrame : IComponentData
    {
        /// <summary>
        /// Increments once per published solve, starting at 1. Zero is never published, which is
        /// what lets <see cref="FootholdCandidate.ObservedFrame"/> use it to mean "unstamped".
        /// </summary>
        public uint FrameId;

        /// <summary>The heading the homes were rotated by. <c>(1, 0)</c> for a side-view creature.</summary>
        public float2 Forward;
    }
}
