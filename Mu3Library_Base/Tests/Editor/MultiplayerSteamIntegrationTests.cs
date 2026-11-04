#if MU3LIBRARY_STEAMWORKS_SUPPORT && UNITY_STANDALONE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Protocol;
using Mu3Library.Multiplayer.Steam;
using NUnit.Framework;
using UnityEngine.TestTools;
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

        // Robin Walker of Valve (steamcommunity.com/id/robinwalker), a public profile since 2003.
        private const ulong PublicAccountId = 76561197960435530UL;

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
            public void OnAvatarChanged(PlayerId player) { }
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

                // FR-36: the host's own entry of the pings every player reads; a client's needs a second account.
                Assert.That(manager.TryGetParticipantPing(_backend.LocalPlayerId, out int ping), Is.True);
                Assert.That(ping, Is.Zero);
                Assert.That(manager.TryGetParticipantPing(new PlayerId(_backend.LocalPlayerId.Value + 1), out _), Is.False);
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

        [Test]
        public void SteamLobby_RefusesAKeyThatDiffersOnlyByCase_AndSearchFindsAFilterKeyAndValueInAnyCase()
        {
            // FR-11, FR-38: Steam keeps one value for keys that differ only by case, so the manager
            // refuses the second key on every backend, and the local backends match filter keys
            // and values in any case because Steam does (#239).
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                LobbyId lobby = CreatePublicLobby(manager, "Map", "desert");

                Assert.That(manager.SetLobbyData("map", "forest").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
                Assert.That(manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["Map"] = "desert" }));
                Assert.That(manager.LobbyData["map"], Is.EqualTo("desert"));
                Assert.That(_backend.GetLobbyData(lobby, "Map"), Is.EqualTo("desert"));
                Assert.That(_backend.GetLobbyData(lobby, "map"), Is.EqualTo("desert"), "Steam reads a key in any case");

                LobbyInfo found = SearchUntil(manager, () => Worldwide().AddStringFilter("Map", "desert"), lobby);
                Assert.That(found, Is.Not.Null, "the lobby never showed up in a search filtered by its key as spelled");
                // Steam's search may spell a key otherwise than the host wrote it, so a result finds it in any case.
                TestContext.WriteLine($"Steam search hands the key back as '{string.Join("', '", found.Data.Keys)}'.");
                Assert.That(found.Data.Count, Is.EqualTo(1));
                Assert.That(found.Data["Map"], Is.EqualTo("desert"));
                Assert.That(found.Data["map"], Is.EqualTo("desert"));

                Assert.That(SearchUntil(manager, () => Worldwide().AddStringFilter("MAP", "desert"), lobby, seconds: 10), Is.Not.Null,
                    "Steam matches a filter key in any case, as the local backends do");
                Assert.That(SearchUntil(manager, () => Worldwide().AddStringFilter("Map", "DESERT"), lobby, seconds: 10), Is.Not.Null,
                    "Steam matches a filter value in any case, as the local backends do");

                manager.Leave();
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamLobby_ComparesStringFilterValues_AsTheLocalBackendsDo()
        {
            // FR-6, FR-38, #239, #286: all six comparisons of a string filter on Steam itself,
            // on the lobby's values and on a key the lobby lacks or holds empty, against the
            // results the local backends' tests hold every backend to.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                Dictionary<string, string> data = MultiplayerSessionTests.StringComparisonData();
                LobbyId lobby = CreatePublicLobby(manager, data);
                LobbyInfo found = null;
                foreach (KeyValuePair<string, string> pair in data.Where(pair => pair.Value.Length > 0))
                {
                    found = SearchUntil(manager, () => Worldwide().AddStringFilter(pair.Key, pair.Value), lobby);
                    Assert.That(found, Is.Not.Null, $"the lobby never showed up in a search filtered by {pair.Key}");
                }

                Assert.That(found.Data.ContainsKey(MultiplayerSessionTests.EmptyStringKey), Is.False,
                    "a lobby created with a key written empty keeps no such key, so no filter can keep the lobby by it");

                // Every value reached search's filters above, so a filter that drops the lobby
                // drops it at once, and one that keeps it is asked a little longer.
                List<string> mismatches = new List<string>();
                foreach ((string key, string filter, LobbyComparison comparison, bool isKept) in MultiplayerSessionTests.StringComparisonChecks())
                {
                    bool isFound = SearchUntil(manager, () => Worldwide().AddStringFilter(key, filter, comparison), lobby, seconds: isKept ? 10 : 0) != null;
                    if (isFound != isKept)
                    {
                        mismatches.Add(MultiplayerSessionTests.DescribeStringComparison(key, filter, comparison, isKept));
                    }
                }

                Assert.That(mismatches, Is.Empty);
                manager.Leave();
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamLobby_ReadsAKeyTheLobbyLacks_AsZero_InNumericFilters_AsTheLocalBackendsDo()
        {
            // FR-6, FR-38, #353: numeric filters on a key the lobby lacks on Steam itself, against
            // the results the local backends' tests hold every backend to.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                LobbyId lobby = CreatePublicLobby(manager, MultiplayerSessionTests.KeylessFilterLobbyKey, MultiplayerSessionTests.KeylessFilterLobbyValue);
                LobbyInfo listed = SearchUntil(manager,
                    () => Worldwide().AddStringFilter(MultiplayerSessionTests.KeylessFilterLobbyKey, MultiplayerSessionTests.KeylessFilterLobbyValue), lobby);
                Assert.That(listed, Is.Not.Null, "the lobby never showed up in a search filtered by its key");
                Assert.That(listed.Data.ContainsKey(MultiplayerSessionTests.MissingNumericKey), Is.False);

                // The lobby reached search's filters above, so a filter that drops the lobby
                // drops it at once, and one that keeps it is asked a little longer.
                List<string> results = new List<string>();
                List<string> mismatches = new List<string>();
                foreach ((string description, LobbySearchQuery query, bool isKept) in MultiplayerSessionTests.MissingKeyNumericFilterChecks())
                {
                    query.Distance = LobbyDistance.Worldwide;
                    bool isFound = SearchUntil(manager, () => query, lobby, seconds: isKept ? 10 : 0) != null;
                    results.Add($"{description} {(isFound ? "keeps" : "drops")}");
                    if (isFound != isKept)
                    {
                        mismatches.Add($"{description} should {(isKept ? "keep" : "drop")} the lobby");
                    }
                }

                TestContext.WriteLine("Numeric filters on a key the lobby lacks: " + string.Join("; ", results) + ".");
                Assert.That(mismatches, Is.Empty);
                manager.Leave();
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamLobby_OpenedForAReturningPlayer_DuringAGameWithoutLateJoiners_StaysOutOfTheLibrarysSearch()
        {
            // FR-19, FR-20, #253: the host opens such a lobby while a player back in its slot is
            // outside it, and Steam's own search then lists the joinable lobby; the library's does not.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                LobbyId lobby = CreatePublicLobby(manager, "Map", "desert");
                Assert.That(SearchUntil(manager, Worldwide, lobby), Is.Not.Null, "the lobby never showed up in the library's search");
                Assert.That(manager.StartGame().IsSuccess, Is.True);
                Assert.That(_backend.GetLobbyData(lobby, LobbyDataKeys.Closed), Is.EqualTo(LobbyDataKeys.ClosedForTheGame));

                // As the host does for a returning player outside the lobby.
                Assert.That(_backend.SetLobbyJoinable(lobby, true), Is.True);
                Assert.That(SteamListsUntil(manager, lobby, data => data.TryGetValue(LobbyDataKeys.Closed, out string closed) && closed == LobbyDataKeys.ClosedForTheGame), Is.True,
                    "Steam's own search never listed the opened lobby as closed for the game");

                // Steam's filters may follow the data a moment later (#249).
                bool isFound = SearchUntil(manager, Worldwide, result => result.Lobbies.Any(info => info.Id == lobby), found => !found, 30);
                Assert.That(isFound, Is.False, "the library's search leaves the opened lobby out");
                Assert.That(SteamListsUntil(manager, lobby, data => true), Is.True, "Steam's own search still lists the opened lobby");
                Assert.That(SearchUntil(manager, Worldwide, lobby, seconds: 5), Is.Null, "the library's search keeps the opened lobby out");

                manager.Leave();
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamLobby_DeletesALobbyDataKey_FromTheLobbyAndFromSearch()
        {
            // FR-11 on Steam itself: a deleted key leaves the lobby, what search returns, and what
            // it filters on; the host's own participant data loses a key the same way.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                List<(string Key, string Value)> lobbyChanges = new List<(string, string)>();
                List<(Participant Participant, string Key)> dataChanges = new List<(Participant, string)>();
                manager.OnLobbyDataChanged += (key, value) => lobbyChanges.Add((key, value));
                manager.OnParticipantDataChanged += (participant, key) => dataChanges.Add((participant, key));
                LobbyId lobby = CreatePublicLobby(manager, "Map", "desert");
                Assert.That(manager.SetLobbyData("Mode", "coop").IsSuccess, Is.True);
                Assert.That(SearchUntil(manager, () => Worldwide().AddStringFilter("Map", "desert"), lobby), Is.Not.Null,
                    "the lobby never showed up in a search filtered by the key");

                Assert.That(manager.DeleteLobbyData("Map").IsSuccess, Is.True);
                Assert.That(_backend.GetLobbyData(lobby, "Map"), Is.Null);
                Assert.That(manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["Mode"] = "coop" }));
                Assert.That(lobbyChanges.Last(), Is.EqualTo(("Map", (string)null)));

                LobbyInfo after = SearchUntil(manager, Worldwide, result => result.Lobbies.FirstOrDefault(info => info.Id == lobby),
                    info => info != null && !info.Data.ContainsKey("Map"), 30);
                Assert.That(after, Is.Not.Null, "search never dropped the deleted key");
                Assert.That(after.Data["Mode"], Is.EqualTo("coop"));

                // Steam's filters follow a deletion later than the data a search returns, for up
                // to about a second past it on AppID 480 (#249), so search is asked again until
                // it no longer finds the lobby by the deleted key.
                bool isStillFound = SearchUntil(manager, () => Worldwide().AddStringFilter("Map", "desert"),
                    result => result.Lobbies.Any(info => info.Id == lobby), found => !found, 30);
                Assert.That(isStillFound, Is.False, "search no longer finds the lobby by the deleted key");

                Assert.That(manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
                Assert.That(manager.DeleteParticipantData("character").IsSuccess, Is.True);
                Assert.That(manager.Participants.Single().Data, Is.Empty);
                Assert.That(dataChanges.Select(change => change.Key), Is.EqualTo(new[] { "character", "character" }));

                manager.Leave();
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamLobby_LosesAKeyWrittenEmpty_FromTheLobbyAndSearch_AsTheLocalBackendsDo()
        {
            // FR-11, FR-38, #313 on Steam itself: the manager deletes a key written with an empty
            // value, so the key leaves the owner's lobby, what search returns, and every copy, and
            // a lobby created with one starts without it, as the local backends' tests hold them
            // to. What the backend itself does with an empty value, which the manager keeps from
            // happening, is the next test's.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                List<(string Key, string Value)> lobbyChanges = new List<(string, string)>();
                manager.OnLobbyDataChanged += (key, value) => lobbyChanges.Add((key, value));
                LobbyId lobby = CreatePublicLobby(manager, new Dictionary<string, string> { ["Map"] = "desert", ["e1"] = string.Empty });
                Assert.That(manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["Map"] = "desert" }), "the lobby starts without the key written empty");
                Assert.That(manager.SetLobbyData("Mode", "coop").IsSuccess, Is.True);
                Assert.That(SearchUntil(manager, () => Worldwide().AddStringFilter("Mode", "coop"), lobby), Is.Not.Null,
                    "the lobby never showed up in a search filtered by the key");

                Assert.That(manager.SetLobbyData("Mode", string.Empty).IsSuccess, Is.True);
                Assert.That(manager.SetLobbyData("never", string.Empty).IsSuccess, Is.True, "a key the lobby data does not hold changes nothing");
                Assert.That(lobbyChanges.Last(), Is.EqualTo(("Mode", (string)null)));
                Assert.That(manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["Map"] = "desert" }));
                Assert.That(_backend.GetLobbyData(lobby, "Mode"), Is.Null);
                Pump(manager, () => OwnLobbyGameKeys(lobby).Count == 1, 10);
                List<string> owned = OwnLobbyGameKeys(lobby);
                TestContext.WriteLine($"The owner's lobby holds the game keys '{string.Join("', '", owned)}'.");
                Assert.That(owned.Select(key => key.ToLowerInvariant()), Is.EquivalentTo(new[] { "map" }), "the owner's lobby keeps neither key written empty");

                LobbyInfo after = SearchUntil(manager, Worldwide, result => result.Lobbies.FirstOrDefault(info => info.Id == lobby),
                    info => info != null && !info.Data.ContainsKey("Mode"), 30);
                Assert.That(after, Is.Not.Null, "search never dropped the key written empty");
                Assert.That(after.Data.Count, Is.EqualTo(1), $"search returns the keys '{string.Join("', '", after.Data.Keys)}'");
                Assert.That(after.Data["Map"], Is.EqualTo("desert"));

                manager.Leave();
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamLobby_KeepsOnlyAKeyItHoldsSetEmptyThroughTheBackend_AndFiltersIt_AsTheLocalBackendsDo()
        {
            // #343 on Steam itself: the backend called directly with an empty value, which the
            // manager never does (#313). Steam writes no key the lobby lacks, keeps a key it holds
            // with the empty value, which search returns, and every filter on that value gives
            // what the local backends' tests hold them to.
            string key = MultiplayerSessionTests.EmptyValueKey;
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                LobbyId lobby = CreatePublicLobby(manager, "Map", "desert");
                Assert.That(_backend.SetLobbyData(lobby, key, "v"), Is.True);
                Assert.That(SearchUntil(manager, () => Worldwide().AddStringFilter(key, "v"), lobby), Is.Not.Null,
                    "the lobby never showed up in a search filtered by the key set through the backend");

                Assert.That(_backend.SetLobbyData(lobby, "never", string.Empty), Is.True, "an empty value for a key the lobby lacks");
                Assert.That(_backend.SetLobbyData(lobby, key, string.Empty), Is.True);
                Assert.That(_backend.GetLobbyData(lobby, key), Is.Null, "the backend reads an empty value as none");
                Assert.That(_backend.DeleteLobbyData(lobby, "never"), Is.False, "Steam refuses to delete a key the lobby lacks");
                List<string> owned = OwnLobbyGameKeys(lobby);
                Assert.That(owned.Select(name => name.ToLowerInvariant()), Is.EquivalentTo(new[] { "map", key }),
                    $"the owner's lobby holds the game keys '{string.Join("', '", owned)}'");

                LobbyInfo listed = SearchUntil(manager, Worldwide, result => result.Lobbies.FirstOrDefault(info => info.Id == lobby),
                    info => info != null && info.Data.TryGetValue(key, out string value) && value.Length == 0, 15);
                Assert.That(listed, Is.Not.Null, "Steam's search never returned the key with the empty value it was set to");

                // The value reached search's filters above, so a filter that drops the lobby drops
                // it at once, and one that keeps it is asked a little longer.
                List<string> results = new List<string>();
                List<string> mismatches = new List<string>();
                foreach ((string description, Func<LobbySearchQuery, LobbySearchQuery> addTo, bool isKept) in MultiplayerSessionTests.EmptyValueFilterChecks())
                {
                    bool isFound = SearchUntil(manager, () => addTo(Worldwide()), lobby, seconds: isKept ? 10 : 0) != null;
                    results.Add($"{description} {(isFound ? "keeps" : "drops")}");
                    if (isFound != isKept)
                    {
                        mismatches.Add($"{description} should {(isKept ? "keep" : "drop")} the lobby");
                    }
                }

                TestContext.WriteLine("Filters on a key Steam holds with an empty value: " + string.Join("; ", results) + ".");
                Assert.That(mismatches, Is.Empty);
                manager.Leave();
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamAvatar_OfThisAccount_ArrivesAsATextureOfSteamsSize_TheRightWayUp()
        {
            // FR-4 on Steam itself, with this account's own avatar; another account's follows.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                Assert.That(manager.Initialize(_backend, new MultiplayerConfig("mu3.test.avatar", 1)).IsSuccess, Is.True);
                PlayerId self = _backend.LocalPlayerId;
                Steamworks.CSteamID steamId = new Steamworks.CSteamID(self.Value);
                List<UnityEngine.Texture2D> textures = new List<UnityEngine.Texture2D>();

                foreach ((AvatarSize size, int side) in new[] { (AvatarSize.Small, 32), (AvatarSize.Medium, 64), (AvatarSize.Large, 184) })
                {
                    AvatarResult? result = null;
                    Stopwatch watch = Stopwatch.StartNew();
                    manager.LoadAvatar(self, size, r => result = r);
                    Pump(manager, () => result.HasValue, 15);
                    TestContext.WriteLine($"Steam answered the {size} avatar with {result?.Reason.ToString() ?? "nothing"} after {watch.Elapsed.TotalSeconds:0.00} s.");
                    Assert.That(result?.IsSuccess, Is.True, result?.Reason.ToString());

                    UnityEngine.Texture2D texture = result.Value.Texture;
                    Assert.That(texture != null, Is.True);
                    Assert.That((texture.width, texture.height), Is.EqualTo((side, side)));

                    // Steam's own copy runs from the top row down, the texture from the bottom row up.
                    int image = size == AvatarSize.Small ? Steamworks.SteamFriends.GetSmallFriendAvatar(steamId)
                        : size == AvatarSize.Medium ? Steamworks.SteamFriends.GetMediumFriendAvatar(steamId)
                        : Steamworks.SteamFriends.GetLargeFriendAvatar(steamId);
                    byte[] steam = new byte[side * side * 4];
                    Assert.That(Steamworks.SteamUtils.GetImageRGBA(image, steam, steam.Length), Is.True);

                    UnityEngine.Color32[] pixels = texture.GetPixels32();
                    int mismatches = 0;
                    bool readsTheSameUpsideDown = true;
                    for (int y = 0; y < side; y++)
                    {
                        for (int x = 0; x < side; x++)
                        {
                            UnityEngine.Color32 pixel = pixels[y * side + x];
                            int i = ((side - 1 - y) * side + x) * 4;
                            if (pixel.r != steam[i] || pixel.g != steam[i + 1] || pixel.b != steam[i + 2] || pixel.a != steam[i + 3])
                            {
                                mismatches++;
                            }

                            int mirrored = (y * side + x) * 4;
                            if (steam[i] != steam[mirrored] || steam[i + 1] != steam[mirrored + 1] || steam[i + 2] != steam[mirrored + 2])
                            {
                                readsTheSameUpsideDown = false;
                            }
                        }
                    }

                    TestContext.WriteLine(readsTheSameUpsideDown
                        ? $"The {size} avatar reads the same upside down, so it cannot show the orientation."
                        : $"The {size} avatar differs upside down, so the comparison checks the orientation.");
                    Assert.That(mismatches, Is.Zero, "pixels that differ from Steam's, the top row taken as the texture's top");

                    AvatarResult? again = null;
                    manager.LoadAvatar(self, size, r => again = r);
                    Assert.That(again?.Texture, Is.SameAs(texture), "the same texture, at once");
                    textures.Add(texture);
                }

                manager.Dispose();
                Assert.That(textures.TrueForAll(texture => texture == null), Is.True, "disposing the manager destroys its textures");
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamAvatar_OnceSteamShutsDown_AnswersSteamNotRunning_ToTheRequestWaitingAndToANewOne()
        {
            // FR-4, #318: an avatar answers the reason the lobby requests answer once Steam is gone,
            // the one the manager answers itself then. An account this client is unlikely to know makes Steam download it first.
            // No update runs before Steam shuts down, since Steam can download an avatar within one.
            PlayerId stranger = new PlayerId(76561197960265728UL + (ulong)new System.Random().Next(1 << 20, int.MaxValue));
            MultiplayerReason? waiting = null;
            _backend.LoadAvatar(stranger, AvatarSize.Large, (reason, avatar) => waiting = reason);
            if (_backend.PendingAvatarCount == 0)
            {
                Assert.Ignore($"Steam held the avatar of {stranger} already, so no request waited.");
            }

            // Steam shut down under the backend, as a game may do; TearDown then releases only the
            // backend's own resources, and the next SetUp starts Steam again.
            Steamworks.SteamAPI.Shutdown();
            Assert.That(_backend.IsAvailable, Is.False);
            MultiplayerReason? later = null;
            _backend.LoadAvatar(stranger, AvatarSize.Medium, (reason, avatar) => later = reason);
            _backend.Update();

            Assert.That(waiting, Is.EqualTo(MultiplayerReason.SteamNotRunning), "the request that waited");
            Assert.That(later, Is.EqualTo(MultiplayerReason.SteamNotRunning), "the request made after Steam shut down");
            Assert.That(_backend.PendingAvatarCount, Is.Zero);
        }

        [TestCase(false, TestName = "SteamLobby_RequestsSteamHasNotAnswered_AnswerSteamNotRunningOnce_AfterASteamCallFailed")]
        [TestCase(true, TestName = "SteamLobby_RequestsSteamHasNotAnswered_AnswerSteamNotRunningOnce_AfterSteamShutDown")]
        public void SteamLobby_RequestsSteamHasNotAnswered_AnswerSteamNotRunningOnce_OnceSteamIsGone(bool shutsSteamDown)
        {
            // The backend contract: every lobby request answers once, also when Steam is gone
            // before it answered. No update runs before, so Steam's answers wait in its queue.
            List<string> answers = new List<string>();
            _backend.CreateLobby(LobbyVisibility.InviteOnly, 2, null, (reason, lobby) => answers.Add("create " + reason));
            _backend.JoinLobby(new LobbyId(109775241000000001UL), reason => answers.Add("join " + reason));

            if (shutsSteamDown)
            {
                // As a game may do; TearDown then releases only the backend's own resources.
                Steamworks.SteamAPI.Shutdown();
            }
            else
            {
                // What a Steam call that throws EntryPointNotFoundException does.
                LogAssert.Expect(UnityEngine.LogType.Warning, new Regex(@"a Steam call failed \(EntryPointNotFoundException\): simulated"));
                _backend.Break(new EntryPointNotFoundException("simulated"));
            }

            Assert.That(_backend.IsAvailable, Is.False);
            Assert.That(_backend.InviteFriend(new LobbyId(109775241000000001UL), new PlayerId(PublicAccountId)).Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning),
                "an invite answers as the lobby requests do");
            Pump(() => false, 1);

            Assert.That(answers, Is.EquivalentTo(new[] { "create SteamNotRunning", "join SteamNotRunning" }));
        }

        [Test]
        public void SteamLobby_AnAnswerSteamDispatchesAfterACallFailed_AnswersSteamNotRunning_WithoutCallingSteam()
        {
            // #272: Steam may dispatch an answer after a call failed, later in the same run of its
            // callbacks or in the game's own run of them, as here. The backend hands it on as one
            // Steam never answered, and does not write the lobby data of the lobby Steam made.
            List<string> answers = new List<string>();
            ulong entered = 0UL;

            // Steam enters the lobby it creates for this player, which tells the test its ID.
            Steamworks.Callback<Steamworks.LobbyEnter_t> enter = Steamworks.Callback<Steamworks.LobbyEnter_t>.Create(lobby => entered = lobby.m_ulSteamIDLobby);
            try
            {
                _backend.CreateLobby(LobbyVisibility.InviteOnly, 2, new Dictionary<string, string> { ["mu3_test"] = "late" }, (reason, lobby) => answers.Add("create " + reason));
                LogAssert.Expect(UnityEngine.LogType.Warning, new Regex(@"a Steam call failed \(EntryPointNotFoundException\): simulated"));
                _backend.Break(new EntryPointNotFoundException("simulated"));

                Stopwatch watch = Stopwatch.StartNew();
                while ((_backend.PendingLobbyCallCount > 0 || entered == 0UL) && watch.Elapsed.TotalSeconds < 20.0)
                {
                    Steamworks.SteamAPI.RunCallbacks();
                    Thread.Sleep(20);
                }

                Assert.That(_backend.PendingLobbyCallCount, Is.Zero, "Steam answered the creation");
                Assert.That(answers, Is.Empty, "the answer waits for the backend's update");
                _backend.Update();
                Assert.That(answers, Is.EqualTo(new[] { "create SteamNotRunning" }));

                Assert.That(entered, Is.Not.EqualTo(0UL), "Steam made the lobby");
                Assert.That(Steamworks.SteamMatchmaking.GetLobbyData(new Steamworks.CSteamID(entered), "mu3_test"), Is.Empty, "no lobby data written after the failure");

                _backend.Update();
                Assert.That(answers.Count, Is.EqualTo(1), "answered once");
            }
            finally
            {
                enter.Dispose();
                if (entered != 0UL)
                {
                    Steamworks.SteamMatchmaking.LeaveLobby(new Steamworks.CSteamID(entered));
                }
            }
        }

        [Test]
        public void SteamConnectionsAndLobbies_AreClosedAndLeft_WhenASteamCallFails()
        {
            // #272: Steam's client still runs after the failure, so the backend closes its
            // connections and leaves its lobbies at once, as disposing it does, and the other
            // players need not wait for this one to dispose it. The loopback pair's two ends are
            // both this backend's.
            Recorder recorder = new Recorder();
            _backend.SetListener(recorder);
            Assert.That(_backend.CreateLoopbackPair(out ConnectionId first, out ConnectionId second), Is.True);
            bool isCreated = false;
            LobbyId lobby = LobbyId.None;
            _backend.CreateLobby(LobbyVisibility.InviteOnly, 2, null, (reason, made) =>
            {
                isCreated = true;
                lobby = made;
            });
            Pump(() => isCreated, 20);
            Assert.That(lobby.IsValid, Is.True);
            Assert.That(IsOwnLobbyMember(lobby), Is.True);
            Assert.That(Steamworks.SteamNetworkingSockets.GetConnectionInfo(new Steamworks.HSteamNetConnection(first.Value), out _), Is.True);
            recorder.Events.Clear();

            LogAssert.Expect(UnityEngine.LogType.Warning, new Regex(@"a Steam call failed \(EntryPointNotFoundException\): simulated"));
            _backend.Break(new EntryPointNotFoundException("simulated"));

            Assert.That(_backend.HeldConnectionCount, Is.Zero);
            Assert.That(_backend.HeldLobbyCount, Is.Zero);
            Assert.That(Steamworks.SteamNetworkingSockets.GetConnectionInfo(new Steamworks.HSteamNetConnection(first.Value), out _), Is.False, "the first end is closed");
            Assert.That(Steamworks.SteamNetworkingSockets.GetConnectionInfo(new Steamworks.HSteamNetConnection(second.Value), out _), Is.False, "the second end is closed");
            Assert.That(IsOwnLobbyMember(lobby), Is.False, "the lobby is left");

            // What Steam dispatches afterwards, in the game's own run of its callbacks, reaches nobody.
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.Elapsed.TotalSeconds < 1.0)
            {
                Steamworks.SteamAPI.RunCallbacks();
                _backend.Update();
                Thread.Sleep(20);
            }

            Assert.That(recorder.Events, Is.Empty);
        }

        [TestCase(false, TestName = "SteamSession_EndsWithSteamNotRunning_AfterASteamCallFailed")]
        [TestCase(true, TestName = "SteamSession_EndsWithSteamNotRunning_AfterSteamShutDown")]
        public void SteamSession_EndsWithSteamNotRunning_OnceSteamIsGone(bool shutsSteamDown)
        {
            // #272: the backend reports nothing more once Steam is gone, so the manager ends the
            // session on its next update; #273: a lobby creation then answers the same reason at once.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                LobbyId lobby = CreateFriendsOnlyLobby(manager, "forest");
                List<SessionStateChange> states = new List<SessionStateChange>();
                manager.OnStateChanged += states.Add;

                if (shutsSteamDown)
                {
                    // As a game may do; TearDown then releases only the backend's own resources.
                    Steamworks.SteamAPI.Shutdown();
                }
                else
                {
                    LogAssert.Expect(UnityEngine.LogType.Warning, new Regex(@"a Steam call failed \(EntryPointNotFoundException\): simulated"));
                    _backend.Break(new EntryPointNotFoundException("simulated"));
                    Assert.That(IsOwnLobbyMember(lobby), Is.False, "Steam's client still runs, and the backend left the lobby at the failure");
                }

                Assert.That(manager.State, Is.EqualTo(SessionState.Lobby), "until the manager's next update");
                manager.Update();
                Assert.That(states, Is.EqualTo(new[] { new SessionStateChange(SessionState.Lobby, SessionState.Offline, MultiplayerReason.SteamNotRunning) }));

                MultiplayerResult? created = null;
                manager.CreateLobby(new LobbyCreateOptions(), result => created = result);
                Assert.That(created?.Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning));

                manager.Update();
                Assert.That(states.Count, Is.EqualTo(1));
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamPresence_IsDeleted_WhenASteamCallFails()
        {
            // #319: Steam's client still runs after such a failure, so the backend deletes the keys
            // it set right then, as disposing it does; the friends list would otherwise show this
            // player's status and session group until the backend is disposed.
            Assert.That(_backend.SetPresence("Mu3 test: failure", new LobbyId(109775241000000001UL), 2), Is.True);
            Assert.That(OwnPresence(SteamBackend.StatusKey), Is.EqualTo("Mu3 test: failure"));
            Assert.That(OwnPresence(SteamBackend.GroupKey), Is.EqualTo("109775241000000001"));

            LogAssert.Expect(UnityEngine.LogType.Warning, new Regex(@"a Steam call failed \(EntryPointNotFoundException\): simulated"));
            _backend.Break(new EntryPointNotFoundException("simulated"));

            Assert.That(OwnPresence(SteamBackend.StatusKey), Is.Empty);
            Assert.That(OwnPresence(SteamBackend.GroupKey), Is.Empty);
            Assert.That(OwnPresence(SteamBackend.GroupSizeKey), Is.Empty);
        }

        [Test]
        public void SteamBackend_AfterSteamShutsDownAndStartsAgain_StaysUnavailable_AndLeavesTheNewSteamRunning()
        {
            // #322: Steamworks.NET let go of the backend's callbacks when Steam shut down, and its
            // sockets and lobbies belonged to that Steam, so the backend from before does not come
            // back with the new Steam. Disposing it touches nothing of the new Steam, which it
            // would otherwise shut down when it started the one before.
            Steamworks.SteamAPI.Shutdown();
            Assert.That(_backend.IsAvailable, Is.False);

            MultiplayerResult created = SteamBackend.TryCreate(new SteamBackendOptions { AppId = 480 }, out SteamBackend again);
            try
            {
                Assert.That(created.IsSuccess, Is.True, created.ToString());
                Assert.That(Steamworks.CallbackDispatcher.IsInitialized, Is.True, "Steam runs again");
                Assert.That(_backend.IsAvailable, Is.False, "the backend from before stays unavailable");
                Assert.That(_backend.InviteFriend(new LobbyId(109775241000000001UL), new PlayerId(PublicAccountId)).Reason, Is.EqualTo(MultiplayerReason.SteamNotRunning));

                _backend.Dispose();
                Assert.That(Steamworks.CallbackDispatcher.IsInitialized, Is.True, "the new Steam still runs");
                Assert.That(again.IsAvailable, Is.True);
                Assert.That(again.GetPlayerName(again.LocalPlayerId), Is.Not.Empty, "the new backend uses the new Steam");
            }
            finally
            {
                again?.Dispose();
            }
        }

        [Test]
        public void SteamAvatar_OfAPublicAccountThatIsNoFriend_IsDownloaded_AfterAskingSteamOnce()
        {
            // FR-4 through the path that downloads an avatar first: an account this one is no
            // friend of and shares no lobby with, so Steam knows little of it. Robin Walker's
            // account at Valve has had a public profile with a picture of its own since 2003, which
            // makes it about as stable as a public account gets. Only a Steam that is offline, or
            // that never learned the account's name, as for a deleted account, leaves the test
            // ignored; any other failure to bring the avatar fails it.
            PlayerId account = new PlayerId(PublicAccountId);
            Steamworks.CSteamID steamId = new Steamworks.CSteamID(account.Value);
            if (!Steamworks.SteamUser.BLoggedOn())
            {
                Assert.Ignore("Steam is not logged on to its servers, so it downloads nothing.");
            }

            if (Steamworks.SteamFriends.HasFriend(steamId, Steamworks.EFriendFlags.k_EFriendFlagImmediate))
            {
                Assert.Ignore($"{account} is a friend of this account, so Steam does not download the avatar.");
            }

            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                Assert.That(manager.Initialize(_backend, new MultiplayerConfig("mu3.test.avatar", 1)).IsSuccess, Is.True);
                List<AvatarResult> changes = new List<AvatarResult>();
                manager.OnAvatarChanged += changes.Add;

                AvatarResult? result = null;
                Stopwatch watch = Stopwatch.StartNew();
                manager.LoadAvatar(account, AvatarSize.Large, r => result = r);
                // Steam hands out no image (0) for an account it does not know yet, which the
                // backend asks it about, or one it still loads (-1); either way the request waits.
                bool downloads = _backend.PendingAvatarCount > 0;
                bool knewNothing = _backend.UserInformationRequestCount > 0;
                Pump(manager, () => result.HasValue, 15);
                TestContext.WriteLine($"Steam answered the large avatar of {account} with {result?.Reason.ToString() ?? "nothing"} after {watch.Elapsed.TotalSeconds:0.00} s; "
                    + (downloads ? (knewNothing ? "it knew nothing of the account at first" : "it was still loading the image") : "it handed out an image at once")
                    + $", and the backend asked Steam about the account {_backend.UserInformationRequestCount} time(s).");

                Assert.That(result.HasValue, Is.True, "every request answers, at the latest when its 10 seconds run out");
                if (!result.Value.IsSuccess)
                {
                    if (!Steamworks.SteamUser.BLoggedOn())
                    {
                        Assert.Ignore($"Steam went offline while it fetched {account} ({result.Value.Reason}).");
                    }

                    // A name that never arrived excuses the failure only once the backend asked
                    // Steam for it: a backend that stopped asking leaves the name unknown too.
                    string name = Steamworks.SteamFriends.GetFriendPersonaName(steamId);
                    bool isNameUnknown = string.IsNullOrEmpty(name) || name == "[unknown]";
                    Assert.That(!isNameUnknown || _backend.UserInformationRequestCount > 0, Is.True,
                        $"Steam never learned {account} ({result.Value.Reason}), and the backend never asked it to");
                    if (isNameUnknown)
                    {
                        Assert.Ignore($"Steam never learned {account} ({result.Value.Reason}) although the backend asked; the account may be gone.");
                    }
                }

                Assert.That(result.Value.IsSuccess, Is.True, result.Value.Reason.ToString());
                Assert.That(_backend.UserInformationRequestCount, Is.LessThanOrEqualTo(2), "asked once, and at most once more after Steam told of the account, not on every update");
                UnityEngine.Texture2D texture = result.Value.Texture;
                Assert.That((texture.width, texture.height), Is.EqualTo((184, 184)));

                // For an account it did not know, Steam hands out its default image at once and the
                // account's own picture once it downloaded it, which replaces the first image in
                // the same texture; the test waits for that notice, on every path, since Steam may
                // hand out the default image without a wait first.
                byte[] first = texture.GetRawTextureData();
                Pump(manager, () => changes.Count > 0, 10);
                Pump(manager, () => false, 1);
                bool isReplaced = !texture.GetRawTextureData().SequenceEqual(first);
                int mismatches = CountMismatches(texture, Steamworks.SteamFriends.GetLargeFriendAvatar(steamId));
                TestContext.WriteLine($"After {watch.Elapsed.TotalSeconds:0.00} s the texture {(mismatches == 0 ? "shows" : "still differs from")} the image Steam holds; "
                    + (isReplaced ? "it replaced the first image in place" : "the first image was the account's own")
                    + $", and the manager raised OnAvatarChanged {changes.Count} time(s).");

                Assert.That(mismatches, Is.Zero, "the texture shows the image Steam holds now");
                Assert.That(changes.TrueForAll(change => ReferenceEquals(change.Texture, texture)), Is.True, "the same texture, redrawn");
                Assert.That(changes.Count, Is.EqualTo(isReplaced ? 1 : 0), "raised once for a replaced image, although Steam sends two notices for it");
                if (!downloads && !isReplaced)
                {
                    Assert.Ignore($"Steam held the avatar of {account} already, so the download path did not run; it runs again once the Steam client restarts.");
                }
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamAvatar_OfAnAccountSteamCannotFind_AnswersOnce_AfterAskingSteamOnce()
        {
            // An account number far past the ones Steam has handed out: Steam knows nothing of it,
            // so the backend asks about it, not on every update, and the request answers once,
            // whatever Steam makes of it (its default image in the runs so far, or nothing until
            // the 10 seconds run out).
            PlayerId missing = new PlayerId(76561197960265728UL + 0xFFFFFF00UL + (ulong)new System.Random().Next(0, 0xF0));
            List<MultiplayerReason> answers = new List<MultiplayerReason>();
            Stopwatch watch = Stopwatch.StartNew();
            _backend.LoadAvatar(missing, AvatarSize.Medium, (reason, avatar) => answers.Add(reason));
            Pump(() => answers.Count > 0, 15);
            Pump(() => false, 1);
            TestContext.WriteLine($"Steam answered the avatar of {missing} with {(answers.Count > 0 ? answers[0].ToString() : "nothing")} after {watch.Elapsed.TotalSeconds:0.00} s, "
                + $"asked about the account {_backend.UserInformationRequestCount} time(s).");

            Assert.That(answers.Count, Is.EqualTo(1), "answered once");
            Assert.That(_backend.UserInformationRequestCount, Is.LessThanOrEqualTo(2), "asked once, and at most once more after Steam told of the account, not on every update");
            Assert.That(_backend.PendingAvatarCount, Is.Zero);
        }

        [Test]
        public void SteamPresence_ShowsTheStatusAndTheSessionAsAGroup_UntilTheSessionEnds()
        {
            // FR-13 and FR-28 on Steam itself, read back from this account's own rich presence:
            // how another account's friends list shows them is a manual check.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                LobbyId lobby = CreateFriendsOnlyLobby(manager, "forest");
                Assert.That(OwnPresence(SteamBackend.GroupKey), Is.EqualTo(lobby.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                Assert.That(OwnPresence(SteamBackend.GroupSizeKey), Is.EqualTo("1"));
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.Empty);

                Assert.That(manager.SetPresenceStatus("Mu3 test: 숲").IsSuccess, Is.True);
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.EqualTo("Mu3 test: 숲"));

                // The longest status the manager takes reads back whole; Steam itself refuses one byte more.
                string longest = new string('x', MultiplayerLimits.MaxPresenceStatusSize);
                Assert.That(manager.SetPresenceStatus(longest).IsSuccess, Is.True);
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.EqualTo(longest));
                bool takesOneMore = Steamworks.SteamFriends.SetRichPresence(SteamBackend.StatusKey, longest + "x");
                TestContext.WriteLine($"Steam {(takesOneMore ? "took" : "refused")} a status of {longest.Length + 1} bytes; it reads back {OwnPresence(SteamBackend.StatusKey).Length} bytes.");
                Assert.That(takesOneMore, Is.False, "Steam refuses a status one byte longer");
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.EqualTo(longest));

                manager.Leave();
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.Empty);
                Assert.That(OwnPresence(SteamBackend.GroupKey), Is.Empty);
                Assert.That(OwnPresence(SteamBackend.GroupSizeKey), Is.Empty);
            }
            finally
            {
                manager.Dispose();
            }
        }

        [Test]
        public void SteamPresence_IsRemovedWhenTheManagerIsDisposed_AndLeavesTheGamesOwnKeys()
        {
            // FR-28 on Steam itself; a key the game set through Steamworks stays the game's.
            MultiplayerManager manager = new MultiplayerManager();
            try
            {
                CreateFriendsOnlyLobby(manager, "forest");
                Assert.That(Steamworks.SteamFriends.SetRichPresence("mu3_test", "kept"), Is.True);
                Assert.That(Steamworks.SteamFriends.SetRichPresence(SteamBackend.DisplayKey, "#GameOwn"), Is.True);
                Assert.That(manager.SetPresenceStatus("Mu3 test").IsSuccess, Is.True);
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.EqualTo("Mu3 test"));

                manager.Dispose();
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.Empty);
                Assert.That(OwnPresence(SteamBackend.GroupKey), Is.Empty);
                Assert.That(OwnPresence(SteamBackend.GroupSizeKey), Is.Empty);
                Assert.That(OwnPresence("mu3_test"), Is.EqualTo("kept"));
                Assert.That(HasOwnPresenceKey(SteamBackend.DisplayKey), Is.True, "without a token, steam_display stays the game's");
            }
            finally
            {
                manager.Dispose();
                Steamworks.SteamFriends.SetRichPresence("mu3_test", string.Empty);
                Steamworks.SteamFriends.SetRichPresence(SteamBackend.DisplayKey, string.Empty);
            }
        }

        [Test]
        public void SteamPresence_IsRemovedWhenTheBackendIsDisposed()
        {
            // FR-28 for a game that disposes the backend without the manager. A second backend
            // finds Steam running, so disposing it leaves Steam up to read the keys back.
            MultiplayerResult created = SteamBackend.TryCreate(new SteamBackendOptions { AppId = 480 }, out SteamBackend second);
            Assert.That(created.IsSuccess, Is.True, created.ToString());
            try
            {
                Assert.That(second.OwnsSteam, Is.False);
                Assert.That(second.SetPresence("Mu3 test", new LobbyId(123), 2), Is.True);
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.EqualTo("Mu3 test"));
                Assert.That(OwnPresence(SteamBackend.GroupKey), Is.EqualTo("123"));
                Assert.That(OwnPresence(SteamBackend.GroupSizeKey), Is.EqualTo("2"));
            }
            finally
            {
                second.Dispose();
            }

            Assert.That(OwnPresence(SteamBackend.StatusKey), Is.Empty);
            Assert.That(OwnPresence(SteamBackend.GroupKey), Is.Empty);
            Assert.That(OwnPresence(SteamBackend.GroupSizeKey), Is.Empty);
        }

        [Test]
        public void SteamPresence_WithADisplayToken_SetsSteamDisplayWhileAStatusIsSet()
        {
            // FR-13: the token is what Steam's friends list shows; the test app has none of this
            // library's, so only the keys are read back. Steam lists steam_display among the
            // account's keys but answers its value as empty, so for that key the test sees only
            // whether it is set. A second backend leaves Steam running.
            MultiplayerResult created = SteamBackend.TryCreate(new SteamBackendOptions { AppId = 480, PresenceDisplayToken = "#Mu3Status" }, out SteamBackend second);
            Assert.That(created.IsSuccess, Is.True, created.ToString());
            try
            {
                Assert.That(second.SetPresence(null, new LobbyId(123), 2), Is.True);
                Assert.That(HasOwnPresenceKey(SteamBackend.DisplayKey), Is.False, "no status, no line");
                Assert.That(OwnPresence(SteamBackend.GroupKey), Is.EqualTo("123"));

                Assert.That(second.SetPresence("Mu3 test", new LobbyId(123), 2), Is.True);
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.EqualTo("Mu3 test"));
                Assert.That(HasOwnPresenceKey(SteamBackend.DisplayKey), Is.True);
                TestContext.WriteLine($"Steam reads steam_display back as '{OwnPresence(SteamBackend.DisplayKey)}'.");

                Assert.That(second.SetPresence(null, new LobbyId(123), 2), Is.True);
                Assert.That(OwnPresence(SteamBackend.StatusKey), Is.Empty);
                Assert.That(HasOwnPresenceKey(SteamBackend.DisplayKey), Is.False);
                Assert.That(OwnPresence(SteamBackend.GroupKey), Is.EqualTo("123"), "the group stays without a status");

                Assert.That(second.SetPresence("Again", new LobbyId(123), 2), Is.True);
                Assert.That(HasOwnPresenceKey(SteamBackend.DisplayKey), Is.True);
            }
            finally
            {
                second.Dispose();
            }

            Assert.That(OwnPresence(SteamBackend.StatusKey), Is.Empty);
            Assert.That(HasOwnPresenceKey(SteamBackend.DisplayKey), Is.False, "disposing the backend deletes the token it set");
            Assert.That(OwnPresence(SteamBackend.GroupKey), Is.Empty);
            Assert.That(OwnPresence(SteamBackend.GroupSizeKey), Is.Empty);
        }

        /// <summary>Whether Steam lists this account among the lobby's members.</summary>
        private bool IsOwnLobbyMember(LobbyId lobby)
        {
            Steamworks.CSteamID steamLobby = new Steamworks.CSteamID(lobby.Value);
            int count = Steamworks.SteamMatchmaking.GetNumLobbyMembers(steamLobby);
            for (int i = 0; i < count; i++)
            {
                if (Steamworks.SteamMatchmaking.GetLobbyMemberByIndex(steamLobby, i).m_SteamID == _backend.LocalPlayerId.Value)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The game's own keys of the lobby as Steam lists them to its owner, without the library's.</summary>
        private static List<string> OwnLobbyGameKeys(LobbyId lobby)
        {
            Steamworks.CSteamID steamLobby = new Steamworks.CSteamID(lobby.Value);
            List<string> keys = new List<string>();
            int count = Steamworks.SteamMatchmaking.GetLobbyDataCount(steamLobby);
            for (int i = 0; i < count; i++)
            {
                if (Steamworks.SteamMatchmaking.GetLobbyDataByIndex(steamLobby, i, out string key, Steamworks.Constants.k_nMaxLobbyKeyLength, out _, Steamworks.Constants.k_cubChatMetadataMax)
                    && !LobbyDataKeys.IsReserved(key))
                {
                    keys.Add(key);
                }
            }

            return keys;
        }

        /// <summary>Whether this account's own rich presence lists the key.</summary>
        private bool HasOwnPresenceKey(string key)
        {
            Steamworks.CSteamID self = new Steamworks.CSteamID(_backend.LocalPlayerId.Value);
            int count = Steamworks.SteamFriends.GetFriendRichPresenceKeyCount(self);
            for (int i = 0; i < count; i++)
            {
                if (Steamworks.SteamFriends.GetFriendRichPresenceKeyByIndex(self, i) == key)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A value of this account's own rich presence; empty when the key is not set.</summary>
        private string OwnPresence(string key)
        {
            return Steamworks.SteamFriends.GetFriendRichPresence(new Steamworks.CSteamID(_backend.LocalPlayerId.Value), key);
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

        /// <summary>Creates a lobby that search finds, with one game-defined value.</summary>
        private LobbyId CreatePublicLobby(MultiplayerManager manager, string key, string value)
        {
            return CreatePublicLobby(manager, new Dictionary<string, string> { [key] = value });
        }

        /// <summary>Creates a lobby that search finds, with the game-defined values.</summary>
        private LobbyId CreatePublicLobby(MultiplayerManager manager, IReadOnlyDictionary<string, string> data)
        {
            string gameId = "mu3.test." + Guid.NewGuid().ToString("N").Substring(0, 12);
            Assert.That(manager.Initialize(_backend, new MultiplayerConfig(gameId, 1)).IsSuccess, Is.True);

            LobbyCreateOptions options = new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Mu3 test" };
            foreach (KeyValuePair<string, string> pair in data)
            {
                options.Data[pair.Key] = pair.Value;
            }

            MultiplayerResult? created = null;
            manager.CreateLobby(options, result => created = result);
            Pump(manager, () => created.HasValue, 20);
            Assert.That(created?.IsSuccess, Is.True);
            return manager.CurrentLobbyId;
        }

        private static LobbySearchQuery Worldwide() => new LobbySearchQuery { Distance = LobbyDistance.Worldwide };

        /// <summary>
        /// Searches again and again until the results hold the lobby, since Steam publishes lobby
        /// data a moment after it is set; null when they never do within the time.
        /// </summary>
        private static LobbyInfo SearchUntil(MultiplayerManager manager, Func<LobbySearchQuery> query, LobbyId lobby, int seconds = 30)
        {
            return SearchUntil(manager, query, result => result.Lobbies.FirstOrDefault(info => info.Id == lobby), found => found != null, seconds);
        }

        /// <summary>
        /// Searches again and again until <paramref name="done"/> holds for what <paramref name="pick"/>
        /// takes from the results, at least once even when <paramref name="seconds"/> is 0.
        /// </summary>
        private static T SearchUntil<T>(MultiplayerManager manager, Func<LobbySearchQuery> query, Func<LobbySearchResult, T> pick, Func<T, bool> done, int seconds)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (true)
            {
                LobbySearchResult? search = null;
                manager.SearchLobbies(query(), result => search = result);
                Pump(manager, () => search.HasValue, 25);
                Assert.That(search?.IsSuccess, Is.True);
                T picked = pick(search.Value);
                if (done(picked) || watch.Elapsed.TotalSeconds >= seconds)
                {
                    return picked;
                }

                Pump(manager, () => false, 2);
            }
        }

        /// <summary>
        /// Asks Steam's own search, filtered only by the lobby's game ID, again and again until it
        /// lists the lobby with data that passes <paramref name="check"/>; false when it never does.
        /// </summary>
        private bool SteamListsUntil(MultiplayerManager manager, LobbyId lobby, Func<IReadOnlyDictionary<string, string>, bool> check, int seconds = 30)
        {
            string gameId = _backend.GetLobbyData(lobby, LobbyDataKeys.GameId);
            Stopwatch watch = Stopwatch.StartNew();
            while (true)
            {
                IReadOnlyList<BackendLobby> lobbies = null;
                bool isAnswered = false;
                _backend.SearchLobbies(Worldwide().AddStringFilter(LobbyDataKeys.GameId, gameId), (reason, found) =>
                {
                    isAnswered = true;
                    lobbies = found;
                });
                Pump(manager, () => isAnswered, 25);
                if (lobbies != null && lobbies.Any(found => found.Id == lobby && check(found.Data)))
                {
                    return true;
                }

                if (watch.Elapsed.TotalSeconds >= seconds)
                {
                    return false;
                }

                Pump(manager, () => false, 2);
            }
        }

        /// <summary>How many pixels of the texture differ from Steam's image, the top row taken as the texture's top.</summary>
        private static int CountMismatches(UnityEngine.Texture2D texture, int image)
        {
            if (!Steamworks.SteamUtils.GetImageSize(image, out uint width, out uint height) || width != texture.width || height != texture.height)
            {
                return texture.width * texture.height;
            }

            int side = texture.width;
            byte[] steam = new byte[side * side * 4];
            if (!Steamworks.SteamUtils.GetImageRGBA(image, steam, steam.Length))
            {
                return side * side;
            }

            UnityEngine.Color32[] pixels = texture.GetPixels32();
            int mismatches = 0;
            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    UnityEngine.Color32 pixel = pixels[y * side + x];
                    int i = ((side - 1 - y) * side + x) * 4;
                    if (pixel.r != steam[i] || pixel.g != steam[i + 1] || pixel.b != steam[i + 2] || pixel.a != steam[i + 3])
                    {
                        mismatches++;
                    }
                }
            }

            return mismatches;
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
