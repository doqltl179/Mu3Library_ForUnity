using System;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.DI;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using UnityEngine;

namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// Syncs registered states over an <see cref="IMultiplayerMessenger"/>, as
    /// <see cref="IStateSyncManager"/> describes. It sends on its own message type, registered with
    /// <see cref="MessagePolicy.NoClientRelay"/>, so no client reaches another client except
    /// through the host.
    /// </summary>
    /// <remarks>
    /// A steady tick allocates nothing: the manager writes each state at most once a tick for its
    /// changes and once whole, and copies those bytes into each player's message. Create it after
    /// the multiplayer manager, dispose it before, and call <see cref="Update"/> every frame after
    /// the multiplayer manager's.
    /// </remarks>
    public sealed partial class StateSyncManager : IStateSyncManager, IUpdatable, IDisposable
    {
        private const int MaxPlayers = MultiplayerLimits.MaxCapacity;

        // An unreliable update this many ticks older than the last one a copy read, about three
        // seconds at the default interval, comes from a sender whose ticks started again rather
        // than from a packet that arrived late, so the copy reads it.
        private const int ReorderWindowTicks = 64;

        // Distinct failures logged per session, by what failed, the state, and the exception
        // type; past this many the manager logs no more, so a flood of malformed bundles cannot
        // flood the log.
        private const int MaxLoggedFailures = 32;

        /// <summary>What a state's owner keeps for one player it sends to.</summary>
        private struct Peer
        {
            /// <summary>The player has no copy, or missed a change, so the next send is whole.</summary>
            public bool NeedsFull;

            /// <summary>The priority the state gathered while it waited for this player.</summary>
            public float Claim;

            /// <summary>When an unreliable state goes whole to this player again, changed or not.</summary>
            public double FullDueAt;
        }

        private sealed class Entry
        {
            public ISyncState State;
            public uint Id;
            public PlayerId Owner;
            public DeliveryMode Mode;
            public float Priority;
            public SyncInterestFilter Interest;
            public int Order;
            public readonly Peer[] Peers = new Peer[MaxPlayers];

            // On a receiver: the sender's tick of the last update this copy read.
            public bool HasLastTick;
            public uint LastTick;

            // On the host, for a state a client owns: an update arrived since the last tick, and
            // one ever arrived in this game, so the host has something to send on.
            public bool Received;
            public bool HasReceived;

            // Within a tick: whether this player sends the state, whether it changed, and where
            // its writes are in the tick's bytes, -1 before they are written.
            public bool Sends;
            public bool IsRelay;
            public bool Changed;
            public int FullOffset;
            public int FullLength;
            public bool FullFailed;
            public int DeltaOffset;
            public int DeltaLength;
            public bool DeltaFailed;

            // A state too large for a tick is logged once a session.
            public bool LoggedTooLarge;
        }

        private struct Candidate
        {
            public Entry Entry;
            public bool Full;
            public float Claim;
            public int Order;

            // Packed into this tick's message to the player.
            public bool Sent;
        }

        private struct Stats
        {
            public uint LastTick;
            public int LastTickBytes;
            public int LastTickStates;
            public int LastTickDeferred;
            public int MaxTickBytes;
            public long TotalBytes;
        }

        private readonly IMultiplayerManager _session;
        private readonly IMultiplayerMessenger _messenger;
        private readonly ushort _messageType;
        private readonly float _tickInterval;
        private readonly int _maxBytesPerTick;
        private readonly double _fullStateInterval;
        private readonly Func<double> _clock;

        private readonly Dictionary<uint, Entry> _byId = new Dictionary<uint, Entry>();
        private readonly List<Entry> _entries = new List<Entry>();
        private int _nextOrder = 0;

        // The players this one sends to, by slot: on the host each connected client, on a client
        // the host. Each entry keeps its per-player data under the same slot.
        private readonly PlayerId[] _slots = new PlayerId[MaxPlayers];
        private readonly bool[] _seen = new bool[MaxPlayers];
        private readonly Stats[] _stats = new Stats[MaxPlayers];

        private readonly MessageWriter _stateWriter = new MessageWriter();
        private readonly SyncBuffer _tickBytes = new SyncBuffer(1024);
        private readonly SyncBuffer _reliable;
        private readonly SyncBuffer _unreliable;
        private Candidate[] _candidates = new Candidate[16];

        private readonly HashSet<(string What, uint Id, Type Exception)> _loggedFailures = new HashSet<(string What, uint Id, Type Exception)>();
        private bool _loggedRateLimits = false;

        private bool _isDisposed = false;
        private bool _isTicking = false;
        private bool _isActive = false;
        private LobbyId _activeLobby = LobbyId.None;
        private PlayerId _activeHost = PlayerId.None;
        private double _nextTickAt = 0.0;

        private uint _tick = 0;
        public uint Tick => _tick;

        public int Count => _entries.Count;
        public float TickIntervalSeconds => _tickInterval;
        public int MaxBytesPerTick => _maxBytesPerTick;

        /// <summary>The session this manager syncs, for the editor's Multiplayer Debugger window.</summary>
        internal IMultiplayerManager Session => _session;

        private double Now => _clock != null ? _clock() : Time.realtimeSinceStartupAsDouble;



        /// <summary>Syncs over a <see cref="MultiplayerManager"/>, which is both the session and the messenger.</summary>
        public StateSyncManager(MultiplayerManager manager, StateSyncConfig config) : this(manager, manager, config)
        {
        }

        /// <summary>
        /// Registers the config's message type on <paramref name="messenger"/>, which belongs to
        /// <paramref name="session"/>, as every player of the game does.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <see cref="StateSyncConfig.TickIntervalSeconds"/> is not above 0 or not finite,
        /// <see cref="StateSyncConfig.MaxBytesPerTick"/> is below
        /// <see cref="StateSyncConfig.MinBytesPerTick"/> or above the messenger's
        /// <see cref="IMultiplayerMessenger.MaxMessageSize"/>, or
        /// <see cref="StateSyncConfig.FullStateIntervalSeconds"/> is below 0 or not finite.
        /// </exception>
        /// <exception cref="InvalidOperationException">The messenger refused the message type, as one already registered.</exception>
        public StateSyncManager(IMultiplayerManager session, IMultiplayerMessenger messenger, StateSyncConfig config)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (!(config.TickIntervalSeconds > 0f) || float.IsInfinity(config.TickIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(config), config.TickIntervalSeconds, "The tick interval must be a finite number of seconds above 0.");
            }

            if (config.MaxBytesPerTick < StateSyncConfig.MinBytesPerTick || config.MaxBytesPerTick > messenger.MaxMessageSize)
            {
                throw new ArgumentOutOfRangeException(nameof(config), config.MaxBytesPerTick,
                    $"The bytes per tick must be from {StateSyncConfig.MinBytesPerTick} to the messenger's largest message, {messenger.MaxMessageSize}.");
            }

            if (!(config.FullStateIntervalSeconds >= 0f) || float.IsInfinity(config.FullStateIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(config), config.FullStateIntervalSeconds, "The full state interval must be a finite number of seconds, 0 or more.");
            }

            _messageType = config.MessageType;
            _tickInterval = config.TickIntervalSeconds;
            _maxBytesPerTick = config.MaxBytesPerTick;
            _fullStateInterval = config.FullStateIntervalSeconds;
            _clock = config.Clock;
            _reliable = new SyncBuffer(_maxBytesPerTick);
            _unreliable = new SyncBuffer(_maxBytesPerTick);

            MultiplayerResult registered = messenger.RegisterMessage(_messageType, OnMessage, MessagePolicy.NoClientRelay);
            if (!registered.IsSuccess)
            {
                throw new InvalidOperationException($"State sync could not register message type {_messageType}: {registered}.");
            }

            StateSyncManagerRegistry.Add(this);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _messenger.UnregisterMessage(_messageType);
            StateSyncManagerRegistry.Remove(this);
            _entries.Clear();
            _byId.Clear();
        }

        #region Registration
        public MultiplayerResult Register(ISyncState state, PlayerId owner = default, DeliveryMode deliveryMode = DeliveryMode.Unreliable, float priority = 1f, SyncInterestFilter interest = null)
        {
            if (_isDisposed || _isTicking)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidState);
            }

            if (state == null || _byId.ContainsKey(state.Id)
                || !(priority > 0f) || float.IsInfinity(priority)
                || (deliveryMode != DeliveryMode.Reliable && deliveryMode != DeliveryMode.Unreliable))
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
            }

            Entry entry = new Entry
            {
                State = state,
                Id = state.Id,
                Owner = owner,
                Mode = deliveryMode,
                Priority = priority,
                Interest = interest,
                Order = _nextOrder++,
            };

            // A state new to the game goes whole to every player at the next tick.
            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                entry.Peers[slot].NeedsFull = true;
            }

            _byId.Add(entry.Id, entry);
            _entries.Add(entry);

            if (_isActive && IsClientOwned(entry))
            {
                CheckRateLimitsOnce();
            }

            return MultiplayerResult.Success;
        }

        public bool Unregister(uint id)
        {
            if (_isDisposed || _isTicking || !_byId.TryGetValue(id, out Entry entry))
            {
                return false;
            }

            _byId.Remove(id);
            _entries.Remove(entry);
            return true;
        }

        public bool TryGetState(uint id, out ISyncState state)
        {
            if (_byId.TryGetValue(id, out Entry entry))
            {
                state = entry.State;
                return true;
            }

            state = null;
            return false;
        }

        /// <summary>A state that a client sends: one whose owner is a player other than the host.</summary>
        private bool IsClientOwned(Entry entry)
        {
            return entry.Owner.IsValid && entry.Owner != _session.HostId;
        }
        #endregion

        #region Rate Limits
        public MultiplayerResult CheckRateLimits(SessionRules rules)
        {
            return FitsRateLimits(rules, out _, out _, out _) ? MultiplayerResult.Success : MultiplayerResult.Failure(MultiplayerReason.InvalidArgument);
        }

        private bool FitsRateLimits(SessionRules rules, out int ticksPerSecond, out long messagesPerSecond, out long bytesPerSecond)
        {
            bool reliable = false;
            bool unreliable = false;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                if (entry.Owner.IsValid && entry.Owner != _session.HostId)
                {
                    reliable |= entry.Mode == DeliveryMode.Reliable;
                    unreliable |= entry.Mode == DeliveryMode.Unreliable;
                }
            }

            int modes = (reliable ? 1 : 0) + (unreliable ? 1 : 0);

            // A tick runs once its interval passed since the last one was due, so a late frame
            // can bring two closer together; a second holds at most one more than its share.
            ticksPerSecond = (int)Math.Ceiling(1.0 / _tickInterval) + 1;
            messagesPerSecond = (long)ticksPerSecond * modes;
            bytesPerSecond = modes == 0 ? 0L : (long)ticksPerSecond * (_maxBytesPerTick + modes * MessageHeader.ToHostSize);

            return (rules.MaxMessagesPerSecond <= 0 || messagesPerSecond <= rules.MaxMessagesPerSecond)
                && (rules.MaxBytesPerSecond <= 0 || bytesPerSecond <= rules.MaxBytesPerSecond);
        }

        private void CheckRateLimitsOnce()
        {
            if (_loggedRateLimits)
            {
                return;
            }

            SessionRules rules = _session.Rules;
            if (FitsRateLimits(rules, out int ticks, out long messages, out long bytes))
            {
                return;
            }

            _loggedRateLimits = true;
            Debug.LogWarning(string.Format(CultureInfo.InvariantCulture,
                "[StateSync] A client may send up to {0} sync messages and {1} bytes a second ({2} ticks of {3} bytes and their headers), "
                + "past the session's rate limits of {4} messages and {5} bytes a second (0 is no limit), so the host drops some of them. "
                + "Raise the limits, or lengthen the tick interval or lower the bytes per tick.",
                messages, bytes, ticks, _maxBytesPerTick, rules.MaxMessagesPerSecond, rules.MaxBytesPerSecond));
        }
        #endregion

        #region Stats
        public void GetPlayerStats(List<SyncPlayerStats> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            if (!_isActive)
            {
                return;
            }

            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                if (_slots[slot].IsValid)
                {
                    Stats stats = _stats[slot];
                    results.Add(new SyncPlayerStats(_slots[slot], stats.LastTick, stats.LastTickBytes, stats.LastTickStates,
                        stats.LastTickDeferred, stats.MaxTickBytes, stats.TotalBytes));
                }
            }
        }
        #endregion

        #region Session
        public void Update()
        {
            if (_isDisposed)
            {
                return;
            }

            if (_session.State != SessionState.InGame)
            {
                if (_isActive)
                {
                    EndSession();
                }

                return;
            }

            double now = Now;
            if (!_isActive || _session.CurrentLobbyId != _activeLobby || _session.HostId != _activeHost)
            {
                BeginSession(now);
            }

            if (now < _nextTickAt)
            {
                return;
            }

            _nextTickAt += _tickInterval;
            if (_nextTickAt <= now)
            {
                _nextTickAt = now + _tickInterval;
            }

            RunTick(now);
        }

        /// <summary>A game starts: every player gets every state whole, and every copy reads anew.</summary>
        private void BeginSession(double now)
        {
            _isActive = true;
            _activeLobby = _session.CurrentLobbyId;
            _activeHost = _session.HostId;
            _nextTickAt = now;
            _loggedFailures.Clear();
            _loggedRateLimits = false;

            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                _slots[slot] = PlayerId.None;
                _stats[slot] = default;
            }

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                entry.HasLastTick = false;
                entry.Received = false;
                entry.HasReceived = false;
                entry.LoggedTooLarge = false;
            }

            CheckRateLimitsOnce();
        }

        private void EndSession()
        {
            _isActive = false;
            _activeLobby = LobbyId.None;
            _activeHost = PlayerId.None;
            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                _slots[slot] = PlayerId.None;
            }
        }

        /// <summary>
        /// Matches the slots to the players this one sends to now. A player new to a slot, back
        /// from being away, or new to the game gets every state whole.
        /// </summary>
        private void RefreshSlots()
        {
            Array.Clear(_seen, 0, MaxPlayers);
            if (_session.IsHost)
            {
                PlayerId local = _session.LocalPlayerId;
                IReadOnlyList<Participant> participants = _session.Participants;
                for (int i = 0; i < participants.Count; i++)
                {
                    Participant participant = participants[i];
                    if (participant.Id != local && participant.IsConnected)
                    {
                        Take(participant.Id);
                    }
                }
            }
            else if (_session.HostId.IsValid)
            {
                Take(_session.HostId);
            }

            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                if (!_seen[slot])
                {
                    _slots[slot] = PlayerId.None;
                }
            }
        }

        private void Take(PlayerId player)
        {
            int free = -1;
            for (int slot = 0; slot < MaxPlayers; slot++)
            {
                if (_slots[slot] == player)
                {
                    _seen[slot] = true;
                    return;
                }

                if (free < 0 && !_slots[slot].IsValid && !_seen[slot])
                {
                    free = slot;
                }
            }

            if (free < 0)
            {
                return;
            }

            _slots[free] = player;
            _seen[free] = true;
            _stats[free] = default;
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                entry.Peers[free] = new Peer { NeedsFull = true };

                // A client that joins again may come from a game started anew, whose ticks start again.
                if (entry.Owner == player)
                {
                    entry.HasLastTick = false;
                }
            }
        }
        #endregion

        #region Logging
        private void LogFailure(string what, Exception exception, uint id)
        {
            if (_loggedFailures.Count >= MaxLoggedFailures || !_loggedFailures.Add((what, id, exception.GetType())))
            {
                return;
            }

            if (exception is PayloadFormatException)
            {
                Debug.LogWarning($"[StateSync] {what} of state {id} failed: {exception.Message} Later failures of the kind go unlogged until the game ends.");
                return;
            }

            Debug.LogException(exception);
            Debug.LogWarning($"[StateSync] {what} of state {id} threw {exception.GetType().Name}. Later failures of the kind go unlogged until the game ends.");
        }

        private void LogOnce(string what, string message)
        {
            if (_loggedFailures.Count >= MaxLoggedFailures || !_loggedFailures.Add((what, 0u, null)))
            {
                return;
            }

            Debug.LogWarning($"[StateSync] {message} Later ones go unlogged until the game ends.");
        }
        #endregion
    }
}
