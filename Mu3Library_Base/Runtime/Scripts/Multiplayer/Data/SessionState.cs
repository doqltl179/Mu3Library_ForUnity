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
        /// A client's connection to the host dropped during a game, and it connects to the host
        /// again, once a second, to come back to the slot the host keeps for it
        /// (<see cref="SessionRules.ReconnectReservationSeconds"/>). Once the host takes it back,
        /// the state turns to wherever the session is then, <see cref="InGame"/> or
        /// <see cref="Lobby"/>. Otherwise the session ends: with
        /// <see cref="MultiplayerReason.ConnectionLost"/> when the reservation time, counted from
        /// when this player noticed the drop, runs out or the host no longer keeps the slot; with
        /// <see cref="MultiplayerReason.Banned"/>, <see cref="MultiplayerReason.Kicked"/>, or
        /// <see cref="MultiplayerReason.RateLimited"/> when the host refuses it so; with
        /// <see cref="MultiplayerReason.HostLeft"/> as soon as the lobby shows the host gone,
        /// because it passed to another member or no longer lists the host while it still lists
        /// this player; and with <see cref="MultiplayerReason.LeftNormally"/> on
        /// <see cref="IMultiplayerManager.Leave"/>. A client cannot tell a host whose own network
        /// or power went down from a drop of its own, so while the lobby does not show the host
        /// gone it keeps trying for the whole reservation time. Inside one process, the local
        /// lobby shows a host that crashed or left at once; on a machine-wide network, once this
        /// process notices that the owner's process is gone. A Steam lobby passes to another
        /// member when its owner leaves Steam, which a player whose own Steam connection dropped
        /// may not hear.
        /// </summary>
        Reconnecting = 5,
    }
}
