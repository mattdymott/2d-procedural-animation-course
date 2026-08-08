using Unity.Entities;
using Unity.Mathematics;

namespace Tealeaf.ProceduralAnimation.Dots
{
	public struct VerletChain : IComponentData
	{
		public float RestLength;
		public float Damping;

		/// <summary>Acceleration applied to every point but the pinned root.</summary>
		public float2 Gravity;

		/// <summary>Vertical bob applied to the pinned root. Zero amplitude means no bob.</summary>
		public float RootBobAmplitude;

		public float RootBobFrequency;

		/// <summary>Seconds simulated, used only to phase the root bob.</summary>
		public float Time;
	}

	[InternalBufferCapacity(16)]
	public struct VerletPoint : IBufferElementData
	{
		public float2 Position;
		public float2 PreviousPosition;
	}

	/// <summary>
	/// Consumer-owned muscle target: the world point the chain tip is drawn toward each tick.
	/// Present only when the creature was composed with muscles, and written by your game —
	/// the package never invents a target of its own.
	/// </summary>
	public struct ChainTarget : IComponentData
	{
		public float2 Position;
		public float Strength;
	}

	/// <summary>Runtime-owned body root and liftoff carry state.</summary>
	public struct CreatureBody : IComponentData
	{
		public float2 RootPosition;
		public float2 CarryVelocity;
	}

	/// <summary>Consumer-owned desired root velocity, applied at the start of each package tick.</summary>
	public struct CreatureLocomotion : IComponentData
	{
		public float2 DesiredVelocity;
	}

	public struct ContactPlane : IBufferElementData
	{
		public float2 Point;
		public float2 Normal;
		public float Radius;
		public float Friction;
	}

	public struct Limb2Bone : IComponentData // TODO: this doesn't need to be an IComponentData
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
		Swinging
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

	public struct Gait : IComponentData
	{
		public float Comfort;
		public float StepDuration;
		public float StepLead;
		public float StepHeight;
		public float MinimumSupport;
		public float MinimumForward;
	}
}