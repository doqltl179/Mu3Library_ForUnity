namespace Mu3Library.Multiplayer.Data
{
    public enum LobbyVisibility
    {
        /// <summary>Joinable only through an invite. Lobby search never returns it.</summary>
        InviteOnly = 0,
        /// <summary>Joinable by friends and invitees. Lobby search never returns it.</summary>
        FriendsOnly = 1,
        /// <summary>Returned by lobby search and joinable by anyone.</summary>
        Public = 2,
    }
}
