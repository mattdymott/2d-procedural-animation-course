# 2D Procedural Animation with Unity DOTS

A self-contained, browser-based course on building Rain World-style creatures — soft,
bendy bodies that move believably — using Unity DOTS/ECS. It covers verlet chains,
muscles, analytic and iterative (FABRIK) IK, gait and stepping, second-order "juice",
batching at scale, and reusable package API design.

The core path is complete in eight lessons, followed by an optional four-part side-view
creature build. Five outcome-based workshops deepen the course for moving supports,
top-down locomotion, insect cadence, readable motion, and gait debugging. Supporting
experiments remain available as a field-note library rather than a mandatory thirty-step
sequence; see [index.html](index.html) for the map.

Open `index.html` directly in a browser, or serve the folder locally (for example,
`python -m http.server 8734`) since some lessons load assets that browsers block under
`file://`. Also live on GitHub Pages: https://mattdymott.github.io/2d-procedural-animation-course/

## Structure

- `lessons/` — core lessons, applied track, and supporting field notes
- `workshops/` — optional, outcome-based applied builds
- `reference/` — cheatsheets and a glossary
- `learning-records/` — per-lesson and workshop-progress notes
- `assets/` — shared CSS and JavaScript components
- `MISSION.md` / `NOTES.md` — course goals and teaching notes
- `unity/` — runnable DOTS companion project; it is not linked from the published course

## Note

This course was built using the [`teach` skill](https://github.com/mattpocock/skills/tree/main/skills/productivity/teach)
by Matt Pocock for Claude Code, and its content is AI-generated.
