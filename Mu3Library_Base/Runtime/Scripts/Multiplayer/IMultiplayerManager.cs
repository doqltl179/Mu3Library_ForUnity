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
    /// <see cref="CreateLobby"/>, <see cref="JoinLobby"/>, and <see cref="SearchLobbies"/>, arrive
    /// from inside <c>Update</c>. A request that changes the session raises its own events before
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

        /// <summary>The players in the session, the host first. Empty outside a session.</summary>
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
        /// Host only, in the lobby. The capacity cannot drop below the current player count.
        /// </summary>
        public MultiplayerResult UpdateRules(SessionRules rules);

        /// <summary>
        /// Host only. The player cannot rejoin this session.
        /// </summary>
        public MultiplayerResult Kick(PlayerId player);
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
        /// The data lasts until the player leaves the session.
        /// </summary>
        public MultiplayerResult SetParticipantData(string key, string value);
        #endregion

        #region Chat
        /// <summary>
        /// In the lobby only. Sends a text message to every player of the session, this player
        /// included, through <see cref="IMultiplayerManagerEventBus.OnChatReceived"/>. Null or
        /// empty text answers <see cref="MultiplayerReason.InvalidArgument"/>, and text longer than
        /// <see cref="MultiplayerLimits.MaxChatMessageSize"/> in UTF-8 bytes answers
        /// <see cref="MultiplayerReason.MessageTooLarge"/> and reaches nobody. The host's own
        /// message is handled before the call returns, and a client's, its own copy included, once
        /// the host passed it on, so every player hears the messages in the same order. The host
        /// drops a client's message that arrives after the game started.
        /// </summary>
        public MultiplayerResult SendChat(string text);
        #endregion

        #region Game
        /// <summary>
        /// Host only, in the lobby. Starts the game for everyone with the given start data, such
        /// as a map name or a random seed, once the rules' start conditions are met. Start data
        /// larger than <see cref="MultiplayerLimits.MaxStartDataSize"/> answers
        /// <see cref="MultiplayerReason.MessageTooLarge"/>.
        /// </summary>
        public StartGameResult StartGame(ReadOnlySpan<byte> startData = default);

        /// <summary>
        /// Host only, in a game. Returns everyone to the lobby and clears every ready flag.
        /// </summary>
        public MultiplayerResult EndGame();
        #endregion
    }
}
