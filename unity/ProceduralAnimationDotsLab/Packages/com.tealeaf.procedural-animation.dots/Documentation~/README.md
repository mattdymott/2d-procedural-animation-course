# Documentation

The package interface is now exercised by the in-project `PackageConsumer`
tracer: it authors a creature through `ProceduralCreatureAuthoring`, writes
`CreatureLocomotion`, and publishes `FootholdCandidate` world facts without a
reference to the lab runtime assembly.

Consumer adapters run before `ProceduralAnimationSolveSystemGroup`; that group
owns the package's fixed-step simulation order.

The full extraction decision remains in the lab so it can be reviewed
alongside the source it describes.
