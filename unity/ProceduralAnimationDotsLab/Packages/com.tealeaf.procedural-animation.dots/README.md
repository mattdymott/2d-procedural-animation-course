# Tealeaf Procedural Animation DOTS

An embedded package holding the 2D grounded-appendage runtime extracted from
`ProceduralAnimationDotsLab`.

The package owns the 2D Verlet-chain, gait, contact, and two-bone IK solve
pipeline. The teaching lab is now the `Lab` sample: patrol policy, world-fact
adapters, and line-renderer presentation, all outside the package Runtime.

## Layout

```text
Runtime/        Package-owned solve group, state, helpers, and creature recipe
Editor/         Creature Baker, validation, and derived previews
Tests/Editor/   Solver, gait, and authoring coverage
Samples~/Lab/   The teaching lab: adapters, presentation, and its scenes
Documentation~/ Consumer usage: install, authoring reference, world-fact seam
```

Install the `Lab` sample from the Package Manager to get a runnable scene. In
this repository the imported copy is already committed under
`Assets/Samples/Tealeaf Procedural Animation DOTS/<version>/Lab`, so
`Scenes/Lab.unity` opens and plays from a fresh clone.

## Documentation

[`Documentation~/index.md`](Documentation~/index.md) is the consumer guide and
stands alone: install, the three things a consumer does, ordering, and what the
first release deliberately leaves out. It links an
[authoring reference](Documentation~/authoring-reference.md) and the
[world-fact seam](Documentation~/world-facts.md).

The *why* behind the interface — what moved, what stayed a sample, and what is
deferred until a second consumer — is the extraction contract at
[`../../Documentation~/package-extraction.md`](../../Documentation~/package-extraction.md).
That path is repo-local: the contract lives beside the lab it describes and is
not part of the distributed package.

The rest of this file summarizes the same interface for someone already in the
repository.

## Authoring a creature

Add `ProceduralCreatureAuthoring` to a GameObject and set its chain, gait,
leg, and optional contact-plane recipe. Its Editor-side Baker creates the
complete creature entity: chain/body/target/gait settings plus point, limb,
gait-leg, contact-plane, and foothold-candidate buffers.

The recipe deliberately excludes solver history. Point previous positions,
plants, swing progress, support relations, and carry state are initialized by
the Baker or established during the first simulation tick; consumers should
not construct or mutate them directly.

## Supplying world facts

Before the package solve runs, a consumer writes `CreatureLocomotion` with the
desired root velocity and refreshes the creature's `FootholdCandidate` buffer.
`CreatureLocomotionSystem` applies desired and carry velocity to the private
body state; package systems then integrate the chain, advance gait, and solve
the legs. A terrain, physics, or support adapter may also create entities with
`SupportPose` and `SupportKinematics` for moving and conveyor surfaces.

`ProceduralAnimationSolveSystemGroup` is the fixed-step package entry point.
It owns locomotion, chain, gait, IK, and hard-resolve ordering. Consumers
schedule adapters before this group; they do not schedule individual solvers.

The in-project `PackageConsumer` assembly is a compile-time tracer for this
interface. It references only the package assemblies, authors a creature with
`ProceduralCreatureAuthoring`, and supplies patrol plus flat-ground adapters
without referencing the sample assembly.

The `Lab` sample is the worked example of the same seam with a moving support
and non-flat terrain. See its README for what belongs to a consumer.

## Runtime dependencies

The core package depends only on `com.unity.entities` (and its transitive
Mathematics and Collections dependencies). Physics, tilemaps, rendering, and
input remain consumer or sample concerns.
