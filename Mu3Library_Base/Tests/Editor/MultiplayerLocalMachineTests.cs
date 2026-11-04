using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using Mu3Library.Multiplayer.Protocol;
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

        // How long a pump yields between updates before it sleeps (PauseBetweenUpdates).
        private const double YieldMilliseconds = 20.0;

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
            public void OnAvatarChanged(PlayerId player) { }
        }

        /// <summary>Records connections and hands each packet it receives, inside the update, to <see cref="Received"/>.</summary>
        private sealed class CallbackListener : IMultiplayerBackendListener
        {
            public readonly List<ConnectionId> Connected = new List<ConnectionId>();
            public Action<ConnectionId, byte[]> Received;

            public void OnConnected(ConnectionId connection, PlayerId remote, bool isIncoming) => Connected.Add(connection);
            public void OnDisconnected(ConnectionId connection, PlayerId remote, BackendDisconnectKind kind, int reasonCode) { }
            public void OnReceived(ConnectionId connection, PlayerId remote, ReadOnlySpan<byte> data) => Received?.Invoke(connection, data.ToArray());
            public void OnLobbyOwnerChanged(LobbyId lobby, PlayerId newOwner) { }
            public void OnLobbyJoinRequested(LobbyId lobby) { }
            public void OnAvatarChanged(PlayerId player) { }
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
        public void DataDeletions_ReachEveryProcess_AndLeaveLobbySearch()
        {
            // FR-11 between "processes": a deletion travels as a change does, and another
            // process's search reads the lobby file without the key once the file caught up.
            Node host = CreateNode("Host");
            Node a = CreateNode("A");
            StartSession(host, a);
            Assert.That(host.Manager.SetLobbyData("map", "desert").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("mode", "coop").IsSuccess, Is.True);
            Assert.That(a.Manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
            Node[] everyone = { host, a };
            PumpUntil(() => everyone.All(node => node.Manager.LobbyData.ContainsKey("map") && CharacterOf(node, a.Id) == "knight"), "every player to see the data");

            LocalBackend seeker = CreateRawBackend(CreateNetwork(), "Seeker");
            LobbyId lobby = host.Manager.CurrentLobbyId;
            Assert.That(SearchedData(seeker, lobby, data => data.ContainsKey("map")), Is.Not.Null, "the lobby file never listed the key");

            Assert.That(host.Manager.DeleteLobbyData("map").IsSuccess, Is.True);
            Assert.That(a.Manager.DeleteParticipantData("character").IsSuccess, Is.True);
            PumpUntil(() => everyone.All(node => !node.Manager.LobbyData.ContainsKey("map") && CharacterOf(node, a.Id) == null), "every player to see the deletions");

            IReadOnlyDictionary<string, string> seen = SearchedData(seeker, lobby, data => !data.ContainsKey("map"));
            Assert.That(seen, Is.Not.Null, "the lobby file never dropped the key");
            Assert.That(seen["mode"], Is.EqualTo("coop"));
        }

        [Test]
        public void LobbyData_WrittenEmpty_IsDeleted_FromEveryProcess_TheLobbyFile_AndSearch()
        {
            // FR-11, FR-38, #313 between "processes": an empty value deletes the key, so neither
            // the owner's lobby, a member's copy, nor the lobby file holds it, and a lobby created
            // with one starts without it.
            Node host = CreateNode("Host");
            Node a = CreateNode("A");
            LobbyCreateOptions options = new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = new SessionRules(4, requireReady: false) };
            options.Data["map"] = "desert";
            options.Data["e1"] = string.Empty;
            MultiplayerResult? created = null;
            host.Manager.CreateLobby(options, r => created = r);
            PumpUntil(() => created.HasValue, "the lobby");
            Assert.That(created.Value.IsSuccess, Is.True);
            LobbyId lobby = host.Manager.CurrentLobbyId;

            MultiplayerResult? joined = null;
            a.Manager.JoinLobby(lobby, r => joined = r);
            PumpUntil(() => joined.HasValue, "A to join");
            Assert.That(joined.Value.IsSuccess, Is.True, joined.Value.Reason.ToString());

            Node[] everyone = { host, a };
            LocalBackend seeker = CreateRawBackend(CreateNetwork(), "Seeker");
            Assert.That(host.Manager.SetLobbyData("mode", "coop").IsSuccess, Is.True);
            PumpUntil(() => a.Manager.LobbyData.ContainsKey("mode") && a.Backend.GetLobbyData(lobby, "mode") == "coop", "A to see the value");
            Assert.That(SearchedData(seeker, lobby, data => data.ContainsKey("mode")), Is.Not.Null, "the lobby file never listed the key");

            Assert.That(host.Manager.SetLobbyData("mode", string.Empty).IsSuccess, Is.True);
            PumpUntil(() => everyone.All(node => !node.Manager.LobbyData.ContainsKey("mode") && node.Backend.GetLobbyData(lobby, "mode") == null),
                "every player and lobby copy to lose the key");
            IReadOnlyDictionary<string, string> seen = SearchedData(seeker, lobby, data => !data.ContainsKey("mode"));
            Assert.That(seen, Is.Not.Null, "the lobby file kept the key written empty");
            Assert.That(seen.Keys.Where(key => !LobbyDataKeys.IsReserved(key)), Is.EquivalentTo(new[] { "map" }), "another process's search");
            Assert.That(ListedData(lobby, "e1"), Is.Null, "the lobby file listed the key the lobby was created with empty");

            foreach (Node node in everyone)
            {
                Assert.That(node.Manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["map"] = "desert" }), node.Backend.PlayerName);
                Assert.That(node.Backend.GetLobbyData(lobby, "e1"), Is.Null, node.Backend.PlayerName);
            }
        }

        [Test]
        public void LobbySearch_ComparesStringValues_AsSteamDoes()
        {
            // FR-6, FR-38, #239, #286: every comparison of a string filter, from another process,
            // on the values the lobby file lists and on a key it lacks or holds empty.
            Node host = CreateNode("Host");
            LobbyCreateOptions options = new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = new SessionRules(4) };
            foreach (KeyValuePair<string, string> pair in MultiplayerSessionTests.StringComparisonData())
            {
                options.Data[pair.Key] = pair.Value;
            }

            MultiplayerResult? created = null;
            host.Manager.CreateLobby(options, r => created = r);
            PumpUntil(() => created.HasValue, "the lobby");
            Assert.That(created.Value.IsSuccess, Is.True);
            Node seeker = CreateNode("Seeker");

            List<string> mismatches = new List<string>();
            foreach ((string key, string filter, LobbyComparison comparison, bool isKept) in MultiplayerSessionTests.StringComparisonChecks())
            {
                LobbySearchResult? found = null;
                seeker.Manager.SearchLobbies(new LobbySearchQuery().AddStringFilter(key, filter, comparison), r => found = r);
                PumpUntil(() => found.HasValue, "the search");
                if ((found.Value.Lobbies.Count == 1) != isKept)
                {
                    mismatches.Add(MultiplayerSessionTests.DescribeStringComparison(key, filter, comparison, isKept));
                }
            }

            Assert.That(mismatches, Is.Empty);
        }

        [Test]
        public void LobbySearch_SkipsAFilterWhoseKeyIsNullOrEmpty_AsTheSteamBackendDoes()
        {
            // FR-6, FR-38, #314: a string or numeric filter with a null or empty key leaves out no
            // lobby, from another process, which reads the lobby file, and from the owner's own
            // process, which reads its lobby as it is.
            Node host = CreateNode("Host");
            LobbyCreateOptions options = new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = new SessionRules(4) };
            options.Data[MultiplayerSessionTests.KeylessFilterLobbyKey] = MultiplayerSessionTests.KeylessFilterLobbyValue;

            MultiplayerResult? created = null;
            host.Manager.CreateLobby(options, r => created = r);
            PumpUntil(() => created.HasValue, "the lobby");
            Assert.That(created.Value.IsSuccess, Is.True);

            List<string> mismatches = new List<string>();
            foreach (Node seeker in new[] { CreateNode("Seeker"), host })
            {
                foreach ((string description, LobbySearchQuery query, bool isKept) in MultiplayerSessionTests.KeylessFilterChecks())
                {
                    LobbySearchResult? found = null;
                    seeker.Manager.SearchLobbies(query, r => found = r);
                    PumpUntil(() => found.HasValue, "the search");
                    if ((found.Value.Lobbies.Count == 1) != isKept)
                    {
                        mismatches.Add($"{seeker.Backend.PlayerName}: {description} should {(isKept ? "keep" : "drop")} the lobby");
                    }
                }
            }

            Assert.That(mismatches, Is.Empty);
        }

        [Test]
        public void Restart_OfAnAccountWhoseSlotTheHostKeeps_ComesBackPastTheClosedLobby_WhichSearchLeavesOut()
        {
            // FR-21, FR-20, FR-38, #252, #253: a crashed "process" of the client starts again
            // within the reservation time, while the game refuses late joiners.
            Node host = CreateNode("Host");
            Node client = CreateNode("Client");
            StartSession(host, client);
            Assert.That(host.Manager.StartGame(new byte[] { 4 }).IsSuccess, Is.True);
            PumpUntil(() => client.Manager.State == SessionState.InGame, "the game to start");
            LobbyId lobby = host.Manager.CurrentLobbyId;
            PumpUntil(() => ListedData(lobby, LobbyDataKeys.Closed) == LobbyDataKeys.ClosedForTheGame, "the folder to list the game as closed");

            client.Backend.SimulateCrash();
            PumpUntil(() => host.Manager.TryGetParticipant(client.Id, out Participant away) && !away.IsConnected, "the host to keep the slot");
            _paused.Add(client.Backend);

            Node again = CreateNode("Client", id: client.Id);
            MultiplayerResult? joined = null;
            again.Manager.JoinLobby(lobby, r => joined = r);
            PumpUntil(() => joined.HasValue, "the new start to join");
            Assert.That(joined.Value.IsSuccess, Is.True, joined.Value.Reason.ToString());
            Assert.That(again.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(again.GameStarts.Single(), Is.EqualTo(new byte[] { 4 }));

            PumpUntil(() => again.Backend.IsLobbyMember(lobby, again.Id), "the new start to enter the lobby");
            Assert.That(host.Manager.TryGetParticipant(client.Id, out Participant back) && back.IsConnected, Is.True);

            Node stranger = CreateNode("Stranger");
            LobbySearchResult? found = null;
            stranger.Manager.SearchLobbies(null, r => found = r);
            PumpUntil(() => found.HasValue, "the search");
            Assert.That(found.Value.Lobbies, Is.Empty, "search leaves the game out");

            MultiplayerResult? refused = null;
            stranger.Manager.JoinLobby(lobby, r => refused = r);
            PumpUntil(() => refused.HasValue, "the stranger's join");
            Assert.That(refused.Value.Reason, Is.EqualTo(MultiplayerReason.GameInProgress));
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
        public void RelayedBurst_LeavesInOneSocketCallPerConnection_AndArrivesWithoutAnotherHostUpdate()
        {
            // NFR-1, NFR-2: a host relaying a burst it read in one update hands each connection to
            // its sending thread once instead of once for each message, before the update ends,
            // and that thread makes one socket call for each.
            Node host = CreateNode("Host");
            Node a = CreateNode("A");
            Node b = CreateNode("B");
            StartSession(host, a, b);

            // B joined last, so the host reads B's connection after A's: what it relays to A from
            // there leaves only at the end of the update.
            const int burst = 50;
            for (int i = 0; i < burst; i++)
            {
                Assert.That(b.Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.Others, DeliveryMode.Unreliable).IsSuccess, Is.True);
            }

            // B sends between updates, so the burst is on the loopback already; let it cross.
            Thread.Sleep(100);
            WaitForSends(host.Backend);
            long before = host.Backend.SocketSends;
            host.Manager.Update();
            WaitForSends(host.Backend);
            long sends = host.Backend.SocketSends - before;

            Assert.That(host.Messages.Count, Is.EqualTo(burst), "The host read the whole burst in one update.");
            // The host has four connections, a session and a lobby one to each client: at most one
            // call each, pings and probes included, at the end of the update.
            Assert.That(sends, Is.LessThanOrEqualTo(4));

            _paused.Add(host.Backend);
            _paused.Add(b.Backend);
            PumpUntil(() => a.Messages.Count == burst, "the relayed burst to reach A while the host waits");
            Assert.That(a.Messages.Select(message => BitConverter.ToInt32(message.Payload, 0)), Is.EqualTo(Enumerable.Range(0, burst)));
            Assert.That(a.Messages.All(message => message.Sender == b.Id), Is.True);
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
        public void LoadAvatar_AnswersNotSupported_OnTheNextUpdate()
        {
            // FR-4: the machine-wide network has no avatars either.
            LocalBackend backend = CreateRawBackend(CreateNetwork(), "A");

            MultiplayerReason? reason = null;
            MultiplayerReason? invalid = null;
            backend.LoadAvatar(backend.LocalPlayerId, AvatarSize.Large, (answer, avatar) => reason = answer);
            backend.LoadAvatar(PlayerId.None, AvatarSize.Large, (answer, avatar) => invalid = answer);
            Assert.That(reason.HasValue || invalid.HasValue, Is.False);

            backend.Update();
            Assert.That(reason, Is.EqualTo(MultiplayerReason.NotSupported));
            Assert.That(invalid, Is.EqualTo(MultiplayerReason.InvalidArgument));
        }

        [Test]
        public void SetPresence_ChangesNothingAndSucceeds()
        {
            // FR-13: the machine-wide network has no friends list either.
            LocalBackend backend = CreateRawBackend(CreateNetwork(), "A");

            Assert.That(backend.SetPresence("Status", new LobbyId(1), 2), Is.True);
            Assert.That(backend.SetPresence(null, LobbyId.None, 0), Is.True);
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
            Assert.That(owner.GetLobbyData(lobby, "MAP"), Is.EqualTo("forest"), "a key reads in any case, as on Steam");
            Assert.That(member.GetLobbyData(lobby, "Map"), Is.EqualTo("forest"));

            Assert.That(member.GetLobbyOwner(lobby), Is.EqualTo(owner.LocalPlayerId));
            Assert.That(owner.IsLobbyMember(lobby, member.LocalPlayerId) && member.IsLobbyMember(lobby, owner.LocalPlayerId), Is.True);
            Assert.That(member.GetPlayerName(owner.LocalPlayerId), Is.EqualTo("Owner"));

            LocalBackend seeker = CreateRawBackend(CreateNetwork(), "Seeker");
            PumpUntil(() => Search(seeker).Any(found => found.Id == lobby && found.MemberCount == 2 && found.Data["MAP"] == "forest"), "the search to show the change, a key in any case");

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

        [Test]
        public void Dispose_WhileTheLobbyFileIsBeingRewritten_LeavesNoFile()
        {
            // A rewrite runs on a thread of its own and takes milliseconds; the dispose waits for
            // it, so the write cannot bring the file back after the dispose deleted it.
            for (int round = 0; round < 5; round++)
            {
                LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
                LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);

                // A change is listed a quarter second after the last listing at the soonest.
                Thread.Sleep(300);
                Assert.That(owner.SetLobbyData(lobby, "round", round.ToString(CultureInfo.InvariantCulture)), Is.True);
                owner.Update();
                owner.Dispose();

                Assert.That(Directory.GetFiles(_folder), Is.Empty, $"Round {round} left a file behind.");
            }
        }

        [Test]
        public void Crash_WhileTheLobbyFileIsBeingRewritten_LeavesTheFileAsItWas()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);

            // The change goes to the writer thread; the crash waits for that write if it began
            // and drops it if not, so nothing writes the file afterwards.
            Thread.Sleep(300);
            Assert.That(owner.SetLobbyData(lobby, "map", "forest"), Is.True);
            owner.Update();
            owner.SimulateCrash();

            string file = Directory.GetFiles(_folder, "*.lobbies").Single();
            byte[] contents = File.ReadAllBytes(file);
            DateTime written = File.GetLastWriteTimeUtc(file);

            // Longer than a rewrite takes, and past the next one a running owner would make.
            Thread.Sleep(500);
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(contents));
            Assert.That(File.GetLastWriteTimeUtc(file), Is.EqualTo(written));
            Assert.That(Directory.GetFiles(_folder, "*.tmp"), Is.Empty);
        }

        [Test]
        public void SecondLobby_IsInTheFolder_ByTheEndOfTheUpdateThatCreatedIt()
        {
            // A lobby that a listing adds is written at once, past the shortest interval and
            // ahead of the writer thread, so a join by its ID right after finds it.
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId first = CreateLobby(owner, LobbyVisibility.Public, 4);
            LobbyId second = CreateLobby(owner, LobbyVisibility.Public, 4);

            Assert.That(MachineDirectory.Read(_folder).Select(entry => entry.Lobby), Is.EquivalentTo(new[] { first, second }));
            Assert.That(Join(CreateRawBackend(CreateNetwork(), "Joiner"), second), Is.EqualTo(MultiplayerReason.None));
        }

        [Test]
        public void Owner_LeavingItsLastLobby_DeletesTheLobbyFile()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);

            owner.LeaveLobby(lobby);

            PumpUntil(() => Directory.GetFiles(_folder).Length == 0, "the lobby file to be deleted");
        }

        [Test]
        public void Directory_ReadFromAnotherThread_WhileTheOwnerRewritesIt_ListsTheLobbyWhole()
        {
            const int Rounds = 5;
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            PumpFor(0.3);
            Assert.That(owner.SetLobbyData(lobby, "round", "0"), Is.True);
            PumpUntil(() => ListedData(lobby, "round") == "0", "the first round to reach the file");

            // Another thread reads the folder over and over, as a search in another process does,
            // while the owner rewrites the file. On Windows the file is missing or held for a
            // moment in each replacement, which a read waits out.
            PlayerId ownerId = owner.LocalPlayerId;
            int ownerPort = owner.Port;
            int reads = 0;
            int listed = 0;
            int missed = 0;
            List<string> broken = new List<string>();
            Exception failure = null;
            ManualResetEventSlim isDone = new ManualResetEventSlim();
            Thread reader = new Thread(() =>
            {
                try
                {
                    while (!isDone.IsSet)
                    {
                        reads++;
                        bool isListed = false;
                        foreach (MachineDirectoryEntry entry in MachineDirectory.Read(_folder))
                        {
                            listed++;
                            isListed |= entry.Lobby == lobby;
                            bool isWhole = entry.Lobby == lobby
                                && entry.Owner == ownerId
                                && entry.Port == ownerPort
                                && entry.Capacity == 4
                                && entry.Data.TryGetValue("round", out string listedRound)
                                && int.TryParse(listedRound, NumberStyles.None, CultureInfo.InvariantCulture, out int value)
                                && value <= Rounds;
                            if (!isWhole)
                            {
                                broken.Add($"{entry.Lobby} on {entry.Port}, capacity {entry.Capacity}: {string.Join(", ", entry.Data.Select(pair => pair.Key + "=" + pair.Value))}");
                            }
                        }

                        if (!isListed)
                        {
                            missed++;
                        }
                    }
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });

            reader.Start();
            try
            {
                for (int round = 1; round <= Rounds; round++)
                {
                    PumpFor(0.3);
                    Assert.That(owner.SetLobbyData(lobby, "round", round.ToString(CultureInfo.InvariantCulture)), Is.True);
                }

                PumpFor(0.3);
            }
            finally
            {
                isDone.Set();
                reader.Join();
                isDone.Dispose();
            }

            TestContext.Out.WriteLine($"{reads} reads, {listed} of which listed the lobby, {missed} missed it.");
            Assert.That(failure, Is.Null, failure?.ToString());
            Assert.That(broken, Is.Empty);
            Assert.That(listed, Is.GreaterThan(0));
            Assert.That(missed, Is.Zero, $"{missed} of {reads} reads missed the lobby.");

            // A rewrite that the reader got in the way of is tried again.
            PumpUntil(() => ListedData(lobby, "round") == Rounds.ToString(CultureInfo.InvariantCulture), "the last round to reach the file");
        }

        [Test]
        public void Directory_ReadInTheMiddleOfAReplacement_WaitsForTheLobbyFile()
        {
            // As File.Replace leaves the folder on Windows for a moment: the old file moved aside,
            // the new one, just written, not yet in place, so no file has the lobby file's name.
            // The rounds move the new one in at times 10 microseconds apart from the start of the
            // read, and then a millisecond apart, so some move it between the read listing a file
            // and looking at it.
            const int FineRounds = 200;
            const int CoarseRounds = 25;
            MachineDirectory directory = new MachineDirectory(_folder, 7);
            directory.Publish(new PlayerId(9), 7, new List<MachineLobby> { ListedLobby(1, "map", "forest") });
            directory.Abandon();
            string file = Directory.GetFiles(_folder, "*.lobbies").Single();
            string temporary = Path.ChangeExtension(file, ".tmp");

            for (int round = 0; round < FineRounds + CoarseRounds; round++)
            {
                File.Move(file, temporary);
                File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow);
                double delayMilliseconds = round < FineRounds ? round * 0.01 : round - FineRounds;
                long delayTicks = (long)(delayMilliseconds * Stopwatch.Frequency / 1000.0);
                long readStart = 0;
                Thread owner = new Thread(() =>
                {
                    long start;
                    while ((start = Interlocked.Read(ref readStart)) == 0)
                    {
                        Thread.SpinWait(10);
                    }

                    while (Stopwatch.GetTimestamp() - start < delayTicks)
                    {
                        Thread.SpinWait(10);
                    }

                    File.Move(temporary, file);
                });
                owner.Start();
                try
                {
                    Interlocked.Exchange(ref readStart, Stopwatch.GetTimestamp());
                    Assert.That(MachineDirectory.Read(_folder).Select(entry => entry.Lobby), Is.EqualTo(new[] { new LobbyId(1) }),
                        $"Round {round} missed the lobby moved in {delayMilliseconds:0.00} ms after the read started.");
                }
                finally
                {
                    Interlocked.CompareExchange(ref readStart, Stopwatch.GetTimestamp(), 0);
                    owner.Join();
                }
            }

            // What a process that ended in the middle of a replacement left is older, and nobody
            // waits for it: neither two seconds later, far longer than a replacement takes, while a
            // lobby file that old would still count, nor once it is stale, when the read deletes it.
            File.Move(file, temporary);
            File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow.AddSeconds(-2.0));
            Stopwatch read = Stopwatch.StartNew();
            Assert.That(MachineDirectory.Read(_folder), Is.Empty);
            Assert.That(read.Elapsed.TotalSeconds, Is.LessThan(MachineDirectory.ReplaceWaitSeconds * 0.8), "The read waited for a file that a process that ended two seconds ago left.");
            Assert.That(File.Exists(temporary), Is.True, "The read deleted a temporary file younger than a lobby file goes stale.");

            File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow.AddSeconds(-MachineDirectory.StaleSeconds - 1.0));
            read.Restart();
            Assert.That(MachineDirectory.Read(_folder), Is.Empty);
            Assert.That(read.Elapsed.TotalSeconds, Is.LessThan(MachineDirectory.ReplaceWaitSeconds * 0.8), "The read waited for a file that a process that ended long ago left.");
            Assert.That(File.Exists(temporary), Is.False, "The read left a stale temporary file of a process that ended.");
        }

        [Test]
        public void Directory_DeletesTheTemporaryFileAndBackupsThatAProcessThatEndedLeft_AndKeepsTheOthers()
        {
            Directory.CreateDirectory(_folder);
            DateTime stale = DateTime.UtcNow.AddSeconds(-MachineDirectory.StaleSeconds - 1.0);

            // Port 5: a process that ended long ago in the middle of a replacement, with the old
            // lobby file moved aside and the new one not yet in place.
            string endedTemporary = WriteFile("peer-5.tmp", stale);
            string endedBackup = WriteFile("peer-5.lobbies~RF1a2b3c.TMP", stale);
            string unknown = WriteFile("peer-5.other", stale);

            // Port 6: a process that ended long ago in the middle of a write, whose lobby file went stale.
            MachineDirectory ended = new MachineDirectory(_folder, 6);
            ended.Publish(new PlayerId(6), 6, new List<MachineLobby> { ListedLobby(6, "map", "forest") });
            ended.Abandon();
            string staleFile = Path.Combine(_folder, "peer-6.lobbies");
            byte[] bytes = File.ReadAllBytes(staleFile);
            BitConverter.GetBytes(stale.Ticks).CopyTo(bytes, 8);
            File.WriteAllBytes(staleFile, bytes);
            string staleTemporary = WriteFile("peer-6.tmp", stale);

            // Port 7: an owner in the middle of a replacement, whose new file it just wrote, and
            // whose backup has the time of the lobby file it replaces, rewritten a few seconds ago.
            string freshTemporary = WriteFile("peer-7.tmp", DateTime.UtcNow);
            string freshBackup = WriteFile("peer-7.lobbies~RF4d5e6f.TMP", DateTime.UtcNow.AddSeconds(-2.0));

            // Port 8: an owner that runs, beside a temporary file that a process that ended on
            // the same port long ago left; its own next write replaces that file.
            MachineDirectory running = new MachineDirectory(_folder, 8);
            running.Publish(new PlayerId(8), 8, new List<MachineLobby> { ListedLobby(8, "map", "forest") });
            try
            {
                string oldTemporary = WriteFile("peer-8.tmp", stale);

                Assert.That(MachineDirectory.Read(_folder).Select(entry => entry.Lobby), Is.EqualTo(new[] { new LobbyId(8) }));

                Assert.That(new[] { endedTemporary, endedBackup, staleFile, staleTemporary }.Where(File.Exists), Is.Empty);
                Assert.That(new[] { unknown, freshTemporary, freshBackup, oldTemporary }.Where(path => !File.Exists(path)), Is.Empty);
            }
            finally
            {
                running.Remove();
            }
        }

        [Test]
        public void Directory_SkipsAFileThatDeclaresOrHoldsMoreThanAnOwnerLists_AndReadsTheOthers()
        {
            MachineDirectory valid = new MachineDirectory(_folder, 1);
            valid.Publish(new PlayerId(1), 1, new List<MachineLobby> { ListedLobby(1, "map", "forest") });
            try
            {
                // What a listing starts with: the magic number, the format version, the time
                // written, the port, and the owner.
                byte[] header = File.ReadAllBytes(Path.Combine(_folder, "peer-1.lobbies")).Take(28).ToArray();

                // Short files that declare more lobbies, or more data entries for a lobby, than an owner lists.
                WriteListing(2, header, writer => writer.Write(int.MaxValue));
                WriteListing(3, header, writer =>
                {
                    writer.Write(1);
                    WriteListedLobby(writer, 3, int.MaxValue, 0);
                });

                // Whole files that list one lobby more, or one data entry more, than an owner does.
                WriteListing(4, header, writer =>
                {
                    writer.Write(MachineDirectory.MaxListedLobbies + 1);
                    for (int i = 0; i <= MachineDirectory.MaxListedLobbies; i++)
                    {
                        WriteListedLobby(writer, 400UL + (ulong)i, 0, 0);
                    }
                });
                WriteListing(5, header, writer =>
                {
                    writer.Write(1);
                    WriteListedLobby(writer, 5, MachineDirectory.MaxLobbyDataEntries + 1, MachineDirectory.MaxLobbyDataEntries + 1);
                });

                // Files whose lobby is whole, followed by nothing up to the largest file an owner
                // writes, and one byte more.
                string largest = WriteListing(6, header, writer =>
                {
                    writer.Write(1);
                    WriteListedLobby(writer, 6, 1, 1);
                });
                string larger = WriteListing(7, header, writer =>
                {
                    writer.Write(1);
                    WriteListedLobby(writer, 7, 1, 1);
                });
                using (FileStream stream = new FileStream(largest, FileMode.Open, FileAccess.Write))
                {
                    stream.SetLength(MachineDirectory.MaxFileBytes);
                }

                using (FileStream stream = new FileStream(larger, FileMode.Open, FileAccess.Write))
                {
                    stream.SetLength(MachineDirectory.MaxFileBytes + 1);
                }

                Assert.That(MachineDirectory.Read(_folder).Select(entry => entry.Lobby), Is.EquivalentTo(new[] { new LobbyId(1), new LobbyId(6) }));
            }
            finally
            {
                valid.Remove();
            }
        }

        [Test]
        public void Directory_ReadsAListingAtTheBounds_WhichLeavesOutTheLobbiesPastThem()
        {
            // As many lobbies as a file lists, the first with as many data entries as a lobby
            // carries and the others with as much data as a MultiplayerManager's lobby holds.
            string value = new string('v', MultiplayerLimits.MaxLobbyDataSize - 3);
            MachineLobby crowded = ListedLobby(1, "map", value);
            for (int i = 1; i < MachineDirectory.MaxLobbyDataEntries; i++)
            {
                crowded.Data["k" + i.ToString(CultureInfo.InvariantCulture)] = string.Empty;
            }

            // Past the bounds: one data entry more, a lobby that does not fit in the file, and one
            // lobby more than a file lists, after the others.
            MachineLobby tooCrowded = ListedLobby(2, "map", value);
            for (int i = 1; i <= MachineDirectory.MaxLobbyDataEntries; i++)
            {
                tooCrowded.Data["k" + i.ToString(CultureInfo.InvariantCulture)] = string.Empty;
            }

            MachineLobby tooLarge = ListedLobby(3, "map", value);
            for (int i = 1; i * MachineProtocol.MaxDataValueBytes <= MachineDirectory.MaxFileBytes; i++)
            {
                tooLarge.Data["k" + i.ToString(CultureInfo.InvariantCulture)] = new string('v', MachineProtocol.MaxDataValueBytes);
            }

            List<MachineLobby> lobbies = new List<MachineLobby> { crowded, tooCrowded, tooLarge };
            List<LobbyId> expected = new List<LobbyId> { crowded.Id };
            for (ulong id = 10; lobbies.Count < MachineDirectory.MaxListedLobbies + 2; id++)
            {
                lobbies.Add(ListedLobby(id, "map", value));
                expected.Add(new LobbyId(id));
            }

            lobbies.Add(ListedLobby(100, "map", value));

            MachineDirectory directory = new MachineDirectory(_folder, 1);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("is left out of search"));
            directory.Publish(new PlayerId(9), 1, lobbies);
            try
            {
                Assert.That(new FileInfo(Path.Combine(_folder, "peer-1.lobbies")).Length, Is.LessThanOrEqualTo(MachineDirectory.MaxFileBytes));
                List<MachineDirectoryEntry> entries = MachineDirectory.Read(_folder);
                Assert.That(entries.Select(entry => entry.Lobby), Is.EqualTo(expected));
                Assert.That(entries[0].Data, Has.Count.EqualTo(MachineDirectory.MaxLobbyDataEntries));
                Assert.That(entries[1].Data["map"], Is.EqualTo(value));

                // The lobbies left out do not count as added, which would write the file on the
                // main thread at every update.
                Assert.That(directory.AddsLobby(lobbies), Is.False);
            }
            finally
            {
                directory.Remove();
            }
        }

        [Test]
        public void SearchAndJoin_ThatMeetAReplacement_FindTheLobby_WithoutHoldingAnUpdate()
        {
            // An update that read the files itself would hold the frame for the whole wait, at
            // least a sleep of about 16 ms in batch mode on Windows, and the full
            // ReplaceWaitSeconds for a replacement that does not finish in time.
            double bound = MachineDirectory.ReplaceWaitSeconds * 1000.0 * 0.5;
            LocalBackend seeker = CreateRawBackend(CreateNetwork(), "Seeker");
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            string file = Directory.GetFiles(_folder, "*.lobbies").Single();
            string temporary = Path.ChangeExtension(file, ".tmp");

            // The new file moves in 10 ms after the search's first update started.
            File.Move(file, temporary);
            File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow);
            (double Slowest, double Answer, double Updates, bool IsFound) moved = TimeSearch(seeker, lobby, () => File.Move(temporary, file), 10.0);
            Assert.That(moved.IsFound, Is.True, "The search missed the lobby whose file its owner was replacing.");
            Assert.That(moved.Slowest, Is.LessThan(bound), "An update of the search waited for the replacement.");

            // A replacement that does not finish in time: the read waits it out on its own thread.
            File.Move(file, temporary);
            File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow);
            (double Slowest, double Answer, double Updates, bool IsFound) unfinished = TimeSearch(seeker, lobby, null, 0.0);
            File.Move(temporary, file);
            Assert.That(unfinished.IsFound, Is.False);
            Assert.That(unfinished.Slowest, Is.LessThan(bound), "An update of the search waited for the replacement.");
            Assert.That(unfinished.Answer, Is.GreaterThan(MachineDirectory.ReplaceWaitSeconds * 1000.0 * 0.8), "The read did not wait for the replacement.");

            // A join by the lobby's ID, whose owner answers it.
            File.Move(file, temporary);
            File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow);
            MultiplayerReason? reason = null;
            seeker.JoinLobby(lobby, r => reason = r);
            (double Slowest, double Answer, double Updates) joined = TimeUpdates(seeker, () => reason.HasValue, () => File.Move(temporary, file), 10.0, owner);
            Assert.That(reason, Is.EqualTo(MultiplayerReason.None));
            Assert.That(joined.Slowest, Is.LessThan(bound), "An update of the join waited for the replacement.");
        }

        [Test]
        public void Directory_RewriteThatFails_IsTriedAgain_BeforeTheNextRefresh()
        {
            // Only Windows refuses to replace a file another handle opened with FileShare.None;
            // elsewhere the first rewrite would succeed and the test prove nothing.
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
            {
                Assert.Ignore("Needs Windows file sharing to make the rewrite fail.");
            }

            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            string file = Directory.GetFiles(_folder, "*.lobbies").Single();

            // Another program holds the file, so the owner cannot replace it.
            using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                PumpFor(0.3);
                Assert.That(owner.SetLobbyData(lobby, "map", "forest"), Is.True);
                PumpFor(0.6);
            }

            // The owner refreshes its file every two seconds, and tries a failed write again in half a second.
            PumpUntil(() => ListedData(lobby, "map") == "forest", "the change to reach the file", 1.0);
        }

        [Test]
        public void Directory_LeavesOutALobbyWhoseKeyOrValueIsLongerThanTheFramesCarry()
        {
            string longestKey = new string('k', MachineProtocol.MaxDataKeyBytes);
            string longestValue = new string('v', MachineProtocol.MaxDataValueBytes);
            MachineLobby whole = ListedLobby(1, longestKey, longestValue);
            MachineLobby longKey = ListedLobby(2, longestKey + "k", "v");
            MachineLobby longValue = ListedLobby(3, "map", longestValue + "v");
            MachineLobby after = ListedLobby(4, "map", "forest");

            // Written as a process of another version could write it.
            MachineDirectory directory = new MachineDirectory(_folder, 1);
            directory.Publish(new PlayerId(9), 1, new List<MachineLobby> { whole, longKey, longValue, after });
            try
            {
                List<MachineDirectoryEntry> entries = MachineDirectory.Read(_folder);

                Assert.That(entries.Select(entry => entry.Lobby), Is.EqualTo(new[] { whole.Id, after.Id }));
                Assert.That(entries[0].Data[longestKey], Is.EqualTo(longestValue));
                Assert.That(entries[1].Data["map"], Is.EqualTo("forest"));
            }
            finally
            {
                directory.Remove();
            }
        }

        [Test]
        public void Join_WithANameLongerThanTheFramesCarry_IsDropped_AndTheOwnerGoesOn()
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            PlayerId intruder = new PlayerId(0x1234567UL);

            PacketWriter writer = new PacketWriter();
            writer.WriteInt(MachineProtocol.Magic);
            writer.WriteInt(MachineProtocol.Version);
            writer.WriteULong(lobby.Value);
            writer.WriteULong(intruder.Value);
            writer.WriteInt(1);
            writer.WriteString(new string('n', MachineProtocol.MaxNameBytes + 1));
            writer.WriteBool(false);

            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Connect(new IPEndPoint(IPAddress.Loopback, owner.Port));
                socket.Send(Frame(MachineFrameKind.LobbyJoin, writer.Written));

                // The owner drops the frame and its connection without an answer.
                int received = -1;
                PumpUntil(() => (received = ReceiveOnce(socket)) >= 0, "the owner to close the connection");
                Assert.That(received, Is.Zero);
            }

            Assert.That(owner.IsLobbyMember(lobby, intruder), Is.False);
            LocalBackend joiner = CreateRawBackend(CreateNetwork(), "Joiner");
            Assert.That(Join(joiner, lobby), Is.EqualTo(MultiplayerReason.None));
            Assert.That(owner.IsLobbyMember(lobby, joiner.LocalPlayerId), Is.True);
        }

        [Test]
        public void Join_ToAnOwnerWhoseLobbyCarriesAValueLongerThanTheFramesCarry_Fails_AndTheJoinerGoesOn()
        {
            // Another process lists a lobby and answers the join with data that no owner of this
            // version sends.
            using (Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                listener.Listen(1);
                int port = ((IPEndPoint)listener.LocalEndPoint).Port;
                PlayerId impostor = new PlayerId(0x7654321UL);
                MachineLobby listed = ListedLobby(0x123456UL, "map", "forest");
                listed.OwnerId = impostor;
                listed.OwnerPort = port;
                listed.Members.Add(new MachineMember { Id = impostor, Port = port, Name = "Impostor" });

                MachineDirectory directory = new MachineDirectory(_folder, port);
                directory.Publish(impostor, port, new List<MachineLobby> { listed });
                try
                {
                    LocalBackend joiner = CreateRawBackend(CreateNetwork(), "Joiner");
                    MultiplayerReason? reason = null;
                    joiner.JoinLobby(listed.Id, r => reason = r);
                    PumpUntil(() => listener.Poll(0, SelectMode.SelectRead), "the join to connect");
                    using (Socket accepted = listener.Accept())
                    {
                        listed.Data["map"] = new string('v', MachineProtocol.MaxDataValueBytes + 1);
                        PacketWriter writer = new PacketWriter();
                        writer.WriteInt((int)MultiplayerReason.None);
                        MachineProtocol.WriteSnapshot(writer, listed);
                        accepted.Send(Frame(MachineFrameKind.LobbyReply, writer.Written));
                        PumpUntil(() => reason.HasValue, "the join");
                    }

                    Assert.That(reason, Is.EqualTo(MultiplayerReason.LobbyNotFound));
                    Assert.That(joiner.GetLobbyOwner(listed.Id).IsValid, Is.False);

                    LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
                    LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
                    Assert.That(Join(joiner, lobby), Is.EqualTo(MultiplayerReason.None));
                }
                finally
                {
                    directory.Remove();
                }
            }
        }

        [Test]
        public void PlayerName_LongerThanTheRosterTakes_ReachesTheOtherProcessesCut()
        {
            // 300 UTF-8 bytes, more than a frame carries.
            string name = new string('가', 100);
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            LocalBackend joiner = CreateRawBackend(CreateNetwork(), name);

            Assert.That(Join(joiner, lobby), Is.EqualTo(MultiplayerReason.None));
            Assert.That(joiner.GetPlayerName(joiner.LocalPlayerId), Is.EqualTo(name));
            Assert.That(owner.GetPlayerName(joiner.LocalPlayerId), Is.EqualTo(name.Substring(0, MultiplayerLimits.MaxPlayerNameLength)));
        }

        [Test]
        public void LobbyData_LongerThanTheFramesCarry_IsRefused_AndTheLongestReachesEveryProcess()
        {
            string longestKey = new string('k', MachineProtocol.MaxDataKeyBytes);
            string longestValue = new string('v', MachineProtocol.MaxDataValueBytes);
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);

            Assert.That(owner.SetLobbyData(lobby, longestKey + "k", "v"), Is.False);
            Assert.That(owner.SetLobbyData(lobby, "map", longestValue + "v"), Is.False);
            Assert.That(owner.SetLobbyData(lobby, longestKey, longestValue), Is.True);

            MultiplayerReason? created = null;
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("lobby was not created"));
            owner.CreateLobby(LobbyVisibility.Public, 4, new Dictionary<string, string> { ["map"] = longestValue + "v" }, (r, id) => created = r);
            PumpUntil(() => created.HasValue, "the refused lobby");
            Assert.That(created, Is.EqualTo(MultiplayerReason.BackendError));

            LocalBackend member = CreateRawBackend(CreateNetwork(), "Member");
            Assert.That(SearchedData(member, lobby, data => data.TryGetValue(longestKey, out string value) && value == longestValue), Is.Not.Null);
            Assert.That(Join(member, lobby), Is.EqualTo(MultiplayerReason.None));
            Assert.That(member.GetLobbyData(lobby, longestKey), Is.EqualTo(longestValue));
        }

        #endregion

        #region Lobby file cost
        /// <summary>
        /// What one rewrite of the lobby file allocates, logged in a line that starts with
        /// <c>[Lobby file]</c>: the call the writer thread makes for each listing, made here, where
        /// the GC.Alloc recorder can be limited to this thread. Unity's Mono does not count the
        /// bytes of one thread, so the bytes are how much the managed heap grew over many writes,
        /// from batches that no collection interrupted: a coarse figure, since the heap grows by
        /// whole blocks.
        /// </summary>
        [Explicit("Logs a measurement; run it by name.")]
        [Test]
        public void LobbyFileWrite_Allocations_AreMeasured()
        {
            const int Writes = 40;
            const int Batches = 20;
            const int BatchWrites = 100;
            byte[] listing = new byte[160];
            new System.Random(1).NextBytes(listing);
            MachineDirectory directory = new MachineDirectory(_folder, 1);

            // The first write creates the folder and the file; the later ones replace the file, as
            // the writer thread does.
            Assert.That(directory.TryWrite(listing, listing.Length), Is.True);
            Assert.That(directory.TryWrite(listing, listing.Length), Is.True);

            UnityEngine.Profiling.Recorder recorder = UnityEngine.Profiling.Recorder.Get("GC.Alloc");
            int[] counts = new int[Writes];
            for (int i = 0; i < Writes; i++)
            {
                recorder.FilterToCurrentThread();
                recorder.enabled = false;
                recorder.enabled = true;
                bool isWritten = directory.TryWrite(listing, listing.Length);
                recorder.enabled = false;
                Assert.That(isWritten, Is.True);
                counts[i] = recorder.sampleBlockCount;
            }

            recorder.CollectFromAllThreads();
            Array.Sort(counts);
            Assert.That(counts[Writes - 1], Is.GreaterThan(0), "The GC.Alloc recorder saw no allocation.");

            List<double> bytes = new List<double>();
            for (int batch = 0; batch < Batches; batch++)
            {
                int collections = GC.CollectionCount(0);
                long before = GC.GetTotalMemory(false);
                for (int i = 0; i < BatchWrites; i++)
                {
                    directory.TryWrite(listing, listing.Length);
                }

                long after = GC.GetTotalMemory(false);
                if (GC.CollectionCount(0) == collections && after > before)
                {
                    bytes.Add((after - before) / (double)BatchWrites);
                }
            }

            bytes.Sort();
            string report = $"[Lobby file] one rewrite allocates: count min={counts[0]} median={counts[Writes / 2]} max={counts[Writes - 1]}"
                + " | bytes per write, heap growth over " + BatchWrites + " writes: "
                + (bytes.Count > 0
                    ? $"min={bytes[0]:0} quartiles={bytes[bytes.Count / 4]:0}/{bytes[bytes.Count / 2]:0}/{bytes[bytes.Count * 3 / 4]:0} max={bytes[bytes.Count - 1]:0} ({bytes.Count} of {Batches} batches)"
                    : "n/a, a collection ran in every batch");
            UnityEngine.Debug.Log(report);
            TestContext.Out.WriteLine(report);
        }

        [Test]
        public void LobbyFileWrite_ReplacesOrCreatesTheFile_WhateverElseLeftOrDeletedIt()
        {
            MachineDirectory directory = new MachineDirectory(_folder, 3);
            string file = Path.Combine(_folder, "peer-3.lobbies");

            // A process that ended on this port left its file: the first write replaces it.
            Directory.CreateDirectory(_folder);
            File.WriteAllBytes(file, new byte[] { 1 });
            Assert.That(directory.TryWrite(new byte[] { 2, 2 }, 2), Is.True);
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(new byte[] { 2, 2 }));

            // A reader deleted it, taking this owner for ended: the next write creates it again.
            File.Delete(file);
            Assert.That(directory.TryWrite(new byte[] { 3, 3, 3 }, 3), Is.True);
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(new byte[] { 3, 3, 3 }));

            Assert.That(directory.TryWrite(new byte[] { 4 }, 1), Is.True);
            Assert.That(File.ReadAllBytes(file), Is.EqualTo(new byte[] { 4 }));
            Assert.That(Directory.GetFiles(_folder), Has.Length.EqualTo(1));
        }

        /// <summary>
        /// How long the main thread takes, logged in a line that starts with <c>[Lobby file]</c>,
        /// where it may wait for a write of the writer thread: the update that deletes the file
        /// after the last lobby left, <see cref="LocalBackend.Dispose"/>,
        /// <see cref="LocalBackend.SimulateCrash"/>, and the update in which an owner whose file
        /// is there creates another lobby, which writes the file itself. Each runs once while a
        /// rewrite is in flight and once while the writer thread is idle; the difference is the wait.
        /// </summary>
        [Explicit("Measures in real time, about 45 seconds; run it by name.")]
        [Test]
        public void LobbyFile_MainThreadWaits_AreMeasured()
        {
            const int Rounds = 12;
            double[] leave = new double[Rounds], leaveIdle = new double[Rounds];
            double[] dispose = new double[Rounds], disposeIdle = new double[Rounds];
            double[] crash = new double[Rounds], crashIdle = new double[Rounds];
            double[] create = new double[Rounds], createIdle = new double[Rounds];
            int inFlight = 0;
            for (int round = 0; round < Rounds; round++)
            {
                for (int busy = 0; busy < 2; busy++)
                {
                    bool isBusy = busy == 1;

                    // The listing that deletes the file waits out the shortest interval after the
                    // last listing, so the slowest update in that time is the one that deleted it.
                    (LocalBackend owner, LobbyId lobby) = OwnerWithAWrite(round, isBusy, ref inFlight);
                    owner.LeaveLobby(lobby);
                    double slowest = 0.0;
                    Stopwatch waited = Stopwatch.StartNew();
                    while (waited.Elapsed.TotalSeconds < 0.4)
                    {
                        slowest = Math.Max(slowest, Time(owner.Update));
                        Thread.Sleep(1);
                    }

                    Assert.That(Directory.GetFiles(_folder, "*.lobbies"), Is.Empty);
                    (isBusy ? leave : leaveIdle)[round] = slowest;

                    (owner, lobby) = OwnerWithAWrite(round, isBusy, ref inFlight);
                    (isBusy ? dispose : disposeIdle)[round] = Time(owner.Dispose);

                    (owner, lobby) = OwnerWithAWrite(round, isBusy, ref inFlight);
                    (isBusy ? crash : crashIdle)[round] = Time(owner.SimulateCrash);
                    foreach (string file in Directory.GetFiles(_folder))
                    {
                        File.Delete(file);
                    }

                    (owner, lobby) = OwnerWithAWrite(round, isBusy, ref inFlight);
                    LobbyId second = LobbyId.None;
                    owner.CreateLobby(LobbyVisibility.Public, 4, null, (r, id) => second = id);
                    (isBusy ? create : createIdle)[round] = Time(owner.Update);
                    Assert.That(second.IsValid, Is.True);
                    owner.Dispose();
                }
            }

            string report = "[Lobby file] main thread ms, with a rewrite in flight | with the writer thread idle:"
                + " last leave " + Summary(leave) + " | " + Summary(leaveIdle)
                + "; dispose " + Summary(dispose) + " | " + Summary(disposeIdle)
                + "; crash " + Summary(crash) + " | " + Summary(crashIdle)
                + "; second lobby " + Summary(create) + " | " + Summary(createIdle)
                + $"; rewrite seen in flight {inFlight} of {Rounds * 4} times";
            UnityEngine.Debug.Log(report);
            TestContext.Out.WriteLine(report);
        }

        /// <summary>
        /// How long the main thread takes in the updates of a search from another process, logged
        /// in a line that starts with <c>[Lobby file]</c>: with the owner's lobby file in place; in
        /// the middle of a replacement, the new file moved in a few milliseconds after the
        /// search's first update starts; and with only the new file that a process that ended in
        /// the middle of a replacement two seconds ago left. For each, the slowest update of the
        /// search, the time from its first update to its answer, and the updates it took; the
        /// updates come a millisecond's sleep apart, about 16 ms in batch mode on Windows. The
        /// first comes right after the search starts, before the read on a thread of the pool can
        /// be done, so a search answers in the second at the soonest here, while a game's next
        /// update, a frame after the search, usually finds the read done.
        /// </summary>
        [Explicit("Measures in real time, about 2 seconds; run it by name.")]
        [Test]
        public void LobbyFile_SearchesMeetingAReplacement_AreMeasured()
        {
            const int Rounds = 30;
            const double MoveInMilliseconds = 10.0;
            double[] inPlace = new double[Rounds], inPlaceAnswer = new double[Rounds], inPlaceUpdates = new double[Rounds];
            double[] replacing = new double[Rounds], replacingAnswer = new double[Rounds], replacingUpdates = new double[Rounds];
            double[] ended = new double[Rounds], endedAnswer = new double[Rounds], endedUpdates = new double[Rounds];
            int inPlaceFound = 0, replacingFound = 0, endedFound = 0;
            LocalBackend seeker = CreateRawBackend(CreateNetwork(), "Seeker");
            bool isFound;
            for (int round = 0; round < Rounds; round++)
            {
                LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
                LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
                (inPlace[round], inPlaceAnswer[round], inPlaceUpdates[round], isFound) = TimeSearch(seeker, lobby, null, 0.0);
                inPlaceFound += isFound ? 1 : 0;
                owner.Dispose();

                // As File.Replace leaves the folder for a moment: the old file gone, the new one
                // freshly written beside it and not yet moved in.
                owner = CreateRawBackend(CreateNetwork(), "Owner");
                lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
                string file = Directory.GetFiles(_folder, "*.lobbies").Single();
                string temporary = Path.ChangeExtension(file, ".tmp");
                File.Move(file, temporary);
                File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow);
                (replacing[round], replacingAnswer[round], replacingUpdates[round], isFound) = TimeSearch(seeker, lobby, () => File.Move(temporary, file), MoveInMilliseconds);
                replacingFound += isFound ? 1 : 0;
                owner.Dispose();

                owner = CreateRawBackend(CreateNetwork(), "Owner");
                lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
                owner.SimulateCrash();
                file = Directory.GetFiles(_folder, "*.lobbies").Single();
                temporary = Path.ChangeExtension(file, ".tmp");
                File.Move(file, temporary);
                File.SetLastWriteTimeUtc(temporary, DateTime.UtcNow.AddSeconds(-2.0));
                (ended[round], endedAnswer[round], endedUpdates[round], isFound) = TimeSearch(seeker, lobby, null, 0.0);
                endedFound += isFound ? 1 : 0;
                File.Delete(temporary);
            }

            string report = "[Lobby file] a search from another process, main thread ms of its slowest update | ms from its first update to its answer | updates:"
                + " file in place " + Summary(inPlace) + " | " + Summary(inPlaceAnswer) + " | " + Summary(inPlaceUpdates)
                + $", listed the lobby {inPlaceFound} of {Rounds} times"
                + $"; new file moved in {MoveInMilliseconds:0} ms after the first update started " + Summary(replacing) + " | " + Summary(replacingAnswer) + " | " + Summary(replacingUpdates)
                + $", listed the lobby {replacingFound} of {Rounds} times"
                + "; new file of a process that ended 2 s ago " + Summary(ended) + " | " + Summary(endedAnswer) + " | " + Summary(endedUpdates)
                + $", listed the lobby {endedFound} of {Rounds} times";
            UnityEngine.Debug.Log(report);
            TestContext.Out.WriteLine(report);
        }

        /// <summary>
        /// Searches from <paramref name="seeker"/>, as <see cref="TimeUpdates"/> times it, and
        /// whether the search listed <paramref name="lobby"/>.
        /// </summary>
        private static (double Slowest, double Answer, double Updates, bool IsFound) TimeSearch(LocalBackend seeker, LobbyId lobby, Action moveIn, double moveInMilliseconds)
        {
            IReadOnlyList<BackendLobby> found = null;
            seeker.SearchLobbies(new LobbySearchQuery(), (r, lobbies) => found = lobbies);
            (double slowest, double answer, double updates) = TimeUpdates(seeker, () => found != null, moveIn, moveInMilliseconds);
            return (slowest, answer, updates, found.Any(entry => entry.Id == lobby));
        }

        /// <summary>
        /// Updates <paramref name="backend"/> a millisecond's sleep apart until
        /// <paramref name="isAnswered"/>, with <paramref name="other"/>, when given, updated
        /// untimed in between: the slowest update in milliseconds, the milliseconds from the first
        /// update to the answer, and the updates it took. <paramref name="moveIn"/>, when given,
        /// runs on another thread <paramref name="moveInMilliseconds"/> after the first update
        /// starts, as an owner's replacement would finish.
        /// </summary>
        private static (double Slowest, double Answer, double Updates) TimeUpdates(LocalBackend backend, Func<bool> isAnswered, Action moveIn, double moveInMilliseconds, LocalBackend other = null)
        {
            long firstUpdate = 0;
            long moveInTicks = (long)(moveInMilliseconds * Stopwatch.Frequency / 1000.0);
            Exception moveFailure = null;
            Thread mover = null;
            if (moveIn != null)
            {
                mover = new Thread(() =>
                {
                    long start;
                    while ((start = Interlocked.Read(ref firstUpdate)) == 0)
                    {
                        Thread.SpinWait(10);
                    }

                    while (Stopwatch.GetTimestamp() - start < moveInTicks)
                    {
                        Thread.SpinWait(10);
                    }

                    try
                    {
                        moveIn();
                    }
                    catch (Exception e)
                    {
                        moveFailure = e;
                    }
                });
                mover.Start();
            }

            double slowest = 0.0;
            int updates = 0;
            long started = Stopwatch.GetTimestamp();
            try
            {
                Interlocked.Exchange(ref firstUpdate, started);
                while (!isAnswered())
                {
                    Assert.That((Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency, Is.LessThan(WaitSeconds), "No answer came.");
                    if (updates > 0)
                    {
                        Thread.Sleep(1);
                        other?.Update();
                    }

                    slowest = Math.Max(slowest, Time(backend.Update));
                    updates++;
                }
            }
            finally
            {
                mover?.Join();
            }

            Assert.That(moveFailure, Is.Null, moveFailure?.ToString());
            double answer = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            return (slowest, answer, updates);
        }

        /// <summary>
        /// An owner whose lobby file is there, past the shortest interval between listings, with
        /// a rewrite on the writer thread when <paramref name="isBusy"/>, caught while it runs.
        /// </summary>
        private (LocalBackend Owner, LobbyId Lobby) OwnerWithAWrite(int round, bool isBusy, ref int inFlight)
        {
            LocalBackend owner = CreateRawBackend(CreateNetwork(), "Owner");
            LobbyId lobby = CreateLobby(owner, LobbyVisibility.Public, 4);
            Thread.Sleep(300);
            if (isBusy)
            {
                string temporary = Path.Combine(_folder, "peer-" + owner.Port.ToString(CultureInfo.InvariantCulture) + ".tmp");
                Assert.That(owner.SetLobbyData(lobby, "round", round.ToString(CultureInfo.InvariantCulture)), Is.True);
                owner.Update();
                if (SpinWait.SpinUntil(() => File.Exists(temporary), 50))
                {
                    inFlight++;
                }
            }

            return (owner, lobby);
        }

        private static double Time(Action action)
        {
            long start = Stopwatch.GetTimestamp();
            action();
            return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        private static string Summary(double[] values)
        {
            double[] sorted = (double[])values.Clone();
            Array.Sort(sorted);
            return "median=" + sorted[sorted.Length / 2].ToString("0.000", CultureInfo.InvariantCulture)
                + " max=" + sorted[sorted.Length - 1].ToString("0.000", CultureInfo.InvariantCulture);
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
        public void ParticipantPings_ReachEveryProcess_EachPlayersOwn()
        {
            // FR-36, FR-40 between "processes": each process delays what it sends, so A's 100 ms
            // make a 100 ms round trip to the host and B's none, and every player reads both.
            Node host = CreateNode("Host");
            Node a = CreateNode("A", network => network.SimulatedLatencyMilliseconds = 100f);
            Node b = CreateNode("B");
            StartSession(host, a, b);
            Node[] everyone = { host, a, b };

            PumpUntil(() => everyone.All(node => PingOf(node, a.Id) >= 80 && PingOf(node, b.Id) >= 0), "every player to read both pings");
            foreach (Node node in everyone)
            {
                string name = node.Backend.PlayerName;
                TestContext.WriteLine($"{name} reads A at {PingOf(node, a.Id)} ms and B at {PingOf(node, b.Id)} ms.");
                Assert.That(PingOf(node, host.Id), Is.Zero, name);
                Assert.That(PingOf(node, a.Id), Is.InRange(80, 130), name);
                Assert.That(PingOf(node, b.Id), Is.InRange(0, 40), name);
            }

            Assert.That(b.Manager.TryGetConnectionQuality(a.Id, out _), Is.False, "B has no connection of its own to A");
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
        public void HostLeaving_RightAfterMoreThanItsSocketsTakeAtOnce_DeliversItsLastReliableMessagesFirst()
        {
            // FR-27, FR-32, NFR-4, #198: what the host sent last is still on its way on the session
            // connection when the hand-over on the lobby connection reaches the client, and the
            // client's session must not end before those messages and the close. The host sends
            // and leaves while the client does not read, on a thread of its own, so that a send
            // that waited for the client to read (#251) fails the test instead of holding it; the
            // client then reads a quarter of a megabyte in each update, so what the host's
            // sockets hold takes several updates to arrive.
            Node host = CreateNode("Host");
            Node client = CreateNode("Client");
            StartSession(host, client);
            _paused.Add(host.Backend);
            client.Backend.ReceiveBytesPerUpdate = 256 * 1024;

            const int count = 32;
            double leftAt = double.NaN;
            RunWithin(5.0, "sending 2 MiB to a client that does not read and leaving", () =>
            {
                byte[] payload = new byte[MultiplayerConfig.DefaultMaxMessageSize];
                for (int i = 0; i < count; i++)
                {
                    BitConverter.GetBytes(i).CopyTo(payload, 0);
                    MultiplayerResult sent = host.Manager.Send(Chat, payload, MessageTarget.Others);
                    if (!sent.IsSuccess)
                    {
                        throw new InvalidOperationException($"Message {i} answered {sent.Reason}.");
                    }
                }

                host.Manager.Leave();
                leftAt = _clock.Elapsed.TotalSeconds;
            }, () => client.Backend.SimulateCrash());

            PumpUntil(() => client.Manager.State == SessionState.Offline, "the client to end its session", 2.0);
            double offlineAt = _clock.Elapsed.TotalSeconds;

            Assert.That(client.Messages.Select(message => BitConverter.ToInt32(message.Payload, 0)), Is.EqualTo(Enumerable.Range(0, count)));
            Assert.That(client.Messages.Select(message => message.Payload.Length), Is.All.EqualTo(MultiplayerConfig.DefaultMaxMessageSize));
            Assert.That(client.States.Last().Reason, Is.EqualTo(MultiplayerReason.HostLeft));
            Assert.That(offlineAt - leftAt, Is.LessThan(2.0), "NFR-4");
        }

        [Test]
        public void Sends_LargerThanTheSocketsHold_ToAPlayerThatDoesNotRead_ReturnAtOnce_AndArriveInOrderOnceItReads([Values(false, true)] bool sendsSegments)
        {
            // #251: Unity's Socket.Send calls the system again until the socket took everything,
            // also on a non-blocking socket, so a send to a full socket waited for the other side
            // to read, and with both players in one process, as here, for ever. The host sends
            // megabytes to a client that does not update meanwhile, with either socket call of
            // its sending thread.
            Node host = CreateNode("Host");
            Node client = CreateNode("Client");
            host.Backend.SendsSegments = sendsSegments;
            StartSession(host, client);
            _paused.Add(client.Backend);

            const int count = 48;
            RunWithin(5.0, "sending 3 MiB to a client that does not read", () =>
            {
                byte[] payload = new byte[MultiplayerConfig.DefaultMaxMessageSize];
                for (int i = 0; i < count; i++)
                {
                    BitConverter.GetBytes(i).CopyTo(payload, 0);
                    MultiplayerResult sent = host.Manager.Send(Chat, payload, MessageTarget.Others);
                    if (!sent.IsSuccess)
                    {
                        throw new InvalidOperationException($"Message {i} answered {sent.Reason}.");
                    }
                }
            }, () => client.Backend.SimulateCrash());

            _paused.Remove(client.Backend);
            PumpUntil(() => client.Messages.Count >= count, "every message to arrive");
            Assert.That(client.Messages.Select(message => BitConverter.ToInt32(message.Payload, 0)), Is.EqualTo(Enumerable.Range(0, count)));
            Assert.That(client.Messages.Select(message => message.Payload.Length), Is.All.EqualTo(MultiplayerConfig.DefaultMaxMessageSize));
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(2), "The host kept the client.");
        }

        [Test]
        public void Send_ToAPlayerThatDoesNotRead_AnswersFalsePastWhatTheConnectionKeeps_AndTheConnectionGoesOn([Values(false, true)] bool sendsSegments)
        {
            // #251, as a full send buffer on Steam: once a connection holds more than the backend
            // keeps for it that its socket did not take, a send answers false at once.
            (LocalBackend host, RecordingListener hostEvents, LocalBackend client, RecordingListener clientEvents) = ConnectPair();
            ConnectionId toHost = clientEvents.Connected.Single().Connection;
            client.SendsSegments = sendsSegments;
            _paused.Add(host);

            byte[] packet = new byte[64 * 1024];
            int accepted = 0;
            bool isRefused = false;
            RunWithin(5.0, "sending to a host that does not read", () =>
            {
                for (int i = 0; i < 256 && !isRefused; i++)
                {
                    BitConverter.GetBytes(i).CopyTo(packet, 0);
                    if (client.Send(toHost, packet, DeliveryMode.Reliable))
                    {
                        accepted++;
                    }
                    else
                    {
                        isRefused = true;
                    }
                }
            }, () => host.SimulateCrash());

            Assert.That(isRefused, Is.True, "No send answered false.");
            _paused.Remove(host);
            PumpUntil(() => hostEvents.Received.Count >= accepted, "what was sent before the refusal");
            Assert.That(hostEvents.Received.Select(data => BitConverter.ToInt32(data, 0)), Is.EqualTo(Enumerable.Range(0, accepted)));

            Assert.That(client.Send(toHost, packet, DeliveryMode.Reliable), Is.True, "The connection takes sends again once the host read.");
            PumpUntil(() => hostEvents.Received.Count == accepted + 1, "the send after the refusal");
            Assert.That(hostEvents.Disconnected, Is.Empty);
            Assert.That(clientEvents.Disconnected, Is.Empty);
        }

        [Test]
        public void Sends_ToAPlayerThatReadsSlowly_GoOutAMegabyteACallAtMost_AndDoNotHoldTheOtherPlayers([Values(false, true)] bool sendsSegments)
        {
            // #290: once a full socket takes more again, megabytes can wait for it. Windows was
            // seen to take the whole of a send on a writable socket only up to a megabyte in
            // Unity, and a socket that took part of a larger one would keep Unity's Socket.Send
            // calling, and with it the sending thread and every other player's messages, until
            // the slow player read the rest. So each call hands a socket a megabyte at most, and
            // the other player's messages go out between the calls while the slow one reads.
            const int allowedLag = 8;
            (LocalBackend host, RecordingListener hostEvents, Peer slow, Peer other) = ConnectTwo();
            host.SendsSegments = sendsSegments;
            ConnectionId toSlow = ConnectionTo(hostEvents, slow);
            ConnectionId toOther = ConnectionTo(hostEvents, other);
            _paused.Add(slow.Backend);

            byte[] packet = new byte[64 * 1024];
            int accepted = 0;
            RunWithin(5.0, "filling the connection to a player that does not read", () =>
            {
                for (; accepted < 256; accepted++)
                {
                    BitConverter.GetBytes(accepted).CopyTo(packet, 0);
                    if (!host.Send(toSlow, packet, DeliveryMode.Reliable))
                    {
                        return;
                    }
                }

                throw new InvalidOperationException("No send answered false.");
            }, () => slow.Backend.SimulateCrash());

            // The slow player reads a little in each update; the host sends the other player a
            // message before each update and counts the updates until it arrives.
            slow.Backend.ReceiveBytesPerUpdate = 32 * 1024;
            _paused.Remove(slow.Backend);
            List<int> sentAt = new List<int>();
            int updates = 0;
            int arrived = 0;
            int maxLag = 0;
            Stopwatch waited = Stopwatch.StartNew();
            while (slow.Events.Received.Count < accepted || arrived < sentAt.Count)
            {
                Assert.That(waited.Elapsed.TotalSeconds, Is.LessThan(WaitSeconds), "Timed out waiting for what the slow player was sent.");
                if (slow.Events.Received.Count < accepted)
                {
                    Assert.That(host.Send(toOther, BitConverter.GetBytes(sentAt.Count), DeliveryMode.Reliable), Is.True);
                    sentAt.Add(updates);
                }

                Thread.Sleep(2);
                UpdateAll();
                updates++;

                // A connection delivers in order.
                for (; arrived < other.Events.Received.Count; arrived++)
                {
                    maxLag = Math.Max(maxLag, updates - sentAt[arrived]);
                }
            }

            Assert.That(slow.Events.Received.Select(data => BitConverter.ToInt32(data, 0)), Is.EqualTo(Enumerable.Range(0, accepted)));
            Assert.That(other.Events.Received.Select(data => BitConverter.ToInt32(data, 0)), Is.EqualTo(Enumerable.Range(0, sentAt.Count)));
            Assert.That(host.LargestSocketSend, Is.InRange(1, 1024 * 1024), "One socket call took more than a megabyte.");
            Assert.That(sentAt.Count, Is.GreaterThan(4 * allowedLag), "The slow player read its backlog too fast for the test to tell.");
            Assert.That(maxLag, Is.LessThanOrEqualTo(allowedLag), "A message to the other player waited while the slow player read.");
            Assert.That(hostEvents.Disconnected, Is.Empty);
        }

        [Test]
        public void SendingThread_ThatMeetsAnUnexpectedException_LosesTheConnectionItWasSendingOn_AndGoesOnWithTheOthers()
        {
            // #290: a defect that throws on a backend's sending thread would end the thread, and
            // nothing would leave the backend after it. The thread starts its loop over instead:
            // the connection it was sending on drops as lost, reported once, since how much of its
            // data reached the socket is unknown, and what the other connections were handed,
            // also while the thread failed, still goes out.
            (LocalBackend host, RecordingListener hostEvents, Peer failed, Peer kept) = ConnectTwo();
            ConnectionId toFailed = ConnectionTo(hostEvents, failed);
            ConnectionId toKept = ConnectionTo(hostEvents, kept);
            WaitForSends(host);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Exception, new System.Text.RegularExpressions.Regex("sending thread fails"));
            host.FailsSendingThread = true;
            Assert.That(host.Send(toFailed, BitConverter.GetBytes(1), DeliveryMode.Reliable), Is.True);
            Assert.That(host.Send(toKept, BitConverter.GetBytes(2), DeliveryMode.Reliable), Is.True);
            PumpUntil(() => hostEvents.Disconnected.Count > 0 && failed.Events.Disconnected.Count > 0 && kept.Events.Received.Count == 1, "the connection the thread was sending on to drop on both sides, and the other one to deliver");

            Assert.That(host.Send(toKept, BitConverter.GetBytes(3), DeliveryMode.Reliable), Is.True);
            PumpUntil(() => kept.Events.Received.Count == 2, "a send after the thread failed");
            PumpFor(0.5);

            Assert.That(hostEvents.Disconnected, Is.EqualTo(new[] { (toFailed, BackendDisconnectKind.ConnectionLost, 0) }));
            Assert.That(failed.Events.Disconnected.Select(disconnect => disconnect.Kind), Is.EqualTo(new[] { BackendDisconnectKind.ConnectionLost }));
            Assert.That(failed.Events.Received, Is.Empty, "The thread failed before its socket call.");
            Assert.That(kept.Events.Received.Select(data => BitConverter.ToInt32(data, 0)), Is.EqualTo(new[] { 2, 3 }));
            Assert.That(kept.Events.Disconnected, Is.Empty);
            Assert.That(host.FailsSendingThread, Is.False, "The switch turned itself off.");
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

        [Test]
        public void Outage_ShorterThanTheTimeout_KeepsTheConnection_AndDelaysReliableMessages()
        {
            // As the in-process backend: what a short outage holds back arrives once it ends. The
            // outage takes a quarter of the timeout, so a slow update cannot make it a drop.
            Node host = CreateNode("Host", network => network.ConnectionTimeoutSeconds = 2f);
            Node client = CreateNode("Client", network => network.ConnectionTimeoutSeconds = 2f);
            StartSession(host, client);

            client.Backend.SimulateOutage(0.5f);
            Assert.That(client.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host).IsSuccess, Is.True);
            Assert.That(client.Manager.Send(Chat, new byte[] { 2 }, MessageTarget.Host, DeliveryMode.Unreliable).IsSuccess, Is.True);
            Assert.That(host.Manager.Send(Chat, new byte[] { 3 }, MessageTarget.To(client.Id)).IsSuccess, Is.True);
            PumpFor(0.25);
            Assert.That(host.Messages.Concat(client.Messages), Is.Empty);

            PumpUntil(() => host.Messages.Count == 1 && client.Messages.Count == 1, "the reliable messages after the outage", 3.0);
            Assert.That(host.Messages.Single().Payload, Is.EqualTo(new byte[] { 1 }));
            Assert.That(client.Messages.Single().Payload, Is.EqualTo(new byte[] { 3 }));
            Assert.That(client.States.Last().Current, Is.EqualTo(SessionState.Lobby));
        }

        [Test]
        public void Outage_StartedInsideAnUpdate_LetsWhatTheUpdateSentBeforeItLeave()
        {
            // An answer sent from a callback of the update is on the wire before an outage the same
            // callback starts, as a packet sent between updates is, and not held back until it ends.
            CallbackListener hostEvents = new CallbackListener();
            RecordingListener clientEvents = new RecordingListener();
            LocalBackend host = CreateRawBackend(CreateNetwork(), "Host", hostEvents);
            LocalBackend client = CreateRawBackend(CreateNetwork(), "Client", clientEvents);
            LobbyId lobby = CreateLobby(host, LobbyVisibility.Public, 2);
            Assert.That(host.StartListening(), Is.True);
            Assert.That(Join(client, lobby), Is.EqualTo(MultiplayerReason.None));
            ConnectionId toHost = client.Connect(host.LocalPlayerId);
            PumpUntil(() => clientEvents.Connected.Count == 1 && hostEvents.Connected.Count == 1, "the connection");

            bool? answered = null;
            hostEvents.Received = (connection, data) =>
            {
                hostEvents.Received = null;
                answered = host.Send(connection, new byte[] { 7 }, DeliveryMode.Unreliable);
                host.SimulateOutage(5f);
            };
            Assert.That(client.Send(toHost, new byte[] { 1 }, DeliveryMode.Reliable), Is.True);

            PumpUntil(() => answered.HasValue, "the host to answer");
            Assert.That(answered, Is.True);
            PumpUntil(() => clientEvents.Received.Count == 1, "the answer sent just before the outage", 2.0);
            Assert.That(clientEvents.Received.Single(), Is.EqualTo(new byte[] { 7 }));
        }

        [Test]
        public void Outage_LongerThanTheTimeout_DropsTheConnection_AndTheClientComesBackToItsSlot()
        {
            // FR-20 across "processes": each side times out on its own, and the client connects
            // again. The outage lasts well past the timeout and the retries that fail during it.
            Node host = CreateNode("Host", network => network.ConnectionTimeoutSeconds = 2f);
            Node client = CreateNode("Client", network => network.ConnectionTimeoutSeconds = 2f);
            StartSession(host, client);
            Assert.That(host.Manager.StartGame(new byte[] { 4 }).IsSuccess, Is.True);
            PumpUntil(() => client.Manager.State == SessionState.InGame, "the game to start");

            client.Backend.SimulateOutage(5f);
            PumpUntil(() => client.Manager.State == SessionState.Reconnecting
                && host.Manager.TryGetParticipant(client.Id, out Participant away) && !away.IsConnected, "both sides to notice the outage", 4.5);
            PumpUntil(() => client.Manager.State == SessionState.InGame
                && host.Manager.TryGetParticipant(client.Id, out Participant back) && back.IsConnected, "the client to come back", 6.0);

            Assert.That(client.States.Select(change => change.Current).Skip(3), Is.EqualTo(new[] { SessionState.Reconnecting, SessionState.InGame }));
            Assert.That(client.GameStarts.Count, Is.EqualTo(1));
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(2));

            Assert.That(client.Manager.Send(Chat, new byte[] { 5 }, MessageTarget.Host).IsSuccess, Is.True);
            PumpUntil(() => host.Messages.Count == 1, "a message after the return");
        }

        [Test]
        public void Outage_ThatTheLobbyDropsTheClientIn_EndsWithTheClientBackInTheSameLobby()
        {
            // FR-20, FR-19, #201 across "processes": the client leaves the lobby behind its
            // manager's back, as a Steam lobby may drop a member whose network went down, and
            // enters it again once it is back, though the game refuses late joiners.
            Node host = CreateNode("Host", network => network.ConnectionTimeoutSeconds = 2f);
            Node client = CreateNode("Client", network => network.ConnectionTimeoutSeconds = 2f);
            StartSession(host, client);
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            PumpUntil(() => client.Manager.State == SessionState.InGame, "the game to start");
            LobbyId lobby = host.Manager.CurrentLobbyId;

            client.Backend.LeaveLobby(lobby);
            PumpUntil(() => !host.Backend.IsLobbyMember(lobby, client.Id), "the host to see the client leave the lobby");
            client.Backend.SimulateOutage(5f);
            PumpUntil(() => client.Manager.State == SessionState.Reconnecting, "the client to notice the outage", 4.5);
            PumpUntil(() => client.Manager.State == SessionState.InGame, "the client to come back", 6.0);
            PumpUntil(() => client.Backend.IsLobbyMember(lobby, client.Id) && host.Backend.IsLobbyMember(lobby, client.Id),
                "the client to be in the lobby again", 5.0);

            Assert.That(client.Manager.CurrentLobbyId, Is.EqualTo(lobby));
            Assert.That(client.Manager.State, Is.EqualTo(SessionState.InGame));
        }

        [Test]
        public void HostCrash_InAGame_EndsTheClientsSessionWithHostLeft_WithoutWaitingOutTheReservation()
        {
            // FR-27, FR-20 across "processes": whichever the client notices first, its session
            // connection or its lobby link breaking, the lobby passing on ends the session.
            Node host = CreateNode("Host", network => network.ConnectionTimeoutSeconds = 2f);
            Node client = CreateNode("Client", network => network.ConnectionTimeoutSeconds = 2f);
            StartSession(host, client);
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            PumpUntil(() => client.Manager.State == SessionState.InGame, "the game to start");

            host.Backend.SimulateCrash();
            PumpUntil(() => client.Manager.State == SessionState.Offline, "the client to end its session", 6.0);

            Assert.That(client.States.Last().Reason, Is.EqualTo(MultiplayerReason.HostLeft));
            Assert.That(client.States.Skip(3).Select(change => change.Current), Is.EqualTo(new[] { SessionState.Reconnecting, SessionState.Offline })
                .Or.EqualTo(new[] { SessionState.Offline }));
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

        private Node CreateNode(string name, Action<LocalNetwork> configure = null, PlayerId id = default)
        {
            LocalNetwork network = CreateNetwork(configure);
            LocalBackend backend = new LocalBackend(network, name, id);
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

        /// <summary>The value the folder lists for the key of the lobby, null when it lists neither.</summary>
        private string ListedData(LobbyId lobby, string key)
        {
            foreach (MachineDirectoryEntry entry in MachineDirectory.Read(_folder))
            {
                if (entry.Lobby == lobby && entry.Data.TryGetValue(key, out string value))
                {
                    return value;
                }
            }

            return null;
        }

        private List<BackendLobby> Search(LocalBackend backend)
        {
            IReadOnlyList<BackendLobby> lobbies = null;
            backend.SearchLobbies(new LobbySearchQuery(), (r, found) => lobbies = found);
            PumpUntil(() => lobbies != null, "the search");
            return lobbies.ToList();
        }

        /// <summary>
        /// Searches from another process until it shows the lobby with data that passes
        /// <paramref name="check"/>, and returns that data; null when it never does in time.
        /// </summary>
        private IReadOnlyDictionary<string, string> SearchedData(LocalBackend seeker, LobbyId lobby, Func<IReadOnlyDictionary<string, string>, bool> check)
        {
            Stopwatch waited = Stopwatch.StartNew();
            while (waited.Elapsed.TotalSeconds < WaitSeconds)
            {
                foreach (BackendLobby found in Search(seeker))
                {
                    if (found.Id == lobby && check(found.Data))
                    {
                        return found.Data;
                    }
                }
            }

            return null;
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

        /// <summary>The player's ping as the node reads it, or -1 while it reads none.</summary>
        private static int PingOf(Node node, PlayerId player)
        {
            return node.Manager.TryGetParticipantPing(player, out int ping) ? ping : -1;
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

        /// <summary>A host listening in a lobby and two members connected to it, each in a "process" of its own.</summary>
        private (LocalBackend Host, RecordingListener HostEvents, Peer First, Peer Second) ConnectTwo()
        {
            RecordingListener hostEvents = new RecordingListener();
            LocalBackend host = CreateRawBackend(CreateNetwork(), "Host", hostEvents);
            LobbyId lobby = CreateLobby(host, LobbyVisibility.Public, 3);
            Assert.That(host.StartListening(), Is.True);

            Peer first = CreatePeer("First");
            Peer second = CreatePeer("Second");
            foreach (Peer peer in new[] { first, second })
            {
                Assert.That(Join(peer.Backend, lobby), Is.EqualTo(MultiplayerReason.None));
                peer.Backend.Connect(host.LocalPlayerId);
            }

            PumpUntil(() => hostEvents.Connected.Count == 2 && first.Events.Connected.Count == 1 && second.Events.Connected.Count == 1, "both connections");
            return (host, hostEvents, first, second);
        }

        /// <summary>The host's connection to the peer.</summary>
        private static ConnectionId ConnectionTo(RecordingListener hostEvents, Peer peer)
        {
            return hostEvents.Connected.Single(connected => connected.Remote == peer.Id).Connection;
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
                PauseBetweenUpdates(waited);
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

                PauseBetweenUpdates(waited);
                UpdateAll();
            }
        }

        /// <summary>
        /// Waits between two updates of a pump that has run for <paramref name="waited"/>: it only
        /// yields for the first <see cref="YieldMilliseconds"/>, then sleeps. A search or a join by
        /// lobby ID answers in the first update after its read of the lobby files, which takes a
        /// few milliseconds on a thread of the pool, while a sleep takes about 16 ms in batch mode
        /// on Windows, so a pump that slept from the start waited one sleep more for each answer (#310).
        /// </summary>
        private static void PauseBetweenUpdates(Stopwatch waited)
        {
            if (waited.Elapsed.TotalMilliseconds < YieldMilliseconds)
            {
                Thread.Yield();
            }
            else
            {
                Thread.Sleep(2);
            }
        }

        /// <summary>Waits until the backend's sending thread handed everything to its sockets.</summary>
        private static void WaitForSends(LocalBackend backend)
        {
            Assert.That(SpinWait.SpinUntil(() => !backend.HasSendsInFlight, TimeSpan.FromSeconds(WaitSeconds)), Is.True, "The sending thread kept something.");
        }

        /// <summary>
        /// Runs <paramref name="action"/> on a thread of its own and waits for it at most
        /// <paramref name="seconds"/>, so a send that a full socket holds (#251) fails the test
        /// instead of holding the run. <paramref name="release"/> breaks the connection that
        /// holds it, so the thread ends before the test does.
        /// </summary>
        private static void RunWithin(double seconds, string what, Action action, Action release)
        {
            Exception failure = null;
            Thread thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    failure = e;
                }
            })
            {
                IsBackground = true,
            };

            thread.Start();
            if (!thread.Join(TimeSpan.FromSeconds(seconds)))
            {
                release();
                thread.Join(TimeSpan.FromSeconds(WaitSeconds));
                Assert.Fail($"Timed out after {seconds} s {what}: a send waited for the other side to read.");
            }

            Assert.That(failure, Is.Null, failure?.ToString());
        }

        /// <summary>A lobby as its owner lists it, with one data entry.</summary>
        private static MachineLobby ListedLobby(ulong id, string key, string value)
        {
            MachineLobby lobby = new MachineLobby { Id = new LobbyId(id), IsOwned = true, Visibility = LobbyVisibility.Public, Capacity = 4 };
            lobby.Data[key] = value;
            return lobby;
        }

        /// <summary>A file of one byte in the network's folder, last written at <paramref name="written"/>.</summary>
        private string WriteFile(string name, DateTime written)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllBytes(path, new byte[] { 1 });
            File.SetLastWriteTimeUtc(path, written);
            return path;
        }

        /// <summary>
        /// The lobby file of <paramref name="port"/> as another process could write it: the
        /// <paramref name="header"/> of a listing, then what <paramref name="write"/> writes.
        /// </summary>
        private string WriteListing(int port, byte[] header, Action<BinaryWriter> write)
        {
            string path = Path.Combine(_folder, "peer-" + port.ToString(CultureInfo.InvariantCulture) + ".lobbies");
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(header);
                write(writer);
            }

            return path;
        }

        /// <summary>
        /// A public lobby of a listing, as <see cref="MachineDirectory.Publish"/> writes one, that
        /// declares <paramref name="declared"/> data entries and holds the first
        /// <paramref name="written"/> of them.
        /// </summary>
        private static void WriteListedLobby(BinaryWriter writer, ulong id, int declared, int written)
        {
            writer.Write(id);
            writer.Write((byte)LobbyVisibility.Public);
            writer.Write(4);
            writer.Write(1);
            writer.Write(true);
            writer.Write(declared);
            for (int i = 0; i < written; i++)
            {
                writer.Write("k" + i.ToString(CultureInfo.InvariantCulture));
                writer.Write(string.Empty);
            }
        }

        /// <summary>One frame of the machine-wide protocol, as another process sends it.</summary>
        private static byte[] Frame(MachineFrameKind kind, ReadOnlySpan<byte> payload)
        {
            byte[] frame = new byte[MachineProtocol.HeaderBytes + 1 + payload.Length];
            BinaryPrimitives.WriteInt32LittleEndian(frame, 1 + payload.Length);
            frame[MachineProtocol.HeaderBytes] = (byte)kind;
            payload.CopyTo(new Span<byte>(frame, MachineProtocol.HeaderBytes + 1, payload.Length));
            return frame;
        }

        /// <summary>What one read of the socket returns: -1 while nothing arrived, 0 once the other side closed it.</summary>
        private static int ReceiveOnce(Socket socket)
        {
            try
            {
                return socket.Poll(0, SelectMode.SelectRead) ? socket.Receive(new byte[4096]) : -1;
            }
            catch (SocketException)
            {
                // Reset: closed all the same.
                return 0;
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
