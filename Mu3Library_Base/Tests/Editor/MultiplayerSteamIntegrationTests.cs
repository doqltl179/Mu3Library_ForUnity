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
using UnityEngine.TestTools.Constraints;
// Unity's Is extends NUnit's with AllocatingGCMemory.
using Is = UnityEngine.TestTools.Constraints.Is;

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
        public void SteamConnection_ReportsItsQuality_UntilItCloses()
        {
            // FR-36 on Steam's own measure of a connection, which needs packets on the network.
            Recorder recorder = new Recorder();
            _backend.SetListener(recorder);
            Assert.That(_backend.CreateLoopbackPair(out ConnectionId sender, out ConnectionId receiver, throughNetwork: true), Is.True);

            // Steam reports the data rates once it has measured a few seconds of traffic.
            byte[] payload = new byte[1024];
            ConnectionQuality sending = default;
            ConnectionQuality receiving = default;
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < 15.0 && !(sending.BytesSentPerSecond > 0f && receiving.BytesReceivedPerSecond > 0f))
            {
                Assert.That(_backend.Send(sender, payload, DeliveryMode.Unreliable), Is.True);
                _backend.Update();
                Thread.Sleep(20);
                Assert.That(_backend.TryGetConnectionQuality(sender, out sending), Is.True);
                Assert.That(_backend.TryGetConnectionQuality(receiver, out receiving), Is.True);
            }

            TestContext.WriteLine($"Steam measured the data rates after {watch.Elapsed.TotalSeconds:0.0} s.");
            TestContext.WriteLine($"Steam measured the sender as {sending} and the receiver as {receiving}.");
            Assert.That(sending.PingMilliseconds, Is.InRange(0, 1000));
            Assert.That(sending.PacketLoss, Is.InRange(0f, 1f));
            Assert.That(receiving.PacketLoss, Is.InRange(0f, 1f));
            Assert.That(sending.BytesSentPerSecond, Is.GreaterThan(0f));
            Assert.That(receiving.BytesReceivedPerSecond, Is.GreaterThan(0f));
            Assert.That(recorder.Received, Is.Not.Empty);

            // From here on the backend answers from what Steam last said, which the loop above
            // asked for, so no read below asks Steam again.
            _backend.QualityRefreshSeconds = 3600.0;
            Assert.That(() => { _backend.TryGetConnectionQuality(sender, out _); }, Is.Not.AllocatingGCMemory(), "a read between Steam's answers");
            Assert.That(_backend.TryGetConnectionQuality(receiver, out _), Is.True);

            Assert.That(_backend.TryGetConnectionQuality(new ConnectionId(uint.MaxValue), out _), Is.False);
            _backend.Disconnect(sender, 0);
            Assert.That(_backend.TryGetConnectionQuality(sender, out _), Is.False);

            // The other side answers false right after the callback run that reports the close,
            // while its last answer from Steam still stands and before the backend hands the close on.
            bool answers = true;
            Stopwatch closing = Stopwatch.StartNew();
            while (answers && closing.Elapsed.TotalSeconds < 10.0)
            {
                Thread.Sleep(20);
                Steamworks.SteamAPI.RunCallbacks();
                answers = _backend.TryGetConnectionQuality(receiver, out _);
            }

            Assert.That(answers, Is.False, "Steam reported the close");
            Assert.That(recorder.Events, Does.Not.Contain("disconnected " + receiver), "the close is not handed on yet");
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

        [Test]
        public void SteamLobby_TakesTheHostsLobbyAndParticipantData_DuringTheSession()
        {
            // FR-11 on Steam itself, with one account: the host's side of both kinds of data.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                List<(string Key, string Value)> lobbyChanges = new List<(string, string)>();
                List<(Participant Participant, string Key)> dataChanges = new List<(Participant, string)>();
                manager.OnLobbyDataChanged += (key, value) => lobbyChanges.Add((key, value));
                manager.OnParticipantDataChanged += (participant, key) => dataChanges.Add((participant, key));
                LobbyId lobby = CreateFriendsOnlyLobby(manager, "forest");

                Assert.That(manager.SetLobbyData("map", "desert").IsSuccess, Is.True);
                Assert.That(_backend.GetLobbyData(lobby, "map"), Is.EqualTo("desert"));
                Assert.That(manager.LobbyData["map"], Is.EqualTo("desert"));
                Assert.That(lobbyChanges, Is.EqualTo(new[] { ("map", "desert") }));
                Assert.That(manager.SetLobbyData(LobbyDataKeys.State, LobbyDataKeys.StateInGame).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
                Assert.That(_backend.GetLobbyData(lobby, LobbyDataKeys.State), Is.EqualTo(LobbyDataKeys.StateLobby));

                Assert.That(manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
                Assert.That(manager.Participants.Single().Data["character"], Is.EqualTo("knight"));
                Assert.That(dataChanges.Select(change => (change.Participant.Id, change.Key)), Is.EqualTo(new[] { (_backend.LocalPlayerId, "character") }));

                manager.Leave();
                Assert.That(manager.LobbyData, Is.Empty);
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamLobby_HostHearsItsOwnChat_AndAMessageOverSteamsLimitGoesNowhere()
        {
            // FR-12 on Steam itself, with one account: the host's side of lobby chat.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                List<(Participant Sender, string Text)> chats = new List<(Participant, string)>();
                manager.OnChatReceived += (sender, text) => chats.Add((sender, text));
                CreateFriendsOnlyLobby(manager, "forest");

                Assert.That(manager.SendChat("hello").IsSuccess, Is.True);
                Assert.That(manager.SendChat(new string('x', MultiplayerLimits.MaxChatMessageSize + 1)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
                Assert.That(chats.Select(chat => (chat.Sender.Id, chat.Text)), Is.EqualTo(new[] { (_backend.LocalPlayerId, "hello") }));

                manager.Leave();
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamLobby_KeepsTheReservedKeys_WhateverCaseAGameWritesThemIn()
        {
            // FR-11: whether Steam tells lobby data keys apart by case decides whether a game key
            // such as "MU3.state" could reach a reserved one; the reserved keys stay the library's either way.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                LobbyId lobby = CreateFriendsOnlyLobby(manager, "forest");

                Assert.That(_backend.SetLobbyData(lobby, "Probe.Case", "upper"), Is.True);
                string lower = _backend.GetLobbyData(lobby, "probe.case");
                TestContext.WriteLine(lower == "upper"
                    ? "Steam folds the case of lobby data keys."
                    : $"Steam keeps the case of lobby data keys (lower-case key reads '{lower ?? "null"}').");

                Assert.That(manager.SetLobbyData("MU3.State", LobbyDataKeys.StateInGame).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
                Assert.That(manager.SetLobbyData("MU3.HOST", "0").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
                Assert.That(_backend.GetLobbyData(lobby, LobbyDataKeys.State), Is.EqualTo(LobbyDataKeys.StateLobby));
                Assert.That(_backend.GetLobbyData(lobby, LobbyDataKeys.HostId), Is.EqualTo(_backend.LocalPlayerId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

                manager.Leave();
            }
            finally
            {
                manager.Dispose();
            }
        }

        /// <summary>Creates a lobby nobody finds in search, with one game-defined value.</summary>
        private LobbyId CreateFriendsOnlyLobby(MultiplayerManager manager, string map)
        {
            string gameId = "mu3.test." + Guid.NewGuid().ToString("N").Substring(0, 12);
            Assert.That(manager.Initialize(_backend, new MultiplayerConfig(gameId, 1)).IsSuccess, Is.True);

            LobbyCreateOptions options = new LobbyCreateOptions { Visibility = LobbyVisibility.FriendsOnly, Name = "Mu3 test" };
            options.Data["map"] = map;
            MultiplayerResult? created = null;
            manager.CreateLobby(options, result => created = result);
            Pump(manager, () => created.HasValue, 20);
            Assert.That(created?.IsSuccess, Is.True);

            LobbyId lobby = manager.CurrentLobbyId;
            Assert.That(_backend.GetLobbyData(lobby, "map"), Is.EqualTo(map));
            return lobby;
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
