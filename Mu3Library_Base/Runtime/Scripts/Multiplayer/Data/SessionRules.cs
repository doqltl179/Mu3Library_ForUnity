using System.Globalization;

namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// The host's rules for one session. Every count includes the host.
    /// </summary>
    public readonly struct SessionRules
    {
        /// <summary>Most players the session holds, from <see cref="MultiplayerLimits.MinCapacity"/> to <see cref="MultiplayerLimits.MaxCapacity"/>.</summary>
        public int Capacity { get; }
        /// <summary>Fewest players the host can start the game with, from 1 to <see cref="Capacity"/>.</summary>
        public int MinPlayersToStart { get; }
        /// <summary>When true, every player except the host must be ready before the game starts.</summary>
        public bool RequireReady { get; }
        /// <summary>When false, nobody can join while the game runs and the lobby leaves search until the game ends.</summary>
        public bool AllowLateJoin { get; }

        /// <summary>
        /// Most packets the host takes from one client in a second: 0, the default, sets no limit.
        /// Everything a client sends the host counts: its game messages, chat messages, ready
        /// changes, load reports, participant data requests, and each hello after its first, with
        /// which it connects again or joins again. The host counts in windows of one second, by
        /// account and for the whole session, so neither connecting again nor leaving and joining
        /// again starts a fresh window: the first starts with the account's first packet, each
        /// next one where the last ended, and after a second without packets, the next packet
        /// starts a new one. Within a window, the host drops a game or chat message beyond a limit
        /// before it handles or passes it on, and raises
        /// <see cref="IMultiplayerManagerEventBus.OnRateLimitExceeded"/>; the client's own
        /// <see cref="IMultiplayerMessenger.Send"/> or <see cref="IMultiplayerManager.SendChat"/>
        /// already succeeded. A ready change, a load report, or a participant data request beyond
        /// a limit counts and raises the event too, but waits instead: the host applies such
        /// requests in the order they came, as later windows have room, since a client checks
        /// each data request against all the ones it sent before. While any of them waits, the
        /// host drops the client's game messages, reliable ones included, and chat messages, even
        /// when the window has room, and counts them as past the limit, so that none of them
        /// overtakes a request sent before it. A hello beyond a limit waits for room as well, up
        /// to <see cref="MultiplayerConfig.HandshakeTimeoutSeconds"/>, and a window that took
        /// nothing yet takes one request or hello of any size. A client with more than
        /// <see cref="MultiplayerLimits.MaxDelayedRequests"/> requests waiting is disconnected with
        /// <see cref="MultiplayerReason.RateLimited"/>, whatever <see cref="RateLimitKickSeconds"/>
        /// says, but may join again; requests that still wait when the client's connection drops
        /// are lost with it. The host's own sends are never limited.
        /// </summary>
        public int MaxMessagesPerSecond { get; }

        /// <summary>
        /// Most bytes the host takes from one client in a second, counted as the packets arrive,
        /// headers included, in the windows <see cref="MaxMessagesPerSecond"/> describes: 0, the
        /// default, sets no limit. A game or chat message that would take the window past it is
        /// dropped, so one larger than the limit never gets through.
        /// </summary>
        public int MaxBytesPerSecond { get; }

        /// <summary>
        /// When greater than 0, the host kicks a client that goes past a rate limit in every
        /// window for this many seconds, with <see cref="MultiplayerReason.RateLimited"/>, and the
        /// account cannot rejoin the session, as after <see cref="IMultiplayerManager.Kick"/>. A
        /// window within both limits, or a second without packets, starts the count again. 0,
        /// the default, never kicks. Either way, a client with more than
        /// <see cref="MultiplayerLimits.MaxDelayedRequests"/> requests waiting is disconnected
        /// with <see cref="MultiplayerReason.RateLimited"/>, without a ban, since the host can
        /// neither drop such a request nor hold more.
        /// </summary>
        public float RateLimitKickSeconds { get; }

        /// <summary>
        /// How long the host keeps the slot of a client whose connection drops during a game, in
        /// seconds: <see cref="DefaultReconnectReservationSeconds"/> by default, 0 to free it at
        /// once. A connection drops when it stops answering or a reliable message cannot reach
        /// it; a player who leaves, or whom the host kicks, never keeps a slot. Meanwhile the
        /// player stays in <see cref="IMultiplayerManager.Participants"/> with
        /// <see cref="Participant.IsConnected"/> false and still counts toward
        /// <see cref="Capacity"/> and <see cref="MinPlayersToStart"/>, and only the same account
        /// can take the slot: by connecting again from the game that kept running, or by joining
        /// again from another start of the game, which the lobby admits only in the lobby phase
        /// or with <see cref="AllowLateJoin"/>. The library on that player's side connects again
        /// by itself while its game runs: its state turns to
        /// <see cref="SessionState.Reconnecting"/>, and it connects to the host again until it is
        /// back or this time is over, counted from when it noticed the drop. The slot outlasts
        /// the end of the game, so a player may come back to the lobby or to the next game. A
        /// player who does not come back in time leaves with
        /// <see cref="MultiplayerReason.ConnectionLost"/>; <see cref="SessionState.Reconnecting"/>
        /// tells what else ends the attempt. New rules with a shorter time cut the slots kept
        /// then to at most that time from then on; a longer one leaves them as they are.
        /// </summary>
        public float ReconnectReservationSeconds { get; }

        public const float DefaultReconnectReservationSeconds = 60f;

        /// <summary>
        /// How long the host waits from the start of a game for every player to report with
        /// <see cref="IMultiplayerManager.ReportLoaded"/> that they are ready to play, in seconds:
        /// <see cref="DefaultLoadTimeoutSeconds"/> by default, 0 to wait without a limit. When the
        /// time passes first, the host hears <see cref="IMultiplayerManagerEventBus.OnLoadTimedOut"/>
        /// with the players who did not report.
        /// </summary>
        public float LoadTimeoutSeconds { get; }

        public const float DefaultLoadTimeoutSeconds = 60f;

        public static SessionRules Default => new SessionRules(MultiplayerLimits.DefaultCapacity);

        public SessionRules(
            int capacity,
            int minPlayersToStart = 1,
            bool requireReady = true,
            bool allowLateJoin = false,
            int maxMessagesPerSecond = 0,
            int maxBytesPerSecond = 0,
            float rateLimitKickSeconds = 0f,
            float reconnectReservationSeconds = DefaultReconnectReservationSeconds,
            float loadTimeoutSeconds = DefaultLoadTimeoutSeconds)
        {
            Capacity = capacity;
            MinPlayersToStart = minPlayersToStart;
            RequireReady = requireReady;
            AllowLateJoin = allowLateJoin;
            MaxMessagesPerSecond = maxMessagesPerSecond;
            MaxBytesPerSecond = maxBytesPerSecond;
            RateLimitKickSeconds = rateLimitKickSeconds;
            ReconnectReservationSeconds = reconnectReservationSeconds;
            LoadTimeoutSeconds = loadTimeoutSeconds;
        }

        public SessionRules WithCapacity(int capacity) =>
            new SessionRules(capacity, MinPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds, ReconnectReservationSeconds, LoadTimeoutSeconds);
        public SessionRules WithMinPlayersToStart(int minPlayersToStart) =>
            new SessionRules(Capacity, minPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds, ReconnectReservationSeconds, LoadTimeoutSeconds);
        public SessionRules WithRequireReady(bool requireReady) =>
            new SessionRules(Capacity, MinPlayersToStart, requireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds, ReconnectReservationSeconds, LoadTimeoutSeconds);
        public SessionRules WithAllowLateJoin(bool allowLateJoin) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, allowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds, ReconnectReservationSeconds, LoadTimeoutSeconds);
        public SessionRules WithMaxMessagesPerSecond(int maxMessagesPerSecond) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, AllowLateJoin, maxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds, ReconnectReservationSeconds, LoadTimeoutSeconds);
        public SessionRules WithMaxBytesPerSecond(int maxBytesPerSecond) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, maxBytesPerSecond, RateLimitKickSeconds, ReconnectReservationSeconds, LoadTimeoutSeconds);
        public SessionRules WithRateLimitKickSeconds(float rateLimitKickSeconds) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, rateLimitKickSeconds, ReconnectReservationSeconds, LoadTimeoutSeconds);
        public SessionRules WithReconnectReservationSeconds(float reconnectReservationSeconds) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds, reconnectReservationSeconds, LoadTimeoutSeconds);
        public SessionRules WithLoadTimeoutSeconds(float loadTimeoutSeconds) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds, ReconnectReservationSeconds, loadTimeoutSeconds);

        public override string ToString() =>
            $"Capacity: {Capacity}, MinPlayersToStart: {MinPlayersToStart}, RequireReady: {RequireReady}, AllowLateJoin: {AllowLateJoin}, "
            + $"MaxMessagesPerSecond: {MaxMessagesPerSecond}, MaxBytesPerSecond: {MaxBytesPerSecond}, RateLimitKickSeconds: {RateLimitKickSeconds.ToString(CultureInfo.InvariantCulture)}, "
            + $"ReconnectReservationSeconds: {ReconnectReservationSeconds.ToString(CultureInfo.InvariantCulture)}, "
            + $"LoadTimeoutSeconds: {LoadTimeoutSeconds.ToString(CultureInfo.InvariantCulture)}";
    }
}
