# Changelog

## [Unreleased]

- Added configurable item-out line placement and board-width fitting for `BoardArea`.
- Removed `BoardItemInfo.MergingEffect` / `MergedEffect`, the effect playback in `MergingCommand`, and the sample merge effect prefabs, materials and textures; the prefabs were saved in the legacy Unity 5 prefab format and crashed the Editor natively inside the particle renderer when instantiated.
- Fixed `BoardController` game-end detection to end the game when an item above the board line rests on the board floor or on other items, excluding items that have not landed yet and the side walls.
- Fixed board item bookkeeping so merging items stay registered until their command completes and the same item cannot be registered twice, preventing leaked items and stale duplicates that grew the board without bound.
- Fixed `MergingCommand` so a merge cannot start or complete more than once and never completes after disposal.

## [0.1.1] - 2026-08-07

- Added optional `ParticleHandler` merge effects to `BoardItemInfo` and cleaned up spawned effects after completion.
- Added static Watermelon merge VFX textures and `WatermelonMergingEffect` / `WatermelonMergedEffect` prefabs to the sample, wired through `BoardConfig`.
- Made merge effect processing skip null effect entries instead of stopping the full merge command.

## [0.1.0] - 2026-08-07

- Added the reusable `Mu3Library.Game.WatermelonGame` runtime assembly with board, item, merge, configuration, and input helpers.
- Added `BoardArea` fitting, collision boundaries, normalized-position conversions, camera projection helpers, and safe `Try...` variants.
- Added eleven fixed fruit entries addressed by zero-based index and the virtual `BoardItemScoreRule.GetScore(int)` scoring extension point.
- Added the complete Watermelon Game sample surface with the `BoardConfig` asset, fruit/background images, sample manager/core scripts, and playable `Demo` scene.
- Fixed board-area calculations for orthographic and perspective cameras, item sizing, boundary collider placement, and item collider synchronization.
