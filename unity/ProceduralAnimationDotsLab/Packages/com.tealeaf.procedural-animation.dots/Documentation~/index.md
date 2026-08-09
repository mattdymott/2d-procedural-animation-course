# Tealeaf Procedural Animation DOTS

2D grounded procedural appendage animation for Unity DOTS.

The package owns a Verlet-chain body with two-bone legs, an alternating gait
that commits planted feet, contact projection, and the fixed-step order those
pieces need. Your game supplies desired motion and world facts; the package
owns the simulation and publishes resolved poses.

- [Authoring reference](authoring-reference.md) — the creature recipe and what
  the Baker creates from it.
- [World facts](world-facts.md) — the data an adapter writes each tick.
- [Top-down creatures](top-down.md) — the same rules on a movement plane:
  heading-relative homes, walkable footholds, insect cadences, and drawn lift.

## Requirements

- Unity 6000.0 or newer.
- `com.unity.entities` 6.5.0. Mathematics and Collections come with it.

The runtime has no Unity Physics, tilemap, rendering, or input dependency. Those
stay on your side of the seam.

## Install

Add the package to `Packages/manifest.json`, then install the **Lab** sample
from the Package Manager for a runnable scene that walks a creature over a ramp
and onto a moving conveyor. The **TopDownLab** sample is the same seam on a
movement plane — see [top-down creatures](top-down.md).

To run the package's own tests in your project, list it under `testables`:

```json
{
  "testables": [
    "com.tealeaf.procedural-animation.dots"
  ]
}
```

## The three things a consumer does

### 1. Compose a creature

A creature is not a type — it is whichever components its entity carries. Add
the authoring components for the behaviour you want:

| Component | Gives you | Requires |
| --- | --- | --- |
| `VerletChainAuthoring` | A chain body and its root | — |
| `MusclesAuthoring` | Draws the chain tip toward a target you write | `VerletChainAuthoring` |
| `LegsAuthoring` | Two-bone limbs | `VerletChainAuthoring` |
| `GaitAuthoring` | Alternating stepping | `LegsAuthoring` |
| `PlanarGaitAuthoring` | Moves that stepping onto a top-down movement plane | `GaitAuthoring` |
| `ContactPlanesAuthoring` | Static planes the body cannot sink through | `VerletChainAuthoring` |

```text
VerletChain                                   a rope or hanging tail
VerletChain + Muscles                         a tentacle reaching for a target you write
VerletChain + Legs                            limbs you aim yourself
VerletChain + Legs + Gait                     a walking creature
VerletChain + Legs + Gait + ContactPlanes     a walking creature with a floor
VerletChain + Legs + Gait + PlanarGait        a top-down creature on a movement plane
```

The dependencies are declared with `[RequireComponent]`, so adding
`GaitAuthoring` to a bare GameObject pulls in legs and a chain for you, and the
defaults already walk. Each baker owns only its own feature's data.

Authoring is the supported path to a creature. There is no runtime factory, and
hand-building the component set is not a supported alternative: the recipes are
stable designer intent, while previous point positions, plants, swing progress,
and support relations are history the bakers seed and the solver owns from the
first tick.

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

### Reading the published aim instead

Working out *where to look* is the fiddly half of an adapter, and it is the half
the package already knows: it has to combine hip, home offset, heading, and step
lead the same way gait does. So the package publishes the answer. Reading it
makes the adapter shorter and removes the chance of the two disagreeing:

```csharp
foreach (var (frame, probes, candidates) in
         SystemAPI.Query<
             RefRO<FootholdProbeFrame>,
             DynamicBuffer<FootholdProbe>,
             DynamicBuffer<FootholdCandidate>>())
{
    var mutableCandidates = candidates;
    mutableCandidates.Clear();
    if (frame.ValueRO.FrameId == 0u)
        continue;               // nothing published yet, on the very first tick

    for (var index = 0; index < probes.Length; index++)
    {
        if (probes[index].Valid == 0)
            continue;           // this leg has no hip to measure from

        var aim = probes[index].PredictedHome;
        mutableCandidates.Add(new FootholdCandidate
        {
            LegIndex = (byte)index,
            Point = new float2(aim.x, 0f),   // flat ground at y = 0
            Normal = new float2(0f, 1f),
            ObservedFrame = frame.ValueRO.FrameId,
        });
    }
}
```

