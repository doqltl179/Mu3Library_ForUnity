using System;
using Mu3Library.Attribute;
using Mu3Library.Game.WatermelonGame.Helpers;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    [RequireComponent(typeof(InputHandler))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardArea : MonoBehaviour
    {
        /// <summary>
        /// Left, Right, Top, Bottom
        /// </summary>
        [Tooltip("x: Left\ny: Right\nz: Top\nw: Bottom")]
        [SerializeField] private Vector4 _viewportPadding = new Vector4(0.1f, 0.1f, 0.1f, 0.1f);
        /// <summary>
        /// Left, Right, Top, Bottom
        /// </summary>
        [Tooltip("x: Left\ny: Right\nz: Top\nw: Bottom")]
        [SerializeField] private Vector4 _itemAreaViewportPadding = new Vector4(0.04f, 0.04f, 0.0f, 0.04f);
        [SerializeField] private Vector2 _pivot = new Vector2(0.5f, 0.5f);

        protected InputHandler m_inputHandler;
        protected InputHandler _inputHandler => m_inputHandler ??= GetComponent<InputHandler>();

        protected SpriteRenderer m_renderer;
        protected SpriteRenderer _renderer => m_renderer ??= GetComponent<SpriteRenderer>();

        private float _aspectRatio => _renderer != null && _renderer.sprite != null ?
            _renderer.sprite.rect.size.x / _renderer.sprite.rect.size.y :
            1.0f;
        public float AspectRatio => _aspectRatio;

        private bool _isDragging = false;
        public bool IsDragging => _isDragging;

        private CoordinateBounds _localBounds;
        private CoordinateBounds _screenBounds;
        private CoordinateBounds _worldBounds;

        private bool _hasCalculatedPositions;

        private const float OutColliderThickness = 10.0f;
        private const float OutColliderHeight = 100.0f;
        private const string LeftOutColliderName = "OutCollider_Left";
        private const string RightOutColliderName = "OutCollider_Right";
        private const string BottomOutColliderName = "OutCollider_Bottom";

        private Camera _lastCalculationCamera;
        private Camera _calculationCamera =>
            _lastCalculationCamera != null ? _lastCalculationCamera : Camera.main;

        public Vector4 ViewportPadding
        {
            get => _viewportPadding;
            set => _viewportPadding = Clamp01(value);
        }

        public Vector2 Pivot
        {
            get => _pivot;
            set => _pivot = Clamp01(value);
        }

        public Vector2 LocalLB => _localBounds.Min;
        public Vector2 LocalRT => _localBounds.Max;
        public Vector2 ScreenLB => _screenBounds.Min;
        public Vector2 ScreenRT => _screenBounds.Max;
        public Vector2 WorldLB => _worldBounds.Min;
        public Vector2 WorldRT => _worldBounds.Max;

        public Vector2 LocalSize => _localBounds.Size;
        public Vector2 ScreenSize => _screenBounds.Size;
        public Vector2 WorldSize => _worldBounds.Size;

        public Vector2 LocalCenter => _localBounds.Center;
        public Vector2 ScreenCenter => _screenBounds.Center;
        public Vector2 WorldCenter => _worldBounds.Center;

        /// <summary>
        /// Gets the calculated local XY rectangle.
        /// </summary>
        public Rect LocalRect => _localBounds.Rect;

        /// <summary>
        /// Gets the calculated screen rectangle in pixels.
        /// </summary>
        public Rect ScreenRect => _screenBounds.Rect;

        /// <summary>
        /// Gets the calculated world XY rectangle.
        /// </summary>
        public Rect WorldRect => _worldBounds.Rect;

        public Action<Vector2> OnTouchBegan;
        public Action<Vector2> OnTouchMoved;
        public Action<Vector2> OnTouchEnded;



#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_hasCalculatedPositions)
            {
                return;
            }

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector2(_worldBounds.Min.x, _worldBounds.Min.y), new Vector2(_worldBounds.Min.x, _worldBounds.Max.y));
            Gizmos.DrawLine(new Vector2(_worldBounds.Min.x, _worldBounds.Max.y), new Vector2(_worldBounds.Max.x, _worldBounds.Max.y));
            Gizmos.DrawLine(new Vector2(_worldBounds.Max.x, _worldBounds.Max.y), new Vector2(_worldBounds.Max.x, _worldBounds.Min.y));
            Gizmos.DrawLine(new Vector2(_worldBounds.Max.x, _worldBounds.Min.y), new Vector2(_worldBounds.Min.x, _worldBounds.Min.y));
        }
