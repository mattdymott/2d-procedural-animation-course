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
- One composable authoring layer plus a `LowLevel` namespace of pure,
  `[BurstCompile]` geometry helpers for advanced callers.

It does not promise arbitrary 3D rigs, generic creature taxonomies, or a
physics-query implementation. Those need a second real consumer before they
become package scope.

## Interface

### Composed authoring

The package front door is a set of composable authoring components rather than
one recipe: `VerletChainAuthoring`, `LegsAuthoring`, `GaitAuthoring`, and
`ContactPlanesAuthoring`. A creature is whichever of them its GameObject
carries, and `[RequireComponent]` declares the dependencies between them.

Each has its own Baker, and each Baker copies only stable designer intent for
its own feature into configuration:

- chain segment count, rest length, damping, and constraint settings;
- leg attachment indices, bone lengths, bend direction, and home offsets;
- gait comfort, duration, lead, height, and foothold policy.

Gait carries tuning only. Leg count, home offsets, and partner pairing are read
back from `LegsAuthoring`, so the gait and limb buffers are index-aligned by
construction rather than by convention.

Bakers read sibling *authoring* components, never another Baker's output, which
is what makes them independent of baking order.

The Bakers allocate and initialize runtime state separately. They never bake a
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
are authored in a sub scene with the package's four authoring components plus
three small lab adapter components, so no consumer code constructs chain points,
plants, or swing state. `VerletChainDemo` is read-only presentation bound to baked
entities, and `LabSampleBakingTests` traces the whole sample through the public
interface. The repository also commits the imported copy under
`Assets/Samples/…` so a clone can open `Scenes/Lab.unity` and press play;
`Samples~/Lab` remains the source of truth.

## What moves and what remains a sample

| Current lab responsibility | Package destination | Notes |
| --- | --- | --- |
| `VerletChainSolver`, `VerletContactSolver`, `TwoBoneIkSolver` | `Runtime/LowLevel`, public and `[BurstCompile]` | Geometry helpers are the advanced escape hatch. |
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
3. Add the authoring components and their Bakers. Make authoring the only
   supported path for creating a creature.
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

## Alignment pass against Lesson 8

A later review compared the shipped package against the course's Lesson 8
design. Four gaps were closed:

**Composition.** Lesson 8's one named principle is that an entity is a creature
by its components. The first release contradicted it: a single
`ProceduralCreatureAuthoring` baked gait, limbs, contacts, and foothold buffers
unconditionally, so "a chain without gait" was not expressible. The recipe is now
split into four authoring components with `[RequireComponent]` dependencies. The
`HardResolveSystem` reaches creatures with no `ContactPlane` buffer through a
`BufferLookup`, so making contacts optional did not quietly drop the final
constraint pass from every other creature.

**A primitives layer.** Lesson 8 specifies pure static Burst functions under
their own namespace. `Tealeaf.ProceduralAnimation.Dots.LowLevel` now holds them,
and the five solve systems are `[BurstCompile]` so the primitives run in Burst
on the real path rather than only being annotated.

**Escape-hatch honesty.** `VerletChainSolver`, `VerletContactSolver`, and
`GaitStepper` were public but undocumented, so the "free to change below the
contract" promise did not actually cover them. The geometry helpers are now
documented published surface; `GaitStepper` is `internal` (with
`InternalsVisibleTo` for the package tests) because it is stateful policy, as is
`VerletChainSolver.ResolveRoot`.

**Glossary sync.** `VerletChain.LinkLength` is now `RestLength`, matching the
course glossary. The authoring field carries `[FormerlySerializedAs]`.

**Lab constants removed from the runtime.** `VerletChainSolver.ResolveRoot`
applied a hardcoded sine bob, and `VerletChainSystem` drove the chain tip toward
a hardcoded endpoint `+6.5` units away under a hardcoded gravity of `-3.5`. Once
any composition became expressible, those constants applied to every creature: a
plain rope waved like the lesson tentacle and was stretched toward a target
beyond its own length.

Gravity and the root bob are now authored fields on `VerletChainAuthoring`, with
gravity defaulting to the previous value and the bob defaulting to *off* — the
package invents no motion that was not asked for. The tip target is no longer
computed at all. It moved behind a new `MusclesAuthoring` component that bakes
`ChainTarget`, which the consumer writes each tick exactly like
`CreatureLocomotion`.

