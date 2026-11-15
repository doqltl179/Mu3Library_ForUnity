using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Avatar textures in play mode, where Unity destroys an object only at the end of the frame
    /// (FR-4): a texture the manager releases or ends with the session goes then, a callback or
    /// request in that frame never gets it, and a change of avatar redraws the texture the game
    /// shows instead of destroying it. Edit mode destroys at once, so it cannot show this.
    /// </summary>
    public class MultiplayerAvatarLifetimeTests
    {
        private const string GameId = "tests.multiplayer.avatars";
        private const int MaxUpdates = 200;

        /// <summary>
        /// A <see cref="LocalBackend"/> with the avatars a test hands it, answered on its next
        /// update as a backend must, and the avatar changes a test reports through it.
        /// </summary>
        private sealed class AvatarBackend : IMultiplayerBackend
        {
            private readonly LocalBackend _inner;
            private readonly Queue<Action> _answers = new Queue<Action>();
            private IMultiplayerBackendListener _listener;

            public readonly Dictionary<(PlayerId Player, AvatarSize Size), BackendAvatar> Avatars =
                new Dictionary<(PlayerId Player, AvatarSize Size), BackendAvatar>();

            public AvatarBackend(LocalBackend inner)
            {
                _inner = inner;
            }

            public string Name => "Avatars";
            public bool IsAvailable => _inner.IsAvailable;
            public PlayerId LocalPlayerId => _inner.LocalPlayerId;
            public int MaxPacketSize => _inner.MaxPacketSize;
            public string GetPlayerName(PlayerId player) => _inner.GetPlayerName(player);

            public void SetListener(IMultiplayerBackendListener listener)
            {
                _listener = listener;
                _inner.SetListener(listener);
            }

            public void Update()
            {
                _inner.Update();
                int count = _answers.Count;
                for (int i = 0; i < count; i++)
                {
                    _answers.Dequeue()();
                }
            }

            /// <summary>Tells the manager on the next update that the player's avatar changed.</summary>
            public void ReportAvatarChanged(PlayerId player) => _answers.Enqueue(() => _listener?.OnAvatarChanged(player));

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
            public bool SetPresence(string status, LobbyId group, int groupSize) => _inner.SetPresence(status, group, groupSize);

            public void LoadAvatar(PlayerId player, AvatarSize size, Action<MultiplayerReason, BackendAvatar> onCompleted)
            {
                MultiplayerReason reason = MultiplayerReason.NotSupported;
                BackendAvatar avatar = default;
                if (Avatars.TryGetValue((player, size), out BackendAvatar answer))
                {
                    // A copy for every answer, since the manager takes the array over.
                    reason = MultiplayerReason.None;
                    avatar = new BackendAvatar(answer.Width, answer.Height, (byte[])answer.Pixels.Clone());
                }

                _answers.Enqueue(() => onCompleted?.Invoke(reason, avatar));
            }

            public void Dispose() => _inner.Dispose();
        }

        private readonly List<MultiplayerManager> _managers = new List<MultiplayerManager>();
        private readonly List<IMultiplayerBackend> _backends = new List<IMultiplayerBackend>();
        private LocalNetwork _network;



        [SetUp]
        public void SetUp()
        {
            _network = new LocalNetwork();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (MultiplayerManager manager in _managers)
            {
                manager.Dispose();
            }

            foreach (IMultiplayerBackend backend in _backends)
            {
                backend.Dispose();
            }

            _managers.Clear();
            _backends.Clear();
        }

        [UnityTest]
        public IEnumerator ReleaseAvatar_DestroysTheTextureAtTheEndOfTheFrame_AndARequestInThatFrameGetsANewOne()
        {
            (MultiplayerManager manager, AvatarBackend backend) = Create("A");
            PlayerId self = manager.LocalPlayerId;
            backend.Avatars[(self, AvatarSize.Large)] = Solid(4, 4, 10);
            Texture2D released = Load(manager, self, AvatarSize.Large);

            manager.ReleaseAvatar(self);
            Assert.IsTrue(released != null, "Unity destroys it only at the end of the frame.");

            AvatarResult? again = null;
            manager.LoadAvatar(self, AvatarSize.Large, result => again = result);
            Assert.IsFalse(again.HasValue, "The texture being destroyed is no texture the manager holds.");
            manager.Update();
            Assert.IsTrue(again.HasValue && again.Value.IsSuccess);
            Texture2D fresh = again.Value.Texture;
            Assert.AreNotSame(released, fresh);

            yield return null;
            Assert.IsTrue(released == null, "Destroyed at the end of the frame it was released in.");
            Assert.IsTrue(fresh != null);
        }

        [UnityTest]
        public IEnumerator ACallbackThatReleasesTheAvatar_LeavesTheNextOnesATextureThatOutlivesTheFrame()
        {
            // The released texture still compares unequal to null until the frame ends, so the
            // manager must not hand it to the callbacks after the one that released it.
            (MultiplayerManager manager, AvatarBackend backend) = Create("A");
            PlayerId stranger = new PlayerId(424242);
            backend.Avatars[(stranger, AvatarSize.Medium)] = Solid(2, 2, 10);

            Texture2D released = null;
            AvatarResult? later = null;
            manager.LoadAvatar(stranger, AvatarSize.Medium, result =>
            {
                released = result.Texture;
                manager.ReleaseAvatars();
            });
            manager.LoadAvatar(stranger, AvatarSize.Medium, result => later = result);
            manager.Update();

            Assert.IsTrue(released != null, "Released, but not destroyed yet.");
            Assert.IsFalse(later.HasValue, "Asked again instead of answered with the texture being destroyed.");
            manager.Update();
            Assert.IsTrue(later.HasValue && later.Value.IsSuccess);
            Assert.AreNotSame(released, later.Value.Texture);

            yield return null;
            Assert.IsTrue(released == null);
            Assert.IsTrue(later.Value.Texture != null);
        }

        [UnityTest]
        public IEnumerator SessionEnd_DestroysTheAvatarsOfTheOtherPlayers_AtTheEndOfThatFrame()
        {
            (MultiplayerManager host, AvatarBackend backend) = Create("Host");
            LocalBackend clientBackend = new LocalBackend(_network, "Client");
            _backends.Add(clientBackend);
            MultiplayerManager client = new MultiplayerManager();
            _managers.Add(client);
            Assert.IsTrue(client.Initialize(clientBackend, new MultiplayerConfig(GameId, 1)).IsSuccess);

            MultiplayerResult? created = null;
            host.CreateLobby(new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = new SessionRules(4) }, result => created = result);
            Pump(() => created.HasValue, host, client);
            Assert.IsTrue(created.HasValue && created.Value.IsSuccess);

            MultiplayerResult? joined = null;
            client.JoinLobby(host.CurrentLobbyId, result => joined = result);
            Pump(() => joined.HasValue && host.Participants.Count == 2, host, client);
            Assert.IsTrue(joined.HasValue && joined.Value.IsSuccess);

            backend.Avatars[(client.LocalPlayerId, AvatarSize.Small)] = Solid(2, 2, 10);
            Texture2D ofClient = Load(host, client.LocalPlayerId, AvatarSize.Small);

            bool? isThereAtOffline = null;
            host.OnStateChanged += change =>
            {
                if (change.Current == SessionState.Offline)
                {
                    isThereAtOffline = ofClient != null;
                }
            };
            host.Leave();

            Assert.AreEqual(true, isThereAtOffline, "Unity was asked to destroy it, and does so at the end of the frame.");
            yield return null;
            Assert.IsTrue(ofClient == null, "Destroyed at the end of the frame the session ended in.");
        }

        [UnityTest]
        public IEnumerator AvatarChange_RedrawsTheTextureInPlace_WhichOutlivesTheFrame()
        {
            (MultiplayerManager manager, AvatarBackend backend) = Create("A");
            PlayerId self = manager.LocalPlayerId;
            backend.Avatars[(self, AvatarSize.Large)] = Solid(4, 4, 10);
            Texture2D texture = Load(manager, self, AvatarSize.Large);

            List<AvatarResult> changes = new List<AvatarResult>();
            manager.OnAvatarChanged += changes.Add;
            backend.Avatars[(self, AvatarSize.Large)] = Solid(4, 4, 200);
            backend.ReportAvatarChanged(self);
            manager.Update();
            manager.Update();

            Assert.AreEqual(1, changes.Count);
            Assert.AreSame(texture, changes[0].Texture, "The texture the game shows, redrawn.");
            Assert.AreEqual(200, texture.GetPixels32()[0].r);

            yield return null;
            Assert.IsTrue(texture != null, "Redrawn, not destroyed.");
            Assert.AreEqual(200, texture.GetPixels32()[0].r);
        }

        private (MultiplayerManager Manager, AvatarBackend Backend) Create(string name)
        {
            AvatarBackend backend = new AvatarBackend(new LocalBackend(_network, name));
            _backends.Add(backend);
            MultiplayerManager manager = new MultiplayerManager();
            _managers.Add(manager);
            Assert.IsTrue(manager.Initialize(backend, new MultiplayerConfig(GameId, 1)).IsSuccess);
            return (manager, backend);
        }

        /// <summary>Loads an avatar within this frame, updating the manager until it answers.</summary>
        private static Texture2D Load(MultiplayerManager manager, PlayerId player, AvatarSize size)
        {
            AvatarResult? result = null;
            manager.LoadAvatar(player, size, r => result = r);
            for (int i = 0; i < MaxUpdates && !result.HasValue; i++)
            {
                manager.Update();
            }

            Assert.IsTrue(result.HasValue && result.Value.IsSuccess, "LoadAvatar did not answer with a texture.");
            return result.Value.Texture;
        }

        /// <summary>Updates both managers within this frame until the condition holds.</summary>
        private static void Pump(Func<bool> done, MultiplayerManager host, MultiplayerManager client)
        {
            for (int i = 0; i < MaxUpdates && !done(); i++)
            {
                host.Update();
                client.Update();
            }
        }

        private static BackendAvatar Solid(int width, int height, byte red)
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
    }
}
