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

    public struct CreatureIntent : IComponentData
    {
        public float2 DesiredVelocity;
    }

    public struct CreatureBody : IComponentData
    {
        public float2 RootPosition;
        public float2 CarryVelocity;
    }

    public struct ContactPlane : IBufferElementData
    {
        public float2 Point;
        public float2 Normal;
        public float Radius;
        public float Friction;
    }

    public struct SupportPose : IComponentData
    {
        public float2 Position;
        public float Rotation;
    }

    public struct SupportMotion : IComponentData
    {
        public float2 Origin;
        public float2 Amplitude;
        public float Frequency;
        public float Time;
        public float2 BeltVelocityLocal;
        public float2 WorldVelocity;
        public float AngularVelocity;
    }

    [InternalBufferCapacity(2)]
    public struct GroundHit : IBufferElementData
    {
        public byte Exists;
        public byte LegIndex;
        public float2 Probe;
        public float2 Point;
        public float2 Normal;
        public Entity Support;
        public float2 LocalPoint;
        public byte Surface;
    }

    public struct Limb2Bone : IComponentData
    {
        public float2 Root;
        public float2 Target;
        public float LengthA;
        public float LengthB;
        public float BendSign;
        public float2 Knee;
        public float2 Foot;
    }

    [InternalBufferCapacity(2)]
    public struct Limb2BoneLeg : IBufferElementData
    {
        public Limb2Bone Limb;
        public int RootPointIndex;
    }

    public enum FootState : byte
    {
        Planted,
        Swinging,
    }

    [InternalBufferCapacity(2)]
    public struct GaitLeg : IBufferElementData
    {
        public FootState State;
        public float2 Plant;
        public float2 SwingFrom;
        public float2 SwingTo;
        public float SwingT;
        public float2 HomeOffset;
        public sbyte PartnerIndex;
        public Entity Support;
        public float2 LocalPlant;
        public float2 SurfaceOffset;
        public float2 CarryVelocity;
        public Entity SwingSupport;
        public float2 SwingLocalPlant;
    }

    public struct GaitSettings : IComponentData
    {
        public float Comfort;
        public float StepDuration;
        public float StepLead;
        public float StepHeight;
        public float MinimumSupport;
        public float MinimumForward;
    }
}
