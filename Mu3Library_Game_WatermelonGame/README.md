# Mu3 Library Game - Watermelon

Reusable 2D Watermelon Game board runtime for Unity 6.3 and later. The runtime assembly is `Mu3Library.Game.WatermelonGame` and references `Mu3Library` and `Mu3Library.URP`, and `Unity.InputSystem` while the Input System package is installed.

## Runtime API

`BoardArea` fits a board sprite to a camera viewport with configurable padding, creates the left/right/bottom collision boundaries, and exposes board-local and board-screen normalized-position conversions with initialization-safe `Try...` variants.

`BoardController` coordinates falling items and merges through `BoardItem`, `BoardConfig`, and `BoardItemsConfig`. Call `SetBoardConfig(BoardConfig)` before `Prepare`, then call `GameStart()` after a successful preparation. The default Watermelon Game uses the first eleven fruit entries addressed by zero-based list index. Extra catalog entries are preserved for future rules but are not spawned or merged by the default rules.

The board refuses a configuration until each of the first eleven item entries carries a sprite, since an item is sized from its sprite. `BoardItemsConfig.IsComplete(List<int>)` lists the entries that lack one, and both the error the board logs and the warning `BoardConfig` logs while the asset is edited name their indices. A configuration applied during a game keeps every item at the same place relative to the board, also when its board sprite has another size. Hiding the board hierarchy, for a pause menu or a screen change, keeps every item as it was; only an item that goes back to the pool is reset.

`BoardConfig.BoardSpawnGuideLineSprite` configures the one-segment sprite used for the vertical, tiled spawn guide line. The guide line appears only during a drag, is two fifths of the spawn marker's width, and renders at board sorting order `+1`; the spawn marker is drawn at `+2`.

`BoardConfig.SoundConfig` holds the optional board sounds, but not the volumes they are played at. Every clip can be left empty, and a moment without one stays silent, so a board can be configured with only the sounds a project already has. It carries one clip per `BoardSoundType` (`GameStart`, `GameEnd`, `ItemDrop`), a BGM playlist with shuffle, inter-track interval, and cycle count, and the merge clip list. The playlist starts on `GameStart()` and stops on `GameEnd()`.

Merge sounds follow the combo: the first merge plays the first clip of `ItemMergeClips`, and every merge that lands within `MergeComboInterval` (5 seconds by default) of the one before it steps one clip further, stopping at the last one. A merge that comes later starts the combo over. `BoardController.MergeComboIndex` exposes the current step, and `PlayBoardSound(BoardSoundType)` / `PlayItemMergeSound()` are `protected virtual`.

The combo counts every merge whether or not the configuration carries merge sounds, so `OnMergeComboChanged` drives a combo display on a silent board as well: the first merge reports 0, and `Prepare` reports -1 when it drops a running combo. The step climbs no further than the last entry of `ItemMergeClips`, empty slots included, so it stays at 0 while that list holds one entry or none.

Board sounds are played through `Mu3Library.Audio.AudioManager`. `BoardController.AudioManager` accepts the `IAudioManager` a project already runs, which the board then shares without touching its lifetime or its volumes. While none is assigned the board creates one of its own with the first sound it plays, drives it from `Update`, applies `BoardController.BgmVolume` to it, and disposes it with the board.

The volumes are not part of the configuration; the project sets them on the board. `BoardController.SfxVolume` (1 by default) is passed to the audio manager per sound, which scales it with its own SFX and master volume, so it works on a shared manager as well. `BoardController.BgmVolume` (0.8 by default) is only applied to the audio manager the board created for itself, and takes effect right away while that one is playing; an assigned manager keeps the BGM volume the project set on it. Both clamp to 0 to 1.

`BoardItemScoreRule.GetScore(int)` is virtual and defaults to the triangular Watermelon Game score progression, allowing external projects to override scoring.

## Events

`BoardController` reports what happens on it so a project can drive its UI and its effects without polling the board:

