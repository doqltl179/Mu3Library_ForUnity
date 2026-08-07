using UnityEngine;

namespace Mu3Library.Game.WatermelonGame.Board.Item
{
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class BoardItem : MonoBehaviour
    {
        private SpriteRenderer m_renderer;
        protected SpriteRenderer _renderer => m_renderer ??= GetComponent<SpriteRenderer>();

        private CircleCollider2D m_collider;
        protected CircleCollider2D _collider => m_collider ??= GetComponent<CircleCollider2D>();
        public CircleCollider2D Collider => _collider;

        private Rigidbody2D m_rb;
        protected Rigidbody2D _rb => m_rb ??= GetComponent<Rigidbody2D>();

        public RigidbodyType2D BodyType
        {
            get => _rb.bodyType;
            set
            {
                _rb.bodyType = value;

                if (value == RigidbodyType2D.Dynamic)
                {
                    _rb.WakeUp();
                }
            }
        }

        public bool ColliderEnabled
        {
            get => _collider.enabled;
            set => _collider.enabled = value;
        }

        /// <summary>
        /// The collider radius measured in the parent(board) local space.
        /// </summary>
        public float BoardLocalRadius
        {
            get
            {
                Vector3 scale = transform.localScale;
                return _collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            }
        }

        /// <summary>
        /// The local Y of the item's top edge, in the parent(board) local space.
        /// </summary>
        public float BoardLocalTopY => GetBoardLocalTopY(transform.localPosition.y);

        protected BoardItemInfo _info;
        public BoardItemInfo Info => _info;

        protected int _index = -1;
        public int Index => _index;

        protected bool _isMerging = false;
        public bool IsMerging => _isMerging;

        /// <summary>
        /// True from the moment the item is placed on the board until it first hits something.
        /// </summary>
        protected bool _isFalling = false;
        public bool IsFalling => _isFalling;

        private static readonly Collider2D[] ContactBuffer = new Collider2D[16];



        protected virtual void OnDisable()
        {
            _rb.bodyType = RigidbodyType2D.Kinematic;

            // A pooled item is always dropped again before it can be judged.
            _isFalling = true;
        }

        protected virtual void OnCollisionEnter2D(Collision2D collision)
        {
            _isFalling = false;
        }

        #region Utility
        public virtual bool CanMerge(BoardItem hitItem)
        {
            if (this == null ||
                this == hitItem ||
                hitItem == null ||
                _index != hitItem.Index ||
                _info == null ||
                hitItem.Info == null ||
                !IsTouching(hitItem) ||
                _isMerging ||
                hitItem.IsMerging)
            {
                return false;
            }

            return true;
        }

        public bool IsTouching(BoardItem hitItem)
        {
            if (this == null || hitItem == null)
            {
                return false;
            }

            return _collider.IsTouching(hitItem.Collider);
        }

        public void SetMergeState(bool value) => _isMerging = value;

        /// <summary>
        /// Marks the item as falling. Call it whenever the item is placed on the board,
        /// because every item is placed above the board line and has to drop first.
        /// </summary>
        public void SetFallState(bool value) => _isFalling = value;

        /// <summary>
        /// <br/> Returns true when this item is stacked above the board line.
        /// <br/> An item counts as stacked only while it rests on <paramref name="floorCollider"/>
        /// <br/> or on other board items. The side walls are never a support, they cannot hold an item up.
        /// <br/> Every item is placed above the board line, so an item that has not landed yet
        /// <br/> is excluded through <see cref="IsFalling"/>.
        /// </summary>
        /// <param name="boardLocalOutLineY">The board line position in the board local space.</param>
        /// <param name="floorCollider">The board floor items can rest on.</param>
        internal bool IsStackedOverLine(float boardLocalOutLineY, Collider2D floorCollider)
        {
            // A held item is kinematic and a merging item is about to disappear.
            if (_isFalling || _isMerging || BodyType != RigidbodyType2D.Dynamic || !_collider.enabled)
            {
                return false;
            }

            if (BoardLocalTopY <= boardLocalOutLineY)
            {
                return false;
            }

            return IsSupported(floorCollider);
        }

        public virtual void Init(int index, BoardItemInfo info)
        {
            _index = index;
            _isMerging = false;

            if (info == null)
            {
                _info = null;
                return;
            }

            _renderer.sprite = info.Sprite;

            if (info.Sprite != null)
            {
                Vector2 spriteSize = info.Sprite.bounds.size;
                _collider.radius = Mathf.Max(spriteSize.x, spriteSize.y) * 0.5f;
            }

            _info = info;
        }

        public virtual void Init(BoardItemInfo info) => Init(-1, info);
        #endregion

        private float GetBoardLocalTopY(float localY)
        {
            Vector3 scale = transform.localScale;
            return localY + _collider.offset.y * scale.y + BoardLocalRadius;
        }

        /// <summary>
        /// True while the item touches the board floor or another board item.
        /// </summary>
        private bool IsSupported(Collider2D floorCollider)
        {
            int contactCount = _rb.GetContacts(ContactBuffer);

            for (int i = 0; i < contactCount; i++)
            {
                Collider2D contact = ContactBuffer[i];
                if (contact == null)
                {
                    continue;
                }

                if (floorCollider != null && contact == floorCollider)
                {
                    return true;
                }

                // The side walls are skipped here, only other items can stack this one up.
                if (contact.TryGetComponent(out BoardItem _))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
