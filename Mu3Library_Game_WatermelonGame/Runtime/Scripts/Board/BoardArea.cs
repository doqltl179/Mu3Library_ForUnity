using System;
using Mu3Library.Attribute;
using Mu3Library.Game.WatermelonGame.Board.Area;
using Mu3Library.Game.WatermelonGame.Helpers;
using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board
{
    /// <summary>
    /// The playable board rectangle.
    /// <br/> This component only owns the board settings and hands the work to the parts in
    /// <br/> <see cref="Mu3Library.Game.WatermelonGame.Board.Area"/>:
    /// <br/> <see cref="BoardAreaBoundsCalculator"/> measures the rectangle,
    /// <br/> <see cref="BoardAreaCoordinateConverter"/> converts positions,
    /// <br/> <see cref="BoardAreaView"/> draws the board, the item out line and the spawn marker,
    /// <br/> <see cref="BoardAreaOutColliders"/> keeps the items inside,
    /// <br/> <see cref="BoardAreaInputRelay"/> reports the touches that belong to the board.
    /// </summary>
    [RequireComponent(typeof(InputHandler))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BoardArea : MonoBehaviour
    {
        /// <summary>
        /// Left, Right, Top, Bottom
        /// </summary>
        [Tooltip("x: Left\ny: Right\nz: Top\nw: Bottom")]
        [SerializeField] protected Vector4 _viewportPadding = new Vector4(0.1f, 0.1f, 0.1f, 0.1f);
        /// <summary>
        /// Left, Right, Top, Bottom
        /// </summary>
        [Tooltip("x: Left\ny: Right\nz: Top\nw: Bottom")]
        [SerializeField] protected Vector4 _itemAreaViewportPadding = new Vector4(0.04f, 0.04f, 0.0f, 0.04f);
        [SerializeField] protected Vector2 _pivot = new Vector2(0.5f, 0.5f);

        /// <summary>
        /// 아이템 기준선(표시용)의 위치.
        /// <br/> 게임 오버 판정은 board area 상단(<see cref="LocalRect"/>의 yMax)을 사용하므로 이 값과 무관하다.
        /// </summary>
        [Space(20)]
        [SerializeField, Range(0.0f, 1.0f)] protected float _boardLocalNormalizedItemOutPosY = 0.9f;

        public float BoardLocalNormalizedItemOutPosY
        {
            get => Mathf.Clamp01(_boardLocalNormalizedItemOutPosY);
            set => _boardLocalNormalizedItemOutPosY = Mathf.Clamp01(value);
        }

        protected InputHandler m_inputHandler;
        protected InputHandler _inputHandler => m_inputHandler ??= GetComponent<InputHandler>();

        protected SpriteRenderer m_boardRenderer;
        protected SpriteRenderer _boardRenderer => m_boardRenderer ??= GetComponent<SpriteRenderer>();

        private BoardAreaView m_view;
        protected BoardAreaView _view => m_view ??= new BoardAreaView(transform, _boardRenderer);

        private BoardAreaCoordinateConverter m_converter;
        protected BoardAreaCoordinateConverter _converter => m_converter ??= new BoardAreaCoordinateConverter(transform);

        private BoardAreaOutColliders m_outColliders;
        protected BoardAreaOutColliders _outColliders => m_outColliders ??= new BoardAreaOutColliders(transform);

        private BoardAreaInputRelay m_inputRelay;
        protected BoardAreaInputRelay _inputRelay => m_inputRelay ??= CreateInputRelay();

        /// <summary>
        /// The board rectangle in every coordinate space, invalid until it has been calculated.
        /// </summary>
        protected BoardAreaBounds _bounds => _converter.Bounds;

        public float AspectRatio => _view.AspectRatio;

        public bool IsDragging => _inputRelay.IsDragging;

        /// <summary>
        /// The floor an item can rest on.
        /// <br/> The left and right walls are not exposed on purpose, they cannot hold an item up.
        /// </summary>
        internal Collider2D BottomOutCollider => _outColliders.Bottom;

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

        public Vector2 LocalLB => _bounds.Local.Min;
        public Vector2 LocalRT => _bounds.Local.Max;
        public Vector2 ScreenLB => _bounds.Screen.Min;
        public Vector2 ScreenRT => _bounds.Screen.Max;
        public Vector2 WorldLB => _bounds.World.Min;
        public Vector2 WorldRT => _bounds.World.Max;

        public Vector2 LocalSize => _bounds.Local.Size;
        public Vector2 ScreenSize => _bounds.Screen.Size;
        public Vector2 WorldSize => _bounds.World.Size;

        public Vector2 LocalCenter => _bounds.Local.Center;
        public Vector2 ScreenCenter => _bounds.Screen.Center;
        public Vector2 WorldCenter => _bounds.World.Center;

        /// <summary>
        /// Gets the calculated local XY rectangle.
        /// </summary>
        public Rect LocalRect => _bounds.Local.Rect;

        internal Rect ItemAreaLocalRect => ItemAreaLocalBounds.Rect;

        /// <summary>
        /// Gets the calculated screen rectangle in pixels.
        /// </summary>
        public Rect ScreenRect => _bounds.Screen.Rect;

        /// <summary>
        /// Gets the calculated world XY rectangle.
        /// </summary>
        public Rect WorldRect => _bounds.World.Rect;

        /// <summary>
        /// The area the items are allowed to fall into, the board rectangle shrunk by the item padding.
        /// </summary>
        protected CoordinateBounds ItemAreaLocalBounds => _bounds.GetPaddedLocal(Clamp01(_itemAreaViewportPadding));

        public Action<Vector2> OnTouchBegan;
        public Action<Vector2> OnTouchMoved;
        public Action<Vector2> OnTouchEnded;



#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_bounds.IsValid)
            {
                return;
            }

            CoordinateBounds world = _bounds.World;

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(new Vector2(world.Min.x, world.Min.y), new Vector2(world.Min.x, world.Max.y));
            Gizmos.DrawLine(new Vector2(world.Min.x, world.Max.y), new Vector2(world.Max.x, world.Max.y));
            Gizmos.DrawLine(new Vector2(world.Max.x, world.Max.y), new Vector2(world.Max.x, world.Min.y));
            Gizmos.DrawLine(new Vector2(world.Max.x, world.Min.y), new Vector2(world.Min.x, world.Min.y));

            Gizmos.color = Color.cyan;
            Vector2 lineNormalizedPosition = Vector2.up * _boardLocalNormalizedItemOutPosY;
            Vector3 lineLeft = BoardLocalNormalizedPositionToWorld(lineNormalizedPosition);
            lineNormalizedPosition.x = 1.0f;
            Vector3 lineRight = BoardLocalNormalizedPositionToWorld(lineNormalizedPosition);
            Gizmos.DrawLine(lineLeft, lineRight);
        }
#endif


        protected virtual void OnEnable()
        {
            if (Camera.main != null)
            {
                CalculateBounds();
            }

            _inputRelay.Enable();
        }

        protected virtual void OnDisable()
        {
            _inputRelay.Disable();
        }

        #region Utility
        [ButtonInvoke("Fit Board", ButtonHeight = 30f)]
        public void Fit()
            => Fit(Camera.main, _view.BoardSprite);

        public void Fit(Sprite sprite)
            => Fit(Camera.main, sprite);

        public void Fit(Camera cam, Sprite sprite)
        {
            if (!_view.Fit(cam, sprite, Clamp01(_viewportPadding)))
            {
                return;
            }

            CalculateBounds(cam);
        }

        public void SetBoardImage(Sprite boardSprite)
        {
            if (boardSprite == null)
            {
                return;
            }

            _view.SetBoardSprite(boardSprite);
            Fit();
        }

        public void SetBoardLocalItemOutLine()
            => SetBoardLocalItemOutLine(_view.LineSprite);

        public void SetBoardLocalItemOutLine(Sprite sprite)
        {
            _view.SetItemOutLine(sprite, _bounds, BoardLocalNormalizedItemOutPosY);

            // The line cannot be placed without the board rectangle,
            // and calculating it applies the sprite assigned above.
            if (sprite != null && !_bounds.IsValid)
            {
                CalculateBounds();
            }
        }

        public void SetBoardSpawnImage()
            => SetBoardSpawnImage(_view.SpawnSprite);

        /// <summary>
        /// Assigns the image that hangs on the top edge of the board area and follows the held item.
        /// </summary>
        /// <param name="sprite">The spawn sprite, null hides the marker.</param>
        public void SetBoardSpawnImage(Sprite sprite)
        {
            _view.SetSpawnMarker(sprite, _bounds, _view.SpawnNormalizedX);

            // The marker cannot be placed without the board rectangle,
            // and calculating it applies the sprite assigned above.
            if (sprite != null && !_bounds.IsValid)
            {
                CalculateBounds();
            }
        }

        /// <summary>
        /// Moves the spawn marker along the top edge of the board area.
        /// </summary>
        /// <param name="localX">The marker position in the board local space.</param>
        public void SetSpawnLocalPositionX(float localX)
        {
            // Without the board rectangle the local position cannot be normalized,
            // and a wrong fraction would move the marker once the board is calculated.
            if (!_bounds.IsValid)
            {
                return;
            }

            SetSpawnBoardLocalNormalizedPositionX(_bounds.Local.Normalize(new Vector2(localX, 0.0f)).x);
        }

        /// <summary>
        /// Moves the spawn marker along the top edge of the board area.
        /// </summary>
        /// <param name="normalizedX">The marker position as a fraction of the board width.</param>
        public void SetSpawnBoardLocalNormalizedPositionX(float normalizedX)
            => _view.SetSpawnMarker(_view.SpawnSprite, _bounds, normalizedX);

        public void SetOutColliders()
        {
            if (!_bounds.IsValid)
            {
                CalculateBounds();
                return;
            }

            _outColliders.Rebuild(ItemAreaLocalBounds);
        }

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

        public void Refresh()
            => CalculateBounds();

        public void CalculateBounds()
            => CalculateBounds(Camera.main);

        public void CalculateBounds(Camera cam)
            => CalculateBounds(cam, _pivot, AspectRatio);

        public void CalculateBounds(Camera cam, Vector2 pivot)
            => CalculateBounds(cam, pivot, AspectRatio);

        public void CalculateBounds(Camera cam, float aspectRatio)
            => CalculateBounds(cam, _pivot, aspectRatio);

        public void CalculateBounds(Camera cam, Vector2 pivot, float aspectRatio)
        {
            _converter.SetCamera(cam);
            _converter.ClearBounds();

            if (!BoardAreaBoundsCalculator.TryCalculate(
                    cam,
                    transform,
                    Clamp01(_viewportPadding),
                    Clamp01(pivot),
                    aspectRatio,
                    out BoardAreaBounds bounds))
            {
                return;
            }

            _converter.SetBounds(bounds);

            _outColliders.Rebuild(ItemAreaLocalBounds);
            _view.SetItemOutLine(_view.LineSprite, bounds, BoardLocalNormalizedItemOutPosY);
            _view.SetSpawnMarker(_view.SpawnSprite, bounds, _view.SpawnNormalizedX);
        }
        #endregion

        private BoardAreaInputRelay CreateInputRelay()
        {
            BoardAreaInputRelay relay = new BoardAreaInputRelay(_inputHandler, screenPos => IsInScreenArea(screenPos));

            relay.TouchBegan += screenPos => OnTouchBegan?.Invoke(screenPos);
            relay.TouchMoved += screenPos => OnTouchMoved?.Invoke(screenPos);
            relay.TouchEnded += screenPos => OnTouchEnded?.Invoke(screenPos);

            return relay;
        }

        private void OnValidate()
        {
            _viewportPadding = Clamp01(_viewportPadding);
            _itemAreaViewportPadding = Clamp01(_itemAreaViewportPadding);
            _pivot = Clamp01(_pivot);
            _boardLocalNormalizedItemOutPosY = Mathf.Clamp01(_boardLocalNormalizedItemOutPosY);
        }

        private static Vector2 Clamp01(Vector2 value)
            => BoardAreaGeometry.Clamp01(value);

        private static Vector4 Clamp01(Vector4 value)
            => BoardAreaGeometry.Clamp01(value);
    }
}
