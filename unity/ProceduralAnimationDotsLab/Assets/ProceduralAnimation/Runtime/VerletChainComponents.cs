using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    /// <summary>Lesson-only patrol intent. Gameplay owns root motion in a consuming project.</summary>
    public struct CreatureIntent : IComponentData
    {
        public float2 DesiredVelocity;
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
