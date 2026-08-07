using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    public partial class BoardArea
    {
        public Vector3 BoardLocalNormalizedPositionToWorld(Vector2 position)
            => _converter.BoardLocalNormalizedPositionToWorld(position);

        public Vector3 BoardLocalNormalizedPositionToScreen(Vector2 position)
            => _converter.BoardLocalNormalizedPositionToScreen(position);

        public Vector3 BoardLocalNormalizedPositionToLocal(Vector2 position)
            => _converter.BoardLocalNormalizedPositionToLocal(position);

        public Vector3 BoardWorldNormalizedPositionToWorld(Vector2 position)
            => BoardLocalNormalizedPositionToWorld(position);

        public Vector3 BoardWorldNormalizedPositionToScreen(Vector2 position)
            => BoardLocalNormalizedPositionToScreen(position);

        public Vector3 BoardWorldNormalizedPositionToLocal(Vector2 position)
            => BoardLocalNormalizedPositionToLocal(position);

        public Vector3 BoardScreenNormalizedPositionToWorld(Vector2 position)
            => _converter.BoardScreenNormalizedPositionToWorld(position);

        public Vector3 BoardScreenNormalizedPositionToScreen(Vector2 position)
            => _converter.BoardScreenNormalizedPositionToScreen(position);

        public Vector3 BoardScreenNormalizedPositionToLocal(Vector2 position)
            => _converter.BoardScreenNormalizedPositionToLocal(position);

        public Vector2 WorldToBoardWorldNormalizedPosition(Vector3 worldPos)
            => TryWorldToBoardWorldNormalizedPosition(worldPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a world position to normalized board world coordinates when the board has been calculated.
        /// </summary>
        public bool TryWorldToBoardWorldNormalizedPosition(Vector3 worldPos, out Vector2 boardNormalizedPosition)
            => _converter.TryWorldToBoardLocalNormalizedPosition(worldPos, out boardNormalizedPosition);

        public Vector2 ScreenToBoardWorldNormalizedPosition(Vector3 screenPos)
            => TryScreenToBoardWorldNormalizedPosition(screenPos, out Vector2 normalized) ? normalized : default;

        public Vector2 LocalToBoardWorldNormalizedPosition(Vector3 localPos)
            => TryLocalToBoardWorldNormalizedPosition(localPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a world position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public Vector2 WorldToBoardLocalNormalizedPosition(Vector3 worldPos)
            => TryWorldToBoardLocalNormalizedPosition(worldPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a world position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public bool TryWorldToBoardLocalNormalizedPosition(Vector3 worldPos, out Vector2 boardNormalizedPosition)
            => _converter.TryWorldToBoardLocalNormalizedPosition(worldPos, out boardNormalizedPosition);

        /// <summary>
        /// Converts a screen position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public Vector2 ScreenToBoardLocalNormalizedPosition(Vector3 screenPos)
            => TryScreenToBoardLocalNormalizedPosition(screenPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a screen position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public bool TryScreenToBoardLocalNormalizedPosition(Vector3 screenPos, out Vector2 boardNormalizedPosition)
            => _converter.TryScreenToBoardLocalNormalizedPosition(screenPos, out boardNormalizedPosition);

        /// <summary>
        /// Converts a local position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public Vector2 LocalToBoardLocalNormalizedPosition(Vector3 localPos)
            => TryLocalToBoardLocalNormalizedPosition(localPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a local position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public bool TryLocalToBoardLocalNormalizedPosition(Vector3 localPos, out Vector2 boardNormalizedPosition)
            => _converter.TryLocalToBoardLocalNormalizedPosition(localPos, out boardNormalizedPosition);

        /// <summary>
        /// Converts a local position to normalized board world coordinates when the board has been calculated.
        /// </summary>
        public bool TryLocalToBoardWorldNormalizedPosition(Vector3 localPos, out Vector2 boardNormalizedPosition)
            => TryLocalToBoardLocalNormalizedPosition(localPos, out boardNormalizedPosition);

        public Vector2 WorldToBoardScreenNormalizedPosition(Vector3 worldPos)
            => TryWorldToBoardScreenNormalizedPosition(worldPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a world position to normalized board screen coordinates when the board has been calculated.
        /// </summary>
        public bool TryWorldToBoardScreenNormalizedPosition(Vector3 worldPos, out Vector2 boardNormalizedPosition)
            => _converter.TryWorldToBoardScreenNormalizedPosition(worldPos, out boardNormalizedPosition);

        public Vector2 ScreenToBoardScreenNormalizedPosition(Vector3 screenPos)
            => TryScreenToBoardScreenNormalizedPosition(screenPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a screen position to normalized board screen coordinates when the board has been calculated.
        /// </summary>
        public bool TryScreenToBoardScreenNormalizedPosition(Vector3 screenPos, out Vector2 boardNormalizedPosition)
            => _converter.TryScreenToBoardScreenNormalizedPosition(screenPos, out boardNormalizedPosition);

        /// <summary>
        /// Converts a screen position to normalized board world coordinates when the board has been calculated.
        /// </summary>
        public bool TryScreenToBoardWorldNormalizedPosition(Vector3 screenPos, out Vector2 boardNormalizedPosition)
            => _converter.TryScreenToBoardLocalNormalizedPosition(screenPos, out boardNormalizedPosition);

        public Vector2 LocalToBoardScreenNormalizedPosition(Vector3 localPos)
            => TryLocalToBoardScreenNormalizedPosition(localPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a local position to normalized board screen coordinates when the board has been calculated.
        /// </summary>
        public bool TryLocalToBoardScreenNormalizedPosition(Vector3 localPos, out Vector2 boardNormalizedPosition)
            => _converter.TryLocalToBoardScreenNormalizedPosition(localPos, out boardNormalizedPosition);

        /// <summary>
        /// Converts a screen position to a point on the board plane using the specified camera.
        /// </summary>
        public bool TryScreenToWorld(Camera cam, Vector2 screenPosition, out Vector3 worldPosition)
            => _converter.TryScreenToWorld(cam, screenPosition, out worldPosition);

        public bool TryScreenToWorld(Vector3 screenPos, out Vector3 worldPos)
            => _converter.TryScreenToWorld(screenPos, out worldPos);

        public Vector3 WorldToLocal(Vector3 worldPos)
            => transform.InverseTransformPoint(worldPos);

        public Vector3 LocalToWorld(Vector3 localPos)
            => transform.TransformPoint(localPos);

        public bool IsInWorldArea(Vector3 worldPos)
            => IsInWorldArea(worldPos, false);

        public bool IsInWorldArea(Vector3 worldPos, bool includeBoundary)
            => _converter.IsInWorldArea(worldPos, includeBoundary);

        public bool IsInLocalArea(Vector3 localPos)
            => IsInLocalArea(localPos, false);

        public bool IsInLocalArea(Vector3 localPos, bool includeBoundary)
            => _converter.IsInLocalArea(localPos, includeBoundary);

        public bool IsInScreenArea(Vector3 screenPos)
            => IsInScreenArea(screenPos, false);

        public bool IsInScreenArea(Vector3 screenPos, bool includeBoundary)
            => _converter.IsInScreenArea(screenPos, includeBoundary);

        /// <summary>
        /// Clamps normalized board coordinates to the board area.
        /// </summary>
        public Vector2 ClampBoardNormalizedPosition(Vector2 boardNormalizedPosition)
            => Clamp01(boardNormalizedPosition);

        /// <summary>
        /// Clamps a local position to the calculated board rectangle while preserving its local Z value.
        /// </summary>
        public Vector3 ClampLocalPosition(Vector3 localPos)
            => _converter.ClampLocalPosition(localPos);

        /// <summary>
        /// Clamps a world position to the calculated board rectangle while preserving its distance from the board plane.
        /// </summary>
        public Vector3 ClampWorldPosition(Vector3 worldPos)
            => _converter.ClampWorldPosition(worldPos);

        /// <summary>
        /// Clamps a screen position to the calculated board rectangle while preserving its screen Z value.
        /// </summary>
        public Vector3 ClampScreenPosition(Vector3 screenPos)
            => _converter.ClampScreenPosition(screenPos);
    }
}
