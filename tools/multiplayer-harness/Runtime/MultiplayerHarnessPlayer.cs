using System;
using System.Collections.Generic;
using System.Globalization;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using UnityEngine;

// One player of the multiplayer harness (tools/README.md#multiplayer-harness). The scene is empty:
// the component creates itself when it loads and plays the host's or the guest's part of one
// session on the machine-wide local network. It drives a MultiplayerManager on a LocalBackend
// without a DI core, calling the manager's Update itself, as a project with another container or
// none does. Each step of its part logs one MPHARNESS CHECK line, in the order its MPHARNESS PLAN
// line names them, and the player quits with 0 only when every step passed and nothing logged an
// error or exception. mu3-multiplayer-harness starts a host and a guest and reads these lines.
namespace MultiplayerHarness
{
    public sealed class MultiplayerHarnessPlayer : MonoBehaviour
    {
        private const string Prefix = "MPHARNESS ";
        private const string RoleOption = "-mpharness-role";
        private const string LobbyOption = "-mpharness-lobby";
        private const string TimeoutOption = "-mpharness-timeout";

        // The machine-wide network and the game ID of the harness alone, so that no other game on
        // this computer, the Multiplayer sample among them, sees its lobbies.
        private const string NetworkName = "mu3library-multiplayer-harness";
        private const int ProtocolVersion = 1;

        private const string HostName = "Harness Host";
        private const string GuestName = "Harness Guest";

        // Fixed values, so that each side checks exactly what the other one sent.
        private const string LobbyDataKey = "map";
        private const string LobbyDataValue = "forest";
        private const string ParticipantDataKey = "color";
        private const string ParticipantDataValue = "blue";
        private const int StartSeed = 424242;
        private const ushort GreetingType = 1;
        private const string HostGreeting = "hello from the host";
        private const int HostGreetingNumber = 1;
        private const string GuestGreeting = "hello from the guest";
        private const int GuestGreetingNumber = 2;

        private const float SearchIntervalSeconds = 1f;

        // How long a finished player goes on updating before it tears down, so that what it sent
        // last, such as the host's kick, reaches the other player before the connection closes.
        private const float LingerSeconds = 2f;

        private enum Role
        {
            None = 0,
            Host,
            Guest,
        }

        private readonly struct Verdict
        {
            public readonly bool IsDone;
            public readonly bool IsPassed;
            public readonly string Detail;

            private Verdict(bool isDone, bool isPassed, string detail)
            {
                IsDone = isDone;
                IsPassed = isPassed;
                Detail = detail;
            }

            public static Verdict Wait => default;
            public static Verdict Pass(string detail) => new Verdict(true, true, detail);
            public static Verdict Fail(string detail) => new Verdict(true, false, detail);
        }

        /// <summary>One step of a part: what it does once when it starts, and what it waits for.</summary>
        private sealed class Step
        {
            public readonly string Name;
            public readonly Action Enter;
            public readonly Func<Verdict> Poll;

            public Step(string name, Action enter, Func<Verdict> poll)
            {
                Name = name;
                Enter = enter;
                Poll = poll;
            }
        }

        private readonly MessageWriter _writer = new MessageWriter();
        private readonly object _errorLock = new object();
        private readonly List<string> _errors = new List<string>();

        private Role _role = Role.None;
        private string _lobbyName = string.Empty;
        private float _timeoutSeconds;
        private float _deadline;

        private List<Step> _steps = new List<Step>();
        private int _stepIndex = 0;
        private bool _isStepEntered = false;
        private int _checks = 0;
        private int _failures = 0;

        private bool _isFinished = false;
        private bool _hasPassed = false;
        private float _tearDownTime = 0f;
        private bool _isTornDown = false;

        private LocalBackend _backend;
        private MultiplayerManager _manager;
        private string _backendError;

