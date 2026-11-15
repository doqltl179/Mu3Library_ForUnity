using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// How long a connection may receive nothing before it counts as lost, in seconds: 10 by
        /// default; 0 waits forever, for example while a debugger holds the other process. On a
        /// network inside one process, only an outage that <see cref="LocalBackend.SimulateOutage"/>
        /// starts keeps a connection silent. On a machine-wide network it is also how long a join
        /// may wait for the lobby's owner; give every process of the network the same value: each
        /// pings often enough for its own.
        /// </summary>
        public float ConnectionTimeoutSeconds { get; set; } = 10f;

        private float _simulatedLatencyMilliseconds;
        private float _simulatedJitterMilliseconds;
        private float _simulatedLossPercent;

        /// <summary>
        /// How long each packet that a backend of this network sends on a session connection takes
        /// to arrive, in milliseconds: 0 by default. A round trip takes twice as long, and the ping
        /// that <see cref="IMultiplayerManager.TryGetConnectionQuality"/> reports says so.
        /// </summary>
        /// <remarks>
        /// With <see cref="SimulatedJitterMilliseconds"/> and <see cref="SimulatedLossPercent"/>, it
        /// makes the network as bad as a real one can be, to try a game on it without Steam. They
        /// act on the connections between players, not on lobbies, and can change at any time. On
        /// a machine-wide network each process applies its own values to what it sends, so give
        /// every process the same ones.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">A negative, infinite, or NaN value.</exception>
        public float SimulatedLatencyMilliseconds
        {
            get => _simulatedLatencyMilliseconds;
            set
            {
                _simulatedLatencyMilliseconds = CheckSimulated(value, float.MaxValue, nameof(SimulatedLatencyMilliseconds));
                Simulation.LatencySeconds = value / 1000.0;
            }
        }

        /// <summary>
        /// How far each packet's delay strays from <see cref="SimulatedLatencyMilliseconds"/>, at
        /// random up to this much either way and never below zero, in milliseconds: 0 by default.
        /// Unreliable packets can overtake each other then; reliable ones keep their order.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A negative, infinite, or NaN value.</exception>
        public float SimulatedJitterMilliseconds
        {
            get => _simulatedJitterMilliseconds;
            set
            {
                _simulatedJitterMilliseconds = CheckSimulated(value, float.MaxValue, nameof(SimulatedJitterMilliseconds));
                Simulation.JitterSeconds = value / 1000.0;
            }
        }

        /// <summary>
        /// Share of packets the network loses, in percent from 0 to 100: 0 by default. An
        /// unreliable packet it takes never arrives. A reliable one is sent again one round trip of
        /// <see cref="SimulatedLatencyMilliseconds"/> later, as often as the loss takes it, up to
        /// ten times, and the reliable packets after it wait for it, so they all still arrive, in
        /// the order they were sent.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A value outside 0 to 100, or NaN.</exception>
        public float SimulatedLossPercent
        {
            get => _simulatedLossPercent;
            set
            {
                _simulatedLossPercent = CheckSimulated(value, 100f, nameof(SimulatedLossPercent));
                Simulation.LossRate = value / 100.0;
            }
        }

        /// <summary>What the simulated latency, jitter, and loss do to each packet.</summary>
        internal NetworkSimulation Simulation { get; } = new NetworkSimulation();

        /// <summary>
        /// Machine-wide networks only: where the processes list their lobbies, a folder of this
        /// user's temporary folder by default. Tests point it elsewhere.
        /// </summary>
        internal string MachineFolder { get; set; }

        /// <summary>Seconds since an arbitrary start; null uses a monotonic clock. Tests hand in their own.</summary>
        internal Func<double> Clock { get; set; }

        internal double Now => Clock != null ? Clock() : Stopwatch.GetTimestamp() * SecondsPerTick;

        private static readonly double SecondsPerTick = 1.0 / Stopwatch.Frequency;

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

        /// <summary>
        /// Makes the simulated loss and jitter draw the same numbers on every run, so a test that
        /// sends the same packets loses the same ones.
        /// </summary>
        internal void SeedSimulation(int seed) => Simulation.Seed(seed);

        private static float CheckSimulated(float value, float max, string name)
        {
            if (!(value >= 0f && value <= max) || float.IsInfinity(value))
            {
                string range = max < float.MaxValue ? $"from 0 to {max:0}" : "finite and not negative";
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be {range}.");
            }

            return value;
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
