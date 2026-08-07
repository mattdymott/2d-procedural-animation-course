# Procedural Animation DOTS Package Extraction

## Decision

Extract a focused **2D grounded-appendage runtime**, not the whole teaching
lab. The future package name is `com.tealeaf.procedural-animation.dots`.

The package module accepts a baked creature recipe and compact world facts. It
owns the mutable simulation, solve ordering, planted-foot commitments, and
resolved poses. A game supplies locomotion, terrain/physics samples, and
support motion through adapters; it does not run or repair the solvers itself.

This keeps the package interface small while preserving the useful behaviour
already proven in the lab: Verlet bodies, contact projection, two-bone legs,
alternating gait, ground-aware footholds, moving supports, and conveyors.

## Scope of the first package

The first release is deliberately narrow:

- 2D (`float2`) only.
- A Verlet chain with two-bone legs and grounded stepping.
- Entity Component System and Mathematics dependencies only; no Unity Physics,
  tilemap, rendering, or input dependency in the runtime.
- One high-level authoring path plus a small set of pure geometry helpers for
  advanced callers.

It does not promise arbitrary 3D rigs, generic creature taxonomies, or a
physics-query implementation. Those need a second real consumer before they
become package scope.

## Interface

### Authored recipe

`ProceduralCreatureAuthoring` is the package front door. Its Baker copies only
stable designer intent into configuration:

- chain segment count, rest length, damping, and constraint settings;
- leg attachment indices, bone lengths, bend direction, and home offsets;
- gait comfort, duration, lead, height, and foothold policy.

The Baker allocates and initializes runtime state separately. It never bakes a
previous point position, current plant, swing phase, support relation, or
solver iteration. These are all history created after simulation starts.

### World facts supplied by adapters

Adapters populate data before the package's fixed-step solve group runs.
They may come from a tilemap, Unity Physics, a custom signed-distance field, or
a hand-authored game world.

