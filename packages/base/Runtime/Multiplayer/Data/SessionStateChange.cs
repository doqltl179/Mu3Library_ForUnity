namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// One session state transition. <see cref="Reason"/> names why a request failed or the
    /// session ended, is <see cref="MultiplayerReason.ConnectionLost"/> for a change to
    /// <see cref="SessionState.Reconnecting"/>, and is <see cref="MultiplayerReason.None"/> otherwise.
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
