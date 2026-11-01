using System;
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
    /// packet carries (NFR-5).
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

            // Each string at the limit its packet reads it with, and one byte over it. The limit
            // of a name counts three UTF-8 bytes for each of its characters.
            string name = new string('가', MultiplayerLimits.MaxPlayerNameLength);
            string lobbyValue = new string('v', MultiplayerLimits.MaxLobbyDataSize);
            string participantValue = new string('v', MultiplayerLimits.MaxParticipantDataSize);
            PlayerId late = new PlayerId(hostId.Value + 1000);

            using (LogRecorder log = new LogRecorder())
            {
                fakeHost.Send(connection, WelcomePacket(hostId, a.Id, "FakeHost", "m", lobbyValue + "v"), DeliveryMode.Reliable);
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
                    fakeHost.Send(connection, DataPacket(PacketKind.LobbyDataChanged, "map", lobbyValue + "v"), DeliveryMode.Reliable);
                    fakeHost.Send(connection, ParticipantDataChangedPacket(hostId, "c", participantValue + "v"), DeliveryMode.Reliable);
                    fakeHost.Send(connection, ParticipantJoinedPacket(late, name + "x"), DeliveryMode.Reliable);
                }

                Pump();
                Assert.That(a.Manager.LobbyData.ContainsKey("map"), Is.False);
                Assert.That(DataOf(a, hostId).ContainsKey("c"), Is.False);
                Assert.That(a.Manager.TryGetParticipant(late, out _), Is.False);
                Assert.That(log.Count("Dropped a malformed packet"), Is.EqualTo(4), "once for each kind: Welcome, LobbyDataChanged, ParticipantDataChanged, ParticipantJoined");
            }

            fakeHost.Send(connection, DataPacket(PacketKind.LobbyDataChanged, "map", lobbyValue), DeliveryMode.Reliable);
            fakeHost.Send(connection, ParticipantDataChangedPacket(hostId, "c", participantValue), DeliveryMode.Reliable);
            fakeHost.Send(connection, ParticipantJoinedPacket(late, name), DeliveryMode.Reliable);
            Pump();

            Assert.That(a.Manager.LobbyData["map"], Is.EqualTo(lobbyValue));
            Assert.That(DataOf(a, hostId)["c"], Is.EqualTo(participantValue));
            Assert.That(a.Manager.TryGetParticipant(late, out Participant joined) && joined.Name == name, Is.True);
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
        /// under <paramref name="hostName"/>, and the client.
        /// </summary>
        private static byte[] WelcomePacket(PlayerId host, PlayerId client, string hostName, string lobbyKey, string lobbyValue)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.Welcome);
            writer.WriteByte((byte)SessionState.Lobby);
            writer.WriteULong(host.Value);
            writer.WriteInt(0);

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
            writer.WriteBytes(ReadOnlySpan<byte>.Empty);
            writer.WriteInt(1);
            writer.WriteString(lobbyKey);
            writer.WriteString(lobbyValue);
            writer.WriteInt(2);
            WriteParticipantEntry(writer, host, hostName);
            WriteParticipantEntry(writer, client, "A");
            return writer.Written.ToArray();
        }

        private static byte[] ParticipantJoinedPacket(PlayerId player, string name)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ParticipantJoined);
            WriteParticipantEntry(writer, player, name);
            return writer.Written.ToArray();
        }

        /// <summary>A roster entry: the ID, the name, the ready, late-join, connected, and loaded flags, and no data.</summary>
        private static void WriteParticipantEntry(PacketWriter writer, PlayerId player, string name)
        {
            writer.WriteULong(player.Value);
            writer.WriteString(name);
            writer.WriteBool(false);
            writer.WriteBool(false);
            writer.WriteBool(true);
            writer.WriteBool(false);
            writer.WriteInt(0);
        }
        #endregion
    }
}
