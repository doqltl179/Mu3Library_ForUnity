using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local
{
    internal sealed class LocalLobby
    {
        public LobbyId Id;
        public LocalBackend Owner;
        public LobbyVisibility Visibility;
        public int Capacity;
        public bool IsJoinable = true;
        public readonly List<LocalBackend> Members = new List<LocalBackend>();
        // Keys are found in any case, as Steam finds lobby data keys.
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
