using System;
using System.Collections.Generic;
using System.Text;
using Mu3Library.DI;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using UnityEngine;

namespace Mu3Library.Sample.Multiplayer
{
    /// <summary>
    /// The sample's core. It registers a <see cref="MultiplayerManager"/> in its container, which
    /// then updates the manager every frame (<see cref="IUpdatable"/>) and disposes it with the core,
    /// attaches the backend that <see cref="SampleBackend"/> picks, and holds the lobby flows:
    /// create, search, join by lobby ID, invite (Steam only), ready, start, message exchange, and
    /// kick. The buttons in <c>SampleMultiplayerCore.Gui.cs</c> call them, and so does the
    /// unattended run in <c>SampleMultiplayerCore.AutoRun.cs</c> that the command line turns on.
    /// </summary>
    public partial class SampleMultiplayerCore : CoreBase
    {
        /// <summary>
        /// Players meet only when their game ID and protocol version match. The ID tells this
        /// sample's lobbies apart from other games on the same Steam app ID, such as every game
        /// developed on Steam's test app 480, and names the machine-wide local network.
        /// </summary>
        private const string GameId = "mu3library-multiplayer-sample";

        /// <summary>Raise it whenever builds can no longer play together.</summary>
        private const int ProtocolVersion = 1;

        [Tooltip("The app ID Steam starts with when Steamworks.NET is installed. Steam's test app 480 works during development.")]
        [SerializeField] private uint _steamAppId = 480;

        [Tooltip("Players a lobby holds, the host included.")]
        [SerializeField, Range(2, MultiplayerLimits.MaxCapacity)] private int _capacity = 4;

        private readonly SampleLog _log = new SampleLog();
        private readonly MessageWriter _writer = new MessageWriter();
        private readonly List<LobbyInfo> _foundLobbies = new List<LobbyInfo>();

        private SampleCommandLine _commandLine;
        private SampleBackend _backend;
        private IMultiplayerManager _manager;
        private IMultiplayerManagerEventBus _eventBus;
        private IMultiplayerMessenger _messenger;

        private bool _isSearching = false;
        private int _sentGreetings = 0;

        private string LocalName => _backend != null && _manager != null
            ? _backend.Backend.GetPlayerName(_manager.LocalPlayerId)
            : string.Empty;



        protected override void ConfigureContainer()
        {
            RegisterClass<MultiplayerManager>();
        }

        protected override void Awake()
        {
            // A session goes on while its window is in the background: the host keeps answering
            // its players, and a player keeps hearing the host, while another window has the focus.
            Application.runInBackground = true;

            base.Awake();
        }

        protected override void Start()
        {
            base.Start();

            _manager = GetClass<IMultiplayerManager>();
            _eventBus = GetClass<IMultiplayerManagerEventBus>();
            _messenger = GetClass<IMultiplayerMessenger>();
            _commandLine = SampleCommandLine.FromEnvironment();

            string playerName = string.IsNullOrWhiteSpace(_commandLine.PlayerName)
                ? $"{(Application.isEditor ? "Editor" : "Player")} {UnityEngine.Random.Range(100, 1000)}"
                : _commandLine.PlayerName;
            _backend = SampleBackend.Create(GameId, _steamAppId, playerName, _commandLine.ForceLocal);

            SubscribeEvents();

            MultiplayerResult result = _manager.Initialize(_backend.Backend, new MultiplayerConfig(GameId, ProtocolVersion));
            if (!result.IsSuccess)
            {
                _log.Warning($"The manager could not start on the {_backend.Backend.Name} backend: {result.Reason}");
                StartAutoRun(false);
                return;
            }

            // Every player registers the same message types with the same policy.
            Report(_messenger.RegisterMessage(SampleGreeting.MessageType, OnGreetingReceived), "Register the greeting message");

            _log.Info($"Ready on the {_backend.Backend.Name} backend as {LocalName} ({_manager.LocalPlayerId}).");
            StartAutoRun(true);
        }

        protected override void OnDestroy()
        {
            UnsubscribeEvents();

            // The core's container disposes the manager here, which leaves the session and tells
            // the other players why. The manager does not own its backend, which goes after it.
            base.OnDestroy();

            _backend?.Dispose();
            _backend = null;
        }

