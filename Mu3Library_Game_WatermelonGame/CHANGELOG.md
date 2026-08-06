# Changelog

## [Unreleased]

## [0.1.1] - 2026-08-07

- Added optional `ParticleHandler` merge effects to `BoardItemInfo` and cleaned up spawned effects after completion.
- Made merge effect processing skip null effect entries instead of stopping the full merge command.

## [0.1.0] - 2026-08-07

- Added the reusable `Mu3Library.Game.WatermelonGame` runtime assembly with board, item, merge, configuration, and input helpers.
- Added `BoardArea` fitting, collision boundaries, normalized-position conversions, camera projection helpers, and safe `Try...` variants.
- Added eleven fixed fruit entries addressed by zero-based index and the virtual `BoardItemScoreRule.GetScore(int)` scoring extension point.
- Added the complete Watermelon Game sample surface with the `BoardConfig` asset, fruit/background images, sample manager/core scripts, and playable `Demo` scene.
- Fixed board-area calculations for orthographic and perspective cameras, item sizing, boundary collider placement, and item collider synchronization.
