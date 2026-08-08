# Mu3 Library Game - Watermelon

Reusable 2D Watermelon Game board runtime for Unity 6. The runtime assembly is `Mu3Library.Game.WatermelonGame` and references `Mu3Library` and `Mu3Library.URP`.

## Runtime API

`BoardArea` fits a board sprite to a camera viewport with configurable padding, creates the left/right/bottom collision boundaries, and exposes local, screen, and world normalized-position conversions with initialization-safe `Try...` variants.

`BoardController` coordinates falling items and merges through `BoardItem`, `BoardConfig`, and `BoardItemsConfig`. Call `SetBoardConfig(BoardConfig)` before `Prepare`, then call `GameStart()` after a successful preparation; the legacy `SetBoareConfig` spelling remains as a compatibility alias. The default Watermelon Game uses the first eleven fruit entries addressed by zero-based list index. Extra catalog entries are preserved for future rules but are not spawned or merged by the default rules.

`BoardConfig.BoardSpawnGuideLineSprite` configures the one-segment sprite used for the vertical, tiled spawn guide line. The guide line appears only during a drag, is one fifth of the spawn marker's width, and renders at board sorting order `+1`; the spawn marker is drawn at `+2`.

`BoardConfig.SoundConfig` holds the optional board sounds. Every clip can be left empty, and a moment without one stays silent, so a board can be configured with only the sounds a project already has. It carries one clip per `BoardSoundType` (`GameStart`, `GameEnd`, `ItemDrop`), a BGM playlist with shuffle, inter-track interval, and cycle count, and the merge clip list. The playlist starts on `GameStart()` and stops on `GameEnd()`.

Merge sounds follow the combo: the first merge plays the first clip of `ItemMergeClips`, and every merge that lands within `MergeComboInterval` (5 seconds by default) of the one before it steps one clip further, stopping at the last one. A merge that comes later starts the combo over. `BoardController.MergeComboIndex` exposes the current step, and `PlayBoardSound(BoardSoundType)` / `PlayItemMergeSound()` are `protected virtual`.

Board sounds are played through `Mu3Library.Audio.AudioManager`. `BoardController.AudioManager` accepts the `IAudioManager` a project already runs, which the board then shares without touching its lifetime or its volumes. While none is assigned the board creates one of its own with the first sound it plays, drives it from `Update`, applies `BoardSoundConfig.BgmVolume` to it, and disposes it with the board.

`BoardItemScoreRule.GetScore(int)` is virtual and defaults to the triangular Watermelon Game score progression, allowing external projects to override scoring.

`BoardArea` also calculates aspect-preserving local, screen, and world rectangles, mathematical screen-to-board-plane conversions, boundary-aware area checks, and position clamping helpers.

Runtime configuration changes validate the complete default catalog before updating the board, active items, and the held item together. Pool reuse resets item presentation and physics state before the next item is initialized.

## Import

Add this package to a project that already references `Mu3Library_Base` and `Mu3Library_URP`:

```json
"com.github.doqltl179.mu3library.game.watermelon": "file:../../Mu3Library_Game_WatermelonGame"
```

Import the `Watermelon Game` package sample to receive the `BoardConfig` asset, fruit/background and board-guide images, sample manager/core scripts, and playable `Demo` scene.
