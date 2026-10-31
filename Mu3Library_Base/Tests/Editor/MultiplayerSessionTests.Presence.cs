using System.Text.RegularExpressions;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Mu3Library.Tests
{
    /// <summary>
    /// What the friends list shows of a session (FR-13, FR-28), played on a backend that records
    /// what the manager asks it to show.
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        #region Presence
        [Test]
        public void Presence_GroupsThePlayersOfASession_FromTheLobbyOn()
        {
            // FR-13: the players of one session show as one group, without a call from the game.
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            LobbyId lobby = host.Manager.CurrentLobbyId;
            Assert.That(host.Platform.Presences, Is.EqualTo(new[] { Shown(null, lobby, 1) }));

            Node a = CreatePlatformNode("A");
            Assert.That(Join(a, host).IsSuccess, Is.True);
            Node b = JoinNew("B", host);
            Assert.That(a.Platform.Presences, Is.EqualTo(new[] { Shown(null, lobby, 2), Shown(null, lobby, 3) }), "nothing before the host took A in");
            Assert.That(host.Platform.Presence, Is.EqualTo(Shown(null, lobby, 3)));

            b.Manager.Leave();
            Pump();
            Assert.That(host.Platform.Presence, Is.EqualTo(Shown(null, lobby, 2)));
            Assert.That(a.Platform.Presence, Is.EqualTo(Shown(null, lobby, 2)));

            int calls = host.Platform.Presences.Count;
            Pump();
            Assert.That(host.Platform.Presences.Count, Is.EqualTo(calls), "nothing new while nothing changes");
        }

        [Test]
        public void SetPresenceStatus_ShowsTheStatusAtOnce_AndRefusesBadText()
        {
            // FR-13: the game sets the status while it is in a session.
            MultiplayerManager uninitialized = new MultiplayerManager();
            Assert.That(uninitialized.SetPresenceStatus("Menu").Reason, Is.EqualTo(MultiplayerReason.NotInitialized));
            uninitialized.Dispose();

            Node host = CreatePlatformNode("Host");
            Assert.That(host.Manager.SetPresenceStatus("Menu").Reason, Is.EqualTo(MultiplayerReason.InvalidState), "outside a session");
            CreateHost(host, PublicOptions(SessionRules.Default));
            LobbyId lobby = host.Manager.CurrentLobbyId;

            Assert.That(host.Manager.SetPresenceStatus("In the lobby").IsSuccess, Is.True);
            Assert.That(host.Platform.Presence, Is.EqualTo(Shown("In the lobby", lobby, 1)), "at once");
            int calls = host.Platform.Presences.Count;
            Assert.That(host.Manager.SetPresenceStatus("In the lobby").IsSuccess, Is.True);
            Assert.That(host.Platform.Presences.Count, Is.EqualTo(calls), "the same status again changes nothing");

            Assert.That(host.Manager.SetPresenceStatus("a\0b").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.SetPresenceStatus("\ud800").Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(host.Manager.SetPresenceStatus(new string('x', MultiplayerLimits.MaxPresenceStatusSize + 1)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));

            // The limit counts UTF-8 bytes: 85 three-byte characters fit in 255 bytes, 86 do not.
            Assert.That(host.Manager.SetPresenceStatus(new string('가', 86)).Reason, Is.EqualTo(MultiplayerReason.MessageTooLarge));
            Assert.That(host.Manager.SetPresenceStatus(new string('가', 85)).IsSuccess, Is.True);
            Assert.That(host.Platform.Presence, Is.EqualTo(Shown(new string('가', 85), lobby, 1)));

            Assert.That(host.Manager.SetPresenceStatus(null).IsSuccess, Is.True);
            Assert.That(host.Platform.Presence, Is.EqualTo(Shown(null, lobby, 1)));
            Assert.That(host.Manager.SetPresenceStatus("Again").IsSuccess, Is.True);
            Assert.That(host.Manager.SetPresenceStatus(string.Empty).IsSuccess, Is.True);
            Assert.That(host.Platform.Presence, Is.EqualTo(Shown(null, lobby, 1)));

            host.Platform.RefusesPresence = true;
            LogAssert.Expect(LogType.Warning, new Regex("backend refused to show the presence"));
            Assert.That(host.Manager.SetPresenceStatus("Refused").Reason, Is.EqualTo(MultiplayerReason.BackendError));
            host.Platform.RefusesPresence = false;
            calls = host.Platform.Presences.Count;
            Pump();
            Assert.That(host.Platform.Presences.Count, Is.EqualTo(calls + 1), "the next update shows the status before the refused one again");
            Assert.That(host.Platform.Presence, Is.EqualTo(Shown(null, lobby, 1)));
        }

        [Test]
        public void Presence_ReachesALateJoiner_AndStaysThroughAReconnect()
        {
            // FR-13 for every player of the session, the ones who join or come back during a game included.
            UseTestNetworkClock();
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(new SessionRules(4, requireReady: false, allowLateJoin: true)));
            LobbyId lobby = host.Manager.CurrentLobbyId;
            Node a = CreatePlatformNode("A");
            Assert.That(Join(a, host).IsSuccess, Is.True);
            Assert.That(host.Manager.StartGame().IsSuccess, Is.True);
            Pump();

            Node late = CreatePlatformNode("Late");
            Assert.That(Join(late, host).IsSuccess, Is.True);
            Assert.That(late.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(late.Platform.Presences, Is.EqualTo(new[] { Shown(null, lobby, 3) }));

            Assert.That(a.Manager.SetPresenceStatus("Playing").IsSuccess, Is.True);
            Pump();
            int calls = a.Platform.Presences.Count;
            a.Backend.SimulateOutage(20f);
            Advance(DropSeconds + 0.5);
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Reconnecting));
            Advance(11.0);

            Assert.That(a.Manager.State, Is.EqualTo(SessionState.InGame));
            Assert.That(a.Platform.Presences.Count, Is.EqualTo(calls), "the status and the group stay while the player is away");
            Assert.That(a.Platform.Presence, Is.EqualTo(Shown("Playing", lobby, 3)));
        }

        [Test]
        public void Presence_IsRemovedWhenTheSessionEnds_AndTheNextSessionStartsWithoutAStatus()
        {
            // FR-13, FR-28: leaving, the host leaving, and a failed join all remove what the session showed.
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            LobbyId lobby = host.Manager.CurrentLobbyId;
            Node a = CreatePlatformNode("A");
            Assert.That(Join(a, host).IsSuccess, Is.True);
            Assert.That(a.Manager.SetPresenceStatus("Waiting").IsSuccess, Is.True);

            bool isRemovedAtOffline = false;
            a.Manager.OnStateChanged += change =>
            {
                if (change.Current == SessionState.Offline)
                {
                    isRemovedAtOffline = a.Platform.Presence == Shown(null, LobbyId.None, 0);
                }
            };
            a.Manager.Leave();
            Assert.That(isRemovedAtOffline, Is.True, "removed before the change to Offline is raised");

            Assert.That(Join(a, host).IsSuccess, Is.True);
            Assert.That(a.Platform.Presence, Is.EqualTo(Shown(null, lobby, 2)), "the next session starts without a status");

            Assert.That(host.Manager.SetPresenceStatus("Hosting").IsSuccess, Is.True);
            isRemovedAtOffline = false;
            host.Manager.Leave();
            Pump();
            Assert.That(host.Platform.Presence, Is.EqualTo(Shown(null, LobbyId.None, 0)));
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.HostLeft));
            Assert.That(isRemovedAtOffline, Is.True, "a client whose host left");

            // A status set while joining shows without a group, and goes when the join fails.
            a.Manager.JoinLobby(new LobbyId(987654321));
            Assert.That(a.Manager.SetPresenceStatus("Joining").IsSuccess, Is.True);
            Assert.That(a.Platform.Presence, Is.EqualTo(Shown("Joining", LobbyId.None, 0)));
            Pump();
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.LobbyNotFound));
            Assert.That(a.Platform.Presence, Is.EqualTo(Shown(null, LobbyId.None, 0)));
        }

        [Test]
        public void Presence_IsRemovedOnAKick_AndWhenTheManagerIsDisposed()
        {
            // FR-28
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            Node a = CreatePlatformNode("A");
            Node b = CreatePlatformNode("B");
            Assert.That(Join(a, host).IsSuccess, Is.True);
            Assert.That(Join(b, host).IsSuccess, Is.True);
            Assert.That(a.Manager.SetPresenceStatus("Waiting").IsSuccess, Is.True);
            Assert.That(b.Manager.SetPresenceStatus("Waiting").IsSuccess, Is.True);
            Assert.That(host.Manager.SetPresenceStatus("Hosting").IsSuccess, Is.True);

            Assert.That(host.Manager.Kick(a.Id).IsSuccess, Is.True);
            Pump();
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.Kicked));
            Assert.That(a.Platform.Presence, Is.EqualTo(Shown(null, LobbyId.None, 0)));

            b.Manager.Dispose();
            host.Manager.Dispose();
            Assert.That(b.Platform.Presence, Is.EqualTo(Shown(null, LobbyId.None, 0)));
            Assert.That(host.Platform.Presence, Is.EqualTo(Shown(null, LobbyId.None, 0)));
        }

        [Test]
        public void Presence_ARemovalTheBackendRefuses_IsAskedAgainOnTheNextUpdate_ThenOnceASecond()
        {
            // FR-28: once the manager is offline, nothing else would remove what the session showed.
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            Node a = CreatePlatformNode("A");
            Assert.That(Join(a, host).IsSuccess, Is.True);
            Assert.That(a.Manager.SetPresenceStatus("Waiting").IsSuccess, Is.True);

            a.Platform.RefusesPresence = true;
            LogAssert.Expect(LogType.Warning, new Regex("backend refused to show the presence"));
            a.Manager.Leave();
            Assert.That(a.Manager.State, Is.EqualTo(SessionState.Offline));
            int attempts = a.Platform.PresenceAttempts;

            a.Manager.Update();
            Assert.That(a.Platform.PresenceAttempts, Is.EqualTo(attempts + 1), "asked again on the next update");
            a.Manager.Update();
            _now += 0.5;
            a.Manager.Update();
            Assert.That(a.Platform.PresenceAttempts, Is.EqualTo(attempts + 1), "not every frame while the backend keeps refusing");

            _now += 0.6;
            a.Platform.RefusesPresence = false;
            a.Manager.Update();
            Assert.That(a.Platform.PresenceAttempts, Is.EqualTo(attempts + 2), "once a second");
            Assert.That(a.Platform.Presence, Is.EqualTo(Shown(null, LobbyId.None, 0)));

            _now += 5.0;
            a.Manager.Update();
            Assert.That(a.Platform.PresenceAttempts, Is.EqualTo(attempts + 2), "nothing more once the backend took it");
        }

        [Test]
        public void Presence_ARefusedChangeThatIsNoRemoval_IsNotAskedAgain()
        {
            // A refused status during a session answers BackendError, and the next update shows the
            // status before it, as SetPresenceStatus_ShowsTheStatusAtOnce_AndRefusesBadText checks;
            // a refused group change waits for the next change.
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            host.Platform.RefusesPresence = true;
            Node a = CreatePlatformNode("A");
            LogAssert.Expect(LogType.Warning, new Regex("backend refused to show the presence"));
            Assert.That(Join(a, host).IsSuccess, Is.True);
            int attempts = host.Platform.PresenceAttempts;

            _now += 5.0;
            Pump();
            Assert.That(host.Platform.PresenceAttempts, Is.EqualTo(attempts));
        }

        [Test]
        public void SetPresence_OnTheLocalBackend_ChangesNothingAndSucceeds()
        {
            // FR-13: the local backend does nothing and does not fail.
            Node host = CreateHost("Host");
            Assert.That(host.Backend.SetPresence("Status", host.Manager.CurrentLobbyId, 1), Is.True);
            Assert.That(host.Manager.SetPresenceStatus("In the lobby").IsSuccess, Is.True);

            host.Manager.Leave();
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(host.Backend.SetPresence(null, LobbyId.None, 0), Is.True);
        }
        #endregion

        private static (string Status, LobbyId Group, int GroupSize) Shown(string status, LobbyId group, int groupSize) => (status, group, groupSize);
    }
}
