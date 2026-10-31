namespace Mu3Library.Multiplayer.Data
{
    public enum SessionState
    {
        /// <summary>Not in a session.</summary>
        Offline = 0,
        /// <summary>The host is creating its lobby.</summary>
        Creating = 1,
        /// <summary>Entering a lobby and connecting to its host.</summary>
        Joining = 2,
        /// <summary>In the lobby before or between games.</summary>
        Lobby = 3,
        /// <summary>The host started the game.</summary>
        InGame = 4,
        /// <summary>
        /// A client's connection to the host dropped during a game, and it connects again to the
        /// slot the host keeps for it (<see cref="SessionRules.ReconnectReservationSeconds"/>).
        /// </summary>
        Reconnecting = 5,
    }
}
