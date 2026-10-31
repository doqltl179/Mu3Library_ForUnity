using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// A player-hosted multiplayer session: one player's machine hosts, and the others find it
    /// through a lobby and connect to it. The host decides the session state; every other player
    /// follows what the host reports.
    /// </summary>
    /// <remarks>
    /// Everything runs on the main thread. What the backend reports, and the answers to
    /// <see cref="CreateLobby"/>, <see cref="JoinLobby"/>, <see cref="SearchLobbies"/>, and
    /// <see cref="LoadAvatar"/> for an avatar the manager does not hold yet, arrive from inside
    /// <c>Update</c>. A request that changes the session raises its own events before
    /// it returns: <see cref="CreateLobby"/> and <see cref="JoinLobby"/> their first state change,
    /// <see cref="Leave"/> the change to Offline and the failed answer of a pending create or join,
    /// and the host's requests the events they cause. A handler may call back into the manager.
    /// A request that fails its own checks answers at once and changes nothing.
    /// </remarks>
    public partial interface IMultiplayerManager
    {
        public bool IsInitialized { get; }
        public SessionState State { get; }
        public bool IsHost { get; }
        public PlayerId LocalPlayerId { get; }
        public PlayerId HostId { get; }
        public LobbyId CurrentLobbyId { get; }
        public SessionRules Rules { get; }

        /// <summary>
        /// The players in the session, the host first, players whose slot the host keeps while
        /// they are away included (<see cref="Participant.IsConnected"/>). Empty outside a session.
        /// </summary>
        public IReadOnlyList<Participant> Participants { get; }

        /// <summary>The data the host started the current game with. Empty outside a game.</summary>
        public ReadOnlyMemory<byte> StartData { get; }

        /// <summary>
        /// The game-defined lobby data of the current session, as the host last reported it,
        /// without the library's own <c>mu3.</c> keys. Empty outside a session.
        /// </summary>
        public IReadOnlyDictionary<string, string> LobbyData { get; }

        /// <summary>
        /// Attaches the backend. The manager does not own the backend: dispose it after the manager.
        /// </summary>
        public MultiplayerResult Initialize(IMultiplayerBackend backend, MultiplayerConfig config);

        public bool TryGetParticipant(PlayerId player, out Participant participant);

        #region Lobby
        /// <summary>
        /// Hosts a new session. On success the state is <see cref="SessionState.Lobby"/> and this
        /// player is the host.
        /// </summary>
        public void CreateLobby(LobbyCreateOptions options, Action<MultiplayerResult> callback = null);

        /// <summary>
        /// Finds public lobbies of this game and protocol version that have a free slot.
        /// </summary>
        public void SearchLobbies(LobbySearchQuery query, Action<LobbySearchResult> callback);

        /// <summary>
        /// Joins a session: enters its lobby, connects to the host, and waits for the host to
        /// accept. On success the state is <see cref="SessionState.Lobby"/>, or
        /// <see cref="SessionState.InGame"/> for a late join.
        /// </summary>
        public void JoinLobby(LobbyId lobby, Action<MultiplayerResult> callback = null);

        /// <summary>
        /// Leaves the session from any state. When the host leaves, the session ends for everyone.
        /// A player who leaves never keeps a slot. One that leaves while
        /// <see cref="SessionState.Reconnecting"/> frees its slot at once only when its attempt
        /// already reached the host; otherwise the host frees it when the reservation runs out.
        /// </summary>
        public void Leave();
        #endregion

        #region Players
        /// <summary>
        /// In the lobby only. For a client, success means the request went to the host; the flag
        /// changes when the host reports it back through
        /// <see cref="IMultiplayerManagerEventBus.OnParticipantChanged"/>.
        /// </summary>
        public MultiplayerResult SetReady(bool isReady);

        /// <summary>
        /// Host only, in the lobby. The capacity cannot drop below the current player count,
        /// which counts the players whose slot the host keeps while they are away.
        /// </summary>
        public MultiplayerResult UpdateRules(SessionRules rules);

        /// <summary>
        /// Host only. The player cannot rejoin this session. A player whose slot the host keeps
        /// while they are away can be kicked too, which frees the slot.
        /// </summary>
        public MultiplayerResult Kick(PlayerId player);

        /// <summary>
        /// How this player's connection to <paramref name="player"/> performs, as the backend
        /// measures it: the ping, the share of packets lost on their way here, and the bytes each
        /// way per second. The host has a connection to every client, and a client has one to the
        /// host only, since everything it sends travels through the host; so a client asking about
        /// another client, a player asking about themselves, and a player outside the session
        /// answer false. Reading allocates nothing on the local backend, and on Steam only when
        /// the backend asks Steam again, at most twice a second per connection, so a game can show
        /// it every frame.
        /// </summary>
        public bool TryGetConnectionQuality(PlayerId player, out ConnectionQuality quality);
        #endregion

        #region Data
        /// <summary>
        /// Host only, in the lobby or in a game; a client answers <see cref="MultiplayerReason.NotHost"/>.
        /// Sets one value of the lobby data, which lobby search returns and filters on, and reports
        /// it to every player through <see cref="IMultiplayerManagerEventBus.OnLobbyDataChanged"/>.
        /// An empty key, one longer than <see cref="MultiplayerLimits.MaxDataKeyLength"/>, one that
        /// starts with the library's <c>mu3.</c> in any case (Steam does not tell keys apart by
        /// case), a null value, or a key or value that holds a NUL character or an unpaired UTF-16
        /// surrogate answers <see cref="MultiplayerReason.InvalidArgument"/>, and lobby data that would grow past
        /// <see cref="MultiplayerLimits.MaxLobbyDataSize"/> answers
        /// <see cref="MultiplayerReason.MessageTooLarge"/>. Setting the value a key already has
        /// changes nothing.
        /// </summary>
        public MultiplayerResult SetLobbyData(string key, string value);

        /// <summary>
        /// In the lobby or in a game. Sets one value of this player's own participant data, such as
        /// the character they chose, which every player reads from <see cref="Participant.Data"/>;
        /// no player can change another's. Keys and values follow the rules of
        /// <see cref="SetLobbyData"/>, and one player's data stays within
        /// <see cref="MultiplayerLimits.MaxParticipantDataSize"/>. For a client, success means the
        /// request went to the host, and the value changes when the host reports it back through
        /// <see cref="IMultiplayerManagerEventBus.OnParticipantDataChanged"/>. A client checks each
        /// request against its own earlier ones, as the host will have applied them, so the host
        /// takes every request that succeeds here even while earlier ones are still on their way.
        /// The data lasts until the player leaves the session, a time away while the host keeps
        /// their slot included. While this player is <see cref="SessionState.Reconnecting"/> it
        /// answers <see cref="MultiplayerReason.ConnectionLost"/>, and a request still on its way
        /// when the connection dropped is lost: the returning player takes the data the host holds.
        /// </summary>
        public MultiplayerResult SetParticipantData(string key, string value);
        #endregion

        #region Chat
        /// <summary>
        /// In the lobby only; while this player is <see cref="SessionState.Reconnecting"/> it
        /// answers <see cref="MultiplayerReason.ConnectionLost"/>, as sending does. Sends a text
        /// message to every player of the session, this player included, through
        /// <see cref="IMultiplayerManagerEventBus.OnChatReceived"/>. Null or empty text answers
        /// <see cref="MultiplayerReason.InvalidArgument"/>, and text longer than
        /// <see cref="MultiplayerLimits.MaxChatMessageSize"/> in UTF-8 bytes answers
        /// <see cref="MultiplayerReason.MessageTooLarge"/> and reaches nobody. That limit stands
        /// apart from <see cref="MultiplayerConfig.MaxMessageSize"/>, which caps game messages only.
        /// The host's own message is handled before the call returns, and a client's, its own copy
        /// included, once the host passed it on, so every player hears the messages in the same
        /// order. The host drops a client's message that arrives after the game started.
        /// </summary>
        public MultiplayerResult SendChat(string text);
        #endregion

        #region Game
        /// <summary>
        /// Host only, in the lobby. Starts the game for everyone with the given start data, such
        /// as a map name or a random seed, once the rules' start conditions are met. Start data
        /// larger than <see cref="MultiplayerLimits.MaxStartDataSize"/> answers
        /// <see cref="MultiplayerReason.MessageTooLarge"/>. A player whose slot the host keeps
        /// while they are away counts as a player who is not ready.
        /// </summary>
        public StartGameResult StartGame(ReadOnlySpan<byte> startData = default);

        /// <summary>
        /// Host only, in a game. Returns everyone to the lobby and clears every ready flag.
        /// </summary>
        public MultiplayerResult EndGame();

        /// <summary>
        /// In a game. Reports that this player is ready to play it, for example once its scene
        /// loaded, which every player reads from <see cref="Participant.IsLoaded"/> and hears
        /// through <see cref="IMultiplayerManagerEventBus.OnParticipantChanged"/>. From the start
        /// of a game, the host waits for every player it started with, the ones whose slot it
        /// keeps while they are away included, since they may come back and report, and hears
        /// <see cref="IMultiplayerManagerEventBus.OnAllPlayersLoaded"/> once all of them reported, or
        /// <see cref="IMultiplayerManagerEventBus.OnLoadTimedOut"/> when
        /// <see cref="SessionRules.LoadTimeoutSeconds"/> passes first. A player who leaves, is
        /// kicked, or does not come back to their slot in time is no longer waited for; a player
        /// who joins the running game is not waited for, but can report all the same; and the
        /// return to the lobby ends the wait without either event and clears every flag. A player
        /// who reported before its connection dropped stays loaded when it comes back to the same
        /// game, and a report still on its way then goes again by itself; one that joins again
        /// from another start of the game loads again, so its flag clears, and the host waits for
        /// it again when the game started with it. For a client, success means the report went to
        /// the host. Reporting again changes nothing.
        /// Outside a game it answers <see cref="MultiplayerReason.InvalidState"/>, and while this
        /// player is <see cref="SessionState.Reconnecting"/>,
        /// <see cref="MultiplayerReason.ConnectionLost"/>.
        /// </summary>
        public MultiplayerResult ReportLoaded();
        #endregion

        #region Platform
        /// <summary>
        /// Loads a player's avatar as a texture of the given size: this player's own, another
        /// player's of the session, or any other account the backend can tell about, such as the
        /// host of a lobby that search found. It answers at once when the manager already holds
        /// that avatar or the request fails its own checks, and otherwise from inside <c>Update</c>
        /// once the backend has the image, which on Steam can take a moment for an avatar it still
        /// downloads. A failure answers a null texture and the reason:
        /// <see cref="MultiplayerReason.NotSupported"/> on a backend without avatars, such as the
        /// local backend, <see cref="MultiplayerReason.InvalidArgument"/> for an invalid player or
        /// size, <see cref="MultiplayerReason.BackendError"/> at once when the backend is no
        /// longer available, for example once it is disposed, and
        /// <see cref="MultiplayerReason.Timeout"/> when Steam has no image within 10 seconds.
        /// Several requests for the same avatar while it loads share one answer, and a request that
        /// failed is tried again the next time.
        /// <para>
        /// The manager owns every texture it hands out: never destroy or change one. Asking again
        /// for the same player and size answers the same texture. An avatar of another player
        /// that arrived while that player was in the session lasts until the session ends, even
        /// when the player left earlier: the manager asks Unity to destroy it before
        /// <see cref="IMultiplayerManagerEventBus.OnStateChanged"/> reports the change to Offline,
        /// and in play mode Unity destroys it at the end of that frame, in edit mode at once. An
        /// avatar that arrives after its player left, and any other avatar, this player's own
        /// included, lasts until the manager is disposed. A game that still shows an avatar after
        /// that asks for it again. When one of several callbacks for the same avatar ends the
        /// session, the manager asks again for the ones after it, so none gets a texture that is
        /// being destroyed.
        /// </para>
        /// </summary>
        public void LoadAvatar(PlayerId player, AvatarSize size, Action<AvatarResult> callback);

        /// <summary>
        /// In a session, from <see cref="CreateLobby"/> or <see cref="JoinLobby"/> on; outside one
        /// it answers <see cref="MultiplayerReason.InvalidState"/>. Sets this player's status text
        /// in the platform's presence, and null or empty removes it. Where friends see it is the
        /// platform's to decide: Steam's friends list shows it only through a
        /// <c>steam_display</c> localization token that the game defines for its app ID and names
        /// in <c>SteamBackendOptions.PresenceDisplayToken</c>. Without a call, the manager still
        /// tells the platform which session this player is in and how many players it has, the
        /// ones whose slot the host keeps while they are away included, so the platform can show
        /// the players of one session as one group, which Steam does through
        /// <c>steam_player_group</c>: from the lobby or the game on, for a late joiner too, and
        /// through <see cref="SessionState.Reconnecting"/>. When the session ends
        /// for any reason, or the manager is disposed, the manager removes the status and the
        /// group before <see cref="IMultiplayerManagerEventBus.OnStateChanged"/> reports the change
        /// to Offline, so every session starts without a status. Text that holds a NUL character
        /// or an unpaired UTF-16 surrogate answers <see cref="MultiplayerReason.InvalidArgument"/>,
        /// text longer than <see cref="MultiplayerLimits.MaxPresenceStatusSize"/> UTF-8 bytes
        /// <see cref="MultiplayerReason.MessageTooLarge"/>, and a platform that refuses it
        /// <see cref="MultiplayerReason.BackendError"/>. The local backend shows nothing and
        /// succeeds. What Steam shows of it is in the remarks of <c>SteamBackend.SetPresence</c>.
        /// </summary>
        public MultiplayerResult SetPresenceStatus(string status);
        #endregion
    }
}