        // What the requests answered and the events told, which the steps wait for.
        private MultiplayerResult? _createResult;
        private MultiplayerResult? _joinResult;
        private MultiplayerResult _requestResult;
        private StartGameResult _startResult;
        private bool _isSearching = false;
        private float _nextSearchTime = 0f;
        private LobbyInfo _foundLobby;
        private PlayerId _peer = PlayerId.None;
        private MultiplayerReason? _sessionEnd;
        private MultiplayerReason? _peerLeft;
        private bool _ownDataSet = false;
        private bool _ownDataDeleted = false;
        private bool _ownReady = false;
        private bool _peerDataSet = false;
        private bool _peerDataDeleted = false;
        private bool _lobbyDataDeleted = false;
        private int? _startSeed;
        private string _startDataError;
        private PlayerId _greetingSender = PlayerId.None;
        private string _greetingText;
        private int _greetingNumber;
        private string _greetingError;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePlayer()
        {
            new GameObject(nameof(MultiplayerHarnessPlayer)).AddComponent<MultiplayerHarnessPlayer>();
        }

        private void Start()
        {
            Application.logMessageReceivedThreaded += OnLog;
            // A player session goes on in the background, and a batch-mode player needs no more
            // frames than this to keep the session's timers.
            Application.runInBackground = true;
            Application.targetFrameRate = 60;

            string error = ReadArguments(Environment.GetCommandLineArgs());
            Debug.Log($"{Prefix}START role={RoleName} lobby={_lobbyName} timeout={_timeoutSeconds.ToString(CultureInfo.InvariantCulture)} " +
                $"network={NetworkName} unity={Application.unityVersion} pid={System.Diagnostics.Process.GetCurrentProcess().Id}");
            if (error != null)
            {
                Check("arguments", Verdict.Fail(error));
                Finish(false);
                return;
            }

            _deadline = Time.realtimeSinceStartup + _timeoutSeconds;
            _steps = _role == Role.Host ? HostSteps() : GuestSteps();
            string[] names = new string[_steps.Count];
            for (int i = 0; i < _steps.Count; i++)
            {
                names[i] = _steps[i].Name;
            }

            Debug.Log($"{Prefix}PLAN {RoleName} {string.Join(" ", names)}");
        }

        private string RoleName => _role == Role.Host ? "host" : _role == Role.Guest ? "guest" : "none";

        private void Update()
        {
            if (_isTornDown)
            {
                return;
            }

            if (_manager != null)
            {
                _manager.Update();
            }

            if (_isFinished)
            {
                if (Time.realtimeSinceStartup >= _tearDownTime)
                {
                    TearDownAndQuit();
                }

                return;
            }

            // The events of one update can finish several steps, so the steps go on in this frame
            // for as long as they pass.
            while (_stepIndex < _steps.Count)
            {
                Step step = _steps[_stepIndex];
                Verdict verdict;
                try
                {
                    if (!_isStepEntered)
                    {
                        _isStepEntered = true;
                        step.Enter?.Invoke();
                    }

                    verdict = step.Poll();
                }
                catch (Exception exception)
                {
                    verdict = Verdict.Fail($"{exception.GetType().Name}: {exception.Message}");
                }

                if (!verdict.IsDone)
                {
                    string interruption = Interruption();
                    if (interruption == null)
                    {
                        return;
                    }

                    verdict = Verdict.Fail(interruption);
                }

                Check(step.Name, verdict);
                if (!verdict.IsPassed)
                {
                    Finish(false);
                    return;
                }

                _stepIndex++;
                _isStepEntered = false;
            }

            Finish(true);
        }

        /// <summary>Why the step that waits cannot finish any more, or null while it still can.</summary>
        private string Interruption()
        {
            if (_sessionEnd.HasValue)
            {
                return $"the session ended: {_sessionEnd.Value}";
            }

            if (_peerLeft.HasValue)
            {
                return $"the guest left: {_peerLeft.Value}";
            }

            if (Time.realtimeSinceStartup > _deadline)
            {
                return $"timed out after {_timeoutSeconds.ToString(CultureInfo.InvariantCulture)} s";
            }

            return null;
        }

