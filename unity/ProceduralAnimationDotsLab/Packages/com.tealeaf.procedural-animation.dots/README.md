# Tealeaf Procedural Animation DOTS

An embedded-package scaffold for the 2D grounded-appendage runtime extracted
from `ProceduralAnimationDotsLab`.

This package intentionally contains no assemblies or implementation yet. It
establishes the publishable identity and layout before moving behaviour, so the
sample remains runnable throughout the extraction.

## Intended layout

```text
Runtime/        Package-owned solve group, configuration, state, and helpers
Editor/         Creature authoring, Baker, validation, and derived previews
Tests/Editor/   Public-interface bake-and-tick coverage
Samples~/Lab/   The current demo, terrain/support adapters, and presentation
Documentation~/ Package usage and integration contract
```

The extraction contract lives at
[`../../Documentation~/package-extraction.md`](../../Documentation~/package-extraction.md).

## Runtime dependencies

The core package depends only on `com.unity.entities` (and its transitive
Mathematics and Collections dependencies). Physics, tilemaps, rendering, and
input remain consumer or sample concerns.
