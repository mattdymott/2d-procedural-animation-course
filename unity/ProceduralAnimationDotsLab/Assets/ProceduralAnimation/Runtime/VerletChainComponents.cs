using Unity.Entities;
using Unity.Mathematics;

namespace ProceduralAnimationDotsLab
{
    public struct VerletChain : IComponentData
    {
        public float LinkLength;
        public float Damping;
        public float MuscleStrength;
        public float Time;
    }

    [InternalBufferCapacity(16)]
    public struct VerletPoint : IBufferElementData
    {
        public float2 Position;
        public float2 PreviousPosition;
    }

    public struct ChainTarget : IComponentData
    {
        public float2 Position;
    }
}
