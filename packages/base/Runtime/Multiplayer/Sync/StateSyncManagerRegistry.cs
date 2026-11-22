using System.Collections.Generic;

namespace Mu3Library.Multiplayer.Sync
{
    /// <summary>
    /// The state sync managers that exist, oldest first, so the editor's Multiplayer Debugger
    /// window shows each one under the multiplayer manager it runs on. Each adds itself when it is
    /// created and leaves when it is disposed. Reading changes nothing.
    /// </summary>
    internal static class StateSyncManagerRegistry
    {
        private static readonly List<StateSyncManager> _managers = new List<StateSyncManager>();

        public static IReadOnlyList<StateSyncManager> Managers => _managers;

        public static void Add(StateSyncManager manager)
        {
            _managers.Add(manager);
        }

        public static void Remove(StateSyncManager manager)
        {
            _managers.Remove(manager);
        }
    }
}
