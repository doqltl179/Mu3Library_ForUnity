# Mu3 Library Game - Watermelon

Reusable 2D Watermelon Game board runtime for Unity 6. The runtime assembly is `Mu3Library.Game.WatermelonGame` and references `Mu3Library` and `Mu3Library.URP`.

## Runtime API

`BoardArea` fits a board sprite to a camera viewport with configurable padding, creates the left/right/bottom collision boundaries, and exposes local, screen, and world normalized-position conversions with initialization-safe `Try...` variants.

`BoardController` coordinates falling items and merges through `BoardItem`, `BoardConfig`, and `BoardItemsConfig`. The board configuration contains exactly eleven fruit entries addressed by zero-based list index.

`BoardItemInfo` can reference optional `ParticleHandler` resources for merge effects. The board controller plays the merged effect and destroys the spawned effect instance after completion.

`BoardItemScoreRule.GetScore(int)` is virtual and defaults to the triangular Watermelon Game score progression, allowing external projects to override scoring.

`BoardArea` also calculates aspect-preserving local, screen, and world rectangles, mathematical screen-to-board-plane conversions, boundary-aware area checks, and position clamping helpers.

## Import

Add this package to a project that already references `Mu3Library_Base` and `Mu3Library_URP`:

```json
"com.github.doqltl179.mu3library.game.watermelon": "file:../../Mu3Library_Game_WatermelonGame"
```

Import the `Watermelon Game` package sample to receive the `BoardConfig` asset, fruit/background images, sample manager/core scripts, and playable `Demo` scene.
