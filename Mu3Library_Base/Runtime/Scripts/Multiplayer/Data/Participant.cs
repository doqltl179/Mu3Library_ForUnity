using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Mu3Library.Multiplayer.Data
{
    /// <summary>
    /// One player in the session as the host last reported them.
    /// </summary>
    public readonly struct Participant
    {
        private static readonly IReadOnlyDictionary<string, string> NoData = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

        private readonly IReadOnlyDictionary<string, string> _data;

        public PlayerId Id { get; }
        public string Name { get; }
        public bool IsHost { get; }
        public bool IsLocal { get; }
        public bool IsReady { get; }

        /// <summary>True for a player who joined while the game ran, until the game returns to the lobby.</summary>
        public bool IsLateJoiner { get; }

        /// <summary>
        /// False while the player's connection is down and the host keeps their slot for them
        /// (<see cref="SessionRules.ReconnectReservationSeconds"/>), true otherwise. What the
        /// other players send meanwhile does not reach them.
        /// </summary>
        public bool IsConnected { get; }

        /// <summary>
        /// True once the player reported with <see cref="IMultiplayerManager.ReportLoaded"/> that
        /// they are ready to play the current game, for example because its scene loaded; false
        /// in the lobby and from the start of each game until they report.
        /// </summary>
        public bool IsLoaded { get; }

        /// <summary>
        /// The game-defined data this player set for themselves with
        /// <see cref="IMultiplayerManager.SetParticipantData"/>, as the host last reported it. A
        /// later change comes as a new participant; this one keeps the data it was handed out with.
        /// </summary>
        public IReadOnlyDictionary<string, string> Data => _data ?? NoData;

        internal Participant(PlayerId id, string name, bool isHost, bool isLocal, bool isReady, bool isLateJoiner,
            bool isConnected = true, bool isLoaded = false, IReadOnlyDictionary<string, string> data = null)
        {
            Id = id;
            Name = name ?? string.Empty;
            IsHost = isHost;
            IsLocal = isLocal;
            IsReady = isReady;
            IsLateJoiner = isLateJoiner;
            IsConnected = isConnected;
            IsLoaded = isLoaded;
            _data = data;
        }

        internal Participant WithReady(bool isReady) => new Participant(Id, Name, IsHost, IsLocal, isReady, IsLateJoiner, IsConnected, IsLoaded, _data);
        internal Participant WithLateJoiner(bool isLateJoiner) => new Participant(Id, Name, IsHost, IsLocal, IsReady, isLateJoiner, IsConnected, IsLoaded, _data);
        internal Participant WithConnected(bool isConnected) => new Participant(Id, Name, IsHost, IsLocal, IsReady, IsLateJoiner, isConnected, IsLoaded, _data);
        internal Participant WithLoaded(bool isLoaded) => new Participant(Id, Name, IsHost, IsLocal, IsReady, IsLateJoiner, IsConnected, isLoaded, _data);
        internal Participant WithData(IReadOnlyDictionary<string, string> data) => new Participant(Id, Name, IsHost, IsLocal, IsReady, IsLateJoiner, IsConnected, IsLoaded, data);

        public override string ToString() => $"{Name} ({Id})";
    }
}
