using System.Collections.Generic;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;

namespace Mu3Library.Editor.Window
{
    /// <summary>
    /// What the <see cref="MultiplayerDebuggerWindow"/> shows of one manager, as it was when last
    /// captured: the session, its players with the quality of this player's connection to each,
    /// and its message types with their counts and their rates since the capture before.
    /// </summary>
    internal sealed class MultiplayerSessionView
    {
        internal struct PlayerRow
        {
            public Participant Participant;

            /// <summary>False for this player itself and, on a client, for the other clients.</summary>
            public bool HasQuality;
            public ConnectionQuality Quality;
        }

        internal struct MessageRow
        {
            public MessageCounts Counts;
            public float SentPerSecond;
            public float ReceivedPerSecond;
        }

        private readonly List<MessageCounts> _counts = new List<MessageCounts>();
        private readonly Dictionary<ushort, MessageCounts> _previousCounts = new Dictionary<ushort, MessageCounts>();
        private double _previousCapture = double.NaN;

        /// <summary>The core that holds the manager.</summary>
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
                row.HasQuality = Manager.TryGetConnectionQuality(row.Participant.Id, out row.Quality);
                Players.Add(row);
            }

            CaptureMessages(now);
        }

        private void CaptureMessages(double now)
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
            double elapsed = now - _previousCapture;
            for (int i = 0; i < _counts.Count; i++)
            {
                MessageCounts counts = _counts[i];
                MessageRow row = new MessageRow { Counts = counts };
                if (elapsed > 0.0 && _previousCounts.TryGetValue(counts.MessageType, out MessageCounts previous))
                {
                    row.SentPerSecond = (float)((counts.Sent - previous.Sent) / elapsed);
                    row.ReceivedPerSecond = (float)((counts.Received - previous.Received) / elapsed);
                }

                Messages.Add(row);
            }

            Messages.Sort((a, b) => a.Counts.MessageType.CompareTo(b.Counts.MessageType));

            _previousCounts.Clear();
            for (int i = 0; i < _counts.Count; i++)
            {
                _previousCounts[_counts[i].MessageType] = _counts[i];
            }

            _previousCapture = now;
        }
    }
}
