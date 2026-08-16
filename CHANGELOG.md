# Changelog

<div align="center">

[![English](https://img.shields.io/badge/EN-English-2D7FF9?style=flat-square)](CHANGELOG.md) [![Korean](https://img.shields.io/badge/KO-한국어-00A86B?style=flat-square)](docs/changelog/CHANGELOG.ko.md) [![Japanese](https://img.shields.io/badge/JA-日本語-EA4AAA?style=flat-square)](docs/changelog/CHANGELOG.ja.md)

</div>

All notable changes to Mu3Library For Unity will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This changelog tracks package release changes only. Repository development workflow and tooling changes are tracked in [`docs/repository/CHANGELOG.md`](docs/repository/CHANGELOG.md).

## [Unreleased]

### Added
- `BoardItemInfo.ColliderOffset`: Added the collider center of a catalog entry, measured as a fraction of the collider diameter, which starts at `(0.0, -0.03)`. The board keeps the collider diameter of an item index the same on every screen resolution, so an offset written here moves the contact area by the same fraction of it everywhere. An entry serialized before this field existed reads `(0, 0)`, which centers the collider on the sprite.
- `BoardItemScaleRule.GetBoardContactDiameter(int, Vector2)` and `BoardItemScaleRule.GetBoardContactDiameter(int, Vector2, float, float)`: Added the contact diameter of an item index in the board local space, which depends on the index and the board size alone and never on the sprites a configuration carries.
- `BoardItem.BoardLocalColliderCenter`: Added the collider center measured from the item position in the parent(board) local space, which is what the held item is clamped by and what the spawn marker rides above.

### Changed
- `BoardItemScaleRule`: The board-relative resize now sizes the item collider instead of the item sprite. The sprite was fitted to the diameter its item index asks for and the collider was shrunk inside it afterwards, so `BoardItemInfo.ColliderScale` decided how large an item really played and a new configuration moved the contact area of every index with it. The rule now scales an item until its collider reaches that diameter and the sprite is drawn around it at whatever size that takes, so an item index touches over the same area whichever configuration is applied. A catalog entry whose collider covers less of its sprite is therefore drawn larger than before, which is the intended trade for a contact area that stays put.
- `BoardItemScaleRule.GetBoardScale`: Takes the `BoardItemInfo` where it took a `Sprite`, because the collider scale of the entry is now part of what is measured. `GetBoardScale(int, Sprite, Vector2)` and `GetBoardScale(int, Sprite, Vector2, float, float)` are gone; hand it the catalog entry instead, and override the new overload in a subclass that overrode the sprite one.
- `BoardController`: The held item is kept inside the item area by its contact area instead of its sprite bounds, and the spawn marker rides above the contact center, so where an item index can be dropped from no longer depends on the padding its sprite carries.
## [base/0.23.3] - 2026-08-15

### Fixed
- `UIAreaGrid`, `UIAreaElement`, `SafeCanvas`: Loading a prefab while play mode runs no longer fills the console with `SendMessage cannot be called during Awake, CheckConsistency, or OnValidate`. Unity calls `OnValidate` whenever an asset is deserialized, which includes a prefab Addressables loads at runtime, and these components applied their layout right there while play mode ran: the grid anchored its elements and the canvas created and fitted its safe rect. Setting an anchor makes Unity send `OnRectTransformDimensionsChange` to the children, a message that is refused inside `OnValidate` whichever mode is running. The work is deferred out of `OnValidate` in play mode too now, the way it already was in edit mode.

## [base/0.23.2] - 2026-08-15

### Fixed
- `SafeRect`: The anchors no longer alternate between two sets of values every frame, which kept resizing the rect the component owns. It read `Screen` on its own from `OnRectTransformDimensionsChange`, a message Unity sends from inside a canvas rebuild, and `Screen` answers with the render target of the pass that asks it: a scene view repaint reports the scene view while the game view repaint reports the game view, so each answer looked like a screen that had just changed into the other one. `ScreenChangeNotifier` is the only screen the component reads now, so one frame reports one screen and the anchors settle.
- `SafeRect`: A screen that reports no safe area is no longer taken for a screen that changed on every check. The full-screen rect that took the place of the empty safe area was recorded as the applied one, so the recorded rect could never match what the screen reported, `IsScreenChanged()` stayed true, and the safe area was applied again on every message the component received. What was read is recorded now, while the substitute stays what the anchors and `OnCalculated(Rect)` are given.
- `ScreenChangeNotifier`: The editor loop no longer reads the screen while play mode runs. Play mode already reads it from the player loop, so both loops read the same frame and could report it twice with two different answers.

## [base/0.23.1] - 2026-08-15

### Fixed
- `MVPManager`: A presenter opened from inside `LoadFunc()` or `OpenFunc()` now resolves its parent as owner. The manager put a presenter into its state list only after the matching lifecycle callback had already returned, so `OpenAsChild()` called from either callback found no owner entry, logged `Owner presenter not found or not active`, and opened the child detached: without the ownership link, without the owner `RectTransform` as its host, and outside the cascade close. Every phase transition now happens before the callback that belongs to it, so a presenter stays resolvable while its own `LoadFunc()`, `OpenFunc()`, `CloseFunc()`, and `UnloadFunc()` run.
- `MVPManager`: A cascade close now reaches a child that is still loading. Such a child only ever sat in the load queue, which the close path never read, so force-closing its owner left it behind as an orphan under a view that was already going away. A loading view has never been shown and is therefore inactive, so it is activated first to let the close coroutine run.
- `MVPManager`: A presenter that cannot close now leaves its children alone. The close path force-closed the whole child chain first and only then found out that the presenter itself was not closable, which tore down every child of a presenter that stayed open.
- `MVPManager`: Sorting order placement counts presenters that are still loading. It read only the entries whose view was on screen, so two views of the same type opened in the same frame both kept the sorting order of their prefab and overlapped. Placement reads the registry now and skips only the presenter it is placing.
- `MVPManager`: `CloseAll()`, `CloseAll(IEnumerable<string>)`, and `CloseAllWithoutDefault()` offer presenters that are still loading. A window opened in the same frame as the close-all pass sat only in the load queue, which the pass never read, so it survived the close and appeared right afterwards. A forced close now takes it, while an unforced one still leaves it alone.

### Changed
- `MVPManager`: The three close-all overloads share one candidate collector instead of each carrying its own pair of loops. It walks the opened, opening, and loading presenters in that order, so an owner is always reached before the children it cascades to.
- `MVPManager`: Presenter lookup goes through a `Dictionary<PresenterBase, PresenterEntry>` registry that holds an entry from the moment it is opened until it is pooled or its view is destroyed, replacing three linear scans over the opened, open-check, and load-check lists. Lookup is O(1), the five state lists became pure per-state queues, and an owner is now refused on its recorded phase — one that is closing or unloading takes no new children — instead of on whichever list it happened to sit in. `CleanupDestroyedPresenters()` sweeps the registry once rather than all five lists.

## [base/0.23.0] - 2026-08-15

### Added
- `ScriptIdentifier`: Added the editor utility that owns the C# identifier rules the script exporters use, with `Sanitize` for the keyword-aware form, `SanitizePascal` for the form that also raises the first letter, `SanitizeUnderscore` for the form that keeps the original spelling and only replaces the characters an identifier cannot hold, and `ToPublicMember` for public member names. Each exporter drawer had its own copy of the rule it needed, so the same fix had to be repeated per drawer.
- `FileCreator.WriteScript(string, string, string)`: Added the single place generated scripts are written, which saves `{fileName}.cs` into the given system directory as UTF-8 with BOM and returns the path it wrote.
- `FileFinder.IsAssetsFolder(Object)`: Added the check for whether an asset lives inside the project `Assets` folder, which the exporter and MVP helper drawers each carried a copy of.
- `CameraExtensions.IsReady(Camera)`: Added the camera readiness check that anything fitting an object to the view waits for: the camera exists, renders, and has a viewport rect and pixel size that cover an area. `WorldSpaceBackground` and the Watermelon board area each carried their own copy of it.
- `RectTransformExtensions.AnchorTo(RectTransform, Vector2, Vector2, bool)`: Added the anchoring a rect goes through to cover a normalized range of its parent, which clears the offsets while `fill` is on so that the rect fills the range exactly. `Stretch()` now anchors through it, and `UIAreaGrid.Apply(RectTransform, UIAreaType, bool)` and `SafeRect` each carried a copy of the same writes.
- `Mu3WindowDrawer`: Added `DrawRefreshButton(Action)`, `DrawAssetsFolderField(SerializedObject, SerializedProperty, string)`, `DrawNamespaceField(string, Action<string>, string)`, and `DrawClassNameField(string, Action<string>, string, string)`, the drawer rows the exporter drawers each carried a copy of. `DrawAssetsFolderField` clears a folder outside `Assets` and warns about it, which the MVP helper row did silently before.
- `UIAreaGrid` and `UIAreaElement`: Added the nine-area anchoring helper. `UIAreaGrid` splits its RectTransform into the left/middle/right columns and the bottom/middle/top rows through `UIAreaBoundary` cut lines, and anchors every child `UIAreaElement` to the `UIAreaType` it picked. Each side of an axis keeps 0.08 of it by default, which leaves 0.84 to the middle. `GetAreaRect(UIAreaType)`, `GetAreaAnchors(UIAreaType, out Vector2, out Vector2)`, `Apply()`, and `Apply(RectTransform, UIAreaType, bool)` report and apply the same layout from code.
- `UIAreaGrid.CreateElementsAutomatically`: Added the child creation that gives every area an element of its own, which is on by default. `ResolveElements()` maps each area to its child and creates the missing ones, `GetElement(UIAreaType)` reports the element of an area without creating one, and `CreateElement(UIAreaType)` adds one by hand. `GetRectTransform(UIAreaType)` and `TryGetRectTransform(UIAreaType, out RectTransform)` reach the RectTransform of an area without going through its element. An area holds one element: `UIAreaElement.AreaType` refuses an area another element of the same grid owns, `UIAreaElement.IsAreaTaken(UIAreaType)` reports whether it is free, `CreateElement(UIAreaType)` returns the owner instead of adding a second one, and `ResolveElements()` moves a copied element that landed on a taken area to a free one.
- `UIAreaElement`: The element now reports what the grid owns through a `DrivenRectTransformTracker`, so the RectTransform inspector shows those properties as read only. The anchors are always driven and the position and the size join them while `FillArea` is on, while the pivot, the rotation and the scale are never driven. An element whose parent has no grid drives nothing.
- `UIAreaGrid.DrawAreaGizmo` and `UIAreaGrid.AreaGizmoColor`: Added the scene view outline of the nine areas, which is on by default and follows the scene view gizmo toggle. It is drawn from `OnDrawGizmos` in the local space of the grid, so it follows the rect through any rotation and scale. A Screen Space - Overlay canvas is rendered after the gizmo pass, so on such a canvas the outline is hidden behind whatever the UI draws over it.
- `UIAreaGrid.HorizontalEditMode` and `UIAreaGrid.VerticalEditMode`: Added the `UIAreaEditMode` scope of each axis, which decides what a single cut line owns. `Uniform` keeps one pair of cut lines for the whole grid, so widening the top area to the right narrows the whole right line of areas, while `Independent` keeps a pair per row (horizontal) or per column (vertical), so the same edit narrows the right top area only. Switching an axis to `Independent` copies the shared cut lines into every row/column first, so the layout does not jump.
- `UIAreaGridEditor` and `UIAreaElementEditor`: Added the inspectors of the two components, which move the cut lines with min/max sliders, pick an area from a 3x3 grid that disables the areas another element already owns, create the missing elements, edit the object, active state and fill state of every element from the grid itself, and write the anchors of the affected children through `Undo`.
- `SafeCanvas` and `SafeRect`: Added the safe area components. `SafeRect` anchors its RectTransform to `Screen.safeArea` and follows a screen that changes size or orientation, and `SafeCanvas` gives its canvas a `SafeRect` of its own. `SafeRect.Calculate()` applies the safe area by hand and `OnCalculated(Rect)` hands the applied area to a subclass. The anchors are relative to the parent, so only a `SafeRect` on a direct child belongs to a canvas, the same way a `UIAreaElement` only follows a `UIAreaGrid` on its direct parent.
- `SafeCanvas.SafeRect`, `SafeCanvas.CreateSafeRect()`, and `SafeCanvas.ResolveSafeRect()`: `SafeRect` reports the safe rect the canvas holds, or null while it holds none, and never creates one. `CreateSafeRect()` adds it by hand and returns the one the canvas already holds instead of creating a second one, and `ResolveSafeRect()` creates the missing one and fits it to the screen.
- `SafeRect`: The component reports what it owns through a `DrivenRectTransformTracker`, so the RectTransform inspector shows those properties as read only: the anchors, the position, the size and the pivot, while the rotation and the scale are never driven. It fits itself in edit mode as well, so the editor shows the same layout the player does, and what it drives is kept out of the scene file.
- `ScreenChangeNotifier`: Added the screen reporter that raises `OnChanged` once the screen has changed its size or its safe area, and reports what it found through `ScreenSize` and `SafeArea`. Unity raises no event of its own for `Screen.safeArea`, so the screen is read once a frame from this one place instead of from every listener, through `Application.onBeforeRender` in play mode and through the editor loop in edit mode. A listener that throws is reported through `Debug.LogException` and does not keep the rest from following the screen.
- `SafeRect`: The component now follows the screen through what reports it instead of through a per frame check of its own, so a scene keeps no `Update()` per safe rect. Unity sends `OnRectTransformDimensionsChange` as soon as the canvas follows a screen that changed size or orientation, and `ScreenChangeNotifier` covers a safe area that changed while the screen kept its size, such as the one a device turned upside down leaves behind. The `Update()` the component overrode is gone: a subclass that overrode it should override `OnRectTransformDimensionsChange()` or subscribe to `ScreenChangeNotifier.OnChanged` instead.

### Changed
- `SubscribeHandler.UnSubscribe(uint)`: Renamed to `Unsubscribe(uint)`, matching the `Unsubscribe` spelling `ISubscriptionInfo` and `SubscriptionInfo` already use.
- `NotificationArguments.CancelmText`: Renamed to `CancelText` in the Template sample, matching the neighbouring `ConfirmText`.
- `WorldSpaceBackground`: Moved from the `Mellow.Utility` namespace to `Mu3Library.Utility`, where the folder it lives in and the rest of the package already are. Update the `using` in a project that referenced it.
- `CoroutineSafeRunner`: Moved from the `Mu3Library.Coroutine.Foundation` namespace to `Mu3Library.Foundation.Coroutine`, matching its `Foundation/Coroutine` folder and the neighbouring `Mu3Library.Foundation.Event`. Update the `using` in a project that referenced it.
- `MVPManager.Dispose()`: Now clears `OnWindowLoaded`, `OnWindowOpened`, `OnWindowClosed`, and `OnWindowUnloaded`, drops the manager root, render camera, and out panel references, and destroys the `EventSystem` it created itself. An `EventSystem` that already lived in the scene belongs to the project and is left alone. Before this, the created `EventSystem` survived the manager as a `DontDestroyOnLoad` object and the events kept their subscribers.
- `LocalizationManager`: The fallback locale is now created at most once and destroyed on `Dispose()`. A locale that came from the project settings is an asset and is never destroyed. Before this, every reset of the cached default locale could create another fallback that nothing released.
- `SceneLoader`: The command guards that the built-in, editor, and Addressables scene commands each carried a copy of now live in one place: `GuardSingleScenePreload`, `GuardAdditiveScenePreload`, `GuardAdditiveSceneUnload`, `ActivatePreloadedSingleScene`, and `ActivatePreloadedAdditiveScene`. The rejection reasons, their order, and the events they emit are unchanged; each backend keeps only the part that is its own, such as the build-settings check, the scene asset check, or the Addressables handle lookup.
- `LocalizationManager`: Now implements `IDisposable`, so the DI scope that owns it tears it down with its core, the way `AddressablesManager` and `SceneLoader` already did. Disposing releases the one-shot subscriptions, takes the completion handler back off the Localization initialization operation, cancels a locale change that is still running, and clears the events and the cached locales. The initialization operation itself belongs to the Localization package and is not released.
- MVP Helper: The generated MVP scripts are now written as UTF-8 with BOM through `FileCreator.WriteScript`, matching the other exporters instead of the platform default this drawer wrote with.
- Caught exceptions are now reported with `Debug.LogException` instead of a `Debug.LogError` that stringified them, so the exception type and its stack trace survive in the console: `LocalizationCharacterCollectorDrawer`, `InputSystemManager.AddInputActionAsset(string, ...)`, and `WebRequestManager.CreateUnexpectedFailureResult` and `WebRequestManager.ParseResult`. The two `WebRequestManager` sites still return the same failure message on `WebRequestResult`, so the url and method context is unchanged for callers. A project that filtered these through `Application.logMessageReceived` now sees `LogType.Exception` where it saw `LogType.Error`.

### Fixed
- `CanvasExtensions.CopyTo`: `overwriteScaler` now copies `CanvasScaler` settings and `overwriteRaycaster` copies `GraphicRaycaster` settings, matching the option names instead of applying them in reverse.
- `AddressablesManager.Initialize(Action)`, `AddressablesManager.InitializeWithResult(Action<bool, string>)`, `LocalizationManager.Initialize(Action)`, and `LocalizationManager.InitializeWithResult(Action<bool, string>)`: The callback now goes through the one-shot subscription these managers already offer instead of staying on `OnInitialized` or `OnInitializeResult`. Calling initialize again after a failed attempt no longer invokes a callback the earlier attempt was given a second time, and a callback is no longer held, together with whatever it captured, for the rest of the manager's life once initialization has completed.
- `CoreRoot`: Destroying it now disposes its subscription handler and clears `OnCoreInitialized` and `OnCorePrepared`, so the core-wait subscriptions it created do not outlive it. `CoreBase` already did this on destroy.

## [game/watermelon/0.4.0] - 2026-08-15

### Added
- `BoardController`: Added the `OnScoreAdded`, `OnBoardConfigChanged`, `OnHoldingItemChanged`, `OnHoldingItemMoved`, `OnItemDropped`, `OnItemAdded`, `OnItemRemoved`, `OnItemMerged`, and `OnMergeComboChanged` events, so a project can drive its UI and effects from what the board reports instead of polling it. `OnScoreAdded` carries the points a single change paid out and skips a change the zero clamp swallowed, and `OnItemRemoved` runs while the item still carries its catalog entry and its place.
- `BoardController`: Added `HoldingNormalizedX`, the place the held item waits on as a fraction of the board area width, which is what `OnHoldingItemMoved` reports.
- `BoardMergeInfo`: Added the merge report handed to `BoardController.OnItemMerged`, carrying the catalog index that merged, the index and the instance it became, the board-normalized position it happened at, the score it paid out, and `IsValid`.
- `BoardController.CountMerge(BoardMergeInfo)` and `IBoardCommandContext.CountMerge(BoardMergeInfo)`: Added the merge count that reports what it merged, which every merge the board and `MergingCommand` carry out now uses. `CountMerge()` still counts a merge without detail and reports `BoardMergeInfo.Unknown`.
- `CompositeBoardCommand`: Added the `CompositeBoardCommand(Action, IBoardCommand[])` constructor and the `Step(float)` hook, so a command group only describes how it carries its children one step further. `SequenceCommand` and `ParallelCommand` each carried the completion callback and the `OnRun`/`OnUpdate`/`OnComplete` wiring of their own. A group that drives its children by overriding `OnRun` and `OnUpdate` keeps working untouched.

### Changed
- `MergingCommand`: The merge now plays its sound before it counts itself, so a listener of `OnItemMerged` already sees the merge combo step the merge landed on. A `BoardController` subclass that overrode `CountMerge()` to follow merges should override `CountMerge(BoardMergeInfo)` instead.
- `BoardSnapshot.FromJson`: A snapshot it cannot read is now reported with `Debug.LogException` instead of a `Debug.LogError` that stringified the exception, so the exception type and its stack trace survive in the console. A project that filtered this through `Application.logMessageReceived` now sees `LogType.Exception` where it saw `LogType.Error`.

### Removed
- `BoardController.SetBoareConfig(BoardConfig)`: Removed the misspelled compatibility alias of `SetBoardConfig(BoardConfig)`, which only forwarded to it. Call `SetBoardConfig(BoardConfig)` instead.
- `BoardArea`: Removed the "board world normalized" conversions, which only forwarded to their board local counterparts and gave one coordinate space two names: `BoardWorldNormalizedPositionToWorld`, `BoardWorldNormalizedPositionToScreen`, `BoardWorldNormalizedPositionToLocal`, `WorldToBoardWorldNormalizedPosition`, `TryWorldToBoardWorldNormalizedPosition`, `ScreenToBoardWorldNormalizedPosition`, `TryScreenToBoardWorldNormalizedPosition`, `LocalToBoardWorldNormalizedPosition`, and `TryLocalToBoardWorldNormalizedPosition`. Use the matching `BoardLocalNormalized` conversion instead.

## [game/watermelon/0.3.0] - 2026-08-09

### Added
- `BoardController`: Added a public board command queue through `EnqueueCommand`, `CancelCommand`, `CancelCommands<T>`, `CancelAllCommands`, `HasCommand<T>`, `Commands`, `CommandCount`, and the `OnCommandEnqueued`, `OnCommandFinished`, and `OnCommandFailed` events. Commands added or canceled while the queue is advancing are applied after the current pass, and a throwing command is logged, canceled, and removed without stopping the rest.
- `IUpdatableBoardCommand` and `ICancelableBoardCommand`: Added optional update and cancellation contracts while keeping `IBoardCommand` minimal.
- `BoardCommand`: Changed the base to lifecycle hooks through `OnRun`, `OnUpdate`, `OnComplete`, `OnCancel`, and `OnDispose`, with `Complete`, `Cancel`, and `BoardCommandState`; commands with their own state machine can implement `IBoardCommand` directly.
- `BoardCommandRunner`: Added a shared runner for the board queue, command groups, and hosts outside a board.
- `IBoardCommandContext` and `BoardController.CommandContext`: Added the narrow board surface external commands use for item operations, score, merge count, sounds, queueing, and board state.
- Flow commands: Added `ActionCommand`, `DelayCommand`, `WaitUntilCommand`, `SequenceCommand`, `ParallelCommand`, and `CompositeBoardCommand`.
- Board commands: Added `SpawnItemCommand`, `RemoveItemsCommand`, `PromoteItemCommand`, `ShakeBoardCommand`, and `AddScoreCommand`.
- `MergingCommand`: Changed merge execution to use `IBoardCommandContext`, so the board and external projects can use the same merge command, with protected virtual hooks for merge index, score, spawn position, and sound.
- `MergingCommand`: Moved the command to `Board.Command.Item` and removed the old `Board.Command.Merge` namespace and folder.
- `BoardController`: Added `AddScore`, `CountMerge`, `Items`, `Config`, `Area`, `ItemIndexCount`, `ContainsItem`, and public `GetItemInfo`; score and merge updates now use one path, and negative score changes clamp at zero.
- `BoardItem`: Added `AddVelocity(Vector2, float)` for pushing an item without replacing its existing velocity.
- `BoardConfig`: Added an optional `SoundConfig` that carries the board sounds: one clip per `BoardSoundType`, `GameStart`, `GameEnd`, and `ItemDrop`. Every clip can be left empty and a moment without one stays silent, so a board can be configured with only the sounds a project already has.
- `BoardController`: Changed sound volume ownership to `SfxVolume` and `BgmVolume`, which clamp to 0–1; BGM changes apply immediately to the board-owned audio manager while an assigned manager keeps its own volume.
- `BoardSoundConfig`: Added an optional BGM playlist through `BgmClips`, `BgmShuffle`, `BgmTrackInterval`, and `BgmLoopCount`. The board starts it on game start and stops it on game end, and it only ever stops a playlist it started itself.
- `BoardSoundConfig`: Added combo-driven merge sounds through `ItemMergeClips` and `MergeComboInterval`. The first merge plays the first clip, and every merge that lands within the interval, 5 seconds by default, steps one clip further until the last one is reached; a merge that comes later starts the combo over. `BoardController.MergeComboIndex` exposes the current step and `PlayItemMergeSound()` is `protected virtual`.
- `BoardController`: Added `SoundConfig`, `AudioManager`, and the `protected virtual PlayBoardSound(BoardSoundType)` hook. Board sounds go through `Mu3Library.Audio.AudioManager` instead of a second playback path built into this package. Assign the `IAudioManager` a project already runs to share it, volumes and instance limit included, and the board leaves its lifetime alone. While none is assigned the board creates one of its own with the first sound it plays, so a configuration without any clip never builds one, drives it from `Update` because it is a plain class, and disposes it with the board.

### Changed
- `BoardArea`: The spawn guide line width is now two fifths of the spawn marker width instead of one tenth, which is one twenty-fifth of the board width.

### Fixed
- `BoardArea`: The spawn guide line is drawn at the intended size again. A tiled `SpriteRenderer` draws over `SpriteRenderer.size` and not over the sprite bounds, so the line is now sized through that size while the child transform scale, which decides how big one repeated segment is drawn, stays the same on both axes.
- `BoardArea` and Watermelon sample: Added the item-area rectangle to board gizmos and refreshed the sample board background and sound configuration.

## [game/watermelon/0.2.0] - 2026-08-08

### Added
- `BoardArea`: Added configurable item-out line placement and board-width fitting for the optional outline sprite.
- `BoardConfig`: Added a configurable drag-only tiled spawn guide line sprite, rendered below the spawn marker at board sorting order `+1` while the marker renders at `+2`.
- `BoardController`: Added a drop interval, 0.5 seconds by default, so a touch can no longer spawn items back to back, exposed through `CanSpawnItem` and `DropCooldown`.
- `BoardController`: Added an initial downward speed for a released item, configured as a fraction of the board area height per second. The board area follows the screen resolution, so the same push is applied on every device. `BoardItem.SetDropVelocity(Vector2)` applies it as a velocity instead of a force, because the items differ in mass.
- `BoardController`: Added a board-relative fall acceleration, configured as a fraction of the board area height per second squared and applied through `BoardItem.GravityScale` when an item is dropped. Together with the drop speed the whole fall, and not only its start, covers the same fraction of the board in the same time on every screen resolution. Only the board items are scaled, the project gravity is never modified.
- `BoardSnapshot`: Added JSON serialization and deserialization through `ToJson` and `FromJson`, preserving the score, spawn and merge counts, the held and preview item indices, board-relative item positions, and item rotations.
- `BoardController`: Added `ExportSnapshot`, `ExportSnapshotJson`, and `ImportSnapshotJson` to save and restore a board, including the item in hand, the preview item, and the positions and rotations of board items.
- `BoardConfig`: Added optional `ItemPhysicsMaterial` and `ItemRigidbodySettings` settings for the physics material, linear damping, and angular damping applied to every board item.
- `BoardItem`: Added `PhysicsMaterial`, `LinearDamping`, and `AngularDamping` properties so board-wide physics can be configured without changing the item prefab.

- `BoardController`: Added a next-item preview through `NextItemIndex`, `NextItemInfo` and `OnNextItemChanged`. The spawn rule is drawn one item ahead, so the player can see what follows the item in hand, and `HoldingItem` exposes the item in hand itself.
- `BoardController`: Added `IsGameEndCheckPaused`, which suspends the game-end check for the drop interval after an item is released. A dropped item lands on a high stack almost immediately, so the stack now gets that time to slide into place before it is judged.

### Changed
- `BoardController`: Board configuration now goes through validated `SetBoardConfig(BoardConfig)` (`SetBoareConfig` remains as a compatibility alias), updating active and held items atomically, including their scale and board-relative physics.
- `BoardItemsConfig`: Catalog entries beyond the eleven default fruits are now preserved for future rules, while the default spawn and merge rules remain limited to those eleven entries.
- `BoardController`: Merge matching now groups active items by index before checking contacts, and board child renderers and out colliders are cached during layout rebuilds.
- `BoardArea`: Local board bounds now remain correct for camera-aligned and tilted board planes while the public world XY bounds API is retained.
- `WatermelonGame` sample: Preparation now validates and caches its dependencies, and game start is triggered only after a successful board preparation.
- `BoardItemInfo`: Increased the default collider diameter ratio from `0.96` to `0.98` so fruit contacts better match the configured sprite size.
- `BoardController`: The item to drop now waits at the top of the board as soon as the drop interval has passed, instead of being created on the next touch. It waits where the previous item was dropped from, and a touch only picks it up.
- `BoardArea`: Split the component into single-purpose parts under `Board/Area` — `BoardAreaBoundsCalculator` measures the board rectangle, `BoardAreaCoordinateConverter` converts positions, `BoardAreaView` draws the board and the item-out line, `BoardAreaOutColliders` keeps the items inside, and `BoardAreaInputRelay` reports the touches that belong to the board, with the rectangle itself carried by the new `BoardAreaBounds` and `CoordinateBounds` types. The component keeps its public API and only forwards to those parts.
- `BoardItemScaleRule`: Item diameters are now spread linearly over the board area width, from `1/20` for the smallest fruit to `2/5` for the largest one, instead of growing by a shrinking area multiplier. `GetBoardScale` takes the largest ratio next to the smallest one, and `GetBoardWidthDiameterRatio(int)` returns an item's diameter as a fraction of the board width.

### Removed
- Merge effects: Removed `BoardItemInfo.MergingEffect` / `MergedEffect`, the effect playback in `MergingCommand`, and the sample merge effect prefabs, materials and textures. The prefabs were saved in the legacy Unity 5 prefab format, so their `ParticleSystemRenderer` had no `serializedVersion`, was missing most of its fields, and held a null material at index 0; instantiating one crashed the Editor natively in `ParticleSystemRenderer::PrepareForRender` on the first merge.

### Fixed
- `BoardArea`: The spawn guide line now appears when a drag began during the drop cooldown and the held item becomes available afterward, and its board-relative dimensions are applied through transform scale.
- `MergingCommand`: Merge reservations are released for invalid, canceled, and failed commands without mutating a pooled item that has already been reused.
- `BoardItem`: Pooled instances now reset their presentation, collider, physics, support, and merge state before reuse.
- `InputHandler`: Touch moves and ends now stay tied to the finger that began the drag.
- `BoardArea`: `CalculateBounds(Camera, float)` and `CalculateBounds(Camera, Vector2, float)` now use the aspect ratio they are given, they always fell back to the board sprite's own ratio.
- `BoardController`: Game-end detection now ends the game when an item whose top edge is above the board line rests on the board floor or on other items. Items are always placed above the line, so an item that has not landed yet is excluded through its falling state. The side walls are never treated as a support.
- `BoardController`: Merging items stay registered on the board until their command completes and the same item can no longer be registered twice, so preparing the board again collects every item instead of leaking it or leaving stale duplicates that grew the board without bound. Renamed the misspelled `OnDestory` so commands are actually disposed.
- `MergingCommand`: A merge can no longer start or complete more than once, and never completes after the command has been disposed.

## [game/watermelon/0.1.1] - 2026-08-07

### Added
- `BoardItemInfo`: Added optional `ParticleHandler` resources for merge effects and automatic cleanup after spawned effects complete.

### Fixed
- `MergingCommand`: Null merge effects are skipped instead of stopping the full merge command.

## [base/0.22.0] - 2026-08-07

### Added
- `Mu3Library.Base`: Added `ParticleHandler`, a `MonoBehaviour` requiring a `ParticleSystem` and providing play, pause, stop, clear, restart, loop controls, and lifecycle events including natural-completion `OnCompleted`.
- `Mu3Library.Base`: Added `Mellow.Utility.WorldSpaceBackground`, which fits a required `SpriteRenderer` background to a camera viewport with optional fit-on-enable and camera-front placement.
- `GameObjectPool<T>` and `GameObjectPool<T, TArgs>`: Added batch enqueue/dequeue overloads and optional initialization callbacks for newly created and pooled objects.
- `GameObjectPool<T, TArgs>`: Changed creation callbacks to use no arguments and added typed initialization callbacks that accept `CreateArguments` subclasses.

## [urp/0.2.1] - 2026-08-07

### Added
- `Mu3Library.URP.Cam.CameraStackSetter`: Added `EnsureUniversalCameraData(Camera)` for safe Universal Camera Data bootstrap in reusable samples.

## [game/watermelon/0.1.0] - 2026-08-07

### Added
- `Mu3Library.Game.WatermelonGame`: Added the reusable board, item, merge, configuration, and input runtime assembly.
- `BoardArea`: Added board fitting, collision boundaries, normalized-position conversions, camera projection helpers, and initialization-safe `Try...` variants.
- `BoardItemsConfig`: Added eleven fixed fruit entries addressed by zero-based index.
- `BoardItemScoreRule`: Added the virtual `GetScore(int)` scoring extension point with the default triangular progression.
- Watermelon Game sample: Added the `BoardConfig` asset, fruit/background images, sample manager/core scripts, and playable `Demo` scene.

### Changed
- `BoardItemScaleRule`: Item sizing now uses the board area's local width and supports orthographic and perspective camera calculations.

### Fixed
- `BoardArea`: Corrected board bounds and generated boundary collider placement.
- `BoardItem`: Synchronized the circle collider radius with the configured sprite.

## [base/0.21.0] - 2026-08-03

### Changed
- `ButtonInvokeAttribute`: Replaced serialized-field usage with parameterless instance-method usage. The decorated method is invoked directly, so overloaded method names no longer cause `AmbiguousMatchException`; its label and height are optional, and omitting the label displays `Invoke {method name}`.

### Removed
- `ButtonInvokeAttribute` field attachment, method-name argument, and `drawProperty` option.

## [base/0.20.0] - 2026-08-03

### Added
- `IAddressablesManager` / `AddressablesManager`: Added `LoadAssetsWithKeys<T>` and `LoadAssetsWithKeysAsync<T>` to load multiple assets and return a `Dictionary<string, T>` indexed by each resource location's `PrimaryKey`.

### Changed
- `LocalizationDataExporterDrawer`: Removed the Split by Table toggle and now always generates a shared `{ClassName}Locales` script, one `TableData`-derived script per table with its `EntryData` instances, and a compact root table index.

## [base/0.19.1] - 2026-08-02

### Fixed
- `AddressablesManager`: Guard the UniTask implementation with both `MU3LIBRARY_ADDRESSABLES_SUPPORT` and `MU3LIBRARY_UNITASK_SUPPORT`.
- `ResourceLoader`: Skip direct `Resources.UnloadAsset` calls for cached `GameObject`, `Component`, and `AssetBundle` instances while still releasing them from the loader cache.

## [base/0.19.0] - 2026-07-29

### Added
- `ICoreRoot` / `CoreRoot`: Added the `OnCorePrepared` event and one-shot `SubscribeOnCorePreparedOnce<T>(Action)` / `SubscribeOnCorePreparedOnce(Type, Action)` APIs for observing core preparation completion.
- `CoreBase` / `IDICore`: Added `IsPreparing` and `IsPrepared` states for tracking core preparation.

## [base/0.18.0] - 2026-07-28

### Added
- `ICoreRoot` / `CoreRoot`: Added the `OnCoreInitialized` event and one-shot `SubscribeOnCoreInitializedOnce<T>(Action)` / `SubscribeOnCoreInitializedOnce(Type, Action)` APIs for observing core initialization completion.

### Changed
- `CoreBase` / `IDICore`: Renamed `IsPrepared` to `IsInitialized` and aligned core initialization notifications with completion of DI scope initialization.
- `CoreRoot`: Core initialization notifications now occur after the core scope initializes, and initialization subscriptions return disposable `ISubscriptionInfo` tokens.
- Template samples: Replaced `WaitForOtherCore` callbacks with cross-core `[Inject]` dependencies.

### Removed
- `CoreBase.WaitForOtherCore<TCore>`: Removed the old cross-core readiness helper.

## [base/0.17.0] - 2026-07-27

### Added
- `IObjectInjector`: Added a narrow injection capability for applying existing `[Inject]` field and property injection to objects created outside the container without exposing `ContainerScope` internals.
- `MVPManager`: Container-managed instances now inject `[Inject]` members into presenters created or reused by the presenter pool before presenter initialization.
- `Mu3Library.Foundation`: Added a no-Engine runtime assembly for reusable subscription infrastructure.

### Changed
- `AddressableGroupDataExporterDrawer`: Reworked generated Addressables data into a dedicated `{ClassName}Labels` string-label script, one `GroupData`-derived script per group, nested `EntryData`-derived asset classes, and a compact root group index. The split toggle and `LabelData` runtime type were removed.
- `SubscribeHandler`: Moved the reusable one-shot subscription implementation into `Mu3Library.Foundation` while preserving its `Mu3Library.Event` namespace and public API.
- `SubscribeHandler`: Temporarily commented Foundation diagnostic logging until the logging integration is redesigned.
- `SubscribeHandler` / `SubscriptionInfo`: Hardened subscription lifecycle handling with idempotent unsubscribe, exception-safe cleanup, one-shot callback cleanup, and collision-safe internal ID allocation.
- Event Bus interfaces and implementations: Changed one-shot subscription methods to return disposable `ISubscriptionInfo` tokens instead of `uint` IDs.

## [base/0.16.0] - 2026-07-12

### Changed
- `ContainerScope`: Services registered in a `CoreBase`, including classes created by `RegisterClass<T>()`, now receive `[Inject]` field and property injection after construction and before lifecycle callbacks. Factory-created and pre-created registered instances are covered as well.

## [base/0.15.0] - 2026-07-01

### Added
- `ISceneLoader` / `SceneLoader`: Added Addressables UniTask helpers for `PreloadSingleSceneWithAddressablesAsync`, `ActivateSingleSceneWithAddressablesAsync`, `LoadSingleSceneWithAddressablesAsync`, `PreloadAdditiveSceneWithAddressablesAsync`, `ActivateAdditiveSceneWithAddressablesAsync`, `LoadAdditiveSceneWithAddressablesAsync`, and `UnloadAdditiveSceneWithAddressablesAsync`.
- `ISceneLoaderEventBus` / `SceneLoader`: Added structured `SceneLifecycleInfo` callbacks for single and additive scene lifecycle updates. The payload keeps the requested target/key and exposes `ResolvedSceneName`, using `UnnamedAddressableScene` until an Addressables runtime scene name is resolved.
- `ScenePhase`: Added `Unloaded` so structured lifecycle callbacks can report additive unload completion explicitly.

## [urp/0.2.0] - 2026-06-24

### Added
- `Mu3Library.URP.Cam.CameraStackSetter`: Added `SetCameraStackToMainAsFirst`, `SetCameraStackToMain(Camera)`, `SetCameraStackToMain(Camera, int)`, `SetCameraStackAsFirst(Camera, Camera)`, `SetCameraStack(Camera, Camera)`, and `SetCameraStack(Camera, Camera, int)` helpers so any URP overlay camera can be inserted into `Camera.main` or an explicit root camera stack.

### Changed
- `Mu3Library_URP/package.json`, `README.md`, localized READMEs, and localized changelogs: Bumped the URP package to `0.2.0` and updated the public UPM install tag references.

## [urp/0.1.5] - 2026-06-21

### Removed
- `Mu3Library_URP/Runtime/Scripts/AGENTS.md`, `Mu3Library_URP/Runtime/Shaders/AGENTS.md`, and `Mu3Library_URP/Samples~/AGENTS.md`: Removed package-local agent routing documents from the importable URP package surface so Unity package import no longer includes AGENTS docs that can drift from their `.meta` counterparts.

### Changed
- `Mu3Library_URP/package.json`, `README.md`, and localized READMEs: Bumped the URP package to `0.1.5` and updated the public UPM install tag references.

## [base/0.14.2] - 2026-06-20

### Changed
- `IMVPManager` / `MVPManager` / `PresenterBase`: Removed `OpenOptions` and replaced it with direct `HostOptions` overloads while keeping every `Open` / `OpenAsChild` convenience overload delegated to one final explicit open signature.

## [base/0.14.1] - 2026-06-17

### Added
- Added `OpenOptions` and `HostOptions` to the MVP UI runtime so chained presenter opens can configure ownership separately from visual hosting and root layout placement.

### Changed
- `IMVPManager` / `MVPManager` / `PresenterBase`: Refactored chained presenter opening around `owner` terminology and explicit host options. Owned presenters still default to the owner's root view when no `HostOptions.Host` is supplied, but each open now reapplies the child view resource's root layout instead of copying the owner's `RectTransform` values.

## [base/0.14.0] - 2026-05-25

### Added
- `ButtonInvokeAttribute` / `ButtonInvokeAttributeDrawer`: Added support for rendering an Inspector button on a serialized field that invokes a parameterless instance method, and updated the Attribute sample to demonstrate it alongside `ConditionalHideAttribute`.

## [base/0.13.0] - 2026-05-24

### Added
- `GameObjectPool<T>`: Added an optional `Create` delegate constructor for user-defined empty-pool creation and `Clear()` to destroy pooled inactive objects.

### Changed
- `GameObjectPool<T>`: Replaced the internal `List<T>` with `Queue<T>`, now prevents duplicate inactive enqueues by tracking pooled instance IDs, and no longer instantiates directly from a resource reference.
  The previous `GameObjectPool(T resource)` constructor was removed; migrate call sites to `GameObjectPool(Create onCreate)` and provide the instantiation logic explicitly.

## [urp/0.1.4] - 2026-05-24

### Changed
- `Mu3Library_URP/package.json`: Updated the URP manifest to depend on `com.github.doqltl179.mu3library.base` `0.13.0` and aligned the package metadata with Base `0.13.0`.

## [base/0.12.0] - 2026-05-24

### Added
- `ISceneLoaderEventBus` / `SceneLoader`: Expanded one-shot subscription helpers beyond `SubscribeOnSingleSceneLoadedOnce` to cover single-scene `LoadStarted`, `Preloaded`, and `Changed` callbacks, plus additive `LoadStarted`, `Preloaded`, `Loaded`, and `Unloaded` callbacks.
  This changes the `ISceneLoaderEventBus` implementation contract, so custom implementers must add the new once-subscription methods when upgrading.
- `ILocalizationManagerEventBus` / `LocalizationManager`, `IAddressablesManagerEventBus` / `AddressablesManager`, `IMVPManagerEventBus` / `MVPManager`: Added one-shot subscription helpers for localization initialization completion/result events, Addressables initialization events, and MVP window lifecycle events.
  This changes each event-bus implementation contract, so custom implementers must add the new once-subscription methods when upgrading.
- `SubscribeHandler`: Added reusable `SubscribeOnce(...)` overloads for `Action`, `Action<T>`, and `Action<T1, T2>` registrations so each service can manage one-shot subscriptions through its own handler instance.

### Changed
- `SceneLoader`: `OnSingleSceneLoaded`, `OnAdditiveSceneLoaded`, and `OnAdditiveSceneUnloaded` now prefer `SceneManager.sceneLoaded` / `SceneManager.sceneUnloaded` timing, while `OnAdditiveScenePreloaded` remains the pre-activation milestone. Built-in and Editor additive unload no longer gate completion through `allowSceneActivation`, so unload progress reflects the underlying async operation directly.

## [base/0.11.0] - 2026-05-02

### Added
- `.github/workflows/unity-compile-gate.yml`: Added a manual self-hosted Windows workflow for `scripts/compile-gate/run-unity-compile.ps1`, plus an automatic GitHub-hosted guidance job for push and pull request events.

### Changed
- `IWebRequestManager` / `WebRequestManager`: UniTask WebRequest APIs now accept optional `propagateCancellation` flags. By default cancellation becomes a failure/default-value path, while callers that need explicit cancellation can opt in.
- `ISceneLoader` / `SceneLoader`: Simplified the scene-loading API around explicit `Preload*`, `Activate*`, `Load*`, and `Unload*` commands. Removed fake-loading controls, added phase/status queries plus matching `*Async` wait helpers, and aligned the Editor and Addressables scene-loading surfaces with the same flow.
  - Simplified `ISceneLoaderEventBus` to load-lifecycle callbacks (`LoadStarted`, `Preloaded`, `Loaded`, `Unloaded`), restored progress callbacks, unified rejection reporting into `OnSceneCommandRejected(SceneCommandRejectedInfo)`, and added `OnSingleSceneChanged(previousSceneName, loadedSceneName)` for single-scene transitions.
  - Removed `UseFakeLoading`, `FakeLoadingTime`, and the previous CancellationToken-based scene async helper contract.

### Fixed
- `README.md` and localized READMEs: Documented the opt-in WebRequest cancellation propagation behavior.

## [urp/0.1.3] - 2026-05-02

### Changed
- `Mu3Library_URP/package.json`: Updated the URP manifest to depend on `com.github.doqltl179.mu3library.base` `0.11.0` and aligned the package release with Base `0.11.0`.

## [urp/0.1.2] - 2026-04-26

### Added
- `ShakeEffect` / `ShakePass`: Added `SetPeriod(float period)` so the URP shake screen effect can control the shake loop duration independently from amplitude.
- `GaussianBlurEffect` / `GaussianBlurPass`: Added a new URP full-screen gaussian blur effect with matching pass and shader implementation.
- `DepthOutlineEffect` / `DepthOutlinePass`: Added `SetOutlineThickness(float outlineThickness)` plus a matching sample slider so depth-based outlines can be widened without changing the threshold.

### Changed
- `IScreenEffect` / `IScreenEffectManager`: Renamed the URP screen-effect contract from `IPassInjector`, and renamed the manager registration APIs from `RegisterPass` / `UnregisterPass` to `RegisterEffect` / `UnregisterEffect` so the public API matches the current effect-based flow.
- `ScreenEffectBase` / `ScreenPassBase`: Added reusable base classes for custom URP screen effects and passes, centralising active-state, disposal, pass creation, and shader/material lifecycle management.
- `ScreenEffectManager` / `IScreenEffectManager`: Renamed the URP screen-effect pass registry class and interface from `PostVolumeManager` / `IPostVolumeManager` so their names reflect the current non-Volume-based responsibility.
- `ScreenEffect` sample: Kept `ScreenEffectCore` on the existing handler-driven setup flow and added matching sample handler scripts so effects follow the same integration pattern as grayscale, shake, gaussian blur, and depth outline.
- `GaussianBlurEffect` / `GaussianBlurPass`: Finalised the canonical gaussian blur naming for the full-screen blur API surface, sample handler, serialized sample field, and sample scene object names, with `Blur Radius` as the public control. If you adopted an earlier unreleased blur prototype from this branch, migrate it to `GaussianBlur*`.

### Fixed
- `ShakeEffect` / `ShakePass`: Changing `SetPeriod(float period)` now preserves the current shake position instead of jumping to a different offset mid-animation.
- `Mu3Library_URP/package.json`: Published the `ScreenEffect` sample through the package manifest so it is discoverable from the Unity Package Manager.

## [base/0.10.0] - 2026-04-26

### Added
- `IMVPManager`: Added parent-linked `Open<TPresenter>(IPresenter parent, ...)` overloads (four new signatures: with no extra args, with `Arguments`, with `OutPanelSettings`, and with both). Opening a presenter with a parent link connects the child's RectTransform to the parent, inheriting its anchored position, size delta, and local scale.
- `IPresenter`: Added `AnchoredPosition`, `SizeDelta`, and `LocalScale` properties for reading and writing the presenter's RectTransform layout values at runtime.

## [base/0.8.0] - 2026-04-05

### Added
- `AudioManager`: BGM playlist support via `PlayBgmPlaylist(AudioClip[] clips, ...)` and `StopBgmPlaylist()`.
  - Accepts an array of `AudioClip`s and plays them sequentially.
  - `loopCount`: 0 or less = infinite cycle; positive value = play that many full cycles (default: -1).
  - `shuffle`: randomises playback order using Fisher-Yates before each cycle (default: false).
  - `interval`: seconds to wait between tracks (default: 1.0).
  - Eight overloads follow the same pattern as `PlaySfx` for ergonomic API composition.
  - Calling `PlayBgmPlaylist` stops any currently playing BGM before starting.
  - Calling `StopBgm` or `StopBgmPlaylist` deactivates the playlist.
  - Interval countdown is pause-aware: timer does not advance while BGM is paused.
- `IAudioManager`: Extended with `PlayBgmPlaylist` overloads and `StopBgmPlaylist` via new `IAudioManager.BgmPlaylist.cs` partial file.
- `ResourcesPathExporterDrawer`: New editor drawer that scans all `*/Resources/*` paths in the project and generates a C# script with nested static classes reflecting the folder hierarchy. Each asset is exposed as a `ResourcePathData` field containing its resource-relative path (without extension) and file name.
- `ResourcePathData`: New class in the `Mu3Library.Resource.Data` namespace with `Path` and `Name` string properties.

### Changed
- `LocalizationNameExporterDrawer`, `AddressableGroupNameExporterDrawer`, `InputSystemNameExporterDrawer`: Renamed to `LocalizationDataExporterDrawer`, `AddressableGroupDataExporterDrawer`, and `InputSystemDataExporterDrawer` respectively. Associated sample `.asset` files also renamed accordingly.
- `LocaleData`, `EntryData`, `TableData`: Moved to the `Mu3Library.Localization.Data` namespace as standalone public classes; constructors changed from `internal` to `public`; removed `#if MU3LIBRARY_LOCALIZATION_SUPPORT` guards (no Unity.Localization dependency).
- `EntryData`: Added `TableName` property; constructor updated to `EntryData(string tableName, string key, string id)`.
- `LocalizationDataExporterDrawer`: Generated script no longer inlines `LocaleData`, `EntryData`, and `TableData` class definitions; instead imports them via `using Mu3Library.Localization.Data;`. `EntryData` construction now passes the table name as the first argument.
- `LabelData`, `EntryData`, `GroupData`: Added to the `Mu3Library.Addressable.Data` namespace as standalone public classes (no `#if` guard; pure C#). `GroupData` acts as a base class for generated per-group sealed classes, holding `Name`, `Entries`, and `Labels` dictionaries.
- `AddressableGroupDataExporterDrawer`: Generated script structure changed to mirror the Localization pattern — `Labels` class now holds `LabelData` instances instead of `const string`; `Groups` class now holds typed `*Data` group instances and an `IReadOnlyDictionary<string, GroupData> All`; per-group classes are `sealed class *Data : GroupData` with a constructor. Non-folder entries become `EntryData` fields; folder entries remain static classes with an `EntryData Data` field plus sub-entry `Assets` class. Generated output now includes `using Mu3Library.Addressable.Data;`.

## [base/0.6.0] - 2026-03-23

### Added
- `MVPManager` / `IMVPManager`: Added `FocusIgnoredLayers` property and `SetFocusIgnoredLayer(string layerName, bool ignored)` method.
  - Presenters on ignored layers are excluded from focus and `OutPanel` update calculations.
  - Ignored layers can be toggled at runtime; `UpdateFocus()` is called immediately on each change.
- `LocalizationNameExporterDrawer`: Generated script now includes a root `Locales` class (with an `All` string array and per-locale inner classes exposing `Code`, `EnglishName`, and `NativeName` as `const string`) and a root `Tables` class (with an `All` string array and a `const string` per table referencing the table class's `Name`). Each per-table class also gains a `Locales` inner class whose entries mirror the root `Locales` structure via `const string` references.
- `AddressableGroupNameExporterDrawer`: Generated script now includes a root `Groups` class (with an `All` string array and a `const string` per group referencing the group class's `Name`), a root `Labels` class (collecting all unique labels across every group and entry, with an `All` array and per-label `const string` values), and a per-group `Labels` inner class whose entries mirror the root `Labels` entries via `const string` references.

## [base/0.5.0] - 2026-03-18

### Changed
- Repository restructured to monorepo layout: `Mu3Library_Base/` and `Mu3Library_URP/` are now standalone UPM packages; `UnityProject_BuiltIn/` and `UnityProject_URP/` are separate development projects within the same repository.
- `.gitignore` updated with `**/` prefix patterns to support all sub-projects in the monorepo.

### Fixed
- `CoreBase.WaitForOtherCore`: Fixed `NullReferenceException` when `CoreRoot.Instance` returns null (e.g., during application quit).
- `CoreBase.GetClassFromOtherCore`: Same null safety fix applied.
- `ContainerScope.ResolveFromCore`: Same null safety fix applied.
- Documentation: Corrected `ConfigureContainer()` signature in all READMEs — removed incorrect `ContainerScope scope` parameter; service registration now uses `RegisterClass<T>()`.

## [base/0.4.7] - 2026-03-15

### Added
- `ScriptBuilder`: Added `ArrayBlock` struct (`FieldName`, `Values`) and `AppendArrayBlock` method.
  - `ArrayBlock` can be placed in a `CodeBlock.Content` list alongside `string` and `CodeBlock` items.
  - Indentation is handled automatically by `ScriptBuilder`, consistent with `CodeBlock` output.

### Changed
- `AddressableGroupNameExporterDrawer`: Replaced `BuildArrayLines` helper with `ScriptBuilder.ArrayBlock`.
  - `AllNames`, `AllAddresses` and `Labels.All` arrays are now declared as `ArrayBlock` entries, reducing per-array boilerplate from a `foreach` loop to a single `.Add()` call.

## [base/0.4.6] - 2026-03-15

### Added
- `AudioManager.Resource`: Added key-based `AudioClip` registration system.
  - `RegisterAudioResource(string key, AudioClip clip)`: Register a single clip under a key.
  - `RegisterAudioResources(Dictionary<string, AudioClip> resources)`: Batch register multiple clips.
- `IAudioManager` / `AudioManager`: Added `WithKey` overloads to play audio by registered key across all channel types.
  - BGM: `PlayBgmWithKey`, `PlayBgmForceWithKey`, `TransitionBgmWithKey`
  - SFX: `PlaySfxWithKey`, `StopFirstSfxWithKey`, `FadeInSfxWithKey`, `FadeOutFirstSfxWithKey`
  - Environment: `PlayEnvironmentWithKey`, `StopFirstEnvironmentWithKey`, `FadeInEnvironmentWithKey`, `FadeOutFirstEnvironmentWithKey`

### Changed
- `IAudioManager.Bgm`, `IAudioManager.Sfx`, `IAudioManager.Environment`: Sorted interface declarations alphabetically and grouped by action type for readability.
- `AudioManager.Bgm`, `AudioManager.Sfx`, `AudioManager.Environment`: Sorted public methods alphabetically.
- `WithKey` overloads use a delegation pattern — shorter overloads delegate to the full-argument overload, which calls `TryGetCachedAudioResource` once.

## [base/0.4.5] - 2026-03-14

### Changed
- `AddressableGroupNameExporterDrawer`: Sub-asset class names that begin with the parent class name now have the parent prefix stripped.
  - e.g. parent `Views`, sub-asset `ViewsDialoguePanelPrefab` → emitted as `DialoguePanelPrefab`.
  - Applied recursively through nested folder hierarchies.

## [base/0.4.4] - 2026-03-14

### Changed
- `AddressableGroupNameExporterDrawer`: Added folder entry support.
  - Folder entries registered in an Addressable group are now detected via `AssetDatabase.IsValidFolder()`.
  - Sub-assets inside a folder are collected with `GatherAllAssets()` and emitted as a nested `Assets` static class inside the folder entry class.
  - The editor preview marks folder entries with a `[Folder]` prefix and shows sub-assets indented beneath them.

## [base/0.4.3] - 2026-03-14

### Added
- `AddressableGroupNameExporterDrawer`: Added an editor drawer (guarded by `MU3LIBRARY_ADDRESSABLES_SUPPORT`) that reads all Addressable groups at editor time and exports their group names, asset names, addresses (keys), and labels as nested C# static classes.
  - `Labels` inner class provides individual `const string` fields per label and a `static readonly string[] All` containing all label values.
- `UtilWindow`: Added a sample `AddressableGroupNameExporter` drawer asset to the utility window drawer list.
- `Template`: Added `AddressableGroupKeys` as a generated example for Addressable group/address constants.
- `Mu3Library.Editor.asmdef`: Added optional references to `Unity.Addressables` and `Unity.Addressables.Editor` with `MU3LIBRARY_ADDRESSABLES_SUPPORT` version define.

## [base/0.4.2] - 2026-03-08

### Added
- `LocalizationNameExporterDrawer`: Added an editor drawer that exports Localization string table names and entry keys as C# constants for pre-declared lookup.
- `UtilWindow`: Added a sample `LocalizationNameExporter` drawer asset to the utility window drawer list.
- `Template`: Added `LocalizationTableKeys` as a generated example for Localization table/key constants.

### Changed
- `InputSystemNameExporterDrawer` and `LocalizationNameExporterDrawer`: Standardized private serialized helper member naming so backing fields and cached accessors are easier to distinguish while keeping behavior unchanged.

### Fixed
- `LocalizationNameExporterDrawer`: Fixed `SanitizeIdentifier` to produce proper PascalCase class names from entry keys. `-` and other non-identifier characters now act as word boundaries (dropped, next letter capitalized). `_` is preserved as-is and also capitalizes the next letter (e.g. `my-key_name` → `MyKey_Name`).

## [base/0.4.0] - 2026-03-08

### Added
- `AudioSourceSettings`: Added `LoopCount` and `LoopInterval` properties to control looping behavior per settings instance.
  - `LoopCount`: number of play cycles (`≤0` = infinite, `1` = one-shot).
  - `LoopInterval`: wait time in seconds between loop cycles.
- `AudioSourceSettings`: Added named preset instances for common use cases.
  - `Standard` (infinite loop, 2D), `OneShot` (play once, 2D)
  - `BgmStandard`, `BgmStandard3D`
  - `SfxStandard`, `SfxStandard3D`
  - `EnvironmentStandard`, `EnvironmentStandard3D`
- `Audio3dSoundSettings.Standard3D`: New preset with full 3D spatial blend (`spatialBlend = 1`).
- `AudioController`: Loop-with-interval playback driven by `LoopCount` and `LoopInterval` from `AudioSourceSettings`.
- `AudioController`: `FadeIn` / `FadeOut` coroutine API with optional completion callback.

### Changed
- `FadeInFirstSfx(AudioClip, float)` renamed to `FadeInSfx(AudioClip, float)` and behavior changed: now **plays a new SFX instance** from volume `0` and fades in, instead of targeting an already-playing instance.
- `FadeInFirstEnvironment(AudioClip, float)` renamed to `FadeInEnvironment(AudioClip, float)` with the same behavior change.
- `IAudioManager`: Removed `SourceSettings`, `BaseSettings`, and `SoundSettings` properties (superseded by per-call `AudioSourceSettings` parameter).
- `AudioManager` and `IAudioManager` refactored into partial class files split by category (`Bgm`, `Sfx`, `Environment`) for maintainability. No public API change.

## [base/0.3.3] - 2026-03-02

### Added
- `AudioManager`: Added `EnvironmentController` support for environment audio playback.
  - New `EnvironmentController` class: plays audio using `EnvironmentVolume` as its category volume.
  - `EnvironmentInstanceCountMax` property added (default: `3`, max: `5`).
  - `EnvironmentVolume`, `CalculatedEnvironmentVolume`, `ResetEnvironmentVolume()` added to `AudioManager` and `IVolumeSettings`.
  - `PlayEnvironment`, `StopFirstEnvironment`, `StopEnvironmentAll`, `PauseEnvironmentAll`, `UnPauseEnvironmentAll` methods added to `AudioManager` and `IAudioManager`.
  - `OnEnvironmentVolumeChanged` event added to `IAudioManagerEventBus`.
  - `Stop()`, `Pause()`, and `UnPause()` now include environment audio.

## [base/0.3.2] - 2026-03-02

### Fixed
- `Mu3WindowDrawer`: Added `DrawWithUndo<T>(Func<T>, Action<T>, string)` helper to the base class to eliminate repetitive `BeginChangeCheck` / `RecordObject` / `SetDirty` boilerplate in derived drawers.
- `Mu3WindowDrawer`: `DrawFoldoutHeader1` and `DrawFoldoutHeader2` now use `EditorGUI.BeginChangeCheck` / `EndChangeCheck` consistently instead of an explicit `!=` comparison.
- `DependencyCheckerDrawer`, `FileFinderDrawer`, `InputSystemNameExporterDrawer`, `MVPHelperDrawer`, `ScreenCaptureDrawer`: All interactive fields now correctly record undo/redo state via the new `DrawWithUndo<T>` helper.

## [base/0.3.1] - 2026-03-02

### Fixed
- `MVPManager`: Fixed a one-frame sync issue where the View rendered in its default prefab state before the open animation started.
  - Changed `Open()` to call `SetActiveView(false)` instead of `SetActiveView(true)`, and deferred `SetActiveView(true)` to just before `Open()` begins (after Load completes).
  - Animations (e.g. alpha 0→1) now start in sync with the View's intended initial state.

## [base/0.3.0] - 2026-03-01

### Added
- `InputSystemManager`: New Input System module (requires `MU3LIBRARY_INPUTSYSTEM_SUPPORT`):
  - Register `InputActionAsset` instances by custom ID; supports GUID-based and name-based action/map lookup.
  - Interactive rebinding via `StartInteractiveRebind(...)` with optional device-type filtering and cancel-control support.
  - Binding override serialization for per-asset, per-action-map, and per-action levels: save and load as JSON.
  - Enable/disable entire asset or individual action maps.
- `InputSystemNameExporterDrawer`: Editor drawer for exporting Input System action names as string constants.
- `LocalizationCharacterCollectorDrawer`: Editor drawer for collecting and reviewing characters across Localization string tables.
- `PresenterBase.CloseSelf(bool forceClose = false)`: A presenter can now close itself via the injected `IMVPManager` reference without needing an external caller.

### Changed
- `PresenterBase.Initialize(View, Arguments)` and `PresenterBase.Initialize(Arguments)` changed from `public` to `internal`.
  - Initialization is now managed exclusively by `MVPManager`; external code can no longer call these methods directly.
- `LayerCanvas` now synchronizes its Layer value to each child item automatically.

## [base/0.2.3] - 2026-02-16
### Changed
- Audio volume contract decoupled from event bus:
  - `IAudioVolumeSettings` no longer inherits `IAudioManagerEventBus`.
- Observable API now supports read-only exposure for external consumers:
  - Added `IObservableValue<TValue>` for `Value` + `Subscribe(...)` access.
  - `ObservableProperty<T>` and `ObservableDictionary<TKey, TValue>` now expose `ReadOnly`.
  - Subscription token handling was extracted to a dedicated `SubscriptionToken` file.
- MVP UI settings and runtime safety were improved:
  - `OutPanelSettings` is now serializable with explicit serialized backing fields.
  - `OutPanelSettings.Standard` default dim color alpha changed to `0.5f`.
  - `MVPManager` now validates `EventSystem` during focus updates and logs explicit errors if missing.

## [base/0.2.0] - 2026-02-14

### Added
- Scene UniTask APIs with cancellation support:
  - `ISceneLoader.LoadSingleSceneAsync`
  - `ISceneLoader.LoadAdditiveSceneAsync`
  - `ISceneLoader.UnloadAdditiveSceneAsync`

### Changed
- Addressables and Localization initialization contracts now expose explicit result state:
  - `IsInitialized`
  - `IsInitializing`
  - `InitializeError`
  - `OnInitializeResult` event
  - `InitializeWithResult(Action<bool, string>)` API
- WebRequest API now provides structured result variants:
  - `WebRequestResult<T>` with `IsSuccess`, `StatusCode`, `ErrorMessage`, `ResponseHeaders`, `Data`
  - Callback APIs: `GetWithResult`, `PostWithResult`, `GetDownloadSizeWithResult`
  - UniTask APIs: `GetResultAsync`, `PostResultAsync`, `GetDownloadSizeResultAsync`
  - Added request timeout and retry options for result-based APIs.
- Core execution order is now deterministic via `CoreBase` serialized execution order setting.
- Scene unload lifecycle now emits explicit events:
  - `OnAdditiveSceneUnloadStart`
  - `OnAdditiveSceneUnloadEnd`
  - `LoadingCount` now includes additive unload operations.
- Separated service event contracts into dedicated event bus interfaces:
  - `IAddressablesManagerEventBus`
  - `ILocalizationManagerEventBus`
  - `ISceneLoaderEventBus`
  - `IMVPManagerEventBus`
  - `IAudioManagerEventBus`
  - Existing service interfaces no longer declare these `event` members directly.

## [base/0.1.11] - 2026-02-08

### 🌟 Overview

**Major architectural overhaul** - Complete package restructure with new DI Container system, enhanced MVP pattern, and comprehensive manager systems. This release represents a fundamental redesign of the library's core architecture.

### ⚠️ BREAKING CHANGES

#### Package Structure
- **Complete folder reorganization**: Separated into `Runtime/` and `Editor/` directories
- **Assembly definitions updated**: New asmdef files with proper dependencies
- **Namespace changes**: All code moved to `Mu3Library` namespace hierarchy
  - `Mu3Library.DI` for Dependency Injection
  - `Mu3Library.MVP` for UI pattern
  - Module-specific namespaces for Audio, Scene, etc.

#### API Changes
- **MVP Pattern**: Complete rewrite of MVP system
  - `Presenter<TView, TModel, TArgs>` signature changed
  - `View` lifecycle methods restructured
  - `MVPManager` API completely redesigned
  - Animation system integrated into MVP views
- **DI Container**: New injection system replaces manual initialization
  - `[Inject]` attribute for dependency injection
  - Core-based architecture with `CoreBase` and `CoreRoot`
- **ResourceLoader**: Now DI-based, no longer static
- **SceneLoader**: Interface-based with separate implementations
  - `ISceneLoader.Editor` for Editor scenes
  - `ISceneLoader.Addressables` for Addressables scenes

#### Removed Features
- ❌ **InputSystem helpers** (`InputSystem_Actions`, `InputSystemHelper`)
- ❌ **MarchingCubes system** (entire compute shader system removed)
- ❌ **PostEffect/CommandBuffer effects** (Blur, EdgeDetect, GrayScale, Toon shaders)
- ❌ **Camera view components** (FirstPerson, ThirdPerson, FreeView cameras)
- ❌ **Custom UI components** (DatePicker, IntSlider)
- ❌ **Old sample scenes** (Sample_InputAction, Sample_CustomUI, Sample_CommandBufferEffect, Sample_CameraView, Sample_MarchingCubes, Sample_RenderingPipeline)

### ✨ Major Features

#### 🏗️ Dependency Injection (DI) Container
- Custom DI container with three lifetime scopes:
  - `Singleton`: One instance per container
  - `Transient`: New instance per request
  - `Scoped`: One instance per scope
- `CoreBase` architecture for modular system design
- `[Inject]` attribute for automatic dependency resolution
- Cross-core injection support with `[Inject(typeof(OtherCore))]`
- Interface-based lifecycle management:
  - `IInitializable` for initialization
  - `IUpdatable` for Update loop
  - `ILateUpdatable` for LateUpdate loop
  - `IDisposable` for cleanup

#### 🎨 Enhanced MVP Pattern
- Completely redesigned MVP system with DI integration
- **AnimationView** system with configurable animations:
  - `OneCurveAnimation` for single-curve animations
  - `TwoCurveAnimation` for dual-curve animations
  - `AnimationConfig` ScriptableObject for reusable configs
- **MVPCanvasSettings** for fine-grained canvas configuration:
  - Canvas component settings
  - CanvasScaler settings
  - GraphicRaycaster settings
  - OutPanel system for backdrop/dimming
- Enhanced view lifecycle with proper initialization order
- Resource-based and Camera-based view loading
- Loading screen integration

#### 📦 Manager Systems
- **AddressablesManager**: Full Addressables support with caching
  - Load/unload assets with reference counting
  - Scene loading support
  - Progress tracking
  - UniTask integration
- **LocalizationManager**: Unity Localization integration
  - Async initialization
  - Locale switching
  - String table access
  - UniTask support
- **WebRequestManager**: HTTP request handling
  - GET/POST requests
  - Download size queries
  - UniTask integration
  - Callback-based alternatives
- **AudioManager**: Enhanced audio system
  - 3D spatial audio support
  - Separate BGM and SFX controllers
  - Volume management through `IVolumeSettings`
  - AudioSource pooling
- **SceneLoader**: Flexible scene loading
  - Editor scene loading
  - Addressables scene loading
  - Additive scene support
  - Progress events
  - Scene load policies (allow/prevent duplicates)
- **ResourceLoader**: Enhanced Resources folder management
  - Type-safe loading
  - Caching with reference counting
  - UniTask support

#### 🔧 Utility & Extensions
- **Observable types**: Data-binding support
  - `ObservableProperty<T>`, `ObservableBool`, `ObservableInt`, `ObservableFloat`, `ObservableLong`, `ObservableString`
  - `ObservableList<T>` with collection change events
  - `ObservableDictionary<TKey, TValue>` with dictionary events
- **GameObjectPool**: Component pooling system
- **Extensions**: Rich extension methods
  - `CameraExtensions`: Camera property copying
  - `TransformExtensions`: Layer management with children
  - `intExtensions`: Bitwise operations
  - Canvas-related extensions
- **PlayerPrefsLoader**: Type-safe PlayerPrefs access

### 🎯 Added

#### Core Systems
- `CoreBase` and `CoreRoot` for modular architecture
- `ContainerScope` for service registration and resolution
- `ServiceDescriptor` for service configuration
- Automatic lifecycle management through interfaces

#### UI/MVP
- `AnimationHandler` for view animations
- `AnimationConfig`, `OneCurveAnimation`, `TwoCurveAnimation` ScriptableObjects
- `OutPanel` system for UI backdrops
- `MVPCanvasSettings` for granular canvas control
- `IMVPManager` interface with Camera and Resource variants

#### Managers
- `IAddressablesManager` with full CRUD operations
- `ILocalizationManager` for localization
- `IWebRequestManager` for network requests
- `IAudioManager` with volume control interface
- `ISceneLoader` with Editor and Addressables implementations
- `IResourceLoader` for Resources management
- `IPlayerPrefsLoader` for PlayerPrefs

#### Editor Tools
- **Mu3Window**: Unified utility window
  - MVPHelper: Generate MVP boilerplate code
  - SceneList: Quick scene navigation
  - FileFinder: Asset search and organization
  - ScreenCapture: In-editor screenshots
  - DependencyChecker: Package dependency validation
- Custom property drawers for Observable types
- `ScriptBuilder`: Code generation helper

#### Samples
- **Template**: Comprehensive sample project
  - Sample_MVP: MVP pattern demonstration
  - Sample_Audio: Audio system showcase
  - Sample_Audio3D: 3D spatial audio example
  - Sample_WebRequest: HTTP request examples
  - Sample_Addressables: Asset loading demonstration
  - Sample_AddressablesAdditive: Additive scene loading
  - Sample_Localization: Multi-language support
  - LoadingScreen implementation
  - Splash screen with animations

#### Assets
- Basic color materials (Black, White, Red, Green, Blue, Magenta)
- Sample fonts (NotoSans, NotoSansJP, NotoSansKR with SDF)
- Sample BGM tracks (3 songs)
- Sample SFX sounds (3 effects)
- UI texture assets (circles with shadows, 1px square)
- Scene thumbnails for samples

### 🔧 Changed

#### Architecture
- Package name: `com.github.doqltl179.mu3libraryassets.base`
- Unity version requirement: 6000.0+ (Unity 6)
- Namespace restructure: All code under `Mu3Library.*`
- Assembly separation: Runtime and Editor assemblies

#### MVP System
- `Presenter` lifecycle completely redesigned
- `View` now supports animation integration
- Model-View-Presenter binding improved
- Canvas management centralized in `MVPCanvasSettings`
- View instantiation now supports Resources and Camera-based loading

#### Audio System
- Split into `BgmController` and `SfxController`
- Added `AudioSourceSettings` for fine-grained control
- 3D audio positioning support
- Volume change events through `IAudioVolumeSettings`

#### Scene Management
- Interface-based design with multiple implementations
- Progress events for loading operations
- Duplicate scene load policies
- Better async operation support

#### Observable Pattern
- Extended to support multiple primitive types
- Added collection types (List, Dictionary)
- Custom property drawers for editor integration
- Value change callbacks

#### Extensions
- Renamed `Overwrite` to `CopyTo`
- Organized by component type
- Added layer management helpers
- Camera property copying

### 🐛 Fixed

#### Critical Fixes
- **DI Container lifecycle bug**: Fixed service lifetime management issues
- Multiple interface implementation now correctly shares single instance
- Collection immutability: Made collections readonly where appropriate
- SceneLoader event timing: Corrected `OnSceneLoadEnd` callback timing
- Null reference handling throughout codebase

#### Stability Improvements
- AnimationView exception handling enhanced
- LocalizationManager initialization robustness
- Scene loading state management
- MVP view lifecycle edge cases

### 🗑️ Removed

#### Complete System Removals
- InputSystem helper classes and generated code
- MarchingCubes compute shader system
- CommandBuffer post-processing effects
- Camera controller components
- Custom UI components (DatePicker, IntSlider)
- Old sample scenes (6 samples removed)
- Shader collection (Toon, Blur, EdgeDetect, GrayScale, etc.)

#### Code Cleanup
- Removed unused utility functions
- Removed deprecated MVP implementation
- Removed old pool system (replaced with GameObjectPool)
- Removed old singleton implementation
- Removed legacy Observable implementation

### 📦 Dependencies

#### Added
- ✅ **com.cysharp.unitask**: UniTask for async operations
- ✅ **com.coplaydev.unity-mcp**: Unity MCP integration
- ✅ **com.unity.localization** (1.5.9): Localization support
- ✅ **com.unity.addressables** (implicitly through package manager)

#### Updated
- Unity 6000.0+ (Unity 6) required
- .NET Standard 2.1

#### Removed
- ❌ Old Unity-MCP package (IvanMurzak)

### 📚 Documentation

#### Added
- Multi-language README files (English, Japanese, Korean)
- MIT License
- Comprehensive inline documentation (XML comments)
- GitHub Copilot agent files for development assistance
- Unity-specific instruction files

#### Improved
- README with detailed feature descriptions
- Installation instructions (Git URL and local disk methods)
- Code examples for all major features
- Sample scene documentation

### 🔄 Migration Guide

#### For Users of v0.0.20

**⚠️ This is a major breaking release. A full project update is recommended.**

##### Step 1: Clean Installation
1. Remove the old package from your project
2. Delete any cached files in `Library/`
3. Install v0.1.11 using the new Git URL:
   ```
   https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Assets/Mu3LibraryAssets
   ```

##### Step 2: Update Namespaces
```csharp
// Old (0.0.20)
using Mu3LibraryAssets;

// New (0.1.11)
using Mu3Library;
using Mu3Library.DI;
using Mu3Library.MVP;
```

##### Step 3: Migrate to DI Architecture
The new version uses Dependency Injection. Update your initialization code:

```csharp
// Old: Manual initialization
public class GameManager : MonoBehaviour
{
    private AudioManager audioManager;

    void Start()
    {
        audioManager = FindObjectOfType<AudioManager>();
    }
}

// New: DI-based approach
public class AudioCore : CoreBase
{
    protected override void ConfigureContainer(ContainerScope scope)
    {
        scope.Register<IAudioManager, AudioManager>(ServiceLifetime.Singleton);
    }
}

public class GameManager : CoreBase
{
    [Inject] private IAudioManager _audioManager;

    protected override void Start()
    {
        base.Start(); // Required for injection
        // Use _audioManager
    }
}
```

##### Step 4: Update MVP Code
If you were using the old MVP pattern:

```csharp
// Old Presenter
public class OldPresenter : Presenter<MyView, MyModel>
{
    // Old structure
}

// New Presenter
public class NewPresenter : Presenter<MyView, MyModel, MyArgs>
{
    // Must define Arguments class
}

public class MyArgs : Arguments { }
```

##### Step 5: Replace Removed Features
- **InputSystem**: Use Unity's Input System directly
- **Camera Controllers**: Implement custom or use third-party solutions
- **PostEffects**: Use Unity's Post Processing Stack or URP/HDRP volume system
- **Custom UI**: Use Unity's UI Toolkit or create custom components

##### Step 6: Update Resource Loading
```csharp
// Old: Static calls
var asset = ResourceLoader.Load<Sprite>("path");

// New: DI-based
public class MyCore : CoreBase
{
    [Inject] private IResourceLoader _resourceLoader;

    void LoadAsset()
    {
        _resourceLoader.Load<Sprite>("path", (sprite) => {
            // Use sprite
        });
    }
}
```

##### Step 7: Test Thoroughly
- Verify all DI injections are working
- Check MVP views are loading correctly
- Test audio playback
- Validate scene transitions
- Check Addressables loading if used

### 🎉 Acknowledgments

Special thanks to the open-source community for:
- UniTask by Cysharp (async/await support)
- Unity MCP by CoplayDev (Model Context Protocol)
- Sample audio assets from various Creative Commons sources

---

## [base/0.0.20] - Previous Release

### Added
- ObservableProperty implementation

For earlier versions, please refer to commit history.

[urp/0.1.2]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/urp%2Fv0.1.2
[base/0.10.0]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.6.0...base%2Fv0.10.0
[base/0.2.3]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.2.0...v0.2.3
[base/0.2.0]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.1.11...v0.2.0
[0.1.11]: https://github.com/doqltl179/Mu3Library_ForUnity/compare/v0.0.20...v0.1.11
[0.0.20]: https://github.com/doqltl179/Mu3Library_ForUnity/releases/tag/v0.0.20
