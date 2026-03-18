using UnityEngine;
using UnityEngine.UI;

namespace Mu3Library.Extensions
{
    public static class GraphicRaycasterExtensions
    {
        public static void CopyTo(this GraphicRaycaster source, GraphicRaycaster target)
        {
            if (source == null || target == null)
            {
                Debug.LogWarning("Exist null object.");
                return;
            }

            target.ignoreReversedGraphics = source.ignoreReversedGraphics;
            target.blockingObjects = source.blockingObjects;
            target.blockingMask = source.blockingMask;
        }
    }
}
