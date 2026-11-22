# Mu3 Library Steam

Steam package of **Mu3Library For Unity**: `SteamManager`, which starts Steam, runs Steam's callbacks, and shuts Steam down for a game that uses Steam without multiplayer; `SteamBackend`, a backend that runs the player-hosted multiplayer sessions of Base's `MultiplayerManager` on Steam lobbies and Steam networking sockets, with invites through the Steam overlay, players' Steam avatars, Steam rich presence, Steam voice, Steam's measure of each connection, and, in the editor, a fallback to Base's local backend when Steam cannot start; Steam achievements and stats, with optional debug console commands for achievements; and a storage of Base's save system on Steam Cloud.

- Package name: `com.github.doqltl179.mu3library.steam`
- Assemblies, each with the namespace of its name, for the editor and 64-bit standalone players only (Windows x64, macOS, Linux x64): `Mu3Library.Steam`, the Steam manager, which the other three reference; `Mu3Library.Steam.Multiplayer`, the backend; `Mu3Library.Steam.Stats`, achievements, stats, and their console commands; and `Mu3Library.Steam.Cloud`, the Steam Cloud save storage.
- Depends on Mu3 Library (Base), whose `Mu3Library.Foundation` assembly holds the `IUpdatable` contract the Steam manager implements, whose `Mu3Library.Multiplayer` assembly holds the manager, the backend interface this backend implements, and the local backend, whose `Mu3Library.Save` assembly holds the save system the Steam Cloud storage serves, and whose `Mu3Library.DebugConsole` assembly holds the console the achievement commands join.
- Needs [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) 2025.164.1 or later. Steamworks.NET is distributed through Git, so this package cannot declare it as a dependency: install it yourself first.

## Installation

