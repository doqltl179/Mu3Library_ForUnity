---
owns: "Design rules the package runtime has settled on: Base managers' callbacks, initialization, disposal, object ownership, and shared command guards; superseded async operations; the Watermelon board command contracts and list; and the Watermelon board and sample rules"
volatility: evolving
reviewed: 2026-10-30
---

# Runtime Design

Rules the package runtime follows because a defect or review proved them. Each names the code that shows the pattern.

## When

- changing or reviewing a Base manager's initialization, subscriptions, disposal, or async operations,
- changing the Watermelon board, its commands, or its sample.

## Route Away When

- package surfaces, assemblies, DI, and C# conventions: [package-architecture.md](rules/package-architecture.md),
- verifying the change: [unity-verification.md](unity-verification.md),
- editing a sample scene or prefab: [unity-yaml-guide.md](unity-yaml-guide.md).

## Managers

- A callback parameter that belongs to one call, such as the one `Initialize(Action)` takes, is registered through the manager's own one-shot subscription (`SubscribeOnInitializedOnce` and its siblings), never with `event +=`: a retried call would stack the same callback once per attempt, and a completed operation would keep holding it.
- A one-shot subscription allows for the event arriving synchronously while it registers, and a failed registration cleans up before it returns or rethrows.
- A manager that offers both `Initialize(Action)` and `InitializeAsync()` keeps one owner of the acquire-attach-complete sequence, and the async entry point awaits the handle that owner produced instead of repeating the sequence; `LocalizationManager` and `AddressablesManager` both route through `BeginInitialize()`. Every initialize entry point of one manager reports failure the same way, through `InitializeError` and `OnInitializeResult`; none of them rethrows.
- A manager that owns subscriptions, cancellation tokens, or operation handlers implements `IDisposable`, so its DI scope tears it down with its core. A manager in the same family that does not is the one that leaks.
- Before `Dispose` destroys an object, it checks an ownership flag set when the library created that object, not a null check, because the same field can hold a scene object or a project asset instead. `MVPManager` destroys only an `EventSystem` it created (`_isEventSystemOwned`), and `LocalizationManager` only the fallback `Locale` it created.
- When several backends run the same command through different APIs, one helper holds the shared guard sequence and answers "settled with this result" or "go on"; each backend keeps only its own checks, in its own order. `SceneLoader` does this with `SceneCommandGate` for the built-in, editor, and Addressables scene commands.

## Async Operations

- An operation that a newer call can supersede never settles shared state from its own `finally`. `LocalizationManager.ChangeLocaleAsync` once cleared `IsLocaleChanging` after the call that replaced it had set it, so the newer change reported itself finished. The cancellation source is the ownership token: settle the state only while the field still points at the source this call created.
- Never `await` inside a `finally` that runs while an exception is in flight. A `UniTask.WaitUntil` there carries no timeout and no token, so the wait outlives the exception it holds.

## Watermelon Board Commands

- `IBoardCommand` stays minimal. Per-frame updating and cancellation live in the optional `IUpdatableBoardCommand` and `ICancelableBoardCommand`; the lifecycle base class `BoardCommand` implements all of them, while a caller that owns its own state machine implements only the minimal contract.
- A command reaches the board through `IBoardCommandContext`, which the board implements, never through loosened member access. Protected members that the interface also exposes are reached through explicit interface implementations.
- Commands may add commands to the list that runs them or remove them from it, so the list defers structural changes: `BoardController` buffers additions in `_pendingCommands`, records removal requests, and applies both outside the loop that advances the list.

## Watermelon Board And Sample

- The collider decides gameplay, so a resolution-relative resize sizes the collider and draws the sprite around it, and a per-asset shape correction changes only the art. Fitting the sprite to the item diameter and shrinking the collider inside it made a new `BoardConfig` move every item index's contact area.
- When a fixed default rule keeps catalog entries meant for later extensions, the default simulation is gated to the range it supports, so extension-only entries are neither activated nor destroyed early.
- The sample keeps `Prepare` and `GameStart` as separate lifecycle stages: it subscribes before preparing and starts only from the prepared event, so a failed preparation cannot publish a running board.
- A generated guide sprite that the `SpriteRenderer` tiles holds one source segment, and the renderer repeats it; the repeated pattern is never baked into the image.
- Generated sample audio uses layered harmonic timbres, restrained ambience, and a sufficiently long musical bed. Oscillator-only effects read as 8-bit, which misses a request for a polished subculture-game feel.