| Event | Raised with |
| --- | --- |
| `OnBoardPrepared` / `OnGameStarted` / `OnGameEnded` | Nothing; the session moved on. |
| `OnScoreChanged` | The new score, also when `Prepare` starts the board over or restores a snapshot with another score. |
| `OnScoreAdded` | The points a single change paid out, and the score they were added to. A change that moves nothing, because the score stops at zero, is not reported. |
| `OnNextItemChanged` | `NextItemIndex`, the preview one item ahead. |
| `OnBoardConfigChanged` | The configuration that was applied to the board and to every item on it. |
| `OnHoldingItemChanged` | The item now in the player's hand, null when the hand was emptied. |
| `OnHoldingItemMoved` | Where the held item was moved to, as a fraction of the board area width; `HoldingNormalizedX` reads the same value. |
| `OnItemDropped` | The item the player released, already falling and counted. |
| `OnItemAdded` / `OnItemRemoved` | An item that joined or left the board, whoever put it there. A removed item still carries its catalog entry and its place while the event runs. |
| `OnItemMerged` | A `BoardMergeInfo`: the entry that merged, the entry it became, the item it became, where it happened, and the points it paid. |
| `OnMergeComboChanged` | The merge combo step, also on a board without merge sounds; -1 when the combo is dropped. |
| `OnCommandEnqueued` / `OnCommandFinished` / `OnCommandFailed` | The board command that was taken, finished, or threw. |

```csharp
boardController.OnItemMerged += info =>
{
    if (!info.IsValid)
    {
        return;
    }

    Vector2 localPosition = boardArea.BoardLocalNormalizedPositionToLocal(info.BoardNormalizedPosition);
    ShowScorePopup(localPosition, info.Score);

    if (info.MergedItemIndex < 0)
    {
        ShowLastItemBanner();
    }
};
```

Every merge is reported, the ones the board finds by itself and the ones a command carries out, because both run through `CountMerge(BoardMergeInfo)`. `CountMerge()` still counts a merge that cannot tell what it merged, and reports `BoardMergeInfo.Unknown`.

A handler can call `GameEnd()` or `Prepare()`, as a rule that wins the game on the first watermelon does from `OnItemMerged`. The board then hands out no further item in that frame, and a drag on a board that is not running neither moves nor drops the item in hand. The commands that frame already advances still finish their step, so a second merge found in the same frame is still scored and reported after the handler ended the game.

## Commands

The board runs its work as commands, and a project can hand it its own. `BoardController.EnqueueCommand(IBoardCommand)` takes a command, `CancelCommand`, `CancelCommands<T>()` and `CancelAllCommands()` stop one, `HasCommand<T>()` and `Commands` report what is queued, and `OnCommandEnqueued`, `OnCommandFinished` and `OnCommandFailed` follow it. Commands are advanced only while the board runs, so one given to a prepared board waits for `GameStart()`, and `Prepare` drops whatever is left.

`IBoardCommand` is the whole contract: `Run()` until it reports `IsRunning` or `IsCompleted`, then `Dispose()`. Add `IUpdatableBoardCommand` to be advanced every frame and `ICancelableBoardCommand` to be stoppable. `BoardCommand` implements all three and hands out the lifecycle hooks instead, so a command only overrides `OnRun`, `OnUpdate`, `OnComplete`, `OnCancel` and `OnDispose`, and closes itself with `Complete()` or `Cancel()`. `BoardCommandRunner` drives one command through the optional parts of the contract; the board queue and the command groups both run on it, and it can host commands outside a board as well.

`BoardController.CommandContext` is what a command is given to reach the board: `TrySpawnItem`, `TryReplaceItem`, `RemoveItem`, `ContainsItem`, `AddScore`, `CountMerge()`, `CountMerge(BoardMergeInfo)`, `PlayBoardSound`, `PlayItemMergeSound`, `EnqueueCommand`, and the `Area`, `Config`, `ScoreRule`, `Items` and `HoldingItem` it reads. A command written outside this package only takes `IBoardCommandContext`, so it never reaches into the board itself.

