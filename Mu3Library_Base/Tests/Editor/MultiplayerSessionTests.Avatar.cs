using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
// Unity's Is extends NUnit's with AllocatingGCMemory.
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Mu3Library.Tests
{
    /// <summary>
    /// Avatars as textures (FR-4): what the local backend answers, and how the manager makes,
    /// shares, and destroys the textures, played on a backend that has avatars.
    /// </summary>
    public partial class MultiplayerSessionTests
    {
        /// <summary>
        /// A <see cref="LocalBackend"/> with what the local network lacks: the avatars a test hands
        /// it, answered on its next update as a backend must, the avatar changes a test reports
        /// through it, and a record of every presence the manager asked it to show.
        /// </summary>
        private sealed class PlatformBackend : IMultiplayerBackend
        {
            private readonly LocalBackend _inner;
            private readonly Queue<Action> _avatarAnswers = new Queue<Action>();
            private readonly Queue<PlayerId> _avatarChanges = new Queue<PlayerId>();
            private IMultiplayerBackendListener _listener;

            /// <summary>What a request for a player and size answers; any other answers NotSupported.</summary>
            public readonly Dictionary<(PlayerId Player, AvatarSize Size), (MultiplayerReason Reason, BackendAvatar Avatar)> Avatars =
                new Dictionary<(PlayerId Player, AvatarSize Size), (MultiplayerReason Reason, BackendAvatar Avatar)>();

            public int AvatarReads;

            /// <summary>Keeps the avatar answers back while true; the avatar changes still go out.</summary>
            public bool HoldsAvatars;

            /// <summary>Answers the avatar requests of an update from the last one made, as a backend may.</summary>
            public bool AnswersLastFirst;

            public PlatformBackend(LocalBackend inner)
            {
                _inner = inner;
            }

            public string Name => "Platform";
            public bool IsAvailable => _inner.IsAvailable;
            public PlayerId LocalPlayerId => _inner.LocalPlayerId;
            public int MaxPacketSize => _inner.MaxPacketSize;
            public string GetPlayerName(PlayerId player) => _inner.GetPlayerName(player);

            public void SetListener(IMultiplayerBackendListener listener)
            {
                _listener = listener;
                _inner.SetListener(listener);
            }

            /// <summary>Tells the listener on the next update that the player's avatar changed.</summary>
            public void ReportAvatarChanged(PlayerId player) => _avatarChanges.Enqueue(player);

            public void Update()
            {
                _inner.Update();
                while (_avatarChanges.Count > 0)
                {
                    _listener?.OnAvatarChanged(_avatarChanges.Dequeue());
                }

                if (HoldsAvatars)
                {
                    return;
                }

                Action[] answers = _avatarAnswers.ToArray();
                _avatarAnswers.Clear();
                for (int i = 0; i < answers.Length; i++)
                {
                    answers[AnswersLastFirst ? answers.Length - 1 - i : i]();
                }
            }

            public void CreateLobby(LobbyVisibility visibility, int capacity, IReadOnlyDictionary<string, string> data, Action<MultiplayerReason, LobbyId> onCompleted) => _inner.CreateLobby(visibility, capacity, data, onCompleted);
            public void SearchLobbies(LobbySearchQuery query, Action<MultiplayerReason, IReadOnlyList<BackendLobby>> onCompleted) => _inner.SearchLobbies(query, onCompleted);
            public void JoinLobby(LobbyId lobby, Action<MultiplayerReason> onCompleted) => _inner.JoinLobby(lobby, onCompleted);
            public void LeaveLobby(LobbyId lobby) => _inner.LeaveLobby(lobby);
            public string GetLobbyData(LobbyId lobby, string key) => _inner.GetLobbyData(lobby, key);
            public bool SetLobbyData(LobbyId lobby, string key, string value) => _inner.SetLobbyData(lobby, key, value);
            public bool DeleteLobbyData(LobbyId lobby, string key) => _inner.DeleteLobbyData(lobby, key);
            public bool SetLobbyJoinable(LobbyId lobby, bool joinable) => _inner.SetLobbyJoinable(lobby, joinable);
            public bool SetLobbyCapacity(LobbyId lobby, int capacity) => _inner.SetLobbyCapacity(lobby, capacity);
            public PlayerId GetLobbyOwner(LobbyId lobby) => _inner.GetLobbyOwner(lobby);
            public bool IsLobbyMember(LobbyId lobby, PlayerId player) => _inner.IsLobbyMember(lobby, player);
            public bool StartListening() => _inner.StartListening();
            public void StopListening() => _inner.StopListening();
            public ConnectionId Connect(PlayerId host) => _inner.Connect(host);
            public bool Send(ConnectionId connection, ReadOnlySpan<byte> data, DeliveryMode mode) => _inner.Send(connection, data, mode);
            public void Disconnect(ConnectionId connection, int reasonCode) => _inner.Disconnect(connection, reasonCode);
            public bool TryGetConnectionQuality(ConnectionId connection, out ConnectionQuality quality) => _inner.TryGetConnectionQuality(connection, out quality);

            public void LoadAvatar(PlayerId player, AvatarSize size, Action<MultiplayerReason, BackendAvatar> onCompleted)
            {
                AvatarReads++;
                MultiplayerReason reason = MultiplayerReason.NotSupported;
                BackendAvatar avatar = default;
                if (Avatars.TryGetValue((player, size), out (MultiplayerReason Reason, BackendAvatar Avatar) answer))
                {
                    // A copy for every answer, since the manager takes the array over.
                    reason = answer.Reason;
                    byte[] pixels = answer.Avatar.Pixels != null ? (byte[])answer.Avatar.Pixels.Clone() : null;
                    avatar = new BackendAvatar(answer.Avatar.Width, answer.Avatar.Height, pixels);
                }

                _avatarAnswers.Enqueue(() => onCompleted?.Invoke(reason, avatar));
            }

            /// <summary>Every presence the manager asked to show, in order, the refused ones left out.</summary>
            public readonly List<(string Status, LobbyId Group, int GroupSize)> Presences = new List<(string Status, LobbyId Group, int GroupSize)>();

            public bool RefusesPresence;

            /// <summary>Every time the manager asked to show a presence, the refused ones included.</summary>
            public int PresenceAttempts;

            public (string Status, LobbyId Group, int GroupSize) Presence => Presences.Count > 0 ? Presences[Presences.Count - 1] : (null, LobbyId.None, 0);

            public bool SetPresence(string status, LobbyId group, int groupSize)
            {
                PresenceAttempts++;
                if (RefusesPresence)
                {
                    return false;
                }

                Presences.Add((status, group, groupSize));
                return _inner.SetPresence(status, group, groupSize);
            }

            public void Dispose() => _inner.Dispose();
        }

        #region Avatar
        [Test]
        public void LoadAvatar_OnTheLocalBackend_AnswersNotSupportedWithoutATexture()
        {
            // FR-4: the local backend tells that it has no avatars with a reason.
            Node host = CreateHost("Host");

            AvatarResult? result = null;
            host.Manager.LoadAvatar(host.Id, AvatarSize.Medium, r => result = r);
            Assert.That(result.HasValue, Is.False, "the answer comes from the backend's update");

            Pump();
            Assert.That(result?.Reason, Is.EqualTo(MultiplayerReason.NotSupported));
            Assert.That(result.Value.IsSuccess, Is.False);
            Assert.That(result.Value.Texture == null, Is.True);
            Assert.That(result.Value.Player, Is.EqualTo(host.Id));
            Assert.That(result.Value.Size, Is.EqualTo(AvatarSize.Medium));
        }

        [Test]
        public void LoadAvatar_RefusesABadRequestAtOnce()
        {
            AvatarResult? result = null;
            MultiplayerManager uninitialized = new MultiplayerManager();
            uninitialized.LoadAvatar(new PlayerId(1), AvatarSize.Small, r => result = r);
            uninitialized.Dispose();
            Assert.That(result?.Reason, Is.EqualTo(MultiplayerReason.NotInitialized));

            Node node = CreatePlatformNode("A");
            result = null;
            node.Manager.LoadAvatar(PlayerId.None, AvatarSize.Small, r => result = r);
            Assert.That(result?.Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));

            result = null;
            node.Manager.LoadAvatar(node.Id, (AvatarSize)3, r => result = r);
            Assert.That(result?.Reason, Is.EqualTo(MultiplayerReason.InvalidArgument));
            Assert.That(result.Value.Texture == null, Is.True);

            LogAssert.Expect(LogType.Warning, "[Multiplayer] LoadAvatar needs a callback.");
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, null);
            Assert.That(node.Platform.AvatarReads, Is.Zero);
        }

        [Test]
        public void LoadAvatar_MakesATextureTheRightWayUp_AndAnswersTheSameOneAtOnceAfterwards()
        {
            // FR-4: an avatar the manager does not hold yet arrives with the backend's answer.
            Node node = CreatePlatformNode("A");
            node.Platform.Avatars[(node.Id, AvatarSize.Small)] = (MultiplayerReason.None, StripedAvatar(3, 2));

            AvatarResult first = LoadAvatar(node, node.Id, AvatarSize.Small);
            Assert.That(first.IsSuccess, Is.True);
            Texture2D texture = first.Texture;
            Assert.That(texture != null, Is.True);
            Assert.That(texture.width, Is.EqualTo(3));
            Assert.That(texture.height, Is.EqualTo(2));
            Assert.That(texture.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(texture.mipmapCount, Is.EqualTo(1));

            // The backend's rows run from the top down, a texture's from the bottom up.
            Color32[] pixels = texture.GetPixels32();
            for (int y = 0; y < 2; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    Color32 pixel = pixels[y * 3 + x];
                    Assert.That((pixel.r, pixel.g), Is.EqualTo((StripeRed(1 - y), (byte)x)), $"pixel {x}, {y}");
                }
            }

            AvatarResult? again = null;
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, r => again = r);
            Assert.That(again.HasValue, Is.True, "an avatar the manager holds answers at once");
            Assert.That(again.Value.Texture, Is.SameAs(texture));
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(1));
        }

        [Test]
        public void LoadAvatar_RequestsWhileTheBackendReads_ShareOneAnswer()
        {
            Node node = CreatePlatformNode("A");
            node.Platform.Avatars[(node.Id, AvatarSize.Large)] = (MultiplayerReason.None, StripedAvatar(4, 4));
            node.Platform.HoldsAvatars = true;

            List<AvatarResult> results = new List<AvatarResult>();
            node.Manager.LoadAvatar(node.Id, AvatarSize.Large, results.Add);
            node.Manager.LoadAvatar(node.Id, AvatarSize.Large, results.Add);
            Pump();
            Assert.That(results, Is.Empty);

            node.Platform.HoldsAvatars = false;
            Pump();
            Assert.That(results.Count, Is.EqualTo(2));
            Assert.That(results[0].IsSuccess && results[1].IsSuccess, Is.True);
            Assert.That(results[1].Texture, Is.SameAs(results[0].Texture));
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(1));
        }

        [Test]
        public void LoadAvatar_Failure_AnswersNullWithTheReason_AndIsTriedAgain()
        {
            // FR-4: a failure delivers null and a reason.
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.Timeout, default);

            LogAssert.Expect(LogType.Warning, new Regex(@"avatar of 424242 could not be loaded \(Timeout\)"));
            AvatarResult failed = LoadAvatar(node, stranger, AvatarSize.Medium);
            Assert.That(failed.Reason, Is.EqualTo(MultiplayerReason.Timeout));
            Assert.That(failed.Texture == null, Is.True);

            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            Assert.That(LoadAvatar(node, stranger, AvatarSize.Medium).IsSuccess, Is.True);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(2));

            // An image whose pixels do not match its size is no avatar either.
            node.Platform.Avatars[(stranger, AvatarSize.Small)] = (MultiplayerReason.None, new BackendAvatar(2, 2, new byte[3]));
            LogAssert.Expect(LogType.Warning, new Regex(@"as 2x2 pixels in 3 bytes"));
            AvatarResult broken = LoadAvatar(node, stranger, AvatarSize.Small);
            Assert.That(broken.Reason, Is.EqualTo(MultiplayerReason.BackendError));
            Assert.That(broken.Texture == null, Is.True);
        }

        [Test]
        public void SessionEnd_DestroysTheAvatarsOfTheOtherPlayers_AndKeepsTheOthersUntilDispose()
        {
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            Node a = CreatePlatformNode("A");
            Assert.That(Join(a, host).IsSuccess, Is.True);
            Node b = JoinNew("B", host);
            PlayerId stranger = new PlayerId(424242);
            foreach (Node node in new[] { host, a })
            {
                foreach (PlayerId player in new[] { host.Id, a.Id, b.Id, stranger })
                {
                    node.Platform.Avatars[(player, AvatarSize.Medium)] = (MultiplayerReason.None, StripedAvatar(2, 2));
                }
            }

            Texture2D own = LoadAvatar(host, host.Id, AvatarSize.Medium).Texture;
            Texture2D ofA = LoadAvatar(host, a.Id, AvatarSize.Medium).Texture;
            Texture2D ofB = LoadAvatar(host, b.Id, AvatarSize.Medium).Texture;
            Texture2D ofStranger = LoadAvatar(host, stranger, AvatarSize.Medium).Texture;
            Texture2D hostOnA = LoadAvatar(a, host.Id, AvatarSize.Medium).Texture;

            // B leaves first; the avatar that arrived while B was in the session still ends with it.
            b.Manager.Leave();
            Pump();
            Assert.That(ofB != null, Is.True);

            bool isGoneAtOffline = false;
            host.Manager.OnStateChanged += change => isGoneAtOffline = change.Current == SessionState.Offline && ofA == null;
            host.Manager.Leave();
            Pump();

            Assert.That(isGoneAtOffline, Is.True, "destroyed before the change to Offline is raised");
            Assert.That(ofA == null && ofB == null, Is.True);
            Assert.That(a.LastChange.Reason, Is.EqualTo(MultiplayerReason.HostLeft));
            Assert.That(hostOnA == null, Is.True, "a client's avatar of the host ends with the session too");
            Assert.That(own != null && ofStranger != null, Is.True);

            AvatarResult? again = null;
            host.Manager.LoadAvatar(stranger, AvatarSize.Medium, r => again = r);
            Assert.That(again?.Texture, Is.SameAs(ofStranger));

            host.Manager.Dispose();
            Assert.That(own == null && ofStranger == null, Is.True);
        }

        [Test]
        public void LoadAvatar_ATextureTheGameDestroyed_IsMadeAgain()
        {
            Node node = CreatePlatformNode("A");
            node.Platform.Avatars[(node.Id, AvatarSize.Small)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            Texture2D first = LoadAvatar(node, node.Id, AvatarSize.Small).Texture;
            UnityEngine.Object.DestroyImmediate(first);

            AvatarResult? again = null;
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, r => again = r);
            Assert.That(again.HasValue, Is.False, "a destroyed texture is no texture the manager holds");
            Pump();

            Assert.That(again?.IsSuccess, Is.True);
            Assert.That(again.Value.Texture != null, Is.True);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(2));
        }

        [Test]
        public void LoadAvatar_ABackendThatIsGone_AnswersBackendErrorAtOnce_AndToTheRequestStillWaiting()
        {
            Node node = CreatePlatformNode("A");
            node.Platform.Avatars[(node.Id, AvatarSize.Small)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            node.Platform.HoldsAvatars = true;
            List<AvatarResult> answers = new List<AvatarResult>();
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, answers.Add);
            Pump();

            // A disposed backend drops the answers it still owes, as Steam's does; this one keeps
            // holding its answer, so only the manager can answer the request already waiting.
            node.Backend.Dispose();
            AvatarResult? result = null;
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, r => result = r);
            Assert.That(result?.Reason, Is.EqualTo(MultiplayerReason.BackendError), "at once, not behind the request already waiting");
            Assert.That(result.Value.Texture == null, Is.True);
            Assert.That(answers.Select(answer => answer.Reason), Is.EqualTo(new[] { MultiplayerReason.BackendError }), "the request already waiting is answered, not held");

            result = null;
            node.Manager.LoadAvatar(node.Id, AvatarSize.Large, r => result = r);
            Assert.That(result?.Reason, Is.EqualTo(MultiplayerReason.BackendError));
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(1));
            Assert.That(answers.Count, Is.EqualTo(1), "answered once");
        }

        [Test]
        public void Update_AnswersTheAvatarRequestsStillWaiting_OnceTheBackendIsGone()
        {
            // A backend that is gone never answers, so the game's callbacks are not held until the
            // manager is disposed.
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(node.Id, AvatarSize.Small)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            node.Platform.HoldsAvatars = true;

            List<AvatarResult> answers = new List<AvatarResult>();
            AvatarResult? askedAgain = null;
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, answer =>
            {
                answers.Add(answer);
                node.Manager.LoadAvatar(node.Id, AvatarSize.Small, r => askedAgain = r);
            });
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, answers.Add);
            node.Manager.LoadAvatar(stranger, AvatarSize.Large, answers.Add);
            Pump();
            Assert.That(answers, Is.Empty);

            node.Backend.Dispose();
            node.Manager.Update();

            Assert.That(answers.Select(answer => (answer.Reason, answer.Player, answer.Size)), Is.EquivalentTo(new[]
            {
                (MultiplayerReason.BackendError, node.Id, AvatarSize.Small),
                (MultiplayerReason.BackendError, node.Id, AvatarSize.Small),
                (MultiplayerReason.BackendError, stranger, AvatarSize.Large),
            }));
            Assert.That(answers.TrueForAll(answer => answer.Texture == null), Is.True);
            Assert.That(askedAgain?.Reason, Is.EqualTo(MultiplayerReason.BackendError), "a callback that asks again is answered at once");
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(2));

            // The answers the backend still held find nothing waiting.
            node.Platform.HoldsAvatars = false;
            node.Platform.Update();
            node.Manager.Update();
            Assert.That(answers.Count, Is.EqualTo(3));
        }

        [Test]
        public void LoadAvatar_ACallbackThatEndsTheSession_LeavesTheNextOnesAFreshTexture()
        {
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            Node a = JoinNew("A", host);
            host.Platform.Avatars[(a.Id, AvatarSize.Medium)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            host.Platform.HoldsAvatars = true;

            Texture2D destroyed = null;
            List<AvatarResult> later = new List<AvatarResult>();
            host.Manager.LoadAvatar(a.Id, AvatarSize.Medium, result =>
            {
                destroyed = result.Texture;
                host.Manager.Leave();
            });
            host.Manager.LoadAvatar(a.Id, AvatarSize.Medium, later.Add);
            Pump();

            host.Platform.HoldsAvatars = false;
            Pump();
            Assert.That(host.Manager.State, Is.EqualTo(SessionState.Offline));
            Assert.That(destroyed == null, Is.True, "the first callback's texture ended with the session");
            Assert.That(later.Count, Is.EqualTo(1), "the next callback was asked again, and answered once");
            Assert.That(later[0].IsSuccess, Is.True);
            Assert.That(later[0].Texture != null, Is.True);
            Assert.That(host.Platform.AvatarReads, Is.EqualTo(2));

            // Read outside the session, the new texture lasts until the manager is disposed.
            Texture2D fresh = later[0].Texture;
            host.Manager.Dispose();
            Assert.That(fresh == null, Is.True);
        }

        [Test]
        public void LoadAvatar_AnAvatarThatArrivesAfterItsPlayerLeft_LastsUntilDispose()
        {
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            Node a = JoinNew("A", host);
            host.Platform.Avatars[(a.Id, AvatarSize.Small)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            host.Platform.HoldsAvatars = true;
            AvatarResult? result = null;
            host.Manager.LoadAvatar(a.Id, AvatarSize.Small, r => result = r);

            a.Manager.Leave();
            Pump();
            Assert.That(host.Manager.TryGetParticipant(a.Id, out _), Is.False);
            host.Platform.HoldsAvatars = false;
            Pump();
            Texture2D texture = result?.Texture;
            Assert.That(texture != null, Is.True);

            host.Manager.Leave();
            Assert.That(texture != null, Is.True, "it did not arrive while the player was in the session");
            host.Manager.Dispose();
            Assert.That(texture == null, Is.True);
        }

        [Test]
        public void ReleaseAvatar_DestroysEverySizeOfThatPlayer_AndARequestAfterwardsReadsItAgain()
        {
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            foreach (AvatarSize size in new[] { AvatarSize.Small, AvatarSize.Large })
            {
                node.Platform.Avatars[(node.Id, size)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            }

            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            Texture2D small = LoadAvatar(node, node.Id, AvatarSize.Small).Texture;
            Texture2D large = LoadAvatar(node, node.Id, AvatarSize.Large).Texture;
            Texture2D ofStranger = LoadAvatar(node, stranger, AvatarSize.Medium).Texture;

            node.Manager.ReleaseAvatar(node.Id);
            Assert.That(small == null && large == null, Is.True, "every size, this player's own included");
            Assert.That(ofStranger != null, Is.True, "another player's stays");

            AvatarResult? again = null;
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, r => again = r);
            Assert.That(again.HasValue, Is.False, "a released avatar is no avatar the manager holds");
            Pump();
            Assert.That(again?.IsSuccess, Is.True);
            Assert.That(again.Value.Texture != null, Is.True);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(4), "read from the backend again");

            // Nothing to release changes nothing.
            node.Manager.ReleaseAvatar(new PlayerId(7));
            node.Manager.ReleaseAvatar(PlayerId.None);
            Assert.That(ofStranger != null && again.Value.Texture != null, Is.True);

            MultiplayerManager uninitialized = new MultiplayerManager();
            uninitialized.ReleaseAvatar(node.Id);
            uninitialized.ReleaseAvatars();
            uninitialized.Dispose();
            uninitialized.ReleaseAvatars();
        }

        [Test]
        public void ReleaseAvatars_KeepsThisPlayersOwnAndThoseOfThePlayersInTheSession_AndDestroysTheRest()
        {
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            Node a = JoinNew("A", host);
            Node b = JoinNew("B", host);
            Node c = CreatePlatformNode("C");
            PlayerId stranger = new PlayerId(424242);
            foreach (PlayerId player in new[] { host.Id, a.Id, b.Id, c.Id, stranger })
            {
                host.Platform.Avatars[(player, AvatarSize.Medium)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            }

            // C's avatar arrives before C joins, as a lobby list shows a host before the game joins.
            Texture2D ofC = LoadAvatar(host, c.Id, AvatarSize.Medium).Texture;
            Assert.That(Join(c, host).IsSuccess, Is.True);
            Texture2D own = LoadAvatar(host, host.Id, AvatarSize.Medium).Texture;
            Texture2D ofA = LoadAvatar(host, a.Id, AvatarSize.Medium).Texture;
            Texture2D ofB = LoadAvatar(host, b.Id, AvatarSize.Medium).Texture;
            Texture2D ofStranger = LoadAvatar(host, stranger, AvatarSize.Medium).Texture;
            b.Manager.Leave();
            Pump();

            host.Manager.ReleaseAvatars();
            Assert.That(own != null, Is.True, "this player's own stays");
            Assert.That(ofA != null && ofC != null, Is.True, "the players in the session keep theirs, also one that arrived before they joined");
            Assert.That(ofB == null, Is.True, "a player who left");
            Assert.That(ofStranger == null, Is.True, "an account outside the session");

            // The session still ends with the avatars it holds.
            host.Manager.Leave();
            Assert.That(ofA == null, Is.True);
            Assert.That(ofC != null, Is.True, "it did not arrive while C was in the session");
            host.Manager.ReleaseAvatars();
            Assert.That(ofC == null && own != null, Is.True);
        }

        [Test]
        public void ReleaseAvatar_FromACallback_LeavesTheNextOnesAFreshTexture()
        {
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            node.Platform.HoldsAvatars = true;

            Texture2D released = null;
            List<AvatarResult> later = new List<AvatarResult>();
            node.Manager.LoadAvatar(stranger, AvatarSize.Large, result =>
            {
                released = result.Texture;
                node.Manager.ReleaseAvatars();
            });
            node.Manager.LoadAvatar(stranger, AvatarSize.Large, later.Add);
            Pump();

            node.Platform.HoldsAvatars = false;
            Pump();
            Assert.That(released == null, Is.True, "the first callback's texture was released");
            Assert.That(later.Count, Is.EqualTo(1), "the next callback was asked again, and answered once");
            Assert.That(later[0].IsSuccess, Is.True);
            Assert.That(later[0].Texture != null, Is.True);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(2));
        }

        [Test]
        public void ReleaseAvatar_LeavesARequestStillWaiting_WhichIsAnsweredAndHeldAsUsual()
        {
            Node node = CreatePlatformNode("A");
            node.Platform.Avatars[(node.Id, AvatarSize.Small)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            node.Platform.HoldsAvatars = true;
            AvatarResult? result = null;
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, r => result = r);
            Pump();

            node.Manager.ReleaseAvatar(node.Id);
            node.Platform.HoldsAvatars = false;
            Pump();
            Texture2D texture = result?.Texture;
            Assert.That(texture != null, Is.True);

            AvatarResult? again = null;
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, r => again = r);
            Assert.That(again?.Texture, Is.SameAs(texture), "held, so answered at once");
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(1));
        }

        [Test]
        public void AvatarChange_RedrawsEverySizeTheManagerHolds_InTheSameTexture_AndRaisesOnAvatarChanged()
        {
            // FR-4: a player who picks a new picture shows it without the game asking again.
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Small)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, StripedAvatar(3, 2));
            Texture2D small = LoadAvatar(node, stranger, AvatarSize.Small).Texture;
            Texture2D large = LoadAvatar(node, stranger, AvatarSize.Large).Texture;
            List<AvatarResult> changes = new List<AvatarResult>();
            node.Manager.OnAvatarChanged += changes.Add;

            node.Platform.Avatars[(stranger, AvatarSize.Small)] = (MultiplayerReason.None, SolidAvatar(2, 2, 90));
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, StripedAvatar(4, 4));
            node.Platform.ReportAvatarChanged(stranger);
            Pump();

            Assert.That(changes.Select(change => (change.Reason, change.Player, change.Size)), Is.EquivalentTo(new[]
            {
                (MultiplayerReason.None, stranger, AvatarSize.Small),
                (MultiplayerReason.None, stranger, AvatarSize.Large),
            }));
            Assert.That(changes.Single(change => change.Size == AvatarSize.Small).Texture, Is.SameAs(small));
            Assert.That(changes.Single(change => change.Size == AvatarSize.Large).Texture, Is.SameAs(large));
            Assert.That(small.GetPixels32().All(pixel => pixel.r == 90), Is.True, "the new image, in the texture the game holds");
            Assert.That((large.width, large.height), Is.EqualTo((4, 4)), "the texture takes the new image's size");
            Color32[] pixels = large.GetPixels32();
            for (int y = 0; y < 4; y++)
            {
                Assert.That(pixels[y * 4].r, Is.EqualTo(StripeRed(3 - y)), $"row {y}, the right way up");
            }

            Assert.That(node.Platform.AvatarReads, Is.EqualTo(4), "the two sizes it holds, not the medium one");
            AvatarResult? again = null;
            node.Manager.LoadAvatar(stranger, AvatarSize.Small, r => again = r);
            Assert.That(again?.Texture, Is.SameAs(small), "still held, so answered at once");
        }

        [Test]
        public void AvatarChange_ToTheSameImage_OrOfAnAvatarTheManagerDoesNotHold_RaisesNothing()
        {
            Node node = CreatePlatformNode("A");
            node.Platform.Avatars[(node.Id, AvatarSize.Medium)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            Texture2D own = LoadAvatar(node, node.Id, AvatarSize.Medium).Texture;
            List<AvatarResult> changes = new List<AvatarResult>();
            node.Manager.OnAvatarChanged += changes.Add;

            node.Platform.ReportAvatarChanged(node.Id);
            node.Platform.ReportAvatarChanged(new PlayerId(7));
            Pump();

            Assert.That(changes, Is.Empty);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(2), "read again for the avatar it holds only");
            Assert.That(own != null, Is.True);
        }

        [Test]
        public void AvatarChange_ThatCannotBeRead_IsReadAgainASecondAfterEachFailure_UntilATryBringsTheImage()
        {
            // FR-4: a failed read after a change, such as the one that replaces the default image
            // Steam hands out first, does not leave the earlier image behind for good.
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.None, SolidAvatar(2, 2, 10));
            Texture2D texture = LoadAvatar(node, stranger, AvatarSize.Medium).Texture;
            List<AvatarResult> changes = new List<AvatarResult>();
            node.Manager.OnAvatarChanged += changes.Add;

            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.Timeout, default);
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(2));

            // NFR-1: a read that waits to be tried again costs a frame no allocation. The backend
            // holds its answers meanwhile, so only the manager's own update is measured.
            node.Platform.HoldsAvatars = true;
            Assert.That(() => node.Manager.Update(), Is.Not.AllocatingGCMemory());
            node.Platform.HoldsAvatars = false;

            Advance(0.9);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(2), "not read again within the second");
            Advance(0.2);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(3), "read again a second after the failure");
            Assert.That(changes, Is.Empty);

            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.None, SolidAvatar(2, 2, 90));
            Advance(1.0);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(4), "read again a second after the second failure");
            Assert.That(changes.Select(change => (change.Reason, change.Player, change.Size)), Is.EqualTo(new[] { (MultiplayerReason.None, stranger, AvatarSize.Medium) }));
            Assert.That(changes[0].Texture, Is.SameAs(texture));
            Assert.That(texture.GetPixels32().All(pixel => pixel.r == 90), Is.True, "the image the try brought");

            Advance(5.0);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(4), "not read again once a try brought the image");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AvatarChange_ThatStillCannotBeRead_GivesUpAfterThreeMoreTries_WithOneWarning()
        {
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.None, SolidAvatar(2, 2, 10));
            Texture2D texture = LoadAvatar(node, stranger, AvatarSize.Medium).Texture;
            List<AvatarResult> changes = new List<AvatarResult>();
            node.Manager.OnAvatarChanged += changes.Add;

            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.Timeout, default);
            LogAssert.Expect(LogType.Warning, new Regex(@"Medium avatar of 424242 changed but could not be read again \(Timeout\) in 4 tries"));
            node.Platform.ReportAvatarChanged(stranger);
            Advance(10.0);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(5), "the read after the change and three more");
            Assert.That(changes, Is.Empty);
            Assert.That(texture.GetPixels32().All(pixel => pixel.r == 10), Is.True, "the earlier image");

            // An image that arrives broken is not read again, as reading cannot mend it.
            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.None, new BackendAvatar(2, 2, new byte[3]));
            LogAssert.Expect(LogType.Warning, new Regex(@"as 2x2 pixels in 3 bytes"));
            node.Platform.ReportAvatarChanged(stranger);
            Advance(10.0);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(6));

            // A change while a failed read waits to be tried again starts the tries anew.
            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.Timeout, default);
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            LogAssert.Expect(LogType.Warning, new Regex(@"Medium avatar of 424242 changed but could not be read again \(Timeout\) in 4 tries"));
            Advance(10.0);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(11), "two reads after the changes and three more after the second");

            Assert.That(changes, Is.Empty);
            Assert.That(texture.GetPixels32().All(pixel => pixel.r == 10), Is.True, "the earlier image");
            AvatarResult? again = null;
            node.Manager.LoadAvatar(stranger, AvatarSize.Medium, r => again = r);
            Assert.That(again?.Texture, Is.SameAs(texture));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AvatarChange_AFailedReadToTryAgain_EndsWithItsAvatar_AndWithTheBackend()
        {
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Small)] = (MultiplayerReason.None, SolidAvatar(2, 2, 10));
            LoadAvatar(node, stranger, AvatarSize.Small);

            node.Platform.Avatars[(stranger, AvatarSize.Small)] = (MultiplayerReason.Timeout, default);
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Manager.ReleaseAvatar(stranger);
            Advance(5.0);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(2), "a released avatar is not read again");

            node.Platform.Avatars[(stranger, AvatarSize.Small)] = (MultiplayerReason.None, SolidAvatar(2, 2, 10));
            LoadAvatar(node, stranger, AvatarSize.Small);
            node.Platform.Avatars[(stranger, AvatarSize.Small)] = (MultiplayerReason.Timeout, default);
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Backend.Dispose();
            Advance(5.0);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(4), "a backend that is gone is not asked again");
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AvatarChange_WhoseLatestReadFails_TakesTheNewImageAnEarlierReadBrought(bool answersLastFirst)
        {
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, SolidAvatar(2, 2, 10));
            Texture2D texture = LoadAvatar(node, stranger, AvatarSize.Large).Texture;
            List<AvatarResult> changes = new List<AvatarResult>();
            node.Manager.OnAvatarChanged += changes.Add;

            // Two changes in a row: the read after the first brings the new image, the one after
            // the second fails, and the backend answers them in either order.
            node.Platform.HoldsAvatars = true;
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, SolidAvatar(2, 2, 50));
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.Timeout, default);
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Platform.AnswersLastFirst = answersLastFirst;
            node.Platform.HoldsAvatars = false;
            Pump();

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(changes[0].Texture, Is.SameAs(texture));
            Assert.That(texture.GetPixels32().All(pixel => pixel.r == 50), Is.True, "the image the earlier read brought");

            // The failed read is still tried again, as the later change may have brought another image.
            node.Platform.AnswersLastFirst = false;
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, SolidAvatar(2, 2, 90));
            Advance(1.5);
            Assert.That(changes.Count, Is.EqualTo(2));
            Assert.That(texture.GetPixels32().All(pixel => pixel.r == 90), Is.True);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(4));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AvatarChange_AnImageThatArrivesBroken_DoesNotHideTheNewImageAnEarlierReadBrings()
        {
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, SolidAvatar(2, 2, 10));
            Texture2D texture = LoadAvatar(node, stranger, AvatarSize.Large).Texture;
            List<AvatarResult> changes = new List<AvatarResult>();
            node.Manager.OnAvatarChanged += changes.Add;

            // Two changes in a row: the read after the first brings the new image, the one after
            // the second a broken image, which the backend answers first.
            node.Platform.HoldsAvatars = true;
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, SolidAvatar(2, 2, 50));
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, new BackendAvatar(2, 2, new byte[3]));
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Platform.AnswersLastFirst = true;
            node.Platform.HoldsAvatars = false;
            LogAssert.Expect(LogType.Warning, new Regex(@"as 2x2 pixels in 3 bytes"));
            Pump();

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(texture.GetPixels32().All(pixel => pixel.r == 50), Is.True, "the image the earlier read brought");
            Advance(5.0);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(3), "a broken image is not read again");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AvatarChange_WhileATryIsOnItsWay_StartsTheTriesAnew_AndThatTryChangesNothing()
        {
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.None, SolidAvatar(2, 2, 10));
            Texture2D texture = LoadAvatar(node, stranger, AvatarSize.Medium).Texture;
            List<AvatarResult> changes = new List<AvatarResult>();
            node.Manager.OnAvatarChanged += changes.Add;

            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.Timeout, default);
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Platform.HoldsAvatars = true;
            Advance(1.1);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(3), "the try is on its way");

            // A new change while the try is on its way; the try fails, the read after the change brings the image.
            node.Platform.Avatars[(stranger, AvatarSize.Medium)] = (MultiplayerReason.None, SolidAvatar(2, 2, 90));
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Platform.HoldsAvatars = false;
            Pump();

            Assert.That(changes.Count, Is.EqualTo(1));
            Assert.That(texture.GetPixels32().All(pixel => pixel.r == 90), Is.True);
            Advance(5.0);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(4), "the failed try, no longer the latest read, is not tried again");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AvatarChange_AFailedReadToTryAgain_EndsWithTheSession()
        {
            Node host = CreateHost(CreatePlatformNode("Host"), PublicOptions(SessionRules.Default));
            Node a = JoinNew("A", host);
            host.Platform.Avatars[(a.Id, AvatarSize.Small)] = (MultiplayerReason.None, SolidAvatar(2, 2, 10));
            Texture2D ofA = LoadAvatar(host, a.Id, AvatarSize.Small).Texture;

            host.Platform.Avatars[(a.Id, AvatarSize.Small)] = (MultiplayerReason.Timeout, default);
            host.Platform.ReportAvatarChanged(a.Id);
            Pump();
            Assert.That(host.Platform.AvatarReads, Is.EqualTo(2));
            host.Manager.Leave();
            Pump();
            Assert.That(ofA == null, Is.True, "ended with the session");

            Advance(5.0);
            Assert.That(host.Platform.AvatarReads, Is.EqualTo(2), "an avatar that ended with the session is not read again");
        }

        [Test]
        public void AvatarChange_AnEarlierReadThatAnswersAfterALaterOne_ChangesNothing_AndOneForAReleasedAvatarChangesNothing()
        {
            Node node = CreatePlatformNode("A");
            PlayerId stranger = new PlayerId(424242);
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, SolidAvatar(2, 2, 10));
            Texture2D texture = LoadAvatar(node, stranger, AvatarSize.Large).Texture;
            List<AvatarResult> changes = new List<AvatarResult>();
            node.Manager.OnAvatarChanged += changes.Add;

            // Two changes in a row, whose reads the backend answers the other way round.
            node.Platform.HoldsAvatars = true;
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, SolidAvatar(2, 2, 50));
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, SolidAvatar(2, 2, 90));
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Platform.AnswersLastFirst = true;
            node.Platform.HoldsAvatars = false;
            Pump();

            Assert.That(changes.Count, Is.EqualTo(1), "the earlier read, answered last, changes nothing");
            Assert.That(texture.GetPixels32().All(pixel => pixel.r == 90), Is.True);

            // A read that answers after the game released the avatar does not bring it back.
            node.Platform.HoldsAvatars = true;
            node.Platform.Avatars[(stranger, AvatarSize.Large)] = (MultiplayerReason.None, SolidAvatar(2, 2, 120));
            node.Platform.ReportAvatarChanged(stranger);
            Pump();
            node.Manager.ReleaseAvatar(stranger);
            node.Platform.HoldsAvatars = false;
            Pump();

            Assert.That(changes.Count, Is.EqualTo(1));
            AvatarResult? again = null;
            node.Manager.LoadAvatar(stranger, AvatarSize.Large, r => again = r);
            Assert.That(again.HasValue, Is.False, "released, so read anew");
            Pump();
            Assert.That(again?.Texture.GetPixels32().All(pixel => pixel.r == 120), Is.True);
            Assert.That(node.Platform.AvatarReads, Is.EqualTo(5));
        }

        [Test]
        public void LocalBackend_AnswersAnInvalidAvatarRequestWithInvalidArgument()
        {
            LocalBackend backend = CreateRawBackend("A");
            List<MultiplayerReason> reasons = new List<MultiplayerReason>();
            backend.LoadAvatar(PlayerId.None, AvatarSize.Small, (reason, avatar) => reasons.Add(reason));
            backend.LoadAvatar(backend.LocalPlayerId, (AvatarSize)3, (reason, avatar) => reasons.Add(reason));
            backend.LoadAvatar(backend.LocalPlayerId, AvatarSize.Small, (reason, avatar) => reasons.Add(reason));
            backend.Update();

            Assert.That(reasons, Is.EqualTo(new[] { MultiplayerReason.InvalidArgument, MultiplayerReason.InvalidArgument, MultiplayerReason.NotSupported }));
        }

        [Test]
        public void Dispose_DropsTheAvatarAnswersStillOnTheirWay()
        {
            Node node = CreatePlatformNode("A");
            node.Platform.Avatars[(node.Id, AvatarSize.Small)] = (MultiplayerReason.None, StripedAvatar(2, 2));
            node.Platform.HoldsAvatars = true;

            bool isAnswered = false;
            node.Manager.LoadAvatar(node.Id, AvatarSize.Small, _ => isAnswered = true);
            Pump();
            node.Manager.Dispose();

            node.Platform.HoldsAvatars = false;
            node.Platform.Update();
            Assert.That(isAnswered, Is.False);
        }
        #endregion

        #region Avatar Helpers
        private Node CreatePlatformNode(string name)
        {
            LocalBackend backend = new LocalBackend(_network, name);
            return AttachNode(backend, platform: new PlatformBackend(backend));
        }

        private AvatarResult LoadAvatar(Node node, PlayerId player, AvatarSize size)
        {
            AvatarResult? result = null;
            node.Manager.LoadAvatar(player, size, r => result = r);
            Pump();
            Assert.That(result.HasValue, Is.True, "LoadAvatar did not answer.");
            return result.Value;
        }

        /// <summary>An image whose rows each have a red of their own, from the top row down, and whose green is the column.</summary>
        private static BackendAvatar StripedAvatar(int width, int height)
        {
            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 4;
                    pixels[i] = StripeRed(y);
                    pixels[i + 1] = (byte)x;
                    pixels[i + 2] = 200;
                    pixels[i + 3] = 255;
                }
            }

            return new BackendAvatar(width, height, pixels);
        }

        private static byte StripeRed(int rowFromTop) => (byte)(10 + rowFromTop * 20);

        /// <summary>An image of one color, whose red tells it apart.</summary>
        private static BackendAvatar SolidAvatar(int width, int height, byte red)
        {
            byte[] pixels = new byte[width * height * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = red;
                pixels[i + 1] = 20;
                pixels[i + 2] = 30;
                pixels[i + 3] = 255;
            }

            return new BackendAvatar(width, height, pixels);
        }
        #endregion
    }
}