        #region Host
        private List<Step> HostSteps()
        {
            return new List<Step>
            {
                new Step("backend.start", () => StartBackend(HostName), BackendStarted),
                new Step("lobby.create", CreateLobby, LobbyCreated),
                new Step("guest.join", null, GuestJoined),
                new Step("guest.participant-data", null, GuestDataSet),
                new Step("guest.ready", null, GuestReady),
                new Step("game.start", StartGame, GameStartedAsHost),
                new Step("message.send", () => SendGreeting(HostGreeting, HostGreetingNumber, MessageTarget.Others), RequestAnswered),
                new Step("message.reply", null, () => GreetingReceived(_peer, GuestGreeting, GuestGreetingNumber)),
                new Step("lobby-data.delete", () => _requestResult = _manager.DeleteLobbyData(LobbyDataKey), LobbyDataDeletedAsHost),
                new Step("guest.participant-data-delete", null, GuestDataDeleted),
                new Step("guest.kick", KickGuest, GuestKicked),
            };
        }

        private void CreateLobby()
        {
            LobbyCreateOptions options = new LobbyCreateOptions
            {
                // Lobby search finds public lobbies only.
                Visibility = LobbyVisibility.Public,
                Name = _lobbyName,
                Rules = SessionRules.Default
                    .WithCapacity(4)
                    .WithMinPlayersToStart(2)
                    .WithRequireReady(true),
            };
            options.Data[LobbyDataKey] = LobbyDataValue;
            _manager.CreateLobby(options, result => _createResult = result);
        }

        private Verdict LobbyCreated()
        {
            if (!_createResult.HasValue)
            {
                return Verdict.Wait;
            }

            if (!_createResult.Value.IsSuccess)
            {
                return Verdict.Fail($"CreateLobby answered {_createResult.Value.Reason}");
            }

            _manager.LobbyData.TryGetValue(LobbyDataKey, out string map);
            string detail = $"lobby ID {_manager.CurrentLobbyId}, state {_manager.State}, host={_manager.IsHost}, lobby data \"{LobbyDataKey}\" \"{map}\"";
            return _manager.State == SessionState.Lobby && _manager.IsHost && map == LobbyDataValue
                ? Verdict.Pass(detail)
                : Verdict.Fail(detail);
        }

        private Verdict GuestJoined()
        {
            if (!TryGetOther(out Participant guest))
            {
                return Verdict.Wait;
            }

            _peer = guest.Id;
            string detail = $"{guest}";
            return guest.Name == GuestName && !guest.IsHost ? Verdict.Pass(detail) : Verdict.Fail($"{detail}, expected {GuestName}");
        }

        private Verdict GuestDataSet()
        {
            if (!_manager.TryGetParticipant(_peer, out Participant guest) || !guest.Data.TryGetValue(ParticipantDataKey, out string color))
            {
                return Verdict.Wait;
            }

            string detail = $"the guest's data \"{ParticipantDataKey}\" is \"{color}\"";
            return color == ParticipantDataValue ? Verdict.Pass(detail) : Verdict.Fail(detail);
        }

        private Verdict GuestReady()
        {
            return _manager.TryGetParticipant(_peer, out Participant guest) && guest.IsReady
                ? Verdict.Pass($"{guest.Name} is ready")
                : Verdict.Wait;
        }

        private void StartGame()
        {
            _requestResult = _manager.SetReady(true);
            if (!_requestResult.IsSuccess)
            {
                return;
            }

            _writer.Clear();
            _writer.WriteInt(StartSeed);
            _startResult = _manager.StartGame(_writer.Written);
        }

        private Verdict GameStartedAsHost()
        {
            if (!_requestResult.IsSuccess)
            {
                return Verdict.Fail($"SetReady answered {_requestResult.Reason}");
            }

            if (!_startResult.IsSuccess)
            {
                return Verdict.Fail($"StartGame answered {_startResult.Reason}, not ready: {_startResult.NotReadyPlayers.Count}");
            }

            string detail = $"state {_manager.State}, start data seed {SeedText}";
            return _manager.State == SessionState.InGame && _startSeed == StartSeed ? Verdict.Pass(detail) : Verdict.Fail(detail);
        }

        private Verdict LobbyDataDeletedAsHost()
        {
            if (!_requestResult.IsSuccess)
            {
                return Verdict.Fail($"DeleteLobbyData answered {_requestResult.Reason}");
            }

            string detail = $"lobby data holds \"{LobbyDataKey}\": {_manager.LobbyData.ContainsKey(LobbyDataKey)}, deletion event: {_lobbyDataDeleted}";
            return !_manager.LobbyData.ContainsKey(LobbyDataKey) && _lobbyDataDeleted ? Verdict.Pass(detail) : Verdict.Fail(detail);
        }

