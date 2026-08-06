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

Each step leaves the lab runnable. The package is ready to publish only after a
fresh consumer can author a creature and provide its own world facts without
referencing the sample assembly.

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
