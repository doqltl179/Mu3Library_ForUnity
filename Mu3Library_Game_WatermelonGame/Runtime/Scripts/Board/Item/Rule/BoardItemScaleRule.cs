using UnityEngine;
using Mu3Library.Game.WatermelonGame.Board.Config;

namespace Mu3Library.Game.WatermelonGame.Board.Item.Rule
{
    /// <summary>
    /// Decides how big every fruit is.
    /// <br/> The size is always measured as the diameter of the area the fruit touches with, its
    /// <br/> collider, relative to the board area width, so the fruits keep their proportions on
    /// <br/> every screen resolution:
    /// <br/> the smallest fruit is <see cref="SmallestBoardWidthRatio"/> of the board width,
    /// <br/> the largest one is <see cref="LargestBoardWidthRatio"/>,
    /// <br/> and the fruits in between are spread linearly over that range.
    /// <br/> The sprite is only what the fruit is drawn with: it is scaled until its collider reaches
    /// <br/> the diameter above, so how much of a sprite its collider covers changes how large the
    /// <br/> sprite is drawn and never how large the fruit plays.
    /// <br/> The range is owned here, nobody else decides how big a fruit is.
    /// </summary>
    public class BoardItemScaleRule
    {
        /// <summary>
        /// The smallest fruit's contact diameter as a fraction of the board area width. (1/20)
        /// </summary>
        public const float DefaultSmallestBoardWidthRatio = 1.0f / 20.0f;

        /// <summary>
        /// The largest fruit's contact diameter as a fraction of the board area width. (2/5)
        /// </summary>
        public const float DefaultLargestBoardWidthRatio = 2.0f / 5.0f;

        private const int IndexMax = BoardItemsConfig.FruitItemCount - 1;

        protected float _smallestBoardWidthRatio = DefaultSmallestBoardWidthRatio;
        /// <summary>
        /// The smallest fruit's contact diameter as a fraction of the board area width,
        /// <see cref="DefaultSmallestBoardWidthRatio"/> until it is set. Negative values are ignored.
        /// </summary>
        public float SmallestBoardWidthRatio
        {
            get => _smallestBoardWidthRatio;
            set => _smallestBoardWidthRatio = Mathf.Max(0.0f, value);
        }

        protected float _largestBoardWidthRatio = DefaultLargestBoardWidthRatio;
        /// <summary>
        /// The largest fruit's contact diameter as a fraction of the board area width,
        /// <see cref="DefaultLargestBoardWidthRatio"/> until it is set. Negative values are ignored.
        /// </summary>
        public float LargestBoardWidthRatio
        {
            get => _largestBoardWidthRatio;
            set => _largestBoardWidthRatio = Mathf.Max(0.0f, value);
        }

        /// <summary>
        /// Sets the contact diameter range every fruit is spread over.
        /// </summary>
        /// <param name="smallestBoardWidthRatio">The smallest fruit's contact diameter as a fraction of the board area width.</param>
        /// <param name="largestBoardWidthRatio">The largest fruit's contact diameter as a fraction of the board area width.</param>
        public void SetBoardWidthRatios(float smallestBoardWidthRatio, float largestBoardWidthRatio)
        {
            SmallestBoardWidthRatio = smallestBoardWidthRatio;
            LargestBoardWidthRatio = largestBoardWidthRatio;
        }

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
        /// Returns the local scale needed to make the item's contact area relative to the board
        /// width, using the diameter range this rule carries.
        /// </summary>
        /// <param name="index">Zero-based item index. Index 0 is the smallest item.</param>
        /// <param name="info">The catalog entry assigned to the item.</param>
        /// <param name="boardSize">The board area's local size.</param>
        public float GetBoardScale(int index, BoardItemInfo info, Vector2 boardSize)
            => GetBoardScale(index, info, boardSize, _smallestBoardWidthRatio, _largestBoardWidthRatio);

