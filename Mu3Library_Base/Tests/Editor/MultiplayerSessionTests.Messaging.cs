using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Protocol;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
// Unity's Is extends NUnit's with AllocatingGCMemory.
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Game messages through <see cref="IMultiplayerMessenger"/>: targets, delivery, policies,
    /// and limits (FR-31 to FR-34 and NFR-1, NFR-2, NFR-5).
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        private const ushort Chat = 1;
        private const ushort HostCommand = 2;
        private const ushort Report = 3;

        private sealed class Inbox
        {
            public readonly List<(PlayerId Sender, DeliveryMode Mode, byte[] Payload)> Received =
                new List<(PlayerId, DeliveryMode, byte[])>();

            public IEnumerable<PlayerId> Senders => Received.Select(message => message.Sender);
        }

        #region Targets
        [Test]
        public void HostToItself_IsHandledBeforeSendReturns_WithTheHostAsSender()
        {
            // FR-31
            (Node host, Node[] clients) = CreateSession(3);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, clients.Prepend(host));

            Assert.That(host.Manager.Send(Chat, new byte[] { 7 }, MessageTarget.Host).IsSuccess, Is.True);

            Assert.That(inboxes[host].Received.Single().Sender, Is.EqualTo(host.Id));
            Assert.That(inboxes[host].Received.Single().Payload, Is.EqualTo(new byte[] { 7 }));
            Pump();
            Assert.That(clients.All(client => inboxes[client].Received.Count == 0), Is.True);
        }

        [Test]
        public void HostTargets_ReachExactlyTheirPlayers()
        {
            // FR-31
            (Node host, Node[] clients) = CreateSession(3);
            Node a = clients[0], b = clients[1], c = clients[2];
            Node[] everyone = clients.Prepend(host).ToArray();

            AssertReceivers(host, MessageTarget.Others, everyone, a, b, c);
            AssertReceivers(host, MessageTarget.All, everyone, host, a, b, c);
            AssertReceivers(host, MessageTarget.To(b.Id), everyone, b);
            AssertReceivers(host, MessageTarget.AllExcept(b.Id), everyone, host, a, c);
        }

        [Test]
        public void ClientTargets_ReachExactlyTheirPlayers_ThroughTheHost()
        {
            // FR-31
            (Node host, Node[] clients) = CreateSession(3);
            Node a = clients[0], b = clients[1], c = clients[2];
            Node[] everyone = clients.Prepend(host).ToArray();

            AssertReceivers(a, MessageTarget.Host, everyone, host);
            AssertReceivers(a, MessageTarget.To(b.Id), everyone, b);
            AssertReceivers(a, MessageTarget.To(a.Id), everyone, a);
            AssertReceivers(a, MessageTarget.All, everyone, host, a, b, c);
            AssertReceivers(a, MessageTarget.Others, everyone, host, b, c);
            AssertReceivers(a, MessageTarget.AllExcept(b.Id), everyone, host, a, c);
        }

        [Test]
        public void ReliableMessages_ArriveInTheOrderTheyWereSent()
        {
            // FR-32. Loss is not simulated yet (FR-40); this checks order and the delivery mode.
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, a, b });

            for (int i = 0; i < 1000; i++)
            {
                Assert.That(a.Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.To(b.Id)).IsSuccess, Is.True);
            }

            a.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.To(b.Id), DeliveryMode.Unreliable);
            Pump();

            List<(PlayerId Sender, DeliveryMode Mode, byte[] Payload)> received = inboxes[b].Received;
            Assert.That(received.Count, Is.EqualTo(1001));
            Assert.That(received.Take(1000).Select(message => BitConverter.ToInt32(message.Payload, 0)), Is.EqualTo(Enumerable.Range(0, 1000)));
            Assert.That(received.Take(1000).All(message => message.Mode == DeliveryMode.Reliable && message.Sender == a.Id), Is.True);
            Assert.That(received[1000].Mode, Is.EqualTo(DeliveryMode.Unreliable));
        }

        [Test]
        public void AMessage_ArrivesOnTheReceiversNextUpdate()
        {
            // NFR-2
            (Node host, Node[] clients) = CreateSession(1);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, clients[0] });

            clients[0].Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host);
            host.Manager.Update();

            Assert.That(inboxes[host].Received.Count, Is.EqualTo(1));
        }
        #endregion

        #region Policies
        [Test]
        public void HostOnlyType_FromAClient_IsRefusedOrDropped()
        {
            // FR-33, NFR-5
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Dictionary<Node, Inbox> inboxes = ListenAll(HostCommand, new[] { host, a, b }, MessagePolicy.HostOnly);

            Assert.That(a.Manager.Send(HostCommand, new byte[] { 1 }, MessageTarget.All).Reason, Is.EqualTo(MultiplayerReason.NotHost));

            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped message type 2: only the host may send it"));
            raw.Send(connection, MessageToHostPacket(HostCommand, MessageTargetKind.All, PlayerId.None, new byte[] { 2 }), DeliveryMode.Reliable);
            Pump();

            Assert.That(inboxes.Values.All(inbox => inbox.Received.Count == 0), Is.True);

            Assert.That(host.Manager.Send(HostCommand, new byte[] { 3 }, MessageTarget.Others).IsSuccess, Is.True);
            Pump();
            Assert.That(inboxes[a].Senders, Is.EqualTo(new[] { host.Id }));
            Assert.That(inboxes[b].Senders, Is.EqualTo(new[] { host.Id }));
        }

        [Test]
        public void NoClientRelayType_ReachesTheHostOnly()
        {
            // FR-31: the host can keep a type from passing between clients.
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Dictionary<Node, Inbox> inboxes = ListenAll(Report, new[] { host, a, b }, MessagePolicy.NoClientRelay);

            Assert.That(a.Manager.Send(Report, new byte[] { 1 }, MessageTarget.Host).IsSuccess, Is.True);
            Assert.That(a.Manager.Send(Report, new byte[] { 2 }, MessageTarget.Others).Reason, Is.EqualTo(MultiplayerReason.NotHost));

            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);
            raw.Send(connection, MessageToHostPacket(Report, MessageTargetKind.Others, PlayerId.None, new byte[] { 3 }), DeliveryMode.Reliable);
            Pump();

            Assert.That(inboxes[host].Senders, Is.EqualTo(new[] { a.Id, raw.LocalPlayerId }));
            Assert.That(inboxes[b].Received, Is.Empty);
            Assert.That(inboxes[a].Received, Is.Empty);
        }

        [Test]
        public void TheSender_IsTheConnectionsPlayer_NeverWhatThePacketSays()
        {
            // FR-33, NFR-5: a message to the host carries no sender at all.
            (Node host, Node[] clients) = CreateSession(1);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, clients[0] });
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);

            raw.Send(connection, MessageToHostPacket(Chat, MessageTargetKind.All, PlayerId.None, new byte[] { 1 }), DeliveryMode.Reliable);
            Pump();

            Assert.That(inboxes[host].Senders, Is.EqualTo(new[] { raw.LocalPlayerId }));
            Assert.That(inboxes[clients[0]].Senders, Is.EqualTo(new[] { raw.LocalPlayerId }));
        }

        [Test]
        public void UnregisteredType_IsDropped_WhereverItIsMissing()
        {
            // FR-33
            const ushort OnlyClients = 20;
            const ushort OnlyHost = 21;
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Dictionary<Node, Inbox> clientInboxes = ListenAll(OnlyClients, new[] { a, b });
            Inbox hostInbox = Listen(host, OnlyHost);

            LogAssert.Expect(LogType.Warning, new Regex("Dropped message type 20: the host has not registered it"));
            LogAssert.Expect(LogType.Warning, new Regex("Dropped message type 21: this player has not registered it"));
            Assert.That(a.Manager.Send(OnlyClients, new byte[] { 1 }, MessageTarget.To(b.Id)).IsSuccess, Is.True);
            Assert.That(host.Manager.Send(OnlyHost, new byte[] { 2 }, MessageTarget.All).IsSuccess, Is.True);
            Pump();

            Assert.That(clientInboxes[b].Received, Is.Empty, "the host drops a type it has not registered, even to pass it on");
            Assert.That(hostInbox.Received.Count, Is.EqualTo(1));
            Assert.That(clientInboxes[a].Received, Is.Empty);
        }

        [Test]
        public void HostRegisteredWithoutAHandler_PassesTheTypeOn()
        {
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Assert.That(host.Manager.RegisterMessage(Chat, null).IsSuccess, Is.True);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { a, b });

            a.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.All);
            Pump();

            Assert.That(inboxes[b].Senders, Is.EqualTo(new[] { a.Id }));
            Assert.That(inboxes[a].Senders, Is.EqualTo(new[] { a.Id }));
        }

        [Test]
        public void AThrowingHandler_IsLogged_AndLaterMessagesStillArrive()
        {
            // FR-33
            (Node host, Node[] clients) = CreateSession(1);
            int handled = 0;
            host.Manager.RegisterMessage(Chat, (context, payload) =>
            {
                handled++;
                if (payload[0] == 0)
                {
                    throw new InvalidOperationException("handler failure");
                }
            });
            clients[0].Manager.RegisterMessage(Chat, null);

            LogAssert.Expect(LogType.Exception, new Regex("handler failure"));
            clients[0].Manager.Send(Chat, new byte[] { 0 }, MessageTarget.Host);
            clients[0].Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host);
            Pump();

            Assert.That(handled, Is.EqualTo(2));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Lobby));
        }
        #endregion

        #region Limits And Requests
        [Test]
        public void MessageAboveTheLimit_IsRefused_AndOneAtTheLimitArrives()
        {
            // FR-34
            (Node host, Node[] clients) = CreateSession(1);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, clients[0] });
            Assert.That(host.Manager.MaxMessageSize, Is.EqualTo(64 * 1024));

            Assert.That(clients[0].Manager.Send(Chat, new byte[65 * 1024], MessageTarget.All).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Pump();
            Assert.That(inboxes.Values.All(inbox => inbox.Received.Count == 0), Is.True);

            Assert.That(clients[0].Manager.Send(Chat, new byte[64 * 1024], MessageTarget.Host).IsSuccess, Is.True);
            Pump();
            Assert.That(inboxes[host].Received.Single().Payload.Length, Is.EqualTo(64 * 1024));
        }

        [Test]
        public void MessageSizeLimit_AboveWhatTheBackendCarries_IsRefusedAtInitialize()
        {
            // FR-34
            LocalBackend backend = CreateRawBackend("Backend");
            MultiplayerManager tooLarge = new MultiplayerManager();
            MultiplayerManager largest = new MultiplayerManager();

            int limit = backend.MaxPacketSize - MessageHeader.ToHostSize;
            MultiplayerResult refused = tooLarge.Initialize(backend, new MultiplayerConfig(GameId, 1) { MaxMessageSize = limit + 1 });
            MultiplayerResult accepted = largest.Initialize(backend, new MultiplayerConfig(GameId, 1) { MaxMessageSize = limit });

            Assert.That(refused.Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(accepted.IsSuccess, Is.True);
            Assert.That(largest.MaxMessageSize, Is.EqualTo(limit));
            largest.Dispose();
            tooLarge.Dispose();
        }

        [Test]
        public void Send_OutsideASessionOrToAnUnknownPlayer_IsRefused()
        {
            (Node host, Node[] clients) = CreateSession(1);
            Node idle = CreateNode("Idle");
            Node joining = CreateNode("Joining");
            joining.Manager.JoinLobby(host.Manager.CurrentLobbyId);

            using (MultiplayerManager uninitialized = new MultiplayerManager())
            {
                Assert.That(uninitialized.Send(Chat, default, MessageTarget.All).Reason, Is.EqualTo(MultiplayerReason.NotInitialized));
            }

            Assert.That(idle.Manager.Send(Chat, default, MessageTarget.All).Reason, Is.EqualTo(MultiplayerReason.InvalidState));
            Assert.That(joining.Manager.Send(Chat, default, MessageTarget.All).Reason, Is.EqualTo(MultiplayerReason.InvalidState));
            Assert.That(host.Manager.Send(Chat, default, MessageTarget.To(idle.Id)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.Send(Chat, default, MessageTarget.AllExcept(PlayerId.None)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.Send(Chat, default, default(MessageTarget)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(clients[0].Manager.Send(Chat, default, MessageTarget.Host, (DeliveryMode)9).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
        }

        [Test]
        public void RegisterMessage_OncePerType_UntilUnregistered()
        {
            (Node host, Node[] clients) = CreateSession(1);
            Inbox inbox = Listen(host, Chat);
            clients[0].Manager.RegisterMessage(Chat, null);

            Assert.That(host.Manager.RegisterMessage(Chat, (context, payload) => { }).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.RegisterMessage(Report, null, (MessagePolicy)4).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.UnregisterMessage(Chat), Is.True);
            Assert.That(host.Manager.UnregisterMessage(Chat), Is.False);

            clients[0].Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host);
            Pump();
            Assert.That(inbox.Received, Is.Empty);
        }

        [Test]
        public void MalformedMessagePackets_AreDropped()
        {
            // NFR-5: an unknown target, an unknown delivery mode, and a header cut short.
            (Node host, Node[] clients) = CreateSession(1);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, clients[0] });
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);

            byte[] badTarget = MessageToHostPacket(Chat, MessageTargetKind.All, PlayerId.None, new byte[] { 1 });
            badTarget[2] = 99;
            byte[] badMode = MessageToHostPacket(Chat, MessageTargetKind.All, PlayerId.None, new byte[] { 1 });
            badMode[1] = 7;
            byte[] cutShort = { (byte)PacketKind.MessageToHost, 0, (byte)MessageTargetKind.All };
            byte[] noTarget = MessageToHostPacket(Chat, MessageTargetKind.None, PlayerId.None, new byte[] { 1 });

            raw.Send(connection, badTarget, DeliveryMode.Reliable);
            raw.Send(connection, noTarget, DeliveryMode.Reliable);
            raw.Send(connection, badMode, DeliveryMode.Reliable);
            raw.Send(connection, cutShort, DeliveryMode.Reliable);
            Pump();

            Assert.That(inboxes.Values.All(inbox => inbox.Received.Count == 0), Is.True);
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(3));
        }

        [Test]
        public void RelayingMessages_AllocatesNothingOnceWarm()
        {
            // NFR-1: a client's message to everyone, through the host, received by every player.
            // The network's clock stands still, so no quality probe falls into a measured frame.
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(3);
            int handled = 0;
            foreach (Node node in clients.Prepend(host))
            {
                node.Manager.RegisterMessage(Chat, (context, payload) => handled += payload.Length);
            }

            byte[] payload = new byte[256];
            void SendAndDeliver()
            {
                clients[0].Manager.Send(Chat, payload, MessageTarget.Others, DeliveryMode.Unreliable);
                Pump(1);
            }

            for (int i = 0; i < 8; i++)
            {
                SendAndDeliver();
            }

            void HostSendsAndDelivers()
            {
                host.Manager.Send(Chat, payload, MessageTarget.All, DeliveryMode.Reliable);
                Pump(1);
            }

            for (int i = 0; i < 8; i++)
            {
                HostSendsAndDelivers();
            }

            Assert.That(SendAndDeliver, Is.Not.AllocatingGCMemory());
            Assert.That(HostSendsAndDelivers, Is.Not.AllocatingGCMemory());
            Assert.That(handled, Is.GreaterThan(0));
        }
        #endregion

        #region Review Regressions
        [Test]
        public void TheHostsSizeLimit_DropsAnOversizedMessageFromAClient()
        {
            // FR-34, NFR-5: a client that skips its own check still cannot get past the host.
            Node host = CreateHost(
                CreateNode("Host", configure: config => config.MaxMessageSize = 16),
                PublicOptions(new SessionRules(MultiplayerLimits.MaxCapacity, requireReady: false)));
            Node a = JoinNew("A", host);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, a });
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);

            LogAssert.Expect(LogType.Warning, new Regex("Dropped message type 1: 17 bytes"));
            raw.Send(connection, MessageToHostPacket(Chat, MessageTargetKind.All, PlayerId.None, new byte[17]), DeliveryMode.Reliable);
            raw.Send(connection, MessageToHostPacket(Chat, MessageTargetKind.All, PlayerId.None, new byte[16]), DeliveryMode.Reliable);
            Pump();

            Assert.That(inboxes[host].Received.Select(message => message.Payload.Length), Is.EqualTo(new[] { 16 }));
            Assert.That(inboxes[a].Received.Select(message => message.Payload.Length), Is.EqualTo(new[] { 16 }));
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(3));
        }

        [Test]
        public void AClient_TakesTheHostsSmallerLimit_ForTheSession()
        {
            // FR-34: the welcome carries the host's limit.
            Node host = CreateHost(
                CreateNode("Host", configure: config => config.MaxMessageSize = 16),
                PublicOptions(new SessionRules(4, requireReady: false)));
            Node a = CreateNode("A");
            Assert.That(a.Manager.MaxMessageSize, Is.EqualTo(MultiplayerConfig.DefaultMaxMessageSize));

            Join(a, host);
            Listen(host, Chat);
            a.Manager.RegisterMessage(Chat, null);

            Assert.That(a.Manager.MaxMessageSize, Is.EqualTo(16));
            Assert.That(a.Manager.Send(Chat, new byte[17], MessageTarget.Host).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(a.Manager.Send(Chat, new byte[16], MessageTarget.Host).IsSuccess, Is.True);

            a.Manager.Leave();
            Assert.That(a.Manager.MaxMessageSize, Is.EqualTo(MultiplayerConfig.DefaultMaxMessageSize));
        }

        [Test]
        public void AnotherWireVersion_IsRefused_InSearchJoinAndHello()
        {
            // Builds that share a game protocol but not a library version cannot talk.
            Node host = CreateHost("Host");
            LocalBackend other = CreateRawBackend("OtherLibrary");
            LobbyId otherLobby = CreateRawLobby(other, GameId, 1, wireVersion: (WireProtocol.Version + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            Node seeker = CreateNode("Seeker");

            Assert.That(Search(seeker).Lobbies.Select(lobby => lobby.Id), Is.EqualTo(new[] { host.Manager.CurrentLobbyId }));
            Assert.That(Join(seeker, otherLobby).Reason, Is.EqualTo(MultiplayerReason.VersionMismatch));

            RecordingListener listener = new RecordingListener();
            LocalBackend raw = CreateRawBackend("Raw", listener);
            ConnectionId connection = raw.Connect(host.Id);
            Pump();
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.Hello);
            writer.WriteInt(WireProtocol.Version + 1);
            writer.WriteString(GameId);
            writer.WriteInt(1);
            raw.Send(connection, writer.Written, DeliveryMode.Reliable);
            Pump();

            Assert.That(listener.Disconnected.Single().ReasonCode, Is.EqualTo(ReasonCodes.ToWire(MultiplayerReason.VersionMismatch)));
        }

        [Test]
        public void AReliableMessageThatCannotLeave_DropsThatPlayer_AnUnreliableOneDoesNot()
        {
            // FR-32: a player who would silently miss a reliable message leaves the session instead.
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, a, b });

            host.Backend.FailingSends = 1;
            Assert.That(host.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Others, DeliveryMode.Unreliable).IsSuccess, Is.True);
            Pump();
            Assert.That(inboxes[a].Received.Count + inboxes[b].Received.Count, Is.EqualTo(1));
            Assert.That(host.Manager.Participants.Count, Is.EqualTo(3));

            host.Backend.FailingSends = 1;
            Assert.That(host.Manager.Send(Chat, new byte[] { 2 }, MessageTarget.To(b.Id)).Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            Pump();

            Assert.That(host.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id, a.Id }));
            Assert.That(a.Left.Single().Participant.Id, Is.EqualTo(b.Id));
            Assert.That(a.Left.Single().Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            Assert.That(b.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(b.LastChange.Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
        }

        [Test]
        public void HandlersThatKickOrLeave_WhileAMessageIsPassedOn_KeepTheSessionConsistent()
        {
            (Node host, Node[] clients) = CreateSession(3);
            Node a = clients[0], b = clients[1], c = clients[2];
            host.Manager.RegisterMessage(Chat, (context, payload) => host.Manager.Kick(context.Sender));
            a.Manager.RegisterMessage(Chat, null);
            b.Manager.RegisterMessage(Chat, (context, payload) => b.Manager.Leave());
            Inbox inbox = Listen(c, Chat);

            Assert.That(a.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Others).IsSuccess, Is.True);
            Pump();

            Assert.That(inbox.Senders, Is.EqualTo(new[] { a.Id }));
            Assert.That(host.Manager.Participants.Select(participant => participant.Id), Is.EqualTo(new[] { host.Id, c.Id }));
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.Kicked));
            Assert.That(b.LastChange.Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
        }
        [Test]
        public void AClientWhoseReliableMessageCannotLeave_EndsItsSession()
        {
            // FR-32: the host would go on without the message, so the client leaves instead.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            ListenAll(Chat, new[] { host, a });

            a.Backend.FailingSends = 1;
            Assert.That(a.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host, DeliveryMode.Unreliable).Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));

            a.Backend.FailingSends = 1;
            Assert.That(a.Manager.Send(Chat, new byte[] { 2 }, MessageTarget.Host).Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));

            Pump();
            Assert.That(host.Left.Single().Participant.Id, Is.EqualTo(a.Id));
        }

        [Test]
        public void AHandlerThatLeavesWhileAPlayerIsDropped_GetsNoMessageAfterward()
        {
            // The drop raises events before the host's own copy is handled; a session that ended
            // there handles nothing more, and the message answers the reason it ended with (#340).
            (Node host, Node[] clients) = CreateSession(2);
            Inbox inbox = Listen(host, Chat);
            host.Manager.OnParticipantLeft += (participant, reason) => host.Manager.Leave();

            host.Backend.FailingSends = 1;
            Assert.That(host.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.All).Reason, Is.EqualTo(MultiplayerReason.LeftNormally));

            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(inbox.Received, Is.Empty);
        }

        [TestCase(true, TestName = "Send_AMessageThatWentOutBeforeAHandlerEndedTheSession_CountsAsSent(TheHostsPastADropThatLeaves)")]
        [TestCase(false, TestName = "Send_AMessageThatWentOutBeforeAHandlerEndedTheSession_CountsAsSent(AClientsWhoseOwnCopyLeaves)")]
        public void Send_AMessageThatWentOutBeforeAHandlerEndedTheSession_CountsAsSent(bool fromHost)
        {
            // #340: the call answers the reason the session ended with, but the message went out
            // and its receivers count it, so GetMessageCounts counts it as sent too.
            (Node host, Node[] clients) = CreateSession(2);
            Node sender = fromHost ? host : clients[0];
            if (fromHost)
            {
                Listen(host, Chat);
                host.Manager.OnParticipantLeft += (participant, reason) => host.Manager.Leave();
                host.Backend.FailingSends = 1;
            }
            else
            {
                Assert.That(sender.Manager.RegisterMessage(Chat, (context, payload) => sender.Manager.Leave()).IsSuccess, Is.True);
            }

            Assert.That(sender.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.All).Reason, Is.EqualTo(MultiplayerReason.LeftNormally));

            List<MessageCounts> counts = new List<MessageCounts>();
            sender.Manager.GetMessageCounts(counts);
            Assert.That(counts.Single(count => count.MessageType == Chat).Sent, Is.EqualTo(1), "the message went out");
        }

        [TestCase(false, TestName = "StartGame_AHandlerThatEndsTheSessionWhileTheStartGoesOut_KeepsTheGameFromStarting(Leaves)")]
        [TestCase(true, TestName = "StartGame_AHandlerThatEndsTheSessionWhileTheStartGoesOut_KeepsTheGameFromStarting(LeavesAndCreatesAnother)")]
        public void StartGame_AHandlerThatEndsTheSessionWhileTheStartGoesOut_KeepsTheGameFromStarting(bool createsAnother)
        {
            // #340: the start could not reach a player, as with a full send buffer on Steam, so the
            // host dropped that player, and the game's handler of the leave ended the session. The
            // start then turned Offline into InGame, with no session behind it, and answered
            // success; after a new CreateLobby it turned Creating into InGame.
            Node host = CreateSessionEndedByTheNextDrop(new SessionRules(4, requireReady: false), afterLeaving: node =>
            {
                if (createsAnother)
                {
                    node.Manager.CreateLobby(PublicOptions(SessionRules.Default));
                }
            });
            int before = host.States.Count;

            host.Backend.FailingSends = 1;

            Assert.That(host.Manager.StartGame(new byte[] { 7 }).Reason, Is.EqualTo(MultiplayerReason.LeftNormally), "the reason the session ended with");
            string[] expected = createsAnother
                ? new[] { "Lobby -> Offline (LeftNormally)", "Offline -> Creating (None)" }
                : new[] { "Lobby -> Offline (LeftNormally)" };
            Assert.That(host.States.Skip(before).Select(change => change.ToString()), Is.EqualTo(expected));
            Assert.That(host.GameStarts, Is.Empty);
        }

        [Test]
        public void EndGame_AHandlerThatEndsTheSessionWhileTheReturnGoesOut_KeepsTheSessionFromReturningToTheLobby()
        {
            // #340: without a reservation the dropped player leaves at once, which raises the leave.
            Node host = CreateSessionEndedByTheNextDrop(new SessionRules(4, requireReady: false, reconnectReservationSeconds: 0f));
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();
            int before = host.States.Count;

            host.Backend.FailingSends = 1;

            Assert.That(host.Manager.EndGame().Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            Assert.That(host.States.Skip(before).Select(change => change.ToString()), Is.EqualTo(new[] { "InGame -> Offline (LeftNormally)" }));
            Assert.That(host.ReturnedToLobbyCount, Is.Zero);
        }

        [Test]
        public void UpdateRules_AHandlerThatEndsTheSessionWhileTheRulesGoOut_RaisesNoRulesChange()
        {
            // #340
            SessionRules rules = new SessionRules(4, requireReady: false);
            Node host = CreateSessionEndedByTheNextDrop(rules);
            List<SessionRules> changes = new List<SessionRules>();
            host.Manager.OnRulesChanged += changes.Add;
            int before = host.States.Count;

            host.Backend.FailingSends = 1;

            Assert.That(host.Manager.UpdateRules(rules.WithCapacity(3)).Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            Assert.That(host.States.Skip(before).Select(change => change.ToString()), Is.EqualTo(new[] { "Lobby -> Offline (LeftNormally)" }));
            Assert.That(changes, Is.Empty);
        }

        [Test]
        public void Join_AHandlerThatEndsTheSessionWhileTheJoinGoesOut_RaisesNoJoin()
        {
            // #340: the host welcomes the player, then tells the others; a send of that news fails.
            // LocalBackend.FailingSends would fail the welcome, the first send, so the stand-in
            // backend lets that one through.
            Node host = CreateSessionEndedByTheNextDrop(new SessionRules(4, requireReady: false), onPlatform: true);
            int before = host.States.Count;
            int joined = host.Joined.Count;

            host.Platform.SendsBeforeFailure = 1;
            Node c = CreateNode("C");
            c.Manager.JoinLobby(host.Manager.CurrentLobbyId);
            Pump();

            Assert.That(host.Platform.SendsBeforeFailure, Is.EqualTo(-1), "the news failed after the welcome");
            Assert.That(host.States.Skip(before).Select(change => change.ToString()), Is.EqualTo(new[] { "Lobby -> Offline (LeftNormally)" }));
            Assert.That(host.Joined.Count, Is.EqualTo(joined), "no join after the session ended");
        }

        [Test]
        public void SetReady_TheHostsOwn_AHandlerThatEndsTheSessionWhileTheChangeGoesOut_RaisesNoChange()
        {
            // #340
            Node host = CreateSessionEndedByTheNextDrop(new SessionRules(4, requireReady: false));
            int before = host.States.Count;
            int changed = host.Changed.Count;

            host.Backend.FailingSends = 1;

            Assert.That(host.Manager.SetReady(true).Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            Assert.That(host.States.Skip(before).Select(change => change.ToString()), Is.EqualTo(new[] { "Lobby -> Offline (LeftNormally)" }));
            Assert.That(host.Changed.Count, Is.EqualTo(changed));
        }

        [Test]
        public void Kick_AHandlerThatEndsTheSessionWhileTheKickGoesOut_RaisesNoLeaveForTheKickedPlayer()
        {
            // #340: the news of the kick could not reach the other player, whose drop raised the
            // leave whose handler ended the session; the kicked player's own leave came after it.
            Node host = CreateSessionEndedByTheNextDrop(new SessionRules(4, requireReady: false));
            PlayerId kicked = host.Manager.Participants[1].Id;
            PlayerId dropped = host.Manager.Participants[2].Id;

            host.Backend.FailingSends = 1;

            Assert.That(host.Manager.Kick(kicked).Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            Assert.That(host.Left.Select(left => left.Participant.Id), Is.EqualTo(new[] { dropped }));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
        }

        [Test]
        public void HostRequest_AHandlerThatEndsTheSessionWhileItsMessageGoesOut_AnswersTheReasonTheSessionEndedWith(
            [ValueSource(nameof(HostRequestsThatSend))] string request)
        {
            // #340: these requests already raised no event of their own after such a drop, but
            // answered success, and the host's message to one player answered ConnectionLost
            // although the session ended with LeftNormally. Without a reservation the dropped
            // player leaves at once, also in a game.
            Node host = CreateSessionEndedByTheNextDrop(new SessionRules(4, requireReady: false, reconnectReservationSeconds: 0f));
            Inbox inbox = Listen(host, Chat);
            PrepareHostRequest(host, request);
            int before = host.States.Count;
            int events = CountOwnEvents(host, inbox, request);

            host.Backend.FailingSends = 1;

            Assert.That(MakeHostRequest(host, request).Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            Assert.That(host.States.Skip(before).Select(change => change.Current), Is.EqualTo(new[] { SessionState.Offline }));
            Assert.That(CountOwnEvents(host, inbox, request), Is.EqualTo(events), "no event of its own after the session ended");
        }

        [Test]
        public void HostRequest_AHandlerOfItsOwnEventThatEndsTheSession_AnswersTheReasonTheSessionEndedWith(
            [ValueSource(nameof(HostRequestsWithOwnEvents))] string request)
        {
            // #340: the change went out and the request's event ran, but the session it changed
            // ended there, so success would claim a change that no longer stands, as CreateLobby
            // and JoinLobby answer the reason too. StartGame answered success when a handler of
            // its change to InGame left, though OnGameStarted never came.
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false, reconnectReservationSeconds: 0f));
            JoinNew("A", host);
            JoinNew("B", host);
            PrepareHostRequest(host, request);
            LeaveFromOwnEvent(host, request);
            int before = host.States.Count;

            Assert.That(MakeHostRequest(host, request).Reason, Is.EqualTo(MultiplayerReason.LeftNormally));
            Assert.That(host.States.Skip(before).Last().ToString(), Does.EndWith("-> Offline (LeftNormally)"));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
        }

        [Test]
        public void SendingToAConnectionTheOtherSideClosed_KeepsTheReasonItGave()
        {
            // FR-27, FR-22, FR-28: a message sent while the close is on its way does not turn a
            // host leaving, a kick, or a player leaving into a lost connection.
            (Node host, Node[] clients) = CreateSession(3);
            Node a = clients[0], b = clients[1], c = clients[2];

            Assert.That(host.Manager.Kick(a.Id).IsSuccess, Is.True);
            Assert.That(a.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host).IsSuccess, Is.True);
            c.Manager.Leave();
            Assert.That(host.Manager.Send(Chat, new byte[] { 2 }, MessageTarget.All).IsSuccess, Is.True);
            Pump();

            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.Kicked));
            Assert.That(host.Left.Single(left => left.Participant.Id == c.Id).Reason, Is.EqualTo(MultiplayerReason.LeftNormally));

            host.Manager.Leave();
            Assert.That(b.Manager.Send(Chat, new byte[] { 3 }, MessageTarget.Host).IsSuccess, Is.True);
            Pump();
            Assert.That(b.LastChange.Reason, Is.EqualTo(MultiplayerReason.HostLeft));
        }
        #endregion

        #region Message Helpers
        /// <summary>A host and that many clients, all in the lobby.</summary>
        private (Node Host, Node[] Clients) CreateSession(int clientCount)
        {
            Node host = CreateHost("Host", rules: new SessionRules(MultiplayerLimits.MaxCapacity, requireReady: false));
            Node[] clients = new Node[clientCount];
            for (int i = 0; i < clientCount; i++)
            {
                clients[i] = JoinNew(((char)('A' + i)).ToString(), host);
            }

            return (host, clients);
        }

        /// <summary>
        /// A host and two clients in the lobby, whose host leaves the session from its handler of
        /// a player's leave, as a game may, and then does what <paramref name="afterLeaving"/>
        /// does. A test makes the host's next send fail while the connection stays open, as a full
        /// send buffer on Steam does, so the request that sends next drops a player, and that
        /// player's leave ends the session in the middle of the request (#340).
        /// </summary>
        private Node CreateSessionEndedByTheNextDrop(SessionRules rules, Action<Node> afterLeaving = null, bool onPlatform = false)
        {
            Node host = onPlatform
                ? CreateHost(CreatePlatformNode("Host"), PublicOptions(rules))
                : CreateHost("Host", rules: rules);
            JoinNew("A", host);
            JoinNew("B", host);
            host.Manager.OnParticipantLeft += (participant, reason) =>
            {
                host.Manager.Leave();
                afterLeaving?.Invoke(host);
            };

            return host;
        }

        /// <summary>
        /// The host's requests that send to the players, besides StartGame, EndGame, UpdateRules,
        /// SetReady, and Kick, which tests of their own cover; the host's own data and loading
        /// report among them. "SendToAll" sends <see cref="Chat"/> to everyone, "SendToOne" to the
        /// first client.
        /// </summary>
        private static readonly string[] HostRequestsThatSend =
        {
            "ReportLoaded", "SendChat", "SetLobbyData", "DeleteLobbyData", "SetParticipantData", "DeleteParticipantData", "SendToAll", "SendToOne",
        };

        /// <summary>The host's requests that raise an event of their own on the host, or hand it its own copy.</summary>
        private static readonly string[] HostRequestsWithOwnEvents =
        {
            "StartGame", "EndGame", "UpdateRules", "SetReady", "Kick",
            "ReportLoaded", "SendChat", "SetLobbyData", "DeleteLobbyData", "SetParticipantData", "DeleteParticipantData", "SendToAll",
        };

        /// <summary>Takes a host with two clients in the lobby where it may make <paramref name="request"/>.</summary>
        private void PrepareHostRequest(Node host, string request)
        {
            switch (request)
            {
                case "EndGame":
                case "ReportLoaded":
                    Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
                    break;

                case "DeleteLobbyData":
                    Assert.That(host.Manager.SetLobbyData("map", "forest").IsSuccess, Is.True);
                    break;

                case "DeleteParticipantData":
                    Assert.That(host.Manager.SetParticipantData("color", "red").IsSuccess, Is.True);
                    break;
            }

            Pump();
        }

        private static MultiplayerResult MakeHostRequest(Node host, string request)
        {
            switch (request)
            {
                case "StartGame": return new MultiplayerResult(host.Manager.StartGame().Reason);
                case "EndGame": return host.Manager.EndGame();
                case "UpdateRules": return host.Manager.UpdateRules(host.Manager.Rules.WithCapacity(3));
                case "SetReady": return host.Manager.SetReady(true);
                case "Kick": return host.Manager.Kick(host.Manager.Participants[1].Id);
                case "ReportLoaded": return host.Manager.ReportLoaded();
                case "SendChat": return host.Manager.SendChat("Hello");
                case "SetLobbyData": return host.Manager.SetLobbyData("map", "forest");
                case "DeleteLobbyData": return host.Manager.DeleteLobbyData("map");
                case "SetParticipantData": return host.Manager.SetParticipantData("color", "blue");
                case "DeleteParticipantData": return host.Manager.DeleteParticipantData("color");
                case "SendToAll": return host.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.All);
                case "SendToOne": return host.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.To(host.Manager.Participants[1].Id));
                default: throw new ArgumentOutOfRangeException(nameof(request), request, null);
            }
        }

        /// <summary>How many times the host heard the event of its own that <paramref name="request"/> raises.</summary>
        private static int CountOwnEvents(Node host, Inbox inbox, string request)
        {
            switch (request)
            {
                case "ReportLoaded": return host.Changed.Count;
                case "SendChat": return host.Chats.Count;
                case "SetLobbyData":
                case "DeleteLobbyData": return host.LobbyDataChanges.Count;
                case "SetParticipantData":
                case "DeleteParticipantData": return host.DataChanges.Count;
                case "SendToAll": return inbox.Received.Count;

                // A message to another player has no event on the host; the reason is what counts.
                case "SendToOne": return 0;
                default: throw new ArgumentOutOfRangeException(nameof(request), request, null);
            }
        }

        /// <summary>Has the host leave the session from a handler of the event of its own that <paramref name="request"/> raises.</summary>
        private static void LeaveFromOwnEvent(Node host, string request)
        {
            switch (request)
            {
                case "StartGame":
                    host.Manager.OnStateChanged += change =>
                    {
                        if (change.Current == SessionState.InGame)
                        {
                            host.Manager.Leave();
                        }
                    };
                    break;

                case "EndGame":
                    host.Manager.OnStateChanged += change =>
                    {
                        if (change.Current == SessionState.Lobby)
                        {
                            host.Manager.Leave();
                        }
                    };
                    break;

                case "UpdateRules":
                    host.Manager.OnRulesChanged += rules => host.Manager.Leave();
                    break;

                case "SetReady":
                case "ReportLoaded":
                    host.Manager.OnParticipantChanged += participant => host.Manager.Leave();
                    break;

                case "Kick":
                    host.Manager.OnParticipantLeft += (participant, reason) => host.Manager.Leave();
                    break;

                case "SendChat":
                    host.Manager.OnChatReceived += (sender, text) => host.Manager.Leave();
                    break;

                case "SetLobbyData":
                case "DeleteLobbyData":
                    host.Manager.OnLobbyDataChanged += (key, value) => host.Manager.Leave();
                    break;

                case "SetParticipantData":
                case "DeleteParticipantData":
                    host.Manager.OnParticipantDataChanged += (participant, key) => host.Manager.Leave();
                    break;

                case "SendToAll":
                    Assert.That(host.Manager.RegisterMessage(Chat, (context, payload) => host.Manager.Leave()).IsSuccess, Is.True);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(request), request, null);
            }
        }

        private static Inbox Listen(Node node, ushort messageType, MessagePolicy policy = MessagePolicy.Default)
        {
            Inbox inbox = new Inbox();
            MultiplayerResult result = node.Manager.RegisterMessage(
                messageType,
                (context, payload) => inbox.Received.Add((context.Sender, context.DeliveryMode, payload.ToArray())),
                policy);
            Assert.That(result.IsSuccess, Is.True);
            return inbox;
        }

        private static Dictionary<Node, Inbox> ListenAll(ushort messageType, IEnumerable<Node> nodes, MessagePolicy policy = MessagePolicy.Default)
        {
            return nodes.ToDictionary(node => node, node => Listen(node, messageType, policy));
        }

        /// <summary>Sends one message from <paramref name="sender"/> and checks who handled it, then forgets it.</summary>
        private void AssertReceivers(Node sender, MessageTarget target, Node[] everyone, params Node[] expected)
        {
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, everyone);
            Assert.That(sender.Manager.Send(Chat, new byte[] { 42 }, target).IsSuccess, Is.True, target.ToString());
            Pump();

            foreach (Node node in everyone)
            {
                int count = inboxes[node].Received.Count;
                Assert.That(count, Is.EqualTo(expected.Contains(node) ? 1 : 0), $"{target} from {sender.Backend.PlayerName} at {node.Backend.PlayerName}");
                if (count == 1)
                {
                    Assert.That(inboxes[node].Received[0].Sender, Is.EqualTo(sender.Id));
                }
            }

            foreach (Node node in everyone)
            {
                node.Manager.UnregisterMessage(Chat);
            }
        }

        private static byte[] MessageToHostPacket(ushort messageType, MessageTargetKind kind, PlayerId target, byte[] payload)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.MessageToHost);
            writer.WriteByte((byte)DeliveryMode.Reliable);
            writer.WriteByte((byte)kind);
            writer.WriteULong(target.Value);
            writer.WriteUShort(messageType);
            writer.WriteRemaining(payload);
            return writer.Written.ToArray();
        }
        #endregion
    }
}
