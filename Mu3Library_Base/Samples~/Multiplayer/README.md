# Multiplayer Sample

One scene, `Scenes/Sample_Multiplayer.unity`, that walks through a player-hosted session of `Mu3Library.Multiplayer` with buttons: create a lobby, search for lobbies, join one by its lobby ID, invite friends (Steam only), get ready, start the game, exchange a message, set and delete lobby data as the host and your own participant data as any player, and kick a player. The player list shows each player's ping as the host measures it, and the lists show the players' avatars where the backend has them. The on-screen log and the Unity log, with the prefix `[MultiplayerSample]`, show what the sample takes part in: state changes with their reasons, players joining, leaving, dropping during a game and coming back, ready changes, the start and end of a game, join requests from Steam, the messages, changes of the lobby data and of the players' participant data, deletions included, and avatar changes. The manager's other events, such as chat and load reports, are left out. Every line starts with the time it was logged at ([The Log](#the-log)).

## What It Needs

- Base alone. Steamworks.NET is optional: without it the sample compiles without the Steam code and plays on the local backend on every platform.
- With Steamworks.NET, installed through the Package Manager, the sample builds for the editor and 64-bit standalone players only. `Mu3Library.Multiplayer.Steam` leaves out 32-bit Windows players, and the sample's Steam code, under `MULTIPLAYER_STEAMWORKS_SUPPORT` and `UNITY_STANDALONE`, cannot tell them apart, so a 32-bit Windows player does not compile then.
- Nothing else: the buttons are drawn with IMGUI, which needs no `EventSystem`, no input module, and no TextMesh Pro resources, so the scene responds to input with or without the Input System package.

## How It Picks The Backend

`SampleBackend` decides when the scene starts.

| Project | Backend |
|---|---|
| Steamworks.NET 2025.164.1 or later installed, a standalone build target, and the Steam client running | Steam, on the app ID in the core's **Steam App Id** field (Steam's test app 480 by default) |
| The same, but Steam cannot start | In the editor, `SteamBackend.CreateOrFallback` hands back the local backend through `SteamBackendOptions.EditorFallbackNetwork` with a warning; in a player, the sample falls back to the local backend itself, since a player build never falls back on its own |
| No Steamworks.NET, another build target, the core's **Use Local Backend** field, or the `-mu3mp-local` option | The local backend |

The local backend runs on `LocalNetwork.CreateMachineWide("mu3library-multiplayer-sample")`, so every editor and player build on this computer that runs the sample meets the others. Nothing outside this computer can see its lobbies or connect to it.

## Trying It On One Computer

With Steamworks.NET installed and the Steam client running, an editor and a player on one computer both play on Steam with the same account, and the host refuses the join with `DuplicateAccount`, since one account takes one slot. Turn on **Use Local Backend** on the `SampleMultiplayerCore` object before building, or pass `-mu3mp-local` to both: to the player on its command line, and to the editor in the Unity Hub's command-line arguments for the project.

1. Add `Scenes/Sample_Multiplayer.unity` to the build profile's scene list and build a Windows, macOS, or Linux player.
2. Start the player, and enter play mode in the editor with the same scene open.
3. In one of them, press **Create a public lobby**. The lobby ID shows above the player list.
4. In the other, press **Search lobbies** and then the lobby's **Join** button, or type the lobby ID into **Lobby ID** and press **Join by ID**. Under each lobby's button, the search list shows the game-defined lobby data that search returned with the lobby (`LobbyInfo.Data`), such as `Lobby data: "map" "forest"`, or `Lobby data: (none)`; the keys come in the case the backend hands them back in, which on Steam can differ from the case the host wrote them in. The local backend has no invites: **Copy the lobby ID** in the lobby puts the ID on the clipboard to paste into the other instance.
5. Press **Ready** in both. The host's **Start the game** needs two players at least, every one of them ready; pressed earlier, it logs who is not ready yet. The start sends a random seed as start data, which every player logs.
6. **Send a message to the others** sends a `SampleGreeting`, a text and a number written with `MessageWriter` and read with `MessageReader`, through the host to every other player.
7. The player list shows each player's ping as the host measures it (`TryGetParticipantPing`), which every player reads for every player of the session: the host's own is 0 ms, and a client shows `-` for a player until the host's report names them, which the host sends every second. The lists show the avatars that `LoadAvatar` loads, each player's in the player list and each host's in the search list. A cell whose avatar could not be loaded, such as one Steam did not download in time, stays empty, and while the lists show that player, in this session or a later one, they ask for the avatar again 10 seconds after each failure. The local backend has no avatars and answers `NotSupported`, so there the sample logs that once and the lists show none.
8. **Lobby data "map"** holds a value the host's lobby starts with. Only the host has its **Set**, which picks another value than the key holds, and **Delete**, which deletes the key; every player logs the change, and a deletion as `The lobby data "map" was deleted.`, from the null value of `OnLobbyDataChanged`. Lobby search follows the lobby data, so a search after a deletion lists the lobby without the key; Steam's search takes a moment to follow, which the documentation of `DeleteLobbyData` measures. **My data "color"** is this player's own participant data, which the player list shows beside each player: **Set**, which also picks another value, and **Delete** change it, and every player logs a deletion as the player whose data no longer holds the key (`OnParticipantDataChanged`).
9. The host's **Kick** next to a player removes them; the kicked player's log shows the session end with `Kicked`, and that player cannot rejoin the session.

The search list lets go of the avatar of a host whose lobby drops out of the next search (`ReleaseAvatar`), and of the remaining hosts' avatars once this player is in a session (`ReleaseAvatars`), which keeps this player's own and those of the session's players. When a player's avatar changes, the manager draws the new picture into the texture the list shows already, so the sample only logs `OnAvatarChanged`.

A player of the sample sets `Application.runInBackground`, so its window keeps the session going in the background. In the editor, the project's **Run In Background** player setting decides, and the sample leaves it alone.

### With Multiplayer Play Mode

Multiplayer Play Mode tries the sample in the editor alone, without a player build. This was checked on Unity 6000.6.7f1 with Multiplayer Play Mode 3.0.0, the version that editor bundles, on the local backend: a virtual player inherits the main editor's environment, so it finds the main editor's lobbies in the same temporary folder.

1. Add `com.unity.multiplayer.playmode` to the project through the Package Manager, at the version your editor bundles. Base and the sample neither need it nor install it, so it can go in only while you try this and come out again afterwards.
2. Open `Scenes/Sample_Multiplayer.unity`, turn on **Use Local Backend** on the `SampleMultiplayerCore` object, and save the scene. The main editor and a virtual player are one Steam account, so on Steam the host refuses the virtual player with `DuplicateAccount`. Without Steamworks.NET the sample is on the local backend already.
3. In **Window > Multiplayer > Multiplayer Play Mode**, turn on Player 2, then enter play mode in the main editor. Player 2 enters play mode with it.
4. Go through steps 3 to 9 above with the main editor and Player 2 as the two instances.

A kicked player cannot come back into the same game: the host refuses them with `Banned` for the rest of the session, and a lobby in a game drops out of the search and refuses joins with `GameInProgress`. To try something else after a kick, such as the host's **Leave**, which ends the other player's session with `HostLeft`, exit play mode and enter it again for a new session.

## On Steam

With Steamworks.NET installed and Steam running, the same buttons play on Steam lobbies and connections, and the lobby shows **Invite friends (Steam overlay)**, which opens the overlay's invite dialog. An accepted invite, a join from the friends list, or a launch from an invite joins by itself; the log shows the join request. While the Steam overlay is off for the game, the button logs `OverlayUnavailable` instead. One Steam account takes one slot in a session, so two players need two accounts, normally on two computers.

## The Log

The on-screen log shows the latest 16 lines. Each of them, and each line with the prefix `[MultiplayerSample]` in the Unity log, starts with the time it was logged at: this computer's local time of day to the millisecond, `HH:mm:ss.fff`. In the Unity log the prefix follows the time:

```
14:05:09.123 [MultiplayerSample] State: InGame -> Offline (HostLeft)
```

The time comes from the computer's clock rather than from the start of the process, so the lines of two computers compare when their clocks are synchronized and set to the same time zone: which came first, and how long after one event another came, such as how long after the host's process ended a player's session ended with `HostLeft`. Two computers' lines are only as close as their clocks, so synchronize both before such a check. The time carries no date. The library's own lines, which start with `[Multiplayer]`, carry no time, and neither does the exception the sample logs as it is when its backend cannot start, which a timed line about it follows.

## Unattended Run

The command line can run the whole flow without anyone pressing a button, for example to check an editor and a build, or two builds, against each other:

| Option | Effect |
|---|---|
| `-mu3mp-host <lobby name>` | Creates a public lobby of that name with the lobby data `map`, waits for a guest to join and get ready, gets ready, starts the game, sends the guest a message, deletes `map` once the guest answers, and kicks the guest once it deleted its participant data `color` |
| `-mu3mp-join <lobby name>` | Searches every second for a lobby of that name, joins it by its lobby ID, sets its participant data `color`, gets ready, answers the host's message, deletes `color` once it heard `map` deleted, and waits to be kicked |
| `-mu3mp-local` | Uses the local backend even where Steam would start |
| `-mu3mp-name <name>` | The player's name on the local backend |
| `-mu3mp-timeout <seconds>` | Fails the run after that long; 120 by default |

Each step logs a line whose text after the time and the prefix starts with `AUTO`, such as `14:05:09.123 [MultiplayerSample] AUTO Guest: the host started the game`, and the run ends with `AUTO PASS` or `AUTO FAIL`. The guest's line for the lobby it found names the lobby data that search returned, and its line after the join the lobby data the host sent, so the log shows both. A player then quits with exit code 0 or 1, and the editor leaves play mode. The run fails at once, before any backend starts, when an option lacks its value: a value never starts with `-`, so `-mu3mp-host -mu3mp-local` names no lobby instead of a lobby called `-mu3mp-local`, and `-mu3mp-join -logFile guest.log` does not search for a lobby called `-logFile`. It also fails when the backend cannot start, and when it reaches `-mu3mp-timeout`.

On Steam's test app 480, every copy of this sample shares the game ID, so a guest finds anyone's lobby of the same name. Give each run a lobby name of its own, such as one with the date and a random number, or pass `-mu3mp-local` to both sides. For example, two Windows players on one computer:

```
Sample.exe -mu3mp-local -mu3mp-host check-1 -mu3mp-name Host -logFile host.log
Sample.exe -mu3mp-local -mu3mp-join check-1 -mu3mp-name Guest -logFile guest.log
```

The editor reads the same options from its own command line, so an editor started with `-mu3mp-join check-1` runs the guest's part when it enters play mode in this scene.

## Files

| File | Holds |
|---|---|
| `Scripts/SampleMultiplayerCore.cs` | The core: registers `MultiplayerManager` in its container, which updates and disposes it, attaches the backend, disposes the backend after the manager, and holds the flows, the event handlers, and the avatars the lists show |
| `Scripts/SampleMultiplayerCore.Gui.cs` | The IMGUI buttons |
| `Scripts/SampleMultiplayerCore.AutoRun.cs` | The unattended run |
| `Scripts/SampleBackend.cs` | The backend choice; its Steam code compiles only with Steamworks.NET and a standalone build target (`MULTIPLAYER_STEAMWORKS_SUPPORT`) |
| `Scripts/SampleGreeting.cs` | The game message and its payload |
| `Scripts/SampleCommandLine.cs` | The command-line options |
| `Scripts/SampleLog.cs` | The on-screen log and the time each line starts with |

In play mode, **Mu3Library > Windows > Multiplayer Debugger** shows the sample's session, its players with each one's ping as the host measures it and this player's own connection quality to each, and the message counts.