        #region Flows
        private void CreateLobby(string lobbyName)
        {
            LobbyCreateOptions options = new LobbyCreateOptions
            {
                // Lobby search finds public lobbies only; invite-only and friends-only ones are joined through Steam.
                Visibility = LobbyVisibility.Public,
                Name = lobbyName,
                // The host starts once two players at least are in the lobby and every one of them is ready.
                Rules = SessionRules.Default
                    .WithCapacity(_capacity)
                    .WithMinPlayersToStart(2)
                    .WithRequireReady(true),
            };

            _log.Info($"Creating the lobby \"{lobbyName}\"...");
            _manager.CreateLobby(options, result =>
            {
                if (result.IsSuccess)
                {
                    _log.Info($"Created the lobby. Lobby ID: {_manager.CurrentLobbyId}");
                }
                else
                {
                    _log.Warning($"Could not create the lobby: {result.Reason}");
                    FailAutoRun($"creating the lobby failed: {result.Reason}");
                }
            });
        }

        private void SearchLobbies()
        {
            if (_isSearching)
            {
                return;
            }

            // The manager adds the filters for this game ID and protocol version, and the backend
            // returns only lobbies with a free slot. A query can add string and number filters on
            // the lobby data too.
            _isSearching = true;
            _manager.SearchLobbies(new LobbySearchQuery { MaxResults = 10 }, OnLobbiesFound);
        }

        private void OnLobbiesFound(LobbySearchResult result)
        {
            _isSearching = false;
            _foundLobbies.Clear();

            if (!result.IsSuccess)
            {
                _log.Warning($"Lobby search failed: {result.Reason}");
                return;
            }

            _foundLobbies.AddRange(result.Lobbies);

            // The unattended run searches every second, so only a search from the button logs.
            if (!IsAutoRunning)
            {
                _log.Info($"Found {_foundLobbies.Count} lobbies.");
            }

            OnAutoRunLobbiesFound();
        }

        /// <summary>
        /// Joins by lobby ID, whether search found the lobby or a player typed its ID. An accepted
        /// Steam invite or a join from the friends list goes the same way by itself.
        /// </summary>
        private void JoinLobby(LobbyId lobby)
        {
            _log.Info($"Joining lobby {lobby}...");
            _manager.JoinLobby(lobby, result =>
            {
                if (result.IsSuccess)
                {
                    _log.Info($"Joined the lobby of {NameOf(_manager.HostId)}.");
                }
                else
                {
                    _log.Warning($"Could not join lobby {lobby}: {result.Reason}");
                    FailAutoRun($"joining the lobby failed: {result.Reason}");
                }
            });
        }

        private void SetReady(bool isReady)
        {
            // For a client, success means the request went to the host; the flag changes once the
            // host reports it back through OnParticipantChanged.
            Report(_manager.SetReady(isReady), isReady ? "Get ready" : "Get unready");
        }

        private void StartGame()
        {
            // The start data reaches every player together with the start, here a random seed.
            int seed = UnityEngine.Random.Range(1, 1000000);
            _writer.Clear();
            _writer.WriteInt(seed);

            StartGameResult result = _manager.StartGame(_writer.Written);
            if (result.IsSuccess)
            {
                return;
            }

            if (result.NotReadyPlayers.Count > 0)
            {
                _log.Warning($"Cannot start yet; not ready: {NamesOf(result.NotReadyPlayers)}.");
            }
            else
            {
                _log.Warning($"Cannot start: {result.Reason} (at least {_manager.Rules.MinPlayersToStart} players).");
            }
        }

        private void EndGame()
        {
            Report(_manager.EndGame(), "End the game");
        }

        private void SendGreeting(MessageTarget target)
        {
            _sentGreetings++;
            SampleGreeting greeting = new SampleGreeting($"Hello from {LocalName}", _sentGreetings);
            _writer.Clear();
            greeting.Write(_writer);

            MultiplayerResult result = _messenger.Send(SampleGreeting.MessageType, _writer.Written, target);
            if (result.IsSuccess)
            {
                _log.Info($"Sent \"{greeting.Text}\" #{greeting.Number} to {target}.");
            }
            else
            {
                _log.Warning($"Could not send the message: {result.Reason}");
            }
        }

        private void Kick(PlayerId player)
        {
            // Host only. The kicked player cannot rejoin this session.
            Report(_manager.Kick(player), $"Kick {NameOf(player)}");
        }

