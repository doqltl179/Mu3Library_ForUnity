# Mu3 Library Game - Watermelon

Reusable 2D Watermelon Game board runtime for Unity 6. The runtime assembly is `Mu3Library.Game.WatermelonGame` and references `Mu3Library` and `Mu3Library.URP`.

## Runtime API

`BoardArea` fits a board sprite to a camera viewport with configurable padding, creates the left/right/bottom collision boundaries, and exposes local, screen, and world normalized-position conversions with initialization-safe `Try...` variants.

`BoardController` coordinates falling items and merges through `BoardItem`, `BoardConfig`, and `BoardItemsConfig`. Call `SetBoardConfig(BoardConfig)` before `Prepare`, then call `GameStart()` after a successful preparation; the legacy `SetBoareConfig` spelling remains as a compatibility alias. The default Watermelon Game uses the first eleven fruit entries addressed by zero-based list index. Extra catalog entries are preserved for future rules but are not spawned or merged by the default rules.

`BoardItemScoreRule.GetScore(int)` is virtual and defaults to the triangular Watermelon Game score progression, allowing external projects to override scoring.

`BoardArea` also calculates aspect-preserving local, screen, and world rectangles, mathematical screen-to-board-plane conversions, boundary-aware area checks, and position clamping helpers.

Runtime configuration changes validate the complete default catalog before updating the board, active items, and the held item together. Pool reuse resets item presentation and physics state before the next item is initialized.

## Import

Add this package to a project that already references `Mu3Library_Base` and `Mu3Library_URP`:

```json
"com.github.doqltl179.mu3library.game.watermelon": "file:../../Mu3Library_Game_WatermelonGame"
```

Import the `Watermelon Game` package sample to receive the `BoardConfig` asset, fruit/background and board-guide images, sample manager/core scripts, and playable `Demo` scene.
