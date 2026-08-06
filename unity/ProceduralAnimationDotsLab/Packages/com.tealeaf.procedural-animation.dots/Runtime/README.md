# Runtime

`ProceduralCreatureAuthoring` is the supported front door for a complete
creature recipe. Its Editor-side Baker owns the runtime allocation and initial
state. `TwoBoneIk` remains the small, stateless advanced escape hatch.

`SupportPose`, `SupportKinematics`, and `SupportMath` are the package's
world-fact seam for moving and conveyor supports. `FootholdCandidate` is the
compact terrain/physics/custom-world evidence that gait evaluates only at swing
start. Consumers provide those facts and read resolved poses; they do not
construct or mutate solver history directly.
