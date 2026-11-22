using System.Collections.Generic;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Sync;

namespace Mu3Library.Editor.Window
{
    /// <summary>
    /// What the <see cref="MultiplayerDebuggerWindow"/> shows of one manager, as it was when last
    /// captured: the session, its players with each one's ping as the host measures it and the
    /// quality of this player's own connection to each, its message types with their counts and
    /// their rates since the capture before, and what each state sync manager on it sent each
    /// player.
    /// </summary>
    internal sealed class MultiplayerSessionView
    {
        internal struct PlayerRow
        {
            public Participant Participant;

            /// <summary>
            /// The round trip between the host and the player as the host measures it
            /// (<see cref="IMultiplayerManager.TryGetParticipantPing"/>), which every player reads
            /// for every player; the host's own is 0. False for a player away while the host keeps
            /// their slot and, on a client, for a player the host has not reported yet.
            /// </summary>
            public bool HasHostPing;
            public int HostPingMilliseconds;

            /// <summary>
            /// This player's own measure of its connection to the player
            /// (<see cref="IMultiplayerManager.TryGetConnectionQuality"/>). False for this player
            /// itself and, on a client, for the other clients.
            /// </summary>
            public bool HasQuality;
            public ConnectionQuality Quality;
        }

        internal struct MessageRow
        {
            public MessageCounts Counts;
            public float SentPerSecond;
            public float ReceivedPerSecond;
            public float SentBytesPerSecond;
            public float ReceivedBytesPerSecond;
        }

        /// <summary>What one state sync manager on the manager sent one player, with its bytes a second since the capture before.</summary>
        internal struct SyncPlayerRow
        {
            public SyncPlayerStats Stats;
            public float BytesPerSecond;
        }

        /// <summary>One state sync manager that runs on the manager.</summary>
        internal sealed class SyncView
        {
            private readonly List<SyncPlayerStats> _stats = new List<SyncPlayerStats>();
            private readonly Dictionary<PlayerId, long> _previousBytes = new Dictionary<PlayerId, long>();

            public StateSyncManager Sync { get; }
            public uint Tick { get; private set; }
            public int StateCount { get; private set; }
            public List<SyncPlayerRow> Players { get; } = new List<SyncPlayerRow>();

            public SyncView(StateSyncManager sync)
            {
                Sync = sync;
            }

            public void Capture(double elapsed)
            {
                Tick = Sync.Tick;
                StateCount = Sync.Count;
                Sync.GetPlayerStats(_stats);

                Players.Clear();
                for (int i = 0; i < _stats.Count; i++)
                {
                    SyncPlayerStats stats = _stats[i];
                    SyncPlayerRow row = new SyncPlayerRow { Stats = stats };
                    if (elapsed > 0.0 && _previousBytes.TryGetValue(stats.Player, out long previous) && stats.TotalBytes >= previous)
                    {
                        row.BytesPerSecond = (float)((stats.TotalBytes - previous) / elapsed);
                    }

                    Players.Add(row);
                }

                _previousBytes.Clear();
                for (int i = 0; i < _stats.Count; i++)
                {
                    _previousBytes[_stats[i].Player] = _stats[i].TotalBytes;
                }
            }
        }

        private readonly List<MessageCounts> _counts = new List<MessageCounts>();
        private readonly Dictionary<ushort, MessageCounts> _previousCounts = new Dictionary<ushort, MessageCounts>();
        private readonly List<SyncView> _previousSyncs = new List<SyncView>();
        private double _previousCapture = double.NaN;

        /// <summary>The name the window shows the manager under.</summary>
        public string Source { get; }
        public IMultiplayerManager Manager { get; }

        public double CapturedAt { get; private set; } = double.NaN;
        public SessionState State { get; private set; }
        public bool IsHost { get; private set; }
        public PlayerId LocalPlayerId { get; private set; }
        public LobbyId Lobby { get; private set; }
        public SessionRules Rules { get; private set; }

        public List<PlayerRow> Players { get; } = new List<PlayerRow>();

        /// <summary>By message type.</summary>
        public List<MessageRow> Messages { get; } = new List<MessageRow>();

        /// <summary>The state sync managers that run on the manager, oldest first.</summary>
        public List<SyncView> Syncs { get; } = new List<SyncView>();



        public MultiplayerSessionView(string source, IMultiplayerManager manager)
        {
            Source = source;
            Manager = manager;
        }

        public void Capture(double now)
        {
            CapturedAt = now;
            State = Manager.State;
            IsHost = Manager.IsHost;
            LocalPlayerId = Manager.LocalPlayerId;
            Lobby = Manager.CurrentLobbyId;
            Rules = Manager.Rules;

            Players.Clear();
            IReadOnlyList<Participant> participants = Manager.Participants;
            for (int i = 0; i < participants.Count; i++)
            {
                PlayerRow row = new PlayerRow { Participant = participants[i] };
                row.HasHostPing = Manager.TryGetParticipantPing(row.Participant.Id, out row.HostPingMilliseconds);
                row.HasQuality = Manager.TryGetConnectionQuality(row.Participant.Id, out row.Quality);
                Players.Add(row);
            }

            double elapsed = now - _previousCapture;
            CaptureMessages(elapsed);
            CaptureSyncs(elapsed);
            _previousCapture = now;
        }

        private void CaptureSyncs(double elapsed)
        {
            _previousSyncs.Clear();
            _previousSyncs.AddRange(Syncs);
            Syncs.Clear();

            IReadOnlyList<StateSyncManager> syncs = StateSyncManagerRegistry.Managers;
            for (int i = 0; i < syncs.Count; i++)
            {
                StateSyncManager sync = syncs[i];
                if (sync.Session != Manager)
                {
                    continue;
                }

                // A sync shown before keeps its view, so its rates follow on from the last capture.
                SyncView view = _previousSyncs.Find(previous => previous.Sync == sync) ?? new SyncView(sync);
                view.Capture(_previousSyncs.Contains(view) ? elapsed : double.NaN);
                Syncs.Add(view);
            }

            _previousSyncs.Clear();
        }

        private void CaptureMessages(double elapsed)
        {
            Messages.Clear();
            if (Manager is IMultiplayerMessenger messenger)
            {
                messenger.GetMessageCounts(_counts);
            }
            else
            {
                _counts.Clear();
            }

            // The first capture of a type has nothing to compare with.
            for (int i = 0; i < _counts.Count; i++)
            {
                MessageCounts counts = _counts[i];
                MessageRow row = new MessageRow { Counts = counts };
                if (elapsed > 0.0 && _previousCounts.TryGetValue(counts.MessageType, out MessageCounts previous))
                {
                    row.SentPerSecond = (float)((counts.Sent - previous.Sent) / elapsed);
                    row.ReceivedPerSecond = (float)((counts.Received - previous.Received) / elapsed);
                    row.SentBytesPerSecond = (float)((counts.SentBytes - previous.SentBytes) / elapsed);
                    row.ReceivedBytesPerSecond = (float)((counts.ReceivedBytes - previous.ReceivedBytes) / elapsed);
                }

                Messages.Add(row);
            }

            Messages.Sort((a, b) => a.Counts.MessageType.CompareTo(b.Counts.MessageType));

            _previousCounts.Clear();
            for (int i = 0; i < _counts.Count; i++)
            {
                _previousCounts[_counts[i].MessageType] = _counts[i];
            }
        }
    }
}
