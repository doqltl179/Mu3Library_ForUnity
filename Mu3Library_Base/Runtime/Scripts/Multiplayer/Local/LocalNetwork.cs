using System.Collections.Generic;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// A network that lives inside one process. Every <see cref="LocalBackend"/> created on it
    /// sees the same lobbies and can connect to the others, so several players can share one
    /// process without Steam: automated tests, or a quick check in the editor.
    /// </summary>
    public sealed class LocalNetwork
    {
        private ulong _nextPlayerId = 1;
        private ulong _nextLobbyId = 1;
        private uint _nextConnectionId = 1;

        internal readonly List<LocalBackend> Backends = new List<LocalBackend>();
        internal readonly List<LocalLobby> Lobbies = new List<LocalLobby>();

        /// <summary>
        /// When true, entering a lobby skips its capacity check, the way two players who enter at
        /// nearly the same moment both get past it. Tests use it to reach the host's own check.
        /// </summary>
        internal bool IgnoreLobbyCapacity { get; set; }

        internal PlayerId AllocatePlayerId()
        {
            while (IsPlayerIdInUse(_nextPlayerId))
            {
                _nextPlayerId++;
            }

            return new PlayerId(_nextPlayerId++);
        }

        internal LobbyId AllocateLobbyId() => new LobbyId(_nextLobbyId++);

        internal ConnectionId AllocateConnectionId() => new ConnectionId(_nextConnectionId++);

        internal LocalLobby FindLobby(LobbyId id)
        {
            for (int i = 0; i < Lobbies.Count; i++)
            {
                if (Lobbies[i].Id == id)
                {
                    return Lobbies[i];
                }
            }

            return null;
        }

        internal LocalBackend FindListening(PlayerId player, LocalBackend except)
        {
            for (int i = 0; i < Backends.Count; i++)
            {
                LocalBackend backend = Backends[i];
                if (backend != except && backend.LocalPlayerId == player && backend.IsListening)
                {
                    return backend;
                }
            }

            return null;
        }

        internal string GetPlayerName(PlayerId player)
        {
            for (int i = 0; i < Backends.Count; i++)
            {
                if (Backends[i].LocalPlayerId == player)
                {
                    return Backends[i].PlayerName;
                }
            }

            return null;
        }

        /// <summary>
        /// Takes a member out of a lobby. As a Steam lobby does, a lobby passes to another member
        /// when its owner leaves and disappears when its last member leaves.
        /// </summary>
        internal void RemoveMember(LocalLobby lobby, LocalBackend member)
        {
            if (!lobby.Members.Remove(member))
            {
                return;
            }

            if (lobby.Members.Count == 0)
            {
                Lobbies.Remove(lobby);
                return;
            }

            if (lobby.Owner != member)
            {
                return;
            }

            lobby.Owner = lobby.Members[0];
            for (int i = 0; i < lobby.Members.Count; i++)
            {
                lobby.Members[i].EnqueueOwnerChanged(lobby.Id, lobby.Owner.LocalPlayerId);
            }
        }

        private bool IsPlayerIdInUse(ulong value)
        {
            for (int i = 0; i < Backends.Count; i++)
            {
                if (Backends[i].LocalPlayerId.Value == value)
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal sealed class LocalLobby
    {
        public LobbyId Id;
        public LocalBackend Owner;
        public LobbyVisibility Visibility;
        public int Capacity;
        public bool IsJoinable = true;
        public readonly List<LocalBackend> Members = new List<LocalBackend>();
        public readonly Dictionary<string, string> Data = new Dictionary<string, string>();
    }

    internal sealed class LocalConnection
    {
        public ConnectionId Id;
        public LocalBackend Owner;
        public PlayerId RemotePlayer;
        public LocalConnection Remote;
        public bool IsOpen;
    }
}
