using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Area
{
    /// <summary>
    /// Geometry helpers shared by the board area parts.
    /// </summary>
    internal static class BoardAreaGeometry
    {
        /// <summary>
        /// Projects a screen position onto the plane the board lies on.
        /// </summary>
        /// <param name="plane">The board transform, its forward axis is the plane normal.</param>
        public static bool TryScreenToWorldOnPlane(Camera cam, Transform plane, Vector2 screenPosition, out Vector3 worldPosition)
        {
            if (cam == null || plane == null)
            {
                worldPosition = default;
                return false;
            }

            Ray ray = cam.ScreenPointToRay(screenPosition);
            Vector3 planeNormal = plane.forward;
            float denominator = Vector3.Dot(planeNormal, ray.direction);
            if (Mathf.Abs(denominator) <= Mathf.Epsilon)
            {
                worldPosition = default;
                return false;
            }

            float distance = Vector3.Dot(planeNormal, plane.position - ray.origin) / denominator;
            if (distance < 0.0f)
            {
                worldPosition = default;
                return false;
            }

            worldPosition = ray.GetPoint(distance);
            return true;
        }

        public static Vector2 Clamp01(Vector2 value)
            => new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));

        public static Vector4 Clamp01(Vector4 value)
            => new Vector4(
                Mathf.Clamp01(value.x),
                Mathf.Clamp01(value.y),
                Mathf.Clamp01(value.z),
                Mathf.Clamp01(value.w));

        public static Vector3 ToVector3(Vector2 value)
            => new Vector3(value.x, value.y, 0.0f);

        public static Vector2 ToVector2(Vector3 value)
            => new Vector2(value.x, value.y);
    }
}
