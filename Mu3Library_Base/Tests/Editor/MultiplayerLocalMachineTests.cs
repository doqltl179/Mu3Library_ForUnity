using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Local.Machine;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The machine-wide <see cref="LocalNetwork"/> of FR-38: backends that reach each other only
    /// through loopback sockets and the network's folder, as separate processes on one computer
    /// do. Every backend here listens on a port of its own and writes a lobby file of its own, so
    /// one process can play all of them; the tests wait in real time for the sockets to deliver.
    /// </summary>
    public class MultiplayerLocalMachineTests
    {
        private const string GameId = "tests.multiplayer.machine";
        private const ushort Chat = 1;
        private const double WaitSeconds = 10.0;

        private sealed class Node
        {
            public LocalNetwork Network;
            public LocalBackend Backend;
            public MultiplayerManager Manager;
            public readonly List<SessionStateChange> States = new List<SessionStateChange>();
            public readonly List<byte[]> GameStarts = new List<byte[]>();
            public readonly List<(PlayerId Sender, byte[] Payload)> Messages = new List<(PlayerId, byte[])>();
            public readonly List<(PlayerId Sender, string Text)> Chats = new List<(PlayerId, string)>();

            public PlayerId Id => Backend.LocalPlayerId;
        }

        private sealed class RecordingListener : IMultiplayerBackendListener
        {
            public readonly List<(ConnectionId Connection, PlayerId Remote, bool IsIncoming)> Connected =
                new List<(ConnectionId, PlayerId, bool)>();
            public readonly List<(ConnectionId Connection, BackendDisconnectKind Kind, int ReasonCode)> Disconnected =
                new List<(ConnectionId, BackendDisconnectKind, int)>();
            public readonly List<byte[]> Received = new List<byte[]>();
            public readonly List<(LobbyId Lobby, PlayerId Owner)> OwnerChanges = new List<(LobbyId, PlayerId)>();

            public void OnConnected(ConnectionId connection, PlayerId remote, bool isIncoming) => Connected.Add((connection, remote, isIncoming));
            public void OnDisconnected(ConnectionId connection, PlayerId remote, BackendDisconnectKind kind, int reasonCode) => Disconnected.Add((connection, kind, reasonCode));
            public void OnReceived(ConnectionId connection, PlayerId remote, ReadOnlySpan<byte> data) => Received.Add(data.ToArray());
            public void OnLobbyOwnerChanged(LobbyId lobby, PlayerId newOwner) => OwnerChanges.Add((lobby, newOwner));
            public void OnLobbyJoinRequested(LobbyId lobby) { }
        }

        /// <summary>A raw backend with what its listener heard.</summary>
        private sealed class Peer
        {
            public LocalBackend Backend;
            public RecordingListener Events;

            public PlayerId Id => Backend.LocalPlayerId;
        }

        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<LocalBackend> _rawBackends = new List<LocalBackend>();
        private readonly HashSet<LocalBackend> _paused = new HashSet<LocalBackend>();
        private string _folder;



        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "Mu3LibraryTests", Guid.NewGuid().ToString("N"));
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
            _paused.Clear();

            try
            {
                Directory.Delete(_folder, recursive: true);
            }
            catch (IOException)
            {
                // Nobody owned a lobby, or another program still holds a file; the folder is temporary.
            }
        }

        #region Network
        [Test]
        public void CreateMachineWide_TakesAFileSafeName()
        {
            LocalNetwork network = LocalNetwork.CreateMachineWide("My-Game_2");

            Assert.That(network.IsMachineWide, Is.True);
            Assert.That(network.MachineWideName, Is.EqualTo("My-Game_2"));
            Assert.That(new LocalNetwork().IsMachineWide, Is.False);

            foreach (string name in new[] { null, string.Empty, "two words", "../escape", "dot.name", "CON", "nul", "com1", "LPT9", new string('a', LocalNetwork.MaxNameLength + 1) })
            {
                Assert.Throws<ArgumentException>(() => LocalNetwork.CreateMachineWide(name), name);
            }
        }

        [Test]
        public void Backends_PickDifferentRandomIds_AndListenOnPortsOfTheirOwn()
        {
            LocalBackend a = CreateRawBackend(CreateNetwork(), "A");
            LocalBackend b = CreateRawBackend(CreateNetwork(), "B");

            Assert.That(a.LocalPlayerId.IsValid && b.LocalPlayerId.IsValid, Is.True);
            Assert.That(a.LocalPlayerId, Is.Not.EqualTo(b.LocalPlayerId));
            Assert.That(a.Port, Is.GreaterThan(0));
            Assert.That(a.Port, Is.Not.EqualTo(b.Port));
            Assert.That(a.IsAvailable, Is.True);
        }

        [Test]
        public void Listener_TakesConnectionsOnTheLoopbackAddressOnly()
        {
            // FR-38, D7: another computer reaches this one through one of its other addresses.
            LocalBackend backend = CreateRawBackend(CreateNetwork(), "Host");
            IPAddress outside = FindNonLoopbackAddress();
            if (outside == null)
            {
                Assert.Ignore("This computer has no network address besides loopback.");
            }

            Assert.That(CanConnect(IPAddress.Loopback, backend.Port), Is.True);
            Assert.That(CanConnect(outside, backend.Port), Is.False, $"{outside} reached the local backend.");
        }

        [Test]
        public void Lobbies_StayInsideTheirNetworksFolder()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);

            LocalNetwork other = LocalNetwork.CreateMachineWide("tests");
            other.MachineFolder = Path.Combine(_folder, "other");
            LocalBackend outsider = CreateRawBackend(other, "Outsider");

            Assert.That(Search(outsider), Is.Empty);
            Assert.That(Join(outsider, lobby), Is.EqualTo(MultiplayerReason.LobbyNotFound));
        }
        #endregion

        #region Session
        [Test]
        public void Session_FromLobbyToGameAndMessages_UntilTheHostLeaves()
        {
            // FR-38: lobby creation, search, join, start, and messages between "processes".
            Node host = CreateNode("Host");
            List<Node> clients = new List<Node> { CreateNode("A"), CreateNode("B"), CreateNode("C") };

            MultiplayerResult? created = null;
            host.Manager.CreateLobby(new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = new SessionRules(4) }, r => created = r);
            PumpUntil(() => created.HasValue, "the lobby");
            Assert.That(created.Value.IsSuccess, Is.True);

            LobbySearchResult? found = null;
            clients[0].Manager.SearchLobbies(null, r => found = r);
            PumpUntil(() => found.HasValue, "the search");
            Assert.That(found.Value.Lobbies.Select(lobby => lobby.Id), Is.EqualTo(new[] { host.Manager.CurrentLobbyId }));
            Assert.That(found.Value.Lobbies[0].HostId, Is.EqualTo(host.Id));

            foreach (Node client in clients)
            {
                MultiplayerResult? joined = null;
                client.Manager.JoinLobby(host.Manager.CurrentLobbyId, r => joined = r);
                PumpUntil(() => joined.HasValue, $"{client.Backend.PlayerName} to join");
                Assert.That(joined.Value.IsSuccess, Is.True, joined.Value.Reason.ToString());
            }

            PumpUntil(() => clients.All(client => client.Manager.Participants.Count == 4), "every roster to list four players");
            Assert.That(host.Manager.Participants.Select(participant => participant.Name), Is.EquivalentTo(new[] { "Host", "A", "B", "C" }));

            foreach (Node client in clients)
            {
                Assert.That(client.Manager.SetReady(true).IsSuccess, Is.True);
            }

            PumpUntil(() => host.Manager.Participants.All(participant => participant.IsHost || participant.IsReady), "every client to be ready");
            Assert.That(host.Manager.StartGame(new byte[] { 9, 8, 7 }).IsSuccess, Is.True);
            PumpUntil(() => clients.All(client => client.GameStarts.Count == 1), "the game to start everywhere");
            Assert.That(clients.Select(client => client.GameStarts[0]), Is.All.EqualTo(new byte[] { 9, 8, 7 }));

            Assert.That(clients[0].Manager.Send(Chat, new byte[] { 1 }, MessageTarget.All).IsSuccess, Is.True);
            Assert.That(host.Manager.Send(Chat, new byte[] { 2 }, MessageTarget.To(clients[2].Id)).IsSuccess, Is.True);
            PumpUntil(() => host.Messages.Count == 1 && clients[1].Messages.Count == 1 && clients[2].Messages.Count == 2, "the messages");
            // The host's own message leaves at once; A's passes through the host first.
            Assert.That(host.Messages.Single().Sender, Is.EqualTo(clients[0].Id));
            Assert.That(clients[1].Messages.Select(message => (message.Sender, message.Payload[0])), Is.EqualTo(new[] { (clients[0].Id, (byte)1) }));
            Assert.That(clients[2].Messages.Select(message => (message.Sender, message.Payload[0])), Is.EquivalentTo(new[] { (clients[0].Id, (byte)1), (host.Id, (byte)2) }));

            // NFR-4: the clients hear HostLeft, not a lost connection.
            host.Manager.Leave();
            PumpUntil(() => clients.All(client => client.Manager.State == SessionState.Offline), "every client to end its session", 2.0);
            Assert.That(clients.Select(client => client.States.Last().Reason), Is.All.EqualTo(MultiplayerReason.HostLeft));
        }

        [Test]
        public void LobbyAndParticipantData_ReachEveryProcess_AndLobbySearch()
        {
            // FR-11 between "processes": the host's lobby data and each player's own participant data.
            Node host = CreateNode("Host");
            Node a = CreateNode("A");
            Node b = CreateNode("B");
            StartSession(host, a, b);

            Assert.That(a.Manager.SetLobbyData("map", "desert").Reason, Is.EqualTo(MultiplayerReason.NotHost));
            Assert.That(host.Manager.SetLobbyData("map", "desert").IsSuccess, Is.True);
            Assert.That(a.Manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
            Assert.That(b.Manager.SetParticipantData("character", "archer").IsSuccess, Is.True);

            Node[] everyone = { host, a, b };
            PumpUntil(() => everyone.All(node => node.Manager.LobbyData.TryGetValue("map", out string map) && map == "desert"
                && CharacterOf(node, a.Id) == "knight" && CharacterOf(node, b.Id) == "archer"), "every player to see the data");
            Assert.That(everyone.Select(node => CharacterOf(node, host.Id)), Is.All.Null);

            // Another process's search reads the lobby file, which catches up a moment later.
            LocalBackend seeker = CreateRawBackend(CreateNetwork(), "Seeker");
            string seen = null;
            Stopwatch waited = Stopwatch.StartNew();
            while (seen != "desert" && waited.Elapsed.TotalSeconds < WaitSeconds)
            {
                foreach (BackendLobby lobby in Search(seeker))
                {
                    if (lobby.Id == host.Manager.CurrentLobbyId)
                    {
                        lobby.Data.TryGetValue("map", out seen);
                    }
                }
            }

            Assert.That(seen, Is.EqualTo("desert"));
        }

        [Test]
        public void Chat_ReachesEveryProcess_InTheSameOrder()
        {
            // FR-12 between "processes".
            Node host = CreateNode("Host");
            Node a = CreateNode("A");
            Node b = CreateNode("B");
            StartSession(host, a, b);

            Assert.That(a.Manager.SendChat("from A").IsSuccess, Is.True);
            Assert.That(host.Manager.SendChat("from the host").IsSuccess, Is.True);
            Assert.That(b.Manager.SendChat(new string('x', MultiplayerLimits.MaxChatMessageSize + 1)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));

            Node[] everyone = { host, a, b };
            PumpUntil(() => everyone.All(node => node.Chats.Count == 2), "every player to hear both messages");
            PumpFor(0.2);

            // The host's own message left first; A's passed through the host after it.
            (PlayerId, string)[] expected = { (host.Id, "from the host"), (a.Id, "from A") };
            Assert.That(everyone.Select(node => node.Chats), Is.All.EqualTo(expected));
        }

        [Test]
        public void Disconnect_ArrivesAfterTheDataSentBeforeIt_WithItsReason()
        {
            (_, RecordingListener hostEvents, LocalBackend client, RecordingListener clientEvents) = ConnectPair();
            ConnectionId toHost = clientEvents.Connected.Single().Connection;

            for (int i = 0; i < 200; i++)
            {
                Assert.That(client.Send(toHost, BitConverter.GetBytes(i), DeliveryMode.Reliable), Is.True);
            }

            client.Disconnect(toHost, 42);
            PumpUntil(() => hostEvents.Disconnected.Count == 1, "the close");

            Assert.That(hostEvents.Received.Select(data => BitConverter.ToInt32(data, 0)), Is.EqualTo(Enumerable.Range(0, 200)));
            Assert.That(hostEvents.Disconnected[0].Kind, Is.EqualTo(BackendDisconnectKind.ClosedByPeer));
            Assert.That(hostEvents.Disconnected[0].ReasonCode, Is.EqualTo(42));
            Assert.That(clientEvents.Disconnected, Is.Empty, "The side that closed hears nothing.");
        }

        [Test]
        public void LargestPacket_ArrivesWhole_AndALargerOneIsRefused()
        {
            (_, RecordingListener hostEvents, LocalBackend client, RecordingListener clientEvents) = ConnectPair();
            ConnectionId toHost = clientEvents.Connected.Single().Connection;
            byte[] packet = Enumerable.Range(0, client.MaxPacketSize).Select(i => (byte)i).ToArray();

            Assert.That(client.Send(toHost, packet, DeliveryMode.Reliable), Is.True);
            Assert.That(client.Send(toHost, new byte[client.MaxPacketSize + 1], DeliveryMode.Reliable), Is.False);
            PumpUntil(() => hostEvents.Received.Count == 1, "the packet");

            Assert.That(hostEvents.Received[0], Is.EqualTo(packet));
        }

        [Test]
        public void Crash_IsReportedAsALostConnection()
        {
            (LocalBackend host, _, _, RecordingListener clientEvents) = ConnectPair();

            host.SimulateCrash();
            PumpUntil(() => clientEvents.Disconnected.Count == 1, "the lost connection");

            Assert.That(clientEvents.Disconnected[0].Kind, Is.EqualTo(BackendDisconnectKind.ConnectionLost));
        }

        [Test]
        public void QuietConnection_IsLostAfterTheTimeout()
        {
            // NFR-3: a process that stops answering, as one stopped in a debugger does.
            (LocalBackend host, RecordingListener hostEvents, _, RecordingListener clientEvents) = ConnectPair(network => network.ConnectionTimeoutSeconds = 0.5f);

            // A quiet connection between two players that keep updating outlives the timeout.
            PumpFor(1.5);
            Assert.That(clientEvents.Disconnected, Is.Empty);
            Assert.That(hostEvents.Disconnected, Is.Empty);

            _paused.Add(host);
            PumpUntil(() => clientEvents.Disconnected.Count == 1, "the timeout", 3.0);

            Assert.That(clientEvents.Disconnected[0].Kind, Is.EqualTo(BackendDisconnectKind.ConnectionLost));
        }

        [Test]
        public void Connect_ToAPlayerNoSharedLobbyLists_Fails()
        {
            RecordingListener events = new RecordingListener();
            LocalBackend backend = CreateRawBackend(CreateNetwork(), "Lonely", events);

            ConnectionId connection = backend.Connect(new PlayerId(12345));
            Assert.That(connection.IsValid, Is.True);
            PumpUntil(() => events.Disconnected.Count == 1, "the failure");

            Assert.That(events.Disconnected[0], Is.EqualTo((connection, BackendDisconnectKind.ConnectFailed, 0)));
        }
        #endregion

        #region Lobby
        [Test]
        public void Join_IsRefused_WhenFullNotJoinableOrMissing()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 1);

            Assert.That(Join(CreateRawBackend(CreateNetwork(), "Late"), lobby), Is.EqualTo(MultiplayerReason.SessionFull));

            Assert.That(owner.SetLobbyCapacity(lobby, 4) && owner.SetLobbyJoinable(lobby, false), Is.True);
            Assert.That(Join(CreateRawBackend(CreateNetwork(), "Later"), lobby), Is.EqualTo(MultiplayerReason.GameInProgress));

            Assert.That(Join(CreateRawBackend(CreateNetwork(), "Lost"), new LobbyId(987654321)), Is.EqualTo(MultiplayerReason.LobbyNotFound));
        }

        [Test]
        public void LobbyChanges_ReachTheMembersAndTheSearch()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            LocalBackend member = CreateRawBackend(CreateNetwork(), "Member");
            Assert.That(Join(member, lobby), Is.EqualTo(MultiplayerReason.None));

            Assert.That(owner.SetLobbyData(lobby, "map", "forest"), Is.True);
            Assert.That(member.SetLobbyData(lobby, "map", "spoofed"), Is.False);
            PumpUntil(() => member.GetLobbyData(lobby, "map") == "forest", "the data");

            Assert.That(member.GetLobbyOwner(lobby), Is.EqualTo(owner.LocalPlayerId));
            Assert.That(owner.IsLobbyMember(lobby, member.LocalPlayerId) && member.IsLobbyMember(lobby, owner.LocalPlayerId), Is.True);
            Assert.That(member.GetPlayerName(owner.LocalPlayerId), Is.EqualTo("Owner"));

            LocalBackend seeker = CreateRawBackend(CreateNetwork(), "Seeker");
            PumpUntil(() => Search(seeker).Any(found => found.Id == lobby && found.MemberCount == 2 && found.Data["map"] == "forest"), "the search to show the change");

            member.LeaveLobby(lobby);
            PumpUntil(() => !owner.IsLobbyMember(lobby, member.LocalPlayerId), "the owner to drop the member");
        }

        [Test]
        public void Owner_Leaving_HandsTheLobbyToTheFirstMember()
        {
            (LobbyId lobby, LocalBackend owner, Peer first, Peer second) = LobbyOfThree();

            owner.LeaveLobby(lobby);

            AssertPassedTo(lobby, first, second);
        }

        [Test]
        public void Owner_Crashing_PassesTheLobbyToTheFirstMember_AndJoinsReachTheNewOwner()
        {
            (LobbyId lobby, LocalBackend owner, Peer first, Peer second) = LobbyOfThree();

            // The crashed owner's file still lists the lobby until it goes stale.
            owner.SimulateCrash();

            AssertPassedTo(lobby, first, second);

            LocalBackend late = CreateRawBackend(CreateNetwork(), "Late");
            PumpUntil(() => Join(late, lobby) == MultiplayerReason.None, "a join through the new owner's file");
            PumpUntil(() => first.Backend.IsLobbyMember(lobby, late.LocalPlayerId), "the new owner to list the newcomer");
            Assert.That(late.GetLobbyOwner(lobby), Is.EqualTo(first.Id));
        }

        [Test]
        public void Member_ThatReachesTheNextOwnerFirst_AsksAgainUntilItTookOver()
        {
            // The owner stalls; the second member gives up on it sooner than the first, which
            // answers NotYet until it notices too. The owner pings often enough for the shortest
            // timeout until then.
            LocalBackend owner = CreateRawBackend(CreateNetwork(network => network.ConnectionTimeoutSeconds = 0.5f), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            Peer first = CreatePeer("First", network => network.ConnectionTimeoutSeconds = 1.5f);
            Peer second = CreatePeer("Second", network => network.ConnectionTimeoutSeconds = 0.5f);
            Assert.That(Join(first.Backend, lobby), Is.EqualTo(MultiplayerReason.None));
            Assert.That(Join(second.Backend, lobby), Is.EqualTo(MultiplayerReason.None));
            PumpUntil(() => first.Backend.IsLobbyMember(lobby, second.Id), "the first member to see the second");

            _paused.Add(owner);
            PumpUntil(() => second.Events.OwnerChanges.Count == 1, "the second member to give up on the owner", 3.0);
            Assert.That(first.Events.OwnerChanges, Is.Empty, "The first member noticed first; the race did not happen.");

            AssertPassedTo(lobby, first, second);
        }

        [Test]
        public void OwnerCrash_TakesTheLobbyOutOfSearch_OnceAMemberNotices()
        {
            // FR-27: the crashed owner's file would list the lobby for StaleSeconds more.
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            Peer member = CreatePeer("Member");
            Assert.That(Join(member.Backend, lobby), Is.EqualTo(MultiplayerReason.None));

            owner.SimulateCrash();
            PumpUntil(() => member.Events.OwnerChanges.Count == 1, "the member to notice");

            // A session ends with its host, so the member leaves as the manager would.
            member.Backend.LeaveLobby(lobby);
            LocalBackend seeker = CreateRawBackend(CreateNetwork(), "Seeker");
            PumpUntil(() => Search(seeker).Count == 0, "the lobby to leave search", 3.0);
        }

        [Test]
        public void OwnerCrash_WithoutMembers_LeavesSearch_AfterAJoinFindsNobody()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            owner.SimulateCrash();

            LocalBackend seeker = CreateRawBackend(CreateNetwork(), "Seeker");
            Assert.That(Search(seeker).Select(found => found.Id), Is.EqualTo(new[] { lobby }), "Nobody noticed the crash yet.");
            Assert.That(Join(seeker, lobby), Is.EqualTo(MultiplayerReason.LobbyNotFound));
            Assert.That(Search(seeker), Is.Empty);
        }

        [Test]
        public void StalledMember_IsDropped_WithoutTakingTheOwnerForGone()
        {
            Action<LocalNetwork> shortTimeout = network => network.ConnectionTimeoutSeconds = 0.5f;
            LocalBackend owner = CreateRawBackend(CreateNetwork(shortTimeout), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            RecordingListener events = new RecordingListener();
            LocalBackend member = CreateRawBackend(CreateNetwork(shortTimeout), "Member", events);
            Assert.That(Join(member, lobby), Is.EqualTo(MultiplayerReason.None));

            _paused.Add(member);
            PumpUntil(() => !owner.IsLobbyMember(lobby, member.LocalPlayerId), "the owner to drop the stalled member", 3.0);
            _paused.Remove(member);
            PumpUntil(() => !member.GetLobbyOwner(lobby).IsValid, "the member to learn it was dropped", 3.0);

            Assert.That(events.OwnerChanges, Is.Empty);
        }

        [Test]
        public void Directory_IgnoresAndDeletesAFileThatStoppedBeingRewritten()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            Assert.That(MachineDirectory.Read(_folder).Select(entry => entry.Lobby), Is.EqualTo(new[] { lobby }));

            // The write time follows the magic number and the format version.
            string file = Directory.GetFiles(_folder, "*.lobbies").Single();
            byte[] bytes = File.ReadAllBytes(file);
            long old = DateTime.UtcNow.AddSeconds(-MachineDirectory.StaleSeconds - 1.0).Ticks;
            BitConverter.GetBytes(old).CopyTo(bytes, 8);
            File.WriteAllBytes(file, bytes);

            Assert.That(MachineDirectory.Read(_folder), Is.Empty);
            Assert.That(File.Exists(file), Is.False);
        }

        [Test]
        public void Dispose_DeletesTheLobbyFile()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            CreateLobby(owner, LobbyVisibility.InviteOnly, 2);
            Assert.That(Directory.GetFiles(_folder, "*.lobbies"), Has.Length.EqualTo(1));

            owner.Dispose();

            Assert.That(Directory.GetFiles(_folder, "*.lobbies"), Is.Empty);
        }
        #endregion

        #region Simulation
        [Test]
        public void SimulatedLatency_OnBothProcesses_ReportsTheRoundTripAsPing()
        {
            // FR-36, FR-40: each process delays what it sends, so 100 ms one way on each is a 200 ms round trip.
            Node host = CreateNode("Host");
            Node client = CreateNode("Client");
            StartSession(host, client);
            host.Network.SimulatedLatencyMilliseconds = 100f;
            client.Network.SimulatedLatencyMilliseconds = 100f;

            // Probes measured before the latency count until a window without them goes by.
            PumpFor(1.5);
            ConnectionQuality fromHost = default;
            ConnectionQuality fromClient = default;
            PumpUntil(() => host.Manager.TryGetConnectionQuality(client.Id, out fromHost) && fromHost.PingMilliseconds > 0
                && client.Manager.TryGetConnectionQuality(host.Id, out fromClient) && fromClient.PingMilliseconds > 0, "both pings");

            TestContext.WriteLine($"Ping at 100 ms one way between processes: host {fromHost.PingMilliseconds} ms, client {fromClient.PingMilliseconds} ms.");
            Assert.That(fromHost.PingMilliseconds, Is.InRange(180, 220));
            Assert.That(fromClient.PingMilliseconds, Is.InRange(180, 220));

            // Messages wait for the latency too.
            Stopwatch sent = Stopwatch.StartNew();
            Assert.That(client.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host).IsSuccess, Is.True);
            PumpUntil(() => host.Messages.Count == 1, "the delayed message");
            Assert.That(sent.Elapsed.TotalMilliseconds, Is.GreaterThanOrEqualTo(100.0));
        }

        [Test]
        public void SimulatedLoss_DropsUnreliableMessages_ButReliableOnesArriveInOrder()
        {
            // FR-40, FR-32 between processes; the seed makes the client lose the same messages on every run.
            Node host = CreateNode("Host");
            Node client = CreateNode("Client");
            StartSession(host, client);
            client.Network.SeedSimulation(40);
            client.Network.SimulatedLossPercent = 10f;

            for (int i = 0; i < 1000; i++)
            {
                Assert.That(client.Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.Host, DeliveryMode.Unreliable).IsSuccess, Is.True);
            }

            // The connection's own probes keep arriving, so the host can tell when the burst is in.
            ConnectionQuality quality = default;
            PumpUntil(() => host.Manager.TryGetConnectionQuality(client.Id, out quality) && quality.PacketLoss > 0f, "the host to measure the loss");
            int unreliable = host.Messages.Count;
            TestContext.WriteLine($"{unreliable} of 1000 unreliable messages arrived between processes at 10% loss; the host measured {quality}.");
            Assert.That(unreliable, Is.InRange(850, 950));
            Assert.That(quality.PacketLoss, Is.InRange(0.05f, 0.15f));

            host.Messages.Clear();
            client.Network.SimulatedLatencyMilliseconds = 10f;
            client.Network.SimulatedLossPercent = 20f;
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(client.Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.Host).IsSuccess, Is.True);
            }

            PumpUntil(() => host.Messages.Count >= 1000, "every reliable message");
            PumpFor(0.2);
            Assert.That(host.Messages.Select(message => BitConverter.ToInt32(message.Payload, 0)), Is.EqualTo(Enumerable.Range(0, 1000)));
        }

        [Test]
        public void SimulatedJitter_KeepsReliableMessagesInOrder_ThroughTheHost()
        {
            // FR-32, FR-40: both hops between processes stray in their delay.
            Node host = CreateNode("Host");
            Node a = CreateNode("A");
            Node b = CreateNode("B");
            StartSession(host, a, b);
            foreach (Node node in new[] { host, a, b })
            {
                node.Network.SeedSimulation(4);
                node.Network.SimulatedLatencyMilliseconds = 20f;
                node.Network.SimulatedJitterMilliseconds = 15f;
            }

            for (int i = 0; i < 300; i++)
            {
                Assert.That(a.Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.To(b.Id)).IsSuccess, Is.True);
                Assert.That(a.Manager.Send(Chat, BitConverter.GetBytes(1000 + i), MessageTarget.To(b.Id), DeliveryMode.Unreliable).IsSuccess, Is.True);
            }

            PumpUntil(() => b.Messages.Count >= 600, "every message");
            List<int> received = b.Messages.Select(message => BitConverter.ToInt32(message.Payload, 0)).ToList();
            Assert.That(received.Where(value => value < 1000), Is.EqualTo(Enumerable.Range(0, 300)));
            Assert.That(received.Where(value => value >= 1000), Is.EquivalentTo(Enumerable.Range(1000, 300)));
        }

        [Test]
        public void SimulatedLatency_AHostThatLeaves_DeliversItsLastReliableMessagesFirst()
        {
            // FR-32: the host's hand-over on the lobby connection must not end a client's session
            // before the host's last messages and its close on the session connection arrive.
            Node host = CreateNode("Host");
            Node a = CreateNode("A");
            Node b = CreateNode("B");
            StartSession(host, a, b);
            host.Network.SimulatedLatencyMilliseconds = 100f;
            host.Network.SimulatedJitterMilliseconds = 30f;

            for (int i = 0; i < 20; i++)
            {
                Assert.That(host.Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.Others).IsSuccess, Is.True);
            }

            host.Manager.Leave();
            PumpUntil(() => a.Manager.State == SessionState.Offline && b.Manager.State == SessionState.Offline, "both clients to end their sessions");
            foreach (Node client in new[] { a, b })
            {
                Assert.That(client.Messages.Select(message => BitConverter.ToInt32(message.Payload, 0)), Is.EqualTo(Enumerable.Range(0, 20)), client.Backend.PlayerName);
                Assert.That(client.States.Last().Reason, Is.EqualTo(MultiplayerReason.HostLeft));
            }
        }

        [Test]
        public void SimulatedLatency_ABackendDisposedWithoutClosing_StillSendsWhatItHeldBack()
        {
            // As the in-process backend: what a backend already sent arrives even when it goes away.
            Node host = CreateNode("Host");
            Node client = CreateNode("Client");
            StartSession(host, client);
            client.Network.SimulatedLatencyMilliseconds = 500f;

            for (int i = 0; i < 10; i++)
            {
                Assert.That(client.Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.Host).IsSuccess, Is.True);
            }

            client.Backend.Dispose();
            PumpUntil(() => host.Manager.Participants.Count == 1, "the host to drop the client");
            Assert.That(host.Messages.Select(message => BitConverter.ToInt32(message.Payload, 0)), Is.EqualTo(Enumerable.Range(0, 10)));
        }
        #endregion

        #region Helpers
        private LocalNetwork CreateNetwork(Action<LocalNetwork> configure = null)
        {
            LocalNetwork network = LocalNetwork.CreateMachineWide("tests");
            network.MachineFolder = _folder;
            configure?.Invoke(network);
            return network;
        }

        private LocalBackend CreateRawBackend(LocalNetwork network, string name, IMultiplayerBackendListener listener = null)
        {
            LocalBackend backend = new LocalBackend(network, name);
            backend.SetListener(listener ?? new RecordingListener());
            _rawBackends.Add(backend);
            return backend;
        }

        private Node CreateNode(string name)
        {
            LocalNetwork network = CreateNetwork();
            LocalBackend backend = new LocalBackend(network, name);
            MultiplayerManager manager = new MultiplayerManager();
            Assert.That(manager.Initialize(backend, new MultiplayerConfig(GameId, 1) { Clock = () => _clock.Elapsed.TotalSeconds }).IsSuccess, Is.True);

            Node node = new Node { Network = network, Backend = backend, Manager = manager };
            manager.OnStateChanged += change => node.States.Add(change);
            manager.OnGameStarted += data => node.GameStarts.Add(data.ToArray());
            manager.OnChatReceived += (sender, text) => node.Chats.Add((sender.Id, text));
            Assert.That(manager.RegisterMessage(Chat, (context, payload) => node.Messages.Add((context.Sender, payload.ToArray()))).IsSuccess, Is.True);

            _nodes.Add(node);
            return node;
        }

        private LobbyId CreateLobby(LocalBackend owner, LobbyVisibility visibility, int capacity)
        {
            MultiplayerReason? reason = null;
            LobbyId lobby = LobbyId.None;
            owner.CreateLobby(visibility, capacity, null, (r, id) =>
            {
                reason = r;
                lobby = id;
            });

            // The update that answers also writes the lobby file.
            PumpUntil(() => reason.HasValue, "the lobby");
            Assert.That(reason, Is.EqualTo(MultiplayerReason.None));
            return lobby;
        }

        private MultiplayerReason Join(LocalBackend backend, LobbyId lobby)
        {
            MultiplayerReason? reason = null;
            backend.JoinLobby(lobby, r => reason = r);
            PumpUntil(() => reason.HasValue, "the join");
            return reason.Value;
        }

        private List<BackendLobby> Search(LocalBackend backend)
        {
            IReadOnlyList<BackendLobby> lobbies = null;
            backend.SearchLobbies(new LobbySearchQuery(), (r, found) => lobbies = found);
            PumpUntil(() => lobbies != null, "the search");
            return lobbies.ToList();
        }

        /// <summary>The host creates a public lobby, and every client joins it.</summary>
        private void StartSession(Node host, params Node[] clients)
        {
            MultiplayerResult? created = null;
            host.Manager.CreateLobby(new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = new SessionRules(4, requireReady: false) }, r => created = r);
            PumpUntil(() => created.HasValue, "the lobby");
            Assert.That(created.Value.IsSuccess, Is.True);

            foreach (Node client in clients)
            {
                MultiplayerResult? joined = null;
                client.Manager.JoinLobby(host.Manager.CurrentLobbyId, r => joined = r);
                PumpUntil(() => joined.HasValue, $"{client.Backend.PlayerName} to join");
                Assert.That(joined.Value.IsSuccess, Is.True, joined.Value.Reason.ToString());
            }

            PumpUntil(() => clients.All(client => client.Manager.Participants.Count == clients.Length + 1), "every roster to list every player");
        }

        private static string CharacterOf(Node node, PlayerId player)
        {
            return node.Manager.TryGetParticipant(player, out Participant participant) && participant.Data.TryGetValue("character", out string character)
                ? character
                : null;
        }

        /// <summary>A host listening in a lobby and a member connected to it, each in a "process" of its own.</summary>
        private (LocalBackend Host, RecordingListener HostEvents, LocalBackend Client, RecordingListener ClientEvents) ConnectPair(Action<LocalNetwork> configure = null)
        {
            RecordingListener hostEvents = new RecordingListener();
            RecordingListener clientEvents = new RecordingListener();
            LocalBackend host = CreateRawBackend(CreateNetwork(configure), "Host", hostEvents);
            LocalBackend client = CreateRawBackend(CreateNetwork(configure), "Client", clientEvents);

            LobbyId lobby = CreateLobby(host, LobbyVisibility.Public, 2);
            Assert.That(host.StartListening(), Is.True);
            Assert.That(Join(client, lobby), Is.EqualTo(MultiplayerReason.None));

            ConnectionId connection = client.Connect(host.LocalPlayerId);
            PumpUntil(() => clientEvents.Connected.Count == 1 && hostEvents.Connected.Count == 1, "the connection");

            Assert.That(clientEvents.Connected[0], Is.EqualTo((connection, host.LocalPlayerId, false)));
            Assert.That(hostEvents.Connected[0].Remote, Is.EqualTo(client.LocalPlayerId));
            Assert.That(hostEvents.Connected[0].IsIncoming, Is.True);
            return (host, hostEvents, client, clientEvents);
        }

        /// <summary>An owner and two members, who joined in the order of their names.</summary>
        private (LobbyId Lobby, LocalBackend Owner, Peer First, Peer Second) LobbyOfThree()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);

            Peer first = CreatePeer("First");
            Peer second = CreatePeer("Second");
            Assert.That(Join(first.Backend, lobby), Is.EqualTo(MultiplayerReason.None));
            Assert.That(Join(second.Backend, lobby), Is.EqualTo(MultiplayerReason.None));
            PumpUntil(() => first.Backend.IsLobbyMember(lobby, second.Id), "the first member to see the second");

            return (lobby, owner, first, second);
        }

        private Peer CreatePeer(string name, Action<LocalNetwork> configure = null)
        {
            RecordingListener events = new RecordingListener();
            return new Peer { Backend = CreateRawBackend(CreateNetwork(configure), name, events), Events = events };
        }

        /// <summary>
        /// Both members hear that <paramref name="next"/> owns the lobby now, the other member
        /// reconnects to it, and the new owner's changes reach that member.
        /// </summary>
        private void AssertPassedTo(LobbyId lobby, Peer next, Peer other)
        {
            PumpUntil(() => next.Events.OwnerChanges.Count == 1 && other.Events.OwnerChanges.Count == 1, "both members to hear the owner change");
            PumpUntil(() => next.Backend.IsLobbyMember(lobby, other.Id) && other.Backend.IsLobbyMember(lobby, next.Id), "the other member to reach the new owner");

            Assert.That(next.Events.OwnerChanges.Single(), Is.EqualTo((lobby, next.Id)));
            Assert.That(other.Events.OwnerChanges.Single(), Is.EqualTo((lobby, next.Id)));
            Assert.That(next.Backend.GetLobbyOwner(lobby), Is.EqualTo(next.Id));
            Assert.That(other.Backend.GetLobbyOwner(lobby), Is.EqualTo(next.Id));
            Assert.That(next.Backend.SetLobbyData(lobby, "owner", "next"), Is.True);
            PumpUntil(() => other.Backend.GetLobbyData(lobby, "owner") == "next", "the new owner's change to reach the other member");
        }

        private void UpdateAll()
        {
            foreach (Node node in _nodes)
            {
                if (!_paused.Contains(node.Backend))
                {
                    node.Manager.Update();
                }
            }

            foreach (LocalBackend backend in _rawBackends)
            {
                if (!_paused.Contains(backend))
                {
                    backend.Update();
                }
            }
        }

        private void PumpFor(double seconds)
        {
            Stopwatch waited = Stopwatch.StartNew();
            while (waited.Elapsed.TotalSeconds < seconds)
            {
                UpdateAll();
                Thread.Sleep(2);
            }
        }

        /// <summary>Updates every player that is not paused until the condition holds, in real time.</summary>
        private void PumpUntil(Func<bool> condition, string what, double seconds = WaitSeconds)
        {
            Stopwatch waited = Stopwatch.StartNew();
            UpdateAll();
            while (!condition())
            {
                if (waited.Elapsed.TotalSeconds > seconds)
                {
                    Assert.Fail($"Timed out after {seconds} s waiting for {what}.");
                }

                Thread.Sleep(2);
                UpdateAll();
            }
        }

        private static IPAddress FindNonLoopbackAddress()
        {
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                foreach (UnicastIPAddressInformation address in adapter.GetIPProperties().UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
                    {
                        return address.Address;
                    }
                }
            }

            return null;
        }

        private static bool CanConnect(IPAddress address, int port)
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                try
                {
                    IAsyncResult attempt = socket.BeginConnect(new IPEndPoint(address, port), null, null);
                    if (!attempt.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3)))
                    {
                        return false;
                    }

                    socket.EndConnect(attempt);
                    return true;
                }
                catch (SocketException)
                {
                    return false;
                }
            }
        }
        #endregion
    }
}
