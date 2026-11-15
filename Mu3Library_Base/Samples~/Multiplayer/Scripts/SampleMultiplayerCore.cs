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
    /// create, search, join by lobby ID, invite (Steam only), ready, start, message exchange,
    /// setting and deleting lobby data and this player's own participant data, and kick. The
    /// buttons in <c>SampleMultiplayerCore.Gui.cs</c> call them, and so does the unattended run in
    /// <c>SampleMultiplayerCore.AutoRun.cs</c> that the command line turns on. The lists show each
    /// player's ping as the host measures it and the players' avatars, which the core loads and
    /// releases.
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

        /// <summary>The lobby data key the host sets when it creates the lobby, and sets and deletes again.</summary>
        private const string LobbyDataKey = "map";

        /// <summary>The participant data key each player sets and deletes for themselves.</summary>
        private const string ParticipantDataKey = "color";

        private static readonly string[] Maps = { "forest", "desert", "harbor", "castle" };
        private static readonly string[] Colors = { "red", "green", "blue", "yellow" };

        [Tooltip("The app ID Steam starts with when Steamworks.NET is installed. Steam's test app 480 works during development.")]
        [SerializeField] private uint _steamAppId = 480;

        [Tooltip("Plays on the machine-wide local backend even where Steam would start, as the -mu3mp-local option does. "
            + "Turn it on to try an editor and a build on one computer with one Steam account, which Steam lets take only one slot of a session.")]
        [SerializeField] private bool _useLocalBackend = false;

        [Tooltip("Players a lobby holds, the host included.")]
        [SerializeField, Range(2, MultiplayerLimits.MaxCapacity)] private int _capacity = 4;

        private readonly SampleLog _log = new SampleLog();
        private readonly MessageWriter _writer = new MessageWriter();
        private readonly List<LobbyInfo> _foundLobbies = new List<LobbyInfo>();

        /// <summary>
        /// The search list's line of lobby data for each found lobby, at the same index as in
        /// <see cref="_foundLobbies"/>, written once per search rather than in every frame the
        /// list is drawn in.
        /// </summary>
        private readonly List<string> _foundLobbyDataLines = new List<string>();

        private readonly Dictionary<PlayerId, AvatarSlot> _avatars = new Dictionary<PlayerId, AvatarSlot>();

        private SampleCommandLine _commandLine;
        private SampleBackend _backend;
        private IMultiplayerManager _manager;
        private IMultiplayerManagerEventBus _eventBus;
        private IMultiplayerMessenger _messenger;

        private bool _isSearching = false;
        private int _sentGreetings = 0;
        private bool _backendHasNoAvatars = false;

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
            // Only in a player: in the editor this would change the project's Run In Background
            // player setting, which decides there.
            if (!Application.isEditor)
            {
                Application.runInBackground = true;
            }

            base.Awake();
        }

        protected override void Start()
        {
            base.Start();

            _manager = GetClass<IMultiplayerManager>();
            _eventBus = GetClass<IMultiplayerManagerEventBus>();
            _messenger = GetClass<IMultiplayerMessenger>();

            // The command line comes first, so an automatic run ends with AUTO FAIL rather than
            // waiting for good when what follows fails.
            _commandLine = SampleCommandLine.FromEnvironment();
            BeginAutoRun();
            if (_commandLine.Error != null)
            {
                _log.Warning($"Command line: {_commandLine.Error}.");
                FailAutoRun(_commandLine.Error);
                if (_commandLine.AutoRole != SampleAutoRole.None)
                {
                    return;
                }
            }

            if (!TryStartManager())
            {
                FailAutoRun("the multiplayer manager did not start");
                return;
            }

            // Every player registers the same message types with the same policy.
            Report(_messenger.RegisterMessage(SampleGreeting.MessageType, OnGreetingReceived), "Register the greeting message");

            _log.Info($"Ready on the {_backend.Backend.Name} backend as {LocalName} ({_manager.LocalPlayerId}).");
            ContinueAutoRun();
        }

        /// <summary>Creates the backend and attaches it to the manager; false, with the reason logged, when either fails.</summary>
        private bool TryStartManager()
        {
            string playerName = string.IsNullOrWhiteSpace(_commandLine.PlayerName)
                ? $"{(Application.isEditor ? "Editor" : "Player")} {UnityEngine.Random.Range(100, 1000)}"
                : _commandLine.PlayerName;

            try
            {
                _backend = SampleBackend.Create(GameId, _steamAppId, playerName, _useLocalBackend || _commandLine.ForceLocal);
                SubscribeEvents();

                MultiplayerResult result = _manager.Initialize(_backend.Backend, new MultiplayerConfig(GameId, ProtocolVersion));
                if (result.IsSuccess)
                {
                    return true;
                }

                _log.Warning($"The manager could not start on the {_backend.Backend.Name} backend: {result.Reason}");
            }
            catch (Exception exception)
            {
                // Steam's own failures, a broken steam_api library included, answer a reason
                // instead, both when the backend is created and in its later calls. What still
                // throws here is a mistake in the code, such as a game ID that cannot name a
                // machine-wide network (CreateMachineWide throws ArgumentException for one with a
                // space or a dot). The sample cannot go on then.
                Debug.LogException(exception);
                _log.Warning($"The backend could not start: {exception.GetType().Name}: {exception.Message}");
            }

            return false;
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

            // Lobby data that the lobby starts with, which search returns too. The host sets and
            // deletes it later with SetLobbyData and DeleteLobbyData.
            options.Data[LobbyDataKey] = Pick(Maps);

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

            // The hosts of lobbies that drop out of the list no longer show, so their avatars go.
            for (int i = 0; i < _foundLobbies.Count; i++)
            {
                PlayerId host = _foundLobbies[i].HostId;
                if (!result.IsSuccess || !HostsAnyOf(result.Lobbies, host))
                {
                    ReleaseAvatar(host);
                }
            }

            _foundLobbies.Clear();
            _foundLobbyDataLines.Clear();

            if (!result.IsSuccess)
            {
                _log.Warning($"Lobby search failed: {result.Reason}");
                return;
            }

            _foundLobbies.AddRange(result.Lobbies);
            for (int i = 0; i < _foundLobbies.Count; i++)
            {
                string data = LobbyDataText(_foundLobbies[i].Data);
                _foundLobbyDataLines.Add($"Lobby data: {data ?? "(none)"}");
            }

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

        /// <summary>
        /// Host only. Sets the lobby data key to another value than it holds, which every player
        /// hears through <see cref="IMultiplayerManagerEventBus.OnLobbyDataChanged"/>.
        /// </summary>
        private bool SetLobbyData()
        {
            _manager.LobbyData.TryGetValue(LobbyDataKey, out string current);
            return Report(_manager.SetLobbyData(LobbyDataKey, PickOther(Maps, current)), $"Set the lobby data \"{LobbyDataKey}\"");
        }

        /// <summary>
        /// Host only. Deletes the lobby data key, from lobby search too; every player hears it
        /// through <see cref="IMultiplayerManagerEventBus.OnLobbyDataChanged"/> with a null value.
        /// </summary>
        private bool DeleteLobbyData()
        {
            return Report(_manager.DeleteLobbyData(LobbyDataKey), $"Delete the lobby data \"{LobbyDataKey}\"");
        }

        /// <summary>
        /// Sets this player's own participant data key to another value than it holds. For a
        /// client, the value changes once the host reports it back through
        /// <see cref="IMultiplayerManagerEventBus.OnParticipantDataChanged"/>.
        /// </summary>
        private bool SetParticipantData()
        {
            string current = null;
            if (_manager.TryGetParticipant(_manager.LocalPlayerId, out Participant local))
            {
                local.Data.TryGetValue(ParticipantDataKey, out current);
            }

            return Report(_manager.SetParticipantData(ParticipantDataKey, PickOther(Colors, current)), $"Set my data \"{ParticipantDataKey}\"");
        }

        /// <summary>
        /// Deletes this player's own participant data key; every player hears it through
        /// <see cref="IMultiplayerManagerEventBus.OnParticipantDataChanged"/>, with the player
        /// as they are without it. No player can delete another's.
        /// </summary>
        private bool DeleteParticipantData()
        {
            return Report(_manager.DeleteParticipantData(ParticipantDataKey), $"Delete my data \"{ParticipantDataKey}\"");
        }
        #endregion

        #region Events
        private void SubscribeEvents()
        {
            _eventBus.OnStateChanged += OnStateChanged;
            _eventBus.OnParticipantJoined += OnParticipantJoined;
            _eventBus.OnParticipantLeft += OnParticipantLeft;
            _eventBus.OnParticipantDisconnected += OnParticipantDisconnected;
            _eventBus.OnParticipantReconnected += OnParticipantReconnected;
            _eventBus.OnParticipantChanged += OnParticipantChanged;
            _eventBus.OnParticipantDataChanged += OnParticipantDataChanged;
            _eventBus.OnLobbyDataChanged += OnLobbyDataChanged;
            _eventBus.OnGameStarted += OnGameStarted;
            _eventBus.OnReturnedToLobby += OnReturnedToLobby;
            _eventBus.OnJoinRequested += OnJoinRequested;
            _eventBus.OnAvatarChanged += OnAvatarChanged;
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
            _eventBus.OnParticipantDisconnected -= OnParticipantDisconnected;
            _eventBus.OnParticipantReconnected -= OnParticipantReconnected;
            _eventBus.OnParticipantChanged -= OnParticipantChanged;
            _eventBus.OnParticipantDataChanged -= OnParticipantDataChanged;
            _eventBus.OnLobbyDataChanged -= OnLobbyDataChanged;
            _eventBus.OnGameStarted -= OnGameStarted;
            _eventBus.OnReturnedToLobby -= OnReturnedToLobby;
            _eventBus.OnJoinRequested -= OnJoinRequested;
            _eventBus.OnAvatarChanged -= OnAvatarChanged;
        }

        private void OnStateChanged(SessionStateChange change)
        {
            // A failed request or an ended session names its reason, such as Kicked or HostLeft.
            string reason = change.Reason == MultiplayerReason.None ? string.Empty : $" ({change.Reason})";
            _log.Info($"State: {change.Previous} -> {change.Current}{reason}");

            // In a session, the list of found lobbies closes.
            if (change.Current == SessionState.Lobby || change.Current == SessionState.InGame)
            {
                CloseLobbyList();
            }

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

        private void OnParticipantDisconnected(Participant participant)
        {
            // In a game, the host keeps the slot of a player whose connection dropped for a while.
            _log.Info($"{participant.Name} dropped; the host keeps their slot for {_manager.Rules.ReconnectReservationSeconds:0} seconds.");
        }

        private void OnParticipantReconnected(Participant participant)
        {
            _log.Info($"{participant.Name} came back.");
        }

        private void OnParticipantChanged(Participant participant)
        {
            _log.Info($"{participant.Name} is {(participant.IsReady ? "ready" : "not ready")}.");
        }

        private void OnParticipantDataChanged(Participant participant, string key)
        {
            // The participant comes as the host reports them now: after a deletion, their data no
            // longer holds the key.
            if (participant.Data.TryGetValue(key, out string value))
            {
                _log.Info($"{participant.Name}'s data \"{key}\" is \"{value}\".");
            }
            else
            {
                _log.Info($"{participant.Name}'s data no longer holds \"{key}\".");
            }

            OnAutoRunParticipantDataChanged(participant, key);
        }

        private void OnLobbyDataChanged(string key, string value)
        {
            // A deleted key arrives with a null value.
            if (value == null)
            {
                _log.Info($"The lobby data \"{key}\" was deleted.");
            }
            else
            {
                _log.Info($"The lobby data \"{key}\" is \"{value}\".");
            }

            OnAutoRunLobbyDataChanged(key, value);
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

        private void OnAvatarChanged(AvatarResult result)
        {
            // The manager drew the new picture into the texture it handed out, which the lists
            // show already, so there is nothing to do but tell.
            _log.Info($"The avatar of {NameOf(result.Player)} changed; the lists show the new picture in the same texture.");
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

        #region Avatars
        /// <summary>
        /// How long the lists wait before they ask again for an avatar that could not be loaded.
        /// The manager tries a failed request again only when it is asked again, and its
        /// <see cref="IMultiplayerManagerEventBus.OnAvatarChanged"/> comes only for an avatar it
        /// holds, never for one that failed; a new session alone would leave the cell empty for
        /// the rest of the session after one failure. So the sample asks again at this fixed
        /// interval, in this session or a later one: a failure that passes, such as a Steam
        /// download that timed out, ends with the next request, and one that lasts asks once per
        /// interval instead of in every frame the lists are drawn in.
        /// </summary>
        private const float AvatarRetrySeconds = 10f;

        /// <summary>What the lists hold of one player's avatar.</summary>
        private sealed class AvatarSlot
        {
            public Texture2D Texture;
            public bool IsLoading;

            /// <summary>
            /// The <see cref="Time.realtimeSinceStartup"/> from which the lists may ask again,
            /// <see cref="AvatarRetrySeconds"/> after a request failed; 0 until one fails.
            /// </summary>
            public float RetryTime;
        }

        /// <summary>
        /// The small avatar of a player for the lists, asked for the first time a list shows the
        /// player: null while it loads, when it could not be loaded, and on a backend without
        /// avatars, such as the local backend, which answers <see cref="MultiplayerReason.NotSupported"/>.
        /// One that could not be loaded is asked for again <see cref="AvatarRetrySeconds"/> later.
        /// </summary>
        private Texture2D GetAvatar(PlayerId player)
        {
            if (_backendHasNoAvatars)
            {
                return null;
            }

            if (!_avatars.TryGetValue(player, out AvatarSlot slot))
            {
                slot = new AvatarSlot();
                _avatars.Add(player, slot);
            }

            // A texture the manager destroyed, as it destroys the avatars of a session's players
            // when the session ends, is asked for again. The answer can come before LoadAvatar returns.
            if (slot.Texture == null && !slot.IsLoading && Time.realtimeSinceStartup >= slot.RetryTime)
            {
                slot.IsLoading = true;
                _manager.LoadAvatar(player, AvatarSize.Small, OnAvatarLoaded);
            }

            return slot.Texture;
        }

        private void OnAvatarLoaded(AvatarResult result)
        {
            // A slot let go while its avatar loaded leaves the texture to the manager, which
            // destroys it with the next ReleaseAvatars or when it is disposed.
            if (!_avatars.TryGetValue(result.Player, out AvatarSlot slot))
            {
                return;
            }

            slot.IsLoading = false;
            if (result.IsSuccess)
            {
                slot.Texture = result.Texture;
            }
            else if (result.Reason == MultiplayerReason.NotSupported)
            {
                // Every request that was waiting answers so; the first one tells.
                if (!_backendHasNoAvatars)
                {
                    _backendHasNoAvatars = true;
                    _log.Info($"The {_backend.Backend.Name} backend has no avatars ({result.Reason}); the lists leave them out.");
                }
            }
            else
            {
                // The manager warns of a failure the backend answered with, such as Timeout. The
                // lists are drawn every frame, so they ask again only once AvatarRetrySeconds have
                // passed.
                slot.RetryTime = Time.realtimeSinceStartup + AvatarRetrySeconds;
            }
        }

        /// <summary>
        /// Lets go of the avatar of a player the lists no longer show, such as the host of a lobby
        /// that dropped out of the search. The manager destroys its textures.
        /// </summary>
        private void ReleaseAvatar(PlayerId player)
        {
            _avatars.Remove(player);
            _manager.ReleaseAvatar(player);
        }

        /// <summary>
        /// Closes the list of found lobbies once this player is in a session. ReleaseAvatars lets
        /// go of the hosts' avatars and keeps this player's own and those of the session's players,
        /// the host of the lobby just joined among them.
        /// </summary>
        private void CloseLobbyList()
        {
            if (_foundLobbies.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _foundLobbies.Count; i++)
            {
                PlayerId host = _foundLobbies[i].HostId;
                if (!_manager.TryGetParticipant(host, out _))
                {
                    _avatars.Remove(host);
                }
            }

            _foundLobbies.Clear();
            _foundLobbyDataLines.Clear();
            _manager.ReleaseAvatars();
        }
        #endregion

        #region Utility
        /// <summary>Logs a failed request; true when it succeeded.</summary>
        private bool Report(MultiplayerResult result, string action)
        {
            if (!result.IsSuccess)
            {
                _log.Warning($"{action}: {result.Reason}");
            }

            return result.IsSuccess;
        }

        /// <summary>
        /// A player's ping as the host measures it, which every player reads for every player of
        /// the session. The host's own is 0; a player the host has not reported yet, such as one
        /// that just joined, and one away while the host keeps their slot show a dash.
        /// </summary>
        private string PingText(PlayerId player)
        {
            return _manager.TryGetParticipantPing(player, out int ping) ? $"{ping} ms" : "-";
        }

        /// <summary>
        /// The longest text <see cref="LobbyDataText"/> writes, the cut included. A lobby holds up
        /// to <see cref="MultiplayerLimits.MaxLobbyDataSize"/> bytes of game-defined data, so the
        /// search list cuts the rest off rather than grow one row of the list to many lines.
        /// </summary>
        private const int MaxLobbyDataTextLength = 120;

        /// <summary>
        /// The game-defined lobby data that lobby search returned with a lobby (<see cref="LobbyInfo.Data"/>),
        /// such as <c>"map" "forest"</c>, its keys in order and each in the case the backend
        /// handed it back in, which on Steam can differ from the case the host wrote it in; null
        /// when it holds none, as after the host deleted every key. The library's own keys are
        /// never in it.
        /// </summary>
        private static string LobbyDataText(IReadOnlyDictionary<string, string> data)
        {
            if (data.Count == 0)
            {
                return null;
            }

            List<string> keys = new List<string>(data.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < keys.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append('"').Append(keys[i]).Append("\" \"").Append(data[keys[i]]).Append('"');
            }

            if (builder.Length > MaxLobbyDataTextLength)
            {
                // Never between the two halves of a surrogate pair.
                int length = MaxLobbyDataTextLength - 3;
                if (char.IsHighSurrogate(builder[length - 1]))
                {
                    length--;
                }

                builder.Length = length;
                builder.Append("...");
            }

            return builder.ToString();
        }

        private static string Pick(string[] values)
        {
            return values[UnityEngine.Random.Range(0, values.Length)];
        }

        /// <summary>
        /// One of the values other than the current one, each as likely, or any of them when the
        /// current one is none of them, such as a key that holds no value. Setting a key to the
        /// value it holds changes nothing, and no player hears an event for it, so a <b>Set</b>
        /// that picked it would change neither the value nor the log.
        /// </summary>
        private static string PickOther(string[] values, string current)
        {
            int currentIndex = Array.IndexOf(values, current);
            if (currentIndex < 0)
            {
                return Pick(values);
            }

            // Draws from the other values only, by skipping the current one's place.
            int index = UnityEngine.Random.Range(0, values.Length - 1);
            if (index >= currentIndex)
            {
                index++;
            }

            return values[index];
        }

        private static bool HostsAnyOf(IReadOnlyList<LobbyInfo> lobbies, PlayerId host)
        {
            for (int i = 0; i < lobbies.Count; i++)
            {
                if (lobbies[i].HostId == host)
                {
                    return true;
                }
            }

            return false;
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
