using System;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Multiplayer.Data;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The host's per-client rate limits (FR-37, NFR-10). The managers' clock is the test's own,
    /// moved in steps that never land on the edge of a one-second window, so every run counts the
    /// same packets in the same windows.
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        // 1 s is no whole number of these steps, so no step lands on a window's edge.
        private const double RateStepSeconds = 0.015;

        private static readonly SessionRules LimitedRules = new SessionRules(MultiplayerLimits.MaxCapacity, requireReady: false);

        #region Rate Limits
        [Test]
        public void RateLimit_ClientSending500PerSecond_TheHostHandlesAtMost100PerSecond_AndHearsOfItEverySecond()
        {
            // FR-37
            (Node host, Node[] clients) = CreateLimitedSession(LimitedRules.WithMaxMessagesPerSecond(100), 2);
            Node a = clients[0], b = clients[1];
            List<double> handled = new List<double>();
            host.Manager.RegisterMessage(Chat, (context, payload) => handled.Add(_now));
            Listen(a, Chat);
            Inbox atB = Listen(b, Chat);

            int sent = SendAtRate(a, 500, 3.0, MessageTarget.All);
            Pump();

            Assert.That(sent, Is.EqualTo(1500));
            double first = handled[0];
            for (int window = 0; window < 3; window++)
            {
                int inWindow = handled.Count(time => time >= first + window && time < first + window + 1);
                Assert.That(inWindow, Is.EqualTo(100), $"window {window}");
            }

            Assert.That(handled.Count, Is.EqualTo(300));
            Assert.That(atB.Received.Count, Is.EqualTo(300), "the host passes on only what it takes");
            Assert.That(host.RateLimited.Select(participant => participant.Id), Is.EqualTo(new[] { a.Id, a.Id, a.Id }));
            Assert.That(a.RateLimited.Concat(b.RateLimited), Is.Empty, "only the host hears of it");
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby), "without a kick time nobody is kicked");
        }

        [Test]
        public void RateLimit_ByteLimit_DropsWhatWouldTakeTheWindowPastIt_AndOneLargerMessageNeverGetsThrough()
        {
            // FR-37: the bytes count the packets as they arrive, headers included.
            int packetBytes = MessageToHostPacket(Chat, MessageTargetKind.Host, PlayerId.None, new byte[100]).Length;
            (Node host, Node[] clients) = CreateLimitedSession(LimitedRules.WithMaxBytesPerSecond(packetBytes * 8 + packetBytes / 2), 1);
            Node a = clients[0];
            Inbox atHost = Listen(host, Chat);

            for (int i = 0; i < 20; i++)
            {
                Assert.That(a.Manager.Send(Chat, new byte[100], MessageTarget.Host).IsSuccess, Is.True);
            }

            Pump();
            Assert.That(atHost.Received.Count, Is.EqualTo(8));
            Assert.That(host.RateLimited.Count, Is.EqualTo(1));

            AdvanceBy(1.5);
            a.Manager.Send(Chat, new byte[packetBytes * 9], MessageTarget.Host);
            a.Manager.Send(Chat, new byte[1], MessageTarget.Host);
            Pump();

            Assert.That(atHost.Received.Select(message => message.Payload.Length).Skip(8), Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void RateLimit_CountsChatReadyAndDataRequests_ButDropsOnlyGameAndChatMessages()
        {
            // FR-37, FR-10, FR-11, FR-12: a data request past the limit is taken, so the client's
            // own check, which builds on every request it sent, keeps agreeing with the host.
            (Node host, Node[] clients) = CreateLimitedSession(LimitedRules.WithMaxMessagesPerSecond(2), 2);
            Node a = clients[0], b = clients[1];
            Node[] everyone = { host, a, b };
            Inbox atHost = Listen(host, Chat);
            string large = new string('x', 1500);

            Assert.That(a.Manager.SetParticipantData("first", large).IsSuccess, Is.True);
            Pump();
            AdvanceBy(1.5);

            Assert.That(a.Manager.SendChat("one").IsSuccess, Is.True);
            Assert.That(a.Manager.SendChat("two").IsSuccess, Is.True);
            Assert.That(a.Manager.SetParticipantData("first", string.Empty).IsSuccess, Is.True);
            Assert.That(a.Manager.SetParticipantData("second", large).IsSuccess, Is.True, "fits only after the request before it");
            Assert.That(a.Manager.SendChat("three").IsSuccess, Is.True);
            Assert.That(a.Manager.SetReady(true).IsSuccess, Is.True);
            Assert.That(a.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host).IsSuccess, Is.True);
            Pump();

            Dictionary<string, string> expected = new Dictionary<string, string> { ["first"] = string.Empty, ["second"] = large };
            foreach (Node node in everyone)
            {
                Assert.That(node.Chats.Select(chat => chat.Text), Is.EqualTo(new[] { "one", "two" }), node.Backend.PlayerName);
                Assert.That(DataOf(node, a.Id), Is.EquivalentTo(expected), node.Backend.PlayerName);
                Assert.That(node.Manager.TryGetParticipant(a.Id, out Participant player) && player.IsReady, Is.True, node.Backend.PlayerName);
            }

            Assert.That(atHost.Received, Is.Empty);
            Assert.That(host.RateLimited.Select(participant => participant.Id), Is.EqualTo(new[] { a.Id }), "once in the window");
        }

        [Test]
        public void RateLimit_ExcessForTheKickTime_KicksWithRateLimited_AndTheAccountCannotRejoin()
        {
            // FR-37, FR-22
            (Node host, Node[] clients) = CreateLimitedSession(LimitedRules.WithMaxMessagesPerSecond(10).WithRateLimitKickSeconds(2f), 2);
            Node a = clients[0], b = clients[1];
            Listen(host, Chat);
            List<double> exceeded = new List<double>();
            double kickedAt = double.NaN;
            host.Manager.OnRateLimitExceeded += participant => exceeded.Add(_now);
            host.Manager.OnParticipantLeft += (participant, reason) => kickedAt = _now;

            SendAtRate(a, 30, 3.0, MessageTarget.Host, () => a.Manager.State == SessionState.Offline);
            Pump();

            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.RateLimited));
            Assert.That(kickedAt - exceeded[0], Is.InRange(2.0, 2.1));
            foreach (Node remaining in new[] { host, b })
            {
                Assert.That(remaining.Left.Single().Participant.Id, Is.EqualTo(a.Id), remaining.Backend.PlayerName);
                Assert.That(remaining.Left.Single().Reason, Is.EqualTo(MultiplayerReason.RateLimited), remaining.Backend.PlayerName);
            }

            Assert.That(Join(a, host).Reason, Is.EqualTo(MultiplayerReason.Banned));
        }

        [Test]
        public void RateLimit_AWindowWithinTheLimits_StartsTheKickTimeAgain()
        {
            // FR-37
            (Node host, Node[] clients) = CreateLimitedSession(LimitedRules.WithMaxMessagesPerSecond(10).WithRateLimitKickSeconds(2f), 1);
            Node a = clients[0];
            Listen(host, Chat);

            SendAtRate(a, 30, 1.5, MessageTarget.Host);
            SendAtRate(a, 5, 1.6, MessageTarget.Host);
            SendAtRate(a, 30, 1.5, MessageTarget.Host);
            Pump();

            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Lobby));
            Assert.That(host.Left, Is.Empty);
            Assert.That(host.RateLimited.Count, Is.GreaterThanOrEqualTo(3));

            SendAtRate(a, 30, 3.0, MessageTarget.Host, () => a.Manager.State == SessionState.Offline);
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.RateLimited));
        }

        [Test]
        public void RateLimit_Rules_RefuseNegativeOrNonFiniteValues_ReachEveryPlayer_AndSpareTheHost()
        {
            // FR-37, FR-17
            (Node host, Node[] clients) = CreateLimitedSession(LimitedRules, 1);
            Node a = clients[0];

            foreach (SessionRules invalid in new[]
            {
                LimitedRules.WithMaxMessagesPerSecond(-1),
                LimitedRules.WithMaxBytesPerSecond(-1),
                LimitedRules.WithRateLimitKickSeconds(-1f),
                LimitedRules.WithRateLimitKickSeconds(float.NaN),
                LimitedRules.WithRateLimitKickSeconds(float.PositiveInfinity),
            })
            {
                Assert.That(host.Manager.UpdateRules(invalid).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), invalid.ToString());
                Assert.That(CreateLobby(CreateNode("Other"), PublicOptions(invalid)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), invalid.ToString());
            }

            SessionRules limited = LimitedRules.WithMaxMessagesPerSecond(3).WithMaxBytesPerSecond(4096).WithRateLimitKickSeconds(1.5f);
            Assert.That(host.Manager.UpdateRules(limited).IsSuccess, Is.True);
            Pump();
            Node late = JoinNew("Late", host);

            foreach (Node node in new[] { a, late })
            {
                Assert.That(node.Manager.Rules.MaxMessagesPerSecond, Is.EqualTo(3), node.Backend.PlayerName);
                Assert.That(node.Manager.Rules.MaxBytesPerSecond, Is.EqualTo(4096), node.Backend.PlayerName);
                Assert.That(node.Manager.Rules.RateLimitKickSeconds, Is.EqualTo(1.5f), node.Backend.PlayerName);
            }

            Inbox atA = Listen(a, Chat);
            Listen(host, Chat);
            for (int i = 0; i < 20; i++)
            {
                Assert.That(host.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.All).IsSuccess, Is.True);
            }

            Pump();
            Assert.That(atA.Received.Count, Is.EqualTo(20), "the host's own messages are never limited");
        }
        #endregion

        #region Rate Limit Helpers
        /// <summary>A host with these rules and that many clients, all in the lobby.</summary>
        private (Node Host, Node[] Clients) CreateLimitedSession(SessionRules rules, int clientCount)
        {
            Node host = CreateHost("Host", rules: rules);
            Node[] clients = new Node[clientCount];
            for (int i = 0; i < clientCount; i++)
            {
                clients[i] = JoinNew(((char)('A' + i)).ToString(), host);
            }

            return (host, clients);
        }

        /// <summary>
        /// Has <paramref name="sender"/> send one-byte messages at this rate for this long, or
        /// until <paramref name="stop"/> holds, while every player runs in steps of
        /// <see cref="RateStepSeconds"/>. Returns how many it sent.
        /// </summary>
        private int SendAtRate(Node sender, int perSecond, double seconds, MessageTarget target, Func<bool> stop = null)
        {
            double start = _now;
            int steps = (int)Math.Round(seconds / RateStepSeconds);
            int sent = 0;
            byte[] payload = { 1 };
            for (int step = 1; step <= steps && (stop == null || !stop()); step++)
            {
                _now = start + step * RateStepSeconds;
                int due = (int)Math.Round(step * RateStepSeconds * perSecond);
                for (; sent < due && sender.Manager.State == SessionState.Lobby; sent++)
                {
                    Assert.That(sender.Manager.Send(Chat, payload, target).IsSuccess, Is.True);
                }

                Pump(1);
            }

            return sent;
        }

        /// <summary>Runs every player in steps of <see cref="RateStepSeconds"/> for this long.</summary>
        private void AdvanceBy(double seconds)
        {
            double start = _now;
            int steps = (int)Math.Round(seconds / RateStepSeconds);
            for (int step = 1; step <= steps; step++)
            {
                _now = start + step * RateStepSeconds;
                Pump(1);
            }
        }
        #endregion
    }
}
