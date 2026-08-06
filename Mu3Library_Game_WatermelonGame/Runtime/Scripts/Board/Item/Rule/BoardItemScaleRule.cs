using UnityEngine;
using Mu3Library.Game.WatermelonGame.Board.Config;

namespace Mu3Library.Game.WatermelonGame.Board.Item.Rule
{
    public class BoardItemScaleRule
    {
        public float GetScreenSize(int index, Sprite sprite)
        {
            Camera cam = Camera.main;
            return GetScreenSize(index, sprite, cam);
        }

        public float GetScreenSize(int index, Sprite sprite, Camera cam)
        {
            Vector2 size = sprite != null ? sprite.rect.size : Vector2.zero;
            return GetScreenSize(index, size, cam);
        }

        public float GetScreenSize(int index, Vector2 spriteSize)
        {
            Camera cam = Camera.main;
            return GetScreenSize(index, spriteSize, cam);
        }

        public float GetScreenSize(int index, Vector2 spriteSize, Camera cam)
        {
            float sizeMin = Mathf.Min(spriteSize.x, spriteSize.y);
            return GetScreenSize(index, sizeMin, cam);
        }

        public float GetScreenSize(int index, float spriteSize)
        {
            Camera cam = Camera.main;
            return GetScreenSize(index, spriteSize, cam);
        }

        /// <summary>
        /// Returns the item's projected diameter in camera pixels.
        /// <paramref name="spriteSize"/> keeps the unit supplied by the caller.
        /// </summary>
        public virtual float GetScreenSize(int index, float spriteSize, Camera cam)
        {
            if (cam == null || spriteSize <= 0.0f)
            {
                return 0.0f;
            }

            Rect pixelRect = cam.pixelRect;
            if (pixelRect.height <= 0.0f)
            {
                return 0.0f;
            }

            float screenWorldHeight;
            if (cam.orthographic)
            {
                screenWorldHeight = cam.orthographicSize * 2.0f;
            }
            else
            {
                // BoardItemRule has no item position, so use the world origin as the 2D board plane.
                Vector3 toReferencePoint = -cam.transform.position;
                float distance = Vector3.Dot(toReferencePoint, cam.transform.forward);
                if (distance <= 0.0f)
                {
                    return 0.0f;
                }

                float halfFieldOfView = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
                screenWorldHeight = 2.0f * distance * Mathf.Tan(halfFieldOfView);
            }

            if (screenWorldHeight <= 0.0f)
            {
                return 0.0f;
            }

            float normalizedDiameter = GetNormalizedDiameter(index);
            return spriteSize / screenWorldHeight * pixelRect.height * normalizedDiameter;
        }

        public float GetScreenScale(int index, Sprite sprite)
        {
            Camera cam = Camera.main;
            return GetScreenScale(index, sprite, cam);
        }

        public float GetScreenScale(int index, Sprite sprite, Camera cam)
        {
            Vector2 size = sprite != null ? sprite.rect.size : Vector2.zero;
            return GetScreenScale(index, size, cam);
        }

        public float GetScreenScale(int index, Vector2 spriteSize)
        {
            Camera cam = Camera.main;
            return GetScreenScale(index, spriteSize, cam);
        }

        public float GetScreenScale(int index, Vector2 spriteSize, Camera cam)
        {
            float sizeMin = Mathf.Min(spriteSize.x, spriteSize.y);
            return GetScreenScale(index, sizeMin, cam);
        }

        public float GetScreenScale(int index, float spriteSize)
        {
            Camera cam = Camera.main;
            return GetScreenScale(index, spriteSize, cam);
        }

        /// <summary>
        /// Returns the local scale needed to make the item the requested normalized size.
        /// </summary>
        public virtual float GetScreenScale(int index, float spriteSize, Camera cam)
        {
            float baseScreenSize = GetScreenSize(0, spriteSize, cam);
            if (baseScreenSize <= Mathf.Epsilon)
            {
                return 0.0f;
            }

            return GetScreenSize(index, spriteSize, cam) / baseScreenSize;
        }

        /// <summary>
        /// Returns the local scale needed to make the item size relative to the board width.
        /// </summary>
        /// <param name="index">Zero-based item index. Index 0 is the smallest item.</param>
        /// <param name="sprite">The sprite assigned to the item.</param>
        /// <param name="boardSize">The board area's local size.</param>
        /// <param name="smallestItemWidthRatio">The smallest item's width as a fraction of the board width.</param>
        public virtual float GetBoardScale(
            int index,
            Sprite sprite,
            Vector2 boardSize,
            float smallestItemWidthRatio)
        {
            if (sprite == null || boardSize.x <= 0.0f || smallestItemWidthRatio <= 0.0f)
            {
                return 0.0f;
            }

            Vector2 spriteSize = sprite.bounds.size;
            float spriteDiameter = Mathf.Max(spriteSize.x, spriteSize.y);
            if (spriteDiameter <= Mathf.Epsilon)
            {
                return 0.0f;
            }

            float targetDiameter = boardSize.x * smallestItemWidthRatio * GetNormalizedDiameter(index);
            return targetDiameter / spriteDiameter;
        }

        public virtual float GetNormalizedArea(int index)
        {
            const float multiplyMax = 2.0f;
            const float multiplyInterval = 0.1f;
            const int indexMax = BoardItemsConfig.FruitItemCount - 1;

            float area = Mathf.PI * 0.25f; // Area of a circle with diameter 1.
            int clampedIndex = Mathf.Clamp(index, 0, indexMax);

            for (int i = 1; i <= clampedIndex; i++)
            {
                float multiplyFactor = multiplyMax - ((i - 1) * multiplyInterval);
                area *= multiplyFactor;
            }

            return area;
        }

        /// <summary>
        /// <br/> index 0: diameter 1
        /// </summary>
        public virtual float GetNormalizedDiameter(int index)
        {
            float normalizedArea = GetNormalizedArea(index);
            return 2.0f * Mathf.Sqrt(normalizedArea / Mathf.PI);
        }
    }
}
