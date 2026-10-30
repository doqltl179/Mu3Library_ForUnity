using System;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Backend
{
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
}
