# Tealeaf Procedural Animation DOTS

2D grounded procedural appendage animation for Unity DOTS.

The package owns a Verlet-chain body with two-bone legs, an alternating gait
that commits planted feet, contact projection, and the fixed-step order those
pieces need. Your game supplies desired motion and world facts; the package
owns the simulation and publishes resolved poses.

- [Authoring reference](authoring-reference.md) — the creature recipe and what
  the Baker creates from it.
- [World facts](world-facts.md) — the data an adapter writes each tick.

## Requirements

- Unity 6000.0 or newer.
- `com.unity.entities` 6.5.0. Mathematics and Collections come with it.

The runtime has no Unity Physics, tilemap, rendering, or input dependency. Those
stay on your side of the seam.

## Install

Add the package to `Packages/manifest.json`, then install the **Lab** sample
from the Package Manager for a runnable scene that walks a creature over a ramp
and onto a moving conveyor.

To run the package's own tests in your project, list it under `testables`:

```json
{
  "testables": [
    "com.tealeaf.procedural-animation.dots"
  ]
}
```

## The three things a consumer does

### 1. Author a creature

Add `ProceduralCreatureAuthoring` to a GameObject and fill in its chain, gait,
leg, and optional contact-plane recipe. Baking creates the entire creature
entity — configuration, buffers, and initial state.

This is the only supported path to a complete creature. There is no runtime
factory, and hand-building the component set is not a supported alternative:
the recipe is stable designer intent, while previous point positions, plants,
swing progress, and support relations are history the Baker seeds and the
solver owns from the first tick.

See the [authoring reference](authoring-reference.md) for every field.

### 2. Write world facts before the solve

Each fixed step, an adapter writes the creature's `CreatureLocomotion` and
refreshes its `FootholdCandidate` buffer. Moving and conveyor surfaces are
separate entities carrying `SupportPose` and `SupportKinematics`.

```csharp
using Tealeaf.ProceduralAnimation.Dots;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(ProceduralAnimationSolveSystemGroup))]
public partial struct WalkOnFlatGroundSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (locomotion, gaitLegs, limbs, points, candidates) in
                 SystemAPI.Query<
                     RefRW<CreatureLocomotion>,
                     DynamicBuffer<GaitLeg>,
                     DynamicBuffer<Limb2BoneLeg>,
                     DynamicBuffer<VerletPoint>,
                     DynamicBuffer<FootholdCandidate>>())
        {
            locomotion.ValueRW.DesiredVelocity = new float2(0.8f, 0f);

            candidates.Clear();
            var legCount = math.min(gaitLegs.Length, limbs.Length);
            for (var index = 0; index < legCount; index++)
            {
                var hip = points[limbs[index].RootPointIndex].Position;
                var probe = hip + gaitLegs[index].HomeOffset;
                candidates.Add(new FootholdCandidate
                {
                    LegIndex = (byte)index,
                    Point = new float2(probe.x, 0f),   // flat ground at y = 0
                    Normal = new float2(0f, 1f),
                });
            }
        }
    }
}
```

Reading `Limb2BoneLeg.RootPointIndex` and `VerletPoint` here is how an adapter
finds out *where to look* — which is a read of resolved output, not a write to
solver state. `Assets/PackageConsumer` in this repository is the same adapter
with patrol reversal and a configurable ground height, and it compiles against
the package assemblies alone.

A candidate is evidence, not a command. Gait decides whether to accept it and
commits a target only on a planted-to-swinging transition; while a foot is
planted the package tracks the committed support relation instead of querying
again. Publishing a candidate every tick is normal and cheap.

See [world facts](world-facts.md) for the full seam.

### 3. Read the resolved pose

After the solve group runs, `VerletPoint` holds the resolved chain, and
`Limb2BoneLeg.Limb` holds each leg's root, knee, and foot. Read them from
presentation or gameplay; do not write them between package ticks.

`GaitLeg` is visible for debugging and is not part of the stable interface.
Plant, swing progress, support-local coordinates, surface offset, and Verlet
previous positions are implementation detail even when a debug view draws them.

## Ordering

`ProceduralAnimationSolveSystemGroup` updates in `FixedStepSimulationSystemGroup`
and is the package's only scheduling seam. Target the group; its child systems
are internal and marked `DisableAutoCreation`.

```text
FixedStepSimulationSystemGroup
├── your support adapters          write SupportPose / SupportKinematics
├── your locomotion adapter        write CreatureLocomotion
├── your foothold adapter          refresh FootholdCandidate
└── ProceduralAnimationSolveSystemGroup
    ├── apply locomotion and carry velocity to the body root
    ├── integrate and constrain the chain
    ├── advance gait and resolve support-relative plants
    ├── solve two-bone legs
    └── project contacts and publish the resolved pose
```

Order your own adapters relative to each other: sample footholds *after* you
move a support, so candidates are measured against the pose the solver will
use. Presentation runs after the group and never writes solver state.

The package may change its internal systems, jobs, and constraint passes
without changing this contract.

## Advanced escape hatch

`TwoBoneIk.Solve` is a stateless, allocation-free planar IK helper, and
`SupportMath` exposes the same pose and point-velocity maths the gait uses. Both
are public so an advanced caller can reuse the geometry directly. Neither is
required for normal use.

## Not in this release

The first release is deliberately narrow. It does not promise arbitrary 3D
rigs, generic creature taxonomies, or a physics-query implementation, and it
ships no terrain or tilemap adapter — deterministic lesson terrain lives in the
Lab sample and is meant to be replaced wholesale. Those need a second real
consumer before they become package scope.

`ContactPlane` remains an optional direct constraint input rather than a wider
contact-query abstraction, for the same reason.