```csharp
public struct FootholdCandidate : IBufferElementData
{
    public byte LegIndex;
    public float2 Point;
    public float2 Normal;
    public Entity Support;
    public float2 SupportLocalPoint;
}

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

`FootholdCandidate` is evidence, not a command. Gait owns the decision to
accept it and commits the target only on a planted-to-swinging transition.
While a foot is planted, the package uses the committed support relation rather
than querying again. `SupportPose` and `SupportKinematics` make moving,
rotating, and conveyor supports one compact data seam.

Use data rather than an `IFootholdSource` interface: the hot loop remains
Burst-friendly, and the existing static-ground and moving-support paths are
already two distinct adapters at that seam.

### Locomotion input and resolved output

The game owns desired body motion. The package consumes a root pose/velocity
input at the start of its tick; it must not require `CreatureIntent` or impose
the lab's patrol behaviour. The package owns its output pose buffers. Rendering
and gameplay may read them, but must not write them between package ticks.

The initial public diagnostics are read-only data for resolved chain points,
leg root/knee/foot positions, and foot state. Foot plant, swing progress,
support-local coordinates, surface offset, and Verlet previous positions stay
runtime implementation details even when a debug view displays them.

### Ordering contract

`ProceduralAnimationSolveSystemGroup` runs in the fixed-step group. Before it
runs, adapters update locomotion input, foothold candidates, support pose, and
support kinematics. Inside it, the package owns the order:

1. integrate and constrain the chain;
2. choose/advance gait and resolve existing support-relative plants;
3. solve two-bone leg poses;
4. project contacts and publish the resolved pose.

The package may change internal systems, jobs, or constraint passes without
changing the interface. Presentation runs afterwards and never writes solver
state.

## Current extraction progress

The first functional migration is complete: `TwoBoneIkRequest`,
`TwoBoneIkPose`, and `TwoBoneIk.Solve` now live in the package Runtime
assembly. The lab's `TwoBoneIkSystem` and its focused IK tests consume that
interface directly. This establishes a real package-to-lab dependency before
the higher-level, stateful simulation is moved.

`SupportPose`, `SupportKinematics`, and `SupportMath` are also now package
Runtime types. The lesson elevator/conveyor writes them through its
`DemoMovingSupport` adapter; gait, terrain discovery, and presentation consume
the package seam without depending on the demo animator.

`FootholdCandidate` is now the package terrain/physics/custom-world seam. The
lab's deterministic `GroundQuery` writes candidate evidence, while its probe
markers use a separate `GroundQueryDebugHit` buffer that remains sample-only.

The core creature chain, contact, limb, gait, and body state now also live in
the package Runtime assembly. The lab retains only its `CreatureIntent`,
moving-support animation, terrain adapter, and debug presentation state.

The package now also owns Verlet distance repair, one-sided contact projection,
the hard-resolve pass, and the `ProceduralAnimationSolveSystemGroup`. The
group contains locomotion, chain integration, gait, IK, and hard resolve in
that order; callers target the group rather than its internal systems. The
fresh `PackageConsumer` tracer supplies patrol and flat-ground adapters without
referencing the lab runtime assembly.

The solver, gait, and authoring tests now live in the package's own
`Tests/Editor` assembly, and the project manifest lists the package under
`testables`.

The lab is now the package's `Samples~/Lab` sample. Its creature and elevator
are authored in a sub scene with `ProceduralCreatureAuthoring` plus three small
lab adapter components, so no consumer code constructs chain points, plants, or
swing state. `VerletChainDemo` is read-only presentation bound to baked
entities, and `LabSampleBakingTests` traces the whole sample through the public
interface. The repository also commits the imported copy under
`Assets/Samples/…` so a clone can open `Scenes/Lab.unity` and press play;
`Samples~/Lab` remains the source of truth.

## What moves and what remains a sample

| Current lab responsibility | Package destination | Notes |
| --- | --- | --- |
| `VerletChainSolver`, `VerletContactSolver`, `TwoBoneIkSolver` | Runtime implementation; selected pure helpers stay public | Geometry helpers are the advanced escape hatch. |
| `VerletChainSystem`, `GaitSystem`, `TwoBoneIkSystem`, `HardResolveSystem` | Runtime implementation inside the solve group | Ordering is package-owned. |
| `GroundHit` | Public `FootholdCandidate` | Rename it because terrain is only one source of a foothold. |
| `SupportPose`, support transform/velocity math | Public support-data seam | Split animation inputs from evaluated kinematics. |
| `GaitLeg`, `VerletPoint`, `Limb2BoneLeg` | Runtime state and resolved-output data | Do not require callers to construct mutable solver buffers. |
| `CreatureIntent`, `CreatureBody` | Replace with package locomotion input | The demo patrol policy is not reusable behaviour. |
| `GroundQuery`, `GroundQuerySystem` | Sample adapter | It is deterministic lesson terrain, not package terrain. |
| `MovingSupportSystem` | Sample adapter | It animates one lesson elevator/conveyor. |
| `VerletChainDemo` and line renderers | Sample presentation | The first consumer of the extracted package. |

`ContactPlane` remains an optional direct constraint input in the first
release. It has a real independent use today and does not need a wider query
abstraction until there are multiple contact providers.

## Extraction sequence

1. Create the embedded package with Runtime, Editor, Tests, and Samples~
   assemblies plus package metadata.
2. Move pure geometry and package-owned systems behind the solve group, keeping
   their existing tests green.
3. Add `ProceduralCreatureAuthoring` and its Baker. Make it the only supported
   path for creating a complete creature.
4. Rename and introduce the world-fact data seam, then refactor the current
   ground and support code into the first sample adapters.
5. Move `VerletChainDemo` and the lesson scene into Samples~, and add one
   end-to-end bake-and-tick test through the public interface.

All five steps are done. Each step left the lab runnable.

## Packaging decisions after extraction

The interface work is finished; what followed was packaging polish, recorded
here so the reasoning survives.

**Version stays `0.1.0`.** The scope above is deliberately narrow and defers 3D,
generic taxonomies, and physics queries, which is a `0.x` shape rather than a
`1.0` one. The version also names the committed sample import folder
(`Assets/Samples/Tealeaf Procedural Animation DOTS/0.1.0/Lab`), so a bump is a
delete-and-reimport, not an edit.

**Consumer documentation moved into the package; this contract did not.** The
package's `Documentation~/` now stands alone for someone who installs from a
registry and never sees this lab: install, authoring reference, and the
world-fact seam. This file stays beside the source it describes, and the
package README references it as an explicitly repo-local pointer.

**No `Tests/Runtime` (PlayMode) assembly.** EditMode already bakes and ticks the
real solve group, which is fixed-step, deterministic, and independent of
rendering, input, and the player loop. A PlayMode assembly would repeat that
coverage more slowly. It becomes worth adding when a behaviour only reproduces
across real frames or once presentation participates in the simulation rather
than reading it.

**A second real consumer remains open**, and it is the gate on widening scope.
`Assets/PackageConsumer` is a compile-time tracer that proves the interface is
usable without the sample, and the `Lab` sample is the worked example — neither
is a second independent game exercising the package for its own reasons. Until
one exists, 3D, generic creature taxonomies, a physics-query implementation,
and a wider contact abstraction stay out of scope.

## Acceptance checks

- A new scene creates a walking creature using only the package's authoring
  module and Runtime assembly.
- Replacing static terrain with a support adapter changes no gait or solver
  code.
- A planted foot remains stable in support-local space and receives point plus
  conveyor velocity only at liftoff.
- Presentation can be removed without changing the simulation result.
- The existing pure solver and gait tests remain green, alongside an
  end-to-end public-interface bake-and-tick test.
