# Changelog

## [Unreleased]

- Added configurable item-out line placement and board-width fitting for `BoardArea`.
- Added a drop interval to `BoardController`, 0.5 seconds by default, so a touch can no longer spawn items back to back.
- Added an initial downward speed for a released item, set as a fraction of the board area height per second so every screen resolution gives the same push.
- Added a board-relative fall acceleration, set as a fraction of the board area height per second squared and applied through the item gravity scale, so the whole fall stays the same on every screen resolution without touching the project gravity.
- Added a next-item preview to `BoardController`(`NextItemIndex`, `NextItemInfo`, `OnNextItemChanged`), drawn one item ahead of the one in hand.
- Added `BoardController.IsGameEndCheckPaused`, which suspends the game-end check for the drop interval so a freshly dropped stack can slide into place before it is judged.
- Changed the item to drop to wait at the top of the board as soon as the drop interval has passed, at the place the previous item was dropped from, instead of being created on the next touch.
- Split `BoardArea` into single-purpose parts under `Board/Area`(bounds calculation, coordinate conversion, view, out colliders, input relay) while keeping its public API.
- Changed `BoardItemScaleRule` to spread the item diameters linearly over the board area width, from `1/20` for the smallest fruit to `2/5` for the largest one.
- Fixed `BoardArea.CalculateBounds` overloads so the aspect ratio argument is used instead of the board sprite's own ratio.
- Removed `BoardItemInfo.MergingEffect` / `MergedEffect`, the effect playback in `MergingCommand`, and the sample merge effect prefabs, materials and textures; the prefabs were saved in the legacy Unity 5 prefab format and crashed the Editor natively inside the particle renderer when instantiated.
- Fixed `BoardController` game-end detection to end the game when an item stacked above the top edge of the board area rests on the board floor or on other items, excluding items that have not landed yet and the side walls; the item-out line is now display only.
- Fixed board item fall detection so a fall only ends on the board floor or another item; brushing a side wall no longer counts as landing, which made items stacked against a wall end the game too early.
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