        private void OpenInviteDialog()
        {
            MultiplayerResult result = _backend.OpenInviteDialog(_manager.CurrentLobbyId);
            if (result.IsSuccess)
            {
                _log.Info("Opened the Steam invite dialog.");
            }
            else
            {
                // OverlayUnavailable when the Steam overlay is off, as it is in the editor.
                _log.Warning($"Could not open the invite dialog: {result.Reason}");
            }
        }

        private void Leave()
        {
            _manager.Leave();
        }
        #endregion

        #region Events
        private void SubscribeEvents()
        {
            _eventBus.OnStateChanged += OnStateChanged;
            _eventBus.OnParticipantJoined += OnParticipantJoined;
            _eventBus.OnParticipantLeft += OnParticipantLeft;
            _eventBus.OnParticipantChanged += OnParticipantChanged;
            _eventBus.OnGameStarted += OnGameStarted;
            _eventBus.OnReturnedToLobby += OnReturnedToLobby;
            _eventBus.OnJoinRequested += OnJoinRequested;
        }

        private void UnsubscribeEvents()
        {
            if (_eventBus == null)
            {
                return;
            }

            _eventBus.OnStateChanged -= OnStateChanged;
            _eventBus.OnParticipantJoined -= OnParticipantJoined;
            _eventBus.OnParticipantLeft -= OnParticipantLeft;
            _eventBus.OnParticipantChanged -= OnParticipantChanged;
            _eventBus.OnGameStarted -= OnGameStarted;
            _eventBus.OnReturnedToLobby -= OnReturnedToLobby;
            _eventBus.OnJoinRequested -= OnJoinRequested;
        }

        private void OnStateChanged(SessionStateChange change)
        {
            // A failed request or an ended session names its reason, such as Kicked or HostLeft.
            string reason = change.Reason == MultiplayerReason.None ? string.Empty : $" ({change.Reason})";
            _log.Info($"State: {change.Previous} -> {change.Current}{reason}");

            OnAutoRunStateChanged(change);
        }

        private void OnParticipantJoined(Participant participant)
        {
            _log.Info($"{participant.Name} joined.");
        }

        private void OnParticipantLeft(Participant participant, MultiplayerReason reason)
        {
            _log.Info($"{participant.Name} left ({reason}).");

            OnAutoRunParticipantLeft(participant, reason);
        }

        private void OnParticipantChanged(Participant participant)
        {
            _log.Info($"{participant.Name} is {(participant.IsReady ? "ready" : "not ready")}.");
        }

        private void OnGameStarted(ReadOnlyMemory<byte> startData)
        {
            try
            {
                MessageReader reader = new MessageReader(startData.Span);
                _log.Info($"The game started with the seed {reader.ReadInt()}.");
            }
            catch (FormatException)
            {
                _log.Warning("The game started with start data this sample cannot read.");
            }

            OnAutoRunGameStarted();
        }

        private void OnReturnedToLobby()
        {
            _log.Info("The game ended; back in the lobby, and every ready flag is cleared.");
        }

        private void OnJoinRequested(LobbyId lobby)
        {
            // An accepted Steam invite, a join from the friends list, or a launch from an invite.
            // The manager leaves any current session and joins by itself.
            _log.Info($"Join requested for lobby {lobby}.");
        }

        /// <summary>
        /// Handles a greeting. The sender is the player the host proved from the connection the
        /// message arrived on, never what the message says.
        /// </summary>
        private void OnGreetingReceived(MessageContext context, ReadOnlySpan<byte> payload)
        {
            SampleGreeting greeting = SampleGreeting.Read(payload);
            _log.Info($"{NameOf(context.Sender)} says \"{greeting.Text}\" #{greeting.Number}.");

            OnAutoRunGreetingReceived(context.Sender);
        }
        #endregion

        #region Utility
        private void Report(MultiplayerResult result, string action)
        {
            if (!result.IsSuccess)
            {
                _log.Warning($"{action}: {result.Reason}");
            }
        }

        private string NameOf(PlayerId player)
        {
            return _manager.TryGetParticipant(player, out Participant participant)
                ? participant.Name
                : _backend.Backend.GetPlayerName(player);
        }

        private string NamesOf(IReadOnlyList<PlayerId> players)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < players.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(NameOf(players[i]));
            }

            return builder.ToString();
        }

        private bool TryGetOtherParticipant(out Participant other)
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
    }
}
