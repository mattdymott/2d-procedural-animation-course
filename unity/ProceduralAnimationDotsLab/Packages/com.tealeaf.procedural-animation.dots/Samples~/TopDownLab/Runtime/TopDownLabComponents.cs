using Unity.Entities;
using Unity.Mathematics;

namespace TopDownLab
{
    /// <summary>
    /// The demo creature's own steering: a constant circuit around the origin. Gameplay owns
    /// this decision in a real project — the package only consumes the result.
    /// </summary>
    public struct TopDownIntent : IComponentData
    {
        public float2 Centre;
        public float Radius;
        public float Speed;

        /// <summary>How fast the creature turns toward the course it wants, per second.</summary>
        public float TurnRate;

        /// <summary>Speed multiplier while gait is asking for a safer approach.</summary>
        public float RecoverySpeedScale;

        /// <summary>How strongly a recovery request bends the heading, per second.</summary>
        public float RecoveryTurnRate;
    }

    /// <summary>A circular region of the movement plane no foot may stand in.</summary>
    public struct PlanarIsland : IComponentData
    {
        public float2 Centre;
        public float Radius;
    }

    /// <summary>
    /// What the planar query offered this tick, kept only so presentation can draw the evidence
    /// gait was given. Nothing reads it back into the simulation.
    /// </summary>
    [InternalBufferCapacity(30)]
    public struct PlanarQueryDebugHit : IBufferElementData
    {
        public byte LegIndex;
        public float2 Point;
        public byte Legal;
    }
}
