# Changelog — Mu3 Library Game - Watermelon

Release history for `com.github.doqltl179.mu3library.game.watermelon`.

Detailed per-release notes live in the repository root `CHANGELOG.md` under the `game/watermelon/x.y.z` tags; this file marks the package-local view of them.

## [Unreleased]

## [0.6.0] - 2026-08-17

- See `game/watermelon/0.6.0` in the repository root `CHANGELOG.md`. Highlights: the mouse drives `InputHandler` like a touch, so the game plays in the editor and on desktop, and an edit-mode test suite covers `BoardCommandRunner`.

## [0.5.0] - 2026-08-16

- See `game/watermelon/0.5.0` in the repository root `CHANGELOG.md`. Highlights: the board-relative resize sizes the item collider instead of the sprite, so an item index keeps its contact area in every configuration, plus `BoardItemInfo.ColliderOffset` and `BoardItemScaleRule.GetBoardContactDiameter` (breaking: `GetBoardScale` takes a `BoardItemInfo`).

## [0.4.0] - 2026-08-15

- See `game/watermelon/0.4.0` in the repository root `CHANGELOG.md`. Highlights: the `BoardController` events a project drives its UI from, `BoardMergeInfo` and `CountMerge(BoardMergeInfo)`, and the removal of `SetBoareConfig` and the "board world normalized" conversions (breaking).

## [0.3.0] - 2026-08-09

- See `game/watermelon/0.3.0` in the repository root `CHANGELOG.md`. Highlights: the public board command queue with `IBoardCommandContext`, lifecycle-based `BoardCommand`, flow, item, and score commands, a `MergingCommand` shared by the board and projects, and optional board sounds played through `AudioManager`.

## [0.2.0] - 2026-08-08

- See `game/watermelon/0.2.0` in the repository root `CHANGELOG.md`. Highlights: `SetBoardConfig`, the drop interval and board-relative fall, board snapshots, the next-item preview, the split of `BoardArea` into single-purpose parts, and fixes to merging, pooling, and game-end detection.

## [0.1.1] - 2026-08-07

- See `game/watermelon/0.1.1` in the repository root `CHANGELOG.md`. Highlights: optional `ParticleHandler` merge effects.

## [0.1.0] - 2026-08-07

- See `game/watermelon/0.1.0` in the repository root `CHANGELOG.md`. Highlights: the first release of the `Mu3Library.Game.WatermelonGame` runtime assembly and its playable sample.
