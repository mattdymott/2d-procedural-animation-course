# 2D Procedural Animation with Unity DOTS

A self-contained, browser-based course on building Rain World-style creatures — soft,
bendy bodies that move believably — using Unity DOTS/ECS. Covers verlet chains, muscles,
analytic and iterative (FABRIK) IK, gait/stepping, second-order "juice," batching at scale,
and reusable package API design, plus an end-to-end lizard capstone and world-contact branch, across 12 lessons. Each lesson is a standalone HTML page
with an interactive simulation, a challenge, and a quiz; see [index.html](index.html) for
the full table of contents.

Open `index.html` directly in a browser, or serve the folder locally (e.g.
`python -m http.server 8734`) since some lessons load assets that browsers block under
`file://`. Also live on GitHub Pages: https://mattdymott.github.io/2d-procedural-animation-course/

## Structure
- `lessons/` — the 12 lesson pages
- `reference/` — cheatsheets and a glossary
- `learning-records/` — per-lesson progress notes
- `assets/` — shared CSS/JS
- `MISSION.md` / `NOTES.md` — course goals and teaching notes

## Note

This course was built using the [`teach` skill](https://github.com/mattpocock/skills/tree/main/skills/productivity/teach)
by [Matt Pocock](https://github.com/mattpocock) for Claude Code, and its content is
AI-generated.
