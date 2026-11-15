using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// An optional capability of an <see cref="IMultiplayerBackend"/> whose lobbies that are not
    /// <see cref="LobbyVisibility.Public"/> let a player in only by invite, as Steam's
    /// invite-only and friends-only lobbies do. The host's <see cref="MultiplayerManager"/> calls
    /// <see cref="InviteToLobby"/> from inside its <see cref="MultiplayerManager.Update"/>, once
    /// for each player who came back to a kept slot (FR-20, FR-21) but is outside such a lobby. A
    /// backend whose lobbies let any player in by ID, such as the local backend, does not
    /// implement it.
    /// </summary>
    public interface ILobbyInviteBackend
    {
        /// <summary>
        /// Lets the player enter the lobby, as an invite does. False when the platform refused,
        /// which the manager only logs.
        /// </summary>
        public bool InviteToLobby(LobbyId lobby, PlayerId player);
    }
}
