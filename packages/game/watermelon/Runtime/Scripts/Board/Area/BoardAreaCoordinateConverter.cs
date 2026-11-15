using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Area
{
    /// <summary>
    /// Converts positions between the board spaces(local, world, screen) and their normalized forms.
    /// <br/> Every conversion needs the calculated <see cref="BoardAreaBounds"/>, so all of them fail
    /// <br/> until <see cref="SetBounds"/> received a valid rectangle.
    /// </summary>
    public sealed class BoardAreaCoordinateConverter
    {
        private readonly Transform _board;

        private BoardAreaBounds _bounds;
        public BoardAreaBounds Bounds => _bounds;

        private Camera _camera;
        /// <summary>
        /// The camera the board was calculated for, falling back to the main camera.
        /// </summary>
        public Camera Camera => _camera != null ? _camera : Camera.main;

        public bool HasBounds => _bounds.IsValid;

        public BoardAreaCoordinateConverter(Transform board)
        {
            _board = board;
        }

        public void SetBounds(BoardAreaBounds bounds) => _bounds = bounds;

        public void SetCamera(Camera cam) => _camera = cam;

        public void ClearBounds() => _bounds = default;

        #region Board local normalized
        public Vector3 BoardLocalNormalizedPositionToWorld(Vector2 position)
            => TryGetLocalPosition(position, out Vector3 local) ? LocalToWorld(local) : default;

        public Vector3 BoardLocalNormalizedPositionToScreen(Vector2 position)
        {
            if (!TryGetLocalPosition(position, out Vector3 local))
            {
                return default;
            }

            Camera cam = Camera;
            return cam != null
                ? cam.WorldToScreenPoint(LocalToWorld(local))
                : BoardScreenNormalizedPositionToScreen(position);
        }

        public Vector3 BoardLocalNormalizedPositionToLocal(Vector2 position)
            => TryGetLocalPosition(position, out Vector3 local) ? local : default;
        #endregion

        #region Board screen normalized
        public Vector3 BoardScreenNormalizedPositionToWorld(Vector2 position)
            => TryGetBoardScreenNormalizedWorldPosition(position, out Vector3 world) ? world : default;

        public Vector3 BoardScreenNormalizedPositionToScreen(Vector2 position)
        {
            if (!_bounds.IsValid)
            {
                return default;
            }

            Vector2 screenPosition = _bounds.Screen.Lerp(position);
            Camera cam = Camera;
            float depth = cam == null || _board == null ? 0.0f : cam.WorldToScreenPoint(_board.position).z;
            return new Vector3(screenPosition.x, screenPosition.y, depth);
        }

        public Vector3 BoardScreenNormalizedPositionToLocal(Vector2 position)
            => TryGetBoardScreenNormalizedWorldPosition(position, out Vector3 world) ? WorldToLocal(world) : default;
        #endregion

        #region To normalized
        public bool TryWorldToBoardLocalNormalizedPosition(Vector3 worldPos, out Vector2 boardNormalizedPosition)
            => TryLocalToBoardLocalNormalizedPosition(WorldToLocal(worldPos), out boardNormalizedPosition);

        public bool TryLocalToBoardLocalNormalizedPosition(Vector3 localPos, out Vector2 boardNormalizedPosition)
            => TryNormalize(_bounds.Local, BoardAreaGeometry.ToVector2(localPos), out boardNormalizedPosition);

        public bool TryScreenToBoardLocalNormalizedPosition(Vector3 screenPos, out Vector2 boardNormalizedPosition)
        {
            if (!TryScreenToWorld(screenPos, out Vector3 worldPosition))
            {
                boardNormalizedPosition = default;
                return false;
            }

            return TryWorldToBoardLocalNormalizedPosition(worldPosition, out boardNormalizedPosition);
        }

        public bool TryWorldToBoardScreenNormalizedPosition(Vector3 worldPos, out Vector2 boardNormalizedPosition)
        {
            Camera cam = Camera;
            if (cam == null)
            {
                boardNormalizedPosition = default;
                return false;
            }

            return TryScreenToBoardScreenNormalizedPosition(cam.WorldToScreenPoint(worldPos), out boardNormalizedPosition);
        }

        public bool TryScreenToBoardScreenNormalizedPosition(Vector3 screenPos, out Vector2 boardNormalizedPosition)
            => TryNormalize(_bounds.Screen, BoardAreaGeometry.ToVector2(screenPos), out boardNormalizedPosition);

        public bool TryLocalToBoardScreenNormalizedPosition(Vector3 localPos, out Vector2 boardNormalizedPosition)
            => TryWorldToBoardScreenNormalizedPosition(LocalToWorld(localPos), out boardNormalizedPosition);
        #endregion

        #region Space conversion
        /// <summary>
        /// Projects a screen position onto the board plane using the specified camera.
        /// </summary>
        public bool TryScreenToWorld(Camera cam, Vector2 screenPosition, out Vector3 worldPosition)
            => BoardAreaGeometry.TryScreenToWorldOnPlane(cam, _board, screenPosition, out worldPosition);

        public bool TryScreenToWorld(Vector3 screenPos, out Vector3 worldPos)
        {
            Camera cam = Camera;
            if (cam == null)
            {
                worldPos = default;
                return false;
            }

            return TryScreenToWorld(cam, new Vector2(screenPos.x, screenPos.y), out worldPos);
        }

        public Vector3 WorldToLocal(Vector3 worldPos)
            => _board != null ? _board.InverseTransformPoint(worldPos) : worldPos;

        public Vector3 LocalToWorld(Vector3 localPos)
            => _board != null ? _board.TransformPoint(localPos) : localPos;
        #endregion

        #region Area test and clamp
        public bool IsInLocalArea(Vector3 localPos, bool includeBoundary)
            => _bounds.Local.Contains(BoardAreaGeometry.ToVector2(localPos), includeBoundary);

        public bool IsInWorldArea(Vector3 worldPos, bool includeBoundary)
            => IsInLocalArea(WorldToLocal(worldPos), includeBoundary);

        public bool IsInScreenArea(Vector3 screenPos, bool includeBoundary)
            => _bounds.Screen.Contains(BoardAreaGeometry.ToVector2(screenPos), includeBoundary);

        public Vector3 ClampLocalPosition(Vector3 localPos)
            => _bounds.Local.Clamp(localPos);

        public Vector3 ClampWorldPosition(Vector3 worldPos)
            => LocalToWorld(ClampLocalPosition(WorldToLocal(worldPos)));

        public Vector3 ClampScreenPosition(Vector3 screenPos)
            => _bounds.Screen.Clamp(screenPos);
        #endregion

        private bool TryGetLocalPosition(Vector2 normalizedPosition, out Vector3 localPosition)
        {
            if (!_bounds.IsValid)
            {
                localPosition = default;
                return false;
            }

            localPosition = BoardAreaGeometry.ToVector3(_bounds.Local.Lerp(normalizedPosition));
            return true;
        }

        private bool TryNormalize(CoordinateBounds bounds, Vector2 position, out Vector2 normalizedPosition)
        {
            if (!_bounds.IsValid)
            {
                normalizedPosition = default;
                return false;
            }

            normalizedPosition = bounds.Normalize(position);
            return true;
        }

        private bool TryGetBoardScreenNormalizedWorldPosition(Vector2 normalizedPosition, out Vector3 worldPosition)
        {
            Camera cam = Camera;
            if (!_bounds.IsValid || cam == null)
            {
                worldPosition = default;
                return false;
            }

            Vector3 screenPosition = BoardScreenNormalizedPositionToScreen(normalizedPosition);
            return TryScreenToWorld(cam, BoardAreaGeometry.ToVector2(screenPosition), out worldPosition);
        }
    }
}
