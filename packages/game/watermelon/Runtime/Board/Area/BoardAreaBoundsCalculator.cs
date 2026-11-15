using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Area
{
    /// <summary>
    /// Builds the board rectangle from a camera.
    /// <br/> The rectangle is first laid out in screen pixels, because the padding and the pivot
    /// <br/> describe the visible viewport, and is then projected onto the board plane.
    /// </summary>
    public static class BoardAreaBoundsCalculator
    {
        /// <summary>
        /// Calculates the board rectangle in every coordinate space.
        /// </summary>
        /// <param name="cam">The camera the board is laid out for.</param>
        /// <param name="board">The board transform, its plane receives the projected rectangle.</param>
        /// <param name="viewportPadding">x: Left, y: Right, z: Top, w: Bottom, as a fraction of the camera pixel rect.</param>
        /// <param name="pivot">Where the board sits inside the padded viewport.</param>
        /// <param name="aspectRatio">The board width divided by its height.</param>
        public static bool TryCalculate(
            Camera cam,
            Transform board,
            Vector4 viewportPadding,
            Vector2 pivot,
            float aspectRatio,
            out BoardAreaBounds bounds)
        {
            bounds = default;
            if (board == null)
            {
                return false;
            }

            if (!TryGetScreenRect(cam, viewportPadding, pivot, aspectRatio, board, out Rect screenRect))
            {
                return false;
            }

            if (!TryGetBoardBounds(cam, board, screenRect, out CoordinateBounds localBounds, out CoordinateBounds worldBounds))
            {
                Debug.LogWarning("Cannot calculate board screen positions because the board plane cannot be intersected mathematically from the camera.", board);
                return false;
            }

            bounds = new BoardAreaBounds(
                localBounds,
                new CoordinateBounds(screenRect.min, screenRect.max),
                worldBounds);
            return true;
        }

        private static bool TryGetScreenRect(
            Camera cam,
            Vector4 viewportPadding,
            Vector2 pivot,
            float aspectRatio,
            Object context,
            out Rect boardRect)
        {
            boardRect = default;
            if (cam == null)
            {
                Debug.LogWarning("Cannot calculate board screen positions without a camera.", context);
                return false;
            }

            Rect pixelRect = cam.pixelRect;
            if (pixelRect.width <= Mathf.Epsilon || pixelRect.height <= Mathf.Epsilon)
            {
                Debug.LogWarning("Cannot calculate board screen positions from an empty camera pixel rect.", context);
                return false;
            }

            Vector4 padding = BoardAreaGeometry.Clamp01(viewportPadding);
            Rect viewportRect = Rect.MinMaxRect(
                pixelRect.xMin + pixelRect.width * padding.x,
                pixelRect.yMin + pixelRect.height * padding.w,
                pixelRect.xMax - pixelRect.width * padding.y,
                pixelRect.yMax - pixelRect.height * padding.z);
            if (viewportRect.width <= Mathf.Epsilon || viewportRect.height <= Mathf.Epsilon)
            {
                Debug.LogWarning("Cannot calculate board screen positions because viewport padding leaves no area.", context);
                return false;
            }

            if (aspectRatio <= Mathf.Epsilon)
            {
                Debug.LogWarning("Cannot calculate board screen positions from a non positive aspect ratio.", context);
                return false;
            }

            float boardWidth = viewportRect.width;
            float boardHeight = viewportRect.height;
            if (boardWidth / boardHeight > aspectRatio)
            {
                boardWidth = boardHeight * aspectRatio;
            }
            else
            {
                boardHeight = boardWidth / aspectRatio;
            }

            Vector2 clampedPivot = BoardAreaGeometry.Clamp01(pivot);
            float boardLeft = viewportRect.xMin + (viewportRect.width - boardWidth) * clampedPivot.x;
            float boardBottom = viewportRect.yMin + (viewportRect.height - boardHeight) * clampedPivot.y;
            boardRect = new Rect(boardLeft, boardBottom, boardWidth, boardHeight);
            return true;
        }

        private static bool TryGetBoardBounds(
            Camera cam,
            Transform board,
            Rect screenRect,
            out CoordinateBounds localBounds,
            out CoordinateBounds worldBounds)
        {
            localBounds = default;
            worldBounds = default;

            Vector2 bottomLeft = new Vector2(screenRect.xMin, screenRect.yMin);
            Vector2 topLeft = new Vector2(screenRect.xMin, screenRect.yMax);
            Vector2 bottomRight = new Vector2(screenRect.xMax, screenRect.yMin);
            Vector2 topRight = new Vector2(screenRect.xMax, screenRect.yMax);

            if (!BoardAreaGeometry.TryScreenToWorldOnPlane(cam, board, bottomLeft, out Vector3 worldBottomLeft) ||
                !BoardAreaGeometry.TryScreenToWorldOnPlane(cam, board, topLeft, out Vector3 worldTopLeft) ||
                !BoardAreaGeometry.TryScreenToWorldOnPlane(cam, board, bottomRight, out Vector3 worldBottomRight) ||
                !BoardAreaGeometry.TryScreenToWorldOnPlane(cam, board, topRight, out Vector3 worldTopRight))
            {
                return false;
            }

            // The public world bounds remain an XY AABB for compatibility. The local bounds,
            // however, must come from the actual plane intersections so tilted cameras and boards
            // do not lose their world Z coordinates before the inverse transform.
            Vector3 localBottomLeft = board.InverseTransformPoint(worldBottomLeft);
            Vector3 localTopLeft = board.InverseTransformPoint(worldTopLeft);
            Vector3 localBottomRight = board.InverseTransformPoint(worldBottomRight);
            Vector3 localTopRight = board.InverseTransformPoint(worldTopRight);

            Vector2 localMin = new Vector2(
                Mathf.Min(Mathf.Min(localBottomLeft.x, localTopLeft.x), Mathf.Min(localBottomRight.x, localTopRight.x)),
                Mathf.Min(Mathf.Min(localBottomLeft.y, localTopLeft.y), Mathf.Min(localBottomRight.y, localTopRight.y)));
            Vector2 localMax = new Vector2(
                Mathf.Max(Mathf.Max(localBottomLeft.x, localTopLeft.x), Mathf.Max(localBottomRight.x, localTopRight.x)),
                Mathf.Max(Mathf.Max(localBottomLeft.y, localTopLeft.y), Mathf.Max(localBottomRight.y, localTopRight.y)));
            localBounds = new CoordinateBounds(localMin, localMax);

            Vector2 worldMin = new Vector2(
                Mathf.Min(Mathf.Min(worldBottomLeft.x, worldTopLeft.x), Mathf.Min(worldBottomRight.x, worldTopRight.x)),
                Mathf.Min(Mathf.Min(worldBottomLeft.y, worldTopLeft.y), Mathf.Min(worldBottomRight.y, worldTopRight.y)));
            Vector2 worldMax = new Vector2(
                Mathf.Max(Mathf.Max(worldBottomLeft.x, worldTopLeft.x), Mathf.Max(worldBottomRight.x, worldTopRight.x)),
                Mathf.Max(Mathf.Max(worldBottomLeft.y, worldTopLeft.y), Mathf.Max(worldBottomRight.y, worldTopRight.y)));

            worldBounds = new CoordinateBounds(worldMin, worldMax);
            return true;
        }
    }
}
