# 2D Procedural Animation with Unity DOTS — Resources

## Knowledge

- [Talk: "The Rain World Animation Process" — Joar Jakobsson & James Therrien, GDC 2016 (YouTube, official GDC channel)](https://www.youtube.com/watch?v=-iXwvoFhPuU)
  The definitive primary source for the mission. Joar explains the whole philosophy: "I have a bunch of points in space and I connect them at certain distances." Use for: creature body construction, the mindset behind point-and-constraint animation, how graphics drape over simulation.
- [Article: "The Rain World Animation Process" — GameAnim summary](https://www.gameanim.com/2017/07/24/rain-world-animation-process/)
  Written companion/summary of the talk. Use for: quick review without rewatching the video.
- [Paper: "Advanced Character Physics" — Thomas Jakobsen, GDC 2001 (community transcription)](https://github.com/krisives/advanced-character-physics)
  The foundational verlet + constraint-relaxation paper (Hitman: Codename 47). Use for: verlet integration, distance constraints, stiffness via iteration, particle-based bodies. This is the math bedrock of everything Rain World-like.
- [Video: "Giving Personality to Procedural Animations using Math" — t3ssel8r](https://www.youtube.com/watch?v=KPoeNZZ6H4s)
  Second-order dynamics (frequency / damping / response) as a universal "juice" filter. Use for: the toolkit's spring/follow module, squash-stretch drivers, procedural lean and overshoot. Also: [text transcription](https://github.com/SalvatoreScalia/Giving-Personality-to-Procedural-Animations-using-Math).
- [Tutorial series: "Inverse Kinematics in 2D" Parts 1 & 2 — Alan Zucconi](https://www.alanzucconi.com/2018/05/02/ik-2d-1/)
  Gentle, geometry-first 2D IK (analytic two-bone, then code). Use for: limb IK foundations before FABRIK.
- [Paper: "FABRIK: A fast, iterative solver for the Inverse Kinematics problem" — Aristidou & Lasenby, 2011 (author PDF)](https://www.andreasaristidou.com/publications/papers/FABRIK.pdf)
  The n-joint IK algorithm that pairs naturally with verlet chains (it's position-based too). Use for: multi-segment limbs, tails that reach, constraint-friendly IK.
- [Talk: "Animation Bootcamp: An Indie Approach to Procedural Animation" — David Rosen, GDC 2014 (GDC Vault)](https://www.gdcvault.com/play/1020583/Animation-Bootcamp-An-Indie-Approach)
  The canonical "tiny rules, emergent motion" talk (Overgrowth/Receiver). Also [free on the Internet Archive](https://archive.org/details/GDC2014Rosen). Use for: gait philosophy, doing more with fewer authored poses, procedural transitions.
- [Series: "Procedural Animation: Locomotion" Parts 1–2 — Little Polygon](https://blog.littlepolygon.com/posts/loco1/)
  Phase-driven stepping with interactive figures: root motion, leaning, hip bobbing, sine-metronome foot pinning (π offset per leg), step extrapolation. Use for: the phase-driven alternative to event-driven gait, and body-language layers (lean, bob).
- [Docs: Unity Entities manual](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/index.html)
  Use for: verifying current API when mapping recipes to DynamicBuffers/jobs. (User is fluent — reference only.)
- [Course: Unity DOTS Best Practices — Unity Learn](https://learn.unity.com/course/dots-best-practices)
  Official. The "minimizing cache misses" unit is the one that matters for animation at scale. Use for: L7 batching patterns, cache-friendly layout.
- [Docs: Enableable components — Unity Entities manual](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/components-enableable-intro.html)
  Use for: representing dead/swinging/grab-active/LOD-tier state with no structural change (L7 Pattern 3). Verified: no archetype move, no sync point, thread-safe toggle; only IComponentData/IBufferElementData qualify.
- [Docs: Set the capacity of a dynamic buffer — Unity Entities manual](https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/components-buffer-set-capacity.html)
  Use for: [InternalBufferCapacity] sizing so chains stay in-chunk (L7 Pattern 4). Verified: within capacity = inline in chunk; overflow spills whole buffer to heap and wastes the inline slot.
- [Book: _A Philosophy of Software Design_ — John Ousterhout](https://web.stanford.edu/~ouster/cgi-bin/book.php)
  The canonical source for L8 package/API design. Deep modules (simple interface, powerful implementation), information hiding, "the cost of a module is its interface." Short, high-signal.
- [Docs: Baker overview & baking workflow — Unity Entities manual](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/baking-baker-overview.html)
  Use for: the config-baked-once pattern (L8) — authoring MonoBehaviour + Baker<T> → immutable runtime IComponentData. Verified: bakers convert authoring components, must declare dependencies to re-run on change.

## Wisdom (Communities)

- [Unity Discussions — Entity Component System forum](https://discussions.unity.com/tag/entities)
  Official, well-trafficked. Use for: DOTS-specific implementation questions (buffer layouts, job structuring for chains).
- Official Unity Discord — DOTS channel
  Real-time feedback from ECS practitioners. Use for: quick sanity checks on system design.
- r/proceduralgeneration and r/gamedev (share work-in-progress creature GIFs)
  Use for: feedback on whether creatures *feel* alive — the thing only human eyes can judge.

## Gaps

- No high-trust source yet specifically on *2D procedural creature locomotion at ECS scale* (batching many chains in Burst). Likely must be synthesized from Jakobsen + DOTS docs + experimentation.
- ~~Procedural gait/stepping~~ — resolved 2026-07-14: David Rosen GDC 2014 + Little Polygon locomotion series (above).
