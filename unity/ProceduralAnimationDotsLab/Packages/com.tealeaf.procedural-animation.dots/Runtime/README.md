# Runtime

A creature is whichever components its entity carries. `VerletChainAuthoring`,
`LegsAuthoring`, `GaitAuthoring`, and `ContactPlanesAuthoring` are the composable
front door; each has its own Editor-side Baker that owns the runtime allocation
and initial state for that feature alone. `[RequireComponent]` declares the
dependencies between them.

`LowLevel/` holds the package's primitives — pure, stateless static functions
(`TwoBoneIk`, `VerletChainSolver`, `VerletContactSolver`, `SupportMath`,
`CreatureLayout`). They are the advanced escape hatch and the only published
surface below the components.

They deliberately carry no `[BurstCompile]` attribute. Burst compiles them as
part of the `[BurstCompile]` systems that call them, which is what gets them
vectorized; annotating them individually would instead make them direct-call
external functions, and that ABI cannot pass or return `float2` by value.

`SupportPose`, `SupportKinematics`, and `SupportMath` are the package's
world-fact seam for moving and conveyor supports. `FootholdCandidate` is the
compact terrain/physics/custom-world evidence that gait evaluates only at swing
start. Consumers write `CreatureLocomotion.DesiredVelocity`, provide those
facts, and read resolved poses; they do not construct or mutate solver history
directly. `CreatureLocomotionSystem` applies the desired velocity and any
package-owned carry velocity at the start of the fixed-step solve.

`ProceduralAnimationSolveSystemGroup` is the consumer-facing fixed-step entry
point. The locomotion, chain, gait, IK, and hard-resolve systems remain
implementation details behind it.
