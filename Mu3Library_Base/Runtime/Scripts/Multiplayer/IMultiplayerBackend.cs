using System;
using System.Collections.Generic;

namespace Mu3Library.Multiplayer
{
    public enum BackendDisconnectKind
    {
        /// <summary>The connection was never established.</summary>
        ConnectFailed = 0,
        /// <summary>The other side closed the connection, with the reason code it passed.</summary>
        ClosedByPeer = 1,
        /// <summary>The connection dropped: a timeout, a crash, or a network problem.</summary>
        ConnectionLost = 2,
    }

    /// <summary>
    /// One lobby as a backend's search found it, with all of its lobby data.
    /// </summary>
    public readonly struct BackendLobby
    {
        public LobbyId Id { get; }
        public int MemberCount { get; }
        public int Capacity { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        public BackendLobby(LobbyId id, int memberCount, int capacity, IReadOnlyDictionary<string, string> data)
        {
            Id = id;
            MemberCount = memberCount;
            Capacity = capacity;
            Data = data;
        }
    }

    /// <summary>
    /// Receives what a backend reports. The backend calls it only from inside
    /// <see cref="IMultiplayerBackend.Update"/>, on the main thread.
    /// </summary>
    public interface IMultiplayerBackendListener
    {
        /// <summary>
        /// A connection opened: one another player made to this listening host
        /// (<paramref name="isIncoming"/> true), or one this player made with
        /// <see cref="IMultiplayerBackend.Connect"/>. <paramref name="remote"/> is the identity the
        /// backend verified, never one the other side claimed.
        /// </summary>
        void OnConnected(ConnectionId connection, PlayerId remote, bool isIncoming);

        /// <summary>
        /// A connection closed without this side calling <see cref="IMultiplayerBackend.Disconnect"/>.
        /// <paramref name="reasonCode"/> is what the other side passed to its own Disconnect, and 0
        /// when it passed none.
        /// </summary>
        void OnDisconnected(ConnectionId connection, PlayerId remote, BackendDisconnectKind kind, int reasonCode);

        /// <summary>
        /// Data arrived. The span is valid only during the call.
        /// </summary>
        void OnReceived(ConnectionId connection, PlayerId remote, ReadOnlySpan<byte> data);

        /// <summary>
        /// The lobby passed to another member, as a lobby does when its owner leaves.
        /// </summary>
        void OnLobbyOwnerChanged(LobbyId lobby, PlayerId newOwner);

        /// <summary>
        /// The player asked, outside the game, to join this lobby: an accepted invite, a join from
        /// the friends list, or a launch from either.
        /// </summary>
        void OnLobbyJoinRequested(LobbyId lobby);
    }

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
    /// A backend also promises that every lobby request answers exactly once, even when several
    /// searches overlap or the platform refuses the request, since the manager has no timeout
    /// while it creates a lobby. It reports a connection that stops answering as
    /// <see cref="BackendDisconnectKind.ConnectionLost"/> within its own timeout (10 seconds by
    /// default), which is how a client notices that its host crashed. And it keeps a join request
    /// that arrives before <see cref="SetListener"/>, such as the lobby a launch from an invite
    /// names, until a listener can receive it.
    /// </para>
    /// </remarks>
    public interface IMultiplayerBackend : IDisposable
    {
        public string Name { get; }

        /// <summary>False once the backend cannot serve requests, for example after it is disposed.</summary>
        public bool IsAvailable { get; }

        public PlayerId LocalPlayerId { get; }
        public string GetPlayerName(PlayerId player);

        public void SetListener(IMultiplayerBackendListener listener);

        /// <summary>Delivers what arrived since the last call. The manager calls it once per frame.</summary>
        public void Update();

        #region Lobby
        /// <summary>
        /// Creates a lobby owned by this player with the given lobby data. The callback answers
        /// with <see cref="MultiplayerReason.None"/> and the new lobby, or a failure reason.
        /// </summary>
        public void CreateLobby(LobbyVisibility visibility, int capacity, IReadOnlyDictionary<string, string> data, Action<MultiplayerReason, LobbyId> onCompleted);

        /// <summary>
        /// Searches public, joinable lobbies that have a free slot and pass every filter.
        /// </summary>
        public void SearchLobbies(LobbySearchQuery query, Action<MultiplayerReason, IReadOnlyList<BackendLobby>> onCompleted);

        /// <summary>
        /// Enters a lobby. A full lobby answers <see cref="MultiplayerReason.SessionFull"/>, one that
        /// is not joinable <see cref="MultiplayerReason.GameInProgress"/>, and a missing one
        /// <see cref="MultiplayerReason.LobbyNotFound"/>.
        /// </summary>
        public void JoinLobby(LobbyId lobby, Action<MultiplayerReason> onCompleted);

        public void LeaveLobby(LobbyId lobby);

        /// <summary>Null when the lobby or the key is unknown to this player.</summary>
        public string GetLobbyData(LobbyId lobby, string key);

        /// <summary>Owner only; false otherwise.</summary>
        public bool SetLobbyData(LobbyId lobby, string key, string value);

        /// <summary>Owner only. A lobby that is not joinable leaves search and refuses every join.</summary>
        public bool SetLobbyJoinable(LobbyId lobby, bool joinable);

        /// <summary>Owner only.</summary>
        public bool SetLobbyCapacity(LobbyId lobby, int capacity);

        public PlayerId GetLobbyOwner(LobbyId lobby);
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

        /// <summary>False when the connection is closed or the data cannot be sent.</summary>
        public bool Send(ConnectionId connection, ReadOnlySpan<byte> data, DeliveryMode mode);

        /// <summary>
        /// Closes the connection after the data already sent on it. The other side receives
        /// <paramref name="reasonCode"/>; this side receives no disconnect callback for it.
        /// </summary>
        public void Disconnect(ConnectionId connection, int reasonCode);
        #endregion
    }
}
