using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// Answers, on the host, whether <paramref name="recipient"/> receives <paramref name="state"/>
    /// this tick, as a game sends a character only to the players close to it
    /// (<see cref="IStateSyncManager.Register"/>). A player the filter leaves out gets the whole
    /// state once the filter lets it in again. Answer from what the host knows, such as the
    /// positions it keeps; the filter must not register, unregister, send, or change the session.
    /// </summary>
    public delegate bool SyncInterestFilter(ISyncState state, PlayerId recipient);
}
