# Multiplayer Sample

One scene, `Scenes/Sample_Multiplayer.unity`, that walks through a player-hosted session of `Mu3Library.Multiplayer` with buttons: create a lobby, search for lobbies, join one by its lobby ID, invite friends (Steam only), get ready, start the game, exchange a message, and kick a player. The on-screen log and the Unity log, with the prefix `[MultiplayerSample]`, show what the sample takes part in: state changes with their reasons, players joining, leaving, dropping during a game and coming back, ready changes, the start and end of a game, join requests from Steam, and the messages. The manager's other events, such as lobby data, chat, and load reports, are left out.

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
4. In the other, press **Search lobbies** and then the lobby's **Join** button, or type the lobby ID into **Lobby ID** and press **Join by ID**. The local backend has no invites: **Copy the lobby ID** in the lobby puts the ID on the clipboard to paste into the other instance.
5. Press **Ready** in both. The host's **Start the game** needs two players at least, every one of them ready; pressed earlier, it logs who is not ready yet. The start sends a random seed as start data, which every player logs.
6. **Send a message to the others** sends a `SampleGreeting`, a text and a number written with `MessageWriter` and read with `MessageReader`, through the host to every other player.
7. The host's **Kick** next to a player removes them; the kicked player's log shows the session end with `Kicked`, and that player cannot rejoin the session.

A player of the sample sets `Application.runInBackground`, so its window keeps the session going in the background. In the editor, the project's **Run In Background** player setting decides, and the sample leaves it alone.

## On Steam

With Steamworks.NET installed and Steam running, the same buttons play on Steam lobbies and connections, and the lobby shows **Invite friends (Steam overlay)**, which opens the overlay's invite dialog. An accepted invite, a join from the friends list, or a launch from an invite joins by itself; the log shows the join request. While the Steam overlay is off for the game, the button logs `OverlayUnavailable` instead. One Steam account takes one slot in a session, so two players need two accounts, normally on two computers.

## Unattended Run

The command line can run the whole flow without anyone pressing a button, for example to check an editor and a build, or two builds, against each other:

| Option | Effect |
|---|---|
| `-mu3mp-host <lobby name>` | Creates a public lobby of that name, waits for a guest to join and get ready, gets ready, starts the game, sends the guest a message, and kicks the guest once it answers |
| `-mu3mp-join <lobby name>` | Searches every second for a lobby of that name, joins it by its lobby ID, gets ready, answers the host's message, and waits to be kicked |
| `-mu3mp-local` | Uses the local backend even where Steam would start |
| `-mu3mp-name <name>` | The player's name on the local backend |
| `-mu3mp-timeout <seconds>` | Fails the run after that long; 120 by default |

Each step logs a line that starts with `[MultiplayerSample] AUTO`, and the run ends with `AUTO PASS` or `AUTO FAIL`. A player then quits with exit code 0 or 1, and the editor leaves play mode. The run fails at once, before any backend starts, when an option lacks its value: a value never starts with `-`, so `-mu3mp-host -mu3mp-local` names no lobby instead of a lobby called `-mu3mp-local`, and `-mu3mp-join -logFile guest.log` does not search for a lobby called `-logFile`. It also fails when the backend cannot start, and when it reaches `-mu3mp-timeout`.

On Steam's test app 480, every copy of this sample shares the game ID, so a guest finds anyone's lobby of the same name. Give each run a lobby name of its own, such as one with the date and a random number, or pass `-mu3mp-local` to both sides. For example, two Windows players on one computer:

```
Sample.exe -mu3mp-local -mu3mp-host check-1 -mu3mp-name Host -logFile host.log
Sample.exe -mu3mp-local -mu3mp-join check-1 -mu3mp-name Guest -logFile guest.log
```

The editor reads the same options from its own command line, so an editor started with `-mu3mp-join check-1` runs the guest's part when it enters play mode in this scene.

## Files

| File | Holds |
|---|---|
| `Scripts/SampleMultiplayerCore.cs` | The core: registers `MultiplayerManager` in its container, which updates and disposes it, attaches the backend, disposes the backend after the manager, and holds the flows and the event handlers |
| `Scripts/SampleMultiplayerCore.Gui.cs` | The IMGUI buttons |
| `Scripts/SampleMultiplayerCore.AutoRun.cs` | The unattended run |
| `Scripts/SampleBackend.cs` | The backend choice; its Steam code compiles only with Steamworks.NET and a standalone build target (`MULTIPLAYER_STEAMWORKS_SUPPORT`) |
| `Scripts/SampleGreeting.cs` | The game message and its payload |
| `Scripts/SampleCommandLine.cs` | The command-line options |
| `Scripts/SampleLog.cs` | The on-screen log |

In play mode, **Mu3Library > Windows > Multiplayer Debugger** shows the sample's session, its players, their connection quality, and the message counts.
