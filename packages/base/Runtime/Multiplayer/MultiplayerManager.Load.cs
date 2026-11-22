using System;
using System.Collections.Generic;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;

namespace Mu3Library.Multiplayer
{
    public partial class MultiplayerManager
    {
        // On the host, the players the current game started with, and those of them it still
        // waits for, while the wait runs.
        private readonly List<PlayerId> _loadStartedWith = new List<PlayerId>();
        private readonly List<PlayerId> _loadWaiting = new List<PlayerId>();
        private bool _isLoadWaitRunning = false;
        private double _loadDeadline = 0d;

        // On a client, the game and the load phase this player last reported loaded for, so a
        // report that a dropped connection lost can go again; 0 before any.
        private int _reportedLoadedGame = 0;
        private int _reportedLoadedPhase = 0;

        // The load phase of the current game, as the host numbered them: 0 from the start of the
        // game, and one more for each BeginLoadPhase. A report counts only in the phase it names.
        private int _loadPhase = 0;
        private byte[] _loadPhaseData = Array.Empty<byte>();



        public MultiplayerResult BeginLoadPhase(ReadOnlySpan<byte> phaseData = default)
        {
            MultiplayerReason check = CheckHostRequest(SessionState.InGame);
            if (check != MultiplayerReason.None)
            {
                return MultiplayerResult.Failure(check);
            }

            if (phaseData.Length > MultiplayerLimits.MaxStartDataSize)
            {
                return MultiplayerResult.Failure(MultiplayerReason.MessageTooLarge);
            }

            int generation = _sessionGeneration;
            _loadPhase++;
            _loadPhaseData = phaseData.ToArray();

            // A wait that still runs ends here without either event.
            BeginLoadWait();
            _writer.Begin(PacketKind.LoadPhaseBegan).WriteInt(_gameNumber);
            _writer.WriteInt(_loadPhase);
            _writer.WriteBytes(_loadPhaseData);

            // As in StartGame, a handler of a player dropped on the way may have ended the session (#340).
            BroadcastToWelcomed(ConnectionId.None);
            if (generation == _sessionGeneration)
            {
                Raise(_onLoadPhaseBegan, new ReadOnlyMemory<byte>(_loadPhaseData));
            }

            return ResultSince(generation);
        }

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
                int generation = _sessionGeneration;
                HostApplyLoaded(LocalPlayerId);
                return ResultSince(generation);
            }

            // The host decides; this player's own flag changes when the host reports it back.
            WriteReportLoaded();
            MultiplayerReason failure = SendToHost(DeliveryMode.Reliable);
            if (failure != MultiplayerReason.None)
            {
                return MultiplayerResult.Failure(failure);
            }

            _reportedLoadedGame = _gameNumber;
            _reportedLoadedPhase = _loadPhase;
            return MultiplayerResult.Success;
        }

        /// <summary>Writes this player's report for the game and the load phase it plays now.</summary>
        private void WriteReportLoaded()
        {
            _writer.Begin(PacketKind.ReportLoaded).WriteInt(_gameNumber);
            _writer.WriteInt(_loadPhase);
        }

        /// <summary>Starts the game's first load phase over, as a game starts or ends.</summary>
        private void ResetLoadPhase()
        {
            _loadPhase = 0;
            _loadPhaseData = Array.Empty<byte>();
        }

        #region Client
        /// <summary>
        /// The host began another load phase of the game this player plays: every flag clears, as
        /// it did on the host, and the game hears the phase's data.
        /// </summary>
        private void HandleLoadPhaseBegan(ref PacketReader reader)
        {
            int gameNumber = reader.ReadInt();
            int loadPhase = reader.ReadInt();
            byte[] phaseData = reader.ReadBytes(MultiplayerLimits.MaxStartDataSize);
            if (_state != SessionState.InGame || gameNumber != _gameNumber || loadPhase <= _loadPhase)
            {
                return;
            }

            _loadPhase = loadPhase;
            _loadPhaseData = phaseData;
            for (int i = 0; i < _participants.Count; i++)
            {
                _participants[i] = _participants[i].WithLoaded(false);
            }

            Raise(_onLoadPhaseBegan, new ReadOnlyMemory<byte>(_loadPhaseData));
        }
        #endregion

        #region Host
        /// <summary>
        /// Starts waiting, as a game or a load phase starts, for every player in the roster: the
        /// host, and the players whose slot it keeps while they are away, since they may come
        /// back and report.
        /// </summary>
        private void BeginLoadWait()
        {
            _loadWaiting.Clear();
            _loadStartedWith.Clear();
            for (int i = 0; i < _participants.Count; i++)
            {
                _participants[i] = _participants[i].WithLoaded(false);
                _loadWaiting.Add(_participants[i].Id);
                _loadStartedWith.Add(_participants[i].Id);
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
            _loadStartedWith.Clear();
        }

        /// <summary>
        /// Waits again for a player who has to load again, as another start of their game does,
        /// when the game started with them; a late joiner was never waited for.
        /// </summary>
        private void WaitAgainFor(PlayerId player)
        {
            if (_isLoadWaitRunning && _loadStartedWith.Contains(player) && !_loadWaiting.Contains(player))
            {
                _loadWaiting.Add(player);
            }
        }

        private void HostHandleLoaded(PeerConnection peer, ref PacketReader reader)
        {
            int gameNumber = reader.ReadInt();
            HostTakeLoaded(peer.Player, gameNumber, reader.ReadInt());
        }

        private void HostTakeLoaded(PlayerId player, int gameNumber, int loadPhase)
        {
            // A report from the game or the load phase before, which crossed the start of this
            // one, counts for nothing.
            if (gameNumber == _gameNumber && loadPhase == _loadPhase)
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

            Raise(_onParticipantChanged, updated);
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
            Raise(_onAllPlayersLoaded);
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
            Raise(_onLoadTimedOut, (IReadOnlyList<PlayerId>)missing);
        }
        #endregion

        private void ResetLoadState()
        {
            EndLoadWait();
            ResetLoadPhase();
            _loadDeadline = 0d;
            _reportedLoadedGame = 0;
            _reportedLoadedPhase = 0;
        }
    }
}
