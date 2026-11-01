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
    /// </remarks>
    public interface IMultiplayerBackend : IDisposable
    {
        public string Name { get; }

        /// <summary>False once the backend cannot serve requests, for example after it is disposed.</summary>
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

        /// <summary>
        /// Null when the lobby or the key is unknown to this player. Lobby data keys are told apart
        /// without regard to case, as Steam tells them apart, so a key is found in any case here, in
        /// the data a search returns, and by the search filters.
        /// </summary>
        public string GetLobbyData(LobbyId lobby, string key);

        /// <summary>Owner only; false otherwise.</summary>
        public bool SetLobbyData(LobbyId lobby, string key, string value);

        /// <summary>
        /// Owner only; false otherwise. Removes the key from the lobby, so lobby search no longer
        /// returns it or finds the lobby by it. The manager asks only for a key the lobby holds.
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
