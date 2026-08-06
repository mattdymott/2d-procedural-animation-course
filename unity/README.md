# Procedural Animation DOTS Lab

This Unity project is the runnable companion to the browser course. It is deliberately a small,
inspectable vertical slice rather than a production-ready animation package.

## Current slice

- Lesson 1: a `DynamicBuffer<VerletPoint>` holds position and previous position for a constrained Verlet chain.
- Lesson 2: the endpoint is pulled toward a moving target as a light muscle force.
- DOTS tick: `VerletChainSystem` runs in `FixedStepSimulationSystemGroup`.
- Presentation: `VerletChainDemo` reads the resolved buffer and draws it with a `LineRenderer`.

Open `ProceduralAnimationDotsLab` with Unity `6000.7.0a3`, then enter Play mode. The demo scene needs no hand-authored setup: it creates its simulation entity, camera, and lines at runtime.

## Next slices

1. Add an asymmetric two-bone leg and target visualisation (Lesson 3).
2. Drive paired legs through the committed plant/swing state machine (Lesson 4).
3. Compose the chain and legs into the Lesson 9 lizard, then add terrain, moving supports, and conveyor support from Lessons 10–13.

The project is intentionally excluded from GitHub Pages navigation: the course remains served by the repository root `index.html`.
