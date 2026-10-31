using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using Mu3Library.Multiplayer.Protocol;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Lobby data and participant data during a session (FR-9, FR-11, NFR-5).
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        #region Lobby Data
        [Test]
        public void LobbyData_FromTheHost_ReachesEveryPlayerAndLobbySearch()
        {
            // FR-11
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["map"] = "forest";
            Node host = CreateHost(CreateNode("Host"), options);
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            Assert.That(a.Manager.LobbyData["map"], Is.EqualTo("forest"));

            Assert.That(host.Manager.SetLobbyData("map", "desert").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("mode", "coop").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("mode", "coop").IsSuccess, Is.True, "setting the same value again");
            Assert.That(host.LobbyDataChanges.Count, Is.EqualTo(2), "the host's own events arrive before the calls return");
            Pump();

            foreach (Node node in new[] { host, a, b })
            {
                Assert.That(node.LobbyDataChanges, Is.EqualTo(new[] { ("map", "desert"), ("mode", "coop") }), node.Backend.PlayerName);
                Assert.That(node.Manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["map"] = "desert", ["mode"] = "coop" }));
            }

            LobbyInfo found = Search(CreateNode("Seeker")).Lobbies.Single();
            Assert.That(found.Data["map"], Is.EqualTo("desert"));
            Assert.That(found.Data["mode"], Is.EqualTo("coop"));
        }

        [Test]
        public void LobbyData_FromAClient_IsRefusedWithNotHost()
        {
            // FR-11
            Node host = CreateHost("Host");
            Node a = JoinNew("A", host);

            Assert.That(a.Manager.SetLobbyData("map", "desert").Reason, Is.EqualTo(MultiplayerReason.NotHost));
            Pump();

            Assert.That(host.LobbyDataChanges.Concat(a.LobbyDataChanges), Is.Empty);
            Assert.That(host.Manager.LobbyData.ContainsKey("map") || a.Manager.LobbyData.ContainsKey("map"), Is.False);
            Assert.That(host.Backend.GetLobbyData(host.Manager.CurrentLobbyId, "map"), Is.Null);
            Assert.That(CreateNode("Lonely").Manager.SetLobbyData("map", "desert").Reason, Is.EqualTo(MultiplayerReason.InvalidState));
        }

        [Test]
        public void LobbyData_RefusesReservedEmptyAndLongKeys_NullValues_AndTooMuchData()
        {
            // FR-11: the library's own keys stay out of the game's reach.
            Node host = CreateHost("Host");
            LobbyId lobby = host.Manager.CurrentLobbyId;

            // Steam does not tell keys apart by case, so the reserved prefix counts in any case.
            foreach (string key in new[] { LobbyDataKeys.State, "MU3.STATE", "Mu3.custom", string.Empty, null, new string('k', MultiplayerLimits.MaxDataKeyLength + 1) })
            {
                Assert.That(host.Manager.SetLobbyData(key, "spoofed").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), key);
            }

            // Text that would not reach the other players unchanged.
            foreach (string text in UncleanTexts)
            {
                Assert.That(host.Manager.SetLobbyData(text, "value").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), Escape(text));
                Assert.That(host.Manager.SetLobbyData("map", text).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), Escape(text));
            }

            Assert.That(host.Manager.SetLobbyData("map", null).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Backend.GetLobbyData(lobby, LobbyDataKeys.State), Is.EqualTo(LobbyDataKeys.StateLobby));

            // The limit counts UTF-8 bytes: three for each of these characters.
            Assert.That(host.Manager.SetLobbyData("k", new string('가', MultiplayerLimits.MaxLobbyDataSize / 3 + 1)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));

            string longest = new string('k', MultiplayerLimits.MaxDataKeyLength);
            Assert.That(host.Manager.SetLobbyData(longest, new string('v', MultiplayerLimits.MaxLobbyDataSize - longest.Length)).IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("x", string.Empty).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));

            // A new value takes the place of the old one in the count.
            Assert.That(host.Manager.SetLobbyData(longest, "short").IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("x", string.Empty).IsSuccess, Is.True);
            Assert.That(host.Manager.SetLobbyData("emoji \U0001F3AE", "\U0001F3AE").IsSuccess, Is.True, "a surrogate pair is fine");
            Assert.That(host.LobbyDataChanges.Select(change => change.Key), Is.EqualTo(new[] { longest, longest, "x", "emoji \U0001F3AE" }));
        }

        [Test]
        public void CreateLobby_RefusesALongKey_AndTooMuchData()
        {
            // FR-11: the data a lobby starts with follows the same rules.
            Node host = CreateNode("Host");
            LobbyCreateOptions longKey = PublicOptions(SessionRules.Default);
            longKey.Data[new string('k', MultiplayerLimits.MaxDataKeyLength + 1)] = "value";
            LobbyCreateOptions large = PublicOptions(SessionRules.Default);
            large.Data["a"] = new string('v', MultiplayerLimits.MaxLobbyDataSize / 2);
            large.Data["b"] = new string('v', MultiplayerLimits.MaxLobbyDataSize / 2);
            LobbyCreateOptions reserved = PublicOptions(SessionRules.Default);
            reserved.Data["MU3.game"] = "spoofed";
            LobbyCreateOptions unclean = PublicOptions(SessionRules.Default);
            unclean.Data["map"] = "forest\0desert";

            Assert.That(CreateLobby(host, longKey).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateLobby(host, reserved).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateLobby(host, unclean).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateLobby(host, large).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(_network.Lobbies, Is.Empty);
        }

        [Test]
        public void LobbyData_AKeyThatDiffersOnlyByCase_IsRefused_SoEveryCopyKeepsTheSameKeys()
        {
            // FR-11, FR-38: Steam keeps one value for both keys, so every backend refuses the second one.
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["Map"] = "desert";
            Node host = CreateHost(CreateNode("Host"), options);
            Node a = JoinNew("A", host);
            LobbyId lobby = host.Manager.CurrentLobbyId;

            LogAssert.Expect(LogType.Warning, new Regex("'map' differs from the key 'Map' only by case"));
            Assert.That(host.Manager.SetLobbyData("map", "forest").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            LogAssert.Expect(LogType.Warning, new Regex("'MAP' differs from the key 'Map' only by case"));
            Assert.That(host.Manager.SetLobbyData("MAP", new string('v', MultiplayerLimits.MaxLobbyDataSize)).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument),
                "the key is refused before its size counts");
            Assert.That(host.Manager.SetLobbyData("Map", "forest").IsSuccess, Is.True, "the key as it is spelled");
            Pump();

            foreach (Node node in new[] { host, a })
            {
                string name = node.Backend.PlayerName;
                Assert.That(node.Manager.LobbyData, Is.EquivalentTo(new Dictionary<string, string> { ["Map"] = "forest" }), name);
                Assert.That(node.Manager.LobbyData["map"], Is.EqualTo("forest"), $"{name} reads a key in any case, as Steam does");
                Assert.That(node.LobbyDataChanges, Is.EqualTo(new[] { ("Map", "forest") }), name);
            }

            Assert.That(host.Backend.GetLobbyData(lobby, "map"), Is.Null, "the lobby holds the key as it is spelled");
            LobbyInfo found = Search(CreateNode("Seeker")).Lobbies.Single();
            Assert.That(found.Data, Is.EquivalentTo(new Dictionary<string, string> { ["Map"] = "forest" }));
            Assert.That(found.Data["MAP"], Is.EqualTo("forest"), "a search result reads a key in any case");
        }

        [Test]
        public void CreateLobby_KeysThatDifferOnlyByCase_AreRefused()
        {
            // FR-11: the data a lobby starts with follows the same rule.
            Node host = CreateNode("Host");
            LobbyCreateOptions options = PublicOptions(SessionRules.Default);
            options.Data["Map"] = "desert";
            options.Data["map"] = "forest";

            LogAssert.Expect(LogType.Warning, new Regex("differs from the key '(Map|map)' only by case"));
            Assert.That(CreateLobby(host, options).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(_network.Lobbies, Is.Empty);
        }

        [Test]
        public void LobbySearch_FindsTheKeyOfAFilterInAnyCase_AsSteamDoes()
        {
            // FR-6, FR-38: the local backends match filter keys the way Steam does.
            LobbyCreateOptions options = PublicOptions(new SessionRules(4));
            options.Data["Map"] = "desert";
            options.Data["Level"] = "3";
            CreateHost(CreateNode("Host"), options);
            Node seeker = CreateNode("Seeker");

            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("MAP", "desert")).Lobbies.Count, Is.EqualTo(1));
            Assert.That(Search(seeker, new LobbySearchQuery().AddNumericFilter("level", 3)).Lobbies.Count, Is.EqualTo(1));
            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("map", "forest")).Lobbies, Is.Empty);
            Assert.That(Search(seeker, new LobbySearchQuery().AddStringFilter("map", "desert", LobbyComparison.NotEqual)).Lobbies, Is.Empty);
            Assert.That(Search(seeker, new LobbySearchQuery().AddNumericFilter("LEVEL", 3, LobbyComparison.GreaterThan)).Lobbies, Is.Empty);
        }
        #endregion

        #region Participant Data
        [Test]
        public void ParticipantData_SetByEachPlayer_ReachesEveryPlayer()
        {
            // FR-9, FR-11
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];

            Assert.That(a.Manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
            Assert.That(DataOf(a, a.Id), Is.Empty, "the host decides, and reports A's own data back");
            Assert.That(host.Manager.SetParticipantData("character", "wizard").IsSuccess, Is.True);
            Assert.That(host.DataChanges.Count, Is.EqualTo(1), "the host's own event arrives before the call returns");
            Pump();

            foreach (Node node in new[] { host, a, b })
            {
                string name = node.Backend.PlayerName;
                Assert.That(DataOf(node, host.Id)["character"], Is.EqualTo("wizard"), name);
                Assert.That(DataOf(node, a.Id)["character"], Is.EqualTo("knight"), name);
                Assert.That(DataOf(node, b.Id), Is.Empty, name);
                Assert.That(node.DataChanges.Select(change => (change.Participant.Id, change.Key)), Is.EqualTo(new[] { (host.Id, "character"), (a.Id, "character") }), name);
                Assert.That(node.DataChanges.Select(change => change.Participant.Data["character"]), Is.EqualTo(new[] { "wizard", "knight" }), name);
            }

            // A participant handed out before keeps the data it had then.
            Assert.That(a.Manager.SetParticipantData("character", "archer").IsSuccess, Is.True);
            Pump();
            Assert.That(b.DataChanges[1].Participant.Data["character"], Is.EqualTo("knight"));
            Assert.That(b.DataChanges[2].Participant.Data["character"], Is.EqualTo("archer"));
            Assert.That(DataOf(b, a.Id)["character"], Is.EqualTo("archer"));
            Assert.That(b.Changed, Is.Empty, "data changes have an event of their own");
        }

        [Test]
        public void ParticipantData_FromAClient_ChangesOnlyItsOwn()
        {
            // FR-11, NFR-5: the request names no player, and the host's own packets from a client are dropped.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);

            raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "team", "red"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped ParticipantDataChanged from client"));
            raw.Send(connection, ParticipantDataChangedPacket(a.Id, "team", "spoofed"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped LobbyDataChanged from client"));
            raw.Send(connection, DataPacket(PacketKind.LobbyDataChanged, "map", "spoofed"), DeliveryMode.Reliable);
            Pump();

            foreach (Node node in new[] { host, a })
            {
                Assert.That(DataOf(node, raw.LocalPlayerId)["team"], Is.EqualTo("red"));
                Assert.That(DataOf(node, a.Id), Is.Empty);
                Assert.That(node.Manager.LobbyData.ContainsKey("map"), Is.False);
                Assert.That(node.DataChanges.Select(change => change.Participant.Id), Is.EqualTo(new[] { raw.LocalPlayerId }));
            }
        }

        [Test]
        public void ParticipantData_RefusesReservedKeysAndTooMuchData_HereAndAtTheHost()
        {
            // FR-11, NFR-5: a client that skips its own check still cannot get past the host.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];

            Assert.That(a.Manager.SetParticipantData("mu3.ready", "true").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(a.Manager.SetParticipantData("MU3.Ready", "true").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(a.Manager.SetParticipantData("character", null).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            foreach (string text in UncleanTexts)
            {
                Assert.That(a.Manager.SetParticipantData(text, "value").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), Escape(text));
                Assert.That(a.Manager.SetParticipantData("character", text).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), Escape(text));
            }

            Assert.That(a.Manager.SetParticipantData("character", new string('v', MultiplayerLimits.MaxParticipantDataSize)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(host.Manager.SetParticipantData(LobbyDataKeys.State, "game").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateNode("Lonely").Manager.SetParticipantData("character", "knight").Reason, Is.EqualTo(MultiplayerReason.InvalidState));

            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);
            List<string> refusals = new List<string>();
            void Record(string message, string stackTrace, LogType type)
            {
                if (message.Contains("Dropped participant data"))
                {
                    refusals.Add(message);
                }
            }

            Application.logMessageReceived += Record;
            try
            {
                // A flood of bad requests logs each reason once. A lone surrogate turns into a
                // replacement character on its way, so only a NUL reaches the host unchanged.
                for (int i = 0; i < 3; i++)
                {
                    raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "mu3.ready", "true"), DeliveryMode.Reliable);
                    raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "map", "a\0b"), DeliveryMode.Reliable);
                    raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "c", new string('v', MultiplayerLimits.MaxParticipantDataSize)), DeliveryMode.Reliable);
                }

                Pump();
            }
            finally
            {
                Application.logMessageReceived -= Record;
            }

            Assert.That(refusals.Count, Is.EqualTo(2), string.Join("\n", refusals));
            Assert.That(refusals[0], Does.Contain(": InvalidArgument."));
            Assert.That(refusals[1], Does.Contain(": MessageTooLarge."));
            Assert.That(DataOf(host, raw.LocalPlayerId), Is.Empty);
            Assert.That(host.DataChanges.Concat(a.DataChanges), Is.Empty);
        }

        [Test]
        public void ParticipantData_FromAClient_IsCheckedAgainstItsOwnRequests_NotTheCopyStillOnItsWay()
        {
            // FR-11: what a client is told is what the host does, before any change came back.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            int half = MultiplayerLimits.MaxParticipantDataSize / 2;
            List<string> refusals = new List<string>();
            void Record(string message, string stackTrace, LogType type)
            {
                if (message.Contains("Dropped participant data"))
                {
                    refusals.Add(message);
                }
            }

            Application.logMessageReceived += Record;
            try
            {
                Assert.That(a.Manager.SetParticipantData("a", new string('v', half + 100)).IsSuccess, Is.True);
                Assert.That(a.Manager.SetParticipantData("b", new string('v', half)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge),
                    "the first request has not come back yet, but the host will hold it");

                Assert.That(a.Manager.SetParticipantData("a", string.Empty).IsSuccess, Is.True);
                Assert.That(a.Manager.SetParticipantData("b", new string('v', half)).IsSuccess, Is.True,
                    "the room the second request freed counts before the host reported it");
                Assert.That(DataOf(a, a.Id), Is.Empty);
                Pump();
            }
            finally
            {
                Application.logMessageReceived -= Record;
            }

            Assert.That(refusals, Is.Empty);
            foreach (Node node in new[] { host, a })
            {
                Assert.That(DataOf(node, a.Id)["a"], Is.Empty);
                Assert.That(DataOf(node, a.Id)["b"].Length, Is.EqualTo(half));
                Assert.That(node.DataChanges.Count, Is.EqualTo(3));
            }
        }

        [Test]
        public void AFullSession_AtEveryDataLimit_StillAdmitsAPlayerWhoJoinsLate()
        {
            // FR-11, FR-24: the welcome carries the start data, the lobby data, and every
            // player's data at once, so it has to fit in one send at every limit together. The
            // data uses the shortest keys there are, whose length fields cost the most.
            Dictionary<string, string> lobbyData = DataOfSize(MultiplayerLimits.MaxLobbyDataSize, keysIgnoreCase: true);
            LobbyCreateOptions options = PublicOptions(new SessionRules(MultiplayerLimits.MaxCapacity, requireReady: false, allowLateJoin: true));
            foreach (KeyValuePair<string, string> pair in lobbyData)
            {
                options.Data.Add(pair.Key, pair.Value);
            }

            Node host = CreateHost(CreateNode("Host"), options);
            Dictionary<string, string> playerData = DataOfSize(MultiplayerLimits.MaxParticipantDataSize);
            List<LocalBackend> players = new List<LocalBackend>();
            for (int i = 0; i < MultiplayerLimits.MaxCapacity - 2; i++)
            {
                LocalBackend raw = CreateRawBackend($"Player {i:D2} {new string('n', MultiplayerLimits.MaxPlayerNameLength)}", new RecordingListener());
                ConnectionId connection = ConnectAsPlayer(raw, host);
                foreach (KeyValuePair<string, string> pair in playerData)
                {
                    raw.Send(connection, DataPacket(PacketKind.SetParticipantData, pair.Key, pair.Value), DeliveryMode.Reliable);
                }

                players.Add(raw);
            }

            foreach (KeyValuePair<string, string> pair in playerData)
            {
                Assert.That(host.Manager.SetParticipantData(pair.Key, pair.Value).IsSuccess, Is.True);
            }

            Pump();
            Assert.That(players.All(raw => DataOf(host, raw.LocalPlayerId).Count == playerData.Count), Is.True);
            Assert.That(host.Manager.StartGame(new byte[MultiplayerLimits.MaxStartDataSize]).IsSuccess, Is.True);
            Pump();

            Node late = CreateNode("Late");
            Assert.That(Join(late, host).IsSuccess, Is.True);

            Assert.That(late.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(late.Manager.Participants.Count, Is.EqualTo(MultiplayerLimits.MaxCapacity));
            Assert.That(late.Manager.StartData.Length, Is.EqualTo(MultiplayerLimits.MaxStartDataSize));
            Assert.That(late.Manager.LobbyData, Is.EquivalentTo(lobbyData));
            Assert.That(DataOf(late, host.Id), Is.EquivalentTo(playerData));
            Assert.That(players.All(raw => DataOf(late, raw.LocalPlayerId).Count == playerData.Count), Is.True);
        }

        [Test]
        public void Data_ReachesALateJoiner_AndOutlivesTheGame_ButNotTheSession()
        {
            // FR-11
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false, allowLateJoin: true));
            Node a = JoinNew("A", host);
            Assert.That(host.Manager.SetLobbyData("map", "desert").IsSuccess, Is.True);
            Assert.That(a.Manager.SetParticipantData("character", "knight").IsSuccess, Is.True);
            Pump();
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Assert.That(host.Manager.SetParticipantData("character", "wizard").IsSuccess, Is.True, "in a game too");
            Pump();

            Node late = JoinNew("Late", host);
            Assert.That(late.Manager.LobbyData["map"], Is.EqualTo("desert"));
            Assert.That(DataOf(late, a.Id)["character"], Is.EqualTo("knight"));
            Assert.That(DataOf(late, host.Id)["character"], Is.EqualTo("wizard"));
            Assert.That(late.LobbyDataChanges, Is.Empty);
            Assert.That(late.DataChanges, Is.Empty);
            Assert.That(a.Joined.Single(participant => participant.Id == late.Id).Data, Is.Empty);

            Assert.That(host.Manager.EndGame().IsSuccess, Is.True);
            Pump();
            foreach (Node node in new[] { host, a, late })
            {
                Assert.That(node.Manager.LobbyData["map"], Is.EqualTo("desert"));
                Assert.That(DataOf(node, a.Id)["character"], Is.EqualTo("knight"));
            }

            a.Manager.Leave();
            Pump();
            Assert.That(a.Manager.LobbyData, Is.Empty);
            Assert.That(host.Manager.SetLobbyData("map", "forest").IsSuccess, Is.True);
            host.Manager.Leave();
            Assert.That(host.Manager.LobbyData, Is.Empty);
        }
        #endregion

        #region Data Helpers
        // A NUL, where Steam ends a string, and surrogates without their pair, which UTF-8 replaces.
        private static readonly string[] UncleanTexts = { "a\0b", "a\uD800", "\uDC00a", "\uDBFF" };

        private static string Escape(string text) => string.Concat(text.Select(c => c < 0x20 || char.IsSurrogate(c) ? $"\\u{(int)c:X4}" : c.ToString()));

        /// <summary>
        /// Data whose keys and values take exactly <paramref name="size"/> UTF-8 bytes in as many
        /// entries as distinct keys allow: every one-character key, then two-character keys.
        /// </summary>
        /// <summary>
        /// Data of exactly <paramref name="size"/> UTF-8 bytes in as many entries as fit. With
        /// <paramref name="keysIgnoreCase"/>, as lobby data needs, no two keys differ only by case.
        /// </summary>
        private static Dictionary<string, string> DataOfSize(int size, bool keysIgnoreCase = false)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();
            HashSet<string> taken = new HashSet<string>(keysIgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            int left = size;
            for (char c = '!'; c <= '~' && left > 0; c++)
            {
                if (taken.Add(c.ToString()))
                {
                    data.Add(c.ToString(), string.Empty);
                    left--;
                }
            }

            for (char first = '!'; first <= '~' && left >= 2; first++)
            {
                for (char second = '!'; second <= '~' && left >= 2; second++)
                {
                    string key = new string(new[] { first, second });
                    if (taken.Add(key))
                    {
                        data.Add(key, string.Empty);
                        left -= 2;
                    }
                }
            }

            if (left > 0)
            {
                data["!"] = new string('v', left);
            }

            return data;
        }

        private static IReadOnlyDictionary<string, string> DataOf(Node node, PlayerId player)
        {
            Assert.That(node.Manager.TryGetParticipant(player, out Participant participant), Is.True, $"{player} at {node.Backend.PlayerName}");
            return participant.Data;
        }

        private static byte[] DataPacket(PacketKind kind, string key, string value)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(kind);
            writer.WriteString(key);
            writer.WriteString(value);
            return writer.Written.ToArray();
        }

        private static byte[] ParticipantDataChangedPacket(PlayerId player, string key, string value)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ParticipantDataChanged);
            writer.WriteULong(player.Value);
            writer.WriteString(key);
            writer.WriteString(value);
            return writer.Written.ToArray();
        }
        #endregion
    }
}