        /// <summary>
        /// Returns the local scale needed to make the item's contact area relative to the board width.
        /// <br/> The size measured here is the collider and not the sprite, because the collider is
        /// <br/> what the items touch each other with. A catalog entry whose collider covers less of
        /// <br/> its sprite is therefore drawn larger, and an item index keeps the same contact area
        /// <br/> whichever configuration is applied.
        /// </summary>
        /// <param name="index">Zero-based item index. Index 0 is the smallest item.</param>
        /// <param name="info">The catalog entry assigned to the item.</param>
        /// <param name="boardSize">The board area's local size.</param>
        /// <param name="smallestItemWidthRatio">The smallest item's contact diameter as a fraction of the board width.</param>
        /// <param name="largestItemWidthRatio">The largest item's contact diameter as a fraction of the board width.</param>
        public virtual float GetBoardScale(
            int index,
            BoardItemInfo info,
            Vector2 boardSize,
            float smallestItemWidthRatio,
            float largestItemWidthRatio)
        {
            Sprite sprite = info != null ? info.Sprite : null;
            if (sprite == null || boardSize.x <= 0.0f || smallestItemWidthRatio <= 0.0f || largestItemWidthRatio <= 0.0f)
            {
                return 0.0f;
            }

            Vector2 spriteSize = sprite.bounds.size;
            float contactDiameter = Mathf.Max(spriteSize.x, spriteSize.y) * info.ColliderScale;
            if (contactDiameter <= Mathf.Epsilon)
            {
                return 0.0f;
            }

            float targetDiameter = GetBoardContactDiameter(index, boardSize, smallestItemWidthRatio, largestItemWidthRatio);
            return targetDiameter / contactDiameter;
        }

        /// <summary>
        /// Returns the item's contact diameter in the board local space, using the range this rule
        /// carries.
        /// </summary>
        /// <param name="index">Zero-based item index. Index 0 is the smallest item.</param>
        /// <param name="boardSize">The board area's local size.</param>
        public float GetBoardContactDiameter(int index, Vector2 boardSize)
            => GetBoardContactDiameter(index, boardSize, _smallestBoardWidthRatio, _largestBoardWidthRatio);

        /// <summary>
        /// Returns the item's contact diameter in the board local space.
        /// <br/> It depends on the item index and the board size alone, so the same index touches
        /// <br/> over the same area no matter which sprites the configuration carries.
        /// </summary>
        /// <param name="index">Zero-based item index. Index 0 is the smallest item.</param>
        /// <param name="boardSize">The board area's local size.</param>
        /// <param name="smallestItemWidthRatio">The smallest item's contact diameter as a fraction of the board width.</param>
        /// <param name="largestItemWidthRatio">The largest item's contact diameter as a fraction of the board width.</param>
        public virtual float GetBoardContactDiameter(
            int index,
            Vector2 boardSize,
            float smallestItemWidthRatio,
            float largestItemWidthRatio)
        {
            if (boardSize.x <= 0.0f)
            {
                return 0.0f;
            }

            return boardSize.x * GetBoardWidthDiameterRatio(index, smallestItemWidthRatio, largestItemWidthRatio);
        }

        /// <summary>
        /// Returns the item's contact diameter as a fraction of the board area width,
        /// using the range this rule carries.
        /// </summary>
        public float GetBoardWidthDiameterRatio(int index)
            => GetBoardWidthDiameterRatio(index, _smallestBoardWidthRatio, _largestBoardWidthRatio);

        /// <summary>
        /// Returns the item's contact diameter as a fraction of the board area width.
        /// <br/> Index 0 gets <paramref name="smallestItemWidthRatio"/>, the last item gets
        /// <br/> <paramref name="largestItemWidthRatio"/>, and every item in between is placed on the
        /// <br/> straight line connecting them.
        /// </summary>
        public virtual float GetBoardWidthDiameterRatio(int index, float smallestItemWidthRatio, float largestItemWidthRatio)
        {
            int clampedIndex = Mathf.Clamp(index, 0, IndexMax);
            return Mathf.Lerp(smallestItemWidthRatio, largestItemWidthRatio, clampedIndex / (float)Mathf.Max(1, IndexMax));
        }

        /// <summary>
        /// <br/> index 0: diameter 1
        /// </summary>
        public virtual float GetNormalizedDiameter(int index)
        {
            float smallestRatio = GetBoardWidthDiameterRatio(0);
            if (smallestRatio <= Mathf.Epsilon)
            {
                return 0.0f;
            }

            return GetBoardWidthDiameterRatio(index) / smallestRatio;
        }

        /// <summary>
        /// <br/> index 0: the area of a circle with diameter 1
        /// </summary>
        public virtual float GetNormalizedArea(int index)
        {
            float normalizedDiameter = GetNormalizedDiameter(index);
            return Mathf.PI * normalizedDiameter * normalizedDiameter * 0.25f;
        }
    }
}
