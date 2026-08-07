# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-08-07

Initial release. The interface is deliberately narrow and stays at `0.x` until
a second real consumer justifies widening it.

### Added

- `VerletChainAuthoring`, `LegsAuthoring`, `GaitAuthoring`, and
  `ContactPlanesAuthoring`: the composable path to a 2D creature. A creature is
  whichever components its entity carries, dependencies are declared with
  `[RequireComponent]`, and each Baker allocates only its own feature's runtime
  state and bakes no solver history.
- `ProceduralAnimationSolveSystemGroup`: the fixed-step entry point. It owns
  locomotion, chain integration, gait, two-bone IK, and hard-resolve ordering.
  Its child systems are internal implementation detail.
- `CreatureLocomotion`: the consumer-owned desired root velocity, applied at
  the start of each package tick.
- `Gait` and `GaitLeg`: the baked step policy and its per-leg runtime state.
  Baked components are bare-named after the course glossary — `Gait`, not
  `GaitSettings` or Lesson 8's sketched `GaitConfig` — so the contract carries
  one naming rule and no `-Settings`/`-Config` suffixes anywhere.
- `FootholdCandidate`: the terrain, physics, or custom-world evidence seam.
  Gait applies its own support and reach policy and commits a target only on a
  planted-to-swinging transition.
- `SupportPose` and `SupportKinematics`: the moving-support and
  conveyor seam. A planted foot stays fixed in support-local space and receives
  point plus conveyor velocity only at liftoff.
- `MusclesAuthoring` and `ChainTarget`: an opt-in muscle that draws the chain
  tip toward a target the consumer writes. The package invents no target of its
  own, and a chain composed without muscles is never pulled anywhere.
- `ContactPlane`: an optional direct one-sided constraint input for static
  geometry.
- Authored chain motion: `Gravity` and an optional root bob are fields on
  `VerletChainAuthoring` rather than constants inside the solver. A chain with
  zero gravity, no bob, and no muscles stays exactly where it was authored.
- `Tealeaf.ProceduralAnimation.Dots.LowLevel`: the primitives layer and the
  package's whole published escape hatch — `TwoBoneIk`, `VerletChainSolver`,
  `VerletContactSolver`, `SupportMath`, and `CreatureLayout`, all pure and
  stateless. The solve systems are `[BurstCompile]` and call exactly these
  functions, so the primitives are Burst-compiled as part of them.
- `Lab` sample: an authored creature, deterministic lesson terrain, a moving
  support, line-renderer presentation, two scenes, and a bake-and-tick test
  through the public interface.
- `Documentation~`: usage, authoring reference, and the world-fact seam.
