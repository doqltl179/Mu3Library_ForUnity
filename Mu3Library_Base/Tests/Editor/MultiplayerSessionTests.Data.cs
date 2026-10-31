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

            foreach (string key in new[] { LobbyDataKeys.State, "mu3.custom", string.Empty, null, new string('k', MultiplayerLimits.MaxDataKeyLength + 1) })
            {
                Assert.That(host.Manager.SetLobbyData(key, "spoofed").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), key);
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
            Assert.That(host.LobbyDataChanges.Select(change => change.Key), Is.EqualTo(new[] { longest, longest, "x" }));
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

            Assert.That(CreateLobby(host, longKey).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateLobby(host, large).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(_network.Lobbies, Is.Empty);
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
            Assert.That(a.Manager.SetParticipantData("character", null).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(a.Manager.SetParticipantData("character", new string('v', MultiplayerLimits.MaxParticipantDataSize)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(host.Manager.SetParticipantData(LobbyDataKeys.State, "game").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateNode("Lonely").Manager.SetParticipantData("character", "knight").Reason, Is.EqualTo(MultiplayerReason.InvalidState));

            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped participant data from .*: InvalidArgument"));
            raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "mu3.ready", "true"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped participant data from .*: MessageTooLarge"));
            raw.Send(connection, DataPacket(PacketKind.SetParticipantData, "c", new string('v', MultiplayerLimits.MaxParticipantDataSize)), DeliveryMode.Reliable);
            Pump();

            Assert.That(DataOf(host, raw.LocalPlayerId), Is.Empty);
            Assert.That(host.DataChanges.Concat(a.DataChanges), Is.Empty);
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
