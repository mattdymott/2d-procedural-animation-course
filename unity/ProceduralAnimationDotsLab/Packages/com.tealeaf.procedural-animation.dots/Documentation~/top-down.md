# Top-down creatures

The side-view gait was never really about *down*. It is a body moving past feet
that have made promises. Add `PlanarGaitAuthoring` and the same rules run on a
movement plane: leg homes rotate with heading, footholds are judged by
walkability instead of a floor normal, and the swing arc becomes a picture the
renderer draws rather than geometry the solver believes.

Everything else is unchanged. Same `GaitLeg` state, same foothold seam, same IK
solver, same support relations, same hard resolve.

## What changes, and what does not

| Side-view | Top-down replacement | Action |
| --- | --- | --- |
| World-down and ground height | An explicit movement plane | Replace |
| Hip projected to the ground | Heading-rotated local home | Replace |
| `MinimumSupport` normal test | `Walkable` and `PathClear` facts | Replace |
| Parabolic swing target | Planar swing target plus drawn lift | Reinterpret |
| Committed plant | Committed planar plant | Keep |
| Velocity lead | Planar velocity lead | Keep |
| Support-relative plants and carry | Unchanged — the maths was always planar | Keep |

Set `VerletChainAuthoring.Gravity` to zero as well. On a movement plane there is
no down for a body to sag toward.

## Composing one

```text
VerletChain + Legs + Gait                  a walking creature
  + PlanarGait                             …on a top-down movement plane
```

`PlanarGaitAuthoring` requires `GaitAuthoring`, which already pulls in legs and
a chain. It bakes `PlanarHeading`, `GaitSupportPolicy`, `GaitCadenceState`,
`WaveGaitState`, the `WaveOrder` buffer, and `GaitRecoveryRequest`.

Presence of `PlanarHeading` is the mode switch. A creature without it keeps the
side-view behaviour exactly, including the arc baked into its swing target.

Leg `HomeOffset` changes meaning: x runs along the heading, y across it. The
initial heading is +X, so offsets authored for a side-view creature bake to the
same rest plants they always did.

## Heading

```csharp
public struct PlanarHeading : IComponentData
{
    public float2 LastForward;
}
```

`CreatureLocomotionSystem` refreshes it each tick from
`CreatureLocomotion.DesiredHeading` when you write one, otherwise from
`DesiredVelocity`, otherwise it keeps the facing it had.

Writing `DesiredHeading` is what lets a creature turn on the spot. That case
matters: a stationary turn rotates every home away from its plant, which is what
gives a planted foot an honest reason to step instead of sliding.

Heading rotates leg homes and nothing else. The package does not turn the chain
body — body pose stays whatever your locomotion drives, exactly as it did before.
If you want the body to visibly face its heading, that is presentation or your
own body pose, not a gait output.

## Who may step

Stress asks; support permits. Urgency is how far a plant has drifted from its
home; the cadence decides which stressed legs are allowed to act on it.

| Cadence | Permits | Character |
| --- | --- | --- |
| `Partner` | A leg whose authored partner stays planted | The original biped/quadruped rule |
| `Support` | One most-urgent leg per tick, if enough feet stay planted | Deliberate quadruped turns |
| `Tripod` | A whole diagonal tripod, while the opposing one is fully planted | Quick, rhythmic insect |
| `Wave` | Exactly the leg the cursor names | Slow, cautious crawler |

`GaitSupportPolicy.MinimumPlantedFeet` is the base a grant must leave behind
under `Partner`, `Support`, and `Wave`. `Tripod` does not consult it: its base is
already the whole opposing group, which is a stronger guarantee than a count.
Start with the smallest rule that reads well — a partner guard, then a
minimum planted count — and only reach for something geometric if a real
creature's turns expose a problem the smaller rule cannot express.

Permission never overrules legality. A permitted leg still has to find a
walkable, path-clear, reachable candidate that is worth stepping to, and a leg
that cannot holds its turn rather than passing it to a neighbour.

### Tripods

