# Authoring reference

A creature is whichever of these components its GameObject carries. Add the ones
whose behaviour you want; each one bakes its own runtime data and nothing else.

| Component | Adds | Requires |
| --- | --- | --- |
| `VerletChainAuthoring` | A chain body and its root | — |
| `MusclesAuthoring` | Draws the chain tip toward a target you write | `VerletChainAuthoring` |
| `LegsAuthoring` | Two-bone limbs | `VerletChainAuthoring` |
| `GaitAuthoring` | Alternating stepping | `LegsAuthoring` |
| `PlanarGaitAuthoring` | Moves that stepping onto a top-down movement plane | `GaitAuthoring` |
| `ContactPlanesAuthoring` | Static planes the body cannot sink through | `VerletChainAuthoring` |

The dependencies are declared with `[RequireComponent]`, so adding
`GaitAuthoring` to a bare GameObject pulls in legs and a chain automatically.
Every field is clamped to a usable range during baking, so an out-of-range value
degrades rather than producing an invalid creature.

Some compositions and what they give you:

```text
VerletChain                                   a rope or hanging tail
VerletChain + Muscles                         a tentacle reaching for a target you write
VerletChain + Legs                            limbs you aim yourself
VerletChain + Legs + Gait                     a walking creature
VerletChain + Legs + Gait + ContactPlanes     a walking creature with a floor
VerletChain + Legs + Gait + PlanarGait        a top-down creature on a movement plane
```

## VerletChainAuthoring

The chain body — a creature's spine, and the root everything else hangs from.
Legs index into its points, so in this package the chain *is* the body: there is
no separate body component to add first.

| Field | Default | Meaning |
| --- | --- | --- |
| `ChainSegmentCount` | 16 | Number of Verlet points. Minimum 2. |
| `InitialRootPosition` | (-3.5, 0.5) | World position of point 0 at bake time. |
| `RestLength` | 0.48 | Rest distance between neighbouring points. |
| `Damping` | 0.992 | Velocity retained per step. Clamped to 0–1. |
| `Gravity` | (0, -3.5) | Acceleration on every point but the pinned root. Zero for a chain in free space. |
| `RootBobAmplitude` | 0 | Decorative vertical oscillation of the pinned root. Zero means no bob. |
| `RootBobFrequency` | 0 | Radians per second for that oscillation. |

Points are laid out along +X from the root at `RestLength` spacing.

Bakes `VerletChain`, `CreatureBody`, `CreatureLocomotion`, and the
`VerletPoint` buffer.

Nothing here invents motion you did not ask for. With `Gravity` zeroed, no bob,
and no muscles, a baked chain sits exactly where it was authored until your game
moves it.

## MusclesAuthoring

| Field | Default | Meaning |
| --- | --- | --- |
| `Strength` | 0.08 | Fraction of the remaining distance the tip closes on the target each tick. Clamped to 0–1. |

Bakes `ChainTarget`, seeded on the tip's own rest position.

`ChainTarget.Position` is **yours to write**, like `CreatureLocomotion` — set it
from your own system before the solve group and the tip is drawn toward it. The
package never writes a target of its own. `Strength` shares that component, so
read-modify-write it rather than assigning a fresh `ChainTarget`, or you will
zero the strength you just authored. See [world facts](world-facts.md).

Composing without this component is what makes a plain rope a plain rope:
no `ChainTarget` exists, so nothing pulls the tip anywhere. That is deliberate —
a target nobody writes would anchor the tip to a stale world point and hold the
creature back as it moved.

## LegsAuthoring

`Legs` is an array of `LegRecipe`:

| Field | Meaning |
| --- | --- |
| `AttachmentPointIndex` | Chain point the leg hangs from. Clamped into range. |
| `LengthA` | Upper bone length. |
| `LengthB` | Lower bone length. |
| `BendSign` | Knee side. Any negative value becomes -1, otherwise +1. |
| `HomeOffset` | Rest foot position relative to the attachment point. A planar creature reads it as x along the heading, y across it. |
| `TripodGroup` | Which alternating tripod this leg belongs to: 0 or 1. Read only by the tripod cadence. |

Bakes the `Limb2BoneLeg` buffer, one entry per recipe.

Without `GaitAuthoring` the limbs still solve every tick — write
`Limb2BoneLeg.Limb.Target` from your own system before the solve group and
two-bone IK will resolve the knee and foot for you. That is the composition to
reach for when the limb is a reaching arm or a grabbing tail rather than a leg.

## GaitAuthoring

