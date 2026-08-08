# Procedural Animation DOTS Lab sample

The teaching lab is this package's first consumer. Open `Scenes/Lab.unity` and
press play: a creature walks a ramp, steps onto a rising conveyor, and keeps a
planted foot glued to that surface while it moves.

## Layout

```text
Runtime/          Sample adapters, lesson components, and presentation
Editor/           Bakers for the sample-side authoring components
Tests/EditMode/   A bake-and-tick trace through the package's public interface
Scenes/Lab.unity  Host scene: camera, 2D light, presentation, and the sub scene
Scenes/LabCreature.unity   Sub scene holding all authoring GameObjects
```

## What the sample owns

Everything here is a consumer concern that the package deliberately does not
provide:

- `LabCreaturePatrolSystem` writes `CreatureLocomotion.DesiredVelocity` and
  `ChainTarget` — where the creature walks, and where its tail reaches. Both are
  this creature's character rather than package behaviour; the lesson tail reach
  and sway constants live in that file. They are fractions of the chain's own
  length, not world units, so retuning `RestLength` or the point count cannot
  aim the tail past what it can reach. Real gameplay decides both its own way.
- `GroundQuery` and `GroundQuerySystem` are deterministic lesson terrain. They
  fill the creature's `FootholdCandidate` buffer, preferring the moving support
  when the probe falls inside it. A tilemap, Unity Physics, or a signed-distance
  field would replace this file and nothing else. It reports `Walkable` and
  `PathClear` as true because this terrain has no blocked regions; the side-view
  creature ignores both, but a top-down one would reject on them.
- `MovingSupportSystem` animates the elevator/conveyor and publishes
  `SupportPose` and `SupportKinematics`.
- `VerletChainDemo` draws line renderers from resolved output. Delete it and the
  simulation is unchanged.

The creature itself is composed from the package's authoring components —
`VerletChainAuthoring`, `LegsAuthoring`, `GaitAuthoring`, and
`ContactPlanesAuthoring` — plus the three lab adapter components; the sample
never constructs chain points, plants, or swing state.

## Ordering

The sample's adapters run in `FixedStepSimulationSystemGroup` before
`ProceduralAnimationSolveSystemGroup`, which is the package's only scheduling
seam. `MovingSupportSystem` runs before `GroundQuerySystem` so footholds are
sampled against the support's current pose.

## Source of truth

`Samples~/Lab` in the package is the source of truth. This repository also
commits the imported copy under
`Assets/Samples/Tealeaf Procedural Animation DOTS/<version>/Lab` so a fresh
clone can open the scene and press play without an import step, and so the
sample is proven to work rather than merely stored. Edit the `Samples~` copy
and re-import when the two drift.

Two rules keep that arrangement working:

- Every `.cs` and `.unity` file here keeps its `.meta` beside it. The imported
  copy reuses those GUIDs, which is what lets the host scene keep resolving the
  sub scene and the presentation script across a move or a re-import.
- Package Manager imports into a version-named folder. On a version bump,
  **delete the previous `Assets/Samples/.../<old version>/Lab` before
  re-importing** — two copies mean two `ProceduralAnimationDotsLab.Runtime`
  assemblies and the project will not compile.
