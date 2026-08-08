# Changelog

## [Unreleased]

- Added a public board command queue to `BoardController`: `EnqueueCommand`, `CancelCommand`, `CancelCommands<T>`, `CancelAllCommands`, `HasCommand<T>`, `Commands`, `CommandCount`, and the `OnCommandEnqueued`, `OnCommandFinished` and `OnCommandFailed` events. A command that throws is logged, canceled and dropped without stopping the rest, and commands enqueued or canceled from a running command are applied once the board is done advancing the current ones.
- Added `IUpdatableBoardCommand` and `ICancelableBoardCommand` as the optional parts of the command contract, so a command can be advanced every frame and stopped before its end while `IBoardCommand` stays the minimal one.
- Changed `BoardCommand` into a lifecycle base with the `OnRun`, `OnUpdate`, `OnComplete`, `OnCancel` and `OnDispose` hooks, the `Complete` and `Cancel` transitions, and a `State` of `BoardCommandState`. A command that owns its own state machine implements `IBoardCommand` directly instead.
- Added `BoardCommandRunner`, which drives one command through the optional parts of the contract and is shared by the board queue, the command groups, and any host outside a board.
- Added `IBoardCommandContext` and `BoardController.CommandContext`, the board surface a command written outside this package reaches: `TrySpawnItem`, `TryReplaceItem`, `RemoveItem`, `ContainsItem`, `AddScore`, `CountMerge`, `PlayBoardSound`, `PlayItemMergeSound`, `EnqueueCommand`, `Area`, `Config`, `ScoreRule`, `Items`, `HoldingItem` and `ItemIndexCount`.
- Added the `ActionCommand`, `DelayCommand`, `WaitUntilCommand`, `SequenceCommand`, `ParallelCommand` and `CompositeBoardCommand` flow commands, so board work can be ordered, paced, and grouped without a project writing its own scheduler.
- Added the `SpawnItemCommand`, `RemoveItemsCommand`, `PromoteItemCommand`, `ShakeBoardCommand` and `AddScoreCommand` board commands, which cover bonus items, clearing power-ups, a single-item promotion, a board shake, and score bonuses.
- Changed `MergingCommand` to carry out the merge itself through `IBoardCommandContext` instead of running the two callbacks the board passed it, so one merge command serves both the pairs the board finds and a pair a project picked, which no longer has to be reproduced anywhere. It takes a context and the pair, keeps reserving them, and exposes `GetMergedIndex`, `GetMergeScore`, `SpawnMergedItem`, `TryGetMergedPosition` and `PlayMergeSound` as `protected virtual`. `BoardController.CreateMergingCommand(BoardItem, BoardItem)` is the factory to override for a subclass, and the pair check moved to `TryCreateMergingCommand`.
- Changed `MergingCommand` to live in `Board.Command.Item` with the other item commands; the `Board.Command.Merge` namespace and folder are gone.
- Added `BoardController.AddScore(int)`, `CountMerge()`, `Items`, `Config`, `Area`, `ItemIndexCount`, `ContainsItem(BoardItem)` and a public `GetItemInfo(int)`; every score change and every merge count, the board's own merges included, now runs through them, and a negative score amount never pushes the score below zero.
- Added `BoardItem.AddVelocity(Vector2, float)`, which pushes an item that already rests on the board without wiping the speed it carries.
- Added an optional board sound configuration to `BoardConfig`, with separate SFX and BGM volumes and optional game start, game end, and item drop clips. Every clip can be left empty, and a moment without one stays silent.
- Added an optional board BGM playlist to `BoardConfig.SoundConfig`, with shuffle, an inter-track interval, and a cycle count. It starts on game start and stops on game end.
- Added combo-driven merge sounds; the first merge plays the first clip of `BoardSoundConfig.ItemMergeClips` and every merge that follows within the combo interval, 5 seconds by default, steps one clip further until the last one is reached. `BoardController.MergeComboIndex` exposes the current step.
- Added `BoardController.AudioManager`, which the board plays every sound through. The board creates and drives a `Mu3Library.Audio.AudioManager` of its own with the first sound it plays; assign the one a project already runs to share it instead.
- Changed the spawn guide line width from one tenth to one fifth of the spawn marker width.
- Fixed the spawn guide line being drawn at an unintended size; the tiled renderer is now fitted through `SpriteRenderer.size`, and its transform scale, which sizes one repeated segment, stays the same on both axes.

## [watermelon/0.2.0] - 2026-08-08

- Fixed the spawn guide line disappearing when its board-relative dimensions were applied through `SpriteRenderer.size`; it now fits through its child transform scale.
- Fixed the spawn guide line staying hidden when a drag begins during the drop cooldown and the held item becomes available afterward.
- Added a configurable drag-only vertical tiled spawn guide line; it is one tenth of the spawn marker's width, renders at board sorting order `+1`, and moves the spawn marker to `+2`.
- Fixed the package sample lifecycle so a successfully prepared board starts running.
- Configured the package sample board with its item-out line and spawn-marker images.
- Added `BoardController.SetBoardConfig(BoardConfig)` as the validated configuration boundary; `SetBoareConfig` remains as a compatibility alias.
- Changed default item catalog validation to preserve entries after the eleven default fruits without enabling them in the default spawn or merge rules.
- Changed board configuration application to update active and held items atomically, including their scale and board-relative physics.
- Changed merge matching to group active items by index before checking contacts, avoiding unrelated pair comparisons and per-frame group allocations after warmup.
- Fixed merge reservations to be released for invalid, canceled, and failed commands without changing a pooled instance that has already been reused.
- Fixed pooled `BoardItem` instances to reset presentation, collider, physics, support, and merge state before reuse.
- Fixed board local bounds calculation for camera-aligned or tilted board planes while retaining the public world XY bounds API.
- Fixed touch handling to track the finger that began a drag, preventing another finger's move/end event from changing it.
- Improved cached board child renderers and out colliders to avoid repeated hierarchy/component lookups during layout rebuilds.
- Improved sample preparation so absent scene/config dependencies do not publish a prepared board state, and its resource lookup is cached.
- Changed the default `BoardItemInfo` collider diameter ratio from `0.96` to `0.98` so fruit contacts better match the configured sprite size.
- Added configurable item-out line placement and board-width fitting for `BoardArea`.
- Added a drop interval to `BoardController`, 0.5 seconds by default, so a touch can no longer spawn items back to back.
- Added an initial downward speed for a released item, set as a fraction of the board area height per second so every screen resolution gives the same push.
- Added a board-relative fall acceleration, set as a fraction of the board area height per second squared and applied through the item gravity scale, so the whole fall stays the same on every screen resolution without touching the project gravity.
- Added JSON serialization and deserialization to `BoardSnapshot` through `ToJson` and `FromJson`, preserving board counters, the held and preview item indices, item positions, and rotations.
- Added `BoardController.ExportSnapshot`, `ExportSnapshotJson`, and `ImportSnapshotJson` to save and restore the board, including the held item, preview item, and board item positions and rotations.
- Added optional `BoardConfig.ItemPhysicsMaterial` and `BoardConfig.ItemRigidbodySettings` settings for the physics material, linear damping, and angular damping applied to every board item.
- Added `BoardItem.PhysicsMaterial`, `LinearDamping`, and `AngularDamping` so board-wide physics can be configured without changing the item prefab.
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
