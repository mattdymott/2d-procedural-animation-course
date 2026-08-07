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
}
```

Your game decides root motion — patrol, player input, steering, pathfinding.
The package applies `DesiredVelocity` plus any carry velocity it owes the
creature from its last liftoff, then simulates.

Write it every tick. It is intent, not an impulse, so leaving a stale value in
place keeps the creature walking.

## Foothold candidates

```csharp
public struct FootholdCandidate : IBufferElementData
{
    public byte LegIndex;
    public float2 Point;
    public float2 Normal;
    public Entity Support;
    public float2 SupportLocalPoint;
}
```

One observation of somewhere a leg *could* step. Clear and refill the buffer
each tick; gait reads it only when a foot is about to leave the ground.

- `LegIndex` selects which leg the observation is for.
- `Point` and `Normal` are the world contact point and surface normal. `Normal`
  is what `MinimumSupport` tests against, so a wall-steep normal is rejected
  rather than walked on.
- `Support` and `SupportLocalPoint` are optional. Set them when the surface
  moves: `Support` is the entity carrying `SupportPose`, and
  `SupportLocalPoint` is the same contact point expressed in that support's
  local space. Leave them default for static ground.

Where the observations come from is entirely yours — a raycast, a tilemap
lookup, a signed-distance field, or a hand-authored function. The Lab sample's
`GroundQuery` is a deterministic analytic version of exactly this and is meant
to be replaced whole.

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

`SupportMath` is the same pure planar maths the gait uses, exposed for adapters
that need it:

- `TransformPoint` / `InverseTransformPoint` — convert between world and
  support-local space. `InverseTransformPoint` is how you fill
  `SupportLocalPoint`.
- `TransformDirection` — rotate a direction into world space.
- `PointVelocity` — world velocity of a support-local point, including
  rotational contribution and conveyor travel.
