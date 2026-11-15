using System.Collections.Generic;
using Mu3Library.DI;

namespace Mu3Library.Multiplayer
{
    /// <summary>
    /// Finds the multiplayer managers that the running cores under <see cref="CoreRoot"/> hold as
    /// singletons, the way the editor's Runtime Diagnostics drawer finds its services. The editor's
    /// Multiplayer Debugger window reads it; it lives in this assembly so that the play-mode tests,
    /// which cannot reach editor code, check the same lookup. Reading changes nothing.
    /// </summary>
    internal static class MultiplayerManagerLookup
    {
        /// <summary>Adds each manager with the type name of the core that holds it.</summary>
        public static void FindInCores(List<(string Source, IMultiplayerManager Manager)> results)
        {
            CoreRoot coreRoot = UnityEngine.Object.FindAnyObjectByType<CoreRoot>();
            if (coreRoot == null)
            {
                return;
            }

            foreach (CoreBase core in coreRoot.RegisteredCores)
            {
                if (core == null)
                {
                    continue;
                }

                foreach (object instance in core.GetActiveSingletonInstances())
                {
                    if (instance is IMultiplayerManager manager)
                    {
                        results.Add((core.GetType().Name, manager));
                    }
                }
            }
        }
    }
}
