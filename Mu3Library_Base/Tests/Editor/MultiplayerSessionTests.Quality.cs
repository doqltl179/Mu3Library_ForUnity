using System;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
// Unity's Is extends NUnit's with AllocatingGCMemory.
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Connection quality (FR-36) and the simulated latency, jitter, and loss of the local network
    /// (FR-40, with FR-32 under loss). The network's clock and the managers' clock are the test's
    /// own, moved one frame of a 60 FPS game at a time, and the simulation is seeded, so every run
    /// loses the same packets.
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        private const double FrameSeconds = 1.0 / 60.0;
        private const int SimulationSeed = 182;

        #region Simulation
        [Test]
        public void LossOf10Percent_Delivers8500To9500Of10000UnreliableMessages()
        {
            // FR-40
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(1);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, clients[0] });
            _network.SimulatedLossPercent = 10f;

            for (int i = 0; i < 10000; i++)
            {
                Assert.That(clients[0].Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.Host, DeliveryMode.Unreliable).IsSuccess, Is.True);
            }

            Advance(0.1);

            int arrived = inboxes[host].Received.Count;
            TestContext.WriteLine($"{arrived} of 10000 unreliable messages arrived at 10% loss.");
            Assert.That(arrived, Is.InRange(8500, 9500));
        }

        [Test]
        public void LossOf20Percent_DeliversEvery1000ReliableMessagesInOrder()
        {
            // FR-32, FR-40: through the host, so each message crosses the lossy network twice.
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, a, b });
            _network.SimulatedLatencyMilliseconds = 30f;
            _network.SimulatedJitterMilliseconds = 10f;
            _network.SimulatedLossPercent = 20f;

            for (int i = 0; i < 1000; i++)
            {
                Assert.That(a.Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.To(b.Id)).IsSuccess, Is.True);
            }

            AdvanceUntil(() => inboxes[b].Received.Count >= 1000, 10.0);
            Advance(1.0);

            List<(PlayerId Sender, DeliveryMode Mode, byte[] Payload)> received = inboxes[b].Received;
            Assert.That(received.Count, Is.EqualTo(1000));
            Assert.That(received.Select(message => BitConverter.ToInt32(message.Payload, 0)), Is.EqualTo(Enumerable.Range(0, 1000)));
            Assert.That(received.All(message => message.Sender == a.Id), Is.True);

            // The loss took reliable packets too; they arrived only because they were sent again.
            Assert.That(host.Manager.TryGetConnectionQuality(a.Id, out ConnectionQuality quality), Is.True);
            TestContext.WriteLine($"1000 of 1000 reliable messages arrived in order at 20% loss; the host measured {quality}.");
            Assert.That(quality.PacketLoss, Is.InRange(0.1f, 0.3f));
        }

        [Test]
        public void OneWayLatencyOf100ms_ReportsAPingOf200ms()
        {
            // FR-36
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(1);
            Node client = clients[0];
            _network.SimulatedLatencyMilliseconds = 100f;

            Advance(2.0);

            Assert.That(host.Manager.TryGetConnectionQuality(client.Id, out ConnectionQuality fromHost), Is.True);
            Assert.That(client.Manager.TryGetConnectionQuality(host.Id, out ConnectionQuality fromClient), Is.True);
            TestContext.WriteLine($"Ping at 100 ms one way: host {fromHost.PingMilliseconds} ms, client {fromClient.PingMilliseconds} ms.");
            Assert.That(fromHost.PingMilliseconds, Is.InRange(180, 220));
            Assert.That(fromClient.PingMilliseconds, Is.InRange(180, 220));
        }

        [Test]
        public void Latency_HoldsMessagesBack_AndJitterReordersOnlyUnreliableOnes()
        {
            // FR-40, FR-32
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(1);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, new[] { host, clients[0] });
            _network.SimulatedLatencyMilliseconds = 100f;
            _network.SimulatedJitterMilliseconds = 40f;

            for (int i = 0; i < 200; i++)
            {
                clients[0].Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.Host, DeliveryMode.Reliable);
                clients[0].Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.Host, DeliveryMode.Unreliable);
            }

            // Before 100 - 40 ms nothing can have arrived, and past 100 + 40 ms everything has.
            Advance(0.04);
            Assert.That(_now, Is.LessThan(0.06));
            Assert.That(inboxes[host].Received, Is.Empty);
            Advance(0.1);

            List<int> reliable = Indices(inboxes[host], DeliveryMode.Reliable);
            List<int> unreliable = Indices(inboxes[host], DeliveryMode.Unreliable);
            Assert.That(reliable, Is.EqualTo(Enumerable.Range(0, 200)));
            Assert.That(unreliable, Is.EquivalentTo(Enumerable.Range(0, 200)));
            Assert.That(unreliable, Is.Not.EqualTo(Enumerable.Range(0, 200)), "jitter lets unreliable messages overtake each other");
        }

        [Test]
        public void UnderLatency_AKickArrivesAfterTheMessagesSentBeforeIt()
        {
            // The backend contract: a close follows the data already sent on its connection.
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(1);
            Node client = clients[0];
            List<SessionState> statesAtArrival = new List<SessionState>();
            client.Manager.RegisterMessage(Chat, (context, payload) => statesAtArrival.Add(client.Manager.State));
            host.Manager.RegisterMessage(Chat, null);
            _network.SimulatedLatencyMilliseconds = 50f;
            _network.SimulatedJitterMilliseconds = 30f;
            _network.SimulatedLossPercent = 20f;

            for (int i = 0; i < 20; i++)
            {
                host.Manager.Send(Chat, new byte[] { (byte)i }, MessageTarget.Others);
            }

            Assert.That(host.Manager.Kick(client.Id).IsSuccess, Is.True);
            Advance(2.0);

            Assert.That(statesAtArrival, Is.EqualTo(Enumerable.Repeat(SessionState.Lobby, 20)));
            Assert.That(client.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(client.LastChange.Reason, Is.EqualTo(MultiplayerReason.Kicked));
        }

        [Test]
        public void UnderSimulation_AHostThatLeaves_DeliversItsLastReliableMessagesFirst()
        {
            // FR-32: the lobby passing on, which the network does not delay, must not overtake the
            // host's last messages and its close, or the clients would end their sessions first.
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(2);
            Dictionary<Node, Inbox> inboxes = ListenAll(Chat, clients);
            _network.SimulatedLatencyMilliseconds = 50f;
            _network.SimulatedJitterMilliseconds = 20f;
            _network.SimulatedLossPercent = 20f;

            for (int i = 0; i < 20; i++)
            {
                Assert.That(host.Manager.Send(Chat, BitConverter.GetBytes(i), MessageTarget.Others).IsSuccess, Is.True);
            }

            host.Manager.Leave();
            Advance(2.0);

            foreach (Node client in clients)
            {
                Assert.That(Indices(inboxes[client], DeliveryMode.Reliable), Is.EqualTo(Enumerable.Range(0, 20)), client.Backend.PlayerName);
                Assert.That(client.Manager.State, Is.EqualTo(SessionState.Offline));
                Assert.That(client.LastChange.Reason, Is.EqualTo(MultiplayerReason.HostLeft));
            }
        }

        [Test]
        public void SimulatedConditions_RefuseValuesOutOfRange()
        {
            LocalNetwork network = new LocalNetwork();
            foreach (float value in new[] { -1f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => network.SimulatedLatencyMilliseconds = value, value.ToString());
                Assert.Throws<ArgumentOutOfRangeException>(() => network.SimulatedJitterMilliseconds = value, value.ToString());
                Assert.Throws<ArgumentOutOfRangeException>(() => network.SimulatedLossPercent = value, value.ToString());
            }

            Assert.Throws<ArgumentOutOfRangeException>(() => network.SimulatedLossPercent = 100.5f);

            network.SimulatedLatencyMilliseconds = 250f;
            network.SimulatedJitterMilliseconds = 30f;
            network.SimulatedLossPercent = 100f;
            Assert.That(network.SimulatedLatencyMilliseconds, Is.EqualTo(250f));
            Assert.That(network.SimulatedJitterMilliseconds, Is.EqualTo(30f));
            Assert.That(network.SimulatedLossPercent, Is.EqualTo(100f));
        }
        #endregion

        #region Quality
        [Test]
        public void Quality_ReportsLossAndBytesPerSecond_OfEachConnection()
        {
            // FR-36
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(1);
            Node client = clients[0];
            ListenAll(Chat, new[] { host, client });

            // The last window measured after the join is a quiet one.
            Advance(1.5);
            Assert.That(host.Manager.TryGetConnectionQuality(client.Id, out ConnectionQuality quiet), Is.True);
            Assert.That(quiet.PingMilliseconds, Is.EqualTo(0), "the network itself adds no delay");
            Assert.That(quiet.PacketLoss, Is.EqualTo(0f));
            Assert.That(quiet.BytesReceivedPerSecond, Is.EqualTo(0f));

            // One unreliable message of 100 bytes a frame, which travels with its 13-byte header.
            _network.SimulatedLossPercent = 10f;
            byte[] payload = new byte[100];
            for (int frame = 0; frame < 240; frame++)
            {
                client.Manager.Send(Chat, payload, MessageTarget.Host, DeliveryMode.Unreliable);
                Advance(FrameSeconds);
            }

            const float SentPerSecond = 113f * 60f;
            Assert.That(client.Manager.TryGetConnectionQuality(host.Id, out ConnectionQuality sending), Is.True);
            Assert.That(host.Manager.TryGetConnectionQuality(client.Id, out ConnectionQuality receiving), Is.True);
            TestContext.WriteLine($"At 10% loss the client measured {sending}, and the host {receiving}.");

            // A window of half a second holds 30 or 31 frames.
            Assert.That(sending.BytesSentPerSecond, Is.InRange(SentPerSecond * 0.9f, SentPerSecond * 1.1f));
            Assert.That(receiving.BytesReceivedPerSecond, Is.InRange(SentPerSecond * 0.7f, SentPerSecond * 1.1f));
            Assert.That(receiving.PacketLoss, Is.InRange(0.02f, 0.2f));
            Assert.That(sending.BytesReceivedPerSecond, Is.EqualTo(0f), "the host sent the client nothing");
        }

        [Test]
        public void Quality_IsOnlyForTheConnectionsThisPlayerHas()
        {
            // A client's traffic goes through the host, so it has no connection of its own to another client.
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            Advance(1.0);

            Assert.That(host.Manager.TryGetConnectionQuality(a.Id, out _), Is.True);
            Assert.That(host.Manager.TryGetConnectionQuality(b.Id, out _), Is.True);
            Assert.That(a.Manager.TryGetConnectionQuality(host.Id, out _), Is.True);

            Assert.That(a.Manager.TryGetConnectionQuality(b.Id, out _), Is.False);
            Assert.That(host.Manager.TryGetConnectionQuality(host.Id, out _), Is.False);
            Assert.That(a.Manager.TryGetConnectionQuality(a.Id, out _), Is.False);
            Assert.That(host.Manager.TryGetConnectionQuality(new PlayerId(987654), out _), Is.False);
            Assert.That(host.Manager.TryGetConnectionQuality(PlayerId.None, out _), Is.False);

            a.Manager.Leave();
            Advance(0.1);
            Assert.That(host.Manager.TryGetConnectionQuality(a.Id, out _), Is.False);
            Assert.That(a.Manager.TryGetConnectionQuality(host.Id, out _), Is.False);
            Assert.That(CreateNode("Offline").Manager.TryGetConnectionQuality(host.Id, out _), Is.False);
        }

        [Test]
        public void ReadingQuality_AndMessagesUnderSimulation_AllocateNothing()
        {
            // NFR-1: the simulated network holds packets back in buffers it reuses.
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(3);
            int handled = 0;
            foreach (Node node in clients.Prepend(host))
            {
                node.Manager.RegisterMessage(Chat, (context, payload) => handled += payload.Length);
            }

            _network.SimulatedLatencyMilliseconds = 20f;
            _network.SimulatedJitterMilliseconds = 5f;
            _network.SimulatedLossPercent = 5f;

            byte[] payload = new byte[256];
            void SendAndDeliver()
            {
                clients[0].Manager.Send(Chat, payload, MessageTarget.Others, DeliveryMode.Unreliable);
                host.Manager.Send(Chat, payload, MessageTarget.All, DeliveryMode.Reliable);
                _now += FrameSeconds;
                Pump(1);
            }

            for (int i = 0; i < 240; i++)
            {
                SendAndDeliver();
            }

            void ReadQuality()
            {
                host.Manager.TryGetConnectionQuality(clients[0].Id, out _);
                clients[0].Manager.TryGetConnectionQuality(host.Id, out _);
            }

            Assert.That(SendAndDeliver, Is.Not.AllocatingGCMemory());
            Assert.That(ReadQuality, Is.Not.AllocatingGCMemory());
            Assert.That(handled, Is.GreaterThan(0));
        }
        #endregion

        #region Simulation Helpers
        /// <summary>Hands the network the test's clock and a fixed seed; the managers already use that clock.</summary>
        private void UseTestNetworkClock()
        {
            _network.Clock = () => _now;
            _network.SeedSimulation(SimulationSeed);
        }

        /// <summary>Runs every player frame by frame, at 60 frames a second, for this long.</summary>
        private void Advance(double seconds)
        {
            double end = _now + seconds;
            while (_now < end)
            {
                _now += FrameSeconds;
                Pump(1);
            }
        }

        private void AdvanceUntil(Func<bool> condition, double seconds)
        {
            double end = _now + seconds;
            while (!condition())
            {
                Assert.That(_now, Is.LessThan(end), $"Nothing happened within {seconds} s of test time.");
                _now += FrameSeconds;
                Pump(1);
            }
        }

        private static List<int> Indices(Inbox inbox, DeliveryMode mode)
        {
            return inbox.Received
                .Where(message => message.Mode == mode)
                .Select(message => BitConverter.ToInt32(message.Payload, 0))
                .ToList();
        }
        #endregion
    }
}
