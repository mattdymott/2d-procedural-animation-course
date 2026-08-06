# Procedural Animation DOTS Lab

This Unity project is the runnable companion to the browser course. It is deliberately a small,
inspectable vertical slice rather than a production-ready animation package.

The planned extraction is documented in
[`ProceduralAnimationDotsLab/Documentation~/package-extraction.md`](ProceduralAnimationDotsLab/Documentation~/package-extraction.md).
It defines the small package interface before any runtime code moves; the lab remains the first
sample and verification host during that work.

## Current slice

- Lesson 1: a `DynamicBuffer<VerletPoint>` holds position and previous position for a constrained Verlet chain.
- Lesson 2: the endpoint is pulled toward a moving target as a light muscle force.
- Lesson 3: a two-bone analytic leg has a stable bend direction and clamps its foot to the reachable annulus.
- Lesson 4: a pair of legs alternates committed planted and swinging targets before IK solves each pose.
- Lesson 9 (first capstone slice): `CreatureIntent` moves a persistent body root, while the existing gait and IK stages consume that motion.
- Lesson 9 hard resolve: a post-IK pass reasserts the root pin and spine link lengths before presentation reads the pose.
- Lesson 10: `ContactPlane` provides one-sided ground/wall promises; hard resolve interleaves contact projection with distance repair and preserves legal tangential motion.
- Lesson 11: `GroundQuerySystem` gathers terrain candidates; gait filters support, reach, and forward policy, then commits a foothold only when a swing begins.
- Lesson 12: planted feet can store a support entity plus a local plant; `MovingSupportSystem` resolves the world target from the current elevator pose until liftoff.
- Lesson 13: conveyor material travel accumulates in `GaitLeg.SurfaceOffset`; support-point and belt velocity combine into a carry velocity only at liftoff.
- DOTS tick: `VerletChainSystem` runs in `FixedStepSimulationSystemGroup`.
- Presentation: `VerletChainDemo` reads the resolved buffers and draws the chain, both legs, and their targets. A swinging leg turns amber.

Open `ProceduralAnimationDotsLab` with Unity `6000.7.0a3`, then enter Play mode. The demo scene needs no hand-authored setup: it creates its simulation entity, camera, and lines at runtime.

## Next slices

1. Compose the chain and legs into the Lesson 9 lizard, then add terrain, moving supports, and conveyor support from Lessons 10–13.

The project is intentionally excluded from GitHub Pages navigation: the course remains served by the repository root `index.html`.
