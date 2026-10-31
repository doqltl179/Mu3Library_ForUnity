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
        /// changes, and participant data requests. The host counts in windows of one second; the
        /// first starts with the client's first packet, each next one where the last ended, and
        /// after a second without packets, the next packet starts a new one. Within a window, the
        /// host drops a game or chat message beyond a limit before it handles or passes it on,
        /// and raises <see cref="IMultiplayerManagerEventBus.OnRateLimitExceeded"/>; the client's
        /// own <see cref="IMultiplayerMessenger.Send"/> or <see cref="IMultiplayerManager.SendChat"/>
        /// already succeeded. A ready change or a participant data request beyond a limit still
        /// counts and still raises the event, but the host takes it, since a client checks each
        /// data request against all the ones it sent before, which a dropped one would leave the
        /// client and the host disagreeing on for the rest of the session. The host's own sends
        /// are never limited.
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
        /// the default, never kicks.
        /// </summary>
        public float RateLimitKickSeconds { get; }

        public static SessionRules Default => new SessionRules(MultiplayerLimits.DefaultCapacity);

        public SessionRules(
            int capacity,
            int minPlayersToStart = 1,
            bool requireReady = true,
            bool allowLateJoin = false,
            int maxMessagesPerSecond = 0,
            int maxBytesPerSecond = 0,
            float rateLimitKickSeconds = 0f)
        {
            Capacity = capacity;
            MinPlayersToStart = minPlayersToStart;
            RequireReady = requireReady;
            AllowLateJoin = allowLateJoin;
            MaxMessagesPerSecond = maxMessagesPerSecond;
            MaxBytesPerSecond = maxBytesPerSecond;
            RateLimitKickSeconds = rateLimitKickSeconds;
        }

        public SessionRules WithCapacity(int capacity) =>
            new SessionRules(capacity, MinPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds);
        public SessionRules WithMinPlayersToStart(int minPlayersToStart) =>
            new SessionRules(Capacity, minPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds);
        public SessionRules WithRequireReady(bool requireReady) =>
            new SessionRules(Capacity, MinPlayersToStart, requireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds);
        public SessionRules WithAllowLateJoin(bool allowLateJoin) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, allowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds);
        public SessionRules WithMaxMessagesPerSecond(int maxMessagesPerSecond) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, AllowLateJoin, maxMessagesPerSecond, MaxBytesPerSecond, RateLimitKickSeconds);
        public SessionRules WithMaxBytesPerSecond(int maxBytesPerSecond) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, maxBytesPerSecond, RateLimitKickSeconds);
        public SessionRules WithRateLimitKickSeconds(float rateLimitKickSeconds) =>
            new SessionRules(Capacity, MinPlayersToStart, RequireReady, AllowLateJoin, MaxMessagesPerSecond, MaxBytesPerSecond, rateLimitKickSeconds);

        public override string ToString() =>
            $"Capacity: {Capacity}, MinPlayersToStart: {MinPlayersToStart}, RequireReady: {RequireReady}, AllowLateJoin: {AllowLateJoin}, "
            + $"MaxMessagesPerSecond: {MaxMessagesPerSecond}, MaxBytesPerSecond: {MaxBytesPerSecond}, RateLimitKickSeconds: {RateLimitKickSeconds.ToString(CultureInfo.InvariantCulture)}";
    }
}
