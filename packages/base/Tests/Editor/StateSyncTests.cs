using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mu3Library.Editor.Window;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Sync;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The state sync layer over a live session on the local network inside this process, frame by
    /// frame at 60 frames a second on a test clock: what each tick sends whom, within its budget.
    /// </summary>
    public class StateSyncTests
    {
        private const string GameId = "tests.statesync";
        private const ushort SyncMessage = 900;
        private const double FrameSeconds = 1.0 / 60.0;
        private const double TickSeconds = StateSyncConfig.DefaultTickIntervalSeconds;

        private sealed class Node
        {
            public LocalBackend Backend;
            public MultiplayerManager Manager;
            public StateSyncManager Sync;
            public PlayerId Id => Backend.LocalPlayerId;
        }

        /// <summary>An int, after padding bytes that set its size, which records every read.</summary>
        private sealed class CounterState : SyncState
        {
            private readonly int _padding;
            private int _value;

            public readonly List<(bool Full, int Value)> Reads = new List<(bool Full, int Value)>();

            public CounterState(uint id, int padding = 0) : base(id)
            {
                _padding = padding;
            }

            public int Value
            {
                get => _value;
                set
                {
                    if (_value != value)
                    {
                        _value = value;
                        SetDirty();
                    }
                }
            }

            public override void WriteFull(MessageWriter writer)
            {
                for (int i = 0; i < _padding; i++)
                {
                    writer.WriteByte(0);
                }

                writer.WriteInt(_value);
            }

            public override void ReadFull(ref MessageReader reader, in SyncReadContext context) => Read(ref reader, true);

            public override void ReadDelta(ref MessageReader reader, in SyncReadContext context) => Read(ref reader, false);

            private void Read(ref MessageReader reader, bool full)
            {
                for (int i = 0; i < _padding; i++)
                {
                    reader.ReadByte();
                }

                _value = reader.ReadInt();
                Reads.Add((full, _value));
            }
        }

        private LocalNetwork _network;
        private double _now;
        private readonly List<Node> _nodes = new List<Node>();
        private readonly List<GameObject> _objects = new List<GameObject>();



        [SetUp]
        public void SetUp()
        {
            _now = 0.0;
            _network = new LocalNetwork { Clock = () => _now };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Node node in _nodes)
            {
                node.Sync.Dispose();
                node.Manager.Dispose();
                node.Backend.Dispose();
            }

            foreach (GameObject gameObject in _objects)
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }

            _nodes.Clear();
            _objects.Clear();
        }

        [Test]
        public void ChangedStates_GoToEachClient_TogetherInOneMessageATick()
        {
            (Node host, Node[] clients) = StartGame(2);
            Dictionary<Node, CounterState[]> states = _nodes.ToDictionary(node => node, node => Enumerable.Range(1, 5)
                .Select(id => Register(node, new CounterState((uint)id), deliveryMode: DeliveryMode.Reliable))
                .ToArray());
            Advance(0.3);

            long sentBefore = SyncCounts(host).Sent;
            foreach (CounterState state in states[host])
            {
                state.Value = 7;
            }

            AdvanceUntil(() => clients.All(client => states[client].All(state => state.Value == 7)), 1.0);

            Assert.That(SyncCounts(host).Sent - sentBefore, Is.EqualTo(clients.Length), "one message a client, not one a state");
            foreach (Node client in clients)
            {
                foreach (CounterState state in states[client])
                {
                    Assert.That(state.Reads.Last(), Is.EqualTo((false, 7)), "the change alone, after the whole state the game began with");
                    Assert.That(state.Reads[0].Full, Is.True);
                }
            }
        }

        [Test]
        public void StatesPastTheBudget_WaitForLaterTicks_TheHigherPriorityAndTheLongerWaitingFirst()
        {
            // 64 bytes a tick hold the tick and two of these states, 22 bytes each with their entry.
            (Node host, Node[] clients) = StartGame(1, configure: config => config.MaxBytesPerTick = 64);
            Node client = clients[0];
            Dictionary<Node, CounterState[]> states = _nodes.ToDictionary(node => node, node => new[]
            {
                Register(node, new CounterState(1, padding: 16), deliveryMode: DeliveryMode.Reliable, priority: 4f),
                Register(node, new CounterState(2, padding: 16), deliveryMode: DeliveryMode.Reliable),
                Register(node, new CounterState(3, padding: 16), deliveryMode: DeliveryMode.Reliable),
                Register(node, new CounterState(4, padding: 16), deliveryMode: DeliveryMode.Reliable),
            });
            Advance(1.0);
            foreach (CounterState state in states[client])
            {
                state.Reads.Clear();
            }

            List<SyncPlayerStats> stats = new List<SyncPlayerStats>();
            const int Ticks = 6;
            for (int tick = 1; tick <= Ticks; tick++)
            {
                foreach (CounterState state in states[host])
                {
                    state.Value = tick;
                }

                uint before = host.Sync.Tick;
                AdvanceUntil(() => host.Sync.Tick != before, 1.0);
                host.Sync.GetPlayerStats(stats);
                SyncPlayerStats sent = stats.Single();
                Assert.That(sent.LastTickBytes, Is.LessThanOrEqualTo(64), $"tick {tick}");
                Assert.That(sent.LastTickStates, Is.EqualTo(2), $"tick {tick}");
                Assert.That(sent.LastTickDeferred, Is.EqualTo(2), $"tick {tick}");
            }

            // The client read each tick in the frame the host sent it.
            CounterState[] copies = states[client];
            Assert.That(copies[0].Reads.Count, Is.EqualTo(Ticks), "the state of priority 4 goes every tick");
            for (int i = 1; i < copies.Length; i++)
            {
                // The other three take turns with the room left, as their claims grow while they wait.
                Assert.That(copies[i].Reads.Count, Is.EqualTo(Ticks / 3), $"state {copies[i].Id}");
            }

            // Once nothing changes, the ones that missed a change catch up whole.
            Advance(0.5);
            Assert.That(copies.Select(copy => copy.Value), Is.All.EqualTo(Ticks));
            Assert.That(copies.Skip(1).Select(copy => copy.Reads.Last().Full), Is.All.True);
        }

        [Test]
        public void InterestFilter_LeavesAPlayerOut_AndSendsThemTheWholeStateOnceItLetsThemIn()
        {
            (Node host, Node[] clients) = StartGame(2);
            Node near = clients[0], far = clients[1];
            bool farIsIn = false;
            SyncInterestFilter interest = (state, recipient) => recipient == near.Id || farIsIn;
            Dictionary<Node, CounterState> states = _nodes.ToDictionary(node => node,
                node => Register(node, new CounterState(1), deliveryMode: DeliveryMode.Reliable, interest: interest));
            Advance(0.3);

            states[host].Value = 5;
            Advance(0.3);

            Assert.That(states[near].Value, Is.EqualTo(5));
            Assert.That(states[far].Value, Is.Zero);
            Assert.That(states[far].Reads, Is.Empty);

            farIsIn = true;
            Advance(0.3);

            Assert.That(states[far].Reads, Is.EqualTo(new[] { (true, 5) }));
        }

        [Test]
        public void LostUnreliableChange_IsRepaired_ByTheNextWholeState()
        {
            (Node host, Node[] clients) = StartGame(1);
            Dictionary<Node, CounterState> states = _nodes.ToDictionary(node => node, node => Register(node, new CounterState(1)));
            CounterState copy = states[clients[0]];

            // Right after a whole state, so the next one is about a second away.
            AdvanceUntil(() => copy.Reads.Count > 1 && copy.Reads.Last().Full, 2.0);

            _network.SimulatedLossPercent = 100f;
            states[host].Value = 2;
            Advance(0.1);
            _network.SimulatedLossPercent = 0f;
            Advance(0.3);

            Assert.That(copy.Value, Is.Zero, "a lost unreliable change is not sent again");

            AdvanceUntil(() => copy.Value == 2, StateSyncConfig.DefaultFullStateIntervalSeconds + 0.5);
            Assert.That(copy.Reads.Last(), Is.EqualTo((true, 2)));
        }

        [Test]
        public void ClientOwnedState_ReachesTheHost_AndGoesOnToTheOtherClients_NeverBackToItsOwner()
        {
            (Node host, Node[] clients) = StartGame(2);
            Node owner = clients[0], other = clients[1];
            Dictionary<Node, CounterState> states = _nodes.ToDictionary(node => node,
                node => Register(node, new CounterState(10), owner.Id, DeliveryMode.Reliable));
            Advance(0.3);

            states[owner].Value = 3;
            Advance(0.3);

            Assert.That(states[host].Value, Is.EqualTo(3));
            Assert.That(states[other].Value, Is.EqualTo(3));
            Assert.That(states[owner].Reads, Is.Empty);
        }

        [Test]
        public void ClientOwnedState_ThatReachesTheHostBeforeItsFirstUpdateInTheGame_StillGoesOnToTheOtherClients()
        {
            // The host's sync updated in the lobby, then its game starts later in the same frame,
            // as from a button; the clients then update before the host in each frame, so the
            // host reads the owner's first tick before its own sync updates in the game.
            Node host = CreateNode("Host");
            MultiplayerResult? created = null;
            host.Manager.CreateLobby(new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = new SessionRules(4, requireReady: false) },
                result => created = result);
            AdvanceUntil(() => created.HasValue, 2.0);
            Node owner = CreateNode("Owner");
            Node other = CreateNode("Other");
            Join(owner, host);
            Join(other, host);

            // A reliable state its owner set before the game and does not change again.
            Dictionary<Node, CounterState> states = _nodes.ToDictionary(node => node,
                node => Register(node, new CounterState(12), owner.Id, DeliveryMode.Reliable));
            states[owner].Value = 6;
            host.Sync.Update();
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);

            for (int frame = 0; frame < 20; frame++)
            {
                _now += FrameSeconds;
                for (int i = _nodes.Count - 1; i >= 0; i--)
                {
                    _nodes[i].Manager.Update();
                    _nodes[i].Sync.Update();
                }
            }

            Assert.That(states[host].Value, Is.EqualTo(6));
            Assert.That(states[other].Value, Is.EqualTo(6));
        }

        [Test]
        public void HostDropsAnUpdate_FromAPlayerWhoDoesNotOwnTheState()
        {
            (Node host, Node[] clients) = StartGame(2);
            Node owner = clients[0], other = clients[1];
            CounterState onHost = Register(host, new CounterState(11), owner.Id, DeliveryMode.Reliable);
            Register(owner, new CounterState(11), owner.Id, DeliveryMode.Reliable);

            // The other client claims the state as its own.
            CounterState claimed = Register(other, new CounterState(11), other.Id, DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[StateSync\] Dropped an update of state 11 from " + other.Id));
            Advance(0.3);

            int reads = onHost.Reads.Count;
            claimed.Value = 9;
            Advance(0.3);

            Assert.That(onHost.Value, Is.Zero);
            Assert.That(onHost.Reads.Count, Is.EqualTo(reads), "nothing from the player who claims it");
        }

        [Test]
        public void PlayerWhoJoinsDuringTheGame_GetsEveryStateWhole()
        {
            (Node host, Node[] clients) = StartGame(1, new SessionRules(4, requireReady: false, allowLateJoin: true));
            CounterState[] onHost = { Register(host, new CounterState(1)), Register(host, new CounterState(2), deliveryMode: DeliveryMode.Reliable) };
            Register(clients[0], new CounterState(1));
            Register(clients[0], new CounterState(2), deliveryMode: DeliveryMode.Reliable);
            onHost[0].Value = 4;
            onHost[1].Value = 5;
            Advance(0.3);

            Node late = CreateNode("Late");
            CounterState[] copies = { Register(late, new CounterState(1)), Register(late, new CounterState(2), deliveryMode: DeliveryMode.Reliable) };
            Join(late, host);
            Advance(0.3);

            Assert.That(copies[0].Reads, Is.EqualTo(new[] { (true, 4) }));
            Assert.That(copies[1].Reads, Is.EqualTo(new[] { (true, 5) }));
        }

        [Test]
        public void RateLimits_ThatCannotHoldWhatAClientSends_AreFound_AndLoggedAsTheGameStarts()
        {
            Node host = CreateNode("Host");
            SessionRules tight = new SessionRules(4, requireReady: false).WithMaxMessagesPerSecond(10).WithMaxBytesPerSecond(4 * 1024);

            // A client that owns no state sends no sync message.
            Assert.That(host.Sync.CheckRateLimits(tight).IsSuccess, Is.True);

            // One a tick for each delivery mode in use, 512 bytes a tick and the headers.
            Register(host, new CounterState(1), new PlayerId(424242));
            Register(host, new CounterState(2), new PlayerId(424242), DeliveryMode.Reliable);
            SessionRules readme = SessionRules.Default.WithCapacity(4).WithMaxMessagesPerSecond(60).WithMaxBytesPerSecond(16 * 1024);
            Assert.That(host.Sync.CheckRateLimits(readme).IsSuccess, Is.True);
            Assert.That(host.Sync.CheckRateLimits(SessionRules.Default).IsSuccess, Is.True, "no limit");
            Assert.That(host.Sync.CheckRateLimits(tight.WithMaxBytesPerSecond(0)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Sync.CheckRateLimits(tight.WithMaxMessagesPerSecond(0)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));

            // The game starts under rules that cannot hold the client's own state.
            host.Sync.Unregister(1);
            host.Sync.Unregister(2);
            LogAssert.Expect(LogType.Warning, new Regex(@"\[StateSync\] A client may send up to 21 sync messages"));
            StartGame(host, new[] { "Client" }, tight, node =>
            {
                Register(node, new CounterState(3), node.Id);
            });
        }

        [Test]
        public void TransformSync_ACopyFollowsTheOwnersTransform_SmoothlyAndABitBehind()
        {
            (Node host, Node[] clients) = StartGame(1);
            Transform owned = CreateObject("Owned").transform;
            Transform shown = CreateObject("Shown").transform;
            TransformSync source = new TransformSync(20, owned, clock: () => _now);
            TransformSync copy = new TransformSync(20, shown, clock: () => _now);
            Assert.That(host.Sync.Register(source).IsSuccess, Is.True);
            Assert.That(clients[0].Sync.Register(copy).IsSuccess, Is.True);

            // One unit a second along x while it turns 90 degrees a second.
            double start = _now;
            List<float> shownX = new List<float>();
            Advance(2.0, () =>
            {
                float t = (float)(_now - start);
                owned.SetPositionAndRotation(new Vector3(t, 0f, 0f), Quaternion.Euler(0f, 90f * t, 0f));
                copy.Update();
                shownX.Add(shown.position.x);
            });

            Assert.That(copy.IsCopy, Is.True);
            Assert.That(source.IsCopy, Is.False);
            float lag = owned.position.x - shown.position.x;
            Assert.That(lag, Is.InRange(TransformSync.DefaultInterpolationDelaySeconds * 0.5f, TransformSync.DefaultInterpolationDelaySeconds + 3f * (float)TickSeconds));
            Assert.That(Quaternion.Angle(shown.rotation, Quaternion.Euler(0f, 90f * shown.position.x, 0f)), Is.LessThan(1f), "the rotation of the same moment");

            // Between two ticks the copy moves every frame, rather than once a tick.
            List<float> lastSecond = shownX.Skip(shownX.Count - 60).ToList();
            for (int i = 1; i < lastSecond.Count; i++)
            {
                float step = lastSecond[i] - lastSecond[i - 1];
                Assert.That(step, Is.GreaterThan(0f).And.LessThan(0.9f * (float)TickSeconds), $"frame {i}");
            }

            // Once the owner stops, the copy comes to rest where it is, within a step.
            Advance(0.5, () => copy.Update());
            Assert.That(Vector3.Distance(shown.position, owned.position), Is.LessThanOrEqualTo(TransformSync.DefaultPositionPrecision));
            Assert.That(Quaternion.Angle(shown.rotation, owned.rotation), Is.LessThan(0.2f));
        }

        [Test]
        public void Debugger_ShowsWhatEachTickSentEachPlayer()
        {
            (Node host, Node[] clients) = StartGame(2);
            Dictionary<Node, CounterState> states = _nodes.ToDictionary(node => node, node => Register(node, new CounterState(1)));
            List<(string Source, IMultiplayerManager Manager)> managers = _nodes
                .Select(node => (node.Backend.PlayerName, (IMultiplayerManager)node.Manager))
                .ToList();

            MultiplayerDebuggerWindow window = ScriptableObject.CreateInstance<MultiplayerDebuggerWindow>();
            try
            {
                window.Refresh(managers, _now);
                Advance(1.0, () => states[host].Value++);
                window.Refresh(managers, _now);

                MultiplayerSessionView.SyncView sync = window.Views[0].Syncs.Single();
                Assert.That(sync.Sync, Is.SameAs(host.Sync));
                Assert.That(sync.StateCount, Is.EqualTo(1));
                Assert.That(sync.Players.Select(row => row.Stats.Player), Is.EquivalentTo(clients.Select(client => client.Id)));
                foreach (MultiplayerSessionView.SyncPlayerRow row in sync.Players)
                {
                    Assert.That(row.BytesPerSecond, Is.GreaterThan(0f), row.Stats.ToString());
                    Assert.That(row.Stats.MaxTickBytes, Is.InRange(1, host.Sync.MaxBytesPerTick), row.Stats.ToString());
                }

                // A client sends to the host only.
                Assert.That(window.Views[1].Syncs.Single().Players.Single().Stats.Player, Is.EqualTo(host.Id));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void TransformSync_Rotation_TravelsWithinAFifthOfADegree()
        {
            System.Random random = new System.Random(7);
            for (int i = 0; i < 1000; i++)
            {
                Quaternion rotation = Quaternion.Euler((float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f, (float)random.NextDouble() * 360f);
                Quaternion travelled = TransformSync.UnpackRotation(TransformSync.PackRotation(rotation));
                Assert.That(Quaternion.Angle(rotation, travelled), Is.LessThan(0.2f), rotation.ToString());
            }
        }

        #region Helpers
        private Node CreateNode(string name, Action<StateSyncConfig> configure = null)
        {
            LocalBackend backend = new LocalBackend(_network, name);
            MultiplayerManager manager = new MultiplayerManager();
            Assert.That(manager.Initialize(backend, new MultiplayerConfig(GameId, 1) { Clock = () => _now }).IsSuccess, Is.True);

            StateSyncConfig config = new StateSyncConfig(SyncMessage) { Clock = () => _now };
            configure?.Invoke(config);
            Node node = new Node { Backend = backend, Manager = manager, Sync = new StateSyncManager(manager, config) };
            _nodes.Add(node);
            return node;
        }

        private (Node Host, Node[] Clients) StartGame(int clients, SessionRules? rules = null, Action<StateSyncConfig> configure = null)
        {
            Node host = CreateNode("Host", configure);
            Node[] joined = StartGame(host, Enumerable.Range(1, clients).Select(i => "Client" + i).ToArray(),
                rules ?? new SessionRules(4, requireReady: false), null, configure);
            return (host, joined);
        }

        /// <summary>Hosts a lobby, lets new players join it, prepares each, and starts the game.</summary>
        private Node[] StartGame(Node host, string[] clientNames, SessionRules rules, Action<Node> prepare, Action<StateSyncConfig> configure = null)
        {
            MultiplayerResult? created = null;
            host.Manager.CreateLobby(new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = rules }, result => created = result);
            AdvanceUntil(() => created.HasValue, 2.0);
            Assert.That(created.Value.IsSuccess, Is.True, created.Value.ToString());

            Node[] clients = clientNames.Select(name =>
            {
                Node client = CreateNode(name, configure);
                Join(client, host);
                prepare?.Invoke(client);
                return client;
            }).ToArray();

            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            AdvanceUntil(() => _nodes.All(node => node.Manager.State == SessionState.InGame), 2.0);
            return clients;
        }

        private void Join(Node node, Node host)
        {
            MultiplayerResult? joined = null;
            node.Manager.JoinLobby(host.Manager.CurrentLobbyId, result => joined = result);
            AdvanceUntil(() => joined.HasValue, 2.0);
            Assert.That(joined.Value.IsSuccess, Is.True, joined.Value.ToString());
        }

        private static CounterState Register(Node node, CounterState state, PlayerId owner = default,
            DeliveryMode deliveryMode = DeliveryMode.Unreliable, float priority = 1f, SyncInterestFilter interest = null)
        {
            Assert.That(node.Sync.Register(state, owner, deliveryMode, priority, interest).IsSuccess, Is.True);
            return state;
        }

        private static MessageCounts SyncCounts(Node node)
        {
            List<MessageCounts> counts = new List<MessageCounts>();
            node.Manager.GetMessageCounts(counts);
            return counts.Single(entry => entry.MessageType == SyncMessage);
        }

        private GameObject CreateObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject;
        }

        /// <summary>One frame of every player: its multiplayer manager, then its state sync.</summary>
        private void Frame()
        {
            _now += FrameSeconds;
            for (int i = 0; i < _nodes.Count; i++)
            {
                _nodes[i].Manager.Update();
                _nodes[i].Sync.Update();
            }
        }

        private void Advance(double seconds, Action eachFrame = null)
        {
            double end = _now + seconds;
            while (_now < end)
            {
                Frame();
                eachFrame?.Invoke();
            }
        }

        private void AdvanceUntil(Func<bool> condition, double seconds)
        {
            double end = _now + seconds;
            while (!condition())
            {
                Assert.That(_now, Is.LessThan(end), $"Nothing happened within {seconds} s of test time.");
                Frame();
            }
        }
        #endregion
    }
}
