using System;
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
    public partial class BoardArea : MonoBehaviour
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



    }
}
