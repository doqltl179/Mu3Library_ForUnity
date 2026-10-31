using System;
using Mu3Library.Multiplayer.Data;
using UnityEngine;

namespace Mu3Library.Sample.Multiplayer
{
    /// <summary>
    /// The unattended run that <see cref="SampleCommandLine"/> turns on, which goes through the
    /// same flows as the buttons. The host creates a public lobby, waits for a guest to join and
    /// get ready, gets ready, starts the game, sends the guest a message, and kicks it once it
    /// answers. The guest searches for the lobby by name, joins it by its lobby ID, gets ready,
    /// answers the host's message, and waits to be kicked. Every step logs a line that starts with
    /// <c>[MultiplayerSample] AUTO</c>; the run ends with <c>AUTO PASS</c> or <c>AUTO FAIL</c> and
    /// then quits, a player with exit code 0 or 1 and the editor by leaving play mode.
    /// </summary>
    public partial class SampleMultiplayerCore
    {
        private const float SearchIntervalSeconds = 1f;
        private const float QuitDelaySeconds = 1f;

        private enum AutoStep
        {
            None = 0,
            StartingBackend,

            CreatingLobby,
            WaitingForGuest,
            WaitingForGuestReady,
            WaitingForReply,
            WaitingForKick,

            SearchingLobby,
            JoiningLobby,
            WaitingForStart,
            WaitingForGreeting,
            WaitingToBeKicked,

            Finished,
        }

        private AutoStep _autoStep = AutoStep.None;
        private bool _hasAutoPassed = false;
        private float _autoDeadline = 0f;
        private float _nextSearchTime = 0f;
        private float _quitTime = -1f;

        private PlayerId _autoGuest = PlayerId.None;
        private bool _hasGameStarted = false;
        private bool _hasPeerGreeted = false;
        private bool _wasGuestKicked = false;
        private bool _wasKicked = false;

        private bool IsAutoRunning => _autoStep != AutoStep.None && _autoStep != AutoStep.Finished;

        private string AutoRunStatus => _autoStep == AutoStep.Finished
            ? (_hasAutoPassed ? "passed" : "failed")
            : _autoStep.ToString();



        /// <summary>
        /// Starts the clock of an automatic run before the backend starts, so that the run ends
        /// with AUTO FAIL wherever it stops, the start of the backend included.
        /// </summary>
        private void BeginAutoRun()
        {
            if (_commandLine.AutoRole == SampleAutoRole.None)
            {
                return;
            }

            _autoDeadline = Time.realtimeSinceStartup + _commandLine.TimeoutSeconds;
            _autoStep = AutoStep.StartingBackend;
            if (_commandLine.Error == null)
            {
                _log.Info($"AUTO {_commandLine.AutoRole}: starting the backend");
            }
        }

        /// <summary>Goes on with an automatic run once the manager is ready.</summary>
        private void ContinueAutoRun()
        {
            if (!IsAutoRunning)
            {
                return;
            }

            if (_commandLine.AutoRole == SampleAutoRole.Host)
            {
                StepAutoRun(AutoStep.CreatingLobby, $"creating the public lobby \"{_commandLine.LobbyName}\"");
                CreateLobby(_commandLine.LobbyName);
            }
            else
            {
                StepAutoRun(AutoStep.SearchingLobby, $"searching for the lobby \"{_commandLine.LobbyName}\"");
            }
        }

