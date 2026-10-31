using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local.Machine;

namespace Mu3Library.Multiplayer.Local
{
    /// <summary>
    /// A backend whose lobbies and connections live on a <see cref="LocalNetwork"/>: inside this
    /// process, or with <see cref="LocalNetwork.CreateMachineWide"/>, across the processes of this
    /// computer. Like a real network, nothing arrives during the call that sent it: every answer
    /// and every packet waits until this backend's next <see cref="Update"/>, in the order it was
    /// sent. Messages are never lost, whichever <see cref="DeliveryMode"/> they use, unless the
    /// network simulates a worse one with <see cref="LocalNetwork.SimulatedLatencyMilliseconds"/>,
    /// <see cref="LocalNetwork.SimulatedJitterMilliseconds"/>, and
    /// <see cref="LocalNetwork.SimulatedLossPercent"/>, or <see cref="SimulateOutage"/> cuts a
    /// player off for a while.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each connection sends a small probe four times a second, which the backend's own
    /// <see cref="Update"/> answers, to measure what <see cref="TryGetConnectionQuality"/> reports.
    /// The ping is the round trip the simulated latency and jitter gave those probes; the time a
    /// packet waits for the next <see cref="Update"/> is left out, as Steam's ping leaves it out, so
    /// without simulated latency the ping is 0. The bytes count the packets the session sent and
    /// received, without resends.
    /// </para>
    /// <para>
    /// Lobby search returns public lobbies only, as Steam does; joining by lobby ID works for every
    /// visibility, since the local network has no friends or invites.
    /// </para>
    /// <para>
    /// On a machine-wide network each backend listens on a port of the loopback address, and the
    /// process that owns a lobby answers the joins to it. A lobby takes a moment to show up in
    /// another process's search, and a connection that receives nothing for
    /// <see cref="LocalNetwork.ConnectionTimeoutSeconds"/> counts as lost. The other side states
    /// its player ID itself, so the identity is only as trustworthy as the processes on this
    /// computer.
    /// </para>
    /// </remarks>
    public sealed class LocalBackend : IMultiplayerBackend
    {
        private enum EventKind
        {
            Callback,
            Connected,
            Disconnected,
            Received,
            OwnerChanged,
            JoinRequested,
            Probe,
            ProbeReply,
        }

        private struct PendingEvent
        {
            public EventKind Kind;
            public ConnectionId Connection;
            public PlayerId Player;
            public bool IsIncoming;
            public BackendDisconnectKind DisconnectKind;
            public int ReasonCode;
            public byte[] Data;
            public int Length;
            public LobbyId Lobby;
            public Action Callback;

            /// <summary>The connection a packet crossed the network to, which counts what it still waits for.</summary>
            public LocalConnection Target;

            /// <summary>A probe's round trip so far, in seconds.</summary>
            public double RoundTrip;

            /// <summary>A reliable packet or a close, which waits for the end of an outage instead of getting lost.</summary>
            public bool IsOrdered;
        }

        private readonly LocalNetwork _network;

        // Set on a machine-wide network, where it runs every call; the members below serve a
        // network inside this process.
        private readonly MachinePeer _machine;

        private readonly Queue<PendingEvent> _events = new Queue<PendingEvent>();

        // Packets, closes, and probes that the simulated network holds back until their time comes.
        private readonly DelayQueue<PendingEvent> _delayed = new DelayQueue<PendingEvent>();
        private readonly Dictionary<ConnectionId, LocalConnection> _connections = new Dictionary<ConnectionId, LocalConnection>();
        private IMultiplayerBackendListener _listener;
        private bool _isDisposed = false;

        // A join request that arrived while nothing listened, as one from a launch does before
        // the game initializes; it goes to the next listener.
        private LobbyId _heldJoinRequest = LobbyId.None;

        // The largest packet, the same as Steam's, so a game that fits here fits there.
        private const int MaxPacketBytes = 512 * 1024;

        // Packets in flight borrow their buffers from a pool of this type's own: the shared pool
        // may drop its buffers on a garbage collection, which would make sending allocate again.
        private static readonly ArrayPool<byte> Packets = ArrayPool<byte>.Create(MaxPacketBytes, 64);

