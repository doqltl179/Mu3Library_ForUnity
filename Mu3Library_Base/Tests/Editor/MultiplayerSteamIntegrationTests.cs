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

        [Test]
        public void SteamAvatar_OfThisAccount_ArrivesAsATextureOfSteamsSize_TheRightWayUp()
        {
            // FR-4 on Steam itself, with this account's own avatar: another account's is a manual check.
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
