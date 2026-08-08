# Changelog

All notable changes to this package are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this package adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `PlanarGaitAuthoring`: moves an existing gait onto a top-down movement plane.
  Leg home offsets become heading-relative, footholds are judged by walkability
  rather than a floor normal, and the swing target stays planar. Presence of the
  baked `PlanarHeading` is the mode switch, so a creature without it keeps the
  side-view behaviour unchanged.
- Gait cadences — `Partner`, `Support`, `Tripod`, and `Wave` — as one permission
  rule each over the same plant and swing implementation. `GaitSupportPolicy`
  carries the minimum planted base and the speed thresholds; `GaitCadenceState`
  applies a requested change only when no foot is in the air, so switching
  policy never rewrites a foot that has already promised.
- `WaveGaitState` and the `WaveOrder` buffer: an authored crawl order whose
  cursor advances only when the leg it names lands.
- `GaitRecoveryRequest`: gait's semantic hand-off when a permitted leg has no
  legal foothold. It keeps the plant and the cursor, and asks locomotion to slow
  or turn instead of inventing a target.
- `FootholdCandidate.Walkable` and `.PathClear`: the two facts a planar query
  adapter reports. Read only by a top-down creature, so existing adapters need
  no change.
- `CreatureLocomotion.DesiredHeading`: an optional facing independent of travel,
  which is what lets a top-down creature turn on the spot.
- `CreatureLocomotion.RequestedTurnSign`: a turn your locomotion has decided on
  but not yet resolved, published so presentation can wind the body up before
  the heading moves. No package system reads it, so writing it never steers.
- `LegsAuthoring.LegRecipe.TripodGroup`: which diagonal tripod a leg belongs to.
- `PlanarMath` and `FootPresentationMath` in `LowLevel`: heading-relative homes,
  and the lift, shadow, and sort key derived from one resolved planar foot point.
- `SecondOrderMath` in `LowLevel`, with `SecondOrderTuning`, `SecondOrderFloat`,
  and `SecondOrderFloat2`: the spring-damper response filter. Its stability
  clamp is derived per call rather than baked, so the same filter is correct on
  a fixed step and on a variable presentation delta.
- `BodyPresentationMath` in `LowLevel`, with `BodyPresentationPolicy`,
  `BodyPresentationState`, and `BodyPresentation`: top-down body language —
  bank into a resolved turn, stretch with resolved speed, a short wind-up
  against a requested turn, and a weight shift under resolved acceleration.
  It reads resolved output and returns a picture; nothing in the package reads
  it back, and its previous-frame state lives in the presentation struct rather
  than on locomotion or gait.

### Changed

- Gait now runs as a whole-creature selection pass followed by a per-leg
  commitment, which is what lets a rule reason about the base a lift would leave
  behind. The single-leg `GaitStepper.Update` entry points and the side-view
  partner guard behave as before.

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
