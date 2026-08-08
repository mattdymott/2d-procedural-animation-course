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

		/// <summary>
		/// Optional facing for a creature with <see cref="PlanarHeading"/>. Zero means "face the
		/// way you are travelling"; writing it is what lets a top-down creature turn on the spot,
		/// which is the case that gives a planted foot an honest reason to step.
		/// </summary>
		public float2 DesiredHeading;

		/// <summary>
		/// A turn your locomotion has decided on but not yet resolved: positive to turn left,
		/// negative to turn right, zero for no request. It is published as a semantic fact so that
		/// presentation can wind the body up before the heading actually changes. Nothing in the
		/// package steers by it — writing it never turns the creature.
		/// </summary>
		public float RequestedTurnSign;
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

	[InternalBufferCapacity(6)]
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

	[InternalBufferCapacity(6)]
	public struct GaitLeg : IBufferElementData
	{
		public FootState State;
		public float2 Plant;
		public float2 SwingFrom;
		public float2 SwingTo;
		public float SwingT;

		/// <summary>
		/// Where this foot wants to be, relative to its hip. Side-view reads it as a world offset;
		/// a creature with <see cref="PlanarHeading"/> reads it as a local offset — x along the
		/// heading, y across it — so the home rotates when the body turns.
		/// </summary>
		public float2 HomeOffset;

		public sbyte PartnerIndex;

		/// <summary>Which alternating tripod this leg belongs to: 0 or 1. Unused by other cadences.</summary>
		public byte TripodGroup;

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