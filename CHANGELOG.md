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
- Base: `Mu3Library.Multiplayer`, a new assembly for player-hosted multiplayer sessions, where one player's machine hosts and the others find it through a lobby, without a server of your own. `MultiplayerManager` (`IMultiplayerManager`, `IMultiplayerManagerEventBus`) creates, searches, joins, and leaves lobbies, keeps the roster and ready flags, enforces the session rules (`SessionRules`: a capacity of 1 to 16 players including the host, the minimum player count, the ready check, and whether players may join a running game), starts and ends games with start data, kicks players for the rest of the session, and ends the session for everyone when the host leaves. During a session, in the lobby and in a game alike, the host changes the lobby data that lobby search returns and filters on (`SetLobbyData`; a client's attempt answers `MultiplayerReason.NotHost`), which every player reads from `IMultiplayerManager.LobbyData`, and each player sets their own participant data, such as the character they chose, which every player reads from `Participant.Data` (`SetParticipantData`; no player can set another's). For a client, success means the request went to the host, and its own value changes once the host reports it back; the client checks each request against its own earlier ones as the host will have applied them, so the host takes every request that succeeded. The host carries both kinds of data over its connections, so every player hears each change (`OnLobbyDataChanged`, `OnParticipantDataChanged`) on either backend, and a player who joins later receives the data already set. Keys that start with `mu3.` stay the library's in any letter case, since Steam does not tell lobby data keys apart by case; a key holds at most `MultiplayerLimits.MaxDataKeyLength` (64) characters; a key or value may not hold a NUL character, where Steam ends a string, or an unpaired UTF-16 surrogate, which UTF-8 cannot carry; and the lobby data and each player's data hold at most `MultiplayerLimits.MaxLobbyDataSize` (4 KiB) and `MultiplayerLimits.MaxParticipantDataSize` (2 KiB) of UTF-8 keys and values. These rules cover the data a lobby is created with too; a broken rule answers `MultiplayerReason.InvalidArgument`, and too much data `MultiplayerReason.MessageTooLarge`. In the lobby, `SendChat` sends a text message to every player of the session through the host, which names the sender from the connection the message arrived on; every player, the sender included, hears it through `OnChatReceived`, all in the order the host passed the messages on, and a message longer than Steam's 4 KB lobby chat limit in UTF-8 bytes (`MultiplayerLimits.MaxChatMessageSize`) answers `MultiplayerReason.MessageTooLarge` and reaches nobody, whatever `MultiplayerConfig.MaxMessageSize` is, since that caps game messages only. The host can limit what each client sends it: `SessionRules.MaxMessagesPerSecond` and `SessionRules.MaxBytesPerSecond` (0, the default, sets no limit) count every packet a client sends once it joined, game messages, chat messages, ready changes, load reports, participant data requests, and the hellos with which it connects or joins again alike, with their headers, in windows of one second that belong to the account for the whole session, so neither connecting again nor leaving and joining again starts a fresh window or a fresh count toward the kick. The host drops a game or chat message past a limit before it handles or passes it on, and raises `OnRateLimitExceeded` on the host at most once a second for each client. A ready change, a load report, or a participant data request past a limit counts and is reported too, but waits: the host applies such requests in the order they came, as later windows have room, since a client checks each data request against every one it sent before. While any of them waits, the host drops the client's game messages, reliable ones included, and its chat messages, even when the window has room, and counts them as past the limit, so that none overtakes a request sent before it; a hello past a limit waits for room as well. A client with more than `MultiplayerLimits.MaxDelayedRequests` (64) requests waiting is disconnected with `MultiplayerReason.RateLimited`, even without a kick time, but may join again; a waiting request holds no more than a data entry may, since the host refuses a larger key or value as it arrives, and requests that still wait when the connection drops are lost with it. With `SessionRules.RateLimitKickSeconds`, the host kicks a client that goes past a limit in every window for that long, with `MultiplayerReason.RateLimited`, and the account cannot rejoin the session. The host's own sends are never limited. During a game, a client whose connection drops keeps its slot for `SessionRules.ReconnectReservationSeconds` (60 by default; 0 frees it at once). Meanwhile the host and the other players see `OnParticipantDisconnected` and `Participant.IsConnected` false, the slot counts toward the capacity, for a capacity change too, and toward `SessionRules.MinPlayersToStart`, and is no free slot for another account, and a message for that player alone answers `MultiplayerReason.ConnectionLost` while one for several reaches the others only. New rules with a shorter reservation time cut the slots kept then to at most that time. The player's own state turns to the new `SessionState.Reconnecting`, in which the library connects to the host again once a second and `Send`, `SendChat`, `SetParticipantData`, and `ReportLoaded` answer `MultiplayerReason.ConnectionLost`. It ends with `MultiplayerReason.ConnectionLost` when the reservation time, counted from when the player noticed the drop, runs out or the host no longer keeps the slot, with the host's reason when the host refuses it after a kick, and with `MultiplayerReason.HostLeft` as soon as the lobby shows the host gone, because it passed to another member or no longer lists the host while it still lists this player. A client cannot tell a host whose own network or power went down from a drop of its own, so while the lobby shows nothing it keeps trying for the whole reservation time. Coming back in time restores the same slot with the player's participant data and its ready and late-join flags, and raises `OnParticipantReconnected` on every player, the returning one included. The returning player takes the roster, rules, lobby data, and start data the host holds then, without events for what changed while it was away, hears `OnReturnedToLobby` or `OnGameStarted` first when its game ended or another started meanwhile, since the slot outlasts the game, and never receives what was sent while it was away; its message counts go on. The same account joining again from another start of the game during the reservation also comes back to its slot instead of being refused as `DuplicateAccount`, when the lobby admits it, in the lobby phase or with `SessionRules.AllowLateJoin`; otherwise the lobby refuses it with `MultiplayerReason.GameInProgress`. A player who does not come back in time leaves with `MultiplayerReason.ConnectionLost`; a player who leaves or is kicked never keeps a slot, and a kick while the player is away bans the return. A player who leaves while reconnecting frees the slot at once only when one of its attempts already reached the host, which otherwise cannot know and frees it when the reservation runs out. On the local backend, whose simulated outage leaves the lobby alone, a client whose host crashes or leaves in a game, while the client is cut off or already reconnecting, ends with `HostLeft` through the lobby; that a Steam lobby tells it the same way is not verified, and an outage on Steam may drop the player's lobby membership, which leaves only the reservation time. After a game starts, each player reports with `ReportLoaded` that it is ready to play, for example once its scene loaded, which every player reads from `Participant.IsLoaded` and hears through `OnParticipantChanged`. The host waits for every player the game started with, those away while it keeps their slot included, and hears `OnAllPlayersLoaded` once all of them reported, or `OnLoadTimedOut` with the players who did not, in roster order, when `SessionRules.LoadTimeoutSeconds` (60 by default; 0 waits without a limit) passes first. A player who leaves, is kicked, or does not come back in time is no longer waited for, a player who joins the running game is not waited for but can report, and returning to the lobby ends the wait without either event and clears every flag. A player who reported before its connection dropped stays loaded when it comes back to the same game, and a report the drop lost goes again by itself; the same account taking its slot back from another start of the game loads again, so its flag clears, and the host waits for it again when the game started with it. Lobbies and connections come from an `IMultiplayerBackend`: `LocalBackend` on a `LocalNetwork` runs several players inside one process for tests, and `SteamBackend`, below, plays over Steam. Every failure and every ended session names a `MultiplayerReason`. `TryGetConnectionQuality(player, out ConnectionQuality)` tells how this player's connection to another player performs, as the backend measures it: the round-trip ping in milliseconds, the share of the packets on their way to this player that were lost, and the bytes sent and received per second. The host has a connection to each client, and a client has one to the host only, since everything it sends travels through the host, so a client asking about another client, a player asking about themselves, and a player outside the session answer false. In play mode, the editor window **Mu3Library > Windows > Multiplayer Debugger** shows every manager that a core under `CoreRoot` holds as a singleton, the way the Runtime Diagnostics drawer finds its services: its session state, `Reconnecting` included, lobby, and players with their host, ready, late-join, away, and loaded flags and the quality of this player's connection to each, and its message types with how many it sent and received and how many per second since the window last looked, read again every half second. The requirements are in `docs/specs/steam-player-hosted-multiplayer.md`.
- Base: `IMultiplayerMessenger`, which `MultiplayerManager` implements, sends game messages between the players of a session: to the host, one player, everyone, everyone but the sender, or everyone but one player, reliably or unreliably. Every message travels through the host, which names the sender from the connection the message arrived on and enforces each message type's `MessagePolicy` (`HostOnly`, `NoClientRelay`). A copy addressed to the sending player is handled before `Send` returns, and `MultiplayerConfig.MaxMessageSize` caps the payload (64 KiB by default; a larger message answers `MultiplayerReason.MessageTooLarge`); a client takes the host's limit when it is smaller. A player whom a reliable message cannot reach, for example because its send buffer is full, is dropped from the session with `MultiplayerReason.ConnectionLost` rather than silently missing the message. Players whose copies of the library exchange different packets refuse each other with `MultiplayerReason.VersionMismatch`, even when the game's protocol version matches. `MessageWriter` writes a payload's numbers (`byte` through `ulong`, `float`, `double`), `bool`, strings, byte blocks, `Vector2`, `Vector3`, `Quaternion`, and `Color` into a buffer it reuses, and `MessageReader` reads them back in the same order: numbers are little-endian, and a floating-point value travels as its bits, negative zero, NaN payloads, and subnormal values included, so every player reads exactly what was written. The one exception is a signaling `float` NaN: the Mono runtime, which the editor uses, may turn it quiet both before it is written and when `ReadFloat` reads it, so players on different runtimes can read different bits for it; write such bits with `WriteInt` or `WriteUInt` and read them with `ReadInt` or `ReadUInt` to keep them exact. Once a reused writer's buffer has grown, writing allocates nothing, and reading allocates only the strings and arrays it returns; a writer that would grow past the largest byte array the runtime creates throws `InvalidOperationException`. A payload that ends early or holds an invalid length throws `FormatException`, which the manager logs before it goes on with the next message. `GetMessageCounts` fills a list with one `MessageCounts` per registered message type: how many messages of the type this player sent, each successful `Send` counting once, and how many that other players addressed to it arrived, its own copies, the messages it dropped, and the ones the host only passed on between clients left out. The counts last from registering the type until unregistering it, and reading them into a list with room allocates nothing.
- Base: with [Steamworks.NET](https://github.com/rlabrecque/Steamworks.NET) installed (`MU3LIBRARY_STEAMWORKS_SUPPORT`), `SteamBackend` in the new `Mu3Library.Multiplayer.Steam` assembly runs sessions on Steam: Steam lobbies to create, search, and join them, and Steam networking sockets for the connections, which it sets up to go only through Valve's relays, keeping players' IP addresses from each other, unless `SteamBackendOptions.AllowDirectConnections` is set. It needs Steamworks.NET 2025.164.1 or later. `SteamBackend.TryCreate` starts Steam with `SteamBackendOptions.AppId` when the game has not started it already, and only then shuts it down when disposed; `SteamBackend.CreateOrFallback` returns a `LocalBackend` instead in the editor when Steam cannot start and `SteamBackendOptions.EditorFallbackNetwork` is set. Accepting an invite, joining from the friends list, and launching the game from an invite (`+connect_lobby`) all raise `IMultiplayerManagerEventBus.OnJoinRequested` and join as `JoinLobby` does, and `OpenInviteDialog` and `InviteFriend` send invites. A lobby can take a few seconds to show up in search after it is created, while Steam publishes its lobby data. The assembly compiles only while the build target is a standalone platform, in the editor too, so game code that uses it needs the same gate (`#if STEAMWORKS_NET && UNITY_STANDALONE`, or an assembly that references it). Dispose the manager before the backend, so the other players learn why the session ended before Steam shuts down. A host admits only players its lobby lists as members, so connecting to the host directly does not get around an invite-only or friends-only lobby, and a message sent while the other side's close is on its way leaves the reason of that close intact. On Steam, the connection quality is Steam's own measure of the connection: its ping, the share of packets from the other player that did not arrive in order, and its data rates, which Steam reports once it has measured a few seconds of traffic. The backend asks Steam at most every half second per connection and answers from that in between: each time it asks, Steamworks.NET copies Steam's status into new arrays, and the reads in between allocate nothing. A connection that Steam reports closed answers false at once.
- Base: `LocalNetwork.CreateMachineWide(name)` creates a local network that every process on this computer reaches by the same name, so editors and player builds on one computer create, search, and join lobbies and play sessions together without Steam, through the same `LocalBackend` (`new LocalBackend(LocalNetwork.CreateMachineWide("my-game"), "Player")`); `new LocalNetwork()` still keeps every player inside one process. Each backend listens on a port of its own on the loopback address, and the process that creates a lobby lists it in a file under the user's temporary folder, so another computer can neither find the lobbies nor connect to them. When a lobby's owner leaves or its process ends, the lobby passes to its first remaining member, as a Steam lobby does, and a lobby whose owner's process ended leaves search as soon as another process notices. The name may not be a Windows device name such as `CON`. A connection that receives nothing for `LocalNetwork.ConnectionTimeoutSeconds` (10 by default; 0 waits forever, for example while a debugger holds the other process) counts as lost; give every process of the network the same value. Without a player ID, a backend on such a network picks a random one. `SteamBackendOptions.EditorFallbackNetwork` takes such a network too, so an editor without Steam plays with the other editors and builds on the computer. Both kinds of local network can play a worse network, to try a game on one without Steam: `LocalNetwork.SimulatedLatencyMilliseconds` delays each packet between players by that much one way, `SimulatedJitterMilliseconds` lets each delay stray at random up to that much either way, so unreliable messages can overtake each other, and `SimulatedLossPercent` loses that share of the packets. An unreliable message that the loss takes never arrives, while a reliable one is sent again one round trip later, up to ten times, and the reliable messages after it wait for it, so they all still arrive, in the order they were sent. On a network inside one process, a host that leaves always delivers its last reliable messages before its clients hear that it left. On a machine-wide network, what the simulated network still holds back when a connection closes, or when its backend is disposed, goes out at once, ahead of the close, and a host that leaves delivers its last reliable messages first as far as its socket takes them before it hands the lobby over; the rest of a final burst larger than that can still be cut off when its clients hear that it left. The values act on the connections between players, not on lobbies, can change at any time, and refuse a negative or non-finite value or a loss above 100 %; on a machine-wide network each process applies its own values to what it sends, so give every process the same ones. `LocalBackend.SimulateOutage(seconds)` cuts a player off its session connections for a while, as when its network drops: no session connection opens to or from it, unreliable messages either way are lost, and reliable ones, a close included, wait for the outage to end. A connection that the outage keeps silent for `LocalNetwork.ConnectionTimeoutSeconds`, which now applies inside one process too, drops on both sides as lost, one that the cut-off player closes ends on the other side at once as lost, and lobbies go on as before. On a machine-wide network it acts on that process's side of its connections, so what the other side sent unreliably meanwhile still arrives when a connection lives through the outage. The local backend measures each connection with a small probe four times a second and publishes the quality every half second: the ping is the round trip that the simulated latency and jitter gave the probes, and so 0 without them, since it leaves out the wait for the next update, as Steam's ping leaves out how often a game reads its messages, and the bytes count the session's packets without resends.

### Fixed
- Base: the `Black`, `Blue`, `Green`, `Magenta`, `Red`, and `White` materials in `Runtime/Materials` use Unity's built-in Standard shader. They pointed at a copy of `Standard.shader` from Unity's built-in shader source that the repository never contained, so a project that installed Base could not find their shader.

## [base/0.27.0] - 2026-10-31

### Added
- `IAddressablesManager.UpdateCatalogs(callback, autoCleanBundleCache, autoReleaseHandle)` and, with UniTask, `UpdateCatalogsAsync(autoCleanBundleCache, autoReleaseHandle)` also remove the cached bundles that the updated catalogs no longer reference when `autoCleanBundleCache` is true. A class that implements `IAddressablesManager` itself has to add them.
- `CoreBase.OnDisable()` is `protected virtual`, like `Awake`, `Start`, `OnEnable`, and `OnDestroy`. A core that declares its own `OnDisable` should override it and call `base.OnDisable()`; until then the compiler reports warning CS0114 for it, and the preparation fix below does not reach that core. The Template sample cores that unhook UI events in `OnDisable` now do so.
- Base, URP, Watermelon Game: each package carries the MIT license as `LICENSE.md` at its root, the text of the repository's `LICENSE`. A Git install receives only the package folder, so it came without a license file before. Each `package.json` also declares `"license": "MIT"` and points the Package Manager at the repository: `documentationUrl` at the README, for Watermelon Game at its package README, which documents its runtime API, and `changelogUrl` at the root `CHANGELOG.md`, which holds the full release notes that the package-local `CHANGELOG.md` only refers to.
- `InjectableMembers.GetFields(Type)` and `InjectableMembers.GetProperties(Type)` return the fields and properties that `[Inject]` fills on a type. The container and the editor DI validator both use them, so the validator checks exactly the members the container injects.
- `TimeMode` (`Mu3Library.Utility`) names the clock a timed transition runs on: `Unscaled` follows `Time.unscaledDeltaTime`, `Scaled` follows `Time.deltaTime`. Each `AnimationHandler` has a **Time Mode** field and a `TimeMode` property for its view, and `IAudioManager.TimeMode` sets the clock for every fade, duck, loop interval, and gap between playlist tracks of that manager. Both default to `Unscaled`, as described under Changed. A class that implements `IAudioManager` itself has to add the property.
- With Addressables installed, `ISceneLoaderEventBus.OnSceneLoadFailed` reports an Addressables single or additive scene load that started and then failed, for example for a key no catalog holds. Its `SceneLoadFailedInfo` carries the key as `Target`, `IsAdditive`, and the message Addressables failed the load with. By the time it is raised the failed command is over: `IsLoading`, `LoadingCount`, and the status queries no longer count it, and a handler can start the next load. A class that implements `ISceneLoaderEventBus` itself has to add the event where Addressables is installed.
- Editor: `ScriptBuilder.TryBuild(spaces, out script, out error, blocks)` returns false with the reason, and no script, when the blocks cannot be built completely, and `ScriptBuilder.MaxNestingDepth` is the deepest code block nesting a build accepts (64). `ScriptIdentifier.IsValidIdentifier` and `ScriptIdentifier.IsValidNamespace` tell whether a name can go into generated code unchanged.
- Editor: the Addressable Group Data Exporter and the Localization Data Exporter have a **Define Symbol** field. When it is set, every script they write is wrapped in `#if <symbol>` below the generated header, so a project compiles those scripts only where the symbol is defined, such as the `versionDefines` symbol an assembly sets for the package the scripts use. A value that is not a valid symbol name disables **Generate C# Scripts** and says why. The UtilWindow sample's exporters set it to `TEMPLATE_ADDRESSABLES_SUPPORT` and `TEMPLATE_LOCALIZATION_SUPPORT`, so the Template sample's key scripts keep their guard when they are generated again.

### Changed
- Base, URP, Watermelon Game: each package declares Unity 6000.3 as the lowest Editor it supports (`"unity": "6000.3"` in `package.json`, was `"6000.0"`), and the README states it in its badge (6000.3+) and requirements (Unity 6.3). The URP package already depended on URP 17.3.0, which itself requires Unity 6000.3, and Watermelon Game depends on the URP package, so the old floor contradicted their dependencies; no Editor below 6000.3 had compiled Base either. All three packages compile on Unity 6000.3.17f1 with the Unity package versions that Editor bundles, URP 17.3.0 and uGUI 2.0.0 among them, and their EditMode tests pass there, so their dependency versions stay as they are. Unity 6000.0 through 6000.2 are no longer supported.
- MVP view animations and audio fades, ducks, loop intervals, and the gap between BGM playlist tracks advance on unscaled time by default, so they keep running while `Time.timeScale` is 0, as under a pause menu. They followed `Time.deltaTime` before: an `AnimationView` opened at a time scale of 0 stayed opening, where `Close`, `CloseFocused`, and a click on the outside panel could not close it, and `TransitionBgm`, `FadeOutBgm`, and `DuckBgm` stood still. Under any other time scale than 1 they now run at real speed as well. To keep the old behavior, set `TimeMode.Scaled` on the `AnimationHandler` or on the `AudioManager`; views saved before this change have no stored value and take the new default.
- `CoreBase`, `CoreRoot`, `MVPManager`, `Singleton`, and `GenericSingleton` find scene objects through the `FindObjectsByType` overloads without `FindObjectsSortMode` from Unity 6.4, where the overloads that take it are obsolete. Earlier editors keep the old call, so every version finds the same objects and returns them unsorted.
- The DI container's registration log no longer depends on the `DEVELOPMENT_BUILD` symbol, which Unity 6.6 deprecates. The Editor always logs, and a player reads `Debug.isDebugBuild` once at startup, so it logs in development builds only, as before.
- `InputSystemManager.Dispose` disables every Input Action asset the manager holds, an asset added with `AddInputActionAsset` included, as replacing or removing an asset under its id already did, and it still destroys only the assets it built from JSON. Before, an asset from outside that the manager had enabled kept raising input after its scope was disposed. Enable such an asset again after the dispose if it should keep running.
- Editor: the DI validator and runtime diagnostics drawers find the `CoreRoot` through `FindAnyObjectByType`, since Unity 6.4 deprecates `FindFirstObjectByType`. From Unity 6.5 the MVP helper looks a type up in `CurrentAssemblies.GetLoadedAssemblies()`, because `AppDomain.GetAssemblies()` can return assemblies Unity already unloaded.
- `UpdateCatalogs` and `UpdateCatalogsAsync` reach Addressables only once the `AddressablesManager` is initialized. A manager that has not initialized yet runs the initialization `Initialize` runs, with the same `IsInitializing` state and the same initialization events, and an update made while an initialization runs waits for it; the update goes on once those events have run. When that initialization fails, the callback receives null and Addressables is not called, and later updates fail the same way without another attempt until an initialize call succeeds, because Addressables does not run a failed initialization again. Before, an update made before Addressables finished initializing started a check inside a chain operation that nothing released.
- The Template sample takes 15.7 MiB instead of 30.4 MiB, which a Git install downloads with the package even when the sample is not imported. Its three BGM tracks are re-encoded from 320 kbps to 160 kbps MP3 and import with the **Load Type** Streaming instead of Decompress On Load, so a loaded track no longer decompresses to about 27 MiB of PCM in memory. Its `NotoSansKR-Bold.ttf`, the font of the Localization scene, keeps only printable ASCII, the characters of KS X 1001 other than Hanja (the 2,350 precomposed Hangul syllables, the Hangul compatibility jamo, kana, and the symbols), and the three characters of the Japanese locale's native name, which takes it from 5.9 MiB to 0.7 MiB. Every string the sample shows is among them, but other text, such as a Hangul syllable outside KS X 1001 or most kanji, does not render with this font, so a project that shows such text needs the full Noto Sans KR or another font; `Third-Party Notices.txt` describes the subset. Its four scene thumbnails are 960×540 instead of 1920×1080, still larger than the Main scene draws them at its 1920×1080 reference resolution.

### Removed
- The Template sample's `Mu3Library.Sample.Template.Editor` assembly definition, which held no scripts. Unity logged "will not be compiled, because it has no scripts associated with it" for it in every project that imported the sample.
- The Template sample's NotoSans-Bold and NotoSansJP-Bold fonts with their font assets. The Localization scene was the only user of either, as the font of its text and a fallback for Japanese, and now uses NotoSansKR-Bold alone, because a character drawn from a fallback font asset makes TextMesh Pro read the settings that only the TMP Essential Resources provide.

### Fixed
- The package compiles on Unity 6.5 and later, where two APIs it used became compile errors. `Scene.handle` became a `SceneHandle` that no longer converts to `int`, so `SceneLoader` keeps the `Scene` an operation loads or unloads and compares scenes. `Object.GetInstanceID()` is rejected, so `GameObjectPool` tells pooled objects apart by reference, which also ignores an `Equals` override on the component, and its warning names the object instead of an instance ID. The same code also compiles on Unity 6000.3, the lowest Editor the packages now support.
- `package.json` declares the Unity packages and built-in modules that the package's code uses outside a define symbol: uGUI (`com.unity.ugui`) and the UI, Audio, Particle System, JSONSerialize, AssetBundle, UnityWebRequest, and UnityWebRequestTexture modules, plus IMGUI, Image Conversion, and Screen Capture for the editor tools. A project that had removed uGUI or disabled one of these modules got compile errors inside the read-only package; the Package Manager now keeps them enabled. The optional integrations (UniTask, Addressables, Localization, Input System, Mobile Notifications) stay out of `dependencies` and compile only behind their define symbols, as before.
- A Git install no longer receives `Runtime/Prefabs.meta`, the metadata of a folder that never held a file. Git stores no empty folders, so the install got the `.meta` without its folder.
- With UniTask installed, the default `CoreBase.PrepareCoreAsync(CancellationToken)` returns a completed `UniTask<bool>` instead of being an `async` method without `await`, which raised compiler warning CS1998 and built a state machine on every call. It keeps its signature and stays `protected virtual`, so overrides keep compiling.
- DI lifecycle callbacks run one at a time. An exception from a service's `Initialize`, `Update`, `LateUpdate`, or `Dispose`, or from a core's update, is logged with `Debug.LogException`, and the remaining services and cores still run, in that frame and in the frames after. Before, one failing service stopped every service and core after it, and a failed `Initialize` kept its core from ever reporting `OnCoreInitialized` and `OnCorePrepared`; the core now reports both. A service whose `Initialize` throws when it is first resolved after its core initialized is logged the same way and still returned.
- A core that registers or leaves while `CoreRoot` updates its cores, for example a core instantiated from a service's `Update`, no longer reorders the running loop, which updated one core twice and skipped another in that frame. A new core still initializes at once and joins the update loop from the next frame.
- `[Inject]` fields and properties that a base class declares, private ones included, are injected. Reflection on the derived type did not see a base class's private members, so they stayed null without an error, and a required one was never checked. The search stops at `CoreBase`, `object`, and the engine's own types. The editor DI validator checks the same members. A required base-class member that cannot be resolved now throws "Inject failed" when the core starts, as a required member of the core's own class always did, so that core no longer prepares. A registered service with such a member throws when it is resolved, which for a service added with `RegisterClass<T>()` is during `ConfigureContainer`, so its core is not registered at all. Mark a member that may stay empty `[Inject(required: false)]`.
- The instances of one registration, which is its implementation type, key, and lifetime, are the same whatever service type resolves them. A keyed singleton was created once per service type and ran `Initialize` and `Update` once per instance, and a scoped registration gave each service type its own instance within one scope. Several factories registered for one service type now each create their own instance in `ResolveAll`, and a factory registered after the first resolve takes effect; before, singleton and scoped factories all returned the instance the first one created. Transient factories were not affected.
- Once the application starts quitting, which includes leaving play mode in the Editor, `CoreRoot.Instance` returns null instead of creating a new `CoreRoot`. A core destroyed after the root no longer leaves a new `CoreRoot` object behind, which Unity reported as "Some objects were not cleaned up when closing the scene".
- Without UniTask, a core whose component is disabled or whose object is deactivated while it prepares stops preparing, and once both are back it starts preparing again from the beginning, logging one warning then, so it reaches `OnPrepared`. Before, Unity stopped the preparing coroutine when the object was deactivated, and the core stayed `IsPreparing` for good. Disabling only the component used to let the preparation run on; it now stops it as well, because deactivating the object after that stopped the coroutine with no `OnDisable` left to notice, so a core that stays disabled no longer finishes preparing. The core steps the routine that `PrepareCoreAsync` returns, and every routine that one yields, inside its one coroutine, so a stopped preparation does not keep stepping next to the one that replaces it. A coroutine the routine starts with `StartCoroutine`, or an asynchronous operation it waits on, is not part of that and keeps running while only the component is disabled. The UniTask path is unchanged.
- `ObservableProperty<T>` is marked `[Serializable]`, as every Observable type derived from it already was. Unity 6.6's serialization analyzer reported warning UAC1002 for a field of such a type, such as the Attribute sample's `ObservableFloat`. The `_value` field keeps its name and place, so values already saved in scenes and assets load as before.
- A new `ObservableList<T>` starts with an empty list, so `Value` and `ReadOnly.Value` are not null before the first change, and a subscriber notified at once receives the empty list. Before, the list was created only when another member was first used.
- When `Subscribe(callback, notifyImmediately: true)` on an `ObservableProperty<T>` or `ObservableDictionary<TKey, TValue>` calls the callback and it throws, the callback is removed again before the exception reaches the caller. Before, the caller got no token, so the callback stayed subscribed with no way to remove it and ran on every later change. The exception still reaches the caller, and later changes still isolate a subscriber that throws.
- Editing an `ObservableDictionary<TKey, TValue>` in the Inspector no longer overwrites or resets entries. When serialized rows repeat a key, whether they come from the Inspector, a scene, or JSON, the first row now wins where the last one used to, so a row added with "+" or a key typed into a new row never replaces an existing entry. Such a row, and a row whose object key is empty, stays in the list through serialization, and the drawer names it in a warning under the list until its key is changed or the row is removed. A row that repeats a key leaves with that key when runtime code removes it. The drawer no longer resets the rows the list gains, so an entry added by a runtime `Add`, restored by Undo, or created by duplicating an element keeps its key and value, and the per-path size cache it kept for that is gone. Before, every new row was reset to the default key and value, and the next serialization rebuilt the rows from the dictionary, so the new row replaced the entry with the default key and then disappeared.
- Closing an MVP owner unloads every child window hosted under its view, even when the owner finishes closing first, such as a plain `View` owner with an `AnimationView` child. Hiding the owner stopped the child's close coroutine and left the child closing for good: it raised neither `OnWindowClosed` nor `OnWindowUnloaded`, never ran `UnloadFunc()` or returned to the pool, showed up again under the owner when the pooled owner reopened, and could hold the focus so that `CloseFocused()` did nothing. A view whose close is cut short by being hidden now counts as closed, so the child unloads in the next frame, after its owner. A force-closed owner that is still loading, for example in a `CloseAll(true)` in the frame where its `LoadFunc()` opened a child, is now shown before its children close, so the child runs its close like any other window; before, Unity refused to start the child's close coroutine under the hidden owner. A view that is closed while it is hidden, such as a child closed directly while its owner still loads, skips its close transition and goes straight to closed.
- `CloseAsync` completes once the window it closed has unloaded, even when an `OnWindowUnloaded` handler opens the same presenter type again at once, as a toast or notification queue does. The pool hands that handler the same presenter instance, and `CloseAsync` waited for the instance to leave the manager, so it also waited for every window the queue opened after it and never completed while the queue stayed full. `OpenAsync` now follows the opening it started the same way, so a window released and reopened in one manager update no longer makes it wait for the later opening.
- Opening the first window outside play mode, for example in an EditMode test, no longer throws `InvalidOperationException`. `MVPManager` calls `DontDestroyOnLoad` on its root and on the `EventSystem` it creates only in play mode, the only place Unity allows it. The rest of its edit-mode behavior is unchanged: `Dispose` still destroys with `Object.Destroy`, which Unity refuses outside play mode, and a view only gets `OnDisable` there when its class has `[ExecuteAlways]`.
- The `EventSystem` that `MVPManager` creates when the scene has none gets the input module of the input backend that Active Input Handling enables. With the Input System package installed under "Input Manager (Old)", as in a project that gets the package as another package's dependency, it got `InputSystemUIInputModule`, which reads nothing while the Input System backend is off, so no MVP view could be clicked. It now gets `StandaloneInputModule` there. "Input System Package (New)" and "Both" with the package still get `InputSystemUIInputModule`, and a project without the package gets `StandaloneInputModule`. The README lists the four combinations.
- A window whose host or owner is hidden while it is still opening, such as a window behind a tab panel that is switched away during its open animation, reaches opened. Hiding the view stopped its open coroutine, so the window stayed on its way in for good: `OpenAsync` never completed, a `Close` that was not forced, such as the window's own `CloseSelf()`, was refused after the tab was shown again, and an `AnimationView` kept the pose of the frame it was hidden in. The open now ends when the view is hidden: the rest of its transition is skipped, `OpenEnd()` runs, an `AnimationView` takes its opened pose, and the window counts as opened in the next manager update. The manager ends such an open itself when the view overrides `OnDisable` without calling `base.OnDisable()`.
- An `AnimationHandler` that is hidden or disabled keeps the time factor of its pose. It reset the factor to 0, the closed pose, while the view stayed in its pose on screen, so an `AnimationView` hidden and shown again while open closed at once, without its close animation. A handler hidden in the middle of an animation now also takes that animation's end pose, where it stopped in the pose of that frame.
- The Template sample compiles in a project without Addressables, Localization, or the Input System. Its generated Addressables and Localization key scripts, `ISCore`, and the scripts that load the Addressables scenes, read the Addressables labels, release the Input System actions asset, or refresh an input action used those packages outside their `TEMPLATE_*_SUPPORT` symbols, so importing the sample into such a project stopped every script in it from compiling. Without one of the packages, the part of the sample that needs it logs that the package is not installed.
- The Template sample's keyboard shortcuts and its Audio3D mouse clicks read the input backend the project enables, through the sample's new `SampleInput` class: the Input System while its backend is on, otherwise the Input Manager. They called `UnityEngine.Input`, which throws `InvalidOperationException` on every frame under "Input System Package (New)".
- Base, URP: the buttons, toggles, and sliders of the Template, Attribute, and ScreenEffect sample scenes respond to input without the Input System package, and with it under "Input Manager (Old)", "Input System Package (New)", or "Both". The `EventSystem` in these scenes held an `InputSystemUIInputModule`, which is a missing script without the package and reads nothing under "Input Manager (Old)". It now holds a `StandaloneInputModule`, which reads the Input Manager, and the sample's new `SampleInputModuleSelector`, which replaces that module with an `InputSystemUIInputModule` when the scene starts if the package is installed and Active Input Handling is "Input System Package (New)" or "Both", the rule `MVPManager` follows for the `EventSystem` it creates. Each of the three samples carries its own copy of the component. For it, the Attribute sample's scripts compile into an assembly of their own, `Mu3Library.Sample.Attribute`, which defines `ATTRIBUTE_INPUTSYSTEM_SUPPORT` while the package is installed, instead of the project's `Assembly-CSharp`. The Template sample's `Sample_IS` scene, which shows `InputSystemManager`, still needs the package with "Input System Package (New)" or "Both" for its own features; its UI responds in every setup.
- Base, URP: the TextMesh Pro text of the Template, Attribute, and ScreenEffect samples renders in a player built from a project without the TMP Essential Resources. Apart from three texts in the Template's Localization scene, which used the sample's NotoSans-Bold SDF with its fallbacks, every text used the LiberationSans SDF font asset of those resources, and in such a project each text threw `NullReferenceException` in `TMP_Settings` and stayed blank. The font is not all that TextMesh Pro reads from its settings at runtime: a text without a style sheet of its own reads the default style sheet there, a character missing from its font asset reads the fallback fonts, a character drawn from a fallback font asset reads the material preset setting, and Chinese, Japanese, or Korean text that wraps or is truncated reads the line-breaking rules. TextMesh Pro's shaders also come only with those resources. Each sample therefore ships what its text uses: `Fonts/Sample Sans SDF.asset`, a static font asset of 250 Latin characters that TextMesh Pro built from Liberation Sans, `Fonts/Sample Style Sheet.asset` with the Normal style, and `Shaders/TMP_SDF-Mobile.shader`, TextMesh Pro's mobile distance field shader under a name of the sample's own, and every text references that font asset, its material, and the style sheet. Without its settings TextMesh Pro also reports a `preferredWidth` and `preferredHeight` of 0 to the layout system, so the six texts whose size a layout takes, the scrolling log of the WebRequest scene and the section titles of the Audio scene, carry the Template sample's new `SampleTextPreferredSize`, which measures them in such a player with `TMP_Text.GetPreferredValues(width, height)`; without it the log could not scroll and the titles shrank. The Template sample's Localization scene draws its English, Japanese, and Korean strings with `NotoSansKR-Bold SDF` alone, which covers Latin, Hangul, and kana, in a text that does not wrap, and that dynamic font asset no longer reads font features at runtime. Each sample's `Third-Party Notices.txt` lists the licenses: the SIL Open Font License 1.1 for the fonts and the font assets built from them, and the Unity Companion License for the shader. In the Editor, TextMesh Pro still draws no text until the TMP Essential Resources are imported, and it offers to import them; text with characters that the samples' fonts do not carry needs them as well.
- The UtilWindow sample's `DependencyChecker.asset` starts with an empty object list. Its one entry pointed at a scene the repository never contained, so the Dependency Checker of every project that imported the sample listed it as "Missing". The asset no longer holds a reference that cannot resolve.
- The Template sample's Localization scene fills its locale dropdown when a locale has a custom code. `LocaleIdentifier.CultureInfo` stays null for a code the culture table does not carry, and the sample read its English name straight through, so `SetDropdownOptions` threw `NullReferenceException` and left the dropdown empty. Such a locale is now listed under its code.
- Editor: the Observable drawers find the edited instance by following the property path from each selected object instead of reading the drawer's field from the inspected object. An Observable field inside a `[Serializable]` class, in an array or list element, or in a multi-object selection now takes the edit and notifies its subscribers. Before, a field inside a serializable class threw `ArgumentException` on every edit, an element's field changed without a notification, and a multi-object edit notified only the first object. The value drawers apply the edit first and then notify the instances whose value it changed.
- An undo, a redo, or a prefab revert in the Editor that changes the value of an Observable notifies its subscribers with the restored value, on the main thread right after the operation. Unity writes restored data into the live objects without going through the Observable, so a subscriber, such as the label that shows the Attribute sample's `ObservableFloat`, kept the value from before. An undo or a redo covers the Observables of the selected objects, including the components of a selected GameObject, which is where an Inspector edit comes from; a revert covers the reverted prefab instance. An Observable whose value is a number, a bool, a char, a string, an enum, a Unity object reference, or a struct made of such fields notifies only when the restore changed its value, as `Set` does, and an `ObservableDictionary<TKey, TValue>` with such keys and values compares its entries as its indexer does. Unity refills a list or another object in place, so its previous contents cannot be compared: an `ObservableList<T>`, or an Observable or dictionary that holds such a value, notifies on every undo, redo, or revert that covers its object once Unity has deserialized that object since the last notification, even when the contents stayed the same. Deserialization itself still calls no subscriber, so loading a scene or instantiating a prefab notifies nobody: `ObservableProperty<T>` now implements `ISerializationCallbackReceiver` explicitly, and its callbacks only note a restore. A derived class that implements `ISerializationCallbackReceiver` itself replaces those callbacks, and its restores are not notified. EditMode tests now cover the revert as well: `PrefabUtility.RevertPropertyOverride`, `RevertObjectOverride`, and `RevertPrefabInstance` each notify the reverted value, an undo of the revert notifies the overridden value again, and an Observable whose value the revert left as it was is not notified.
- An SFX or environment source that `PlaySfx` or `PlayEnvironment` takes from the pool, or takes over from the oldest playing sound, plays at full volume. A source that `FadeOutSfxAll`, `FadeOutFirstSfx`, or their environment counterparts returned to the pool kept the silence its fade-out ended in, so the sounds played on it afterwards were mute. `FadeInSfx` and `FadeInEnvironment` still start from silence.
- An audio fade of 0 seconds or less takes effect at once. `TransitionBgm(clip, 0f)`, `FadeInSfx(clip, 0f)`, and `FadeInEnvironment(clip, 0f)` play at full volume, where they stayed silent, and a fade-out of 0 seconds, such as `FadeOutBgm(0f)`, silences the source before it pauses, stops, or pools it.
- `PlayBgm`, `PlayBgmForce`, and `TransitionBgm`, with their `WithKey` and async variants, end a running BGM playlist, as `StopBgm` already did. Before, the playlist kept running: when the gap between two tracks ran out, it replaced the track the caller had just started with its next one, and a requested track that played a set number of times resumed the playlist when it ended. The playlist still plays its own tracks, and a `PlayBgm` with the clip it is playing starts that clip again under the requested settings, because the playlist plays each track once. Outside a playlist, `PlayBgm` with the clip that is fading out, for example right after `FadeOutBgm`, now cancels the fade and keeps the clip playing at full volume; before, the call was ignored and the fade paused the clip.
- A window opened under a host that is hidden, such as an inactive `HostOptions.Host`, reaches opened. Unity refused to start its open coroutine and logged an error, so the window stayed on its way in for good: `OpenAsync` never completed, a `Close` that was not forced was ignored, and the window could hold the focus while `CloseFocused()` did nothing. It now skips the wait of its open transition: `OpenStart()` and `OpenEnd()` run back to back, an `AnimationView` takes its opened pose at once, and the window shows fully open when the host is shown. Closed while the host is still hidden, an `AnimationView` takes its closed pose the same way, so its next open animates again.
- A child window hosted under an owner that finishes closing first also unloads when the child's view overrides `OnDisable` without calling `base.OnDisable()`. Hiding the view cuts its close short, and the manager now ends such a close itself in its next update, so the unload described above no longer depends on that call.
- Creating `AudioManager`'s root outside play mode, for example in an EditMode test, no longer throws `InvalidOperationException`. Like `MVPManager`, it calls `DontDestroyOnLoad` only in play mode.
- With UniTask installed, `GetResultAsync`, `PostResultAsync`, and `GetDownloadSizeResultAsync` answer an HTTP error response, such as 401, 404, or 500, with its `StatusCode` and `ResponseHeaders`, as the callback API does, so a caller can tell an expired login from a missing resource and read a `Retry-After` header. UniTask throws for such a response, and the manager took it for an unexpected failure: status code -1, no headers, and the exception in the log. A request that got no response, such as a refused connection, answers with status code 0 and no headers, again as the callback API does, and both now log the failed request as an error. Retries and cancellation are unchanged.
- An Addressables scene load that fails after it started is reported through `OnSceneLoadFailed`, described under Added. Before, the loader only logged it, so a loading screen raised on the load-started event and taken down on the loaded event stayed up for good.
- `UpdateCatalogs` and `UpdateCatalogsAsync` hand their `autoReleaseHandle` to Addressables as the handle release choice. It went into the bundle cache cleanup parameter, so the default call removed cached bundles nobody had asked to remove, and where the cache cannot be cleaned, an update that succeeded answered null and left the known keys unrefreshed; with `autoReleaseHandle: false`, the handle was released twice. The bundle cache is now cleaned only through the overloads described under Added. `CheckForCatalogUpdatesAsync` and `UpdateCatalogsAsync` take the result inside the completion callback and then resume on the player loop as before. They read the handle one frame later, after Addressables had released it, and threw "Attempting to use an invalid operation handle" whenever the check or update finished asynchronously, as it does against a remote catalog, so a successful update also skipped refreshing the known keys.
- `CheckForCatalogUpdates`, `UpdateCatalogs`, and their UniTask variants called with `autoReleaseHandle: true` release the handle Addressables returned whenever Addressables does not. Addressables auto-releases only the catalog operation itself; when it first has to wait, for its initialization or, in an update that knows of no catalog update yet, for the check it starts, it returns a chain operation around that operation and never releases the chain. That happened on every check or update made before Addressables finished initializing and on every update that no earlier check had found an update for, the default `UpdateCatalogs` call included, and the chain stayed in memory with the operations it held. The manager now looks at the handle again on the next ResourceManager update after the callback, once Addressables has released what it auto-releases, and releases it if it is still valid, so each handle is released once. An update no longer reaches Addressables before the manager has initialized, as described under Changed, so the check Addressables starts for it no longer stays behind inside a chain the manager cannot reach.
- `ChangeLocaleAsync` completes once the locale change is over: the Localization package has initialized again for the new locale, table preloading included, and `OnLocaleChanged` has been raised for it. `IsLocaleChanging` stays true until then, and `CancelChangeLocale()` ends the wait while the selection stays. Before, it completed in the frame of the call, while the tables were still loading and before `OnLocaleChanged`, because it waited on the selected-locale handle, which the package completes at once. Selecting the locale that is already selected restarts nothing, so the call completes at once, unless an initialization or the announcement of that locale is still under way, such as after a second call for the locale a first call is still waiting on; it then waits for them as well.
- `InputSystemManager.StartInteractiveRebind` gives the action back the enabled state it had once the rebind completes, is canceled, or cannot start. A rebind of an action in a disabled asset left that one action enabled. A binding index that points at a composite, such as a "2D Vector", is rejected before the action is touched and answered with an error log and null, like an index out of range; before, the Input System's exception escaped and left the action disabled.
- The derivatives of the `EaseInOut` easing functions, such as `EaseInOutQuadD` or `GetEasingFunctionDerivative(Ease.EaseInOutCubic)`, return the slope of their easing function. Each `EaseInOut` function runs its two halves on doubled time, and every derivative but `EaseInOutSineD` left out that factor of 2, so a speed taken from them was half the real one. `EaseInOutElasticD` also ignored the doubled time and used a wrong phase, so its values did not follow the curve at all, and `SpringD` subtracted one of its terms five times too strongly. An EditMode test now compares every `EasingFunction.Ease` kind with a central-difference derivative of its easing function. Code that doubled these values to make up for the halved speed has to stop doing so.
- Editor: `ScriptBuilder` no longer stops writing after 1000 lines. A Resources path script for some 450 Resources assets or more, or an Input System name script for some 160 actions or more, ended in the middle of a class, was saved anyway with a "generated" log, and failed to compile with CS1513. The output length is no longer limited; to stop a block that contains itself, a build refuses code blocks nested deeper than `MaxNestingDepth`. When the blocks cannot be built completely, for that reason or because a content item is null or of an unsupported type, `Build` throws `InvalidOperationException` instead of returning part of the script; it used to skip an item of an unsupported type with a warning. Code that calls `Build` itself can catch it or call `TryBuild`. The Resources path and Input System exporters and the MVP Helper use `TryBuild`, log an error, and write no file. `AppendStackCountMax` has no effect any more and is marked obsolete, and `Build` no longer logs its line count.
- Editor: the MVP Helper generates scripts that compile with its default settings. With the namespace left empty, it writes the four scripts without a namespace block, where it wrote `namespace` without a name, and it checks for existing types in the global namespace, where it looked for `.{name}` and never found one. A script name or namespace that is not a valid C# identifier, such as `My Popup`, disables **Create MVP Components** and shows why; spaces around them are ignored. Before it replaces a script file that already exists, it asks, and if the overwrite is canceled it writes none of the scripts. The UtilWindow sample's `MVPHelper.asset` starts with no save folder, script name, or namespace and with **Ignore Type Overlap** off. It pointed at the Template sample's `LinkParent` scripts, so in a project that imported both samples one click replaced them with empty skeletons.
- Editor: the Resources Path, InputSystem Data, Addressable Group Data, and Localization Data exporters check the namespace as the MVP Helper does. A namespace that is not C# identifiers joined with `.`, such as `My Game`, `Game.`, `1st`, or `class`, replaces the generate button with a warning that says why, as the exporters' other input checks do; spaces around it are ignored. Before, they wrote it into `namespace` unchecked, and the saved script failed to compile in its assembly. An empty namespace still generates the scripts without a namespace block.
- Editor: the fallback Inspector that draws `ButtonInvoke` buttons builds its UI with UI Toolkit through `CreateInspectorGUI`: Unity's default inspector, then the buttons. It draws every `MonoBehaviour` and `ScriptableObject` without a custom editor of its own, whether or not the type uses `ButtonInvoke`, and it drew them through IMGUI, so once Base was installed a field whose property drawer implements only `CreatePropertyGUI`, as UI Toolkit drawers from other packages may, showed "No GUI Implemented" in every such Inspector. Those drawers now render, IMGUI drawers keep working inside it as in Unity's own default inspector, and multi-object editing and the buttons work as before. An editor derived from `ButtonInvokeEditorBase` that overrides `OnInspectorGUI` keeps drawing through it, because `CreateInspectorGUI` then returns null. A tool that calls `OnInspectorGUI` directly still gets the IMGUI version. The README describes the fallback Inspector.
- `AddressablesManager`'s catalog checks and updates, the UniTask variants included, no longer overlap in Addressables. Addressables releases the check handle it still holds whenever a new check starts, the check that an update knowing of no catalog update starts by itself included, so a second check made before the first one finished, or an update made from a check callback, released one handle twice: the callbacks did not receive their results, the check operation was destroyed twice, and the exception this threw out of the ResourceManager update stopped every later update, so no Addressables operation finished for the rest of the session. The calls now reach Addressables one at a time, in call order: a call made while another one runs, from its callback included, starts on the ResourceManager update after that call's callback, once its handle is settled. A handle asked for with `autoReleaseHandle: false` is released on that update too, instead of right after the callback. `Dispose` reports a catalog call that has not reached Addressables yet with null, so an awaited `CheckForCatalogUpdatesAsync` or `UpdateCatalogsAsync` does not wait forever, and a call Addressables already runs finishes as before. A call that throws before it reaches Addressables is logged and reported with null, and the calls after it still get their turn.
- `AddressablesManager.Dispose()` no longer releases the initialization handle. Addressables releases its initialization itself once it completes and hands a running one to every caller without adding a reference, so the manager never held one: disposed while it initialized, or before Addressables let go of an initialization that had already finished, it released the operation once too often, and the operation was destroyed inside its own completion, before the callbacks registered after the manager's could read it. `Dispose` also really takes its completion handler off now. The handler went through the untyped handle, whose `Completed` event wraps every handler, so the removal added in base/0.24.0 never matched, and a completion after `Dispose` still reached the disposed manager.
- When `Addressables.InitializeAsync` throws, `AddressablesManager.IsInitializing` stays false, so a later initialize call or catalog update starts a new attempt instead of waiting for one that never started.

## [urp/0.4.0] - 2026-10-31

### Added
- Base, URP, Watermelon Game: each package carries the MIT license as `LICENSE.md` at its root, the text of the repository's `LICENSE`. A Git install receives only the package folder, so it came without a license file before. Each `package.json` also declares `"license": "MIT"` and points the Package Manager at the repository: `documentationUrl` at the README, for Watermelon Game at its package README, which documents its runtime API, and `changelogUrl` at the root `CHANGELOG.md`, which holds the full release notes that the package-local `CHANGELOG.md` only refers to.

### Changed
- Base, URP, Watermelon Game: each package declares Unity 6000.3 as the lowest Editor it supports (`"unity": "6000.3"` in `package.json`, was `"6000.0"`), and the README states it in its badge (6000.3+) and requirements (Unity 6.3). The URP package already depended on URP 17.3.0, which itself requires Unity 6000.3, and Watermelon Game depends on the URP package, so the old floor contradicted their dependencies; no Editor below 6000.3 had compiled Base either. All three packages compile on Unity 6000.3.17f1 with the Unity package versions that Editor bundles, URP 17.3.0 and uGUI 2.0.0 among them, and their EditMode tests pass there, so their dependency versions stay as they are. Unity 6000.0 through 6000.2 are no longer supported.

### Removed
- The package's `Mu3Library.URP.Editor` assembly definition and the ScreenEffect sample's `Mu3Library.URP.Sample.ScreenEffect.Editor`, which held no scripts either and logged the same warning in every project that installed the package or imported the sample. An assembly without scripts was never compiled, so a reference to one of them had no effect.
- The `MU3LIBRARY_URP_SUPPORT` define symbol, which `Mu3Library.URP.asmdef` declared through `versionDefines` whenever URP was installed. No code read it, and URP is a required dependency of the package, so the symbol was always defined and gated nothing. A symbol from `versionDefines` exists only inside its own assembly, so only code compiled into `Mu3Library.URP`, such as scripts added to it through an assembly definition reference, could see it. Such code that tests the symbol must now define it itself, for example under Scripting Define Symbols in Player Settings.

### Fixed
- Base, URP: the buttons, toggles, and sliders of the Template, Attribute, and ScreenEffect sample scenes respond to input without the Input System package, and with it under "Input Manager (Old)", "Input System Package (New)", or "Both". The `EventSystem` in these scenes held an `InputSystemUIInputModule`, which is a missing script without the package and reads nothing under "Input Manager (Old)". It now holds a `StandaloneInputModule`, which reads the Input Manager, and the sample's new `SampleInputModuleSelector`, which replaces that module with an `InputSystemUIInputModule` when the scene starts if the package is installed and Active Input Handling is "Input System Package (New)" or "Both", the rule `MVPManager` follows for the `EventSystem` it creates. Each of the three samples carries its own copy of the component. For it, the Attribute sample's scripts compile into an assembly of their own, `Mu3Library.Sample.Attribute`, which defines `ATTRIBUTE_INPUTSYSTEM_SUPPORT` while the package is installed, instead of the project's `Assembly-CSharp`. The Template sample's `Sample_IS` scene, which shows `InputSystemManager`, still needs the package with "Input System Package (New)" or "Both" for its own features; its UI responds in every setup.
- Base, URP: the TextMesh Pro text of the Template, Attribute, and ScreenEffect samples renders in a player built from a project without the TMP Essential Resources. Apart from three texts in the Template's Localization scene, which used the sample's NotoSans-Bold SDF with its fallbacks, every text used the LiberationSans SDF font asset of those resources, and in such a project each text threw `NullReferenceException` in `TMP_Settings` and stayed blank. The font is not all that TextMesh Pro reads from its settings at runtime: a text without a style sheet of its own reads the default style sheet there, a character missing from its font asset reads the fallback fonts, a character drawn from a fallback font asset reads the material preset setting, and Chinese, Japanese, or Korean text that wraps or is truncated reads the line-breaking rules. TextMesh Pro's shaders also come only with those resources. Each sample therefore ships what its text uses: `Fonts/Sample Sans SDF.asset`, a static font asset of 250 Latin characters that TextMesh Pro built from Liberation Sans, `Fonts/Sample Style Sheet.asset` with the Normal style, and `Shaders/TMP_SDF-Mobile.shader`, TextMesh Pro's mobile distance field shader under a name of the sample's own, and every text references that font asset, its material, and the style sheet. Without its settings TextMesh Pro also reports a `preferredWidth` and `preferredHeight` of 0 to the layout system, so the six texts whose size a layout takes, the scrolling log of the WebRequest scene and the section titles of the Audio scene, carry the Template sample's new `SampleTextPreferredSize`, which measures them in such a player with `TMP_Text.GetPreferredValues(width, height)`; without it the log could not scroll and the titles shrank. The Template sample's Localization scene draws its English, Japanese, and Korean strings with `NotoSansKR-Bold SDF` alone, which covers Latin, Hangul, and kana, in a text that does not wrap, and that dynamic font asset no longer reads font features at runtime. Each sample's `Third-Party Notices.txt` lists the licenses: the SIL Open Font License 1.1 for the fonts and the font assets built from them, and the Unity Companion License for the shader. In the Editor, TextMesh Pro still draws no text until the TMP Essential Resources are imported, and it offers to import them; text with characters that the samples' fonts do not carry needs them as well.
- The ScreenEffect sample's `ScreenEffectCore.OnDestroy` calls `base.OnDestroy()` after its own cleanup, so a destroyed core leaves `CoreRoot` and disposes its services. Loading the scene again in the same session logged "Core is already exist" and left the new core uninitialized.
- The four screen effects, Grayscale, GaussianBlur, DepthOutline, and Shake, work in a player build. The package's new `Runtime/Resources/Mu3Library.URP/ScreenEffectShaders.asset` references their shaders, and each effect takes its shader from it. Before, an effect looked its shader up by name with `Shader.Find` alone, and nothing in a build referenced those shaders, so a player left them out and every effect drew nothing unless the project had added the shaders to Always Included Shaders by hand. A project needs no setup now, and shaders already listed there keep working. Because the asset sits in a Resources folder, every player build of a project that installs the package carries the four shaders. A pass that a project derives from `ScreenPassBase` for a shader of its own still finds that shader by name, so the project still has to include it in the build.
- A screen effect whose shader cannot be found logs one error per shader name per play session, and its pass stops looking for the shader. Before, the pass searched and logged again whenever it asked for its material, which every active effect did for every camera in every frame. The error says how to include the shader in the build. A pass that a project derives for a shader it loads later, for example from an AssetBundle, has to be created after that shader is loaded.
- The ScreenEffect sample scene's Global Volume has no volume profile. It referred to `SampleSceneProfile.asset` of the `UnityProject_URP` development project, which the sample does not ship, so a project that imported the sample showed the profile as "Missing" and the scene drew without that profile's post-processing. The scene no longer refers to an asset of a development project. It draws without that profile's Bloom, Vignette, and Tonemapping in the development project too; the four screen effects do not use the volume and work as before.

## [game/watermelon/0.7.0] - 2026-10-31

### Added
- `InputHandler.OnApplicationFocus(bool)` and `InputHandler.OnApplicationPause(bool)` are `protected virtual` and cancel a running drag when the application loses focus or is paused, as described under Fixed. An `InputHandler` subclass that declares its own `OnApplicationFocus` or `OnApplicationPause` should override them and call the base method; until then the compiler reports warning CS0114 for it, and the cancel does not reach that handler.
- Base, URP, Watermelon Game: each package carries the MIT license as `LICENSE.md` at its root, the text of the repository's `LICENSE`. A Git install receives only the package folder, so it came without a license file before. Each `package.json` also declares `"license": "MIT"` and points the Package Manager at the repository: `documentationUrl` at the README, for Watermelon Game at its package README, which documents its runtime API, and `changelogUrl` at the root `CHANGELOG.md`, which holds the full release notes that the package-local `CHANGELOG.md` only refers to.
- `BoardItemsConfig.IsComplete()` tells whether each of the first eleven item entries can make an item, which takes an entry that is there and carries a sprite, and `IsComplete(List<int>)` also lists the indices of the entries that cannot. The board and `BoardConfig` use it to refuse and to report such a catalog, as described under Fixed.
- `InputHandler.OnTouchCanceled`, `BoardArea.OnTouchCanceled`, and `BoardController.OnDragCancel(Vector2)` report a drag that was taken away before it was released, with the screen position the `InputHandler` last reported for it, which can lie outside the board, as described under Fixed.
- `InputHandler.IgnorePressOverUI`, the **Ignore Press Over UI** field, which is on by default, keeps a press over UI from starting a drag, as described under Changed. `InputHandler.DragStartFilter` holds a press back by a project's own rule: it is asked with the screen position of every mouse press and touch before it starts a drag, and every function added has to return true. A subclass can override the `protected virtual` `CanStartDrag(Vector2)` and `IsPointerOverUI(Vector2)` instead.
- `InputHandler` reads its input through the `protected virtual` `IsMousePresent`, `MousePosition`, `GetMouseButtonDown()`, `GetMouseButton()`, `TouchCount`, and `GetTouch(int)`, so a subclass can drive the board from another input source. `BoardArea` uses a subclass that is already on its GameObject instead of adding an `InputHandler`.
- `IGuardedBoardCommand` (`Mu3Library.Game.WatermelonGame.Board.Command`) is an optional interface for a command that advances several commands within one step. The board hands it a guard when it accepts it, and the guard turns false once the board stops running. `CompositeBoardCommand` implements it: it passes the guard on to the groups it holds, and its `protected` `CanContinueStep()` tells a derived group whether it may advance another command in the current step, which it may not once the board stopped running or the group itself was canceled or disposed during the step. `IBoardCommandContext` is unchanged.

### Changed
- Base, URP, Watermelon Game: each package declares Unity 6000.3 as the lowest Editor it supports (`"unity": "6000.3"` in `package.json`, was `"6000.0"`), and the README states it in its badge (6000.3+) and requirements (Unity 6.3). The URP package already depended on URP 17.3.0, which itself requires Unity 6000.3, and Watermelon Game depends on the URP package, so the old floor contradicted their dependencies; no Editor below 6000.3 had compiled Base either. All three packages compile on Unity 6000.3.17f1 with the Unity package versions that Editor bundles, URP 17.3.0 and uGUI 2.0.0 among them, and their EditMode tests pass there, so their dependency versions stay as they are. Unity 6000.0 through 6000.2 are no longer supported.
- The sample's `WatermelonGameManager` finds the `BoardController` through `FindAnyObjectByType`, since Unity 6.4 deprecates `FindFirstObjectByType`. The sample scene holds one board, so the result is the same.
- A press that lands on UI no longer starts a board drag, so pressing a pause or item button drawn over the board no longer takes the held item along and drops it on release. `InputHandler` raycasts the press position through the current `EventSystem`, which reaches uGUI canvases and UI Toolkit panels; it reaches a UI Toolkit panel only while that `EventSystem` has an input module and keeps its UI Toolkit integration on, as it does by default. A hit of a `PhysicsRaycaster` or `Physics2DRaycaster` is a scene object and does not count, and without an `EventSystem` no press is held back. A full-screen raycast target over the board now holds back every press. Turn **Ignore Press Over UI** off to keep the previous behavior.
- Under "Both" with the Input System package installed, `InputHandler` reads the Input System instead of the Input Manager, as it does under "Input System Package (New)", described under Fixed. A subclass that reads the `Touch` that `OnTouchBeganHandler`, `OnTouchMovedHandler`, or `OnTouchEndedHandler` receives then gets the Input System's touch id as `fingerId`, which a new touch does not reuse.
- The sample's `item_box_background.png` is compressed again without changing a pixel, from 1.8 MiB to 1.0 MiB.

### Fixed
- `package.json` also declares the Physics 2D, Audio, and JSONSerialize modules its runtime uses.
- Hiding the board hierarchy during a game, for a pause menu or a screen change, keeps every item as it was. `BoardItem.OnDisable` reset the item for the item pool, so once the hierarchy was shown again every item on the board and the item in the player's hand had lost its index, sprite, collider, and physics. They stayed on the board as invisible obstacles that never moved, merged, ended the game, or went into a snapshot, and `SetBoardConfig` refused every configuration until the next `Prepare`. `OnDisable` now leaves the item alone; it stays `protected virtual`, so an override that calls `base.OnDisable()` keeps compiling. An item that goes back to the pool is still reset, because the board resets it before it pools it.
- After a `GameEnd()` or `Prepare()` called from an `OnItemMerged` or `OnScoreChanged` handler, the board hands out no further item in that frame. `Update` went on after the commands that raise those events, so it could put a new item into the player's hand after `OnGameEnded`, or before `GameStart()`, and the drag that followed dropped it on the board that was not running, which counted `SpawnCount`, played the drop sound, and raised `OnItemDropped`. `OnDragging` and `OnDragEnd` also leave the held item where it is on a board that is not running.
- After a handler of a board command's event, such as `OnItemMerged` or `OnScoreChanged`, calls `GameEnd()` or `Prepare()`, the board advances no further command in that frame, so an ended game keeps the score and the events it ended with. The commands after it still ran their step: a second merge found in the same frame took its pair off the board, paid its points into the score of the ended game, raised `OnScoreChanged` with another final score after `OnGameEnded`, and was reported through `OnItemMerged`. The command that raised the event still finishes its step, so a merge whose `OnScoreChanged` handler ends the game still puts the merged item on the board and reports `OnItemMerged`. After `GameEnd()` the commands left over stay in `Commands` without being advanced, and `Prepare` drops them. The protected `UpdateCommands()` also advances nothing when it is called on a board that is not running, as `EnqueueCommand` already describes. A `SequenceCommand` or `ParallelCommand` stops the same way between the commands it holds, through `IGuardedBoardCommand` described under Added: the command in it that raised the event finishes its step, and the group advances no further command in that frame, so a step after the merge in a sequence or a second merge in the group neither pays into the ended game nor is reported. A group that runs outside a board, through `BoardCommandRunner`, still advances every command of its step.
- A merge during which a handler of an event it raises, such as `OnScoreChanged`, `OnItemRemoved`, `OnItemAdded`, or `OnMergeComboChanged`, calls `Prepare()` skips the rest of its step. `Prepare` drops the running merge with the board it merged on, but the merge went on: it could take an item of a restored board off it again, put the merged item on the new board, a restored one included, counted the merge into the new board's `MergeCount`, and raised `OnItemMerged` after `OnBoardPrepared`. A `GameEnd()` from those handlers still lets the merge finish its step.
- `RemoveItemsCommand` and `PromoteItemCommand` skip the rest of their step as well when a handler of an event they raise calls `Prepare()`, and the board's `TryReplaceItem` and `AddScore` stop once a handler prepared the board again. Before, a removal went on paying the points of the items it removed into the new board and could take an item of a restored board off it again, a promotion paid its points into the new board, `TryReplaceItem` put the replacement on the new board when an `OnItemRemoved` handler prepared it, where it now puts none and returns false, and `AddScore` raised `OnScoreAdded` after `OnBoardPrepared` when an `OnScoreChanged` handler prepared the board, with the score the new board starts with less the old score as the points, which was negative whenever the new board started below the old score. A `GameEnd()` from those handlers still lets the command finish its step.
- The board's own steps stop as well once a handler of an event they raise calls `Prepare()`, a restored snapshot included. Before, a release whose `OnHoldingItemChanged`, `OnHoldingItemMoved`, or `OnItemAdded` handler prepared the board could drop the held item onto the new board, counted the drop into its `SpawnCount`, started the drop interval, and raised `OnItemDropped` after `OnBoardPrepared`; handing out the item in hand drew a second preview over the one `Prepare` drew, which made a restored board skip an item it was saved with; `TrySpawnItem` returned true and an item that had gone back to the pool, which `SpawnItemCommand.SpawnedItem` then kept; a `GameEnd()` whose handler of the emptied hand prepared the board raised `OnGameEnded` on the new board; and a `Prepare` whose own events prepared the board again went on after the inner one, clearing its board and the commands it was given, restoring over it, and reporting `OnBoardPrepared` twice. `TrySpawnItem`, `TryReplaceItem`, and `TrySpawnHoldingItem` return false in these cases. A `GameEnd()` from those handlers still lets the step finish.
- The sample's `BoardConfig.asset` no longer refers to nine audio clips the repository never contained: the BGM track, the game start, game end, and drop sounds, and the five merge sounds. The imported sample showed them as "Missing (Audio Clip)". The slots are empty now, so the sample board stays silent as before, and every reference in the sample's assets and scene resolves.
- `SetBoardConfig` and `Prepare` refuse a configuration in which one of the first eleven item entries has no sprite, and the error names the indices of those entries. Such an entry was accepted, and every item made from it was created at scale 0, invisible and with no collider to touch, without any log. `BoardConfig` logs a warning that names the same indices while the asset is edited.
- `SetBoardConfig` during a game keeps every item on the board at the same place relative to the board when the new board sprite has another size. The board fits any sprite into the same viewport, which rescales the board transform the items hang under, while the items kept their local positions, so a smaller sprite pushed them out through the walls, where they fell without end, and a larger one squeezed them toward the middle. Only the held item was placed again before. An item stacked above the top edge keeps its place above it, and a sprite of the same size leaves every item where it is.
- After `Prepare`, a restored snapshot included, the next items come from the stage of the spawn count the board stands at. `BoardItemSpawnRule` refilled its bag only at the first count of a stage or once the bag ran empty, so a board restored at a later count went on drawing from the first stage's bag, and one restored at an earlier count drew items larger than its stage holds. The rule now also refills the bag when a count falls into another stage than the bag was filled for.
- `Prepare` raises `OnScoreChanged` when it changes the score: with 0 when the board starts over, and with the snapshot's score when it restores one. It is raised once the board is prepared, right before `OnBoardPrepared`. A score label that followed the event showed the previous game's score until the first merge.
- `InputHandler` reads the input backend that Active Input Handling enables. It read only `UnityEngine.Input`, which gets no input under "Input System Package (New)", so nothing on the board could be dragged or dropped there. With the Input System package installed and its backend on, it now reads `Mouse.current` and the touches of the EnhancedTouch API, holding one `EnhancedTouchSupport.Enable()` call while it reads them and giving it back in `OnDisable`; otherwise it reads the Input Manager. The package still does not depend on the Input System: its assembly defines `MU3LIBRARY_INPUTSYSTEM_SUPPORT` and references `Unity.InputSystem` only while `com.unity.inputsystem` is installed. Under "Input System Package (New)" without the package there is nothing the handler can read, and it logs a warning once. The package README lists the supported settings.
- A drag taken away before it was released, by disabling the `InputHandler`, by a tracked touch that vanished or that the system canceled, or by a disconnected mouse, is canceled and leaves the held item in hand where the drag last put it. Disabling the handler and a vanished touch reported a release at screen position (0, 0), so the item jumped to the left edge of the board and fell there as a player drop, counted in `SpawnCount`, and a touch the system canceled or a disconnected mouse was taken as a release that dropped the item. A listener that follows a drag through `OnTouchEnded` also needs `OnTouchCanceled`, described under Added.
- A drag in progress when the application loses focus or is paused is canceled and leaves the held item in hand, as a drag taken away in another way is. The input backends let go of a held button once the focus is gone, and `InputHandler` took that for the player's release, so the held item was dropped and counted in `SpawnCount`: in a Windows player the Input Manager reported the button released in the first frame without the focus, and under the default background behavior the Input System reset the mouse when the focus left a player that ran in the background, and reset or synced it when the focus came back to one that did not, which dropped the item only then. In the Editor's Game view, under the Input System, the play mode kept reading the button held while the focus was elsewhere, so the drag ended with the next release it read after the focus came back. The handler now cancels the drag on `OnApplicationFocus(false)` and `OnApplicationPause(true)`, described under Added, and under the Input System also on a reset of the mouse it reads. The EditMode tests that change the focus through the Input System test fixture need Input System 1.20.0, the first version whose fixture can send a focus change; under the 1.19.0 that Unity 6000.3 resolves they are left out, and the package's other tests compile and run.
- A `BoardArea` added in code with `AddComponent<BoardArea>()` enables without an exception and forwards its touches. The `InputHandler` it adds left `OnTouchBegan`, `OnTouchMoved`, and `OnTouchEnded` null, since only serialized scene data filled them, so `BoardArea.OnEnable` threw `NullReferenceException` and the board received no touches.

## [base/0.26.0] - 2026-08-17

### Added
- `Mu3Library.Notifications`, a new optional assembly in the Base package: `INotificationManager`/`NotificationManager` wrap the Mobile Notifications package's unified `NotificationCenter` — initialization with an Android channel, permission requests, scheduling at a time or after a delay, cancels, badge clearing, opening the system settings, and the notification the app was opened from. `INotificationManagerEventBus.OnNotificationReceived` reports notifications that arrive while the app runs. The assembly compiles only on Android, iOS, and in the editor, and only while `com.unity.mobile.notifications` is installed (`MU3LIBRARY_NOTIFICATIONS_SUPPORT`), so a project without the package or the platforms pays nothing. The package pushes no events for its yieldable permission and query operations, so the manager follows each one in a short-lived async loop that exists only while that operation runs — nothing polls while nothing is pending, and the manager works however it is hosted, with no per-frame driver. With UniTask installed, `RequestPermissionAsync` and `GetLastRespondedNotificationAsync` await the same answers. The Template sample gained a `NotificationCore`.

## [base/0.25.0] - 2026-08-17

### Added
- `Mu3Logger` (Foundation): the pure C# layer logs through a registered sink now. The Base runtime registers a Unity console sink on startup, and the Foundation event classes report the disposed-handler and invalid-id cases they used to swallow.
- `AudioManager`: `SetMixerGroups` routes each category's sources through an `AudioMixerGroup` while the volume math stays on `AudioSource.volume`; `SaveVolumes`/`LoadVolumes` persist the four category volumes through `IPlayerPrefsLoader`; `DuckBgm` dips the music under a voice line or a heavy SFX; `FadeInSfx`/`FadeInEnvironment` accept settings and a position; and a UniTask surface (`FadeInBgmAsync`, `FadeOutBgmAsync`, `TransitionBgmAsync`, `DuckBgmAsync`) completes when the fade really ended, as canceled when a newer fade replaced it.
- `ILocalizationManager`: `GetAsset<T>(tableName, key, callback)` and `GetAssetAsync<T>(tableName, key)` load the asset an AssetTable holds for the current locale — a font or a sprite that changes with the language — through the Localization package's own AssetDatabase, the way `GetString` already wraps the StringDatabase.
- `IMVPManager`: `OpenAsync`/`CloseAsync` complete when the window finished opening or fully left the manager, so a caller awaits the animation instead of chaining window events.
- `IWebRequestManager`: PUT, PATCH, and DELETE verbs; exponential retry backoff on the callback API; `WebRequestCancellation` aborts a request in flight; and an `onDownloadProgress` callback reports download progress.
- `IPlayerPrefsLoader`: bool, enum (stored by name), and JSON object entries, with matching defaults.
- `IInputSystemManagerEventBus`: asset added/removed and rebind started/completed/canceled events.
- `GameObjectPool`: `MaxSize` destroys what a full pool cannot keep, `Prewarm` fills the pool up front, a return hook runs on every enqueue, and `Count` reports the waiting objects.
- Editor: `DIValidatorDrawer` reports registrations whose constructor or required `[Inject]` members cannot resolve, without resolving anything; `RuntimeDiagnosticsDrawer` shows the managed MVP presenters and the Addressables cache; the package updater menu covers the WatermelonGame package.
- Diagnostics: `Container.GetAllDescriptors`/`IsRegistered`/`GetActiveSingletonInstances`, `CoreBase` counterparts, `CoreRoot.RegisteredCores`, `MVPManager.GetPresenterDiagnostics`, and Addressables cache counters back the new drawers.
- Tests: edit-mode suites for the DI container, the Foundation event classes, the Observables, `GameObjectPool`, and `PlayerPrefsLoader`, registered as a testable in the BuiltIn project.
- Package-level `README.md`/`CHANGELOG.md` for the Base package.

### Changed
- `Container.CreateScope()` is internal now. A core owns exactly one scope for its whole life; a second scope could run `Initialize()` twice on a singleton and dispose an instance the container still hands out. **Breaking**: code that created scopes directly has to go through a `CoreBase`.
- `AudioController`: a finite loop waits for `AudioSource.isPlaying` to end instead of watching normalized time reach 0.97, so a one-shot SFX and a playlist track play to their last sample instead of losing their tail.
- `AudioManager`: the category volume setters clamp to 0..1; a BGM playlist drops null clips up front and refuses a list with nothing playable, which used to recurse forever.
- `AddressablesManager`: the asset cache keys carry the requested type, so one address loaded as two types holds two entries instead of releasing each other's handle; `ReleaseCachedAsset` releases everything the key produced — single, list, and keyed-dictionary entries alike; `IsKeyExist` and `GetCachedAddress` keep answering by the base key; catalog updates refresh the known-key set; and a failed dependency download reports completion instead of leaving the caller waiting forever.
- `InputSystemManager`: replacing an asset under an id disables the one that left, an asset the manager built from JSON is destroyed when it leaves, and `Dispose` tears down what the manager owns.
- `Container`: re-registering an implementation with a different lifetime is a new registration that wins, with a warning, instead of being silently dropped.
- Observables invoke every subscriber on its own, so one that throws is reported and no longer blocks the others.

### Fixed
- `CoreRoot`: a destroyed duplicate core no longer drags the surviving core of the same type out of the registry; only the registered instance may take its type out, and the duplicate still disposes its own scope.
- `SceneLoader`: the additive update passes iterate a snapshot, so a scene callback that starts or stops another scene operation no longer breaks the pass with a collection-modified exception; a failed Addressables scene load releases its handle instead of leaking it.
- `CoreBase`: `GetClass`/`RegisterClass` answer null or report instead of throwing before the container exists, and a core whose object was inactive during `Start` retries its preparation on enable.
- `Singleton`/`GenericSingleton`: a destroyed duplicate no longer clears the surviving instance's static reference, and an access during application quit no longer recreates a ghost object.
- `ResourceLoader`: concurrent async loads of one path share a single request, and re-caching the same asset no longer warns.
- `OutPanel.CanvasGroup` answers through the lazy property instead of the raw backing field, which was null before first access.
- `AudioManager`: the audio controller creation error names the missing side — the source or the clip — instead of always blaming the source.

## [urp/0.3.0] - 2026-08-17

### Added
- Package-level `README.md`/`CHANGELOG.md` for the URP package.

### Changed
- `ScreenEffectManager` implements `IDisposable`: disposing takes it off `RenderPipelineManager.beginCameraRendering` instead of staying attached forever. The effects themselves stay with their owners.

### Fixed
- `ScreenEffectManager.UnregisterEffectAll<TEffect>()` removes effects again. It compared the effect type against `PassType`, which names the render pass, so it could never match and silently removed nothing.

## [game/watermelon/0.6.0] - 2026-08-17

### Added
- `InputHandler`: the mouse drives the same drag pipeline as a touch, under a finger id reserved for it, so the game plays in the editor and on desktop without a touch screen, and a project that only ever sees touches behaves as before.
- Tests: an edit-mode suite for `BoardCommandRunner`, registered as a testable in the WatermelonGame project alongside the Base suites.

## [base/0.24.1] - 2026-08-16

### Changed
- `ScreenChangeNotifier`: The screen is followed only while the game runs. `ScreenSize` and `SafeArea` stay empty until the game reads the screen, and `OnChanged` is never raised in edit mode.
- `SafeRect`: A screen that holds no safe area to inset by leaves the rect covering its parent whole, which is what edit mode gives it now that the screen is followed only while the game runs. A screen that reported no size used to leave the rect untouched instead, so a rect created in edit mode kept the size it was created with until play mode fitted it.

### Fixed
- `SafeRect`: The rect no longer takes a new size on every frame of edit mode. `ScreenChangeNotifier` read the screen from the editor loop, where `Screen` answers with the view the editor is drawing: a game view on one tick, a scene view or an inspector on the next. Every tick therefore looked like a screen that had just changed, and the safe area was applied again from a different pair of numbers each time, while nothing on the screen had changed at all.

## [base/0.24.0] - 2026-08-16

### Added
- `ILocalizationManager`: `GetStringAsync(EntryData)` completes the async surface. It previously took only a table name and a key, while the callback API already accepted the `EntryData` the Localization data exporter generates.
- `ILocalizationManagerEventBus`: `OnLocaleChanged` reports every selected locale change, including one made outside this manager. `LocalizationManager` holds the subscription on the Localization package itself and takes it back off in `Dispose()`, so a caller no longer attaches to `LocalizationSettings` and has to remember to detach.

### Changed
- `ILocalizationManager`: `AddLocaleChangedEvent(Action<Locale>)` and `RemoveLocaleChangedEvent(Action<Locale>)` are removed. Subscribing to a locale change belongs to the event bus, so `ILocalizationManagerEventBus.OnLocaleChanged` replaces both; resolve the manager as `ILocalizationManagerEventBus` and use `+=` and `-=`.
- `ILocalizationManager`: `GetAllKeys` returns `IReadOnlyList<string>` and `GetAvailableLocales` returns `IReadOnlyList<Locale>`. `GetAvailableLocales` used to hand out the very `List<Locale>` the Localization package keeps, which a caller could sort or clear.
- `Mu3Library.Localization.Data`: `EntryData`, `LocaleData`, and `TableData` are behind `MU3LIBRARY_LOCALIZATION_SUPPORT` now, the way `Mu3Library.Addressable.Data` already was. The exporter does not gate the scripts it generates, so a project that keeps generated localization scripts while the define is off has to remove them as well.
- `LocalizationManager.InitializeAsync()`: A failed initialization no longer rethrows out of the await. It is reported the way the callback API reports it, through `IsInitialized`, `InitializeError`, and `OnInitializeResult`, and it is still logged.
- `LocalizationManager`: A project that carries no `LocalizationSettings` asset is answered with an empty string, an empty list, or the fallback locale, instead of being handed a throwaway default. Reading a `LocalizationSettings` member creates a default settings object on access and logs a warning for it, so every entry point checks `LocalizationSettings.HasSettings` first.
- `AddressablesManager.InitializeAsync()`: A failed initialization no longer rethrows out of the await. It is reported the way the callback API reports it, through `IsInitialized`, `InitializeError`, and `OnInitializeResult`, and it is still logged, so all three initialize entry points report one failure the same way. It also runs through `BeginInitialize()` instead of repeating the handle acquisition and completion sequence, which had left the callback and the async path free to drift apart.

### Fixed
- `LocalizationManager`: A custom locale code no longer throws while the default locale is resolved or a locale is looked up by English name. `LocaleIdentifier.CultureInfo` stays null for a code the culture table does not carry, which the Localization package documents as a supported case, and both lookups read it straight through.
- `LocalizationManager`: `GetSelectedLocale(Action<Locale>)` waits on the handle it checked. It read `LocalizationSettings.SelectedLocaleAsync` again to attach the completion handler, and assigning `SelectedLocale` in between releases that operation and builds a new one, so the handle that was tested for completion and the handle that carried the callback could be different ones. The property is read once now.
- `LocalizationManager`: A canceled `ChangeLocaleAsync(Locale)` no longer reports that the change which replaced it has finished. The superseded call cleared `IsLocaleChanging` from its own `finally`, after the newer call had already set it. Only the call that still owns the cancellation source settles that state now.
- `LocalizationManager`: `CurrentLocale` follows a locale change made outside the manager. Only this manager's own change methods wrote it, so setting `LocalizationSettings.SelectedLocale` directly, or letting a startup selector choose, left it reporting the previous locale.
- `LocalizationManager`: `ChangeLocaleToNative()` matches a system language whose enum name differs from its culture's English name. Comparing `SystemLanguage.ToString()` against `CultureInfo.EnglishName` could never match `ChineseSimplified` against `Chinese (Simplified)`. The identifier the Localization package builds from a `SystemLanguage` is tried first, then the same language with any region, and the English name last.
- `LocalizationManager`: `GetStringAsync(string, string)` returns an empty string for a failed table load, as it was meant to. The status check after the await was unreachable, because awaiting a failed operation throws before it is read.
- `AddressablesManager`: `InitializeAsync()` no longer awaits inside a `finally` while an exception is in flight. A caller that arrived while an initialization was already running waited there on `UniTask.WaitUntil`, which carries neither a timeout nor a cancellation token, so the wait held the exception instead of letting it surface.
- `AddressablesManager`: `Dispose()` takes its completion handler back off the initialization handle before releasing it, so a completion that lands after disposal cannot write `_isInitialized` back into a manager that has already been torn down.

## [game/watermelon/0.5.0] - 2026-08-16

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
- `BoardController`: Added the `OnScoreAdded`, `OnBoardConfigChanged`, `OnHoldingItemChanged`, `OnHoldingItemMoved`, `OnItemDropped`, `OnItemAdded`, `OnItemRemoved`, `OnItemMerged`, and `OnMergeComboChanged` events, so a project can drive its UI and effects from what the board reports instead of polling it. `OnScoreAdded` carries the points a single change paid out and skips a change the zero clamp swallowed, `OnItemRemoved` runs while the item still carries its catalog entry and its place, and `OnItemAdded` covers every item that joins the board: the player's drop, a merge, a command, and a restored snapshot alike.
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
- `IBoardCommandContext` and `BoardController.CommandContext`: Added the narrow board surface external commands use for item operations, score, merge count, sounds, queueing, and board state: `TrySpawnItem`, `TryReplaceItem`, `RemoveItem`, `ContainsItem`, `GetItemInfo`, `AddScore`, `CountMerge`, `PlayBoardSound`, `PlayItemMergeSound`, and `EnqueueCommand`, with `Area`, `Config`, `ScoreRule`, `Items`, `HoldingItem`, `ItemIndexCount`, `IsPrepared`, `IsRunning`, and `Score` to read.
- Flow commands: Added `ActionCommand`, `DelayCommand`, `WaitUntilCommand`, `SequenceCommand`, `ParallelCommand`, and `CompositeBoardCommand`, so board work can be ordered, paced, and grouped without a project writing its own scheduler.
- Board commands: Added `SpawnItemCommand`, `RemoveItemsCommand`, `PromoteItemCommand`, `ShakeBoardCommand`, and `AddScoreCommand`, which cover bonus items, clearing power-ups, a single-item promotion, a board shake, and score bonuses.
- `MergingCommand`: Changed merge execution to use `IBoardCommandContext`, so the board and external projects can use the same merge command, with the `protected virtual` hooks `GetMergedIndex`, `GetMergeScore`, `SpawnMergedItem`, `TryGetMergedPosition`, and `PlayMergeSound` for the merged index, the score, the merged item, its position, and the sound. `BoardController.CreateMergingCommand(BoardItem, BoardItem)` is the factory to override so the whole board runs a subclass, and the pair check moved to `TryCreateMergingCommand`.
- `MergingCommand`: Moved the command to `Board.Command.Item` and removed the old `Board.Command.Merge` namespace and folder.
- `BoardController`: Added `AddScore`, `CountMerge`, `Items`, `Config`, `Area`, `ItemIndexCount`, `ContainsItem`, and public `GetItemInfo`; score and merge updates now use one path, and negative score changes clamp at zero.
- `BoardItem`: Added `AddVelocity(Vector2, float)` for pushing an item without replacing its existing velocity.
- `BoardConfig`: Added an optional `SoundConfig` that carries the board sounds: one clip per `BoardSoundType`, `GameStart`, `GameEnd`, and `ItemDrop`. Every clip can be left empty and a moment without one stays silent, so a board can be configured with only the sounds a project already has.
- `BoardController`: Changed sound volume ownership to `SfxVolume` and `BgmVolume`, so a project sets them on the board instead of carrying them in `BoardSoundConfig`, where a volume setting the player changes could not reach them. Both clamp to 0–1; BGM changes apply immediately to the board-owned audio manager while an assigned manager keeps its own volume.
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
- `BoardConfig`: Added a configurable drag-only vertical tiled spawn guide line sprite. The line is one tenth of the spawn marker's width and renders below the spawn marker at board sorting order `+1` while the marker renders at `+2`.
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
- `BoardController`: Merge matching now groups active items by index before checking contacts, which skips comparing unrelated pairs and allocates no groups per frame after warmup, and board child renderers and out colliders are cached during layout rebuilds.
- `BoardArea`: Local board bounds now remain correct for camera-aligned and tilted board planes while the public world XY bounds API is retained.
- `WatermelonGame` sample: Preparation now validates and caches its dependencies, so a missing scene or configuration dependency no longer reports a prepared board, and game start is triggered only after a successful board preparation. The sample board is configured with its item-out line and spawn-marker images.
- `BoardItemInfo`: Increased the default collider diameter ratio from `0.96` to `0.98` so fruit contacts better match the configured sprite size.
- `BoardController`: The item to drop now waits at the top of the board as soon as the drop interval has passed, instead of being created on the next touch. It waits where the previous item was dropped from, and a touch only picks it up.
- `BoardArea`: Split the component into single-purpose parts under `Board/Area` — `BoardAreaBoundsCalculator` measures the board rectangle, `BoardAreaCoordinateConverter` converts positions, `BoardAreaView` draws the board and the item-out line, `BoardAreaOutColliders` keeps the items inside, and `BoardAreaInputRelay` reports the touches that belong to the board, with the rectangle itself carried by the new `BoardAreaBounds` and `CoordinateBounds` types. The component keeps its public API and only forwards to those parts.
- `BoardItemScaleRule`: Item diameters are now spread linearly over the board area width, from `1/20` for the smallest fruit to `2/5` for the largest one, instead of growing by a shrinking area multiplier. `GetBoardScale` takes the largest ratio next to the smallest one, and `GetBoardWidthDiameterRatio(int)` returns an item's diameter as a fraction of the board width.

### Removed
- Merge effects: Removed `BoardItemInfo.MergingEffect` / `MergedEffect`, the effect playback in `MergingCommand`, and the sample merge effect prefabs, materials and textures. The prefabs were saved in the legacy Unity 5 prefab format, so their `ParticleSystemRenderer` had no `serializedVersion`, was missing most of its fields, and held a null material at index 0; instantiating one crashed the Editor natively in `ParticleSystemRenderer::PrepareForRender` on the first merge.

### Fixed
- `BoardArea`: The spawn guide line now appears when a drag began during the drop cooldown and the held item becomes available afterward, and its board-relative dimensions are applied through transform scale; applied through `SpriteRenderer.size`, they made the line disappear.
- `MergingCommand`: Merge reservations are released for invalid, canceled, and failed commands without mutating a pooled item that has already been reused.
- `BoardItem`: Pooled instances now reset their presentation, collider, physics, support, and merge state before reuse.
- `InputHandler`: Touch moves and ends now stay tied to the finger that began the drag.
- `BoardArea`: `CalculateBounds(Camera, float)` and `CalculateBounds(Camera, Vector2, float)` now use the aspect ratio they are given, they always fell back to the board sprite's own ratio.
- `BoardController`: Game-end detection now ends the game when an item whose top edge is above the board line rests on the board floor or on other items. Items are always placed above the line, so an item that has not landed yet is excluded through its falling state. The side walls are never treated as a support, and the item-out line is now display only.
- `BoardItem`: A fall now ends only on the board floor or on another item. Brushing a side wall counted as landing, so items stacked against a wall ended the game too early.
- `BoardController`: Merging items stay registered on the board until their command completes and the same item can no longer be registered twice, so preparing the board again collects every item instead of leaking it or leaving stale duplicates that grew the board without bound. Renamed the misspelled `OnDestory` so commands are actually disposed.
- `MergingCommand`: A merge can no longer start or complete more than once, and never completes after the command has been disposed.

## [game/watermelon/0.1.1] - 2026-08-07

### Added
- `BoardItemInfo`: Added optional `ParticleHandler` resources for merge effects and automatic cleanup after spawned effects complete.
- Watermelon Game sample: Added static merge VFX textures and the `WatermelonMergingEffect` / `WatermelonMergedEffect` prefabs, wired through `BoardConfig`.

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
