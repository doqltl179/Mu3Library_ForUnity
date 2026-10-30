namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// One player in the session as the host last reported them.
    /// </summary>
    public readonly struct Participant
    {
        public PlayerId Id { get; }
        public string Name { get; }
        public bool IsHost { get; }
        public bool IsLocal { get; }
        public bool IsReady { get; }

        /// <summary>True for a player who joined while the game ran, until the game returns to the lobby.</summary>
        public bool IsLateJoiner { get; }

        internal Participant(PlayerId id, string name, bool isHost, bool isLocal, bool isReady, bool isLateJoiner)
        {
            Id = id;
            Name = name ?? string.Empty;
            IsHost = isHost;
            IsLocal = isLocal;
            IsReady = isReady;
            IsLateJoiner = isLateJoiner;
        }

        internal Participant WithReady(bool isReady) => new Participant(Id, Name, IsHost, IsLocal, isReady, IsLateJoiner);
        internal Participant WithLateJoiner(bool isLateJoiner) => new Participant(Id, Name, IsHost, IsLocal, IsReady, isLateJoiner);

        public override string ToString() => $"{Name} ({Id})";
    }
}
