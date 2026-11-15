using System.Linq;
using System.Text;
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
    /// Lobby chat through the host (FR-12, NFR-5, NFR-9).
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        #region Chat
        [Test]
        public void Chat_ReachesEveryPlayer_InTheSameOrder_FromTheSenderTheHostNamed()
        {
            // FR-12, NFR-5
            (Node host, Node[] clients) = CreateSession(2);
            Node a = clients[0], b = clients[1];

            Assert.That(host.Manager.SendChat("first").IsSuccess, Is.True);
            Assert.That(a.Manager.SendChat("from A").IsSuccess, Is.True);
            Assert.That(host.Manager.SendChat("second").IsSuccess, Is.True);
            Assert.That(b.Manager.SendChat("from B 준비").IsSuccess, Is.True);
            Assert.That(host.Chats.Select(chat => chat.Text), Is.EqualTo(new[] { "first", "second" }), "the host's own messages arrive before the calls return");
            Assert.That(a.Chats, Is.Empty, "a client hears its own message once the host passed it on");
            Pump();

            (PlayerId, string)[] expected = { (host.Id, "first"), (host.Id, "second"), (a.Id, "from A"), (b.Id, "from B 준비") };
            foreach (Node node in new[] { host, a, b })
            {
                Assert.That(node.Chats.Select(chat => (chat.Sender.Id, chat.Text)), Is.EqualTo(expected), node.Backend.PlayerName);
                Assert.That(node.Chats.Select(chat => chat.Sender.Name), Is.EqualTo(new[] { "Host", "Host", "A", "B" }), node.Backend.PlayerName);
            }
        }

        [Test]
        public void Chat_OverTheSizeLimit_IsRefusedWithMessageTooLarge_AndReachesNobody()
        {
            // FR-12: the limit counts UTF-8 bytes, three for each of these characters.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            string largest = new string('x', MultiplayerLimits.MaxChatMessageSize);
            string tooLong = new string('x', MultiplayerLimits.MaxChatMessageSize + 1);
            string tooWide = new string('가', MultiplayerLimits.MaxChatMessageSize / 3 + 1);
            Assert.That(tooWide.Length, Is.LessThan(MultiplayerLimits.MaxChatMessageSize));

            foreach (Node sender in new[] { host, a })
            {
                Assert.That(sender.Manager.SendChat(tooLong).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
                Assert.That(sender.Manager.SendChat(tooWide).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            }

            Pump();
            Assert.That(host.Chats.Concat(a.Chats), Is.Empty);

            Assert.That(a.Manager.SendChat(largest).IsSuccess, Is.True);
            Pump();
            Assert.That(host.Chats.Single().Text, Is.EqualTo(largest));
            Assert.That(a.Chats.Single().Text, Is.EqualTo(largest));
        }

        [Test]
        public void Chat_TakesItsOwnLimit_ApartFromTheGameMessageLimit()
        {
            // FR-12: a session whose game messages hold 16 bytes still carries a full chat message.
            Node host = CreateHost(
                CreateNode("Host", configure: config => config.MaxMessageSize = 16),
                PublicOptions(new SessionRules(4, requireReady: false)));
            Node a = JoinNew("A", host);
            string largest = new string('x', MultiplayerLimits.MaxChatMessageSize);

            Assert.That(a.Manager.MaxMessageSize, Is.EqualTo(16));
            Assert.That(a.Manager.SendChat(largest).IsSuccess, Is.True);
            Pump();

            Assert.That(host.Chats.Single().Text, Is.EqualTo(largest));
            Assert.That(a.Chats.Single().Text, Is.EqualTo(largest));
        }

        [Test]
        public void Chat_OutsideTheLobby_OrWithoutText_IsRefused()
        {
            // FR-12: chat belongs to the lobby, not to the game.
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);

            Assert.That(a.Manager.SendChat(null).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(a.Manager.SendChat(string.Empty).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(CreateNode("Lonely").Manager.SendChat("hello").Reason, Is.EqualTo(MultiplayerReason.InvalidState));

            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();
            Assert.That(host.Manager.SendChat("hello").Reason, Is.EqualTo(MultiplayerReason.InvalidState));
            Assert.That(a.Manager.SendChat("hello").Reason, Is.EqualTo(MultiplayerReason.InvalidState));

            Assert.That(host.Manager.EndGame().IsSuccess, Is.True);
            Pump();
            Assert.That(a.Manager.SendChat("back").IsSuccess, Is.True);
            Pump();
            Assert.That(host.Chats.Select(chat => chat.Text), Is.EqualTo(new[] { "back" }));
        }

        [Test]
        public void Chat_WithAnUnpairedSurrogate_IsRefused_SoEveryPlayerHearsTheSameText()
        {
            // FR-12: UTF-8 would carry a lone surrogate as a replacement character, so the
            // clients would hear other text than the host's own handlers.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            string emoji = "\U0001F3AE";
            string[] cut = { "cut " + emoji[0], emoji[1] + " cut", "a\uDC00b", "\uD800", new string(new[] { emoji[0], emoji[0], emoji[1] }) };

            foreach (string text in cut)
            {
                foreach (Node sender in new[] { host, a })
                {
                    Assert.That(sender.Manager.SendChat(text).Reason, Is.EqualTo(MultiplayerReason.InvalidArgument), $"{sender.Backend.PlayerName}: {Escape(text)}");
                }
            }

            Assert.That(host.Manager.SendChat("whole " + emoji).IsSuccess, Is.True);
            Assert.That(a.Manager.SendChat(emoji + " too").IsSuccess, Is.True);
            Pump();

            foreach (Node node in new[] { host, a })
            {
                Assert.That(node.Chats.Select(chat => chat.Text), Is.EqualTo(new[] { "whole " + emoji, emoji + " too" }), node.Backend.PlayerName);
            }
        }

        [Test]
        public void Chat_SentAsTheGameStarts_ReachesNobody()
        {
            // FR-12: the host drops a client's message that arrives after the lobby phase ended.
            Node host = CreateHost("Host", rules: new SessionRules(4, requireReady: false));
            Node a = JoinNew("A", host);

            Assert.That(a.Manager.SendChat("too late").IsSuccess, Is.True);
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();

            Assert.That(host.Chats.Concat(a.Chats), Is.Empty);
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.InGame));
        }

        [Test]
        public void Chat_FromAMisbehavingClient_KeepsTheSenderTheConnectionProved()
        {
            // NFR-5, NFR-9: the request names no sender, the host's own packet from a client is
            // dropped, and so is a message over the limit, without its text in the log.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);

            raw.Send(connection, ChatToHostPacket("hello"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped ChatFromHost from client"));
            raw.Send(connection, ChatFromHostPacket(a.Id, "spoofed"), DeliveryMode.Reliable);
            LogAssert.Expect(LogType.Warning, new Regex("Dropped a malformed packet .*exceeds the 4096-byte limit"));
            raw.Send(connection, ChatToHostPacket(new string('x', MultiplayerLimits.MaxChatMessageSize + 1)), DeliveryMode.Reliable);
            // Invalid UTF-8 grows when decoded; the host counts the decoded text. It logs only the
            // first malformed chat packet of the session.
            raw.Send(connection, RawChatToHostPacket(Enumerable.Repeat((byte)0xFF, MultiplayerLimits.MaxChatMessageSize).ToArray()), DeliveryMode.Reliable);
            Pump();

            foreach (Node node in new[] { host, a })
            {
                Assert.That(node.Chats.Select(chat => (chat.Sender.Id, chat.Text)), Is.EqualTo(new[] { (raw.LocalPlayerId, "hello") }), node.Backend.PlayerName);
            }
        }
        #endregion

        #region Chat Helpers
        private static byte[] ChatToHostPacket(string text)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ChatToHost);
            writer.WriteString(text);
            return writer.Written.ToArray();
        }

        private static byte[] RawChatToHostPacket(byte[] utf8)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ChatToHost);
            writer.WriteBytes(utf8);
            return writer.Written.ToArray();
        }

        private static byte[] ChatFromHostPacket(PlayerId sender, string text)
        {
            PacketWriter writer = new PacketWriter();
            writer.Begin(PacketKind.ChatFromHost);
            writer.WriteULong(sender.Value);
            writer.WriteString(text);
            return writer.Written.ToArray();
        }
        #endregion
    }
}
