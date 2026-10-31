using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        // On the host, the players the current game still waits for, while the wait runs.
        private readonly List<PlayerId> _loadWaiting = new List<PlayerId>();
        private bool _isLoadWaitRunning = false;
        private double _loadDeadline = 0d;



        public MultiplayerResult ReportLoaded()
        {
            if (!_isInitialized || _isDisposed)
            {
                return MultiplayerResult.Failure(MultiplayerReason.NotInitialized);
            }

            if (_state == SessionState.Reconnecting)
            {
                return MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
            }

            if (_state != SessionState.InGame)
            {
                return MultiplayerResult.Failure(MultiplayerReason.InvalidState);
            }

            if (_isHost)
            {
                HostApplyLoaded(LocalPlayerId);
                return MultiplayerResult.Success;
            }

            // The host decides; this player's own flag changes when the host reports it back.
            _writer.Begin(PacketKind.ReportLoaded).WriteInt(_gameNumber);
            return SendToHost(DeliveryMode.Reliable)
                ? MultiplayerResult.Success
                : MultiplayerResult.Failure(MultiplayerReason.ConnectionLost);
        }

        #region Host
        /// <summary>
        /// Starts waiting, as a game starts, for every player in the roster: the host, and the
        /// players whose slot it keeps while they are away, since they may come back and report.
        /// </summary>
        private void BeginLoadWait()
        {
            _loadWaiting.Clear();
            for (int i = 0; i < _participants.Count; i++)
            {
                _participants[i] = _participants[i].WithLoaded(false);
                _loadWaiting.Add(_participants[i].Id);
            }

            float seconds = _rules.LoadTimeoutSeconds;
            _isLoadWaitRunning = true;
            _loadDeadline = seconds > 0f ? Now + seconds : double.PositiveInfinity;
        }

        /// <summary>Stops waiting without telling anyone, as the game returns to the lobby.</summary>
        private void EndLoadWait()
        {
            _isLoadWaitRunning = false;
            _loadWaiting.Clear();
        }

        private void HostHandleLoaded(PeerConnection peer, ref PacketReader reader)
        {
            HostTakeLoaded(peer.Player, reader.ReadInt());
        }

        private void HostTakeLoaded(PlayerId player, int gameNumber)
        {
            // A report from the game before, which crossed the start of this one, counts for nothing.
            if (gameNumber == _gameNumber)
            {
                HostApplyLoaded(player);
            }
        }

        private void HostApplyLoaded(PlayerId player)
        {
            if (_state != SessionState.InGame)
            {
                return;
            }

            int index = IndexOfParticipant(player);
            if (index < 0 || _participants[index].IsLoaded)
            {
                return;
            }

            Participant updated = _participants[index].WithLoaded(true);
            _participants[index] = updated;

            WriteParticipantUpdated(updated);
            int generation = _sessionGeneration;
            BroadcastToWelcomed(ConnectionId.None);
            if (generation != _sessionGeneration)
            {
                return;
            }

            Raise(OnParticipantChanged, updated);
            if (generation == _sessionGeneration)
            {
                StopWaitingFor(player);
            }
        }

        /// <summary>
        /// The player reported, or left the session: the wait goes on without them, and ends once
        /// nobody is left to wait for.
        /// </summary>
        private void StopWaitingFor(PlayerId player)
        {
            if (!_isLoadWaitRunning || !_loadWaiting.Remove(player) || _loadWaiting.Count > 0)
            {
                return;
            }

            _isLoadWaitRunning = false;
            Raise(OnAllPlayersLoaded);
        }

        /// <summary>Ends the wait once its time passed, with the players who did not report, in roster order.</summary>
        private void UpdateLoadWait()
        {
            if (!_isHost || !_isLoadWaitRunning || Now < _loadDeadline)
            {
                return;
            }

            PlayerId[] missing = new PlayerId[_loadWaiting.Count];
            int count = 0;
            for (int i = 0; i < _participants.Count && count < missing.Length; i++)
            {
                if (_loadWaiting.Contains(_participants[i].Id))
                {
                    missing[count++] = _participants[i].Id;
                }
            }

            EndLoadWait();
            Raise(OnLoadTimedOut, (IReadOnlyList<PlayerId>)missing);
        }
        #endregion

        private void ResetLoadState()
        {
            EndLoadWait();
            _loadDeadline = 0d;
        }
    }
}
