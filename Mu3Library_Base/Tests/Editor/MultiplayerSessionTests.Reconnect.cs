using System.Collections.Generic;
using System.Linq;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Protocol;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Slot reservation and reconnection after a connection drops during a game (FR-20, FR-21,
    /// FR-9, FR-14, FR-17, FR-22, NFR-10), with the local network's simulated outage. The network
    /// and the managers run on the test's clock, so an outage of 20 seconds takes no real time.
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        // LocalNetwork.ConnectionTimeoutSeconds by default: how long an outage takes to drop a connection.
        private const double DropSeconds = 10.0;

        #region Reconnect
        [Test]
        public void Reconnect_After20SecondsAway_TheSameSlotComesBack_AndEveryPlayerHearsOfIt()
        {
            // FR-20, FR-9
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            Node[] everyone = { host, a, b };
            Assert.That(b.Manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
            Assert.That(b.Manager.SetReady(true).IsSuccess, Is.True);
            Pump();
            Assert.That(host.Manager.StartGame(new byte[] { 7 }).IsSuccess, Is.True);
            Pump();
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, everyone);
            Assert.That(b.Manager.Send(Chat, new byte[] { 9 }, MessageTarget.Host).IsSuccess, Is.True);
            Pump();

            b.Backend.SimulateOutage(20f);
            Advance(DropSeconds + 0.5);

            Assert.That(b.Manager.State, Is.EqualTo(SessionState.Reconnecting));
            Assert.That(b.LastChange.ToString(), Is.EqualTo("InGame -> Reconnecting (ConnectionLost)"));
            Assert.That(b.Manager.TryGetParticipant(b.Id, out Participant self) && !self.IsConnected, Is.True);
            foreach (Node other in new[] { host, a })
            {
                Assert.That(other.Disconnected.Select(participant => participant.Id), Is.EqualTo(new[] { b.Id }), other.Backend.PlayerName);
                Assert.That(other.Manager.TryGetParticipant(b.Id, out Participant away) && !away.IsConnected, Is.True, other.Backend.PlayerName);
                Assert.That(other.Manager.Participants.Count, Is.EqualTo(3), other.Backend.PlayerName);
            }

            Assert.That(host.Left.Concat(a.Left), Is.Empty);
            Assert.That(b.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host).Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            Assert.That(b.Manager.SetParticipantData("character", "mage").Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            Assert.That(host.Manager.Send(Chat, new byte[] { 2 }, MessageTarget.All).IsSuccess, Is.True);
            Assert.That(host.Manager.Send(Chat, new byte[] { 3 }, MessageTarget.To(b.Id)).IsSuccess, Is.True, "a message for a player who is away goes nowhere");

            Advance(11.0);

            Assert.That(b.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(b.LastChange.ToString(), Is.EqualTo("Reconnecting -> InGame (None)"));
            foreach (Node node in everyone)
            {
                Assert.That(node.Reconnected.Select(participant => participant.Id), Is.EqualTo(new[] { b.Id }), node.Backend.PlayerName);
                Assert.That(node.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id, a.Id, b.Id }), node.Backend.PlayerName);
                Assert.That(node.Manager.Participants.All(participant => participant.IsConnected), Is.True, node.Backend.PlayerName);
                Assert.That(DataOf(node, b.Id)["character"], Is.EqualTo("knight"), node.Backend.PlayerName);
                Assert.That(node.Manager.TryGetParticipant(b.Id, out Participant back) && back.IsReady, Is.True, node.Backend.PlayerName);
            }

            Assert.That(b.GameStarts.Count, Is.EqualTo(1), "the same game goes on");
            Assert.That(b.Manager.StartData.ToArray(), Is.EqualTo(new byte[] { 7 }));
            Assert.That(inboxes[b].Received, Is.Empty, "what was sent meanwhile does not reach the player who was away");
            Assert.That(inboxes[a].Received.Count, Is.EqualTo(1));
            Assert.That(host.Left.Concat(a.Left).Concat(b.Left), Is.Empty);

            Assert.That(b.Manager.Send(Chat, new byte[] { 4 }, MessageTarget.All).IsSuccess, Is.True);
            Assert.That(b.Manager.SetParticipantData("character", "mage").IsSuccess, Is.True);
            Pump();
            Assert.That(inboxes[host].Received.Last().Payload, Is.EqualTo(new byte[] { 4 }));
            Assert.That(inboxes[a].Received.Last().Payload, Is.EqualTo(new byte[] { 4 }));
            Assert.That(DataOf(host, b.Id)["character"], Is.EqualTo("mage"));

            // The counts go on through the time away; the send refused meanwhile does not count.
            List<MessageCounts> counts = new List<MessageCounts>();
            b.Manager.GetMessageCounts(counts);
            Assert.That(counts.Single(entry => entry.MessageType == Chat).Sent, Is.EqualTo(2));
        }

        [Test]
        public void Reconnect_NotBackWithinTheReservation_FreesTheSlotWithConnectionLost()
        {
            // FR-20
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            host.Manager.StartGame();
            Pump();

            b.Backend.SimulateOutage(120f);
            Advance(DropSeconds + 0.5);
            Assert.That(host.Disconnected.Select(participant => participant.Id), Is.EqualTo(new[] { b.Id }));

            Advance(SessionRules.DefaultReconnectReservationSeconds - 1.0);
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(3), "the slot is kept for 60 seconds");
            Assert.That(b.Manager.State, Is.EqualTo(SessionState.Reconnecting));

            Advance(1.0);
            Assert.That(b.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(b.LastChange.Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            foreach (Node remaining in new[] { host, a })
            {
                Assert.That(remaining.Left.Single().Participant.Id, Is.EqualTo(b.Id), remaining.Backend.PlayerName);
                Assert.That(remaining.Left.Single().Reason, Is.EqualTo(MultiplayerReason.ConnectionLost), remaining.Backend.PlayerName);
                Assert.That(remaining.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id, a.Id }), remaining.Backend.PlayerName);
                Assert.That(remaining.Reconnected, Is.Empty, remaining.Backend.PlayerName);
            }
        }

        [Test]
        public void Reconnect_AKeptSlot_IsNoFreeSlotForAnotherAccount_AndCountsTowardTheCapacity()
        {
            // FR-20, FR-16, FR-17: the slot outlasts the game, so the lobby counts it as well.
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(3, requireReady: false, allowLateJoin: true));
            JoinNew("A", host);
            Node b = JoinNew("B", host);
            host.Manager.StartGame();
            Pump();

            b.Backend.SimulateOutage(120f);
            Advance(DropSeconds + 0.5);

            // The lobby still lists the player who is away; get past its count to reach the host's.
            _network.IgnoreLobbyCapacity = true;
            Node other = CreateNode("Other");
            Assert.That(Join(other, host).Reason, Is.EqualTo(MultiplayerReason.SessionFull));

            Assert.That(host.Manager.EndGame().IsSuccess, Is.True);
            Pump();
            Assert.That(host.Manager.TryGetParticipant(b.Id, out Participant away) && !away.IsConnected, Is.True);
            Assert.That(host.Manager.UpdateRules(host.Manager.Rules.WithCapacity(2)).Reason, Is.EqualTo(MultiplayerReason.InvalidCapacity));
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True, "without the ready check, a player who is away does not hold up the start");
        }

        [Test]
        public void Reconnect_TheReservedAccountJoiningAgain_ComesBackToItsSlotInsteadOfBeingADuplicate()
        {
            // FR-21: the same account on another start of the game, as after a restart.
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false, allowLateJoin: true));
            Node a = JoinNew("A", host);
            host.Manager.StartGame(new byte[] { 5 });
            Pump();
            Node b = JoinNew("B", host);
            Assert.That(b.Manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
            Pump();

            b.Backend.SimulateOutage(120f);
            Advance(DropSeconds + 0.5);

            Node again = CreateNode("B", id: b.Id);
            Assert.That(Join(again, host).IsSuccess, Is.True);

            Assert.That(again.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(again.GameStarts.Single(), Is.EqualTo(new byte[] { 5 }));
            Assert.That(again.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id, a.Id, b.Id }));
            Assert.That(DataOf(again, b.Id)["character"], Is.EqualTo("knight"));
            foreach (Node node in new[] { host, a })
            {
                Assert.That(node.Reconnected.Select(participant => participant.Id), Is.EqualTo(new[] { b.Id }), node.Backend.PlayerName);
                Assert.That(node.Joined.Count(participant => participant.Id == b.Id), Is.LessThanOrEqualTo(1), node.Backend.PlayerName);
                Assert.That(node.Manager.TryGetParticipant(b.Id, out Participant back) && back.IsConnected, Is.True, node.Backend.PlayerName);
            }

            foreach (Node node in new[] { host, a, again })
            {
                Assert.That(node.Manager.TryGetParticipant(b.Id, out Participant late) && late.IsLateJoiner, Is.True, $"still a late joiner at {node.Backend.PlayerName}");
            }

            Assert.That(host.Manager.Participants.Count, Is.EqualTo(3));
            Assert.That(host.Left, Is.Empty);
        }

        [Test]
        public void Reconnect_LeavingOrBeingKicked_NeverKeepsTheSlot_AndAKickWhileAwayBansTheReturn()
        {
            // FR-14, FR-22
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            Node c = JoinNew("C", host);
            host.Manager.StartGame();
            Pump();

            a.Manager.Leave();
            Pump();
            Assert.That(host.Manager.Kick(b.Id).IsSuccess, Is.True);
            Pump();

            Assert.That(host.Left.Select(left => (left.Participant.Id, left.Reason)), Is.EqualTo(new[]
            {
                (a.Id, MultiplayerReason.LeftNormally),
                (b.Id, MultiplayerReason.Kicked),
            }));
            Assert.That(host.Disconnected, Is.Empty);
            Assert.That(b.Manager.State, Is.EqualTo(SessionState.Offline));

            c.Backend.SimulateOutage(20f);
            Advance(DropSeconds + 0.5);
            Assert.That(host.Disconnected.Select(participant => participant.Id), Is.EqualTo(new[] { c.Id }));
            Assert.That(host.Manager.Kick(c.Id).IsSuccess, Is.True);
            Assert.That(host.Left.Last().Reason, Is.EqualTo(MultiplayerReason.Kicked));

            Advance(11.0);
            Assert.That(c.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(c.LastChange.Reason, Is.EqualTo(MultiplayerReason.Banned));
            Assert.That(host.Reconnected, Is.Empty);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Reconnect_TheGameEndedWhileAway_TheReturningPlayerHearsWhatItMissed(bool startAnother)
        {
            // FR-20, FR-26: the slot outlasts the game.
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node b = JoinNew("B", host);
            host.Manager.StartGame(new byte[] { 1 });
            Pump();

            b.Backend.SimulateOutage(20f);
            Advance(DropSeconds + 0.5);
            Assert.That(host.Manager.EndGame().IsSuccess, Is.True);
            if (startAnother)
            {
                Assert.That(host.Manager.StartGame(new byte[] { 2 }).IsSuccess, Is.True);
            }

            Pump();
            Advance(11.0);

            SessionState expected = startAnother ? SessionState.InGame : SessionState.Lobby;
            Assert.That(b.Manager.State, Is.EqualTo(expected));
            Assert.That(b.LastChange.ToString(), Is.EqualTo($"Reconnecting -> {expected} (None)"));
            Assert.That(b.GameStarts, Is.EqualTo(startAnother ? new[] { new byte[] { 1 }, new byte[] { 2 } } : new[] { new byte[] { 1 } }));
            Assert.That(b.ReturnedToLobbyCount, Is.EqualTo(startAnother ? 0 : 1));
            Assert.That(b.Manager.StartData.ToArray(), Is.EqualTo(startAnother ? new byte[] { 2 } : new byte[0]));
            Assert.That(b.Reconnected.Select(participant => participant.Id), Is.EqualTo(new[] { b.Id }));
            Assert.That(host.Reconnected.Select(participant => participant.Id), Is.EqualTo(new[] { b.Id }));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Reconnect_InTheLobby_OrWithoutAReservationTime_ADropEndsThePlayersSession(bool inGame)
        {
            // FR-20: only a game keeps the slot, and only with a reservation time.
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false).WithReconnectReservationSeconds(inGame ? 0f : 60f));
            Node a = JoinNew("A", host);
            if (inGame)
            {
                host.Manager.StartGame();
                Pump();
            }

            a.Backend.SimulateOutage(20f);
            Advance(DropSeconds + 0.5);

            Assert.That(host.Left.Single().Participant.Id, Is.EqualTo(a.Id));
            Assert.That(host.Left.Single().Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            Assert.That(host.Disconnected, Is.Empty);
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(a.States.Any(change => change.Current == SessionState.Reconnecting), Is.False);
        }

        [Test]
        public void Reconnect_TheHostLeavingWhileAPlayerIsCutOff_ReachesItThroughTheLobby()
        {
            // FR-27: the lobby goes on through an outage of the session connections.
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            host.Manager.StartGame();
            Pump();

            b.Backend.SimulateOutage(30f);
            Advance(1.0);
            host.Manager.Leave();
            Advance(0.5);

            foreach (Node client in new[] { a, b })
            {
                Assert.That(client.Manager.State, Is.EqualTo(SessionState.Offline), client.Backend.PlayerName);
                Assert.That(client.LastChange.Reason, Is.EqualTo(MultiplayerReason.HostLeft), client.Backend.PlayerName);
            }
        }

        [Test]
        public void Reconnect_AReturningHello_TakesOverAConnectionTheHostDidNotSeeBreak_OrIsRefusedWithoutASlot()
        {
            // FR-21, NFR-5: the account the transport proved decides, and only it can come back.
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            RecordingListener listener = new RecordingListener();
            LocalBackend raw = CreateRawBackend("Raw", listener);
            ConnectionId first = ConnectAsPlayer(raw, host);
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();

            ConnectionId second = raw.Connect(host.Id);
            Pump();
            raw.Send(second, HelloPacket(isReturning: true), DeliveryMode.Reliable);
            Pump();

            Assert.That(listener.Disconnected.Single(), Is.EqualTo((first, BackendDisconnectKind.ClosedByPeer, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost))));
            Assert.That(host.Disconnected.Select(participant => participant.Id), Is.EqualTo(new[] { raw.LocalPlayerId }));
            Assert.That(host.Reconnected.Select(participant => participant.Id), Is.EqualTo(new[] { raw.LocalPlayerId }));
            Assert.That(host.Manager.TryGetParticipant(raw.LocalPlayerId, out Participant back) && back.IsConnected, Is.True);
            Assert.That(host.Left, Is.Empty);

            RecordingListener strangerEvents = new RecordingListener();
            LocalBackend stranger = CreateRawBackend("Stranger", strangerEvents);
            ConnectionId attempt = stranger.Connect(host.Id);
            Pump();
            stranger.Send(attempt, HelloPacket(isReturning: true), DeliveryMode.Reliable);
            Pump();

            Assert.That(strangerEvents.Disconnected.Single(), Is.EqualTo((attempt, BackendDisconnectKind.ClosedByPeer, ReasonCodes.ToWire(MultiplayerReason.ConnectionLost))));
            Assert.That(host.Manager.TryGetParticipant(stranger.LocalPlayerId, out _), Is.False);
        }
        #endregion

        #region Outage
        [Test]
        public void Outage_ShorterThanTheTimeout_KeepsTheConnection_LosesUnreliableMessages_AndDelaysReliableOnes()
        {
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, a });

            a.Backend.SimulateOutage(5f);
            Advance(1.0);
            Assert.That(host.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.To(a.Id)).IsSuccess, Is.True);
            Assert.That(host.Manager.Send(Chat, new byte[] { 2 }, MessageTarget.To(a.Id), DeliveryMode.Unreliable).IsSuccess, Is.True);
            Assert.That(a.Manager.Send(Chat, new byte[] { 3 }, MessageTarget.Host).IsSuccess, Is.True);
            Assert.That(a.Manager.Send(Chat, new byte[] { 4 }, MessageTarget.Host, DeliveryMode.Unreliable).IsSuccess, Is.True);
            Advance(3.5);
            Assert.That(inboxes[a].Received.Concat(inboxes[host].Received), Is.Empty);

            Advance(1.0);
            Assert.That(inboxes[a].Received.Select(message => message.Payload[0]), Is.EqualTo(new byte[] { 1 }));
            Assert.That(inboxes[host].Received.Select(message => message.Payload[0]), Is.EqualTo(new byte[] { 3 }));
            Assert.That(host.Left.Concat(a.Left), Is.Empty);
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));
        }

        [Test]
        public void Outage_RefusesNegativeOrNonFiniteLengths_AndBlocksNewConnections()
        {
            UseTestNetworkClock();
            Node host = CreateHost("Host");
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());

            Assert.Throws<System.ArgumentOutOfRangeException>(() => raw.SimulateOutage(-1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => raw.SimulateOutage(float.NaN));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => raw.SimulateOutage(float.PositiveInfinity));

            host.Backend.SimulateOutage(2f);
            Node a = CreateNode("A");
            Assert.That(Join(a, host).Reason, Is.EqualTo(MultiplayerReason.ConnectionFailed), "the lobby answers, the host does not");

            Advance(2.5);
            Assert.That(Join(a, host).IsSuccess, Is.True);
        }
        #endregion

        #region Reconnect Helpers
        private static byte[] HelloPacket(bool isReturning)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.Hello);
            writer.WriteInt(WireProtocol.Version);
            writer.WriteString(GameId);
            writer.WriteInt(1);
            writer.WriteBool(isReturning);
            return writer.Written.ToArray();
        }
        #endregion
    }
}
