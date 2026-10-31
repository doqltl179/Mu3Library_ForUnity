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
    /// The host's wait for every player to be ready to play after a game starts (FR-25, NFR-10),
    /// on the test's clock.
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        #region Load Sync
        [Test]
        public void LoadSync_FourPlayersAndThreeReport_After30SecondsTheHostHearsOfTheOneMissing()
        {
            // FR-25
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false).WithLoadTimeoutSeconds(30f));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            Node c = JoinNew("C", host);
            Node[] everyone = { host, a, b, c };
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();

            Assert.That(host.Manager.ReportLoaded().IsSuccess, Is.True);
            Assert.That(a.Manager.ReportLoaded().IsSuccess, Is.True);
            Assert.That(b.Manager.ReportLoaded().IsSuccess, Is.True);
            Advance(29.5);
            Assert.That(host.LoadTimeouts, Is.Empty);

            Advance(1.0);
            Assert.That(host.LoadTimeouts, Is.EqualTo(new[] { new[] { c.Id } }));
            Assert.That(host.AllLoadedCount, Is.Zero);
            foreach (Node node in everyone)
            {
                Assert.That(node.Manager.Participants.Select(participant => participant.IsLoaded), Is.EqualTo(new[] { true, true, true, false }), node.Backend.PlayerName);
            }

            // A late report still shows, but the wait is over.
            Assert.That(c.Manager.ReportLoaded().IsSuccess, Is.True);
            Advance(1.0);
            foreach (Node node in everyone)
            {
                Assert.That(node.Manager.Participants.All(participant => participant.IsLoaded), Is.True, node.Backend.PlayerName);
                Assert.That(node.Changed.Any(participant => participant.Id == c.Id && participant.IsLoaded), Is.True, node.Backend.PlayerName);
            }

            Assert.That(host.LoadTimeouts.Count, Is.EqualTo(1));
            Assert.That(host.AllLoadedCount, Is.Zero);
            Assert.That(a.LoadTimeouts.Concat(b.LoadTimeouts).Concat(c.LoadTimeouts), Is.Empty, "only the host waits");
        }

        [Test]
        public void LoadSync_EveryPlayerReports_TheHostHearsItOnce_AndNoTimeoutFollows()
        {
            // FR-25
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();

            Assert.That(a.Manager.ReportLoaded().IsSuccess, Is.True);
            Assert.That(host.Manager.ReportLoaded().IsSuccess, Is.True);
            Pump();
            Assert.That(host.AllLoadedCount, Is.Zero);

            Assert.That(b.Manager.ReportLoaded().IsSuccess, Is.True);
            Assert.That(a.Manager.ReportLoaded().IsSuccess, Is.True, "reporting again changes nothing");
            Pump();
            Assert.That(host.AllLoadedCount, Is.EqualTo(1));
            Assert.That(a.Changed.Count(participant => participant.Id == a.Id && participant.IsLoaded), Is.EqualTo(1));

            Advance(SessionRules.DefaultLoadTimeoutSeconds + 1.0);
            Assert.That(host.AllLoadedCount, Is.EqualTo(1));
            Assert.That(host.LoadTimeouts, Is.Empty);
            Assert.That(a.AllLoadedCount + b.AllLoadedCount, Is.Zero);
        }

        [Test]
        public void LoadSync_APlayerLeaving_OrTheReturnToTheLobby_ChangesWhatTheHostWaitsFor()
        {
            // FR-25, FR-26
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false).WithLoadTimeoutSeconds(30f));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            host.Manager.StartGame();
            Pump();

            host.Manager.ReportLoaded();
            a.Manager.ReportLoaded();
            Pump();
            b.Manager.Leave();
            Pump();
            Assert.That(host.AllLoadedCount, Is.EqualTo(1), "nobody left to wait for");

            Assert.That(host.Manager.EndGame().IsSuccess, Is.True);
            Pump();
            Node c = JoinNew("C", host);
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();
            foreach (Node node in new[] { host, a, c })
            {
                Assert.That(node.Manager.Participants.Any(participant => participant.IsLoaded), Is.False, $"every flag starts over at {node.Backend.PlayerName}");
            }

            a.Manager.ReportLoaded();
            Pump();
            Assert.That(host.Manager.EndGame().IsSuccess, Is.True, "returning to the lobby ends the wait");
            Pump();
            Advance(31.0);
            Assert.That(host.LoadTimeouts, Is.Empty);
            Assert.That(host.AllLoadedCount, Is.EqualTo(1));
            Assert.That(a.Manager.Participants.Any(participant => participant.IsLoaded), Is.False);
            Assert.That(a.Manager.ReportLoaded().Reason, Is.EqualTo(MultiplayerReason.InvalidState));

            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();
            Advance(31.0);
            Assert.That(host.LoadTimeouts, Is.EqualTo(new[] { new[] { host.Id, a.Id, c.Id } }));
        }

        [Test]
        public void LoadSync_ALateJoinerIsNotWaitedFor_APlayerAwayIs_AndAReportBeforeADropStays()
        {
            // FR-25, FR-19, FR-20
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false, allowLateJoin: true).WithLoadTimeoutSeconds(30f));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            host.Manager.StartGame();
            Pump();

            Assert.That(host.Manager.ReportLoaded().IsSuccess, Is.True);
            Assert.That(a.Manager.ReportLoaded().IsSuccess, Is.True);
            Pump();
            a.Backend.SimulateOutage(15f);
            b.Backend.SimulateOutage(60f);
            Advance(DropSeconds + 0.5);
            Assert.That(host.Disconnected.Select(participant => participant.Id), Is.EquivalentTo(new[] { a.Id, b.Id }));
            Assert.That(a.Manager.ReportLoaded().Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));

            Node late = JoinNew("Late", host);
            Assert.That(late.Manager.State, Is.EqualTo(SessionState.InGame));
            Advance(6.0);
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(a.Manager.TryGetParticipant(a.Id, out Participant self) && self.IsLoaded, Is.True, "a player who reported before the drop stays loaded");

            Advance(14.0);
            Assert.That(host.LoadTimeouts, Is.EqualTo(new[] { new[] { b.Id } }), "the player away is waited for; the late joiner is not");
            Assert.That(host.AllLoadedCount, Is.Zero);

            Assert.That(late.Manager.ReportLoaded().IsSuccess, Is.True);
            Pump();
            Assert.That(host.Manager.TryGetParticipant(late.Id, out Participant loaded) && loaded.IsLoaded, Is.True, "a late joiner can report all the same");
        }

        [Test]
        public void LoadSync_APlayerWhoseSlotFrees_IsNoLongerWaitedFor()
        {
            // FR-25, FR-20
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false).WithLoadTimeoutSeconds(100f).WithReconnectReservationSeconds(20f));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            host.Manager.StartGame();
            Pump();

            host.Manager.ReportLoaded();
            b.Manager.ReportLoaded();
            a.Backend.SimulateOutage(120f);
            Advance(DropSeconds + 0.5);
            Assert.That(host.AllLoadedCount, Is.Zero);

            Advance(20.0);
            Assert.That(host.Left.Single().Reason, Is.EqualTo(MultiplayerReason.ConnectionLost));
            Assert.That(host.AllLoadedCount, Is.EqualTo(1));
            Assert.That(host.LoadTimeouts, Is.Empty);
        }

        [Test]
        public void LoadSync_RefusedOutsideAGame_IgnoresAReportForAnotherGame_AndPassesTheRateLimit()
        {
            // FR-25, FR-37, NFR-5
            Assert.That(new MultiplayerManager().ReportLoaded().Reason, Is.EqualTo(MultiplayerReason.NotInitialized));
            Assert.That(CreateNode("Idle").Manager.ReportLoaded().Reason, Is.EqualTo(MultiplayerReason.InvalidState));

            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false).WithMaxMessagesPerSecond(1));
            Node a = JoinNew("A", host);
            Assert.That(host.Manager.ReportLoaded().Reason, Is.EqualTo(MultiplayerReason.InvalidState));
            Assert.That(a.Manager.ReportLoaded().Reason, Is.EqualTo(MultiplayerReason.InvalidState));

            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();

            raw.Send(connection, ReportLoadedPacket(0), DeliveryMode.Reliable);
            raw.Send(connection, ReportLoadedPacket(2), DeliveryMode.Reliable);
            Pump();
            Assert.That(host.Manager.TryGetParticipant(raw.LocalPlayerId, out Participant stale) && stale.IsLoaded, Is.False);

            raw.Send(connection, ReportLoadedPacket(1), DeliveryMode.Reliable);
            Pump();
            Assert.That(host.Manager.TryGetParticipant(raw.LocalPlayerId, out Participant current) && current.IsLoaded, Is.True);

            // Past the limit of one packet a second, a report counts but still gets through.
            Listen(host, Chat);
            Assert.That(a.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.Host).IsSuccess, Is.True);
            Assert.That(a.Manager.ReportLoaded().IsSuccess, Is.True);
            Pump();
            Assert.That(host.Manager.TryGetParticipant(a.Id, out Participant limited) && limited.IsLoaded, Is.True);
            Assert.That(host.RateLimited.Select(participant => participant.Id), Is.EqualTo(new[] { raw.LocalPlayerId, a.Id }));
        }

        [Test]
        public void LoadSync_Rules_RefuseANegativeOrNonFiniteTimeout_AndReachEveryPlayer()
        {
            // FR-25
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);
            SessionRules rules = host.Manager.Rules;

            Assert.That(rules.LoadTimeoutSeconds, Is.EqualTo(SessionRules.DefaultLoadTimeoutSeconds));
            Assert.That(a.Manager.Rules.ReconnectReservationSeconds, Is.EqualTo(SessionRules.DefaultReconnectReservationSeconds));
            Assert.That(host.Manager.UpdateRules(rules.WithLoadTimeoutSeconds(-1f)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.UpdateRules(rules.WithLoadTimeoutSeconds(float.NaN)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.UpdateRules(rules.WithReconnectReservationSeconds(float.PositiveInfinity)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));

            Assert.That(host.Manager.UpdateRules(rules.WithLoadTimeoutSeconds(0f).WithReconnectReservationSeconds(5f)).IsSuccess, Is.True);
            Pump();
            Assert.That(a.Manager.Rules.LoadTimeoutSeconds, Is.Zero);
            Assert.That(a.Manager.Rules.ReconnectReservationSeconds, Is.EqualTo(5f));

            // Without a time limit the host waits as long as it takes.
            host.Manager.StartGame();
            Pump();
            Advance(2.0 * SessionRules.DefaultLoadTimeoutSeconds);
            Assert.That(host.LoadTimeouts, Is.Empty);
            a.Manager.ReportLoaded();
            host.Manager.ReportLoaded();
            Pump();
            Assert.That(host.AllLoadedCount, Is.EqualTo(1));
        }
        #endregion

        #region Load Sync Helpers
        private static byte[] ReportLoadedPacket(int gameNumber)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ReportLoaded).WriteInt(gameNumber);
            return writer.Written.ToArray();
        }
        #endregion
    }
}
