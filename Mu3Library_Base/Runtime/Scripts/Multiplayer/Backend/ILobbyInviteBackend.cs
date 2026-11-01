using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// A backend whose lobbies take some players only by invite, as Steam's invite-only and
    /// friends-only lobbies do. The host invites a player back into such a lobby when the player
    /// came back to its slot but the lobby no longer lists it.
    /// </summary>
    internal interface ILobbyInviteBackend
    {
        /// <summary>Lets the player enter the lobby, as an invite does. False when the platform refused.</summary>
        bool InviteToLobby(LobbyId lobby, PlayerId player);
    }
}
