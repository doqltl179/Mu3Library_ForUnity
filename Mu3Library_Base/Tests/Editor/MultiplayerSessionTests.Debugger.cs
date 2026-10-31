using System;
using System.Collections.Generic;
using System.Linq;
using Mu3Library.Editor.Window;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
// Unity's Is extends NUnit's with AllocatingGCMemory.
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Mu3Library.Tests
{
    /// <summary>
    /// The message counts and the editor's Multiplayer Debugger of FR-42. The debugger captures
    /// live sessions on the local network here, the way it captures the managers that the cores
    /// hold in play mode, with the test's clock standing in for the editor's.
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        #region Message Counts
        [Test]
        public void MessageCounts_CountEachSendOnce_AndOnlyOtherPlayersMessagesAsReceived()
        {
            // FR-42
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];
            ListenAll(Chat, new[] { host, a, b });
            ListenAll(Report, new[] { host, a, b });

            Assert.That(host.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.All).IsSuccess, Is.True);
            Assert.That(a.Manager.Send(Chat, new byte[] { 2 }, MessageTarget.Others).IsSuccess, Is.True);
            Assert.That(a.Manager.Send(Report, new byte[] { 3 }, MessageTarget.Host).IsSuccess, Is.True);
            Assert.That(a.Manager.Send(Chat, new byte[a.Manager.MaxMessageSize + 1], MessageTarget.All).IsSuccess, Is.False);
            Pump();

            Assert.That(Counts(host), Is.EqualTo(new[] { new MessageCounts(Chat, 1, 1), new MessageCounts(Report, 0, 1) }));
            Assert.That(Counts(a), Is.EqualTo(new[] { new MessageCounts(Chat, 1, 1), new MessageCounts(Report, 1, 0) }));
            Assert.That(Counts(b), Is.EqualTo(new[] { new MessageCounts(Chat, 0, 2), new MessageCounts(Report, 0, 0) }));

            // Unregistering a type drops its counts; registering it again starts over.
            Assert.That(host.Manager.UnregisterMessage(Report), Is.True);
            Assert.That(Counts(host), Is.EqualTo(new[] { new MessageCounts(Chat, 1, 1) }));
            Listen(host, Report);
            Assert.That(Counts(host), Is.EqualTo(new[] { new MessageCounts(Chat, 1, 1), new MessageCounts(Report, 0, 0) }));
        }

        [Test]
        public void MessageCounts_AllocateNothing_OnceTheListHasRoom()
        {
            // NFR-1
            (Node host, Node[] clients) = CreateSession(1);
            ListenAll(Chat, new[] { host, clients[0] });
            ListenAll(Report, new[] { host, clients[0] });
            List<MessageCounts> counts = new List<MessageCounts>(8);
            host.Manager.GetMessageCounts(counts);

            Assert.That(() => host.Manager.GetMessageCounts(counts), Is.Not.AllocatingGCMemory());
            Assert.That(counts.Count, Is.EqualTo(2));
            Assert.Throws<ArgumentNullException>(() => host.Manager.GetMessageCounts(null));
        }
        #endregion

        #region Debugger
        [Test]
        public void Debugger_InAFourPlayerSession_ShowsEveryPlayersStateAndPing_RefreshedEveryHalfSecond()
        {
            // FR-42
            UseTestNetworkClock();
            (Node host, Node[] clients) = CreateSession(3);
            Node[] everyone = clients.Prepend(host).ToArray();
            ListenAll(Chat, everyone);
            _network.SimulatedLatencyMilliseconds = 50f;
            List<(string Source, IMultiplayerManager Manager)> managers = everyone
                .Select(node => (node.Backend.PlayerName, (IMultiplayerManager)node.Manager))
                .ToList();

            MultiplayerDebuggerWindow window = ScriptableObject.CreateInstance<MultiplayerDebuggerWindow>();
            try
            {
                Assert.That(MultiplayerDebuggerWindow.RefreshIntervalSeconds, Is.LessThanOrEqualTo(1.0));
                RefreshFor(window, managers, 2.0);

                Assert.That(window.Views.Select(view => view.Manager), Is.EqualTo(managers.Select(entry => entry.Manager)));
                MultiplayerSessionView hostView = window.Views[0];
                Assert.That(hostView.State, Is.EqualTo(SessionState.Lobby));
                Assert.That(hostView.IsHost, Is.True);
                Assert.That(hostView.Lobby, Is.EqualTo(host.Manager.CurrentLobbyId));
                AssertPings(window, host, everyone, 80, 120);

                // A player's new state shows at the next refresh.
                Assert.That(clients[0].Manager.SetReady(true).IsSuccess, Is.True);
                RefreshFor(window, managers, MultiplayerDebuggerWindow.RefreshIntervalSeconds);
                foreach (MultiplayerSessionView view in window.Views)
                {
                    Assert.That(view.Players.Single(row => row.Participant.Id == clients[0].Id).Participant.IsReady, Is.True, view.Source);
                }

                // So does a new ping, once the backend has measured a window of it.
                _network.SimulatedLatencyMilliseconds = 100f;
                RefreshFor(window, managers, 2.0);
                AssertPings(window, host, everyone, 180, 220);

                // Message counts, with their rates since the capture before.
                double before = hostView.CapturedAt;
                for (int i = 0; i < 10; i++)
                {
                    host.Manager.Send(Chat, new byte[] { 1 }, MessageTarget.All);
                }

                foreach (Node client in clients)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        client.Manager.Send(Chat, new byte[] { 2 }, MessageTarget.Host);
                    }
                }

                RefreshFor(window, managers, MultiplayerDebuggerWindow.RefreshIntervalSeconds);
                double elapsed = hostView.CapturedAt - before;
                MultiplayerSessionView.MessageRow chat = hostView.Messages.Single();
                Assert.That(chat.Counts, Is.EqualTo(new MessageCounts(Chat, 10, 15)));
                Assert.That(chat.SentPerSecond, Is.EqualTo(10.0 / elapsed).Within(0.01));
                Assert.That(chat.ReceivedPerSecond, Is.EqualTo(15.0 / elapsed).Within(0.01));
                Assert.That(window.Views[1].Messages.Single().Counts, Is.EqualTo(new MessageCounts(Chat, 5, 10)));

                // A manager that left the list leaves the window.
                RefreshFor(window, managers.Take(1).ToList(), MultiplayerDebuggerWindow.RefreshIntervalSeconds);
                Assert.That(window.Views.Single(), Is.SameAs(hostView));
                Assert.That(MultiplayerDebuggerWindow.Describe(hostView.Players[0].Participant), Is.EqualTo($"Host ({host.Id}) [host, you]"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Debugger_ShowsWhoIsAwayOrLoaded_AndAClientThatReconnects()
        {
            // FR-42, FR-9, FR-20, FR-25
            UseTestNetworkClock();
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            host.Manager.StartGame();
            Pump();
            a.Manager.ReportLoaded();
            b.Backend.SimulateOutage(60f);
            Advance(DropSeconds + 0.5);

            MultiplayerDebuggerWindow window = ScriptableObject.CreateInstance<MultiplayerDebuggerWindow>();
            try
            {
                window.Refresh(new List<(string, IMultiplayerManager)> { ("Host", host.Manager), ("B", b.Manager) }, 1.0);

                MultiplayerSessionView hostView = window.Views[0];
                Assert.That(hostView.Players.Select(row => MultiplayerDebuggerWindow.Describe(row.Participant)), Is.EqualTo(new[]
                {
                    $"Host ({host.Id}) [host, you]",
                    $"A ({a.Id}) [loaded]",
                    $"B ({b.Id}) [away]",
                }));
                Assert.That(hostView.Players[2].HasQuality, Is.False, "nothing to measure while the player is away");

                MultiplayerSessionView clientView = window.Views[1];
                Assert.That(clientView.State, Is.EqualTo(SessionState.Reconnecting));
                Assert.That(MultiplayerDebuggerWindow.Describe(clientView.Players[2].Participant), Is.EqualTo($"B ({b.Id}) [you, away]"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void Debugger_ShowsAnOfflineManagerWithoutPlayers()
        {
            Node offline = CreateNode("Offline");
            MultiplayerDebuggerWindow window = ScriptableObject.CreateInstance<MultiplayerDebuggerWindow>();
            try
            {
                window.Refresh(new List<(string, IMultiplayerManager)> { ("Core", offline.Manager) }, 1.0);

                MultiplayerSessionView view = window.Views.Single();
                Assert.That(view.Source, Is.EqualTo("Core"));
                Assert.That(view.State, Is.EqualTo(SessionState.Offline));
                Assert.That(view.Players, Is.Empty);
                Assert.That(view.Messages, Is.Empty);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }
        #endregion

        #region Debugger Helpers
        private static List<MessageCounts> Counts(Node node)
        {
            List<MessageCounts> counts = new List<MessageCounts>();
            node.Manager.GetMessageCounts(counts);
            return counts.OrderBy(entry => entry.MessageType).ToList();
        }

        /// <summary>Runs the session and refreshes the window as often as the window refreshes itself.</summary>
        private void RefreshFor(MultiplayerDebuggerWindow window, List<(string Source, IMultiplayerManager Manager)> managers, double seconds)
        {
            double end = _now + seconds - 1e-9;
            while (_now < end)
            {
                Advance(MultiplayerDebuggerWindow.RefreshIntervalSeconds);
                window.Refresh(managers, _now);
            }
        }

        /// <summary>
        /// The host's view shows a ping for every client and none for itself; a client's view shows
        /// one for the host only, since it reaches the other clients through the host.
        /// </summary>
        private static void AssertPings(MultiplayerDebuggerWindow window, Node host, Node[] everyone, int min, int max)
        {
            foreach (MultiplayerSessionView view in window.Views)
            {
                Assert.That(view.Players.Select(row => row.Participant.Id), Is.EquivalentTo(everyone.Select(node => node.Id)), view.Source);
                foreach (MultiplayerSessionView.PlayerRow row in view.Players)
                {
                    bool hasConnection = view.IsHost ? row.Participant.Id != host.Id : row.Participant.Id == host.Id;
                    Assert.That(row.HasQuality, Is.EqualTo(hasConnection), $"{view.Source} about {row.Participant}");
                    if (hasConnection)
                    {
                        Assert.That(row.Quality.PingMilliseconds, Is.InRange(min, max), $"{view.Source} about {row.Participant}");
                    }
                }
            }
        }
        #endregion
    }
}