        public string Name => "Local";
        public int MaxPacketSize => MaxPacketBytes;
        public bool IsAvailable => _machine != null ? _machine.IsAvailable : !_isDisposed;
        public PlayerId LocalPlayerId { get; }
        public string PlayerName { get; }

        private bool _isListening = false;
        internal bool IsListening => _isListening && !_isDisposed;

        private int _failingSends;

        // Until when SimulateOutage cuts this backend's session connections off.
        private double _outageEnd = double.NegativeInfinity;

        /// <summary>
        /// Test switch: this many of the next sends report failure while the connection stays
        /// open, as a full send buffer on Steam does.
        /// </summary>
        internal int FailingSends
        {
            get => _machine != null ? _machine.FailingSends : _failingSends;
            set
            {
                if (_machine != null)
                {
                    _machine.FailingSends = value;
                }
                else
                {
                    _failingSends = value;
                }
            }
        }

        /// <summary>The loopback port this backend listens on; 0 on a network inside one process.</summary>
        internal int Port => _machine != null ? _machine.Port : 0;



        public LocalBackend(LocalNetwork network, string playerName)
            : this(network, playerName, PlayerId.None)
        {
        }

        /// <summary>
        /// Creates a backend for a given account. Two backends with the same ID stand for one
        /// account signed in on two machines. Without an ID, a network inside one process numbers
        /// its players from 1, and a machine-wide network picks a random one.
        /// </summary>
        public LocalBackend(LocalNetwork network, string playerName, PlayerId playerId)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            if (network.IsMachineWide)
            {
                _machine = new MachinePeer(network, playerName, playerId);
                LocalPlayerId = _machine.LocalPlayerId;
                PlayerName = _machine.PlayerName;
                return;
            }

            LocalPlayerId = playerId.IsValid ? playerId : network.AllocatePlayerId();
            PlayerName = string.IsNullOrEmpty(playerName)
                ? $"Player {LocalPlayerId.Value.ToString(CultureInfo.InvariantCulture)}"
                : playerName;
            network.Backends.Add(this);
        }

        public string GetPlayerName(PlayerId player)
        {
            if (_machine != null)
            {
                return _machine.GetPlayerName(player);
            }

            return _network.GetPlayerName(player) ?? player.ToString();
        }

        public void SetListener(IMultiplayerBackendListener listener)
        {
            if (_machine != null)
            {
                _machine.SetListener(listener);
                return;
            }

            _listener = listener;

            if (listener != null && _heldJoinRequest.IsValid && !_isDisposed)
            {
                _events.Enqueue(new PendingEvent { Kind = EventKind.JoinRequested, Lobby = _heldJoinRequest });
                _heldJoinRequest = LobbyId.None;
            }
        }

        public void Update()
        {
            if (_machine != null)
            {
                _machine.Update();
                return;
            }

            // Whatever a callback queues waits for the next Update, as an answer from a real
            // network would.
            double now = _network.Now;
            int count = _events.Count;
            for (int i = 0; i < count && !_isDisposed; i++)
            {
                PendingEvent pending = _events.Dequeue();
                if (!HoldsBack(pending, now))
                {
                    Dispatch(pending);
                }
            }

            // What the simulated network held back follows once its time has come; what reaches
            // it during this update waits for the next one.
            long sequence = _delayed.NextSequence;
            while (!_isDisposed && _delayed.TryPeek(out double due, out long next) && due <= now && next < sequence)
            {
                PendingEvent pending = _delayed.Pop();
                pending.Target.DelayedCount--;
                if (!HoldsBack(pending, now))
                {
                    Dispatch(pending);
                }
            }

            if (_isDisposed)
            {
                return;
            }

            ReportSilencedConnections(now);

            foreach (LocalConnection connection in _connections.Values)
            {
                if (!connection.IsOpen)
                {
                    continue;
                }

                connection.Meter.Update(now);
                if (connection.Meter.TakeProbe(now))
                {
                    SendProbe(connection, EventKind.Probe, 0.0);
                }
            }
        }

