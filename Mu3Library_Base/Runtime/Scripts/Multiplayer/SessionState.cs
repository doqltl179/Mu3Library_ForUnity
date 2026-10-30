namespace Mu3Library.Multiplayer
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
    }

    /// <summary>
    /// One session state transition. <see cref="Reason"/> names why a request failed or the
    /// session ended, and is <see cref="MultiplayerReason.None"/> otherwise.
    /// </summary>
    public readonly struct SessionStateChange
    {
        public SessionState Previous { get; }
        public SessionState Current { get; }
        public MultiplayerReason Reason { get; }

        public SessionStateChange(SessionState previous, SessionState current, MultiplayerReason reason)
        {
            Previous = previous;
            Current = current;
            Reason = reason;
        }

        public override string ToString() => $"{Previous} -> {Current} ({Reason})";
    }
}
