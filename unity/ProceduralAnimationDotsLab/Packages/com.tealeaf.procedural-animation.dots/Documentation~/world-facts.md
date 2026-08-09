# World facts

World facts are the data your adapters write before
`ProceduralAnimationSolveSystemGroup` runs each fixed step. They are plain
component data rather than an interface, so the solve stays Burst-friendly and
so a terrain adapter and a moving-support adapter are two independent writers
at the same seam.

## Desired motion

```csharp
public struct CreatureLocomotion : IComponentData
{
    public float2 DesiredVelocity;
    public float2 DesiredHeading;
}
```

Your game decides root motion — patrol, player input, steering, pathfinding.
The package applies `DesiredVelocity` plus any carry velocity it owes the
creature from its last liftoff, then simulates.

Write it every tick. It is intent, not an impulse, so leaving a stale value in
place keeps the creature walking.

`DesiredHeading` matters only to a creature with `PlanarHeading` (see
[top-down creatures](top-down.md)). Leave it zero to face the way you travel;
write it to aim independently of travel, which is what lets a top-down creature
turn on the spot.

## Muscle target

```csharp
public struct ChainTarget : IComponentData
{
    public float2 Position;
    public float Strength;
}
```

Present only on creatures composed with `MusclesAuthoring`. `Position` is the
world point the chain tip is drawn toward each tick, and it is yours: a lure, a
cursor, a swaying reach offset from the body. The package reads it and never
writes it.

Write it every tick, like `CreatureLocomotion` — it is a standing aim, not an
impulse. `Strength` shares the component, so write both fields together:

```csharp
var muscle = SystemAPI.GetComponent<ChainTarget>(entity);
muscle.Position = aim;               // Strength carried forward
SystemAPI.SetComponent(entity, muscle);
```

Assigning a fresh `ChainTarget` with only `Position` set would zero the strength
baked from `MusclesAuthoring` and quietly stop the pull.

## Foothold candidates

```csharp
public struct FootholdCandidate : IBufferElementData
{
    public byte LegIndex;
    public float2 Point;
    public float2 Normal;
    public byte Walkable;
    public byte PathClear;
    public Entity Support;
    public float2 SupportLocalPoint;
}
```

One observation of somewhere a leg *could* step. Clear and refill the buffer
each tick; gait reads it only when a foot is about to leave the ground.

- `LegIndex` selects which leg the observation is for. A leg may have several
  candidates; gait accepts at most one.
- `Point` and `Normal` are the world contact point and surface normal. `Normal`
  is what `MinimumSupport` tests against, so a wall-steep normal is rejected
  rather than walked on.
- `Walkable` and `PathClear` are read only by a top-down creature, which has no
  meaningful floor normal to judge. Set both to 1 for a legal point. A side-view
  creature ignores them, so existing adapters need no change. You choose what
  `PathClear` measures along; measuring from the current plant is a trap, since a
  blocked leg's plant goes stale while it waits and the lengthening segment locks
  the leg out permanently. The planar sample measures from the hip.
- `Support` and `SupportLocalPoint` are optional. Set them when the surface
  moves: `Support` is the entity carrying `SupportPose`, and
  `SupportLocalPoint` is the same contact point expressed in that support's
  local space. Leave them default for static ground.

  Both projections use them. The top-down sample leaves them default because its
  arena is static, not because a planar creature cannot ride a moving support —
  it can, and `PlanarGaitTests` pins it. An unpopulated field means this world has
  nothing to report, never that the projection cannot use it.

- `ObservedFrame` is the published frame this observation was made against; see
  the probe frame below. Leave it zero and the candidate is judged against the
  live body, which is what every adapter written before this existed does.

Where the observations come from is entirely yours — a raycast, a tilemap
lookup, a signed-distance field, or a hand-authored function. The Lab sample's
`GroundQuery` is a deterministic analytic version of exactly this and is meant
to be replaced whole.

## The probe frame

```csharp
public struct FootholdProbe : IBufferElementData   // one per leg, index-aligned
{
    public float2 Home;
    public float2 PredictedHome;
    public float2 Hip;
    public byte Valid;
}

public struct FootholdProbeFrame : IComponentData
{
    public uint FrameId;
    public float2 Forward;
}
```

Where each leg is aiming its next step, published by the package as the last
thing the solve does. `Home` is the leg's home resolved against the body —
heading-rotated on a movement plane — and `PredictedHome` is that home led by
body velocity over `Gait.StepLead`. Working this out is the part of an adapter
that has to agree with gait exactly, so the package does it once and hands you
the answer.

- Read it to decide where to look. You need no ordering attribute: the frame is
  published before your adapter next runs, so it is already there.
- The aim you read was measured against the previous solve. Stamp
  `FootholdCandidate.ObservedFrame` with `FrameId` and gait judges your candidate
  against that same aim, so the two cannot drift apart.
- `Valid` is zero for a leg with no usable hip. Skip those rather than offering
  a foothold at the origin.
- `FrameId` starts at 1 and is never zero once published, which is what lets
  zero mean "unstamped" on a candidate.

Gait refuses stamped evidence older than `Gait.MaximumEvidenceAge` frames and
reports `GaitRecovery.HoldingForFreshEvidence`. An adapter running once per solve
produces age zero or one; raise the tolerance if yours runs at a slower cadence
than the fixed step. This is a different problem from having nowhere to stand,
and it is reported differently because the fix is different: an adapter that
keeps up, rather than a creature that turns away.

Reading the frame is optional. An adapter that derives its own aim and stamps
nothing keeps working exactly as it did — the package ships one of each, and
`PlanarGaitTests` pins the published aim against the derived one.

Candidates are evidence. Gait applies its own support, reach, and forward
policy, and commits a target only on a planted-to-swinging transition. While a
foot is planted the package uses the committed support relation and ignores new
candidates for that leg.

## Moving and conveyor supports

```csharp
public struct SupportPose : IComponentData
{
    public float2 Position;
    public float RotationRadians;
}

public struct SupportKinematics : IComponentData
{
    public float2 LinearVelocity;
    public float AngularVelocityRadians;
    public float2 SurfaceVelocityLocal;
}
```

Put both on any entity a foot can stand on and that is not stationary — a
platform, a lift, a rotating disc, a conveyor belt.

`SupportPose` is the animation input: where the support is now. Keep it in step
with however you move the support, and write it before you sample footholds
against it.

`SupportKinematics` is the evaluated motion the package needs to carry a
planted foot correctly. `LinearVelocity` and `AngularVelocityRadians` describe
the body's own motion; `SurfaceVelocityLocal` is travel *across* the surface in
support-local coordinates, which is what makes a conveyor a conveyor without
moving the platform.

A planted foot stays fixed in support-local space, so it stays glued while the
support moves and rotates beneath it. It receives point plus conveyor velocity
as carry only at liftoff — that is what keeps a creature stepping off a moving
platform with the platform's momentum instead of snapping.

Splitting pose from kinematics keeps a support that is teleported (pose only)
distinguishable from one that is moving (both), and lets a conveyor with a
stationary body express surface travel without a fake linear velocity.

### SupportMath

`SupportMath` lives in `Tealeaf.ProceduralAnimation.Dots.LowLevel`. It is the
same pure planar maths the gait uses, exposed for adapters that need it:

- `TransformPoint` / `InverseTransformPoint` — convert between world and
  support-local space. `InverseTransformPoint` is how you fill
  `SupportLocalPoint`.
- `TransformDirection` — rotate a direction into world space.
- `PointVelocity` — world velocity of a support-local point, including
  rotational contribution and conveyor travel.
