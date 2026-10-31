using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using NUnit.Framework;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The load of NFR-1 in docs/specs/steam-player-hosted-multiplayer.md on the local backend:
    /// a full session of 16 players, every client sending 30 unreliable messages of 256 bytes a
    /// second and the host 30 reliable messages of 1 KB a second to everyone, at 60 frames a
    /// second, each run once with the clients' messages going to the host only and once to every
    /// other player through the host. It times only the host's own calls, its manager's update
    /// (which updates its backend) and its sends, and counts what they allocate; it also checks
    /// NFR-2, that each message reaches its handler in the frame after it was sent at the latest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It asserts what does not depend on the machine, no allocation in the steady state and no
    /// message handled later than the frame after the one it was sent in (one frame more on a
    /// machine-wide network, for the loopback), and only logs the timing against the 0.5 ms
    /// target, in a line that starts with <c>[NFR-1]</c>, in the Unity log and in the test's
    /// output in the results file.
    /// </para>
    /// <para>
    /// On a network inside this process the players run frame by frame on a test clock as fast
    /// as they can, about a second for each case, so these cases run with every other test. On a
    /// machine-wide network every player has a network object of its own, as a separate process
    /// would, the frames keep to 60 a second in real time, and the timing includes the loopback
    /// sockets; those cases take about 40 seconds each, so <see cref="ExplicitAttribute"/> keeps
    /// them out of a normal run. Run them by name: in this repository with <c>bash
    /// ./test-unity.sh built-in --filter Mu3Library.Tests.MultiplayerPerformanceTests</c>,
    /// elsewhere with the Unity Test Framework's <c>-testFilter</c>. The players share this
    /// thread, so a garbage collection that another player's
    /// allocation starts can land in a host frame.
    /// </para>
    /// </remarks>
    [Category("Performance")]
    public class MultiplayerPerformanceTests
    {
        /// <summary>Who the clients' messages go to.</summary>
        public enum ClientRoute
        {
            /// <summary>To the host only.</summary>
            ToHost,
            /// <summary>To every other player, which the host relays to the other clients.</summary>
            ToOthers,
        }

        private const string GameId = "tests.multiplayer.performance";
        private const ushort ClientMessage = 1;
        private const ushort HostMessage = 2;
        private const int Players = MultiplayerLimits.MaxCapacity;
        private const int FramesPerSecond = 60;
        private const double FrameSeconds = 1.0 / FramesPerSecond;

        // 30 a second at 60 frames a second: every second frame, the clients in two halves.
        private const int SendEveryFrames = 2;
        private const int ClientPayloadBytes = 256;
        private const int HostPayloadBytes = 1024;
        private const double TargetMilliseconds = 0.5;

        // The report counts the host frames that took longer than this, such as one that waited for a file.
        private const double LongFrameMilliseconds = 2.0;

        private const int WarmupFrames = 4 * FramesPerSecond;
        private const int InProcessFrames = 60 * FramesPerSecond;
        private const int MachineWideFrames = 30 * FramesPerSecond;
        private const double SettleSeconds = 15.0;

        private sealed class Player
        {
            public LocalNetwork Network;
            public LocalBackend Backend;
            public MultiplayerManager Manager;
            public long Sent;
            public long ReceivedFromClients;
            public long ReceivedFromHost;
            public int MaxLateFrames;
            public long LateMessages;
            public bool IsInGame;
            public bool HasLeftOrDropped;
        }

        /// <summary>The host's numbers for each measured frame.</summary>
        private sealed class Samples
        {
            public readonly double[] UpdateMilliseconds;
            public readonly double[] SendMilliseconds;
            public readonly int[] UpdateAllocations;
            public readonly int[] SendAllocations;
            public readonly long[] UpdateBytes;
            public readonly long[] SendBytes;
            public int Count;

            public Samples(int frames)
            {
                UpdateMilliseconds = new double[frames];
                SendMilliseconds = new double[frames];
                UpdateAllocations = new int[frames];
                SendAllocations = new int[frames];
                UpdateBytes = new long[frames];
                SendBytes = new long[frames];
            }
        }

        private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;

        private readonly List<Player> _players = new List<Player>();
        private readonly Stopwatch _realClock = new Stopwatch();
        private Player _host;
        private LocalNetwork _inProcessNetwork;
        private bool _isRealTime;
        private double _now;
        private double _nextFrameAt;
        private int _frame;
        private string _folder;
        private ClientRoute _route;
        private byte[] _clientPayload;
        private byte[] _hostPayload;
        private Recorder _allocations;
        private bool _hasAllocatedBytes;



        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "Mu3LibraryTests", Guid.NewGuid().ToString("N"));
            _clientPayload = new byte[ClientPayloadBytes];
            _hostPayload = new byte[HostPayloadBytes];
            // NUnit runs every case on one instance of the fixture.
            _realClock.Restart();
            _nextFrameAt = 0.0;
            _now = 0.0;
            _frame = 0;
            _allocations = Recorder.Get("GC.Alloc");
            _allocations.enabled = false;
            _hasAllocatedBytes = ProbeAllocatedBytes();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Player player in _players)
            {
                player.Manager.Dispose();
                player.Backend.Dispose();
            }

            _players.Clear();
            _host = null;
            _allocations.enabled = false;
            _allocations.CollectFromAllThreads();

            try
            {
                Directory.Delete(_folder, recursive: true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Nobody owned a lobby, or another program still holds a file; the folder is temporary.
            }
        }

        [TestCase(ClientRoute.ToHost)]
        [TestCase(ClientRoute.ToOthers)]
        public void InProcessNetwork_UnderTheNfr1Load(ClientRoute route)
        {
            _isRealTime = false;
            _inProcessNetwork = new LocalNetwork { Clock = () => _now };
            Run("InProcess", route, InProcessFrames);
        }

        [Explicit("Runs about 40 seconds of real time for each case; run it by name.")]
        [TestCase(ClientRoute.ToHost)]
        [TestCase(ClientRoute.ToOthers)]
        public void MachineWideNetwork_UnderTheNfr1Load(ClientRoute route)
        {
            _isRealTime = true;
            _inProcessNetwork = null;
            Run("MachineWide", route, MachineWideFrames);
        }

        #region Benchmark
        private void Run(string mode, ClientRoute route, int measuredFrames)
        {
            _route = route;
            AssertTheAllocationRecorderWorks();
            StartFullSession();

            for (int i = 0; i < WarmupFrames; i++)
            {
                RunFrame(send: true, samples: null);
            }

            ResetCounters();
            Samples samples = new Samples(measuredFrames);
            double measuredFrom = Now;
            for (int i = 0; i < measuredFrames; i++)
            {
                RunFrame(send: true, samples);
            }

            double measuredSeconds = Now - measuredFrom;

            // What is still on its way arrives once nobody sends any more.
            RunFramesUntil(EveryMessageArrived, "every message sent while measuring to arrive", seconds: 5.0);

            string report = Report(mode, route, samples, measuredSeconds);
            Debug.Log(report);
            TestContext.Out.WriteLine(report);

            Assert.That(_players.TrueForAll(player => !player.HasLeftOrDropped), Is.True, "A player left or dropped during the run.");
            Assert.That(_host.Manager.Participants.Count, Is.EqualTo(Players));
            for (int i = 0; i < _players.Count; i++)
            {
                Player player = _players[i];
                Assert.That(player.LateMessages, Is.Zero, $"NFR-2: {player.LateMessages} messages reached player {i} more than {AllowedLateFrames} frames after the frame they were sent in.");
            }

            Assert.That(SumOf(samples.UpdateAllocations, samples), Is.Zero, "NFR-1: the host's update allocated in the steady state.");
            Assert.That(SumOf(samples.SendAllocations, samples), Is.Zero, "NFR-1: the host's sends allocated in the steady state.");
        }

        /// <summary>
        /// One frame of every player: the host's manager updates and its game code sends, both
        /// measured; then each client sends and updates, unmeasured.
        /// </summary>
        private void RunFrame(bool send, Samples samples)
        {
            _frame++;
            if (!_isRealTime)
            {
                _now += FrameSeconds;
            }

            long allocatedBefore = AllocatedBytes();
            _allocations.FilterToCurrentThread();
            _allocations.enabled = true;
            long start = Stopwatch.GetTimestamp();
            _host.Manager.Update();
            long updated = Stopwatch.GetTimestamp();
            _allocations.enabled = false;
            int updateAllocations = _allocations.sampleBlockCount;
            long allocatedAfterUpdate = AllocatedBytes();

            _allocations.FilterToCurrentThread();
            _allocations.enabled = true;
            long sendStart = Stopwatch.GetTimestamp();
            if (send && _frame % SendEveryFrames == 0)
            {
                Send(_host, HostMessage, _hostPayload, MessageTarget.All, DeliveryMode.Reliable);
            }

            long sent = Stopwatch.GetTimestamp();
            _allocations.enabled = false;
            int sendAllocations = _allocations.sampleBlockCount;
            long allocatedAfterSend = AllocatedBytes();

            if (samples != null)
            {
                int i = samples.Count++;
                samples.UpdateMilliseconds[i] = (updated - start) * MillisecondsPerTick;
                samples.SendMilliseconds[i] = (sent - sendStart) * MillisecondsPerTick;
                samples.UpdateAllocations[i] = updateAllocations;
                samples.SendAllocations[i] = sendAllocations;
                samples.UpdateBytes[i] = allocatedAfterUpdate - allocatedBefore;
                samples.SendBytes[i] = allocatedAfterSend - allocatedAfterUpdate;
            }

            MessageTarget clientTarget = _route == ClientRoute.ToHost ? MessageTarget.Host : MessageTarget.Others;
            for (int i = 1; i < _players.Count; i++)
            {
                Player client = _players[i];
                if (send && (_frame + i) % SendEveryFrames == 0)
                {
                    Send(client, ClientMessage, _clientPayload, clientTarget, DeliveryMode.Unreliable);
                }

                client.Manager.Update();
            }

            WaitForTheNextFrame();
        }

        private void Send(Player sender, ushort messageType, byte[] payload, MessageTarget target, DeliveryMode mode)
        {
            BinaryPrimitives.WriteInt32LittleEndian(payload, _frame);
            MultiplayerResult result = sender.Manager.Send(messageType, payload, target, mode);
            if (result.IsSuccess)
            {
                sender.Sent++;
            }
        }

        private void Handle(Player receiver, ushort messageType, ReadOnlySpan<byte> payload)
        {
            int late = _frame - BinaryPrimitives.ReadInt32LittleEndian(payload);
            if (messageType == ClientMessage)
            {
                receiver.ReceivedFromClients++;
            }
            else
            {
                receiver.ReceivedFromHost++;
            }

            if (late > receiver.MaxLateFrames)
            {
                receiver.MaxLateFrames = late;
            }

            if (late > AllowedLateFrames)
            {
                receiver.LateMessages++;
            }
        }

        private void ResetCounters()
        {
            foreach (Player player in _players)
            {
                player.Sent = 0;
                player.ReceivedFromClients = 0;
                player.ReceivedFromHost = 0;
                player.MaxLateFrames = 0;
                player.LateMessages = 0;
            }
        }

        private bool EveryMessageArrived()
        {
            long clientsSent = 0;
            for (int i = 1; i < _players.Count; i++)
            {
                clientsSent += _players[i].Sent;
            }

            for (int i = 0; i < _players.Count; i++)
            {
                Player player = _players[i];
                long fromClients = i == 0 ? clientsSent : _route == ClientRoute.ToOthers ? clientsSent - player.Sent : 0;
                if (player.ReceivedFromClients < fromClients || player.ReceivedFromHost < _host.Sent)
                {
                    return false;
                }
            }

            return true;
        }
        #endregion

        #region Session
        /// <summary>A public lobby of 16 with every player in it, the game started, and every player loaded.</summary>
        private void StartFullSession()
        {
            _host = CreatePlayer(0);
            MultiplayerResult? created = null;
            _host.Manager.CreateLobby(new LobbyCreateOptions
            {
                Visibility = LobbyVisibility.Public,
                Name = "Benchmark",
                Rules = new SessionRules(Players, requireReady: false),
            }, result => created = result);
            RunFramesUntil(() => created.HasValue, "the lobby");
            Assert.That(created.Value.IsSuccess, Is.True, created.Value.ToString());

            for (int i = 1; i < Players; i++)
            {
                Player client = CreatePlayer(i);
                MultiplayerResult? joined = null;
                client.Manager.JoinLobby(_host.Manager.CurrentLobbyId, result => joined = result);
                RunFramesUntil(() => joined.HasValue, $"client {i} to join");
                Assert.That(joined.Value.IsSuccess, Is.True, $"client {i}: {joined.Value}");
            }

            RunFramesUntil(() => _players.TrueForAll(player => player.Manager.Participants.Count == Players), "every roster to list every player");

            Assert.That(_host.Manager.StartGame().IsSuccess, Is.True);
            RunFramesUntil(() => _players.TrueForAll(player => player.IsInGame), "every player to start the game");

            bool isLoaded = false;
            _host.Manager.OnAllPlayersLoaded += () => isLoaded = true;
            foreach (Player player in _players)
            {
                Assert.That(player.Manager.ReportLoaded().IsSuccess, Is.True);
            }

            RunFramesUntil(() => isLoaded, "every player to load");
        }

        private Player CreatePlayer(int index)
        {
            Player player = new Player();
            string name = index == 0 ? "Host" : "Client" + index.ToString(CultureInfo.InvariantCulture);
            if (_isRealTime)
            {
                player.Network = LocalNetwork.CreateMachineWide("performance");
                player.Network.MachineFolder = _folder;
            }
            else
            {
                player.Network = _inProcessNetwork;
            }

            player.Backend = new LocalBackend(player.Network, name);
            player.Manager = new MultiplayerManager();
            MultiplayerConfig config = new MultiplayerConfig(GameId, 1) { Clock = () => Now };
            Assert.That(player.Manager.Initialize(player.Backend, config).IsSuccess, Is.True);

            player.Manager.OnStateChanged += change => player.IsInGame = change.Current == SessionState.InGame;
            player.Manager.OnParticipantLeft += (participant, reason) => player.HasLeftOrDropped = true;
            player.Manager.OnParticipantDisconnected += participant => player.HasLeftOrDropped = true;
            Assert.That(player.Manager.RegisterMessage(ClientMessage, (context, payload) => Handle(player, ClientMessage, payload)).IsSuccess, Is.True);
            Assert.That(player.Manager.RegisterMessage(HostMessage, (context, payload) => Handle(player, HostMessage, payload)).IsSuccess, Is.True);

            _players.Add(player);
            return player;
        }

        private void RunFramesUntil(Func<bool> condition, string what, double seconds = SettleSeconds)
        {
            double end = Now + seconds;
            while (!condition())
            {
                Assert.That(Now, Is.LessThan(end), $"Timed out after {seconds} s waiting for {what}.");
                RunFrame(send: false, samples: null);
            }
        }
        #endregion

        #region Clock
        private double Now => _isRealTime ? _realClock.Elapsed.TotalSeconds : _now;

        /// <summary>
        /// How many frames after the one it was sent in a message may reach its handler. A client
        /// sends after the host's update, so the host handles it in the next frame, and so does
        /// every client the host relays it to. Lateness counts from the frame of the send rather
        /// than from the arrival NFR-2 counts from, so on a machine-wide network one more frame is
        /// allowed for a packet the loopback has not delivered yet when the receiver reads; the
        /// report shows the largest lateness either way.
        /// </summary>
        private int AllowedLateFrames => _isRealTime ? 2 : 1;

        /// <summary>Keeps a machine-wide run at 60 frames a second; a run inside this process goes on at once.</summary>
        private void WaitForTheNextFrame()
        {
            if (!_isRealTime)
            {
                return;
            }

            double now = _realClock.Elapsed.TotalSeconds;
            _nextFrameAt = Math.Max(_nextFrameAt + FrameSeconds, now - FrameSeconds);
            while ((now = _realClock.Elapsed.TotalSeconds) < _nextFrameAt)
            {
                if (_nextFrameAt - now > 0.002)
                {
                    Thread.Sleep(1);
                }
                else
                {
                    Thread.Yield();
                }
            }
        }
        #endregion

        #region Allocations
        /// <summary>The GC.Alloc recorder that Unity's AllocatingGCMemory constraint uses has to see one allocation.</summary>
        private void AssertTheAllocationRecorderWorks()
        {
            _allocations.FilterToCurrentThread();
            _allocations.enabled = true;
            byte[] allocated = new byte[64];
            _allocations.enabled = false;
            GC.KeepAlive(allocated);
            Assert.That(_allocations.sampleBlockCount, Is.GreaterThan(0), "The GC.Alloc recorder saw no allocation.");
        }

        /// <summary>Whether this runtime counts the bytes a thread allocates; Mono's Boehm collector may not.</summary>
        private static bool ProbeAllocatedBytes()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            byte[] allocated = new byte[4096];
            long after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(allocated);
            return after - before >= 4096;
        }

        private long AllocatedBytes() => _hasAllocatedBytes ? GC.GetAllocatedBytesForCurrentThread() : 0L;

        private static long SumOf(int[] values, Samples samples)
        {
            long sum = 0;
            for (int i = 0; i < samples.Count; i++)
            {
                sum += values[i];
            }

            return sum;
        }
        #endregion

        #region Report
        private string Report(string mode, ClientRoute route, Samples samples, double measuredSeconds)
        {
            int count = samples.Count;
            double[] total = new double[count];
            long allocationCount = 0;
            long allocatedBytes = 0;
            int allocatingFrames = 0;
            int longFrames = 0;
            for (int i = 0; i < count; i++)
            {
                total[i] = samples.UpdateMilliseconds[i] + samples.SendMilliseconds[i];
                int allocations = samples.UpdateAllocations[i] + samples.SendAllocations[i];
                allocationCount += allocations;
                allocatedBytes += samples.UpdateBytes[i] + samples.SendBytes[i];
                if (allocations > 0)
                {
                    allocatingFrames++;
                }

                if (total[i] > LongFrameMilliseconds)
                {
                    longFrames++;
                }
            }

            int maxLateAtHost = _host.MaxLateFrames;
            int maxLateAtClients = 0;
            long received = 0;
            for (int i = 0; i < _players.Count; i++)
            {
                received += _players[i].ReceivedFromClients + _players[i].ReceivedFromHost;
                if (i > 0)
                {
                    maxLateAtClients = Math.Max(maxLateAtClients, _players[i].MaxLateFrames);
                }
            }

            long clientsSent = 0;
            for (int i = 1; i < _players.Count; i++)
            {
                clientsSent += _players[i].Sent;
            }

            StringBuilder report = new StringBuilder();
            report.Append("[NFR-1] mode=").Append(mode)
                .Append(" route=").Append(route)
                .Append(" players=").Append(_players.Count)
                .Append(" frames=").Append(count)
                .Append(" seconds=").Append(Format(measuredSeconds))
                .Append(" fps=").Append(Format(count / measuredSeconds))
                .Append(" | host ms/frame avg=").Append(Format(Average(total)))
                .Append(" median=").Append(Format(Percentile(total, 0.50)))
                .Append(" p95=").Append(Format(Percentile(total, 0.95)))
                .Append(" p99=").Append(Format(Percentile(total, 0.99)))
                .Append(" max=").Append(Format(Max(total)))
                .Append(" (update avg=").Append(Format(Average(samples.UpdateMilliseconds, count)))
                .Append(" sends avg=").Append(Format(Average(samples.SendMilliseconds, count)))
                .Append(") target ").Append(Format(TargetMilliseconds)).Append(" ms ")
                .Append(Average(total) <= TargetMilliseconds ? "met" : "MISSED")
                .Append(" | frames over ").Append(Format(LongFrameMilliseconds)).Append(" ms=").Append(longFrames)
                .Append(" | allocating frames=").Append(allocatingFrames)
                .Append(", allocations=").Append(allocationCount)
                .Append(" (").Append(_hasAllocatedBytes ? allocatedBytes.ToString(CultureInfo.InvariantCulture) : "n/a").Append(" B)")
                .Append(" | messages sent: clients=").Append(clientsSent)
                .Append(" host=").Append(_host.Sent)
                .Append(" handled=").Append(received)
                .Append(" | NFR-2 max frames late: at host=").Append(maxLateAtHost)
                .Append(" at clients=").Append(maxLateAtClients);
            return report.ToString();
        }

        private static string Format(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

        private static double Average(double[] values) => Average(values, values.Length);

        private static double Average(double[] values, int count)
        {
            double sum = 0.0;
            for (int i = 0; i < count; i++)
            {
                sum += values[i];
            }

            return count > 0 ? sum / count : 0.0;
        }

        private static double Max(double[] values)
        {
            double max = 0.0;
            foreach (double value in values)
            {
                max = Math.Max(max, value);
            }

            return max;
        }

        private static double Percentile(double[] values, double fraction)
        {
            if (values.Length == 0)
            {
                return 0.0;
            }

            double[] sorted = (double[])values.Clone();
            Array.Sort(sorted);
            int index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, index))];
        }
        #endregion
    }
}
