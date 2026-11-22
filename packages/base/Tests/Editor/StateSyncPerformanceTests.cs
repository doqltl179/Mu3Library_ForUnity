using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Sync;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The state sync layer under a full session of 16 players on the local network inside this
    /// process, at 60 frames a second on a test clock: the host owns 48 states that move every
    /// frame and every client owns one, its character, which the host sends on to the other
    /// clients, more than one tick's budget holds for any player. It times the host's own calls,
    /// its multiplayer manager's update and its state sync's, counts what they allocate, and checks
    /// that every tick stays within the budget and that no state waits long for any player.
    /// </summary>
    /// <remarks>
    /// The case logs the host's timing in a line that starts with <c>[StateSync]</c>, in the Unity
    /// log and in the test's output in the results file. It asserts no allocation in the steady
    /// state but no time, which depends on the machine, as <see cref="MultiplayerPerformanceTests"/>
    /// does for the cases it only logs. The players share this thread, so a garbage collection that
    /// another player's allocation starts can land in a host frame.
    /// </remarks>
    [Category("Performance")]
    public class StateSyncPerformanceTests
    {
        private const string GameId = "tests.statesync.performance";
        private const ushort SyncMessage = 900;
        private const int Players = MultiplayerLimits.MaxCapacity;
        private const int HostStates = 48;
        private const uint ClientStateIds = 1000;
        private const double FrameSeconds = 1.0 / 60.0;
        private const int WarmupFrames = 4 * 60;
        private const int MeasuredFrames = 30 * 60;

        // Every state changes every frame, and each tick holds about half of what is due for a
        // player, so a state goes about every second tick; this leaves room for the order of the claims.
        private const double MaxWaitSeconds = 0.5;

        /// <summary>A moving position, three floats, which records when each copy last read it.</summary>
        private sealed class PositionState : SyncState
        {
            private Vector3 _position;

            public double LastReadAt = double.NaN;
            public double LongestWait;

            public PositionState(uint id) : base(id)
            {
            }

            public void Move(Vector3 position)
            {
                _position = position;
                SetDirty();
            }

            public override void WriteFull(MessageWriter writer) => writer.WriteVector3(_position);

            public override void ReadFull(ref MessageReader reader, in SyncReadContext context)
            {
                _position = reader.ReadVector3();
                if (!double.IsNaN(LastReadAt))
                {
                    LongestWait = Math.Max(LongestWait, context.ReceivedAt - LastReadAt);
                }

                LastReadAt = context.ReceivedAt;
            }
        }

        private sealed class Player
        {
            public LocalBackend Backend;
            public MultiplayerManager Manager;
            public StateSyncManager Sync;
            public readonly List<PositionState> States = new List<PositionState>();
            public PositionState Own;
            public bool IsInGame;
        }

        private static readonly double MillisecondsPerTick = 1000.0 / Stopwatch.Frequency;

        private readonly List<Player> _players = new List<Player>();
        private readonly List<SyncPlayerStats> _stats = new List<SyncPlayerStats>(Players);
        private LocalNetwork _network;
        private Player _host;
        private double _now;
        private Recorder _allocations;



        [SetUp]
        public void SetUp()
        {
            _now = 0.0;
            _network = new LocalNetwork { Clock = () => _now };
            _allocations = Recorder.Get("GC.Alloc");
            _allocations.enabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Player player in _players)
            {
                player.Sync.Dispose();
                player.Manager.Dispose();
                player.Backend.Dispose();
            }

            _players.Clear();
            _host = null;
            _allocations.enabled = false;
        }

        [Test]
        public void SixteenPlayers_StatesPastTheBudget_StayWithinItWithoutAllocating()
        {
            StartFullSession();
            for (int i = 0; i < WarmupFrames; i++)
            {
                RunFrame(null, null);
            }

            foreach (Player player in _players)
            {
                foreach (PositionState state in player.States)
                {
                    state.LongestWait = 0.0;
                }
            }

            long bytesBefore = TotalBytesToClients();
            double measuredFrom = _now;
            double[] milliseconds = new double[MeasuredFrames];
            int[] allocations = new int[MeasuredFrames];
            for (int i = 0; i < MeasuredFrames; i++)
            {
                RunFrame(milliseconds, allocations, i);
            }

            double measuredSeconds = _now - measuredFrom;
            long bytesPerClient = (TotalBytesToClients() - bytesBefore) / (Players - 1);

            double total = 0.0;
            double max = 0.0;
            long allocated = 0;
            for (int i = 0; i < MeasuredFrames; i++)
            {
                total += milliseconds[i];
                max = Math.Max(max, milliseconds[i]);
                allocated += allocations[i];
            }

            double longestWait = 0.0;
            for (int p = 1; p < _players.Count; p++)
            {
                foreach (PositionState state in _players[p].States)
                {
                    if (state != _players[p].Own)
                    {
                        longestWait = Math.Max(longestWait, state.LongestWait);
                    }
                }
            }

            _host.Sync.GetPlayerStats(_stats);
            int maxTickBytes = 0;
            foreach (SyncPlayerStats stats in _stats)
            {
                maxTickBytes = Math.Max(maxTickBytes, stats.MaxTickBytes);
            }

            string report = string.Format(CultureInfo.InvariantCulture,
                "[StateSync] {0} players, {1} host states and {2} client states changing every frame, {3} B a player a tick at {4:0.##} ticks a second: "
                + "host update and sync {5:0.000} ms a frame on average, {6:0.000} ms at most; at most {7} B to one player in one tick; "
                + "{8:0} B a second to each client on average; longest wait of a state for a client {9:0.000} s; {10} allocations",
                Players, HostStates, Players - 1, _host.Sync.MaxBytesPerTick, 1.0 / _host.Sync.TickIntervalSeconds,
                total / MeasuredFrames, max, maxTickBytes, bytesPerClient / measuredSeconds, longestWait, allocated);
            Debug.Log(report);
            TestContext.Out.WriteLine(report);

            Assert.That(_players.TrueForAll(player => player.IsInGame), Is.True, "A player left the game during the run.");
            Assert.That(_stats.Count, Is.EqualTo(Players - 1));
            Assert.That(maxTickBytes, Is.LessThanOrEqualTo(_host.Sync.MaxBytesPerTick));
            Assert.That(longestWait, Is.LessThanOrEqualTo(MaxWaitSeconds), "A state waited too long for a client.");
            Assert.That(allocated, Is.Zero, "The host's update and sync allocated in the steady state.");
        }

        /// <summary>
        /// One frame: every state moves, then the host's multiplayer manager and state sync update,
        /// measured, then each client's, unmeasured.
        /// </summary>
        private void RunFrame(double[] milliseconds, int[] allocations, int sample = 0)
        {
            _now += FrameSeconds;
            float t = (float)_now;
            foreach (Player player in _players)
            {
                if (player == _host)
                {
                    for (int i = 0; i < HostStates; i++)
                    {
                        player.States[i].Move(new Vector3(Mathf.Sin(t + i), 0f, Mathf.Cos(t + i)));
                    }
                }
                else
                {
                    player.Own.Move(new Vector3(t, 0f, player.Own.Id));
                }
            }

            _allocations.FilterToCurrentThread();
            _allocations.enabled = true;
            long start = Stopwatch.GetTimestamp();
            _host.Manager.Update();
            _host.Sync.Update();
            long end = Stopwatch.GetTimestamp();
            _allocations.enabled = false;

            if (milliseconds != null)
            {
                milliseconds[sample] = (end - start) * MillisecondsPerTick;
                allocations[sample] = _allocations.sampleBlockCount;
            }

            for (int i = 1; i < _players.Count; i++)
            {
                _players[i].Manager.Update();
                _players[i].Sync.Update();
            }
        }

        private long TotalBytesToClients()
        {
            _host.Sync.GetPlayerStats(_stats);
            long bytes = 0;
            foreach (SyncPlayerStats stats in _stats)
            {
                bytes += stats.TotalBytes;
            }

            return bytes;
        }

        private void StartFullSession()
        {
            for (int i = 0; i < Players; i++)
            {
                CreatePlayer(i);
            }

            _host = _players[0];
            MultiplayerResult? created = null;
            _host.Manager.CreateLobby(new LobbyCreateOptions
            {
                Visibility = LobbyVisibility.Public,
                Name = "Benchmark",
                Rules = new SessionRules(Players, requireReady: false),
            }, result => created = result);
            RunUntil(() => created.HasValue, "the lobby");
            Assert.That(created.Value.IsSuccess, Is.True, created.Value.ToString());

            for (int i = 1; i < Players; i++)
            {
                MultiplayerResult? joined = null;
                _players[i].Manager.JoinLobby(_host.Manager.CurrentLobbyId, result => joined = result);
                RunUntil(() => joined.HasValue, $"client {i} to join");
                Assert.That(joined.Value.IsSuccess, Is.True, $"client {i}: {joined.Value}");
            }

            // Each player's own state, and every state the host and the other clients own.
            for (int i = 0; i < Players; i++)
            {
                Player player = _players[i];
                for (uint id = 1; id <= HostStates; id++)
                {
                    Register(player, new PositionState(id), PlayerId.None);
                }

                for (int owner = 1; owner < Players; owner++)
                {
                    PositionState state = Register(player, new PositionState(ClientStateIds + (uint)owner), _players[owner].Backend.LocalPlayerId);
                    if (owner == i)
                    {
                        player.Own = state;
                    }
                }
            }

            Assert.That(_host.Manager.StartGame().IsSuccess, Is.True);
            RunUntil(() => _players.TrueForAll(player => player.IsInGame), "every player to start the game");
        }

        private static PositionState Register(Player player, PositionState state, PlayerId owner)
        {
            Assert.That(player.Sync.Register(state, owner).IsSuccess, Is.True);
            player.States.Add(state);
            return state;
        }

        private void CreatePlayer(int index)
        {
            Player player = new Player();
            string name = index == 0 ? "Host" : "Client" + index.ToString(CultureInfo.InvariantCulture);
            player.Backend = new LocalBackend(_network, name);
            player.Manager = new MultiplayerManager();
            Assert.That(player.Manager.Initialize(player.Backend, new MultiplayerConfig(GameId, 1) { Clock = () => _now }).IsSuccess, Is.True);
            player.Sync = new StateSyncManager(player.Manager, new StateSyncConfig(SyncMessage) { Clock = () => _now });
            player.Manager.OnStateChanged += change => player.IsInGame = change.Current == SessionState.InGame;
            _players.Add(player);
        }

        private void RunUntil(Func<bool> condition, string what)
        {
            double end = _now + 15.0;
            while (!condition())
            {
                Assert.That(_now, Is.LessThan(end), $"Timed out waiting for {what}.");
                _now += FrameSeconds;
                foreach (Player player in _players)
                {
                    player.Manager.Update();
                    player.Sync.Update();
                }
            }
        }
    }
}
