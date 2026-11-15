using UnityEngine;

namespace Mu3Library.Extensions
{
    public static class CameraExtensions
    {
        /// <summary>
        /// Whether the camera can be measured for viewport work: it exists, it renders, and both
        /// <br/> its viewport rect and its pixel size cover an area.
        /// </summary>
        /// <remarks>
        /// A camera that is not ready returns a degenerate viewport, so anything that fits an
        /// object to the view has to wait for this instead of scaling against zero.
        /// </remarks>
        public static bool IsReady(this Camera camera)
        {
            return camera != null
                && camera.isActiveAndEnabled
                && camera.rect.width > 0.0f
                && camera.rect.height > 0.0f
                && camera.pixelWidth > 0
                && camera.pixelHeight > 0;
        }
    }
}