Making muscles opt-in rather than always-baked is what avoids the obvious
alternative's bug: a `ChainTarget` nobody writes would anchor the tip to a stale
bake-time world point and hold the creature back as it walked. `VerletChainSystem`
therefore reaches the target through a `ComponentLookup` rather than requiring it
in the query — the same shape as the `ContactPlane` fix, and for the same reason.
The lesson sway moved into `LabCreaturePatrolSystem`, where it belongs.

### Follow-up pass: play mode, naming, and the last Lab constant

The alignment pass was verified structurally — tests, baking, Burst, scene
resolution — but nobody had watched the Lab creature actually run since the sway
moved out of the package. Play mode found the one thing those checks cannot see.

**The `+6.5` tip target survived as a literal.** The package stopped computing a
tip target, but `LabCreaturePatrolSystem` inherited the constant verbatim as
`TailReach = 6.5`, and the Lab scene has since been retuned to a 20-point chain
at `RestLength 0.23` — 4.37 units of reach. The tail was therefore aimed 50%
beyond its own length: measured live, the chain sat at 106% extension, taut and
over-stretched every frame, so it rendered as a rigid rod and the sway was
almost entirely lost. This is precisely the failure the extraction removed from
the runtime, re-created one layer up by a world-unit constant outliving the
chain it was tuned against.

The reach and sway amplitude are now fractions of the chain's own length
(`0.9` and `0.18` of `span = (points - 1) x RestLength`), which makes the fix a
bound rather than a tuning. The pinned point sits at the root plus the bob and
the target at the root plus reach and sway, so the worst case — sway and bob
opposing — is `sqrt((0.9 span)^2 + (0.18 span + bobAmplitude)^2)`, and that is
under `span` for any chain long enough to matter. It cannot be pushed past 100%
by retuning `RestLength` or the point count, which is exactly what the world-unit
constants could not promise.

Against the shipped 16-point, `0.48` chain the fractions reproduce the old 6.5
and 1.3 to within 0.3% (6.48 and 1.296), and the bound sits at 93% — the same
place the old constants put it. Against the retuned 20-point, `0.23` scene the
bound is 94%; measured live it ran at 90–93% with the chain riding at 83–92%
extension, curving. The old constants against that same scene sat at 153%.

Only the retuned scene was run: `Samples~` is not compiled, so the shipped
chain's numbers above are arithmetic, not observation.

The lesson generalises past this sample: **a consumer constant expressed in
world units is a hidden dependency on authored data.** Where a package exposes
the authored quantity — here `RestLength` and the point count — consumer policy
should be written against it.

**Naming.** `GaitSettings` is now `Gait`. Lesson 8's code sketch says
`GaitConfig`, but it names its whole component family with a `-Config` suffix
(`VerletChainConfig`, `Limb2BoneConfig`, `MusclesConfig`) that this package
rejected wholesale, and Lesson 8 itself designates the course glossary as the
package's naming spec — where the term is bare `Gait`. Every other baked
component here is bare too: `VerletChain`, `Limb2Bone`, `ChainTarget`,
`GaitLeg`, `SupportPose`, `ContactPlane`, `FootholdCandidate`. `GaitConfig`
matched neither authority and would have been the package's only suffixed
component. No scene stores the component by name, so the rename touched code and
docs only.

Lesson 4's own snippet still reads `GaitSettings`. It is the reader's
scratch-project code rather than the package's, so it was left alone — but the
course now spells the concept three ways across L4, L8, and the glossary, and
the glossary is the one the package follows.

### Still open

`VerletChain.Time` is retained. It exists only to phase the root bob, so a
creature that leaves the bob at zero carries the float for nothing — but the Lab
creature authors `RootBobAmplitude 0.35` at `0.9 Hz` and the bob is visibly
applied in play mode, so the feature it serves is in use by the package's own
worked example. The alternative, an optional consumer-written root-offset
component, remains more API than one float is worth. Revisit only if a real
second consumer leaves the bob off.

The Lesson 8 playground briefs that need FABRIK chains and grab targets are still
out of scope, as `Documentation~/index.md` declares. That gate is a scope
decision — how much a `0.x` package should promise — not a missing
implementation, and it should stay closed until a second real consumer exists.

Second-order dynamics has since crossed that line, but only as far as it had to.
`LowLevel.SecondOrderMath` exists because Lessons 26–28 need a response filter,
and it arrived the way `PlanarMath` and `FootPresentationMath` did: a pure
function a consumer calls, with no component, no system, and no place in the
tick. The playground brief — a juice component the package owns and updates for
you — remains closed. `Assets/PackageConsumer` is a compile-time tracer and the Lab is the
worked example; neither counts.
