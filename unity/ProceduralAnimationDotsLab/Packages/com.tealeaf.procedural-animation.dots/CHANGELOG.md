# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-08-07

Initial release. The interface is deliberately narrow and stays at `0.x` until
a second real consumer justifies widening it.

### Added

- `ProceduralCreatureAuthoring` and its Baker: the only supported path to a
  complete 2D creature. The recipe carries chain, gait, leg, and optional
  contact-plane intent; the Baker allocates all runtime state and bakes no
  solver history.
- `ProceduralAnimationSolveSystemGroup`: the fixed-step entry point. It owns
  locomotion, chain integration, gait, two-bone IK, and hard-resolve ordering.
  Its child systems are internal implementation detail.
- `CreatureLocomotion`: the consumer-owned desired root velocity, applied at
  the start of each package tick.
- `FootholdCandidate`: the terrain, physics, or custom-world evidence seam.
  Gait applies its own support and reach policy and commits a target only on a
  planted-to-swinging transition.
- `SupportPose`, `SupportKinematics`, and `SupportMath`: the moving-support and
  conveyor seam. A planted foot stays fixed in support-local space and receives
  point plus conveyor velocity only at liftoff.
- `ContactPlane`: an optional direct one-sided constraint input for static
  geometry.
- `TwoBoneIk`: a stateless, allocation-free planar IK helper for advanced
  callers.
- `Lab` sample: an authored creature, deterministic lesson terrain, a moving
  support, line-renderer presentation, two scenes, and a bake-and-tick test
  through the public interface.
- `Documentation~`: usage, authoring reference, and the world-fact seam.