        private Verdict GuestDataDeleted()
        {
            return _peerDataDeleted
                ? Verdict.Pass($"the guest's data no longer holds \"{ParticipantDataKey}\"")
                : Verdict.Wait;
        }

        private void KickGuest()
        {
            Debug.Log($"{Prefix}INFO the guest's ping as this host measures it: {PingText(_peer)}");
            _requestResult = _manager.Kick(_peer);
        }

        private Verdict GuestKicked()
        {
            if (!_requestResult.IsSuccess)
            {
                return Verdict.Fail($"Kick answered {_requestResult.Reason}");
            }

            // The kick ends the guest's part, so its leaving is the step's own result here.
            if (_peerLeft.HasValue)
            {
                return _peerLeft.Value == MultiplayerReason.Kicked
                    ? Verdict.Pass("the guest left: Kicked")
                    : Verdict.Fail($"the guest left: {_peerLeft.Value}");
            }

            return Verdict.Wait;
        }
        #endregion

        #region Guest
        private List<Step> GuestSteps()
        {
            return new List<Step>
            {
                new Step("backend.start", () => StartBackend(GuestName), BackendStarted),
                new Step("lobby.search", null, SearchLobby),
                new Step("lobby.join", () => _manager.JoinLobby(_foundLobby.Id, result => _joinResult = result), LobbyJoined),
                new Step("participant-data.set", () => _requestResult = _manager.SetParticipantData(ParticipantDataKey, ParticipantDataValue), OwnDataSet),
                new Step("ready", () => _requestResult = _manager.SetReady(true), OwnReady),
                new Step("game.started", null, GameStartedAsGuest),
                new Step("message.receive", null, () => GreetingReceived(_manager.HostId, HostGreeting, HostGreetingNumber)),
                new Step("message.reply", () => SendGreeting(GuestGreeting, GuestGreetingNumber, MessageTarget.Host), RequestAnswered),
                new Step("lobby-data.deleted", null, LobbyDataDeletedAsGuest),
                new Step("participant-data.delete", DeleteOwnData, OwnDataDeleted),
                new Step("kicked", null, Kicked),
            };
        }

        private Verdict SearchLobby()
        {
            if (_foundLobby != null)
            {
                _foundLobby.Data.TryGetValue(LobbyDataKey, out string map);
                string detail = $"lobby ID {_foundLobby.Id}, host {_foundLobby.HostId}, members {_foundLobby.MemberCount}, " +
                    $"in game {_foundLobby.IsInGame}, lobby data \"{LobbyDataKey}\" \"{map}\"";
                return map == LobbyDataValue && !_foundLobby.IsInGame ? Verdict.Pass(detail) : Verdict.Fail(detail);
            }

            // As the sample's guest does: search every second until the lobby shows.
            if (!_isSearching && Time.realtimeSinceStartup >= _nextSearchTime)
            {
                _isSearching = true;
                _nextSearchTime = Time.realtimeSinceStartup + SearchIntervalSeconds;
                _manager.SearchLobbies(new LobbySearchQuery { MaxResults = 10 }, OnLobbiesFound);
            }

            return Verdict.Wait;
        }

        private void OnLobbiesFound(LobbySearchResult result)
        {
            _isSearching = false;
            if (!result.IsSuccess)
            {
                Debug.Log($"{Prefix}INFO lobby search answered {result.Reason}; searching again");
                return;
            }

            for (int i = 0; i < result.Lobbies.Count; i++)
            {
                if (string.Equals(result.Lobbies[i].Name, _lobbyName, StringComparison.Ordinal))
                {
                    _foundLobby = result.Lobbies[i];
                    return;
                }
            }
        }