#endif


        protected virtual void OnEnable()
        {
            if (Camera.main != null)
            {
                CalculateBounds();
            }

            _inputHandler.OnTouchBegan.AddListener(OnTouchBeganEvent);
            _inputHandler.OnTouchMoved.AddListener(OnTouchMovedEvent);
            _inputHandler.OnTouchEnded.AddListener(OnTouchEndedEvent);
        }

        protected virtual void OnDisable()
        {
            _inputHandler.OnTouchBegan.RemoveListener(OnTouchBeganEvent);
            _inputHandler.OnTouchMoved.RemoveListener(OnTouchMovedEvent);
            _inputHandler.OnTouchEnded.RemoveListener(OnTouchEndedEvent);
        }

        private void OnTouchBeganEvent(Vector2 screenPos)
        {
            if (_isDragging || !IsInScreenArea(screenPos))
            {
                return;
            }

            _isDragging = true;

            OnTouchBegan?.Invoke(screenPos);
        }

        private void OnTouchMovedEvent(Vector2 screenPos)
        {
            if (!_isDragging || !IsInScreenArea(screenPos))
            {
                return;
            }

            OnTouchMoved?.Invoke(screenPos);
        }

        private void OnTouchEndedEvent(Vector2 screenPos)
        {
            if (!_isDragging)
            {
                return;
            }

            _isDragging = false;

            OnTouchEnded?.Invoke(screenPos);
        }

        #region Utility
        [ButtonInvoke("Fit Board", ButtonHeight = 30f)]
        public void Fit()
        {
            SpriteRenderer renderer = _renderer;
            if (renderer == null)
            {
                return;
            }

            Fit(Camera.main, renderer.sprite);
        }

        public void Fit(Sprite sprite)
            => Fit(Camera.main, sprite);

        public void Fit(Camera cam, Sprite sprite)
        {
            SpriteRenderer renderer = _renderer;
            if (renderer == null || !IsCameraReady(cam) || sprite == null)
            {
                return;
            }

            renderer.sprite = sprite;

            Vector2 spriteSize = sprite.bounds.size;
            if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            {
                return;
            }

            Vector4 padding = Clamp01(_viewportPadding);
            float left = padding.x;
            float right = 1f - padding.y;
            float bottom = padding.w;
            float top = 1f - padding.z;
            if (right - left <= Mathf.Epsilon || top - bottom <= Mathf.Epsilon)
            {
                return;
            }

            float distance = Vector3.Dot(transform.position - cam.transform.position, cam.transform.forward);
            if (distance < cam.nearClipPlane || distance > cam.farClipPlane)
            {
                return;
            }

            Vector3 lowerLeft = cam.ViewportToWorldPoint(new Vector3(left, bottom, distance));
            Vector3 lowerRight = cam.ViewportToWorldPoint(new Vector3(right, bottom, distance));
            Vector3 upperLeft = cam.ViewportToWorldPoint(new Vector3(left, top, distance));
            float viewportWidth = Vector3.Distance(lowerLeft, lowerRight);
            float viewportHeight = Vector3.Distance(lowerLeft, upperLeft);
            float fitScale = Mathf.Min(viewportWidth / spriteSize.x, viewportHeight / spriteSize.y);

            Vector3 localScale = transform.localScale;
            transform.localScale = new Vector3(fitScale, fitScale, localScale.z);

            Vector3 viewportCenter = cam.ViewportToWorldPoint(new Vector3(
                (left + right) * 0.5f,
                (bottom + top) * 0.5f,
                distance));
            transform.SetPositionAndRotation(viewportCenter, cam.transform.rotation);
            transform.position += viewportCenter - renderer.bounds.center;

            CalculateBounds(cam);
        }

        public void SetBoardImage(Sprite sprite)
        {
            if (sprite == null)
            {
                return;
            }

            _renderer.sprite = sprite;
            Fit();
        }

        public void SetOutColliders()
        {
            if (!_hasCalculatedPositions)
            {
                CalculateBounds();
            }

            if (!_hasCalculatedPositions)
            {
                return;
            }

            Vector4 padding = Clamp01(_itemAreaViewportPadding);
            Vector2 itemAreaMin = _localBounds.Lerp(new Vector2(padding.x, padding.w));
            Vector2 itemAreaMax = _localBounds.Lerp(new Vector2(1.0f - padding.y, 1.0f - padding.z));
            if (itemAreaMax.x - itemAreaMin.x <= Mathf.Epsilon || itemAreaMax.y - itemAreaMin.y <= Mathf.Epsilon)
            {
                return;
            }

            Vector2 itemAreaCenter = (itemAreaMin + itemAreaMax) * 0.5f;
            SetOutCollider(
                GetOrCreateOutCollider(LeftOutColliderName),
                new Vector2(itemAreaMin.x, itemAreaCenter.y),
                new Vector2(OutColliderThickness, OutColliderHeight),
                new Vector2(1.0f, 0.5f));
            SetOutCollider(
                GetOrCreateOutCollider(RightOutColliderName),
                new Vector2(itemAreaMax.x, itemAreaCenter.y),
                new Vector2(OutColliderThickness, OutColliderHeight),
                new Vector2(0.0f, 0.5f));
            SetOutCollider(
                GetOrCreateOutCollider(BottomOutColliderName),
                new Vector2(itemAreaCenter.x, itemAreaMin.y),
                new Vector2(itemAreaMax.x - itemAreaMin.x, OutColliderThickness),
                new Vector2(0.5f, 1.0f));
        }

        public Vector3 BoardLocalNormalizedPositionToWorld(Vector2 position)
            => TryGetLocalPosition(position, out Vector3 local) ? LocalToWorld(local) : default;

        public Vector3 BoardLocalNormalizedPositionToScreen(Vector2 position)
        {
            if (!TryGetLocalPosition(position, out Vector3 local))
            {
                return default;
            }

            Camera camera = _calculationCamera;
            return camera != null
                ? camera.WorldToScreenPoint(LocalToWorld(local))
                : BoardScreenNormalizedPositionToScreen(position);
        }

        public Vector3 BoardLocalNormalizedPositionToLocal(Vector2 position)
            => TryGetLocalPosition(position, out Vector3 local) ? local : default;

        public Vector3 BoardWorldNormalizedPositionToWorld(Vector2 position)
            => BoardLocalNormalizedPositionToWorld(position);

        public Vector3 BoardWorldNormalizedPositionToScreen(Vector2 position)
            => BoardLocalNormalizedPositionToScreen(position);

        public Vector3 BoardWorldNormalizedPositionToLocal(Vector2 position)
            => BoardLocalNormalizedPositionToLocal(position);

        public Vector3 BoardScreenNormalizedPositionToWorld(Vector2 position)
            => TryGetBoardScreenNormalizedWorldPosition(position, out Vector3 world) ? world : default;

        public Vector3 BoardScreenNormalizedPositionToScreen(Vector2 position)
        {
            if (!_hasCalculatedPositions)
            {
                return default;
            }

            Vector2 screenPosition = _screenBounds.Lerp(position);
            Camera camera = _calculationCamera;
            float depth = camera == null ? 0.0f : camera.WorldToScreenPoint(transform.position).z;
            return new Vector3(screenPosition.x, screenPosition.y, depth);
        }

        public Vector3 BoardScreenNormalizedPositionToLocal(Vector2 position)
            => TryGetBoardScreenNormalizedWorldPosition(position, out Vector3 world) ? WorldToLocal(world) : default;

        public Vector2 WorldToBoardWorldNormalizedPosition(Vector3 worldPos)
            => TryGetBoardWorldNormalizedPosition(WorldToLocal(worldPos), out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a world position to normalized board world coordinates when the board has been calculated.
        /// </summary>
        public bool TryWorldToBoardWorldNormalizedPosition(Vector3 worldPos, out Vector2 boardNormalizedPosition)
            => TryGetBoardWorldNormalizedPosition(WorldToLocal(worldPos), out boardNormalizedPosition);

        public Vector2 ScreenToBoardWorldNormalizedPosition(Vector3 screenPos)
            => TryScreenToBoardWorldNormalizedPosition(screenPos, out Vector2 normalized) ? normalized : default;

        public Vector2 LocalToBoardWorldNormalizedPosition(Vector3 localPos)
            => TryGetBoardWorldNormalizedPosition(localPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a world position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public Vector2 WorldToBoardLocalNormalizedPosition(Vector3 worldPos)
            => TryWorldToBoardLocalNormalizedPosition(worldPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a world position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public bool TryWorldToBoardLocalNormalizedPosition(Vector3 worldPos, out Vector2 boardNormalizedPosition)
            => TryGetBoardWorldNormalizedPosition(WorldToLocal(worldPos), out boardNormalizedPosition);

        /// <summary>
        /// Converts a screen position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public Vector2 ScreenToBoardLocalNormalizedPosition(Vector3 screenPos)
            => TryScreenToBoardLocalNormalizedPosition(screenPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a screen position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public bool TryScreenToBoardLocalNormalizedPosition(Vector3 screenPos, out Vector2 boardNormalizedPosition)
            => TryGetBoardWorldNormalizedPositionFromScreen(screenPos, out boardNormalizedPosition);

        /// <summary>
        /// Converts a local position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public Vector2 LocalToBoardLocalNormalizedPosition(Vector3 localPos)
            => TryLocalToBoardLocalNormalizedPosition(localPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a local position to normalized board local coordinates when the board has been calculated.
        /// </summary>
        public bool TryLocalToBoardLocalNormalizedPosition(Vector3 localPos, out Vector2 boardNormalizedPosition)
            => TryGetBoardWorldNormalizedPosition(localPos, out boardNormalizedPosition);

        /// <summary>
        /// Converts a local position to normalized board world coordinates when the board has been calculated.
        /// </summary>
        public bool TryLocalToBoardWorldNormalizedPosition(Vector3 localPos, out Vector2 boardNormalizedPosition)
            => TryLocalToBoardLocalNormalizedPosition(localPos, out boardNormalizedPosition);

        public Vector2 WorldToBoardScreenNormalizedPosition(Vector3 worldPos)
        {
            return TryWorldToBoardScreenNormalizedPosition(worldPos, out Vector2 normalized)
                ? normalized : default;
        }

        /// <summary>
        /// Converts a world position to normalized board screen coordinates when the board has been calculated.
        /// </summary>
        public bool TryWorldToBoardScreenNormalizedPosition(Vector3 worldPos, out Vector2 boardNormalizedPosition)
        {
            Camera camera = _calculationCamera;
            if (camera == null)
            {
                boardNormalizedPosition = default;
                return false;
            }

            return TryGetBoardScreenNormalizedPosition(camera.WorldToScreenPoint(worldPos), out boardNormalizedPosition);
        }

        public Vector2 ScreenToBoardScreenNormalizedPosition(Vector3 screenPos)
            => TryGetBoardScreenNormalizedPosition(screenPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a screen position to normalized board screen coordinates when the board has been calculated.
        /// </summary>
        public bool TryScreenToBoardScreenNormalizedPosition(Vector3 screenPos, out Vector2 boardNormalizedPosition)
            => TryGetBoardScreenNormalizedPosition(screenPos, out boardNormalizedPosition);

        /// <summary>
        /// Converts a screen position to normalized board world coordinates when the board has been calculated.
        /// </summary>
        public bool TryScreenToBoardWorldNormalizedPosition(Vector3 screenPos, out Vector2 boardNormalizedPosition)
            => TryGetBoardWorldNormalizedPositionFromScreen(screenPos, out boardNormalizedPosition);

        public Vector2 LocalToBoardScreenNormalizedPosition(Vector3 localPos)
            => TryLocalToBoardScreenNormalizedPosition(localPos, out Vector2 normalized) ? normalized : default;

        /// <summary>
        /// Converts a local position to normalized board screen coordinates when the board has been calculated.
        /// </summary>
        public bool TryLocalToBoardScreenNormalizedPosition(Vector3 localPos, out Vector2 boardNormalizedPosition)
            => TryWorldToBoardScreenNormalizedPosition(LocalToWorld(localPos), out boardNormalizedPosition);

        /// <summary>
        /// Converts a screen position to a point on the board plane using the specified camera.
        /// </summary>
        public bool TryScreenToWorld(Camera cam, Vector2 screenPosition, out Vector3 worldPosition)
            => TryScreenToWorldOnBoardPlane(cam, screenPosition, out worldPosition);

        public bool TryScreenToWorld(Vector3 screenPos, out Vector3 worldPos)
        {
            Camera camera = _calculationCamera;
            if (camera == null)
            {
                worldPos = default;
                return false;
            }

            return TryScreenToWorld(camera, new Vector2(screenPos.x, screenPos.y), out worldPos);
        }

        public Vector3 WorldToLocal(Vector3 worldPos)
            => transform.InverseTransformPoint(worldPos);

        public Vector3 LocalToWorld(Vector3 localPos)
            => transform.TransformPoint(localPos);

        public bool IsInWorldArea(Vector3 worldPos)
            => IsInWorldArea(worldPos, false);

        public bool IsInWorldArea(Vector3 worldPos, bool includeBoundary)
            => IsInLocalArea(WorldToLocal(worldPos), includeBoundary);

        public bool IsInLocalArea(Vector3 localPos)
            => IsInLocalArea(localPos, false);

        public bool IsInLocalArea(Vector3 localPos, bool includeBoundary)
            => IsInBounds(_localBounds, localPos, includeBoundary);

        public bool IsInScreenArea(Vector3 screenPos)
            => IsInScreenArea(screenPos, false);

        public bool IsInScreenArea(Vector3 screenPos, bool includeBoundary)
            => IsInBounds(_screenBounds, screenPos, includeBoundary);

        /// <summary>
        /// Clamps normalized board coordinates to the board area.
        /// </summary>
        public Vector2 ClampBoardNormalizedPosition(Vector2 boardNormalizedPosition)
            => Clamp01(boardNormalizedPosition);

        /// <summary>
        /// Clamps a local position to the calculated board rectangle while preserving its local Z value.
        /// </summary>
        public Vector3 ClampLocalPosition(Vector3 localPos)
            => _localBounds.Clamp(localPos);

        /// <summary>
        /// Clamps a world position to the calculated board rectangle while preserving its distance from the board plane.
        /// </summary>
        public Vector3 ClampWorldPosition(Vector3 worldPos)
            => LocalToWorld(ClampLocalPosition(WorldToLocal(worldPos)));

        /// <summary>
        /// Clamps a screen position to the calculated board rectangle while preserving its screen Z value.
        /// </summary>
        public Vector3 ClampScreenPosition(Vector3 screenPos)
            => _screenBounds.Clamp(screenPos);

        public void Refresh()
            => CalculateBounds();

        public void CalculateBounds()
            => CalculateBounds(Camera.main);

        public void CalculateBounds(Camera cam)
            => CalculateBounds(cam, _pivot, _aspectRatio);

        public void CalculateBounds(Camera cam, Vector2 pivot)
            => CalculateBounds(cam, pivot, _aspectRatio);

        public void CalculateBounds(Camera cam, float aspectRatio)
            => CalculateBounds(cam, _pivot, aspectRatio);

        public void CalculateBounds(Camera cam, Vector2 pivot, float aspectRatio)
        {
            _lastCalculationCamera = cam;
            _hasCalculatedPositions = false;
            ClearCalculatedBounds();

            if (!TryGetBoardScreenRect(cam, pivot, aspectRatio, out Rect boardRect))
            {
                return;
            }

            if (!TryGetBoardWorldBounds(cam, boardRect, out CoordinateBounds worldBounds))
            {
                Debug.LogWarning("Cannot calculate board screen positions because the board plane cannot be intersected mathematically from the camera.", this);
                return;
            }

            _screenBounds = new CoordinateBounds(boardRect.min, boardRect.max);
            _worldBounds = worldBounds;

            Vector3 localMin = WorldToLocal(ToVector3(worldBounds.Min));
            Vector3 localMax = WorldToLocal(ToVector3(worldBounds.Max));
            _localBounds = new CoordinateBounds(new Vector2(localMin.x, localMin.y), new Vector2(localMax.x, localMax.y));

            _hasCalculatedPositions = true;
            SetOutColliders();
        }
        #endregion

        private BoxCollider2D GetOrCreateOutCollider(string colliderName)
        {
            Transform colliderTransform = transform.Find(colliderName);
            if (colliderTransform == null)
            {
                GameObject colliderObject = new GameObject(colliderName);
                colliderObject.layer = gameObject.layer;
                colliderTransform = colliderObject.transform;
                colliderTransform.SetParent(transform, false);
            }

            BoxCollider2D collider = colliderTransform.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = colliderTransform.gameObject.AddComponent<BoxCollider2D>();
            }

            return collider;
        }

        private static void SetOutCollider(BoxCollider2D collider, Vector2 pivotPosition, Vector2 size, Vector2 pivot)
        {
            Transform colliderTransform = collider.transform;
            Vector2 centerPosition = pivotPosition + (new Vector2(0.5f, 0.5f) - pivot) * size;
            colliderTransform.localPosition = ToVector3(centerPosition);
            colliderTransform.localRotation = Quaternion.identity;
            colliderTransform.localScale = Vector3.one;

            collider.offset = Vector2.zero;
            collider.size = size;
            collider.enabled = true;
        }

        private static bool IsCameraReady(Camera cam)
        {
            return cam != null
                && cam.isActiveAndEnabled
                && cam.rect.width > 0f
                && cam.rect.height > 0f
                && cam.pixelWidth > 0
                && cam.pixelHeight > 0;
        }

        private bool TryGetLocalPosition(Vector2 normalizedPosition, out Vector3 localPosition)
        {
            if (!_hasCalculatedPositions)
            {
                localPosition = default;
                return false;
            }

            localPosition = ToVector3(_localBounds.Lerp(normalizedPosition));
            return true;
        }

        private bool TryGetBoardWorldNormalizedPosition(Vector3 localPosition, out Vector2 normalizedPosition)
            => TryGetBoardNormalizedPosition(_localBounds, ToVector2(localPosition), out normalizedPosition);

        private bool TryGetBoardScreenNormalizedPosition(Vector3 screenPosition, out Vector2 normalizedPosition)
            => TryGetBoardNormalizedPosition(_screenBounds, ToVector2(screenPosition), out normalizedPosition);

        private bool TryGetBoardNormalizedPosition(CoordinateBounds bounds, Vector2 position, out Vector2 normalizedPosition)
        {
            if (!_hasCalculatedPositions)
            {
                normalizedPosition = default;
                return false;
            }

            normalizedPosition = bounds.Normalize(position);
            return true;
        }

        private bool TryGetBoardWorldNormalizedPositionFromScreen(Vector3 screenPosition, out Vector2 normalizedPosition)
        {
            if (!TryScreenToWorld(screenPosition, out Vector3 worldPosition))
            {
                normalizedPosition = default;
                return false;
            }

            return TryGetBoardWorldNormalizedPosition(WorldToLocal(worldPosition), out normalizedPosition);
        }

        private bool TryGetBoardScreenNormalizedWorldPosition(Vector2 normalizedPosition, out Vector3 worldPosition)
        {
            Camera camera = _calculationCamera;
            if (!_hasCalculatedPositions || camera == null)
            {
                worldPosition = default;
                return false;
            }

            return TryScreenToWorldOnBoardPlane(camera, ToVector2(BoardScreenNormalizedPositionToScreen(normalizedPosition)), out worldPosition);
        }

        private bool TryScreenToWorldOnBoardPlane(Camera cam, Vector2 screenPosition, out Vector3 worldPosition)
        {
            if (cam == null)
            {
                worldPosition = default;
                return false;
            }

            Ray ray = cam.ScreenPointToRay(screenPosition);
            Vector3 planeNormal = transform.forward;
            float denominator = Vector3.Dot(planeNormal, ray.direction);
            if (Mathf.Abs(denominator) <= Mathf.Epsilon)
            {
                worldPosition = default;
                return false;
            }

            float distance = Vector3.Dot(planeNormal, transform.position - ray.origin) / denominator;
            if (distance < 0.0f)
            {
                worldPosition = default;
                return false;
            }

            worldPosition = ray.GetPoint(distance);
            return true;
        }

        private bool TryGetBoardScreenRect(Camera cam, Vector2 pivot, float aspectRatio, out Rect boardRect)
        {
            boardRect = default;
            if (cam == null)
            {
                Debug.LogWarning("Cannot calculate board screen positions without a camera.", this);
                return false;
            }

            Rect pixelRect = cam.pixelRect;
            if (pixelRect.width <= Mathf.Epsilon || pixelRect.height <= Mathf.Epsilon)
            {
                Debug.LogWarning("Cannot calculate board screen positions from an empty camera pixel rect.", this);
                return false;
            }

            Vector4 padding = Clamp01(_viewportPadding);
            Rect viewportRect = Rect.MinMaxRect(
                pixelRect.xMin + pixelRect.width * padding.x,
                pixelRect.yMin + pixelRect.height * padding.w,
                pixelRect.xMax - pixelRect.width * padding.y,
                pixelRect.yMax - pixelRect.height * padding.z);
            if (viewportRect.width <= Mathf.Epsilon || viewportRect.height <= Mathf.Epsilon)
            {
                Debug.LogWarning("Cannot calculate board screen positions because viewport padding leaves no area.", this);
                return false;
            }

            float boardWidth = viewportRect.width;
            float boardHeight = viewportRect.height;
            if (boardWidth / boardHeight > _aspectRatio)
            {
                boardWidth = boardHeight * _aspectRatio;
            }
            else
            {
                boardHeight = boardWidth / _aspectRatio;
            }
            Vector2 clampedPivot = Clamp01(pivot);
            float boardLeft = viewportRect.xMin + (viewportRect.width - boardWidth) * clampedPivot.x;
            float boardBottom = viewportRect.yMin + (viewportRect.height - boardHeight) * clampedPivot.y;
            boardRect = new Rect(boardLeft, boardBottom, boardWidth, boardHeight);
            return true;
        }

        private bool TryGetBoardWorldBounds(Camera cam, Rect screenRect, out CoordinateBounds worldBounds)
        {
            Vector2 bottomLeft = new Vector2(screenRect.xMin, screenRect.yMin);
            Vector2 topLeft = new Vector2(screenRect.xMin, screenRect.yMax);
            Vector2 bottomRight = new Vector2(screenRect.xMax, screenRect.yMin);
            Vector2 topRight = new Vector2(screenRect.xMax, screenRect.yMax);

            if (!TryScreenToWorldOnBoardPlane(cam, bottomLeft, out Vector3 worldBottomLeft) ||
                !TryScreenToWorldOnBoardPlane(cam, topLeft, out Vector3 worldTopLeft) ||
                !TryScreenToWorldOnBoardPlane(cam, bottomRight, out Vector3 worldBottomRight) ||
                !TryScreenToWorldOnBoardPlane(cam, topRight, out Vector3 worldTopRight))
            {
                worldBounds = default;
                return false;
            }

            Vector2 worldMin = new Vector2(
                Mathf.Min(Mathf.Min(worldBottomLeft.x, worldTopLeft.x), Mathf.Min(worldBottomRight.x, worldTopRight.x)),
                Mathf.Min(Mathf.Min(worldBottomLeft.y, worldTopLeft.y), Mathf.Min(worldBottomRight.y, worldTopRight.y)));
            Vector2 worldMax = new Vector2(
                Mathf.Max(Mathf.Max(worldBottomLeft.x, worldTopLeft.x), Mathf.Max(worldBottomRight.x, worldTopRight.x)),
                Mathf.Max(Mathf.Max(worldBottomLeft.y, worldTopLeft.y), Mathf.Max(worldBottomRight.y, worldTopRight.y)));

            worldBounds = new CoordinateBounds(
                worldMin,
                worldMax);
            return true;
        }

        private void ClearCalculatedBounds()
        {
            _localBounds = default;
            _screenBounds = default;
            _worldBounds = default;
        }

        private static bool IsInBounds(
            CoordinateBounds bounds,
            Vector3 position,
            bool includeBoundary)
            => bounds.Contains(new Vector2(position.x, position.y), includeBoundary);

        private void OnValidate()
        {
            _viewportPadding = Clamp01(_viewportPadding);
            _itemAreaViewportPadding = Clamp01(_itemAreaViewportPadding);
            _pivot = Clamp01(_pivot);
        }

        private static Vector2 Clamp01(Vector2 value)
            => new Vector2(Mathf.Clamp01(value.x), Mathf.Clamp01(value.y));

        private static Vector4 Clamp01(Vector4 value)
            => new Vector4(
                Mathf.Clamp01(value.x),
                Mathf.Clamp01(value.y),
                Mathf.Clamp01(value.z),
                Mathf.Clamp01(value.w));

        private static Vector3 ToVector3(Vector2 value)
            => new Vector3(value.x, value.y, 0.0f);

        private static Vector2 ToVector2(Vector3 value)
            => new Vector2(value.x, value.y);

        private struct CoordinateBounds
        {
            public CoordinateBounds(Vector2 min, Vector2 max)
            {
                Min = min;
                Max = max;
            }

            public Vector2 Min;
            public Vector2 Max;
            public Vector2 Size => Max - Min;
            public Vector2 Center => (Min + Max) * 0.5f;
            public Rect Rect => Rect.MinMaxRect(Min.x, Min.y, Max.x, Max.y);

            public Vector2 Lerp(Vector2 normalizedPosition)
                => new Vector2(Mathf.Lerp(Min.x, Max.x, normalizedPosition.x), Mathf.Lerp(Min.y, Max.y, normalizedPosition.y));

            public Vector2 Normalize(Vector2 position)
                => new Vector2(SafeInverseLerp(Min.x, Max.x, position.x), SafeInverseLerp(Min.y, Max.y, position.y));

            public Vector3 Clamp(Vector3 position)
                => new Vector3(Mathf.Clamp(position.x, Min.x, Max.x), Mathf.Clamp(position.y, Min.y, Max.y), position.z);

            public bool Contains(Vector2 position, bool includeBoundary)
                => includeBoundary
                    ? Min.x <= position.x && position.x <= Max.x && Min.y <= position.y && position.y <= Max.y
                    : Min.x < position.x && position.x < Max.x && Min.y < position.y && position.y < Max.y;

            private static float SafeInverseLerp(float min, float max, float value)
                => Mathf.Abs(max - min) <= Mathf.Epsilon ? 0.0f : Mathf.InverseLerp(min, max, value);
        }
    }
}
