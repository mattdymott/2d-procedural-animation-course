# Editor tests

These tests cover the package through its own authoring and runtime interface,
so they travel with the package rather than with the lab sample.

- `VerletChainSolverTests` and `GaitStepperTests` cover the pure solver and
  gait-decision helpers.
- `CreatureCompositionTests` covers the composable front door: which components
  each authoring combination bakes, that a chain alone still simulates, that
  limbs without gait are consumer-aimed, and that gait stays index-aligned with
  the legs it was derived from.

The project manifest lists this package under `testables` so the Test Runner
discovers them.

End-to-end consumer coverage lives outside the package: `PackageConsumer.Tests`
in the lab project is an independent compile-time consumer that bakes and ticks
through the public interface only.

## No PlayMode assembly

The package deliberately ships no `Tests/Runtime` (PlayMode) assembly. EditMode
already bakes a creature and drives `ProceduralAnimationSolveSystemGroup` for
real ticks, so a PlayMode assembly would repeat that coverage in a slower
harness without testing anything new. The solve is fixed-step and deterministic
and has no dependency on rendering, input, or the player loop.

Add one when a behaviour appears that EditMode cannot express — a bug that only
reproduces across real frames, timing that depends on the actual player loop,
or presentation participating in the simulation rather than reading it. Until
then the absence is the decision, not an omission.
