using System;
using System.Collections.Generic;
using System.IO;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// The network that <see cref="LocalBackend"/> players meet on without Steam. Every backend
    /// created on it sees the same lobbies and can connect to the others.
    /// <list type="bullet">
    /// <item><c>new LocalNetwork()</c> lives inside one process: automated tests, or a quick check
    /// with several players in one editor.</item>
    /// <item><see cref="CreateMachineWide"/> reaches every process on this computer that uses the
    /// same name, such as an editor and a player build. Nothing outside this computer can see its
    /// lobbies or connect to it.</item>
    /// </list>
    /// </summary>
    public sealed class LocalNetwork
    {
        public const int MaxNameLength = 64;

        private ulong _nextPlayerId = 1;
        private ulong _nextLobbyId = 1;
        private uint _nextConnectionId = 1;

        internal readonly List<LocalBackend> Backends = new List<LocalBackend>();
        internal readonly List<LocalLobby> Lobbies = new List<LocalLobby>();

        /// <summary>
        /// The name the processes of a machine-wide network share; null for a network inside one
        /// process.
        /// </summary>
        public string MachineWideName { get; }
        public bool IsMachineWide => MachineWideName != null;

        /// <summary>
        /// Machine-wide networks only: how long a connection may receive nothing before it counts
        /// as lost, and how long a join may wait for the lobby's owner, in seconds. 10 by default;
        /// 0 waits forever, for example while a debugger holds the other process. Give every
        /// process of the network the same value: each pings often enough for its own.
        /// </summary>
        public float ConnectionTimeoutSeconds { get; set; } = 10f;

        /// <summary>
        /// Machine-wide networks only: where the processes list their lobbies, a folder of this
        /// user's temporary folder by default. Tests point it elsewhere.
        /// </summary>
        internal string MachineFolder { get; set; }

        /// <summary>Seconds since an arbitrary start; null uses a monotonic clock. Tests hand in their own.</summary>
        internal Func<double> Clock { get; set; }

        /// <summary>
        /// When true, entering a lobby skips its capacity check, the way two players who enter at
        /// nearly the same moment both get past it. Tests use it to reach the host's own check.
        /// </summary>
        internal bool IgnoreLobbyCapacity { get; set; }

        /// <summary>Creates a network inside this process.</summary>
        public LocalNetwork()
        {
        }

        private LocalNetwork(string machineWideName)
        {
            MachineWideName = machineWideName;
            MachineFolder = Path.Combine(Path.GetTempPath(), "Mu3Library", "LocalNetwork", machineWideName);
        }

        /// <summary>
        /// Creates a network that every process on this computer reaches by the same
        /// <paramref name="name"/>, such as the game's ID. Each process lists the lobbies it owns
        /// in files under this user's temporary folder and talks to the others over the loopback
        /// address, so neither the lobbies nor the connections reach another computer. Any number
        /// of backends may share one such network in one process, too.
        /// </summary>
        /// <param name="name">
        /// 1 to <see cref="MaxNameLength"/> ASCII letters, digits, '-', or '_', and not a name
        /// Windows keeps for a device, such as CON or NUL, since it names a folder.
        /// </param>
        public static LocalNetwork CreateMachineWide(string name)
        {
            if (!IsValidName(name))
            {
                throw new ArgumentException($"A machine-wide network name is 1 to {MaxNameLength} ASCII letters, digits, '-', or '_', and not a Windows device name such as CON.", nameof(name));
            }

            return new LocalNetwork(name);
        }

        private static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            {
                return false;
            }

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                bool isValid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!isValid)
                {
                    return false;
                }
            }

            return !IsWindowsDeviceName(name);
        }

        private static bool IsWindowsDeviceName(string name)
        {
            string upper = name.ToUpperInvariant();
            if (upper == "CON" || upper == "PRN" || upper == "AUX" || upper == "NUL")
            {
                return true;
            }

            return upper.Length == 4
                && (upper.StartsWith("COM", StringComparison.Ordinal) || upper.StartsWith("LPT", StringComparison.Ordinal))
                && upper[3] >= '0' && upper[3] <= '9';
        }

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
}
