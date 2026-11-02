using System;
using System.Collections.Generic;
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
    /// What a host or a client takes from another player before it decodes anything: the
    /// connections of one account that wait for the welcome, and the length of each string a
    /// packet carries; and what a client takes from a host over many packets: the roster and the
    /// data, each up to its limit (NFR-5).
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        #region Input Limits
        [Test]
        public void ConnectionsWaitingForTheWelcome_AreCappedPerAccount_AndOneMoreClosesAtOnce()
        {
            // NFR-5: an account that opens connection after connection without saying hello
            // keeps only the limit open; another account has a limit of its own.
            const int Limit = MultiplayerLimits.MaxPendingConnectionsPerAccount;
            int timeoutCode = ReasonCodes.ToWire(MultiplayerReason.Timeout);
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            RecordingListener listener = new RecordingListener();
            LocalBackend raw = CreateRawBackend("Raw", listener);
            RecordingListener otherListener = new RecordingListener();
            LocalBackend other = CreateRawBackend("Other", otherListener);

            ConnectionId[] connections;
            using (LogRecorder log = new LogRecorder())
            {
                connections = Enumerable.Range(0, Limit + 3).Select(_ => raw.Connect(host.Id)).ToArray();
                for (int i = 0; i < Limit; i++)
                {
                    other.Connect(host.Id);
                }

                Pump();

                Assert.That(listener.Disconnected.Select(closed => closed.Connection), Is.EquivalentTo(connections.Skip(Limit)));
                Assert.That(listener.Disconnected.All(closed => closed.ReasonCode == timeoutCode), Is.True);
                Assert.That(otherListener.Disconnected, Is.Empty);
                Assert.That(log.Count("Closed a connection from"), Is.EqualTo(1), "the closes after the first go unlogged");
            }

            // A connection the host kept still gets in, and once it is welcomed it no longer
            // waits, so the account may open one more.
            EnterLobby(raw, host);
            raw.Send(connections[0], HelloPacket(isReturning: false), DeliveryMode.Reliable);
            Pump();
            Assert.That(host.Manager.TryGetParticipant(raw.LocalPlayerId, out _), Is.True);

            int closedBefore = listener.Disconnected.Count;
            raw.Connect(host.Id);
            ConnectionId tooMany = raw.Connect(host.Id);
            Pump();
            Assert.That(listener.Disconnected.Skip(closedBefore).Select(closed => closed.Connection), Is.EqualTo(new[] { tooMany }));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Lobby));
        }

        [Test]
        public void AGameIdLongerThanAHelloCarries_IsMalformed_BeforeItIsDecoded()
        {
            // NFR-5: the host reads the game ID with the limit of a game ID, so an oversized
            // hello is dropped as malformed rather than decoded and refused as another game.
            Node host = CreateHost("Host");
            RecordingListener listener = new RecordingListener();
            LocalBackend raw = CreateRawBackend("Raw", listener);
            ConnectionId connection = raw.Connect(host.Id);
            Pump();

            using (LogRecorder log = new LogRecorder())
            {
                raw.Send(connection, HelloWithGameId(new string('g', MultiplayerConfig.MaxGameIdLength * 3 + 1)), DeliveryMode.Reliable);
                Pump();

                Assert.That(log.Count($"exceeds the {MultiplayerConfig.MaxGameIdLength * 3}-byte limit"), Is.EqualTo(1));
            }

            Assert.That(listener.Disconnected, Is.Empty, "dropped, not refused");

            // The longest game ID in characters still fits, and is refused only as another game.
            raw.Send(connection, HelloWithGameId(new string('가', MultiplayerConfig.MaxGameIdLength)), DeliveryMode.Reliable);
            Pump();
            Assert.That(listener.Disconnected.Single().ReasonCode, Is.EqualTo(ReasonCodes.ToWire(MultiplayerReason.VersionMismatch)));
        }

        [Test]
        public void StringsLongerThanTheirPacketCarries_AreMalformedAtAClient_AndTheLongestStillArrive()
        {
            // NFR-5: names, lobby data, and participant data, each up to its own limit, in the
            // welcome and in the packets that change them; each kind is logged once.
            RecordingListener listener = new RecordingListener();
            LocalBackend fakeHost = CreateRawBackend("FakeHost", listener);
            Assert.That(fakeHost.StartListening(), Is.True);
            LobbyId lobby = CreateRawLobby(fakeHost, GameId, 1);
            Node a = CreateNode("A");
            a.Manager.JoinLobby(lobby);
            Pump();
            ConnectionId connection = listener.Connected.Single();
            PlayerId hostId = fakeHost.LocalPlayerId;

            // Each string one byte over the limit its packet reads it with, and the longest that
            // arrives. A value shares its data limit with its key, so the longest one leaves a
            // byte for a one-character key. The limit of a name counts three UTF-8 bytes for each
            // of its characters.
            string name = new string('가', MultiplayerLimits.MaxPlayerNameLength);
            string lobbyValue = new string('v', MultiplayerLimits.MaxLobbyDataSize - 1);
            string participantValue = new string('v', MultiplayerLimits.MaxParticipantDataSize - 1);
            string tooLongLobbyValue = new string('v', MultiplayerLimits.MaxLobbyDataSize + 1);
            string tooLongParticipantValue = new string('v', MultiplayerLimits.MaxParticipantDataSize + 1);
            PlayerId late = new PlayerId(hostId.Value + 1000);

            using (LogRecorder log = new LogRecorder())
            {
                fakeHost.Send(connection, WelcomePacket(hostId, a.Id, "FakeHost", "m", tooLongLobbyValue), DeliveryMode.Reliable);
                fakeHost.Send(connection, WelcomePacket(hostId, a.Id, name + "x", "m", "forest"), DeliveryMode.Reliable);
                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.Joining), "both welcomes are dropped");

                fakeHost.Send(connection, WelcomePacket(hostId, a.Id, name, "m", lobbyValue), DeliveryMode.Reliable);
                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));
                Assert.That(a.Manager.LobbyData["m"], Is.EqualTo(lobbyValue));
                Assert.That(a.Manager.TryGetParticipant(hostId, out Participant hostSeen) && hostSeen.Name == name, Is.True);

                for (int i = 0; i < 2; i++)
                {
                    fakeHost.Send(connection, DataPacket(PacketKind.LobbyDataChanged, "map", tooLongLobbyValue), DeliveryMode.Reliable);
                    fakeHost.Send(connection, ParticipantDataChangedPacket(hostId, "c", tooLongParticipantValue), DeliveryMode.Reliable);
                    fakeHost.Send(connection, ParticipantJoinedPacket(late, name + "x"), DeliveryMode.Reliable);
                }

                Pump();
                Assert.That(a.Manager.LobbyData.ContainsKey("map"), Is.False);
                Assert.That(DataOf(a, hostId).ContainsKey("c"), Is.False);
                Assert.That(a.Manager.TryGetParticipant(late, out _), Is.False);
                Assert.That(log.Count("Dropped a malformed packet"), Is.EqualTo(4), "once for each kind: Welcome, LobbyDataChanged, ParticipantDataChanged, ParticipantJoined");
                Assert.That(log.Count("-byte data limit"), Is.Zero, "each string is dropped for its own length");
            }

            string otherLobbyValue = new string('w', MultiplayerLimits.MaxLobbyDataSize - 1);
            fakeHost.Send(connection, DataPacket(PacketKind.LobbyDataChanged, "m", otherLobbyValue), DeliveryMode.Reliable);
            fakeHost.Send(connection, ParticipantDataChangedPacket(hostId, "c", participantValue), DeliveryMode.Reliable);
            fakeHost.Send(connection, ParticipantJoinedPacket(late, name), DeliveryMode.Reliable);
            Pump();

            Assert.That(a.Manager.LobbyData["m"], Is.EqualTo(otherLobbyValue));
            Assert.That(DataOf(a, hostId)["c"], Is.EqualTo(participantValue));
            Assert.That(a.Manager.TryGetParticipant(late, out Participant joined) && joined.Name == name, Is.True);
        }

        [Test]
        public void ARosterLargerThanASessionHolds_IsMalformedAtAClient_AndAFullOneStillArrives()
        {
            // NFR-5: a host admits at most MaxCapacity players, so a client drops a welcome with a
            // larger roster, and a join to a full roster whatever ID it names, as malformed;
            // without that a host could grow the roster without bound. Once a player has left,
            // the next join arrives. Each kind is logged once.
            RecordingListener listener = new RecordingListener();
            LocalBackend fakeHost = CreateRawBackend("FakeHost", listener);
            Assert.That(fakeHost.StartListening(), Is.True);
            LobbyId lobby = CreateRawLobby(fakeHost, GameId, 1);
            Node a = CreateNode("A");
            a.Manager.JoinLobby(lobby);
            Pump();
            ConnectionId connection = listener.Connected.Single();
            PlayerId hostId = fakeHost.LocalPlayerId;

            // With the host and A, one more than a session holds.
            PlayerId[] others = Enumerable.Range(1, MultiplayerLimits.MaxCapacity - 1)
                .Select(i => new PlayerId(hostId.Value + 1000 + (ulong)i))
                .ToArray();
            PlayerId[] fullRoster = others.Take(MultiplayerLimits.MaxCapacity - 2).ToArray();
            PlayerId late = others[others.Length - 1];

            using (LogRecorder log = new LogRecorder())
            {
                fakeHost.Send(connection, WelcomeWithDataPacket(hostId, a.Id, NoEntries, NoEntries, others), DeliveryMode.Reliable);
                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.Joining), "the welcome is dropped");
                Assert.That(log.Count($"Invalid participant count {MultiplayerLimits.MaxCapacity + 1} in the welcome"), Is.EqualTo(1));
            }

            fakeHost.Send(connection, WelcomeWithDataPacket(hostId, a.Id, NoEntries, NoEntries, fullRoster), DeliveryMode.Reliable);
            Pump();
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));
            Assert.That(a.Manager.Participants.Count, Is.EqualTo(MultiplayerLimits.MaxCapacity));
            a.Joined.Clear();

            using (LogRecorder log = new LogRecorder())
            {
                for (ulong i = 0; i < 3; i++)
                {
                    fakeHost.Send(connection, ParticipantJoinedPacket(new PlayerId(late.Value + i), "Late"), DeliveryMode.Reliable);
                }

                Pump();
                Assert.That(a.Manager.Participants.Count, Is.EqualTo(MultiplayerLimits.MaxCapacity));
                Assert.That(a.Manager.TryGetParticipant(late, out _), Is.False);
                Assert.That(a.Joined, Is.Empty);
                Assert.That(log.Count("Dropped a malformed packet"), Is.EqualTo(1), "the first join; the others go unlogged");
                Assert.That(log.Count($"already holds {MultiplayerLimits.MaxCapacity} players"), Is.EqualTo(1));
            }

            fakeHost.Send(connection, ParticipantLeftPacket(fullRoster[0]), DeliveryMode.Reliable);
            fakeHost.Send(connection, ParticipantJoinedPacket(late, "Late"), DeliveryMode.Reliable);
            Pump();
            Assert.That(a.Manager.Participants.Count, Is.EqualTo(MultiplayerLimits.MaxCapacity));
            Assert.That(a.Joined.Select(participant => participant.Id), Is.EqualTo(new[] { late }));
        }

        [Test]
        public void DataLargerInAllThanItsLimit_IsMalformedAtAClient_AndDataAtTheLimitStillArrives()
        {
            // NFR-5: a host holds the keys and values of its lobby data, and of each player's
            // data, to their limits in UTF-8 bytes, so a client drops a welcome, a join, or a
            // change that brings more as malformed, though each value alone fits. A change counts
            // against the data the client holds, without the entry it replaces, as the host
            // counts it; without that a host could grow the data without bound under ever new
            // keys. Data at its limit still arrives. Each kind is logged once.
            RecordingListener listener = new RecordingListener();
            LocalBackend fakeHost = CreateRawBackend("FakeHost", listener);
            Assert.That(fakeHost.StartListening(), Is.True);
            LobbyId lobby = CreateRawLobby(fakeHost, GameId, 1);
            Node a = CreateNode("A");
            a.Manager.JoinLobby(lobby);
            Pump();
            ConnectionId connection = listener.Connected.Single();
            PlayerId hostId = fakeHost.LocalPlayerId;
            PlayerId late = new PlayerId(hostId.Value + 1000);

            const int LobbyLimit = MultiplayerLimits.MaxLobbyDataSize;
            const int PlayerLimit = MultiplayerLimits.MaxParticipantDataSize;
            (string Key, string Value)[] fullLobby = TwoEntriesOfSize(LobbyLimit);
            (string Key, string Value)[] fullPlayer = TwoEntriesOfSize(PlayerLimit);

            using (LogRecorder log = new LogRecorder())
            {
                fakeHost.Send(connection, WelcomeWithDataPacket(hostId, a.Id, TwoEntriesOfSize(LobbyLimit + 1), NoEntries), DeliveryMode.Reliable);
                fakeHost.Send(connection, WelcomeWithDataPacket(hostId, a.Id, fullLobby, TwoEntriesOfSize(PlayerLimit + 1)), DeliveryMode.Reliable);
                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.Joining), "both welcomes are dropped");

                fakeHost.Send(connection, WelcomeWithDataPacket(hostId, a.Id, fullLobby, fullPlayer), DeliveryMode.Reliable);
                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));
                Assert.That(a.Manager.LobbyData, Is.EquivalentTo(DataFrom(fullLobby)));
                Assert.That(DataOf(a, hostId), Is.EquivalentTo(DataFrom(fullPlayer)));

                // A new key, even with an empty value, and a value one byte longer than the one it replaces.
                string longerLobbyValue = fullLobby[0].Value + "v";
                string longerPlayerValue = fullPlayer[0].Value + "v";
                for (int i = 0; i < 2; i++)
                {
                    fakeHost.Send(connection, ParticipantJoinedWithDataPacket(late, 2, TwoEntriesOfSize(PlayerLimit + 1)), DeliveryMode.Reliable);
                    fakeHost.Send(connection, DataPacket(PacketKind.LobbyDataChanged, "c", string.Empty), DeliveryMode.Reliable);
                    fakeHost.Send(connection, DataPacket(PacketKind.LobbyDataChanged, fullLobby[0].Key, longerLobbyValue), DeliveryMode.Reliable);
                    fakeHost.Send(connection, ParticipantDataChangedPacket(hostId, "c", string.Empty), DeliveryMode.Reliable);
                    fakeHost.Send(connection, ParticipantDataChangedPacket(hostId, fullPlayer[0].Key, longerPlayerValue), DeliveryMode.Reliable);
                }

                Pump();
                Assert.That(a.Manager.TryGetParticipant(late, out _), Is.False);
                Assert.That(a.Manager.LobbyData, Is.EquivalentTo(DataFrom(fullLobby)));
                Assert.That(DataOf(a, hostId), Is.EquivalentTo(DataFrom(fullPlayer)));
                Assert.That(a.LobbyDataChanges, Is.Empty);
                Assert.That(a.DataChanges, Is.Empty);
                Assert.That(log.Count("Dropped a malformed packet"), Is.EqualTo(4), "once for each kind: Welcome, ParticipantJoined, LobbyDataChanged, ParticipantDataChanged");
                Assert.That(log.Count($"exceed the {LobbyLimit}-byte data limit"), Is.EqualTo(2), "Welcome, LobbyDataChanged");
                Assert.That(log.Count($"exceed the {PlayerLimit}-byte data limit"), Is.EqualTo(2), "ParticipantJoined, ParticipantDataChanged");
            }

            // A value as long as the one it replaces, a join with data at the limit, and a new key
            // in the room a deleted one left.
            string otherLobbyValue = new string('w', fullLobby[0].Value.Length);
            string otherPlayerValue = new string('w', fullPlayer[0].Value.Length);
            string newLobbyValue = new string('n', fullLobby[1].Value.Length);
            fakeHost.Send(connection, DataPacket(PacketKind.LobbyDataChanged, fullLobby[0].Key, otherLobbyValue), DeliveryMode.Reliable);
            fakeHost.Send(connection, ParticipantDataChangedPacket(hostId, fullPlayer[0].Key, otherPlayerValue), DeliveryMode.Reliable);
            fakeHost.Send(connection, ParticipantJoinedWithDataPacket(late, 2, fullPlayer), DeliveryMode.Reliable);
            fakeHost.Send(connection, KeyPacket(PacketKind.LobbyDataDeleted, fullLobby[1].Key), DeliveryMode.Reliable);
            fakeHost.Send(connection, DataPacket(PacketKind.LobbyDataChanged, "c", newLobbyValue), DeliveryMode.Reliable);
            Pump();

            Assert.That(a.Manager.LobbyData[fullLobby[0].Key], Is.EqualTo(otherLobbyValue));
            Assert.That(a.Manager.LobbyData.ContainsKey(fullLobby[1].Key), Is.False);
            Assert.That(a.Manager.LobbyData["c"], Is.EqualTo(newLobbyValue));
            Assert.That(DataOf(a, hostId)[fullPlayer[0].Key], Is.EqualTo(otherPlayerValue));
            Assert.That(DataOf(a, late), Is.EquivalentTo(DataFrom(fullPlayer)));
        }

        [Test]
        public void DeletionsAndPingReports_PastTheirRules_AreMalformedAtAClient_AndChangeNothing()
        {
            // NFR-5: the deletions and ping reports a host sends are read as carefully as its other
            // packets: a key past its limit or one of the library's, a report of more players than
            // a session holds, or one shorter than its count says. Each kind is logged once.
            RecordingListener listener = new RecordingListener();
            LocalBackend fakeHost = CreateRawBackend("FakeHost", listener);
            Assert.That(fakeHost.StartListening(), Is.True);
            LobbyId lobby = CreateRawLobby(fakeHost, GameId, 1);
            Node a = CreateNode("A");
            a.Manager.JoinLobby(lobby);
            Pump();
            ConnectionId connection = listener.Connected.Single();
            PlayerId hostId = fakeHost.LocalPlayerId;

            fakeHost.Send(connection, WelcomePacket(hostId, a.Id, "FakeHost", "map", "forest"), DeliveryMode.Reliable);
            fakeHost.Send(connection, ParticipantDataChangedPacket(hostId, "c", "knight"), DeliveryMode.Reliable);
            fakeHost.Send(connection, PingsPacket(1, (a.Id, 50)), DeliveryMode.Reliable);
            Pump();
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));
            Assert.That(a.Manager.TryGetParticipantPing(a.Id, out int first) && first == 50, Is.True);

            string longKey = new string('k', MultiplayerLimits.MaxDataKeyLength * 3 + 1);
            (PlayerId, ushort)[] tooMany = Enumerable.Range(1, MultiplayerLimits.MaxCapacity + 1)
                .Select(i => (new PlayerId(hostId.Value + (ulong)i), (ushort)1))
                .ToArray();
            using (LogRecorder log = new LogRecorder())
            {
                for (int i = 0; i < 2; i++)
                {
                    fakeHost.Send(connection, KeyPacket(PacketKind.LobbyDataDeleted, longKey), DeliveryMode.Reliable);
                    fakeHost.Send(connection, KeyPacket(PacketKind.LobbyDataDeleted, LobbyDataKeys.State), DeliveryMode.Reliable);
                    fakeHost.Send(connection, ParticipantDataDeletedPacket(hostId, longKey), DeliveryMode.Reliable);
                    fakeHost.Send(connection, ParticipantDataDeletedPacket(hostId, "MU3.ready"), DeliveryMode.Reliable);
                    fakeHost.Send(connection, PingsPacket(tooMany.Length, tooMany), DeliveryMode.Reliable);
                    fakeHost.Send(connection, PingsPacket(2, (a.Id, 70)), DeliveryMode.Reliable);
                }

                Pump();
                Assert.That(a.Manager.LobbyData["map"], Is.EqualTo("forest"));
                Assert.That(DataOf(a, hostId)["c"], Is.EqualTo("knight"));
                Assert.That(a.LobbyDataChanges, Is.Empty);
                Assert.That(a.DataChanges.Count, Is.EqualTo(1));
                Assert.That(a.Manager.TryGetParticipantPing(a.Id, out int kept) && kept == 50, Is.True, "a malformed report leaves the last one");
                Assert.That(log.Count("Dropped a malformed packet"), Is.EqualTo(3), "once for each kind: LobbyDataDeleted, ParticipantDataDeleted, ParticipantPings");
            }

            // A report that names a player outside the session is taken, but tells nothing of that player.
            PlayerId stranger = new PlayerId(hostId.Value + 1000);
            fakeHost.Send(connection, PingsPacket(2, (a.Id, 70), (stranger, 5)), DeliveryMode.Reliable);
            Pump();
            Assert.That(a.Manager.TryGetParticipantPing(a.Id, out int next) && next == 70, Is.True);
            Assert.That(a.Manager.TryGetParticipantPing(stranger, out _), Is.False);

            // Within their rules the same packets arrive.
            fakeHost.Send(connection, KeyPacket(PacketKind.LobbyDataDeleted, "map"), DeliveryMode.Reliable);
            fakeHost.Send(connection, ParticipantDataDeletedPacket(hostId, "c"), DeliveryMode.Reliable);
            Pump();
            Assert.That(a.Manager.LobbyData, Is.Empty);
            Assert.That(DataOf(a, hostId), Is.Empty);
        }

        [Test]
        public void ADataEntryCountThePacketCannotHold_IsMalformedAtAClient_BeforeAnythingIsReservedForIt()
        {
            // NFR-5: every entry of lobby or participant data takes at least its two length
            // prefixes, so a count larger than the rest of the packet can hold is malformed before
            // the client reserves room for it. Room for the most entries of participant data
            // takes about 57 KB, so a flood of 21-byte packets that claim them would cost that
            // much each. Each kind is logged once.
            RecordingListener listener = new RecordingListener();
            LocalBackend fakeHost = CreateRawBackend("FakeHost", listener);
            Assert.That(fakeHost.StartListening(), Is.True);
            LobbyId lobby = CreateRawLobby(fakeHost, GameId, 1);
            Node a = CreateNode("A");
            a.Manager.JoinLobby(lobby);
            Pump();
            ConnectionId connection = listener.Connected.Single();
            PlayerId hostId = fakeHost.LocalPlayerId;
            PlayerId late = new PlayerId(hostId.Value + 1000);

            const int Floods = 64;
            byte[] shortJoin = ParticipantJoinedWithDataPacket(late, MultiplayerLimits.MaxParticipantDataSize);
            Assert.That(shortJoin.Length, Is.EqualTo(21));

            using (LogRecorder log = new LogRecorder())
            {
                fakeHost.Send(connection, WelcomePacket(hostId, a.Id, "FakeHost", "m", "forest", lobbyDataCount: MultiplayerLimits.MaxLobbyDataSize), DeliveryMode.Reliable);
                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.Joining), "the welcome is dropped");

                fakeHost.Send(connection, WelcomePacket(hostId, a.Id, "FakeHost", "m", "forest"), DeliveryMode.Reliable);
                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));

                // The runtime counts no bytes per thread, so the growth of the managed heap stands
                // in: a collection may run meanwhile and hide part of what was reserved, which only
                // makes the bound easier to meet. Room reserved for the entries of even 5 of the
                // packets would pass it.
                long before = GC.GetTotalMemory(forceFullCollection: true);
                for (int i = 0; i < Floods; i++)
                {
                    fakeHost.Send(connection, shortJoin, DeliveryMode.Reliable);
                }

                Pump();
                long allocated = GC.GetTotalMemory(forceFullCollection: false) - before;
                TestContext.WriteLine($"{Floods} ParticipantJoined packets of {shortJoin.Length} bytes that claim {MultiplayerLimits.MaxParticipantDataSize} data entries each took {allocated} bytes of the managed heap.");
                Assert.That(allocated, Is.LessThan(Floods * 4 * 1024), "no packet reserves room for the entries it claims");

                Assert.That(a.Manager.TryGetParticipant(late, out _), Is.False);
                Assert.That(log.Count($"Invalid data entry count {MultiplayerLimits.MaxLobbyDataSize} with"), Is.EqualTo(1), "the welcome");
                Assert.That(log.Count($"Invalid data entry count {MultiplayerLimits.MaxParticipantDataSize} with 0 bytes left"), Is.EqualTo(1), "the first ParticipantJoined; the others go unlogged");
            }

            // As many entries as the rest of the packet holds still arrive.
            fakeHost.Send(connection, ParticipantJoinedWithDataPacket(late, 1, ("c", string.Empty)), DeliveryMode.Reliable);
            Pump();
            Assert.That(DataOf(a, late)["c"], Is.EqualTo(string.Empty));
        }

        [Test]
        public void StartDataLargerThanAHostMayStartWith_IsMalformedAtAClient_AndTheLargestStillArrives()
        {
            // NFR-5: a host never starts a game with more than the start data limit, so a client
            // reads the start data of a welcome into a running game and of a game start with that
            // limit and drops a larger one before it copies it. Each kind is logged once.
            RecordingListener listener = new RecordingListener();
            LocalBackend fakeHost = CreateRawBackend("FakeHost", listener);
            Assert.That(fakeHost.StartListening(), Is.True);
            LobbyId lobby = CreateRawLobby(fakeHost, GameId, 1);
            Node a = CreateNode("A");
            a.Manager.JoinLobby(lobby);
            Pump();
            ConnectionId connection = listener.Connected.Single();
            PlayerId hostId = fakeHost.LocalPlayerId;

            byte[] largest = new byte[MultiplayerLimits.MaxStartDataSize];
            largest[largest.Length - 1] = 7;
            byte[] tooLarge = new byte[MultiplayerLimits.MaxStartDataSize + 1];
            string overLimit = $"exceeds the {MultiplayerLimits.MaxStartDataSize}-byte limit";

            using (LogRecorder log = new LogRecorder())
            {
                for (int i = 0; i < 2; i++)
                {
                    fakeHost.Send(connection, WelcomePacket(hostId, a.Id, "FakeHost", "m", "forest", startData: tooLarge), DeliveryMode.Reliable);
                }

                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.Joining), "both welcomes are dropped");
                Assert.That(log.Count(overLimit), Is.EqualTo(1));
            }

            fakeHost.Send(connection, WelcomePacket(hostId, a.Id, "FakeHost", "m", "forest", startData: largest), DeliveryMode.Reliable);
            Pump();
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(a.GameStarts.Single(), Is.EqualTo(largest));

            fakeHost.Send(connection, new[] { (byte)PacketKind.ReturnedToLobby }, DeliveryMode.Reliable);
            Pump();
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));

            using (LogRecorder log = new LogRecorder())
            {
                for (int i = 0; i < 2; i++)
                {
                    fakeHost.Send(connection, GameStartedPacket(2, tooLarge), DeliveryMode.Reliable);
                }

                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby), "both game starts are dropped");
                Assert.That(log.Count(overLimit), Is.EqualTo(1));
            }

            fakeHost.Send(connection, GameStartedPacket(2, largest), DeliveryMode.Reliable);
            Pump();
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(a.GameStarts.Count, Is.EqualTo(2));
            Assert.That(a.GameStarts[1], Is.EqualTo(largest));
            Assert.That(a.Manager.StartData.ToArray(), Is.EqualTo(largest));
        }
        #endregion

        #region Input Limit Helpers
        private static byte[] HelloWithGameId(string gameId)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.Hello);
            writer.WriteInt(WireProtocol.Version);
            writer.WriteString(gameId);
            writer.WriteInt(1);
            writer.WriteBool(false);
            return writer.Written.ToArray();
        }

        /// <summary>
        /// The welcome into a lobby with the default rules, one entry of lobby data, the host
        /// under <paramref name="hostName"/>, and the client. A <paramref name="lobbyDataCount"/>
        /// other than 1 says that many entries, whatever follows, and <paramref name="startData"/>
        /// makes it a welcome into a running game with that start data.
        /// </summary>
        private static byte[] WelcomePacket(PlayerId host, PlayerId client, string hostName, string lobbyKey, string lobbyValue,
            int lobbyDataCount = 1, byte[] startData = null)
        {
            PacketWriter writer = new PacketWriter();
            WriteWelcomeStart(writer, host, startData);
            writer.WriteInt(lobbyDataCount);
            writer.WriteString(lobbyKey);
            writer.WriteString(lobbyValue);
            writer.WriteInt(2);
            WriteParticipantEntry(writer, host, hostName);
            WriteParticipantEntry(writer, client, "A");
            return writer.Written.ToArray();
        }

        /// <summary>
        /// The welcome into a lobby with the default rules and <paramref name="lobbyData"/>, and a
        /// roster of the host with <paramref name="hostData"/>, the client, and <paramref name="others"/>.
        /// </summary>
        private static byte[] WelcomeWithDataPacket(PlayerId host, PlayerId client, (string Key, string Value)[] lobbyData,
            (string Key, string Value)[] hostData, params PlayerId[] others)
        {
            PacketWriter writer = new PacketWriter();
            WriteWelcomeStart(writer, host, startData: null);
            writer.WriteInt(lobbyData.Length);
            foreach ((string key, string value) in lobbyData)
            {
                writer.WriteString(key);
                writer.WriteString(value);
            }

            writer.WriteInt(2 + others.Length);
            WriteParticipantEntry(writer, host, "FakeHost", hostData);
            WriteParticipantEntry(writer, client, "A");
            foreach (PlayerId other in others)
            {
                WriteParticipantEntry(writer, other, "Other");
            }

            return writer.Written.ToArray();
        }

        /// <summary>
        /// What a welcome holds before its lobby data: the state, the host, the game number, the
        /// default rules, the message size limit, and the start data, which makes it a welcome
        /// into a running game.
        /// </summary>
        private static void WriteWelcomeStart(PacketWriter writer, PlayerId host, byte[] startData)
        {
            writer.Begin(PacketKind.Welcome);
            writer.WriteByte((byte)(startData != null ? SessionState.InGame : SessionState.Lobby));
            writer.WriteULong(host.Value);
            writer.WriteInt(startData != null ? 1 : 0);

            // The rules: capacity, minimum players, ready check, late join, both rate limits, the
            // kick time, the reservation time, and the load timeout.
            writer.WriteInt(4);
            writer.WriteInt(1);
            writer.WriteBool(false);
            writer.WriteBool(false);
            writer.WriteInt(0);
            writer.WriteInt(0);
            writer.WriteInt(BitConverter.SingleToInt32Bits(0f));
            writer.WriteInt(BitConverter.SingleToInt32Bits(SessionRules.DefaultReconnectReservationSeconds));
            writer.WriteInt(BitConverter.SingleToInt32Bits(SessionRules.DefaultLoadTimeoutSeconds));

            writer.WriteInt(MultiplayerConfig.DefaultMaxMessageSize);
            writer.WriteBytes(startData);
        }

        private static readonly (string Key, string Value)[] NoEntries = Array.Empty<(string, string)>();

        /// <summary>Two entries of data, under the keys <c>a</c> and <c>b</c>, whose keys and values take exactly <paramref name="size"/> UTF-8 bytes.</summary>
        private static (string Key, string Value)[] TwoEntriesOfSize(int size)
        {
            int first = size / 2;
            return new[] { ("a", new string('v', first - 1)), ("b", new string('v', size - first - 1)) };
        }

        private static Dictionary<string, string> DataFrom((string Key, string Value)[] entries)
        {
            return entries.ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        private static byte[] ParticipantLeftPacket(PlayerId player)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ParticipantLeft);
            writer.WriteULong(player.Value);
            writer.WriteInt((int)MultiplayerReason.LeftNormally);
            return writer.Written.ToArray();
        }

        /// <summary>A ping report whose count byte says <paramref name="count"/>, whatever entries follow.</summary>
        private static byte[] PingsPacket(int count, params (PlayerId Player, ushort Ping)[] entries)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ParticipantPings);
            writer.WriteByte((byte)count);
            foreach ((PlayerId player, ushort ping) in entries)
            {
                writer.WriteULong(player.Value);
                writer.WriteUShort(ping);
            }

            return writer.Written.ToArray();
        }

        private static byte[] ParticipantJoinedPacket(PlayerId player, string name)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ParticipantJoined);
            WriteParticipantEntry(writer, player, name);
            return writer.Written.ToArray();
        }

        /// <summary>
        /// A ParticipantJoined of a player with an empty name whose data count says
        /// <paramref name="count"/>, whatever entries follow.
        /// </summary>
        private static byte[] ParticipantJoinedWithDataPacket(PlayerId player, int count, params (string Key, string Value)[] entries)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ParticipantJoined);
            writer.WriteULong(player.Value);
            writer.WriteString(string.Empty);
            writer.WriteBool(false);
            writer.WriteBool(false);
            writer.WriteBool(true);
            writer.WriteBool(false);
            writer.WriteInt(count);
            foreach ((string key, string value) in entries)
            {
                writer.WriteString(key);
                writer.WriteString(value);
            }

            return writer.Written.ToArray();
        }

        private static byte[] GameStartedPacket(int gameNumber, byte[] startData)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.GameStarted);
            writer.WriteInt(gameNumber);
            writer.WriteBytes(startData);
            return writer.Written.ToArray();
        }

        /// <summary>A roster entry: the ID, the name, the ready, late-join, connected, and loaded flags, and the data.</summary>
        private static void WriteParticipantEntry(PacketWriter writer, PlayerId player, string name, params (string Key, string Value)[] data)
        {
            writer.WriteULong(player.Value);
            writer.WriteString(name);
            writer.WriteBool(false);
            writer.WriteBool(false);
            writer.WriteBool(true);
            writer.WriteBool(false);
            writer.WriteInt(data.Length);
            foreach ((string key, string value) in data)
            {
                writer.WriteString(key);
                writer.WriteString(value);
            }
        }
        #endregion
    }
}
