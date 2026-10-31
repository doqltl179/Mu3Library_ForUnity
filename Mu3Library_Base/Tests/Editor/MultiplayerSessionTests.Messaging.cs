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
            // there handles nothing more.
            (Node host, Node[] clients) = CreateSession(2);
            Inbox inbox = Listen(host, Chat);
            host.Manager.OnParticipantLeft += (participant, reason) => host.Manager.Leave();

            host.Backend.FailingSends = 1;
            Assert.That(host.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.All).IsSuccess, Is.True);

            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(inbox.Received, Is.Empty);
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
