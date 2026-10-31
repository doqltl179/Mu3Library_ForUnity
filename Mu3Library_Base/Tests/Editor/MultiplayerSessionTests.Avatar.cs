using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Backend;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
        /// it, answered on its next update as a backend must, and a record of every presence the
        /// manager asked it to show.
        /// </summary>
        private sealed class PlatformBackend : IMultiplayerBackend
        {
            private readonly LocalBackend _inner;
            private readonly Queue<Action> _avatarAnswers = new Queue<Action>();

            /// <summary>What a request for a player and size answers; any other answers NotSupported.</summary>
            public readonly Dictionary<(PlayerId Player, AvatarSize Size), (MultiplayerReason Reason, BackendAvatar Avatar)> Avatars =
                new Dictionary<(PlayerId Player, AvatarSize Size), (MultiplayerReason Reason, BackendAvatar Avatar)>();

            public int AvatarReads;

            /// <summary>Keeps the avatar answers back while true.</summary>
            public bool HoldsAvatars;

            public PlatformBackend(LocalBackend inner)
            {
                _inner = inner;
            }

            public string Name => "Platform";
            public bool IsAvailable => _inner.IsAvailable;
            public PlayerId LocalPlayerId => _inner.LocalPlayerId;
            public int MaxPacketSize => _inner.MaxPacketSize;
            public string GetPlayerName(PlayerId player) => _inner.GetPlayerName(player);
            public void SetListener(IMultiplayerBackendListener listener) => _inner.SetListener(listener);

            public void Update()
            {
                _inner.Update();
                if (HoldsAvatars)
                {
                    return;
                }

                int count = _avatarAnswers.Count;
                for (int i = 0; i < count; i++)
                {
                    _avatarAnswers.Dequeue()();
                }
            }

            public void CreateLobby(LobbyVisibility visibility, int capacity, IReadOnlyDictionary<string, string> data, Action<MultiplayerReason, LobbyId> onCompleted) => _inner.CreateLobby(visibility, capacity, data, onCompleted);
            public void SearchLobbies(LobbySearchQuery query, Action<MultiplayerReason, IReadOnlyList<BackendLobby>> onCompleted) => _inner.SearchLobbies(query, onCompleted);
            public void JoinLobby(LobbyId lobby, Action<MultiplayerReason> onCompleted) => _inner.JoinLobby(lobby, onCompleted);
            public void LeaveLobby(LobbyId lobby) => _inner.LeaveLobby(lobby);
            public string GetLobbyData(LobbyId lobby, string key) => _inner.GetLobbyData(lobby, key);
            public bool SetLobbyData(LobbyId lobby, string key, string value) => _inner.SetLobbyData(lobby, key, value);
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

            public (string Status, LobbyId Group, int GroupSize) Presence => Presences.Count > 0 ? Presences[Presences.Count - 1] : (null, LobbyId.None, 0);

            public bool SetPresence(string status, LobbyId group, int groupSize)
            {
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
        #endregion
    }
}
