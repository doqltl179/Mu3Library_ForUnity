using System.Collections;
using System.Collections.Generic;
using Mu3Library.DI;
using Mu3Library.Multiplayer;
using Mu3Library.Multiplayer.Data;
using Mu3Library.Multiplayer.Local;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Mu3Library.Tests
{
    /// <summary>
    /// FR-42 in play mode: the lookup that the editor's Multiplayer Debugger window reads finds a
    /// manager that a core under <see cref="CoreRoot"/> holds as a singleton, and it is the live
    /// manager that the core updates, with the session data the window shows.
    /// </summary>
    public class MultiplayerDebuggerLookupTests
    {
        private const string GameId = "tests.multiplayer.debugger";
        private const int MaxFrames = 120;

        private sealed class SessionCore : CoreBase
        {
            protected override void ConfigureContainer()
            {
                RegisterClass<MultiplayerManager>();
            }
        }

        private readonly List<GameObject> _objects = new();
        private readonly List<LocalBackend> _backends = new();
        private MultiplayerManager _client;



        // Each test starts without a root, so the cores it finds are its own.
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            CoreRoot root = Object.FindAnyObjectByType<CoreRoot>();
            if (root != null)
            {
                Object.Destroy(root.gameObject);
                yield return null;
            }

            Assert.IsNull(Object.FindAnyObjectByType<CoreRoot>());
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _client?.Dispose();
            _client = null;

            foreach (GameObject go in _objects)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            _objects.Clear();
            yield return null;

            CoreRoot root = Object.FindAnyObjectByType<CoreRoot>();
            if (root != null)
            {
                Object.Destroy(root.gameObject);
                yield return null;
            }

            // The managers went with their cores; the backends belong to the test.
            foreach (LocalBackend backend in _backends)
            {
                backend.Dispose();
            }

            _backends.Clear();
        }

        [UnityTest]
        public IEnumerator Lookup_FindsTheManagerACoreHolds_WithItsLiveSession()
        {
            GameObject go = new(nameof(SessionCore));
            _objects.Add(go);
            go.AddComponent<SessionCore>();
            yield return null;

            List<(string Source, IMultiplayerManager Manager)> found = new();
            MultiplayerManagerLookup.FindInCores(found);
            Assert.AreEqual(1, found.Count, "The lookup finds the core's manager.");
            Assert.AreEqual(nameof(SessionCore), found[0].Source);
            IMultiplayerManager host = found[0].Manager;

            // The core updates its manager every frame; this test updates the client it adds.
            LocalNetwork network = new LocalNetwork();
            LocalBackend hostBackend = new LocalBackend(network, "Host");
            LocalBackend clientBackend = new LocalBackend(network, "Client");
            _backends.Add(hostBackend);
            _backends.Add(clientBackend);
            Assert.IsTrue(host.Initialize(hostBackend, new MultiplayerConfig(GameId, 1)).IsSuccess);
            _client = new MultiplayerManager();
            Assert.IsTrue(_client.Initialize(clientBackend, new MultiplayerConfig(GameId, 1)).IsSuccess);

            MultiplayerResult? created = null;
            host.CreateLobby(new LobbyCreateOptions { Visibility = LobbyVisibility.Public, Name = "Room", Rules = new SessionRules(4) }, result => created = result);
            for (int frame = 0; frame < MaxFrames && !created.HasValue; frame++)
            {
                yield return null;
            }

            Assert.IsTrue(created.HasValue && created.Value.IsSuccess, "The core's updates answer the lobby request.");

            MultiplayerResult? joined = null;
            _client.JoinLobby(host.CurrentLobbyId, result => joined = result);
            for (int frame = 0; frame < MaxFrames && !(joined.HasValue && host.Participants.Count == 2); frame++)
            {
                _client.Update();
                yield return null;
            }

            Assert.IsTrue(joined.HasValue && joined.Value.IsSuccess, "The client joins the core's session.");

            found.Clear();
            MultiplayerManagerLookup.FindInCores(found);
            Assert.AreEqual(1, found.Count);
            Assert.AreSame(host, found[0].Manager);
            Assert.AreEqual(SessionState.Lobby, found[0].Manager.State);
            Assert.IsTrue(found[0].Manager.IsHost);
            Assert.AreEqual(2, found[0].Manager.Participants.Count);
            Assert.IsTrue(found[0].Manager.TryGetConnectionQuality(_client.LocalPlayerId, out _), "The window shows the host's connection to the client.");
            Assert.IsFalse(found[0].Manager.TryGetConnectionQuality(host.LocalPlayerId, out _));
        }

        [UnityTest]
        public IEnumerator Lookup_WithoutARoot_FindsNothing()
        {
            yield return null;

            List<(string Source, IMultiplayerManager Manager)> found = new();
            MultiplayerManagerLookup.FindInCores(found);
            Assert.IsEmpty(found);
        }
    }
}
