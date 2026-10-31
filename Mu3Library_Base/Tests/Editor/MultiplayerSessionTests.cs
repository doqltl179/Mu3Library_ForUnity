using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Protocol;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Session rules of <see cref="MultiplayerManager"/>, played out by several players on one
    /// <see cref="LocalNetwork"/> in this process. Each test names the requirement of
    /// docs/specs/steam-player-hosted-multiplayer.md it checks.
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        private const string GameId = "tests.multiplayer";

        private sealed class Node
        {
            public LocalBackend Backend;
            public MultiplayerManager Manager;
            public readonly List<SessionStateChange> States = new List<SessionStateChange>();
            public readonly List<Participant> Joined = new List<Participant>();
            public readonly List<(Participant Participant, MultiplayerReason Reason)> Left = new List<(Participant, MultiplayerReason)>();
            public readonly List<Participant> Changed = new List<Participant>();
            public readonly List<(Participant Participant, string Key)> DataChanges = new List<(Participant, string)>();
            public readonly List<(string Key, string Value)> LobbyDataChanges = new List<(string, string)>();
            public readonly List<(Participant Sender, string Text)> Chats = new List<(Participant, string)>();
            public readonly List<byte[]> GameStarts = new List<byte[]>();
            public readonly List<LobbyId> JoinRequests = new List<LobbyId>();
            public readonly List<Participant> RateLimited = new List<Participant>();
            public readonly List<Participant> Disconnected = new List<Participant>();
            public readonly List<Participant> Reconnected = new List<Participant>();
            public readonly List<PlayerId[]> LoadTimeouts = new List<PlayerId[]>();
            public int AllLoadedCount;
            public int ReturnedToLobbyCount;

            public PlayerId Id => Backend.LocalPlayerId;
            public SessionStateChange LastChange => States[States.Count - 1];
        }

        private sealed class RecordingListener : IMultiplayerBackendListener
        {
            public readonly List<ConnectionId> Connected = new List<ConnectionId>();
            public readonly List<(ConnectionId Connection, BackendDisconnectKind Kind, int ReasonCode)> Disconnected =
                new List<(ConnectionId, BackendDisconnectKind, int)>();

            public void OnConnected(ConnectionId connection, PlayerId remote, bool isIncoming) => Connected.Add(connection);
            public void OnDisconnected(ConnectionId connection, PlayerId remote, BackendDisconnectKind kind, int reasonCode) => Disconnected.Add((connection, kind, reasonCode));
            public void OnReceived(ConnectionId connection, PlayerId remote, ReadOnlySpan<byte> data) { }
            public void OnLobbyOwnerChanged(LobbyId lobby, PlayerId newOwner) { }
            public void OnLobbyJoinRequested(LobbyId lobby) { }
        }

        private LocalNetwork _network;
        private double _now;
        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<LocalBackend> _rawBackends = new List<LocalBackend>();



        [SetUp]
        public void SetUp()
        {
            _network = new LocalNetwork();
            _now = 0d;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Node node in _nodes)
            {
                node.Manager.Dispose();
                node.Backend.Dispose();
            }

            foreach (LocalBackend backend in _rawBackends)
            {
                backend.Dispose();
            }

            _nodes.Clear();
            _rawBackends.Clear();
        }

        #region Lobby
        [Test]
        public void CreateLobby_PublicLobby_IsFoundWithItsCapacityNameAndData()
        {
            // FR-5
            Node host = CreateNode("Host");
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["map"] = "forest";

            Assert.That(CreateLobby(host, options).IsSuccess, Is.True);
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Lobby));
            Assert.That(host.Manager.IsHost, Is.True);
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(1));
            Assert.That(host.Manager.Participants[0].IsHost && host.Manager.Participants[0].IsLocal, Is.True);

            Node seeker = CreateNode("Seeker");
            LobbySearchResult result = Search(seeker);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Lobbies.Count, Is.EqualTo(1));
            LobbyInfo lobby = result.Lobbies[0];
            Assert.That(lobby.Id, Is.EqualTo(host.Manager.CurrentLobbyId));
            Assert.That(lobby.Capacity, Is.EqualTo(4));
            Assert.That(lobby.MemberCount, Is.EqualTo(1));
            Assert.That(lobby.Name, Is.EqualTo("Room"));
            Assert.That(lobby.HostId, Is.EqualTo(host.Id));
            Assert.That(lobby.IsInGame, Is.False);
            Assert.That(lobby.Data["map"], Is.EqualTo("forest"));
            Assert.That(lobby.Data.Keys.Any(key => key.StartsWith("mu3.", StringComparison.Ordinal)), Is.False);
        }

        [Test]
        public void CreateLobby_InviteOnlyOrFriendsOnly_IsNotSearchable()
        {
            // FR-5
            CreateHost("InviteOnly", LobbyVisibility.InviteOnly);
            CreateHost("FriendsOnly", LobbyVisibility.FriendsOnly);

            Assert.That(Search(CreateNode("Seeker")).Lobbies, Is.Empty);
        }

        [Test]
        public void CreateLobby_ReservedOrEmptyDataKey_IsRefused()
        {
            Node host = CreateNode("Host");
            LobbyCreateOptions reserved = PublicOptions(SessionRules.Default);
            reserved.Data["mu3.game"] = "spoofed";
            LobbyCreateOptions empty = PublicOptions(SessionRules.Default);
            empty.Data[string.Empty] = "value";

            Assert.That(CreateLobby(host, reserved).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateLobby(host, empty).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_network.Lobbies, Is.Empty);
        }

        [Test]
        public void Search_LeavesOutOtherGamesOtherVersionsAndFullLobbies()
        {
            // FR-6
            Node same = CreateHost("Same");
            CreateHost(CreateNode("OtherGame", gameId: "another.game"), PublicOptions(SessionRules.Default));
            CreateHost(CreateNode("OtherVersion", protocolVersion: 2), PublicOptions(SessionRules.Default));
            CreateHost(CreateNode("Full"), PublicOptions(new SessionRules(1)));

            LobbySearchResult result = Search(CreateNode("Seeker"));

            Assert.That(result.Lobbies.Select(lobby => lobby.Id), Is.EqualTo(new[] { same.Manager.CurrentLobbyId }));
        }

        [Test]
        public void Search_AppliesGameDefinedFilters()
        {
            // FR-6
            LobbyCreateOptions coop = PublicOptions(SessionRules.Default);
            coop.Data["mode"] = "coop";
            coop.Data["level"] = "3";
            Node coopHost = CreateHost(CreateNode("Coop"), coop);

            LobbyCreateOptions versus = PublicOptions(SessionRules.Default);
            versus.Data["mode"] = "versus";
            versus.Data["level"] = "7";
            Node versusHost = CreateHost(CreateNode("Versus"), versus);

            Node seeker = CreateNode("Seeker");
            LobbySearchResult byMode = Search(seeker, new LobbySearchQuery().AddStringFilter("mode", "coop"));
            LobbySearchResult byLevel = Search(seeker, new LobbySearchQuery().AddNumericFilter("level", 5, LobbyComparison.GreaterThan));

            Assert.That(byMode.Lobbies.Select(lobby => lobby.Id), Is.EqualTo(new[] { coopHost.Manager.CurrentLobbyId }));
            Assert.That(byLevel.Lobbies.Select(lobby => lobby.Id), Is.EqualTo(new[] { versusHost.Manager.CurrentLobbyId }));
        }

        [Test]
        public void JoinLobby_ReachesTheLobby_WithTheSameRosterEverywhere()
        {
            // FR-9, FR-29
            Node host = CreateHost("Host");
            Node a = CreateNode("A");
            Node b = CreateNode("B");

            Assert.That(Join(a, host).IsSuccess, Is.True);
            Assert.That(Join(b, host).IsSuccess, Is.True);

            PlayerId[] expected = { host.Id, a.Id, b.Id };
            foreach (Node node in new[] { host, a, b })
            {
                IReadOnlyList<Participant> roster = node.Manager.Participants;
                Assert.That(roster.Select(participant => participant.Id), Is.EqualTo(expected));
                Assert.That(roster.Select(participant => participant.Name), Is.EqualTo(new[] { "Host", "A", "B" }));
                Assert.That(roster.Single(participant => participant.IsHost).Id, Is.EqualTo(host.Id));
                Assert.That(roster.Single(participant => participant.IsLocal).Id, Is.EqualTo(node.Id));
                Assert.That(node.Manager.HostId, Is.EqualTo(host.Id));
            }

            Assert.That(host.Joined.Select(participant => participant.Id), Is.EqualTo(new[] { a.Id, b.Id }));
            Assert.That(a.Joined.Select(participant => participant.Id), Is.EqualTo(new[] { b.Id }));
            Assert.That(b.Joined, Is.Empty);
        }

        [Test]
        public void JoinRequest_FromOutsideTheGame_JoinsLikeJoinLobby_AndLeavesTheCurrentSession()
        {
            // FR-7
            Node first = CreateHost("First");
            Node second = CreateHost("Second");
            Node player = CreateNode("Player");

            player.Backend.RequestJoin(first.Manager.CurrentLobbyId);
            Pump();

            Assert.That(player.JoinRequests, Is.EqualTo(new[] { first.Manager.CurrentLobbyId }));
            Assert.That(player.Manager.State, Is.EqualTo(SessionState.Lobby));
            Assert.That(player.Manager.CurrentLobbyId, Is.EqualTo(first.Manager.CurrentLobbyId));
            Assert.That(first.Joined.Select(participant => participant.Id), Is.EqualTo(new[] { player.Id }));

            player.Backend.RequestJoin(second.Manager.CurrentLobbyId);
            Pump();

            Assert.That(player.Manager.CurrentLobbyId, Is.EqualTo(second.Manager.CurrentLobbyId));
            Assert.That(first.Left.Single().Participant.Id, Is.EqualTo(player.Id));
            Assert.That(first.Left.Single().Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            Assert.That(second.Joined.Select(participant => participant.Id), Is.EqualTo(new[] { player.Id }));
        }

        [Test]
        public void Leave_RaisesOneLeftEventWithItsReason_OnEveryRemainingPlayer()
        {
            // FR-9
            Node host = CreateHost("Host");
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);

            b.Manager.Leave();
            Pump();

            Assert.That(b.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(b.LastChange.Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            foreach (Node remaining in new[] { host, a })
            {
                Assert.That(remaining.Left.Count, Is.EqualTo(1));
                Assert.That(remaining.Left[0].Participant.Id, Is.EqualTo(b.Id));
                Assert.That(remaining.Left[0].Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
                Assert.That(remaining.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id, a.Id }));
            }
        }

        [Test]
        public void Leave_FreesTheSlotForAnotherPlayer()
        {
            // FR-14
            Node host = CreateHost("Host", rules: new SessionRules(2));
            Node a = JoinNew("A", host);
            Node b = CreateNode("B");

            Assert.That(Join(b, host).Reason, Is.EqualTo(MultiplayerReason.SessionFull));

            a.Manager.Leave();
            Pump();

            Assert.That(Join(b, host).IsSuccess, Is.True);
        }

        [Test]
        public void LeaveWhileCreating_AnswersTheCallbackAndLeavesNoLobbyBehind()
        {
            Node host = CreateNode("Host");
            MultiplayerResult? result = null;
            host.Manager.CreateLobby(PublicOptions(SessionRules.Default), r => result = r);

            host.Manager.Leave();
            Pump();

            Assert.That(result?.Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_network.Lobbies, Is.Empty);
        }

        [Test]
        public void SetReady_ReachesTheHostAndEveryPlayer()
        {
            // FR-10
            Node host = CreateHost("Host");
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);

            Assert.That(a.Manager.SetReady(true).IsSuccess, Is.True);
            Pump();

            foreach (Node node in new[] { host, a, b })
            {
                Assert.That(node.Manager.TryGetParticipant(a.Id, out Participant participant), Is.True);
                Assert.That(participant.IsReady, Is.True);
                Assert.That(node.Changed.Select(changed => changed.Id), Is.EqualTo(new[] { a.Id }));
            }
        }
        #endregion

        #region Capacity
        [TestCase(0)]
        [TestCase(MultiplayerLimits.MaxCapacity + 1)]
        public void CreateLobby_CapacityOutsideTheRange_IsRefusedWithoutALobby(int capacity)
        {
            // FR-15
            Node host = CreateNode("Host");

            MultiplayerResult result = CreateLobby(host, PublicOptions(new SessionRules(capacity)));

            Assert.That(result.Reason, Is.EqualTo(MultiplayerReason.InvalidCapacity));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(host.States, Is.Empty);
            Assert.That(_network.Lobbies, Is.Empty);
        }

        [Test]
        public void HostConnectionCheck_RefusesAPlayerBeyondCapacity_AndKeepsTheOthers()
        {
            // FR-16: two players who pass the lobby's capacity check at the same moment.
            Node host = CreateHost("Host", rules: new SessionRules(2));
            Node a = JoinNew("A", host);
            _network.IgnoreLobbyCapacity = true;
            Node b = CreateNode("B");

            Assert.That(Join(b, host).Reason, Is.EqualTo(MultiplayerReason.SessionFull));
            Assert.That(b.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));
            Assert.That(host.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id, a.Id }));
            Assert.That(_network.FindLobby(host.Manager.CurrentLobbyId).Members.Count, Is.EqualTo(2));
            Assert.That(host.Left, Is.Empty);
        }

        [Test]
        public void UpdateRules_BelowThePlayerCountOrDuringAGame_IsRefused()
        {
            // FR-17
            SessionRules rules = new SessionRules(4, requireReady: false);
            Node host = CreateHost("Host", rules: rules);
            Node a = JoinNew("A", host);
            JoinNew("B", host);

            Assert.That(host.Manager.UpdateRules(rules.WithCapacity(2)).Reason, Is.EqualTo(MultiplayerReason.InvalidCapacity));
            Assert.That(a.Manager.UpdateRules(rules.WithCapacity(3)).Reason, Is.EqualTo(MultiplayerReason.NotHost));

            Assert.That(host.Manager.UpdateRules(rules.WithCapacity(3)).IsSuccess, Is.True);
            Pump();
            Assert.That(a.Manager.Rules.Capacity, Is.EqualTo(3));
            Assert.That(_network.FindLobby(host.Manager.CurrentLobbyId).Capacity, Is.EqualTo(3));

            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Assert.That(host.Manager.UpdateRules(rules).Reason, Is.EqualTo(MultiplayerReason.InvalidState));
        }

        [Test]
        public void StartGame_ReportsAMissingPlayerCountOrWhoIsNotReady()
        {
            // FR-18
            Node host = CreateHost("Host", rules: new SessionRules(4, minPlayersToStart: 2, requireReady: true));

            StartGameResult alone = host.Manager.StartGame();
            Assert.That(alone.Reason, Is.EqualTo(MultiplayerReason.StartConditionNotMet));
            Assert.That(alone.NotReadyPlayers, Is.Empty);

            Node a = JoinNew("A", host);
            StartGameResult notReady = host.Manager.StartGame();
            Assert.That(notReady.Reason, Is.EqualTo(MultiplayerReason.StartConditionNotMet));
            Assert.That(notReady.NotReadyPlayers, Is.EqualTo(new[] { a.Id }));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Lobby));

            a.Manager.SetReady(true);
            Pump();
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
        }

        [Test]
        public void LateJoinDenied_TheRunningGameLeavesSearchAndRefusesJoins()
        {
            // FR-19
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            JoinNew("A", host);
            host.Manager.StartGame();
            Pump();

            Node seeker = CreateNode("Seeker");
            Assert.That(Search(seeker).Lobbies, Is.Empty);
            Assert.That(Join(seeker, host).Reason, Is.EqualTo(MultiplayerReason.GameInProgress));
            Assert.That(seeker.Manager.State, Is.EqualTo(SessionState.Offline));
        }

        [Test]
        public void LateJoinDenied_HostRefusesAPlayerWhoEnteredTheLobbyBeforeTheStart()
        {
            // FR-19: the player is past the lobby when the game starts, so only the host can refuse.
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node late = CreateNode("Late");
            MultiplayerResult? result = null;
            late.Manager.JoinLobby(host.Manager.CurrentLobbyId, r => result = r);
            late.Manager.Update();

            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();

            Assert.That(result?.Reason, Is.EqualTo(MultiplayerReason.GameInProgress));
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(1));
            Assert.That(_network.FindLobby(host.Manager.CurrentLobbyId).Members.Count, Is.EqualTo(1));
        }

        [Test]
        public void LateJoinAllowed_TheJoinerEntersTheRunningGameAsALateJoiner()
        {
            // FR-19
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false, allowLateJoin: true));
            byte[] startData = { 9, 8, 7 };
            host.Manager.StartGame(startData);
            Pump();

            Node seeker = CreateNode("Seeker");
            LobbySearchResult search = Search(seeker);
            Assert.That(search.Lobbies.Single().IsInGame, Is.True);

            Assert.That(Join(seeker, host).IsSuccess, Is.True);
            Assert.That(seeker.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(seeker.GameStarts.Single(), Is.EqualTo(startData));
            Assert.That(seeker.Manager.TryGetParticipant(seeker.Id, out Participant self) && self.IsLateJoiner, Is.True);
            Assert.That(host.Joined.Single().IsLateJoiner, Is.True);
        }

        [Test]
        public void EndGame_ReturnsEveryoneToTheLobby_ClearsReadyFlags_AndReopensJoins()
        {
            // FR-26, FR-19
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: true));
            Node a = JoinNew("A", host);
            a.Manager.SetReady(true);
            Pump();
            Assert.That(host.Manager.StartGame(new byte[] { 1 }).IsSuccess, Is.True);
            Pump();
            Node seeker = CreateNode("Seeker");
            Assert.That(Search(seeker).Lobbies, Is.Empty);

            Assert.That(host.Manager.EndGame().IsSuccess, Is.True);
            Pump();

            foreach (Node node in new[] { host, a })
            {
                Assert.That(node.Manager.State, Is.EqualTo(SessionState.Lobby));
                Assert.That(node.Manager.Participants.All(participant => !participant.IsReady), Is.True);
                Assert.That(node.Manager.StartData.IsEmpty, Is.True);
                Assert.That(node.ReturnedToLobbyCount, Is.EqualTo(1));
            }

            Assert.That(Search(seeker).Lobbies.Count, Is.EqualTo(1));
            Assert.That(Join(seeker, host).IsSuccess, Is.True);
        }

        [Test]
        public void DuplicateAccount_TheSecondConnectionIsRefused()
        {
            // FR-21
            Node host = CreateHost("Host");
            Node a = JoinNew("A", host);
            Node twin = CreateNode("Twin", id: a.Id);

            Assert.That(Join(twin, host).Reason, Is.EqualTo(MultiplayerReason.DuplicateAccount));
            Assert.That(twin.States.Count, Is.EqualTo(2));
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(2));
            Assert.That(host.Left, Is.Empty);
        }

        [Test]
        public void Kick_EndsThePlayersSession_AndBlocksTheirReturn()
        {
            // FR-22
            Node host = CreateHost("Host");
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);

            Assert.That(b.Manager.Kick(a.Id).Reason, Is.EqualTo(MultiplayerReason.NotHost));
            Assert.That(host.Manager.Kick(a.Id).IsSuccess, Is.True);
            Pump();

            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.Kicked));
            foreach (Node remaining in new[] { host, b })
            {
                Assert.That(remaining.Left.Single().Participant.Id, Is.EqualTo(a.Id));
                Assert.That(remaining.Left.Single().Reason, Is.EqualTo(MultiplayerReason.Kicked));
            }

            Assert.That(Join(a, host).Reason, Is.EqualTo(MultiplayerReason.Banned));
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(2));
        }
        #endregion

        #region Lifecycle
        [Test]
        public void StateChanged_ReportsEveryTransitionOnce()
        {
            // FR-23
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);

            host.Manager.StartGame();
            Pump();
            host.Manager.EndGame();
            Pump();
            a.Manager.Leave();
            Pump();

            Assert.That(a.States.Select(change => change.ToString()), Is.EqualTo(new[]
            {
                "Offline -> Joining (None)",
                "Joining -> Lobby (None)",
                "Lobby -> InGame (None)",
                "InGame -> Lobby (None)",
                "Lobby -> Offline (LeftNormally)",
            }));
            Assert.That(host.States.Select(change => change.ToString()), Is.EqualTo(new[]
            {
                "Offline -> Creating (None)",
                "Creating -> Lobby (None)",
                "Lobby -> InGame (None)",
                "InGame -> Lobby (None)",
            }));
        }

        [Test]
        public void StartGame_DeliversTheSameStartDataToEveryone()
        {
            // FR-24
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node[] clients = { JoinNew("A", host), JoinNew("B", host), JoinNew("C", host) };
            byte[] startData = { 1, 2, 3 };

            Assert.That(host.Manager.StartGame(startData).IsSuccess, Is.True);
            Pump();

            foreach (Node node in clients.Prepend(host))
            {
                Assert.That(node.Manager.State, Is.EqualTo(SessionState.InGame));
                Assert.That(node.GameStarts.Single(), Is.EqualTo(startData));
                Assert.That(node.Manager.StartData.ToArray(), Is.EqualTo(startData));
            }
        }

        [Test]
        public void HostLeaving_EndsTheSessionForEveryPlayer()
        {
            // FR-27
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node[] clients = { JoinNew("A", host), JoinNew("B", host), JoinNew("C", host) };
            host.Manager.StartGame();
            Pump();

            host.Manager.Leave();
            Pump();

            foreach (Node client in clients)
            {
                Assert.That(client.Manager.State, Is.EqualTo(SessionState.Offline));
                Assert.That(client.LastChange.Reason, Is.EqualTo(MultiplayerReason.HostLeft));
                Assert.That(client.Manager.Participants, Is.Empty);
            }

            Assert.That(_network.Lobbies, Is.Empty);
        }

        [Test]
        public void HostCrash_EndsTheSession_AndTheLobbyDisappears()
        {
            // FR-27: the lobby passes to a remaining member, but nobody keeps the session going.
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node[] clients = { JoinNew("A", host), JoinNew("B", host) };
            host.Manager.StartGame();
            Pump();

            host.Backend.SimulateCrash();
            Pump();

            foreach (Node client in clients)
            {
                Assert.That(client.Manager.State, Is.EqualTo(SessionState.Offline));
                Assert.That(client.LastChange.Reason, Is.EqualTo(MultiplayerReason.HostLeft));
            }

            Assert.That(Search(CreateNode("Seeker")).Lobbies, Is.Empty);
            Assert.That(_network.Lobbies, Is.Empty);
        }

        [Test]
        public void ClientDispose_TheHostSeesANormalLeave()
        {
            // FR-28
            Node host = CreateHost("Host");
            Node a = JoinNew("A", host);

            a.Manager.Dispose();
            Pump();

            Assert.That(host.Left.Single().Participant.Id, Is.EqualTo(a.Id));
            Assert.That(host.Left.Single().Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            Assert.That(_network.FindLobby(host.Manager.CurrentLobbyId).Members.Count, Is.EqualTo(1));
        }
        #endregion

        #region Connection
        [Test]
        public void HostThatDoesNotListen_JoinFailsAndLeavesTheLobby()
        {
            // FR-29
            LocalBackend silent = CreateRawBackend("Silent");
            LobbyId lobby = CreateRawLobby(silent, GameId, 1);
            Node a = CreateNode("A");

            Assert.That(Join(a, lobby).Reason, Is.EqualTo(MultiplayerReason.ConnectionFailed));
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_network.FindLobby(lobby).Members.Count, Is.EqualTo(1));
        }

        [Test]
        public void VersionMismatch_DirectJoinIsRefused_BeforeReachingTheHost()
        {
            // FR-30
            Node host = CreateHost(CreateNode("Host", protocolVersion: 3), PublicOptions(SessionRules.Default));
            Node old = CreateNode("Old", protocolVersion: 2);

            Assert.That(Join(old, host).Reason, Is.EqualTo(MultiplayerReason.VersionMismatch));
            Assert.That(old.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(old.States.Select(change => change.ToString()), Is.EqualTo(new[]
            {
                "Offline -> Joining (None)",
                "Joining -> Offline (VersionMismatch)",
            }));
            Assert.That(host.Joined, Is.Empty);
            Assert.That(_network.FindLobby(host.Manager.CurrentLobbyId).Members.Count, Is.EqualTo(1));
        }

        [Test]
        public void HelloWithAnotherVersion_IsRefusedByTheHost()
        {
            // FR-30, NFR-5: the host checks the version itself, whatever the lobby data said.
            Node host = CreateHost("Host");
            RecordingListener listener = new RecordingListener();
            LocalBackend raw = CreateRawBackend("Raw", listener);
            ConnectionId connection = raw.Connect(host.Id);
            Pump();

            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.Hello);
            writer.WriteInt(WireProtocol.Version);
            writer.WriteString(GameId);
            writer.WriteInt(99);
            raw.Send(connection, writer.Written, DeliveryMode.Reliable);
            Pump();

            Assert.That(listener.Disconnected.Single().Connection, Is.EqualTo(connection));
            Assert.That(listener.Disconnected.Single().ReasonCode, Is.EqualTo(ReasonCodes.ToWire(MultiplayerReason.VersionMismatch)));
            Assert.That(host.Joined, Is.Empty);
        }

        [Test]
        public void MalformedOrClientOnlyPackets_AreDropped_AndTheSessionGoesOn()
        {
            // NFR-5
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);
            RecordingListener listener = new RecordingListener();
            LocalBackend raw = CreateRawBackend("Raw", listener);
            EnterLobby(raw, host);
            ConnectionId connection = raw.Connect(host.Id);
            Pump();

            raw.Send(connection, new byte[] { 0xFF, 0x01 }, DeliveryMode.Reliable);
            raw.Send(connection, new byte[] { (byte)PacketKind.Hello, 0xFF, 0xFF, 0xFF, 0x7F }, DeliveryMode.Reliable);
            Pump();

            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Lobby));
            Assert.That(host.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id, a.Id }));
            Assert.That(listener.Disconnected, Is.Empty);

            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.Hello);
            writer.WriteInt(WireProtocol.Version);
            writer.WriteString(GameId);
            writer.WriteInt(1);
            writer.WriteBool(false);
            raw.Send(connection, writer.Written, DeliveryMode.Reliable);
            Pump();
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(3));

            // Once accepted, a client still cannot speak for the host.
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            writer.Begin(PacketKind.ReturnedToLobby);
            raw.Send(connection, writer.Written, DeliveryMode.Reliable);
            Pump();

            Assert.That(host.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.InGame));
        }

        [Test]
        public void JoinTimeout_WhenTheHostNeverAnswers()
        {
            // NFR-3
            LocalBackend mute = CreateRawBackend("Mute");
            mute.StartListening();
            LobbyId lobby = CreateRawLobby(mute, GameId, 1);
            Node a = CreateNode("A");
            MultiplayerResult? result = null;
            a.Manager.JoinLobby(lobby, r => result = r);
            Pump();

            _now = 14d;
            Pump();
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Joining));

            _now = 15d;
            Pump();
            Assert.That(result?.Reason, Is.EqualTo(MultiplayerReason.Timeout));
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_network.FindLobby(lobby).Members.Count, Is.EqualTo(1));
        }

        [Test]
        public void HandshakeTimeout_TheHostClosesAConnectionThatNeverSaysHello()
        {
            Node host = CreateHost("Host");
            RecordingListener listener = new RecordingListener();
            LocalBackend raw = CreateRawBackend("Raw", listener);
            ConnectionId connection = raw.Connect(host.Id);
            Pump();

            _now = 9d;
            Pump();
            Assert.That(listener.Disconnected, Is.Empty);

            _now = 10d;
            Pump();
            Assert.That(listener.Disconnected.Single().Connection, Is.EqualTo(connection));
            Assert.That(listener.Disconnected.Single().ReasonCode, Is.EqualTo(ReasonCodes.ToWire(MultiplayerReason.Timeout)));
        }

        [Test]
        public void MalformedPacketsAfterTheWelcome_AreDropped()
        {
            // NFR-5: a short SetReady, and a bool that is neither 0 nor 1.
            Node host = CreateHost("Host");
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);

            raw.Send(connection, new[] { (byte)PacketKind.SetReady }, DeliveryMode.Reliable);
            raw.Send(connection, new byte[] { (byte)PacketKind.SetReady, 2 }, DeliveryMode.Reliable);
            Pump();

            Assert.That(host.Manager.TryGetParticipant(raw.LocalPlayerId, out Participant participant), Is.True);
            Assert.That(participant.IsReady, Is.False);
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Lobby));
        }

        [Test]
        public void MalformedWelcomeFromAHost_IsDropped_AndTheJoinWaitsForItsTimeout()
        {
            RecordingListener listener = new RecordingListener();
            LocalBackend fakeHost = CreateRawBackend("FakeHost", listener);
            fakeHost.StartListening();
            LobbyId lobby = CreateRawLobby(fakeHost, GameId, 1);
            Node a = CreateNode("A");
            a.Manager.JoinLobby(lobby);
            Pump();

            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.Welcome);
            writer.WriteByte((byte)SessionState.Lobby);
            writer.WriteULong(fakeHost.LocalPlayerId.Value);
            writer.WriteInt(4);
            writer.WriteInt(1);
            writer.WriteBool(true);
            writer.WriteBool(false);
            writer.WriteBytes(ReadOnlySpan<byte>.Empty);
            writer.WriteInt(0);
            fakeHost.Send(listener.Connected.Single(), writer.Written, DeliveryMode.Reliable);
            fakeHost.Send(listener.Connected.Single(), new[] { (byte)PacketKind.ParticipantJoined }, DeliveryMode.Reliable);
            Pump();

            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Joining));

            _now = 15d;
            Pump();
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.Timeout));
        }

        [Test]
        public void ClientCloseReason_OtherThanLeaving_IsReportedAsAConnectionLoss()
        {
            // NFR-5: a client that closes as if kicked cannot make the others see a kick.
            Node host = CreateHost("Host");
            Node b = JoinNew("B", host);
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);

            raw.Disconnect(connection, ReasonCodes.ToWire(MultiplayerReason.Kicked));
            Pump();

            Assert.That(host.Left.Single().Participant.Id, Is.EqualTo(raw.LocalPlayerId));
            Assert.That(host.Left.Single().Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            Assert.That(b.Left.Single().Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
        }

        [Test]
        public void StartData_AboveTheLimit_IsRefused_AndTheLimitItselfReachesEveryone()
        {
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);

            StartGameResult tooLarge = host.Manager.StartGame(new byte[MultiplayerLimits.MaxStartDataSize + 1]);
            Assert.That(tooLarge.Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Lobby));

            Assert.That(host.Manager.StartGame(new byte[MultiplayerLimits.MaxStartDataSize]).IsSuccess, Is.True);
            Pump();
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(a.GameStarts.Single().Length, Is.EqualTo(MultiplayerLimits.MaxStartDataSize));
        }
        #endregion

        #region Re-entrancy And Stale Answers
        [Test]
        public void StateHandlerThatLeaves_AnswersCreateAndJoinOnce_WithTheReason()
        {
            // FR-23 and the one-shot callback rule: a handler of the success transition ends the session.
            Node host = CreateNode("Host");
            host.Manager.OnStateChanged += change =>
            {
                if (change.Current == SessionState.Lobby)
                {
                    host.Manager.Leave();
                }
            };
            List<MultiplayerResult> created = new List<MultiplayerResult>();
            host.Manager.CreateLobby(PublicOptions(SessionRules.Default), created.Add);
            Pump();

            Assert.That(created.Select(result => result.Reason), Is.EqualTo(new[] { MultiplayerReason.LeftNormally }));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_network.Lobbies, Is.Empty);

            Node realHost = CreateHost("RealHost");
            Node a = CreateNode("A");
            a.Manager.OnStateChanged += change =>
            {
                if (change.Current == SessionState.Lobby)
                {
                    a.Manager.Leave();
                }
            };
            List<MultiplayerResult> joined = new List<MultiplayerResult>();
            a.Manager.JoinLobby(realHost.Manager.CurrentLobbyId, joined.Add);
            Pump();

            Assert.That(joined.Select(result => result.Reason), Is.EqualTo(new[] { MultiplayerReason.LeftNormally }));
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(realHost.Manager.Participants.Count, Is.EqualTo(1));
        }

        [Test]
        public void LeaveBeforeTheLobbyAnswers_AnswersOnce_AndTheLateAnswerLeavesTheLobby()
        {
            Node host = CreateHost("Host");
            Node a = CreateNode("A");
            List<MultiplayerResult> results = new List<MultiplayerResult>();

            a.Manager.JoinLobby(host.Manager.CurrentLobbyId, results.Add);
            a.Manager.Leave();
            Pump();

            Assert.That(results.Select(result => result.Reason), Is.EqualTo(new[] { MultiplayerReason.LeftNormally }));
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_network.FindLobby(host.Manager.CurrentLobbyId).Members.Count, Is.EqualTo(1));
            Assert.That(host.Joined, Is.Empty);
        }

        [Test]
        public void LeaveAfterHelloBeforeWelcome_TheHostKeepsNoTraceOfThePlayer()
        {
            Node host = CreateHost("Host");
            Node a = CreateNode("A");

            a.Manager.JoinLobby(host.Manager.CurrentLobbyId);
            a.Manager.Update(); // enters the lobby and starts connecting
            a.Manager.Update(); // connected; the hello is on its way
            a.Manager.Leave();
            Pump();

            Assert.That(host.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id }));
            Assert.That(host.Joined, Is.Empty);
            Assert.That(host.Left, Is.Empty);
            Assert.That(_network.FindLobby(host.Manager.CurrentLobbyId).Members.Count, Is.EqualTo(1));
        }

        [Test]
        public void LobbyPassingToAnotherMember_EndsTheSession_EvenWhileTheHostConnectionLives()
        {
            // FR-27
            Node host = CreateHost("Host");
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);

            host.Backend.LeaveLobby(host.Manager.CurrentLobbyId);
            Pump();

            foreach (Node client in new[] { a, b })
            {
                Assert.That(client.Manager.State, Is.EqualTo(SessionState.Offline));
                Assert.That(client.LastChange.Reason, Is.EqualTo(MultiplayerReason.HostLeft));
            }
        }

        [Test]
        public void JoinRequestHandledByItsHandler_TheManagerAddsNothing()
        {
            Node first = CreateHost("First");
            Node second = CreateHost("Second");
            Node player = CreateNode("Player");
            player.Manager.OnJoinRequested += lobby => player.Manager.JoinLobby(second.Manager.CurrentLobbyId);

            player.Backend.RequestJoin(first.Manager.CurrentLobbyId);
            Pump();

            Assert.That(player.Manager.CurrentLobbyId, Is.EqualTo(second.Manager.CurrentLobbyId));
            Assert.That(first.Joined, Is.Empty);
            Assert.That(second.Joined.Single().Id, Is.EqualTo(player.Id));
        }

        [Test]
        public void JoinRequestBeforeInitialize_IsKeptUntilTheManagerListens()
        {
            // FR-7: a launch from an invite names its lobby before the game has set anything up.
            Node host = CreateHost("Host");
            LocalBackend backend = new LocalBackend(_network, "Launched");
            backend.RequestJoin(host.Manager.CurrentLobbyId);
            backend.Update();

            Node launched = AttachNode(backend);
            Pump();

            Assert.That(launched.JoinRequests, Is.EqualTo(new[] { host.Manager.CurrentLobbyId }));
            Assert.That(launched.Manager.State, Is.EqualTo(SessionState.Lobby));
        }
        #endregion

        [Test]
        public void AConnectionFromOutsideTheLobby_WaitsForMembership_AndTimesOutWithout()
        {
            // FR-5, NFR-5: knowing the host's account is not enough to enter an invite-only session.
            Node host = CreateHost("Host", LobbyVisibility.InviteOnly);
            RecordingListener outsiderListener = new RecordingListener();
            LocalBackend outsider = CreateRawBackend("Outsider", outsiderListener);
            LocalBackend latecomer = CreateRawBackend("Latecomer", new RecordingListener());

            ConnectionId outsiderConnection = SayHello(outsider, host);
            SayHello(latecomer, host);
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(1));

            EnterLobby(latecomer, host);
            Assert.That(host.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id, latecomer.LocalPlayerId }));

            _now = 10d;
            Pump();
            Assert.That(outsiderListener.Disconnected.Single().Connection, Is.EqualTo(outsiderConnection));
            Assert.That(outsiderListener.Disconnected.Single().ReasonCode, Is.EqualTo(ReasonCodes.ToWire(MultiplayerReason.Timeout)));
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(2));
        }

        #region Requests
        [Test]
        public void Requests_BeforeInitialize_ReportNotInitialized()
        {
            using MultiplayerManager manager = new MultiplayerManager();
            MultiplayerResult? created = null;
            LobbySearchResult? searched = null;

            manager.CreateLobby(PublicOptions(SessionRules.Default), r => created = r);
            manager.SearchLobbies(null, r => searched = r);

            Assert.That(created?.Reason, Is.EqualTo(MultiplayerReason.NotInitialized));
            Assert.That(searched?.Reason, Is.EqualTo(MultiplayerReason.NotInitialized));
            Assert.That(manager.SetReady(true).Reason, Is.EqualTo(MultiplayerReason.NotInitialized));
            Assert.That(manager.StartGame().Reason, Is.EqualTo(MultiplayerReason.NotInitialized));
        }

        [Test]
        public void SessionRequests_InTheWrongState_ReportInvalidState()
        {
            Node host = CreateHost("Host");
            Node a = JoinNew("A", host);

            Assert.That(CreateLobby(host, PublicOptions(SessionRules.Default)).Reason, Is.EqualTo(MultiplayerReason.InvalidState));
            Assert.That(Join(a, host).Reason, Is.EqualTo(MultiplayerReason.InvalidState));
            Assert.That(host.Manager.EndGame().Reason, Is.EqualTo(MultiplayerReason.InvalidState));
            Assert.That(a.Manager.StartGame().Reason, Is.EqualTo(MultiplayerReason.NotHost));
            Assert.That(CreateNode("Idle").Manager.SetReady(true).Reason, Is.EqualTo(MultiplayerReason.InvalidState));
        }
        #endregion

        #region Helpers
        private Node CreateNode(string name, int protocolVersion = 1, string gameId = GameId, PlayerId id = default, Action<MultiplayerConfig> configure = null)
        {
            return AttachNode(new LocalBackend(_network, name, id), protocolVersion, gameId, configure);
        }

        private Node AttachNode(LocalBackend backend, int protocolVersion = 1, string gameId = GameId, Action<MultiplayerConfig> configure = null)
        {
            MultiplayerManager manager = new MultiplayerManager();
            MultiplayerConfig config = new MultiplayerConfig(gameId, protocolVersion) { Clock = () => _now };
            configure?.Invoke(config);
            Assert.That(manager.Initialize(backend, config).IsSuccess, Is.True);

            Node node = new Node { Backend = backend, Manager = manager };
            manager.OnStateChanged += change => node.States.Add(change);
            manager.OnParticipantJoined += participant => node.Joined.Add(participant);
            manager.OnParticipantLeft += (participant, reason) => node.Left.Add((participant, reason));
            manager.OnParticipantChanged += participant => node.Changed.Add(participant);
            manager.OnParticipantDataChanged += (participant, key) => node.DataChanges.Add((participant, key));
            manager.OnLobbyDataChanged += (key, value) => node.LobbyDataChanges.Add((key, value));
            manager.OnChatReceived += (sender, text) => node.Chats.Add((sender, text));
            manager.OnGameStarted += data => node.GameStarts.Add(data.ToArray());
            manager.OnReturnedToLobby += () => node.ReturnedToLobbyCount++;
            manager.OnJoinRequested += lobby => node.JoinRequests.Add(lobby);
            manager.OnRateLimitExceeded += participant => node.RateLimited.Add(participant);
            manager.OnParticipantDisconnected += participant => node.Disconnected.Add(participant);
            manager.OnParticipantReconnected += participant => node.Reconnected.Add(participant);
            manager.OnAllPlayersLoaded += () => node.AllLoadedCount++;
            manager.OnLoadTimedOut += players => node.LoadTimeouts.Add(players.ToArray());

            _nodes.Add(node);
            return node;
        }

        private LocalBackend CreateRawBackend(string name, IMultiplayerBackendListener listener = null)
        {
            LocalBackend backend = new LocalBackend(_network, name);
            backend.SetListener(listener);
            _rawBackends.Add(backend);
            return backend;
        }

        /// <summary>A lobby that looks like a session of this game but has no manager behind it.</summary>
        private LobbyId CreateRawLobby(LocalBackend owner, string gameId, int protocolVersion, string wireVersion = null)
        {
            Dictionary<string, string> data = new Dictionary<string, string>
            {
                [LobbyDataKeys.GameId] = gameId,
                [LobbyDataKeys.ProtocolVersion] = protocolVersion.ToString(CultureInfo.InvariantCulture),
                [LobbyDataKeys.WireVersion] = wireVersion ?? WireProtocol.VersionText,
                [LobbyDataKeys.HostId] = owner.LocalPlayerId.Value.ToString(CultureInfo.InvariantCulture),
                [LobbyDataKeys.Name] = "Raw",
                [LobbyDataKeys.State] = LobbyDataKeys.StateLobby,
            };

            LobbyId created = LobbyId.None;
            owner.CreateLobby(LobbyVisibility.Public, 4, data, (reason, lobby) => created = lobby);
            owner.Update();
            Assert.That(created.IsValid, Is.True);
            return created;
        }

        /// <summary>Connects a raw backend to the host and has it accepted as a player.</summary>
        private ConnectionId ConnectAsPlayer(LocalBackend raw, Node host)
        {
            EnterLobby(raw, host);
            ConnectionId connection = raw.Connect(host.Id);
            Pump();

            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.Hello);
            writer.WriteInt(WireProtocol.Version);
            writer.WriteString(GameId);
            writer.WriteInt(1);
            writer.WriteBool(false);
            raw.Send(connection, writer.Written, DeliveryMode.Reliable);
            Pump();

            Assert.That(host.Manager.TryGetParticipant(raw.LocalPlayerId, out _), Is.True);
            return connection;
        }

        /// <summary>Connects a raw backend to the host and says hello, without entering the lobby.</summary>
        private ConnectionId SayHello(LocalBackend raw, Node host)
        {
            ConnectionId connection = raw.Connect(host.Id);
            Pump();

            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.Hello);
            writer.WriteInt(WireProtocol.Version);
            writer.WriteString(GameId);
            writer.WriteInt(1);
            writer.WriteBool(false);
            raw.Send(connection, writer.Written, DeliveryMode.Reliable);
            Pump();
            return connection;
        }

        /// <summary>Makes a raw backend a member of the host's lobby.</summary>
        private void EnterLobby(LocalBackend raw, Node host)
        {
            MultiplayerReason? entered = null;
            raw.JoinLobby(host.Manager.CurrentLobbyId, reason => entered = reason);
            Pump();
            Assert.That(entered, Is.EqualTo(MultiplayerReason.None));
        }

        private static LobbyCreateOptions PublicOptions(SessionRules rules)
        {
            return new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = rules };
        }

        private Node CreateHost(string name, LobbyVisibility visibility = LobbyVisibility.Public, SessionRules? rules = null)
        {
            LobbyCreateOptions options = PublicOptions(rules ?? SessionRules.Default);
            options.Visibility = visibility;
            return CreateHost(CreateNode(name), options);
        }

        private Node CreateHost(Node node, LobbyCreateOptions options)
        {
            Assert.That(CreateLobby(node, options).IsSuccess, Is.True);
            return node;
        }

        private MultiplayerResult CreateLobby(Node node, LobbyCreateOptions options)
        {
            MultiplayerResult? result = null;
            node.Manager.CreateLobby(options, r => result = r);
            Pump();
            Assert.That(result.HasValue, Is.True, "CreateLobby did not answer.");
            return result.Value;
        }

        private Node JoinNew(string name, Node host)
        {
            Node node = CreateNode(name);
            Assert.That(Join(node, host).IsSuccess, Is.True);
            return node;
        }

        private MultiplayerResult Join(Node node, Node host) => Join(node, host.Manager.CurrentLobbyId);

        private MultiplayerResult Join(Node node, LobbyId lobby)
        {
            MultiplayerResult? result = null;
            node.Manager.JoinLobby(lobby, r => result = r);
            Pump();
            Assert.That(result.HasValue, Is.True, "JoinLobby did not answer.");
            return result.Value;
        }

        private LobbySearchResult Search(Node node, LobbySearchQuery query = null)
        {
            LobbySearchResult? result = null;
            node.Manager.SearchLobbies(query, r => result = r);
            Pump();
            Assert.That(result.HasValue, Is.True, "SearchLobbies did not answer.");
            return result.Value;
        }

        /// <summary>Runs enough frames on every player for any request and its answers to settle.</summary>
        private void Pump(int frames = 8)
        {
            for (int frame = 0; frame < frames; frame++)
            {
                for (int i = 0; i < _nodes.Count; i++)
                {
                    _nodes[i].Manager.Update();
                }

                for (int i = 0; i < _rawBackends.Count; i++)
                {
                    _rawBackends[i].Update();
                }
            }
        }
        #endregion
    }
}
