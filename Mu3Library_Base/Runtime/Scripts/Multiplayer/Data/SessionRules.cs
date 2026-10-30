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

        public static SessionRules Default => new SessionRules(MultiplayerLimits.DefaultCapacity);

        public SessionRules(int capacity, int minPlayersToStart = 1, bool requireReady = true, bool allowLateJoin = false)
        {
            Capacity = capacity;
            MinPlayersToStart = minPlayersToStart;
            RequireReady = requireReady;
            AllowLateJoin = allowLateJoin;
        }

        public SessionRules WithCapacity(int capacity) => new SessionRules(capacity, MinPlayersToStart, RequireReady, AllowLateJoin);
        public SessionRules WithMinPlayersToStart(int minPlayersToStart) => new SessionRules(Capacity, minPlayersToStart, RequireReady, AllowLateJoin);
        public SessionRules WithRequireReady(bool requireReady) => new SessionRules(Capacity, MinPlayersToStart, requireReady, AllowLateJoin);
        public SessionRules WithAllowLateJoin(bool allowLateJoin) => new SessionRules(Capacity, MinPlayersToStart, RequireReady, allowLateJoin);

        public override string ToString() =>
            $"Capacity: {Capacity}, MinPlayersToStart: {MinPlayersToStart}, RequireReady: {RequireReady}, AllowLateJoin: {AllowLateJoin}";
    }
}