        private Verdict LobbyJoined()
        {
            if (!_joinResult.HasValue)
            {
                return Verdict.Wait;
            }

            if (!_joinResult.Value.IsSuccess)
            {
                return Verdict.Fail($"JoinLobby answered {_joinResult.Value.Reason}");
            }

            _manager.LobbyData.TryGetValue(LobbyDataKey, out string map);
            _manager.TryGetParticipant(_manager.HostId, out Participant host);
            string detail = $"state {_manager.State}, host {host}, lobby data \"{LobbyDataKey}\" \"{map}\"";
            return _manager.State == SessionState.Lobby && !_manager.IsHost && _manager.HostId == _foundLobby.HostId
                && host.Name == HostName && map == LobbyDataValue
                ? Verdict.Pass(detail)
                : Verdict.Fail(detail);
        }

        private Verdict OwnDataSet()
        {
            if (!_requestResult.IsSuccess)
            {
                return Verdict.Fail($"SetParticipantData answered {_requestResult.Reason}");
            }

            // A client's own data changes once the host reports it back.
            return _ownDataSet ? Verdict.Pass($"the host reported \"{ParticipantDataKey}\" \"{ParticipantDataValue}\" back") : Verdict.Wait;
        }

        private Verdict OwnReady()
        {
            if (!_requestResult.IsSuccess)
            {
                return Verdict.Fail($"SetReady answered {_requestResult.Reason}");
            }

            return _ownReady ? Verdict.Pass("the host reported this player ready") : Verdict.Wait;
        }

        private Verdict GameStartedAsGuest()
        {
            if (_startDataError != null)
            {
                return Verdict.Fail(_startDataError);
            }

            if (!_startSeed.HasValue)
            {
                return Verdict.Wait;
            }

            string detail = $"state {_manager.State}, start data seed {SeedText}";
            return _manager.State == SessionState.InGame && _startSeed == StartSeed ? Verdict.Pass(detail) : Verdict.Fail(detail);
        }

        private Verdict LobbyDataDeletedAsGuest()
        {
            if (!_lobbyDataDeleted)
            {
                return Verdict.Wait;
            }

            bool holds = _manager.LobbyData.ContainsKey(LobbyDataKey);
            string detail = $"the lobby data \"{LobbyDataKey}\" was deleted; lobby data holds it: {holds}";
            return holds ? Verdict.Fail(detail) : Verdict.Pass(detail);
        }

        private void DeleteOwnData()
        {
            Debug.Log($"{Prefix}INFO this guest's ping as the host measures it: {PingText(_manager.LocalPlayerId)}");
            _requestResult = _manager.DeleteParticipantData(ParticipantDataKey);
        }

        private Verdict OwnDataDeleted()
        {
            if (!_requestResult.IsSuccess)
            {
                return Verdict.Fail($"DeleteParticipantData answered {_requestResult.Reason}");
            }

            return _ownDataDeleted ? Verdict.Pass($"the host reported \"{ParticipantDataKey}\" deleted") : Verdict.Wait;
        }

        private Verdict Kicked()
        {
            // The kick ends the session, so the end is the step's own result here.
            if (!_sessionEnd.HasValue)
            {
                return Verdict.Wait;
            }

            return _sessionEnd.Value == MultiplayerReason.Kicked
                ? Verdict.Pass("the session ended: Kicked")
                : Verdict.Fail($"the session ended: {_sessionEnd.Value}");
        }
        #endregion

        #region Both
        private void StartBackend(string playerName)
        {
            try
            {
                _backend = new LocalBackend(LocalNetwork.CreateMachineWide(NetworkName), playerName);
                _manager = new MultiplayerManager();
                Subscribe();

                MultiplayerResult result = _manager.Initialize(_backend, new MultiplayerConfig(NetworkName, ProtocolVersion));
                if (!result.IsSuccess)
                {
                    _backendError = $"Initialize answered {result.Reason}";
                    return;
                }

                // Every player registers the same message types with the same policy.
                result = _manager.RegisterMessage(GreetingType, OnGreeting);
                if (!result.IsSuccess)
                {
                    _backendError = $"RegisterMessage answered {result.Reason}";
                }
            }
            catch (Exception exception)
            {
                _backendError = $"{exception.GetType().Name}: {exception.Message}";
            }
        }

