using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// Where lobbies and connections actually live. The Steam backend and the local backend
    /// implement it; the session logic in <see cref="MultiplayerManager"/> runs the same on both.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every callback, both the listener's and the ones a lobby request takes, arrives from inside
    /// <see cref="Update"/>. A backend never calls back from inside the request itself.
    /// </para>
    /// <para>
    /// A backend also promises that every lobby request and every avatar request answers exactly
    /// once, even when several searches overlap or the platform refuses the request, since the
    /// manager has no timeout while it creates a lobby or waits for an avatar. It reports a connection that stops answering as
    /// <see cref="BackendDisconnectKind.ConnectionLost"/> within its own timeout (10 seconds by
    /// default), which is how a client notices that its host crashed. And it keeps a join request
    /// that arrives before <see cref="SetListener"/>, such as the lobby a launch from an invite
    /// names, until a listener can receive it.
    /// </para>
    /// <para>
    /// These promises hold while <see cref="IsAvailable"/> is true. Once it turns false, a backend
    /// need not report its connections closed, and one that is disposed answers nothing more: the
    /// manager ends its session on its next <see cref="MultiplayerManager.Update"/> instead, which
    /// also answers a lobby creation or entry still pending (see <see cref="IsAvailable"/>), and
    /// answers the lobby searches and avatar requests still waiting itself. So a
    /// backend turns <see cref="IsAvailable"/> false only when it can no longer carry the
    /// session, never for an outage it may recover from, which it reports through its
    /// connections as above.
    /// </para>
    /// </remarks>
    public interface IMultiplayerBackend : IDisposable
    {
        public string Name { get; }

        /// <summary>
        /// False once the backend cannot serve requests, for example after it is disposed, and
        /// never false for an outage the backend may recover from. Once it turns false during a
        /// session, the manager ends that session on its next
        /// <see cref="MultiplayerManager.Update"/>, so a backend that stops being available need
        /// not report its connections closed (see the remarks on the interface).
        /// </summary>
        public bool IsAvailable { get; }

        public PlayerId LocalPlayerId { get; }
        public string GetPlayerName(PlayerId player);

        /// <summary>Largest data, in bytes, that one <see cref="Send"/> carries; 512 KiB on Steam.</summary>
        public int MaxPacketSize { get; }

        public void SetListener(IMultiplayerBackendListener listener);

        /// <summary>Delivers what arrived since the last call. The manager calls it once per frame.</summary>
        public void Update();

        #region Lobby
        /// <summary>
        /// Creates a lobby owned by this player with the given lobby data, each entry written as
        /// <see cref="SetLobbyData"/> writes it, so an entry with an empty value writes no key. The
        /// callback answers with <see cref="MultiplayerReason.None"/> and the new lobby, or a
        /// failure reason.
        /// </summary>
        public void CreateLobby(LobbyVisibility visibility, int capacity, IReadOnlyDictionary<string, string> data, Action<MultiplayerReason, LobbyId> onCompleted);

        /// <summary>
        /// Searches public, joinable lobbies that have a free slot and pass every filter. A filter
        /// whose key is null or empty is skipped, as the Steam backend skips it. A filter's key and
        /// a string filter's value are taken as <see cref="SetLobbyData"/> takes lobby data, a
        /// surrogate without its pair as U+FFFD and up to the first NUL, so a key with nothing
        /// before its first NUL is the empty key, which a string filter compares as an empty value
        /// and a numeric filter reads as 0, as Steam's search did, and is not skipped. The
        /// manager hands a backend no filter with a NUL or such a surrogate
        /// (<see cref="LobbySearchQuery.AddStringFilter"/>).
        /// </summary>
        public void SearchLobbies(LobbySearchQuery query, Action<MultiplayerReason, IReadOnlyList<BackendLobby>> onCompleted);

        /// <summary>
        /// Enters a lobby. A full lobby answers <see cref="MultiplayerReason.SessionFull"/>, one that
        /// is not joinable <see cref="MultiplayerReason.GameInProgress"/>, and a missing one
        /// <see cref="MultiplayerReason.LobbyNotFound"/>. After
        /// <see cref="MultiplayerReason.GameInProgress"/>, <see cref="GetLobbyData"/> reads that
        /// lobby's data, though this player is no member, so a player whose slot the host keeps
        /// can reach the host past the lobby; a backend that cannot leaves such a player refused.
        /// </summary>
        public void JoinLobby(LobbyId lobby, Action<MultiplayerReason> onCompleted);

        public void LeaveLobby(LobbyId lobby);

        /// <summary>
        /// Null when the lobby or the key is unknown to this player, and for a key the lobby holds
        /// with an empty value, as Steam answers an empty string for each (<see cref="SetLobbyData"/>).
        /// Lobby data keys are told apart without regard to case, as Steam tells them apart, so a
        /// key is found in any case here, in the data a search returns, and by the search filters.
        /// </summary>
        public string GetLobbyData(LobbyId lobby, string key);

        /// <summary>
        /// Owner only; false otherwise. Every backend takes an empty or null value as a Steam lobby
        /// took an empty one on AppID 480 on 2026-11-04 (#343): it writes no key the lobby lacks,
        /// and answers true all the same, and it leaves a key the lobby holds with the empty value.
        /// <see cref="GetLobbyData"/> answers null for such a key, the data a search returns holds
        /// it with the empty value, and the search filters compare that value as Steam's search
        /// did: a string filter as an empty string, so <c>Equal ""</c> and <c>NotEqual "x"</c>
        /// keep the lobby, and a numeric filter as 0. The manager never writes an empty value here
        /// and deletes the key instead (<see cref="IMultiplayerManager.SetLobbyData"/>), and it
        /// leaves a key with an empty value out of the data it hands <see cref="CreateLobby"/>,
        /// so its <see cref="IMultiplayerManager.LobbyData"/> and the lobby that search reads hold
        /// the same keys (#313).
        /// <para>
        /// Every backend also takes a UTF-16 surrogate without its pair, in the key or the value,
        /// as UTF-8 carries it to Steam: as U+FFFD. So <see cref="GetLobbyData"/>, the data a
        /// search returns, and the search filters, which take their keys and values the same way,
        /// read U+FFFD in its place for every player, the owner included, and keys that differ
        /// only in such surrogates are one key, as on Steam (#372).
        /// </para>
        /// <para>
        /// Every backend takes the key and the value up to their first NUL character as well, as
        /// Steamworks.NET hands Steam text as a C string that ends there, and as Steam took such
        /// text on AppID 480 on 2026-11-08 (#387): <see cref="GetLobbyData"/>, which reads its key
        /// the same way, the data a search returns, and the search filters read only the text
        /// before the NUL, and keys that differ only after a NUL are one key. A value with nothing
        /// before its first NUL is an empty value, as above. A key with nothing before its first
        /// NUL reaches Steam as an empty key: Steam answered true for it, and the owner read the
        /// value back at once, but its lobby kept no such key once Steam's servers answered and
        /// search never returned it, so the local backends write no key and answer true for the
        /// owner, while a key that is null or empty to begin with answers false on every backend.
        /// </para>
        /// <para>
        /// The manager refuses a NUL character and an unpaired surrogate in every text it writes
        /// into the lobby data or searches by: the lobby data a game sets, the game ID
        /// (<see cref="MultiplayerConfig.GameId"/>), the lobby name
        /// (<see cref="LobbyCreateOptions.Name"/>), and search filters
        /// (<see cref="LobbySearchQuery.AddStringFilter"/>), with
        /// <see cref="MultiplayerReason.InvalidArgument"/>, since the lobby would otherwise hold,
        /// or search compare, other text than the game wrote. Only a caller of a backend itself
        /// writes such text.
        /// </para>
        /// </summary>
        public bool SetLobbyData(LobbyId lobby, string key, string value);

        /// <summary>
        /// Owner only; false otherwise, and false for a key the lobby lacks, as Steam answers
        /// (#343). Removes the key from the lobby, so lobby search no longer returns it or finds
        /// the lobby by it, except that a numeric filter reads a key the lobby lacks as 0, as
        /// Steam's search did (#353). The manager asks only for a key the lobby holds.
        /// </summary>
        public bool DeleteLobbyData(LobbyId lobby, string key);

        /// <summary>Owner only. A lobby that is not joinable leaves search and refuses every join.</summary>
        public bool SetLobbyJoinable(LobbyId lobby, bool joinable);

        /// <summary>Owner only.</summary>
        public bool SetLobbyCapacity(LobbyId lobby, int capacity);

        public PlayerId GetLobbyOwner(LobbyId lobby);

        /// <summary>Whether the lobby, as this player knows it, lists the player as a member.</summary>
        public bool IsLobbyMember(LobbyId lobby, PlayerId player);
        #endregion

        #region Connection
        /// <summary>Starts taking connections from other players. False when it cannot.</summary>
        public bool StartListening();
        public void StopListening();

        /// <summary>
        /// Starts connecting to a listening player. The listener's
        /// <see cref="IMultiplayerBackendListener.OnConnected"/> or
        /// <see cref="IMultiplayerBackendListener.OnDisconnected"/> with
        /// <see cref="BackendDisconnectKind.ConnectFailed"/> reports the outcome.
        /// Returns <see cref="ConnectionId.None"/> when the attempt cannot even start.
        /// </summary>
        public ConnectionId Connect(PlayerId host);

        /// <summary>
        /// False when this side closed the connection, or when the open connection cannot take
        /// the data now, for example because its send buffer is full. The session treats a
        /// reliable send that returns false as a lost connection: the host drops that player, and
        /// a client ends its session, since otherwise one side would go on without data the other
        /// side sent. A connection that the other side already closed, or that was already lost,
        /// answers true and drops the data, so the close it is about to report keeps its reason.
        /// </summary>
        public bool Send(ConnectionId connection, ReadOnlySpan<byte> data, DeliveryMode mode);

        /// <summary>
        /// Closes the connection after the data already sent on it. The other side receives
        /// <paramref name="reasonCode"/>; this side receives no disconnect callback for it.
        /// </summary>
        public void Disconnect(ConnectionId connection, int reasonCode);

        /// <summary>
        /// How a connection performs now, as this backend measures it. False for a connection
        /// this backend does not hold open, such as one either side closed.
        /// </summary>
        public bool TryGetConnectionQuality(ConnectionId connection, out ConnectionQuality quality);
        #endregion

        #region Platform
        /// <summary>
        /// Reads a player's avatar at the given size. The callback answers with
        /// <see cref="MultiplayerReason.None"/> and the image, or with a failure reason and no
        /// image: <see cref="MultiplayerReason.NotSupported"/> on a backend that has no avatars,
        /// and <see cref="MultiplayerReason.InvalidArgument"/> for an invalid player or size.
        /// A backend whose platform tells when an avatar changes reports it through
        /// <see cref="IMultiplayerBackendListener.OnAvatarChanged"/>, and the manager then asks
        /// again for the avatars it holds of that player.
        /// </summary>
        public void LoadAvatar(PlayerId player, AvatarSize size, Action<MultiplayerReason, BackendAvatar> onCompleted);

        /// <summary>
        /// Shows this player's friends a status text and the session this player is in, as a
        /// group of <paramref name="groupSize"/> players. A null status removes the status, and
        /// <see cref="LobbyId.None"/> removes the group; the manager calls it only when something
        /// changed, and with neither to remove both. A backend without such a feature does nothing
        /// and answers true. False when the platform refused it.
        /// </summary>
        public bool SetPresence(string status, LobbyId group, int groupSize);
        #endregion
    }
}
