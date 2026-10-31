#if MU3LIBRARY_STEAMWORKS_SUPPORT && UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using Mu3Library.Multiplayer.Steam;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    /// <summary>
    /// <see cref="SteamBackend"/> against the Steam client running on this machine, on Steam's
    /// test app 480. They need a signed-in Steam client, so they run only when the environment
    /// variable <c>MU3LIBRARY_STEAM_TESTS</c> is <c>1</c> and are ignored otherwise. Two players
    /// on two accounts still need the manual check in spec section 13.
    /// </summary>
    public class MultiplayerSteamIntegrationTests
    {
        private const string EnableVariable = "MU3LIBRARY_STEAM_TESTS";

        private sealed class Recorder : IMultiplayerBackendListener
        {
            public readonly List<string> Events = new List<string>();
            public readonly List<byte[]> Received = new List<byte[]>();
            public int DisconnectCode = -1;
            public BackendDisconnectKind DisconnectKind;

            public void OnConnected(ConnectionId connection, PlayerId remote, bool isIncoming) => Events.Add("connected " + connection);
            public void OnReceived(ConnectionId connection, PlayerId remote, ReadOnlySpan<byte> data)
            {
                Events.Add("received " + connection);
                Received.Add(data.ToArray());
            }

            public void OnDisconnected(ConnectionId connection, PlayerId remote, BackendDisconnectKind kind, int reasonCode)
            {
                Events.Add("disconnected " + connection);
                DisconnectKind = kind;
                DisconnectCode = reasonCode;
            }

            public void OnLobbyOwnerChanged(LobbyId lobby, PlayerId newOwner) { }
            public void OnLobbyJoinRequested(LobbyId lobby) { }
        }

        private SteamBackend _backend;



        [SetUp]
        public void SetUp()
        {
            if (Environment.GetEnvironmentVariable(EnableVariable) != "1")
            {
                Assert.Ignore($"Set {EnableVariable}=1 with a signed-in Steam client running to check the Steam backend.");
            }

            MultiplayerResult created = SteamBackend.TryCreate(new SteamBackendOptions { AppId = 480 }, out _backend);
            Assert.That(created.IsSuccess, Is.True, created.ToString());
        }

        [TearDown]
        public void TearDown()
        {
            _backend?.Dispose();
            _backend = null;
        }

        [Test]
        public void SteamConnections_CarryEveryMessageInOrder_ThenTheCloseReason()
        {
            // FR-32, and the backend contract: data before its connection's close, the close
            // reason intact, and more queued messages than one receive call takes.
            Recorder recorder = new Recorder();
            _backend.SetListener(recorder);
            Assert.That(_backend.CreateLoopbackPair(out ConnectionId sender, out ConnectionId receiver), Is.True);

            List<byte[]> sent = new List<byte[]>();
            for (int i = 0; i < 150; i++)
            {
                byte[] payload = Enumerable.Range(0, 1 + i * 7).Select(value => (byte)(value + i)).ToArray();
                sent.Add(payload);
                Assert.That(_backend.Send(sender, payload, DeliveryMode.Reliable), Is.True, $"message {i}");
            }

            // Everything together stays under Steam's 512 KiB send buffer.
            byte[] large = Enumerable.Range(0, 200 * 1024).Select(value => (byte)value).ToArray();
            sent.Add(large);
            Assert.That(_backend.Send(sender, large, DeliveryMode.Reliable), Is.True);

            int kicked = ReasonCodes.ToWire(MultiplayerReason.Kicked);
            _backend.Disconnect(sender, kicked);
            Pump(() => recorder.DisconnectCode >= 0, 10);

            Assert.That(recorder.Received.Count, Is.EqualTo(sent.Count));
            for (int i = 0; i < sent.Count; i++)
            {
                Assert.That(recorder.Received[i], Is.EqualTo(sent[i]), $"message {i}");
            }

            Assert.That(recorder.Events.Last(), Is.EqualTo("disconnected " + receiver));
            Assert.That(recorder.DisconnectKind, Is.EqualTo(BackendDisconnectKind.ClosedByPeer));
            Assert.That(recorder.DisconnectCode, Is.EqualTo(kicked));
            Assert.That(_backend.Send(sender, new byte[] { 1 }, DeliveryMode.Reliable), Is.False, "a connection this side closed refuses data");
        }

        [Test]
        public void SteamLobby_IsCreatedListedAndFound_ThenLeft()
        {
            // FR-5, FR-6 on Steam itself: Steam publishes lobby data a moment after it is set.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                string gameId = "mu3.test." + Guid.NewGuid().ToString("N").Substring(0, 12);
                Assert.That(manager.Initialize(_backend, new MultiplayerConfig(gameId, 1)).IsSuccess, Is.True);

                MultiplayerResult? created = null;
                manager.CreateLobby(new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Mu3 test" }, result => created = result);
                Pump(manager, () => created.HasValue, 20);
                Assert.That(created?.IsSuccess, Is.True);

                LobbyId lobby = manager.CurrentLobbyId;
                Assert.That(_backend.IsLobbyMember(lobby, _backend.LocalPlayerId), Is.True);
                Assert.That(_backend.GetLobbyOwner(lobby), Is.EqualTo(_backend.LocalPlayerId));
                Assert.That(_backend.GetLobbyData(lobby, LobbyDataKeys.WireVersion), Is.EqualTo(WireProtocol.VersionText));

                bool found = false;
                Stopwatch watch = Stopwatch.StartNew();
                while (!found && watch.Elapsed.TotalSeconds < 30)
                {
                    LobbySearchResult? search = null;
                    manager.SearchLobbies(new LobbySearchQuery { Distance = LobbyDistance.Worldwide }, result => search = result);
                    Pump(manager, () => search.HasValue, 25);
                    Assert.That(search?.IsSuccess, Is.True);
                    found = search.Value.Lobbies.Any(info => info.Id == lobby && info.Name == "Mu3 test");
                    if (!found)
                    {
                        Pump(manager, () => false, 2);
                    }
                }

                Assert.That(found, Is.True, "the lobby never showed up in a search filtered by its game ID");

                manager.Leave();
                Assert.That(manager.State, Is.EqualTo(SessionState.Offline));
            }
            finally
            {
                manager.Dispose();
            }
        }

        private void Pump(Func<bool> done, int seconds)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (!done() && watch.Elapsed.TotalSeconds < seconds)
            {
                _backend.Update();
                Thread.Sleep(20);
            }
        }

        private static void Pump(MultiplayerManager manager, Func<bool> done, int seconds)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (!done() && watch.Elapsed.TotalSeconds < seconds)
            {
                manager.Update();
                Thread.Sleep(20);
            }
        }
    }
}
#endif
