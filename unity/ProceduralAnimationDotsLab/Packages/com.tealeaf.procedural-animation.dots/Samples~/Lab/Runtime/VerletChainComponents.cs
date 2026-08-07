using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    /// <summary>
    /// Lesson-only patrol policy. It writes <c>CreatureLocomotion</c> before the package
    /// solve group; gameplay owns that decision in a consuming project.
    /// </summary>
    public struct LabCreaturePatrol : IComponentData
    {
        public float Speed;
        public float Direction;
        public float MinimumX;
        public float MaximumX;
        /// <summary>Seconds simulated, used to phase the lesson tail sway.</summary>
        public float Time;
    }

    public struct DemoMovingSupport : IComponentData
    {
        public float2 Origin;
        public float2 Amplitude;
        public float Frequency;
        public float Time;
        public float2 SurfaceVelocityLocal;
    }

    [InternalBufferCapacity(2)]
    public struct GroundQueryDebugHit : IBufferElementData
    {
        public byte Exists;
        public byte LegIndex;
        public float2 Probe;
        public float2 Point;
        public float2 Normal;
    }

}