The package carries these commands:

| Command | What it does |
| --- | --- |
| `Flow.ActionCommand` | Runs one callback. |
| `Flow.DelayCommand` | Waits for a while. |
| `Flow.WaitUntilCommand` | Waits until the board is in a given state, with an optional timeout that cancels it. |
| `Flow.SequenceCommand` | Runs commands one after another; a step that gives up cancels the sequence. |
| `Flow.ParallelCommand` | Runs commands side by side and finishes with the last one. |
| `Flow.CompositeBoardCommand` | The base the two groups above are built on, for an order this package does not carry. |
| `Item.MergingCommand` | Merges a pair of equal items: both leave, the points are paid, the next entry falls in where they met. |
| `Item.SpawnItemCommand` | Drops an item onto the board without the player holding it. |
| `Item.RemoveItemsCommand` | Takes items off the board, optionally paying out their merge score. |
| `Item.PromoteItemCommand` | Grows a single item into a later catalog entry where it stands. |
| `Item.ShakeBoardCommand` | Pushes every item around for a while, measured against the board height. |
| `Score.AddScoreCommand` | Pays points into the board score, negative for a penalty. |

`MergingCommand` is the one merge of this package. The board builds one for every touching pair it finds, and a project builds one for a pair it picked itself, a magnet power-up pulling together two items that never touched — touching is only the board's own condition, the command asks for nothing but the same catalog index. It reserves the pair while it waits its turn, so nothing else can claim it. `GetMergedIndex`, `GetMergeScore`, `SpawnMergedItem`, `TryGetMergedPosition` and `PlayMergeSound` are `protected virtual`, and `BoardController.CreateMergingCommand(BoardItem, BoardItem)` is the factory to override so the whole board runs a subclass.

A scripted board moment is those pieces put together:

```csharp
IBoardCommandContext context = boardController.CommandContext;

boardController.EnqueueCommand(new SequenceCommand(
    new SpawnItemCommand(context, 0, new Vector2(0.5f, 0.95f)),
    new DelayCommand(0.5f),
    new ShakeBoardCommand(context, 0.4f),
    new AddScoreCommand(context, 100)));
```

A project's own command derives from `BoardCommand` and reaches the board through the context:

```csharp
public class ClearRowCommand : BoardCommand
{
    private readonly IBoardCommandContext _context;
    private readonly float _normalizedY;

    public ClearRowCommand(IBoardCommandContext context, float normalizedY)
    {
        _context = context;
        _normalizedY = normalizedY;
    }

    protected override void OnRun()
    {
        List<BoardItem> items = new();
        foreach (var item in _context.Items)
        {
            if (item != null &&
                _context.Area.TryLocalToBoardLocalNormalizedPosition(item.transform.localPosition, out Vector2 position) &&
                Mathf.Abs(position.y - _normalizedY) < 0.05f)
            {
                items.Add(item);
            }
        }

        _context.EnqueueCommand(new RemoveItemsCommand(_context, items, true));

        Complete();
    }
}
```

`BoardArea` also calculates aspect-preserving local, screen, and world rectangles, mathematical screen-to-board-plane conversions, boundary-aware area checks, and position clamping helpers.

Runtime configuration changes validate the complete default catalog before updating the board, active items, and the held item together. Pool reuse resets item presentation and physics state before the next item is initialized.

## Input

`BoardArea` takes its touches from the `InputHandler` on the same GameObject, which it adds itself when there is none, also on a board created in code with `AddComponent<BoardArea>()`. The handler turns the mouse and the touches into one drag at a time: a press inside the board takes the held item along, and the release drops it there.

The handler reads the input backend that **Active Input Handling** (Project Settings > Player) enables:

| Active Input Handling | Input System package | `InputHandler` reads |
| --- | --- | --- |
| Input Manager (Old) | Not needed | `UnityEngine.Input` |
| Input System Package (New) | Required | `Mouse.current` and the touches of the EnhancedTouch API |
| Both | Installed | The Input System, as under "Input System Package (New)" |
| Both | Not installed | `UnityEngine.Input` |

The package does not depend on the Input System. Its assembly defines `MU3LIBRARY_INPUTSYSTEM_SUPPORT` while `com.unity.inputsystem` is installed, and the handler reads the Input System while that symbol and Unity's `ENABLE_INPUT_SYSTEM` are both defined. While it reads the touches it holds one `EnhancedTouchSupport.Enable()` call, which `OnDisable` gives back. Under "Input System Package (New)" without the package there is nothing it can read, and it logs a warning once.

A drag that is taken away before it is released is canceled, not dropped: the `InputHandler` is disabled, the touch it follows vanishes or is canceled by the system, or the mouse is disconnected. `InputHandler.OnTouchCanceled`, `BoardArea.OnTouchCanceled`, and `BoardController.OnDragCancel(Vector2)` report it in place of `OnTouchEnded` and `OnDragEnd`, with the screen position the `InputHandler` last reported for the drag, which can lie outside the board since moves outside it are not forwarded to the board. The held item stays in hand where the drag last put it, and nothing is dropped or counted, so a project can switch the input off for a popup, an ad, or the back button while the player holds the item. A listener that follows a drag through `OnTouchEnded` also listens to `OnTouchCanceled`.

A press that lands on UI does not start a drag. The handler raycasts the press position through the current `EventSystem`, which reaches uGUI canvases and UI Toolkit panels; it reaches a UI Toolkit panel only while that `EventSystem` has an input module and keeps its UI Toolkit integration on, as it does by default. A hit of a `PhysicsRaycaster` or `Physics2DRaycaster` is a scene object, such as a board item, and does not count, and without an `EventSystem` no press is held back. A press that was held back stays no drag until it is released, also when it moves onto the board. Turn **Ignore Press Over UI** off on the `InputHandler`, or set `IgnorePressOverUI` to false, to let such presses through. A full-screen raycast target over the board holds back every press, so leave **Raycast Target** off on a graphic that only decorates the board.

`InputHandler.DragStartFilter` holds a press back by the project's own rule. It is asked with the screen position of every mouse press and touch before it starts a drag, and every function added has to return true:

```csharp
InputHandler inputHandler = boardArea.GetComponent<InputHandler>();
inputHandler.DragStartFilter += screenPosition => !pauseMenu.IsOpen;
```

A subclass can override `CanStartDrag(Vector2)` and `IsPointerOverUI(Vector2)`, and drive the handler from another input source by overriding `IsMousePresent`, `MousePosition`, `GetMouseButtonDown()`, `GetMouseButton()`, `TouchCount`, and `GetTouch(int)` together. `BoardArea` uses a subclass that is already on its GameObject instead of adding an `InputHandler`.

## Install

The package depends on Base and URP, so install the three packages in the order Base, URP, Watermelon Game. In Unity, open **Window > Package Manager**, choose **+ > Add package from git URL...**, and enter each URL in that order, with `<version>` replaced by the release to install:

```text
https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Base#base/v<version>
https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_URP#urp/v<version>
https://github.com/doqltl179/Mu3Library_ForUnity.git?path=Mu3Library_Game_WatermelonGame#game/watermelon/v<version>
```

The Installation section of the [repository README](https://github.com/doqltl179/Mu3Library_ForUnity) gives these URLs with the latest release tags.

The development projects in this repository reference the package folder by a relative path instead, which resolves only from a `UnityProject_*` folder of this repository:

```json
"com.github.doqltl179.mu3library.game.watermelon": "file:../../Mu3Library_Game_WatermelonGame"
```

## Sample

Import the `Watermelon Game` package sample to receive the `BoardConfig` asset, fruit/background and board-guide images, sample manager/core scripts, and playable `Demo` scene.