`LegsAuthoring.LegRecipe.TripodGroup` (0 or 1) assigns each leg to a diagonal
group. Per-leg gait data lives on the leg recipe so it cannot fall out of step
with the leg list. For a six-legged creature ordered front, middle, rear —
left then right — the diagonals are front-left, middle-right, rear-left against
their opposites.

### Wave

`PlanarGaitAuthoring.WaveOrder` is the crawl order: leg indices in the order
they are permitted to step. The cursor advances **only** when the leg it names
lands. A blocked leg holds the cursor; it never silently skips ahead.

Leaving the order empty bakes every leg once in index order.

## Cadence switching

Speed can request a cadence, but the request is applied only when no foot is in
the air:

```csharp
public struct GaitCadenceState : IComponentData
{
    public GaitCadence Active;
    public GaitCadence Pending;
}
```

`GaitSupportPolicy.EnterSpeed` and `ExitSpeed` are separate thresholds so a
creature loitering near one speed cannot flip cadence every tick; the baker
forces enter above exit. Plants and swing timers survive the switch untouched —
changing an internal policy is never a licence to rewrite a foot that has
already promised.

## Recovery

No legal foothold is information, not a failure to paper over.

```csharp
public struct GaitRecoveryRequest : IComponentData
{
    public GaitRecovery State;        // None | HoldingForFoothold
    public byte SlowDown;
    public float2 PreferredTurn;
    public byte BlockedLegIndex;      // 255 when nothing is blocked
}
```

Gait keeps the plant, keeps the cursor or group, and writes this request.
Your locomotion reads it and decides: slow down, turn, back away, play an
authored escape. `PreferredTurn` is a heading that would bring the blocked leg's
home back toward ground it can stand on — a suggestion, not a command.

Nothing in the package acts on the request. A blocked foot that quietly teleports
to the nearest point and asks IK to disguise it is precisely what this replaces.

## Presentation

Gait and IK produce one authoritative point on the movement plane.
`FootPresentationMath.Derive` turns it into a picture:

```csharp
var presentation = FootPresentationMath.Derive(
    planarFoot,                       // Limb2BoneLeg.Limb.Foot
    gaitLeg.State,
    gaitLeg.SwingT,
    new FootPresentationPolicy
    {
        VisualStepHeight = 0.35f,
        ScreenUp = new float2(0f, 1f),
        SortScale = 1f,
        SwingSortBias = 0.1f,
    });
```

- `ShadowPoint` is the planar foot: draw the shadow there.
- `FootPoint` is the sprite, offset along screen-up by a lift curve that is zero
  at both endpoints, so a foot lifts off and lands without a snap.
- `SortKey` is derived from the planar point plus an explicit swing bias. Sorting
  from the *raised* sprite instead is what makes a foot pass behind the wrong
  object halfway through its arc.

It is a pure function in `LowLevel`, deliberately not a system. Nothing inside
the package reads its output, and nothing may write lift back into a plant, a
swing target, IK, or collision.

## The tick

```text
FixedStepSimulationSystemGroup
├── your support adapters           write SupportPose / SupportKinematics
├── your locomotion adapter         write DesiredVelocity and DesiredHeading;
│                                   read GaitRecoveryRequest
├── your planar query adapter       publish walkable / path-clear candidates
└── ProceduralAnimationSolveSystemGroup
    ├── apply locomotion, carry velocity, and heading
    ├── integrate and constrain the chain
    ├── gait: resolve feet → cadence → permission → commit one target each
    ├── solve two-bone legs
    └── project contacts and publish the resolved pose

presentation (after the group)      derive lift, shadow, and sort key
```

The important edge is between the query adapter and gait: the query reports
evidence, and only gait turns it into a foot promise. The next is between the
solve group and presentation: presentation reads a resolved pose and never
repairs it.

## Building the slice

1. Draw body position, heading, local homes, and current plants.
2. Make a stationary 90° turn. One eligible leg should swing; no plant slides.
3. Add a blocked island. Candidate data changes only when a swing begins.
4. Render shadow and lift from the committed target — then disable presentation
   entirely. The simulation must still be legal.
5. Only then add moving supports, tripods, and cadence switching.