Stamping `ObservedFrame` is what lets gait tell fresh evidence from stale. Leave
it unset and your candidates are judged against the live body exactly as in the
first example — both forms are supported, and the samples ship one of each:
`TopDownLab` reads the frame, the side-view `Lab` derives its own.

A candidate is evidence, not a command. Gait decides whether to accept it and
commits a target only on a planted-to-swinging transition; while a foot is
planted the package tracks the committed support relation instead of querying
again. Publishing a candidate every tick is normal and cheap.

See [world facts](world-facts.md) for the full seam.

### 3. Read the resolved pose

After the solve group runs, `VerletPoint` holds the resolved chain, and
`Limb2BoneLeg.Limb` holds each leg's root, knee, and foot. Read them from
presentation or gameplay; do not write them between package ticks.

`Limb2Bone.Target` is the one exception, and who owns it depends on what you
composed. With `GaitAuthoring` present, gait writes the target every tick and
your writes are overwritten — treat it as output. Without gait, nothing in the
package writes it, so it is yours: set it before the solve group and two-bone IK
will resolve the knee and foot for you. That is what makes a limb without a gait
a reaching arm or a grabbing tail. Everything else on `Limb2BoneLeg` —
`Root`, `Knee`, `Foot`, `RootPointIndex` — is resolved output either way.

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
├── your foothold adapter          read FootholdProbe, refresh FootholdCandidate
└── ProceduralAnimationSolveSystemGroup
    ├── apply locomotion and carry velocity to the body root
    ├── integrate and constrain the chain
    ├── advance gait and resolve support-relative plants
    ├── solve two-bone legs
    ├── project contacts and publish the resolved pose
    └── publish FootholdProbe: where each leg aims next
```

Order your own adapters relative to each other: sample footholds *after* you
move a support, so candidates are measured against the pose the solver will
use. Presentation runs after the group and never writes solver state.

The probe is published last, so the aim your adapter reads was measured against
the previous solve. That is deliberate: gait judges a stamped candidate against
the same aim you offered around it, which it cannot do while each side derives
its own from a body that moves in between. You need no ordering attribute for
it — the fact is already there when your adapter runs.

The package may change its internal systems, jobs, and constraint passes
without changing this contract.

## Advanced escape hatch

`Tealeaf.ProceduralAnimation.Dots.LowLevel` holds the package's primitives:
pure, stateless static functions with no entity or system dependencies. They are
Burst-compatible and compile into whichever Bursted system calls them — the
package's own solve systems are `[BurstCompile]`, so that is the path they take
in normal use. They are the whole of the published escape hatch — anything not
listed here is implementation detail, whatever its C# accessibility.

| Type | What it does |
| --- | --- |
| `TwoBoneIk.Solve` | Analytic planar two-bone IK, clamped to the reachable annulus |
| `VerletChainSolver.Pin` / `.SatisfyDistance` | Verlet point pinning and one distance constraint |
| `VerletContactSolver.ProjectAgainstPlane` | One-sided contact projection with friction |
| `SupportMath` | Support-local transforms and point velocity, including conveyor travel |
| `PlanarMath` | Heading-relative homes and the facing rule behind them |
| `FootPresentationMath.Derive` | Turns one planar foot point into lift, shadow, and sort key |
| `CreatureLayout.PointPosition` | Rest position of a chain point, shared by the bakers |

None of them is required for normal use. The high-level components call exactly
these functions, so going off-road costs you no fidelity.

`GaitStepper` and `GaitPermission`, the gait decision policy, are deliberately
*not* here — they are stateful policy rather than geometry, and free to change.

## Not in this release

The first release is deliberately narrow. It does not promise arbitrary 3D
rigs, generic creature taxonomies, or a physics-query implementation, and it
ships no terrain or tilemap adapter — deterministic lesson terrain lives in the
Lab sample and is meant to be replaced wholesale. Those need a second real
consumer before they become package scope.

`ContactPlane` remains an optional direct constraint input rather than a wider
contact-query abstraction, for the same reason.
