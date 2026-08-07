using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Area
{
    /// <summary>
    /// Keeps the walls and the floor that hold the items inside the board.
    /// <br/> The colliders live as children of the board and are rebuilt whenever the board rectangle changes.
    /// </summary>
    public sealed class BoardAreaOutColliders
    {
        private const float Thickness = 10.0f;
        private const float SideHeight = 100.0f;
        private const string LeftName = "OutCollider_Left";
        private const string RightName = "OutCollider_Right";
        private const string BottomName = "OutCollider_Bottom";

        private readonly Transform _board;

        public BoxCollider2D Left { get; private set; }
        public BoxCollider2D Right { get; private set; }

        /// <summary>
        /// The floor an item can rest on.
        /// <br/> The left and right walls are separate on purpose, they cannot hold an item up.
        /// </summary>
        public BoxCollider2D Bottom { get; private set; }

        public BoardAreaOutColliders(Transform board)
        {
            _board = board;
        }

        /// <summary>
        /// Places the walls and the floor around the given item area.
        /// </summary>
        /// <param name="itemAreaBounds">The item area in the board local space.</param>
        public bool Rebuild(CoordinateBounds itemAreaBounds)
        {
            if (_board == null)
            {
                return false;
            }

            Vector2 size = itemAreaBounds.Size;
            if (size.x <= Mathf.Epsilon || size.y <= Mathf.Epsilon)
            {
                return false;
            }

            Vector2 center = itemAreaBounds.Center;

            Left = GetOrCreate(LeftName);
            Place(
                Left,
                new Vector2(itemAreaBounds.Min.x, center.y),
                new Vector2(Thickness, SideHeight),
                new Vector2(1.0f, 0.5f));

            Right = GetOrCreate(RightName);
            Place(
                Right,
                new Vector2(itemAreaBounds.Max.x, center.y),
                new Vector2(Thickness, SideHeight),
                new Vector2(0.0f, 0.5f));

            Bottom = GetOrCreate(BottomName);
            Place(
                Bottom,
                new Vector2(center.x, itemAreaBounds.Min.y),
                new Vector2(size.x, Thickness),
                new Vector2(0.5f, 1.0f));

            return true;
        }

        private BoxCollider2D GetOrCreate(string colliderName)
        {
            Transform colliderTransform = _board.Find(colliderName);
            if (colliderTransform == null)
            {
                GameObject colliderObject = new GameObject(colliderName);
                colliderObject.layer = _board.gameObject.layer;
                colliderTransform = colliderObject.transform;
                colliderTransform.SetParent(_board, false);
            }

            BoxCollider2D collider = colliderTransform.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = colliderTransform.gameObject.AddComponent<BoxCollider2D>();
            }

            return collider;
        }

        private static void Place(BoxCollider2D collider, Vector2 pivotPosition, Vector2 size, Vector2 pivot)
        {
            Transform colliderTransform = collider.transform;
            Vector2 centerPosition = pivotPosition + (new Vector2(0.5f, 0.5f) - pivot) * size;
            colliderTransform.localPosition = BoardAreaGeometry.ToVector3(centerPosition);
            colliderTransform.localRotation = Quaternion.identity;
            colliderTransform.localScale = Vector3.one;

            collider.offset = Vector2.zero;
            collider.size = size;
            collider.enabled = true;
        }
    }
}
