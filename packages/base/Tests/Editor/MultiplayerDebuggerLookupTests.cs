using System.Collections.Generic;
using System.Linq;
using Mu3Library.Editor.Window;
using Mu3Library.Multiplayer;
using NUnit.Framework;

namespace Mu3Library.Tests
{
    /// <summary>
    /// FR-42: the managers that the editor's Multiplayer Debugger window finds. Each manager lists
    /// itself from its creation until it is disposed, whoever created it, so the window finds one
    /// without a DI core.
    /// </summary>
    public class MultiplayerDebuggerLookupTests
    {
        private readonly List<MultiplayerManager> _managers = new List<MultiplayerManager>();



        [TearDown]
        public void TearDown()
        {
            foreach (MultiplayerManager manager in _managers)
            {
                manager.Dispose();
            }

            _managers.Clear();
        }

        [Test]
        public void FindManagers_ListsAManager_FromItsCreationUntilItIsDisposed()
        {
            MultiplayerManager manager = Create();
            CollectionAssert.Contains(FindManagers().Select(entry => entry.Manager), manager);

            manager.Dispose();
            CollectionAssert.DoesNotContain(FindManagers().Select(entry => entry.Manager), manager);
        }

        [Test]
        public void FindManagers_ListsEveryManager_EachUnderItsOwnName()
        {
            // A host and its clients can run in one process, as on the local network.
            MultiplayerManager host = Create();
            MultiplayerManager client = Create();

            List<(string Source, IMultiplayerManager Manager)> found = FindManagers();
            string hostSource = found.Single(entry => entry.Manager == host).Source;
            string clientSource = found.Single(entry => entry.Manager == client).Source;
            Assert.That(hostSource, Is.Not.EqualTo(clientSource));
        }

        private MultiplayerManager Create()
        {
            MultiplayerManager manager = new MultiplayerManager();
            _managers.Add(manager);
            return manager;
        }

        private static List<(string Source, IMultiplayerManager Manager)> FindManagers()
        {
            List<(string Source, IMultiplayerManager Manager)> found = new List<(string, IMultiplayerManager)>();
            MultiplayerDebuggerWindow.FindManagers(found);
            return found;
        }
    }
}