        private void Update()
        {
            if (_quitTime >= 0f && Time.realtimeSinceStartup >= _quitTime)
            {
                _quitTime = -1f;
                QuitAfterAutoRun();
                return;
            }

            if (!IsAutoRunning)
            {
                return;
            }

            if (Time.realtimeSinceStartup > _autoDeadline)
            {
                FailAutoRun($"timed out at {_autoStep}");
                return;
            }

            switch (_autoStep)
            {
                case AutoStep.CreatingLobby:
                    if (_manager.State == SessionState.Lobby)
                    {
                        StepAutoRun(AutoStep.WaitingForGuest, $"created the lobby, lobby ID {_manager.CurrentLobbyId}");
                    }
                    break;

                case AutoStep.WaitingForGuest:
                    if (TryGetOtherParticipant(out Participant guest))
                    {
                        _autoGuest = guest.Id;
                        StepAutoRun(AutoStep.WaitingForGuestReady, $"{guest.Name} joined");
                    }
                    break;

                case AutoStep.WaitingForGuestReady:
                    if (_manager.TryGetParticipant(_autoGuest, out Participant readyGuest) && readyGuest.IsReady)
                    {
                        SetReady(true);
                        StartGame();
                        if (_manager.State != SessionState.InGame)
                        {
                            FailAutoRun("the game did not start");
                            break;
                        }

                        SendGreeting(MessageTarget.Others);
                        StepAutoRun(AutoStep.WaitingForReply, $"{readyGuest.Name} got ready; started the game and sent a message");
                    }
                    break;

                case AutoStep.WaitingForReply:
                    if (_hasPeerGreeted)
                    {
                        StepAutoRun(AutoStep.WaitingForKick, "the guest answered; kicking it");
                        Kick(_autoGuest);
                    }
                    break;

                case AutoStep.WaitingForKick:
                    if (_wasGuestKicked)
                    {
                        PassAutoRun("created the lobby, the guest joined and got ready, started the game, exchanged messages, kicked the guest");
                    }
                    break;

                case AutoStep.SearchingLobby:
                    if (!_isSearching && Time.realtimeSinceStartup >= _nextSearchTime)
                    {
                        _nextSearchTime = Time.realtimeSinceStartup + SearchIntervalSeconds;
                        SearchLobbies();
                    }
                    break;

                case AutoStep.JoiningLobby:
                    if (_manager.State == SessionState.Lobby)
                    {
                        SetReady(true);
                        StepAutoRun(AutoStep.WaitingForStart, $"joined the lobby of {NameOf(_manager.HostId)} and got ready");
                    }
                    break;

                case AutoStep.WaitingForStart:
                    if (_hasGameStarted)
                    {
                        StepAutoRun(AutoStep.WaitingForGreeting, "the host started the game");
                    }
                    break;

                case AutoStep.WaitingForGreeting:
                    if (_hasPeerGreeted)
                    {
                        SendGreeting(MessageTarget.Host);
                        StepAutoRun(AutoStep.WaitingToBeKicked, "the host sent a message; answered it");
                    }
                    break;

                case AutoStep.WaitingToBeKicked:
                    if (_wasKicked)
                    {
                        PassAutoRun("found the lobby, joined it by its lobby ID, got ready, the game started, exchanged messages, was kicked");
                    }
                    break;
            }
        }

        private void OnAutoRunLobbiesFound()
        {
            if (_autoStep != AutoStep.SearchingLobby)
            {
                return;
            }

            for (int i = 0; i < _foundLobbies.Count; i++)
            {
                LobbyInfo lobby = _foundLobbies[i];
                if (string.Equals(lobby.Name, _commandLine.LobbyName, StringComparison.Ordinal))
                {
                    StepAutoRun(AutoStep.JoiningLobby, $"found the lobby, lobby ID {lobby.Id}; joining it by that ID");
                    JoinLobby(lobby.Id);
                    return;
                }
            }
        }

        private void OnAutoRunStateChanged(SessionStateChange change)
        {
            if (!IsAutoRunning || change.Current != SessionState.Offline)
            {
                return;
            }

            if (_autoStep == AutoStep.WaitingToBeKicked && change.Reason == MultiplayerReason.Kicked)
            {
                _wasKicked = true;
                return;
            }

            FailAutoRun($"the session ended at {_autoStep}: {change.Reason}");
        }

        private void OnAutoRunParticipantLeft(Participant participant, MultiplayerReason reason)
        {
            if (_autoStep == AutoStep.WaitingForKick && participant.Id == _autoGuest && reason == MultiplayerReason.Kicked)
            {
                _wasGuestKicked = true;
            }
        }

        private void OnAutoRunGameStarted()
        {
            _hasGameStarted = true;
        }

        private void OnAutoRunGreetingReceived(PlayerId sender)
        {
            if (!IsAutoRunning)
            {
                return;
            }

            PlayerId peer = _commandLine.AutoRole == SampleAutoRole.Host ? _autoGuest : _manager.HostId;
            if (sender == peer)
            {
                _hasPeerGreeted = true;
            }
        }

        private void StepAutoRun(AutoStep next, string description)
        {
            _autoStep = next;
            _log.Info($"AUTO {_commandLine.AutoRole}: {description}");
        }

        private void PassAutoRun(string summary)
        {
            FinishAutoRun(true, summary);
        }

        /// <summary>Fails a running automatic run; without one, as when the buttons drive the sample, it does nothing.</summary>
        private void FailAutoRun(string reason)
        {
            if (IsAutoRunning)
            {
                FinishAutoRun(false, reason);
            }
        }

        private void FinishAutoRun(bool hasPassed, string text)
        {
            _autoStep = AutoStep.Finished;
            _hasAutoPassed = hasPassed;
            _quitTime = Time.realtimeSinceStartup + QuitDelaySeconds;

            if (hasPassed)
            {
                _log.Info($"AUTO PASS {_commandLine.AutoRole}: {text}");
            }
            else
            {
                _log.Warning($"AUTO FAIL {_commandLine.AutoRole}: {text}");
            }
        }

        private void QuitAfterAutoRun()
        {
            if (_manager != null && _manager.IsInitialized)
            {
                _manager.Leave();
            }

#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit(_hasAutoPassed ? 0 : 1);
#endif
        }
    }
}
