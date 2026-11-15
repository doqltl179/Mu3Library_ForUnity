using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Backend
{
    /// <summary>
    /// One lobby as a backend's search found it, with all of its lobby data.
    /// </summary>
    public readonly struct BackendLobby
    {
        public LobbyId Id { get; }
        public int MemberCount { get; }
        public int Capacity { get; }
        public IReadOnlyDictionary<string, string> Data { get; }

        public BackendLobby(LobbyId id, int memberCount, int capacity, IReadOnlyDictionary<string, string> data)
        {
            Id = id;
            MemberCount = memberCount;
            Capacity = capacity;
            Data = data;
        }
    }
}