        /// <summary>
        /// Stops as a crashed process would: the other players see their connections drop and
        /// the lobbies this player was in pass on, but nothing tells them the player left. On a
        /// machine-wide network the lobby file stays behind too, until it goes stale.
        /// </summary>
        public void SimulateCrash()
        {
            if (_machine != null)
            {
                _machine.SimulateCrash();
                return;
            }

            Shutdown();
        }

        /// <summary>
        /// Cuts this player off its session connections for <paramref name="seconds"/>, as when
        /// its network drops for a while, to try how a game handles that without Steam. Meanwhile
        /// no session connection opens to or from this player, unreliable messages either way are
        /// lost, and reliable ones, a close included, wait for the outage to end, as their resends
        /// would. A connection that the outage keeps silent for
        /// <see cref="LocalNetwork.ConnectionTimeoutSeconds"/> drops on both sides as lost, as a
        /// real one that stops answering does; one that this player closes meanwhile ends on the
        /// other side at once as lost, without its reason. Lobbies go on as before. A call during
        /// an outage makes it last at least <paramref name="seconds"/> from now. On a machine-wide
        /// network the outage acts on this process's side of its connections, so what the other
        /// side sent unreliably meanwhile still arrives when a connection lives through it.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">A negative, infinite, or NaN value.</exception>
        public void SimulateOutage(float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "An outage lasts a finite time of 0 seconds or more.");
            }

            if (_machine != null)
            {
                _machine.SimulateOutage(seconds);
                return;
            }

            if (_isDisposed)
            {
                return;
            }

            double now = _network.Now;
            _outageEnd = Math.Max(_outageEnd, now + seconds);
            if (!IsCutOff(now))
            {
                return;
            }

            foreach (LocalConnection connection in _connections.Values)
            {
                MarkCut(connection, now);
                if (connection.Remote != null)
                {
                    MarkCut(connection.Remote, now);
                }
            }
        }

        /// <summary>
        /// Reports a join request from outside the game, as accepting an invite or joining from
        /// the friends list does on Steam.
        /// </summary>
        public void RequestJoin(LobbyId lobby)
        {
            if (_machine != null)
            {
                _machine.RequestJoin(lobby);
                return;
            }

            if (_isDisposed)
            {
                return;
            }

            _events.Enqueue(new PendingEvent { Kind = EventKind.JoinRequested, Lobby = lobby });
        }

        public void Dispose()
        {
            if (_machine != null)
            {
                _machine.Dispose();
                return;
            }

            Shutdown();
        }

        #region Lobby
        public void CreateLobby(LobbyVisibility visibility, int capacity, IReadOnlyDictionary<string, string> data, Action<MultiplayerReason, LobbyId> onCompleted)
        {
            if (_machine != null)
            {
                _machine.CreateLobby(visibility, capacity, data, onCompleted);
                return;
            }

            Dictionary<string, string> snapshot = new Dictionary<string, string>();
            if (data != null)
            {
                foreach (KeyValuePair<string, string> pair in data)
                {
                    snapshot[pair.Key] = pair.Value;
                }
            }

            EnqueueCallback(() =>
            {
                LocalLobby lobby = new LocalLobby
                {
                    Id = _network.AllocateLobbyId(),
                    Owner = this,
                    Visibility = visibility,
                    Capacity = capacity,
                };
                lobby.Members.Add(this);
                foreach (KeyValuePair<string, string> pair in snapshot)
                {
                    lobby.Data[pair.Key] = pair.Value;
                }

                _network.Lobbies.Add(lobby);
                onCompleted?.Invoke(MultiplayerReason.None, lobby.Id);
            });
        }

        public void SearchLobbies(LobbySearchQuery query, Action<MultiplayerReason, IReadOnlyList<BackendLobby>> onCompleted)
        {
            if (_machine != null)
            {
                _machine.SearchLobbies(query, onCompleted);
                return;
            }

            LobbySearchQuery snapshot = query != null ? query.Clone() : new LobbySearchQuery();

            EnqueueCallback(() =>
            {
                List<BackendLobby> results = new List<BackendLobby>();
                for (int i = 0; i < _network.Lobbies.Count && results.Count < snapshot.MaxResults; i++)
                {
                    LocalLobby lobby = _network.Lobbies[i];
                    if (lobby.Visibility != LobbyVisibility.Public
                        || !lobby.IsJoinable
                        || lobby.Members.Count >= lobby.Capacity
                        || !LobbyFilter.Matches(lobby.Data, snapshot))
                    {
                        continue;
                    }

                    results.Add(new BackendLobby(lobby.Id, lobby.Members.Count, lobby.Capacity, new Dictionary<string, string>(lobby.Data)));
                }

                onCompleted?.Invoke(MultiplayerReason.None, results);
            });
        }

        public void JoinLobby(LobbyId lobbyId, Action<MultiplayerReason> onCompleted)
        {
            if (_machine != null)
            {
                _machine.JoinLobby(lobbyId, onCompleted);
                return;
            }

            EnqueueCallback(() =>
            {
                LocalLobby lobby = _network.FindLobby(lobbyId);
                MultiplayerReason reason = MultiplayerReason.None;
                if (lobby == null)
                {
                    reason = MultiplayerReason.LobbyNotFound;
                }
                else if (!lobby.Members.Contains(this))
                {
                    if (!lobby.IsJoinable)
                    {
                        reason = MultiplayerReason.GameInProgress;
                    }
                    else if (!_network.IgnoreLobbyCapacity && lobby.Members.Count >= lobby.Capacity)
                    {
                        reason = MultiplayerReason.SessionFull;
                    }
                    else
                    {
                        lobby.Members.Add(this);
                    }
                }

                onCompleted?.Invoke(reason);
            });
        }

        public void LeaveLobby(LobbyId lobbyId)
        {
            if (_machine != null)
            {
                _machine.LeaveLobby(lobbyId);
                return;
            }

            LocalLobby lobby = _network.FindLobby(lobbyId);
            if (lobby != null)
            {
                _network.RemoveMember(lobby, this);
            }
        }

        public string GetLobbyData(LobbyId lobbyId, string key)
        {
            if (_machine != null)
            {
                return _machine.GetLobbyData(lobbyId, key);
            }

            LocalLobby lobby = _network.FindLobby(lobbyId);
            return lobby != null && key != null && lobby.Data.TryGetValue(key, out string value) ? value : null;
        }

        public bool SetLobbyData(LobbyId lobbyId, string key, string value)
        {
            if (_machine != null)
            {
                return _machine.SetLobbyData(lobbyId, key, value);
            }

            LocalLobby lobby = FindOwnedLobby(lobbyId);
            if (lobby == null || string.IsNullOrEmpty(key))
            {
                return false;
            }

            lobby.Data[key] = value ?? string.Empty;
            return true;
        }

        public bool SetLobbyJoinable(LobbyId lobbyId, bool joinable)
        {
            if (_machine != null)
            {
                return _machine.SetLobbyJoinable(lobbyId, joinable);
            }

            LocalLobby lobby = FindOwnedLobby(lobbyId);
            if (lobby == null)
            {
                return false;
            }

            lobby.IsJoinable = joinable;
            return true;
        }

        public bool SetLobbyCapacity(LobbyId lobbyId, int capacity)
        {
            if (_machine != null)
            {
                return _machine.SetLobbyCapacity(lobbyId, capacity);
            }

            LocalLobby lobby = FindOwnedLobby(lobbyId);
            if (lobby == null || capacity < 1)
            {
                return false;
            }

            lobby.Capacity = capacity;
            return true;
        }

        public PlayerId GetLobbyOwner(LobbyId lobbyId)
        {
            if (_machine != null)
            {
                return _machine.GetLobbyOwner(lobbyId);
            }

            LocalLobby lobby = _network.FindLobby(lobbyId);
            return lobby != null ? lobby.Owner.LocalPlayerId : PlayerId.None;
        }

        public bool IsLobbyMember(LobbyId lobbyId, PlayerId player)
        {
            if (_machine != null)
            {
                return _machine.IsLobbyMember(lobbyId, player);
            }

            LocalLobby lobby = _network.FindLobby(lobbyId);
            if (lobby == null)
            {
                return false;
            }

            for (int i = 0; i < lobby.Members.Count; i++)
            {
                if (lobby.Members[i].LocalPlayerId == player)
                {
                    return true;
                }
            }

            return false;
        }

        private LocalLobby FindOwnedLobby(LobbyId lobbyId)
        {
            if (_isDisposed)
            {
                return null;
            }

            LocalLobby lobby = _network.FindLobby(lobbyId);
            return lobby != null && lobby.Owner == this ? lobby : null;
        }
        #endregion

        #region Connection
        public bool StartListening()
        {
            if (_machine != null)
            {
                return _machine.StartListening();
            }

            if (_isDisposed)
            {
                return false;
            }

            _isListening = true;
            return true;
        }

        public void StopListening()
        {
            if (_machine != null)
            {
                _machine.StopListening();
                return;
            }

            _isListening = false;
        }

        public ConnectionId Connect(PlayerId host)
        {
            if (_machine != null)
            {
                return _machine.Connect(host);
            }

            if (_isDisposed || !host.IsValid)
            {
                return ConnectionId.None;
            }

            double now = _network.Now;
            LocalConnection local = new LocalConnection
            {
                Id = _network.AllocateConnectionId(),
                Owner = this,
                RemotePlayer = host,
                IsOpen = true,
                Meter = new ConnectionMeter(now),
            };
            _connections[local.Id] = local;

            LocalBackend target = _network.FindListening(host, this);
            if (target == null || IsCutOff(now) || target.IsCutOff(now))
            {
                local.IsOpen = false;
                _events.Enqueue(new PendingEvent
                {
                    Kind = EventKind.Disconnected,
                    Connection = local.Id,
                    Player = host,
                    DisconnectKind = BackendDisconnectKind.ConnectFailed,
                });
                return local.Id;
            }

            LocalConnection remote = new LocalConnection
            {
                Id = _network.AllocateConnectionId(),
                Owner = target,
                RemotePlayer = LocalPlayerId,
                IsOpen = true,
                Remote = local,
                Meter = new ConnectionMeter(now),
            };
            local.Remote = remote;
            target._connections[remote.Id] = remote;

            target._events.Enqueue(new PendingEvent { Kind = EventKind.Connected, Connection = remote.Id, Player = LocalPlayerId, IsIncoming = true });
            _events.Enqueue(new PendingEvent { Kind = EventKind.Connected, Connection = local.Id, Player = host, IsIncoming = false });
            return local.Id;
        }

        public bool Send(ConnectionId connection, ReadOnlySpan<byte> data, DeliveryMode mode)
        {
            if (_machine != null)
            {
                return _machine.Send(connection, data, mode);
            }

            if (_isDisposed || !_connections.TryGetValue(connection, out LocalConnection local) || data.Length > MaxPacketBytes)
            {
                return false;
            }

            // The other side closed it; the close is on its way with the reason, and the data goes nowhere.
            if (!local.IsOpen || local.Remote == null || local.Remote.Owner._isDisposed)
            {
                return true;
            }

            if (_failingSends > 0)
            {
                _failingSends--;
                return false;
            }

            LocalConnection remote = local.Remote;
            NetworkSimulation simulation = _network.Simulation;
            double now = _network.Now;
            double due = now;
            int resends = 0;
            local.Meter.AddSent(data.Length);

            bool isCut = IsLinkCut(local, now);
            if (isCut && mode != DeliveryMode.Reliable)
            {
                remote.Meter.AddInbound(1, 1);
                return true;
            }

            if (simulation.IsActive)
            {
                if (mode == DeliveryMode.Reliable)
                {
                    resends = simulation.ReliableResends();
                    due += simulation.PacketDelay() + resends * simulation.ResendSeconds;
                }
                else if (simulation.DropsPacket())
                {
                    remote.Meter.AddInbound(1, 1);
                    return true;
                }
                else
                {
                    due += simulation.PacketDelay();
                }
            }

            if (isCut)
            {
                due = Math.Max(due, LinkCutEnd(local));
            }

            remote.Meter.AddInbound(1 + resends, resends);

            byte[] buffer = Packets.Rent(Math.Max(1, data.Length));
            data.CopyTo(buffer);

            remote.Owner.Arrive(new PendingEvent
            {
                Kind = EventKind.Received,
                Connection = remote.Id,
                Player = LocalPlayerId,
                Data = buffer,
                Length = data.Length,
                Target = remote,
            }, due, isOrdered: mode == DeliveryMode.Reliable, now);
            return true;
        }

        public bool TryGetConnectionQuality(ConnectionId connection, out ConnectionQuality quality)
        {
            if (_machine != null)
            {
                return _machine.TryGetConnectionQuality(connection, out quality);
            }

            if (!_isDisposed && _connections.TryGetValue(connection, out LocalConnection local) && local.IsOpen)
            {
                quality = local.Meter.Quality;
                return true;
            }

            quality = default;
            return false;
        }

        /// <summary>
        /// Takes what crossed the network to one of this backend's connections, to hand on in the
        /// next update once its simulated delay is over. Reliable packets and the close keep their
        /// order, so one that a resend holds back holds back the ones after it; nothing waits
        /// behind an unreliable packet.
        /// </summary>
        private void Arrive(PendingEvent pending, double due, bool isOrdered, double now)
        {
            LocalConnection target = pending.Target;
            pending.IsOrdered = isOrdered;
            if (isOrdered)
            {
                due = Math.Max(due, target.LastOrderedDue);
                target.LastOrderedDue = due;
            }

            // Without a delay, and with nothing held back that it must not overtake, it takes the
            // way every other event takes.
            if (due <= now && target.DelayedCount == 0)
            {
                _events.Enqueue(pending);
                return;
            }

            target.DelayedCount++;
            _delayed.Push(due, pending);
        }

        /// <summary>
        /// Sends a probe, or the answer to one, across the simulated network, which can lose or
        /// delay it as it does an unreliable packet.
        /// </summary>
        private void SendProbe(LocalConnection local, EventKind kind, double roundTrip)
        {
            LocalConnection remote = local.Remote;
            if (remote == null || !remote.IsOpen || remote.Owner._isDisposed)
            {
                return;
            }

            double now = _network.Now;
            NetworkSimulation simulation = _network.Simulation;
            if (IsLinkCut(local, now) || (simulation.IsActive && simulation.DropsProbe()))
            {
                remote.Meter.AddInbound(1, 1);
                return;
            }

            remote.Meter.AddInbound(1, 0);
            double delay = simulation.IsActive ? simulation.ProbeDelay() : 0.0;
            remote.Owner.Arrive(new PendingEvent
            {
                Kind = kind,
                Connection = remote.Id,
                Player = LocalPlayerId,
                Target = remote,
                RoundTrip = roundTrip + delay,
            }, now + delay, isOrdered: false, now);
        }

        public void Disconnect(ConnectionId connection, int reasonCode)
        {
            if (_machine != null)
            {
                _machine.Disconnect(connection, reasonCode);
                return;
            }

            if (!_connections.TryGetValue(connection, out LocalConnection local))
            {
                return;
            }

            _connections.Remove(connection);
            CloseRemoteSide(local, BackendDisconnectKind.ClosedByPeer, reasonCode);
        }

        private void CloseRemoteSide(LocalConnection local, BackendDisconnectKind kind, int reasonCode)
        {
            bool wasOpen = local.IsOpen;
            local.IsOpen = false;

            LocalConnection remote = local.Remote;
            if (!wasOpen || remote == null || !remote.IsOpen)
            {
                return;
            }

            remote.IsOpen = false;
            double now = _network.Now;

            // Nothing leaves a backend that an outage cut off, so the other side only finds the
            // connection gone, as a reset reports it on a machine-wide network.
            if (IsCutOff(now))
            {
                remote.Owner.Arrive(new PendingEvent
                {
                    Kind = EventKind.Disconnected,
                    Connection = remote.Id,
                    Player = LocalPlayerId,
                    DisconnectKind = BackendDisconnectKind.ConnectionLost,
                    Target = remote,
                }, now, isOrdered: false, now);
                return;
            }

            // The close travels like any packet, after the reliable ones sent before it, and
            // waits for the end of an outage that cuts the other side off.
            NetworkSimulation simulation = _network.Simulation;
            double due = simulation.IsActive ? now + simulation.PacketDelay() : now;
            if (IsLinkCut(local, now))
            {
                due = Math.Max(due, LinkCutEnd(local));
            }

            remote.Owner.Arrive(new PendingEvent
            {
                Kind = EventKind.Disconnected,
                Connection = remote.Id,
                Player = LocalPlayerId,
                DisconnectKind = kind,
                ReasonCode = reasonCode,
                Target = remote,
            }, due, isOrdered: true, now);
        }
        #endregion

        #region Platform
        /// <summary>
        /// The local network has no avatars, so every request answers
        /// <see cref="MultiplayerReason.NotSupported"/> on the next update.
        /// </summary>
        public void LoadAvatar(PlayerId player, AvatarSize size, Action<MultiplayerReason, BackendAvatar> onCompleted)
        {
            if (_machine != null)
            {
                _machine.LoadAvatar(onCompleted);
                return;
            }

            EnqueueCallback(() => onCompleted?.Invoke(MultiplayerReason.NotSupported, default));
        }
        #endregion

        #region Events
        /// <summary>
        /// Tells this member that the lobby passed on. The news comes after the reliable packets
        /// and the closes already on their way to this player's session connections, even while
        /// the simulated network still holds them back: a client that heard first that its host
        /// left the lobby would end its session and drop them, so a host that leaves would lose
        /// its last messages.
        /// </summary>
        internal void EnqueueOwnerChanged(LobbyId lobby, PlayerId newOwner)
        {
            if (_isDisposed)
            {
                return;
            }

            PendingEvent pending = new PendingEvent { Kind = EventKind.OwnerChanged, Lobby = lobby, Player = newOwner };

            // A connection an outage cuts delivers nothing meanwhile, so the lobby's news does not
            // wait for it.
            double now = _network.Now;
            LocalConnection behind = null;
            foreach (LocalConnection connection in _connections.Values)
            {
                if (!IsLinkCut(connection, now) && (behind == null || connection.LastOrderedDue > behind.LastOrderedDue))
                {
                    behind = connection;
                }
            }

            if (behind == null)
            {
                _events.Enqueue(pending);
                return;
            }

            pending.Target = behind;
            Arrive(pending, now, isOrdered: true, now);
        }

        #region Outage
        private bool IsCutOff(double now) => now < _outageEnd;

        /// <summary>A connection's link is cut while an outage cuts off either side.</summary>
        private static bool IsLinkCut(LocalConnection connection, double now)
        {
            return connection.Owner.IsCutOff(now) || (connection.Remote != null && connection.Remote.Owner.IsCutOff(now));
        }

        /// <summary>When the outages that cut a connection's link are over.</summary>
        private static double LinkCutEnd(LocalConnection connection)
        {
            double end = connection.Owner._outageEnd;
            return connection.Remote != null ? Math.Max(end, connection.Remote.Owner._outageEnd) : end;
        }

        private static void MarkCut(LocalConnection connection, double now)
        {
            if (!connection.IsCut)
            {
                connection.IsCut = true;
                connection.CutSince = now;
            }
        }

        /// <summary>
        /// Holds back what reaches a connection whose link an outage cuts: a reliable packet or a
        /// close waits for the outage to end, also when the outage grew since it was sent, and an
        /// unreliable packet or a probe is lost. True when it was not handed on.
        /// </summary>
        private bool HoldsBack(PendingEvent pending, double now)
        {
            LocalConnection target = pending.Target;
            if (target == null || !IsLinkCut(target, now))
            {
                return false;
            }

            switch (pending.Kind)
            {
                case EventKind.Received:
                case EventKind.Disconnected:
                    if (!pending.IsOrdered)
                    {
                        if (pending.Kind == EventKind.Disconnected)
                        {
                            return false;
                        }

                        Packets.Return(pending.Data);
                        return true;
                    }

                    double end = LinkCutEnd(target);
                    target.LastOrderedDue = Math.Max(target.LastOrderedDue, end);
                    target.DelayedCount++;
                    _delayed.Push(end, pending);
                    return true;

                case EventKind.Probe:
                case EventKind.ProbeReply:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Reports a connection lost once an outage kept it silent for the network's connection
        /// timeout. Each side of a connection notices on its own, as each side's timeout would.
        /// </summary>
        private void ReportSilencedConnections(double now)
        {
            float timeout = _network.ConnectionTimeoutSeconds;
            foreach (LocalConnection connection in _connections.Values)
            {
                if (!connection.IsCut || connection.IsLossReported)
                {
                    continue;
                }

                if (!IsLinkCut(connection, now))
                {
                    connection.IsCut = false;
                    continue;
                }

                if (timeout > 0f && now - connection.CutSince >= timeout)
                {
                    connection.IsLossReported = true;
                    connection.IsOpen = false;
                    _events.Enqueue(new PendingEvent
                    {
                        Kind = EventKind.Disconnected,
                        Connection = connection.Id,
                        Player = connection.RemotePlayer,
                        DisconnectKind = BackendDisconnectKind.ConnectionLost,
                    });
                }
            }
        }
        #endregion

        private void EnqueueCallback(Action callback)
        {
            if (_isDisposed)
            {
                return;
            }

            _events.Enqueue(new PendingEvent { Kind = EventKind.Callback, Callback = callback });
        }

        private void Dispatch(PendingEvent pending)
        {
            switch (pending.Kind)
            {
                case EventKind.Callback:
                    pending.Callback();
                    break;

                case EventKind.Connected:
                    if (_connections.ContainsKey(pending.Connection))
                    {
                        _listener?.OnConnected(pending.Connection, pending.Player, pending.IsIncoming);
                    }
                    break;

                case EventKind.Disconnected:
                    // A connection this side closed itself reports nothing more.
                    if (_connections.Remove(pending.Connection))
                    {
                        _listener?.OnDisconnected(pending.Connection, pending.Player, pending.DisconnectKind, pending.ReasonCode);
                    }
                    break;

                case EventKind.Received:
                    try
                    {
                        if (_connections.TryGetValue(pending.Connection, out LocalConnection receiving))
                        {
                            receiving.Meter.AddReceived(pending.Length);
                            _listener?.OnReceived(pending.Connection, pending.Player, new ReadOnlySpan<byte>(pending.Data, 0, pending.Length));
                        }
                    }
                    finally
                    {
                        Packets.Return(pending.Data);
                    }
                    break;

                case EventKind.Probe:
                    if (_connections.TryGetValue(pending.Connection, out LocalConnection probed) && probed.IsOpen)
                    {
                        SendProbe(probed, EventKind.ProbeReply, pending.RoundTrip);
                    }
                    break;

                case EventKind.ProbeReply:
                    if (_connections.TryGetValue(pending.Connection, out LocalConnection answered))
                    {
                        answered.Meter.AddRoundTrip(pending.RoundTrip);
                    }
                    break;

                case EventKind.OwnerChanged:
                    _listener?.OnLobbyOwnerChanged(pending.Lobby, pending.Player);
                    break;

                case EventKind.JoinRequested:
                    if (_listener != null)
                    {
                        _listener.OnLobbyJoinRequested(pending.Lobby);
                    }
                    else
                    {
                        _heldJoinRequest = pending.Lobby;
                    }
                    break;
            }
        }

        private void Shutdown()
        {
            if (_isDisposed)
            {
                return;
            }

            foreach (LocalConnection connection in _connections.Values)
            {
                CloseRemoteSide(connection, BackendDisconnectKind.ConnectionLost, 0);
            }

            _connections.Clear();
            _isListening = false;

            for (int i = _network.Lobbies.Count - 1; i >= 0; i--)
            {
                LocalLobby lobby = _network.Lobbies[i];
                if (lobby.Members.Contains(this))
                {
                    _network.RemoveMember(lobby, this);
                }
            }

            _isDisposed = true;
            _network.Backends.Remove(this);

            while (_events.Count > 0)
            {
                PendingEvent pending = _events.Dequeue();
                if (pending.Kind == EventKind.Received)
                {
                    Packets.Return(pending.Data);
                }
            }

            while (_delayed.Count > 0)
            {
                PendingEvent pending = _delayed.Pop();
                if (pending.Kind == EventKind.Received)
                {
                    Packets.Return(pending.Data);
                }
            }

            _listener = null;
        }
        #endregion
    }
}
