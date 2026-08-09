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

`PlanarHeading` is the top-down mode switch: with it on a creature, gait rotates
each leg's home by the body heading, judges footholds by `Walkable` and
`PathClear` instead of a surface normal, and leaves the swing target on the
movement plane. `FootPresentationMath` then derives lift, shadow, and sort key
from that point — a pure function, deliberately not a system, because nothing in
the package may read presentation back.

`GaitPermission` is the selection half of the gait decision (who may ask) and
`GaitStepper` the commitment half (what a permitted leg accepts). Cadences —
partner, support, tripod, wave — differ only in permission; every one of them
shares the same plant, swing, and support-relation implementation.

`SupportPose`, `SupportKinematics`, and `SupportMath` are the package's
world-fact seam for moving and conveyor supports. The maths was always planar,
so support-relative plants, conveyor travel, and liftoff carry work unchanged for
a top-down creature. `FootholdCandidate` is the
compact terrain/physics/custom-world evidence that gait evaluates only at swing
start. `FootholdProbeSystem` closes the loop from the other side: it publishes
each leg's aim as the last step of the solve, so an adapter reads where to look
rather than recombining hip, home offset, heading, and step lead itself — the
one piece of an adapter that has to agree with gait exactly. Candidates stamped
with the frame they were observed against are judged against that same aim, and
gait can tell when evidence has gone stale; unstamped ones behave as they always
did. Consumers write `CreatureLocomotion.DesiredVelocity`, provide those
facts, and read resolved poses; they do not construct or mutate solver history
directly. `CreatureLocomotionSystem` applies the desired velocity and any
package-owned carry velocity at the start of the fixed-step solve.

`ProceduralAnimationSolveSystemGroup` is the consumer-facing fixed-step entry
point. The locomotion, chain, gait, IK, and hard-resolve systems remain
implementation details behind it.
