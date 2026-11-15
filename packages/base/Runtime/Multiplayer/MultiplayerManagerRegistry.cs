using System.Collections.Generic;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// The multiplayer managers that exist, oldest first. Each manager adds itself when it is
    /// created and leaves when it is disposed, whichever container or code created it, so the
    /// editor's Multiplayer Debugger window finds them without a DI core. Reading changes nothing.
    /// </summary>
    internal static class MultiplayerManagerRegistry
    {
        private static readonly List<MultiplayerManager> _managers = new List<MultiplayerManager>();

        /// <summary>The managers created and not yet disposed, in the order they were created.</summary>
        public static IReadOnlyList<IMultiplayerManager> Managers => _managers;

        public static void Add(MultiplayerManager manager)
        {
            _managers.Add(manager);
        }

        public static void Remove(MultiplayerManager manager)
        {
            _managers.Remove(manager);
        }
    }
}
