# Authoring reference

`ProceduralCreatureAuthoring` is the package front door. Attach it to a
GameObject in a sub scene and bake; the result is a complete creature entity.

The component stores designer intent only. Every value below is clamped to a
usable range during baking, so an out-of-range field degrades rather than
producing an invalid creature.

## Chain

| Field | Default | Meaning |
| --- | --- | --- |
| `ChainSegmentCount` | 16 | Number of Verlet points. Minimum 2. |
| `InitialRootPosition` | (-3.5, 0.5) | World position of point 0 at bake time. |
| `LinkLength` | 0.48 | Rest distance between neighbouring points. |
| `Damping` | 0.992 | Velocity retained per step. Clamped to 0–1. |
| `MuscleStrength` | 0.08 | Per-step lerp of the chain tip toward `ChainTarget`. |

Points are laid out along +X from the root at `LinkLength` spacing, and the
chain target starts on the last point.

## Gait

| Field | Default | Meaning |
| --- | --- | --- |
| `Comfort` | 0.32 | Distance a planted foot may drift from home before a step is wanted. |
| `StepDuration` | 0.34 | Seconds a swing takes. Minimum 0.001. |
| `StepLead` | 0.12 | Seconds of body velocity aimed ahead of home when no candidate is chosen. |
| `StepHeight` | 0.42 | Peak lift of the swing arc, reached at mid-swing. |
| `MinimumSupport` | 0.7 | Minimum dot product between a candidate normal and world up. |
| `MinimumForward` | 0.03 | Forward progress a candidate must offer to be worth stepping to. |

`MinimumSupport` and `MinimumForward` are the foothold policy: they decide
which candidates gait will accept, which is why a candidate is evidence rather
than a command. At `0.7`, a surface tilted more than about 45° stops counting
as ground.

A leg only wants to step while its partner is planted, which is what keeps the
gait alternating rather than hopping.

## Legs

`Legs` is an array of `LegRecipe`:

| Field | Meaning |
| --- | --- |
| `AttachmentPointIndex` | Chain point the leg hangs from. Clamped into range. |
| `LengthA` | Upper bone length. |
| `LengthB` | Lower bone length. |
| `BendSign` | Knee side. Any negative value becomes -1, otherwise +1. |
| `HomeOffset` | Rest foot position relative to the attachment point. |

Legs are paired for alternation by index — 0 with 1, 2 with 3, and so on. An
odd final leg has no partner and steps independently.

## Contact planes

`ContactPlanes` is an optional array of `ContactPlaneRecipe`:

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

## What the Baker creates

From the recipe, baking adds to the entity:

- `VerletChain`, `CreatureBody`, `ChainTarget`, `GaitSettings` — configuration
  and the body root.
- `CreatureLocomotion` — zeroed, ready for your adapter to write.
- `VerletPoint` buffer — one per segment, previous position equal to position.
- `Limb2BoneLeg` and `GaitLeg` buffers — one per leg, each foot planted at its
  home offset with its partner assigned.
- `ContactPlane` buffer — one per recipe entry, possibly empty.
- `FootholdCandidate` buffer — empty, for your adapter to fill.

The Baker bakes no solver history. Previous point positions start equal to
current, feet start planted at home, no swing is in progress, and no support
relation exists. Everything else is established during the first simulation
tick.

The creature entity is created with `TransformUsageFlags.None`: the package
simulates in its own 2D world space and does not read or write the GameObject
transform. Presentation is responsible for mapping resolved points into
whatever transform hierarchy it wants.
