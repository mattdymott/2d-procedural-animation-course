# Mission: 2D Procedural Animation with Unity DOTS

## Why
Build Rain World-style creatures — soft, bendy, lifelike bodies that move believably through any space — plus juicy procedural secondary motion at ECS scale, and distill the techniques into a reusable DOTS package (chains, springs, IK, deformation) for future 2D games.

## Success looks like
- Simulate a verlet-chain creature (body, tail, limbs) as Burst-compiled ECS systems, comfortably running 1000+ instances.
- Apply second-order-dynamics "juice" (squash/stretch, lean, overshoot, follow-through) procedurally to arbitrary entities.
- Solve 2-bone analytic IK and n-joint FABRIK in 2D for feet/hands that plant and reach believably.
- Ship a reusable procedural-animation package with a clean component/system API that future projects consume.

## Constraints
- Already fluent in DOTS (ISystem, Burst, jobs, buffers) — teach the animation techniques, not ECS plumbing.
- Maths as intuition + recipes; skip formal derivations.
- Lessons are HTML run in a browser; the real proving ground is the user's Unity projects.

## Out of scope
- 3D procedural animation and humanoid rigs.
- Rendering internals (shaders, sprite skinning) except where needed to visualize results.
- Unity's built-in GameObject-based 2D IK / Animation Rigging packages.