Tuning only. Leg count, home offsets, and partner pairing are read from
`LegsAuthoring`, so the gait and limb buffers cannot disagree about how many
legs exist.

| Field | Default | Meaning |
| --- | --- | --- |
| `Comfort` | 0.32 | Distance a planted foot may drift from home before a step is wanted. |
| `StepDuration` | 0.34 | Seconds a swing takes. Minimum 0.001. |
| `StepLead` | 0.12 | Seconds of body velocity aimed ahead of home when no candidate is chosen. |
| `StepHeight` | 0.42 | Peak lift of the swing arc, reached at mid-swing. |
| `MinimumSupport` | 0.7 | Minimum dot product between a candidate normal and world up. Side-view only. |
| `MinimumForward` | 0.03 | Forward progress a candidate must offer to be worth stepping to. |
| `MaximumEvidenceAge` | 2 | How many published frames old a *stamped* candidate may be and still be stepped on. Unstamped candidates are never aged out. |

`MinimumSupport` and `MinimumForward` are the foothold policy: they decide
which candidates gait will accept, which is why a candidate is evidence rather
than a command. At `0.7`, a surface tilted more than about 45° stops counting
as ground.

Bakes `Gait`, the `GaitLeg` buffer, an empty `FootholdCandidate` buffer for your
adapter to fill, and the `FootholdProbe` buffer and `FootholdProbeFrame` the
package publishes each leg's aim into — see
[world facts](world-facts.md#the-probe-frame).

`StepHeight` is world geometry only for a side-view creature. On a movement
plane the swing target stays planar and `StepHeight` becomes an input to
presentation instead — see [top-down creatures](top-down.md).

**Author legs in pairs.** Legs are paired for alternation by index — 0 with 1,
2 with 3, and so on. A leg only begins a step while its partner is planted, and
an unpaired final leg is treated as having a permanently swinging partner, so it
never steps. It will stay planted at its initial position and be dragged along
by the chain. The pairing rule is the `Partner` cadence; a planar creature can
choose a different one.

## PlanarGaitAuthoring

Puts the creature on a top-down movement plane. Adding it changes how the same
gait reads its data rather than adding a second stepping system.

| Field | Default | Meaning |
| --- | --- | --- |
| `InitialForward` | (1, 0) | Heading before the creature first moves. Leg home offsets are authored against it. |
| `MinimumPlantedFeet` | 0 | Feet that must stay planted after a lift is granted. Clamped to leg count - 1. |
| `Cadence` | `Partner` | Which permission rule starts active. |
| `SlowCadence` | `Wave` | Requested at or below `ExitSpeed`. |
| `FastCadence` | `Tripod` | Requested at or above `EnterSpeed`. |
| `EnterSpeed` | 1.2 | Speed that requests `FastCadence`. Forced above `ExitSpeed` at bake. |
| `ExitSpeed` | 0.7 | Speed that requests `SlowCadence`. |
| `WaveOrder` | empty | Crawl order for the wave cadence. Empty bakes every leg once, in index order. |

Bakes `PlanarHeading`, `GaitSupportPolicy`, `GaitCadenceState`, `WaveGaitState`,
the `WaveOrder` buffer, and `GaitRecoveryRequest`.

Set `VerletChainAuthoring.Gravity` to zero alongside it: on a movement plane
there is no down for the body to sag toward.

## ContactPlanesAuthoring

`ContactPlanes` is an array of `ContactPlaneRecipe`:

| Field | Meaning |
| --- | --- |
| `Point` | A point on the plane. |
| `Normal` | Plane normal. Normalized at bake; a zero normal becomes +Y. |
| `Radius` | Chain-point radius held off the plane. |
| `Friction` | Tangential damping on contact. Clamped to 0–1. |

These are a direct one-sided constraint input for static geometry a creature
should not sink through — a floor and a wall, typically. They are independent
of footholds: a contact plane stops the body, a foothold candidate offers a
place to step.

Omitting this component omits the `ContactPlane` buffer entirely. The chain's
final constraint pass still runs; it simply has nothing to collide with.

## What the bakers do not create

No baker writes solver history. Previous point positions start equal to
current, feet start planted at home, no swing is in progress, and no support
relation exists. Everything else is established during the first simulation
tick, and consumers should not construct or mutate it directly.

Each baker reads sibling *authoring* components rather than another baker's
output, which is what keeps them independent of baking order.

The creature entity is created with `TransformUsageFlags.None`: the package
simulates in its own 2D world space and does not read or write the GameObject
transform. Presentation is responsible for mapping resolved points into
whatever transform hierarchy it wants.
