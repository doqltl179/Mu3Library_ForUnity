# Mu3 Library Steam

Steam package of **Mu3Library For Unity**: `SteamBackend`, a backend that runs the player-hosted multiplayer sessions of Base's `MultiplayerManager` on Steam lobbies and Steam networking sockets, with invites through the Steam overlay, players' Steam avatars, Steam rich presence, Steam voice, Steam's measure of each connection, and, in the editor, a fallback to Base's local backend when Steam cannot start.

- Package name: `com.github.doqltl179.mu3library.steam`
- Assembly: `Mu3Library.Steam.Multiplayer`, with the namespace `Mu3Library.Steam.Multiplayer`, for the editor and 64-bit standalone players only (Windows x64, macOS, Linux x64).
- Depends on Mu3 Library (Base), whose `Mu3Library.Multiplayer` assembly holds the manager, the backend interface this backend implements, and the local backend.
- Needs [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) 2025.164.1 or later. Steamworks.NET is distributed through Git, so this package cannot declare it as a dependency: install it yourself first.

## Installation

Installation in the [repository README](https://github.com/doqltl179/Mu3Library_ForUnity#-installation) gives the package's Git URL with its latest release tag. Install in this order, since a Git URL install resolves neither dependency by itself:

1. Steamworks.NET through the Package Manager (**Add package from git URL...**), at 2025.164.1 or a later tag:
   ```
   https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1
   ```
2. Mu3 Library (Base).
3. This package.

Without Steamworks.NET, or on a build target other than a standalone platform, Unity skips the `Mu3Library.Steam.Multiplayer` assembly: its `defineConstraints` list `MU3LIBRARY_STEAMWORKS_SUPPORT`, which its `versionDefines` sets for Steamworks.NET 2025.164.1 or later, and `UNITY_STANDALONE`. During development, a `steam_appid.txt` with the app ID in the project folder, or `SteamBackendOptions.AppId`, tells Steam which app the editor runs as; Steam's test app 480 works.

Up to Base 0.28.0, the backend came with Base in the `Mu3Library.Multiplayer.Steam` assembly and namespace. A project that uses it installs this package when it updates Base past 0.28.0 and changes:

- `using Mu3Library.Multiplayer.Steam;` to `using Mu3Library.Steam.Multiplayer;`;
- an assembly definition that references `Mu3Library.Multiplayer.Steam` by name to reference `Mu3Library.Steam.Multiplayer`. A reference by GUID keeps working, since the GUID in the `.meta` file of the assembly definition is unchanged, and so are the type names.

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

## Platforms And The Gate

Steam support covers the editor and 64-bit standalone players only: the `includePlatforms` of `Mu3Library.Steam.Multiplayer` leave out 32-bit Windows, so a 32-bit Windows player builds without that assembly. Put game code that uses its types in an assembly definition of your own that references `Mu3Library.Steam.Multiplayer` and copies the `includePlatforms`, `defineConstraints`, and `versionDefines` entry of `Mu3Library.Steam.Multiplayer.asmdef`; the entry is needed too, since a `versionDefines` symbol is defined only in the assembly that declares it. That code then compiles exactly where the Steam assembly does. `#if` on a `versionDefines` symbol and `UNITY_STANDALONE` cannot leave out a 32-bit Windows player, where both are defined, and adding `UNITY_64` is no way out either, since Unity 6.6 deprecates it, so that gate suits only a game that builds no 32-bit Windows player. Code without an assembly definition of its own (`Assembly-CSharp`) cannot read `versionDefines`, and `versionDefines` sees only a package that the Package Manager installed, not a `.unitypackage` import. Do not gate on `STEAMWORKS_NET`: the Steamworks.NET installer writes it into the project's scripting defines, where it stays after the package is removed.
