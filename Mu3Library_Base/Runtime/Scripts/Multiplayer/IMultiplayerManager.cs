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
        /// without the library's own <c>mu3.</c> keys. Empty outside a session. It finds a key in
        /// any case, as Steam does (see <see cref="SetLobbyData"/>).
        /// </summary>
        public IReadOnlyDictionary<string, string> LobbyData { get; }

        /// <summary>
        /// Attaches the backend. The manager does not own the backend: dispose it after the manager.
        /// </summary>
        /// <remarks>
        /// When the backend stops being available during a session
        /// (<see cref="IMultiplayerBackend.IsAvailable"/>), the manager ends the session on its
        /// next <c>Update</c>, raising the change to <see cref="SessionState.Offline"/> and
        /// answering a pending create or join, with
        /// <see cref="MultiplayerReason.SteamNotRunning"/> for a Steam backend that can no longer
        /// use Steam, because the game shut Steam down or a Steam call failed in its native
        /// library, and <see cref="MultiplayerReason.BackendError"/> for any other backend, such
        /// as one disposed before the manager. Such a backend reports nothing more, so the session
        /// would otherwise stay as it is with no event. A client whose connection to the host, or
        /// whose reliable send to it, fails on such a backend before then ends its session at
        /// once with the same reason, rather than with <see cref="MultiplayerReason.ConnectionFailed"/>
        /// or <see cref="MultiplayerReason.ConnectionLost"/>, and does not try to come back to its
        /// slot; the request that made the send answers the same reason too
        /// (<see cref="IMultiplayerMessenger.Send"/>, <see cref="SendChat"/>, <see cref="SetReady"/>,
        /// <see cref="ReportLoaded"/>, <see cref="SetParticipantData"/>, and
        /// <see cref="DeleteParticipantData"/>), and so does a join whose other steps fail there
        /// (<see cref="JoinLobby"/>). A host whose sends fail on it keeps its players until that
        /// update, which ends the session once, rather than dropping each of them with
        /// <see cref="MultiplayerReason.ConnectionLost"/>; its message to one player that cannot
        /// leave answers the same reason.
        /// </remarks>
        public MultiplayerResult Initialize(IMultiplayerBackend backend, MultiplayerConfig config);

        public bool TryGetParticipant(PlayerId player, out Participant participant);

        #region Lobby
        /// <summary>
        /// Hosts a new session. On success the state is <see cref="SessionState.Lobby"/> and this
        /// player is the host.
        /// </summary>
        /// <remarks>
        /// The host starts taking connections before it creates the lobby. When it cannot, the
        /// request answers at once and changes nothing:
        /// <see cref="MultiplayerReason.SteamNotRunning"/> on a Steam backend that can no longer
        /// use Steam, as its lobby requests answer then, and
        /// <see cref="MultiplayerReason.ConnectionFailed"/> otherwise.
        /// </remarks>
        public void CreateLobby(LobbyCreateOptions options, Action<MultiplayerResult> callback = null);

        /// <summary>
        /// Finds public lobbies of this game and protocol version that have a free slot. Only a
        /// lobby whose reserved lobby data key <c>mu3.closed</c> reads <c>0</c> is found, as every
        /// lobby a manager creates does outside a game that refuses late joiners, so a lobby made
        /// directly through a backend without that key is not found either. A session whose game
        /// refuses late joiners stays out, also while its host opens the lobby for a player coming
        /// back to its slot, though the lobby then takes joins, Steam's own search lists it, and
        /// Steam's friends list may offer to join it (FR-19, FR-20).
        /// </summary>
        /// <remarks>
        /// While the backend is no longer available, the search answers at once, and a search
        /// still waiting for the backend when it stops being available answers on the next
        /// request or the next <c>Update</c> at the latest, once, so no callback stays held until
        /// the manager is disposed: with <see cref="MultiplayerReason.SteamNotRunning"/> on a
        /// Steam backend that can no longer use Steam, as its own search answers then, and with
        /// <see cref="MultiplayerReason.BackendError"/> for any other backend, such as one
        /// disposed before the manager, which answers no search it still holds.
        /// </remarks>
        public void SearchLobbies(LobbySearchQuery query, Action<LobbySearchResult> callback);

        /// <summary>
        /// Joins a session: enters its lobby, connects to the host, and waits for the host to
        /// accept. On success the state is <see cref="SessionState.Lobby"/>, or
        /// <see cref="SessionState.InGame"/> for a late join.
        /// </summary>
        /// <remarks>
        /// A lobby whose game refuses late joiners refuses the entry with
        /// <see cref="MultiplayerReason.GameInProgress"/>. This player then connects to the host
        /// that the lobby data names, since the host may keep a slot for this account, as after a
        /// crash and a new start of the game within the reservation time (FR-21): the host takes
        /// the account back into its slot, the join succeeds with the state the host reports, and
        /// this player enters the lobby, which the host opens for it, as a player back from a
        /// dropped connection does (<see cref="IMultiplayerManagerEventBus.OnLobbyRejoinFailed"/>
        /// when it cannot). The host refuses any other account with the same reason. A game that
        /// saved <see cref="CurrentLobbyId"/> before it went down joins that lobby again this way.
        /// Steam refuses a player without an invite to an invite-only or friends-only lobby with
        /// the same reason while its game runs, also when the game takes late joiners; that
        /// refusal stands, as the lobby data tells the two apart.
        /// Until the host notices that the old connection broke, the host refuses the new start
        /// as <see cref="MultiplayerReason.DuplicateAccount"/>.
        /// <para>
        /// Once the lobby answers, this player reads the lobby's data and owner, which can refuse
        /// the join with <see cref="MultiplayerReason.VersionMismatch"/> or
        /// <see cref="MultiplayerReason.LobbyNotFound"/>, then connects to the host. When the
        /// backend stops being available during any of these steps, or while this player tries
        /// the host past a lobby that refused it with <see cref="MultiplayerReason.GameInProgress"/>,
        /// the join ends with the reason the session would end with (see <see cref="Initialize"/>):
        /// <see cref="MultiplayerReason.SteamNotRunning"/> on a Steam backend that can no longer use
        /// Steam, as a Steam call that failed there leaves no lobby data to read, and
        /// <see cref="MultiplayerReason.BackendError"/> on any other backend, rather than with
        /// <see cref="MultiplayerReason.VersionMismatch"/>, <see cref="MultiplayerReason.LobbyNotFound"/>,
        /// <see cref="MultiplayerReason.GameInProgress"/>, or <see cref="MultiplayerReason.ConnectionFailed"/>.
        /// </para>
        /// </remarks>
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
        /// <see cref="IMultiplayerManagerEventBus.OnParticipantChanged"/>. A client whose request
        /// cannot leave answers <see cref="MultiplayerReason.ConnectionLost"/>, or on a backend
        /// that is no longer available the reason its session ends with at once (see
        /// <see cref="Initialize"/>).
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

        /// <summary>
        /// The round trip between the host and <paramref name="player"/>, in milliseconds, as the
        /// host measures it, which every player reads for every player of the session, so a game
        /// can show everyone's ping on a scoreboard. It stands apart from
        /// <see cref="TryGetConnectionQuality"/>, this player's own measure of its own connections:
        /// a client that reads itself here gets the host's measure of its connection, and reads
        /// another client here although it has no connection to it. The host's own entry is 0. The
        /// host answers from its backend's current measure, and every
        /// <see cref="MultiplayerLimits.PingReportIntervalSeconds"/> it reports the measures to the
        /// clients in an unreliable packet whose size that constant states, so a client's value is
        /// the host's from at most that long ago, plus the time the report travelled, and a report
        /// the network loses leaves the earlier values until the next one. False for a player outside
        /// the session or one whose slot the host keeps while they are away; on a client also before
        /// the first report that names the player, such as right after it joined, and while this
        /// player is <see cref="SessionState.Reconnecting"/>. A client's read allocates nothing, and
        /// the host's reads as <see cref="TryGetConnectionQuality"/> does, so a game can show it
        /// every frame. It works the same on every backend, since the host reads the measure through
        /// <see cref="IMultiplayerBackend.TryGetConnectionQuality"/>.
        /// </summary>
        public bool TryGetParticipantPing(PlayerId player, out int pingMilliseconds);
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
        /// <para>
        /// Steam does not tell lobby data keys apart by case, and every backend follows it. A key
        /// that differs from one the lobby data already holds only by case, such as <c>map</c>
        /// beside <c>Map</c>, answers <see cref="MultiplayerReason.InvalidArgument"/>: Steam would
        /// keep one value for both, so the lobby that search reads would hold other data than
        /// <see cref="LobbyData"/> on every player and than the local backends. The library refuses
        /// the second key rather than fold it into the first, so the game learns of the clash and
        /// every copy holds the same keys. Reading finds a key in any case: <see cref="LobbyData"/>,
        /// <see cref="LobbyInfo.Data"/> of a search result, and lobby search filters, on every
        /// backend. A key keeps the spelling it was set with, except that Steam's search may hand
        /// it back in another case, so a game looks keys up rather than compare them. To change
        /// the case of a key, delete it with <see cref="DeleteLobbyData"/> first.
        /// </para>
        /// <para>
        /// An empty value deletes the key, as <see cref="DeleteLobbyData"/> does: the key is found
        /// in any case, every player hears of it with a null value, it no longer counts toward
        /// <see cref="MultiplayerLimits.MaxLobbyDataSize"/>, and search neither returns it nor
        /// finds the lobby by it; a key the lobby data does not hold changes nothing. A lobby
        /// created with an empty value in <see cref="LobbyCreateOptions.Data"/> starts without
        /// that key. Steam would otherwise keep or drop the key by whether its lobby held it: on
        /// AppID 480 on 2026-11-04 a Steam lobby kept a key it held as a key with an empty value,
        /// which search returned and a filter for the empty value matched, but wrote no key it
        /// lacked, so <see cref="LobbyData"/>, search, and the local backends would have held
        /// different keys.
        /// </para>
        /// </summary>
        public MultiplayerResult SetLobbyData(string key, string value);

        /// <summary>
        /// Host only, in the lobby or in a game; a client answers <see cref="MultiplayerReason.NotHost"/>.
        /// Deletes one key of the lobby data, from the lobby that search reads too, so search no
        /// longer returns it or finds the lobby by it, and reports it to every player through
        /// <see cref="IMultiplayerManagerEventBus.OnLobbyDataChanged"/> with a null value. The key
        /// and its value no longer count toward <see cref="MultiplayerLimits.MaxLobbyDataSize"/>,
        /// so a long session that keeps changing its keys deletes the old ones to make room. The
        /// key is found in any case, as reading and Steam find it, and the event names it as the
        /// lobby data spells it: deleting <c>map</c> deletes <c>Map</c>. A null or empty key, one longer
        /// than <see cref="MultiplayerLimits.MaxDataKeyLength"/>, one that holds a NUL character or
        /// an unpaired UTF-16 surrogate, and the library's own <c>mu3.</c> keys in any case answer
        /// <see cref="MultiplayerReason.InvalidArgument"/>, and deleting a key the lobby data does
        /// not hold changes nothing.
        /// <para>
        /// Steam's search takes a moment to follow a deletion, and its filters follow later than
        /// the data it returns: on AppID 480 on 2026-11-01, over eight deletions, the lobby a
        /// search returned lacked the key within about 0.5 seconds, while a filter on the deleted
        /// value went on finding the lobby for up to about 0.8 seconds and stopped within about
        /// 1.4 seconds. A game that searches right after a deletion may still find the lobby by
        /// the deleted value. The local backends filter on the data their search returns, so
        /// both follow a deletion together.
        /// </para>
        /// </summary>
        public MultiplayerResult DeleteLobbyData(string key);

        /// <summary>
        /// In the lobby or in a game. Sets one value of this player's own participant data, such as
        /// the character they chose, which every player reads from <see cref="Participant.Data"/>;
        /// no player can change another's. Keys and values follow the rules of
        /// <see cref="SetLobbyData"/>, except that keys which differ only by case are two keys
        /// here, since participant data travels over the host's connections and never reaches
        /// Steam's lobby, and one player's data stays within
        /// <see cref="MultiplayerLimits.MaxParticipantDataSize"/>. For a client, success means the
        /// request went to the host, and the value changes when the host reports it back through
        /// <see cref="IMultiplayerManagerEventBus.OnParticipantDataChanged"/>. A client checks each
        /// request against its own earlier ones, as the host will have applied them, so the host
        /// takes every request that succeeds here even while earlier ones are still on their way.
        /// The data lasts until the player leaves the session, a time away while the host keeps
        /// their slot included. While this player is <see cref="SessionState.Reconnecting"/> it
        /// answers <see cref="MultiplayerReason.ConnectionLost"/>, and a request still on its way
        /// when the connection dropped is lost: the returning player takes the data the host holds.
        /// A client's request that cannot leave answers <see cref="MultiplayerReason.ConnectionLost"/>,
        /// or on a backend that is no longer available the reason its session ends with at once
        /// (see <see cref="Initialize"/>).
        /// </summary>
        public MultiplayerResult SetParticipantData(string key, string value);

        /// <summary>
        /// In the lobby or in a game. Deletes one key of this player's own participant data, which
        /// every player hears through <see cref="IMultiplayerManagerEventBus.OnParticipantDataChanged"/>
        /// with the player as they are without it, and the key and its value no longer count toward
        /// <see cref="MultiplayerLimits.MaxParticipantDataSize"/>. No player can delete another's.
        /// An invalid key answers <see cref="MultiplayerReason.InvalidArgument"/>, as for
        /// <see cref="SetParticipantData"/>, the library's own <c>mu3.</c> keys included, and
        /// deleting a key the data does not hold changes nothing. For a client, success means the
        /// request went to the host, as for <see cref="SetParticipantData"/>. A client's later
        /// <see cref="SetParticipantData"/> calls count the room the deletion frees at once, before
        /// the host reports it back, and the rate limits count the deletion as a participant data
        /// request. While this player is <see cref="SessionState.Reconnecting"/> it answers
        /// <see cref="MultiplayerReason.ConnectionLost"/>, and a client's request that cannot leave
        /// answers as for <see cref="SetParticipantData"/>.
        /// </summary>
        public MultiplayerResult DeleteParticipantData(string key);
        #endregion

        #region Chat
        /// <summary>
        /// In the lobby only; while this player is <see cref="SessionState.Reconnecting"/> it
        /// answers <see cref="MultiplayerReason.ConnectionLost"/>, as sending does. Sends a text
        /// message to every player of the session, this player included, through
        /// <see cref="IMultiplayerManagerEventBus.OnChatReceived"/>. Null or empty text answers
        /// <see cref="MultiplayerReason.InvalidArgument"/>, and so does text with a UTF-16
        /// surrogate without its pair, as a text field that cuts an emoji in half leaves: UTF-8
        /// would carry it as a replacement character, so the other players would hear other text
        /// than this player's own handlers. The call refuses such text rather than mending it, so
        /// every player hears the same text and the game learns that its text would not arrive
        /// as written. Text longer than <see cref="MultiplayerLimits.MaxChatMessageSize"/> in UTF-8 bytes answers
        /// <see cref="MultiplayerReason.MessageTooLarge"/> and reaches nobody. That limit stands
        /// apart from <see cref="MultiplayerConfig.MaxMessageSize"/>, which caps game messages only.
        /// The host's own message is handled before the call returns, and a client's, its own copy
        /// included, once the host passed it on, so every player hears the messages in the same
        /// order. The host drops a client's message that arrives after the game started. A
        /// client's message that cannot leave answers <see cref="MultiplayerReason.ConnectionLost"/>,
        /// or on a backend that is no longer available the reason its session ends with at once
        /// (see <see cref="Initialize"/>).
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
        /// <see cref="MultiplayerReason.ConnectionLost"/>. A client's report that cannot leave
        /// answers <see cref="MultiplayerReason.ConnectionLost"/>, or on a backend that is no
        /// longer available the reason its session ends with at once (see <see cref="Initialize"/>).
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
        /// size, <see cref="MultiplayerReason.Timeout"/> when Steam has no image within 10
        /// seconds, and at once when the backend is no longer available
        /// <see cref="MultiplayerReason.SteamNotRunning"/> on a Steam backend that can no longer
        /// use Steam, because the game shut Steam down or a Steam call failed in its native
        /// library, as its lobby requests answer then, and
        /// <see cref="MultiplayerReason.BackendError"/> for any other backend, such as one
        /// disposed before the manager. The requests that still wait when the backend stops being
        /// available answer the same reason, from the next request or the next <c>Update</c> at
        /// the latest, so no callback stays held until the manager is disposed; the Steam backend
        /// answers <see cref="MultiplayerReason.SteamNotRunning"/> as well for a request it still
        /// holds when Steam goes away, so a game that asks the player to start Steam on that
        /// reason sees it for an avatar too.
        /// Several requests for the same avatar while it loads share one answer, and a request that
        /// failed is tried again the next time. When the backend tells that a player's avatar
        /// changed, as Steam does when the player picks a new picture, the manager reads again
        /// every size it holds of that player and draws the new image into the same texture, so a
        /// texture the game shows changes with it, then raises
        /// <see cref="IMultiplayerManagerEventBus.OnAvatarChanged"/>. The same notice corrects the
        /// default image Steam hands out at first for an account it did not know, until it has
        /// downloaded the account's own picture. A read after a change that fails is tried again
        /// up to 3 times, 1 second after each failure, while the backend is available, so one
        /// failure does not leave that default image behind; once the last try fails too, the
        /// manager logs one warning, and the texture keeps the image it shows until the backend
        /// tells of another change. Of reads that overlap, as for two notices in a row, the texture
        /// takes the image of the latest one that brought an image, so a later read that fails does
        /// not throw away the new image an earlier one brought, and an earlier read that answers
        /// after a later one changes nothing.
        /// <para>
        /// The manager owns every texture it hands out: never destroy or change one. Asking again
        /// for the same player and size answers the same texture. An avatar of another player
        /// that arrived while that player was in the session lasts until the session ends, even
        /// when the player left earlier: the manager asks Unity to destroy it before
        /// <see cref="IMultiplayerManagerEventBus.OnStateChanged"/> reports the change to Offline,
        /// and in play mode Unity destroys it at the end of that frame, in edit mode at once. An
        /// avatar that arrives after its player left, and any other avatar, this player's own
        /// included, lasts until the manager is disposed. Before then the game releases what it
        /// no longer shows with <see cref="ReleaseAvatar"/> and <see cref="ReleaseAvatars"/>, which
        /// destroy the textures the same way. A game that still shows an avatar after that asks
        /// for it again, and gets a new texture. When one of several callbacks for the same avatar
        /// ends the session or releases that avatar, the manager asks again for the ones after it,
        /// so none gets a texture that is being destroyed.
        /// </para>
        /// </summary>
        public void LoadAvatar(PlayerId player, AvatarSize size, Action<AvatarResult> callback);

        /// <summary>
        /// Destroys every texture the manager holds of this player's avatar, in every size,
        /// whoever the player is: this player, a player of the session, or any other account, such
        /// as the host of a lobby that search found. A texture the game still shows or keeps is
        /// destroyed with it, in play mode at the end of the frame and in edit mode at once, so the
        /// game stops using it; <see cref="LoadAvatar"/> for that player then reads the avatar from
        /// the backend again and answers a new texture. A request still waiting for the backend is
        /// left alone: it answers when the backend does, and the manager holds that texture as
        /// usual. A player whose avatar the manager does not hold, an invalid player, and a manager
        /// that is not initialized or is disposed change nothing.
        /// </summary>
        /// <remarks>
        /// The manager has no limit of its own on the textures it holds, such as a cap that
        /// destroys the least recently used, since it cannot tell which texture the game still
        /// shows, and a cap would destroy one under the game's feet. The game decides instead: a
        /// large avatar is a readable 184-pixel-square texture, about 135 KB on the GPU and as much
        /// in memory.
        /// </remarks>
        public void ReleaseAvatar(PlayerId player);

        /// <summary>
        /// Destroys, as <see cref="ReleaseAvatar"/> does, every avatar texture the manager holds
        /// except this player's own and those of the players in the session now, players whose
        /// slot the host keeps while they are away included: the avatars of lobby hosts that search
        /// found, of players who left, and of any other account. A game that shows a list of
        /// lobbies calls it when the list closes, also after it joined one of them, since the
        /// players of that session keep their avatars. This player's own, at most three textures
        /// that a game usually shows everywhere, goes only with <see cref="ReleaseAvatar"/>.
        /// </summary>
        public void ReleaseAvatars();

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
        /// to Offline, so every session starts without a status. When the platform refuses that
        /// removal, the manager asks again on the next <c>Update</c> and then once a second, while
        /// the backend is available, until the platform takes it or a new session shows something
        /// else; it logs the refusal once, not every time it asks, and not at all for a backend
        /// that is no longer available, whose session ends with a reason of its own (see
        /// <see cref="Initialize"/>). Text that holds a NUL character
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
