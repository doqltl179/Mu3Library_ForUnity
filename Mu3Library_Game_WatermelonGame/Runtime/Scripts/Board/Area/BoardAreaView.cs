using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Area
{
    /// <summary>
    /// Owns everything the board area draws, the board sprite itself, the item out line
    /// <br/> and the spawn marker.
    /// <br/> It never calculates the board rectangle, it only consumes the calculated one.
    /// </summary>
    public sealed class BoardAreaView
    {
        private const string LineObjectName = "Line";
        private const int LineSortingOffset = 100;

        private const string SpawnObjectName = "Spawn";
        private const int SpawnSortingOffset = 100;

        private readonly Transform _board;
        private readonly SpriteRenderer _boardRenderer;

        private SpriteRenderer _lineRenderer;
        private SpriteRenderer _spawnRenderer;

        /// <summary>
        /// Where the spawn marker sits on the top edge, as a fraction of the board width.
        /// <br/> It is kept here so the marker can be placed again whenever the board is recalculated.
        /// </summary>
        private float _spawnNormalizedX = 0.5f;

        public SpriteRenderer BoardRenderer => _boardRenderer;

        /// <summary>
        /// The item out line renderer, created on first use.
        /// </summary>
        public SpriteRenderer LineRenderer
        {
            get
            {
                if (_lineRenderer == null && _board != null)
                {
                    GameObject lineObject = new GameObject(LineObjectName);
                    lineObject.transform.SetParent(_board);

                    _lineRenderer = lineObject.AddComponent<SpriteRenderer>();
                }

                return _lineRenderer;
            }
        }

        /// <summary>
        /// The spawn marker renderer, created on first use.
        /// </summary>
        public SpriteRenderer SpawnRenderer
        {
            get
            {
                if (_spawnRenderer == null && _board != null)
                {
                    GameObject spawnObject = new GameObject(SpawnObjectName);

                    // The local scale is kept at one, so the marker is scaled by the board
                    // exactly like the board sprite and keeps its size on every screen resolution.
                    spawnObject.transform.SetParent(_board, false);

                    _spawnRenderer = spawnObject.AddComponent<SpriteRenderer>();
                }

                return _spawnRenderer;
            }
        }

        public Sprite BoardSprite => _boardRenderer != null ? _boardRenderer.sprite : null;

        public Sprite LineSprite => _lineRenderer != null ? _lineRenderer.sprite : null;

        public Sprite SpawnSprite => _spawnRenderer != null ? _spawnRenderer.sprite : null;

        public float SpawnNormalizedX => _spawnNormalizedX;

        /// <summary>
        /// The board sprite width divided by its height, 1 while no sprite is assigned.
        /// </summary>
        public float AspectRatio
        {
            get
            {
                Sprite sprite = BoardSprite;
                if (sprite == null || sprite.rect.size.y <= Mathf.Epsilon)
                {
                    return 1.0f;
                }

                return sprite.rect.size.x / sprite.rect.size.y;
            }
        }

        public BoardAreaView(Transform board, SpriteRenderer boardRenderer)
        {
            _board = board;
            _boardRenderer = boardRenderer;
        }

        public void SetBoardSprite(Sprite sprite)
        {
            if (_boardRenderer == null)
            {
                return;
            }

            _boardRenderer.sprite = sprite;
        }

        /// <summary>
        /// Scales and centers the board so the sprite fills the padded camera viewport.
        /// </summary>
        /// <returns>True when the board transform was updated, the caller has to recalculate the bounds.</returns>
        public bool Fit(Camera cam, Sprite sprite, Vector4 viewportPadding)
        {
            if (_board == null || _boardRenderer == null || sprite == null || !BoardAreaGeometry.IsCameraReady(cam))
            {
                return false;
            }

            _boardRenderer.sprite = sprite;

            Vector2 spriteSize = sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return false;
            }

            Vector4 padding = BoardAreaGeometry.Clamp01(viewportPadding);
            float left = padding.x;
            float right = 1f - padding.y;
            float bottom = padding.w;
            float top = 1f - padding.z;
            if (right - left <= Mathf.Epsilon || top - bottom <= Mathf.Epsilon)
            {
                return false;
            }

            float distance = Vector3.Dot(_board.position - cam.transform.position, cam.transform.forward);
            if (distance < cam.nearClipPlane || distance > cam.farClipPlane)
            {
                return false;
            }

            Vector3 lowerLeft = cam.ViewportToWorldPoint(new Vector3(left, bottom, distance));
            Vector3 lowerRight = cam.ViewportToWorldPoint(new Vector3(right, bottom, distance));
            Vector3 upperLeft = cam.ViewportToWorldPoint(new Vector3(left, top, distance));
            float viewportWidth = Vector3.Distance(lowerLeft, lowerRight);
            float viewportHeight = Vector3.Distance(lowerLeft, upperLeft);
            float fitScale = Mathf.Min(viewportWidth / spriteSize.x, viewportHeight / spriteSize.y);

            Vector3 localScale = _board.localScale;
            _board.localScale = new Vector3(fitScale, fitScale, localScale.z);

            Vector3 viewportCenter = cam.ViewportToWorldPoint(new Vector3(
                (left + right) * 0.5f,
                (bottom + top) * 0.5f,
                distance));
            _board.SetPositionAndRotation(viewportCenter, cam.transform.rotation);
            _board.position += viewportCenter - _boardRenderer.bounds.center;

            return true;
        }

        /// <summary>
        /// Places the item out line on the board, creating its renderer when a sprite is given.
        /// </summary>
        /// <param name="sprite">The line sprite, null hides the line.</param>
        /// <param name="bounds">The calculated board rectangle.</param>
        /// <param name="normalizedY">The line height as a fraction of the board height.</param>
        public void SetItemOutLine(Sprite sprite, BoardAreaBounds bounds, float normalizedY)
        {
            // Nothing was ever shown and nothing is asked for, so the line renderer stays uncreated.
            if (sprite == null && _lineRenderer == null)
            {
                return;
            }

            SpriteRenderer lineRenderer = LineRenderer;
            if (lineRenderer == null)
            {
                return;
            }

            lineRenderer.sprite = sprite;
            if (sprite == null)
            {
                lineRenderer.gameObject.SetActive(false);
                return;
            }

            if (!bounds.IsValid)
            {
                return;
            }

            Transform lineTransform = lineRenderer.transform;
            Vector2 normalizedPosition = new Vector2(0.5f, Mathf.Clamp01(normalizedY));
            Vector3 lineLocalPosition = BoardAreaGeometry.ToVector3(bounds.Local.Lerp(normalizedPosition));
            lineLocalPosition.z = lineTransform.localPosition.z;
            lineTransform.localPosition = lineLocalPosition;

            float spriteWidth = sprite.bounds.size.x;
            float boardWidth = bounds.Local.Size.x;
            if (spriteWidth > Mathf.Epsilon && boardWidth > Mathf.Epsilon)
            {
                Vector3 lineScale = lineTransform.localScale;
                lineScale.x = boardWidth / spriteWidth;
                lineTransform.localScale = lineScale;
            }

            if (_boardRenderer != null)
            {
                lineRenderer.sortingOrder = _boardRenderer.sortingOrder + LineSortingOffset;
            }

            lineRenderer.gameObject.SetActive(true);
        }

        /// <summary>
        /// Hangs the spawn marker on the top edge of the board, creating its renderer when a sprite is given.
        /// </summary>
        /// <param name="sprite">
        /// The spawn sprite, null hides the marker.
        /// <br/> Its pivot is expected to be (0.5, 0.0), any other pivot is compensated
        /// <br/> so that the bottom center of the sprite always meets the top edge.
        /// </param>
        /// <param name="bounds">The calculated board rectangle.</param>
        /// <param name="normalizedX">The marker position as a fraction of the board width.</param>
        public void SetSpawnMarker(Sprite sprite, BoardAreaBounds bounds, float normalizedX)
        {
            _spawnNormalizedX = Mathf.Clamp01(normalizedX);

            // Nothing was ever shown and nothing is asked for, so the marker renderer stays uncreated.
            if (sprite == null && _spawnRenderer == null)
            {
                return;
            }

            SpriteRenderer spawnRenderer = SpawnRenderer;
            if (spawnRenderer == null)
            {
                return;
            }

            spawnRenderer.sprite = sprite;
            if (sprite == null)
            {
                spawnRenderer.gameObject.SetActive(false);
                return;
            }

            if (!bounds.IsValid)
            {
                return;
            }

            Transform spawnTransform = spawnRenderer.transform;
            Vector2 topEdgePosition = bounds.Local.Lerp(new Vector2(_spawnNormalizedX, 1.0f));

            // The sprite bounds are measured from the pivot, so removing them from the edge position
            // puts the bottom center of the sprite on the edge. A (0.5, 0.0) pivot removes nothing.
            Vector3 spawnScale = spawnTransform.localScale;
            spawnTransform.localPosition = new Vector3(
                topEdgePosition.x - sprite.bounds.center.x * spawnScale.x,
                topEdgePosition.y - sprite.bounds.min.y * spawnScale.y,
                spawnTransform.localPosition.z);

            if (_boardRenderer != null)
            {
                spawnRenderer.sortingOrder = _boardRenderer.sortingOrder + SpawnSortingOffset;
            }

            spawnRenderer.gameObject.SetActive(true);
        }
    }
}