Installation in the [repository README](https://github.com/doqltl179/Mu3Library_ForUnity#-installation) gives the package's Git URL with its latest release tag. Install in this order, since a Git URL install resolves neither dependency by itself:

1. Steamworks.NET through the Package Manager (**Add package from git URL...**), at 2025.164.1 or a later tag:
   ```
   https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1
   ```
2. Mu3 Library (Base).
3. This package.

Without Steamworks.NET, or on a build target other than a standalone platform, Unity skips the package's assemblies: their `defineConstraints` list `MU3LIBRARY_STEAMWORKS_SUPPORT`, which their `versionDefines` sets for Steamworks.NET 2025.164.1 or later, and `UNITY_STANDALONE`. During development, a `steam_appid.txt` with the app ID in the project folder, or the app ID given to `SteamManager.TryStart` or `SteamBackendOptions.AppId`, tells Steam which app the editor runs as; Steam's test app 480 works.

Up to Base 0.28.0, the backend came with Base in the `Mu3Library.Multiplayer.Steam` assembly and namespace. A project that uses it installs this package when it updates Base past 0.28.0 and changes:

- `using Mu3Library.Multiplayer.Steam;` to `using Mu3Library.Steam.Multiplayer;`;
- an assembly definition that references `Mu3Library.Multiplayer.Steam` by name to reference `Mu3Library.Steam.Multiplayer`. A reference by GUID keeps working, since the GUID in the `.meta` file of the assembly definition is unchanged, and so are the type names.

## Starting Steam

`SteamManager` (`Mu3Library.Steam`) starts Steam for a game that uses achievements, stats, or Steam Cloud without a multiplayer session, runs Steam's callbacks, and shuts Steam down when the game is done with it.

```csharp
using Mu3Library.Steam;
using UnityEngine;

public class SteamRoot : MonoBehaviour
{
    private SteamManager _steam;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);                  // one Steam for the whole game, across scenes
        SteamStartResult started = SteamManager.TryStart(480, out _steam);
        if (!started.IsSuccess)
        {
            Debug.LogWarning($"Steam did not start: {started}");
        }
    }

    private void Update() => _steam?.Update();        // runs Steam's callbacks
    private void OnDestroy() => _steam?.Dispose();    // shuts down the Steam it started
}
```

- `TryStart` starts Steam with the app ID unless Steam runs already; 0 leaves the app ID to `steam_appid.txt` or to the launch through Steam. The manager owns a Steam it started (`OwnsSteam`) and shuts it down when disposed. A Steam that runs already, started by the game or by `SteamBackend.TryCreate`, is left running.
- A start that fails logs a warning, hands back no manager, and answers `SteamNotRunning` when the Steam client is not running, Steam finds no app ID, or Steam's native library is missing or lacks a function, or `VersionMismatch` when the Steam client is older than the native library; `Message` holds what Steam answered.
- With the DI package, a core that registers the manager with `RegisterInstance` calls its `Update` every frame and disposes it with the core.
- Once a Steam call fails in Steam's native library, as an outdated `steam_api64` that still starts Steam can make it, the manager logs a warning, `IsAvailable` turns false, and no exception reaches the game.
- A game that also runs a multiplayer session starts Steam with the manager first; `SteamBackend.TryCreate` then uses that Steam. Dispose the multiplayer manager, then the backend, then `SteamManager`, so the other players learn the reason the session ended before Steam shuts down.
- Steamworks.NET's example `SteamManager` script declares a type of the same name in the global namespace. Where both are in one assembly, a plain `SteamManager` names the example; remove the example, which this manager replaces, or write `Mu3Library.Steam.SteamManager`.

## Usage

`SteamBackend.TryCreate` starts Steam, unless the game started it already, and answers why it could not; the manager then runs its session on the backend.

```csharp
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Steam.Multiplayer;

MultiplayerResult created = SteamBackend.TryCreate(new SteamBackendOptions { AppId = 480 }, out SteamBackend backend);
if (created.IsSuccess)
{
    MultiplayerManager manager = new MultiplayerManager();
    manager.Initialize(backend, new MultiplayerConfig("my.game", 1));
}
```

- [Multiplayer](https://github.com/doqltl179/Mu3Library_ForUnity#multiplayer) in the repository README goes on from there: lobbies, the ready check, the start of the game, loading, and messages, which run the same on every backend.
- Dispose the manager before the backend, so the other players learn the reason the session ended before Steam shuts down.
- `SteamBackend.CreateOrFallback` hands back, in the editor only, a `LocalBackend` on `SteamBackendOptions.EditorFallbackNetwork` with a warning when Steam cannot start. A player build never falls back by itself.
- Connections go only through Valve's relays by default, so no player learns another player's IP address; `SteamBackendOptions.AllowDirectConnections` also allows direct connections.
- `OpenInviteDialog` opens the Steam overlay's invite dialog for a lobby, and `InviteFriend` invites one friend. An accepted invite or a join from the friends list reaches the manager as a join request, and `SteamLaunchArguments.TryGetConnectLobby` reads the lobby from the `+connect_lobby` launch argument of a game that an invite started.
- The manager's avatars load the players' Steam profile pictures as textures and follow a player's new picture.
- The manager's presence status sets Steam rich presence: the friends list shows the status through a `steam_display` token that the game defines for its app ID on the Steamworks partner site and names in `SteamBackendOptions.PresenceDisplayToken`, and the players of one session are grouped together through `steam_player_group`.
- `TryGetConnectionQuality` answers Steam's measure of a connection's ping, packet loss, and data rates.
- Voice (`IVoiceBackend`) records with Steam voice, which uses the microphone and settings the player chose in Steam, and decodes any player's Steam voice at Steam's optimal sample rate (`VoiceSampleRate`). [Multiplayer](https://github.com/doqltl179/Mu3Library_ForUnity#multiplayer) in the repository README shows how a game sends and plays it; in the editor's fallback, `LocalBackend` records the microphone instead.
- `IUnavailableReasonBackend` names why the backend is not available, such as `SteamNotRunning`.

Steam publishes lobby data a moment after it is set, so a new lobby can take a few seconds to show up in a search.

## Achievements, Stats, And Steam Cloud

Two more assemblies of this package, under the same gate as `Mu3Library.Steam.Multiplayer`, use the Steam the game started, through `SteamManager.TryStart` (Starting Steam above), `SteamBackend.TryCreate`, or its own `SteamAPI.Init`, and start none themselves:

- `Mu3Library.Steam.Stats` (namespace `Mu3Library.Steam.Stats`): `SteamStatsManager`, through `ISteamStatsManager`, unlocks an achievement and stores it at once so Steam shows its notification, reads an achievement's state, unlock time, and names, lists every achievement, shows an achievement's progress (`IndicateAchievementProgress`), and locks one again for testing (`ClearAchievement`); it reads and sets integer and float stats and sends them with `StoreStats`. Every call answers a `SteamStatsResult`: `SteamNotRunning` while Steam does not run in the process, `Refused` when Steam answers no, such as for a name the app does not define or before Steam handed this player's stats, which it does a moment after it starts, and `InvalidArgument` for an empty name or a progress at or past its maximum.
- `Mu3Library.Steam.Cloud` (namespace `Mu3Library.Steam.Cloud`): `SteamCloudSaveStorage` is an `ISaveStorage` of Base's save system on Steam Remote Storage, so a `SaveManager` keeps its slots in Steam Cloud: each slot is the file `<folder>/<slot ID>.sav` of the app's Steam Cloud files, `Saves` by default. Steam keeps the files on the computer and sends them to the cloud while Steam Cloud is on for the account and the app (`IsCloudEnabled`); the app's Steam Cloud settings on the partner site need a quota for them. A write Steam refuses, such as one past the quota, answers `StorageFailed`, and so does a read, write, or delete while Steam does not run, when `Exists` answers false and `GetSlotIds` lists nothing. Steam's file names ignore case and Steam lists them in lower case, so `GetSlotIds`, and with it `SaveManager.GetSlots`, answers lower-case slot IDs: give slots lower-case IDs, or compare them ignoring case. Whether a write that a crash cuts short leaves the previous file is up to the Steam client.

```csharp
using Mu3Library.DebugConsole;
using Mu3Library.Save;
using Mu3Library.Steam.Cloud;
using Mu3Library.Steam.Stats;
using UnityEngine;

var stats = new SteamStatsManager();
SteamStatsResult unlocked = stats.UnlockAchievement("ACH_WIN_ONE_GAME");
if (!unlocked.IsSuccess)
{
    Debug.LogWarning($"Achievement not unlocked: {unlocked}");
}

stats.SetStat("NumGames", games);
stats.StoreStats();                                  // at the end of a match, not every frame

var saves = new SaveManager(new SteamCloudSaveStorage());   // slots in Steam Cloud
saves.Save("slot1");

// Only when the game wants it: console commands for achievements.
SteamStatsCommands.Register(console, stats);                          // developer commands
// SteamStatsCommands.Register(console, stats, ConsoleAccess.Player);  // or: listing and unlocking for players too
```

- **Console commands**: `SteamStatsCommands.Register` adds `steam.achievements`, which lists every achievement and whether it is unlocked, `steam.unlock <id>`, and `steam.clear <id>` to a `DebugConsoleManager` of Base's debug console; a game that does not call it has none of them. The game chooses who may list and unlock: developers only, the default, or players too with `ConsoleAccess.Player`, for a game that lets its players take achievements on purpose. `steam.clear` is always a developer command. `steam.unlock` and `steam.clear` are marked as changing progress, so the console's `HasModifiedProgress` tells the game a player used them. `SteamStatsCommands.Unregister` removes them.
- Put the code that uses these assemblies behind the same gate as the multiplayer code (below).

## Platforms And The Gate

Steam support covers the editor and 64-bit standalone players only: the `includePlatforms` of the package's assemblies leave out 32-bit Windows, so a 32-bit Windows player builds without them. Put game code that uses their types in an assembly definition of your own that references the assemblies it uses, such as `Mu3Library.Steam.Multiplayer`, and copies the `includePlatforms`, `defineConstraints`, and `versionDefines` entry of `Mu3Library.Steam.Multiplayer.asmdef`, which the package's other assemblies share; the entry is needed too, since a `versionDefines` symbol is defined only in the assembly that declares it. That code then compiles exactly where the Steam assembly does. `#if` on a `versionDefines` symbol and `UNITY_STANDALONE` cannot leave out a 32-bit Windows player, where both are defined, and adding `UNITY_64` is no way out either, since Unity 6.6 deprecates it, so that gate suits only a game that builds no 32-bit Windows player. Code without an assembly definition of its own (`Assembly-CSharp`) cannot read `versionDefines`, and `versionDefines` sees only a package that the Package Manager installed, not a `.unitypackage` import. Do not gate on `STEAMWORKS_NET`: the Steamworks.NET installer writes it into the project's scripting defines, where it stays after the package is removed.