        private Verdict BackendStarted()
        {
            return _backendError == null
                ? Verdict.Pass($"the {_backend.Name} backend as {_backend.GetPlayerName(_manager.LocalPlayerId)} ({_manager.LocalPlayerId})")
                : Verdict.Fail(_backendError);
        }

        private void SendGreeting(string text, int number, MessageTarget target)
        {
            _writer.Clear();
            _writer.WriteString(text);
            _writer.WriteInt(number);
            _requestResult = _manager.Send(GreetingType, _writer.Written, target);
        }

        private Verdict RequestAnswered()
        {
            return _requestResult.IsSuccess ? Verdict.Pass("") : Verdict.Fail($"the request answered {_requestResult.Reason}");
        }

        private Verdict GreetingReceived(PlayerId expectedSender, string expectedText, int expectedNumber)
        {
            if (_greetingError != null)
            {
                return Verdict.Fail(_greetingError);
            }

            if (!_greetingSender.IsValid)
            {
                return Verdict.Wait;
            }

            string detail = $"from {_greetingSender}: \"{_greetingText}\" #{_greetingNumber}";
            return _greetingSender == expectedSender && _greetingText == expectedText && _greetingNumber == expectedNumber
                ? Verdict.Pass(detail)
                : Verdict.Fail($"{detail}, expected \"{expectedText}\" #{expectedNumber} from {expectedSender}");
        }

        private void Subscribe()
        {
            _manager.OnStateChanged += OnStateChanged;
            _manager.OnParticipantJoined += OnParticipantJoined;
            _manager.OnParticipantLeft += OnParticipantLeft;
            _manager.OnParticipantChanged += OnParticipantChanged;
            _manager.OnParticipantDataChanged += OnParticipantDataChanged;
            _manager.OnLobbyDataChanged += OnLobbyDataChanged;
            _manager.OnGameStarted += OnGameStarted;
        }

        private void OnStateChanged(SessionStateChange change)
        {
            Debug.Log($"{Prefix}INFO state {change}");
            if (change.Current == SessionState.Offline)
            {
                _sessionEnd = change.Reason;
            }
        }

        private void OnParticipantJoined(Participant participant)
        {
            Debug.Log($"{Prefix}INFO {participant} joined");
            if (_role == Role.Host && !participant.IsLocal && !_peer.IsValid)
            {
                _peer = participant.Id;
            }
        }

        private void OnParticipantLeft(Participant participant, MultiplayerReason reason)
        {
            Debug.Log($"{Prefix}INFO {participant} left ({reason})");
            if (_role == Role.Host && participant.Id == _peer)
            {
                _peerLeft = reason;
            }
        }

        private void OnParticipantChanged(Participant participant)
        {
            if (participant.IsLocal && participant.IsReady && _role == Role.Guest)
            {
                _ownReady = true;
            }
        }

        private void OnParticipantDataChanged(Participant participant, string key)
        {
            if (key != ParticipantDataKey)
            {
                return;
            }

            bool holds = participant.Data.TryGetValue(key, out string value);
            if (participant.IsLocal)
            {
                _ownDataSet |= holds && value == ParticipantDataValue;
                _ownDataDeleted |= _ownDataSet && !holds;
            }
            else if (participant.Id == _peer)
            {
                _peerDataSet |= holds && value == ParticipantDataValue;
                _peerDataDeleted |= _peerDataSet && !holds;
            }
        }

        private void OnLobbyDataChanged(string key, string value)
        {
            // A deleted key arrives with a null value.
            if (key == LobbyDataKey && value == null)
            {
                _lobbyDataDeleted = true;
            }
        }

        private void OnGameStarted(ReadOnlyMemory<byte> startData)
        {
            try
            {
                MessageReader reader = new MessageReader(startData.Span);
                _startSeed = reader.ReadInt();
            }
            catch (FormatException exception)
            {
                _startDataError = $"the start data could not be read: {exception.Message}";
            }
        }

        private void OnGreeting(MessageContext context, ReadOnlySpan<byte> payload)
        {
            try
            {
                MessageReader reader = new MessageReader(payload);
                _greetingText = reader.ReadString();
                _greetingNumber = reader.ReadInt();
                // The sender is the player the host proved from the connection, never what the message says.
                _greetingSender = context.Sender;
            }
            catch (FormatException exception)
            {
                _greetingError = $"a greeting from {context.Sender} could not be read: {exception.Message}";
            }
        }

