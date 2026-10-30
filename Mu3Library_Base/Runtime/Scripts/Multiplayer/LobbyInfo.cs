using System.Collections.Generic;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// One lobby that lobby search returned.
    /// </summary>
    public sealed class LobbyInfo
    {
        public LobbyId Id { get; }
        public string Name { get; }
        public PlayerId HostId { get; }
        public int MemberCount { get; }
        public int Capacity { get; }
        public bool IsInGame { get; }

        /// <summary>The game-defined lobby data. The library's own <c>mu3.</c> keys are left out.</summary>
        public IReadOnlyDictionary<string, string> Data { get; }

        public LobbyInfo(LobbyId id, string name, PlayerId hostId, int memberCount, int capacity, bool isInGame, IReadOnlyDictionary<string, string> data)
        {
            Id = id;
            Name = name ?? string.Empty;
            HostId = hostId;
            MemberCount = memberCount;
            Capacity = capacity;
            IsInGame = isInGame;
            Data = data ?? new Dictionary<string, string>();
        }
    }
}
