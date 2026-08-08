# Mu3 Library Game - Watermelon

Reusable 2D Watermelon Game board runtime for Unity 6. The runtime assembly is `Mu3Library.Game.WatermelonGame` and references `Mu3Library` and `Mu3Library.URP`.

## Runtime API

`BoardArea` fits a board sprite to a camera viewport with configurable padding, creates the left/right/bottom collision boundaries, and exposes local, screen, and world normalized-position conversions with initialization-safe `Try...` variants.

`BoardController` coordinates falling items and merges through `BoardItem`, `BoardConfig`, and `BoardItemsConfig`. Call `SetBoardConfig(BoardConfig)` before `Prepare`, then call `GameStart()` after a successful preparation; the legacy `SetBoareConfig` spelling remains as a compatibility alias. The default Watermelon Game uses the first eleven fruit entries addressed by zero-based list index. Extra catalog entries are preserved for future rules but are not spawned or merged by the default rules.

`BoardConfig.BoardSpawnGuideLineSprite` configures the one-segment sprite used for the vertical, tiled spawn guide line. The guide line appears only during a drag, is two fifths of the spawn marker's width, and renders at board sorting order `+1`; the spawn marker is drawn at `+2`.

`BoardConfig.SoundConfig` holds the optional board sounds, but not the volumes they are played at. Every clip can be left empty, and a moment without one stays silent, so a board can be configured with only the sounds a project already has. It carries one clip per `BoardSoundType` (`GameStart`, `GameEnd`, `ItemDrop`), a BGM playlist with shuffle, inter-track interval, and cycle count, and the merge clip list. The playlist starts on `GameStart()` and stops on `GameEnd()`.

Merge sounds follow the combo: the first merge plays the first clip of `ItemMergeClips`, and every merge that lands within `MergeComboInterval` (5 seconds by default) of the one before it steps one clip further, stopping at the last one. A merge that comes later starts the combo over. `BoardController.MergeComboIndex` exposes the current step, and `PlayBoardSound(BoardSoundType)` / `PlayItemMergeSound()` are `protected virtual`.

Board sounds are played through `Mu3Library.Audio.AudioManager`. `BoardController.AudioManager` accepts the `IAudioManager` a project already runs, which the board then shares without touching its lifetime or its volumes. While none is assigned the board creates one of its own with the first sound it plays, drives it from `Update`, applies `BoardController.BgmVolume` to it, and disposes it with the board.

The volumes are not part of the configuration; the project sets them on the board. `BoardController.SfxVolume` (1 by default) is passed to the audio manager per sound, which scales it with its own SFX and master volume, so it works on a shared manager as well. `BoardController.BgmVolume` (0.8 by default) is only applied to the audio manager the board created for itself, and takes effect right away while that one is playing; an assigned manager keeps the BGM volume the project set on it. Both clamp to 0 to 1.

`BoardItemScoreRule.GetScore(int)` is virtual and defaults to the triangular Watermelon Game score progression, allowing external projects to override scoring.

## Events

`BoardController` reports what happens on it so a project can drive its UI and its effects without polling the board:

| Event | Raised with |
| --- | --- |
| `OnBoardPrepared` / `OnGameStarted` / `OnGameEnded` | Nothing; the session moved on. |
| `OnScoreChanged` | The new score. |
| `OnScoreAdded` | The points a single change paid out, and the score they were added to. A change that moves nothing, because the score stops at zero, is not reported. |
| `OnNextItemChanged` | `NextItemIndex`, the preview one item ahead. |
| `OnBoardConfigChanged` | The configuration that was applied to the board and to every item on it. |
| `OnHoldingItemChanged` | The item now in the player's hand, null when the hand was emptied. |
| `OnHoldingItemMoved` | Where the held item was moved to, as a fraction of the board area width; `HoldingNormalizedX` reads the same value. |
| `OnItemDropped` | The item the player released, already falling and counted. |
| `OnItemAdded` / `OnItemRemoved` | An item that joined or left the board, whoever put it there. A removed item still carries its catalog entry and its place while the event runs. |
| `OnItemMerged` | A `BoardMergeInfo`: the entry that merged, the entry it became, the item it became, where it happened, and the points it paid. |
| `OnMergeComboChanged` | The merge combo step, -1 when the combo is dropped. |
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

## Import

Add this package to a project that already references `Mu3Library_Base` and `Mu3Library_URP`:

```json
"com.github.doqltl179.mu3library.game.watermelon": "file:../../Mu3Library_Game_WatermelonGame"
```

Import the `Watermelon Game` package sample to receive the `BoardConfig` asset, fruit/background and board-guide images, sample manager/core scripts, and playable `Demo` scene.
