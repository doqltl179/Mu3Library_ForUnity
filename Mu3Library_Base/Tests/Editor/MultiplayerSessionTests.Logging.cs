using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mu3Library.Multiplayer;
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
    /// What the manager logs when another player floods it with bad packets or with messages its
    /// handlers cannot take: each cause once per session, while the session goes on (FR-33, NFR-5).
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        /// <summary>Records every log message while it lives.</summary>
        private sealed class LogRecorder : IDisposable
        {
            public readonly List<(LogType Type, string Message, string StackTrace)> Logs = new List<(LogType, string, string)>();

            public LogRecorder()
            {
                Application.logMessageReceived += Record;
            }

            public int Count(string fragment) => Logs.Count(log => log.Message.Contains(fragment));

            public void Dispose()
            {
                Application.logMessageReceived -= Record;
            }

            private void Record(string message, string stackTrace, LogType type) => Logs.Add((type, message, stackTrace));
        }

        #region Logging
        [Test]
        public void BadPacketsFloodingTheHost_AreLoggedOncePerKindAndReason_AndTheSessionGoesOn()
        {
            // FR-33, NFR-5: a connection that has not said hello, and a client that sends what only
            // the host sends or what cannot be read.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            LocalBackend stranger = CreateRawBackend("Stranger", new RecordingListener());
            ConnectionId early = stranger.Connect(host.Id);
            LocalBackend raw = CreateRawBackend("Raw", new RecordingListener());
            ConnectionId connection = ConnectAsPlayer(raw, host);

            using (LogRecorder log = new LogRecorder())
            {
                for (int i = 0; i < 5; i++)
                {
                    stranger.Send(early, new byte[] { (byte)PacketKind.SetReady, 1 }, DeliveryMode.Reliable);
                    raw.Send(connection, ChatFromHostPacket(a.Id, "spoofed"), DeliveryMode.Reliable);
                    raw.Send(connection, new[] { (byte)PacketKind.SetReady }, DeliveryMode.Reliable);
                }

                Pump();

                Assert.That(log.Count("Dropped SetReady from"), Is.EqualTo(1), "before the hello");
                Assert.That(log.Count("Dropped ChatFromHost from client"), Is.EqualTo(1));
                Assert.That(log.Count("Dropped a malformed packet"), Is.EqualTo(1));
                Assert.That(log.Logs.Count, Is.EqualTo(3), string.Join("\n", log.Logs.Select(entry => entry.Message)));
            }

            raw.Send(connection, new byte[] { (byte)PacketKind.SetReady, 1 }, DeliveryMode.Reliable);
            Assert.That(a.Manager.SendChat("still here").IsSuccess, Is.True);
            Pump();

            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Lobby));
            Assert.That(host.Manager.TryGetParticipant(raw.LocalPlayerId, out Participant participant) && participant.IsReady, Is.True);
            Assert.That(host.Chats.Select(chat => chat.Text), Is.EqualTo(new[] { "still here" }));
        }

        [Test]
        public void BadPacketsFloodingAClient_AreLoggedOncePerKindAndReason_AndTheSessionGoesOn()
        {
            // FR-33, NFR-5: a host that sends before its welcome, sends what only clients send,
            // or sends what cannot be read.
            RecordingListener listener = new RecordingListener();
            LocalBackend fakeHost = CreateRawBackend("FakeHost", listener);
            Assert.That(fakeHost.StartListening(), Is.True);
            LobbyId lobby = CreateRawLobby(fakeHost, GameId, 1);
            Node a = CreateNode("A");
            a.Manager.JoinLobby(lobby);
            Pump();
            ConnectionId connection = listener.Connected.Single();

            using (LogRecorder log = new LogRecorder())
            {
                for (int i = 0; i < 5; i++)
                {
                    fakeHost.Send(connection, new[] { (byte)PacketKind.ParticipantJoined }, DeliveryMode.Reliable);
                }

                Pump();
                Assert.That(log.Count("Dropped ParticipantJoined from the host before its welcome"), Is.EqualTo(1));

                fakeHost.Send(connection, InGameWelcomePacket(fakeHost.LocalPlayerId, a.Id), DeliveryMode.Reliable);
                Pump();
                Assert.That(a.Manager.State, Is.EqualTo(SessionState.InGame));

                for (int i = 0; i < 5; i++)
                {
                    fakeHost.Send(connection, HelloPacket(isReturning: false), DeliveryMode.Reliable);
                    fakeHost.Send(connection, new[] { (byte)PacketKind.ParticipantLeft }, DeliveryMode.Reliable);
                }

                Pump();
                Assert.That(log.Count("Dropped Hello from the host"), Is.EqualTo(1));
                Assert.That(log.Count("Dropped a malformed packet"), Is.EqualTo(1));
                Assert.That(log.Logs.Count, Is.EqualTo(3), string.Join("\n", log.Logs.Select(entry => entry.Message)));
            }

            fakeHost.Send(connection, DataPacket(PacketKind.LobbyDataChanged, "map", "forest"), DeliveryMode.Reliable);
            Pump();
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(a.Manager.LobbyData["map"], Is.EqualTo("forest"), "a later packet still counts");
        }

        [Test]
        public void AThrowingHandler_IsLoggedOncePerExceptionTypeAndSession_AndEveryMessageStillReachesIt()
        {
            // FR-33: an unreadable payload is a warning without a stack trace; anything else is
            // logged as the exception it is, in either case only the first time in the session.
            (Node host, Node[] clients) = CreateSession(1);
            Node a = clients[0];
            int handled = 0;
            Assert.That(host.Manager.RegisterMessage(Chat, (context, payload) =>
            {
                handled++;
                MessageReader reader = new MessageReader(payload);
                switch (reader.ReadByte())
                {
                    case 0:
                        throw new InvalidOperationException("handler failure");

                    case 1:
                        reader.ReadInt();
                        break;
                }
            }).IsSuccess, Is.True);
            Assert.That(a.Manager.RegisterMessage(Chat, null).IsSuccess, Is.True);

            using (LogRecorder log = new LogRecorder())
            {
                LogAssert.Expect(LogType.Exception, new Regex("handler failure"));
                for (int i = 0; i < 5; i++)
                {
                    foreach (byte value in new byte[] { 0, 1, 2 })
                    {
                        Assert.That(a.Manager.Send(Chat, new[] { value }, MessageTarget.Host).IsSuccess, Is.True);
                    }
                }

                Pump();

                Assert.That(handled, Is.EqualTo(15), "every message reaches the handler");
                Assert.That(log.Logs.Count(entry => entry.Type == LogType.Exception), Is.EqualTo(1));
                Assert.That(log.Count("threw InvalidOperationException"), Is.EqualTo(1));
                (LogType Type, string Message, string StackTrace)[] unreadable = log.Logs.Where(entry => entry.Message.Contains("could not read a message")).ToArray();
                Assert.That(unreadable.Length, Is.EqualTo(1));
                Assert.That(unreadable[0].Type, Is.EqualTo(LogType.Warning));
                Assert.That(unreadable[0].Message, Does.Contain($"message type {Chat}").And.Contain($"from {a.Id}"));
                Assert.That(unreadable[0].StackTrace, Is.Empty);
            }

            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Lobby));

            // The next session logs each cause again.
            host.Manager.Leave();
            Assert.That(CreateLobby(host, PublicOptions(SessionRules.Default)).IsSuccess, Is.True);
            LogAssert.Expect(LogType.Exception, new Regex("handler failure"));
            Assert.That(host.Manager.Send(Chat, new byte[] { 0 }, MessageTarget.Host).IsSuccess, Is.True);
            Assert.That(host.Manager.Send(Chat, new byte[] { 0 }, MessageTarget.Host).IsSuccess, Is.True);
            Assert.That(handled, Is.EqualTo(17));
        }
        #endregion
    }
}
