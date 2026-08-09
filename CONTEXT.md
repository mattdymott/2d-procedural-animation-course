# Procedural Creature Locomotion

A course, and the DOTS package it teaches, for animating walking creatures procedurally in 2D — no keyframes, no animation clips. A creature's body moves, and its legs work out where to stand.

## Language

### Projections

**Projection**:
Which way the world is seen: **side-view** (gravity pulls down the screen, feet find ground) or **planar** (top-down, no screen-space gravity, feet find walkable floor). The same creature machinery serves both; a handful of facts apply to only one.
_Avoid_: 2D mode, view, camera angle

**Planar heading**:
The direction a top-down creature faces. Local homes rotate with it, which is what makes a top-down creature turn rather than strafe.
_Avoid_: rotation, facing angle

### Where a foot goes

**Foothold candidate**:
One observation of somewhere a leg *could* step. Evidence, never a command — gait decides whether to accept it.
_Avoid_: hit, sample, suggestion

**Query adapter**:
The consumer-written module that observes the world and publishes foothold candidates. Raycast, tilemap, signed-distance field, or hand-authored — the package never knows which.
_Avoid_: provider, ground service, sensor

**Home**:
Where a leg wants its foot, expressed relative to the body. In the planar projection it rotates with the planar heading.
_Avoid_: rest position, default position, anchor

**Predicted home**:
Where a leg is *aiming* — its home carried forward by the body's velocity, so a step lands where the body is going rather than where it has been.
_Avoid_: lead position, target

**Probe frame**:
The published fact of where every leg is aiming and the body pose that aim was derived from. A query adapter reads it instead of re-deriving it; gait judges aim against the same fact, so the two cannot silently disagree.
_Avoid_: probe context, query state, snapshot

**Stale evidence**:
Foothold candidates observed against an older probe frame than the one gait is judging with. Distinct from having nowhere legal to stand — the creature's feet are fine, its information isn't.
_Avoid_: expired candidates, outdated hits

**Blocked region**:
Ground a foot may not stand on, and a route a leg may not swing through. Both are reported as facts about a candidate, not as a shape the package understands.
_Avoid_: obstacle, collider, no-go zone

### The plant

**Plant contract**:
The promise a planted foot makes: it stays exactly where it was committed until it lifts, whatever the body does. Breaking it is what skating looks like.
_Avoid_: foot lock, pin, ground constraint

**Support relation**:
The bond between a planted foot and the moving thing it stands on, recorded so the plant travels with a platform or conveyor rather than being re-queried. A side-view concern only.
_Avoid_: parenting, attachment

**Urgency**:
How far a planted foot has drifted from its home — the pressure that makes a step necessary. Compared against **comfort**, the distance a creature tolerates before wanting to move that foot.
_Avoid_: stress, tension, error

### How legs coordinate

**Gait permission**:
The per-tick decision about which legs *may* leave the ground. Separate from where they would step, and separate from whether they can.
_Avoid_: scheduling, arbitration

**Cadence**:
The coordination rule permission applies — partner, support, tripod, or wave. Cadence changes only when no leg is airborne, so a switch never strands a foot.
_Avoid_: gait pattern, walk cycle, mode

**Wave cursor**:
Whose turn it is under the wave cadence. It advances on a landing and on nothing else, so a leg that finds nowhere to step holds its turn rather than passing it on.
_Avoid_: index, pointer, turn counter

**Recovery request**:
Gait telling locomotion it is in trouble — no legal foothold, or no fresh evidence — along with a heading that would help. A request, never a command: locomotion decides what to do with it.
_Avoid_: error, failure signal, override

### Presentation

**Presentation**:
Everything derived from a resolved creature purely to place pixels — visual lift, shadow, sort key, bank, stretch, weight shift. It reads the simulation and never repairs it; switching it off must leave the creature walking legally.
_Avoid_: rendering, view layer, cosmetics

**Visual lift**:
The height a top-down foot is drawn above the movement plane during a swing. The foot itself never leaves the plane — only its picture does.
_Avoid_: step height, hop, arc

**Body language**:
The render-only bank, stretch and weight shift that make a body read as leaning into a turn or bracing against a stop. Deliberately not simulation.
_Avoid_: secondary motion, juice, animation
