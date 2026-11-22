using System.Globalization;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// What a <see cref="StateSyncManager"/> sent one player, as
    /// <see cref="IStateSyncManager.GetPlayerStats"/> reports it: the host has one for each client,
    /// and a client one for the host. Bytes are the payloads of the tick's messages, without the
    /// library's header of each message. The numbers start when the player became a recipient,
    /// in this session or since they came back to their slot.
    /// </summary>
    public readonly struct SyncPlayerStats
    {
        public PlayerId Player { get; }

        /// <summary>The tick of the last tick that sent this player anything.</summary>
        public uint LastTick { get; }

        /// <summary>Bytes the last tick that ran sent this player.</summary>
        public int LastTickBytes { get; }

        /// <summary>States the last tick that ran sent this player.</summary>
        public int LastTickStates { get; }

        /// <summary>States the last tick that ran had for this player but left for a later tick, as they did not fit.</summary>
        public int LastTickDeferred { get; }

        /// <summary>The most bytes one tick sent this player.</summary>
        public int MaxTickBytes { get; }

        /// <summary>Bytes every tick sent this player.</summary>
        public long TotalBytes { get; }

        public SyncPlayerStats(PlayerId player, uint lastTick, int lastTickBytes, int lastTickStates, int lastTickDeferred, int maxTickBytes, long totalBytes)
        {
            Player = player;
            LastTick = lastTick;
            LastTickBytes = lastTickBytes;
            LastTickStates = lastTickStates;
            LastTickDeferred = lastTickDeferred;
            MaxTickBytes = maxTickBytes;
            TotalBytes = totalBytes;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture,
                "player {0}: last tick {1} B, {2} states, {3} deferred; max {4} B; {5} B in all",
                Player, LastTickBytes, LastTickStates, LastTickDeferred, MaxTickBytes, TotalBytes);
        }
    }
}