        private string SeedText => _startSeed.HasValue ? _startSeed.Value.ToString(CultureInfo.InvariantCulture) : "none";

        private string PingText(PlayerId player)
        {
            return _manager.TryGetParticipantPing(player, out int ping) ? $"{ping} ms" : "-";
        }

        private bool TryGetOther(out Participant other)
        {
            IReadOnlyList<Participant> participants = _manager.Participants;
            for (int i = 0; i < participants.Count; i++)
            {
                if (!participants[i].IsLocal)
                {
                    other = participants[i];
                    return true;
                }
            }

            other = default;
            return false;
        }
        #endregion

        #region Run
        /// <summary>Reads the options; null when they are usable, otherwise what is wrong with them.</summary>
        private string ReadArguments(string[] arguments)
        {
            string role = ValueAfter(arguments, RoleOption);
            _role = role == "host" ? Role.Host : role == "guest" ? Role.Guest : Role.None;
            _lobbyName = ValueAfter(arguments, LobbyOption) ?? string.Empty;
            string timeout = ValueAfter(arguments, TimeoutOption);

            if (_role == Role.None)
            {
                return $"{RoleOption} needs host or guest, not {role ?? "nothing"}";
            }

            if (_lobbyName.Trim().Length == 0 || _lobbyName.Length > LobbyCreateOptions.MaxNameLength)
            {
                return $"{LobbyOption} needs a lobby name of 1 to {LobbyCreateOptions.MaxNameLength} characters";
            }

            if (!float.TryParse(timeout, NumberStyles.Float, CultureInfo.InvariantCulture, out _timeoutSeconds)
                || !(_timeoutSeconds > 0f) || float.IsInfinity(_timeoutSeconds))
            {
                return $"{TimeoutOption} needs a number of seconds above 0, not {timeout ?? "nothing"}";
            }

            return null;
        }

        /// <summary>The argument after the option, unless it is missing or another option, as the sample reads its own.</summary>
        private static string ValueAfter(string[] arguments, string option)
        {
            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                if (arguments[i] == option && !arguments[i + 1].StartsWith("-", StringComparison.Ordinal))
                {
                    return arguments[i + 1];
                }
            }

            return null;
        }

        private void Check(string name, Verdict verdict)
        {
            _checks++;
            if (!verdict.IsPassed)
            {
                _failures++;
            }

            Debug.Log($"{Prefix}CHECK {name} {(verdict.IsPassed ? "PASS" : "FAIL")} {verdict.Detail}");
        }

        private void Finish(bool hasPassed)
        {
            _isFinished = true;
            _hasPassed = hasPassed;
            _tearDownTime = Time.realtimeSinceStartup + LingerSeconds;
        }

        private void TearDownAndQuit()
        {
            _isTornDown = true;
            try
            {
                // The manager leaves any session without events and lets go of the backend, which
                // belongs to this player and goes after it.
                _manager?.Dispose();
                _backend?.Dispose();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            int errorLogs;
            lock (_errorLock)
            {
                errorLogs = _errors.Count;
                foreach (string error in _errors)
                {
                    Debug.Log($"{Prefix}ERRORLOG {error}");
                }
            }

            bool pass = _hasPassed && _failures == 0 && errorLogs == 0;
            Debug.Log($"{Prefix}RESULT {(pass ? "PASS" : "FAIL")} role={RoleName} checks={_checks} failures={_failures} errorLogs={errorLogs}");
            Application.logMessageReceivedThreaded -= OnLog;
            Application.Quit(pass ? 0 : 1);
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }

            string firstFrame = string.Empty;
            if (!string.IsNullOrEmpty(stackTrace))
            {
                int end = stackTrace.IndexOf('\n');
                firstFrame = end < 0 ? stackTrace : stackTrace.Substring(0, end);
            }

            lock (_errorLock)
            {
                _errors.Add($"{type}: {condition} @ {firstFrame}");
            }
        }
        #endregion
    }
}
