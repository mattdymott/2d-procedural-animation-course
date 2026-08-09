# The top-down sample omits moving supports; the planar path does not

`PlanarQuerySystem` never fills `FootholdCandidate.Support` or `.SupportLocalPoint`, so every plant in the TopDownLab sample is against static ground. This is a decision about the **sample**, not a limit of the package: the planar path handles moving supports in full, Lesson 21 teaches them, and `PlanarGaitTests.APlanarPlantFollowsItsMovingSupportWithoutAFreshQuery` pins the behaviour on a creature carrying `PlanarHeading`.

We are not adding a platform or conveyor to the top-down sample. It already carries cadence switching, tripods, wave order, blocked regions and recovery; moving supports are demonstrated in the side-view Lab sample, and the plant contract they exercise is the same concept in both projections.

Recorded because the sample reads exactly like an adapter that forgot two fields — an architecture review of this repository made precisely that mistake. `FootholdCandidate` carries facts that any given adapter may leave default; an unpopulated field means "this world has nothing to report", never "this projection cannot use it".
