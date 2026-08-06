# Editor tests

These tests cover the package through its own authoring and runtime interface,
so they travel with the package rather than with the lab sample.

- `VerletChainSolverTests` and `GaitStepperTests` cover the pure solver and
  gait-decision helpers.
- `ProceduralCreatureAuthoringTests` covers the `ProceduralCreatureAuthoring`
  front door and its Baker.

The project manifest lists this package under `testables` so the Test Runner
discovers them.

End-to-end consumer coverage lives outside the package: `PackageConsumer.Tests`
in the lab project is an independent compile-time consumer that bakes and ticks
through the public interface only.
